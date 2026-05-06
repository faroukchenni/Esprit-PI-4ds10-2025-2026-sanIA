"""
Tunisia Meteorologically-Forced Irrigation Dataset Builder
===========================================================
Converts NASA POWER raw weather data into a labeled irrigation dataset
using the FAO-56 soil water balance model driven by real meteorological data.

Why this replaces the generic synthetic dataset:
  The previous dataset used randomly sampled weather (Gaussian noise around
  Mediterranean averages). This causes PSI ≈ 1.2 between training and real
  sensor data — the model "lives in a fantasy world".

  This script:
    1. Uses real NASA POWER reanalysis weather (2023-2024) for 4 Tunisian
       field locations — actual temperature swings, rainfall events, dry spells.
    2. Computes daily ET0 via Hargreaves-Samani (validated for semi-arid
       Mediterranean — FAO Irrigation Paper 56, Annex 2).
    3. Simulates soil moisture evolution using FAO-56 water balance:
         SM(t) = SM(t-1) + rain(t) - ETc(t)
         SM = clipped to [WP, FC]
    4. Labels each day: irrigate=1 if SM < depletion_threshold (fraction of TAW)
    5. Generates multiple "scenario" passes per field to increase dataset size
       and cover the full range of moisture states.

Scenario diversity strategy:
  - 3 "drought levels": normal rain, 60% rain, 30% rain (simulates dry years)
  - 4 starting moisture levels per scenario (from WP to FC)
  - 2 years × 4 fields × 3 drought levels × 4 starts = 96 season simulations
  - Expected dataset size: ~9,500 rows with ~25-35% positive rate

Output: Data/tunisia_dataset.csv  (drop-in replacement for the notebook)
"""

import json
import math
import csv
import random
import numpy as np
from pathlib import Path
from datetime import datetime


random.seed(42)
np.random.seed(42)

# ── Paths ─────────────────────────────────────────────────────────────────────
INPUT_PATH  = Path(__file__).resolve().parent.parent / "Data" / "nasa_power_raw.json"
OUTPUT_PATH = Path(__file__).resolve().parent.parent / "Data" / "tunisia_dataset.csv"


# ── Hargreaves-Samani ET0 ─────────────────────────────────────────────────────
def _extraterrestrial_radiation(lat_deg: float, doy: int) -> float:
    """Ra (MJ/m2/day) — FAO-56 Eq 21."""
    lat_rad = math.radians(lat_deg)
    dr      = 1 + 0.033 * math.cos(2 * math.pi * doy / 365)
    delta   = 0.409 * math.sin(2 * math.pi * doy / 365 - 1.39)
    omega_s = math.acos(max(-1.0, min(1.0, -math.tan(lat_rad) * math.tan(delta))))
    Gsc     = 0.0820
    Ra = (24 * 60 / math.pi) * Gsc * dr * (
        omega_s * math.sin(lat_rad) * math.sin(delta)
        + math.cos(lat_rad) * math.cos(delta) * math.sin(omega_s)
    )
    return max(0.0, Ra)


def hargreaves_et0(t_mean: float, t_max: float, t_min: float,
                   lat: float, doy: int) -> float:
    """ET0 (mm/day) — Hargreaves-Samani 1985."""
    Ra_mm = _extraterrestrial_radiation(lat, doy) * 0.408
    dT    = max(0.0, t_max - t_min)
    return max(0.0, 0.0023 * (t_mean + 17.8) * math.sqrt(dT) * Ra_mm)


# ── Single scenario simulation ─────────────────────────────────────────────────
def simulate_scenario(
    field_meta: dict,
    weather: dict,
    rain_scale: float = 1.0,
    sm_init: float = None,
    p_fraction: float = 0.40,
) -> list[dict]:
    """
    Simulate one "scenario" for the given field and weather data.

    Parameters
    ----------
    rain_scale  : multiply all rainfall by this factor (0.3 = drought year)
    sm_init     : initial soil moisture %; None = 80% of FC
    p_fraction  : depletion fraction (0.40 = FAO-56 default)
    """
    FC   = field_meta["FC"]
    WP   = field_meta["WP"]
    Kc   = field_meta["Kc_mid"]
    root = field_meta["root_depth"]
    lat  = field_meta["lat"]
    soil = field_meta["soil_type"]
    crop = field_meta["crop"]

    RAW = WP + p_fraction * (FC - WP)

    SM = sm_init if sm_init is not None else (WP + 0.80 * (FC - WP))
    SM = float(max(WP, min(FC, SM)))

    dates = sorted(weather["T2M"].keys())

    rows = []
    crop_age = 0
    moisture_history = [SM, SM, SM]
    temp_history     = [25.0, 25.0, 25.0]

    for date_str in dates:
        d   = datetime.strptime(date_str, "%Y%m%d")
        doy = d.timetuple().tm_yday

        if d.month == 1 and d.day == 1:
            crop_age = 0
        crop_age += 1

        # ── Weather ───────────────────────────────────────────────────────
        def _w(key, default):
            v = weather[key].get(date_str, default)
            return default if v <= -900 else v

        t_mean = _w("T2M",         20.0)
        t_max  = _w("T2M_MAX",     t_mean + 5)
        t_min  = _w("T2M_MIN",     t_mean - 5)
        rh     = _w("RH2M",        60.0)
        rain   = max(0.0, _w("PRECTOTCORR", 0.0)) * rain_scale

        # ── ET0, ETc ──────────────────────────────────────────────────────
        et0 = hargreaves_et0(t_mean, t_max, t_min, lat, doy)
        etc = Kc * et0

        # ── Water balance ─────────────────────────────────────────────────
        root_mm   = root * 1000.0
        delta_pct = (etc - rain) / root_mm * 100.0
        SM        = max(WP, min(FC, SM - delta_pct))

        # ── Label ─────────────────────────────────────────────────────────
        # Irrigate if SM below RAW threshold
        irrigate = int(SM < RAW)

        # After irrigation, soil is refilled toward FC
        SM_next = FC if irrigate else SM

        # ── Sensor noise ──────────────────────────────────────────────────
        sm_noisy   = float(np.clip(SM + np.random.normal(0, 0.5), WP - 2, FC + 2))
        temp_noisy = round(t_mean + np.random.normal(0, 0.3), 2)
        rh_noisy   = float(np.clip(rh + np.random.normal(0, 1.0), 10, 99))

        rows.append({
            "date":              date_str,
            "field_id":          field_meta["field_id"],
            "crop":              crop,
            "soil_type":         soil,
            "crop_age_days":     crop_age,
            "temperature_C":     temp_noisy,
            "humidity_%":        round(rh_noisy, 2),
            "soil_moisture_%":   round(sm_noisy, 2),
            "field_capacity_%":  FC,
            "wilting_point_%":   WP,
            "moisture_T-1":      round(moisture_history[0], 2),
            "moisture_T-2":      round(moisture_history[1], 2),
            "moisture_T-3":      round(moisture_history[2], 2),
            "temp_T-1":          round(temp_history[0], 2),
            "temp_T-2":          round(temp_history[1], 2),
            "temp_T-3":          round(temp_history[2], 2),
            "rain_mm":           round(rain, 2),
            "et0_mm":            round(et0, 3),
            "etc_mm":            round(etc, 3),
            "irrigate":          irrigate,
        })

        moisture_history = [SM, moisture_history[0], moisture_history[1]]
        temp_history     = [t_mean, temp_history[0], temp_history[1]]
        SM = SM_next

    return rows


def main():
    print("Loading NASA POWER raw data...")
    with open(INPUT_PATH) as f:
        raw = json.load(f)

    # Scenario matrix: rain scaling × starting moisture levels
    # This ensures we cover the full range of soil moisture states
    rain_scales  = [1.0, 0.60, 0.30]          # normal / dry / drought year
    sm_inits     = [None, 0.95, 0.75, 0.50]   # FC, 95%FC, 75%FC, 50%FC of available
    p_fractions  = [0.40, 0.35]                # slightly more conservative trigger

    all_rows = []
    total_scenarios = 0

    for field_id, entry in raw.items():
        meta    = entry["meta"]
        weather = entry["weather"]
        FC, WP  = meta["FC"], meta["WP"]

        for rain_scale in rain_scales:
            for sm_init_frac in sm_inits:
                # Convert fraction → % volumetric
                if sm_init_frac is None:
                    sm_init = None
                else:
                    sm_init = WP + sm_init_frac * (FC - WP)

                for p in p_fractions:
                    rows = simulate_scenario(
                        meta, weather,
                        rain_scale=rain_scale,
                        sm_init=sm_init,
                        p_fraction=p,
                    )
                    all_rows.extend(rows)
                    total_scenarios += 1

        # Summary per field (last scenario only — for reporting)
        print(f"  {meta['name']} — scenarios done")

    # Shuffle to prevent any ordering artifacts in train/test split
    random.shuffle(all_rows)

    # Write CSV
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = list(all_rows[0].keys())
    with open(OUTPUT_PATH, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(all_rows)

    total = len(all_rows)
    n_irr = sum(r["irrigate"] for r in all_rows)
    print(f"\nScenarios run  : {total_scenarios}")
    print(f"Total rows     : {total}")
    print(f"Irrigate=1     : {n_irr} ({100*n_irr/total:.1f}%)")
    print(f"Irrigate=0     : {total - n_irr} ({100*(total-n_irr)/total:.1f}%)")

    # Per-crop breakdown
    crops = {}
    for r in all_rows:
        c = r["crop"]
        if c not in crops:
            crops[c] = {"total": 0, "irr": 0}
        crops[c]["total"] += 1
        crops[c]["irr"]   += r["irrigate"]
    print("\nPer-crop breakdown:")
    for c, v in crops.items():
        print(f"  {c:10s}: {v['total']:5d} rows | {v['irr']:4d} irrigate=1 "
              f"({100*v['irr']/v['total']:.1f}%)")

    if n_irr / total < 0.15:
        print("\nWARNING: Positive rate still low — use class_weight='balanced' in XGBoost.")
    else:
        print("\nClass balance looks good for XGBoost training.")


if __name__ == "__main__":
    main()
