"""
SanIA Irrigation Prediction Service  —  v4.0
Run with:  uvicorn serve_irrigation:app --host 0.0.0.0 --port 8001
"""
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
import numpy as np, joblib, json, xgboost as xgb
from pathlib import Path

ARTIFACT_DIR = Path(__file__).parent / "artifacts"

# ── Load artifacts once at startup ───────────────────────────────────────────
model = xgb.XGBClassifier()
model.load_model(str(ARTIFACT_DIR / "xgb_champion.json"))
scaler  = joblib.load(ARTIFACT_DIR / "scaler_temporal.pkl")
platt   = joblib.load(ARTIFACT_DIR / "platt_calibrator.pkl")
meta    = json.loads((ARTIFACT_DIR / "model_meta.json").read_text())
FEATURES   = meta["feature_cols"]
CONTINUOUS = meta["continuous_feats"]
WARN_THR   = meta["warn_threshold"]
ACT_THR    = meta["act_threshold"]
CONT_IDX   = [FEATURES.index(f) for f in CONTINUOUS]

app = FastAPI(title="SanIA Irrigation API", version="4.0")

class IrrigationRequest(BaseModel):
    # Core — must match ALL_FEATS order from feature engineering
    SMD:           float = Field(..., ge=0.0, le=1.2)
    Kc:            float = Field(..., ge=0.0, le=1.5)
    ETc:           float = Field(..., ge=0.0, le=15.0)
    et0_mm:        float = Field(..., ge=0.0, le=12.0)
    temperature_C: float = Field(...)
    humidity_pct:  float = Field(..., ge=0.0, le=100.0)
    rain_mm:       float = Field(0.0, ge=0.0)
    crop_age_days: int   = Field(..., ge=0)
    # 7-day rolling SMD lags
    SMD_lag_1: float = 0.0
    SMD_lag_2: float = 0.0
    SMD_lag_3: float = 0.0
    SMD_lag_4: float = 0.0
    SMD_lag_5: float = 0.0
    SMD_lag_6: float = 0.0
    SMD_lag_7: float = 0.0
    # 7-day rolling temperature lags
    temp_lag_1: float = 0.0
    temp_lag_2: float = 0.0
    temp_lag_3: float = 0.0
    temp_lag_4: float = 0.0
    temp_lag_5: float = 0.0
    temp_lag_6: float = 0.0
    temp_lag_7: float = 0.0
    # Crop one-hot — set exactly one to 1
    crop_apple:  int = 0
    crop_grape:  int = 0
    crop_potato: int = 0
    crop_tomato: int = 0

class IrrigationResponse(BaseModel):
    irrigate:               bool
    confidence:             float
    threshold_used:         str
    rain_guard_triggered:   bool
    warn_threshold:         float
    act_threshold:          float

@app.get("/api/v1/health")
def health():
    return {"status": "ok", "model_version": meta["model_version"]}

@app.post("/api/v1/irrigation/predict", response_model=IrrigationResponse)
def predict(req: IrrigationRequest):
    # Rain guard — hard override
    if req.rain_mm > req.ETc:
        return IrrigationResponse(
            irrigate=False, confidence=0.0,
            threshold_used="rain_guard", rain_guard_triggered=True,
            warn_threshold=WARN_THR, act_threshold=ACT_THR
        )

    row  = {f: getattr(req, f, 0.0) for f in FEATURES}
    vals = np.array([[row[f] for f in FEATURES]], dtype=float)
    vals[:, CONT_IDX] = scaler.transform(vals[:, CONT_IDX])
    X_s = vals
    raw = model.predict_proba(X_s)[:, 1]
    cal = float(platt.predict_proba(raw.reshape(-1, 1))[:, 1][0])

    if cal >= ACT_THR:
        decision, label = True,  "act"
    elif cal >= WARN_THR:
        decision, label = True,  "warn"
    else:
        decision, label = False, "skip"

    return IrrigationResponse(
        irrigate=decision, confidence=round(cal, 4),
        threshold_used=label, rain_guard_triggered=False,
        warn_threshold=WARN_THR, act_threshold=ACT_THR
    )