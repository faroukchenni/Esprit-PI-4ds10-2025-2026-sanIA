using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Fullscreen scenario alert overlay. Call Show() from ScenarioManager.
/// Fades in over 0.5 s. Has an X button to dismiss.
/// </summary>
public class ScenarioOverlay : MonoBehaviour
{
    public static ScenarioOverlay Instance { get; private set; }

    // ── Built at runtime if not wired in inspector ────────────────────────────
    private Canvas     _canvas;
    private Image      _bg;
    private TMP_Text   _titleText;
    private TMP_Text   _zoneText;
    private TMP_Text   _statsText;
    private Button     _closeBtn;
    private CanvasGroup _group;

    // ── Colors ────────────────────────────────────────────────────────────────
    public static readonly Color DangerColor  = new Color(0.85f, 0.10f, 0.10f);
    public static readonly Color WarningColor = new Color(0.95f, 0.50f, 0.05f);
    public static readonly Color HealthyColor = new Color(0.10f, 0.80f, 0.30f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildUI();
        HideImmediate();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void BuildUI()
    {
        // Canvas — Screen Space Overlay on top
        GameObject canvasGO = new GameObject("ScenarioOverlayCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        _group = canvasGO.AddComponent<CanvasGroup>();

        // Semi-transparent dark panel (does NOT block the whole screen — centred 600×260)
        GameObject panelGO = new GameObject("AlertPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        _bg = panelGO.AddComponent<Image>();
        _bg.color = new Color(0.04f, 0.04f, 0.08f, 0.92f);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot     = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(620f, 270f);
        panelRT.anchoredPosition = Vector2.zero;

        // Title
        _titleText = MakeText(panelGO.transform, "SCENARIO",
            new Vector2(0f, 80f), new Vector2(580f, 80f), 32f, FontStyles.Bold);

        // Zone
        _zoneText = MakeText(panelGO.transform, "ZONE",
            new Vector2(0f, 20f), new Vector2(580f, 50f), 20f, FontStyles.Normal);

        // Stats
        _statsText = MakeText(panelGO.transform, "",
            new Vector2(0f, -45f), new Vector2(580f, 60f), 15f, FontStyles.Normal);
        _statsText.color = new Color(0.8f, 0.8f, 0.8f);

        // Close button
        GameObject btnGO = new GameObject("CloseBtn");
        btnGO.transform.SetParent(panelGO.transform, false);
        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
        _closeBtn = btnGO.AddComponent<Button>();
        _closeBtn.onClick.AddListener(HideImmediate);

        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 1f);
        btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot     = new Vector2(1f, 1f);
        btnRT.anchoredPosition = new Vector2(-6f, -6f);
        btnRT.sizeDelta = new Vector2(32f, 32f);

        TMP_Text btnLbl = MakeText(btnGO.transform, "✕",
            Vector2.zero, new Vector2(32f, 32f), 16f, FontStyles.Bold);
        btnLbl.alignment = TextAlignmentOptions.Center;
        RectTransform btnLblRT = btnLbl.GetComponent<RectTransform>();
        btnLblRT.anchorMin = Vector2.zero;
        btnLblRT.anchorMax = Vector2.one;
        btnLblRT.offsetMin = Vector2.zero;
        btnLblRT.offsetMax = Vector2.zero;
    }

    TMP_Text MakeText(Transform parent, string text, Vector2 anchoredPos,
                      Vector2 size, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject("Txt_" + text.Substring(0, Mathf.Min(6, text.Length)));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.color     = Color.white;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        return t;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(string title, string zone, string stats, Color accentColor)
    {
        _titleText.text  = title;
        _titleText.color = accentColor;
        _zoneText.text   = zone;
        _zoneText.color  = accentColor * 0.85f + Color.white * 0.15f;
        _statsText.text  = stats;

        // Accent the panel border via background tint
        _bg.color = new Color(0.04f, 0.04f, 0.08f, 0.92f);

        gameObject.SetActive(true);
        if (_canvas != null) _canvas.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    public void HideImmediate()
    {
        if (_group != null) _group.alpha = 0f;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        _group.alpha = 0f;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / 0.5f);
            yield return null;
        }
        _group.alpha = 1f;
    }
}
