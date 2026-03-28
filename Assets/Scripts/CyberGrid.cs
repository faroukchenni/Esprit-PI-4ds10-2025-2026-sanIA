using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class CropMapping
{
    public CropType type;
    public GameObject prefab;
    public float baseScale = 1.0f;
    [Tooltip("Spawn one crop every N cells in each axis (1=dense, 2=sparse orchard grid)")]
    public int spawnEvery = 1;
    [Tooltip("Y offset override — 0 for tall trees, 0.3 for small crops. -1 = auto")]
    public float yOffset = -1f;
    [Tooltip("Optional second prop placed at ground level (e.g. fallen apples under a tree)")]
    public GameObject fallenFruitPrefab;
    public float fallenFruitScale = 1.0f;
}

/// <summary>Tracks one spawned cell for scenario visual overrides.</summary>
[System.Serializable]
public class SpawnedCellInfo
{
    public string     zoneName;      // e.g. "Potato", "Tomato", "Grape", "Apple", "Empty"
    public GameObject soilTile;      // the instantiated soil GameObject
    public GameObject cropObject;    // the instantiated crop GameObject (may be null)
    public Color      originalColor; // zone base soil color
    public Vector3    originalScale; // crop scale at spawn time
    public int        GridX;         // column (x) in the sim grid
    public int        GridY;         // row    (y) in the sim grid
}

public class CyberGrid : MonoBehaviour
{
    [System.Serializable]
    public class GridSettings
    {
        public float spacing = 2.2f;
        public GameObject soilPrefab;
        public GameObject wateredSoilPrefab;
        public List<CropMapping> cropMappings;
        public GameObject fencePrefab;
        public GameObject propPrefab;
        public GameObject zoneLabelPrefab;
    }

    public GridSettings settings;

    [Header("Digital Twin Aesthetics")]
    public Color scanLineColor = new Color(0f, 0.8f, 1f, 1f);
    public Color dryColor      = new Color(1f, 0.6f, 0.2f);
    public Color infectedColor = new Color(1f, 0.1f, 0f);

    [Header("Mountain Scenery")]
    public GameObject mountainPrefab;
    [Tooltip("12 recommended for a clear ring")]
    public int mountainCount = 12;
    [Tooltip("Added to (gridWidth*spacing/2) to push mountains past everything")]
    public float mountainRingOffset = 50f;
    [Tooltip("Min random scale for spawned mountains")]
    public float mountainScaleMin = 2.0f;
    [Tooltip("Max random scale for spawned mountains")]
    public float mountainScaleMax = 3.5f;

    // Public so ScenarioManager can read and modify
    public GameObject[,] gridObjects;
    public Renderer[,][] gridRenderers;
    /// <summary>One soil renderer per cell — used by DiseaseManager for soil colour changes.</summary>
    public Renderer[,]   soilRenderers;

    private TwinSimulationManager sim;

    // ── Disease colour constants (read by UpdateVisuals & DiseaseManager) ─────
    public static readonly Color DiseaseColorEarly   = new Color(0.784f, 0.706f, 0.000f); // #C8B400 yellow
    public static readonly Color DiseaseColorLate    = new Color(0.361f, 0.102f, 0.000f); // #5C1A00 dark rot
    public static readonly Color DiseaseColorSoil    = new Color(0.165f, 0.039f, 0.000f); // #2A0A00 darkened soil
    public static readonly Color DiseaseColorTreated = new Color(0.565f, 0.933f, 0.565f); // #90EE90 recovery green

    // ── Flash timer: new-infection white→yellow animation ─────────────────────
    private readonly Dictionary<Vector2Int, float> _flashTimers  = new Dictionary<Vector2Int, float>();
    private readonly List<Vector2Int>               _flashExpired = new List<Vector2Int>();
    private readonly List<Vector2Int>               _flashKeys    = new List<Vector2Int>(); // scratch list — avoids allocating during iteration

    /// <summary>Called by DiseaseManager when a cell is newly infected.</summary>
    public void StartInfectionFlash(int x, int y) => _flashTimers[new Vector2Int(x, y)] = 0.5f;

    // Per-zone shared material instances — avoids checkerboard
    private readonly Dictionary<CropType, Material> _zoneSoilMats = new Dictionary<CropType, Material>();

    // Scenario support — populated in InitializeGrid, read by FuturisticUI scenario methods
    [HideInInspector] public List<SpawnedCellInfo> spawnedCells = new List<SpawnedCellInfo>();
    /// <summary>When true, UpdateVisuals skips overriding crop scales (lets scenario scales persist).</summary>
    public bool scenarioActive = false;

    // Zone base soil colors
    static readonly Color SoilColorPotato = new Color(0.478f, 0.267f, 0.137f); // #7A4423
    static readonly Color SoilColorTomato = new Color(0.420f, 0.227f, 0.122f); // #6B3A1F
    static readonly Color SoilColorGrape  = new Color(0.361f, 0.200f, 0.090f); // #5C3317
    static readonly Color SoilColorApple  = new Color(0.478f, 0.267f, 0.137f); // #7A4423
    static readonly Color SoilColorPath   = new Color(0.769f, 0.573f, 0.165f); // #C4922A

    IEnumerator Start()
    {
        sim = TwinSimulationManager.Instance;
        while (sim == null || sim.gridData == null)
        {
            yield return null;
            sim = TwinSimulationManager.Instance;
        }
        InitializeGrid();
    }

    CropMapping GetMappingForCrop(CropType type)
    {
        if (settings.cropMappings == null) return null;
        foreach (var m in settings.cropMappings)
            if (m.type == type) return m;
        return null;
    }

    // Returns (or creates) a shared material instance for the given zone.
    // All soil tiles in the same zone share this one instance → no checkerboard.
    Material GetZoneSoilMat(CropType zone, Material sourceMat)
    {
        if (_zoneSoilMats.TryGetValue(zone, out Material existing))
            return existing;

        Material mat = new Material(sourceMat);
        Color c = ZoneSoilColor(zone);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else mat.color = c;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        _zoneSoilMats[zone] = mat;
        return mat;
    }

    static Color ZoneSoilColor(CropType zone)
    {
        switch (zone)
        {
            case CropType.Potato: return SoilColorPotato;
            case CropType.Tomato: return SoilColorTomato;
            case CropType.Grape:  return SoilColorGrape;
            case CropType.Apple:  return SoilColorApple;
            default:              return SoilColorPath;
        }
    }

    void InitializeGrid()
    {
        gridObjects   = new GameObject[sim.gridWidth, sim.gridHeight];
        gridRenderers = new Renderer[sim.gridWidth, sim.gridHeight][];
        soilRenderers = new Renderer[sim.gridWidth, sim.gridHeight];

        int centerX = sim.gridWidth  / 2;
        int centerY = sim.gridHeight / 2;

        SpawnMountains();

        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                if (Mathf.Abs(x - centerX) <= 2 && Mathf.Abs(y - centerY) <= 2)
                {
                    SpawnHubCell(x, y);
                    continue;
                }

                GridCellData cell = sim.gridData[x, y];

                GameObject cellObj = new GameObject($"Cell_{x}_{y}");
                cellObj.transform.parent        = this.transform;
                cellObj.transform.localPosition = new Vector3(x * settings.spacing, 0, y * settings.spacing);

                // ── Soil tile ─────────────────────────────────────────────────
                GameObject soilToSpawn = (cell.Crop == CropType.Empty && settings.wateredSoilPrefab != null)
                    ? settings.wateredSoilPrefab
                    : settings.soilPrefab;

                GameObject soil = null;   // hoisted so spawnedCells can reference it
                if (soilToSpawn != null)
                {
                    soil = Instantiate(soilToSpawn, cellObj.transform);
                    soil.transform.localPosition = Vector3.zero;
                    soil.transform.localRotation = Quaternion.identity; // NO random rotation — prevents furrow checkerboard

                    Renderer soilRend = soil.GetComponentInChildren<Renderer>();
                    float meshSz = (soilRend != null)
                        ? Mathf.Max(soilRend.bounds.size.x, soilRend.bounds.size.z)
                        : 0f;
                    if (meshSz < 0.01f) meshSz = 5.0f;
                    float soilScale = settings.spacing / meshSz;
                    soil.transform.localScale = Vector3.one * soilScale;

                    if (x == 0 && y == 0)
                        Debug.Log($"[CyberGrid] Soil tile: measured={meshSz:F3}m → scale={soilScale:F4}");

                    // Assign the SHARED zone material (one instance per zone = no checkerboard)
                    if (soilRend != null)
                    {
                        soilRend.sharedMaterial = GetZoneSoilMat(cell.Crop, soilRend.sharedMaterial);
                        soilRenderers[x, y]     = soilRend;
                    }
                }

                // ── Crop / tree ───────────────────────────────────────────────
                if (cell.Crop != CropType.Empty)
                {
                    CropMapping mapping = GetMappingForCrop(cell.Crop);
                    if (mapping != null && mapping.prefab != null)
                    {
                        bool spawnHere = (mapping.spawnEvery <= 1) ||
                                         (x % mapping.spawnEvery == 0 && y % mapping.spawnEvery == 0);
                        if (spawnHere)
                        {
                            float yOff = mapping.yOffset >= 0f ? mapping.yOffset : 0.3f;

                            GameObject crop = Instantiate(mapping.prefab, cellObj.transform);
                            crop.transform.localPosition = new Vector3(
                                Random.Range(-0.15f, 0.15f), yOff, Random.Range(-0.15f, 0.15f));
                            crop.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                            crop.transform.localScale    = Vector3.one * mapping.baseScale;

                            Renderer[] rens = crop.GetComponentsInChildren<Renderer>();
                            foreach (Renderer r in rens)
                            {
                                r.material = new Material(r.material);
                                r.material.EnableKeyword("_EMISSION");
                            }
                            gridObjects[x, y]   = crop;
                            gridRenderers[x, y] = rens;

                            // Fallen fruit at ground level
                            if (mapping.fallenFruitPrefab != null)
                            {
                                GameObject fallen = Instantiate(mapping.fallenFruitPrefab, cellObj.transform);
                                fallen.transform.localPosition = new Vector3(
                                    Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                                fallen.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                                fallen.transform.localScale    = Vector3.one * mapping.fallenFruitScale;
                            }
                        }
                    }
                }

                // ── Track spawned cell for scenario overrides ─────────────────
                var cellInfo = new SpawnedCellInfo();
                cellInfo.zoneName      = cell.Crop.ToString();
                cellInfo.soilTile      = soil;
                cellInfo.cropObject    = gridObjects[x, y];
                cellInfo.originalColor = ZoneSoilColor(cell.Crop);
                cellInfo.originalScale = (gridObjects[x, y] != null)
                    ? gridObjects[x, y].transform.localScale
                    : Vector3.one;
                cellInfo.GridX         = x;
                cellInfo.GridY         = y;
                spawnedCells.Add(cellInfo);

                // ── Perimeter fence ───────────────────────────────────────────
                if (settings.fencePrefab != null)
                {
                    bool isPerimeter = (x == 0 || x == sim.gridWidth - 1 || y == 0 || y == sim.gridHeight - 1);
                    if (isPerimeter && x % 2 == 0 && y % 2 == 0)
                        SpawnFence(cellObj.transform, x, y);
                }
            }
        }

        SpawnZoneLabels();

        int cropCount = 0;
        for (int cx = 0; cx < sim.gridWidth; cx++)
            for (int cy = 0; cy < sim.gridHeight; cy++)
                if (gridObjects[cx, cy] != null) cropCount++;

        if (settings.cropMappings == null || settings.cropMappings.Count == 0)
            Debug.LogWarning("[CyberGrid] CropMappings list is EMPTY — run FarmTwin > Build > Fix Crops.");
        else
            Debug.Log($"[CyberGrid] Grid ready: {sim.gridWidth}×{sim.gridHeight} cells, " +
                      $"{cropCount} crops, {_zoneSoilMats.Count} zone soil materials.");
    }

    void SpawnMountains()
    {
        if (mountainPrefab == null || mountainCount <= 0) return;

        GameObject scenery = new GameObject("Scenery");
        scenery.transform.parent        = this.transform;
        scenery.transform.localPosition = Vector3.zero;

        Vector3 gridCenter = new Vector3(
            (sim.gridWidth  - 1) * settings.spacing * 0.5f,
            0f,
            (sim.gridHeight - 1) * settings.spacing * 0.5f);

        // Radius formula requested: (gridWidth*spacing/2) + mountainRingOffset
        float ringRadius = (sim.gridWidth * settings.spacing * 0.5f) + mountainRingOffset;

        for (int i = 0; i < mountainCount; i++)
        {
            float angle = i * (360f / mountainCount);
            float rad   = angle * Mathf.Deg2Rad;
            Vector3 pos = gridCenter + new Vector3(Mathf.Sin(rad) * ringRadius, 0f, Mathf.Cos(rad) * ringRadius);

            GameObject mountain = Instantiate(mountainPrefab, scenery.transform);
            mountain.transform.localPosition = pos;
            mountain.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float scale = Random.Range(mountainScaleMin, mountainScaleMax);
            mountain.transform.localScale = Vector3.one * scale;
            mountain.name = $"Mountain_{i}";
        }

        Debug.Log($"[CyberGrid] Mountains: {mountainCount} placed at radius {ringRadius:F1}m " +
                  $"(scale 2.0–3.5, Y=0).");
    }

    void SpawnHubCell(int x, int y)
    {
        GameObject hubCell = new GameObject($"Hub_{x}_{y}");
        hubCell.transform.parent        = this.transform;
        hubCell.transform.localPosition = new Vector3(x * settings.spacing, 0, y * settings.spacing);
        if (settings.soilPrefab != null) Instantiate(settings.soilPrefab, hubCell.transform);
    }

    void SpawnFence(Transform parent, int x, int y)
    {
        GameObject fence = Instantiate(settings.fencePrefab, parent);
        if (x == 0 || x == sim.gridWidth - 1)
            fence.transform.localRotation = Quaternion.Euler(0, 90, 0);
    }

    void SpawnZoneLabels()
    {
        if (settings.zoneLabelPrefab == null) return;
        CreateLabel("POTATO FIELD",    sim.gridWidth / 4,       sim.gridHeight / 4);
        CreateLabel("TOMATO FIELD",   (sim.gridWidth * 3) / 4,  sim.gridHeight / 4);
        CreateLabel("GRAPE VINEYARD",  sim.gridWidth / 4,       (sim.gridHeight * 3) / 4);
        CreateLabel("APPLE ORCHARD",  (sim.gridWidth * 3) / 4,  (sim.gridHeight * 3) / 4);
    }

    private List<GameObject> zoneLabels = new List<GameObject>();

    void CreateLabel(string txt, int x, int y)
    {
        GameObject labelObj = Instantiate(settings.zoneLabelPrefab, this.transform);
        labelObj.transform.localPosition = new Vector3(x * settings.spacing, 10f, y * settings.spacing);
        labelObj.transform.localScale    = Vector3.one * 0.5f;
        var tmp = labelObj.GetComponentInChildren<TextMeshPro>();
        if (tmp) { tmp.text = txt; tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.Center; }
        zoneLabels.Add(labelObj);
    }

    void Update()
    {
        if (sim == null || sim.gridData == null || gridRenderers == null) return;
        UpdateVisuals();
        UpdateLabels();
    }

    // ── Scenario visual API ───────────────────────────────────────────────────
    // Called by FuturisticUI scenario methods. Uses spawnedCells populated at spawn time.

    public void SetZoneColor(string zoneName, Color color)
    {
        foreach (var cell in spawnedCells)
        {
            if (cell.zoneName != zoneName || cell.soilTile == null) continue;
            Renderer r = cell.soilTile.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.material.SetColor("_BaseColor", color);
                r.material.color = color;
            }
        }
    }

    public void SetZoneCropColor(string zoneName, Color color)
    {
        scenarioActive = true;  // prevent UpdateVisuals from overriding these tints
        foreach (var cell in spawnedCells)
        {
            if (cell.zoneName != zoneName || cell.cropObject == null) continue;
            foreach (Renderer r in cell.cropObject.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                r.material.SetColor("_BaseColor", color);
                r.material.color = color;
            }
        }
    }

    public void SetAllZoneColors(Color color)
    {
        foreach (var cell in spawnedCells)
        {
            if (cell.soilTile == null) continue;
            Renderer r = cell.soilTile.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.material.SetColor("_BaseColor", color);
                r.material.color = color;
            }
        }
    }

    public void SetAllCropColors(Color color)
    {
        scenarioActive = true;  // prevent UpdateVisuals from overriding these tints
        foreach (var cell in spawnedCells)
        {
            if (cell.cropObject == null) continue;
            foreach (Renderer r in cell.cropObject.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                r.material.SetColor("_BaseColor", color);
                r.material.color = color;
            }
        }
    }

    public void RestoreAllOriginals()
    {
        scenarioActive = false;   // let UpdateVisuals resume normal scale control
        foreach (var cell in spawnedCells)
        {
            if (cell.soilTile != null)
            {
                Renderer r = cell.soilTile.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.material.SetColor("_BaseColor", cell.originalColor);
                    r.material.color = cell.originalColor;
                }
            }
            // crop scales are restored automatically by UpdateVisuals (scenarioActive = false)
        }
    }

    void UpdateLabels()
    {
        if (Camera.main == null) return;
        foreach (var label in zoneLabels)
        {
            if (label == null) continue;
            label.transform.LookAt(
                label.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
            float hover = Mathf.Sin(Time.time * 2f) * 0.3f;
            Vector3 p = label.transform.localPosition;
            p.y = 10f + hover;
            label.transform.localPosition = p;
        }
    }

    void UpdateVisuals()
    {
        // During a scenario (drought/heatwave), visual ownership passes to FuturisticUI — skip.
        if (scenarioActive) return;

        // ── Tick infection-flash timers ────────────────────────────────────────
        // Snapshot keys first — modifying a Dictionary (even updating an existing key's value)
        // increments its version counter and throws InvalidOperationException on the next
        // MoveNext(), so we must never write to _flashTimers while foreach is active on it.
        _flashExpired.Clear();
        _flashKeys.Clear();
        _flashKeys.AddRange(_flashTimers.Keys);          // O(n) snapshot, no allocation after warmup
        foreach (var key in _flashKeys)
        {
            float newVal = _flashTimers[key] - Time.deltaTime;
            _flashTimers[key] = newVal;                  // safe: iterating _flashKeys, not _flashTimers
            if (newVal <= 0f) _flashExpired.Add(key);
        }
        foreach (var k in _flashExpired) _flashTimers.Remove(k);

        int curDay = TwinSimulationManager.Instance != null ? TwinSimulationManager.Instance.currentDay : 0;

        for (int x = 0; x < sim.gridWidth; x++)
        {
            for (int y = 0; y < sim.gridHeight; y++)
            {
                GridCellData cell = sim.gridData[x, y];

                // ── Crop visuals ───────────────────────────────────────────────
                if (gridObjects[x, y] != null && gridRenderers[x, y] != null)
                {
                    CropMapping mapping   = GetMappingForCrop(cell.Crop);
                    float       baseScale = mapping != null ? mapping.baseScale : 1.0f;

                    Color cropColor;
                    if (cell.IsInfected)
                    {
                        if (cell.IsTreated)
                        {
                            cropColor = DiseaseColorTreated;
                        }
                        else
                        {
                            int   daysInfected = curDay - cell.InfectionDay;
                            Color target       = daysInfected < 3 ? DiseaseColorEarly : DiseaseColorLate;
                            var   key          = new Vector2Int(x, y);
                            cropColor = _flashTimers.TryGetValue(key, out float t)
                                ? Color.Lerp(target, Color.white, t / 0.5f)
                                : target;
                        }
                    }
                    else if (cell.VegetationHealth < 0.15f)
                        cropColor = new Color(0.2f, 0.1f, 0f);
                    else if (cell.MoistureLevel < 0.35f)
                        cropColor = Color.Lerp(Color.white, dryColor, 1f - cell.MoistureLevel / 0.35f);
                    else
                        cropColor = Color.white;

                    foreach (Renderer r in gridRenderers[x, y])
                    {
                        if (r == null) continue;
                        if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", cropColor);
                        r.material.SetColor("_EmissionColor", cropColor != Color.white ? cropColor * 0.3f : Color.black);
                    }

                    float healthFactor = 0.3f + cell.VegetationHealth * 0.7f;
                    gridObjects[x, y].transform.localScale = Vector3.one * (baseScale * healthFactor);
                }

                // ── Soil — darken for late-stage infection ─────────────────────
                if (soilRenderers != null && soilRenderers[x, y] != null
                    && cell.IsInfected && !cell.IsTreated
                    && (curDay - cell.InfectionDay) >= 3)
                {
                    soilRenderers[x, y].material.SetColor("_BaseColor", DiseaseColorSoil);
                    soilRenderers[x, y].material.color = DiseaseColorSoil;
                }
            }
        }
    }
}
