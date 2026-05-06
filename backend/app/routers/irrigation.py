"""
SanIA Irrigation Router  —  v4.0
FastAPI router mounted at /api/v1/irrigation

Endpoints:
  POST /agent/decision   — raw sensor reading → irrigation decision (auth required)
  GET  /status           — last decision per field (auth required)
  GET  /health           — liveness check (no auth)

The heavy lifting lives in IrrigationAgentService (services/irrigation_agent.py).
The service instance is created once at startup and stored in app.state.
"""
from fastapi import APIRouter, Depends, HTTPException, Request, status

from app.routers.deps import get_current_active_user
from app.schemas.smart_irrigation import (
    AgentSensorReading,
    AgentDecisionResponse,
    IrrigationStatusResponse,
    IrrigationStatusZone,
)

router = APIRouter(prefix="/irrigation", tags=["Irrigation"])


def _get_agent(request: Request):
 """Retrieve (or lazily initialize) the IrrigationAgentService singleton."""
 agent = getattr(request.app.state, "irrigation_agent", None)
 if agent is None:
  try:
   # Lazy init keeps service startup fast for PaaS healthchecks while
   # still enabling irrigation features when first requested.
   from app.services.irrigation_agent import IrrigationAgentService
   agent = IrrigationAgentService()
   request.app.state.irrigation_agent = agent
  except Exception as exc:
   raise HTTPException(
    status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
    detail=f"Irrigation agent failed to initialize: {exc}"
   )
 return agent


# ── Health ────────────────────────────────────────────────────────────────────

@router.get("/health", summary="Irrigation service liveness")
def irrigation_health(request: Request):
    agent = _get_agent(request)
    return {
        "status": "ok" if agent.is_ready else "degraded",
        "model_ready": agent.is_ready,
        "model_version": agent._version,
    }


# ── Decision ─────────────────────────────────────────────────────────────────

@router.post(
    "/agent/decision",
    response_model=AgentDecisionResponse,
    summary="Get irrigation decision from raw sensor reading",
)
def agent_decision(
    reading: AgentSensorReading,
    request: Request,
    current_user=Depends(get_current_active_user),
):
    """
    Accepts a raw sensor/Digital-Twin reading and returns a dual-threshold
    irrigation decision (IRRIGATE | WARN | SKIP | RAIN_GUARD).

    Feature engineering (SMD, ETc, lags, OHE) is performed server-side.
    Open-Meteo weather context is fetched automatically for rain guard.
    """
    agent = _get_agent(request)

    # Extract crop name from field_id convention (twin_tomato → tomato)
    crop_hint = reading.field_id.replace("twin_", "").split("_")[0]

    try:
        decision = agent.make_decision(reading, crop_name=crop_hint)
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Agent error: {exc}"
        )

    return decision


# ── Status snapshot ──────────────────────────────────────────────────────────

@router.get(
    "/status",
    response_model=IrrigationStatusResponse,
    summary="Latest irrigation decision per field",
)
def irrigation_status(
    request: Request,
    current_user=Depends(get_current_active_user),
):
    """
    Returns the most recent irrigation decision for each field that has
    been queried since server startup. Used by Unity Digital Twin for
    periodic status polling without pushing a new sensor reading.
    """
    agent = _get_agent(request)
    zones = agent.get_status()
    return IrrigationStatusResponse(
        zones=zones,
        model_version=agent._version
    )
