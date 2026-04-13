
import csv
from pathlib import Path

# Final COMPREHENSIVE Expert Knowledge Base
# Strictly aligned with the 22 classes in Data/Raw/Plant_leave_diseases_dataset_without_augmentation
CROP_DISEASE_MASTER_DATA = [
    # --- APPLE ---
    {
        "Crop": "Apple",
        "Disease": "Apple Scab (Venturia inaequalis)",
        "Organic_Treatment": "Sulfur or Potassium Bicarbonate sprays.",
        "Chemical_Treatment": "Captan, Myclobutanil (Immunox), Flint.",
        "Dosage": "Captan 80 WDG: 2.5-5.0 lbs/acre. Flint: 2 oz/acre.",
        "PHI": "0-14 days",
        "Expert_Notes": "Primary infection occurs during spring rains. Timing is critical (Mills Table).",
        "Cultural_Control": "Flail mow leaf litter in autumn to speed up decomposition."
    },
    {
        "Crop": "Apple",
        "Disease": "Black Rot (Diplodia seriata)",
        "Organic_Treatment": "Copper based products, Lime-sulfur.",
        "Chemical_Treatment": "Mancozeb (Dithane), Captan, Thiophanate-methyl.",
        "Dosage": "Dithane M45: 3 lbs/acre. Captan: 2.5 lbs/acre.",
        "PHI": "0-7 days",
        "Expert_Notes": "Commonly causes 'frog-eye' leaf spot. Overwinters in cankers and mummies.",
        "Cultural_Control": "Prune out dead wood and remove 'mummies' (shriveled fruit) from tree."
    },
    {
        "Crop": "Apple",
        "Disease": "Cedar Apple Rust (Gymnosporangium)",
        "Organic_Treatment": "Limited success with Sulfur. Focus on resistant varieties.",
        "Chemical_Treatment": "Myclobutanil (Immunox), Chlorothalonil, Mancozeb.",
        "Dosage": "Immunox: 4-8 fl oz/acre. Mancozeb: 3 lbs/acre.",
        "PHI": "14-77 days (check label for specific timing)",
        "Expert_Notes": "Requires alternate host (Juniper/Red Cedar). Do not plant near junipers.",
        "Cultural_Control": "Remove galls from nearby juniper trees in early spring."
    },

    # --- GRAPE ---
    {
        "Crop": "Grape",
        "Disease": "Black Rot (Guignardia bidwellii)",
        "Organic_Treatment": "Copper or Sulfur (can be phytotoxic).",
        "Chemical_Treatment": "Myclobutanil (Rally), Mancozeb, Elite.",
        "Dosage": "Rally: 3-5 oz/acre. Mancozeb: 3-4 lbs/acre.",
        "PHI": "14 days (Rally), 66 days (Mancozeb).",
        "Expert_Notes": "Most critical spray period is pre-bloom through 4 weeks post-bloom.",
        "Cultural_Control": "Winter pruning of mummies is mandatory for control."
    },
    {
        "Crop": "Grape",
        "Disease": "Esca (Black Measles)",
        "Organic_Treatment": "Trichoderma species (Bio-control for pruning wounds).",
        "Chemical_Treatment": "No direct cure for established infection. Vector control for insects.",
        "Dosage": "Pruning sealant (Vitiseal) immediately after cuts.",
        "PHI": "N/A",
        "Expert_Notes": "Fungal complex entering through pruning wounds. Causes 'tiger stripe' leaves.",
        "Cultural_Control": "Disinfect tools between vines. Protect large pruning cuts within 24 hours."
    },
    {
        "Crop": "Grape",
        "Disease": "Leaf Blight (Isariopsis)",
        "Organic_Treatment": "Standard Copper/Sulfur fungicide program.",
        "Chemical_Treatment": "Mancozeb, Ziram, or Strobilurins.",
        "Dosage": "Mancozeb: 2-4 lbs/acre.",
        "PHI": "66 days",
        "Expert_Notes": "Usually a minor disease appearing late in the season on poorly sprayed vines.",
        "Cultural_Control": "Improve canopy airflow through shoot thinning and leaf removal."
    },

    # --- POTATO ---
    {
        "Crop": "Potato",
        "Disease": "Early Blight (Alternaria solani)",
        "Organic_Treatment": "Copper hydroxide, Bacillus subtilis.",
        "Chemical_Treatment": "Chlorothalonil, Mancozeb, Azoxystrobin.",
        "Dosage": "Bravo: 1.5 pt/acre. Quadris: 6.0 fl oz/acre.",
        "PHI": "7 days",
        "Expert_Notes": "Characterized by bull's-eye rings on lower leaves.",
        "Cultural_Control": "Crop rotation (3 years). Avoid overhead irrigation."
    },
    {
        "Crop": "Potato",
        "Disease": "Late Blight (Phytophthora infestans)",
        "Organic_Treatment": "Preventative Copper Sulfate sprays.",
        "Chemical_Treatment": "Ridomil Gold, Revus, Ranman.",
        "Dosage": "Ranman: 2.1 fl oz/acre. Ridomil: 0.42 fl oz/1000 row ft.",
        "PHI": "7-14 days",
        "Expert_Notes": "High humidity (>90%) for 10 hours triggers outbreak.",
        "Cultural_Control": "Plant certified seed. Destroy all cull piles."
    },

    # --- TOMATO ---
    {
        "Crop": "Tomato",
        "Disease": "Bacterial Spot (Xanthomonas)",
        "Organic_Treatment": "Copper based fungicides, Serenade ASO.",
        "Chemical_Treatment": "Copper + Mancozeb tank mix, Actigard.",
        "Dosage": "Actigard: 0.3 oz/acre. Copper mix: 1 lb/acre.",
        "PHI": "14 days (Actigard).",
        "Expert_Notes": "Favors warm, humid weather. Spread by splashing water.",
        "Cultural_Control": "Sanitize tools with 10% bleach. Use disease-free seed."
    },
    {
        "Crop": "Tomato",
        "Disease": "Early Blight (Alternaria solani)",
        "Organic_Treatment": "Copper Octanoate, Bacillus amyloliquefaciens.",
        "Chemical_Treatment": "Chlorothalonil (Bravo), Azoxystrobin (Quadris).",
        "Dosage": "Bravo: 1.5-2 pt/acre. Quadris: 6.2 fl oz/acre.",
        "PHI": "0 days (Quadris).",
        "Expert_Notes": "Starts on bottom leaves. Bull's-eye pattern spots.",
        "Cultural_Control": "Drip irrigation. Stake plants to keep leaves dry."
    },
    {
        "Crop": "Tomato",
        "Disease": "Late Blight (Phytophthora infestans)",
        "Organic_Treatment": "Fixed copper (preventative).",
        "Chemical_Treatment": "Ridomil Gold, Revus Top, Tanos.",
        "Dosage": "Tanos: 8 oz/acre. Ridomil: 1 lb/acre.",
        "PHI": "5 days",
        "Expert_Notes": "Very aggressive. Can kill plants in days.",
        "Cultural_Control": "Avoid overhead watering. Destroy infected plants immediately."
    },
    {
        "Crop": "Tomato",
        "Disease": "Leaf Mold (Passalora fulva)",
        "Organic_Treatment": "Ensure humidity < 85%. Serenade ASO.",
        "Chemical_Treatment": "Revus Top, Tanos, Amistar.",
        "Dosage": "Revus Top: 5.5-7.0 fl oz/acre.",
        "PHI": "1 day",
        "Expert_Notes": "Common in greenhouses. Yellow spots on top, fuzzy mold underside.",
        "Cultural_Control": "Improve ventilation. Remove lower leaves for airflow."
    },
    {
        "Crop": "Tomato",
        "Disease": "Septoria Leaf Spot (Septoria lycopersici)",
        "Organic_Treatment": "Fixed Copper, Regalia.",
        "Chemical_Treatment": "Chlorothalonil, Azoxystrobin, Mancozeb.",
        "Dosage": "Daconil: 1-2 lbs/acre.",
        "PHI": "0-7 days",
        "Expert_Notes": "Small spots with dark borders and grey centers. Spreads upward.",
        "Cultural_Control": "3-year crop rotation. Mulch soil to prevent splashing."
    },
    {
        "Crop": "Tomato",
        "Disease": "Spider Mites (Tetranychus urticae)",
        "Organic_Treatment": "Neem oil, Insecticidal soap, Horticultural oil.",
        "Chemical_Treatment": "Abamectin (Agri-Mek), Oberon, Portal.",
        "Dosage": "Agri-Mek: 8-16 fl oz/acre.",
        "PHI": "7 days",
        "Expert_Notes": "Tiny stippling on leaves. Webbing in severe cases. Favors hot/dry weather.",
        "Cultural_Control": "Blast with water. Increase humidity. Avoid broad-spectrum insecticides."
    },
    {
        "Crop": "Tomato",
        "Disease": "Target Spot (Corynespora cassiicola)",
        "Organic_Treatment": "Standard Copper spray program.",
        "Chemical_Treatment": "Inspire Super, Luna Tranquility, Revus Top.",
        "Dosage": "Inspire Super: 16-20 fl oz/acre.",
        "PHI": "7 days",
        "Expert_Notes": "Necrotic lesions with concentric rings. Mistaken for early blight.",
        "Cultural_Control": "Avoid planting new crops near old diseased ones. Drip irrigation."
    },
    {
        "Crop": "Tomato",
        "Disease": "Yellow Leaf Curl Virus (TYLCV)",
        "Organic_Treatment": "Control whitefly vectors using Neem or Soap.",
        "Chemical_Treatment": "No cure. Insecticides for whiteflies (Imidacloprid, Oberon).",
        "Dosage": "Admire Pro (Imidacloprid): 7-10.5 fl oz/acre.",
        "PHI": "21 days",
        "Expert_Notes": "Transmitted by Whiteflies. Leaves turn small, yellow and curl up.",
        "Cultural_Control": "Use silver reflective mulch. Remove infected plants in bags immediately."
    },
    {
        "Crop": "Tomato",
        "Disease": "Mosaic Virus (ToMV/TMV)",
        "Organic_Treatment": "Sanitation is the only control.",
        "Chemical_Treatment": "No cure. Non-host rotation for 2 years.",
        "Dosage": "N/A",
        "PHI": "N/A",
        "Expert_Notes": "Mottled leaves. Extremely contagious through touch/tools.",
        "Cultural_Control": "Wash hands with milk/soap before handling plants. Avoid tobacco use near plants."
    }
]

def export_master_data():
    DATA_DIR = Path(r"C:\Users\21658\Desktop\ProjetPi\Data\treatment2")
    OUTPUT_FILE = DATA_DIR / "treatment_2.csv"
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    
    with open(OUTPUT_FILE, 'w', newline='', encoding='utf-8') as f:
        fieldnames = ["Crop", "Disease", "Organic_Treatment", "Chemical_Treatment", "Dosage", "PHI", "Expert_Notes", "Cultural_Control"]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(CROP_DISEASE_MASTER_DATA)
        
    print(f"Master RAG Database expanded to cover ALL 17 disease classes in your raw dataset.")

if __name__ == "__main__":
    export_master_data()
