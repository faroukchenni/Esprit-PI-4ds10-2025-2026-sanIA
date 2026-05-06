"""Whether pgvector is available (updated from DB via sync_pgvector_flag)."""

from __future__ import annotations

PGVECTOR_ENABLED: bool = True


def sync_pgvector_flag() -> bool:
    """
    Re-read from PostgreSQL whether extension `vector` is installed.
    Call after startup and before RAG routes so the flag matches the actual DB (avoids stale false).
    """
    global PGVECTOR_ENABLED
    from sqlalchemy import text

    from app.db.session import engine

    try:
        with engine.connect() as conn:
            ok = bool(
                conn.execute(
                    text("SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector')")
                ).scalar()
            )
        PGVECTOR_ENABLED = ok
        return ok
    except Exception:
        PGVECTOR_ENABLED = False
        return False
