using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class AnimalPenData
{
    public string animalType;
    public int    currentCount;
    public float  avgWeight      = 50f;
    public float  avgTemperature = 38.5f;
    [Range(0f, 100f)] public float healthScore = 100f;

    [HideInInspector] public GameObject[] spawnedAnimals;
    [HideInInspector] public GameObject   labelObject;
}

[System.Serializable]
public class AnimalPenConfig
{
    public string     penName;
    public GameObject animalPrefab;
    public int        count       = 4;
    public int        gridCols    = 4;
    public int        gridRows    = 1;
    public float      spacingX    = 2.5f;
    public float      spacingZ    = 2.5f;
    [Tooltip("Uniform scale applied to each spawned animal")]
    public float      animalScale = 1.0f;
}

/// <summary>
/// Animal pens placed EAST of the crop grid.
/// East zone starts at X = (gridColumns * gridSpacing / 2) + 10 from grid origin,
/// so animals never overlap the crop area.
/// </summary>
public class AnimalManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public int   gridColumns = 20;
    public int   gridRows    = 20;
    public float gridSpacing = 2.2f;

    [Header("East Zone Layout")]
    public float zoneWidth  = 40f;
    public float zoneDepth  = 30f;
    public float zoneBuffer = 4f;
    public float pathWidth  = 3f;

    [Header("Prefabs")]
    public GameObject fencePrefab;
    public GameObject zoneLabelPrefab;
    public GameObject dogPrefab;
    public GameObject deerPrefab;

    [Header("Pen Configurations")]
    public AnimalPenConfig[] penConfigs;

    [Header("Live Animal Data")]
    public AnimalPenData[] pens;

    private readonly List<GameObject> _penRoots = new List<GameObject>();

    private TwinSimulationManager _sim;
    private WeatherSystem         _weather;

    void OnDestroy()
    {
        if (_sim != null) _sim.OnDayAdvanced -= OnDayAdvanced;
    }

    void OnDayAdvanced(int day)
    {
        if (pens == null || _weather == null) return;

        foreach (var pen in pens)
        {
            float prev  = pen.healthScore;
            float delta;

            if (_weather.isHeatwave || _weather.currentTemperature > 38f)
                delta = -4f;   // severe heat stress
            else if (_weather.currentTemperature > 34f || _weather.isDrought)
                delta = -2f;   // moderate heat / drought
            else if (_weather.isRaining)
                delta = 0.5f;  // rain neutral-to-good
            else
                delta = 1f;    // comfortable — slow natural recovery

            pen.healthScore = Mathf.Clamp(pen.healthScore + delta, 0f, 100f);
            // Body temperature rises in heatwave
            pen.avgTemperature = _weather.isHeatwave
                ? Mathf.Min(41f, pen.avgTemperature + 0.3f)
                : Mathf.Max(38.5f, pen.avgTemperature - 0.1f);

            RefreshLabel(pen);

            // Tiered health alerts (fire once when crossing threshold)
            if (pen.healthScore < 40f && prev >= 40f)
                TwinEventLogger.Log("LIVESTOCK",
                    $"{pen.animalType}: POOR health ({pen.healthScore:F0}%) — vet attention needed", "error");
            else if (pen.healthScore < 70f && prev >= 70f)
                TwinEventLogger.Log("LIVESTOCK",
                    $"{pen.animalType}: health declining ({pen.healthScore:F0}%) — check conditions", "warn");
        }
    }

    public void BuildAll()
    {
        ClearAll();

        // Grid world-origin (look for MainGrid; fall back to world zero)
        Vector3 gridOrigin = Vector3.zero;
        GameObject mainGrid = GameObject.Find("MainGrid");
        if (mainGrid != null) gridOrigin = mainGrid.transform.position;

        // East zone start X: (gridColumns * gridSpacing / 2) + 10
        // This guarantees the entire crop grid (width = gridColumns * gridSpacing) is to the left.
        float gridWorldWidth = gridColumns * gridSpacing;
        float eastStartX     = gridOrigin.x + gridWorldWidth + zoneBuffer;

        // Zone centre
        float gridWorldDepth = gridRows * gridSpacing;
        Vector3 zoneCenter   = new Vector3(
            eastStartX + zoneWidth * 0.5f,
            0f,
            gridOrigin.z + gridWorldDepth * 0.5f);

        int   penCount = (penConfigs != null && pens != null)
            ? Mathf.Min(penConfigs.Length, pens.Length)
            : 0;

        float penDepth = (zoneDepth - (penCount - 1) * pathWidth) / Mathf.Max(1, penCount);
        float penWidth = zoneWidth - 2f;

        float[] zOffsets = new float[penCount];
        for (int i = 0; i < penCount; i++)
            zOffsets[i] = -zoneDepth * 0.5f + penDepth * 0.5f + i * (penDepth + pathWidth);

        for (int i = 0; i < penCount; i++)
        {
            Vector3 penCenter = zoneCenter + new Vector3(0f, 0f, zOffsets[i]);
            BuildPen(i, penCenter, penWidth, penDepth);
        }

        BuildDogArea(gridOrigin);
        BuildDeerArea(gridOrigin);

        Debug.Log($"[AnimalManager] Built {penCount} pens east of crop grid " +
                  $"(zone centre {zoneCenter.x:F1},{zoneCenter.z:F1}).\n" +
                  $"  Pen X range: {eastStartX:F1} – {eastStartX + zoneWidth:F1}\n" +
                  $"  Pen Z centres: " + string.Join(", ", System.Array.ConvertAll(zOffsets,
                      z => $"{(zoneCenter.z + z):F1}")));
    }

    void BuildPen(int idx, Vector3 center, float width, float depth)
    {
        AnimalPenConfig cfg  = penConfigs[idx];
        AnimalPenData   data = pens[idx];
        data.animalType   = cfg.penName;
        data.currentCount = cfg.count;

        GameObject penRoot = new GameObject("AnimalPen_" + cfg.penName);
        penRoot.transform.SetParent(transform, false);
        penRoot.transform.position = center;
        _penRoots.Add(penRoot);

        PlaceFenceRing(penRoot.transform, center, width, depth, 2f);

        var animals = new List<GameObject>();
        if (cfg.animalPrefab != null)
        {
            int   cols   = Mathf.Max(1, cfg.gridCols);
            int   rows   = Mathf.Max(1, cfg.gridRows);
            float startX = center.x - (cols - 1) * cfg.spacingX * 0.5f;
            float startZ = center.z - (rows - 1) * cfg.spacingZ * 0.5f;
            int   spawned = 0;

            for (int r = 0; r < rows && spawned < cfg.count; r++)
            for (int c = 0; c < cols && spawned < cfg.count; c++)
            {
                Vector3 pos = new Vector3(
                    startX + c * cfg.spacingX + Random.Range(-0.2f, 0.2f),
                    0f,
                    startZ + r * cfg.spacingZ + Random.Range(-0.2f, 0.2f));

                GameObject a = Instantiate(
                    cfg.animalPrefab, pos,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    penRoot.transform);
                a.name = $"{cfg.penName}_{spawned}";
                if (cfg.animalScale > 0f)
                    a.transform.localScale = Vector3.one * cfg.animalScale;

                // Add wandering behaviour — bounds match the fence ring
                AnimalWander wander = a.AddComponent<AnimalWander>();
                wander.Init(new Bounds(center,
                    new Vector3(width - 1f, 2f, depth - 1f)));

                animals.Add(a);
                spawned++;
            }
        }
        data.spawnedAnimals = animals.ToArray();

        if (zoneLabelPrefab != null)
        {
            Vector3 labelPos = center + new Vector3(0f, 5f, 0f);
            GameObject lbl = Instantiate(zoneLabelPrefab, labelPos,
                                         Quaternion.identity, penRoot.transform);
            data.labelObject = lbl;
            RefreshLabel(data);
        }
    }

    void BuildDogArea(Vector3 gridOrigin)
    {
        if (dogPrefab == null) return;

        GameObject dogRoot = new GameObject("AnimalArea_Dogs");
        dogRoot.transform.SetParent(transform, false);
        _penRoots.Add(dogRoot);

        // Near FarmerHouse: grid centre + (15, 0, 55)
        Vector3 gc = gridOrigin + new Vector3(gridColumns * 0.5f * gridSpacing, 0f,
                                              gridRows    * 0.5f * gridSpacing);

        // 3 dogs in a row near FarmerHouse entrance
        Vector3[] positions =
        {
            gc + new Vector3( 9f, 0f, 49f),
            gc + new Vector3(12f, 0f, 52f),
            gc + new Vector3(16f, 0f, 50f),
        };

        foreach (var pos in positions)
        {
            GameObject d = Instantiate(dogPrefab, pos,
                                       Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                                       dogRoot.transform);
            d.transform.localScale = Vector3.one * 2.0f;
        }
    }

    void BuildDeerArea(Vector3 gridOrigin)
    {
        if (deerPrefab == null) return;

        GameObject deerRoot = new GameObject("AnimalArea_Deer");
        deerRoot.transform.SetParent(transform, false);
        _penRoots.Add(deerRoot);

        Vector3 gc = gridOrigin + new Vector3(gridColumns * 0.5f * gridSpacing, 0f,
                                              gridRows    * 0.5f * gridSpacing);

        // 4 deer near north tree border — tree ring radius = (gridColumns*spacing/2)+30
        // so deer at gc.z + 62-70 is just inside the tree line
        Vector3[] positions =
        {
            gc + new Vector3(-8f, 0f, 63f),
            gc + new Vector3(-3f, 0f, 66f),
            gc + new Vector3( 2f, 0f, 64f),
            gc + new Vector3( 7f, 0f, 68f),
        };

        foreach (var pos in positions)
        {
            GameObject d = Instantiate(deerPrefab, pos,
                                       Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                                       deerRoot.transform);
            d.transform.localScale = Vector3.one * 1.8f;
        }
    }

    // ── Public sensor API ─────────────────────────────────────────────────────
    public void UpdatePenData(string type, int count, float health)
    {
        if (pens == null) return;
        foreach (var pen in pens)
        {
            if (string.Equals(pen.animalType, type, System.StringComparison.OrdinalIgnoreCase))
            {
                pen.currentCount = count;
                pen.healthScore  = Mathf.Clamp(health, 0f, 100f);
                RefreshLabel(pen);
                return;
            }
        }
        Debug.LogWarning($"[AnimalManager] UpdatePenData: no pen with type '{type}'.");
    }

    public void UpdatePenData(string type, int count, float health, float avgWeight, float avgTemp)
    {
        if (pens == null) return;
        foreach (var pen in pens)
        {
            if (string.Equals(pen.animalType, type, System.StringComparison.OrdinalIgnoreCase))
            {
                pen.currentCount   = count;
                pen.healthScore    = Mathf.Clamp(health, 0f, 100f);
                pen.avgWeight      = avgWeight;
                pen.avgTemperature = avgTemp;
                RefreshLabel(pen);
                return;
            }
        }
    }

    // ── Backend livestock sync ────────────────────────────────────────────────

    [System.Serializable] class BackendAnimal
    {
        public string species;
        public string status;
        public float  weight_kg;
    }
    [System.Serializable] class BackendAnimalList { public BackendAnimal[] items; }

    // Maps backend species strings (EN + FR) to the pen animalType strings in the scene.
    static readonly Dictionary<string, string> SPECIES_MAP = new Dictionary<string, string>(
        System.StringComparer.OrdinalIgnoreCase)
    {
        { "sheep",    "Sheep"    }, { "mouton",  "Sheep"   }, { "brebis",   "Sheep"   },
        { "goat",     "Goats"    }, { "chevre",  "Goats"   }, { "chèvre",   "Goats"   },
        { "horse",    "Horses"   }, { "cheval",  "Horses"  },
        { "chicken",  "Chickens" }, { "poulet",  "Chickens"}, { "poule",    "Chickens"},
    };

    void Start()
    {
        _sim     = TwinSimulationManager.Instance;
        _weather = Object.FindFirstObjectByType<WeatherSystem>();
        if (_sim != null) _sim.OnDayAdvanced += OnDayAdvanced;

        if (transform.childCount == 0) BuildAll();

        StartCoroutine(LivestockSyncLoop());
    }

    IEnumerator LivestockSyncLoop()
    {
        // Wait until SanIAApiClient is ready (login may take a few seconds)
        float waited = 0f;
        while ((SanIAApiClient.Instance == null || !SanIAApiClient.Instance.IsReady) && waited < 60f)
        {
            yield return new WaitForSeconds(2f);
            waited += 2f;
        }

        while (true)
        {
            if (SanIAApiClient.Instance != null && SanIAApiClient.Instance.IsReady)
                FetchLivestock();
            yield return new WaitForSeconds(60f); // refresh every 60 real seconds
        }
    }

    void FetchLivestock()
    {
        SanIAApiClient.Instance.GetJson("/api/v1/animals/", (json, err) =>
        {
            if (err != null)
            {
                TwinEventLogger.Log("LIVESTOCK", $"Backend sync failed: {err}", "warn");
                return;
            }

            // JsonUtility can't deserialize root arrays — wrap it.
            BackendAnimalList list;
            try { list = JsonUtility.FromJson<BackendAnimalList>("{\"items\":" + json + "}"); }
            catch (System.Exception ex)
            {
                TwinEventLogger.Log("LIVESTOCK", $"Parse error: {ex.Message}", "warn");
                return;
            }

            if (list?.items == null || list.items.Length == 0)
            {
                TwinEventLogger.Log("LIVESTOCK", "No animals in backend — keeping defaults.", "info");
                return;
            }

            // Group by mapped pen type
            var countAll    = new Dictionary<string, int>();
            var countActive = new Dictionary<string, int>();
            var totalWeight = new Dictionary<string, float>();

            foreach (var a in list.items)
            {
                if (a.species == null) continue;
                if (!SPECIES_MAP.TryGetValue(a.species.Trim(), out string penType)) continue;

                countAll[penType]    = (countAll.ContainsKey(penType)    ? countAll[penType]    : 0) + 1;
                totalWeight[penType] = (totalWeight.ContainsKey(penType) ? totalWeight[penType] : 0f) + a.weight_kg;
                if (string.Equals(a.status, "Active", System.StringComparison.OrdinalIgnoreCase))
                    countActive[penType] = (countActive.ContainsKey(penType) ? countActive[penType] : 0) + 1;
            }

            foreach (var kv in countAll)
            {
                string penType  = kv.Key;
                int    total    = kv.Value;
                int    active   = countActive.ContainsKey(penType) ? countActive[penType] : 0;
                float  health   = total > 0 ? (active / (float)total) * 100f : 100f;
                float  avgW     = total > 0 ? (totalWeight.ContainsKey(penType) ? totalWeight[penType] : 0f) / total : 50f;

                UpdatePenData(penType, total, health, avgW, 38.5f);
            }

            TwinEventLogger.Log("LIVESTOCK", $"Synced {list.items.Length} animals from backend.", "info");
        });
    }

    // ── Label ─────────────────────────────────────────────────────────────────
    void RefreshLabel(AnimalPenData pen)
    {
        if (pen.labelObject == null) return;
        TextMeshPro tmp = pen.labelObject.GetComponentInChildren<TextMeshPro>();
        if (tmp == null) return;

        string status = pen.healthScore >= 80f ? "GOOD"
                      : pen.healthScore >= 50f ? "FAIR" : "POOR";

        tmp.text      = $"{pen.animalType.ToUpper()}: {pen.currentCount}\nHealth: {pen.healthScore:F0}% [{status}]";
        tmp.fontSize  = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = pen.healthScore >= 80f ? new Color(0.20f, 0.95f, 0.30f)   // green — good
                      : pen.healthScore >= 50f ? new Color(1.00f, 0.75f, 0.00f)   // amber — fair
                                               : new Color(1.00f, 0.25f, 0.20f);  // red   — poor
    }

    void Update()
    {
        if (Camera.main == null || pens == null) return;
        Quaternion camRot = Camera.main.transform.rotation;
        foreach (var pen in pens)
        {
            if (pen.labelObject == null) continue;
            pen.labelObject.transform.LookAt(
                pen.labelObject.transform.position + camRot * Vector3.forward,
                camRot * Vector3.up);

            Vector3 p = pen.labelObject.transform.localPosition;
            p.y = 5f + Mathf.Sin(Time.time * 1.5f + pen.GetHashCode()) * 0.15f;
            pen.labelObject.transform.localPosition = p;
        }
    }

    // ── Fence ring ────────────────────────────────────────────────────────────
    void PlaceFenceRing(Transform parent, Vector3 center, float width, float depth, float interval)
    {
        if (fencePrefab == null) return;

        // 1-unit inset from the pen boundary so fences never cross into neighbouring pens.
        float hw = width  * 0.5f - 1f;
        float hd = depth  * 0.5f - 1f;
        if (hw <= 0f || hd <= 0f) return;   // pen too small — skip

        // South and North edges (vary X, fixed Z) — strict < avoids double-corner
        for (float fx = center.x - hw; fx < center.x + hw + interval * 0.5f; fx += interval)
        {
            fx = Mathf.Min(fx, center.x + hw);   // clamp last piece to exact corner
            Instantiate(fencePrefab, new Vector3(fx, 0f, center.z - hd), Quaternion.identity, parent);
            Instantiate(fencePrefab, new Vector3(fx, 0f, center.z + hd), Quaternion.identity, parent);
        }
        // West and East edges (vary Z, skip corners already placed)
        for (float fz = center.z - hd + interval; fz < center.z + hd; fz += interval)
        {
            Instantiate(fencePrefab, new Vector3(center.x - hw, 0f, fz), Quaternion.Euler(0, 90, 0), parent);
            Instantiate(fencePrefab, new Vector3(center.x + hw, 0f, fz), Quaternion.Euler(0, 90, 0), parent);
        }
    }

    public void ClearAll()
    {
        var children = new List<GameObject>();
        foreach (Transform t in transform) children.Add(t.gameObject);
        foreach (var go in children)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _penRoots.Clear();
    }
}
