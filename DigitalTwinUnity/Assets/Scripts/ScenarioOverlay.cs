using UnityEngine;

/// <summary>
/// Stubbed out — fullscreen scenario overlay no longer used.
/// Shell exists so SelectionHelper.cs compiles.
/// </summary>
public class ScenarioOverlay : MonoBehaviour
{
    public static ScenarioOverlay Instance { get; private set; }

    public static readonly Color DangerColor  = new Color(0.85f, 0.10f, 0.10f);
    public static readonly Color WarningColor = new Color(0.95f, 0.50f, 0.05f);
    public static readonly Color HealthyColor = new Color(0.10f, 0.80f, 0.30f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Show(string title, string zone, string stats, Color accentColor) { }
    public void HideImmediate() { }
}
