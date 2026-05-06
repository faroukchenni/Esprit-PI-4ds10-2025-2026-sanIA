using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// FarmTwin → Build → Enrich Farm Scene
// Populates the farm with trees, fences, crates, and fruit/veggie props.
// Safe to re-run — removes old FarmDecor root first.
public static class SceneEnricher
{
    // Must match TwinSimulationManager / CyberGrid
    private const float SPACING   = 2.2f;
    private const int   GRID      = 20;
    private const float GRID_W    = GRID * SPACING;  // 44 m
    private const float TREE_GAP  = 5.0f;
    private const float FENCE_W   = 2.0f;

    // ── Menu entry ────────────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Build/Enrich Farm Scene")]
    public static void Enrich()
    {
        // Remove previous decoration root
        var old = GameObject.Find("FarmDecor");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("FarmDecor");

        PlaceTrees(root.transform);
        PlaceFences(root.transform);
        PlaceFarmProps(root.transform);
        PlaceFruitProps(root.transform);
        PlaceNatureClutter(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorUtility.SetDirty(root);

        Debug.Log("[SceneEnricher] Farm enrichment complete. Save scene (Ctrl+S).");
        EditorUtility.DisplayDialog("Farm Enriched",
            "Trees, fences, props, and fruit decorations placed.\n\n" +
            "Run: FarmTwin → Fix → Fix Tree Materials (URP Gradient)\n" +
            "to make trees show in colour.\n\nSave scene (Ctrl+S).", "OK");
    }

    [MenuItem("FarmTwin/Build/Remove Farm Decor")]
    public static void RemoveDecor()
    {
        var old = GameObject.Find("FarmDecor");
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[SceneEnricher] FarmDecor removed.");
        }
    }

    // ── Trees ─────────────────────────────────────────────────────────────────

    static void PlaceTrees(Transform parent)
    {
        var treeRoot = new GameObject("Trees").transform;
        treeRoot.SetParent(parent);

        // North border (z = GRID_W + 4) — Spring / green canopy
        var northTrees = new[] { "Spring01","Spring03","Spring05","Spring07","Spring09","Spring02","Spring04","Spring06","Spring08","Spring10","Summer01","Summer03" };
        PlaceRow(treeRoot, "North", northTrees,
            startX: -4f, endX: GRID_W + 4f, z: GRID_W + 4f, yRot: 0f, scale: 1.2f);

        // South border (z = -6) — Autumn orange/yellow, warm welcome
        var southTrees = new[] { "AutmnOr01","AutmnOr03","AutmnOr05","AutmnOr07","AutmnOr09","AutumnYe01","AutumnYe03","AutumnYe05","AutumnYe07","AutmnOr02","AutumnRed01","AutumnRed03" };
        PlaceRow(treeRoot, "South", southTrees,
            startX: -4f, endX: GRID_W + 4f, z: -6f, yRot: 180f, scale: 1.1f);

        // West border (x = -6) — Summer full canopy
        var westTrees = new[] { "Summer01","Summer02","Summer03","Summer04","Summer05","Spring01","Spring02","Spring03","Spring04" };
        PlaceColumn(treeRoot, "West", westTrees,
            x: -6f, startZ: 0f, endZ: GRID_W, yRot: 90f, scale: 1.15f);

        // East border (x = GRID_W + 8, leave room for pens) — Autumn red / dramatic
        var eastTrees = new[] { "AutumnRed01","AutumnRed02","AutumnRed03","AutumnRed04","AutumnRed05","AutumnRed06","AutumnRed07","AutumnRed08","AutumnRed09" };
        PlaceColumn(treeRoot, "East", eastTrees,
            x: GRID_W + 8f, startZ: 0f, endZ: GRID_W, yRot: 270f, scale: 1.0f);

        // Corner accent trees (larger, feature trees)
        var corners = new (string name, float x, float z, float rot)[]
        {
            ("Summer03", -8f, -8f, 45f),
            ("Summer04", GRID_W + 8f, -8f, 315f),
            ("Summer05", -8f, GRID_W + 8f, 135f),
            ("Summer01", GRID_W + 8f, GRID_W + 8f, 225f),
        };
        foreach (var (name, x, z, rot) in corners)
        {
            var go = Place(treeRoot, "Corner_" + name, TreePath(name), new Vector3(x, 0, z), rot, 1.6f);
        }
    }

    static void PlaceRow(Transform parent, string label, string[] names,
        float startX, float endX, float z, float yRot, float scale)
    {
        var rowRoot = new GameObject("Row_" + label).transform;
        rowRoot.SetParent(parent);
        int i = 0;
        for (float x = startX; x <= endX; x += TREE_GAP, i++)
        {
            string prefabName = names[i % names.Length];
            float jitter = Random.Range(-0.8f, 0.8f);
            float sc     = scale * Random.Range(0.85f, 1.15f);
            float rot    = yRot + Random.Range(-20f, 20f);
            Place(rowRoot, prefabName + "_" + i, TreePath(prefabName),
                new Vector3(x + jitter, 0f, z + jitter), rot, sc);
        }
    }

    static void PlaceColumn(Transform parent, string label, string[] names,
        float x, float startZ, float endZ, float yRot, float scale)
    {
        var colRoot = new GameObject("Col_" + label).transform;
        colRoot.SetParent(parent);
        int i = 0;
        for (float z = startZ; z <= endZ; z += TREE_GAP, i++)
        {
            string prefabName = names[i % names.Length];
            float jitter = Random.Range(-0.8f, 0.8f);
            float sc     = scale * Random.Range(0.85f, 1.15f);
            float rot    = yRot + Random.Range(-25f, 25f);
            Place(colRoot, prefabName + "_" + i, TreePath(prefabName),
                new Vector3(x + jitter, 0f, z + jitter), rot, sc);
        }
    }

    static string TreePath(string name) =>
        $"Assets/51+ LowPolyTrees/LowPolyTrees/Models/Prefab/{name}.prefab";

    // ── Fences ────────────────────────────────────────────────────────────────

    static void PlaceFences(Transform parent)
    {
        var fenceRoot = new GameObject("Fences").transform;
        fenceRoot.SetParent(parent);

        string midPath  = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Fence_Middle.prefab";
        string endPath  = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/FenceEnd.prefab";
        string gatePath = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/FenceGate_A.prefab";

        bool haveFence = AssetExists(midPath);
        if (!haveFence) { Debug.LogWarning("[SceneEnricher] Fence_Middle prefab not found, skipping fences."); return; }

        // South fence (front of farm, z = -1) — gate in the middle
        float gateX = GRID_W * 0.5f;
        PlaceFenceRow(fenceRoot, "Fence_South", midPath, endPath, gatePath,
            startX: 0f, endX: GRID_W, z: -1f, yRot: 0f, gateAtX: gateX);

        // West fence (x = -1)
        PlaceFenceCol(fenceRoot, "Fence_West", midPath, endPath,
            startZ: 0f, endZ: GRID_W, x: -1f, yRot: 90f);
    }

    static void PlaceFenceRow(Transform parent, string label,
        string midPath, string endPath, string gatePath,
        float startX, float endX, float z, float yRot, float gateAtX)
    {
        var row = new GameObject(label).transform;
        row.SetParent(parent);
        bool gatePlace = false;

        Place(row, "FenceEnd_Start", endPath, new Vector3(startX - 0.1f, 0, z), yRot);

        for (float x = startX; x < endX; x += FENCE_W)
        {
            if (!gatePlace && Mathf.Abs(x - gateAtX) < FENCE_W)
            {
                Place(row, "Gate", gatePath, new Vector3(x, 0, z), yRot, 1f);
                gatePlace = true;
                continue;
            }
            Place(row, "Fence_" + (int)x, midPath, new Vector3(x + FENCE_W * 0.5f, 0, z), yRot, 1f);
        }

        Place(row, "FenceEnd_End", endPath, new Vector3(endX + 0.1f, 0, z), yRot + 180f);
    }

    static void PlaceFenceCol(Transform parent, string label,
        string midPath, string endPath,
        float startZ, float endZ, float x, float yRot)
    {
        var col = new GameObject(label).transform;
        col.SetParent(parent);

        Place(col, "FenceEnd_Start", endPath, new Vector3(x, 0, startZ - 0.1f), yRot);
        for (float z = startZ; z < endZ; z += FENCE_W)
            Place(col, "Fence_" + (int)z, midPath, new Vector3(x, 0, z + FENCE_W * 0.5f), yRot, 1f);
        Place(col, "FenceEnd_End", endPath, new Vector3(x, 0, endZ + 0.1f), yRot + 180f);
    }

    // ── Farm structural props ─────────────────────────────────────────────────

    static void PlaceFarmProps(Transform parent)
    {
        var propRoot = new GameObject("FarmProps_Extra").transform;
        propRoot.SetParent(parent);

        float cx = GRID_W * 0.5f;

        // Crates near water tower entrance
        TryPlace(propRoot, "Crate_L", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Crate.prefab",
            new Vector3(cx - 6f, 0f, -3f), 30f, 1.0f);
        TryPlace(propRoot, "Crate_R", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Crate.prefab",
            new Vector3(cx + 6f, 0f, -3f), -25f, 1.2f);
        TryPlace(propRoot, "Crate_Stack", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Crate.prefab",
            new Vector3(cx + 6f, 1.0f, -3f), 10f, 1.1f);

        // WaterCan decorations near each crop zone entrance (on the road)
        TryPlace(propRoot, "WaterCan_NW", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/WaterCan.prefab",
            new Vector3(4.5f * SPACING, 0f, 4.5f * SPACING), 45f);
        TryPlace(propRoot, "WaterCan_NE", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/WaterCan.prefab",
            new Vector3(15.5f * SPACING, 0f, 4.5f * SPACING), -30f);
        TryPlace(propRoot, "WaterCan_SW", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/WaterCan.prefab",
            new Vector3(4.5f * SPACING, 0f, 15.5f * SPACING), 120f);
        TryPlace(propRoot, "WaterCan_SE", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/WaterCan.prefab",
            new Vector3(15.5f * SPACING, 0f, 15.5f * SPACING), -90f);

        // Seed bags at the farm entrance road
        TryPlace(propRoot, "Seed_L", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Seed.prefab",
            new Vector3(cx - 2f, 0f, 1f), 0f);
        TryPlace(propRoot, "Seed_R", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Seed.prefab",
            new Vector3(cx + 2f, 0f, 1f), 90f);

        // Tillage rows visible near the crop grid border
        TryPlace(propRoot, "Tillage_S1", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Tillage_4x1.prefab",
            new Vector3(2f * SPACING, 0f, 0.5f * SPACING), 0f, 1f);
        TryPlace(propRoot, "Tillage_S2", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Tillage_4x1.prefab",
            new Vector3(12f * SPACING, 0f, 0.5f * SPACING), 0f, 1f);

        // Harrow near the west fence
        TryPlace(propRoot, "Harrow", "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Harrow.prefab",
            new Vector3(2f, 0f, 8f * SPACING), 35f);

        // Flower patches at corners
        string flowerPath = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Flower.prefab";
        if (AssetExists(flowerPath))
        {
            for (int i = 0; i < 8; i++)
            {
                float fx = Random.Range(-3f, -1f);
                float fz = Random.Range(2f * SPACING, 6f * SPACING);
                TryPlace(propRoot, "Flower_" + i, flowerPath,
                    new Vector3(fx, 0f, fz), Random.Range(0f, 360f), Random.Range(0.8f, 1.3f));
            }
        }
    }

    // ── Fruit & veggie props ──────────────────────────────────────────────────

    static void PlaceFruitProps(Transform parent)
    {
        var fruitRoot = new GameObject("FruitProps").transform;
        fruitRoot.SetParent(parent);

        // Apple zone (x≥10, z≥10) — apples
        ScatterFruitInZone(fruitRoot, "Apple",
            "Assets/Low Poly Fruits/Prefabs/apple.prefab",
            startX: 11f * SPACING, endX: 19f * SPACING,
            startZ: 11f * SPACING, endZ: 19f * SPACING, count: 8);

        // Grape zone (x<10, z≥10) — pears (purple-ish stand-in)
        ScatterFruitInZone(fruitRoot, "Pear",
            "Assets/Low Poly Fruits/Prefabs/pear.prefab",
            startX: 1f * SPACING, endX: 8f * SPACING,
            startZ: 11f * SPACING, endZ: 19f * SPACING, count: 6);

        // Tomato zone (x≥10, z<10) — strawberry/peach
        ScatterFruitInZone(fruitRoot, "Strawberry",
            "Assets/Low Poly Fruits/Prefabs/strawberry.prefab",
            startX: 11f * SPACING, endX: 19f * SPACING,
            startZ: 1f * SPACING, endZ: 8f * SPACING, count: 8);

        // Potato zone (x<10, z<10) — lemon / banana
        ScatterFruitInZone(fruitRoot, "Lemon",
            "Assets/Low Poly Fruits/Prefabs/lemon.prefab",
            startX: 1f * SPACING, endX: 8f * SPACING,
            startZ: 1f * SPACING, endZ: 8f * SPACING, count: 5);

        // Watermelons near the farm entrance for visual pop
        float farmCx = GRID_W * 0.5f;
        TryPlace(fruitRoot, "Watermelon_1", "Assets/Low Poly Fruits/Prefabs/watermelon.prefab",
            new Vector3(farmCx - 4f, 0f, 2f), 0f, 1.2f);
        TryPlace(fruitRoot, "Watermelon_2", "Assets/Low Poly Fruits/Prefabs/watermelon.prefab",
            new Vector3(farmCx + 4f, 0f, 2f), 45f, 1.4f);
    }

    static void ScatterFruitInZone(Transform parent, string label, string prefabPath,
        float startX, float endX, float startZ, float endZ, int count)
    {
        if (!AssetExists(prefabPath)) return;
        var zone = new GameObject("Zone_" + label).transform;
        zone.SetParent(parent);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(startX, endX);
            float z = Random.Range(startZ, endZ);
            // Skip road area (cols 9-10 ≈ x 19.8..22)
            if (x > 19.5f && x < 22.2f) x -= 3f;
            if (z > 19.5f && z < 22.2f) z -= 3f;
            float sc = Random.Range(0.6f, 0.9f);
            Place(zone, label + "_" + i, prefabPath, new Vector3(x, 0f, z),
                Random.Range(0f, 360f), sc);
        }
    }

    // ── Nature clutter (rocks, small bushes) ─────────────────────────────────

    static void PlaceNatureClutter(Transform parent)
    {
        var clutterRoot = new GameObject("NatureClutter").transform;
        clutterRoot.SetParent(parent);

        // Small plants from Gridness along the west side fence inside
        string plantS = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Plant_Small.prefab";
        string plantM = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Plant_Medium.prefab";
        string grass  = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Grass.prefab";

        if (AssetExists(plantS))
        {
            for (int i = 0; i < 12; i++)
            {
                float z = Random.Range(0f, GRID_W);
                float x = Random.Range(-4f, -1.5f);
                TryPlace(clutterRoot, "PlantS_W" + i, plantS,
                    new Vector3(x, 0f, z), Random.Range(0f, 360f), Random.Range(0.7f, 1.3f));
            }
        }
        if (AssetExists(grass))
        {
            for (int i = 0; i < 16; i++)
            {
                // Scatter grass along south and north edges
                bool south = i < 8;
                float x = Random.Range(-4f, GRID_W + 4f);
                float z = south ? Random.Range(-5f, -1f) : Random.Range(GRID_W + 1f, GRID_W + 5f);
                TryPlace(clutterRoot, "Grass_" + i, grass,
                    new Vector3(x, 0f, z), Random.Range(0f, 360f), Random.Range(0.6f, 1.2f));
            }
        }
        if (AssetExists(plantM))
        {
            for (int i = 0; i < 8; i++)
            {
                float x = Random.Range(GRID_W + 2f, GRID_W + 7f);
                float z = Random.Range(0f, GRID_W);
                TryPlace(clutterRoot, "PlantM_E" + i, plantM,
                    new Vector3(x, 0f, z), Random.Range(0f, 360f), Random.Range(0.8f, 1.4f));
            }
        }
    }

    // ── Placement helpers ─────────────────────────────────────────────────────

    static GameObject Place(Transform parent, string goName, string prefabPath,
        Vector3 pos, float yRot = 0f, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject go;
        if (prefab != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        else
        {
            // Fallback: place a coloured cube so the slot is visible
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        go.name = goName;
        go.transform.position  = pos;
        go.transform.rotation  = Quaternion.Euler(0f, yRot, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static GameObject TryPlace(Transform parent, string goName, string prefabPath,
        Vector3 pos, float yRot = 0f, float scale = 1f)
    {
        if (!AssetExists(prefabPath))
        {
            Debug.LogWarning($"[SceneEnricher] Prefab not found: {prefabPath}");
            return null;
        }
        return Place(parent, goName, prefabPath, pos, yRot, scale);
    }

    static bool AssetExists(string path) =>
        System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../" + path));

}
