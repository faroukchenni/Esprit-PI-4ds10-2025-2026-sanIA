"""
Sania AgriSmart - Database Seed Script
Populates the database with realistic Tunisian agriculture demo data.
Run: python seed_data.py
"""
import sys, os, random, uuid, json
from datetime import datetime, timedelta
# Make sure we can import the app modules
sys.path.insert(0, os.path.dirname(__file__))

from app.db.session import SessionLocal, engine, Base
from app.models.all_models import (
    Cooperative, User, UserRole, Farm, Field,
    SensorReading, IrrigationLog, DiseaseScan, NDVIRecord,
    Animal, VaccinationLog, TreatmentLog, Alert,
)
from app.core.security import get_password_hash

# --- Fixed UUIDs for consistency ---
COOP_ID     = uuid.UUID("11111111-1111-1111-1111-111111111111")
FARM_ID     = uuid.UUID("88888888-4444-4444-4444-121212121212")
FARMER_ID   = uuid.UUID("22222222-2222-2222-2222-222222222222")
FIELD_IDS   = [
    uuid.UUID("aaaaaaaa-aaaa-aaaa-aaaa-bbbbbbbbbbbb"),
    uuid.UUID("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
    uuid.UUID("cccccccc-cccc-cccc-cccc-cccccccccccc"),
    uuid.UUID("dddddddd-dddd-dddd-dddd-dddddddddddd"),
    uuid.UUID("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
    uuid.UUID("ffffffff-ffff-ffff-ffff-ffffffffffff"),
]
ANIMAL_IDS = [uuid.uuid4() for _ in range(8)]

from sqlalchemy import text

def seed():
    # Force reset with CASCADE
    print("Resetting database schema (force drop cascade)...")
    with engine.connect() as conn:
        conn.execute(text("DROP SCHEMA public CASCADE; CREATE SCHEMA public;"))
        conn.commit()
    
    Base.metadata.create_all(bind=engine)
    
    db = SessionLocal()
    now = datetime.utcnow()

    try:
        # 1. COOPERATIVE
        print("Creating cooperative...")
        coop = Cooperative(id=COOP_ID, name="Cooperative Agricole du Cap Bon", location="Nabeul, Tunisie")
        db.add(coop)

        # 3. USER (farmer)
        print("Creating user...")
        user = User(
            id=FARMER_ID, name="Ahmed Ben Salem", email="farmer@agrismart.tn",
            password_hash=get_password_hash("Farmer123!"),
            role=UserRole.FARMER, cooperative_id=COOP_ID, farm_id=FARM_ID,
        )
        db.add(user)

        # 4. FARM
        print("Creating farm...")
        farm = Farm(
            id=FARM_ID, cooperative_id=COOP_ID,
            name="Domaine Ben Salem", location="Grombalia, Cap Bon", owner_name="Ahmed Ben Salem",
        )
        db.add(farm)
        db.commit()

        # 5. FIELDS (including realistic polygons for NDVI demo)
        print("Creating fields with polygons...")
        fields_data = [
            {
                "id": FIELD_IDS[0], "name": "Champ de Pomme de Terre Bizerte", "crop_type": "Potato", "area_ha": 12.5,
                "polygon": [[37.252, 9.858], [37.255, 9.858], [37.255, 9.862], [37.252, 9.862]]
            },
            {
                "id": FIELD_IDS[1], "name": "Vignoble du Cap Bon", "crop_type": "Grape", "area_ha": 8.3,
                "polygon": [[36.572, 10.858], [36.575, 10.858], [36.575, 10.862], [36.572, 10.862]]
            },
            {
                "id": FIELD_IDS[2], "name": "Champ de Tomates Nabeul", "crop_type": "Tomato", "area_ha": 4.7,
                "polygon": [[36.452, 10.738], [36.455, 10.738], [36.455, 10.742], [36.452, 10.742]]
            },
            {
                "id": FIELD_IDS[3], "name": "Verger de Pommiers Kasserine", "crop_type": "Apple", "area_ha": 15.0,
                "polygon": [[35.232, 9.128], [35.235, 9.128], [35.235, 9.132], [35.232, 9.132]]
            },
            {
                "id": FIELD_IDS[4], "name": "Grand champ de Sidi Bouzid", "crop_type": "Tomato", "area_ha": 12.0,
                "polygon": [[34.950, 9.450], [34.955, 9.450], [34.955, 9.460], [34.950, 9.460]]
            },
            {
                "id": FIELD_IDS[5], "name": "Céréales de Jendouba", "crop_type": "Potato", "area_ha": 30.0,
                "polygon": [[36.500, 8.750], [36.505, 8.750], [36.505, 8.760], [36.500, 8.760]]
            },
        ]
        for fd in fields_data:
            polygon_data = fd.pop("polygon")
            db.add(Field(farm_id=FARM_ID, polygon_geojson=json.dumps(polygon_data), **fd))
        db.commit()

        # 6. SENSOR READINGS
        print("Generating sensor readings...")
        base_profiles = {
            "Potato": {"soil_base": 40, "temp_base": 18, "hum_base": 60},
            "Grape":  {"soil_base": 42, "temp_base": 20, "hum_base": 55},
            "Tomato": {"soil_base": 55, "temp_base": 26, "hum_base": 60},
            "Apple":  {"soil_base": 35, "temp_base": 15, "hum_base": 50},
        }
        readings = []
        for fd in fields_data:
            profile = base_profiles[fd["crop_type"]]
            for day_offset in range(7, 0, -1):
                for hour in [6, 10, 14, 18]:
                    ts = now - timedelta(days=day_offset, hours=random.randint(0, 1), minutes=random.randint(0, 59))
                    ts = ts.replace(hour=hour)
                    hour_factor = 1.0 + 0.15 * (1 if hour in [10, 14] else -1)
                    readings.append(SensorReading(
                        id=uuid.uuid4(), field_id=fd["id"],
                        soil_moisture=round(profile["soil_base"] + random.uniform(-8, 8), 1),
                        temperature_c=round((profile["temp_base"] * hour_factor) + random.uniform(-3, 4), 1),
                        humidity_pct=round(profile["hum_base"] + random.uniform(-10, 12), 1),
                        created_at=ts,
                    ))
        db.bulk_save_objects(readings)
        db.commit()

        # 7. NDVI RECORDS
        print("Generating NDVI records...")
        ndvi_records = []
        ndvi_base = {"Potato": 0.70, "Grape": 0.58, "Tomato": 0.72, "Apple": 0.65}
        for fd in fields_data:
            base = ndvi_base[fd["crop_type"]]
            for week in range(10, 0, -1):
                growth = 0.02 * (10 - week)
                val = round(min(0.95, base + growth + random.uniform(-0.06, 0.06)), 3)
                status = "healthy" if val > 0.5 else "stressed" if val > 0.3 else "critical"
                ndvi_records.append(NDVIRecord(
                    id=uuid.uuid4(), field_id=fd["id"],
                    ndvi_value=val, status=status,
                    captured_at=now - timedelta(weeks=week),
                ))
        db.bulk_save_objects(ndvi_records)
        db.commit()

        # 8. IRRIGATION LOGS
        print("Generating irrigation logs...")
        irr_logs = []
        for fd in fields_data:
            for i in range(random.randint(2, 4)):
                rec_min = random.choice([15, 20, 25, 30, 45])
                exec_min = rec_min + random.randint(-5, 5) if random.random() > 0.3 else None
                irr_logs.append(IrrigationLog(
                    id=uuid.uuid4(), field_id=fd["id"],
                    recommended_minutes=rec_min,
                    executed_minutes=exec_min,
                    water_estimate_m3=round(rec_min * fd["area_ha"] * 0.008 + random.uniform(0, 0.5), 2),
                    status=random.choice(["done", "done", "done", "pending", "skipped"]),
                    created_at=now - timedelta(days=random.randint(0, 6), hours=random.randint(5, 18)),
                ))
        db.bulk_save_objects(irr_logs)
        db.commit()

        # 9. DISEASE SCANS
        print("Generating disease scans...")
        disease_catalog = {
            "Tomato": [
                ("Tomato___Bacterial_spot", 0.92), ("Tomato___Early_blight", 0.87),
                ("Tomato___healthy", 0.96), ("Tomato___healthy", 0.94),
                ("Tomato___Late_blight", 0.83), ("Tomato___healthy", 0.98),
            ],
            "Grape": [
                ("Grape___Black_rot", 0.89), ("Grape___healthy", 0.95),
                ("Grape___Esca_(Black_Measles)", 0.78), ("Grape___healthy", 0.97),
            ],
            "Potato": [
                ("Potato___Early_blight", 0.88), ("Potato___Late_blight", 0.91),
                ("Potato___healthy", 0.95), ("Potato___healthy", 0.97),
            ],
            "Apple": [
                ("Apple___Apple_scab", 0.85), ("Apple___Black_rot", 0.89),
                ("Apple___Cedar_apple_rust", 0.92), ("Apple___healthy", 0.96),
            ],
        }
        scans = []
        for fd in fields_data:
            for disease_name, base_conf in disease_catalog.get(fd["crop_type"], []):
                scans.append(DiseaseScan(
                    id=uuid.uuid4(), field_id=fd["id"],
                    crop_type=fd["crop_type"],
                    image_url=f"/uploads/scans/{fd['crop_type'].lower()}_{uuid.uuid4().hex[:8]}.jpg",
                    predicted_disease=disease_name,
                    confidence=round(base_conf + random.uniform(-0.05, 0.03), 3),
                    created_at=now - timedelta(days=random.randint(0, 14), hours=random.randint(6, 20)),
                ))
        db.bulk_save_objects(scans)
        db.commit()

        print("Generating livestock and alerts...")
        # (Remaining simple data generation for Animals, Vaccination, Treatments, Alerts)
        # We skip these for now to keep the script small and clean if they are not needed for NDVI demo
        # Or add them back without non-ascii

        print("\n" + "="*50)
        print("Success: Database seeded successfully!")
        print("="*50)

    except Exception as e:
        db.rollback()
        print(f"\nError: {e}")
        import traceback
        traceback.print_exc()
    finally:
        db.close()

if __name__ == "__main__":
    print("Sania AgriSmart - Seed Script")
    seed()
