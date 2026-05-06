"""
NASA POWER Data Collector — SanIA Project
==========================================
Pulls 2 years of daily meteorological data for the 4 real Tunisian field
locations from the NASA POWER Agroclimatology API (free, no key required).

Parameters pulled per location:
  T2M        — Mean air temperature at 2m (°C)
  T2M_MAX    — Maximum daily temperature (°C)
  T2M_MIN    — Minimum daily temperature (°C)
  RH2M       — Relative humidity at 2m (%)
  PRECTOTCORR— Bias-corrected precipitation (mm/day)
  WS2M       — Wind speed at 2m (m/s)

ET0 (reference evapotranspiration) is computed locally using the
Hargreaves-Samani equation — simpler than Penman-Monteith but accurate
enough for semi-arid Mediterranean climates (validated for Tunisia in
multiple FAO studies).

Output: Data/nasa_power_raw.json
"""

import urllib.request
import json
import time
from pathlib import Path

# ── Field definitions — real DB records with realistic coordinates ────────────
FIELDS = [
    {
        "field_id":   "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "name":       "Champ de Pomme de Terre Bizerte",
        "crop":       "potato",
        "lat":        37.27,
        "lon":        9.87,
        "soil_type":  "Sandy Loam",
        "FC":         38.0,   # Field capacity (%)
        "WP":         14.0,   # Wilting point (%)
        "Kc_mid":     1.15,   # FAO-56 crop coefficient (mid-season)
        "root_depth": 0.40,   # Effective root zone depth (m)
        "area_m2":    10000,
    },
    {
        "field_id":   "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
        "name":       "Vignoble du Cap Bon",
        "crop":       "grape",
        "lat":        36.80,
        "lon":        10.57,
        "soil_type":  "Loam",
        "FC":         35.0,
        "WP":         12.0,
        "Kc_mid":     0.85,
        "root_depth": 0.60,
        "area_m2":    10000,
    },
    {
        "field_id":   "cccccccc-cccc-cccc-cccc-cccccccccccc",
        "name":       "Champ de Tomates Nabeul",
        "crop":       "tomato",
        "lat":        36.45,
        "lon":        10.73,
        "soil_type":  "Sandy Loam",
        "FC":         38.0,
        "WP":         14.0,
        "Kc_mid":     1.15,
        "root_depth": 0.35,
        "area_m2":    10000,
    },
    {
        "field_id":   "dddddddd-dddd-dddd-dddd-dddddddddddd",
        "name":       "Verger de Pommiers Kasserine",
        "crop":       "apple",
        "lat":        35.17,
        "lon":        8.83,
        "soil_type":  "Silt Loam",
        "FC":         32.0,
        "WP":         10.0,
        "Kc_mid":     1.20,
        "root_depth": 0.80,
        "area_m2":    10000,
    },
]

START_DATE = "20230101"
END_DATE   = "20251231"   # 3 full years

PARAMS = "T2M,T2M_MAX,T2M_MIN,RH2M,PRECTOTCORR,WS2M"
BASE_URL = "https://power.larc.nasa.gov/api/temporal/daily/point"

OUTPUT_PATH = Path(__file__).resolve().parent.parent / "Data" / "nasa_power_raw.json"


def pull_field(field: dict) -> dict:
    url = (
        f"{BASE_URL}?parameters={PARAMS}&community=AG"
        f"&longitude={field['lon']}&latitude={field['lat']}"
        f"&start={START_DATE}&end={END_DATE}&format=JSON"
    )
    print(f"  Pulling: {field['name']} ({field['lat']}, {field['lon']})...")
    with urllib.request.urlopen(url, timeout=60) as r:
        data = json.loads(r.read())
    return data["properties"]["parameter"]


def main():
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    result = {}

    for field in FIELDS:
        try:
            weather = pull_field(field)
            result[field["field_id"]] = {
                "meta":    field,
                "weather": weather,
            }
            print(f"  OK — {len(weather['T2M'])} days pulled")
            time.sleep(1)   # be polite to the API
        except Exception as e:
            print(f"  ERROR for {field['name']}: {e}")

    with open(OUTPUT_PATH, "w") as f:
        json.dump(result, f, indent=2)

    print(f"\nSaved to: {OUTPUT_PATH}")
    total_days = sum(len(v["weather"]["T2M"]) for v in result.values())
    print(f"Total field-days collected: {total_days}")


if __name__ == "__main__":
    main()
