using UnityEngine;

public class SimulationControls : MonoBehaviour
{
    public static SimulationControls Instance { get; private set; }

    // Speed presets: Normal | 7-Day Preview
    // timeScale drives hoursDelta per frame: AdvanceTime(deltaTime * timeScale) in hours
    // Normal=1  -> ~10 min/simday (original slow speed, you can watch things happen)
    // Preview7=10 -> ~2.5 min/simday (fast enough to see a week unfold in ~17 min)
    private static readonly float[]  SpeedValues = { 1f,      10f             };
    private static readonly string[] SpeedLabels = { "NORMAL","7-DAY PREVIEW" };

    private int _speedIndex = 0;
    public  int  CurrentSpeedIndex => _speedIndex;
    public  string CurrentSpeedLabel => SpeedLabels[_speedIndex];

    private TwinSimulationManager _sim;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        _sim = TwinSimulationManager.Instance;
        ApplySpeed(_speedIndex);
    }

    void Update()
    {
        // T — cycle through all 3 speed levels
        if (Input.GetKeyDown(KeyCode.T))
            SetSpeedLevel((_speedIndex + 1) % SpeedValues.Length);

        // 1 / 2 / 3 — jump directly
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeedLevel(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeedLevel(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeedLevel(2);

        // O — random disease outbreak
        if (Input.GetKeyDown(KeyCode.O) && DiseaseManager.Instance != null)
        {
            string[] zones = { "Tomato", "Potato", "Grape", "Apple" };
            string zone = zones[Random.Range(0, zones.Length)];
            DiseaseManager.Instance.InfectZone(zone, 0.5f);
        }
    }

    // ── Public API (called by UI buttons) ─────────────────────────────────────

    public void SetSpeedLevel(int level)
    {
        _speedIndex = Mathf.Clamp(level, 0, SpeedValues.Length - 1);
        ApplySpeed(_speedIndex);
    }

    public void SetNormal()       => SetSpeedLevel(0);
    public void SetPreview7Day()  => SetSpeedLevel(1);
    public void SetPreview30Day() => SetSpeedLevel(2);

    private void ApplySpeed(int index)
    {
        if (_sim == null) _sim = TwinSimulationManager.Instance;
        if (_sim == null) return;
        _sim.timeScale = SpeedValues[index];
        TwinEventLogger.Log("CONTROL", $"Simulation speed: {SpeedLabels[index]}", "info");
    }
}
