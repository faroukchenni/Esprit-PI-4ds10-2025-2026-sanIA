/// <summary>
/// IrrigationDecisionManager — Unity ↔ SanIA Backend Bridge
/// ===========================================================
/// Connects the Digital Twin simulation to the SanIA Irrigation Agent (V3.2-Tunisia).
///
/// Architecture:
///   [TwinSimulationManager] → OnDayAdvanced event
///         ↓
///   [IrrigationDecisionManager] reads zone GridCellData averages
///         ↓
///   [SanIAApiClient] POST /api/v1/irrigation/agent/decision
///         ↓
///   XGBoost + Platt calibration + weather modifiers → {irrigate, confidence, volume_m3}
///         ↓
///   [SprinklerSystem] ActivateZone / DeactivateZone
///   [FuturisticUI]    AddLog + status update
///   [GridCellData]    MoistureLevel refill if irrigating
///
/// Zone → Grid mapping (matches TwinSimulationManager.InitializeGrid):
///   Potato:  CropType.Potato cells  (x < 10, y < 10, non-path)
///   Tomato:  CropType.Tomato cells  (x >= 10, y < 10, non-path)
///   Grape:   CropType.Grape cells   (x < 10, y >= 10, non-path)
///   Apple:   CropType.Apple cells   (x >= 10, y >= 10, non-path)
///
/// Moisture conversion (model expects volumetric %):
///   The grid stores MoistureLevel ∈ [0,1] (normalized unitless).
///   Volumetric% = WP + MoistureLevel × (FC - WP)
///   e.g. Potato @ MoistureLevel=0.5 → 14 + 0.5 × (38-14) = 26%
///
/// Setup:
///   1. Add SanIAApiClient component to a GameObject in the scene.
///   2. Add IrrigationDecisionManager to any GameObject (e.g. GameManager).
///   3. Create DigitalTwinConfig asset (Assets → Create → SanIA → Digital Twin Config).
///   4. Drag the config asset into the "Config" inspector slot.
///   5. Auto-references SprinklerSystem, TwinSimulationManager, WeatherSystem
///      via FindAnyObjectByType — no manual wiring needed for those.
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class IrrigationDecisionManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Configuration")]
    [Tooltip("SanIA Digital Twin Config asset. Create via Assets → Create → SanIA → Digital Twin Config.")]
    public DigitalTwinConfig config;

    [Header("Backend Credentials (fallback if no Config asset)")]
    [Tooltip("Used only when Config is null")]
    public string fallbackBackendUrl  = "http://localhost:8001";
    public string fallbackEmail       = "digitaltwin@sania.ai";
    public string fallbackPassword    = "sania2025";

    [Header("Decision Settings")]
    [Tooltip("Run the agent every N simulated days. 1 = every day (matches real crop cycles).")]
    public int decisionEveryNDays = 1;

    [Tooltip("After an IRRIGATE decision, how many sim days to keep sprinklers ON.")]
    public int sprinklerOnDurationDays = 1;

    [Header("Status Polling")]
    [Tooltip("Poll /irrigation/status every N real seconds to refresh the Digital Twin display " +
             "when a Raspberry Pi is pushing decisions independently. 0 = disabled.")]
    public float statusPollIntervalSec = 30f;

    [Header("Debug")]
    [Tooltip("Log every API request payload to the Unity Console for debugging.")]
    public bool verboseLogging = false;

    // ── Runtime references (auto-found) ───────────────────────────────────────

    private SanIAApiClient      _api;
    private SprinklerSystem     _sprinklers;
    private TwinSimulationManager _sim;
    private WeatherSystem       _weather;
    private FuturisticUI        _ui;
    private CyberGrid           _grid;

    // Zone soil colors driven by irrigation state
    private static readonly Color COLOR_IRRIGATE = new Color(0.10f, 0.55f, 0.90f); // blue — watered
    private static readonly Color COLOR_WARN     = new Color(1.00f, 0.55f, 0.00f); // amber — stress
    private static readonly Color COLOR_SKIP     = new Color(0.60f, 0.45f, 0.25f); // dry brown

    // ── Per-zone state ─────────────────────────────────────────────────────────

    // Maps zoneName → remaining days to keep sprinklers on
    private Dictionary<string, int> _sprinklerCountdown = new Dictionary<string, int>();

    // Last decision per zone for UI display
    private Dictionary<string, IrrigationDecisionResult> _lastDecision =
        new Dictionary<string, IrrigationDecisionResult>();

    // Status polling timer
    private float _statusPollTimer = 0f;

    // ── Built-in zone configs (used if Config asset is null) ──────────────────
    // Matches the NASA POWER Tunisia dataset field parameters exactly.
    private static readonly ZoneIrrigationConfig[] DEFAULT_ZONES = new ZoneIrrigationConfig[]
    {
        new ZoneIrrigationConfig {
            zoneName          = "Potato",
            fieldId           = "twin_potato",
            soilType          = "Sandy Loam",
            fieldCapacity_pct = 38f,
            wiltingPoint_pct  = 14f,
            rootZoneDepth_m   = 0.40f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Tomato",
            fieldId           = "twin_tomato",
            soilType          = "Sandy Loam",
            fieldCapacity_pct = 38f,
            wiltingPoint_pct  = 14f,
            rootZoneDepth_m   = 0.35f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Grape",
            fieldId           = "twin_grape",
            soilType          = "Loam",
            fieldCapacity_pct = 35f,
            wiltingPoint_pct  = 12f,
            rootZoneDepth_m   = 0.60f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Apple",
            fieldId           = "twin_apple",
            soilType          = "Silt Loam",
            fieldCapacity_pct = 32f,
            wiltingPoint_pct  = 10f,
            rootZoneDepth_m   = 0.80f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        }
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Auto-find scene components
        _sim        = TwinSimulationManager.Instance;
        _sprinklers = FindAnyObjectByType<SprinklerSystem>();
        _weather    = FindAnyObjectByType<WeatherSystem>();
        _ui         = FindAnyObjectByType<FuturisticUI>();
        _grid       = FindAnyObjectByType<CyberGrid>();

        if (_sim == null)
        {
            Debug.LogError("[IrrigationDecisionManager] TwinSimulationManager not found in scene.");
            enabled = false;
            return;
        }

        // Get or create SanIAApiClient
        _api = FindAnyObjectByType<SanIAApiClient>();
        if (_api == null)
        {
            GameObject apiGO = new GameObject("SanIAApiClient");
            _api = apiGO.AddComponent<SanIAApiClient>();
            DontDestroyOnLoad(apiGO);
        }

        // Resolve credentials
        string url      = config != null ? config.backendUrl    : fallbackBackendUrl;
        string email    = config != null ? config.loginEmail    : fallbackEmail;
        string password = config != null ? config.loginPassword : fallbackPassword;

        if (config != null && config.decisionEveryNDays > 0)
            decisionEveryNDays = config.decisionEveryNDays;

        // Initialize API client → triggers async login
        _api.Initialize(url, email, password);

        // Subscribe to simulation events
        _sim.OnDayAdvanced  += OnDayAdvanced;
        _sim.OnHourAdvanced += OnHourAdvanced;

        // Ensure zone status panel exists — auto-create if IrrigationBridgeBuilder hasn't run
        EnsureZoneStatusPanel();

        // Show connecting state on all zone labels immediately
        SetAllZoneLabelsPlaceholder("-- CONNECTING --", new Color(0.55f, 0.55f, 0.55f));

        TwinEventLogger.Log("IRRIGATION", "IrrigationDecisionManager ready — waiting for login.", "info");

        StartCoroutine(DiseaseSyncLoop());
    }

    // ── Disease sync from backend ─────────────────────────────────────────────

    [System.Serializable] class BackendScan
    {
        public string crop_type;
        public string predicted_disease;
        public float  confidence;
    }
    [System.Serializable] class BackendScanList { public BackendScan[] items; }

    // Maps backend crop_type strings to zone names used by DiseaseManager
    static readonly System.Collections.Generic.Dictionary<string, string> CROP_TO_ZONE =
        new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "potato", "Potato" }, { "tomato", "Tomato" },
        { "grape",  "Grape"  }, { "apple",  "Apple"  },
        { "pomme de terre", "Potato" }, { "tomate", "Tomato" },
        { "raisin", "Grape"  }, { "pomme",  "Apple"  },
    };

    // Track last-seen scan IDs so we only apply each detection once
    readonly System.Collections.Generic.HashSet<string> _appliedScans =
        new System.Collections.Generic.HashSet<string>();

    /// <summary>Latest disease name per zone from backend scans. Read by FuturisticUI for RAG queries.</summary>
    public readonly Dictionary<string, string> ActiveDiseases =
        new Dictionary<string, string>();

    IEnumerator DiseaseSyncLoop()
    {
        // Wait for API login before polling
        float waited = 0f;
        while ((_api == null || !_api.IsReady) && waited < 60f)
        { yield return new WaitForSeconds(2f); waited += 2f; }

        while (true)
        {
            if (_api != null && _api.IsReady)
                _api.GetJson("/api/v1/scans/", ApplyDiseaseScans);
            yield return new WaitForSeconds(30f); // poll every 30 real seconds
        }
    }

    void ApplyDiseaseScans(string json, string err)
    {
        if (err != null) return;
        if (string.IsNullOrEmpty(json)) return;

        BackendScanList list = null;
        try
        {
            // Backend may return array [...] or object {"items":[...]}
            string trimmed = json.Trim();
            if (trimmed.StartsWith("["))
                list = JsonUtility.FromJson<BackendScanList>("{\"items\":" + trimmed + "}");
            else
                list = JsonUtility.FromJson<BackendScanList>(trimmed);
        }
        catch { return; }
        if (list?.items == null) return;

        DiseaseManager dm = DiseaseManager.Instance;
        if (dm == null) return;

        foreach (var scan in list.items)
        {
            if (scan.crop_type == null || scan.predicted_disease == null) continue;
            if (scan.predicted_disease.ToLower().Contains("healthy")) continue;
            if (scan.confidence < 0.5f) continue; // ignore low-confidence

            string key = scan.crop_type + "_" + scan.predicted_disease;
            if (_appliedScans.Contains(key)) continue; // already applied
            _appliedScans.Add(key);

            if (!CROP_TO_ZONE.TryGetValue(scan.crop_type.Trim(), out string zoneName)) continue;

            float severity = Mathf.Clamp01(scan.confidence);
            dm.InfectZone(zoneName, severity);
            ActiveDiseases[zoneName] = scan.predicted_disease; // expose for RAG treatment queries
            TwinEventLogger.Log("DISEASE",
                $"{scan.predicted_disease} on {zoneName} — {severity * 100f:F0}% (backend scan)", "warn");
        }
    }

    private void OnDestroy()
    {
        if (_sim != null)
        {
            _sim.OnDayAdvanced  -= OnDayAdvanced;
            _sim.OnHourAdvanced -= OnHourAdvanced;
        }
    }

    // ── Status polling (real-time) ─────────────────────────────────────────────

    private void Update()
    {
        if (statusPollIntervalSec <= 0f || _api == null || !_api.IsReady) return;

        _statusPollTimer += Time.deltaTime;
        if (_statusPollTimer < statusPollIntervalSec) return;

        _statusPollTimer = 0f;
        _api.PollStatus(OnStatusPolled);
    }

    /// <summary>
    /// Called when /irrigation/status response arrives.
    /// Applies each zone's latest decision to the Digital Twin without
    /// triggering a new sensor reading — suitable when the Pi is the
    /// source of truth and Unity is display-only.
    /// </summary>
    private void OnStatusPolled(IrrigationStatusZone[] zones, string error)
    {
        if (error != null)
        {
            if (verboseLogging)
                TwinEventLogger.Log("IRRIGATION", $"Status poll error: {error}", "warn");
            return;
        }

        if (zones == null || zones.Length == 0) return;

        foreach (var zone in zones)
        {
            if (zone == null) continue;

            // Match field_id to zone name (convention: twin_tomato → Tomato)
            string zoneName = System.Globalization.CultureInfo.CurrentCulture
                .TextInfo.ToTitleCase(zone.field_id.Replace("twin_", "").Split('_')[0]);

            // Synthesise an IrrigationDecisionResult from the status snapshot
            var synth = new IrrigationDecisionResult
            {
                irrigate        = zone.irrigate,
                confidence      = zone.confidence,
                volume_m3       = zone.volume_m3,
                decision_label  = zone.decision_label,
                reason          = $"[status-poll] {zone.decision_label}",
                model_version   = "",
            };

            UpdateZonePanelLabel(zoneName, synth);

            if (verboseLogging)
                TwinEventLogger.Log("IRRIGATION",
                    $"[StatusPoll] {zoneName}: {zone.decision_label} conf={zone.confidence:F2}", "info");
        }
    }

    // ── Day-advance handler ────────────────────────────────────────────────────

    private void OnDayAdvanced(int day)
    {
        TickSprinklerCountdowns();
    }

    // ── Hour-advance handler — fires decisions at 06:00 and 18:00 sim time ───

    private void OnHourAdvanced(float hour)
    {
        int h = Mathf.FloorToInt(hour) % 24;
        if (h != 6 && h != 18) return;

        if (!_api.IsReady)
        {
            TwinEventLogger.Log("IRRIGATION", "Backend not ready — skipping agent decision.", "warn");
            SetAllZoneLabelsPlaceholder("OFFLINE", new Color(0.80f, 0.30f, 0.20f));
            return;
        }

        // Rain active → log it, but still run the agent — it already sees the
        // high moisture values that rain produces, so it will decide SKIP on its own
        // if the soil is adequately hydrated.  Hard-blocking here prevented irrigation
        // from ever firing when the Open-Meteo forecast reported any precipitation.
        if (_weather != null && _weather.isRaining)
            TwinEventLogger.Log("IRRIGATION", "Rain detected — agent will factor hydration into decision.", "info");

        RunZoneDecisions();
    }

    private void RunZoneDecisions()
    {
        ZoneIrrigationConfig[] zones = config != null ? config.zones : DEFAULT_ZONES;

        foreach (var zone in zones)
        {
            IrrigationRequest req = BuildRequest(zone, _sim.currentDay);
            if (req == null) continue;

            if (verboseLogging)
                Debug.Log($"[IrrigationDecisionManager] Sending to {zone.zoneName}: " +
                          $"moisture={req.soil_moisture_pct:F1}% temp={req.temperature_C:F1}C " +
                          $"hum={req.humidity_pct:F1}%");

            string capturedZone = zone.zoneName;
            float  capturedFC   = zone.fieldCapacity_pct;

            _api.RequestDecision(req, (result, error) =>
            {
                if (error != null)
                {
                    TwinEventLogger.Log("IRRIGATION",
                        $"[{capturedZone}] Decision error: {error}", "warn");
                    return;
                }

                // If backend returns 0 volume on an IRRIGATE decision, compute locally
                // using FAO-56: deficit_mm = (FC - smc) * rootDepth_m * 1000 / efficiency
                if (result.irrigate && result.volume_m3 <= 0f)
                {
                    float deficitMm = Mathf.Max(0f, (req.field_capacity_pct - req.soil_moisture_pct) / 100f
                                      * req.root_zone_depth_m * 1000f);
                    float grossMm   = deficitMm / Mathf.Max(0.01f, req.application_efficiency_pct / 100f);
                    result.volume_m3 = Mathf.Round(grossMm * req.area_m2 / 1000f * 100f) / 100f;
                }

                HandleDecision(capturedZone, result, capturedFC, req);
            });
        }
    }

    // ── Build sensor reading from GridCellData ─────────────────────────────────

    /// <summary>
    /// Computes zone-average moisture, temperature and humidity from GridCellData.
    ///
    /// Moisture conversion:
    ///   GridCellData.MoistureLevel ∈ [0,1] (normalized, unitless)
    ///   API expects volumetric% → convert: vol% = WP + MoistureLevel × (FC - WP)
    ///
    /// Humidity:
    ///   WeatherSystem has no explicit humidity field.
    ///   Derived from MoistureLevel + weather state:
    ///     base = 40 + avgMoistureNorm × 35  → [40,75]%
    ///     +20 if raining, -15 if drought
    ///   This is a proxy — the model was trained on real RH2M from NASA POWER.
    ///   The proxy keeps values in the [25,85]% training range.
    /// </summary>
    private IrrigationRequest BuildRequest(ZoneIrrigationConfig zone, int currentDay)
    {
        if (_sim.gridData == null) return null;

        // Map zone name → CropType
        CropType targetCrop;
        switch (zone.zoneName)
        {
            case "Potato": targetCrop = CropType.Potato; break;
            case "Tomato": targetCrop = CropType.Tomato; break;
            case "Grape":  targetCrop = CropType.Grape;  break;
            case "Apple":  targetCrop = CropType.Apple;  break;
            default:
                Debug.LogWarning($"[IrrigationDecisionManager] Unknown zone name: {zone.zoneName}");
                return null;
        }

        float sumMoisture = 0f;
        float sumTemp     = 0f;
        int   count       = 0;

        for (int x = 0; x < _sim.gridWidth; x++)
        {
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                GridCellData cell = _sim.gridData[x, y];
                if (cell.Crop != targetCrop) continue;

                sumMoisture += cell.MoistureLevel;
                sumTemp     += cell.Temperature;
                count++;
            }
        }

        if (count == 0)
        {
            Debug.LogWarning($"[IrrigationDecisionManager] No cells found for zone {zone.zoneName}.");
            return null;
        }

        float avgMoistureNorm = sumMoisture / count;   // [0,1]
        float avgTemp         = sumTemp / count;

        // Volumetric% for the ML model
        float soilMoisturePct = zone.wiltingPoint_pct +
            avgMoistureNorm * (zone.fieldCapacity_pct - zone.wiltingPoint_pct);
        soilMoisturePct = Mathf.Clamp(soilMoisturePct,
            zone.wiltingPoint_pct, zone.fieldCapacity_pct);

        // Use real humidity from WeatherSystem (fetched from Open-Meteo)
        float humidity = _weather != null ? _weather.currentHumidity : 55f;
        humidity = Mathf.Clamp(humidity, 15f, 99f);

        // crop_age_days: offset to mid-season (day 60 = development stage, high Kc)
        // Day 0 at simulation start = initial crop stage (very low Kc, model rarely irrigates).
        // Offsetting to day 60 puts all crops in active growth where irrigation decisions fire.
        int cropAge = (60 + currentDay) % 365;

        // Twin weather → backend uses these for rain_mm / ETc / rain guard (see irrigation_agent.py)
        float twinRain = -1f;
        float twinEt0  = -1f;
        if (_weather != null)
        {
            twinRain = _weather.GetReportedRainMmForIrrigationAgent(currentDay);
            twinEt0  = Mathf.Max(0f, _weather.currentET0_mm);
        }

        return new IrrigationRequest
        {
            field_id                  = zone.fieldId,
            soil_type                 = zone.soilType,
            crop_age_days             = cropAge,
            temperature_C             = avgTemp,
            humidity_pct              = humidity,
            soil_moisture_pct         = soilMoisturePct,
            field_capacity_pct        = zone.fieldCapacity_pct,
            wilting_point_pct         = zone.wiltingPoint_pct,
            area_m2                   = zone.area_m2,
            root_zone_depth_m         = zone.rootZoneDepth_m,
            application_efficiency_pct = zone.appEfficiency_pct,
            twin_rain_mm_24h          = twinRain,
            twin_et0_mm               = twinEt0
        };
    }

    // ── Canvas UI panel zone label updater ────────────────────────────────────

    /// <summary>
    /// Finds the ZoneStatus_{zoneName} TMP_Text label that IrrigationBridgeBuilder
    /// placed on the Canvas and updates it to show the latest decision.
    /// The label GO is named "ZoneStatus_Potato", "ZoneStatus_Tomato", etc.
    /// Graceful no-op if the panel is not present.
    /// </summary>
    private void UpdateZonePanelLabel(string zoneName, IrrigationDecisionResult result)
    {
        string goName = $"ZoneStatus_{zoneName}";
        GameObject labelGO = GameObject.Find(goName);
        if (labelGO == null) return;

        TMP_Text txt = labelGO.GetComponent<TMP_Text>();
        if (txt == null) return;

        string conf  = $"{result.confidence * 100f:F0}%";
        string label = (result.decision_label ?? (result.irrigate ? "IRRIGATE" : "SKIP")).ToUpperInvariant();

        // Build display text
        string display;
        if (label == "IRRIGATE")
            display = $"{zoneName.ToUpper()}: IRRIGATE ({conf})" +
                      (result.volume_m3 > 0f ? $" | {result.volume_m3:F1}m3" : "");
        else if (label == "WARN")
            display = $"{zoneName.ToUpper()}: WARN ({conf})";
        else
            display = $"{zoneName.ToUpper()}: skip ({conf})";

        txt.text = display;

        // Color: IRRIGATE=green, WARN=orange, SKIP=grey, ALERT=red
        switch (label)
        {
            case "IRRIGATE":
                txt.color = new Color(0.20f, 0.95f, 0.30f);  // bright green
                break;
            case "WARN":
                txt.color = new Color(1.00f, 0.65f, 0.00f);  // orange
                break;
            case "ALERT":
                txt.color = new Color(1.00f, 0.25f, 0.20f);  // red
                break;
            default:
                txt.color = new Color(0.55f, 0.55f, 0.55f);  // grey
                break;
        }
    }

    // ── CyberGrid zone color driven by irrigation decision ────────────────────

    private void UpdateZoneGridColor(string zoneName, IrrigationDecisionResult result)
    {
        if (_grid == null) return;

        string label = (result.decision_label ?? "SKIP").ToUpperInvariant();
        switch (label)
        {
            case "IRRIGATE":
                _grid.SetZoneColor(zoneName, COLOR_IRRIGATE);
                break;
            case "WARN":
                _grid.SetZoneColor(zoneName, COLOR_WARN);
                break;
            default:
                _grid.SetZoneColor(zoneName, COLOR_SKIP);
                break;
        }
    }

    // ── Handle agent decision ──────────────────────────────────────────────────

    private void HandleDecision(string zoneName, IrrigationDecisionResult result, float fieldCapacity, IrrigationRequest req)
    {
        // Model decision is used as-is — no fallback override.

        // ── Disease guard: overwatering spreads fungal disease ─────────────────
        // If >40% of the zone is actively infected, hold irrigation to limit spread.
        float infectionPct = DiseaseManager.Instance?.GetZoneInfectionPct(zoneName) ?? 0f;
        if (result.irrigate && infectionPct > 0.4f)
        {
            result.irrigate       = false;
            result.decision_label = "WARN";
            TwinEventLogger.Log("IRRIGATION",
                $"[{zoneName}] Disease guard: irrigation held — {infectionPct * 100f:F0}% infected (overwatering spreads fungal disease)", "warn");
            _ui?.AddLog("AI-AGENT",
                $"{zoneName}: DISEASE GUARD ({infectionPct * 100f:F0}% infected) — irrigation paused", "warn");
        }

        _lastDecision[zoneName] = result;

        // Update canvas UI label
        UpdateZonePanelLabel(zoneName, result);

        // Drive CyberGrid soil color to reflect irrigation state
        UpdateZoneGridColor(zoneName, result);

        string confPct = $"{result.confidence * 100f:F0}%";
        string label   = result.decision_label;  // "IRRIGATE" | "SKIP" | "ALERT"
        
        // Context string for logging
        string ctx = $"[Moist:{req.soil_moisture_pct:F1}% Temp:{req.temperature_C:F1}C Hum:{req.humidity_pct:F1}%]";

        if (result.irrigate)
        {
            // Activate sprinklers for this zone
            _sprinklers?.ActivateZone(zoneName);

            // Schedule auto-deactivation after sprinklerOnDurationDays days
            _sprinklerCountdown[zoneName] = sprinklerOnDurationDays;

            // Reflect in simulation: refill MoistureLevel toward field capacity
            ApplyIrrigationToGrid(zoneName, fieldCapacity);

            TwinEventLogger.Log("IRRIGATION",
                $"[{zoneName}] IRRIGATE | conf={confPct} | {ctx} | vol={result.volume_m3:F1}m3", "info");

            // Update status alert — show label (IRRIGATE or WARN), confidence, volume
            string volStr = result.volume_m3 > 0f ? $" | {result.volume_m3:F1}m³" : "";
            _ui?.AddLog("AI-AGENT",
                $"{zoneName}: {result.decision_label} ({confPct}){volStr} {ctx}", "info");
        }
        else
        {
            // Ensure sprinklers are off (unless still counting down from previous day)
            if (!_sprinklerCountdown.ContainsKey(zoneName) || _sprinklerCountdown[zoneName] <= 0)
                _sprinklers?.DeactivateZone(zoneName);

            TwinEventLogger.Log("IRRIGATION",
                $"[{zoneName}] SKIP | conf={confPct} | {ctx}", "info");

            if (verboseLogging)
                _ui?.AddLog("AI-AGENT", $"{zoneName}: SKIP ({confPct} confident) {ctx}", "info");
        }

        if (label == "ALERT")
        {
            // Sensor miscalibration — log prominently
            TwinEventLogger.Log("IRRIGATION",
                $"[{zoneName}] ALERT: {result.reason} | {ctx}", "warn");
            _ui?.AddLog("AI-AGENT", $"{zoneName}: SENSOR ALERT — {result.reason}", "warn");
        }

        // Update HUD status with aggregate decision summary
        UpdateHUDStatus();
    }

    // ── Sprinkler countdown ticker ─────────────────────────────────────────────

    private void TickSprinklerCountdowns()
    {
        var keys = new List<string>(_sprinklerCountdown.Keys);
        foreach (string zone in keys)
        {
            _sprinklerCountdown[zone]--;
            if (_sprinklerCountdown[zone] <= 0)
            {
                _sprinklerCountdown.Remove(zone);
                _sprinklers?.DeactivateZone(zone);
                // Return soil to dry-brown — moisture is now replenished, next depletion cycle begins
                _grid?.SetZoneColor(zone, COLOR_SKIP);
                TwinEventLogger.Log("IRRIGATION", $"[{zone}] Irrigation cycle complete — sprinklers OFF.", "info");
            }
        }
    }

    // ── Apply irrigation effect to simulation grid ─────────────────────────────

    /// <summary>
    /// When the agent decides to irrigate, refill the zone's soil moisture
    /// toward field capacity in GridCellData so the rest of the simulation
    /// (VegetationSystem, DiseaseManager) sees the effect.
    ///
    /// Refill rate: 80% of field capacity (not 100% — avoids waterlogging).
    /// </summary>
    private void ApplyIrrigationToGrid(string zoneName, float fieldCapacity)
    {
        if (_sim.gridData == null) return;

        // Map zone name → CropType
        CropType targetCrop;
        switch (zoneName)
        {
            case "Potato": targetCrop = CropType.Potato; break;
            case "Tomato": targetCrop = CropType.Tomato; break;
            case "Grape":  targetCrop = CropType.Grape;  break;
            case "Apple":  targetCrop = CropType.Apple;  break;
            default: return;
        }

        // fieldCapacity is the FC%, normalized to [0,1] for MoistureLevel
        // MoistureLevel = 1.0 → soil at FC
        // We refill to 0.80 (80% of FC) to avoid waterlogging
        float targetMoistureNorm = 0.80f;

        for (int x = 0; x < _sim.gridWidth; x++)
        {
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                GridCellData cell = _sim.gridData[x, y];
                if (cell.Crop != targetCrop) continue;

                // Only refill if below target (don't reduce moisture)
                if (cell.MoistureLevel < targetMoistureNorm)
                {
                    cell.MoistureLevel      = Mathf.Lerp(cell.MoistureLevel, targetMoistureNorm, 0.7f);
                    cell.consecutiveDryDays = 0;
                    // Irrigation supports crop recovery — small health boost
                    cell.VegetationHealth   = Mathf.Min(1f, cell.VegetationHealth + 0.05f);
                    cell.UpdateStress();
                }
            }
        }
    }

    // ── HUD status update ──────────────────────────────────────────────────────

    /// <summary>
    /// Updates the FuturisticUI status indicator to reflect current irrigation state.
    /// Shows "IRRIGATING: X zones" when any zone is active, else leaves status to
    /// the existing WeatherSystem logic.
    /// </summary>
    private void UpdateHUDStatus()
    {
        if (_ui == null) return;

        int activeZones = 0;
        foreach (var kv in _sprinklerCountdown)
            if (kv.Value > 0) activeZones++;

        if (activeZones > 0)
        {
            _ui.UpdateHUD(
                GetGridAvgTemp(),
                GetGridAvgMoisture() * 100f,
                $"IRRIGATING: {activeZones} ZONE{(activeZones > 1 ? "S" : "")}"
            );
        }
    }

    // ── Grid helpers ───────────────────────────────────────────────────────────

    private float GetGridAvgTemp()
    {
        if (_sim.gridData == null) return 25f;
        float sum = 0f;
        int count = 0;
        for (int x = 0; x < _sim.gridWidth; x++)
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                if (_sim.gridData[x, y].Crop == CropType.Empty) continue;
                sum += _sim.gridData[x, y].Temperature;
                count++;
            }
        return count > 0 ? sum / count : 25f;
    }

    private float GetGridAvgMoisture()
    {
        if (_sim.gridData == null) return 0.5f;
        float sum = 0f;
        int count = 0;
        for (int x = 0; x < _sim.gridWidth; x++)
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                if (_sim.gridData[x, y].Crop == CropType.Empty) continue;
                sum += _sim.gridData[x, y].MoistureLevel;
                count++;
            }
        return count > 0 ? sum / count : 0.5f;
    }

    // ── Inspector convenience ──────────────────────────────────────────────────

    /// <summary>
    /// Public method for UI buttons — manually trigger agent decisions for all zones.
    /// </summary>
    public void ForceDecisionNow()
    {
        if (_api != null && _api.IsReady)
            RunZoneDecisions();
        else
            TwinEventLogger.Log("IRRIGATION", "ForceDecisionNow: backend not ready.", "warn");
    }

    // ── Zone label placeholder helper ─────────────────────────────────────────

    private static readonly string[] ZONE_NAMES = { "Potato", "Tomato", "Grape", "Apple" };

    private void SetAllZoneLabelsPlaceholder(string text, Color color)
    {
        foreach (string zone in ZONE_NAMES)
        {
            GameObject go = GameObject.Find($"ZoneStatus_{zone}");
            if (go == null) continue;
            TMPro.TMP_Text lbl = go.GetComponent<TMPro.TMP_Text>();
            if (lbl == null) continue;
            lbl.text  = $"{zone.ToUpper()}: {text}";
            lbl.color = color;
        }
    }

    // ── Auto-create irrigation status panel on Canvas ─────────────────────────

    /// <summary>
    /// Builds the SanIA IRRIGATION AI panel on the Canvas if it was never placed
    /// by IrrigationBridgeBuilder. Safe to call multiple times — skips if already present.
    /// </summary>
    private void EnsureZoneStatusPanel()
    {
        // If all labels already exist, nothing to do
        bool allExist = true;
        foreach (string z in ZONE_NAMES)
            if (GameObject.Find($"ZoneStatus_{z}") == null) { allExist = false; break; }
        if (allExist) return;

        UnityEngine.Canvas canvas = FindAnyObjectByType<UnityEngine.Canvas>();
        if (canvas == null) return;

        // Panel container — top LEFT (keeps top-right free for SIM SPEED)
        GameObject panel = new GameObject("IrrigationAIPanel");
        panel.transform.SetParent(canvas.transform, false);

        UnityEngine.RectTransform prt = panel.AddComponent<UnityEngine.RectTransform>();
        prt.anchorMin        = new Vector2(0f, 1f);
        prt.anchorMax        = new Vector2(0f, 1f);
        prt.pivot            = new Vector2(0f, 1f);
        prt.anchoredPosition = new Vector2(10f, -10f); // top-left corner
        prt.sizeDelta        = new Vector2(260f, 20f + ZONE_NAMES.Length * 20f + 10f);

        UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.04f, 0.04f, 0.12f, 0.88f);

        // Title
        CreatePanelLabel(panel, "IrrigationTitle",
            new Vector2(0,1), new Vector2(1,1), new Vector2(6,-4), new Vector2(-6, 16),
            "<b>SanIA IRRIGATION AI</b>", 8f, new Color(0.3f, 0.7f, 1f));

        // One status label per zone
        for (int i = 0; i < ZONE_NAMES.Length; i++)
        {
            string zone = ZONE_NAMES[i];
            float  yOff = -22f - i * 20f;

            if (GameObject.Find($"ZoneStatus_{zone}") != null) continue; // already exists

            CreatePanelLabel(panel, $"ZoneStatus_{zone}",
                new Vector2(0,1), new Vector2(1,1), new Vector2(6, yOff), new Vector2(-6, 18),
                $"{zone.ToUpper()}: -- CONNECTING --", 7.5f, new Color(0.55f, 0.55f, 0.55f));
        }

        Debug.Log("[IrrigationDecisionManager] Zone status panel auto-created on Canvas.");
    }

    static void CreatePanelLabel(
        GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        UnityEngine.RectTransform rt = go.AddComponent<UnityEngine.RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        TMPro.TextMeshProUGUI tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Left;
    }
}
