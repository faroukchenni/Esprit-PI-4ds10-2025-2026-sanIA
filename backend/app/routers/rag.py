"""
RAG maintenance: index PDFs into pgvector, status.
"""
from pathlib import Path

from fastapi import APIRouter, Depends, Header, HTTPException, Query
from sqlalchemy.orm import Session

from ..db.session import get_db
from ..core.config import settings
from ..core import rag_state
from ..services.rag_service import count_chunks, ingest_directory, ingest_document
from .deps import get_current_active_user
from ..models.all_models import User

router = APIRouter()


@router.get("/status")
def rag_status(
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
):
    return {
        "indexed_chunks": count_chunks(db),
        "pgvector_enabled": rag_state.PGVECTOR_ENABLED,
    }


@router.post("/ingest")
def rag_ingest_document(
    db: Session = Depends(get_db),
    x_rag_ingest_secret: str | None = Header(None, alias="X-RAG-Ingest-Secret"),
    path: str | None = Query(
        None,
        description="Absolute path to a file (.pdf, .txt, .md), a folder of those files, or KNOWLEDGE_DIR_PATH",
    ),
):
    """
    Chunk + embed + store. Protected by RAG_INGEST_SECRET env.
    Example: curl -X POST "http://localhost:8000/api/v1/rag/ingest" -H "X-RAG-Ingest-Secret: YOUR_SECRET"
    """
    expected = (settings.RAG_INGEST_SECRET or "").strip()
    if not expected or (x_rag_ingest_secret or "").strip() != expected:
        raise HTTPException(status_code=403, detail="Invalid or missing X-RAG-Ingest-Secret")

    doc_path = (path or settings.KNOWLEDGE_DIR_PATH or settings.KNOWLEDGE_FILE_PATH or "").strip()
    if not doc_path:
        raise HTTPException(
            status_code=400,
            detail="Set KNOWLEDGE_FILE_PATH or KNOWLEDGE_DIR_PATH, or pass ?path=/full/path/to/file-or-folder",
        )
    try:
        p = Path(doc_path)
        if p.is_dir():
            result = ingest_directory(db, doc_path)
        else:
            result = ingest_document(db, doc_path)
        return {"ok": True, **result}
    except FileNotFoundError as e:
        raise HTTPException(status_code=404, detail=str(e)) from e
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e)) from e
    except RuntimeError as e:
        raise HTTPException(status_code=503, detail=str(e)) from e
