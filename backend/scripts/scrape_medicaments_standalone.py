
import os
import time
import requests
from duckduckgo_search import DDGS
from pathlib import Path

# Embedded Treatment Data from app.core.treatments to avoid import issues
TREATMENT_DATA = {
    "Tomato": {
        "Early Blight": {
            "fungicides": [
                "Chlorothalonil (Bravo)",
                "Mancozeb (Dithane)",
                "Azoxystrobin (Quadris)",
                "Pyraclostrobin (Cabrio)",
                "Boscalid (Endura)"
            ],
            "organic": [
                "Fixed Copper (Badge)",
                "Bacillus subtilis (Serenade)",
                "Potassium Bicarbonate"
            ]
        },
        "Late Blight": {
            "fungicides": [
                "Ridomil Gold",
                "Chlorothalonil (Bravo)",
                "Cymoxanil (Curzate)",
                "Propamocarb (Previcur)",
                "Fluopicolide (Presidio)"
            ],
            "organic": [
                "Copper products",
                "Peroxide-based sanitizers"
            ]
        },
        "Bacterial Spot": {
            "fungicides": [
                "Copper + Mancozeb",
                "Streptomycin (Agri-Mycin)",
                "Kasugamycin (Kasumin)"
            ],
            "organic": [
                "Copper Soap",
                "Bacillus subtilis (Serenade)",
                "Bacteriophages (AgriPhage)"
            ]
        }
    },
    "Potato": {
        "Early Blight": {
            "fungicides": [
                "Chlorothalonil (Bravo)",
                "Mancozeb",
                "Azoxystrobin (Quadris)",
                "Pyrimethanil (Scala)",
                "Fluopyram"
            ],
            "organic": [
                "Copper compounds",
                "Bacillus amyloliquefaciens"
            ]
        },
        "Late Blight": {
            "fungicides": [
                "Ridomil",
                "Zoxamide + Mancozeb (Gavel)",
                "Cyazofamid (Ranman)",
                "Mandipropamid (Revus)",
                "Famoxadone + Cymoxanil"
            ],
            "organic": [
                "Fixed Copper"
            ]
        }
    },
    "Apple": {
        "Apple Scab": {
            "fungicides": [
                "Captan",
                "Myclobutanil (Immunox)",
                "Mancozeb",
                "Difenoconazole",
                "Dodine"
            ],
            "organic": [
                "Sulfur",
                "Lime Sulfur",
                "Potassium Bicarbonate"
            ]
        },
        "Black Rot": {
            "fungicides": [
                "Captan",
                "Thiophanate-methyl",
                "Trifloxystrobin",
                "Kresoxim-methyl"
            ],
            "organic": [
                "Lime Sulfur"
            ]
        },
        "Cedar Apple Rust": {
            "fungicides": [
                "Myclobutanil",
                "Triadimefon",
                "Propiconazole",
                "Tebuconazole"
            ],
            "organic": [
                "Sulfur"
            ]
        }
    },
    "Grape": {
        "Black Rot": {
            "fungicides": [
                "Mancozeb",
                "Ziram",
                "Myclobutanil",
                "Azoxystrobin",
                "Tebuconazole"
            ],
            "organic": [
                "Copper formulations",
                "Sulfur"
            ]
        },
        "Esca (Black Measles)": {
            "fungicides": [
                "Thiophanate-methyl",
                "Trichoderma"
            ],
            "organic": [
                "Trichoderma species"
            ]
        },
        "Leaf Blight (Isariopsis)": {
            "fungicides": [
                "Mancozeb",
                "Copper Oxychloride",
                "Chlorothalonil"
            ],
            "organic": [
                "Copper sprays",
                "Sulfur"
            ]
        }
    }
}

BASE_DIR = Path(r"C:\Users\21658\Desktop\ProjetPi\Data\Medicaments")
MAX_IMAGES = 500  # Target number of images per product

def clean_filename(name):
    safe_name = "".join([c for c in name if c.isalnum() or c in (' ', '-', '_', '(', ')')])
    return safe_name.strip()

def download_image(url, folder, filename):
    try:
        headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'}
        response = requests.get(url, headers=headers, timeout=5)
        if response.status_code == 200:
            # Check if valid image content
            if len(response.content) < 1000: # Skip tiny files/errors
                return False
            with open(folder / filename, 'wb') as f:
                f.write(response.content)
            return True
    except Exception as e:
        pass
    return False

def scrape_medicaments():
    print(f"Starting large-scale scrape to {BASE_DIR}...")
    
    try:
        ddgs = DDGS()
    except Exception as e:
        print(f"Failed to initialize DuckDuckGo Search: {e}")
        return

    if not BASE_DIR.exists():
        BASE_DIR.mkdir(parents=True)

    for crop, diseases in TREATMENT_DATA.items():
        print(f"\nTargeting Crop: {crop}")
        
        for disease, treat_cats in diseases.items():
            disease_folder_name = f"{crop}___{disease.replace(' ', '_')}"
            print(f"  Disease Group: {disease_folder_name}")
            
            treatments = treat_cats.get('fungicides', []) + treat_cats.get('organic', [])
            
            for treat_raw in treatments:
                treat_name = clean_filename(treat_raw)
                product_folder = BASE_DIR / disease_folder_name / treat_name
                if not product_folder.exists():
                    product_folder.mkdir(parents=True)
                
                # Check existing count
                existing = list(product_folder.glob("*.jpg"))
                current_count = len(existing)
                
                if current_count >= MAX_IMAGES:
                    print(f"    Skipping {treat_name} (already has {current_count} images)")
                    continue
                
                print(f"    Scraping images for: {treat_name} ({current_count}/{MAX_IMAGES})...")
                
                # Use multiple search variations to find ample images
                queries = [
                    f"{treat_name} fungicide bottle",
                    f"{treat_name} fungicide product",
                    f"{treat_name} label",
                    f"{treat_name} packaging",
                    f"{treat_name} agricultural chemical",
                    f"buy {treat_name} fungicide"
                ]
                
                for query in queries:
                    if current_count >= MAX_IMAGES:
                        break
                        
                    print(f"      Query: '{query}'")
                    try:
                        results = ddgs.images(query, max_results=150)
                        
                        for res in results:
                            if current_count >= MAX_IMAGES:
                                break
                            
                            image_url = res.get('image')
                            if image_url:
                                # Use timestamp to avoid overwriting and ensure unique filenames
                                timestamp = int(time.time() * 1000)
                                if download_image(image_url, product_folder, f"img_{timestamp}.jpg"):
                                    current_count += 1
                                    if current_count % 50 == 0:
                                        print(f"        Downloaded {current_count} images...")
                            time.sleep(0.1)
                            
                    except Exception as e:
                        print(f"      Error with query '{query}': {e}")
                        time.sleep(2) # Backoff
                
                print(f"    Finished {treat_name}: Total {current_count} images.")

if __name__ == "__main__":
    scrape_medicaments()
