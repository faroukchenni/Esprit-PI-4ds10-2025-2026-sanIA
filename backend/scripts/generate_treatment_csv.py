
import csv
import os
from pathlib import Path

# Define the data structure with detailed information
TREATMENT_DETAILS = [
    # TOMATO
    {"Crop": "Tomato", "Disease": "Early Blight", "Treatment": "Chlorothalonil (Bravo)", "Type": "Chemical", "Dosage": "1.5 - 2 pints per acre", "Interval": "7-10 days", "Safety_Period": "Wait 24h before harvest (PHI 0 days)", "Description": "Broad spectrum protectant fungicide. Apply before disease appears."},
    {"Crop": "Tomato", "Disease": "Early Blight", "Treatment": "Mancozeb (Dithane)", "Type": "Chemical", "Dosage": "1.5 - 2 lbs per acre", "Interval": "7-10 days", "Safety_Period": "PHI 5 days", "Description": "Protectant fungicide. Do not apply within 5 days of harvest."},
    {"Crop": "Tomato", "Disease": "Early Blight", "Treatment": "Azoxystrobin (Quadris)", "Type": "Chemical", "Dosage": "6.0 - 15.5 fl oz per acre", "Interval": "7-14 days", "Safety_Period": "PHI 0 days", "Description": "Systemic fungicide. Excellent for resistance management if rotated."},
    {"Crop": "Tomato", "Disease": "Early Blight", "Treatment": "Copper Hydroxide", "Type": "Organic", "Dosage": "0.5 - 1.5 lbs per acre", "Interval": "5-7 days", "Safety_Period": "PHI 0 days", "Description": "Organic option. Can be phytotoxic in high heat."},
    {"Crop": "Tomato", "Disease": "Late Blight", "Treatment": "Ridomil Gold", "Type": "Chemical", "Dosage": "1 pack per 5-10 gallons", "Interval": "14 days", "Safety_Period": "PHI 5 days", "Description": "Standard for late blight. Use preventively."},
    {"Crop": "Tomato", "Disease": "Late Blight", "Treatment": "Propamocarb (Previcur)", "Type": "Chemical", "Dosage": "1.2 pts per acre", "Interval": "7-10 days", "Safety_Period": "PHI 5 days", "Description": "Systemic fungicide targeting oomycetes."},
    {"Crop": "Tomato", "Disease": "Bacterial Spot", "Treatment": "Copper + Mancozeb", "Type": "Chemical", "Dosage": "Tank mix: 1lb Copper + 1.5lb Mancozeb", "Interval": "7 days", "Safety_Period": "PHI 5 days", "Description": "Standard bactericide mix. Copper alone may not suffice due to resistance."},
    {"Crop": "Tomato", "Disease": "Bacterial Spot", "Treatment": "Agri-Mycin", "Type": "Chemical", "Dosage": "200 ppm spray", "Interval": "4-5 days", "Safety_Period": "PHI 0 days", "Description": "Antibiotic (Streptomycin). Use only during transplant production to avoid resistance."},
    
    # POTATO
    {"Crop": "Potato", "Disease": "Early Blight", "Treatment": "Chlorothalonil", "Type": "Chemical", "Dosage": "1.5 pints per acre", "Interval": "7-10 days", "Safety_Period": "PHI 7 days", "Description": "Mainstay protectant. Apply when plants are 6 inches high."},
    {"Crop": "Potato", "Disease": "Early Blight", "Treatment": "Pyrimethanil (Scala)", "Type": "Chemical", "Dosage": "7 fl oz per acre", "Interval": "7-14 days", "Safety_Period": "PHI 7 days", "Description": "Good for Alternaria. Tank mix with protectant recommended."},
    {"Crop": "Potato", "Disease": "Late Blight", "Treatment": "Revus Top", "Type": "Chemical", "Dosage": "5.5 - 7.0 fl oz per acre", "Interval": "7-10 days", "Safety_Period": "PHI 14 days", "Description": "Translaminar activity. Effective against aggressive strains."},
    {"Crop": "Potato", "Disease": "Late Blight", "Treatment": "Curzate (Cymoxanil)", "Type": "Chemical", "Dosage": "3.2 oz per acre", "Interval": "5-7 days", "Safety_Period": "PHI 14 days", "Description": "Locally systemic. Good 'kick-back' curative activity (up to 24-48h)."},

    # APPLE
    {"Crop": "Apple", "Disease": "Apple Scab", "Treatment": "Captan 80 WDG", "Type": "Chemical", "Dosage": "2.5 - 5 lbs per acre", "Interval": "7-10 days", "Safety_Period": "PHI 0 days", "Description": "Multi-site protectant. Good for cover sprays."},
    {"Crop": "Apple", "Disease": "Apple Scab", "Treatment": "Myclobutanil (Immunox)", "Type": "Chemical", "Dosage": "4 - 8 fl oz per acre", "Interval": "10-14 days", "Safety_Period": "PHI 14 days", "Description": "Systemic (DMI). Has 72-96h post-infection activity."},
    {"Crop": "Apple", "Disease": "Apple Scab", "Treatment": "Sulfur", "Type": "Organic", "Dosage": "10-15 lbs per acre", "Interval": "5-7 days", "Safety_Period": "PHI 0 days", "Description": "Organic. Apply before rain events. Wash off easily."},
    {"Crop": "Apple", "Disease": "Cedar Apple Rust", "Treatment": "Myclobutanil", "Type": "Chemical", "Dosage": "4 - 8 fl oz per acre", "Interval": "10-14 days", "Safety_Period": "PHI 14 days", "Description": "Highly effective against rusts."},
    {"Crop": "Apple", "Disease": "Black Rot", "Treatment": "Thiophanate-methyl", "Type": "Chemical", "Dosage": "0.7 - 1.4 lbs per acre", "Interval": "7-14 days", "Safety_Period": "PHI 1 day", "Description": "Systemic. Also controls powdery mildew."},

    # GRAPE
    {"Crop": "Grape", "Disease": "Black Rot", "Treatment": "Mancozeb", "Type": "Chemical", "Dosage": "2 - 4 lbs per acre", "Interval": "10-14 days", "Safety_Period": "PHI 66 days", "Description": "Critical early season protectant. Do not use close to harvest."},
    {"Crop": "Grape", "Disease": "Black Rot", "Treatment": "Myclobutanil (Rally)", "Type": "Chemical", "Dosage": "3 - 5 oz per acre", "Interval": "14-21 days", "Safety_Period": "PHI 14 days", "Description": "Systemic. Good curative activity."},
    {"Crop": "Grape", "Disease": "Esca (Black Measles)", "Treatment": "Trichoderma species", "Type": "Organic", "Dosage": "Apply paste to wounds", "Interval": "After pruning", "Safety_Period": "N/A", "Description": "Bio-fungicide applied to pruning wounds to prevent infection."},
    {"Crop": "Grape", "Disease": "Leaf Blight", "Treatment": "Copper Oxychloride", "Type": "Chemical", "Dosage": "2 - 4 lbs per acre", "Interval": "7-10 days", "Safety_Period": "PHI 0 days", "Description": "Broad spectrum copper fungicide."},
]

OUTPUT_DIR = Path(r"C:\Users\21658\Desktop\ProjetPi\Data")
OUTPUT_FILE = OUTPUT_DIR / "treatments.csv"

def generate_csv():
    if not OUTPUT_DIR.exists():
        OUTPUT_DIR.mkdir(parents=True)
        
    try:
        with open(OUTPUT_FILE, 'w', newline='', encoding='utf-8') as f:
            fieldnames = ["Crop", "Disease", "Treatment", "Type", "Dosage", "Interval", "Safety_Period", "Description"]
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            
            writer.writeheader()
            for row in TREATMENT_DETAILS:
                writer.writerow(row)
                
        print(f"Successfully generated treatments.csv at {OUTPUT_FILE}")
        print(f"Total rows: {len(TREATMENT_DETAILS)}")
        
    except Exception as e:
        print(f"Error generating CSV: {e}")

if __name__ == "__main__":
    generate_csv()
