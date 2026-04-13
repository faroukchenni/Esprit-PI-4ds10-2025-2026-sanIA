"""
Add Digital Twin User to the SanIA database.
============================================
Run AFTER seed_data.py:
    python add_digitaltwin_user.py

Creates a dedicated account for the Unity Digital Twin so it can
authenticate with the irrigation agent endpoint without using the
farmer's credentials.

Credentials (match IrrigationDecisionManager defaults):
    email:    digitaltwin@sania.ai
    password: sania2025
"""
import sys, os, uuid
sys.path.insert(0, os.path.dirname(__file__))

from app.db.session import SessionLocal
from app.models.all_models import User, UserRole, Cooperative
from app.core.security import get_password_hash

DIGITALTWIN_ID = uuid.UUID("d1917741-d191-7741-d191-7741d1917741")
COOP_ID        = uuid.UUID("11111111-1111-1111-1111-111111111111")
FARM_ID        = uuid.UUID("88888888-4444-4444-4444-121212121212")

DT_EMAIL    = "digitaltwin@sania.ai"
DT_PASSWORD = "sania2025"

def add_user():
    db = SessionLocal()
    try:
        existing = db.query(User).filter(User.email == DT_EMAIL).first()
        if existing:
            print(f"[OK] Digital Twin user already exists: {DT_EMAIL}")
            return

        # Check cooperative exists (seed_data.py must have run first)
        coop = db.query(Cooperative).filter(Cooperative.id == COOP_ID).first()
        if not coop:
            print("[ERROR] Cooperative not found — run seed_data.py first.")
            return

        user = User(
            id            = DIGITALTWIN_ID,
            name          = "Digital Twin Agent",
            email         = DT_EMAIL,
            password_hash = get_password_hash(DT_PASSWORD),
            role          = UserRole.FARMER,
            cooperative_id = COOP_ID,
            farm_id       = FARM_ID,
        )
        db.add(user)
        db.commit()
        print(f"[OK] Created Digital Twin user:")
        print(f"     email    : {DT_EMAIL}")
        print(f"     password : {DT_PASSWORD}")
        print(f"     id       : {DIGITALTWIN_ID}")

    except Exception as e:
        db.rollback()
        print(f"[ERROR] {e}")
        import traceback; traceback.print_exc()
    finally:
        db.close()

if __name__ == "__main__":
    add_user()
