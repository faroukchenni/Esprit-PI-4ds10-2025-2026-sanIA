"""
SanIA — Raspberry Pi Irrigation Agent  v4.0
============================================
Full automation loop running on the field Raspberry Pi.

Architecture:
  GPIO sensors  →  read soil moisture, temperature, humidity, rain gauge
  Open-Meteo    →  24h weather forecast (ET0, rain forecast)
  FastAPI :8001 →  POST /api/v1/irrigation/predict  (local notebook-served model)
  GPIO relay    →  activate/deactivate pump

Hardware connections (BCM pin numbering):
  - Soil moisture sensor:  analogue via MCP3008 SPI (channel 0)
  - DHT22 sensor:          data pin GPIO 4  (temperature + humidity)
  - Rain gauge:            digital input GPIO 17
  - Pump relay:            GPIO 27 (HIGH = pump ON)
  - Status LED (green):    GPIO 22
  - Status LED (red):      GPIO 23

Run on Pi:
  python3 pi_agent.py --crop tomato --field-id my_tomato_field
  python3 pi_agent.py --crop potato --field-id my_potato_field --interval 3600

For testing on desktop (no GPIO hardware):
  python3 pi_agent.py --dry-run --crop tomato --field-id test_field
"""

import argparse
import json
import logging
import math
import time
import sys
from collections import deque
from datetime import datetime
from pathlib import Path

import requests

# ── Configure logging ─────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(Path(__file__).parent / "pi_agent.log", encoding="utf-8"),
    ],
)
log = logging.getLogger("pi_agent")

# ── Constants ─────────────────────────────────────────────────────────────────
FASTAPI_URL     = "http://localhost:8001/api/v1/irrigation/predict"
OPEN_METEO_URL  = (
    "https://api.open-meteo.com/v1/forecast"
    "?latitude=36.81&longitude=10.18"
    "&daily=precipitation_sum,temperature_2m_max,et0_fao_evapotranspiration"
    "&timezone=Africa%2FTunis&forecast_days=2"
)
LOG_FILE        = Path(__file__).parent / "irrigation_decisions.jsonl"

# ── FAO-56 Kc (matches notebook exactly) ─────────────────────────────────────
KC_STAGES = {
    "tomato": {"ini": 0.60, "mid": 1.15, "end": 0.80, "d_ini": 30, "d_dev": 40, "d_mid": 40, "d_late": 25},
    "potato": {"ini": 0.50, "mid": 1.15, "end": 0.75, "d_ini": 25, "d_dev": 30, "d_mid": 45, "d_late": 30},
    "apple":  {"ini": 0.60, "mid": 1.20, "end": 0.75, "d_ini": 20, "d_dev": 70, "d_mid": 90, "d_late": 30},
    "grape":  {"ini": 0.30, "mid": 0.85, "end": 0.45, "d_ini": 20, "d_dev": 40, "d_mid": 120, "d_late": 60},
}
DEPLETION_P = {"tomato": 0.40, "potato": 0.35, "apple": 0.50, "grape": 0.45}

# Field soil parameters — edit to match your actual field
FIELD_CONFIG = {
    "tomato": {"FC": 38.0, "WP": 14.0, "soil_type": "Sandy Loam"},
    "potato": {"FC": 38.0, "WP": 14.0, "soil_type": "Sandy Loam"},
    "apple":  {"FC": 32.0, "WP": 10.0, "soil_type": "Silt Loam"},
    "grape":  {"FC": 35.0, "WP": 12.0, "soil_type": "Loam"},
}

# GPIO pin numbers (BCM)
PIN_RELAY  = 27
PIN_LED_OK = 22
PIN_LED_ERR = 23
PIN_RAIN   = 17
PIN_DHT22  = 4


# ── GPIO abstraction (real on Pi, mock on desktop) ───────────────────────────

class _MockGPIO:
    BCM = OUT = IN = HIGH = LOW = 0
    def setmode(self, m):            pass
    def setup(self, pin, mode):      pass
    def output(self, pin, val):      log.debug(f"[DRY-RUN] GPIO {pin} → {val}")
    def input(self, pin) -> int:     return 0
    def cleanup(self):               pass

try:
    import RPi.GPIO as GPIO  # type: ignore
    log.info("Running on Raspberry Pi — GPIO active.")
except ImportError:
    GPIO = _MockGPIO()
    log.info("RPi.GPIO not found — dry-run mode (mock GPIO).")


# ── ADC for soil moisture (MCP3008 via SPI) ───────────────────────────────────

def read_soil_moisture_adc(channel: int = 0) -> float:
    """
    Read soil moisture via MCP3008 SPI ADC.
    Returns volumetric moisture% (calibrated: 0% = dry/air, 100% = saturated).
    ADC range: 0–1023.  Typical: ~870 (dry) → ~370 (saturated).
    """
    try:
        import spidev  # type: ignore
        spi = spidev.SpiDev()
        spi.open(0, 0)
        spi.max_speed_hz = 1_000_000
        cmd = [1, (8 + channel) << 4, 0]
        reply = spi.xfer2(cmd)
        spi.close()
        raw = ((reply[1] & 3) << 8) | reply[2]
        # Calibration: 870 = dry (0%), 370 = saturated (100%)
        DRY_ADC = 870
        WET_ADC = 370
        pct = (DRY_ADC - raw) / (DRY_ADC - WET_ADC) * 100.0
        return float(max(0.0, min(100.0, pct)))
    except Exception as exc:
        log.warning("Soil ADC read failed: %s — using simulation fallback", exc)
        # Fallback: simulate 40% moisture for dry-run testing
        return 40.0


def read_dht22(pin: int = PIN_DHT22):
    """
    Read temperature and humidity from DHT22 sensor.
    Returns (temperature_C, humidity_pct) or (None, None) on failure.
    """
    try:
        import adafruit_dht  # type: ignore
        import board          # type: ignore
        dht = adafruit_dht.DHT22(getattr(board, f"D{pin}"))
        temp = dht.temperature
        hum  = dht.humidity
        dht.exit()
        return float(temp), float(hum)
    except Exception as exc:
        log.warning("DHT22 read failed: %s — using fallback", exc)
        # Simulation fallback for desktop testing
        return 35.0, 40.0


def read_rain_gauge(pin: int = PIN_RAIN) -> bool:
    """Returns True if rain detected (digital low = rain on typical rain sensors)."""
    try:
        return GPIO.input(pin) == GPIO.LOW
    except Exception:
        return False


# ── Weather API ───────────────────────────────────────────────────────────────

def fetch_weather() -> dict:
    """Fetch today's forecast from Open-Meteo. Returns empty dict on failure."""
    try:
        resp = requests.get(OPEN_METEO_URL, timeout=10)
        resp.raise_for_status()
        d = resp.json()["daily"]
        return {
            "rain_mm_24h":     float(d["precipitation_sum"][0] or 0.0),
            "et0_forecast_mm": float(d["et0_fao_evapotranspiration"][0] or 5.0),
            "temp_max_24h":    float(d["temperature_2m_max"][0] or 30.0),
        }
    except Exception as exc:
        log.warning("Open-Meteo fetch failed: %s — using defaults", exc)
        return {"rain_mm_24h": 0.0, "et0_forecast_mm": 5.0, "temp_max_24h": 30.0}


# ── FAO-56 Kc helper ─────────────────────────────────────────────────────────

def get_kc(crop: str, age_days: int) -> float:
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


# ── Relay control ─────────────────────────────────────────────────────────────

def pump_on():
    log.info("PUMP ON  → GPIO %d HIGH", PIN_RELAY)
    GPIO.output(PIN_RELAY, GPIO.HIGH)
    GPIO.output(PIN_LED_OK, GPIO.HIGH)

def pump_off():
    log.info("PUMP OFF → GPIO %d LOW", PIN_RELAY)
    GPIO.output(PIN_RELAY, GPIO.LOW)
    GPIO.output(PIN_LED_OK, GPIO.LOW)

def signal_error():
    for _ in range(3):
        GPIO.output(PIN_LED_ERR, GPIO.HIGH); time.sleep(0.3)
        GPIO.output(PIN_LED_ERR, GPIO.LOW);  time.sleep(0.3)


# ── Decision logging ──────────────────────────────────────────────────────────

def log_decision(record: dict):
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(json.dumps(record) + "\n")


# ── Main agent loop ───────────────────────────────────────────────────────────

class PiAgent:
    def __init__(self, crop: str, field_id: str, interval_sec: int = 3600):
        self.crop       = crop
        self.field_id   = field_id
        self.interval   = interval_sec
        self.cfg        = FIELD_CONFIG[crop]
        self.day_count  = 0
        # 7-reading rolling history for lag features
        self.history: deque = deque(maxlen=7)

    def setup_gpio(self):
        GPIO.setmode(GPIO.BCM)
        GPIO.setup(PIN_RELAY,   GPIO.OUT, initial=GPIO.LOW)
        GPIO.setup(PIN_LED_OK,  GPIO.OUT, initial=GPIO.LOW)
        GPIO.setup(PIN_LED_ERR, GPIO.OUT, initial=GPIO.LOW)
        GPIO.setup(PIN_RAIN,    GPIO.IN)

    def get_lags(self, smd: float, temp: float):
        buf = list(self.history)
        while len(buf) < 7:
            buf.insert(0, {"smd": smd, "temp": temp})
        smd_lags  = {f"SMD_lag_{i}":  buf[-(i)]["smd"]  for i in range(1, 8)}
        temp_lags = {f"temp_lag_{i}": buf[-(i)]["temp"] for i in range(1, 8)}
        return {**smd_lags, **temp_lags}

    def read_sensors(self):
        moisture_pct = read_soil_moisture_adc(channel=0)
        temp_c, hum_pct = read_dht22(PIN_DHT22)
        rain_now = read_rain_gauge(PIN_RAIN)
        return moisture_pct, temp_c, hum_pct, rain_now

    def build_payload(self, moisture_pct, temp_c, hum_pct, weather, crop_age_days):
        fc  = self.cfg["FC"]
        wp  = self.cfg["WP"]
        taw = fc - wp
        smd = max(0.0, min(1.2, (fc - moisture_pct) / taw)) if taw > 0 else 0.0
        kc  = get_kc(self.crop, crop_age_days)
        et0 = weather["et0_forecast_mm"]
        etc = round(et0 * kc, 3)

        ohe = {
            "crop_apple":  1 if self.crop == "apple"  else 0,
            "crop_grape":  1 if self.crop == "grape"  else 0,
            "crop_potato": 1 if self.crop == "potato" else 0,
            "crop_tomato": 1 if self.crop == "tomato" else 0,
        }

        lags = self.get_lags(smd, temp_c)

        return {
            "SMD":           round(smd, 4),
            "Kc":            round(kc, 4),
            "ETc":           etc,
            "et0_mm":        et0,
            "temperature_C": round(temp_c, 2),
            "humidity_pct":  round(hum_pct, 2),
            "rain_mm":       weather["rain_mm_24h"],
            "crop_age_days": crop_age_days,
            **lags,
            **ohe,
        }, smd

    def call_model(self, payload: dict) -> dict:
        """POST to the local FastAPI model service (port 8001, generated by CELL 16)."""
        try:
            resp = requests.post(FASTAPI_URL, json=payload, timeout=10)
            resp.raise_for_status()
            return resp.json()
        except Exception as exc:
            log.error("Model API call failed: %s", exc)
            return None

    def run_once(self, crop_age_days: int = None):
        self.day_count += 1
        if crop_age_days is None:
            crop_age_days = self.day_count % 366

        ts = datetime.utcnow().isoformat()
        log.info("──────────────────────────────────────────")
        log.info("Cycle %d | crop=%s | age=%d days | %s", self.day_count, self.crop, crop_age_days, ts)

        # 1. Read sensors
        try:
            moisture_pct, temp_c, hum_pct, rain_now = self.read_sensors()
            log.info("Sensors: moisture=%.1f%% temp=%.1f°C hum=%.1f%% rain=%s",
                     moisture_pct, temp_c, hum_pct, rain_now)
        except Exception as exc:
            log.error("Sensor read failed: %s", exc)
            signal_error()
            return

        # 2. Fetch weather
        weather = fetch_weather()
        log.info("Weather: rain_24h=%.1fmm et0=%.2fmm temp_max=%.1f°C",
                 weather["rain_mm_24h"], weather["et0_forecast_mm"], weather["temp_max_24h"])

        # 3. Build model payload
        payload, smd = self.build_payload(moisture_pct, temp_c, hum_pct, weather, crop_age_days)
        log.info("Features: SMD=%.3f Kc=%.3f ETc=%.3f", payload["SMD"], payload["Kc"], payload["ETc"])

        # 4. Call model
        result = self.call_model(payload)

        if result is None:
            log.warning("No model response — skipping pump action.")
            signal_error()
            return

        label      = result.get("irrigate", False)
        confidence = result.get("confidence", 0.0)
        decision   = result.get("threshold_used", "skip")
        rain_guard = result.get("rain_guard_triggered", False)

        log.info("Decision: %s | confidence=%.3f | label=%s | rain_guard=%s",
                 "IRRIGATE" if label else "SKIP", confidence, decision, rain_guard)

        # 5. Act on decision
        if label:
            pump_on()
        else:
            pump_off()

        # 6. Push reading to history
        self.history.append({"smd": smd, "temp": temp_c})

        # 7. Log decision
        record = {
            "timestamp":    ts,
            "crop":         self.crop,
            "field_id":     self.field_id,
            "crop_age_days": crop_age_days,
            "cycle":        self.day_count,
            "sensors":      {
                "moisture_pct": moisture_pct,
                "temperature_C": temp_c,
                "humidity_pct": hum_pct,
                "rain_now": rain_now,
            },
            "weather":      weather,
            "features":     {k: payload[k] for k in ("SMD", "Kc", "ETc", "et0_mm")},
            "decision":     {
                "irrigate":   label,
                "confidence": round(confidence, 4),
                "label":      decision,
                "rain_guard": rain_guard,
            },
        }
        log_decision(record)
        return record

    def run_loop(self):
        log.info("Starting Pi agent | crop=%s field_id=%s interval=%ds",
                 self.crop, self.field_id, self.interval)
        self.setup_gpio()
        try:
            while True:
                self.run_once()
                log.info("Sleeping %ds until next cycle...", self.interval)
                time.sleep(self.interval)
        except KeyboardInterrupt:
            log.info("Interrupted — cleaning up GPIO.")
        finally:
            pump_off()
            GPIO.cleanup()


# ── Entry point ───────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="SanIA Pi Irrigation Agent v4.0")
    parser.add_argument("--crop",     required=True, choices=list(KC_STAGES.keys()),
                        help="Crop type: tomato | potato | apple | grape")
    parser.add_argument("--field-id", required=True, help="Unique field identifier")
    parser.add_argument("--interval", type=int, default=3600,
                        help="Decision interval in seconds (default: 3600 = 1h)")
    parser.add_argument("--dry-run",  action="store_true",
                        help="Run without real GPIO (mock sensors + mock relay)")
    parser.add_argument("--once",     action="store_true",
                        help="Run one decision cycle and exit")
    parser.add_argument("--age",      type=int, default=None,
                        help="Override crop_age_days (default: auto-count cycles)")
    args = parser.parse_args()

    if args.dry_run:
        log.info("DRY-RUN mode — GPIO mocked, sensors simulated.")

    agent = PiAgent(
        crop       = args.crop,
        field_id   = args.field_id,
        interval_sec = args.interval,
    )

    if args.once:
        result = agent.run_once(crop_age_days=args.age)
        if result:
            print(json.dumps(result, indent=2))
    else:
        agent.run_loop()


if __name__ == "__main__":
    main()
