"""
build_extended_dataset.py
Regenerates the FULL dataset (2020-2026) from scratch using a single
consistent pipeline: NASA POWER climate data → FAO-56 Penman-Monteith ET0
→ water balance simulation → 24 trajectories per field per day.

Replaces the old mixed dataset where 2022 was simulated and 2023-2025
came from a different source with different irrigation rates.

Run from project root:  python Scripts/build_extended_dataset.py
"""

import math, time, sys
sys.stdout.reconfigure(encoding='utf-8')
import numpy as np
import pandas as pd
import requests
from pathlib import Path

# ── Paths ─────────────────────────────────────────────────────────────────────
ROOT        = Path(__file__).resolve().parent.parent
DATA_DIR    = ROOT / "Data"
UNIFIED_CSV = DATA_DIR / "tunisia_dataset_full.csv"

# ── NASA POWER location (Tunisia — Tunis-Carthage area) ──────────────────────
LAT_DEG = 36.81
LON_DEG = 10.18
ELEV_M  = 50
LAT_RAD = math.radians(LAT_DEG)

# ── FAO-56 Table 12: Kc by growth stage + stage durations (days) ─────────────
KC_STAGES = {
    "tomato": {"ini": 0.60, "mid": 1.15, "end": 0.80, "d_ini": 30, "d_dev": 40, "d_mid": 40, "d_late": 25},
    "potato": {"ini": 0.50, "mid": 1.15, "end": 0.75, "d_ini": 25, "d_dev": 30, "d_mid": 45, "d_late": 30},
    "apple":  {"ini": 0.60, "mid": 1.20, "end": 0.75, "d_ini": 20, "d_dev": 70, "d_mid": 90, "d_late": 30},
    "grape":  {"ini": 0.30, "mid": 0.85, "end": 0.45, "d_ini": 20, "d_dev": 40, "d_mid": 120, "d_late": 60},
}

# ── FAO-56 Table 22: Allowable depletion fraction p per crop ─────────────────
DEPLETION_P = {"tomato": 0.40, "potato": 0.35, "apple": 0.50, "grape": 0.45}

# ── Field profiles (fixed across all years) ───────────────────────────────────
FIELD_PROFILES = pd.DataFrame([
    {"field_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "crop": "potato", "soil_type": "Sandy Loam", "field_capacity_%": 38.0, "wilting_point_%": 14.0},
    {"field_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "crop": "grape",  "soil_type": "Loam",       "field_capacity_%": 35.0, "wilting_point_%": 12.0},
    {"field_id": "cccccccc-cccc-cccc-cccc-cccccccccccc", "crop": "tomato", "soil_type": "Sandy Loam", "field_capacity_%": 38.0, "wilting_point_%": 14.0},
    {"field_id": "dddddddd-dddd-dddd-dddd-dddddddddddd", "crop": "apple",  "soil_type": "Silt Loam",  "field_capacity_%": 32.0, "wilting_point_%": 10.0},
])

# ── All years to generate ─────────────────────────────────────────────────────
FETCH_RANGES = [
    ("2020", "20200101", "20201231"),
    ("2021", "20210101", "20211231"),
    ("2022", "20220101", "20221231"),
    ("2023", "20230101", "20231231"),
    ("2024", "20240101", "20241231"),
    ("2025", "20250101", "20251231"),
    ("2026", "20260101", "20260331"),   # Jan-March only
]


# ── Agronomic helpers ─────────────────────────────────────────────────────────
def get_kc(crop, age_days):
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


# ── FAO-56 Penman-Monteith ET0 (mm/day) ──────────────────────────────────────
def penman_monteith_et0(tmean, tmax, tmin, rh, ws, rs_kwh, doy):
    rs   = rs_kwh * 3.6                             # kWh/m2/day → MJ/m2/day
    P    = 101.3 * ((293 - 0.0065 * ELEV_M) / 293) ** 5.26
    g    = 0.000665 * P
    es   = 0.3054 * (math.exp(17.27 * tmax / (tmax + 237.3)) +
                     math.exp(17.27 * tmin / (tmin + 237.3)))
    ea   = rh / 100.0 * es
    d    = 4098 * (0.6108 * math.exp(17.27 * tmean / (tmean + 237.3))) / (tmean + 237.3) ** 2
    dr   = 1 + 0.033 * math.cos(2 * math.pi * doy / 365)
    sdec = 0.409 * math.sin(2 * math.pi * doy / 365 - 1.39)
    ws_h = math.acos(max(-1.0, min(1.0, -math.tan(LAT_RAD) * math.tan(sdec))))
    Ra   = (24 * 60 / math.pi) * 0.082 * dr * (
               ws_h * math.sin(LAT_RAD) * math.sin(sdec) +
               math.cos(LAT_RAD) * math.cos(sdec) * math.sin(ws_h))
    Rso  = (0.75 + 2e-5 * ELEV_M) * Ra
    Rns  = (1 - 0.23) * rs
    Rnl  = (4.903e-9 * ((tmax + 273.16) ** 4 + (tmin + 273.16) ** 4) / 2 *
             (0.34 - 0.14 * math.sqrt(max(ea, 0))) *
             (1.35 * min(rs / max(Rso, 0.01), 1.0) - 0.35))
    Rn   = Rns - Rnl
    et0  = (0.408 * d * Rn + g * 900 / (tmean + 273) * ws * (es - ea)) / (
               d + g * (1 + 0.34 * ws))
    return min(max(float(et0), 0.0), 12.0)          # clip to physical ceiling


# ── NASA POWER fetch ──────────────────────────────────────────────────────────
def fetch_nasa(start, end, label, retries=3):
    url = (
        "https://power.larc.nasa.gov/api/temporal/daily/point"
        "?parameters=T2M,T2M_MAX,T2M_MIN,RH2M,PRECTOTCORR,WS2M,ALLSKY_SFC_SW_DWN"
        f"&community=AG&longitude={LON_DEG}&latitude={LAT_DEG}"
        f"&start={start}&end={end}&format=JSON"
    )
    for attempt in range(1, retries + 1):
        try:
            print(f"  Fetching {label} from NASA POWER (attempt {attempt})...")
            resp = requests.get(url, timeout=120)
            resp.raise_for_status()
            params = resp.json()["properties"]["parameter"]
            df = pd.DataFrame(params)
            df.index = pd.to_datetime(df.index, format="%Y%m%d")
            df.columns = ["T2M", "T2M_MAX", "T2M_MIN", "RH2M", "PRECTOTCORR", "WS2M", "Rs"]
            df["PRECTOTCORR"] = df["PRECTOTCORR"].clip(lower=0)
            df["WS2M"]        = df["WS2M"].clip(lower=0.5)
            df["Rs"]          = df["Rs"].clip(lower=0)
            print(f"  OK {label}: {len(df)} days | "
                  f"rain_avg={df['PRECTOTCORR'].mean():.2f}mm "
                  f"temp_avg={df['T2M'].mean():.1f}C")
            return df
        except Exception as exc:
            print(f"  FAIL attempt {attempt}: {exc}")
            if attempt < retries:
                time.sleep(5)
    return None


# ── Water balance simulation ──────────────────────────────────────────────────
def simulate_year(climate_df, n_trajectories=24, rng_seed=42):
    rng  = np.random.default_rng(rng_seed)
    rows = []

    for _, prof in FIELD_PROFILES.iterrows():
        crop = prof["crop"]
        FC   = prof["field_capacity_%"]
        WP   = prof["wilting_point_%"]
        soil = prof["soil_type"]
        fid  = prof["field_id"]
        p    = DEPLETION_P[crop]
        TAW  = FC - WP

        # Each trajectory starts at a different random initial moisture
        init_moistures = rng.uniform(WP + 0.2 * TAW, FC, size=n_trajectories)

        for traj_idx in range(n_trajectories):
            moisture = float(init_moistures[traj_idx])

            for doy, (date, row) in enumerate(climate_df.iterrows(), start=1):
                age  = doy % 366 if doy > 365 else doy
                kc   = get_kc(crop, age)
                et0  = penman_monteith_et0(
                           row["T2M"], row["T2M_MAX"], row["T2M_MIN"],
                           row["RH2M"], row["WS2M"], row["Rs"], doy)
                etc  = et0 * kc
                rain = float(row["PRECTOTCORR"])

                # Small per-trajectory micro-climate noise for diversity
                t_noise  = float(rng.normal(0, 0.3))
                rh_noise = float(rng.normal(0, 1.0))

                SMD      = (FC - moisture) / TAW if TAW > 0 else 0
                irrigate = 1 if SMD > p else 0
                if irrigate:
                    moisture = FC           # restore to field capacity on irrigation

                moisture = float(np.clip(moisture + rain - etc, WP, FC))

                rows.append({
                    "date":             int(date.strftime("%Y%m%d")),
                    "field_id":         fid,
                    "crop":             crop,
                    "soil_type":        soil,
                    "crop_age_days":    age,
                    "temperature_C":    round(row["T2M"] + t_noise, 2),
                    "humidity_%":       round(float(np.clip(row["RH2M"] + rh_noise, 0, 100)), 2),
                    "soil_moisture_%":  round(moisture, 2),
                    "field_capacity_%": FC,
                    "wilting_point_%":  WP,
                    "rain_mm":          round(rain, 3),
                    "et0_mm":           round(et0, 3),
                    "etc_mm":           round(etc, 3),
                    "irrigate":         irrigate,
                })
    return pd.DataFrame(rows)


# ── Main ──────────────────────────────────────────────────────────────────────
def main():
    print("=" * 60)
    print("SanIA — Full Dataset Rebuild (2020-2026)")
    print("Single consistent pipeline for ALL years")
    print("=" * 60)

    all_frames = []

    for label, start, end in FETCH_RANGES:
        print(f"\n[{label}]")
        climate = fetch_nasa(start, end, label)
        if climate is None:
            print(f"  SKIP {label} — NASA API unavailable after retries.")
            continue

        print(f"  Simulating {len(FIELD_PROFILES)} fields x 24 trajectories x {len(climate)} days...")
        df_year = simulate_year(climate)
        irr_rate = df_year["irrigate"].mean()
        print(f"  Done: {len(df_year):,} rows | irrigate rate: {irr_rate:.2%}")
        all_frames.append(df_year)

    if not all_frames:
        print("\nERROR: No data generated — check NASA API connectivity.")
        return

    unified = pd.concat(all_frames, ignore_index=True)
    unified = unified.sort_values(["field_id", "date"]).reset_index(drop=True)
    unified.to_csv(UNIFIED_CSV, index=False)

    # Summary
    unified["_year"] = pd.to_datetime(unified["date"].astype(str), format="%Y%m%d").dt.year
    summary = unified.groupby("_year")["irrigate"].agg(["mean", "sum", "count"])
    summary.columns = ["irr_rate", "n_irrigate", "n_rows"]

    print("\n" + "=" * 60)
    print(f"Unified CSV saved: {UNIFIED_CSV}")
    print(f"Total rows : {len(unified):,}")
    print(f"Years      : {sorted(unified['_year'].unique())}")
    print()
    print("Per-year summary:")
    print(f"{'Year':<6} {'Irrigate%':>10} {'N_irrigate':>11} {'N_rows':>8}")
    print("-" * 40)
    for year, row in summary.iterrows():
        print(f"{year:<6} {row['irr_rate']:>10.2%} {int(row['n_irrigate']):>11,} {int(row['n_rows']):>8,}")

    total_pos = unified["irrigate"].sum()
    total_neg = len(unified) - total_pos
    print(f"\nOverall irrigate rate : {unified['irrigate'].mean():.2%}")
    print(f"Positive (irrigate=1) : {total_pos:,}")
    print(f"Negative (irrigate=0) : {total_neg:,}")
    print(f"Imbalance ratio       : 1:{total_neg//total_pos}")
    print("=" * 60)
    print()
    print("Proposed temporal split:")
    print("  Train : 2020-2023  (4 years)")
    print("  Val   : 2024")
    print("  Test  : 2025 + 2026 (Jan-Mar)")


if __name__ == "__main__":
    main()
