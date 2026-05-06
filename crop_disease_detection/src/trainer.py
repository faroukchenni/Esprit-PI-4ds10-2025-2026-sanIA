"""
trainer.py — Phase 3: Model Training & Evaluation
===================================================
Houses the two-phase training loop (Transfer Learning → Fine-Tuning)
and the benchmark runner that iterates over all architectures.

Key fix (Phase 2 BatchNorm bug):
  When base_model.trainable = True, BatchNorm layers update their running
  stats during training, causing train-accuracy to collapse (~46%).
  The fix is to iterate over every layer and freeze any BatchNorm layer
  regardless of the overall trainable flag.
"""
import os
import time
import logging
import numpy as np
import tensorflow as tf
from pathlib import Path

from config import (BATCH_SIZE, STEPS_PER_EPOCH,
                    PHASE_1_EPOCHS, PHASE_2_EPOCHS,
                    PHASE_1_LR, PHASE_2_LR,
                    BENCHMARK_MODELS_DIR)
from model_factory import build_model

log = logging.getLogger(__name__)


def _freeze_batchnorm(model):
    """Freeze all BatchNormalization layers inside a model (in-place)."""
    for layer in model.layers:
        if isinstance(layer, tf.keras.layers.BatchNormalization):
            layer.trainable = False


def _get_callbacks(patience_es=5, patience_lr=3):
    """Return standard training callbacks used in both phases."""
    return [
        tf.keras.callbacks.EarlyStopping(
            monitor="val_loss",
            patience=patience_es,
            restore_best_weights=True,
            verbose=1,
        ),
        tf.keras.callbacks.ReduceLROnPlateau(
            monitor="val_loss",
            factor=0.2,
            patience=patience_lr,
            min_lr=1e-7,
            verbose=1,
        ),
    ]


def train_two_phase(architecture: str, train_ds, val_ds) -> tuple:
    """
    Two-phase training:
      Phase 1 — Transfer Learning  : only the custom head is trained.
      Phase 2 — Fine-Tuning        : entire model trained, BatchNorm frozen.

    Returns
    -------
    model    : trained tf.keras.Model
    history  : dict  merged history from both phases
    """
    log.info(f">  Training: {architecture}")
    model, base_model = build_model(architecture)

    # ── Phase 1: Train head only ─────────────────────────────────────────────
    # Ensure steps_per_epoch is set to prevent infinite loops with repeated datasets
    actual_steps = STEPS_PER_EPOCH
    if actual_steps is None:
        # Fallback calculation if config didn't reload: (13719 images / 32 batch)
        actual_steps = 428
    
    log.info(f"   Phase 1 — head warm-up  ({PHASE_1_EPOCHS} epochs, LR={PHASE_1_LR}, steps={actual_steps})")
    model.compile(
        optimizer=tf.keras.optimizers.Adam(PHASE_1_LR),
        loss=tf.keras.losses.SparseCategoricalCrossentropy(),
        metrics=["accuracy"]
    )
    h1 = model.fit(
        train_ds,
        steps_per_epoch=actual_steps,
        validation_data=val_ds,
        epochs=PHASE_1_EPOCHS,
        callbacks=_get_callbacks(patience_es=5, patience_lr=3),
        verbose=1,
    )

    # ── Phase 2: Fine-tune entire model (with BatchNorm frozen) ─────────────
    # Ensure steps_per_epoch is set
    actual_steps = STEPS_PER_EPOCH or 428

    log.info(f"   Phase 2 — fine-tuning   ({PHASE_2_EPOCHS} epochs, LR={PHASE_2_LR}, steps={actual_steps})")
    base_model.trainable = True          # unfreeze all layers …
    _freeze_batchnorm(base_model)        # … but keep BN stats frozen (critical!)

    model.compile(
        optimizer=tf.keras.optimizers.Adam(PHASE_2_LR),
        loss=tf.keras.losses.SparseCategoricalCrossentropy(),
        metrics=["accuracy"]
    )
    h2 = model.fit(
        train_ds,
        steps_per_epoch=actual_steps,
        validation_data=val_ds,
        epochs=PHASE_2_EPOCHS,
        callbacks=_get_callbacks(patience_es=3, patience_lr=2),
        verbose=1,
    )

    # Merge both histories
    merged = {k: h1.history[k] + h2.history[k] for k in h1.history}
    return model, merged


def run_benchmark(architectures: list, train_ds, val_ds) -> list:
    """
    Train and evaluate every architecture; collect deployment metrics.

    Metrics collected
    -----------------
    • Val Accuracy (%)   — from the last fine-tuning epoch
    • Model Size (MB)    — .keras file size on disk
    • Latency (ms/img)   — average inference time over 10 × BATCH_SIZE images

    Returns
    -------
    results : list[dict]   one dict per architecture
    histories: dict        mapping arch → merged history dict
    """
    results   = []
    histories = {}

    for arch in architectures:
        try:
            model, hist = train_two_phase(arch, train_ds, val_ds)
            histories[arch] = hist

            # Val accuracy
            final_acc = hist["val_accuracy"][-1]

            # Latency
            sample_batch, _ = next(iter(val_ds))
            t0 = time.perf_counter()
            for _ in range(10):
                model.predict(sample_batch, verbose=0)
            latency_ms = ((time.perf_counter() - t0) / (10 * BATCH_SIZE)) * 1000

            # Save & measure size
            save_path = BENCHMARK_MODELS_DIR / f"{arch}.keras"
            model.save(save_path)
            size_mb = os.path.getsize(save_path) / 1e6

            entry = {
                "Architecture"     : arch,
                "Val Accuracy (%)": round(final_acc * 100, 2),
                "Model Size (MB)"  : round(size_mb, 2),
                "Latency (ms/img)" : round(latency_ms, 2),
            }
            results.append(entry)
            log.info(f"DONE: {arch} — acc={final_acc:.4f} | size={size_mb:.1f}MB"
                     f" | latency={latency_ms:.1f}ms")

        except Exception as exc:
            log.error(f"FAIL: {arch} failed: {exc}")

    return results, histories
