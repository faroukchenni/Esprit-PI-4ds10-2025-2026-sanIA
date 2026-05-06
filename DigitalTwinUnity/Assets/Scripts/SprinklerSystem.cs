using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates and manages a grid of 16 sprinkler particle systems (4 zones × 4 corners).
/// Each zone's sprinklers are parented under "IrrigationSystem" → "Zone_XXX" GameObjects.
///
/// Zone corners are derived from the TwinSimulationManager grid dimensions:
///   spacing = 2.2 units, gridWidth = gridHeight = 20
///   Zones separated by 2-unit road at rows/cols 9-10 (centerX = 10)
///
///   POTATO FIELD   : x 0-8,  z 0-8   (world 0–17.6, 0–17.6)
///   TOMATO FIELD   : x 11-19, z 0-8  (world 24.2–41.8, 0–17.6)
///   GRAPE VINEYARD : x 0-8,  z 11-19 (world 0–17.6, 24.2–41.8)
///   APPLE ORCHARD  : x 11-19, z 11-19(world 24.2–41.8, 24.2–41.8)
///
/// Soil color change is driven by SprinklerZoneInfo.moistenCoroutine which lerps each
/// SpawnedCellInfo.soilTile material toward a moist colour over 5 seconds.
/// </summary>
public class SprinklerSystem : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────

    [Header("Sprinkler Prefab")]
    [Tooltip("Drag Assets/Prefabs/Sprinkler.prefab here (auto-found if blank)")]
    public GameObject sprinklerPrefab;

    [Tooltip("Assign a URP Particles/Unlit material (transparent, any color — tint is applied at runtime). " +
             "Leave null to auto-create; assign a proper material here if particles appear pink.")]
    public Material waterParticleMaterial;

    [Header("References")]
    [Tooltip("CyberGrid reference for spawnedCells soil colour lerp. Auto-found if blank.")]
    public CyberGrid cyberGrid;

    [Header("Soil Moisture Colours")]
    public Color drySoilColor   = new Color(0.47f, 0.27f, 0.14f); // earthy brown
    public Color moistSoilColor = new Color(0.25f, 0.15f, 0.07f); // dark wet soil

    [Tooltip("Seconds to transition soil from dry to moist")]
    public float moistenDuration = 5f;

    // ── Runtime state ──────────────────────────────────────────────────────

    private class SprinklerZoneInfo
    {
        public string zoneName;
        public List<ParticleSystem> sprinklers = new List<ParticleSystem>();
        public bool isActive = false;
        public Coroutine moistenCoroutine;
    }

    private Dictionary<string, SprinklerZoneInfo> zones = new Dictionary<string, SprinklerZoneInfo>();

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        // Auto-find CyberGrid
        if (cyberGrid == null) cyberGrid = FindAnyObjectByType<CyberGrid>();

        // Auto-find sprinkler prefab if not set
        // (the prefab is created at Assets/Prefabs/Sprinkler.prefab via this script at runtime)
        if (sprinklerPrefab == null)
        {
            Debug.LogWarning("[SprinklerSystem] sprinklerPrefab not assigned. Sprinklers will be built from primitives.");
        }

        BuildIrrigationSystem();
        DeactivateAll();
        Debug.Log("[SprinklerSystem] Irrigation system ready. 16 sprinklers in 4 zones.");
    }

    // ── Build ─────────────────────────────────────────────────────────────

    void BuildIrrigationSystem()
    {
        // Grid constants matching TwinSimulationManager / CyberGrid
        float sp = 2.2f;   // cell spacing

        // Zone world-space extents [minX, maxX, minZ, maxZ]
        // Col indices for zones: left=0-8, right=11-19 (after 2-wide road at 9-10)
        // Row indices: bottom=0-8, top=11-19
        var zoneData = new (string name, float x0, float x1, float z0, float z1)[]
        {
            ("Potato",  0f * sp,  8f * sp, 0f * sp,  8f * sp),   // rows 0-8, cols 0-8
            ("Tomato", 11f * sp, 19f * sp, 0f * sp,  8f * sp),   // rows 0-8, cols 11-19
            ("Grape",   0f * sp,  8f * sp, 11f * sp, 19f * sp),  // rows 11-19, cols 0-8
            ("Apple",  11f * sp, 19f * sp, 11f * sp, 19f * sp),  // rows 11-19, cols 11-19
        };

        foreach (var zd in zoneData)
        {
            var zoneInfo = new SprinklerZoneInfo { zoneName = zd.name };

            GameObject zoneParent = new GameObject($"Sprinklers_{zd.name}");
            zoneParent.transform.parent = this.transform;

            // 4 corners
            Vector3[] corners = new Vector3[]
            {
                new Vector3(zd.x0, 0f, zd.z0), // SW
                new Vector3(zd.x1, 0f, zd.z0), // SE
                new Vector3(zd.x0, 0f, zd.z1), // NW
                new Vector3(zd.x1, 0f, zd.z1), // NE
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject sprinklerGO;

                if (sprinklerPrefab != null)
                {
                    sprinklerGO = Instantiate(sprinklerPrefab, zoneParent.transform);
                }
                else
                {
                    // Build a minimal particle-system sprinkler procedurally
                    sprinklerGO = BuildProceduralSprinkler(zoneParent.transform);
                }

                sprinklerGO.name = $"Sprinkler_{zd.name}_{i}";
                sprinklerGO.transform.localPosition = corners[i];

                ParticleSystem ps = sprinklerGO.GetComponent<ParticleSystem>();
                if (ps == null) ps = sprinklerGO.GetComponentInChildren<ParticleSystem>();

                if (ps != null)
                {
                    FixParticleMaterial(ps);
                    zoneInfo.sprinklers.Add(ps);
                }
                else
                    Debug.LogError($"[SprinklerSystem] No ParticleSystem on sprinkler prefab for {zd.name}[{i}]!");
            }

            zones[zd.name] = zoneInfo;

            Debug.Log($"[SprinklerSystem] Zone '{zd.name}': {zoneInfo.sprinklers.Count} sprinklers at " +
                      $"X[{zd.x0:F1}-{zd.x1:F1}] Z[{zd.z0:F1}-{zd.z1:F1}]");
        }
    }

    /// <summary>Creates a procedural sprinkler particle system without a prefab.</summary>
    GameObject BuildProceduralSprinkler(Transform parent)
    {
        GameObject go = new GameObject("Sprinkler");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // ── Main module ───────────────────────────────────────────────────
        var main = ps.main;
        main.startSpeed      = 4f;
        main.startSize       = 0.08f;
        main.startLifetime   = 2f;
        main.startColor      = new Color(0.529f, 0.808f, 0.922f, 0.8f); // #87CEEB sky blue
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop            = true;
        main.playOnAwake     = false;
        main.maxParticles    = 500;

        // ── Emission ──────────────────────────────────────────────────────
        var emission = ps.emission;
        emission.rateOverTime = 80f;
        emission.enabled      = false; // off by default

        // ── Shape: Cone 60° ───────────────────────────────────────────────
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 60f;
        shape.radius    = 0.05f;

        // ── Colour over lifetime ──────────────────────────────────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.529f, 0.808f, 0.922f), 0f),
                new GradientColorKey(new Color(0.529f, 0.808f, 0.922f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = g;

        // ── Renderer mode ─────────────────────────────────────────────────
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        // Material is applied via FixParticleMaterial called after Build returns.
        return go;
    }

    // ── Material fix — applied to every particle system (prefab or procedural) ──

    static readonly Color _waterBlue = new Color(0.529f, 0.808f, 0.922f, 0.85f);

    /// <summary>
    /// Ensures the ParticleSystemRenderer uses a URP-compatible blue material.
    /// Priorities: (1) waterParticleMaterial inspector field, (2) any URP Particles shader,
    /// (3) any URP Unlit shader. Pink = Built-in default — this method replaces it.
    /// </summary>
    void FixParticleMaterial(ParticleSystem ps)
    {
        ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
        if (psr == null) return;

        // If the user provided a material in the inspector, clone it and tint it.
        if (waterParticleMaterial != null)
        {
            Material m = new Material(waterParticleMaterial);
            ApplyWaterColor(m);
            psr.material = m;
            return;
        }

        // Otherwise try to find a URP particle/unlit shader at runtime.
        string[] candidates = {
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Particles/Simple Lit",
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Unlit",
        };

        Shader sh = null;
        foreach (string sn in candidates)
        {
            sh = Shader.Find(sn);
            if (sh != null) break;
        }

        if (sh == null)
        {
            Debug.LogWarning("[SprinklerSystem] URP particle shader not found — assign 'waterParticleMaterial' in the inspector to fix pink particles.");
            return;
        }

        Material mat = new Material(sh);
        // URP transparent setup
        mat.SetFloat("_Surface",  1f); // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend",    0f); // Alpha blend
        mat.SetFloat("_ZWrite",   0f);
        mat.SetInt("_SrcBlend",   (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",   (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ApplyWaterColor(mat);
        psr.material = mat;
    }

    static void ApplyWaterColor(Material m)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", _waterBlue);
        if (m.HasProperty("_Color"))     m.SetColor("_Color",     _waterBlue);
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", _waterBlue);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Activates sprinklers for the given zone and begins moistening its soil tiles.
    /// zoneName must match: "Potato", "Tomato", "Grape", or "Apple".
    /// </summary>
    public void ActivateZone(string zoneName)
    {
        if (!zones.ContainsKey(zoneName))
        {
            Debug.LogWarning($"[SprinklerSystem] Unknown zone '{zoneName}'.");
            return;
        }

        SprinklerZoneInfo info = zones[zoneName];
        info.isActive = true;

        foreach (var ps in info.sprinklers)
        {
            if (ps == null) continue;
            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 80f;
            if (!ps.isPlaying) ps.Play();
        }

        // Start soil moistening coroutine
        if (info.moistenCoroutine != null) StopCoroutine(info.moistenCoroutine);
        info.moistenCoroutine = StartCoroutine(MoistenSoil(zoneName, drySoilColor, moistSoilColor, moistenDuration));

        TwinEventLogger.Log("IRRIGATION", $"Zone {zoneName}: sprinklers ON", "info");
        Debug.Log($"[SprinklerSystem] ActivateZone({zoneName}) – {info.sprinklers.Count} sprinklers running.");
    }

    /// <summary>
    /// Turns off all sprinklers in the given zone.
    /// </summary>
    public void DeactivateZone(string zoneName)
    {
        if (!zones.ContainsKey(zoneName)) return;

        SprinklerZoneInfo info = zones[zoneName];
        info.isActive = false;

        foreach (var ps in info.sprinklers)
        {
            if (ps == null) continue;
            var emission = ps.emission;
            emission.enabled = false;
            ps.Stop();
        }

        if (info.moistenCoroutine != null)
        {
            StopCoroutine(info.moistenCoroutine);
            info.moistenCoroutine = null;
        }

        TwinEventLogger.Log("IRRIGATION", $"Zone {zoneName}: sprinklers OFF", "info");
        Debug.Log($"[SprinklerSystem] DeactivateZone({zoneName})");
    }

    /// <summary>Returns true if the given zone's sprinklers are currently active.</summary>
    public bool IsZoneActive(string zoneName)
    {
        return zones.ContainsKey(zoneName) && zones[zoneName].isActive;
    }

    /// <summary>Deactivates all zones.</summary>
    public void DeactivateAll()
    {
        foreach (var key in new List<string>(zones.Keys))
            DeactivateZone(key);

        Debug.Log("[SprinklerSystem] All sprinklers deactivated.");
    }

    // ── Soil moistening ───────────────────────────────────────────────────

    IEnumerator MoistenSoil(string zoneName, Color from, Color to, float duration)
    {
        if (cyberGrid == null || cyberGrid.spawnedCells == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = Color.Lerp(from, to, t);

            foreach (var cell in cyberGrid.spawnedCells)
            {
                if (cell.zoneName != zoneName || cell.soilTile == null) continue;
                Renderer r = cell.soilTile.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                MaterialSafeUtil.ApplyBaseTint(r.material, c);
            }

            yield return null;
        }

        Debug.Log($"[SprinklerSystem] Soil moistening complete for zone {zoneName}.");
    }
}
