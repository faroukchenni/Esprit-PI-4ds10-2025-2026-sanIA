import tensorflow as tf
import os
from pathlib import Path

def build_balanced_train_loader(train_dir, img_size=(224, 224), batch_size=32):
    """
    Creates a perfectly balanced training Data Loader by implementing 
    Alternative 2: Weighted Random Sampling + Dynamic Augmentation.
    """
    # TF automatically sorts subfolders alphabetically, so we do the same
    class_names = sorted([d.name for d in Path(train_dir).iterdir() if d.is_dir()])
    num_classes = len(class_names)
    
    # 1. Define our Robustness Augmentation pipeline (this happens entirely in memory/GPU)
    data_augmentation = tf.keras.Sequential([
        tf.keras.layers.RandomFlip("horizontal_and_vertical"),
        tf.keras.layers.RandomRotation(0.15),  # Randomly rotates up to ±15% of a full circle
        tf.keras.layers.RandomContrast(0.2),   # Randomly adjusts contrast by ±20%
        tf.keras.layers.RandomBrightness(0.2)  # Randomly adjusts brightness by ±20%
    ], name="robustness_augmentation")
    
    def decode_img(file_path):
        img = tf.io.read_file(file_path)
        img = tf.io.decode_jpeg(img, channels=3) # Decode the JPGs
        img = tf.image.resize(img, [img_size[0], img_size[1]])
        return img

    datasets = []
    
    # 2. Create a completely separate, infinite deck of cards (dataset) for each of the 15 diseases
    for i, class_name in enumerate(class_names):
        class_dir = os.path.join(train_dir, class_name)
        
        # Grab all images in this specific folder
        file_pattern = os.path.join(class_dir, "*.*")
        ds = tf.data.Dataset.list_files(file_pattern, shuffle=True)
        
        # Map the filepaths to actual image pixels and attach its correct integer label (0 to 14)
        ds = ds.map(
            lambda x: (decode_img(x), tf.constant(i, dtype=tf.int32)), 
            num_parallel_calls=tf.data.AUTOTUNE
        )
        
        # The secret sauce: Make the dataset repeat forever so we never run out of Potato images!
        ds = ds.repeat() 
        datasets.append(ds)

    # 3. The Load Balancer: We combine all 15 infinite decks into one.
    # We enforce a perfect 1/15th probability (equal weighting) for every batch it pulls.
    balanced_ds = tf.data.Dataset.sample_from_datasets(
        datasets, 
        weights=[1.0 / num_classes] * num_classes
    )

    # 4. Apply the Dynamic Augmentation to the balanced stream, right before the AI sees it
    balanced_ds = balanced_ds.map(
        lambda x, y: (data_augmentation(x, training=True), y),
        num_parallel_calls=tf.data.AUTOTUNE
    )
    
    # 5. Finally, chop the stream into Batches (e.g., 32 images at a time) and prefetch for GPU speed
    balanced_ds = balanced_ds.batch(batch_size).prefetch(buffer_size=tf.data.AUTOTUNE)
    
    return balanced_ds, class_names


def build_standard_evaluation_loader(data_dir, img_size=(224, 224), batch_size=32):
    """
    Standard Data Loader for Validation and Testing sets.
    Rule #1: NO class balancing (we want to test on the raw, true distributions).
    Rule #2: NO augmentation (we must test on pure, un-warped images).
    """
    ds = tf.keras.utils.image_dataset_from_directory(
        data_dir,
        image_size=img_size,
        batch_size=batch_size,
        shuffle=False, # We don't need to shuffle validation/test sets during evaluation
        label_mode='int'
    )
    
    ds = ds.prefetch(buffer_size=tf.data.AUTOTUNE)
    return ds


if __name__ == "__main__":
    # Test our Magic Data Loaders locally
    TRAIN_DIR = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset/train"
    VAL_DIR = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset/val"
    TEST_DIR = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset/test"
    
    print("\n--- BUILDING BALANCED TRAINING LOADER ---")
    train_loader, classes = build_balanced_train_loader(TRAIN_DIR, batch_size=32)
    
    print("\n--- BUILDING VALIDATION LOADER ---")
    val_loader = build_standard_evaluation_loader(VAL_DIR, batch_size=32)
    
    print("\n--- BUILDING TEST LOADER ---")
    test_loader = build_standard_evaluation_loader(TEST_DIR, batch_size=32)
    
    print(f"\nSuccessfully loaded {len(classes)} classes in perfect balance!")
    print("The Data Loaders are officially completed and ready for the modeling phase.")
