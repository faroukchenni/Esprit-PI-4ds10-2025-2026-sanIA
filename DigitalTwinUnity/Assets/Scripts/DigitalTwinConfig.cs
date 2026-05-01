/// <summary>
/// DigitalTwinConfig — ScriptableObject
/// =====================================
/// Create via: Assets → Create → SanIA → Digital Twin Config
/// Drag onto IrrigationDecisionManager's "Config" inspector slot.
///
/// Stores backend connection settings and per-zone irrigation parameters.
/// Defaults are pre-filled to match the SanIA Tunisia dataset (NASA POWER fields).
/// </summary>
using UnityEngine;

[System.Serializable]
public struct ZoneIrrigationConfig
{
    [Tooltip("Must match SprinklerSystem zone name: Potato | Tomato | Grape | Apple")]
    public string zoneName;

    [Tooltip("Stable ID sent to the backend — does not need to be a real DB UUID")]
    public string fieldId;

    [Tooltip("Must be one of: Sandy Loam | Loam | Silt Loam (training set classes)")]
    public string soilType;

    [Tooltip("Volumetric field capacity (%) — e.g. 38 for Sandy Loam")]
    public float fieldCapacity_pct;

    [Tooltip("Permanent wilting point (%) — e.g. 14 for Sandy Loam")]
    public float wiltingPoint_pct;

    [Tooltip("Effective root zone depth (m) — FAO-56 guideline per crop")]
    public float rootZoneDepth_m;

    [Tooltip("Field area for gross volume calculation (m2). Default 1 ha = 10000.")]
    public float area_m2;

    [Tooltip("Irrigation system efficiency (%) — 85 = drip/sprinkler standard")]
    public float appEfficiency_pct;
}

[CreateAssetMenu(fileName = "DigitalTwinConfig", menuName = "SanIA/Digital Twin Config")]
public class DigitalTwinConfig : ScriptableObject
{
    // ── Backend ────────────────────────────────────────────────────────────────

    [Header("Backend Connection")]
    [Tooltip("URL of the running FastAPI backend — no trailing slash")]
    public string backendUrl = "http://localhost:8001";

    [Tooltip("Login email for the SanIA backend — must be a registered user")]
    public string loginEmail = "digitaltwin@sania.ai";

    [Tooltip("Login password for the SanIA backend")]
    public string loginPassword = "sania2025";

    [Header("Irrigation Timing")]
    [Tooltip("How many simulated days between each agent decision call (1 = every day)")]
    public int decisionEveryNDays = 1;

    // ── Zones ──────────────────────────────────────────────────────────────────

    [Header("Zone Configurations")]
    [Tooltip("Pre-filled with the 4 Tunisia NASA POWER field parameters. Edit if needed.")]
    public ZoneIrrigationConfig[] zones = new ZoneIrrigationConfig[]
    {
        // These initializer values are applied when the asset is FIRST created.
        // Existing assets keep their saved values.
        new ZoneIrrigationConfig {
            zoneName          = "Potato",
            fieldId           = "twin_potato",
            soilType          = "Sandy Loam",
            fieldCapacity_pct = 38f,
            wiltingPoint_pct  = 14f,
            rootZoneDepth_m   = 0.40f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Tomato",
            fieldId           = "twin_tomato",
            soilType          = "Sandy Loam",
            fieldCapacity_pct = 38f,
            wiltingPoint_pct  = 14f,
            rootZoneDepth_m   = 0.35f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Grape",
            fieldId           = "twin_grape",
            soilType          = "Loam",
            fieldCapacity_pct = 35f,
            wiltingPoint_pct  = 12f,
            rootZoneDepth_m   = 0.60f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        },
        new ZoneIrrigationConfig {
            zoneName          = "Apple",
            fieldId           = "twin_apple",
            soilType          = "Silt Loam",
            fieldCapacity_pct = 32f,
            wiltingPoint_pct  = 10f,
            rootZoneDepth_m   = 0.80f,
            area_m2           = 10000f,
            appEfficiency_pct = 85f
        }
    };
}
