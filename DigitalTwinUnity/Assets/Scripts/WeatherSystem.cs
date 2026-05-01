using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// WeatherSystem — mirrors real Tunisia weather into the Digital Twin simulation.
///
/// On Start(), fetches from Open-Meteo (free, no API key):
///   temperature, humidity, wind speed, precipitation, weather code, apparent temperature
///
/// These are applied every refreshInterval seconds:
///   Temperature   → all grid cells get baselined to real °C + per-cell noise
///   Wind speed    → drives WindSwaySystem.swayAngle (km/h → degrees)
///   Humidity      → stored as currentHumidity for IrrigationDecisionManager
///   Rain          → RainController.StartRain() / StopRain()
///   Heatwave      → isDrought = true when apparent temp > 37°C
///   Precipitation → RainController.SetRainIntensity() for variable intensity
///
/// Also rolls daily probabilistic sim events that layer on top.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    // ── Runtime state — readable by other systems ──────────────────────────────
    [Header("Current Real Weather (read-only at runtime)")]
    public float currentTemperature  = 25f;   // °C
    public float currentHumidity     = 55f;   // %
    public float currentWindSpeed    = 8f;    // km/h
    public float currentPrecipitation = 0f;  // mm
    public float currentET0_mm       = 5f;   // FAO-56 ET0 mm/day (estimated from temperature)
    public int   currentWeatherCode  = 0;     // WMO code
    public bool  isRaining           = false;
    public bool  isDrought           = false;
    public bool  isHeatwave          = false;

    // ── FAO-56 Kc midseason + TAW per CropType enum index ───────────────────
    // CropType enum: Potato=0, Tomato=1, Pepper=2, Strawberry=3, Grape=4, Apple=5, Empty=6
    // TAW = (FC - WP) * rootDepth * 1000 mm
    // Potato: (38-14)*0.40*1000=96  Tomato: (38-14)*0.35*1000=84
    // Pepper: ~80  Strawberry: ~60  Grape: (35-12)*0.60*1000=138  Apple: (32-10)*0.80*1000=176
    private static readonly float[] KC_BY_CROP  = { 1.15f, 1.15f, 1.05f, 0.85f, 0.85f, 1.20f, 0f };
    private static readonly float[] TAW_BY_CROP = { 96f,   84f,   80f,   60f,  138f,  176f,   0f };

    // ── Inspector settings ─────────────────────────────────────────────────────
    [Header("Sim Weather Settings")]
    public float rainProbability    = 0.15f;
    public float droughtProbability = 0.05f;

    [Header("Backend URL (proxies Open-Meteo — no direct external calls from Unity)")]
    public string backendUrl = "http://localhost:8001/api/v1";
    [Tooltip("Real-world fetch interval in seconds (600 = 10 min)")]
    public float refreshInterval = 600f;

    // ── Open-Meteo daily forecast cache (up to 16 days) ──────────────────────
    private float[] _fcstTemp     = new float[0];
    private float[] _fcstRain     = new float[0];
    private float[] _fcstHumidity = new float[0];
    private int     _fcstDayCount = 0;

    // ── Private refs ───────────────────────────────────────────────────────────
    private TwinSimulationManager _sim;
    private RainController        _rain;
    private WindSwaySystem        _wind;

    // WMO codes that mean precipitation is occurring
    private static readonly int[] RainCodes = {
        51, 53, 55, 56, 57,
        61, 63, 65, 66, 67,
        80, 81, 82,
        95, 96, 99
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        _sim  = TwinSimulationManager.Instance;
        _rain = FindAnyObjectByType<RainController>();
        _wind = FindAnyObjectByType<WindSwaySystem>();

        if (_sim != null)
        {
            _sim.OnDayAdvanced  += RollSimWeather;
            _sim.OnHourAdvanced += ProcessHourlyWeather;
        }

        StartCoroutine(RealWeatherLoop());
        StartCoroutine(ForecastLoop());
    }

    // ── 16-day forecast loop (refresh every 6 real hours) ────────────────────

    private IEnumerator ForecastLoop()
    {
        while (true)
        {
            yield return FetchForecast();
            yield return new WaitForSeconds(6 * 3600f);
        }
    }

    private IEnumerator FetchForecast()
    {
        string url = $"{backendUrl}/weather/forecast";

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[WeatherSystem] Forecast fetch failed: {req.error} — using live temp for all days.");
            yield break;
        }

        string json = req.downloadHandler.text;
        // Backend returns flat JSON — keys are top-level arrays (no "daily" wrapper)
        float[] maxTemps  = ParseFloatArray(json, "temperature_2m_max");
        float[] minTemps  = ParseFloatArray(json, "temperature_2m_min");
        float[] rains     = ParseFloatArray(json, "precipitation_sum");
        float[] humids    = ParseFloatArray(json, "relative_humidity_2m_mean");

        if (maxTemps == null || minTemps == null || maxTemps.Length == 0) yield break;

        int days = maxTemps.Length;
        _fcstTemp     = new float[days];
        _fcstRain     = rains  != null && rains.Length  == days ? rains  : new float[days];
        _fcstHumidity = humids != null && humids.Length == days ? humids : new float[days];
        _fcstDayCount = days;

        for (int i = 0; i < days; i++)
            _fcstTemp[i] = (maxTemps[i] + minTemps[i]) * 0.5f;

        Debug.Log($"[WeatherSystem] Forecast loaded: {days} days. " +
                  $"Day0: T={_fcstTemp[0]:F1}°C Rain={_fcstRain[0]:F1}mm RH={_fcstHumidity[0]:F0}%");
    }

    // ── Real-world weather fetch loop ──────────────────────────────────────────

    private IEnumerator RealWeatherLoop()
    {
        while (true)
        {
            yield return FetchRealWeather();
            yield return new WaitForSeconds(refreshInterval);
        }
    }

    private IEnumerator FetchRealWeather()
    {
        string url = $"{backendUrl}/weather/current";

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[WeatherSystem] Weather fetch failed: {req.error} — using sim-only weather.");
            yield break;
        }

        // Backend returns flat JSON — keys are top-level (no "current" wrapper)
        ParseAndApply(req.downloadHandler.text);
    }

    private void ParseAndApply(string json)
    {
        float temp        = ParseFloat(json, "temperature_2m",      currentTemperature);
        float humidity    = ParseFloat(json, "relative_humidity_2m", currentHumidity);
        float windKmh     = ParseFloat(json, "wind_speed_10m",       currentWindSpeed);
        float precip      = ParseFloat(json, "precipitation",        0f);
        float apparent    = ParseFloat(json, "apparent_temperature",  temp);
        int   code        = ParseInt  (json, "weather_code",         currentWeatherCode);

        currentTemperature   = temp;
        currentHumidity      = humidity;
        currentWindSpeed     = windKmh;
        currentPrecipitation = precip;
        currentWeatherCode   = code;

        bool realRain    = System.Array.IndexOf(RainCodes, code) >= 0;
        bool realHeat    = apparent > 37f;

        Debug.Log($"[WeatherSystem] Tunis weather — " +
                  $"T={temp:F1}°C feels={apparent:F1}°C  RH={humidity:F0}%  " +
                  $"Wind={windKmh:F1}km/h  Precip={precip:F1}mm  Code={code}");

        // ── Apply temperature to grid ─────────────────────────────────────────
        ApplyTemperatureToGrid(temp);

        // ── Drive wind sway ───────────────────────────────────────────────────
        if (_wind != null)
        {
            // 0 km/h → 0.5°, 50 km/h → 8° sway
            _wind.swayAngle = Mathf.Lerp(0.5f, 8f, Mathf.Clamp01(windKmh / 50f));
            _wind.enableGusts = windKmh > 15f;
        }

        // ── Drive rain ────────────────────────────────────────────────────────
        if (realRain && !isRaining)
        {
            isRaining = true;
            isDrought = false;
            // Scale intensity by precipitation amount (0 → 0.3 mm = light, 5+ mm = heavy)
            float intensity = Mathf.Clamp01(precip / 5f);
            if (intensity < 0.1f) intensity = 0.3f; // minimum visible intensity
            _rain?.SetRainIntensity(intensity);
            _rain?.StartRain();
            TwinEventLogger.Log("WEATHER",
                $"Real rain in Tunis (code {code}, {precip:F1}mm). Precipitation started.", "info");
        }
        else if (!realRain && isRaining)
        {
            isRaining = false;
            _rain?.StopRain();
            TwinEventLogger.Log("WEATHER", $"Tunis sky clear (code {code}). Rain stopped.", "info");
        }

        // ── Heatwave / drought ────────────────────────────────────────────────
        if (realHeat && !isDrought)
        {
            isHeatwave = true;
            isDrought  = true;
            TwinEventLogger.Log("WEATHER",
                $"Heatwave in Tunis! Feels like {apparent:F1}°C. Drought conditions active.", "warning");
        }
        else if (!realHeat && isHeatwave)
        {
            isHeatwave = false;
            isDrought  = false;
            TwinEventLogger.Log("WEATHER", "Heatwave ended.", "info");
        }
    }

    // ── Grid temperature application ──────────────────────────────────────────

    private void ApplyTemperatureToGrid(float baseTemp)
    {
        if (_sim?.gridData == null) return;

        for (int x = 0; x < _sim.gridWidth; x++)
        for (int y = 0; y < _sim.gridHeight; y++)
        {
            // Apply real temperature with small spatial noise (±1.5°C variation across field)
            float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 3f - 1.5f;
            _sim.gridData[x, y].Temperature = baseTemp + noise;
        }
    }

    // ── Sim-day probabilistic weather roll ────────────────────────────────────

    private void RollSimWeather(int day)
    {
        // ── Apply forecast data for this simulated day (if loaded) ────────────
        // Days 0-15: use Open-Meteo 16-day forecast (real future temps/rain/RH).
        // Days 16+: fall back to last known live weather.
        if (_fcstDayCount > 0 && day < _fcstDayCount)
        {
            float fcstT  = _fcstTemp[day];
            float fcstR  = _fcstRain[day];
            float fcstRH = _fcstHumidity[day];

            currentTemperature   = fcstT;
            currentHumidity      = fcstRH;
            currentPrecipitation = fcstR;

            ApplyTemperatureToGrid(fcstT);

            bool fcstRaining = fcstR > 0.5f;
            if (fcstRaining && !isRaining)
            {
                isRaining = true;
                isDrought = false;
                float intensity = Mathf.Clamp01(fcstR / 5f);
                if (intensity < 0.1f) intensity = 0.3f;
                _rain?.SetRainIntensity(intensity);
                _rain?.StartRain();
            }
            else if (!fcstRaining && isRaining)
            {
                isRaining = false;
                _rain?.StopRain();
            }

            TwinEventLogger.Log("FORECAST",
                $"Day {day}: T={fcstT:F1}C RH={fcstRH:F0}% Rain={fcstR:F1}mm", "info");
        }

        // ── Hargreaves ET0 estimate (uses currentTemperature — now forecast-driven)
        currentET0_mm = Mathf.Max(1f, 0.18f * currentTemperature - 0.5f);

        // Daily ETc depletion per crop zone (core simulation physics)
        ApplyDailyETDepletion();

        // Don't override rain/drought if real-world or forecast conditions are active
        if (isRaining || isDrought) return;

        float roll = Random.Range(0f, 1f);

        if (roll < droughtProbability)
        {
            isDrought = true;
            _rain?.StopRain();
            TwinEventLogger.Log("WEATHER", "Simulated drought has begun.", "warning");
        }
        else if (roll < droughtProbability + rainProbability)
        {
            isRaining = true;
            _rain?.StartRain();
            TwinEventLogger.Log("WEATHER", "Simulated rain event started.", "info");
        }
        else
        {
            _rain?.StopRain();
            if (isDrought && Random.Range(0f, 1f) < 0.3f)
            {
                isDrought = false;
                TwinEventLogger.Log("WEATHER", "Simulated drought ended.", "info");
            }
        }
    }

    // ── Daily ETc-based soil moisture depletion ───────────────────────────────
    // Each simulated day, crops consume water proportional to ET0 × Kc.
    // Depletion in normalized [0-1] space = ETc_mm / TAW_mm.
    // Rain adds back: each mm of rain = 1/(TAW) normalized units.
    private void ApplyDailyETDepletion()
    {
        if (_sim?.gridData == null) return;

        float rainRecharge = isRaining ? currentET0_mm * 0.8f : 0f; // rough: rain replaces 80% of ET0

        for (int x = 0; x < _sim.gridWidth; x++)
        for (int y = 0; y < _sim.gridHeight; y++)
        {
            GridCellData cell = _sim.gridData[x, y];
            if (cell.Crop == CropType.Empty) continue;

            int cropIdx = (int)cell.Crop;
            if (cropIdx >= KC_BY_CROP.Length || cropIdx >= TAW_BY_CROP.Length) continue;

            float kc     = KC_BY_CROP[cropIdx];
            float taw_mm = TAW_BY_CROP[cropIdx];
            if (taw_mm <= 0f) continue;

            float etc_mm        = currentET0_mm * kc;
            float depletion_norm = etc_mm / taw_mm;
            float recharge_norm  = rainRecharge / taw_mm;

            cell.MoistureLevel = Mathf.Clamp01(cell.MoistureLevel - depletion_norm + recharge_norm);
            cell.UpdateStress();
        }
    }

    // ── Hourly moisture / drought effects ─────────────────────────────────────

    private void ProcessHourlyWeather(float hour)
    {
        if (_sim?.gridData == null) return;

        for (int x = 0; x < _sim.gridWidth; x++)
        for (int y = 0; y < _sim.gridHeight; y++)
        {
            if (isRaining)
            {
                _sim.gridData[x, y].MoistureLevel += 0.01f;
                _sim.gridData[x, y].MoistureLevel  = Mathf.Clamp01(_sim.gridData[x, y].MoistureLevel);
            }

            if (isDrought)
            {
                _sim.gridData[x, y].MoistureLevel -= 0.005f;
                _sim.gridData[x, y].Temperature   += 0.05f;
                _sim.gridData[x, y].MoistureLevel  = Mathf.Clamp01(_sim.gridData[x, y].MoistureLevel);
            }
        }
    }

    // ── JSON helpers (no external library needed) ──────────────────────────────

    private float ParseFloat(string json, string key, float fallback)
    {
        int idx = json.IndexOf($"\"{key}\":");
        if (idx < 0) return fallback;
        idx += key.Length + 3;   // skip "key":
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == '-')) idx++;
        return float.TryParse(json.Substring(start, idx - start),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }

    private int ParseInt(string json, string key, int fallback)
    {
        float v = ParseFloat(json, key, fallback);
        return (int)v;
    }

    // ── JSON array parser (handles Open-Meteo daily arrays) ──────────────────

    private float[] ParseFloatArray(string json, string key)
    {
        int idx = json.IndexOf($"\"{key}\":");
        if (idx < 0) return null;
        idx += key.Length + 3;
        while (idx < json.Length && json[idx] != '[') idx++;
        if (idx >= json.Length) return null;
        idx++; // skip '['
        int end = json.IndexOf(']', idx);
        if (end < 0) return null;

        string[] parts = json.Substring(idx, end - idx).Split(',');
        var list = new System.Collections.Generic.List<float>();
        float last = 0f;
        foreach (string part in parts)
        {
            string t = part.Trim();
            if (t == "null" || t == "") { list.Add(last); continue; }
            if (float.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
            { list.Add(v); last = v; }
        }
        return list.ToArray();
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_sim == null) return;
        _sim.OnDayAdvanced  -= RollSimWeather;
        _sim.OnHourAdvanced -= ProcessHourlyWeather;
    }
}
