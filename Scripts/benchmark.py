import tensorflow as tf
import time
import os
import logging
import json
import pandas as pd
from datetime import datetime
from config import *
from data_loader import build_balanced_train_loader, build_standard_evaluation_loader

# --- TDSP LOGGING CONFIGURATION ---
log_filename = LOGS_DIR / f"benchmark_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(log_filename),
        logging.StreamHandler()
    ]
)

def build_model(architecture):
    """
    Modular model factory for benchmark.
    """
    inputs = tf.keras.Input(shape=INPUT_SHAPE)
    
    if architecture == "MobileNetV3Large":
        base_model = tf.keras.applications.MobileNetV3Large(input_shape=INPUT_SHAPE, include_top=False, weights='imagenet')
        x = tf.keras.applications.mobilenet_v3.preprocess_input(inputs)
    elif architecture == "EfficientNetB0":
        base_model = tf.keras.applications.EfficientNetB0(input_shape=INPUT_SHAPE, include_top=False, weights='imagenet')
        x = tf.keras.applications.efficientnet.preprocess_input(inputs)
    elif architecture == "ResNet50V2":
        base_model = tf.keras.applications.ResNet50V2(input_shape=INPUT_SHAPE, include_top=False, weights='imagenet')
        x = tf.keras.applications.resnet_v2.preprocess_input(inputs)
    else:
        raise ValueError(f"Architecture {architecture} not supported.")

    base_model.trainable = False
    x = base_model(x, training=False)
    x = tf.keras.layers.GlobalAveragePooling2D()(x)
    x = tf.keras.layers.Dropout(0.3)(x)
    outputs = tf.keras.layers.Dense(NUM_CLASSES, activation='softmax')(x)
    
    model = tf.keras.Model(inputs, outputs, name=architecture)
    return model, base_model

def experiment_run(name, train_ds, val_ds):
    logging.info(f"Starting TDSP Modeling Phase for: {name}")
    
    model, base_model = build_model(name)
    
    # --- PHASE 1: Transfer Learning ---
    logging.info(f"[{name}] Phase 1: Training head...")
    model.compile(optimizer=tf.keras.optimizers.Adam(learning_rate=PHASE_1_LR),
                  loss=tf.keras.losses.SparseCategoricalCrossentropy(),
                  metrics=['accuracy'])
    
    model.fit(train_ds, steps_per_epoch=STEPS_PER_EPOCH, validation_data=val_ds, epochs=PHASE_1_EPOCHS, verbose=1)
    
    # --- PHASE 2: Fine-Tuning ---
    logging.info(f"[{name}] Phase 2: Fine-tuning started...")
    base_model.trainable = True
    model.compile(optimizer=tf.keras.optimizers.Adam(learning_rate=PHASE_2_LR),
                  loss=tf.keras.losses.SparseCategoricalCrossentropy(),
                  metrics=['accuracy'])
    
    fit_start = time.time()
    history = model.fit(train_ds, steps_per_epoch=STEPS_PER_EPOCH, validation_data=val_ds, epochs=PHASE_2_EPOCHS, verbose=1)
    fit_end = time.time()
    
    # --- TDSP Metrics Collection ---
    final_acc = history.history['val_accuracy'][-1]
    
    # Measure Latency
    sample_images, _ = next(iter(val_ds))
    inf_start = time.time()
    for _ in range(10): model.predict(sample_images, verbose=0)
    inf_end = time.time()
    latency = ((inf_end - inf_start) / (10 * BATCH_SIZE)) * 1000 
    
    # Save model and measure size
    model_export_path = MODELS_DIR / f"{name}_benchmark.h5"
    model.save(model_export_path)
    file_size = os.path.getsize(model_export_path) / (1024 * 1024)
    
    logging.info(f"[{name}] Run Complete. Accuracy: {final_acc:.4f}, Size: {file_size:.2f}MB, Latency: {latency:.2f}ms")
    
    return {
        "Architecture": name,
        "Accuracy (%)": round(final_acc * 100, 2),
        "Size (MB)": round(file_size, 2),
        "Latency (ms)": round(latency, 2),
        "Timestamp": datetime.now().isoformat()
    }

if __name__ == "__main__":
    train_loader, _ = build_balanced_train_loader(TRAIN_DIR, batch_size=BATCH_SIZE)
    val_loader = build_standard_evaluation_loader(VAL_DIR, batch_size=BATCH_SIZE)
    
    architectures = ["MobileNetV3Large", "EfficientNetB0", "ResNet50V2"]
    results = []
    
    for arch in architectures:
        try:
            results.append(experiment_run(arch, train_loader, val_loader))
        except Exception as e:
            logging.error(f"Failed to benchmark {arch}: {e}")
            
    # Save Final Comparison Table
    df = pd.DataFrame(results)
    benchmark_file = REPORTS_DIR / "final_model_comparison.csv"
    df.to_csv(benchmark_file, index=False)
    
    logging.info("\n" + "="*50)
    logging.info("TDSP BENCHMARK SUMMARY")
    logging.info("\n" + df.to_string(index=False))
    logging.info("="*50)
    logging.info(f"Report saved to {benchmark_file}")
