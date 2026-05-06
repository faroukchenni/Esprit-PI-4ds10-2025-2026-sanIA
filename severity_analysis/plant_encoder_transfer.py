"""
Transfer MobileNetV3Large backbone weights from the plant disease classifier
(TDSP_Crop_Disease_Detection / model_factory) into the Attention U-Net encoder.

The classifier is trained on many crop/disease images (no masks). The U-Net uses
the same MobileNetV3Large layer names, so we copy matching weights by layer name
before segmentation fine-tuning.
"""
from __future__ import annotations

import os
from pathlib import Path

import tensorflow as tf


def default_classifier_path(project_root: Path | str | None = None) -> Path:
    root = Path(project_root) if project_root else Path(__file__).resolve().parent.parent
    return root / "crop_disease_detection" / "models" / "best_model.keras"


def _is_mobilenetv3_large_graph(m: tf.keras.Model) -> bool:
    try:
        m.get_layer("expanded_conv_project")
        m.get_layer("expanded_conv_14_add")
        return True
    except (ValueError, KeyError):
        return False


def _find_mobilenet_backbone(model: tf.keras.Model) -> tf.keras.Model | None:
    """Locate the MobileNetV3Large subgraph inside a saved classifier (possibly nested)."""
    for name in ("mobilenetv3large", "MobileNetV3Large"):
        try:
            m = model.get_layer(name)
            if isinstance(m, tf.keras.Model) and _is_mobilenetv3_large_graph(m):
                return m
        except (ValueError, KeyError):
            pass

    def dfs(m: tf.keras.Model) -> tf.keras.Model | None:
        if _is_mobilenetv3_large_graph(m):
            return m
        for layer in m.layers:
            if isinstance(layer, tf.keras.Model):
                found = dfs(layer)
                if found is not None:
                    return found
        return None

    return dfs(model)


def apply_plant_disease_encoder_to_unet(
    unet: tf.keras.Model,
    classifier_path: str | Path,
) -> bool:
    """
    Copy weights from each MobileNet layer in the classifier onto the U-Net layers
    with the same name (shared encoder topology).

    Returns True if at least one layer was updated, False if file missing or copy failed.
    """
    path = Path(classifier_path)
    if not path.is_file():
        return False

    try:
        classifier = tf.keras.models.load_model(str(path), compile=False, safe_mode=False)
    except Exception:
        try:
            classifier = tf.keras.models.load_model(str(path), compile=False)
        except Exception:
            return False

    src_mb = _find_mobilenet_backbone(classifier)
    if src_mb is None:
        return False

    copied = 0
    skipped = 0
    for layer in src_mb.layers:
        if not layer.weights:
            continue
        try:
            dst = unet.get_layer(layer.name)
        except ValueError:
            skipped += 1
            continue
        try:
            dst.set_weights(layer.get_weights())
            copied += 1
        except ValueError:
            skipped += 1
            continue

    if copied:
        print(f"  [encoder transfer] copied {copied} MobileNet layers ({skipped} name/shape skips)")
    return copied > 0
