"""
VRA Router — Variable Rate Application + Soil Health + Crop Calendar
Pillar 2: Satellite Data → Actionable Intelligence
"""
from fastapi import APIRouter, Body, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session
from uuid import UUID

from ..db.session import get_db
from ..models.all_models import Field, User, UserRole
from .deps import get_current_active_user
from ..services.vra_service import generate_vra_map, get_soil_health, get_crop_calendar
from ..services.ndvi_diagnostic import ndvi_diagnostic_service
from ..services.rag_service import recommend as rag_recommend

router = APIRouter()


def _assemble_full_analysis(field: Field, db: Session, ndvi_data: dict | None) -> dict:
    """Shared payload for full-analysis and RAG (single satellite call upstream)."""
    vra_map = generate_vra_map(db, field, ndvi_data)
    soil_health = get_soil_health(db, field, ndvi_data)
    crop_calendar = get_crop_calendar(db, field, ndvi_data)

    diag_sum = (ndvi_data or {}).get("summary", {})

    sat_src = diag_sum.get("satellite_data_source")
    if not sat_src and ndvi_data:
        # Legacy responses without flag: infer from source label
        src = (diag_sum.get("source") or "").lower()
        if "simulé" in src or "simule" in src or "quota" in src:
            sat_src = "simulated_quota"
        elif "erreur" in src:
            sat_src = "error"
        elif "planetary" in src or "stac" in src:
            sat_src = "planetary_stac"
        elif "eosda" in src or "eos" in src:
            sat_src = "eosda"
        elif diag_sum.get("avg_ndvi") is not None:
            sat_src = "agromonitoring"

    avg_ndvi = diag_sum.get("avg_ndvi")
    # Do not substitute VRA/DB neutral defaults when the live API explicitly failed (avoids showing 0.35 as "truth")
    if avg_ndvi is None and sat_src not in ("error", "api_quota"):
        avg_ndvi = vra_map.get("avg_ndvi")

    ndvi_summary = {
        "avg_ndvi": avg_ndvi,
        "health_label": diag_sum.get("health_label") or soil_health.get("health_label"),
        "health_score": soil_health.get("health_score"),
        "date": diag_sum.get("date"),
        "clouds": diag_sum.get("clouds"),
        "source": diag_sum.get("source", "Sentinel-2 L2A"),
        "satellite_data_source": sat_src,
        # Honesty flags: live NDVI from Agromonitoring or Planetary STAC; VRA grid is modeled; SAVI/MSAVI from NDVI;
        # VRA grid is always a local model; SAVI/MSAVI are formulas from NDVI (not separate API bands).
        "vra_overlay_is_synthetic_grid": True,
        "soil_savi_msavi_computed_from_ndvi": True,
        "ndvi_label": (
            "Excellent" if (avg_ndvi or 0) >= 0.6 else
            "Bon" if (avg_ndvi or 0) >= 0.4 else
            "Modéré" if (avg_ndvi or 0) >= 0.2 else
            "Faible"
        ) if avg_ndvi is not None else "N/A",
        "live_vs_history_note": (
            "Le NDVI affiché en tête vient de la dernière analyse satellite (voir source). "
            "Le graphique d’historique lit les NDVI stockés en base (démo / anciens relevés) "
            "et peut ne pas coïncider avec ce chiffre."
        ),
    }

    return {
        "field_id": str(field.id),
        "field_name": field.name,
        "crop_type": field.crop_type,
        "area_ha": field.area_ha,
        "ndvi_summary": ndvi_summary,
        "ndvi_diagnostic": ndvi_data,
        "vra_map": vra_map,
        "soil_health": soil_health,
        "crop_calendar": crop_calendar,
    }


def _get_field_authorized(field_id: UUID, db: Session, current_user: User) -> Field:
    """Shared helper: fetch field and check ownership."""
    field = db.query(Field).filter(Field.id == field_id).first()
    if not field:
        raise HTTPException(status_code=404, detail="Field not found")
    if current_user.role == UserRole.FARMER and field.farm_id != current_user.farm_id:
        raise HTTPException(status_code=403, detail="Forbidden")
    return field


def _fetch_ndvi_data(field: Field, db: Session) -> dict | None:
    """Fetch live NDVI diagnostic from satellite API (best-effort)."""
    if not field.polygon_geojson or field.polygon_geojson == "[]":
        return None
    try:
        return ndvi_diagnostic_service.get_real_diagnostic(db, field.id, field.polygon_geojson)
    except Exception:
        return None


# ─── VRA Map ──────────────────────────────────────────────────────────────────

@router.get("/{field_id}/map")
def get_vra_map(
    field_id: UUID,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
):
    """
    Generate a Variable Rate Application (VRA) prescription map for the field.

    Returns three management zones (High / Medium / Low need) with:
    - Zone area and percentage of total field
    - Fertilizer prescription (N, P, K in kg)
    - Water prescription (m³)
    - Application rate percentage
    - Estimated input savings vs uniform application
    """
    field = _get_field_authorized(field_id, db, current_user)
    ndvi_data = _fetch_ndvi_data(field, db)
    return generate_vra_map(db, field, ndvi_data)


# ─── Soil Health ──────────────────────────────────────────────────────────────

@router.get("/{field_id}/soil-health")
def get_field_soil_health(
    field_id: UUID,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
):
    """
    Compute soil health indicators for the field.

    Returns:
    - NDVI, SAVI, MSAVI vegetation indices
    - EVI-based moisture stress index
    - Soil fertility classification
    - Overall health score (0–100) with label
    - Actionable recommendations
    """
    field = _get_field_authorized(field_id, db, current_user)
    ndvi_data = _fetch_ndvi_data(field, db)
    return get_soil_health(db, field, ndvi_data)


# ─── Crop Calendar ────────────────────────────────────────────────────────────

@router.get("/{field_id}/crop-calendar")
def get_field_crop_calendar(
    field_id: UUID,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
):
    """
    Generate an AI-driven crop calendar for the field.

    Returns:
    - Current growth stage name and recommended action
    - NDVI vs expected NDVI assessment
    - Full season timeline with status (completed / current / upcoming)
    - Upcoming critical actions (next 3 stages)
    - Overall season progress percentage
    """
    field = _get_field_authorized(field_id, db, current_user)
    ndvi_data = _fetch_ndvi_data(field, db)
    return get_crop_calendar(db, field, ndvi_data)


# ─── Combined Full Analysis ───────────────────────────────────────────────────

@router.get("/{field_id}/full-analysis")
def get_full_satellite_analysis(
    field_id: UUID,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
):
    """
    Full Pillar 2 satellite analysis in a single call (mobile-optimized).

    Returns NDVI diagnostic + VRA map + soil health + crop calendar combined.
    The satellite API is called exactly ONCE and the result is shared across
    all sub-services to avoid redundant round-trips.
    Also exposes a flat `ndvi_summary` block at the top level for easy
    Dashboard consumption (avg_ndvi, health_label, date, clouds).
    """
    field = _get_field_authorized(field_id, db, current_user)
    ndvi_data = _fetch_ndvi_data(field, db)
    return _assemble_full_analysis(field, db, ndvi_data)


# ─── RAG recommendations (pgvector + LLM) ───────────────────────────────────────


class RecommendRequest(BaseModel):
    """Optional: pass the same object as GET /full-analysis to avoid a second satellite API call."""
    analysis: dict | None = None


@router.post("/{field_id}/recommendations")
def post_field_rag_recommendations(
    field_id: UUID,
    db: Session = Depends(get_db),
    current_user: User = Depends(get_current_active_user),
    body: RecommendRequest | None = Body(default=None),
):
    """
    Satellite metrics + retrieval from indexed PDFs → grounded advice in French.
    Requires OPENAI_API_KEY and ingested chunks (POST /rag/ingest).

    Prefer sending `analysis` (full-analysis JSON) in the body so Agromonitoring is not called twice.
    """
    field = _get_field_authorized(field_id, db, current_user)
    req = body or RecommendRequest()
    if req.analysis:
        p = req.analysis
        return rag_recommend(db, p)
    ndvi_data = _fetch_ndvi_data(field, db)
    payload = _assemble_full_analysis(field, db, ndvi_data)
    return rag_recommend(db, payload)
