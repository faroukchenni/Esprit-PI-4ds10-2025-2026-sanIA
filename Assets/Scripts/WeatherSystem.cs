using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    private TwinSimulationManager sim;

    [Header("Weather Settings")]
    public float rainProbability = 0.2f; // 20% chance of rain per day
    public float droughtProbability = 0.05f; // 5% chance of drought
    
    public bool isRaining = false;
    public bool isDrought = false;

    private void Start()
    {
        sim = TwinSimulationManager.Instance;
        sim.OnDayAdvanced += RollWeather;
        sim.OnHourAdvanced += ProcessHourlyWeather;
    }

    private void RollWeather(int day)
    {
        // Reset weather
        isRaining = false;
        
        float roll = Random.Range(0f, 1f);

        if (roll < droughtProbability)
        {
            isDrought = true;
            TwinEventLogger.Log("WEATHER", "Severe drought has begun.", "warning");
        }
        else if (roll < droughtProbability + rainProbability)
        {
            isRaining = true;
            isDrought = false;
            TwinEventLogger.Log("WEATHER", "Rain clouds gathering. Precipitation started.", "info");
        }
        else
        {
            // Normal day
            if (isDrought && Random.Range(0f, 1f) < 0.3f)
            {
                // 30% chance to end drought naturally
                isDrought = false;
                TwinEventLogger.Log("WEATHER", "Drought has ended.", "info");
            }
        }
    }

    private void ProcessHourlyWeather(float hour)
    {
        if (isRaining)
        {
            for (int x = 0; x < sim.gridWidth; x++)
            {
                for (int y = 0; y < sim.gridHeight; y++)
                {
                    // Add 1% moisture per hour of rain
                    sim.gridData[x, y].MoistureLevel += 0.01f;
                    sim.gridData[x, y].MoistureLevel = Mathf.Clamp01(sim.gridData[x, y].MoistureLevel);
                }
            }
        }
        
        if (isDrought)
        {
             for (int x = 0; x < sim.gridWidth; x++)
            {
                for (int y = 0; y < sim.gridHeight; y++)
                {
                    // Higher evaporation during drought, plus hotter temps
                    sim.gridData[x, y].MoistureLevel -= 0.005f; 
                    sim.gridData[x, y].Temperature += 0.1f;
                    sim.gridData[x, y].MoistureLevel = Mathf.Clamp01(sim.gridData[x, y].MoistureLevel);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (sim != null)
        {
            sim.OnDayAdvanced -= RollWeather;
            sim.OnHourAdvanced -= ProcessHourlyWeather;
        }
    }
}
