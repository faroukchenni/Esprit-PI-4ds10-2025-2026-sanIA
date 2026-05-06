"""
SanIA Irrigation Agent Service  —  v4.0
=========================================
Loads the trained XGBoost + Platt pipeline from Models/irrigation/
and makes real-time irrigation decisions from raw sensor readings.

Feature engineering is reproduced exactly as in the notebook:
  • SMD        = (FC - moisture) / (FC - WP)
  • Kc         = FAO-56 Table 12 crop coefficient by age
  • ETc        = ET0 × Kc   (ET0 from weather API or Penman-Monteith estimate)
  • 7-day lags = per-field rolling buffer (in-memory; lost on restart)
  • OHE crop   = 4 binary columns (apple / grape / potato / tomato)

Dual-threshold policy (locked from 2024 val calibration):
  ACT  → irrigate (production operating point, F1-optimal recall≥0.70)
  WARN → irrigate (early warning, highest threshold still recall≥0.85)
  SKIP → do not irrigate

Rain guard: if rain_mm_24h > ETc → skip regardless of model score.
"""
import math
import json
import logging
from collections import deque
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import joblib
import numpy as np
import requests
import xgboost as xgb

from app.schemas.smart_irrigation import (
    AgentSensorReading,
    AgentDecisionResponse,
    IrrigationStatusZone,
)

logger = logging.getLogger(__name__)

# ── Artifact location (relative to backend/ root) ────────────────────────────
ARTIFACT_DIR = Path(__file__).resolve().parents[3] / "Models" / "irrigation"

# ── Open-Meteo endpoint (free, no API key) ────────────────────────────────────
OPEN_METEO_URL = (
    "https://api.open-meteo.com/v1/forecast"
    "?latitude=36.81&longitude=10.18"
    "&daily=precipitation_sum,temperature_2m_max,et0_fao_evapotranspiration"
    "&timezone=Africa%2FTunis&forecast_days=2"
)

# ── FAO-56 Kc stages (must match notebook CELL 1) ────────────────────────────
KC_STAGES = {
    "tomato": {"ini": 0.60, "mid": 1.15, "end": 0.80, "d_ini": 30, "d_dev": 40, "d_mid": 40, "d_late": 25},
    "potato": {"ini": 0.50, "mid": 1.15, "end": 0.75, "d_ini": 25, "d_dev": 30, "d_mid": 45, "d_late": 30},
    "apple":  {"ini": 0.60, "mid": 1.20, "end": 0.75, "d_ini": 20, "d_dev": 70, "d_mid": 90, "d_late": 30},
    "grape":  {"ini": 0.30, "mid": 0.85, "end": 0.45, "d_ini": 20, "d_dev": 40, "d_mid": 120, "d_late": 60},
}

# Zone name → crop name (for OHE)
ZONE_TO_CROP = {
    "Potato": "potato", "potato": "potato",
    "Tomato": "tomato", "tomato": "tomato",
    "Apple":  "apple",  "apple":  "apple",
    "Grape":  "grape",  "grape":  "grape",
}


def _get_kc(crop: str, age_days: int) -> float:
    s = KC_STAGES[crop]
    d1, d2, d3 = s["d_ini"], s["d_dev"], s["d_mid"]
    if age_days <= d1:
        return s["ini"]
    elif age_days <= d1 + d2:
        t = (age_days - d1) / d2
        return s["ini"] + t * (s["mid"] - s["ini"])
    elif age_days <= d1 + d2 + d3:
        return s["mid"]
    return s["end"]


class IrrigationAgentService:
    """
    Singleton-style service. Loaded once at FastAPI startup via
    `app.state.irrigation_agent = IrrigationAgentService()`.
    """

    def __init__(self):
        self._model:    Optional[xgb.XGBClassifier] = None
        self._scaler    = None
        self._platt     = None
        self._meta:     dict = {}
        self._features: List[str] = []
        self._continuous: List[str] = []
        self._cont_idx: List[int] = []
        self._warn_thr: float = 0.35
        self._act_thr:  float = 0.50
        self._version:  str = "unknown"

        # Per-field rolling history: field_id → deque[(SMD, temperature_C)]
        self._history: Dict[str, deque] = {}

        # Last decision per field (for /status endpoint)
        self._last_decisions: Dict[str, IrrigationStatusZone] = {}

        self._load_artifacts()

    # ── Artifact loading ──────────────────────────────────────────────────────

    def _load_artifacts(self):
        xgb_path  = ARTIFACT_DIR / "xgb_champion.json"
        meta_path = ARTIFACT_DIR / "model_meta.json"

        if not xgb_path.exists() or not meta_path.exists():
            logger.error(
                "Irrigation model artifacts not found at %s — "
                "run Smart_Irrigation_v4_0_Production.ipynb first.", ARTIFACT_DIR
            )
            return

        try:
            self._model = xgb.XGBClassifier()
            self._model.load_model(str(xgb_path))
            self._scaler  = joblib.load(ARTIFACT_DIR / "scaler_temporal.pkl")
            self._platt   = joblib.load(ARTIFACT_DIR / "platt_calibrator.pkl")
            with open(meta_path) as f:
                self._meta = json.load(f)

            self._features   = self._meta["feature_cols"]
            self._continuous = self._meta["continuous_feats"]
            self._cont_idx   = [self._features.index(f) for f in self._continuous]
            self._warn_thr   = float(self._meta["warn_threshold"])
            self._act_thr    = float(self._meta["act_threshold"])
            self._version    = self._meta.get("model_version", "SanIA-v4.0")
            logger.info("Irrigation artifacts loaded | version=%s", self._version)
        except Exception as exc:
            logger.error("Failed to load irrigation artifacts: %s", exc)

    @property
    def is_ready(self) -> bool:
        return self._model is not None

    # ── Weather context (Open-Meteo) ──────────────────────────────────────────

    def _fetch_weather(self) -> dict:
        """Fetch today + tomorrow forecast from Open-Meteo. Returns {} on failure."""
        try:
            resp = requests.get(OPEN_METEO_URL, timeout=8)
            resp.raise_for_status()
            data = resp.json()["daily"]
            return {
                "rain_mm_24h":        float(data["precipitation_sum"][0] or 0),
                "rain_mm_48h":        float(data["precipitation_sum"][1] or 0),
                "avg_temp_24h":       float(data["temperature_2m_max"][0] or 25),
                "et0_forecast_mm":    float(data["et0_fao_evapotranspiration"][0] or 5),
            }
        except Exception as exc:
            logger.warning("Open-Meteo fetch failed: %s", exc)
            return {}

    # ── Per-field history buffer ──────────────────────────────────────────────

    def _get_buffer(self, field_id: str) -> deque:
        if field_id not in self._history:
            self._history[field_id] = deque(maxlen=7)
        return self._history[field_id]

    def _get_lags(self, field_id: str, current_smd: float, current_temp: float) -> Tuple[List[float], List[float]]:
        """Return 7 SMD lags and 7 temperature lags.

        When history is short, pad with a linear ramp from 0 → current_smd so the
        model sees a realistic *rising* depletion trend instead of a flat line.
        A flat-padded history produces near-zero irrigation probabilities because
        the model was trained on sequences with natural daily variation.
        """
        buf = list(self._get_buffer(field_id))  # oldest first, up to 7 readings
        n_missing = 7 - len(buf)
        for j in range(n_missing):
            # Ramp: oldest synthetic reading starts near 0, rises to current_smd
            fraction = j / max(n_missing, 1)
            padded_smd = round(current_smd * fraction, 4)
            buf.insert(0, (padded_smd, current_temp))
        # buf[-1] = most recent previous, buf[-7] = oldest
        smd_lags  = [buf[-(i)][0] for i in range(1, 8)]  # lag_1 = yesterday, ..., lag_7
        temp_lags = [buf[-(i)][1] for i in range(1, 8)]
        return smd_lags, temp_lags

    def _push_reading(self, field_id: str, smd: float, temp: float):
        buf = self._get_buffer(field_id)
        buf.append((smd, temp))

    # ── Core irrigation volume estimate ──────────────────────────────────────

    def _compute_volume_m3(
        self, req: AgentSensorReading, smd: float, efficiency_pct: float
    ) -> float:
        """
        Gross irrigation volume using FAO-56 root zone water balance:
          RAW = (FC - WP) × depletion_p × root_zone_depth × 1000  [mm]
          NET  = SMD × (FC - WP) × root_zone_depth × 1000           [mm]
          Gross = NET / (efficiency% / 100)
          Volume = Gross × area / 1000                               [m³]
        """
        fc = req.field_capacity_pct / 100.0
        wp = req.wilting_point_pct  / 100.0
        taw = (fc - wp) * req.root_zone_depth_m * 1000.0  # mm of available water
        net_mm  = float(np.clip(smd, 0.0, 1.0)) * taw
        gross_mm = net_mm / (efficiency_pct / 100.0)
        volume_m3 = gross_mm * req.area_m2 / 1000.0
        return round(max(volume_m3, 0.0), 2)

    # ── Main decision method ──────────────────────────────────────────────────

    def make_decision(
        self, req: AgentSensorReading, crop_name: Optional[str] = None
    ) -> AgentDecisionResponse:
        if not self.is_ready:
            return AgentDecisionResponse(
                irrigate=False, confidence=0.0, volume_m3=0.0,
                decision_label="ERROR",
                reason="Model not loaded — run the notebook first.",
                warn_threshold=self._warn_thr, act_threshold=self._act_thr,
                model_version=self._version, lag_features_used=0
            )

        # ── Resolve crop name ─────────────────────────────────────────────────
        crop = ZONE_TO_CROP.get(crop_name or req.field_id.replace("twin_", ""), "tomato")

        # ── Feature engineering ───────────────────────────────────────────────
        fc  = req.field_capacity_pct
        wp  = req.wilting_point_pct
        taw = fc - wp
        smd = float(np.clip((fc - req.soil_moisture_pct) / taw, 0.0, 1.2)) if taw > 0 else 0.0

        kc  = _get_kc(crop, req.crop_age_days)

        # Weather for ET0 / rain — prefer Digital Twin physics when provided so
        # rain_mm and ETc match what the field was actually "rained on" in sim.
        weather = self._fetch_weather()
        et0_mm = (
            float(req.twin_et0_mm)
            if req.twin_et0_mm >= 0.0
            else weather.get("et0_forecast_mm", 5.0)
        )
        etc_mm = round(et0_mm * kc, 3)
        rain_24h = (
            float(req.twin_rain_mm_24h)
            if req.twin_rain_mm_24h >= 0.0
            else weather.get("rain_mm_24h", 0.0)
        )

        twin_wx = req.twin_rain_mm_24h >= 0.0 or req.twin_et0_mm >= 0.0

        # Rain guard
        if rain_24h > etc_mm:
            self._push_reading(req.field_id, smd, req.temperature_C)
            self._last_decisions[req.field_id] = IrrigationStatusZone(
                field_id=req.field_id, decision_label="RAIN_GUARD",
                confidence=0.0, irrigate=False, volume_m3=0.0
            )
            weather_ctx = {
                **weather,
                "et0_mm_used": et0_mm,
                "rain_mm_used": rain_24h,
                "twin_override": twin_wx,
            }
            return AgentDecisionResponse(
                irrigate=False, confidence=0.0, volume_m3=0.0,
                decision_label="RAIN_GUARD",
                reason=f"Rain guard: {rain_24h:.1f} mm > ETc {etc_mm:.1f} mm",
                warn_threshold=self._warn_thr, act_threshold=self._act_thr,
                model_version=self._version,
                lag_features_used=len(self._get_buffer(req.field_id)),
                weather_context=weather_ctx
            )

        # Lags
        smd_lags, temp_lags = self._get_lags(req.field_id, smd, req.temperature_C)
        n_lags = min(len(self._get_buffer(req.field_id)), 7)

        # OHE
        crop_ohe = {
            "crop_apple":  1 if crop == "apple"  else 0,
            "crop_grape":  1 if crop == "grape"  else 0,
            "crop_potato": 1 if crop == "potato" else 0,
            "crop_tomato": 1 if crop == "tomato" else 0,
        }

        # Build feature row in exact ALL_FEATS order
        # Merge twin context into weather_context for logging / UI
        weather = {
            **weather,
            "et0_mm_used": et0_mm,
            "rain_mm_used": rain_24h,
            "twin_override": twin_wx,
        }

        row = {
            "SMD":           smd,
            "Kc":            kc,
            "ETc":           etc_mm,
            "et0_mm":        et0_mm,
            "temperature_C": req.temperature_C,
            "humidity_pct":  req.humidity_pct,
            "rain_mm":       rain_24h,
            "crop_age_days": req.crop_age_days,
        }
        for i, (s, t) in enumerate(zip(smd_lags, temp_lags), start=1):
            row[f"SMD_lag_{i}"]  = s
            row[f"temp_lag_{i}"] = t
        row.update(crop_ohe)

        # Assemble and scale
        vals = np.array([[row.get(f, 0.0) for f in self._features]], dtype=float)
        vals[:, self._cont_idx] = self._scaler.transform(vals[:, self._cont_idx])

        # Inference
        raw = self._model.predict_proba(vals)[:, 1]
        cal = float(self._platt.predict_proba(raw.reshape(-1, 1))[:, 1][0])

        # Weather probability adjustment (mild, documented)
        reason_parts = []
        if weather.get("avg_temp_24h", 25) > 38:
            cal = min(cal * 1.05, 0.99)
            reason_parts.append("heatwave boost +5%")
        if weather.get("rain_mm_48h", 0) > 5:
            cal = max(cal * 0.90, 0.0)
            reason_parts.append("rain forecast -10%")

        # Decision — warn_thr > act_thr so check WARN first to avoid dead branch
        if cal >= self._warn_thr:
            label, irrigate = "WARN", True
        elif cal >= self._act_thr:
            label, irrigate = "IRRIGATE", True
        else:
            label, irrigate = "SKIP", False

        volume = self._compute_volume_m3(req, smd, req.application_efficiency_pct) if irrigate else 0.0
        reason_base = (
            f"SMD={smd:.3f} ETc={etc_mm:.2f}mm Kc={kc:.2f} "
            f"P(irr)={cal:.3f} thr_act={self._act_thr:.3f} thr_warn={self._warn_thr:.3f}"
        )
        reason = reason_base + (" | " + ", ".join(reason_parts) if reason_parts else "")

        # Store history and last decision
        self._push_reading(req.field_id, smd, req.temperature_C)
        self._last_decisions[req.field_id] = IrrigationStatusZone(
            field_id=req.field_id, decision_label=label,
            confidence=round(cal, 4), irrigate=irrigate, volume_m3=volume
        )

        return AgentDecisionResponse(
            irrigate=irrigate,
            confidence=round(cal, 4),
            volume_m3=volume,
            decision_label=label,
            reason=reason,
            warn_threshold=self._warn_thr,
            act_threshold=self._act_thr,
            model_version=self._version,
            lag_features_used=n_lags,
            weather_context=weather if weather else None
        )

    def get_status(self) -> list:
        return list(self._last_decisions.values())
