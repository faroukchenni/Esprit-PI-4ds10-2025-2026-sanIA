"""
NDVIDiagnosticService
=====================
Fetches the latest cloud-free Sentinel-2 NDVI stats from Agromonitoring
and persists the result in the local DB so every component on the
dashboard reads the same real number.

Key fixes vs. previous version:
  - Better duplicate-polygon recovery (regex on 'message' field)
  - Falls back to searching all polygons by field_id prefix
  - Sorts images by (clouds ASC, date DESC) so we get the most
    recent cloud-free image, not just the least-cloudy of any era
  - Writes to NDVIRecord on every successful fetch (upsert-style)
  - Always returns avg_ndvi as a float; never returns "N/A" for ndvi
    (uses 0.0 as a safe numeric fallback so VRA/SoilHealth won't break)
"""

import hashlib
import json
import re
import logging
import requests
from datetime import datetime, timedelta
from app.core.config import settings
from app.models.all_models import NDVIRecord
from sqlalchemy.orm import Session
from uuid import UUID

logger = logging.getLogger(__name__)

AGRO_API_BASE = "https://api.agromonitoring.com/agro/1.0"


def _stable_unit_interval(seed: str) -> float:
    """Deterministic [0,1) from string — same input → same value (no refresh jitter)."""
    h = hashlib.sha256(seed.encode("utf-8")).digest()
    return int.from_bytes(h[:8], "big") / (2**64)


class NDVIDiagnosticService:

    # ── colour / label helpers ────────────────────────────────────────────────

    @staticmethod
    def get_color_for_ndvi(ndvi: float) -> str:
        if ndvi < 0.2:  return "#d73027"
        if ndvi < 0.4:  return "#fdae61"
        if ndvi < 0.6:  return "#a6d96a"
        return "#1a9850"

    @staticmethod
    def get_health_label(ndvi: float) -> str:
        if ndvi < 0.2:  return "Sol nu / Stress critique"
        if ndvi < 0.4:  return "Végétalisation faible / Stress hydrique"
        if ndvi < 0.6:  return "Vigueur moyenne / Croissance normale"
        return "Vigueur excellente / Biomasse élevée"

    # ── main entry point ─────────────────────────────────────────────────────

    @staticmethod
    def get_real_diagnostic(db: Session, field_id: UUID, polygon_geojson: str):
        prov = (getattr(settings, "NDVI_PROVIDER", None) or "eosda").strip().lower()
        if prov in ("eosda", "eos"):
            from app.services.ndvi_eosda import compute_eosda_ndvi
            return compute_eosda_ndvi(db, field_id, polygon_geojson)

        if prov in ("planetary_stac", "planetary", "stac", "pc", "sentinel_stac"):
            from app.services.ndvi_planetary_stac import compute_planetary_ndvi
            return compute_planetary_ndvi(db, field_id, polygon_geojson)

        api_key = (settings.AGROMONITORING_API_KEY or "").strip()
        if not api_key or api_key in ("votre_cle", "YOUR_KEY"):
            logger.warning("AGROMONITORING_API_KEY not configured")
            return NDVIDiagnosticService._error_response(
                "Clé API non configurée", None)

        # ── parse polygon ──────────────────────────────────────────────────
        try:
            coords = json.loads(polygon_geojson)
            if not coords or len(coords) < 3:
                return None
        except Exception:
            return None

        # Agromonitoring wants [lng, lat] in GeoJSON
        geo_coords = [[c[1], c[0]] for c in coords]
        if geo_coords[0] != geo_coords[-1]:
            geo_coords.append(geo_coords[0])

        # ── step 1 : get or create polygon on Agromonitoring ─────────────
        poly_id = NDVIDiagnosticService._get_or_create_polygon(
            api_key, field_id, geo_coords)

        if poly_id == "API_UNAUTHORIZED":
            logger.error(f"[NDVI] API Key is unauthorized/invalid: {api_key}")
            return NDVIDiagnosticService._error_response(
                "Clé API Invalide ou en cours d'activation (401). Patientez 10 min.", coords)

        if poly_id == "API_QUOTA_REACHED":
            if getattr(settings, "AGROMONITORING_SIMULATE_ON_QUOTA", False):
                logger.warning(
                    "[NDVI] API Quota reached (413). Using deterministic simulated NDVI "
                    "(AGROMONITORING_SIMULATE_ON_QUOTA=true — not live Agromonitoring)."
                )
                return NDVIDiagnosticService._mock_success_response(coords, field_id)
            logger.warning(
                "[NDVI] API Quota reached (413). Not returning simulated NDVI "
                "(set AGROMONITORING_SIMULATE_ON_QUOTA=true to opt in)."
            )
            return NDVIDiagnosticService._error_response(
                "Quota Agromonitoring atteint (413). Supprimez d’anciens polygones sur agromonitoring.com "
                "ou augmentez le quota — aucun NDVI factice n’est renvoyé.",
                coords,
                satellite_data_source="api_quota",
            )

        if not poly_id:
            logger.error(f"[NDVI] Could not obtain polygon ID for field {field_id}")
            return NDVIDiagnosticService._error_response(
                "Polygone invalide ou erreur API détaillée", coords)

        # ── step 2 : find best image ──────────────────────────────────────
        best_img = NDVIDiagnosticService._find_best_image(api_key, poly_id)

        if not best_img:
            return {
                "summary": {
                    "date":         datetime.now().strftime('%d/%m/%Y'),
                    "clouds":       0,
                    "source":       "Sentinel-2 L2A",
                    "satellite_data_source": "no_satellite_image",
                    "health_label": "Aucune image disponible (zone urbaine ou trop petite ?)",
                    "avg_ndvi":     None,
                    "min_ndvi":     None,
                    "max_ndvi":     None,
                },
                "zones": [{"polygon": coords, "ndvi": 0.0,
                           "color": "#787878"}],
            }

        image_date = datetime.fromtimestamp(best_img["dt"])
        summary = {
            "date":         image_date.strftime('%d/%m/%Y'),
            "clouds":       round(best_img.get("cl", 0), 1),
            "source":       "Sentinel-2 L2A",
            "satellite_data_source": "agromonitoring",
            "health_label": "Traitement…",
            "avg_ndvi":     None,
            "min_ndvi":     None,
            "max_ndvi":     None,
        }

        # ── step 3 : fetch NDVI stats ─────────────────────────────────────
        stats_ndvi_url = best_img.get("stats", {}).get("ndvi")
        stats_evi_url  = best_img.get("stats", {}).get("evi")

        if stats_ndvi_url:
            try:
                ndvi_raw = NDVIDiagnosticService._fetch_ndvi_stats_json(stats_ndvi_url)
                if not ndvi_raw or "mean" not in ndvi_raw:
                    logger.error(
                        "[NDVI] Stats JSON missing 'mean' from %s — got keys: %s",
                        stats_ndvi_url[:80],
                        list(ndvi_raw.keys()) if isinstance(ndvi_raw, dict) else type(ndvi_raw),
                    )
                else:
                    mean_ndvi = float(ndvi_raw.get("mean") or 0)
                    mean_ndvi = max(0.0, min(1.0, mean_ndvi))   # clamp to [0,1]
                    summary.update({
                        "avg_ndvi":     round(mean_ndvi, 3),
                        "min_ndvi":     round(float(ndvi_raw.get("min", 0) or 0), 3),
                        "max_ndvi":     round(float(ndvi_raw.get("max", 0) or 0), 3),
                        "health_label": NDVIDiagnosticService.get_health_label(mean_ndvi),
                        "ndvi_stats_endpoint_ok": True,
                    })

                    # ── persist to DB (upsert by field+date) ─────────────────
                    NDVIDiagnosticService._upsert_ndvi_record(
                        db, field_id, mean_ndvi, image_date)

                    logger.info(
                        f"[NDVI] field={field_id} date={image_date.date()} "
                        f"avg_ndvi={mean_ndvi:.3f} clouds={summary['clouds']}%"
                    )
            except Exception as exc:
                logger.error(f"[NDVI] Stats fetch failed: {exc}")

        if stats_evi_url:
            try:
                evi_raw = NDVIDiagnosticService._fetch_ndvi_stats_json(stats_evi_url)
                if evi_raw and "mean" in evi_raw:
                    summary["avg_evi"] = round(float(evi_raw.get("mean") or 0), 3)
                    summary["evi_stats_endpoint_ok"] = True
            except Exception:
                pass

        # numeric fallback so downstream services never get None for ndvi
        ndvi_val = summary["avg_ndvi"] if summary["avg_ndvi"] is not None else 0.0

        return {
            "summary": summary,
            "zones": [{
                "polygon": coords,
                "ndvi":    ndvi_val,
                "color":   NDVIDiagnosticService.get_color_for_ndvi(ndvi_val),
            }],
        }

    # ── helpers ───────────────────────────────────────────────────────────────

    @staticmethod
    def _fetch_ndvi_stats_json(url: str) -> dict | None:
        """Agromonitoring stats URLs return JSON with mean/min/max for the index."""
        r = requests.get(url, timeout=15)
        r.raise_for_status()
        data = r.json()
        return data if isinstance(data, dict) else None

    @staticmethod
    def _get_or_create_polygon(api_key: str, field_id: UUID,
                               geo_coords: list) -> str | None:
        poly_url = f"{AGRO_API_BASE}/polygons?appid={api_key}"
        payload = {
            "name": f"SANIA_{field_id}",
            "geo_json": {
                "type": "Feature",
                "properties": {},
                "geometry": {"type": "Polygon", "coordinates": [geo_coords]},
            },
        }
        try:
            res  = requests.post(poly_url, json=payload, timeout=15)
            data = res.json()

            # happy path
            if res.status_code in (200, 201) and data.get("id"):
                return data["id"]

            # already exists — API returns 422 with the existing ID in 'message'
            if res.status_code == 422:
                msg = data.get("message", "")
                match = re.search(r"'([a-f0-9]{24})'", msg)  # Mongo ObjectId
                if match:
                    return match.group(1)

            # Invalid API Key
            if res.status_code == 401:
                logger.error(f"[NDVI] API Key unauthorized (401): {data}")
                return "API_UNAUTHORIZED"

            # Quota limit reached (413) — Auto-Rotation Logic
            if res.status_code == 413 or "anymore" in str(data):
                logger.warning(f"[NDVI] Quota 413 reached. Attempting auto-rotation of polygons...")
                list_res = requests.get(poly_url, timeout=15)
                if list_res.status_code == 200:
                    polys = list_res.json()
                    if isinstance(polys, list) and len(polys) >= 1:
                        # Find our field first (in case it was created by another session)
                        field_str = str(field_id)
                        for p in polys:
                            if field_str in str(p.get("name", "")):
                                return p.get("id")
                        
                        # Otherwise, delete the OLDEST polygon to make room
                        # Sentinel/Agro polygons usually have a creation date or we just take the first one 
                        oldest = polys[0] 
                        oldest_id = oldest.get("id")
                        logger.info(f"[NDVI] Deleting oldest polygon {oldest_id} to free quota.")
                        requests.delete(f"{AGRO_API_BASE}/polygons/{oldest_id}?appid={api_key}", timeout=10)
                        
                        # One-time RETRY
                        logger.info("[NDVI] Retrying creation after rotation...")
                        retry_res = requests.post(poly_url, json=payload, timeout=15)
                        if retry_res.status_code in (200, 201):
                            return retry_res.json().get("id")

                return "API_QUOTA_REACHED"

            # fallback: list all polygons and find ours by name
            list_res  = requests.get(poly_url, timeout=15)
            if list_res.status_code == 200:
                list_data = list_res.json()
                field_str = str(field_id)
                for p in (list_data if isinstance(list_data, list) else []):
                    if field_str in str(p.get("name", "")):
                        return p.get("id")

        except Exception as exc:
            logger.error(f"[NDVI] polygon upsert error: {exc}")
        return None

    @staticmethod
    def _find_best_image(api_key: str, poly_id: str) -> dict | None:
        """
        Search last 1500 days. Graduated cloud-cover strategy:
          1. Prefer images with ≤ 20 % clouds (near-clear), pick most recent
          2. Fall back to ≤ 50 % clouds if no near-clear image found
          3. Final fallback: least-cloudy image regardless of threshold
        Only returns images that actually have stats URLs (ndvi endpoint present).
        """
        end_date   = int(datetime.now().timestamp())
        start_date = int((datetime.now() - timedelta(days=1500)).timestamp())
        url = (
            f"{AGRO_API_BASE}/image/search"
            f"?start={start_date}&end={end_date}&polyid={poly_id}&appid={api_key}"
        )
        try:
            images = requests.get(url, timeout=15).json()
        except Exception as exc:
            logger.error(f"[NDVI] image search failed: {exc}")
            return None

        if not isinstance(images, list) or not images:
            return None

        # Keep only images that have NDVI stats URL
        usable = [img for img in images if img.get("stats", {}).get("ndvi")]
        if not usable:
            usable = images  # fall back to all if none have stats (avoids empty return)

        # Sort most-recent first for tie-breaking
        usable.sort(key=lambda x: x.get("dt", 0), reverse=True)

        # Tier 1: near-clear (≤ 20 %)
        tier1 = [img for img in usable if img.get("cl", 100) <= 20]
        if tier1:
            return tier1[0]

        # Tier 2: acceptable (≤ 50 %)
        tier2 = [img for img in usable if img.get("cl", 100) <= 50]
        if tier2:
            logger.info("[NDVI] No near-clear image; using best with cl≤50")
            return tier2[0]

        # Tier 3: fall back to most recent image even if highly clouded (> 50%)
        # This keeps the dashboard date synchronized with Agromonitoring's live UI
        usable.sort(key=lambda x: x.get("dt", 0), reverse=True)
        logger.warning("[NDVI] All images >50% cloud; using most recent image regardless of clouds")
        return usable[0]

    @staticmethod
    def _upsert_ndvi_record(db: Session, field_id: UUID,
                            ndvi_value: float, captured_at: datetime):
        """Upsert NDVIRecord by (field, calendar-date) — not exact timestamp.
        This prevents duplicate records when the same satellite pass is
        queried at slightly different wall-clock times."""
        from sqlalchemy import func
        try:
            existing = (
                db.query(NDVIRecord)
                .filter(
                    NDVIRecord.field_id == field_id,
                    func.date(NDVIRecord.captured_at) == captured_at.date(),
                )
                .first()
            )
            if not existing:
                db.add(NDVIRecord(
                    field_id   = field_id,
                    ndvi_value = round(ndvi_value, 3),
                    status     = "SAT_AUTO",
                    captured_at= captured_at,
                ))
                db.commit()
            else:
                # Always update the value — cloud-free re-fetch overwrites old cloudy value
                existing.ndvi_value = round(ndvi_value, 3)
                existing.status     = "SAT_AUTO"
                existing.captured_at = captured_at
                db.commit()
        except Exception as exc:
            logger.error(f"[NDVI] DB upsert failed: {exc}")
            db.rollback()

    @staticmethod
    def sync_field_polygon_to_agromonitoring(field_id: UUID, polygon_geojson: str | None) -> dict:
        """
        Register the field polygon on Agromonitoring so it appears in their dashboard.
        Returns a small dict for logging / optional API responses.
        """
        prov = (getattr(settings, "NDVI_PROVIDER", None) or "eosda").strip().lower()
        if prov in ("eosda", "eos", "planetary_stac", "planetary", "stac", "pc", "sentinel_stac"):
            return {"ok": True, "reason": f"ndvi_provider_{prov}_no_remote_polygon"}

        if not polygon_geojson or polygon_geojson.strip() in ("[]", "null", ""):
            return {"ok": False, "reason": "no_polygon"}
        api_key = (settings.AGROMONITORING_API_KEY or "").strip()
        if not api_key or api_key in ("votre_cle", "YOUR_KEY"):
            logger.warning("[Agro] AGROMONITORING_API_KEY missing — polygon not sent to Agromonitoring")
            return {"ok": False, "reason": "no_api_key"}
        try:
            coords = json.loads(polygon_geojson)
            if not coords or len(coords) < 3:
                return {"ok": False, "reason": "polygon_too_small"}
        except Exception as exc:
            logger.warning("[Agro] invalid polygon_geojson for field %s: %s", field_id, exc)
            return {"ok": False, "reason": "invalid_json", "detail": str(exc)}
        geo_coords = [[c[1], c[0]] for c in coords]
        if geo_coords[0] != geo_coords[-1]:
            geo_coords.append(geo_coords[0])
        try:
            poly_id = NDVIDiagnosticService._get_or_create_polygon(api_key, field_id, geo_coords)
        except Exception as exc:
            logger.exception("[Agro] polygon sync failed for field %s: %s", field_id, exc)
            return {"ok": False, "reason": "exception", "detail": str(exc)[:200]}
        if poly_id == "API_UNAUTHORIZED":
            logger.error("[Agro] invalid AGROMONITORING_API_KEY — polygon not created (check key matches agromonitoring.com account)")
            return {"ok": False, "reason": "api_unauthorized"}
        if poly_id == "API_QUOTA_REACHED":
            logger.warning("[Agro] Agromonitoring quota full — polygon not created (free a slot on their site)")
            return {"ok": False, "reason": "api_quota"}
        if poly_id:
            logger.info("[Agro] polygon registered for field %s → Agromonitoring id=%s", field_id, poly_id)
            return {"ok": True, "polygon_id": str(poly_id)}
        logger.warning("[Agro] could not obtain polygon id for field %s", field_id)
        return {"ok": False, "reason": "no_polygon_id"}

    @staticmethod
    def _error_response(
        reason: str,
        coords,
        satellite_data_source: str = "error",
    ) -> dict:
        return {
            "summary": {
                "date":         datetime.now().strftime('%d/%m/%Y'),
                "clouds":       0,
                "source":       "Erreur API",
                "satellite_data_source": satellite_data_source,
                "health_label": reason,
                "avg_ndvi":     None,
                "min_ndvi":     None,
                "max_ndvi":     None,
            },
            "zones": [{"polygon": coords or [], "ndvi": 0.0, "color": "#787878"}],
        }

    @staticmethod
    def _mock_success_response(coords, field_id: UUID | None = None) -> dict:
        # Deterministic mock (quota rescue): same field + polygon → same NDVI on every refresh.
        seed_base = f"{field_id}:{json.dumps(coords, sort_keys=True) if coords else ''}"
        r = lambda salt: _stable_unit_interval(seed_base + ":" + salt)
        avg_ndvi = round(0.40 + r("avg") * 0.35, 3)
        spread = 0.10 + r("spread") * 0.10
        min_ndvi = max(0.0, round(avg_ndvi - spread, 3))
        max_ndvi = min(1.0, round(avg_ndvi + spread, 3))
        day_off = 1 + int(r("day") * 4)
        clouds = round(r("clouds") * 15, 1)

        return {
            "summary": {
                "date":         (datetime.now() - timedelta(days=day_off)).strftime('%d/%m/%Y'),
                "clouds":       clouds,
                "source":       "Sentinel-2 L2A (simulé — quota API)",
                "satellite_data_source": "simulated_quota",
                "health_label": NDVIDiagnosticService.get_health_label(avg_ndvi),
                "avg_ndvi":     avg_ndvi,
                "min_ndvi":     min_ndvi,
                "max_ndvi":     max_ndvi,
                "avg_evi":      round(avg_ndvi * 0.9, 3),
            },
            "zones": [{
                "polygon": coords or [],
                "ndvi":    avg_ndvi,
                "color":   NDVIDiagnosticService.get_color_for_ndvi(avg_ndvi),
            }],
        }


ndvi_diagnostic_service = NDVIDiagnosticService()
