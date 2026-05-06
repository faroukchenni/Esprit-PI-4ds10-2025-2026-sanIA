using UnityEngine;

public class MoistureSystem : MonoBehaviour
{
    [Header("Settings")]
    public float dailyEvaporationRate = 0.05f; // Lose 5% moisture per day
    public float nightRecoveryRate = 0.01f;    // Small dew recovery at night

    private TwinSimulationManager sim;

    private void Start()
    {
        sim = TwinSimulationManager.Instance;
        // Subscribe to the end of day event
        sim.OnDayAdvanced += ProcessDailyMoisture;
        sim.OnHourAdvanced += ProcessHourlyMoisture;
    }

    private void ProcessHourlyMoisture(float hour)
    {
        // Simple dew logic: between 2 AM and 5 AM, moisture slightly increases
        if (hour >= 2f && hour <= 5f)
        {
            ApplyMoistureChange(nightRecoveryRate / 3f); // Spread recovery over 3 hours
        }
    }

    private void ProcessDailyMoisture(int day)
    {
        ApplyMoistureChange(-dailyEvaporationRate);
        TwinEventLogger.Log("MOISTURE", $"Daily evaporation applied ({dailyEvaporationRate * 100}%).", "info");
    }

    private void ApplyMoistureChange(float amount)
    {
        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                sim.gridData[x, y].MoistureLevel += amount;
                sim.gridData[x, y].MoistureLevel = Mathf.Clamp01(sim.gridData[x, y].MoistureLevel); // Keep between 0 and 1
            }
        }
    }

    private void OnDestroy()
    {
        if (sim != null)
        {
            sim.OnDayAdvanced -= ProcessDailyMoisture;
            sim.OnHourAdvanced -= ProcessHourlyMoisture;
        }
    }
}
