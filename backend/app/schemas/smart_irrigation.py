"""
SanIA v4.0 — Irrigation Schemas
Pydantic models used by the irrigation router.
"""
from typing import List, Optional
from pydantic import BaseModel, Field


class AgentSensorReading(BaseModel):
    """
    Raw sensor reading from a field or Digital Twin zone.
    The backend computes feature engineering (SMD, ETc, lags) from this.
    """
    field_id:                   str   = Field(..., description="Unique field identifier")
    soil_type:                  str   = Field(..., description="Soil type: Sandy Loam | Loam | Silt Loam")
    crop_age_days:              int   = Field(..., ge=0,    description="Days since planting")
    temperature_C:              float = Field(...,          description="Air temperature °C")
    humidity_pct:               float = Field(..., ge=0.0, le=100.0, description="Relative humidity %")
    soil_moisture_pct:          float = Field(..., ge=0.0, le=100.0, description="Volumetric soil moisture %")
    field_capacity_pct:         float = Field(..., ge=0.0, le=100.0, description="Field capacity %")
    wilting_point_pct:          float = Field(..., ge=0.0, le=100.0, description="Permanent wilting point %")
    area_m2:                    float = Field(10000.0, gt=0, description="Field area m²")
    root_zone_depth_m:          float = Field(0.40,   gt=0, description="Effective root zone depth m")
    application_efficiency_pct: float = Field(85.0, ge=50.0, le=100.0, description="Irrigation system efficiency %")

    class Config:
        json_schema_extra = {
            "example": {
                "field_id": "twin_tomato",
                "soil_type": "Sandy Loam",
                "crop_age_days": 65,
                "temperature_C": 38.5,
                "humidity_pct": 28.0,
                "soil_moisture_pct": 22.0,
                "field_capacity_pct": 38.0,
                "wilting_point_pct": 14.0,
                "area_m2": 10000.0,
                "root_zone_depth_m": 0.35,
                "application_efficiency_pct": 85.0,
            }
        }


class AgentDecisionResponse(BaseModel):
    """
    Irrigation decision returned by the agent endpoint.
    """
    irrigate:           bool  = Field(..., description="True → activate irrigation")
    confidence:         float = Field(..., ge=0.0, le=1.0, description="Calibrated P(irrigate)")
    volume_m3:          float = Field(..., ge=0.0, description="Gross irrigation volume if irrigating (m³)")
    decision_label:     str   = Field(..., description="IRRIGATE | WARN | SKIP | RAIN_GUARD")
    reason:             str   = Field(..., description="Human-readable decision explanation")
    warn_threshold:     float = Field(..., description="WARN threshold used")
    act_threshold:      float = Field(..., description="ACT threshold used")
    model_version:      str   = Field(..., description="Model version identifier")
    lag_features_used:  int   = Field(..., description="Number of historical readings available for lag features")
    weather_context:    Optional[dict] = Field(None, description="Open-Meteo weather context if available")


class IrrigationStatusZone(BaseModel):
    """Last known irrigation decision for one zone/field."""
    field_id:       str
    decision_label: str
    confidence:     float
    irrigate:       bool
    volume_m3:      float


class IrrigationStatusResponse(BaseModel):
    """GET /status — snapshot of all field decisions."""
    zones: List[IrrigationStatusZone]
    model_version: str
