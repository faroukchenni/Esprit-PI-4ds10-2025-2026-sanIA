"""Patch all markdown cells in the notebook — header, interpretations, model card."""
import json, sys
sys.stdout.reconfigure(encoding='utf-8')
from pathlib import Path

NB_PATH = Path(__file__).resolve().parent.parent / "irrigation" / "Smart_Irrigation_v4_0_Production.ipynb"

with open(NB_PATH, "r", encoding="utf-8") as f:
    nb = json.load(f)

CELLS = nb["cells"]

# ─────────────────────────────────────────────────────────────────────────────
# [0] HEADER
# ─────────────────────────────────────────────────────────────────────────────
CELLS[0]["source"] = ["""\
# SanIA v4.0 — Smart Irrigation Intelligence

| | |
|---|---|
| **Institution** | ESPRIT School of Engineering — Tunisia — PIDEV 4th Year |
| **Project** | SanIA — Smart Agriculture Intelligence Agent |
| **Version** | 4.0 Production (TDSP Methodology) |
| **Dataset** | NASA POWER FAO-56 Synthetic — Tunisia 2020–2026 |
| **Pipeline** | XGBoost + SHAP + Platt Calibration + Autonomous Irrigation Agent |

---

## Pipeline Overview

| Phase | Name | What it does |
|:-----:|------|-------------|
| **0** | Data Ingestion | Load the pre-built dataset (2020–2026), verify structure |
| **1** | Feature Engineering | Build SMD, Kc, ETc, 7-day rolling lags, one-hot encode crops |
| **2** | Modeling | Train XGBoost, explain with SHAP, calibrate probabilities, tune thresholds |
| **3** | Drift Monitoring | PSI year-over-year to detect when the model needs retraining |
| **4** | Autonomous Agent | Simulate 14-day irrigation decisions on a real field window |
| **5** | Deployment | Export artifacts, write FastAPI microservice, smoke test |
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [3] Phase 0 markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[3]["source"] = ["""\
---
## Phase 0 — Data Ingestion

Load the unified dataset rebuilt from scratch using a single consistent pipeline:
**NASA POWER API** (real climate data) → **FAO-56 Penman-Monteith** (ET0 calculation) → **water balance simulation** (soil moisture + irrigation labels).

All years 2020–2026 were generated with identical logic, ensuring no distribution mismatch across the train/val/test split.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [6] Phase 1A markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[6]["source"] = ["""\
---
## Phase 1A — Data Understanding

Before building any model, we need to understand what the data looks like.
We check: missing values, outliers, how often irrigation happens per crop/year, and seasonal patterns.
If the data is broken here, everything downstream will be wrong.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [8] Phase 1A interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[8]["source"] = ["""\
### Phase 1A — What the EDA tells us

**No missing values** — the simulation pipeline is complete. No imputation needed, which means no risk of introducing artificial patterns.

**Outliers in `rain_mm`** — high rainfall spikes are real Mediterranean storm events, not errors. Removing them would make the model underestimate how much a heavy rain event protects against drought.

**Irrigation rate ~45% across all years** — this is healthy and consistent. It means the dataset is nearly balanced (roughly half the days need irrigation, half don't). This is a direct result of rebuilding all years with the same simulation pipeline.

**Seasonal pattern** — irrigation peaks in summer (July–August) when temperatures are high and ET0 is large. It drops in winter (December–February) when rainfall and lower temperatures keep the soil moist. This is physically correct for Tunisia.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [9] Phase 1B markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[9]["source"] = ["""\
---
## Phase 1B — Feature Engineering

Raw sensor data is not enough. We need to transform it into features that carry agronomic meaning.
The key insight: **the model should think like a farmer**, not a statistician.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [11] Phase 1B interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[11]["source"] = ["""\
### Phase 1B — Why these features matter

**SMD (Soil Moisture Deficit)** is the single most important feature. Instead of raw soil moisture %, it tells us: *how far is the soil from field capacity, relative to the crop's tolerance?* A value of 0 = no stress, 1 = wilting point reached. Two fields at 25% raw moisture can be in completely different stress states depending on soil type — SMD normalises this.

**Growth-stage Kc** — a tomato in week 3 (seedling) drinks much less water than a tomato in week 8 (peak growth). Using a fixed Kc would over-irrigate young plants and under-irrigate mature ones. Linear interpolation through FAO-56 growth stages fixes this.

**Recomputed ETc = ET0 × Kc** — ET0 is how much water the atmosphere "pulls" from the soil. Multiplying by Kc adjusts it for the specific crop. This is the actual daily water demand we're trying to match.

**7-day rolling lags** — yesterday's soil moisture predicts today's irrigation need. The model gets the last 7 days of SMD and temperature as context, so it understands the drying trend, not just the current snapshot.

**One-hot encoding** — the model cannot use the word "tomato" directly. We convert it to 4 binary columns (crop_apple, crop_grape, crop_potato, crop_tomato) so the model can learn crop-specific patterns.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [13] Temporal split interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[13]["source"] = ["""\
### Temporal Split — Why we split by year, not randomly

**The golden rule of time-series ML: never let the model see the future during training.**

If we split randomly (80/20), a training row from July 2025 could sit next to a test row from January 2025. The model would indirectly learn future patterns. This inflates scores and produces a model that fails in production.

By splitting on full years:
- **Train (2020–2023)**: the model learns from 4 years of diverse climate — wet years, dry years, hot summers
- **Val (2024)**: used only to tune thresholds and calibrate probabilities — never touches the model weights
- **Test (2025 + 2026 Jan–Mar)**: seen exactly once, at the very end — this is the honest score

**StandardScaler on train only**: if we computed the mean/std on all data including 2025, we'd be leaking future statistics into training. Fitting only on 2020–2023 keeps the normalisation honest.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [14] Phase 2 markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[14]["source"] = ["""\
---
## Phase 2 — Modeling

We train XGBoost using TimeSeriesSplit cross-validation to find the best hyperparameters.
Then we explain the model with SHAP, calibrate its probabilities with Platt scaling,
and tune two operating thresholds: WARN (high recall) and ACT (balanced production point).
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [16] Phase 2A interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[16]["source"] = ["""\
### Phase 2A — Reading the GridSearch results

**CV Recall** — the score the model achieves on held-out folds during cross-validation. This is what we optimise. We care about recall because a missed irrigation event (false negative) can destroy a harvest — a false alarm just wastes some water.

**Train Recall ≈ 1.0** — the model memorises training data almost perfectly. This is expected for tree-based models and is not a problem by itself. What matters is whether val/test recall holds up.

**The gap train → CV recall** shows how much generalisation drops when the model sees new years. A gap of ~0.06 is healthy. A gap of >0.20 would mean the model is overfitting.

**scale_pos_weight = sqrt(neg/pos)** — with our nearly balanced dataset (~1.15 ratio), this is close to 1.0 and has minimal effect. It's kept for robustness in case the ratio shifts on new data.

**Chosen champion**: depth=6, lr=0.1, n_est=200 — this is a medium-complexity model. Deep enough to learn crop interactions, not so deep it overfits a single year's noise.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [17] Phase 2A' markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[17]["source"] = ["""\
---
## Phase 2A' — Model Selection: Why XGBoost?

We benchmark three algorithms on the same 2024 validation set to justify the choice of XGBoost with empirical evidence rather than assumption.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [19] Model selection interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[19]["source"] = ["""\
### Phase 2A' — What the ROC curves tell us

**AUC-ROC measures ranking ability** — how well the model separates irrigation days from skip days regardless of threshold. AUC=1.0 is perfect, AUC=0.5 is random.

**LogisticRegression (AUC ~0.977)** — performs surprisingly well with clean, engineered features. It draws a straight decision boundary. Its weakness: it cannot learn that "high SMD AND high temperature AND 3rd consecutive dry day" is more dangerous than any single factor alone.

**RandomForest (AUC ~0.986)** — strong, but slower to train. Does not natively support the `scale_pos_weight` parameter and is less tuneable for imbalanced temporal data.

**XGBoost (AUC ~0.988)** — wins on all three metrics (AUC, Recall, F1). Boosting sequentially focuses on the hardest-to-classify examples, which is exactly what we need when irrigation decisions are non-linear combinations of multiple stress signals.

**All three models are close** — this is a sign of clean, well-engineered data. When features carry strong signal, even a simple model does well. XGBoost's edge is consistent, not dramatic.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [21] SHAP interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[21]["source"] = ["""\
### Phase 2B — Reading the SHAP plots

**What SHAP does in plain terms:** for every single prediction, SHAP calculates how much each feature pushed the probability up or down from the average. It opens the black box.

**Beeswarm plot (each dot = one prediction):**
- Features at the **top** matter most overall
- A dot to the **right** (red) = this feature's high value pushed toward "irrigate"
- A dot to the **left** (blue) = this feature's low value pushed toward "skip"
- Expected pattern: high SMD → irrigate, high rain → skip, high humidity → skip

**Bar chart (mean |SHAP|):** The average magnitude of each feature's impact. The top features are your model's decision drivers — if SMD is not at the top, something is wrong with the features.

**Local explanation:** For one specific prediction, we can see exactly why the model said "irrigate" — e.g., "SMD was 0.78 (+0.31 toward irrigate), rain was 0mm (+0.18), temp_lag_3 was 38°C (+0.12)...". This is what makes the model explainable to a farmer or agronomist.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [23] Calibration interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[23]["source"] = ["""\
### Phase 2C — Reading the calibration diagram

**The problem Platt scaling solves:** XGBoost outputs a score between 0 and 1, but this is not a real probability. If the model says 0.8, it doesn't necessarily mean 80% of such cases are true irrigation events. We need to fix this before setting thresholds.

**The reliability diagram (what to look for):**
- The **diagonal line** = perfect calibration (predicted 0.7 → 70% of cases truly irrigate)
- **Orange curve (uncalibrated XGBoost)** — if it bows above or below the diagonal, the raw scores are biased
- **Green curve (Platt-calibrated)** — should lie closer to the diagonal

**Why this matters for thresholds:** once probabilities are honest, a threshold of 0.45 genuinely means "fire when the model is 45% confident." Without calibration, threshold values are arbitrary numbers with no real meaning.

**Per-crop threshold table:** each crop gets its own ACT threshold because the model's probability scale is not identical across crops. Grape needs a lower threshold (more sensitive) because its irrigation events are harder to detect — fewer positive examples, shorter growing season window.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [25] Benchmark interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[25]["source"] = ["""\
### Phase 2D — Reading the final benchmark

**This is the only honest score in the notebook** — 2025+2026 data was never touched until this cell.

**WARN threshold (high recall, lower precision):**
The model fires an alert for anything above a low probability threshold.
Almost every real drought event is caught (recall ≈ 1.0), but there are more false alarms.
Use this layer to send an early notification to the farmer: *"stress may be developing."*

**ACT threshold (balanced, production operating point):**
The threshold tuned for best F1 — the sweet spot between catching drought events and not wasting water.
Precision ~0.89 means 89% of irrigation commands are genuinely needed.
Recall ~0.94 means 94% of drought events trigger a response.
This is what the irrigation pump actually responds to.

**Confusion matrix @ ACT — how to read it:**
- **Top-left (True Negatives):** days correctly identified as "no irrigation needed" ✓
- **Bottom-right (True Positives):** drought days correctly caught ✓
- **Top-right (False Positives):** irrigation triggered unnecessarily — wastes water
- **Bottom-left (False Negatives):** drought days missed — risks yield loss ✗

**Bootstrap CI:** running the evaluation 500 times on random samples of the test set. The narrow interval (e.g., recall 0.934–0.942) confirms the score is statistically stable, not a lucky draw.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [26] Phase 3 markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[26]["source"] = ["""\
---
## Phase 3 — Drift Monitoring

A model trained today may become unreliable next year if the climate shifts.
PSI (Population Stability Index) measures whether the feature distributions in new years
look similar to the baseline year (2020). If they don't, the model needs retraining.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [28] Drift interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[28]["source"] = ["""\
### Phase 3 — Reading the PSI drift analysis

**What PSI measures in plain terms:** imagine plotting a histogram of SMD values for 2020, then plotting the same histogram for 2023. PSI quantifies how different the two shapes are. The more they diverge, the less reliable the model becomes.

**PSI thresholds:**
- **< 0.10 (stable)** — the year looks statistically identical to baseline. The model generalises without degradation.
- **0.10–0.20 (monitor)** — some shift, possibly a wetter or drier than average year. Run a validation check.
- **≥ 0.20 (DRIFT)** — the distribution has changed significantly. Consider retraining.

**2021–2025 all stable** — this confirms the rebuilt dataset is consistent. All years came from the same simulation pipeline, so their feature distributions are naturally similar.

**2026 shows DRIFT** — this is expected and not a real problem. 2026 contains only January–March (winter months). Comparing winter temperature and ET0 distributions against a full-year 2020 baseline will always look like drift — it's seasonal, not model decay.

**Learning curve** — shows model performance as training data grows. A flattening curve means adding more years won't improve the model much. A still-rising curve means more data would help.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [29] Phase 4 markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[29]["source"] = ["""\
---
## Phase 4 — Autonomous Irrigation Agent

The model now becomes an agent that makes daily decisions on a real field window.
We simulate 14 days in July 2025 on the Tomato field — the highest-stress period of the year.
The agent maintains a 7-day rolling memory, applies the rain guard, and uses the dual-threshold policy.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [31] Agent log interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[31]["source"] = ["""\
### Phase 4A — Reading the agent decision log

**Each row is one day.** The agent wakes up, reads the sensors, runs the model, and decides.

**p_cal** — the calibrated probability output by the model. Values above ACT threshold → irrigate. Values between WARN and ACT → warning. Below WARN → skip.

**Rain guard** — if `rain_mm > ETc`, the soil is recharging naturally. The agent skips irrigation regardless of the model score. This is a hard agronomic rule — no model override allowed.

**Irrigate → SMD resets to 0** — when the agent triggers irrigation, it restores the soil to field capacity. The next day's SMD starts fresh.

**Warning days** — the model is uncertain. The agent flags these for human review without triggering the pump. This is the early warning layer in action.

**Reading the agronomic justification column:** shows the FAO-56 logic behind each decision — growth stage, Kc value, ETc, whether the depletion threshold `p` was crossed. This is what makes the system explainable to an agronomist.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [33] Drying curve interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[33]["source"] = ["""\
### Phase 4B — Reading the drying curve

**The drying curve shows soil stress over time** — it's the most intuitive visualisation in the notebook.

**Rising SMD curve** — soil is drying out. Crop ETc is consuming water faster than rain replenishes it. The steeper the rise, the more urgent the irrigation need.

**Orange dashed line (depletion threshold p)** — the FAO-56 stress boundary. When SMD crosses this line, the crop enters water stress and yield loss begins. The agent should fire *before or at* this crossing.

**Triangle markers (▲ irrigate)** — each marker resets SMD back toward 0 (field capacity). A well-timed marker appears just as the curve approaches the orange line.

**Diamond markers (◆ warning)** — the model detected rising stress but hasn't crossed the threshold yet. Think of these as yellow lights.

**Circle markers (● skip)** — soil is still moist enough, no action needed.

**What a good agent looks like:** triangles appear consistently just before the orange line, with no large gaps. Triangles appearing well above the line = over-irrigation. Triangles appearing after the line = the model reacted too late.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [36] Phase 5 markdown
# ─────────────────────────────────────────────────────────────────────────────
CELLS[36]["source"] = ["""\
---
## Phase 5 — Champion Model Deployment

The trained pipeline is packaged into a production-ready **FastAPI microservice**.
All artifacts (model, scaler, calibrator, metadata) are exported to `backend/irrigation_service/`.
The service exposes two endpoints and re-applies the rain guard at the API level.

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/irrigation/predict` | POST | JSON sensor input → irrigation decision |
| `/api/v1/health` | GET | Liveness check |

Response includes: `irrigate`, `confidence`, `threshold_used`, `rain_guard_triggered`.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [38] Phase 5 deployment interpretation
# ─────────────────────────────────────────────────────────────────────────────
CELLS[38]["source"] = ["""\
### Phase 5 — What the deployment does

**Artifact package** — the `irrigation_service/` folder contains everything needed to serve predictions on any machine, without the notebook:
- `xgb_champion.json` — the XGBoost model in portable JSON format
- `scaler_temporal.pkl` — the StandardScaler fitted on 2020–2023 train data
- `platt_calibrator.pkl` — the Platt scaling layer fitted on 2024 val data
- `model_meta.json` — the inference contract: feature list, thresholds, version info

**Rain guard at API level** — the FastAPI endpoint re-applies `rain_mm > ETc → skip` before calling the model. This ensures the hard agronomic rule is enforced even if the caller doesn't know about it.

**Smoke test** — the cell reloads every artifact from disk and runs one full prediction cycle. If this cell passes without error, the pipeline is deployment-ready. If it fails, something in the export was corrupted.

**From notebook to IoT** — the next step is connecting a real sensor (soil moisture probe, rain gauge, weather station) to the `/predict` endpoint. The model never changes — only the data source goes from simulated to real.
"""]

# ─────────────────────────────────────────────────────────────────────────────
# [39] Model card — update years
# ─────────────────────────────────────────────────────────────────────────────
src39 = ''.join(CELLS[39]['source'])
src39 = src39.replace("NASA POWER FAO-56 Tunisia 2022+2023", "NASA POWER FAO-56 Tunisia 2020–2023")
src39 = src39.replace("NASA POWER FAO-56 Tunisia 2024 (threshold tuning)", "NASA POWER FAO-56 Tunisia 2024 (threshold tuning)")
src39 = src39.replace("NASA POWER FAO-56 Tunisia 2025 (final benchmark)", "NASA POWER FAO-56 Tunisia 2025 + 2026 Jan–Mar (final benchmark)")
src39 = src39.replace("(2022-2025)", "(2020-2026)")
CELLS[39]['source'] = [src39]

# ─────────────────────────────────────────────────────────────────────────────
# Save
# ─────────────────────────────────────────────────────────────────────────────
with open(NB_PATH, "w", encoding="utf-8") as f:
    json.dump(nb, f, ensure_ascii=False, indent=1)

print("All markdown cells patched successfully.")
print("Updated: [0] header, [3] phase0, [6] phase1A, [8] interp1A, [9] phase1B,")
print("         [11] interp1B, [13] split, [14] phase2, [16] interp2A, [17] phase2A',")
print("         [19] interp2A', [21] SHAP, [23] calibration, [25] benchmark,")
print("         [26] phase3, [28] drift, [29] phase4, [31] agent, [33] drying,")
print("         [36] phase5, [38] deployment, [39] model card")
