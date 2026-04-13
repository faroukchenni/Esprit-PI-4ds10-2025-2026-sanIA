
import os
import sys
import time
import requests
from duckduckgo_search import DDGS
from pathlib import Path

# Add the backend directory to sys.path to import app.core.treatments
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))

try:
    from app.core.treatments import TREATMENT_DATA
except ImportError:
    print("Error: Could not import TREATMENT_DATA from app.core.treatments")
    sys.exit(1)

BASE_DIR = Path(r"C:\Users\21658\Desktop\ProjetPi\Data\Medicaments")
MAX_IMAGES = 10  # Number of images to download per product

def clean_filename(name):
    return "".join([c for c in name if c.isalnum() or c in (' ', '-', '_')]).strip()

def download_image(url, folder, index):
    try:
        response = requests.get(url, timeout=10)
        if response.status_code == 200:
            with open(folder / f"image_{index}.jpg", 'wb') as f:
                f.write(response.content)
            return True
    except Exception as e:
        print(f"Failed to download {url}: {e}")
    return False

def scrape_medicaments():
    ddgs = DDGS()
    
    if not BASE_DIR.exists():
        BASE_DIR.mkdir(parents=True)

    for crop, diseases in TREATMENT_DATA.items():
        print(f"\nProcessing Crop: {crop}")
        
        for disease, treatments in diseases.items():
            # Format disease name to match existing data folder structure if possible
            # e.g. "Early Blight" -> "Early_Blight" (prefix with Crop___ later?)
            disease_folder_name = f"{crop}___{disease.replace(' ', '_')}"
            
            # Special handling for "Healthy" etc if needed, but treatments usually only for diseases
            if "healthy" in disease.lower():
                continue

            print(f"  Disease: {disease}")
            
            # Combine fungicides and organic treatments
            all_treatments = treatments.get('fungicides', []) + treatments.get('organic', [])
            
            for treatment_text in all_treatments:
                # Extract the main chemical name or product name
                # e.g. "Chlorothalonil (Bravo, Echo)..." -> "Chlorothalonil Bravo Fungicide"
                treatment_name = treatment_text.split(" - ")[0]
                treatment_query = f"{treatment_name} fungicide product bottle"
                
                # Create folder
                treatment_folder = BASE_DIR / disease_folder_name / clean_filename(treatment_name)
                if not treatment_folder.exists():
                    treatment_folder.mkdir(parents=True)
                
                # specific check to avoid re-downloading if folder has images
                if len(list(treatment_folder.glob("*.jpg"))) >= MAX_IMAGES:
                    print(f"    Skipping {treatment_name}, already has images.")
                    continue

                print(f"    Searching for: {treatment_query}...")
                
                try:
                    results = ddgs.images(
                        treatment_query,
                        max_results=MAX_IMAGES + 5,
                    )
                    
                    count = 0
                    for res in results:
                        if count >= MAX_IMAGES:
                            break
                        if download_image(res['image'], treatment_folder, count):
                            count += 1
                        time.sleep(0.5) # Be polite
                    
                    print(f"    Downloaded {count} images.")
                    
                except Exception as e:
                    print(f"    Error searching/downloading for {treatment_name}: {e}")

if __name__ == "__main__":
    scrape_medicaments()
