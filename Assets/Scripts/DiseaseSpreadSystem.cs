using UnityEngine;

public class DiseaseSpreadSystem : MonoBehaviour
{
    private TwinSimulationManager sim;

    [Header("Spread Mechanics")]
    public float baseSpreadChance = 0.05f; // 5% chance to spread laterally per day
    public float highMoistureBonus = 0.1f; // +10% chance if moisture is very high (fungus loves water)
    public float baseDamageRate = 0.15f;   // Faster for testing

    private void Start()
    {
        sim = TwinSimulationManager.Instance;
        // Disease spreads once per day
        sim.OnDayAdvanced += ProcessDisease;
    }

    private void ProcessDisease(int day)
    {
        // We use a temporary array so we don't spread a new disease multiple times in one day
        float[,] newDiseaseLevels = new float[sim.gridWidth, sim.gridHeight];

        // 1. Calculate new disease levels and spread
        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                GridCellData currentCell = sim.gridData[x, y];
                newDiseaseLevels[x,y] = currentCell.DiseaseLevel; // Copy existing level
                
                // If this cell is infected
                if (currentCell.DiseaseLevel > 0)
                {
                    // It gets worse naturally
                    newDiseaseLevels[x,y] += baseDamageRate;

                    // Try to spread to neighbors (Up, Down, Left, Right)
                    TrySpread(x + 1, y, currentCell, newDiseaseLevels);
                    TrySpread(x - 1, y, currentCell, newDiseaseLevels);
                    TrySpread(x, y + 1, currentCell, newDiseaseLevels);
                    TrySpread(x, y - 1, currentCell, newDiseaseLevels);
                }
            }
        }

        // 2. Apply the calculated spread back to the main grid
        int infectedCount = 0;
        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                sim.gridData[x, y].DiseaseLevel = Mathf.Clamp01(newDiseaseLevels[x, y]);
                if (sim.gridData[x,y].DiseaseLevel > 0) infectedCount++;
            }
        }

        if (infectedCount > 0)
        {
            TwinEventLogger.Log("DISEASE", $"Infection tracking: {infectedCount} zones currently infected.", "warning");
        }
    }

    private void TrySpread(int toX, int toY, GridCellData sourceCell, float[,] newDiseaseLevels)
    {
        // Check grid bounds
        if (toX < 0 || toX >= sim.gridWidth || toY < 0 || toY >= sim.gridHeight) return;

        GridCellData targetCell = sim.gridData[toX, toY];
        
        // Only spread to uninfected or lightly infected cells
        if (targetCell.DiseaseLevel < 0.1f)
        {
            float spreadChance = baseSpreadChance;
            
            // Wet leaves spread fungal diseases faster
            if (targetCell.MoistureLevel > 0.8f) spreadChance += highMoistureBonus;

            if (Random.Range(0f, 1f) < spreadChance)
            {
                // Infection takes hold
                newDiseaseLevels[toX, toY] += 0.1f; 
            }
        }
    }

    // A helper method for testing or for the UI button to trigger an outbreak
    public void CauseRandomOutbreak()
    {
        int rx = Random.Range(0, sim.gridWidth);
        int ry = Random.Range(0, sim.gridHeight);
        
        sim.gridData[rx, ry].DiseaseLevel = 0.5f; // Severe instant infection
        TwinEventLogger.Log("DISEASE", $"CRITICAL OUTBREAK DETECTED at Zone [{rx},{ry}]!", "error");
    }

    private void OnDestroy()
    {
        if (sim != null) sim.OnDayAdvanced -= ProcessDisease;
    }
}
