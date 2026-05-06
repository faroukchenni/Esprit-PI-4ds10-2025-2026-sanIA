import os
import shutil
import random
from pathlib import Path

def split_data(source_dir, dest_dir, train_ratio=0.8, val_ratio=0.1, test_ratio=0.1):
    source_path = Path(source_dir)
    dest_path = Path(dest_dir)
    
    # Create main destination directories
    for split in ['train', 'val', 'test']:
        (dest_path / split).mkdir(parents=True, exist_ok=True)
        
    # Get ALL classes, including Background for real-world robustness
    classes = [d for d in source_path.iterdir() if d.is_dir()]
    
    total_copied = 0
    
    for cls in classes:
        print(f"Processing: {cls.name}")
        
        # Get all images properly without duplicates on Windows
        images = [f for f in cls.iterdir() if f.is_file() and f.suffix.lower() in ('.jpg', '.jpeg', '.png')]
        
        # Shuffle randomly for unbiased distribution
        random.seed(42) # Seed for reproducibility ensures we get the exact same split if we run it again
        random.shuffle(images)
        
        # Calculate split indices
        n_total = len(images)
        n_train = int(n_total * train_ratio)
        n_val = int(n_total * val_ratio)
        
        train_images = images[:n_train]
        val_images = images[n_train:n_train+n_val]
        test_images = images[n_train+n_val:]
        
        # Create the sub-folders for this specific disease and copy the files
        splits = [('train', train_images), ('val', val_images), ('test', test_images)]
        
        for split_name, split_images in splits:
            # e.g., Data/Processed/Split_Dataset/train/Tomato___Bacterial_spot
            split_cls_dir = dest_path / split_name / cls.name
            split_cls_dir.mkdir(parents=True, exist_ok=True)
            
            for img in split_images:
                # We use copy2 to preserve the original file's metadata
                shutil.copy2(img, split_cls_dir / img.name)
                total_copied += 1
                
        print(f"  -> Train: {len(train_images)} | Val: {len(val_images)} | Test: {len(test_images)}")

    print(f"\nSuccess! Total images organized into train/val/test: {total_copied}")

if __name__ == "__main__":
    # Define exact paths
    SRC = r"c:/Users/21658/Desktop/ProjetPi/Data/Raw/Plant_leave_diseases_dataset_without_augmentation"
    DEST = r"c:/Users/21658/Desktop/ProjetPi/Data/Processed/Split_Dataset"
    
    print(f"Source Directory: {SRC}")
    print(f"Destination Directory: {DEST}")
    print("Starting 80/10/10 data split... (This might take a minute)")
    
    split_data(SRC, DEST)
