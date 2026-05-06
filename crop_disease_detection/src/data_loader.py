"""
data_loader.py — Phase 2: Data Acquisition & Understanding
==========================================================
Implements two loaders:
  1. build_balanced_train_loader  → weighted sampling + online augmentation
  2. build_standard_eval_loader   → clean loader for val/test (no augmentation)
"""
import os
import tensorflow as tf
from pathlib import Path


def build_balanced_train_loader(train_dir, img_size=(224, 224), batch_size=32):
    """
    Balanced training loader using Weighted Random Sampling.

    Strategy
    --------
    • Each disease class gets its own infinite tf.data stream (via .repeat()).
    • tf.data.Dataset.sample_from_datasets() samples equally from every class,
      guaranteeing perfect 1/N balance regardless of the raw class distribution.
    • Online data augmentation is applied *after* balancing so every class
      benefits equally from the augmentation policy.

    Returns
    -------
    balanced_ds : tf.data.Dataset  (batched, prefetched, infinite)
    class_names : list[str]        (sorted alphabetically, matching label index)
    """
    class_names = sorted([d.name for d in Path(train_dir).iterdir() if d.is_dir()])
    num_classes = len(class_names)

    # ── Augmentation pipeline (happens in GPU/CPU memory, not on disk) ─────────
    augmentation = tf.keras.Sequential([
        tf.keras.layers.RandomFlip("horizontal_and_vertical"),
        tf.keras.layers.RandomRotation(0.20),          # ±20% of 360°
        tf.keras.layers.RandomZoom((-0.35, 0.10)),     # zoom out 35% / in 10% — simulates field distance
        tf.keras.layers.RandomTranslation(0.10, 0.10), # ±10% shift — off-centre framing
        tf.keras.layers.RandomContrast(0.40),          # ±40% contrast — outdoor lighting variation
        tf.keras.layers.RandomBrightness(0.40),        # ±40% brightness — shadows / direct sun
    ], name="augmentation_pipeline")

    def _decode(file_path):
        raw   = tf.io.read_file(file_path)
        image = tf.io.decode_jpeg(raw, channels=3)
        image = tf.image.resize(image, img_size)
        return image

    # ── One infinite stream per class ─────────────────────────────────────────
    streams = []
    for label_idx, class_name in enumerate(class_names):
        pattern = os.path.join(str(train_dir), class_name, "*.*")
        ds = tf.data.Dataset.list_files(pattern, shuffle=True)
        ds = ds.map(
            lambda x, idx=label_idx: (_decode(x), tf.constant(idx, tf.int32)),
            num_parallel_calls=tf.data.AUTOTUNE
        )
        ds = ds.cache()           # Cache images in memory after first decoding
        ds = ds.repeat()          # infinite stream for this class
        streams.append(ds)

    # ── Equal-weight sampling across all class streams ─────────────────────────
    balanced = tf.data.Dataset.sample_from_datasets(
        streams,
        weights=[1.0 / num_classes] * num_classes
    )

    # ── Apply augmentation, batch, prefetch ───────────────────────────────────
    balanced = balanced.map(
        lambda x, y: (augmentation(x, training=True), y),
        num_parallel_calls=tf.data.AUTOTUNE
    )
    balanced = balanced.batch(batch_size).prefetch(tf.data.AUTOTUNE)

    return balanced, class_names


def build_standard_eval_loader(data_dir, img_size=(224, 224), batch_size=32):
    """
    Clean evaluation loader for validation and test sets.

    Rules
    -----
    • NO augmentation  — test on original images only.
    • NO shuffling     — deterministic order for reproducible metrics.
    • NO rebalancing   — evaluate on the true class distribution.
    """
    ds = tf.keras.utils.image_dataset_from_directory(
        data_dir,
        image_size=img_size,
        batch_size=batch_size,
        shuffle=False,
        label_mode="int"
    )
    return ds.prefetch(tf.data.AUTOTUNE)
