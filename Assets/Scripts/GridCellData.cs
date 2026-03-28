using UnityEngine;

public enum CropType { Potato, Tomato, Pepper, Strawberry, Grape, Apple, Empty }

[System.Serializable]
public class GridCellData
{
    public CropType Crop;
    public float MoistureLevel; // 0.0 to 1.0
    public float VegetationHealth; // 0.0 to 1.0
    public float DiseaseLevel; // 0.0 to 1.0
    public float SoilQuality; // 0.0 to 1.0
    public float Temperature; // In Celsius
    public float StressLevel; // Calculated

    public GridCellData(float baseTemp, CropType type = CropType.Tomato)
    {
        Crop = type;
        MoistureLevel = 0.5f; // Start with 50% moisture
        VegetationHealth = 1.0f; // Start 100% healthy
        DiseaseLevel = 0.0f; // No disease
        SoilQuality = 0.8f; // Good soil
        Temperature = baseTemp;
        UpdateStress();
    }

    // ── Disease tracking (managed by DiseaseManager) ──────────────────────────
    public bool  IsInfected;        // true = cell has active disease
    public bool  IsTreated;         // true = treatment applied, immunity window active
    public int   InfectionDay;      // sim day when infection started
    public float InfectionSeverity; // 0–1 severity at time of infection
    public int   TreatmentDay;      // sim day when treatment was applied

    public void UpdateStress()
    {
        // Simple initial formula: High disease or low moisture increases stress.
        StressLevel = (DiseaseLevel * 0.6f) + ((1f - MoistureLevel) * 0.4f);
        StressLevel = Mathf.Clamp01(StressLevel);
    }
}
