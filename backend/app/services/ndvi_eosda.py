"""
EOSDA API Connect — NDVI / EVI / MSI statistics for a field polygon.

Flow:
  1. Cache check — if a fresh NDVIRecord exists (< CACHE_MAX_AGE_H hours old), serve it directly.
  2. POST  /api/gdw/api?api_key=...   → { task_id }
  3. GET   /api/gdw/api/{task_id}?api_key=... — poll with exponential back-off.
     On 429 we wait longer; on done we parse and return.

Reference: https://doc.eos.com/docs/statistics/vegetation-indices-analytics/
"""
from __future__ import annotations

import json
import logging
import threading
import time
from datetime import datetime, timedelta, timezone
from uuid import UUID

import requests
from sqlalchemy.orm import Session

from app.core.config import settings
from app.models.all_models import NDVIRecord

logger = logging.getLogger(__name__)

EOSDA_BASE        = "https://api-connect.eos.com/api/gdw/api"
CACHE_FRESH_H     = 24    # hours — "fresh" threshold; older records are served but also refresh in bg
CACHE_STALE_DAYS  = 30    # days — never go to EOSDA if we have data this new
POLL_INITIAL_S    = 20    # wait before first poll attempt (EOSDA tasks take ~15-30s)
POLL_BASE_S       = 12    # base interval between polls
POLL_MAX_S        = 60    # cap per interval
POLL_MAX_TOTAL_S  = 240   # give up after 4 min total poll time

# Global lock: EOSDA free tier allows ≈1 GET/10s per key.
# Serialising all field polls through one lock means we never send concurrent GETs.
# After the first fetch per field the cache makes every subsequent call instant.
_EOSDA_POLL_LOCK = threading.Lock()


def _parse_coords(polygon_geojson: str) -> list[list[float]] | None:
    try:
        coords = json.loads(polygon_geojson)
        return coords if coords and len(coords) >= 3 else None
    except Exception:
        return None


def _sania_to_geojson_ring(coords: list[list[float]]) -> list[list[float]]:
    """Sania stores [lat, lng]; EOSDA GeoJSON needs [lng, lat]."""
    ring = [[c[1], c[0]] for c in coords]
    if ring[0] != ring[-1]:
        ring.append(ring[0])
    return ring


def _best_scene(results: list[dict]) -> dict | None:
    """Pick the most-recent, least-cloudy scene that has NDVI stats."""
    usable = [
        r for r in results
        if isinstance(r.get("indexes"), dict)
        and r["indexes"].get("NDVI", {}).get("average") is not None
    ]
    if not usable:
        return None
    # Primary: most-recent date; secondary: lowest cloud cover
    usable.sort(key=lambda r: (r.get("date", "") or "", -(r.get("cloud", 99) or 0)), reverse=True)
    return usable[0]


def _upsert(db: Session, field_id: UUID, ndvi: float, image_date: datetime) -> None:
    from sqlalchemy import func
    try:
        existing = (
            db.query(NDVIRecord)
            .filter(
                NDVIRecord.field_id == field_id,
                func.date(NDVIRecord.captured_at) == image_date.date(),
            )
            .first()
        )
        if existing:
            existing.ndvi_value = round(ndvi, 3)
            existing.status = "SAT_AUTO"
            existing.captured_at = image_date
        else:
            db.add(NDVIRecord(
                field_id=field_id,
                ndvi_value=round(ndvi, 3),
                status="SAT_AUTO",
                captured_at=image_date,
            ))
        db.commit()
    except Exception as exc:
        logger.error("[EOSDA] DB upsert failed: %s", exc)
        db.rollback()


def _latest_db_record(db: Session, field_id: UUID) -> NDVIRecord | None:
    """Return the most-recent SAT_AUTO NDVIRecord for this field, regardless of age."""
    try:
        return (
            db.query(NDVIRecord)
            .filter(NDVIRecord.field_id == field_id, NDVIRecord.status == "SAT_AUTO")
            .order_by(NDVIRecord.captured_at.desc())
            .first()
        )
    except Exception:
        return None


def _record_to_response(rec: NDVIRecord, coords: list[list[float]]) -> dict:
    """Build the standard NDVI response dict from a DB record."""
    from app.services.ndvi_diagnostic import NDVIDiagnosticService
    ndvi = float(rec.ndvi_value)
    image_date = rec.captured_at
    if hasattr(image_date, "tzinfo") and image_date.tzinfo is not None:
        image_date = image_date.replace(tzinfo=None)
    now = datetime.now()
    age_h = (now - image_date).total_seconds() / 3600
    summary = {
        "date":   image_date.strftime("%d/%m/%Y"),
        "clouds": 0.0,
        "source": "Sentinel-2 L2A (EOSDA)",
        "satellite_data_source": "eosda",
        "health_label": NDVIDiagnosticService.get_health_label(ndvi),
        "avg_ndvi": round(ndvi, 3),
        "min_ndvi": None,
        "max_ndvi": None,
        "cached": True,
        "cache_age_h": round(age_h, 1),
    }
    return {
        "summary": summary,
        "zones": [{
            "polygon": coords,
            "ndvi":    round(ndvi, 3),
            "color":   NDVIDiagnosticService.get_color_for_ndvi(ndvi),
        }],
    }


def compute_eosda_ndvi(
    db: Session,
    field_id: UUID,
    polygon_geojson: str,
) -> dict | None:
    """
    Returns the same response shape as the other NDVI providers.

    Priority:
      1. Fresh DB cache (< CACHE_MAX_AGE_H h) → no API call
      2. EOSDA API call with exponential back-off polling
    """
    from app.services.ndvi_diagnostic import NDVIDiagnosticService

    api_key = (getattr(settings, "EOSDA_API_KEY", None) or "").strip()
    if not api_key:
        return NDVIDiagnosticService._error_response(
            "EOSDA_API_KEY manquante dans backend/.env.",
            None,
            satellite_data_source="error",
        )

    coords = _parse_coords(polygon_geojson)
    if not coords:
        return None

    # ── 1. Cache check ────────────────────────────────────────────────────────
    # Strategy:
    #   a. If a record exists and is < CACHE_STALE_DAYS old → serve it immediately
    #      (avoids any API call; user can click "Actualiser" to force fresh data)
    #   b. If NO record exists at all → call EOSDA now (blocking; first-time only)
    existing = _latest_db_record(db, field_id)
    if existing is not None:
        age_days = (datetime.now() - existing.captured_at.replace(tzinfo=None)).days \
            if hasattr(existing.captured_at, "tzinfo") \
            else (datetime.now() - existing.captured_at).days
        if age_days < CACHE_STALE_DAYS:
            age_h = round((datetime.now() - existing.captured_at.replace(tzinfo=None)).total_seconds() / 3600, 1) \
                if hasattr(existing.captured_at, "tzinfo") \
                else round((datetime.now() - existing.captured_at).total_seconds() / 3600, 1)
            logger.info("[EOSDA] serving DB cache for field=%s ndvi=%.3f age=%.1fh",
                        field_id, existing.ndvi_value, age_h)
            return _record_to_response(existing, coords)

    # ── 2. Build request payload ──────────────────────────────────────────────
    ring = _sania_to_geojson_ring(coords)
    end_dt   = datetime.now(timezone.utc)
    lookback = int(getattr(settings, "NDVI_STAC_LOOKBACK_DAYS", 120))
    start_dt = end_dt - timedelta(days=lookback)

    payload = {
        "type": "mt_stats",
        "params": {
            "bm_type":    ["NDVI", "EVI", "MSI"],
            "date_start": start_dt.date().isoformat(),
            "date_end":   end_dt.date().isoformat(),
            "geometry":   {"type": "Polygon", "coordinates": [ring]},
            "sensors":    ["sentinel2"],
            "reference":  f"SANIA_{field_id}",
        },
    }

    # ── 3. Create task (POST) ─────────────────────────────────────────────────
    try:
        r = requests.post(
            f"{EOSDA_BASE}?api_key={api_key}",
            json=payload,
            timeout=30,
        )
        r.raise_for_status()
        task_data = r.json()
    except Exception as exc:
        logger.exception("[EOSDA] Task creation failed: %s", exc)
        return NDVIDiagnosticService._error_response(
            f"EOSDA: creation de tache impossible — {str(exc)[:200]}",
            coords,
            satellite_data_source="error",
        )

    task_id = task_data.get("task_id")
    if not task_id:
        logger.error("[EOSDA] No task_id in response: %s", task_data)
        return NDVIDiagnosticService._error_response(
            f"EOSDA: pas de task_id — {str(task_data)[:200]}",
            coords,
            satellite_data_source="error",
        )

    logger.info("[EOSDA] task created: %s (field=%s) — waiting for lock", task_id, field_id)

    # ── 4. Poll with global serialisation lock + exponential back-off ─────────
    # Only ONE field may poll at a time. EOSDA free tier ≈ 1 GET/10s per key.
    # After the first successful fetch, future calls return from DB cache instantly.
    with _EOSDA_POLL_LOCK:
        # Second cache check: another request for this same field may have
        # completed and been saved to DB while we were waiting to acquire the lock.
        existing2 = _latest_db_record(db, field_id)
        if existing2 is not None:
            logger.info("[EOSDA] cache hit (after lock) for field=%s", field_id)
            return _record_to_response(existing2, coords)

        # Give EOSDA server time to process the task before the first poll.
        time.sleep(POLL_INITIAL_S)

        result_data   = None
        interval      = POLL_BASE_S
        total_waited  = POLL_INITIAL_S
        attempt       = 0

        while total_waited < POLL_MAX_TOTAL_S:
            attempt += 1
            try:
                gr = requests.get(
                    f"{EOSDA_BASE}/{task_id}?api_key={api_key}",
                    timeout=30,
                )
                if gr.status_code == 429:
                    retry_after = int(gr.headers.get("Retry-After", interval))
                    wait = max(retry_after + 2, interval)
                    logger.warning(
                        "[EOSDA] 429 on attempt %d — backing off %ds", attempt, wait
                    )
                    time.sleep(wait)
                    total_waited += wait
                    interval = min(interval * 2, POLL_MAX_S)
                    continue

                gr.raise_for_status()
                result_data = gr.json()
            except Exception as exc:
                logger.warning("[EOSDA] poll attempt %d failed: %s — waiting %ds", attempt, exc, interval)
                time.sleep(interval)
                total_waited += interval
                interval = min(interval * 2, POLL_MAX_S)
                continue

            status = result_data.get("status", "")
            if status in ("running", "created", "pending", ""):
                logger.debug("[EOSDA] attempt %d: status=%s, waiting %ds", attempt, status, interval)
                time.sleep(interval)
                total_waited += interval
                continue

            # Task done
            break
        else:
            return NDVIDiagnosticService._error_response(
                "EOSDA: delai d'attente depasse (4 min). Reessayez dans quelques instants.",
                coords,
                satellite_data_source="error",
            )

    if result_data is None:
        return NDVIDiagnosticService._error_response(
            "EOSDA: aucune reponse recue.",
            coords,
            satellite_data_source="error",
        )

    # ── 5. Parse result ───────────────────────────────────────────────────────
    api_errors  = result_data.get("errors") or []
    raw_results = result_data.get("result") or []

    if api_errors and not raw_results:
        logger.error("[EOSDA] API errors: %s", api_errors)
        return NDVIDiagnosticService._error_response(
            f"EOSDA erreur: {str(api_errors)[:200]}",
            coords,
            satellite_data_source="error",
        )

    if not raw_results:
        return {
            "summary": {
                "date":   datetime.now().strftime("%d/%m/%Y"),
                "clouds": 0.0,
                "source": "Sentinel-2 L2A (EOSDA)",
                "satellite_data_source": "no_satellite_image",
                "health_label": (
                    "Aucune image Sentinel-2 sur la periode demandee "
                    "(nuages / zone hors couverture)."
                ),
                "avg_ndvi": None,
                "min_ndvi": None,
                "max_ndvi": None,
            },
            "zones": [{"polygon": coords, "ndvi": 0.0, "color": "#787878"}],
        }

    scene = _best_scene(raw_results)
    if not scene:
        return {
            "summary": {
                "date":   datetime.now().strftime("%d/%m/%Y"),
                "clouds": 0.0,
                "source": "Sentinel-2 L2A (EOSDA)",
                "satellite_data_source": "no_satellite_image",
                "health_label": (
                    "Images trouvees mais aucun NDVI calculable "
                    "(polygone trop petit ou entierement masque)."
                ),
                "avg_ndvi": None,
                "min_ndvi": None,
                "max_ndvi": None,
            },
            "zones": [{"polygon": coords, "ndvi": 0.0, "color": "#787878"}],
        }

    ndvi_idx = scene["indexes"].get("NDVI", {})
    evi_idx  = scene["indexes"].get("EVI",  {})

    avg_ndvi = float(ndvi_idx.get("average") or 0)
    min_ndvi = float(ndvi_idx.get("min")     or 0)
    max_ndvi = float(ndvi_idx.get("max")     or 0)
    avg_evi  = (
        float(evi_idx.get("average") or 0)
        if evi_idx.get("average") is not None else None
    )

    avg_ndvi = max(-1.0, min(1.0, avg_ndvi))
    min_ndvi = max(-1.0, min(1.0, min_ndvi))
    max_ndvi = max(-1.0, min(1.0, max_ndvi))

    scene_date_str = scene.get("date") or ""
    try:
        image_date = datetime.strptime(scene_date_str, "%Y-%m-%d")
    except Exception:
        image_date = datetime.now()

    cloud = float(scene.get("cloud") or 0)

    _upsert(db, field_id, avg_ndvi, image_date)

    logger.info(
        "[EOSDA] field=%s ndvi=%.3f clouds=%.1f%% date=%s (total_wait=%ds)",
        field_id, avg_ndvi, cloud, scene_date_str, total_waited,
    )

    summary = {
        "date":   image_date.strftime("%d/%m/%Y"),
        "clouds": round(cloud, 1),
        "source": "Sentinel-2 L2A (EOSDA)",
        "satellite_data_source": "eosda",
        "health_label": NDVIDiagnosticService.get_health_label(avg_ndvi),
        "avg_ndvi": round(avg_ndvi, 3),
        "min_ndvi": round(min_ndvi, 3),
        "max_ndvi": round(max_ndvi, 3),
        "ndvi_extra": {
            "q1":     ndvi_idx.get("q1"),
            "q3":     ndvi_idx.get("q3"),
            "median": ndvi_idx.get("median"),
            "std":    ndvi_idx.get("std"),
        },
    }
    if avg_evi is not None:
        summary["avg_evi"] = round(avg_evi, 3)

    return {
        "summary": summary,
        "zones": [{
            "polygon": coords,
            "ndvi":    round(avg_ndvi, 3),
            "color":   NDVIDiagnosticService.get_color_for_ndvi(avg_ndvi),
        }],
    }
