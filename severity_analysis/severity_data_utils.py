"""
Helpers for the TDSP severity pipeline (image + binary disease mask per leaf).

Current convention (see TDSP_Severity_Model.ipynb):
  - Originals: ``{id}.jpg`` and ``{id}.png`` under data/images and data/masks.
  - Augmented train-only: ``{id}_{k}.jpg`` / ``{id}_{k}.png`` for k in 0..4.
  - "Severity %" in the notebook = (disease pixels) / (full image area) * 100.
    This matches on-device scoring in mobile/src/services/scanService.js (mask vs 224×224).

To add more data for better generalization:
  1. Use unique ``id`` strings (e.g. ``ext_plantdoc_00042``) so names never collide.
  2. Keep masks aligned with images (same stem); mask values 0 = background/healthy, >0 = disease.
  3. If you add external datasets, resize/crop consistently (e.g. leaf-centered 256×256) before
     merging so train statistics stay comparable.
  4. Re-run the split cells in the notebook after adding originals (stratify by severity if you add code).
  5. Optional: generate new aug_* files with the same 5-variant naming, or rely on Albumentations
     in the notebook instead of pre-baked aug folders.

Public sources often used for *segmentation* (verify licenses): Plant pathology segmentation
challenges on Zenodo/Codalab, CVPPP leaf instances (for leaf outline — combine with disease mask),
Kaggle "plant disease segmentation" style releases. Pixel-accurate disease masks are rarer than
classification-only sets; field photos you label yourself (CVAT, LabelMe) usually help most for
deployment realism.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Iterator

import numpy as np

try:
    import cv2
except ImportError as e:  # pragma: no cover
    raise ImportError("severity_data_utils requires opencv-python: pip install opencv-python") from e


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def default_severity_data_dir() -> Path:
    """Default ``archive (2)/data/data`` next to this package (matches notebook BASE_PATH)."""
    return repo_root() / "severity_analysis" / "archive (2)" / "data" / "data"


def default_aug_dir() -> Path:
    return repo_root() / "severity_analysis" / "archive (2)" / "aug_data" / "aug_data"


def list_original_stems(img_dir: Path | str) -> list[str]:
    p = Path(img_dir)
    if not p.is_dir():
        return []
    return sorted(os.path.splitext(f)[0] for f in os.listdir(p) if f.lower().endswith(".jpg"))


def coverage_percent_full_frame(mask_gray: np.ndarray) -> float:
    """Same definition as the notebook: diseased pixels / total pixels * 100."""
    if mask_gray.size == 0:
        return 0.0
    return float((mask_gray > 0).sum()) / float(mask_gray.size) * 100.0


@dataclass
class SplitStats:
    n_images: int
    n_masks: int
    n_paired: int
    n_missing_mask: int
    n_missing_image: int
    severity_pcts: np.ndarray


def profile_folder(
    base_data_dir: Path | str,
    *,
    limit: int | None = None,
) -> SplitStats:
    """
    Profile one ``data/data``-style folder with ``images/`` and ``masks/``.
    """
    base = Path(base_data_dir)
    img_dir = base / "images"
    msk_dir = base / "masks"
    stems = list_original_stems(img_dir)
    if limit is not None:
        stems = stems[:limit]

    pcts: list[float] = []
    missing_m, missing_i = 0, 0
    paired = 0

    for stem in stems:
        ip = img_dir / f"{stem}.jpg"
        mp = msk_dir / f"{stem}.png"
        if not mp.is_file():
            missing_m += 1
            continue
        if not ip.is_file():
            missing_i += 1
            continue
        msk = cv2.imread(str(mp), cv2.IMREAD_GRAYSCALE)
        if msk is None:
            missing_m += 1
            continue
        paired += 1
        pcts.append(coverage_percent_full_frame(msk))

    n_img = len([f for f in os.listdir(img_dir) if f.lower().endswith(".jpg")]) if img_dir.is_dir() else 0
    n_msk = len([f for f in os.listdir(msk_dir) if f.lower().endswith(".png")]) if msk_dir.is_dir() else 0

    return SplitStats(
        n_images=n_img,
        n_masks=n_msk,
        n_paired=paired,
        n_missing_mask=missing_m,
        n_missing_image=missing_i,
        severity_pcts=np.array(pcts, dtype=np.float64),
    )


def iter_extra_data_roots(extra_roots: Iterable[Path | str]) -> Iterator[Path]:
    for r in extra_roots:
        p = Path(r)
        if p.is_dir():
            yield p


def print_profile(title: str, stats: SplitStats) -> None:
    print(f"\n=== {title} ===")
    print(f"  JPG images in images/: {stats.n_images}")
    print(f"  PNG masks in masks/ : {stats.n_masks}")
    print(f"  Paired (readable)   : {stats.n_paired}")
    print(f"  Missing mask        : {stats.n_missing_mask}")
    print(f"  Missing image       : {stats.n_missing_image}")
    if stats.severity_pcts.size:
        s = stats.severity_pcts
        print(f"  Coverage % (disease / full frame):")
        print(f"    mean={s.mean():.2f}  median={np.median(s):.2f}  std={s.std():.2f}")
        print(f"    min={s.min():.2f}  max={s.max():.2f}")
