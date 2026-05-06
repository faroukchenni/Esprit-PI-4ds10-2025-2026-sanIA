from sqlalchemy.orm import Session
from app.db.session import SessionLocal, engine
from app.models.all_models import (
    Base, User, Cooperative, Farm, Field, UserRole, 
    SensorReading, NDVIRecord, Animal, VaccinationLog, 
    TreatmentLog, DiseaseScan, Alert, IrrigationLog
)
from app.core.security import get_password_hash
import uuid
import random
from datetime import datetime, timedelta

def seed():
    print("Dropping and recreating tables...")
    Base.metadata.drop_all(bind=engine)
    Base.metadata.create_all(bind=engine)
    db = SessionLocal()
    
    print("Populating rich seed data for Sania AI...")

    # 1. Create Cooperative
    coop = Cooperative(id=uuid.uuid4(), name="Coopérative Sania-Export", location="Tunis, Tunisia")
    db.add(coop)
    db.flush()
    
    # 2. Create Users
    admin = User(
        email="admin@agrismart.tn",
        name="Majdi Admin",
        password_hash=get_password_hash("Admin123!"),
        role="COOPERATIVE_ADMIN",
        cooperative_id=coop.id
    )
    db.add(admin)
    
    farm = Farm(id=uuid.uuid4(), cooperative_id=coop.id, name="Ferme El Hana (Excellence)", location="Béja", owner_name="Mohamed Ben Ali")
    db.add(farm)
    db.flush()

    farmer = User(
        email="farmer@agrismart.tn",
        name="Mohamed Ben Ali",
        password_hash=get_password_hash("Farmer123!"),
        role="FARMER",
        cooperative_id=coop.id,
        farm_id=farm.id
    )
    db.add(farmer)

    # 3. Create Fields
    fields_data = [
        {"name": "Parcelle Nord (Tomates)", "crop": "Tomato", "area": 3.2, "poly": "[[36.8065, 10.1815], [36.8085, 10.1815], [36.8085, 10.1835], [36.8065, 10.1835]]"},
        {"name": "Vigne Est", "crop": "Grape", "area": 5.5, "poly": "[[36.8045, 10.1795], [36.8055, 10.1795], [36.8055, 10.1805], [36.8045, 10.1805]]"}
    ]
    
    db_fields = []
    for fd in fields_data:
        f = Field(
            id=uuid.uuid4(),
            farm_id=farm.id,
            name=fd["name"],
            crop_type=fd["crop"],
            area_ha=fd["area"],
            polygon_geojson=fd["poly"]
        )
        db.add(f)
        db_fields.append(f)
    db.flush()

    # 4. Sensor Readings
    now = datetime.now()
    for field in db_fields:
        for d in range(5):
            db.add(SensorReading(
                id=uuid.uuid4(),
                field_id=field.id,
                soil_moisture=random.uniform(35.0, 55.0),
                temperature_c=random.uniform(18.0, 28.0),
                humidity_pct=random.uniform(50.0, 70.0),
                created_at=now - timedelta(days=d)
            ))

    # 5. Animals (Livestock)
    animals_data = [
        {"tag": "TN-001", "species": "Bovin", "breed": "Holstein"},
        {"tag": "TN-002", "species": "Bovin", "breed": "Charolais"},
        {"tag": "TN-003", "species": "Ovin", "breed": "Noire de Thibar"}
    ]
    for ad in animals_data:
        db.add(Animal(
            id=uuid.uuid4(),
            farm_id=farm.id,
            tag_id=ad["tag"],
            species=ad["species"],
            breed=ad["breed"],
            birth_date=now - timedelta(days=365*2)
        ))

    # 6. Disease Scans
    db.add(DiseaseScan(
        id=uuid.uuid4(),
        field_id=db_fields[0].id,
        image_url="https://images.unsplash.com/photo-1592394533824-9440e5d68530",
        crop_type="Tomato",
        predicted_disease="Early Blight",
        confidence=0.89
    ))
    db.add(DiseaseScan(
        id=uuid.uuid4(),
        field_id=db_fields[1].id,
        image_url="https://images.unsplash.com/photo-1530519729491-acf5c5445771",
        crop_type="Grape",
        predicted_disease="Healthy",
        confidence=0.98
    ))

    # 7. Alerts
    db.add(Alert(
        id=uuid.uuid4(),
        farm_id=farm.id,
        field_id=db_fields[0].id,
        type="IRRIGATION",
        severity="CRITICAL",
        note="Niveau d'humidité critique détecté sur la Parcelle Nord",
        status="open"
    ))
    db.add(Alert(
        id=uuid.uuid4(),
        farm_id=farm.id,
        type="SYSTEM",
        severity="INFO",
        note="Mise à jour du firmware des capteurs terminée",
        status="resolved"
    ))

    db.commit()
    db.close()
    print("--- SUCCESS ---")
    print("Login with: farmer@agrismart.tn / Farmer123!")

if __name__ == "__main__":
    seed()
