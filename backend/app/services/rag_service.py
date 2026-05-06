"""
RAG over pgvector: ingest PDF chunks, retrieve by embedding, generate recommendations
grounded in retrieved text + structured satellite analysis.
"""
from __future__ import annotations

import json
import logging
import re
from pathlib import Path
from typing import Any
from uuid import uuid4

from openai import OpenAI
from pypdf import PdfReader
from sqlalchemy import delete, func, select
from sqlalchemy.orm import Session

from app.core.config import settings
from app.core import rag_state
from app.core.rag_state import sync_pgvector_flag
from app.models.all_models import KnowledgeChunk

logger = logging.getLogger(__name__)

_EMBED_BATCH = 64
_DEFAULT_OPENAI_V1 = "https://api.openai.com/v1"
_sentence_transformer_model = None  # lazy: sentence-transformers (free local embeddings)


def _chat_client() -> OpenAI | None:
    """LLM for recommendations (OpenAI, or Token Factory with OPENAI_BASE_URL)."""
    key = (settings.OPENAI_API_KEY or "").strip()
    if not key:
        return None
    base = (settings.OPENAI_BASE_URL or "").strip() or _DEFAULT_OPENAI_V1
    return OpenAI(
        api_key=key,
        base_url=base.rstrip("/"),
        timeout=settings.OPENAI_HTTP_TIMEOUT,
        max_retries=1,
    )


def _embed_client() -> OpenAI | None:
    """
    Embeddings for pgvector. Use OpenAI official embeddings by default.
    If chat uses Token Factory only, set OPENAI_EMBEDDING_API_KEY (and optionally
    OPENAI_EMBEDDING_BASE_URL) so ingest/search still work.
    """
    emb_key = (settings.OPENAI_EMBEDDING_API_KEY or "").strip()
    main_key = (settings.OPENAI_API_KEY or "").strip()
    key = emb_key or main_key
    if not key:
        return None
    if emb_key:
        # Separate embedding key → default to official OpenAI unless base is set explicitly
        base = (settings.OPENAI_EMBEDDING_BASE_URL or "").strip() or _DEFAULT_OPENAI_V1
    else:
        base = (
            (settings.OPENAI_EMBEDDING_BASE_URL or "").strip()
            or (settings.OPENAI_BASE_URL or "").strip()
            or _DEFAULT_OPENAI_V1
        )
    return OpenAI(
        api_key=key,
        base_url=base.rstrip("/"),
        timeout=settings.OPENAI_HTTP_TIMEOUT,
        max_retries=1,
    )


def _sentence_transformer():
    """CPU embeddings, no API cost (downloads model on first use)."""
    global _sentence_transformer_model
    if _sentence_transformer_model is None:
        from sentence_transformers import SentenceTransformer

        name = settings.LOCAL_EMBEDDING_MODEL
        logger.info(
            "Chargement du modèle d'embedding local %s (premier lancement ~ téléchargement 80 Mo)…",
            name,
        )
        _sentence_transformer_model = SentenceTransformer(name)
    return _sentence_transformer_model


def _embed_texts_local(texts: list[str]) -> list[list[float]]:
    model = _sentence_transformer()
    out: list[list[float]] = []
    for i in range(0, len(texts), _EMBED_BATCH):
        batch = texts[i : i + _EMBED_BATCH]
        emb = model.encode(
            batch,
            convert_to_numpy=True,
            show_progress_bar=False,
            normalize_embeddings=True,
        )
        for row in emb:
            vec = row.flatten().tolist()
            if len(vec) != settings.EMBEDDING_DIMENSION:
                raise RuntimeError(
                    f"Embedding local: dimension {len(vec)} != {settings.EMBEDDING_DIMENSION} "
                    f"(modèle {settings.LOCAL_EMBEDDING_MODEL})"
                )
            out.append(vec)
    return out


def embed_texts(texts: list[str]) -> list[list[float]]:
    """Batch embeddings; dimension must match KnowledgeChunk.embedding and settings.EMBEDDING_DIMENSION."""
    if (settings.EMBEDDING_BACKEND or "local") != "openai":
        return _embed_texts_local(texts)

    cli = _embed_client()
    if not cli:
        raise RuntimeError(
            "EMBEDDING_BACKEND=openai mais aucune clé: définissez OPENAI_API_KEY ou OPENAI_EMBEDDING_API_KEY, "
            "ou passez à EMBEDDING_BACKEND=local (gratuit, CPU)."
        )
    out: list[list[float]] = []
    model = settings.EMBEDDING_MODEL
    for i in range(0, len(texts), _EMBED_BATCH):
        batch = texts[i : i + _EMBED_BATCH]
        try:
            resp = cli.embeddings.create(input=batch, model=model)
        except Exception as e:
            code = getattr(e, "status_code", None) or getattr(e, "code", None)
            msg = str(e).lower()
            if code == 429 or "insufficient_quota" in msg or "rate" in type(e).__name__.lower():
                raise RuntimeError(
                    "OpenAI embeddings: quota insuffisant ou limite atteinte (429). Ce n’est pas un bug du projet.\n"
                    "Alternative sans frais: dans backend/.env mettez EMBEDDING_BACKEND=local (embeddings CPU, "
                    "sentence-transformers) puis recréez la table knowledge_chunks si vous passiez de 1536 à 384 dims.\n"
                    "Si vous restez sur OpenAI: sur https://platform.openai.com vérifiez facturation / crédits pour "
                    "la clé OPENAI_EMBEDDING_API_KEY."
                ) from e
            if code in (500, 502, 503) or "internal" in type(e).__name__.lower() or "500" in msg:
                raise RuntimeError(
                    "Embeddings API a échoué (erreur serveur). ESPRIT Token Factory fournit surtout le "
                    "chat (/v1/chat/completions), pas toujours /v1/embeddings.\n\n"
                    "Sans payer: EMBEDDING_BACKEND=local dans backend/.env (embeddings CPU).\n\n"
                    "Avec OpenAI payant: OPENAI_EMBEDDING_API_KEY=sk-... et "
                    "OPENAI_EMBEDDING_BASE_URL=https://api.openai.com/v1 ; gardez OPENAI_API_KEY + OPENAI_BASE_URL "
                    "pour Token Factory sur les réponses du modèle."
                ) from e
            raise
        # API returns in same order as input
        for item in resp.data:
            vec = list(item.embedding)
            if len(vec) != settings.EMBEDDING_DIMENSION:
                raise RuntimeError(
                    f"Embedding dim {len(vec)} != {settings.EMBEDDING_DIMENSION} "
                    f"(check EMBEDDING_MODEL / pgvector column)"
                )
            out.append(vec)
    return out


def _sanitize_for_postgres_text(text: str) -> str:
    """PostgreSQL rejects NUL bytes in strings; pypdf extraction often emits them."""
    if not text:
        return text
    return text.replace("\x00", "")


def _normalize_whitespace(text: str) -> str:
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def chunk_text(raw: str) -> list[str]:
    """Split long text into overlapping character windows."""
    raw = _normalize_whitespace(raw)
    if not raw:
        return []
    size = max(400, settings.RAG_CHUNK_CHARS)
    overlap = min(settings.RAG_CHUNK_OVERLAP, size // 3)
    chunks: list[str] = []
    start = 0
    while start < len(raw):
        end = min(start + size, len(raw))
        piece = raw[start:end].strip()
        if piece:
            chunks.append(piece)
        if end >= len(raw):
            break
        start = end - overlap
    return chunks


def extract_pdf_text(path: Path) -> str:
    reader = PdfReader(str(path))
    parts: list[str] = []
    for page in reader.pages:
        try:
            t = page.extract_text() or ""
        except Exception:
            t = ""
        if t:
            parts.append(t)
    return _sanitize_for_postgres_text("\n\n".join(parts))


def load_document_text(path: Path) -> str:
    """Load raw text from PDF, UTF-8 .txt, or .md."""
    suf = path.suffix.lower()
    if suf == ".pdf":
        return extract_pdf_text(path)
    if suf in (".txt", ".md"):
        return _sanitize_for_postgres_text(path.read_text(encoding="utf-8", errors="replace"))
    raise ValueError(f"Unsupported file type {suf!r}. Use .pdf, .txt, or .md")


def count_chunks(db: Session) -> int:
    sync_pgvector_flag()
    if not rag_state.PGVECTOR_ENABLED:
        return 0
    return int(db.scalar(select(func.count()).select_from(KnowledgeChunk)) or 0)


_CORPUS_SUFFIXES = frozenset({".pdf", ".txt", ".md"})


def iter_corpus_files(dir_path: Path, *, recursive: bool = True) -> list[Path]:
    """Supported .pdf / .txt / .md under a directory. Default: include subfolders (rglob)."""
    if not dir_path.is_dir():
        return []
    root = dir_path.resolve()
    if recursive:
        files = [
            p
            for p in root.rglob("*")
            if p.is_file() and p.suffix.lower() in _CORPUS_SUFFIXES
        ]
    else:
        files = [
            p
            for p in root.iterdir()
            if p.is_file() and p.suffix.lower() in _CORPUS_SUFFIXES
        ]
    return sorted(files, key=lambda x: str(x).lower())


def ingest_document(
    db: Session,
    file_path: str,
    *,
    source_path: str | None = None,
) -> dict[str, Any]:
    """
    Replace all chunks for this source file, re-embed, insert.
    Supports .pdf, .txt, .md (use plain text until you have real PDFs).
    """
    sync_pgvector_flag()
    if not rag_state.PGVECTOR_ENABLED:
        raise RuntimeError(
            "PostgreSQL n'a pas l'extension pgvector. Utilisez une base avec pgvector "
            "(ex. Docker: pgvector/pgvector) ou installez pgvector sur Windows."
        )
    path = Path(file_path)
    if not path.is_file():
        raise FileNotFoundError(f"File not found: {path}")

    try:
        text = _sanitize_for_postgres_text(load_document_text(path))
    except ValueError as e:
        raise ValueError(str(e)) from e

    if not text or len(text) < 50:
        raise ValueError("Not enough text (empty file, or PDF is image-only?)")

    chunks = chunk_text(text)
    if not chunks:
        raise ValueError("No chunks produced from document text")

    # Unique key in DB: basename for a single file, or relative path when ingesting from a folder tree
    source = _sanitize_for_postgres_text((source_path or path.name).strip() or path.name)
    if len(source) > 500:
        source = source[-500:]
    db.execute(delete(KnowledgeChunk).where(KnowledgeChunk.source_path == source))
    db.commit()

    vectors = embed_texts(chunks)
    if len(vectors) != len(chunks):
        raise RuntimeError("Embedding count mismatch")

    for idx, (content, emb) in enumerate(zip(chunks, vectors)):
        row = KnowledgeChunk(
            id=uuid4(),
            source_path=source,
            chunk_index=idx,
            content=_sanitize_for_postgres_text(content),
            embedding=emb,
        )
        db.add(row)
    db.commit()
    return {"source": source, "chunks": len(chunks), "chars": len(text), "format": path.suffix.lower()}


def ingest_directory(db: Session, dir_path: str) -> dict[str, Any]:
    """
    Ingest every .pdf, .txt, .md under a folder (including subfolders). Each file replaces its own chunks.
    """
    root = Path(dir_path).resolve()
    if not root.is_dir():
        raise FileNotFoundError(f"Directory not found: {root}")
    paths = iter_corpus_files(root, recursive=True)
    if not paths:
        raise ValueError(
            f"Aucun fichier .pdf, .txt ou .md dans {root} (y compris sous-dossiers). "
            f"Copiez vos PDF dans ce dossier puis relancez."
        )

    results: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    total_chunks = 0
    for p in paths:
        rel_key = p.resolve().relative_to(root).as_posix()
        try:
            one = ingest_document(db, str(p.resolve()), source_path=rel_key)
            results.append(one)
            total_chunks += int(one.get("chunks", 0))
        except (FileNotFoundError, ValueError, RuntimeError) as e:
            db.rollback()
            errors.append({"file": rel_key, "error": str(e)})

    return {
        "directory": str(root.resolve()),
        "files_ok": len(results),
        "files_failed": len(errors),
        "total_chunks": total_chunks,
        "results": results,
        "errors": errors,
    }


def ingest_pdf(db: Session, file_path: str) -> dict[str, Any]:
    """Backward-compatible alias for ingest_document."""
    return ingest_document(db, file_path)


def search_chunks(db: Session, query: str, k: int | None = None) -> list[KnowledgeChunk]:
    k = k or settings.RAG_TOP_K
    q = _normalize_whitespace(query)
    if not q:
        return []
    vec = embed_texts([q])[0]
    distance = KnowledgeChunk.embedding.cosine_distance(vec)
    return list(
        db.scalars(select(KnowledgeChunk).order_by(distance).limit(k))
    )


def slim_satellite_for_llm(payload: dict[str, Any]) -> dict[str, Any]:
    """Reduce token use: keep actionable structure, drop heavy geometry."""
    ndvi = payload.get("ndvi_summary") or {}
    soil = payload.get("soil_health") or {}
    cal = payload.get("crop_calendar") or {}
    vra = payload.get("vra_map") or {}

    zones = vra.get("zones") or []
    zones_slim = [
        {
            "id": z.get("id"),
            "label": z.get("label") or z.get("label_fr"),
            "area_pct": z.get("area_pct"),
            "interpretation": z.get("interpretation"),
        }
        for z in zones[:6]
    ]

    ndve = cal.get("ndvi_vs_expected") or {}

    return {
        "field_name": payload.get("field_name"),
        "crop_type": payload.get("crop_type"),
        "area_ha": payload.get("area_ha"),
        "ndvi_summary": {
            "avg_ndvi": ndvi.get("avg_ndvi"),
            "health_label": ndvi.get("health_label"),
            "health_score": ndvi.get("health_score"),
            "date": ndvi.get("date"),
            "clouds": ndvi.get("clouds"),
        },
        "soil_health": {
            "health_score": soil.get("health_score"),
            "health_label": soil.get("health_label"),
            "moisture_stress": soil.get("moisture_stress"),
            "fertility_class": soil.get("fertility_class"),
            "indicators": soil.get("indicators"),
            "recommendations": soil.get("recommendations"),
        },
        "crop_calendar": {
            "current_stage": (cal.get("current_stage") or {}).get("name_fr")
            or (cal.get("current_stage") or {}).get("name"),
            "season_progress_pct": cal.get("season_progress_pct"),
            "ndvi_vs_expected": {
                "current_ndvi": ndve.get("current_ndvi"),
                "expected_ndvi": ndve.get("expected_ndvi"),
                "assessment": ndve.get("assessment"),
            },
        },
        "vra_map": {
            "avg_ndvi": vra.get("avg_ndvi"),
            "savings_vs_uniform_pct": vra.get("savings_vs_uniform_pct"),
            "zones": zones_slim,
        },
    }


def build_retrieval_query(payload: dict[str, Any]) -> str:
    """Natural-language query for embedding (contextual retrieval)."""
    slim = slim_satellite_for_llm(payload)
    crop = slim.get("crop_type") or "culture"
    ndvi = slim.get("ndvi_summary") or {}
    soil = slim.get("soil_health") or {}
    cal = slim.get("crop_calendar") or {}
    parts = [
        f"Conseils agricoles pour {crop}.",
        f"NDVI moyen: {ndvi.get('avg_ndvi')}.",
        f"Santé sol / stress hydrique: {soil.get('moisture_stress')}, fertilité: {soil.get('fertility_class')}.",
        f"Stade: {cal.get('current_stage')}.",
        (cal.get("ndvi_vs_expected") or {}).get("assessment") or "",
    ]
    return " ".join(p for p in parts if p)


def recommend(db: Session, satellite_payload: dict[str, Any]) -> dict[str, Any]:
    """
    Retrieve knowledge chunks + call LLM with structured field metrics.
    """
    sync_pgvector_flag()
    if not rag_state.PGVECTOR_ENABLED:
        return {
            "ok": False,
            "error": "RAG désactivé : PostgreSQL sans extension pgvector. "
            "Démarrez une base avec pgvector (recommandé : Docker pgvector/pgvector:pg15 sur le port 5433) "
            "et mettez à jour DATABASE_URL dans backend/.env.",
            "indexed_chunks": 0,
        }
    if not _chat_client():
        return {
            "ok": False,
            "error": "OPENAI_API_KEY manquante sur le serveur — ajoutez-la dans backend/.env (requis pour générer les recommandations).",
            "indexed_chunks": count_chunks(db),
            "fix_steps": [
                "Dans backend/.env : OPENAI_API_KEY=… (et éventuellement OPENAI_BASE_URL si vous utilisez Token Factory).",
                "Redémarrez uvicorn après modification.",
            ],
        }

    n_idx = count_chunks(db)
    if n_idx == 0:
        return {
            "ok": False,
            "error": (
                "Aucun document indexé pour le RAG — ce bloc est indépendant du satellite / Agromonitoring. "
                "Il faut au moins un PDF ou fichier texte ingéré dans PostgreSQL (pgvector)."
            ),
            "indexed_chunks": 0,
            "fix_steps": [
                "Dans backend/.env : définir KNOWLEDGE_FILE_PATH (un fichier) ou KNOWLEDGE_DIR_PATH (dossier contenant .pdf / .txt / .md).",
                "Depuis le dossier backend : python -m scripts.ingest_rag C:\\chemin\\vers\\fichier.pdf (ou sans argument si les variables ci-dessus pointent déjà vers un fichier/dossier).",
                "Ou appeler POST /api/v1/rag/ingest avec l’en-tête X-RAG-Ingest-Secret égal à RAG_INGEST_SECRET (défini dans .env).",
            ],
        }

    q = build_retrieval_query(satellite_payload)
    hits = search_chunks(db, q, k=settings.RAG_TOP_K)
    context_blocks = []
    sources = []
    for h in hits:
        snippet = h.content[:900] + ("…" if len(h.content) > 900 else "")
        context_blocks.append(f"[{h.source_path} #chunk{h.chunk_index}]\n{h.content}")
        sources.append(
            {
                "source_path": h.source_path,
                "chunk_index": h.chunk_index,
                "preview": snippet,
            }
        )

    context_text = "\n\n---\n\n".join(context_blocks)
    slim_json = json.dumps(slim_satellite_for_llm(satellite_payload), ensure_ascii=False, indent=2)

    system = """Tu es un conseiller agricole pour un exploitant. Tu reçois:
1) Des extraits de documentation de référence (contexte ci-dessous).
2) Des indicateurs satellite et de parcelle au format JSON.

Règles:
- Base tes conseils PRIORITAIREMENT sur les extraits; utilise les métriques pour les adapter à la parcelle.
- Si un point n'est pas couvert par les extraits, dis-le clairement et donne seulement des vérifications prudentes (pas de doses inventées).
- Réponds en français, de façon concise et actionnable (puces).
- Termine par une phrase de non-responsabilité: les décisions restent à l'exploitant / agronome local."""

    user = f"""Documentation (extraits):\n{context_text}\n\n---\n\nDonnées parcelle (JSON):\n{slim_json}\n\nProduis:
- Un court paragraphe de synthèse (2-4 phrases).
- 4 à 6 recommandations numérotées (actions concrètes).
- 1 ligne \"À vérifier sur le terrain\"."""

    cli = _chat_client()
    assert cli is not None
    try:
        chat = cli.chat.completions.create(
            model=settings.LLM_MODEL,
            temperature=0.35,
            messages=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        )
        text = (chat.choices[0].message.content or "").strip()
    except Exception as e:
        logger.exception("LLM recommend failed: %s", e)
        return {
            "ok": False,
            "error": (
                "Le modèle de langage (Token Factory / OpenAI) n’a pas répondu à temps ou a renvoyé une erreur. "
                f"Détail: {str(e)[:350]}"
            ),
            "indexed_chunks": n_idx,
            "sources": sources[: settings.RAG_TOP_K],
        }

    return {
        "ok": True,
        "text": text,
        "sources": sources[: settings.RAG_TOP_K],
        "indexed_chunks": n_idx,
        "model": settings.LLM_MODEL,
        "disclaimer": "Les recommandations sont indicatives et ne remplacent pas un diagnostic sur le terrain ni la réglementation locale.",
    }
