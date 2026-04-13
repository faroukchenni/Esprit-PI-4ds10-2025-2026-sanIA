
import csv
import random
import uuid
from datetime import datetime, timedelta
from pathlib import Path

# Configuration
NUM_RECORDS = 2000

# Robust path handling - relative to the script location
# Script location: /backend/scripts/
# Data location: /Data/
try:
    BASE_DIR = Path(__file__).resolve().parent.parent.parent
except NameError:
    # Fallback for notebook/interactive execution
    BASE_DIR = Path.cwd()

DATA_DIR = BASE_DIR / "Data"
OUTPUT_FILE = DATA_DIR / "detailed_treatments.csv"

# Base Knowledge (The "Rules")
CROPS = ["Tomato", "Potato", "Apple", "Grape"]
DISEASES = {
    "Tomato": ["Early Blight", "Late Blight", "Bacterial Spot"],
    "Potato": ["Early Blight", "Late Blight"],
    "Apple": ["Apple Scab", "Black Rot", "Cedar Apple Rust"],
    "Grape": ["Black Rot", "Esca (Black Measles)", "Leaf Blight"]
}

TREATMENTS = {
    "Early Blight": [
        {"name": "Chlorothalonil (Bravo)", "type": "Chemical", "efficacy": 0.85, "base_cost": 25, "phi": 0},
        {"name": "Mancozeb (Dithane)", "type": "Chemical", "efficacy": 0.80, "base_cost": 20, "phi": 5},
        {"name": "Azoxystrobin (Quadris)", "type": "Chemical", "efficacy": 0.90, "base_cost": 45, "phi": 0},
        {"name": "Copper Hydroxide", "type": "Organic", "efficacy": 0.60, "base_cost": 15, "phi": 0},
        {"name": "Bacillus subtilis", "type": "Organic", "efficacy": 0.50, "base_cost": 30, "phi": 0}
    ],
    "Late Blight": [
        {"name": "Ridomil Gold", "type": "Chemical", "efficacy": 0.95, "base_cost": 55, "phi": 5},
        {"name": "Propamocarb", "type": "Chemical", "efficacy": 0.88, "base_cost": 40, "phi": 5},
        {"name": "Fixed Copper", "type": "Organic", "efficacy": 0.55, "base_cost": 15, "phi": 0}
    ],
    "Bacterial Spot": [
        {"name": "Copper + Mancozeb", "type": "Chemical", "efficacy": 0.75, "base_cost": 25, "phi": 5},
        {"name": "Agri-Mycin", "type": "Chemical", "efficacy": 0.70, "base_cost": 35, "phi": 0},
        {"name": "Bacteriophages", "type": "Organic", "efficacy": 0.65, "base_cost": 50, "phi": 0}
    ],
    "Apple Scab": [
        {"name": "Captan", "type": "Chemical", "efficacy": 0.80, "base_cost": 22, "phi": 0},
        {"name": "Myclobutanil", "type": "Chemical", "efficacy": 0.92, "base_cost": 38, "phi": 14},
        {"name": "Sulfur", "type": "Organic", "efficacy": 0.50, "base_cost": 10, "phi": 0}
    ],
    "Black Rot": [
        {"name": "Thiophanate-methyl", "type": "Chemical", "efficacy": 0.85, "base_cost": 32, "phi": 1},
        {"name": "Captan", "type": "Chemical", "efficacy": 0.75, "base_cost": 22, "phi": 0},
        {"name": "Lime Sulfur", "type": "Organic", "efficacy": 0.45, "base_cost": 12, "phi": 0}
    ],
    "Cedar Apple Rust": [
        {"name": "Myclobutanil", "type": "Chemical", "efficacy": 0.95, "base_cost": 38, "phi": 14},
        {"name": "Propiconazole", "type": "Chemical", "efficacy": 0.90, "base_cost": 30, "phi": 14}
    ],
    "Leaf Blight": [
        {"name": "Copper Oxychloride", "type": "Chemical", "efficacy": 0.70, "base_cost": 18, "phi": 0},
        {"name": "Mancozeb", "type": "Chemical", "efficacy": 0.80, "base_cost": 20, "phi": 66}
    ],
    "Esca (Black Measles)": [
        {"name": "Trichoderma species", "type": "Organic", "efficacy": 0.40, "base_cost": 40, "phi": 0},
        {"name": "Pruning & Sanitation", "type": "Mechanical", "efficacy": 0.60, "base_cost": 100, "phi": 0}
    ]
}

STAGES = ["Seedling", "Vegetative", "Flowering", "Fruiting", "Harvest Ready"]
CONDITIONS = ["Dry/Sunny", "Humid", "Rainy", "Cold", "Hot"]

def generate_row():
    crop = random.choice(CROPS)
    disease = random.choice(DISEASES[crop])
    stage = random.choice(STAGES)
    condition = random.choice(CONDITIONS)
    severity = random.choice(["Low", "Moderate", "High", "Critical"])
    
    # Select available treatments for this disease
    options = TREATMENTS.get(disease)
    if not options:
        options = TREATMENTS["Early Blight"] 
    
    treatment = random.choice(options)
    
    # LOGIC SIMULATION
    # 1. Adjust Efficacy based on severity and condition
    actual_efficacy = float(treatment['efficacy'])
    
    # Logic: High severity makes partial control more likely, Critical makes failure likely for weak treatments
    if severity == "Critical" and treatment['type'] == "Organic":
        actual_efficacy -= 0.3 # Organic fails in critical stages
    if severity == "High":
        actual_efficacy -= 0.1
        
    # Logic: Weather impacts
    if condition == "Rainy" and treatment['type'] in ["Organic", "Chemical"]:
        # Assume some wash-off for sprays
        actual_efficacy -= 0.15 
    if condition == "Rainy" and treatment['name'] in ["Sulfur", "Copper Hydroxide"]:
        actual_efficacy -= 0.2 # Wash off significantly
        
    # Safety Check (PHI)
    if stage == "Harvest Ready" and int(treatment['phi']) > 1:
        # Dangerous!
        recommendation_score = 0
        notes = f"unsafe due to PHI ({treatment['phi']} days)"
    else:
        # Clamp efficacy to 0-1 range to avoid negative scores
        actual_efficacy = max(0.0, min(1.0, actual_efficacy))
        recommendation_score = int(actual_efficacy * 100)
        notes = "Recommended"

    if recommendation_score < 40:
        outcome = "Failure"
    elif recommendation_score < 75:
        outcome = "Partial Control"
    else:
        outcome = "Success"

    # Dosage variation
    base_dose = 2.0
    if severity in ["High", "Critical"]:
        dosage = f"{base_dose * 1.5:.1f} pts/acre"
    else:
        dosage = f"{base_dose:.1f} pts/acre"

    farm_id = "{:.8}".format(uuid.uuid4().hex)

    return {
        "Farm_ID": farm_id,
        "Date": (datetime.now() - timedelta(days=random.randint(0, 365))).strftime("%Y-%m-%d"),
        "Crop": crop,
        "Disease": disease,
        "Growth_Stage": stage,
        "Weather_Condition": condition,
        "Severity": severity,
        "Treatment_Applied": treatment['name'],
        "Treatment_Type": treatment['type'],
        "Dosage": dosage,
        "Cost_Per_Acre": int(treatment['base_cost']) + random.randint(-5, 5),
        "Outcome": outcome,
        "Analyst_Notes": f"{treatment['name']} used during {stage} stage under {condition} conditions for {severity} {disease}. Outcome: {outcome}. {notes}."
    }

def main():
    if not DATA_DIR.exists():
        DATA_DIR.mkdir(parents=True)

    print(f"Generating {NUM_RECORDS} synthetic treatment records...")
    
    try:
        with open(OUTPUT_FILE, 'w', newline='', encoding='utf-8') as f:
            fieldnames = ["Farm_ID", "Date", "Crop", "Disease", "Growth_Stage", "Weather_Condition", "Severity", "Treatment_Applied", "Treatment_Type", "Dosage", "Cost_Per_Acre", "Outcome", "Analyst_Notes"]
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            writer.writeheader()
            
            for _ in range(NUM_RECORDS):
                writer.writerow(generate_row())
                
        print(f"Done! Data saved to {OUTPUT_FILE}")
    except Exception as e:
        print(f"Error generating data: {e}")

if __name__ == "__main__":
    main()
