using UnityEngine;

public class VegetationSystem : MonoBehaviour
{
    [Header("Limits")]
    public float minMoistureForGrowth = 0.3f; // Needs at least 30% moisture to grow
    public float maxDiseaseForGrowth = 0.4f;  // Won't grow if disease is > 40%

    [Header("Rates")]
    public float dailyGrowthRate = 0.02f;   // +2% health per day if conditions are met
    public float droughtDecayRate = 0.15f;  // Faster for testing

    private TwinSimulationManager sim;

    private void Start()
    {
        sim = TwinSimulationManager.Instance;
        sim.OnDayAdvanced += ProcessDailyGrowth;
    }

    private void ProcessDailyGrowth(int day)
    {
        int stressedPlants = 0;

        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                GridCellData cell = sim.gridData[x, y];

                // 1. Update the cell's stress calculation
                cell.UpdateStress();

                // 2. Apply Growth or Decay based on the cell's mathematical state
                if (cell.MoistureLevel < minMoistureForGrowth)
                {
                    // Drought Damage
                    cell.VegetationHealth -= droughtDecayRate;
                    stressedPlants++;
                }
                else if (cell.DiseaseLevel < maxDiseaseForGrowth && cell.StressLevel < 0.5f)
                {
                    // Healthy Growth
                    cell.VegetationHealth += dailyGrowthRate;
                }

                // 3. Keep limits safe
                cell.VegetationHealth = Mathf.Clamp01(cell.VegetationHealth);
            }
        }

        if (stressedPlants > 50)
        {
            TwinEventLogger.Log("VEGETATION", $"Warning: {stressedPlants} plants are suffering from stress/drought.", "warning");
        }
    }
    
    private void OnDestroy()
    {
        if (sim != null)
        {
            sim.OnDayAdvanced -= ProcessDailyGrowth;
        }
    }
}
