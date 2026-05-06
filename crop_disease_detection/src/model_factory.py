"""
model_factory.py — Phase 3: Modeling
======================================
Provides a unified factory for building any supported CNN architecture
using Transfer Learning (frozen base) + a custom classification head.
"""
import tensorflow as tf
from config import INPUT_SHAPE, NUM_CLASSES

# Supported architectures: (base class, preprocessing function)
_ARCH_REGISTRY = {
    "MobileNetV3Large": (
        tf.keras.applications.MobileNetV3Large,
        tf.keras.applications.mobilenet_v3.preprocess_input,
    ),
    "EfficientNetB0": (
        tf.keras.applications.EfficientNetB0,
        tf.keras.applications.efficientnet.preprocess_input,
    ),
    "ResNet50V2": (
        tf.keras.applications.ResNet50V2,
        tf.keras.applications.resnet_v2.preprocess_input,
    ),
}


def build_model(architecture: str, num_classes: int = NUM_CLASSES,
                input_shape: tuple = INPUT_SHAPE, dropout: float = 0.3):
    """
    Build a Transfer Learning model with a custom classification head.

    Architecture
    ------------
    Input → Preprocessing → Frozen Base Model → GlobalAvgPool → Dropout → Dense(softmax)

    Parameters
    ----------
    architecture : str    One of 'MobileNetV3Large', 'EfficientNetB0', 'ResNet50V2'
    num_classes  : int    Number of output classes (default: NUM_CLASSES from config)
    input_shape  : tuple  (H, W, C) — must match training images
    dropout      : float  Dropout rate before the final Dense layer

    Returns
    -------
    model      : tf.keras.Model  (uncompiled, base frozen)
    base_model : tf.keras.Model  (the pre-trained backbone, for fine-tuning)
    """
    if architecture not in _ARCH_REGISTRY:
        supported = list(_ARCH_REGISTRY.keys())
        raise ValueError(f"Unknown architecture '{architecture}'. Choose from: {supported}")

    base_cls, preprocess_fn = _ARCH_REGISTRY[architecture]

    # ── 1. Pre-trained backbone (ImageNet weights, no top) ─────────────────────
    base_model = base_cls(input_shape=input_shape, include_top=False, weights="imagenet")
    base_model.trainable = False   # frozen during Phase 1

    # ── 2. Build functional model ──────────────────────────────────────────────
    inputs = tf.keras.Input(shape=input_shape, name="input_image")
    x = preprocess_fn(inputs)                          # architecture-specific scaling
    x = base_model(x, training=False)                 # frozen forward pass
    x = tf.keras.layers.GlobalAveragePooling2D()(x)   # (batch, features)
    x = tf.keras.layers.Dropout(dropout)(x)           # regularization
    outputs = tf.keras.layers.Dense(
        num_classes, activation="softmax", name="disease_classifier"
    )(x)

    model = tf.keras.Model(inputs, outputs, name=architecture)
    return model, base_model
