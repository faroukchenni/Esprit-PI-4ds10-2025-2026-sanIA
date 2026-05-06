using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Four visual test scenarios.
/// Uses MaterialPropertyBlock throughout — material ASSETS are NEVER modified,
/// so Reset always works by simply clearing all blocks.
/// Crop transform scales are stored once on first run and restored on Reset.
/// Disease scenario spawns 5 pulsing red spheres above the tomato zone.
/// </summary>
public class ScenarioManager : MonoBehaviour
{
    public static ScenarioManager Instance { get; private set; }

    // ── Crop scale originals ───────────────────────────────────────────────────
    readonly Dictionary<int, Vector3> _cropScaleOriginals = new Dictionary<int, Vector3>();
    bool _originalsStored = false;

    // ── Disease sphere markers ─────────────────────────────────────────────────
    readonly List<GameObject> _diseaseMarkers = new List<GameObject>();
    Coroutine _pulseCoroutine;

    // ── Lighting originals ─────────────────────────────────────────────────────
    Color _origSunColor;
    float _origSunIntensity;
    Color _origSkyColor;
    Color _origEquatorColor;
    Color _origGroundColor;
    bool  _lightingOriginalsStored = false;

    // ── Scenario lighting presets (inspector-tweakable) ───────────────────────
    [Header("Scenario Sun Colors")]
    public Color droughtSunColor  = new Color(1.00f, 0.549f, 0.000f); // #FF8C00
    public Color heatwaveSunColor = new Color(1.00f, 0.267f, 0.000f); // #FF4400
    public Color diseaseSunColor  = new Color(0.75f, 0.90f,  0.55f);
    public Color healthySunColor  = new Color(1.00f, 0.831f, 0.627f); // #FFD4A0

    [Header("Scenario Sky Colors")]
    public Color droughtSkyColor  = new Color(0.77f, 0.64f, 0.35f);
    public Color heatwaveSkyColor = new Color(1.00f, 0.60f, 0.267f); // #FF9944
    public Color diseaseSkyColor  = new Color(0.30f, 0.48f, 0.14f);
    public Color healthySkyColor  = new Color(0.529f, 0.808f, 0.922f); // #87CEEB

    [Header("Scenario Ground Colors")]
    public Color droughtGroundColor  = new Color(0.769f, 0.627f, 0.314f); // #C4A050
    public Color heatwaveGroundColor = new Color(0.722f, 0.361f, 0.000f); // #B85C00
    public Color diseaseGroundColor  = new Color(0.14f,  0.26f,  0.05f);
    public Color healthyGroundColor  = new Color(0.420f, 0.267f, 0.137f); // #6B4423

    // ── Soil colors ────────────────────────────────────────────────────────────
    static readonly Color SoilDroughtCracked = new Color(0.831f, 0.659f, 0.333f); // #D4A855
    static readonly Color SoilScorched       = new Color(0.722f, 0.361f, 0.000f); // #B85C00
    static readonly Color SoilDiseaseRot     = new Color(0.165f, 0.039f, 0.000f); // #2A0A00

    // ── Crop disease color ─────────────────────────────────────────────────────
    static readonly Color CropDiseaseColor   = new Color(0.361f, 0.102f, 0.000f); // #5C1A00

    // ──────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ── Scene object helpers ───────────────────────────────────────────────────

    CyberGrid GetGrid() => Object.FindFirstObjectByType<CyberGrid>();

    Light GetSun()
    {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "FillLight") return l;
        return null;
    }

    ScenarioOverlay GetOverlay()
    {
        ScenarioOverlay o = Object.FindFirstObjectByType<ScenarioOverlay>();
        if (o == null)
        {
            GameObject go = new GameObject("ScenarioOverlay");
            o = go.AddComponent<ScenarioOverlay>();
        }
        return o;
    }

    FuturisticUI GetHUD() => Object.FindFirstObjectByType<FuturisticUI>();

    // ── Store crop scale originals once ───────────────────────────────────────

    void StoreOriginalsIfNeeded()
    {
        if (_originalsStored) return;

        CyberGrid grid = GetGrid();
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (grid != null && grid.gridObjects != null && sim != null)
        {
            for (int x = 0; x < sim.gridWidth; x++)
            for (int y = 0; y < sim.gridHeight; y++)
            {
                GameObject go = grid.gridObjects[x, y];
                if (go == null) continue;
                int id = go.GetInstanceID();
                if (!_cropScaleOriginals.ContainsKey(id))
                    _cropScaleOriginals[id] = go.transform.localScale;
            }
        }

        if (!_lightingOriginalsStored)
        {
            Light sun = GetSun();
            if (sun != null) { _origSunColor = sun.color; _origSunIntensity = sun.intensity; }
            _origSkyColor     = RenderSettings.ambientSkyColor;
            _origEquatorColor = RenderSettings.ambientEquatorColor;
            _origGroundColor  = RenderSettings.ambientGroundColor;
            _lightingOriginalsStored = true;
        }

        _originalsStored = true;
    }

    // ── Renderer collection helpers ────────────────────────────────────────────

    List<Renderer> GetAllSoilRenderers()
    {
        var list = new List<Renderer>();
        CyberGrid grid = GetGrid();
        if (grid == null) return list;
        foreach (Transform cell in grid.transform)
        {
            foreach (Transform child in cell)
            {
                Renderer r = child.GetComponentInChildren<Renderer>();
                if (r != null) list.Add(r);
                break; // first child = soil tile only
            }
        }
        return list;
    }

    List<Renderer> GetZoneSoilRenderers(CropType zone)
    {
        var list = new List<Renderer>();
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        CyberGrid grid = GetGrid();
        if (sim == null || grid == null) return list;
        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            if (sim.gridData[x, y].Crop != zone) continue;
            Transform cell = grid.transform.Find($"Cell_{x}_{y}");
            if (cell == null) continue;
            foreach (Transform child in cell)
            {
                Renderer r = child.GetComponentInChildren<Renderer>();
                if (r != null) list.Add(r);
                break;
            }
        }
        return list;
    }

    List<GameObject> GetZoneCrops(CropType zone)
    {
        var list = new List<GameObject>();
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        CyberGrid grid = GetGrid();
        if (sim == null || grid == null || grid.gridObjects == null) return list;
        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            if (sim.gridData[x, y].Crop != zone) continue;
            if (grid.gridObjects[x, y] != null) list.Add(grid.gridObjects[x, y]);
        }
        return list;
    }

    List<GameObject> GetAllCrops()
    {
        var list = new List<GameObject>();
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        CyberGrid grid = GetGrid();
        if (sim == null || grid == null || grid.gridObjects == null) return list;
        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
            if (grid.gridObjects[x, y] != null) list.Add(grid.gridObjects[x, y]);
        return list;
    }

    // ── MaterialPropertyBlock helpers ─────────────────────────────────────────
    // These NEVER modify the material asset — they only override per-renderer.
    // Setting null clears the block and restores the material's own properties.

    void ApplySoilBlock(List<Renderer> renderers, Color col)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", col);
        foreach (var r in renderers) r?.SetPropertyBlock(block);
    }

    void ApplyCropBlock(List<GameObject> crops, Color col, Color emission)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", col);
        block.SetColor("_EmissionColor", emission);
        foreach (var go in crops)
        {
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r.SetPropertyBlock(block);
        }
    }

    /// <summary>Clears ALL property blocks — materials revert to their stored asset values.</summary>
    void ClearAllPropertyBlocks()
    {
        foreach (var r in GetAllSoilRenderers()) r?.SetPropertyBlock(null);
        foreach (var go in GetAllCrops())
        {
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r?.SetPropertyBlock(null);
        }
    }

    void SetCropScale(List<GameObject> crops, float scale)
    {
        foreach (var go in crops)
            if (go != null) go.transform.localScale = Vector3.one * scale;
    }

    void SetLighting(Color sunColor, Color sky, Color equator, Color ground, float intensity = 1.2f)
    {
        Light sun = GetSun();
        if (sun != null) { sun.color = sunColor; sun.intensity = intensity; }
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor  = ground;
    }

    bool ValidateSim(TwinSimulationManager sim)
    {
        if (sim == null || sim.gridData == null)
        {
            Debug.LogError("[ScenarioManager] TwinSimulationManager not ready — enter Play Mode first.");
            return false;
        }
        return true;
    }

    // ── Disease sphere markers ─────────────────────────────────────────────────

    void SpawnDiseaseMarkers()
    {
        ClearDiseaseMarkers();
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (sim == null) return;

        // Tomato zone is SE quadrant: x >= gridWidth/2, y < gridHeight/2
        float sp = 2.2f;
        Vector3 tomatoCenter = new Vector3(
            sim.gridWidth  * 0.75f * sp,
            0f,
            sim.gridHeight * 0.25f * sp);

        // 5 spheres scattered across tomato zone at Y=4
        Vector3[] offsets =
        {
            new Vector3(  0f, 0f,  0f),
            new Vector3( -6f, 0f, -6f),
            new Vector3(  6f, 0f, -6f),
            new Vector3( -6f, 0f,  6f),
            new Vector3(  6f, 0f,  6f),
        };

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        foreach (var offset in offsets)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "DiseaseMarker";
            sphere.transform.position   = tomatoCenter + offset + new Vector3(0f, 4f, 0f);
            sphere.transform.localScale = Vector3.one * 0.8f;

            // Glowing red material
            Renderer rend = sphere.GetComponent<Renderer>();
            Material mat = new Material(urpLit);
            mat.SetColor("_BaseColor", new Color(0.9f, 0f, 0f, 1f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(2f, 0f, 0f));
            }
            rend.material = mat;

            // No physics interaction
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _diseaseMarkers.Add(sphere);
        }

        _pulseCoroutine = StartCoroutine(PulseMarkers());
    }

    void ClearDiseaseMarkers()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        foreach (var go in _diseaseMarkers)
            if (go != null) Destroy(go);
        _diseaseMarkers.Clear();
    }

    IEnumerator PulseMarkers()
    {
        while (true)
        {
            float t     = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f; // 0 → 1
            float scale = Mathf.Lerp(0.5f, 1.3f, t);
            Color emit  = Color.Lerp(new Color(0.5f, 0f, 0f), new Color(2.5f, 0f, 0f), t);

            foreach (var go in _diseaseMarkers)
            {
                if (go == null) continue;
                go.transform.localScale = Vector3.one * scale;
                // Gentle hover
                Vector3 p = go.transform.position;
                p.y = 4f + Mathf.Sin(Time.time * 1.8f + go.GetInstanceID() * 0.4f) * 0.4f;
                go.transform.position = p;

                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", new Color(0.9f, 0f, 0f, 1f));
                    block.SetColor("_EmissionColor", emit);
                    r.SetPropertyBlock(block);
                }
            }
            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCENARIO A — DROUGHT
    // ══════════════════════════════════════════════════════════════════════════

    public void RunDrought()
    {
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (!ValidateSim(sim)) return;
        StoreOriginalsIfNeeded();
        ClearDiseaseMarkers();

        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            bool isPotato = sim.gridData[x, y].Crop == CropType.Potato;
            sim.gridData[x, y].Temperature   = isPotato ? 42f : 36f;
            sim.gridData[x, y].MoistureLevel = isPotato ? 0.05f : 0.55f;
            if (isPotato)
                sim.gridData[x, y].VegetationHealth = Mathf.Min(sim.gridData[x, y].VegetationHealth, 0.30f);
            sim.gridData[x, y].UpdateStress();
        }

        WeatherSystem wx = Object.FindFirstObjectByType<WeatherSystem>();
        if (wx != null) wx.isDrought = true;

        // Soil: potato zone cracked yellow
        ApplySoilBlock(GetZoneSoilRenderers(CropType.Potato), SoilDroughtCracked);

        // Crops: potato severe wilt (scale 0.3)
        SetCropScale(GetZoneCrops(CropType.Potato), 0.3f);

        // Lighting: harsh desert sun
        SetLighting(droughtSunColor, droughtSkyColor,
                    Color.Lerp(droughtSkyColor, droughtGroundColor, 0.5f),
                    droughtGroundColor);

        GetOverlay()?.Show(
            "DROUGHT CRITICAL",
            "POTATO FIELD",
            "Temperature: 42.0°C   |   Moisture: 5%   |   Health: ≤30%",
            ScenarioOverlay.DangerColor);

        GetHUD()?.UpdateHUD(42f, 5f, "DROUGHT CRITICAL — POTATO FIELD");
        TwinEventLogger.Log("SCENARIO A", "DROUGHT CRITICAL — Potato Field: moisture 5%, temp 42°C", "error");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCENARIO B — HEATWAVE
    // ══════════════════════════════════════════════════════════════════════════

    public void RunHeatwave()
    {
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (!ValidateSim(sim)) return;
        StoreOriginalsIfNeeded();
        ClearDiseaseMarkers();

        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            sim.gridData[x, y].Temperature    = 47f;
            sim.gridData[x, y].MoistureLevel  = 0.10f;
            sim.gridData[x, y].VegetationHealth = Mathf.Min(sim.gridData[x, y].VegetationHealth, 0.40f);
            sim.gridData[x, y].UpdateStress();
        }

        WeatherSystem wx = Object.FindFirstObjectByType<WeatherSystem>();
        if (wx != null) wx.isDrought = true;

        // ALL soil scorched
        ApplySoilBlock(GetAllSoilRenderers(), SoilScorched);

        // ALL crops wilted (scale 0.4)
        SetCropScale(GetAllCrops(), 0.4f);

        // Lighting: orange-red fiery sky
        SetLighting(heatwaveSunColor, heatwaveSkyColor,
                    Color.Lerp(heatwaveSkyColor, heatwaveGroundColor, 0.5f),
                    heatwaveGroundColor);

        GetOverlay()?.Show(
            "HEATWAVE",
            "ENTIRE FARM CRITICAL",
            "Temperature: 47.0°C   |   Moisture: 10%   |   All crops at risk",
            ScenarioOverlay.DangerColor);

        GetHUD()?.UpdateHUD(47f, 10f, "HEATWAVE — ENTIRE FARM CRITICAL");
        TwinEventLogger.Log("SCENARIO B", "HEATWAVE — All zones 47°C, humidity 10%", "error");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCENARIO C — DISEASE OUTBREAK
    // ══════════════════════════════════════════════════════════════════════════

    public void RunDisease()
    {
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (!ValidateSim(sim)) return;
        StoreOriginalsIfNeeded();

        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            if (sim.gridData[x, y].Crop != CropType.Tomato) continue;
            sim.gridData[x, y].DiseaseLevel     = 0.85f;
            sim.gridData[x, y].Temperature      = 17f;
            sim.gridData[x, y].VegetationHealth = Mathf.Min(sim.gridData[x, y].VegetationHealth, 0.25f);
            sim.gridData[x, y].UpdateStress();
        }

        WeatherSystem wx = Object.FindFirstObjectByType<WeatherSystem>();
        if (wx != null) wx.isDrought = false;

        // Tomato crops: dark rot color + red emission + wilt
        var tomatoCrops = GetZoneCrops(CropType.Tomato);
        ApplyCropBlock(tomatoCrops, CropDiseaseColor, new Color(0.3f, 0f, 0f));
        SetCropScale(tomatoCrops, 0.5f);

        // Tomato soil: infected dark
        ApplySoilBlock(GetZoneSoilRenderers(CropType.Tomato), SoilDiseaseRot);

        // Lighting: moody diseased sky
        SetLighting(diseaseSunColor, diseaseSkyColor,
                    Color.Lerp(diseaseSkyColor, diseaseGroundColor, 0.5f),
                    diseaseGroundColor);

        // 5 pulsing red spheres above tomato zone
        SpawnDiseaseMarkers();

        // Overlay: purple/dark
        Color purpleDark = new Color(0.50f, 0.05f, 0.60f);
        GetOverlay()?.Show(
            "BLIGHT DETECTED",
            "TOMATO FIELD",
            "Temperature: 17.0°C   |   Infection: 85%   |   Health: ≤25%",
            purpleDark);

        GetHUD()?.UpdateHUD(17f, 60f, "BLIGHT DETECTED — TOMATO FIELD");
        TwinEventLogger.Log("SCENARIO C", "BLIGHT DETECTED — Tomato Field 85% infection, 5 outbreak zones", "error");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCENARIO D — HEALTHY FARM (Reset)
    // ══════════════════════════════════════════════════════════════════════════

    public void RunHealthy()
    {
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (!ValidateSim(sim)) return;

        for (int x = 0; x < sim.gridWidth; x++)
        for (int y = 0; y < sim.gridHeight; y++)
        {
            ref GridCellData cell = ref sim.gridData[x, y];
            cell.MoistureLevel    = 0.70f;
            cell.Temperature      = 25f;
            cell.VegetationHealth = 1.00f;
            cell.DiseaseLevel     = 0.00f;
            cell.UpdateStress();
        }

        WeatherSystem wx = Object.FindFirstObjectByType<WeatherSystem>();
        if (wx != null) wx.isDrought = false;

        // Restore crop scales from originals
        foreach (var go in GetAllCrops())
        {
            if (go == null) continue;
            int id = go.GetInstanceID();
            if (_cropScaleOriginals.TryGetValue(id, out Vector3 orig))
                go.transform.localScale = orig;
        }

        // Clear ALL property blocks — materials revert to their own colors
        ClearAllPropertyBlocks();

        // Remove disease markers
        ClearDiseaseMarkers();

        // Restore lighting
        if (_lightingOriginalsStored)
        {
            Light sun = GetSun();
            if (sun != null) { sun.color = _origSunColor; sun.intensity = _origSunIntensity; }
            RenderSettings.ambientSkyColor     = _origSkyColor;
            RenderSettings.ambientEquatorColor = _origEquatorColor;
            RenderSettings.ambientGroundColor  = _origGroundColor;
        }
        else
        {
            SetLighting(healthySunColor, healthySkyColor,
                        Color.Lerp(healthySkyColor, healthyGroundColor, 0.5f),
                        healthyGroundColor);
        }

        GetOverlay()?.Show(
            "ALL SYSTEMS NORMAL",
            "FULL FARM",
            "Temperature: 25.0°C   |   Moisture: 70%   |   Health: 100%",
            ScenarioOverlay.HealthyColor);

        GetHUD()?.UpdateHUD(25f, 70f, "ALL SYSTEMS NORMAL");
        TwinEventLogger.Log("SCENARIO D", "ALL SYSTEMS NORMAL — optimal conditions restored", "info");
    }
}
