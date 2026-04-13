import tensorflow as tf
from data_loader import build_balanced_train_loader, build_standard_evaluation_loader

def build_mobilenet_model(num_classes):
    """
    Constructs a MobileNetV3Large model optimized for deployment 
    on low-power mobile/edge offline devices.
    """
    # 1. Load the Base Model (MobileNetV3Large)
    # We strip the top classification layer (include_top=False)
    # We use pre-trained ImageNet weights so it already knows edges/colors
    base_model = tf.keras.applications.MobileNetV3Large(
        input_shape=(224, 224, 3),
        include_top=False,
        weights='imagenet'
    )
    
    # 2. Freeze the Base Model
    # During Phase 1, we do NOT want to destroy the pre-trained ImageNet weights.
    base_model.trainable = False
    
    # 3. Build our Custom Neural Network Head
    inputs = tf.keras.Input(shape=(224, 224, 3))
    
    # Pass inputs through the frozen base model
    # MobileNet expects inputs in range [-1, 1], but data_loader uses [0, 255].
    # MobileNetV3 includes built-in preprocessing under tf.keras.applications,
    # so we apply it here for safety.
    x = tf.keras.applications.mobilenet_v3.preprocess_input(inputs)
    x = base_model(x, training=False)
    
    # 4. Global Average Pooling
    # Flattens the complex 2D feature maps out of the base model into a neat 1D array
    x = tf.keras.layers.GlobalAveragePooling2D()(x)
    
    # 5. Anti-Overfitting Layer
    # We randomly turn off 30% of neurons so the model doesn't just memorize the data
    x = tf.keras.layers.Dropout(0.3)(x)
    
    # 6. Final Classification Layer
    # Exactly 16 neurons (15 diseases + 1 background).
    # Softmax forces the output probabilities to sum exactly to 1.0 (e.g., 90% Scab, 10% Background)
    outputs = tf.keras.layers.Dense(num_classes, activation='softmax')(x)
    
    model = tf.keras.Model(inputs, outputs)
    
    # 7. Compile the Model
    # Adam optimizer is the industry standard.
    # We use SparseCategoricalCrossentropy because our data_loader returns integer labels (0, 1, 2...)
    # rather than one-hot encoded arrays ([0, 1, 0...]).
    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=0.001),
        loss=tf.keras.losses.SparseCategoricalCrossentropy(),
        metrics=['accuracy']
    )
    
    return model

if __name__ == "__main__":
    # Define exact paths for our data
    TRAIN_DIR = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset/train"
    VAL_DIR = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset/val"
    
    # Define physical constraints
    BATCH_SIZE = 32
    NUM_CLASSES = 16 # We guarantee exactly 15 crops + 1 background folder
    EPOCHS_PHASE_1 = 10
    
    print("\n--- 1. LOADING DATA ---")
    train_loader, classes = build_balanced_train_loader(TRAIN_DIR, batch_size=BATCH_SIZE)
    val_loader = build_standard_evaluation_loader(VAL_DIR, batch_size=BATCH_SIZE)
    
    print(f"\nDiscovered {len(classes)} distinct classes: {classes}")
    
    print("\n--- 2. BUILDING MOBILENETV3 ---")
    model = build_mobilenet_model(num_classes=NUM_CLASSES)
    model.summary()
    
    print("\n--- 3. STARTING PHASE 1 TRAINING ---")
    print("Training just the custom head while MobileNet is frozen...")
    
    # Because our Data Loader repeats infinitely (Alternative 2 strategy),
    # we MUST mathematically dictate exactly when an "Epoch" is over.
    # An Epoch is over when it has seen the largest class mathematically spread out.
    # Tomato_Bacterial_Spot has ~2,042 training images. 
    # 2042 images * 16 classes = 32,672 total target views per epoch.
    # 32,672 / 32 batch size = ~1,021 steps per epoch.
    STEPS_PER_EPOCH = 1021
    
    history = model.fit(
        train_loader,
        steps_per_epoch=STEPS_PER_EPOCH,
        validation_data=val_loader,
        epochs=EPOCHS_PHASE_1
    )
    
    print("\nPhase 1 Training Complete! The model head is now adapted to plant diseases.")
    
    # Note: Phase 2 (Fine-Tuning) and Saving the model to .h5/.tflite 
    # will be implemented after we verify Phase 1 works perfectly.
