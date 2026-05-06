using UnityEngine;

/// <summary>
/// Stubbed out — all scenario logic now runs automatically via WeatherSystem,
/// DiseaseManager, and IrrigationDecisionManager. This shell exists only so
/// SelectionHelper.cs compiles; it does nothing at runtime.
/// </summary>
public class ScenarioManager : MonoBehaviour
{
    public static ScenarioManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RunDrought()  { }
    public void RunHeatwave() { }
    public void RunDisease()  { }
    public void RunHealthy()  { }
}
