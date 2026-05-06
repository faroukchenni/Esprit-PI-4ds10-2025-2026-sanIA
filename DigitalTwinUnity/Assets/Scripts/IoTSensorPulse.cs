using UnityEngine;

/// <summary>
/// Added by FarmPropsBuilder to each sensor pole LED sphere.
/// Pulses emissive intensity and color based on the monitored zone's state:
///   Green slow blink  — zone healthy
///   Amber medium pulse — drought / heatwave active OR low moisture
///   Red fast pulse    — disease detected in zone (faster = more infected)
/// </summary>
public class IoTSensorPulse : MonoBehaviour
{
    [Tooltip("Crop zone this sensor monitors: Potato | Tomato | Grape | Apple")]
    public string zoneName;

    private Material _mat;

    private static readonly Color LED_NORMAL = new Color(0.10f, 1.00f, 0.25f);
    private static readonly Color LED_WARN   = new Color(1.00f, 0.60f, 0.00f);
    private static readonly Color LED_ALERT  = new Color(1.00f, 0.10f, 0.10f);

    private TwinSimulationManager _sim;
    private DiseaseManager        _disease;
    private WeatherSystem         _weather;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) { enabled = false; return; }
        _mat     = rend.material;
        _sim     = TwinSimulationManager.Instance;
        _disease = DiseaseManager.Instance;
        _weather = Object.FindFirstObjectByType<WeatherSystem>();
    }

    void Update()
    {
        Color ledColor;
        float speed;

        float infectionPct = _disease?.GetZoneInfectionPct(zoneName) ?? 0f;

        if (infectionPct > 0.1f)
        {
            ledColor = LED_ALERT;
            speed    = 6f + infectionPct * 10f;
        }
        else if (_weather != null && (_weather.isDrought || _weather.isHeatwave))
        {
            ledColor = LED_WARN;
            speed    = 3f;
        }
        else
        {
            float moisture = GetZoneAvgMoisture();
            if (moisture < 0.35f)
            {
                ledColor = LED_WARN;
                speed    = 2f + (0.35f - moisture) * 8f;
            }
            else
            {
                ledColor = LED_NORMAL;
                speed    = 1.2f;
            }
        }

        float pulse     = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(0.5f, 4f, pulse);

        if (_mat.HasProperty("_BaseColor"))     _mat.SetColor("_BaseColor",     ledColor);
        if (_mat.HasProperty("_EmissionColor")) _mat.SetColor("_EmissionColor", ledColor * intensity);
        _mat.color = ledColor;
    }

    private float GetZoneAvgMoisture()
    {
        if (_sim?.gridData == null) return 0.5f;

        CropType crop;
        switch (zoneName)
        {
            case "Potato": crop = CropType.Potato; break;
            case "Tomato": crop = CropType.Tomato; break;
            case "Grape":  crop = CropType.Grape;  break;
            case "Apple":  crop = CropType.Apple;  break;
            default: return 0.5f;
        }

        float sum = 0f;
        int   n   = 0;
        for (int x = 0; x < _sim.gridWidth; x++)
        for (int y = 0; y < _sim.gridHeight; y++)
        {
            if (_sim.gridData[x, y].Crop != crop) continue;
            sum += _sim.gridData[x, y].MoistureLevel;
            n++;
        }
        return n > 0 ? sum / n : 0.5f;
    }
}
