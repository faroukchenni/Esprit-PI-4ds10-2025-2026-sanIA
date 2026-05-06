"""
Index a PDF into pgvector (same logic as POST /api/v1/rag/ingest).

Usage (from backend/ with venv activated):
  set OPENAI_API_KEY=sk-...
  python -m scripts.ingest_rag C:\\path\\to\\AgriSmart.pdf

Requires: Postgres with CREATE EXTENSION vector; KNOWLEDGE_FILE_PATH or pass path as argv.
"""
import os
import sys
from pathlib import Path

from dotenv import load_dotenv

# Same .env as the API (so HF_TOKEN, DATABASE_URL, etc. apply when run from any cwd)
_BACKEND_ROOT = Path(__file__).resolve().parents[1]
load_dotenv(_BACKEND_ROOT / ".env", override=True)

# Ensure app package is importable
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.db.session import SessionLocal  # noqa: E402
from app.services.rag_service import ingest_directory, ingest_document  # noqa: E402


def main() -> None:
    path = sys.argv[1] if len(sys.argv) > 1 else (
        os.getenv("KNOWLEDGE_DIR_PATH")
        or os.getenv("KNOWLEDGE_FILE_PATH")
        or os.getenv("KNOWLEDGE_PDF_PATH")
    )
    if not path:
        print("Usage: python -m scripts.ingest_rag <file.pdf|.txt|.md|folder>")
        print("   or set KNOWLEDGE_DIR_PATH (folder) or KNOWLEDGE_FILE_PATH (single file)")
        sys.exit(1)
    db = SessionLocal()
    try:
        p = Path(path)
        if p.is_dir():
            out = ingest_directory(db, path)
        else:
            out = ingest_document(db, path)
        print("OK:", out)
    finally:
        db.close()


if __name__ == "__main__":
    main()
