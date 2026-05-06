"""
Sentinel-2 L2A NDVI from Microsoft Planetary Computer (STAC) — no API key, no Agromonitoring.

Uses public COGs (B04 red, B08 NIR), clips to parcel polygon, computes NDVI mean/min/max.
Fair-use: depends on Planetary Computer availability; first request can be slow (COG read).
"""
from __future__ import annotations

import json
import logging
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import UUID

import numpy as np
from dateutil import parser as date_parser
from sqlalchemy.orm import Session

from app.core.config import settings
from app.models.all_models import NDVIRecord

logger = logging.getLogger(__name__)

STAC_URL = "https://planetarycomputer.microsoft.com/api/stac/v1"
COLLECTION = "sentinel-2-l2a"


def _parse_polygon_coords(polygon_geojson: str) -> list[list[float]] | None:
    try:
        coords = json.loads(polygon_geojson)
        if not coords or len(coords) < 3:
            return None
        return coords
    except Exception:
        return None


def _ring_to_geojson_polygon(coords: list[list[float]]) -> dict[str, Any]:
    """App stores [lat, lng]; GeoJSON needs [lng, lat]."""
    ring = [[c[1], c[0]] for c in coords]
    if ring[0] != ring[-1]:
        ring.append(ring[0])
    return {"type": "Polygon", "coordinates": [ring]}


def _compute_ndvi_stats(
    red: np.ndarray,
    nir: np.ndarray,
) -> tuple[float, float, float] | None:
    red = red.astype(np.float64)
    nir = nir.astype(np.float64)
    # Parentheses around the threshold comparison are critical — without them
    # Python evaluates `& (red + nir)` before `> 1e-6`, causing a
    # "bitwise_and not supported for float64" error.
    valid = np.isfinite(red) & np.isfinite(nir) & ((red + nir) > 1e-6)
    valid = valid & (red > 0) & (nir > 0)
    if not np.any(valid):
        return None
    ndvi = (nir[valid] - red[valid]) / (nir[valid] + red[valid] + 1e-9)
    ndvi = np.clip(ndvi, -1.0, 1.0)
    return float(np.nanmean(ndvi)), float(np.nanmin(ndvi)), float(np.nanmax(ndvi))


def compute_planetary_ndvi(
    db: Session,
    field_id: UUID,
    polygon_geojson: str,
) -> dict[str, Any] | None:
    """
    Returns same structure as NDVIDiagnosticService.get_real diagnostic success path.
    """
    from app.services.ndvi_diagnostic import NDVIDiagnosticService

    coords = _parse_polygon_coords(polygon_geojson)
    if not coords:
        return None

    try:
        import planetary_computer as pc  # noqa: WPS433
        import pystac_client
        import rasterio
        from rasterio.mask import mask as rio_mask
        from rasterio.warp import transform_geom
    except ImportError as e:
        logger.error("NDVI STAC deps missing: %s. pip install pystac-client planetary-computer rasterio shapely pyproj", e)
        return NDVIDiagnosticService._error_response(
            "Paquets NDVI manquants (pystac-client, planetary-computer, rasterio, shapely). "
            "Voir backend/requirements.txt.",
            coords,
            satellite_data_source="error",
        )

    geom = _ring_to_geojson_polygon(coords)
    end = datetime.now(timezone.utc)
    start = end - timedelta(days=int(getattr(settings, "NDVI_STAC_LOOKBACK_DAYS", 120)))

    try:
        catalog = pystac_client.Client.open(STAC_URL)
        search = catalog.search(
            collections=[COLLECTION],
            intersects=geom,
            datetime=f"{start.date().isoformat()}/{end.date().isoformat()}",
            limit=50,
        )
        items = list(search.items())
    except Exception as e:
        logger.exception("[STAC] search failed: %s", e)
        return NDVIDiagnosticService._error_response(
            f"Recherche STAC impossible: {str(e)[:200]}",
            coords,
            satellite_data_source="error",
        )

    if not items:
        return {
            "summary": {
                "date": datetime.now().strftime("%d/%m/%Y"),
                "clouds": 0.0,
                "source": "Sentinel-2 L2A (Planetary Computer)",
                "satellite_data_source": "no_satellite_image",
                "health_label": "Aucune image Sentinel-2 récente pour ce polygone (élargir la période ou vérifier la zone).",
                "avg_ndvi": None,
                "min_ndvi": None,
                "max_ndvi": None,
            },
            "zones": [{"polygon": coords, "ndvi": 0.0, "color": "#787878"}],
        }

    def _cloud(it: Any) -> float:
        return float((getattr(it, "properties", None) or {}).get("eo:cloud_cover", 99))

    def _ts(it: Any) -> float:
        ds = (getattr(it, "properties", None) or {}).get("datetime") or ""
        try:
            return date_parser.isoparse(str(ds).replace("Z", "+00:00")).timestamp()
        except Exception:
            return 0.0

    # 1. Prefer near-clear images (< 20% clouds) — pick most recent among them
    clear = [it for it in items if _cloud(it) < 20]
    if clear:
        chosen = max(clear, key=_ts)
    else:
        # 2. Accept up to 50% clouds — pick most recent
        acceptable = [it for it in items if _cloud(it) < 50]
        if acceptable:
            chosen = max(acceptable, key=_ts)
        else:
            # 3. Last resort: pick the least cloudy image available
            chosen = min(items, key=_cloud)
    try:
        signed = pc.sign_item(chosen)
    except Exception as e:
        logger.exception("[STAC] sign_item failed: %s", e)
        return NDVIDiagnosticService._error_response(
            "Impossible de signer les assets Planetary Computer.",
            coords,
            satellite_data_source="error",
        )

    assets = signed.assets
    b04 = assets.get("B04") or assets.get("b04")
    b08 = assets.get("B08") or assets.get("b08")
    if not b04 or not b08:
        return NDVIDiagnosticService._error_response(
            "Scène sans bandes B04/B08.",
            coords,
            satellite_data_source="error",
        )

    red_href = b04.href
    nir_href = b08.href
    geom_4326 = geom

    def _open_raster(href: str):
        try:
            return rasterio.open(href)
        except Exception:
            return rasterio.open(f"/vsicurl/{href}")

    try:
        with _open_raster(red_href) as src_red:
            g_red = transform_geom("EPSG:4326", src_red.crs, geom_4326)
            red_data, _ = rio_mask(src_red, [g_red], crop=True, nodata=0)
            red2d = red_data[0]

        with _open_raster(nir_href) as src_nir:
            g_nir = transform_geom("EPSG:4326", src_nir.crs, geom_4326)
            nir_data, _ = rio_mask(src_nir, [g_nir], crop=True, nodata=0)
            nir2d = nir_data[0]

        if red2d.shape != nir2d.shape:
            h = min(red2d.shape[0], nir2d.shape[0])
            w = min(red2d.shape[1], nir2d.shape[1])
            red2d = red2d[:h, :w]
            nir2d = nir2d[:h, :w]

        stats = _compute_ndvi_stats(red2d, nir2d)
        if stats is None:
            return {
                "summary": {
                    "date": datetime.now().strftime("%d/%m/%Y"),
                    "clouds": float(
                        (getattr(chosen, "properties", None) or {}).get("eo:cloud_cover", 0) or 0
                    ),
                    "source": "Sentinel-2 L2A (Planetary Computer)",
                    "satellite_data_source": "no_satellite_image",
                    "health_label": "Pixels invalides ou masqués sur la parcelle (nuages / bord).",
                    "avg_ndvi": None,
                    "min_ndvi": None,
                    "max_ndvi": None,
                },
                "zones": [{"polygon": coords, "ndvi": 0.0, "color": "#787878"}],
            }

        mean_ndvi, min_ndvi, max_ndvi = stats
        mean_ndvi = float(np.clip(mean_ndvi, -1.0, 1.0))
        min_ndvi = float(np.clip(min_ndvi, -1.0, 1.0))
        max_ndvi = float(np.clip(max_ndvi, -1.0, 1.0))

        props = getattr(chosen, "properties", None) or {}
        cloud = float(props.get("eo:cloud_cover", 0) or 0)
        dt_str = props.get("datetime") or ""
        image_date = end
        try:
            if "T" in str(dt_str):
                image_date = datetime.fromisoformat(str(dt_str).replace("Z", "+00:00"))
            else:
                image_date = datetime.fromisoformat(str(dt_str) + "T00:00:00+00:00")
        except Exception:
            pass

        summary = {
            "date": image_date.strftime("%d/%m/%Y") if hasattr(image_date, "strftime") else datetime.now().strftime("%d/%m/%Y"),
            "clouds": round(cloud, 1),
            "source": "Sentinel-2 L2A (Planetary Computer)",
            "satellite_data_source": "planetary_stac",
            "health_label": NDVIDiagnosticService.get_health_label(mean_ndvi),
            "avg_ndvi": round(mean_ndvi, 3),
            "min_ndvi": round(min_ndvi, 3),
            "max_ndvi": round(max_ndvi, 3),
        }

        NDVIDiagnosticService._upsert_ndvi_record(db, field_id, mean_ndvi, image_date)

        logger.info(
            "[STAC] field=%s ndvi=%.3f clouds=%.1f%%",
            field_id,
            mean_ndvi,
            cloud,
        )

        ndvi_val = summary["avg_ndvi"]
        return {
            "summary": summary,
            "zones": [
                {
                    "polygon": coords,
                    "ndvi": ndvi_val,
                    "color": NDVIDiagnosticService.get_color_for_ndvi(ndvi_val),
                }
            ],
        }
    except Exception as e:
        logger.exception("[STAC] raster read / NDVI failed: %s", e)
        # Try to serve the last known value from DB rather than a blank error
        last = (
            db.query(NDVIRecord)
            .filter(NDVIRecord.field_id == field_id)
            .order_by(NDVIRecord.captured_at.desc())
            .first()
        )
        if last and last.ndvi_value is not None:
            ndvi_cached = round(float(last.ndvi_value), 3)
            date_str = last.captured_at.strftime("%d/%m/%Y") if last.captured_at else datetime.now().strftime("%d/%m/%Y")
            return {
                "summary": {
                    "date": date_str,
                    "clouds": 0.0,
                    "source": "Sentinel-2 L2A (Planetary Computer — valeur cache)",
                    "satellite_data_source": "planetary_stac",
                    "health_label": NDVIDiagnosticService.get_health_label(ndvi_cached),
                    "avg_ndvi": ndvi_cached,
                    "min_ndvi": None,
                    "max_ndvi": None,
                },
                "zones": [{"polygon": coords, "ndvi": ndvi_cached, "color": NDVIDiagnosticService.get_color_for_ndvi(ndvi_cached)}],
            }
        return NDVIDiagnosticService._error_response(
            f"Lecture satellite échouée: {str(e)[:220]}",
            coords,
            satellite_data_source="error",
        )
