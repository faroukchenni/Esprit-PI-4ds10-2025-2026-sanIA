using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    [Header("Livestock Panel")]
    [Tooltip("Auto-created at runtime if left empty")]
    public TMP_Text livestockText;

    private AnimalManager _animals;

    // ── Disease panel runtime state ───────────────────────────────────────────
    private Dictionary<string, TMP_Text>   _diseasePctTexts = new Dictionary<string, TMP_Text>();
    private bool  _diseaseActive = false;
    private float _treatFeedback = 0f; // seconds remaining for "Treatment applied!" flash


    void Start()
    {
        EnsureEventSystem();

        TwinEventLogger.OnLogAdded += AddLog;
        AddLog("SYSTEM", "Digital Twin UI Attached", "info");
        InvokeRepeating("UpdateRealTelemetry", 1f, 1f);
        DestroyRuntimeUIRows();

        _animals = FindAnyObjectByType<AnimalManager>();
        if (ShouldBuildRuntimeHudPanels())
        {
            if (livestockText == null) CreateLivestockPanel();
            CreateDiseasePanel();
            if (logContentText == null) CreateLogPanel();
            else RepositionLogPanel();
        }

        // Report RainController state
        RainController rc = UnityEngine.Object.FindFirstObjectByType<RainController>();
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
        TwinSimulationManager sim     = TwinSimulationManager.Instance;
        WeatherSystem         weather = FindAnyObjectByType<WeatherSystem>();
        if (sim == null || sim.gridData == null) return;

        float totalTemp    = 0f;
        float diseaseCount = 0f;
        int   count        = sim.gridWidth * sim.gridHeight;

        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            totalTemp += sim.gridData[x, y].Temperature;
            if (sim.gridData[x, y].DiseaseLevel > 0.1f) diseaseCount++;
        }

        float avgTemp = totalTemp / count;
        float avgHum  = weather != null ? weather.currentHumidity : 55f;

        string status = "STABLE";
        if (weather != null && weather.isHeatwave)
            status = "HEATWAVE";
        else if (weather != null && weather.isDrought)
            status = "DROUGHT";
        else if (diseaseCount > 0)
            status = $"OUTBREAK ({diseaseCount} units)";

        UpdateHUD(avgTemp, avgHum, status);

        if (status != "STABLE" && Time.frameCount % 300 == 0)
            AddLog("SENSOR", $"Alert: {status} detected in sector.", "warn");

        UpdateLivestockPanel();
        UpdateDiseasePanel();
    }

    public void UpdateHUD(float temp, float hum, string status)
    {
        if (temperatureText != null) temperatureText.text = $"{temp:F1}°C";
        if (humidityText != null) humidityText.text = $"{hum:F0}%";
        if (statusAlertText != null) statusAlertText.text = status;

        bool isAlert = status == "ERROR" || status.StartsWith("OUTBREAK")
                    || status == "HEATWAVE" || status == "DROUGHT";
        if (statusDot != null)      statusDot.color      = isAlert ? warningColor : normalColor;
        if (statusAlertText != null) statusAlertText.color = isAlert ? warningColor : Color.white;
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

    CyberGrid GetGrid()
    {
        if (grid != null) return grid;
        grid = UnityEngine.Object.FindFirstObjectByType<CyberGrid>();
        return grid;
    }

    /// <summary>Canvas that owns this HUD (UIManager pattern). Falls back to any scene canvas.</summary>
    Canvas GetHudCanvas()
    {
        return GetComponentInParent<Canvas>(true)
            ?? UnityEngine.Object.FindFirstObjectByType<Canvas>();
    }

    /// <summary>
    /// Avoid stacking two PLANT HEALTH panels when a nested FuturisticUI duplicates the one on the Canvas root.
    /// </summary>
    bool ShouldBuildRuntimeHudPanels()
    {
        if (GetComponent<Canvas>() != null)
            return true;
        for (Transform p = transform.parent; p != null; p = p.parent)
        {
            if (p.GetComponent<FuturisticUI>() != null && p.GetComponent<Canvas>() != null)
                return false;
        }
        return true;
    }

    SprinklerSystem _sprinklerSys;
    SprinklerSystem GetSprinklers()
    {
        if (_sprinklerSys == null) _sprinklerSys = UnityEngine.Object.FindFirstObjectByType<SprinklerSystem>();
        return _sprinklerSys;
    }

    RainController _rainController;
    RainController GetRain()
    {
        if (_rainController == null) _rainController = UnityEngine.Object.FindFirstObjectByType<RainController>();
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

    // ── Livestock panel ───────────────────────────────────────────────────────

    void CreateLivestockPanel()
    {
        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        int penCount = (_animals != null && _animals.pens != null) ? _animals.pens.Length : 4;

        GameObject panel = new GameObject("LivestockPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(10f, 88f); // above the bottom log strip
        rt.sizeDelta        = new Vector2(195f, 22f + penCount * 17f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.10f, 0.88f);

        GameObject txtGO = new GameObject("LivestockText");
        txtGO.transform.SetParent(panel.transform, false);

        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 4f);
        trt.offsetMax = new Vector2(-4f, -4f);

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = 8f;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color     = new Color(0.80f, 0.90f, 1.00f);
        tmp.richText  = true;
        livestockText = tmp;
    }

    void UpdateLivestockPanel()
    {
        if (livestockText == null || _animals == null || _animals.pens == null) return;

        var sb = new System.Text.StringBuilder();
        sb.Append("<b>LIVESTOCK</b>\n");
        foreach (var pen in _animals.pens)
        {
            string col = pen.healthScore >= 80f ? "#33F353"
                       : pen.healthScore >= 50f ? "#FFB800"
                                                : "#FF4033";
            sb.AppendLine(
                $"{pen.animalType.ToUpper()}: {pen.currentCount}  " +
                $"<color={col}>{pen.healthScore:F0}%</color>");
        }
        livestockText.text = sb.ToString().TrimEnd();
    }

    // ── Fallback log panel — created when logContentText not assigned ─────────

    void CreateLogPanel()
    {
        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        // Full-width strip at the very bottom of the screen
        GameObject panel = new GameObject("EventLogPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.12f, 0f);
        prt.anchorMax        = new Vector2(0.88f, 0f);
        prt.pivot            = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 8f);
        prt.sizeDelta        = new Vector2(0f, 70f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.08f, 0.82f);

        // "EVENT LOG" label
        GameObject hdrGO = new GameObject("LogHeader");
        hdrGO.transform.SetParent(panel.transform, false);
        RectTransform hrt = hdrGO.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0,1); hrt.anchorMax = new Vector2(1,1);
        hrt.pivot = new Vector2(0,1);
        hrt.anchoredPosition = new Vector2(8,-3);
        hrt.sizeDelta = new Vector2(-16, 13);
        TextMeshProUGUI hdr = hdrGO.AddComponent<TextMeshProUGUI>();
        hdr.text = "<b>EVENT LOG</b>";
        hdr.fontSize = 7f;
        hdr.color = new Color(0.4f, 0.8f, 1f);

        // Log text body
        GameObject txtGO = new GameObject("LogText");
        txtGO.transform.SetParent(panel.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8, 4);
        trt.offsetMax = new Vector2(-8, -16);

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = 7.5f;
        tmp.color     = new Color(0.78f, 0.88f, 1f);
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.richText  = true;
        logContentText = tmp;
        maxLogLines = 6;
    }

    // ── EventSystem guard — Unity UI buttons need this to receive clicks ─────

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
        Debug.Log("[FuturisticUI] EventSystem auto-created — UI buttons now work.");
    }

    // ── Log panel reposition — move it away from the status panels ────────────

    void RepositionLogPanel()
    {
        if (logContentText == null) return;

        try
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            // Walk up to find the direct child of the Canvas (root panel for this text)
            Transform t = logContentText.transform;
            if (t == null) return;
            while (t.parent != null && t.parent != canvas.transform)
                t = t.parent;

            RectTransform prt = t.GetComponent<RectTransform>();
            if (prt == null) return;

            // Reposition to bottom-centre strip
            prt.anchorMin        = new Vector2(0.12f, 0f);
            prt.anchorMax        = new Vector2(0.88f, 0f);
            prt.pivot            = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 8f);
            prt.sizeDelta        = new Vector2(0f, 72f);

            Image bg = t.GetComponent<Image>();
            if (bg == null && t.gameObject != null)
                bg = t.gameObject.AddComponent<Image>();
            if (bg == null) return;

            bg.color = new Color(0.03f, 0.03f, 0.08f, 0.82f);

            maxLogLines              = 6;
            logContentText.fontSize  = 7.5f;
            logContentText.alignment = TextAlignmentOptions.BottomLeft;
            logContentText.color     = new Color(0.78f, 0.88f, 1f);
            Debug.Log("[FuturisticUI] Log panel repositioned to bottom-centre.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FuturisticUI] RepositionLogPanel skipped: {ex.Message}");
        }
    }

    // ── Disease / Treatment panel ─────────────────────────────────────────────

    static readonly string[] DISEASE_ZONES = { "Potato", "Tomato", "Grape", "Apple" };

    void CreateDiseasePanel()
    {
        if (!ShouldBuildRuntimeHudPanels())
            return;

        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        float rowH   = 20f;
        float panelH = 26f + DISEASE_ZONES.Length * rowH;

        // Panel — bottom right, above livestock panel
        GameObject panel = new GameObject("DiseasePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin        = new Vector2(1f, 0f);
        prt.anchorMax        = new Vector2(1f, 0f);
        prt.pivot            = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-10f, 88f); // above the bottom log strip
        prt.sizeDelta        = new Vector2(225f, panelH);
        Image pbg = panel.AddComponent<Image>();
        pbg.color = new Color(0.04f, 0.04f, 0.10f, 0.88f);
        pbg.raycastTarget = false;

        // Title row
        AddTMPChild(panel, "DiseaseTitle", new Vector2(0,1), new Vector2(1,1),
            new Vector2(6,-4), new Vector2(-6,18), "<b>PLANT HEALTH</b>",
            8f, new Color(0.4f, 0.8f, 1f), TextAlignmentOptions.Left);

        // Zone rows — full-width label, no treat button
        for (int i = 0; i < DISEASE_ZONES.Length; i++)
        {
            string zone = DISEASE_ZONES[i];
            float  yOff = -24f - i * rowH;

            // Row container
            GameObject row = new GameObject($"DiseaseRow_{zone}");
            row.transform.SetParent(panel.transform, false);
            RectTransform rrt = row.AddComponent<RectTransform>();
            rrt.anchorMin        = new Vector2(0, 1);
            rrt.anchorMax        = new Vector2(1, 1);
            rrt.pivot            = new Vector2(0, 1);
            rrt.anchoredPosition = new Vector2(6, yOff);
            rrt.sizeDelta        = new Vector2(-12, rowH - 2);

            // Zone label — full width
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            RectTransform lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            TextMeshProUGUI lbl = labelGO.AddComponent<TextMeshProUGUI>();
            lbl.text      = $"{zone.ToUpper()}: --";
            lbl.fontSize  = 7.5f;
            lbl.color     = new Color(0.8f, 0.9f, 1f);
            lbl.alignment = TextAlignmentOptions.Left;
            _diseasePctTexts[zone] = lbl;
        }

        // Hint row at the bottom
        AddTMPChild(panel, "TreatHint",
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(6, 4), new Vector2(-12, rowH - 2),
            "Press <b>R</b> to restore field",
            7f, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
    }

    // ── Whole-field treatment (M key) ────────────────────────────────────────

    void TreatAllZones()
    {
        DiseaseManager dm = DiseaseManager.Instance
            ?? UnityEngine.Object.FindFirstObjectByType<DiseaseManager>();
        if (dm == null)
        {
            Debug.LogWarning("[FuturisticUI] DiseaseManager missing — cannot treat.");
            return;
        }

        string[] zoneNames = { "Potato", "Tomato", "Grape", "Apple" };
        foreach (string zone in zoneNames)
            dm.TreatZone(zone);

        // Restore full field visuals and environment
        CyberGrid g = GetGrid();
        if (g != null) g.RestoreAllOriginals();

        GetSprinklers()?.DeactivateAll();

        if (temperatureText != null) temperatureText.text = "25.0°C";
        RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);
        Light sun = RenderSettings.sun;
        if (sun != null) sun.color = new Color(1f, 0.957f, 0.839f);

        _diseaseActive = false;
        _treatFeedback = 3f;

        TwinEventLogger.Log("TREATMENT", "Full-field treatment applied (M key) — all zones cleared", "info");
        AddLog("TREATMENT", "M pressed — all zones treated, field restored", "info");
        Debug.Log("[FuturisticUI] M key: TreatAllZones complete.");
    }

    // ── Disease panel update (called every second) ────────────────────────────

    void UpdateDiseasePanel()
    {
        DiseaseManager dm    = DiseaseManager.Instance;
        bool           anyInfected = false;

        foreach (string zone in DISEASE_ZONES)
        {
            if (!_diseasePctTexts.ContainsKey(zone)) continue;

            float pct     = dm?.GetZoneInfectionPct(zone) ?? 0f;
            bool  infected = pct > 0.0001f;
            if (infected) anyInfected = true;

            TMP_Text lbl = _diseasePctTexts[zone];
            if (infected)
            {
                string col = pct > 0.6f ? "#FF4444" : pct > 0.3f ? "#FFB800" : "#FFEE66";
                lbl.text  = $"{zone.ToUpper()}: <color={col}>{pct * 100f:F0}% infected</color>";
            }
            else
            {
                lbl.text  = $"{zone.ToUpper()}: <color=#33FF55>clean</color>";
            }
            lbl.color = new Color(0.8f, 0.9f, 1f);
        }

        _diseaseActive = anyInfected;
    }

    // ── UI helper: create a TMP_Text child inside a parent GO ────────────────

    static void AddTMPChild(
        GameObject parent,
        string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize,
        Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = align;
        tmp.raycastTarget = false; // let parent Button receive clicks
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

    // ── Keyboard debug shortcuts (D / H / I / R / W / M) ────────────────────
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D)) OnDrought();
        if (Input.GetKeyDown(KeyCode.H)) OnHeatwave();
        if (Input.GetKeyDown(KeyCode.I)) OnDisease();
        if (Input.GetKeyDown(KeyCode.R)) OnReset();
        if (Input.GetKeyDown(KeyCode.W)) OnToggleRain();
        if (Input.GetKeyDown(KeyCode.M)) TreatAllZones();

        if (_treatFeedback > 0f) _treatFeedback -= Time.deltaTime;

        UpdateSpeedDisplay();
    }

    // ── OnGUI treatment feedback flash ────────────────────────────────────────
    void OnGUI()
    {
        if (_treatFeedback <= 0f) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize  = 18;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(0.2f, 1f, 0.45f);
        GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height - 90, 440, 44),
            "Treatment applied! Field restored.", style);
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
