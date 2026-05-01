/// <summary>
/// IrrigationBridgeBuilder — FarmTwin Editor Menu
/// ================================================
/// Adds the following items to the Unity top menu:
///
///   FarmTwin/Build/Setup Irrigation Bridge        (one-click full setup)
///   FarmTwin/Build/Create DigitalTwin Config      (creates the ScriptableObject asset only)
///   FarmTwin/Irrigation/Validate Scene Setup       (health check — no scene changes)
///   FarmTwin/Irrigation/Clear HUD Irrigation Panel (remove previously built UI panel)
///
/// "Setup Irrigation Bridge" does exactly what the other FarmTwin builders do:
///   1. Creates an "IrrigationBridge" host GameObject in the scene
///   2. Adds SanIAApiClient and IrrigationDecisionManager to it
///   3. Creates (or loads) the DigitalTwinConfig ScriptableObject asset
///   4. Wires IrrigationDecisionManager.config = that asset
///   5. Adds an "SanIA IRRIGATION AI" UI panel to the Canvas with two wired buttons:
///        "Force Decision Now" → IDM.ForceDecisionNow()
///        (backend URL label + status text are runtime-only, shown in FuturisticUI log)
///   6. Marks the scene dirty so Ctrl+S saves everything
///
/// Idempotent: safe to run multiple times — existing components are reused,
/// existing asset is loaded rather than re-created.
/// </summary>

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEditor.SceneManagement;

#if UNITY_EDITOR
public static class IrrigationBridgeBuilder
{
    // ── Asset path for the DigitalTwinConfig ScriptableObject ─────────────────
    private const string CONFIG_ASSET_PATH = "Assets/Resources/DigitalTwinConfig.asset";
    private const string RESOURCES_FOLDER  = "Assets/Resources";

    // ── Host GameObject name ──────────────────────────────────────────────────
    private const string BRIDGE_GO_NAME = "IrrigationBridge";

    // ── Canvas UI panel name ──────────────────────────────────────────────────
    private const string PANEL_NAME = "IrrigationAIPanel";

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Build / Setup Irrigation Bridge
    // ═════════════════════════════════════════════════════════════════════════

    public static void SetupIrrigationBridge()
    {
        int warnings = 0;

        // ── Step 1: Ensure Resources folder exists ────────────────────────────
        if (!AssetDatabase.IsValidFolder(RESOURCES_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
            Debug.Log("[IrrigationBridge] Created Assets/Resources folder.");
        }

        // ── Step 2: Load or create the DigitalTwinConfig asset ────────────────
        DigitalTwinConfig config = LoadOrCreateConfig();
        if (config == null)
        {
            Debug.LogError("[IrrigationBridge] Could not create DigitalTwinConfig asset. Setup aborted.");
            return;
        }

        // ── Step 3: Find or create the host GameObject ────────────────────────
        GameObject host = GameObject.Find(BRIDGE_GO_NAME);
        if (host == null)
        {
            host = new GameObject(BRIDGE_GO_NAME);
            Undo.RegisterCreatedObjectUndo(host, "Create IrrigationBridge GO");
            Debug.Log($"[IrrigationBridge] Created '{BRIDGE_GO_NAME}' GameObject.");
        }
        else
        {
            Debug.Log($"[IrrigationBridge] Reusing existing '{BRIDGE_GO_NAME}' GameObject.");
        }

        // ── Step 4: Add SanIAApiClient (one per scene) ────────────────────────
        SanIAApiClient existingApi = UnityEngine.Object.FindFirstObjectByType<SanIAApiClient>();
        if (existingApi == null)
        {
            Undo.AddComponent<SanIAApiClient>(host);
            Debug.Log("[IrrigationBridge] Added SanIAApiClient component.");
        }
        else if (existingApi.gameObject != host)
        {
            Debug.Log($"[IrrigationBridge] SanIAApiClient already present on '{existingApi.gameObject.name}' — skipped.");
            warnings++;
        }
        else
        {
            Debug.Log("[IrrigationBridge] SanIAApiClient already on IrrigationBridge — OK.");
        }

        // ── Step 5: Add IrrigationDecisionManager ─────────────────────────────
        IrrigationDecisionManager idm = UnityEngine.Object.FindFirstObjectByType<IrrigationDecisionManager>();
        if (idm == null)
        {
            idm = Undo.AddComponent<IrrigationDecisionManager>(host);
            Debug.Log("[IrrigationBridge] Added IrrigationDecisionManager component.");
        }
        else if (idm.gameObject != host)
        {
            Debug.Log($"[IrrigationBridge] IrrigationDecisionManager already on '{idm.gameObject.name}' — reusing.");
            warnings++;
        }
        else
        {
            Debug.Log("[IrrigationBridge] IrrigationDecisionManager already on IrrigationBridge — OK.");
        }

        // ── Step 6: Wire the config asset into IrrigationDecisionManager ──────
        if (idm != null)
        {
            Undo.RecordObject(idm, "Wire DigitalTwinConfig");
            idm.config = config;
            EditorUtility.SetDirty(idm);
            Debug.Log($"[IrrigationBridge] Wired DigitalTwinConfig into IrrigationDecisionManager.");
        }

        // ── Step 7: Warn if mandatory scene components are missing ────────────
        if (UnityEngine.Object.FindFirstObjectByType<TwinSimulationManager>() == null)
        {
            Debug.LogWarning("[IrrigationBridge] TwinSimulationManager not found in scene! " +
                             "IrrigationDecisionManager will disable itself at runtime. " +
                             "Add TwinSimulationManager first.");
            warnings++;
        }
        if (UnityEngine.Object.FindFirstObjectByType<SprinklerSystem>() == null)
        {
            Debug.LogWarning("[IrrigationBridge] SprinklerSystem not found in scene. " +
                             "Run 'FarmTwin/Build Sprinkler Prefab' and add SprinklerSystem to scene first.");
            warnings++;
        }

        // ── Step 8: Build canvas UI panel ─────────────────────────────────────
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            BuildIrrigationPanel(canvas, idm);
        }
        else
        {
            Debug.LogWarning("[IrrigationBridge] No Canvas found in scene — UI panel skipped. " +
                             "Add a Canvas then re-run Setup.");
            warnings++;
        }

        // ── Step 9: Mark scene dirty ──────────────────────────────────────────
        EditorSceneManager.MarkAllScenesDirty();

        // ── Summary ───────────────────────────────────────────────────────────
        if (warnings == 0)
        {
            Debug.Log("[IrrigationBridge] Setup complete — no warnings. " +
                      "Run 'python backend/add_digitaltwin_user.py' to register the backend user, " +
                      "then enter Play Mode. Login happens automatically.");
        }
        else
        {
            Debug.LogWarning($"[IrrigationBridge] Setup complete with {warnings} warning(s). " +
                             "Check messages above before entering Play Mode.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Build / Create DigitalTwin Config
    // ═════════════════════════════════════════════════════════════════════════

    public static void CreateConfigOnly()
    {
        if (!AssetDatabase.IsValidFolder(RESOURCES_FOLDER))
            AssetDatabase.CreateFolder("Assets", "Resources");

        DigitalTwinConfig config = LoadOrCreateConfig();
        if (config != null)
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"[IrrigationBridge] DigitalTwinConfig ready at {CONFIG_ASSET_PATH}. " +
                      "Edit backend URL, credentials, and zone parameters in the Inspector.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Irrigation / Validate Scene Setup
    // ═════════════════════════════════════════════════════════════════════════

    public static void ValidateSetup()
    {
        int ok      = 0;
        int missing = 0;

        void Check(bool condition, string label)
        {
            if (condition) { Debug.Log($"  [OK] {label}"); ok++; }
            else           { Debug.LogWarning($"  [MISSING] {label}"); missing++; }
        }

        Debug.Log("=== IrrigationBridge Scene Validation ===");

        Check(UnityEngine.Object.FindFirstObjectByType<TwinSimulationManager>() != null,
              "TwinSimulationManager");
        Check(UnityEngine.Object.FindFirstObjectByType<SprinklerSystem>() != null,
              "SprinklerSystem");
        Check(UnityEngine.Object.FindFirstObjectByType<SanIAApiClient>() != null,
              "SanIAApiClient");

        IrrigationDecisionManager idm =
            UnityEngine.Object.FindFirstObjectByType<IrrigationDecisionManager>();
        Check(idm != null, "IrrigationDecisionManager");
        if (idm != null)
            Check(idm.config != null, "IrrigationDecisionManager.config is wired");

        Check(AssetDatabase.LoadAssetAtPath<DigitalTwinConfig>(CONFIG_ASSET_PATH) != null,
              $"DigitalTwinConfig asset at {CONFIG_ASSET_PATH}");

        Check(UnityEngine.Object.FindFirstObjectByType<FuturisticUI>() != null,
              "FuturisticUI (HUD)");

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        Check(canvas != null, "Canvas");
        if (canvas != null)
            Check(canvas.transform.Find(PANEL_NAME) != null,
                  $"'{PANEL_NAME}' UI panel on Canvas");

        Debug.Log($"=== Result: {ok} OK, {missing} missing ===");
        if (missing == 0)
            Debug.Log("Scene is fully configured. Enter Play Mode — login fires automatically.");
        else
            Debug.LogWarning("Run 'FarmTwin/Build/Setup Irrigation Bridge' to fix missing items.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MENU: FarmTwin / Irrigation / Clear HUD Irrigation Panel
    // ═════════════════════════════════════════════════════════════════════════

    [MenuItem("FarmTwin/Build/Remove Irrigation Panel")]
    public static void ClearIrrigationPanel()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[IrrigationBridge] No Canvas found in scene.");
            return;
        }

        Transform existing = canvas.transform.Find(PANEL_NAME);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[IrrigationBridge] Removed '{PANEL_NAME}' from Canvas.");
        }
        else
        {
            Debug.Log($"[IrrigationBridge] No '{PANEL_NAME}' panel found — nothing to remove.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INTERNAL: LoadOrCreateConfig
    // ═════════════════════════════════════════════════════════════════════════

    private static DigitalTwinConfig LoadOrCreateConfig()
    {
        // Try loading existing asset first — never overwrite user edits
        DigitalTwinConfig existing =
            AssetDatabase.LoadAssetAtPath<DigitalTwinConfig>(CONFIG_ASSET_PATH);
        if (existing != null)
        {
            Debug.Log($"[IrrigationBridge] Loaded existing DigitalTwinConfig from {CONFIG_ASSET_PATH}.");
            return existing;
        }

        // Create new asset with default values
        DigitalTwinConfig config = ScriptableObject.CreateInstance<DigitalTwinConfig>();
        AssetDatabase.CreateAsset(config, CONFIG_ASSET_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[IrrigationBridge] Created new DigitalTwinConfig at {CONFIG_ASSET_PATH}. " +
                  "Pre-filled with NASA POWER Tunisia parameters.");
        return config;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INTERNAL: BuildIrrigationPanel
    //  Creates a canvas panel anchored to the RIGHT side of the screen,
    //  below the existing telemetry HUD.
    //  Panel contains:
    //    - Header label "SanIA IRRIGATION AI"
    //    - Backend URL label (static display of config.backendUrl)
    //    - "Force Decision Now" button → IDM.ForceDecisionNow()
    //    - 4 zone status labels (Potato / Tomato / Grape / Apple) — updated at runtime
    // ═════════════════════════════════════════════════════════════════════════

    private static void BuildIrrigationPanel(Canvas canvas, IrrigationDecisionManager idm)
    {
        // Remove any stale panel from a previous run
        Transform stale = canvas.transform.Find(PANEL_NAME);
        if (stale != null)
        {
            Undo.DestroyObjectImmediate(stale.gameObject);
            Debug.Log("[IrrigationBridge] Removed stale irrigation panel — rebuilding.");
        }

        // ── Panel background ──────────────────────────────────────────────────
        GameObject panel = new GameObject(PANEL_NAME);
        Undo.RegisterCreatedObjectUndo(panel, "Create IrrigationAIPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        // Anchor: top-right corner, matching the futuristic HUD aesthetic
        panelRT.anchorMin        = new Vector2(1f, 1f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -210f);  // below existing HUD
        panelRT.sizeDelta        = new Vector2(200f, 220f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.04f, 0.90f);  // dark green tint — irrigation theme

        // ── Header ────────────────────────────────────────────────────────────
        AddLabel(panel.transform, "SanIA IRRIGATION AI",
                 new Vector2(100f, 205f), new Vector2(190f, 16f),
                 new Color(0.20f, 0.95f, 0.30f), 9.5f, FontStyles.Bold);

        // ── Separator line ────────────────────────────────────────────────────
        AddLabel(panel.transform, "────────────────────",
                 new Vector2(100f, 192f), new Vector2(190f, 10f),
                 new Color(0.20f, 0.95f, 0.30f, 0.35f), 7f, FontStyles.Normal);

        // ── Backend URL label (informational, static from config) ─────────────
        string url = (idm != null && idm.config != null)
            ? idm.config.backendUrl
            : "http://localhost:8000";

        AddLabel(panel.transform, $"Backend: {url}",
                 new Vector2(100f, 180f), new Vector2(190f, 12f),
                 new Color(0.55f, 0.80f, 0.55f), 7.5f, FontStyles.Normal);

        AddLabel(panel.transform, "Model: SanIA-v3.2-Tunisia",
                 new Vector2(100f, 167f), new Vector2(190f, 12f),
                 new Color(0.55f, 0.80f, 0.55f), 7.5f, FontStyles.Normal);

        // ── Zone status labels (runtime-updated by IrrigationDecisionManager) ─
        // These are named so IrrigationDecisionManager can find them by name at runtime.
        string[] zones = { "Potato", "Tomato", "Grape", "Apple" };
        Color[]  zoneColors =
        {
            new Color(0.96f, 0.84f, 0.37f),  // Potato — golden yellow
            new Color(1.00f, 0.40f, 0.20f),  // Tomato — orange-red
            new Color(0.65f, 0.25f, 0.75f),  // Grape  — purple
            new Color(0.55f, 0.82f, 0.35f),  // Apple  — lime green
        };

        AddLabel(panel.transform, "ZONE STATUS",
                 new Vector2(100f, 152f), new Vector2(190f, 12f),
                 new Color(0.70f, 0.90f, 0.70f), 7f, FontStyles.Bold);

        for (int i = 0; i < zones.Length; i++)
        {
            // Name the GO so IrrigationDecisionManager can find and update it
            string goName  = $"ZoneStatus_{zones[i]}";
            string initTxt = $"{zones[i]}: --";
            float  yPos    = 138f - i * 18f;

            AddLabel(panel.transform, initTxt,
                     new Vector2(100f, yPos), new Vector2(190f, 15f),
                     zoneColors[i], 8f, FontStyles.Normal,
                     goName: goName);
        }

        // ── "Force Decision Now" button ───────────────────────────────────────
        // Wired to IrrigationDecisionManager.ForceDecisionNow() via persistent listener
        GameObject btnGO = CreateButton(
            panel.transform,
            "Force Decision Now",
            "ForceDecisionBtn",
            new Vector2(100f, 22f),
            new Vector2(180f, 30f),
            new Color(0.20f, 0.95f, 0.30f)
        );

        if (idm != null)
        {
            Button btn = btnGO.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, idm.ForceDecisionNow);
            EditorUtility.SetDirty(btn);
            Debug.Log("[IrrigationBridge] 'Force Decision Now' button wired to IrrigationDecisionManager.");
        }
        else
        {
            Debug.LogWarning("[IrrigationBridge] IrrigationDecisionManager is null — " +
                             "'Force Decision Now' button not wired. Re-run setup after adding IDM to scene.");
        }

        // ── Login status label (updated at runtime) ───────────────────────────
        AddLabel(panel.transform, "Status: Not started",
                 new Vector2(100f, 7f), new Vector2(190f, 12f),
                 new Color(0.60f, 0.60f, 0.60f), 7f, FontStyles.Normal,
                 goName: "IrrigationStatus");

        EditorUtility.SetDirty(canvas.gameObject);
        Debug.Log($"[IrrigationBridge] Built '{PANEL_NAME}' on Canvas — " +
                  "top-right corner, below existing HUD.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Creates a TMP label as a child of parent with given parameters.</summary>
    private static GameObject AddLabel(
        Transform parent,
        string    text,
        Vector2   anchoredPos,
        Vector2   sizeDelta,
        Color     color,
        float     fontSize,
        FontStyles style,
        string    goName = null)
    {
        GameObject go = new GameObject(goName ?? "Label");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.color     = color;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;

        return go;
    }

    /// <summary>Creates a styled TMP button with background Image and label.</summary>
    private static GameObject CreateButton(
        Transform parent,
        string    label,
        string    goName,
        Vector2   anchoredPos,
        Vector2   sizeDelta,
        Color     textColor)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.05f, 0.15f, 0.05f, 0.95f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(0.05f, 0.15f, 0.05f, 0.95f);
        cb.highlightedColor = new Color(0.10f, 0.30f, 0.10f, 1.00f);
        cb.pressedColor     = new Color(0.02f, 0.08f, 0.02f, 1.00f);
        btn.colors = cb;

        // Text child
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);

        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.color     = textColor;
        tmp.fontSize  = 9.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        return go;
    }
}
#endif
