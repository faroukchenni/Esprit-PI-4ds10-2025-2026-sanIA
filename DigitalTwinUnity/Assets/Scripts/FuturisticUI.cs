using UnityEngine;
using TMPro; // TextMeshPro is required
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages the high-tech HUD for the SanIA Digital Twin.
/// Replicates the Glassmorphism panels from the mockup.
/// </summary>
public class FuturisticUI : MonoBehaviour
{
    [Header("Telemetry Texts")]
    public TMP_Text temperatureText;
    public TMP_Text humidityText;
    public TMP_Text moistureText;
    public TMP_Text statusAlertText;

    [Header("Status Indicators")]
    public Image statusDot; // A small circular image for the status light
    public Color normalColor = Color.green;
    public Color warningColor = Color.red;

    [Header("Log Panel")]
    public TMP_Text logContentText;
    private int maxLogLines = 5;
    private List<string> logs = new List<string>();

    [Header("Speed Display")]
    public TMP_Text speedText;   // drag a TMP label here — shows "NORMAL / 7-DAY PREVIEW / 30-DAY PREVIEW"

    [Header("References")]
    public CyberGrid grid;
    public ScenarioManager scenarioManager;

    void Start()
    {
        TwinEventLogger.OnLogAdded += AddLog;
        AddLog("SYSTEM", "Digital Twin UI Attached", "info");
        InvokeRepeating("UpdateRealTelemetry", 1f, 1f);
        // Clean up any runtime UI rows left over from RainUIBootstrapper
        DestroyRuntimeUIRows();

        // Report RainController state
        RainController rc = Object.FindFirstObjectByType<RainController>();
        if (rc == null)
            Debug.LogWarning("[FuturisticUI] RainController NOT found in scene. Run FarmTwin > Build > Setup Rain Controller.");
        else if (rc.rainScript == null)
            Debug.LogWarning("[FuturisticUI] RainController.rainScript IS NULL — drag RainMakerInstance's BaseRainScript into the field.");
        else
            Debug.Log("[FuturisticUI] RainController OK — rainScript assigned.");
    }

    private void OnDestroy()
    {
        TwinEventLogger.OnLogAdded -= AddLog;
    }

    void UpdateRealTelemetry()
    {
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        WeatherSystem weather = FindAnyObjectByType<WeatherSystem>();
        if (sim == null || sim.gridData == null) return;

        float totalTemp = 0f;
        float totalHum = 0f;
        float diseaseCount = 0f;
        int count = sim.gridWidth * sim.gridHeight;
        
        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                totalTemp += sim.gridData[x, y].Temperature;
                totalHum += sim.gridData[x, y].MoistureLevel;
                if (sim.gridData[x,y].DiseaseLevel > 0.1f) diseaseCount++;
            }
        }
        
        float avgTemp = totalTemp / count;
        float avgHum = (totalHum / count) * 100f; 
        
        string status = "STABLE";
        if (weather != null && weather.isDrought) status = "HEATWAVE";
        else if (diseaseCount > 0) status = $"OUTBREAK ({diseaseCount} units)";

        UpdateHUD(avgTemp, avgHum, status);

        // Add to log if status changed significantly
        if (status != "STABLE" && Time.frameCount % 300 == 0) // Every ~5 seconds
            AddLog("SENSOR", $"Alert: {status} detected in sector.", "warn");
    }

    public void UpdateHUD(float temp, float hum, string status)
    {
        if (temperatureText != null) temperatureText.text = $"{temp:F1}°C";
        if (humidityText != null) humidityText.text = $"{hum:F0}%";
        if (statusAlertText != null) statusAlertText.text = status;

        if (status == "ERROR" || status == "OUTBREAK")
        {
            if (statusDot != null) statusDot.color = warningColor;
            if (statusAlertText != null) statusAlertText.color = warningColor;
        }
        else
        {
            if (statusDot != null) statusDot.color = normalColor;
            if (statusAlertText != null) statusAlertText.color = Color.white;
        }
    }

    public void AddLog(string source, string message, string type = "info")
    {
        string colorTag = type == "info" ? "<color=#388bfd>" : "<color=#f85149>";
        string time = System.DateTime.Now.ToString("HH:mm:ss");
        string entry = $"<color=#8b949e>[{time}]</color> {colorTag}{source}</color>: {message}";
        
        logs.Add(entry);
        if (logs.Count > maxLogLines) logs.RemoveAt(0);

        if (logContentText != null)
        {
            logContentText.text = string.Join("\n", logs);
        }
    }

    // ── Scenario test buttons ─────────────────────────────────────────────────

    /// <summary>
    /// Finds all scene Buttons and wires scenario listeners by name substring.
    /// Also preserves any existing ScenarioManager inspector connections.
    /// </summary>
    void WireScenarioButtons()
    {
        int wired = 0;
        Button[] all = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button btn in all)
        {
            string n = btn.name.ToLower();
            if (n.Contains("drought"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnDrought);
                wired++;
            }
            else if (n.Contains("heatwave") || (n.Contains("heat") && n.Contains("wave")))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnHeatwave);
                wired++;
            }
            else if (n.Contains("disease") || n.Contains("blight") || n.Contains("outbreak"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnDisease);
                wired++;
            }
            else if (n.Contains("reset") || n.Contains("healthy"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnReset);
                wired++;
            }
            else if (n.Contains("rain") || n.Contains("togglerain"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnToggleRain);
                wired++;
            }
            else if (n.Contains("speed1") || n.Contains("speednormal"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnSpeedNormal);
                wired++;
            }
            else if (n.Contains("speed2") || n.Contains("speed7") || n.Contains("preview7"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnSpeedPreview7Day);
                wired++;
            }
            else if (n.Contains("speed3") || n.Contains("speed30") || n.Contains("preview30"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnSpeedPreview30Day);
                wired++;
            }
        }
        Debug.Log($"[FuturisticUI] WireScenarioButtons: wired {wired} button(s). " +
                  "Press D/H/I/R/W to test without clicking.");
    }

    /// <summary>
    /// Creates a "Toggle Rain" button at runtime if no rain button exists in the scene.
    /// Clones the Reset Healthy button (or any other scenario button) as template.
    /// </summary>
    void EnsureRainButton()
    {
        // Skip if a rain button already exists
        Button[] all = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button b in all)
        {
            string n = b.name.ToLower();
            if (n.Contains("rain") || n.Contains("togglerain")) return;
        }

        // Find a template button (Reset Healthy preferred, any scenario button as fallback)
        Button template = null;
        foreach (Button b in all)
        {
            string n = b.name.ToLower();
            if (n.Contains("reset") || n.Contains("healthy")) { template = b; break; }
            if (template == null && (n.Contains("drought") || n.Contains("disease") || n.Contains("heatwave")))
                template = b;
        }

        if (template == null)
        {
            Debug.LogWarning("[FuturisticUI] EnsureRainButton: no scenario button found to clone. " +
                             "Add a Button named 'ToggleRain' manually to the Canvas.");
            return;
        }

        // Clone the template and place it as the next sibling
        GameObject btnGO = Instantiate(template.gameObject, template.transform.parent);
        btnGO.name = "ToggleRainBtn";

        // Set label text
        TMP_Text lbl = btnGO.GetComponentInChildren<TMP_Text>();
        if (lbl != null) lbl.text = "Toggle Rain";
        else
        {
            Text legacyLbl = btnGO.GetComponentInChildren<Text>();
            if (legacyLbl != null) legacyLbl.text = "Toggle Rain";
        }

        // Move to end of parent (after Reset Healthy)
        btnGO.transform.SetAsLastSibling();

        // Wire click
        Button newBtn = btnGO.GetComponent<Button>();
        newBtn.onClick.RemoveAllListeners();
        newBtn.onClick.AddListener(OnToggleRain);

        Debug.Log("[FuturisticUI] Toggle Rain button created and wired.");
    }

    CyberGrid GetGrid()
    {
        if (grid != null) return grid;
        grid = Object.FindFirstObjectByType<CyberGrid>();
        return grid;
    }

    SprinklerSystem _sprinklerSys;
    SprinklerSystem GetSprinklers()
    {
        if (_sprinklerSys == null) _sprinklerSys = Object.FindFirstObjectByType<SprinklerSystem>();
        return _sprinklerSys;
    }

    RainController _rainController;
    RainController GetRain()
    {
        if (_rainController == null) _rainController = Object.FindFirstObjectByType<RainController>();
        return _rainController;
    }

    // ── Inspector button methods (delegate to new implementations) ────────────
    public void OnDroughtTest()   { OnDrought();  }
    public void OnHeatwaveTest()  { OnHeatwave(); }
    public void OnDiseaseTest()   { OnDisease();  }
    public void OnResetHealthy()  { OnReset();    }

    // ── Scenario implementations ──────────────────────────────────────────────

    void OnDrought()
    {
        Debug.Log("[FuturisticUI] DROUGHT BUTTON PRESSED");
        CyberGrid g = GetGrid();
        if (g == null) { Debug.LogError("[FuturisticUI] CyberGrid not found!"); return; }

        g.SetZoneColor("Potato",     new Color(0.831f, 0.659f, 0.333f)); // #D4A855 dry cracked yellow
        g.SetZoneCropColor("Potato", new Color(0.769f, 0.627f, 0.314f)); // #C4A050 yellowing leaves

        if (temperatureText != null) temperatureText.text = "42.0°C";

        Light sun = RenderSettings.sun;
        if (sun != null) sun.color = new Color(1f, 0.549f, 0f);  // #FF8C00 harsh desert sun

        // Activate Potato zone sprinklers to visualise drought-stress irrigation response
        GetSprinklers()?.ActivateZone("Potato");

        TwinEventLogger.Log("SCENARIO", "DROUGHT — Auto irrigation activated", "warn");
        Debug.Log("[FuturisticUI] DROUGHT APPLIED");
    }

    void OnHeatwave()
    {
        Debug.Log("[FuturisticUI] HEATWAVE BUTTON PRESSED");
        CyberGrid g = GetGrid();
        if (g == null) { Debug.LogError("[FuturisticUI] CyberGrid not found!"); return; }

        g.SetAllZoneColors(new Color(0.722f, 0.361f, 0f));        // #B85C00 scorched soil
        g.SetAllCropColors(new Color(0.545f, 0.271f, 0.075f));    // #8B4513 burnt brown

        if (temperatureText != null) temperatureText.text = "47.0°C";
        RenderSettings.ambientLight = new Color(0.3f, 0.1f, 0f);

        TwinEventLogger.Log("SCENARIO", "HEATWAVE — All zones critical", "warn");
        Debug.Log("[FuturisticUI] HEATWAVE APPLIED");
    }

    void OnDisease()
    {
        Debug.Log("[FuturisticUI] DISEASE BUTTON PRESSED");

        // Use DiseaseManager for full spread simulation
        if (DiseaseManager.Instance != null)
        {
            DiseaseManager.Instance.InfectZone("Tomato", 0.85f);
            if (temperatureText != null) temperatureText.text = "17.0°C";
            Debug.Log("[FuturisticUI] DISEASE APPLIED via DiseaseManager");
            return;
        }

        // Fallback if DiseaseManager is not in the scene
        Debug.LogWarning("[FuturisticUI] DiseaseManager not found — add it via FarmTwin/Build/Build Disease System");
    }

    void OnReset()
    {
        Debug.Log("[FuturisticUI] RESET BUTTON PRESSED");
        CyberGrid g = GetGrid();
        if (g == null) { Debug.LogError("[FuturisticUI] CyberGrid not found!"); return; }

        g.RestoreAllOriginals();

        // Clear all disease states
        DiseaseManager.Instance?.ClearAllDiseases();

        // Deactivate all sprinklers
        GetSprinklers()?.DeactivateAll();

        // Reset rain (clear manual override, turn off)
        GetRain()?.StopRain();

        if (temperatureText != null) temperatureText.text = "25.0°C";
        RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);

        Light sun = RenderSettings.sun;
        if (sun != null) sun.color = new Color(1f, 0.957f, 0.839f);  // #FFD4A0 warm daylight

        TwinEventLogger.Log("SCENARIO", "ALL SYSTEMS NORMAL", "info");
        Debug.Log("[FuturisticUI] RESET APPLIED");
    }

    // ── Toggle Rain ───────────────────────────────────────────────────────────
    void OnToggleRain()
    {
        RainController rain = GetRain();
        if (rain != null) rain.Toggle();
        else Debug.LogWarning("[FuturisticUI] RainController not found. Run FarmTwin > Build > Setup Rain Controller.");
    }

    // ── Cleanup RainUIBootstrapper rows if they were created ──────────────────
    void DestroyRuntimeUIRows()
    {
        string[] names = { "RainControls_Row", "SprinklerControls_Row" };
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) Destroy(go);
        }
    }

    // ── Keyboard debug shortcuts (D / H / I / R / W) ─────────────────────────
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D)) OnDrought();
        if (Input.GetKeyDown(KeyCode.H)) OnHeatwave();
        if (Input.GetKeyDown(KeyCode.I)) OnDisease();
        if (Input.GetKeyDown(KeyCode.R)) OnReset();
        if (Input.GetKeyDown(KeyCode.W)) OnToggleRain();

        UpdateSpeedDisplay();
    }

    void UpdateSpeedDisplay()
    {
        if (speedText == null || SimulationControls.Instance == null) return;
        speedText.text = $"SIM: {SimulationControls.Instance.CurrentSpeedLabel}";
    }

    // ── Speed control (called by canvas buttons or keyboard via SimulationControls) ──
    public void OnSpeedNormal()      => SimulationControls.Instance?.SetNormal();
    public void OnSpeedPreview7Day() => SimulationControls.Instance?.SetPreview7Day();
    public void OnSpeedPreview30Day()=> SimulationControls.Instance?.SetPreview30Day();

    // ── Legacy simulation buttons ─────────────────────────────────────────────
    public void OnHeatwaveClick()
    {
        TwinEventLogger.Log("SCENARIO", "Heatwave protocol engaged (+5°C)", "warn");
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (sim != null && sim.gridData != null)
        {
            for (int x = 0; x < sim.gridWidth; x++)
            {
                for (int y = 0; y < sim.gridHeight; y++)
                {
                    sim.gridData[x, y].Temperature += 5f;
                    sim.gridData[x, y].MoistureLevel = Mathf.Max(0f, sim.gridData[x, y].MoistureLevel - 0.2f);
                }
            }
        }
    }

    public void OnOutbreakClick()
    {
        TwinEventLogger.Log("COMMAND", "Manual Outbreak trigger pressed.", "warn");
        if (DiseaseManager.Instance != null)
        {
            DiseaseManager.Instance.InfectZone("Tomato", 0.5f);
        }
    }
}
