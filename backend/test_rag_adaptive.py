import sys
from pathlib import Path

# Add backend to path
sys.path.append(str(Path.cwd()))

from app.services.rag_service import build_index, query_rag
from app.db.session import SessionLocal
from app.models.all_models import Field, DiseaseScan
import uuid

def run_test():
    # 1. First, (Re)build the index to ensure we have data
    print("--- STEP 1: INDEXING DOCUMENTS ---")
    count = build_index()
    print(f"Indexed {count} chunks from treatment2 folder.")

    if count == 0:
        print("Error: No documents indexed. Please check Data/treatment2 path.")
        return

    # 2. Setup Database Session
    db = SessionLocal()
    try:
        # 3. Use the first available field or create one
        field = db.query(Field).first()
        if not field:
            print("No field found in DB. Test cannot proceed with scan_id.")
            return

        # 4. Create a fresh test scan for Tomato Late Blight
        scan = DiseaseScan(
            field_id=field.id,
            crop_type="Tomato",
            predicted_disease="Late Blight",
            confidence=0.98,
            image_url="http://localhost/test.jpg"
        )
        db.add(scan)
        db.commit()
        db.refresh(scan)
        
        print(f"\n--- STEP 2: RUNNING ADAPTIVE RAG TEST ---")
        print(f"Testing for: {scan.crop_type} - {scan.predicted_disease} (ScanID: {scan.id})")

        question = "بجاه ربي عاوني، الطماطم فيها بقع كحلة واليوم ثمة رطوبة كبيرة (95%). شنوة الدواء اللي تنصحني بيه؟"
        
        result = query_rag(question, language="darija", scan_id=scan.id)

        print("\n--- AI RESPONSE (Tunisian Darija) ---")
        print(result['answer'])
        
        print("\n--- DATA SOURCES ---")
        for s in result['sources'][:3]:
            print(f"- {s['filename']} (Relevance: {s['score']:.4f})")

    finally:
        db.close()

if __name__ == "__main__":
    run_test()
