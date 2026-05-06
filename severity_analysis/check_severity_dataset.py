#!/usr/bin/env python3
"""Profile the severity training folders (counts, pairing, severity distribution).

Usage (from repo root or severity_analysis):

  python severity_analysis/check_severity_dataset.py

Optional extra roots to profile the same layout (images/ + masks/):

  python severity_analysis/check_severity_dataset.py --extra "D:/datasets/my_leaf_masks/data"
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_PKG = Path(__file__).resolve().parent
if str(_PKG) not in sys.path:
    sys.path.insert(0, str(_PKG))

from severity_data_utils import (
    default_aug_dir,
    default_severity_data_dir,
    print_profile,
    profile_folder,
)


def count_aug_pairs(aug_base) -> tuple[int, int]:
    from pathlib import Path

    p = Path(aug_base)
    img = p / "images"
    msk = p / "masks"
    if not img.is_dir() or not msk.is_dir():
        return 0, 0
    ni = len([f for f in img.iterdir() if f.suffix.lower() == ".jpg"])
    nm = len([f for f in msk.iterdir() if f.suffix.lower() == ".png"])
    return ni, nm


def main() -> int:
    parser = argparse.ArgumentParser(description="Check severity dataset layout and label distribution.")
    parser.add_argument(
        "--extra",
        action="append",
        default=[],
        metavar="DIR",
        help="Additional data root(s) with images/ and masks/ (can repeat).",
    )
    args = parser.parse_args()

    base = default_severity_data_dir()
    if not base.is_dir():
        print(f"ERROR: Default data folder not found:\n  {base}", file=sys.stderr)
        print("Place your archive under severity_analysis/archive (2)/data/data or pass --extra only.", file=sys.stderr)
        return 1

    stats = profile_folder(base)
    print_profile("Primary (notebook BASE_PATH)", stats)

    aug = default_aug_dir()
    ni, nm = count_aug_pairs(aug)
    print(f"\n=== Augmented (train-only) ===\n  {aug}\n  JPG: {ni}  PNG: {nm}")

    for i, root in enumerate(args.extra):
        p = Path(root)
        if not p.is_dir():
            print(f"\n[skip] not a directory: {root}")
            continue
        ex = profile_folder(p)
        print_profile(f"Extra #{i + 1}: {root}", ex)

    print(
        "\nNote: Severity percent = diseased pixels / entire image area (same as mobile app). "
        "For severity relative to leaf area only, add a leaf mask and use disease/(leaf); notebook + app must match."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
