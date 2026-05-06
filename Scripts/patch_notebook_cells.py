"""Patch CELL 2, CELL 6, and CELL 9 in the v4 notebook."""
import json, sys
sys.stdout.reconfigure(encoding='utf-8')
from pathlib import Path

NB_PATH = Path(__file__).resolve().parent.parent / "irrigation" / "Smart_Irrigation_v4_0_Production.ipynb"

with open(NB_PATH, "r", encoding="utf-8") as f:
    nb = json.load(f)

# ─────────────────────────────────────────────────────────────────────────────
# CELL 2  (index 4) — Phase 0: dataset import, updated year range
# ─────────────────────────────────────────────────────────────────────────────
CELL2 = """\
# ═══════════════════════════════════════════════════════════════════════════════
# CELL 2 │ Phase 0 — Dataset Import
# Full dataset rebuilt from scratch: NASA POWER climate → FAO-56 water balance
# Single consistent simulation pipeline for all years 2020-2026 (2026: Jan-Mar).
# No API fetch needed here — just confirm the unified CSV is present.
# ═══════════════════════════════════════════════════════════════════════════════

assert UNIFIED_CSV.exists(), f"Unified dataset not found: {UNIFIED_CSV}"

_peek = pd.read_csv(UNIFIED_CSV, usecols=["date"])
_peek["date"] = pd.to_datetime(_peek["date"].astype(str), format="%Y%m%d")
_years = sorted(_peek["date"].dt.year.unique())

print(f"Unified dataset found | rows={len(_peek):,} | years={_years}")
print(f"  Path: {UNIFIED_CSV}")
del _peek
"""

# ─────────────────────────────────────────────────────────────────────────────
# CELL 6  (index 12) — Temporal split: 2020-2023 / 2024 / 2025-2026
# ─────────────────────────────────────────────────────────────────────────────
CELL6 = """\
# ═══════════════════════════════════════════════════════════════════════════════
# CELL 6 │ Temporal Train / Val / Test Split
# Train: 2020-2023  |  Val: 2024  |  Test: 2025 + 2026 (Jan-Mar)
# Strict temporal ordering — no future leakage
# ═══════════════════════════════════════════════════════════════════════════════
df_feat["year"] = df_feat["date"].dt.year

TRAIN_YEARS = [2020, 2021, 2022, 2023]
VAL_YEAR    = 2024
TEST_YEARS  = [2025, 2026]

train_mask = df_feat["year"].isin(TRAIN_YEARS)
val_mask   = df_feat["year"] == VAL_YEAR
test_mask  = df_feat["year"].isin(TEST_YEARS)

df_train = df_feat[train_mask].copy()
df_val   = df_feat[val_mask].copy()
df_test  = df_feat[test_mask].copy()

X_train_raw = df_train[ALL_FEATS]
y_train     = df_train[TARGET].astype(int)
X_val_raw   = df_val[ALL_FEATS]
y_val       = df_val[TARGET].astype(int)
X_test_raw  = df_test[ALL_FEATS]
y_test      = df_test[TARGET].astype(int)

# ── Scale continuous features only (OHE stays 0/1) ───────────────────────────
scaler = StandardScaler()
X_train = X_train_raw.copy()
X_train[CONTINUOUS_FEATS] = scaler.fit_transform(X_train_raw[CONTINUOUS_FEATS])

X_val = X_val_raw.copy()
X_val[CONTINUOUS_FEATS]   = scaler.transform(X_val_raw[CONTINUOUS_FEATS])

X_test = X_test_raw.copy()
X_test[CONTINUOUS_FEATS]  = scaler.transform(X_test_raw[CONTINUOUS_FEATS])

# ── Split summary ─────────────────────────────────────────────────────────────
print("=" * 62)
print(f"  Train {TRAIN_YEARS}: {len(df_train):>8,} rows | "
      f"{y_train.sum():>6,} irrigate ({y_train.mean():.2%})")
print(f"  Val   ({VAL_YEAR})     : {len(df_val):>8,} rows | "
      f"{y_val.sum():>6,} irrigate ({y_val.mean():.2%})")
print(f"  Test  {TEST_YEARS}  : {len(df_test):>8,} rows | "
      f"{y_test.sum():>6,} irrigate ({y_test.mean():.2%})")
print("=" * 62)
print(f"  StandardScaler fitted on {len(X_train_raw):,} train rows")
print(f"  Applied to val ({len(X_val_raw):,}) and test ({len(X_test_raw):,}) without refitting")
print(f"  No future data leaked into training")
"""

# ─────────────────────────────────────────────────────────────────────────────
# CELL 9  (index 22) — Platt calibration + sensible thresholds + per-crop
# ─────────────────────────────────────────────────────────────────────────────
CELL9 = """\
# ═══════════════════════════════════════════════════════════════════════════════
# CELL 9 │ Phase 2C — Platt Calibration + Threshold Tuning + Per-Crop Thresholds
# ═══════════════════════════════════════════════════════════════════════════════

# ── Step 1: Platt Scaling on 2024 Val probabilities ──────────────────────────
val_raw = champion.predict_proba(X_val)[:, 1]
platt   = LogisticRegression(C=1.0, solver="lbfgs", random_state=SEED, max_iter=1000)
platt.fit(val_raw.reshape(-1, 1), y_val)
val_cal = platt.predict_proba(val_raw.reshape(-1, 1))[:, 1]

# ── Step 2: Reliability Diagram ───────────────────────────────────────────────
frac_raw, mean_raw = calibration_curve(y_val, val_raw, n_bins=10)
frac_cal, mean_cal = calibration_curve(y_val, val_cal, n_bins=10)

fig, ax = plt.subplots(figsize=(8, 6))
ax.plot([0, 1], [0, 1], "--", color="white", alpha=0.5, label="Perfect calibration")
ax.plot(mean_raw, frac_raw, "o-", color=ORANGE, label="Uncalibrated XGBoost")
ax.plot(mean_cal, frac_cal, "s-", color=GREEN,  label="Platt-calibrated")
ax.set_xlabel("Mean Predicted Probability")
ax.set_ylabel("Fraction of Positives")
ax.set_title("Calibration Reliability Diagram (2024 Val Set)", fontsize=12)
ax.legend(); ax.grid(alpha=0.4)
plt.tight_layout(); plt.show()

# ── Step 3: Global threshold search on calibrated val probs ──────────────────
# WARN: highest threshold achieving recall >= 0.85  (early warning layer)
# ACT : F1-optimal threshold where recall >= 0.70  (production operating point)
thresholds = np.linspace(0.01, 0.99, 500)
warn_thr = None
act_thr  = None
best_f1  = -1.0

for thr in thresholds:
    preds = (val_cal >= thr).astype(int)
    rec   = recall_score(y_val, preds, zero_division=0)
    prec  = precision_score(y_val, preds, zero_division=0)
    f1    = f1_score(y_val, preds, zero_division=0)

    if rec >= 0.85 and warn_thr is None:
        warn_thr = float(thr)

    if rec >= 0.70 and f1 > best_f1:
        best_f1 = f1
        act_thr = float(thr)

if warn_thr is None:
    warn_thr = float(thresholds[0])
    print("WARN fallback — recall 0.85 not achievable; set to minimum threshold")
if act_thr is None:
    act_thr = 0.50
    print("ACT fallback to 0.50")

print(f"\\n{'='*52}")
print(f"  Global WARN threshold = {warn_thr:.4f}  (recall >= 0.85)")
print(f"  Global ACT  threshold = {act_thr:.4f}  (F1-optimal, recall >= 0.70)")
print(f"{'='*52}")

for name, thr in [("WARN", warn_thr), ("ACT", act_thr)]:
    preds = (val_cal >= thr).astype(int)
    rec   = recall_score(y_val, preds, zero_division=0)
    prec  = precision_score(y_val, preds, zero_division=0)
    f1    = f1_score(y_val, preds, zero_division=0)
    print(f"  Val @ {name} (thr={thr:.4f}): P={prec:.3f} | R={rec:.3f} | F1={f1:.3f}")

# ── Step 4: Per-crop ACT threshold tuning on val set ─────────────────────────
df_val_cal = df_val.copy()
df_val_cal["prob_cal"] = val_cal

crop_act_thr  = {}
crop_warn_thr = {}

print("\\nPer-crop threshold tuning (2024 val set):")
print(f"  {'Crop':<8} {'WARN_thr':>9} {'ACT_thr':>9} {'P':>7} {'R':>7} {'F1':>7} {'n_pos':>6}")
print("  " + "-" * 58)

for crop in sorted(df_val_cal["crop"].unique()):
    mask  = df_val_cal["crop"] == crop
    probs = df_val_cal.loc[mask, "prob_cal"].values
    truth = y_val[mask].values
    n_pos = int(truth.sum())

    if n_pos < 5:
        crop_act_thr[crop]  = act_thr
        crop_warn_thr[crop] = warn_thr
        print(f"  {crop:<8} (too few positives — using global thresholds)")
        continue

    c_warn    = None
    c_act     = None
    c_best_f1 = -1.0

    for thr in thresholds:
        preds = (probs >= thr).astype(int)
        rec   = recall_score(truth, preds, zero_division=0)
        prec  = precision_score(truth, preds, zero_division=0)
        f1    = f1_score(truth, preds, zero_division=0)

        if rec >= 0.85 and c_warn is None:
            c_warn = float(thr)
        if rec >= 0.65 and f1 > c_best_f1:
            c_best_f1 = f1
            c_act = float(thr)

    if c_warn is None: c_warn = float(thresholds[0])
    if c_act  is None: c_act  = act_thr

    crop_act_thr[crop]  = c_act
    crop_warn_thr[crop] = c_warn

    preds = (probs >= c_act).astype(int)
    p = precision_score(truth, preds, zero_division=0)
    r = recall_score(truth, preds, zero_division=0)
    f = f1_score(truth, preds, zero_division=0)
    print(f"  {crop:<8} {c_warn:>9.4f} {c_act:>9.4f} {p:>7.3f} {r:>7.3f} {f:>7.3f} {n_pos:>6}")

print("\\nPer-crop ACT  thresholds:", crop_act_thr)
print("Per-crop WARN thresholds:", crop_warn_thr)
print("\\nPlatt calibration fitted on 2024 val | all thresholds locked")
"""

# ── Apply patches ─────────────────────────────────────────────────────────────
nb["cells"][4]["source"]          = [CELL2]
nb["cells"][4]["outputs"]         = []
nb["cells"][4]["execution_count"] = None

nb["cells"][12]["source"]          = [CELL6]
nb["cells"][12]["outputs"]         = []
nb["cells"][12]["execution_count"] = None

nb["cells"][22]["source"]          = [CELL9]
nb["cells"][22]["outputs"]         = []
nb["cells"][22]["execution_count"] = None

with open(NB_PATH, "w", encoding="utf-8") as f:
    json.dump(nb, f, ensure_ascii=False, indent=1)

print("Patched cells 4 (CELL2), 12 (CELL6), 22 (CELL9) successfully.")
