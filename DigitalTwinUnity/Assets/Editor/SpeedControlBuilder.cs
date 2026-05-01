/// <summary>
/// SpeedControlBuilder — FarmTwin Editor Menu
/// ============================================
/// FarmTwin/Build/Setup Speed Controls
///
/// One-click creates:
///   1. Ensures SimulationControls component is in the scene
///   2. Builds a "SimSpeedPanel" on the Canvas with:
///        - Title label: "SIM SPEED"
///        - Speed label (auto-wired to FuturisticUI.speedText): shows current mode
///        - 3 buttons: Normal | 7-Day Preview | 30-Day Preview
///          wired to FuturisticUI.OnSpeedNormal/Preview7Day/Preview30Day
///
/// Idempotent: safe to run multiple times.
/// </summary>

using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
public static class SpeedControlBuilder
{
    private const string PANEL_NAME = "SimSpeedPanel";
    private const string SPEED_LABEL_GO = "SimSpeedLabel";

    [MenuItem("FarmTwin/Build/Setup Speed Controls")]
    public static void SetupSpeedControls()
    {
        int warnings = 0;

        // ── Step 1: Ensure SimulationControls is in scene ─────────────────────
        SimulationControls existing = Object.FindFirstObjectByType<SimulationControls>();
        if (existing == null)
        {
            // Add to SimulationCore GO if it exists, otherwise create host
            GameObject host = GameObject.Find("SimulationCore") ?? GameObject.Find("SimControls");
            if (host == null)
            {
                host = new GameObject("SimControls");
                Undo.RegisterCreatedObjectUndo(host, "Create SimControls GO");
            }
            Undo.AddComponent<SimulationControls>(host);
            Debug.Log($"[SpeedControl] Added SimulationControls to '{host.name}'.");
        }
        else
        {
            Debug.Log($"[SpeedControl] SimulationControls already on '{existing.gameObject.name}' — OK.");
        }

        // ── Step 2: Find FuturisticUI ─────────────────────────────────────────
        FuturisticUI ui = Object.FindFirstObjectByType<FuturisticUI>();
        if (ui == null)
        {
            Debug.LogWarning("[SpeedControl] FuturisticUI not found — button click events won't wire. " +
                             "Add FuturisticUI to the scene and re-run.");
            warnings++;
        }

        // ── Step 3: Find Canvas ───────────────────────────────────────────────
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SpeedControl] No Canvas found in scene. Setup aborted.");
            return;
        }

        // ── Step 4: Remove old panel if it exists (idempotent) ────────────────
        Transform old = canvas.transform.Find(PANEL_NAME);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
            Debug.Log("[SpeedControl] Removed old SimSpeedPanel — rebuilding.");
        }

        // ── Step 5: Build the panel ───────────────────────────────────────────
        TMP_Text speedLabel = BuildSpeedPanel(canvas.transform, ui);

        // ── Step 6: Wire speedLabel into FuturisticUI.speedText ───────────────
        if (ui != null && speedLabel != null)
        {
            Undo.RecordObject(ui, "Wire SpeedLabel");
            ui.speedText = speedLabel;
            EditorUtility.SetDirty(ui);
            Debug.Log("[SpeedControl] Wired SimSpeedLabel into FuturisticUI.speedText.");
        }

        // ── Step 7: Mark scene dirty ──────────────────────────────────────────
        EditorSceneManager.MarkAllScenesDirty();

        if (warnings == 0)
            Debug.Log("[SpeedControl] Setup complete.\n" +
                      "  Keyboard: T=cycle speeds | 1=Normal | 2=7-Day Preview | 3=30-Day Preview\n" +
                      "  UI: SimSpeedPanel with 3 buttons added to Canvas.\n" +
                      "  FuturisticUI.speedText wired — shows current speed mode at runtime.");
        else
            Debug.LogWarning($"[SpeedControl] Setup complete with {warnings} warning(s). Check messages above.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Build the speed panel GO tree
    // ═════════════════════════════════════════════════════════════════════════

    private static TMP_Text BuildSpeedPanel(Transform canvasRoot, FuturisticUI ui)
    {
        // ── Panel root ────────────────────────────────────────────────────────
        GameObject panel = new GameObject(PANEL_NAME);
        Undo.RegisterCreatedObjectUndo(panel, "Create SimSpeedPanel");
        panel.transform.SetParent(canvasRoot, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        // Anchor: top-right corner
        panelRT.anchorMin        = new Vector2(1f, 1f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta        = new Vector2(260f, 95f);

        // Semi-transparent dark background
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);

        // ── Title ─────────────────────────────────────────────────────────────
        TMP_Text title = CreateLabel(panel.transform, "SpeedTitle",
            "SIM SPEED", 10f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -5f), new Vector2(240f, 22f));
        title.color     = new Color(0.40f, 0.80f, 1.00f);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // ── Speed mode label (runtime-updated by FuturisticUI.speedText) ──────
        TMP_Text speedLabel = CreateLabel(panel.transform, SPEED_LABEL_GO,
            "NORMAL", 9f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(240f, 18f));
        speedLabel.color     = new Color(0.85f, 0.85f, 0.85f);
        speedLabel.alignment = TextAlignmentOptions.Center;

        // ── 2 buttons in a horizontal row ────────────────────────────────────
        string[] labels  = { "Normal",           "7-Day Preview"     };
        string[] names   = { "Speed1Btn",        "Speed2Btn"         };
        Color[]  colors  = {
            new Color(0.15f, 0.22f, 0.15f),   // dark green for normal
            new Color(0.28f, 0.20f, 0.02f)    // amber for fast
        };
        Color[] textCols = {
            new Color(0.40f, 0.95f, 0.40f),   // green text
            new Color(1.00f, 0.65f, 0.00f)    // amber text
        };

        float btnW  = 110f;
        float startX = -57f;   // center two buttons with a gap

        for (int i = 0; i < 3; i++)
        {
            GameObject btnGO = new GameObject(names[i]);
            btnGO.transform.SetParent(panel.transform, false);

            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin        = new Vector2(0.5f, 1f);
            btnRT.anchorMax        = new Vector2(0.5f, 1f);
            btnRT.pivot            = new Vector2(0.5f, 1f);
            btnRT.anchoredPosition = new Vector2(startX + i * (btnW + 6f), -52f);
            btnRT.sizeDelta        = new Vector2(btnW, 28f);

            Image btnImg   = btnGO.AddComponent<Image>();
            btnImg.color   = colors[i];
            Button btn     = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock cb  = btn.colors;
            cb.highlightedColor = colors[i] + new Color(0.15f, 0.15f, 0.15f);
            cb.pressedColor     = colors[i] * 0.7f;
            btn.colors = cb;

            // Button label
            TMP_Text btnTxt = CreateLabel(btnGO.transform, "Label",
                labels[i], 9f, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            btnTxt.color             = textCols[i];
            btnTxt.alignment         = TextAlignmentOptions.Center;
            btnTxt.fontStyle         = FontStyles.Bold;
            btnTxt.rectTransform.offsetMin = Vector2.zero;
            btnTxt.rectTransform.offsetMax = Vector2.zero;

            // Wire click event to FuturisticUI if present
            if (ui != null)
            {
                if (i == 0)
                {
                    UnityEventTools.AddPersistentListener(btn.onClick, ui.OnSpeedNormal);
                    Debug.Log("[SpeedControl] Wired Speed1Btn -> FuturisticUI.OnSpeedNormal");
                }
                else if (i == 1)
                {
                    UnityEventTools.AddPersistentListener(btn.onClick, ui.OnSpeedPreview7Day);
                    Debug.Log("[SpeedControl] Wired Speed2Btn -> FuturisticUI.OnSpeedPreview7Day");
                }
                else
                {
                    UnityEventTools.AddPersistentListener(btn.onClick, ui.OnSpeedPreview30Day);
                    Debug.Log("[SpeedControl] Wired Speed3Btn -> FuturisticUI.OnSpeedPreview30Day");
                }
            }
        }

        // ── Keyboard hint ────────────────────────────────────────────────────
        TMP_Text hint = CreateLabel(panel.transform, "SpeedHint",
            "T=cycle  |  1=Normal  |  2=7-Day", 7f,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -76f), new Vector2(240f, 16f));
        hint.color     = new Color(0.50f, 0.50f, 0.50f);
        hint.alignment = TextAlignmentOptions.Center;

        return speedLabel;
    }

    // ── Helper: create a TMP_Text child GO ───────────────────────────────────

    private static TMP_Text CreateLabel(Transform parent, string goName, string text,
        float fontSize, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        TMP_Text lbl   = go.AddComponent<TextMeshProUGUI>();
        lbl.text       = text;
        lbl.fontSize   = fontSize;
        lbl.color      = Color.white;
        return lbl;
    }
}
#endif
