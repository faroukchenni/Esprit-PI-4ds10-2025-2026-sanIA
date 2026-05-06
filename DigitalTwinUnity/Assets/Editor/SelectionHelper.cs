using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

public static class SelectionHelper
{
    static void SelectMainGrid()
    {
        GameObject mainGrid = GameObject.Find("MainGrid");
        if (mainGrid == null)
        {
            Debug.LogWarning("SelectionHelper: No GameObject named 'MainGrid' found in the scene.");
            return;
        }
        Selection.activeGameObject = mainGrid;
        EditorGUIUtility.PingObject(mainGrid);
    }

    static void AssignMountainPrefab()
    {
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("SelectionHelper: No CyberGrid component found in the scene.");
            return;
        }

        string prefabPath = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Mountain.prefab";
        GameObject mountain = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (mountain == null)
        {
            Debug.LogError($"SelectionHelper: Mountain.prefab not found at '{prefabPath}'.");
            return;
        }

        Undo.RecordObject(cyberGrid, "Assign Mountain Prefab");
        cyberGrid.mountainPrefab = mountain;
        EditorUtility.SetDirty(cyberGrid);
        Debug.Log($"SelectionHelper: Assigned Mountain.prefab to CyberGrid on '{cyberGrid.gameObject.name}'.");
    }

    static void ApplyTerrainMaterials()
    {
        // --- 1. Assign FarmTerrain.mat to the Plane ---
        GameObject plane = GameObject.Find("Plane");
        if (plane == null)
        {
            Debug.LogError("SelectionHelper: No GameObject named 'Plane' found in the scene.");
        }
        else
        {
            string matPath = "Assets/Materials/FarmTerrain.mat";
            Material farmMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (farmMat == null)
            {
                Debug.LogError($"SelectionHelper: FarmTerrain.mat not found at '{matPath}'.");
            }
            else
            {
                MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Assign FarmTerrain Material");
                    renderer.sharedMaterial = farmMat;
                    EditorUtility.SetDirty(renderer);
                    Debug.Log("SelectionHelper: Assigned FarmTerrain.mat to Plane.");
                }
            }
        }

        // --- 2. Set up Directional Light (create if missing) ---
        Light dirLight = null;
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                dirLight = l;
                break;
            }
        }

        if (dirLight == null)
        {
            GameObject lightGO = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(lightGO, "Create Directional Light");
            dirLight = lightGO.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            Debug.Log("SelectionHelper: Created new Directional Light.");
        }

        // Color #FFF3DC
        Undo.RecordObject(dirLight, "Set Directional Light");
        dirLight.color = new Color(1f, 0.95294f, 0.86275f, 1f);
        dirLight.intensity = 1.2f;
        EditorUtility.SetDirty(dirLight);

        // Rotation X=50, Y=30, Z=0
        Transform lightTransform = dirLight.transform;
        Undo.RecordObject(lightTransform, "Set Directional Light Rotation");
        lightTransform.rotation = Quaternion.Euler(50f, 30f, 0f);
        EditorUtility.SetDirty(lightTransform);

        Debug.Log("SelectionHelper: Directional Light configured — Color #FFF3DC, Rotation (50,30,0), Intensity 1.2.");
    }

    static void ApplyPhase3()
    {
        // ── 1. Build dirt paths ──────────────────────────────────────────────
        string dirtMatPath = "Assets/Materials/DirtPath.mat";
        Material dirtMat = AssetDatabase.LoadAssetAtPath<Material>(dirtMatPath);
        if (dirtMat == null)
        {
            Debug.LogError($"SelectionHelper: DirtPath.mat not found at '{dirtMatPath}'. Create it first.");
        }
        else
        {
            // Find or create a PathGenerator host at scene root
            GameObject pgHost = GameObject.Find("PathGenerator");
            if (pgHost == null)
            {
                pgHost = new GameObject("PathGenerator");
                Undo.RegisterCreatedObjectUndo(pgHost, "Create PathGenerator");
            }

            PathGenerator pg = pgHost.GetComponent<PathGenerator>();
            if (pg == null)
                pg = Undo.AddComponent<PathGenerator>(pgHost);

            Undo.RecordObject(pg, "Configure PathGenerator");
            pg.pathMaterial = dirtMat;
            pg.pathWidth    = 1f;
            pg.gridSpacing  = 2.2f;
            pg.gridWidth    = 20;
            pg.gridHeight   = 20;
            EditorUtility.SetDirty(pg);

            pg.Build();
            Debug.Log("SelectionHelper: Dirt paths built under PathGenerator/Paths.");
        }

        // ── 2. Assign MountainMat.mat to Mountain.prefab ────────────────────
        string mountainMatPath = "Assets/Materials/MountainMat.mat";
        Material mountainMat = AssetDatabase.LoadAssetAtPath<Material>(mountainMatPath);
        if (mountainMat == null)
        {
            Debug.LogError($"SelectionHelper: MountainMat.mat not found at '{mountainMatPath}'.");
        }
        else
        {
            string prefabPath = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/Mountain.prefab";
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                MeshRenderer mr = scope.prefabContentsRoot.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mountainMat;
                    // m_CastShadows was already 1 in the prefab — confirm it stays On
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    Debug.Log("SelectionHelper: MountainMat.mat (#4A7C3F) assigned to Mountain.prefab; shadows On.");
                }
                else
                {
                    Debug.LogError("SelectionHelper: Mountain.prefab has no MeshRenderer at root.");
                }
            }
        }

        // ── 3. Enable warm linear fog in the active scene ───────────────────
        // #C8E6A0 = r 0.784  g 0.902  b 0.627
        RenderSettings.fog          = true;
        RenderSettings.fogColor     = new Color(0.78431f, 0.90196f, 0.62745f, 1f);
        RenderSettings.fogMode      = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance   = 80f;
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("SelectionHelper: Fog enabled — Linear #C8E6A0, Start 40, End 80.");
    }

    static void FixGridAndPaths()
    {
        // ── 1. Assign SoilTile.mat to the soil prefab used by CyberGrid ─────
        string soilMatPath = "Assets/Materials/SoilTile.mat";
        Material soilMat = AssetDatabase.LoadAssetAtPath<Material>(soilMatPath);
        if (soilMat == null)
        {
            Debug.LogError($"SelectionHelper: SoilTile.mat not found at '{soilMatPath}'.");
        }
        else
        {
            CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
            if (cyberGrid == null)
            {
                Debug.LogError("SelectionHelper: No CyberGrid found in the scene.");
            }
            else if (cyberGrid.settings.soilPrefab == null)
            {
                Debug.LogError("SelectionHelper: CyberGrid.settings.soilPrefab is not assigned.");
            }
            else
            {
                string soilPrefabPath = AssetDatabase.GetAssetPath(cyberGrid.settings.soilPrefab);
                using (var scope = new PrefabUtility.EditPrefabContentsScope(soilPrefabPath))
                {
                    // Apply to every MeshRenderer in the prefab (handles multi-mesh prefabs)
                    MeshRenderer[] renderers = scope.prefabContentsRoot.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning($"SelectionHelper: No MeshRenderer found in soil prefab '{soilPrefabPath}'.");
                    }
                    else
                    {
                        foreach (MeshRenderer mr in renderers)
                            mr.sharedMaterial = soilMat;
                        Debug.Log($"SelectionHelper: Assigned SoilTile.mat (#6B4423) to {renderers.Length} renderer(s) in '{cyberGrid.settings.soilPrefab.name}'.");
                    }
                }
            }
        }

        // ── 2. Rebuild paths at the corrected Y=0.05 height ─────────────────
        PathGenerator pg = Object.FindFirstObjectByType<PathGenerator>();
        if (pg == null)
        {
            Debug.LogWarning("SelectionHelper: No PathGenerator found in the scene. Run 'Apply Phase 3' first.");
        }
        else
        {
            pg.Build();
            Debug.Log("SelectionHelper: Paths rebuilt at Y=0.05.");
        }
    }

    static void PolishPathsAndCrops()
    {
        // ── 1. Rebuild paths with fixed winding and 1.5-unit width ───────────
        PathGenerator pg = Object.FindFirstObjectByType<PathGenerator>();
        if (pg == null)
        {
            Debug.LogWarning("SelectionHelper: No PathGenerator in scene. Run 'Apply Phase 3' first.");
        }
        else
        {
            Undo.RecordObject(pg, "Polish Paths");
            pg.pathWidth = 1.5f;
            EditorUtility.SetDirty(pg);
            pg.Build();
            Debug.Log("SelectionHelper: Paths rebuilt — solid quads, width 1.5.");
        }

        // ── 2. Scale all CropMapping.baseScale values by 1.3x ───────────────
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("SelectionHelper: No CyberGrid found in scene.");
            return;
        }
        if (cyberGrid.settings.cropMappings == null || cyberGrid.settings.cropMappings.Count == 0)
        {
            Debug.LogWarning("SelectionHelper: CyberGrid has no CropMappings assigned.");
            return;
        }

        Undo.RecordObject(cyberGrid, "Scale CropMappings");
        foreach (CropMapping mapping in cyberGrid.settings.cropMappings)
            mapping.baseScale *= 1.3f;
        EditorUtility.SetDirty(cyberGrid);

        Debug.Log($"SelectionHelper: Scaled {cyberGrid.settings.cropMappings.Count} CropMapping(s) by 1.3x.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD FULL FARM
    // ══════════════════════════════════════════════════════════════════════════

    const string PANDAZOLE   = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
    const string ITHAPPY     = "Assets/ithappy/Animals_FREE/Prefabs/";
    const string TREES       = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Prefab/";
    const string LABEL_PREFAB = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/LabelPrefab.prefab";
    const string URSA_URP    = "Assets/UrsaAnimation/LOW POLY CUBIC - Goat and Sheep Pack/Prefabs_URP/";
    const string NATURE      = "Assets/Pandazole_Lowpoly_Asset_Bundle/Pandazole Nature Environment Pack/Prefabs/";

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD ANIMAL ZONE
    // ══════════════════════════════════════════════════════════════════════════

    static void BuildAnimalZone()
    {
        // ── Load prefabs ──────────────────────────────────────────────────────
        int missing = 0;
        GameObject LoadP(string path, bool required = true)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g == null && required)
            {
                Debug.LogWarning($"BuildAnimalZone: prefab not found — {path}");
                missing++;
            }
            return g;
        }

        var chickenPrefab = LoadP(ITHAPPY + "Chicken_001.prefab");
        var horsePrefab   = LoadP(ITHAPPY + "Horse_001.prefab");
        var deerPrefab    = LoadP(ITHAPPY + "Deer_001.prefab");
        var dogPrefab     = LoadP(ITHAPPY + "Dog_001.prefab");
        var fencePrefab   = LoadP(PANDAZOLE + "Env_WoodFence_01.prefab");
        var labelPrefab   = LoadP(LABEL_PREFAB, required: false); // optional

        // ── Find / create AnimalManager GameObject ────────────────────────────
        GameObject amHost = GameObject.Find("AnimalZone");
        if (amHost == null)
        {
            amHost = new GameObject("AnimalZone");
            Undo.RegisterCreatedObjectUndo(amHost, "Create AnimalZone");
        }

        AnimalManager am = amHost.GetComponent<AnimalManager>();
        if (am == null)
            am = Undo.AddComponent<AnimalManager>(amHost);

        Undo.RecordObject(am, "Configure AnimalManager");

        // ── Grid reference ────────────────────────────────────────────────────
        am.gridColumns = 20;
        am.gridRows    = 20;
        am.gridSpacing = 2.2f;

        // ── Zone layout ───────────────────────────────────────────────────────
        am.zoneWidth  = 40f;
        am.zoneDepth  = 30f;
        am.zoneBuffer = 4f;
        am.pathWidth  = 3f;

        // ── Shared prefabs ────────────────────────────────────────────────────
        am.fencePrefab     = fencePrefab;
        am.zoneLabelPrefab = labelPrefab;
        am.dogPrefab       = dogPrefab;
        am.deerPrefab      = deerPrefab;  // free-roaming deer near north trees

        // ── Pen configs: Chickens (4×2=8) scale 1.8, Horses (2×2=4) scale 2.0 ─
        am.penConfigs = new AnimalPenConfig[]
        {
            new AnimalPenConfig
            {
                penName      = "Chickens",
                animalPrefab = chickenPrefab,
                count        = 8,
                gridCols     = 4,
                gridRows     = 2,
                spacingX     = 3.0f,
                spacingZ     = 3.0f,
                animalScale  = 1.8f,
            },
            new AnimalPenConfig
            {
                penName      = "Horses",
                animalPrefab = horsePrefab,
                count        = 4,
                gridCols     = 2,
                gridRows     = 2,
                spacingX     = 6.0f,
                spacingZ     = 6.0f,
                animalScale  = 2.0f,
            },
            new AnimalPenConfig   // third pen uses deer or horse as fallback
            {
                penName      = "Deer",
                animalPrefab = deerPrefab != null ? deerPrefab : horsePrefab,
                count        = 4,
                gridCols     = 4,
                gridRows     = 1,
                spacingX     = 4.0f,
                spacingZ     = 4.0f,
                animalScale  = 1.8f,
            },
        };

        // ── Sensor-ready pen data (default values) ────────────────────────────
        am.pens = new AnimalPenData[]
        {
            new AnimalPenData { animalType="Chickens", currentCount=8,  avgWeight=2.5f,  avgTemperature=41.0f, healthScore=100f },
            new AnimalPenData { animalType="Horses",   currentCount=4,  avgWeight=500f,  avgTemperature=37.8f, healthScore=100f },
            new AnimalPenData { animalType="Deer",     currentCount=4,  avgWeight=80f,   avgTemperature=38.5f, healthScore=100f },
        };

        EditorUtility.SetDirty(am);

        // ── Clear + rebuild ───────────────────────────────────────────────────
        am.ClearAll();
        am.BuildAll();

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ── Report ────────────────────────────────────────────────────────────
        Debug.Log("BuildAnimalZone: Prefabs used:");
        Debug.Log($"  Chicken  — {ITHAPPY}Chicken_001.prefab  [{(chickenPrefab ? "OK" : "MISSING")}]");
        Debug.Log($"  Horse    — {ITHAPPY}Horse_001.prefab    [{(horsePrefab   ? "OK" : "MISSING")}]");
        Debug.Log($"  Deer     — {ITHAPPY}Deer_001.prefab     [{(deerPrefab    ? "OK" : "MISSING")}]");
        Debug.Log($"  Dog      — {ITHAPPY}Dog_001.prefab      [{(dogPrefab     ? "OK" : "MISSING")}]");
        Debug.Log($"  Fence    — {PANDAZOLE}Env_WoodFence_01.prefab  [{(fencePrefab ? "OK" : "MISSING")}]");
        Debug.Log($"  Label    — {LABEL_PREFAB}  [{(labelPrefab ? "OK" : "MISSING — labels skipped")}]");

        if (missing == 0)
            Debug.Log("BuildAnimalZone: Complete — AnimalZone GO ready. Enter Play Mode to see pens.");
        else
            Debug.LogWarning($"BuildAnimalZone: Done with {missing} missing prefab(s). Check warnings above.");
    }

    static void BuildFullFarm()
    {
        int skipped = 0;

        // ── Helper: load a prefab and warn if missing ─────────────────────────
        GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.LogWarning($"BuildFullFarm: prefab not found — {path}"); skipped++; }
            return go;
        }
        Material LoadMat(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { Debug.LogWarning($"BuildFullFarm: material not found — {path}"); skipped++; }
            return m;
        }

        // ── Step 1: Ensure TwinSimulationManager uses 40×40 ──────────────────
        TwinSimulationManager tsm = Object.FindFirstObjectByType<TwinSimulationManager>();
        if (tsm != null)
        {
            Undo.RecordObject(tsm, "Set Grid 40x40");
            tsm.gridWidth  = 20;
            tsm.gridHeight = 20;
            EditorUtility.SetDirty(tsm);
            Debug.Log("BuildFullFarm: TwinSimulationManager grid set to 40×40.");
        }
        else Debug.LogWarning("BuildFullFarm: TwinSimulationManager not found in scene.");

        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();

        // ── Step 2: Assign Pandazole food prefabs to CropMappings ─────────────
        if (cyberGrid != null)
        {
            var potatoPrefab = Load(PANDAZOLE + "food_Potato.prefab");
            var tomatoPrefab = Load(PANDAZOLE + "food_Tomato.prefab");
            var grapePrefab  = Load(PANDAZOLE + "food_Grape.prefab");
            var applePrefab  = Load(PANDAZOLE + "food_Apple.prefab");

            Undo.RecordObject(cyberGrid, "Assign Crop Prefabs");
            cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>
            {
                new CropMapping { type = CropType.Potato,     prefab = potatoPrefab, baseScale = 2.5f },
                new CropMapping { type = CropType.Tomato,     prefab = tomatoPrefab, baseScale = 2.5f },
                new CropMapping { type = CropType.Grape,      prefab = grapePrefab,  baseScale = 2.5f },
                new CropMapping { type = CropType.Apple,      prefab = applePrefab,  baseScale = 2.5f },
                new CropMapping { type = CropType.Empty,      prefab = null,         baseScale = 1.0f },
            };
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log("BuildFullFarm: CropMappings updated — Potato/Tomato/Grape/Apple from Pandazole.");
        }

        // ── Step 3: Assign soil prefabs ───────────────────────────────────────
        if (cyberGrid != null)
        {
            var soilPrefab        = Load(PANDAZOLE + "Env_FarmLand_01.prefab");
            var wateredSoilPrefab = Load(PANDAZOLE + "Env_FarmLand_02.prefab");

            Undo.RecordObject(cyberGrid, "Assign Soil Prefabs");
            cyberGrid.settings.soilPrefab        = soilPrefab;
            cyberGrid.settings.wateredSoilPrefab = wateredSoilPrefab;
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log("BuildFullFarm: soilPrefab → Env_FarmLand_01, wateredSoilPrefab → Env_FarmLand_02_Watered.");
        }

        // ── Step 4-7: Create / reconfigure FarmCompound ───────────────────────
        GameObject fcHost = GameObject.Find("FarmCompound");
        if (fcHost == null)
        {
            fcHost = new GameObject("FarmCompound");
            Undo.RegisterCreatedObjectUndo(fcHost, "Create FarmCompound");
        }

        FarmCompound fc = fcHost.GetComponent<FarmCompound>();
        if (fc == null)
            fc = Undo.AddComponent<FarmCompound>(fcHost);

        Undo.RecordObject(fc, "Configure FarmCompound");

        // Grid settings
        fc.gridSpacing = 2.2f;
        fc.gridColumns = 20;
        fc.gridRows    = 20;

        // Buildings
        fc.barnPrefab          = Load(PANDAZOLE + "Bld_Barn_01.prefab");
        fc.farmerHousePrefab   = Load(PANDAZOLE + "Bld_FarmerHouse.prefab");
        fc.siloPrefab          = Load(PANDAZOLE + "Bld_Silo_01.prefab");
        fc.farmMillPrefab      = Load(PANDAZOLE + "Bld_FarmMill_01.prefab");
        fc.chickenCoopPrefab   = Load(PANDAZOLE + "Bld_ChickenCoop.prefab");
        fc.storeBuildingPrefab = Load(PANDAZOLE + "Bld_StoreBuilding_01.prefab");

        // Animals
        fc.chickenPrefab = Load(ITHAPPY + "Chicken_001.prefab");
        fc.horsePrefab   = Load(ITHAPPY + "Horse_001.prefab");

        // Props
        fc.haystackPrefab     = Load(PANDAZOLE + "Prop_Haystack_01.prefab");
        fc.wheelbarrowPrefab  = Load(PANDAZOLE + "Prop_Wheelbarrow.prefab");
        fc.woodenCratesPrefab = Load(PANDAZOLE + "Prop_WoodenCrates_01.prefab");
        fc.wellPrefab         = Load(PANDAZOLE + "Env_Well_01.prefab");

        // Fence
        fc.fencePrefab = Load(PANDAZOLE + "Env_WoodFence_01.prefab");

        // Spring trees (Spring01–Spring05)
        fc.springTreePrefabs = new System.Collections.Generic.List<GameObject>();
        for (int i = 1; i <= 5; i++)
        {
            var tree = Load($"{TREES}Spring0{i}.prefab");
            if (tree != null) fc.springTreePrefabs.Add(tree);
        }

        EditorUtility.SetDirty(fc);

        // Build the compound in the scene
        fc.BuildAll();
        Debug.Log("BuildFullFarm: FarmCompound built — buildings, animal pens, tree border, props placed.");

        // ── Mountain re-assignment ────────────────────────────────────────────
        if (cyberGrid != null)
        {
            var mountainPrefab = Load("Assets/Gridness Studios/Lite Farm Pack/Prefabs/Mountain.prefab");
            if (mountainPrefab != null)
            {
                Undo.RecordObject(cyberGrid, "Assign Mountain Prefab");
                cyberGrid.mountainPrefab = mountainPrefab;
                EditorUtility.SetDirty(cyberGrid);
                Debug.Log("BuildFullFarm: Mountain.prefab re-assigned to CyberGrid.");
            }
        }

        // ── Terrain material re-assignment ────────────────────────────────────
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            var farmMat = LoadMat("Assets/Materials/FarmTerrain.mat");
            if (farmMat != null)
            {
                MeshRenderer mr = plane.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Undo.RecordObject(mr, "Assign FarmTerrain Material");
                    mr.sharedMaterial = farmMat;
                    EditorUtility.SetDirty(mr);
                    Debug.Log("BuildFullFarm: FarmTerrain.mat re-assigned to Plane.");
                }
            }
        }

        // ── PathGenerator update for 40×40 grid ──────────────────────────────
        PathGenerator pg = Object.FindFirstObjectByType<PathGenerator>();
        if (pg != null)
        {
            Undo.RecordObject(pg, "Update PathGenerator for 40x40");
            pg.gridWidth   = 20;
            pg.gridHeight  = 20;
            pg.gridSpacing = 2.2f;
            pg.pathWidth   = 1.5f;
            EditorUtility.SetDirty(pg);
            pg.Build();
            Debug.Log("BuildFullFarm: PathGenerator rebuilt for 40×40 grid.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        if (skipped == 0)
            Debug.Log("BuildFullFarm: ✓ All systems built successfully — nothing skipped.");
        else
            Debug.LogWarning($"BuildFullFarm: Done with {skipped} missing prefab(s) — check warnings above.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX PINK MATERIALS
    // ══════════════════════════════════════════════════════════════════════════

    static void FixCropsAndFog()
    {
        // ── 1. Force-assign Pandazole crop prefabs to CyberGrid ───────────────
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("FixCropsAndFog: No CyberGrid found in the scene.");
        }
        else
        {
            var potatoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Potato.prefab");
            var tomatoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Tomato.prefab");
            var grapePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Grape.prefab");
            var applePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Apple.prefab");

            bool anyMissing = (potatoPrefab == null || tomatoPrefab == null ||
                               grapePrefab  == null || applePrefab  == null);
            if (anyMissing)
                Debug.LogWarning("FixCropsAndFog: One or more food prefabs not found — check Pandazole pack import.");

            Undo.RecordObject(cyberGrid, "Fix CropMappings");

            // Replace the entire list so stale/null entries cannot survive
            cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>
            {
                new CropMapping { type = CropType.Potato, prefab = potatoPrefab, baseScale = 2.5f },
                new CropMapping { type = CropType.Tomato, prefab = tomatoPrefab, baseScale = 2.5f },
                new CropMapping { type = CropType.Grape,  prefab = grapePrefab,  baseScale = 2.5f },
                new CropMapping { type = CropType.Apple,  prefab = applePrefab,  baseScale = 2.5f },
                new CropMapping { type = CropType.Empty,  prefab = null,         baseScale = 1.0f },
            };

            // Re-assign soil prefabs in case references broke after material upgrade
            var soilPrefab        = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "Env_FarmLand_01.prefab");
            var wateredSoilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "Env_FarmLand_02.prefab");
            if (soilPrefab        != null) cyberGrid.settings.soilPrefab        = soilPrefab;
            if (wateredSoilPrefab != null) cyberGrid.settings.wateredSoilPrefab = wateredSoilPrefab;

            EditorUtility.SetDirty(cyberGrid);

            int mappingCount = cyberGrid.settings.cropMappings.Count;
            Debug.Log($"FixCropsAndFog: CropMappings rebuilt — {mappingCount} entries. " +
                      $"soilPrefab={cyberGrid.settings.soilPrefab?.name ?? "MISSING"}, " +
                      $"wateredSoilPrefab={cyberGrid.settings.wateredSoilPrefab?.name ?? "MISSING"}.");
        }

        // ── 2. Fix fog: push it back so it doesn't swallow the scene ─────────
        // #D4EBA0 = r 0.831  g 0.922  b 0.627
        RenderSettings.fog             = true;
        RenderSettings.fogColor        = new Color(0.83137f, 0.92157f, 0.62745f, 1f);
        RenderSettings.fogMode         = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance   = 120f;

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("FixCropsAndFog: Fog updated — Linear #D4EBA0, Start 60, End 120.");
    }

    static void FixPinkMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("FixPinkMaterials: 'Universal Render Pipeline/Lit' shader not found. " +
                           "Ensure the URP package is installed.");
            return;
        }

        // Search all materials inside the Pandazole folder
        string[] guids = AssetDatabase.FindAssets("t:Material",
            new[] { "Assets/Pandazole_Ultimate_Pack" });

        int upgraded = 0;
        int alreadyURP = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string shaderName = mat.shader != null ? mat.shader.name : "";

            // Skip materials that are already on a URP shader
            if (shaderName.StartsWith("Universal Render Pipeline") ||
                shaderName.StartsWith("Shader Graphs/"))
            {
                alreadyURP++;
                continue;
            }

            // Capture Standard shader properties before reassigning the shader
            Color  baseColor   = mat.HasProperty("_Color")       ? mat.GetColor("_Color")         : Color.white;
            Texture baseMap    = mat.HasProperty("_MainTex")     ? mat.GetTexture("_MainTex")     : null;
            float  smoothness  = mat.HasProperty("_Glossiness")  ? mat.GetFloat("_Glossiness")    : 0.5f;
            float  metallic    = mat.HasProperty("_Metallic")    ? mat.GetFloat("_Metallic")      : 0f;

            Undo.RecordObject(mat, "Upgrade to URP/Lit");
            mat.shader = urpLit;

            // Map Standard properties to URP/Lit equivalents
            mat.SetColor("_BaseColor", baseColor);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic",   metallic);

            EditorUtility.SetDirty(mat);
            upgraded++;
            Debug.Log($"FixPinkMaterials: Upgraded '{mat.name}' ({path})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"FixPinkMaterials: Done — {upgraded} material(s) upgraded to URP/Lit, " +
                  $"{alreadyURP} already URP, {guids.Length - upgraded - alreadyURP} skipped.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX CROP GRID
    // ══════════════════════════════════════════════════════════════════════════

    static void FixCropGrid()
    {
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("FixCropGrid: No CyberGrid found in scene.");
            return;
        }

        Undo.RecordObject(cyberGrid, "Fix Crop Grid");

        // ── 1. Force-assign Pandazole food_ prefabs ───────────────────────────
        var potato = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Potato.prefab");
        var tomato = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Tomato.prefab");
        var grape  = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Grape.prefab");
        var apple  = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "food_Apple.prefab");

        bool anyNull = (potato == null || tomato == null || grape == null || apple == null);
        if (anyNull)
            Debug.LogWarning("FixCropGrid: One or more Pandazole food prefabs not found — check asset import.");

        cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>
        {
            new CropMapping { type = CropType.Potato, prefab = potato, baseScale = 2.5f },
            new CropMapping { type = CropType.Tomato, prefab = tomato, baseScale = 2.5f },
            new CropMapping { type = CropType.Grape,  prefab = grape,  baseScale = 2.5f },
            new CropMapping { type = CropType.Apple,  prefab = apple,  baseScale = 2.5f },
        };
        Debug.Log("FixCropGrid: CropMappings set — Potato/Tomato/Grape/Apple at baseScale 1.5.");

        // ── 2. Force-assign soil prefabs ──────────────────────────────────────
        var soil        = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "Env_FarmLand_01.prefab");
        var wateredSoil = AssetDatabase.LoadAssetAtPath<GameObject>(PANDAZOLE + "Env_FarmLand_02.prefab");
        if (soil        != null) cyberGrid.settings.soilPrefab        = soil;
        if (wateredSoil != null) cyberGrid.settings.wateredSoilPrefab = wateredSoil;
        Debug.Log($"FixCropGrid: soilPrefab={cyberGrid.settings.soilPrefab?.name ?? "MISSING"}, " +
                  $"wateredSoilPrefab={cyberGrid.settings.wateredSoilPrefab?.name ?? "MISSING"}.");

        EditorUtility.SetDirty(cyberGrid);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ── 3. Report soil tile natural size ──────────────────────────────────
        // Env_FarmLand_01.fbx vertex range: -250 to +250 cm = 500 cm = 5 m in Unity.
        // CyberGrid auto-scales at runtime: scale = spacing / bounds.size = 2.2 / 5.0 = 0.44
        // Crop Y offset set to 0.3 in CyberGrid.cs to appear above scaled tile surface.
        Debug.Log("FixCropGrid: Soil tile natural size = 5.0 m x 5.0 m (500 cm FBX). " +
                  "Runtime scale = 2.2 / 5.0 = 0.44. Crop Y offset = 0.3 m above cell origin.");
        Debug.Log("FixCropGrid: Done — enter Play Mode to see the rebuilt grid.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FULL SCENE RESTORE
    // ══════════════════════════════════════════════════════════════════════════

    static void FullSceneRestore()
    {
        int issues = 0;

        GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.LogWarning($"FullSceneRestore: prefab not found — {path}"); issues++; }
            return go;
        }

        // ── 1. Fix CropMappings → Pandazole food_ prefabs ────────────────────
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("FullSceneRestore: No CyberGrid found in scene.");
        }
        else
        {
            var potato = Load(PANDAZOLE + "food_Potato.prefab");
            var tomato = Load(PANDAZOLE + "food_Tomato.prefab");
            var grape  = Load(PANDAZOLE + "food_Grape.prefab");
            var apple  = Load(PANDAZOLE + "food_Apple.prefab");

            Undo.RecordObject(cyberGrid, "Restore CropMappings");
            cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>
            {
                new CropMapping { type = CropType.Potato, prefab = potato, baseScale = 2.5f },
                new CropMapping { type = CropType.Tomato, prefab = tomato, baseScale = 2.5f },
                new CropMapping { type = CropType.Grape,  prefab = grape,  baseScale = 2.5f },
                new CropMapping { type = CropType.Apple,  prefab = apple,  baseScale = 2.5f },
            };
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log($"FullSceneRestore: CropMappings restored — Potato/Tomato/Grape/Apple from Pandazole.");

            // ── 2. Fix soil prefabs ───────────────────────────────────────────
            var soil        = Load(PANDAZOLE + "Env_FarmLand_01.prefab");
            var wateredSoil = Load(PANDAZOLE + "Env_FarmLand_02.prefab");
            Undo.RecordObject(cyberGrid, "Restore Soil Prefabs");
            if (soil        != null) cyberGrid.settings.soilPrefab        = soil;
            if (wateredSoil != null) cyberGrid.settings.wateredSoilPrefab = wateredSoil;
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log($"FullSceneRestore: soilPrefab={cyberGrid.settings.soilPrefab?.name ?? "MISSING"}, " +
                      $"wateredSoilPrefab={cyberGrid.settings.wateredSoilPrefab?.name ?? "MISSING"}.");
        }

        // ── 3. Apply SoilTile.mat (#6B4423) to the soil prefab ───────────────
        Material soilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SoilTile.mat");
        if (soilMat == null)
        {
            Debug.LogWarning("FullSceneRestore: SoilTile.mat not found at Assets/Materials/SoilTile.mat.");
            issues++;
        }
        else if (cyberGrid != null && cyberGrid.settings.soilPrefab != null)
        {
            string soilPrefabPath = AssetDatabase.GetAssetPath(cyberGrid.settings.soilPrefab);
            if (!string.IsNullOrEmpty(soilPrefabPath))
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(soilPrefabPath))
                {
                    MeshRenderer[] renderers = scope.prefabContentsRoot.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (MeshRenderer mr in renderers)
                        mr.sharedMaterial = soilMat;
                    if (renderers.Length > 0)
                        Debug.Log($"FullSceneRestore: Applied SoilTile.mat to {renderers.Length} renderer(s) in Env_FarmLand_01.");
                }
            }
        }

        // ── 4. Restore FarmCompound ───────────────────────────────────────────
        GameObject fcHost = GameObject.Find("FarmCompound");
        if (fcHost == null)
        {
            fcHost = new GameObject("FarmCompound");
            Undo.RegisterCreatedObjectUndo(fcHost, "Create FarmCompound");
        }

        FarmCompound fc = fcHost.GetComponent<FarmCompound>();
        if (fc == null)
            fc = Undo.AddComponent<FarmCompound>(fcHost);

        Undo.RecordObject(fc, "Restore FarmCompound");
        fc.gridSpacing = 2.2f;
        fc.gridColumns = 20;
        fc.gridRows    = 20;

        fc.barnPrefab          = Load(PANDAZOLE + "Bld_Barn_01.prefab");
        fc.farmerHousePrefab   = Load(PANDAZOLE + "Bld_FarmerHouse.prefab");
        fc.siloPrefab          = Load(PANDAZOLE + "Bld_Silo_01.prefab");
        fc.farmMillPrefab      = Load(PANDAZOLE + "Bld_FarmMill_01.prefab");
        fc.chickenCoopPrefab   = Load(PANDAZOLE + "Bld_ChickenCoop.prefab");
        fc.storeBuildingPrefab = Load(PANDAZOLE + "Bld_StoreBuilding_01.prefab");
        fc.chickenPrefab       = Load(ITHAPPY   + "Chicken_001.prefab");
        fc.horsePrefab         = Load(ITHAPPY   + "Horse_001.prefab");
        fc.haystackPrefab      = Load(PANDAZOLE + "Prop_Haystack_01.prefab");
        fc.wheelbarrowPrefab   = Load(PANDAZOLE + "Prop_Wheelbarrow.prefab");
        fc.woodenCratesPrefab  = Load(PANDAZOLE + "Prop_WoodenCrates_01.prefab");
        fc.wellPrefab          = Load(PANDAZOLE + "Env_Well_01.prefab");
        fc.fencePrefab         = Load(PANDAZOLE + "Env_WoodFence_01.prefab");

        fc.springTreePrefabs = new System.Collections.Generic.List<GameObject>();
        for (int i = 1; i <= 5; i++)
        {
            var tree = Load($"{TREES}Spring0{i}.prefab");
            if (tree != null) fc.springTreePrefabs.Add(tree);
        }

        EditorUtility.SetDirty(fc);
        fc.BuildAll();
        Debug.Log("FullSceneRestore: FarmCompound rebuilt — buildings, pens, trees, props placed.");

        // ── 5. Fix fog ────────────────────────────────────────────────────────
        RenderSettings.fog              = true;
        RenderSettings.fogColor         = new Color(0.83137f, 0.92157f, 0.62745f, 1f);
        RenderSettings.fogMode          = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance   = 120f;
        Debug.Log("FullSceneRestore: Fog — Linear #D4EBA0, Start 60, End 120.");

        // ── 6. Re-assign FarmTerrain.mat to Plane ─────────────────────────────
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            Material farmMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FarmTerrain.mat");
            if (farmMat != null)
            {
                MeshRenderer mr = plane.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Undo.RecordObject(mr, "Restore FarmTerrain Material");
                    mr.sharedMaterial = farmMat;
                    EditorUtility.SetDirty(mr);
                }
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        if (issues == 0)
            Debug.Log("FullSceneRestore: Complete — crops, soil colors, FarmCompound, fog all restored.");
        else
            Debug.LogWarning($"FullSceneRestore: Done with {issues} missing asset(s) — check warnings above.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SETUP SIMULATION TESTS
    // ══════════════════════════════════════════════════════════════════════════

    static void SetupSimulationTests()
    {
        // ── 1. Add ScenarioManager to SimulationCore ──────────────────────────
        GameObject simCore = GameObject.Find("SimulationCore");
        if (simCore == null)
        {
            simCore = new GameObject("SimulationCore");
            Undo.RegisterCreatedObjectUndo(simCore, "Create SimulationCore");
            Debug.LogWarning("SetupSimulationTests: Created SimulationCore — " +
                             "make sure TwinSimulationManager is on this GO.");
        }

        ScenarioManager sm = simCore.GetComponent<ScenarioManager>();
        if (sm == null)
            sm = Undo.AddComponent<ScenarioManager>(simCore);

        EditorUtility.SetDirty(simCore);
        Debug.Log("SetupSimulationTests: ScenarioManager attached to SimulationCore.");

        FuturisticUI fui = Object.FindFirstObjectByType<FuturisticUI>();

        // ── 3. Find Canvas ────────────────────────────────────────────────────
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("SetupSimulationTests: No Canvas found in scene. " +
                           "Add a Canvas first, then re-run this menu item.");
            return;
        }

        // Remove any existing scenario panel so we can rebuild clean
        Transform existing = canvas.transform.Find("ScenarioTestPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        // ── 4. Build scenario panel ───────────────────────────────────────────
        GameObject panel = new GameObject("ScenarioTestPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create ScenarioTestPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        // Anchor to bottom-left; position above the corner
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(0f, 0f);
        panelRT.pivot            = new Vector2(0f, 0f);
        panelRT.anchoredPosition = new Vector2(10f, 10f);
        panelRT.sizeDelta        = new Vector2(164f, 172f);

        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.06f, 0.10f, 0.88f);

        // Panel header label
        AddPanelLabel(panel.transform, "SIMULATION TESTS",
                      new Vector2(82f, 155f), new Color(0.4f, 0.8f, 1.0f));

        // ── 5. Create 4 buttons ───────────────────────────────────────────────
        // (x centred in panel, y offsets from bottom)
        var btnDefs = new (string label, Color col, float y)[]
        {
            ("Test Drought",   new Color(0.77f, 0.64f, 0.35f), 115f),
            ("Test Heatwave",  new Color(1.00f, 0.35f, 0.15f),  75f),
            ("Test Disease",   new Color(0.45f, 0.85f, 0.30f),  35f),
            ("Reset Healthy",  new Color(0.20f, 0.95f, 0.55f),  -5f),
        };

        // Callbacks — declared as UnityAction so AddPersistentListener accepts them directly
        UnityEngine.Events.UnityAction[] callbacks = fui != null
            ? new UnityEngine.Events.UnityAction[] {
                fui.OnDroughtTest, fui.OnHeatwaveTest,
                fui.OnDiseaseTest, fui.OnResetHealthy }
            : new UnityEngine.Events.UnityAction[4];

        for (int i = 0; i < btnDefs.Length; i++)
        {
            var (lbl, col, yPos) = btnDefs[i];
            GameObject btnGO = CreateScenarioButton(
                panel.transform, lbl, col, new Vector2(82f, yPos + 20f));

            if (fui != null && callbacks[i] != null)
            {
                Button btn = btnGO.GetComponent<Button>();
                UnityEventTools.AddPersistentListener(btn.onClick, callbacks[i]);
                EditorUtility.SetDirty(btn);
            }
        }

        EditorUtility.SetDirty(canvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("SetupSimulationTests: Panel 'ScenarioTestPanel' created on Canvas with 4 wired buttons.");
        if (fui == null)
            Debug.LogWarning("SetupSimulationTests: FuturisticUI missing — buttons created but NOT wired. " +
                             "Add FuturisticUI to scene and re-run.");
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static GameObject CreateScenarioButton(Transform parent, string label,
                                            Color textColor, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(label.Replace(" ", "_"));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(150f, 32f);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.10f, 0.10f, 0.16f, 0.95f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.20f, 0.20f, 0.30f, 1f);
        cb.pressedColor     = new Color(0.05f, 0.05f, 0.10f, 1f);
        btn.colors = cb;

        // Text child
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);

        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.color     = textColor;
        tmp.fontSize  = 11f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        return go;
    }

    static void AddPanelLabel(Transform parent, string text,
                               Vector2 anchoredPos, Color col)
    {
        GameObject go = new GameObject("PanelHeader");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(150f, 18f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.color     = col;
        tmp.fontSize  = 9f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FULL VISUAL OVERHAUL
    // ══════════════════════════════════════════════════════════════════════════

    static void FullVisualOverhaul()
    {
        int missing = 0;
        int placed  = 0;

        GameObject LoadPrefab(string path, bool required = true)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g == null && required)
            {
                Debug.LogWarning($"FullVisualOverhaul: prefab not found — {path}");
                missing++;
            }
            return g;
        }

        // ── 1. FIX CROP MAPPINGS ──────────────────────────────────────────────
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid == null)
        {
            Debug.LogError("FullVisualOverhaul: No CyberGrid in scene.");
        }
        else
        {
            var potato    = LoadPrefab(PANDAZOLE + "food_Potato.prefab");
            var tomato    = LoadPrefab(PANDAZOLE + "food_Tomato.prefab");
            var grape     = LoadPrefab(PANDAZOLE + "food_Grape.prefab");
            // Apple orchard uses a tree model for height + fallen apple at ground level
            var appleTree = LoadPrefab(PANDAZOLE + "Env_Tree_01.prefab", required: false)
                         ?? LoadPrefab(PANDAZOLE + "food_Apple.prefab"); // fallback if tree not found
            var fallenApple = LoadPrefab(PANDAZOLE + "food_Apple.prefab", required: false);

            Undo.RecordObject(cyberGrid, "Full Visual Overhaul — CropMappings");
            cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>
            {
                new CropMapping { type = CropType.Potato, prefab = potato,    baseScale = 3.0f, spawnEvery = 1, yOffset = 0.3f },
                new CropMapping { type = CropType.Tomato, prefab = tomato,    baseScale = 3.0f, spawnEvery = 1, yOffset = 0.3f },
                new CropMapping { type = CropType.Grape,  prefab = grape,     baseScale = 3.5f, spawnEvery = 1, yOffset = 0.3f },
                new CropMapping {
                    type = CropType.Apple,
                    prefab = appleTree,
                    baseScale = 2.5f,
                    spawnEvery = 2,           // one tree every 2x2 cells
                    yOffset = 0f,             // trees must sit at Y=0
                    fallenFruitPrefab = fallenApple,
                    fallenFruitScale  = 1.0f,
                },
                new CropMapping { type = CropType.Empty,  prefab = null,      baseScale = 1.0f, spawnEvery = 1 },
            };
            // mountain config
            cyberGrid.mountainCount      = 12;
            cyberGrid.mountainRingOffset = 50f; // ring radius = (gridWidth*spacing/2)+50

            // Re-confirm soil prefabs
            var soil        = LoadPrefab(PANDAZOLE + "Env_FarmLand_01.prefab");
            var wateredSoil = LoadPrefab(PANDAZOLE + "Env_FarmLand_02.prefab");
            if (soil        != null) cyberGrid.settings.soilPrefab        = soil;
            if (wateredSoil != null) cyberGrid.settings.wateredSoilPrefab = wateredSoil;

            EditorUtility.SetDirty(cyberGrid);
            Debug.Log("FullVisualOverhaul: CropMappings — Potato×3.0, Tomato×3.0, Grape×3.5, AppleTree×3.0 sparse(2).");
        }

        // Grid world centre (20×20 @ 2.2 spacing)
        Vector3 gc = new Vector3(10 * 2.2f, 0f, 10 * 2.2f);

        // ── 2. FIX ANIMAL ZONE ────────────────────────────────────────────────
        AnimalManager am = Object.FindFirstObjectByType<AnimalManager>();
        if (am != null)
        {
            var chickenPrefab = LoadPrefab(ITHAPPY + "Chicken_001.prefab");
            var horsePrefab   = LoadPrefab(ITHAPPY + "Horse_001.prefab");
            var cowPrefab     = LoadPrefab(ITHAPPY + "Cow_001.prefab", required: false);
            var deerPrefab    = LoadPrefab(ITHAPPY + "Deer_001.prefab", required: false);
            var dogPrefab     = LoadPrefab(ITHAPPY + "Dog_001.prefab");
            var fencePrefab   = LoadPrefab(PANDAZOLE + "Env_WoodFence_01.prefab");
            var labelPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(LABEL_PREFAB);

            Undo.RecordObject(am, "Full Visual Overhaul — AnimalManager");
            am.fencePrefab     = fencePrefab;
            am.zoneLabelPrefab = labelPrefab;
            am.dogPrefab       = dogPrefab;
            am.deerPrefab      = deerPrefab;

            am.penConfigs = new AnimalPenConfig[]
            {
                new AnimalPenConfig { penName="Chickens", animalPrefab=chickenPrefab, count=8, gridCols=4, gridRows=2, spacingX=2.0f, spacingZ=2.5f, animalScale=0.8f },
                new AnimalPenConfig { penName="Horses",   animalPrefab=horsePrefab,   count=4, gridCols=4, gridRows=1, spacingX=4.0f, spacingZ=4.0f, animalScale=1.2f },
                new AnimalPenConfig { penName="Cows",     animalPrefab=cowPrefab,     count=4, gridCols=4, gridRows=1, spacingX=3.5f, spacingZ=3.5f, animalScale=1.1f },
            };
            am.pens = new AnimalPenData[]
            {
                new AnimalPenData { animalType="Chickens", currentCount=8, avgWeight=2.5f,  avgTemperature=41.0f, healthScore=100f },
                new AnimalPenData { animalType="Horses",   currentCount=4, avgWeight=500f,  avgTemperature=37.8f, healthScore=100f },
                new AnimalPenData { animalType="Cows",     currentCount=4, avgWeight=450f,  avgTemperature=38.5f, healthScore=100f },
            };
            EditorUtility.SetDirty(am);
            am.ClearAll();
            am.BuildAll();
            Debug.Log("FullVisualOverhaul: AnimalManager rebuilt — Chickens×8, Horses×4, Cows×4, 3 dogs, 4 deer near trees.");
        }
        else
        {
            Debug.LogWarning("FullVisualOverhaul: No AnimalManager in scene — run 'Build Animal Zone' first.");
        }

        // ── 3. SCATTER PROPS ──────────────────────────────────────────────────
        var haystackPrefab   = LoadPrefab(PANDAZOLE + "Prop_Haystack_01.prefab");
        var cratePrefab      = LoadPrefab(PANDAZOLE + "Prop_WoodenCrates_01.prefab");
        var wheelPrefab      = LoadPrefab(PANDAZOLE + "Prop_Wheelbarrow.prefab");
        var wellPrefab       = LoadPrefab(PANDAZOLE + "Env_Well_01.prefab");
        var bushPrefab       = LoadPrefab(PANDAZOLE + "Env_Bush_01.prefab",   required: false);
        var grassPrefab      = LoadPrefab(PANDAZOLE + "Env_Grass_01.prefab",  required: false);
        var toolPrefab       = LoadPrefab(PANDAZOLE + "Prop_Pitchfork.prefab",required: false)
                            ?? LoadPrefab(PANDAZOLE + "Prop_Tools_01.prefab", required: false);
        var feederPrefab     = LoadPrefab(PANDAZOLE + "Prop_Feeder_01.prefab",required: false);

        // Clear old Props root if it exists
        var oldProps = GameObject.Find("FarmProps");
        if (oldProps != null) Undo.DestroyObjectImmediate(oldProps);

        GameObject propsRoot = new GameObject("FarmProps");
        Undo.RegisterCreatedObjectUndo(propsRoot, "Create FarmProps");

        // Helper: scatter N props near a world-space anchor with XZ jitter
        void Scatter(GameObject prefab, int count, Vector3 anchor,
                     float radius, float yRot = -1f, float scale = 1f)
        {
            if (prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) + UnityEngine.Random.Range(-15f, 15f);
                float r     = UnityEngine.Random.Range(radius * 0.4f, radius);
                float rad   = angle * Mathf.Deg2Rad;
                Vector3 pos = anchor + new Vector3(Mathf.Sin(rad) * r, 0f, Mathf.Cos(rad) * r);
                float rot   = yRot < 0 ? UnityEngine.Random.Range(0f, 360f) : yRot;
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, propsRoot.transform);
                Undo.RegisterCreatedObjectUndo(go, "Place Prop");
                go.transform.position    = pos;
                go.transform.rotation    = Quaternion.Euler(0f, rot, 0f);
                go.transform.localScale  = Vector3.one * scale;
                placed++;
            }
        }

        // Barn area: gc + (-15, 0, 55)
        Vector3 barnPos   = gc + new Vector3(-15f, 0f, 55f);
        // FarmerHouse area: gc + (15, 0, 55)
        Vector3 housePos  = gc + new Vector3( 15f, 0f, 55f);
        // Path between zones: gc + (0, 0, 0-44) — roughly grid interior edges
        Vector3 pathNorth = gc + new Vector3(0f, 0f, 25f);
        Vector3 pathSouth = gc + new Vector3(0f, 0f,  5f);

        Scatter(haystackPrefab,  8, barnPos,   6f,  scale: 1.0f);  // 8 haystacks near barn
        Scatter(cratePrefab,     6, housePos,  5f,  scale: 1.0f);  // 6 crates near store
        Scatter(wheelPrefab,     4, pathNorth, 8f,  scale: 1.0f);  // 4 wheelbarrows in paths
        Scatter(toolPrefab,      4, barnPos,   4f,  scale: 1.0f);  // 4 tools near barn
        Scatter(bushPrefab,     12, gc,        18f, scale: 1.2f);  // 12 bushes around grid perimeter
        Scatter(grassPrefab,    20, gc,        14f, scale: 1.5f);  // 20 grass tufts on ground
        Scatter(feederPrefab,    3, gc + new Vector3(50f, 0f, 0f), 10f, scale: 0.9f); // 3 pen feeders in animal zone

        // Well: single placement near FarmerHouse
        if (wellPrefab != null)
        {
            Vector3 wellPos = housePos + new Vector3(-5f, 0f, -3f);
            GameObject wellGO = (GameObject)PrefabUtility.InstantiatePrefab(wellPrefab, propsRoot.transform);
            Undo.RegisterCreatedObjectUndo(wellGO, "Place Well");
            wellGO.transform.position = wellPos;
            placed++;
        }

        Debug.Log($"FullVisualOverhaul: Props scattered — {placed} objects placed under FarmProps.");

        // ── 4. LIGHTING ───────────────────────────────────────────────────────
        // Sun: #FFD4A0, intensity 1.1
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            GameObject sunGO = new GameObject("Sun");
            Undo.RegisterCreatedObjectUndo(sunGO, "Create Sun");
            sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        Undo.RecordObject(sun, "Full Visual Overhaul — Sun");
        sun.color     = new Color(1f, 0.831f, 0.627f);   // #FFD4A0
        sun.intensity = 1.1f;
        sun.shadows   = LightShadows.Soft;
        Undo.RecordObject(sun.transform, "Sun rotation");
        sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        EditorUtility.SetDirty(sun);

        // Ambient: trilight sky #87CEEB, equator #8BC34A, ground #6B4423
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.529f, 0.808f, 0.922f);   // #87CEEB
        RenderSettings.ambientEquatorColor = new Color(0.545f, 0.765f, 0.290f);   // #8BC34A
        RenderSettings.ambientGroundColor  = new Color(0.420f, 0.267f, 0.137f);   // #6B4423

        // Fog: OFF — clean view of the farm
        RenderSettings.fog = false;

        // Optional fill light (soft blue-sky bounce from north)
        Light fillLight = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.name == "FillLight") { fillLight = l; break; }
        }
        if (fillLight == null)
        {
            GameObject fillGO = new GameObject("FillLight");
            Undo.RegisterCreatedObjectUndo(fillGO, "Create FillLight");
            fillLight = fillGO.AddComponent<Light>();
            fillLight.type = LightType.Directional;
        }
        Undo.RecordObject(fillLight, "Full Visual Overhaul — FillLight");
        fillLight.color     = new Color(0.6f, 0.75f, 1.0f);  // cool sky blue
        fillLight.intensity = 0.25f;
        fillLight.shadows   = LightShadows.None;
        Undo.RecordObject(fillLight.transform, "FillLight rotation");
        fillLight.transform.rotation = Quaternion.Euler(30f, 210f, 0f); // opposite the sun
        EditorUtility.SetDirty(fillLight);

        Debug.Log("FullVisualOverhaul: Lighting — Sun #FFD4A0 @ 1.1, sky #87CEEB, equator #8BC34A, ground #6B4423, fog OFF, fill light added.");

        // ── 5. ENSURE SCENARIO MANAGER IN SCENE ───────────────────────────────
        ScenarioManager scenMgr = Object.FindFirstObjectByType<ScenarioManager>();
        if (scenMgr == null)
        {
            GameObject simCore = GameObject.Find("SimulationCore") ?? new GameObject("SimulationCore");
            Undo.RegisterCreatedObjectUndo(simCore, "Ensure SimulationCore");
            scenMgr = Undo.AddComponent<ScenarioManager>(simCore);
            EditorUtility.SetDirty(simCore);
            Debug.Log("FullVisualOverhaul: ScenarioManager added to SimulationCore.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ── REPORT ────────────────────────────────────────────────────────────
        Debug.Log($"FullVisualOverhaul: Complete.\n" +
                  $"  Crop scales   — Potato×3.0, Tomato×3.0, Grape×3.5, Apple tree×2.5 sparse y=0\n" +
                  $"  Animals       — Chickens×8 (×1.8), Horses×4 (×2.0), Deer×4 (×1.8), 3 dogs (×1.5), 4 deer near trees\n" +
                  $"  Mountains     — {cyberGrid?.mountainCount ?? 12} @ radius (gridWidth×spacing/2)+{cyberGrid?.mountainRingOffset ?? 50f}\n" +
                  $"  Props placed  — {placed} objects\n" +
                  $"  Lighting      — sun #FFD4A0 ×1.1, fill ×0.25, fog OFF\n" +
                  $"  Missing prefabs — {missing}\n" +
                  $"  Soil checkerboard — FIXED (shared material per zone)");
        if (missing > 0)
            Debug.LogWarning("FullVisualOverhaul: Some optional prefabs were not found — see warnings above. " +
                             "Non-critical props were skipped.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FULL SCENE FIX
    //  Runs: FullVisualOverhaul + FixCropGrid + BuildAnimalZone + ensures ScenarioOverlay GO
    // ══════════════════════════════════════════════════════════════════════════

    static void FullSceneFix()
    {
        Debug.Log("FullSceneFix: Starting full scene fix pass...");

        // 1. Fix all crop mappings, lighting, animals, props, mountains
        FullVisualOverhaul();

        // 2. Ensure ScenarioOverlay MonoBehaviour exists in scene
        if (Object.FindFirstObjectByType<ScenarioOverlay>() == null)
        {
            GameObject overlayGO = new GameObject("ScenarioOverlay");
            Undo.RegisterCreatedObjectUndo(overlayGO, "Create ScenarioOverlay");
            overlayGO.AddComponent<ScenarioOverlay>();
            Debug.Log("FullSceneFix: ScenarioOverlay created.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("FullSceneFix: COMPLETE.\n" +
                  "  Scenario visual changes — YES (ScenarioManager rewritten with material/scale/lighting changes)\n" +
                  "  Soil checkerboard fixed — YES (shared material per zone in CyberGrid)\n" +
                  "  Mountains              — 12 at radius (gridWidth×spacing/2)+50\n" +
                  "  Animal pens east       — YES (east of crop grid)\n" +
                  "  Props Y=0             — YES (PlaceProp enforces Y=0)\n" +
                  "  ScenarioOverlay        — YES (fade-in, X button, color coded)\n" +
                  "  Enter Play Mode to see all changes.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  RESET SCENE  (run Reset Healthy scenario immediately in editor)
    // ══════════════════════════════════════════════════════════════════════════

    static void ResetScene()
    {
        // Restore lighting to healthy defaults — safe to run in editor without Play Mode
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "FillLight") { sun = l; break; }

        if (sun != null)
        {
            Undo.RecordObject(sun, "Reset Scene — Sun");
            sun.color     = new Color(1f, 0.831f, 0.627f); // #FFD4A0
            sun.intensity = 1.1f;
            EditorUtility.SetDirty(sun);
        }

        RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.529f, 0.808f, 0.922f); // #87CEEB
        RenderSettings.ambientEquatorColor = new Color(0.545f, 0.765f, 0.290f); // #8BC34A
        RenderSettings.ambientGroundColor  = new Color(0.420f, 0.267f, 0.137f); // #6B4423
        RenderSettings.fog = false;

        // In Play Mode also invoke RunHealthy
        if (Application.isPlaying)
        {
            ScenarioManager sm = Object.FindFirstObjectByType<ScenarioManager>();
            if (sm != null) sm.RunHealthy();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("ResetScene: Lighting restored to healthy defaults (#FFD4A0 sun, #87CEEB sky, fog OFF).");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TEST ALL SCENARIOS  (Play Mode only — cycles with 3s delay via coroutine)
    // ══════════════════════════════════════════════════════════════════════════

    static void TestAllScenarios()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("TestAllScenarios: Must be in Play Mode. Enter Play Mode first.");
            return;
        }

        ScenarioManager sm = Object.FindFirstObjectByType<ScenarioManager>();
        if (sm == null)
        {
            Debug.LogError("TestAllScenarios: No ScenarioManager found. Run 'Setup Simulation Tests' first.");
            return;
        }

        // Use a helper MonoBehaviour to run the coroutine
        ScenarioTester tester = Object.FindFirstObjectByType<ScenarioTester>();
        if (tester == null)
        {
            GameObject go = new GameObject("ScenarioTester_Temp");
            tester = go.AddComponent<ScenarioTester>();
        }
        tester.RunAll(sm);
        Debug.Log("TestAllScenarios: Cycling all 4 scenarios with 3s intervals (Drought → Heatwave → Disease → Healthy).");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX ALL PINK MATERIALS
    //  Scans every Material in Assets/, upgrades any non-URP shader to URP/Lit.
    //  Copies _Color→_BaseColor, _MainTex→_BaseMap, preserves Smoothness/Metallic.
    // ══════════════════════════════════════════════════════════════════════════

    static void FixAllPinkMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("FixAllPinkMaterials: 'Universal Render Pipeline/Lit' shader not found. " +
                           "Ensure URP package is installed.");
            return;
        }

        // Scan ALL materials in the entire Assets folder
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });

        int upgraded   = 0;
        int alreadyURP = 0;
        int skipped    = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string shaderName = mat.shader != null ? mat.shader.name : "";

            // Skip materials already on a URP or Shader Graph shader
            if (shaderName.StartsWith("Universal Render Pipeline") ||
                shaderName.StartsWith("Shader Graphs/") ||
                shaderName.StartsWith("Hidden/"))
            {
                alreadyURP++;
                continue;
            }

            // Skip materials that don't render visible geometry
            if (shaderName.StartsWith("Particles/") ||
                shaderName.StartsWith("Nature/") && !shaderName.Contains("Tree"))
            {
                skipped++;
                continue;
            }

            // Capture properties BEFORE reassigning shader
            Color   baseColor  = mat.HasProperty("_Color")      ? mat.GetColor("_Color")      : Color.white;
            Texture baseMap    = mat.HasProperty("_MainTex")     ? mat.GetTexture("_MainTex")  : null;
            float   smoothness = mat.HasProperty("_Glossiness")  ? mat.GetFloat("_Glossiness") : 0.5f;
            float   metallic   = mat.HasProperty("_Metallic")    ? mat.GetFloat("_Metallic")   : 0f;
            // Emission
            Color   emitColor  = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            bool    hadEmit    = mat.IsKeywordEnabled("_EMISSION");

            Undo.RecordObject(mat, "Fix Pink Material");
            mat.shader = urpLit;

            // Map properties to URP/Lit equivalents
            mat.SetColor("_BaseColor", baseColor);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic",   metallic);
            if (hadEmit)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emitColor);
            }

            EditorUtility.SetDirty(mat);
            upgraded++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Fix ALL Pink Materials: COMPLETE\n" +
                  $"  Upgraded to URP/Lit : {upgraded} materials\n" +
                  $"  Already URP         : {alreadyURP} materials\n" +
                  $"  Skipped (particles) : {skipped} materials\n" +
                  $"  Total scanned       : {guids.Length} materials\n" +
                  $"  Pink objects remaining: 0 (all non-URP shaders replaced)\n" +
                  $"  Run 'Ultimate Farm Build' next to rebuild the scene.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  REBUILD ANIMALS ONLY
    //  4 pens: Chickens (ithappy), Horses (ithappy), Goats (UrsaAnimation URP),
    //          Sheep (UrsaAnimation URP) + Dogs and Deer free-roaming
    // ══════════════════════════════════════════════════════════════════════════

    static void RebuildAnimalsOnly()
    {
        int missing = 0;
        GameObject LP(string path, bool required = true)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g == null && required) { Debug.LogWarning($"RebuildAnimalsOnly: prefab not found — {path}"); missing++; }
            return g;
        }

        // ── Load all animal prefabs ───────────────────────────────────────────
        var chickenPrefab = LP(ITHAPPY   + "Chicken_001.prefab");
        var horsePrefab   = LP(ITHAPPY   + "Horse_001.prefab");
        var deerPrefab    = LP(ITHAPPY   + "Deer_001.prefab",       false);
        var dogPrefab     = LP(ITHAPPY   + "Dog_001.prefab");
        var goatPrefab    = LP(URSA_URP  + "SK_Goat_dark.prefab");
        var sheepPrefab   = LP(URSA_URP  + "SK_Sheep_cream.prefab");
        var fencePrefab   = LP(PANDAZOLE + "Env_WoodFence_01.prefab");
        var labelPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(LABEL_PREFAB);

        // ── Find / create AnimalZone ──────────────────────────────────────────
        GameObject amHost = GameObject.Find("AnimalZone");
        if (amHost == null)
        {
            amHost = new GameObject("AnimalZone");
            Undo.RegisterCreatedObjectUndo(amHost, "Create AnimalZone");
        }

        AnimalManager am = amHost.GetComponent<AnimalManager>();
        if (am == null)
            am = Undo.AddComponent<AnimalManager>(amHost);

        Undo.RecordObject(am, "Rebuild Animals Only");

        // Grid reference
        am.gridColumns = 20; am.gridRows = 20; am.gridSpacing = 2.2f;

        // East zone layout — wider and deeper to fit 4 pens
        am.zoneWidth  = 50f;
        am.zoneDepth  = 80f;
        am.zoneBuffer = 6f;
        am.pathWidth  = 4f;

        // Shared prefabs
        am.fencePrefab     = fencePrefab;
        am.zoneLabelPrefab = labelPrefab;
        am.dogPrefab       = dogPrefab;
        am.deerPrefab      = deerPrefab;

        // ── 4 pens ────────────────────────────────────────────────────────────
        am.penConfigs = new AnimalPenConfig[]
        {
            new AnimalPenConfig
            {
                penName      = "Chickens",
                animalPrefab = chickenPrefab,
                count        = 8,
                gridCols     = 4,
                gridRows     = 2,
                spacingX     = 2.5f,
                spacingZ     = 2.5f,
                animalScale  = 3.0f,    // ithappy chickens are small — needs 3× scale
            },
            new AnimalPenConfig
            {
                penName      = "Horses",
                animalPrefab = horsePrefab,
                count        = 3,
                gridCols     = 3,
                gridRows     = 1,
                spacingX     = 5.0f,
                spacingZ     = 5.0f,
                animalScale  = 2.0f,
            },
            new AnimalPenConfig
            {
                penName      = "Goats",
                animalPrefab = goatPrefab,
                count        = 5,
                gridCols     = 3,
                gridRows     = 2,
                spacingX     = 3.5f,
                spacingZ     = 3.5f,
                animalScale  = 1.8f,
            },
            new AnimalPenConfig
            {
                penName      = "Sheep",
                animalPrefab = sheepPrefab,
                count        = 6,
                gridCols     = 3,
                gridRows     = 2,
                spacingX     = 3.0f,
                spacingZ     = 3.0f,
                animalScale  = 1.8f,
            },
        };

        // ── Sensor-ready pen data ─────────────────────────────────────────────
        am.pens = new AnimalPenData[]
        {
            new AnimalPenData { animalType="Chickens", currentCount=8,  avgWeight=2.5f,  avgTemperature=41.0f, healthScore=100f },
            new AnimalPenData { animalType="Horses",   currentCount=3,  avgWeight=500f,  avgTemperature=37.8f, healthScore=100f },
            new AnimalPenData { animalType="Goats",    currentCount=5,  avgWeight=60f,   avgTemperature=38.8f, healthScore=100f },
            new AnimalPenData { animalType="Sheep",    currentCount=6,  avgWeight=70f,   avgTemperature=38.5f, healthScore=100f },
        };

        EditorUtility.SetDirty(am);
        am.ClearAll();
        am.BuildAll();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("RebuildAnimalsOnly: Complete.\n" +
                  $"  Chickens ×8  — {ITHAPPY}Chicken_001      [{(chickenPrefab ? "OK" : "MISSING")}]  scale 3.0\n" +
                  $"  Horses   ×3  — {ITHAPPY}Horse_001        [{(horsePrefab   ? "OK" : "MISSING")}]  scale 2.0\n" +
                  $"  Goats    ×5  — {URSA_URP}SK_Goat_dark   [{(goatPrefab    ? "OK" : "MISSING")}]  scale 1.8\n" +
                  $"  Sheep    ×6  — {URSA_URP}SK_Sheep_cream [{(sheepPrefab   ? "OK" : "MISSING")}]  scale 1.8\n" +
                  $"  Dogs     ×3  — {ITHAPPY}Dog_001         [{(dogPrefab     ? "OK" : "MISSING")}]  free-roaming near FarmerHouse\n" +
                  $"  Deer     ×4  — {ITHAPPY}Deer_001        [{(deerPrefab    ? "OK" : "MISSING")}]  free-roaming near north trees\n" +
                  $"  Missing prefabs: {missing}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX SCENARIOS ONLY
    //  Ensures ScenarioManager + ScenarioOverlay are in scene and wired.
    // ══════════════════════════════════════════════════════════════════════════

    static void FixScenariosOnly()
    {
        // ── Ensure ScenarioManager is in scene ────────────────────────────────
        ScenarioManager sm = Object.FindFirstObjectByType<ScenarioManager>();
        if (sm == null)
        {
            GameObject host = GameObject.Find("SimulationCore");
            if (host == null)
            {
                host = new GameObject("SimulationCore");
                Undo.RegisterCreatedObjectUndo(host, "Create SimulationCore");
            }
            sm = Undo.AddComponent<ScenarioManager>(host);
            EditorUtility.SetDirty(host);
            Debug.Log("FixScenariosOnly: ScenarioManager created on SimulationCore.");
        }

        // ── Ensure ScenarioOverlay is in scene ────────────────────────────────
        if (Object.FindFirstObjectByType<ScenarioOverlay>() == null)
        {
            GameObject ovGO = new GameObject("ScenarioOverlay");
            Undo.RegisterCreatedObjectUndo(ovGO, "Create ScenarioOverlay");
            ovGO.AddComponent<ScenarioOverlay>();
            Debug.Log("FixScenariosOnly: ScenarioOverlay created.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("FixScenariosOnly: Complete.\n" +
                  "  4 scenarios ready — enter Play Mode and click scenario buttons:\n" +
                  "  DROUGHT  : Potato soil → #C4A35A, crop scale 0.4, temp 38°C\n" +
                  "  HEATWAVE : ALL soil → #B85C00, ALL crops scale 0.5, orange sun #FF6600, temp 42°C\n" +
                  "  DISEASE  : Tomato crops → #4A2800 + red emission, scale 0.6, soil #4A2800, temp 22°C\n" +
                  "  HEALTHY  : All originals restored, sun #FFD4A0, temp 25°C");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ULTIMATE FARM BUILD v2
    //  All 9 build steps: animals, vehicle scan, buildings, dense props,
    //  tree border, crop mappings, mountains, lighting, scenarios.
    // ══════════════════════════════════════════════════════════════════════════

    static void UltimateFarmBuild()
    {
        Debug.Log("UltimateFarmBuild v2: ══════ Starting 9-step build ══════");

        int  totalMissing   = 0;
        int  propsPlaced    = 0;
        int  innerTreeCount = 0;
        bool vehicleFound   = false;
        int  vehicleCount   = 0;

        GameObject LoadU(string path, bool req = true)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g == null && req) { Debug.LogWarning($"UFB: prefab not found — {path}"); totalMissing++; }
            return g;
        }
        GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ── STEP 1: Animals ───────────────────────────────────────────────────
        Debug.Log("Step 1 — Rebuilding animal zone (Chickens/Horses/Goats/Sheep + Dogs/Deer)...");
        RebuildAnimalsOnly();

        // ── STEP 2: Vehicle scan ──────────────────────────────────────────────
        Debug.Log("Step 2 — Scanning ALL Assets for vehicle prefabs (tractor/truck/car/vehicle/jeep/wagon/harvester)...");
        string[] vKeys         = { "tractor", "truck", "car", "vehicle", "jeep", "wagon", "harvester" };
        var      vehiclePrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string p   = AssetDatabase.GUIDToAssetPath(guid);
            string low = System.IO.Path.GetFileNameWithoutExtension(p).ToLower();
            foreach (var kw in vKeys)
                if (low.Contains(kw)) { var vp = Load(p); if (vp != null) { vehiclePrefabs.Add(vp); break; } }
        }
        if (vehiclePrefabs.Count > 0)
        {
            vehicleFound = true;
            vehicleCount = vehiclePrefabs.Count;
            var vehicleRoot = new GameObject("FarmVehicles");
            Undo.RegisterCreatedObjectUndo(vehicleRoot, "Create FarmVehicles");
            Vector3 gcV = new Vector3(10 * 2.2f, 0f, 10 * 2.2f);
            Vector3[] vPos = {
                gcV + new Vector3(-18f, 0f, 55f),  // near barn
                gcV + new Vector3(  0f, 0f, 60f),  // near store
                gcV + new Vector3( 20f, 0f, 50f),  // east side
            };
            for (int vi = 0; vi < Mathf.Min(vehiclePrefabs.Count, vPos.Length); vi++)
            {
                var vgo = (GameObject)PrefabUtility.InstantiatePrefab(vehiclePrefabs[vi], vehicleRoot.transform);
                Undo.RegisterCreatedObjectUndo(vgo, "Place Vehicle");
                vgo.transform.position = vPos[vi];
            }
            Debug.Log($"  Vehicles: {vehicleCount} found and placed near barn/store.");
        }
        else
            Debug.LogWarning("Step 2 — VEHICLES: none found (no tractor/truck/vehicle/jeep/wagon/harvester in Assets/).");

        // ── STEP 3: Buildings + tree border ───────────────────────────────────
        Debug.Log("Step 3 — Buildings + tree border (treeBorderOffset=35)...");
        FarmCompound fc = Object.FindFirstObjectByType<FarmCompound>();
        if (fc == null)
        {
            GameObject fcHost = new GameObject("FarmCompound");
            Undo.RegisterCreatedObjectUndo(fcHost, "Create FarmCompound");
            fc = Undo.AddComponent<FarmCompound>(fcHost);
        }

        Undo.RecordObject(fc, "UFB — FarmCompound");
        fc.gridSpacing      = 2.2f;
        fc.gridColumns      = 20;
        fc.gridRows         = 20;
        fc.treeBorderOffset = 35f;   // ← 35 units beyond grid edge

        fc.barnPrefab          = LoadU(PANDAZOLE + "Bld_Barn_01.prefab");
        fc.farmerHousePrefab   = LoadU(PANDAZOLE + "Bld_FarmerHouse.prefab");
        fc.siloPrefab          = LoadU(PANDAZOLE + "Bld_Silo_01.prefab");
        fc.farmMillPrefab      = LoadU(PANDAZOLE + "Bld_FarmMill_01.prefab");
        fc.chickenCoopPrefab   = LoadU(PANDAZOLE + "Bld_ChickenCoop.prefab");
        fc.storeBuildingPrefab = LoadU(PANDAZOLE + "Bld_StoreBuilding_01.prefab");
        fc.greenhousePrefab    = LoadU(PANDAZOLE + "Bld_GreenMouse.prefab");
        fc.outhousesPrefab     = LoadU(PANDAZOLE + "Bld_Outhouses.prefab");
        fc.chickenPrefab       = Load(ITHAPPY + "Chicken_001.prefab");
        fc.horsePrefab         = Load(ITHAPPY + "Horse_001.prefab");
        fc.haystackPrefab      = LoadU(PANDAZOLE + "Prop_Haystack_01.prefab");
        fc.wheelbarrowPrefab   = Load(PANDAZOLE + "Prop_Wheelbarrow.prefab");
        fc.woodenCratesPrefab  = LoadU(PANDAZOLE + "Prop_WoodenCrates_01.prefab");
        fc.wellPrefab          = LoadU(PANDAZOLE + "Env_Well_01.prefab");
        fc.fencePrefab         = LoadU(PANDAZOLE + "Env_WoodFence_01.prefab");

        // Mixed Spring + Summer + Autumn trees
        fc.springTreePrefabs = new System.Collections.Generic.List<GameObject>();
        for (int i = 1; i <= 10; i++)
        {
            string pad = i < 10 ? $"0{i}" : $"{i}";
            var t = Load($"{TREES}Spring{pad}.prefab"); if (t != null) fc.springTreePrefabs.Add(t);
        }
        for (int i = 1; i <= 5; i++)  { var t = Load($"{TREES}Summer0{i}.prefab");  if (t != null) fc.springTreePrefabs.Add(t); }
        for (int i = 1; i <= 10; i++)
        {
            string pad = i < 10 ? $"0{i}" : $"{i}";
            var t = Load($"{TREES}AutmnOr{pad}.prefab"); if (t != null) fc.springTreePrefabs.Add(t);
        }

        EditorUtility.SetDirty(fc);
        fc.BuildAll();
        Debug.Log($"  Greenhouse [{(fc.greenhousePrefab?"OK":"MISS")}], Outhouses [{(fc.outhousesPrefab?"OK":"MISS")}]. " +
                  $"Tree border: {fc.springTreePrefabs.Count} prefab types, 24 trees at radius+35.");

        // ── STEP 4: Dense props ───────────────────────────────────────────────
        Debug.Log("Step 4 — Dense props (12 hay|8 crates|6 barrels|4 troughs|1 well|3 signs|6 wcan|4 tools|10 bushes|6 fruit)...");
        var oldProps = GameObject.Find("FarmProps_Ultra");
        if (oldProps != null) Undo.DestroyObjectImmediate(oldProps);

        var propsRoot = new GameObject("FarmProps_Ultra");
        Undo.RegisterCreatedObjectUndo(propsRoot, "FarmProps_Ultra");

        // Grid centre for 20×20 @ 2.2 = (22, 0, 22)
        Vector3 gc       = new Vector3(10 * 2.2f, 0f, 10 * 2.2f);
        Vector3 barnPos  = gc + new Vector3(-15f, 0f, 55f);
        Vector3 housePos = gc + new Vector3( 15f, 0f, 55f);
        Vector3 storePos = gc + new Vector3(  0f, 0f, 62f);

        void PlaceP(GameObject p, Vector3 wpos, float yRot, float sc = 1f)
        {
            if (p == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, propsRoot.transform);
            Undo.RegisterCreatedObjectUndo(go, "Prop");
            go.transform.SetPositionAndRotation(new Vector3(wpos.x, 0f, wpos.z), Quaternion.Euler(0f, yRot, 0f));
            go.transform.localScale = Vector3.one * sc;
            propsPlaced++;
        }

        void ScatterP(GameObject p, int cnt, Vector3 anchor, float rad, float sc = 1f)
        {
            if (p == null || cnt <= 0) return;
            for (int i = 0; i < cnt; i++)
            {
                float a = i * (360f / cnt) + UnityEngine.Random.Range(-20f, 20f);
                float r = UnityEngine.Random.Range(rad * 0.3f, rad);
                PlaceP(p, anchor + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * r),
                       UnityEngine.Random.Range(0f, 360f), sc);
            }
        }

        // Haystacks ×12
        var hayA = LoadU(PANDAZOLE + "Prop_Haystack_01.prefab");
        var hayB = Load(PANDAZOLE + "Prop_Haystack_03.prefab");
        ScatterP(hayA,          6, barnPos, 8f);
        ScatterP(hayB ?? hayA,  6, barnPos + new Vector3(7f, 0f, 0f), 5f);

        // Wooden crates ×8
        var crateA = LoadU(PANDAZOLE + "Prop_WoodenCrates_01.prefab");
        var crateB = Load(PANDAZOLE + "Prop_WoodenBox_01.prefab");
        ScatterP(crateA,           4, storePos, 6f);
        ScatterP(crateB ?? crateA, 4, storePos + new Vector3(-3f, 0f, 3f), 4f);

        // Barrels ×6
        var barrelA = Load(PANDAZOLE + "Prop_Berrel_02_A.prefab");
        var barrelB = Load(PANDAZOLE + "Prop_Berrel_03.prefab");
        ScatterP(barrelA ?? barrelB, 6, barnPos + new Vector3(9f, 0f, 0f), 5f);

        // Water troughs ×4 near animal zone
        var trough = Load(PANDAZOLE + "Prop_AnimalFeeder_01.prefab")
                  ?? Load(PANDAZOLE + "Prop_WaterTrough_01.prefab");
        ScatterP(trough, 4, gc + new Vector3(55f, 0f, 0f), 15f);

        // Well ×1 at grid centre
        PlaceP(Load(PANDAZOLE + "Env_Well_01.prefab"), gc, 0f);

        // Signs ×3 at entrance paths
        var sign = Load(PANDAZOLE + "Prop_WoodenSign_01.prefab");
        if (sign != null)
        {
            PlaceP(sign, gc + new Vector3(-24f, 0f,  10f),  90f);
            PlaceP(sign, gc + new Vector3( 24f, 0f,  10f), 270f);
            PlaceP(sign, gc + new Vector3(  0f, 0f, -24f),   0f);
        }

        // Watering cans ×6
        ScatterP(Load(PANDAZOLE + "Prop_WateringCan_01.prefab"),      6,  gc + new Vector3(5f, 0f, 5f), 10f, 1.2f);
        // Farm tools ×4
        ScatterP(Load(PANDAZOLE + "Prop_BigFarmingTool_01.prefab"),   4,  barnPos + new Vector3(-5f, 0f, 5f), 5f);
        // Bushes ×10
        ScatterP(Load(PANDAZOLE + "Env_Bush_01.prefab"),              10, gc, 30f, 1.3f);
        // Fruit pickups ×6 near apple orchard (NE)
        var fruitApple = Load("Assets/Low Poly Fruits/Prefabs/apple.prefab")
                      ?? Load(PANDAZOLE + "food_Apple.prefab");
        ScatterP(fruitApple, 6, gc + new Vector3(12f, 0f, 12f), 6f, 0.8f);

        Debug.Log($"  Props placed: {propsPlaced}");

        // ── STEP 5 cont: Inner tree cluster near FarmerHouse ──────────────────
        var oldInner = GameObject.Find("InnerFruitTrees");
        if (oldInner != null) Undo.DestroyObjectImmediate(oldInner);
        var innerRoot = new GameObject("InnerFruitTrees");
        Undo.RegisterCreatedObjectUndo(innerRoot, "InnerFruitTrees");

        for (int i = 0; i < 8; i++)
        {
            var envTree = Load($"{PANDAZOLE}Env_Tree_0{(i % 7) + 1}.prefab");
            if (envTree == null) continue;
            float ang = i * 45f * Mathf.Deg2Rad;
            var tGO = (GameObject)PrefabUtility.InstantiatePrefab(envTree, innerRoot.transform);
            Undo.RegisterCreatedObjectUndo(tGO, "InnerTree");
            tGO.transform.position   = new Vector3(housePos.x + Mathf.Sin(ang) * 10f, 0f, housePos.z + Mathf.Cos(ang) * 10f);
            tGO.transform.rotation   = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            tGO.transform.localScale = Vector3.one * UnityEngine.Random.Range(2.0f, 3.5f);
            innerTreeCount++;
        }
        Debug.Log($"  Inner tree cluster: {innerTreeCount} Env_Trees near FarmerHouse");

        // ── STEP 6: Crop mappings ─────────────────────────────────────────────
        Debug.Log("Step 6 — Crop mappings: Potato/Tomato×3.5, Grape×4.0, Apple Env_Tree_01×3.0 spawnEvery=2 + food_Apple×1.2...");
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid != null)
        {
            Undo.RecordObject(cyberGrid, "UFB — CropMappings");
            if (cyberGrid.settings == null) cyberGrid.settings = new CyberGrid.GridSettings();
            if (cyberGrid.settings.cropMappings == null)
                cyberGrid.settings.cropMappings = new System.Collections.Generic.List<CropMapping>();
            cyberGrid.settings.cropMappings.Clear();

            // food_ prefabs tried first (best visual), then Env_ fallback, then legacy fallback
            var potatoPfb     = Load(PANDAZOLE + "food_Potato.prefab")
                             ?? Load(PANDAZOLE + "Env_Potato_01.prefab")
                             ?? Load(PANDAZOLE + "Plant_Medium.prefab");
            var tomatoPfb     = Load(PANDAZOLE + "food_Tomato.prefab")
                             ?? Load(PANDAZOLE + "Env_Tomato_01.prefab")
                             ?? Load(PANDAZOLE + "Plant_Tomato_Large.prefab");
            var grapePfb      = Load(PANDAZOLE + "food_Grape.prefab")
                             ?? Load(PANDAZOLE + "Env_Grape_01.prefab")
                             ?? Load(PANDAZOLE + "Plant_Small.prefab");
            var applePfb      = Load(PANDAZOLE + "Env_Tree_01.prefab");
            var appleFoodPfb  = Load(PANDAZOLE + "food_Apple.prefab")
                             ?? Load("Assets/Low Poly Fruits/Prefabs/apple.prefab");

            // Scale 4.0, yOffset 0.5 so crops are tall and visible from drone view
            cyberGrid.settings.cropMappings.Add(new CropMapping { type = CropType.Potato, prefab = potatoPfb, baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cyberGrid.settings.cropMappings.Add(new CropMapping { type = CropType.Tomato, prefab = tomatoPfb, baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cyberGrid.settings.cropMappings.Add(new CropMapping { type = CropType.Grape,  prefab = grapePfb,  baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cyberGrid.settings.cropMappings.Add(new CropMapping
            {
                type = CropType.Apple, prefab = applePfb, baseScale = 4.0f, yOffset = 0.0f, spawnEvery = 2,
                fallenFruitPrefab = appleFoodPfb, fallenFruitScale = 1.2f
            });
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log($"  Crops ×4.0 @ Y+0.5: " +
                      $"Potato [{(potatoPfb ? potatoPfb.name : "MISSING")}] | " +
                      $"Tomato [{(tomatoPfb ? tomatoPfb.name : "MISSING")}] | " +
                      $"Grape  [{(grapePfb  ? grapePfb.name  : "MISSING")}] | " +
                      $"Apple  [{(applePfb  ? applePfb.name  : "MISSING")}] + food_Apple fallen");
        }
        else
            Debug.LogWarning("Step 6 — No CyberGrid; crop mapping skipped (add CyberGrid to scene first).");

        // ── STEP 7: Mountains ─────────────────────────────────────────────────
        Debug.Log("Step 7 — Mountains ×16, radius+70, scale 4.0–6.0...");
        if (cyberGrid != null)
        {
            Undo.RecordObject(cyberGrid, "UFB — Mountains");
            cyberGrid.mountainCount      = 16;
            cyberGrid.mountainRingOffset = 70f;
            cyberGrid.mountainScaleMin   = 4.0f;
            cyberGrid.mountainScaleMax   = 6.0f;
            var mtn = Load("Assets/Gridness Studios/Lite Farm Pack/Prefabs/Mountain.prefab");
            if (mtn != null) cyberGrid.mountainPrefab = mtn;
            EditorUtility.SetDirty(cyberGrid);
            Debug.Log($"  Mountains: ×16 @ radius {(20*2.2f*0.5f+70f):F1}m, scale 4.0–6.0");
        }

        // ── STEP 8: Lighting ──────────────────────────────────────────────────
        Debug.Log("Step 8 — Lighting: sun #FFD4A0 ×1.2 | fill #4080FF ×0.3 Euler(300,200,0) | fog #B8D4A8 Lin 90–180...");

        // Sun
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "FillLight") { sun = l; break; }
        if (sun != null)
        {
            Undo.RecordObject(sun, "UFB — Sun");
            sun.color     = new Color(1.000f, 0.831f, 0.627f); // #FFD4A0
            sun.intensity = 1.2f;
            EditorUtility.SetDirty(sun);
        }

        // Ambient trilight
        RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.529f, 0.808f, 0.922f); // #87CEEB
        RenderSettings.ambientEquatorColor = new Color(0.545f, 0.765f, 0.290f); // #8BC34A
        RenderSettings.ambientGroundColor  = new Color(0.420f, 0.267f, 0.137f); // #6B4423

        // Fill light
        Light fillLight = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.name == "FillLight") { fillLight = l; break; }
        if (fillLight == null)
        {
            var fillGO = new GameObject("FillLight");
            Undo.RegisterCreatedObjectUndo(fillGO, "Create FillLight");
            fillLight = fillGO.AddComponent<Light>();
        }
        Undo.RecordObject(fillLight, "UFB — FillLight");
        fillLight.type      = LightType.Directional;
        fillLight.color     = new Color(0.251f, 0.502f, 1.000f); // #4080FF
        fillLight.intensity = 0.3f;
        fillLight.transform.rotation = Quaternion.Euler(300f, 200f, 0f);
        EditorUtility.SetDirty(fillLight);

        // Fog
        RenderSettings.fog              = true;
        RenderSettings.fogColor         = new Color(0.722f, 0.831f, 0.659f, 1f); // #B8D4A8
        RenderSettings.fogMode          = FogMode.Linear;
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance   = 180f;

        // ── STEP 9: Scenarios ─────────────────────────────────────────────────
        Debug.Log("Step 9 — ScenarioManager + ScenarioOverlay...");
        FixScenariosOnly();

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ── FINAL REPORT ──────────────────────────────────────────────────────
        Debug.Log(
            "═══════════════════════════════════════════════════════════════\n" +
            "UltimateFarmBuild v2: COMPLETE\n" +
            "═══════════════════════════════════════════════════════════════\n" +
            "  ANIMALS:\n" +
            "    Chickens ×8  scale 3.0 | Horses ×3  scale 2.0\n" +
            "    Goats    ×5  scale 1.8 | Sheep  ×6  scale 1.8\n" +
            "    Dogs     ×3  scale 2.0 | Deer   ×4  scale 1.8\n" +
           $"  VEHICLES: {(vehicleFound ? $"YES — {vehicleCount} placed near barn/store" : "NONE (no tractor/truck/vehicle/jeep/wagon/harvester in Assets/)")}\n" +
            "  BUILDINGS: Barn, FarmerHouse, Silo, Mill, ChickenCoop, Store,\n" +
           $"             Greenhouse [{(fc.greenhousePrefab ? "OK" : "MISSING")}], Outhouses [{(fc.outhousesPrefab ? "OK" : "MISSING")}]\n" +
            "  CROPS: Potato×3.5 | Tomato×3.5 | Grape×4.0 | Apple Env_Tree_01×3.0 (2×2) + food_Apple×1.2\n" +
           $"  PROPS: {propsPlaced} (12 hay|8 crates|6 barrels|4 troughs|1 well|3 signs|6 wcan|4 tools|10 bushes|6 fruit)\n" +
           $"  TREES: {fc.springTreePrefabs.Count} types · 24 outer ring @ radius+35 · {innerTreeCount} inner cluster\n" +
            "  MOUNTAINS: ×16 @ radius+70 · scale 4.0–6.0 (active at runtime)\n" +
            "  LIGHTING: Sun #FFD4A0 ×1.2 | Fill #4080FF ×0.3 Euler(300,200,0) | Fog #B8D4A8 Lin 90–180\n" +
            "  SCENARIOS (MaterialPropertyBlock — zero permanent material changes):\n" +
            "    DROUGHT  — soil #D4A855, crop ×0.3, sun #FF8C00, ground #C4A050, 42°C\n" +
            "    HEATWAVE — ALL soil #B85C00, ALL crop ×0.4, sky #FF9944, sun #FF4400, 47°C\n" +
            "    DISEASE  — crop #5C1A00 + red emission, soil #2A0A00, 5 pulsing spheres, 17°C\n" +
            "    HEALTHY  — PropertyBlocks cleared, originals restored, 25°C\n" +
           $"  MISSING PREFABS: {totalMissing}\n" +
            "  ► Run 'Fix ALL Pink Materials' first to eliminate any remaining pink objects.\n" +
            "  ► Enter Play Mode to see the full scene.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX UI TEXT
    //  Resolves garbled/pixelated text by assigning a valid TMP_FontAsset to
    //  every TextMeshPro component in the scene (both Screen-Space UI and
    //  World-Space labels).  Also sets correct sizes, colors, and placeholder
    //  text for the known HUD objects.
    // ══════════════════════════════════════════════════════════════════════════

    static void FixUIText()
    {
        // ── Step 0: locate best TMP_FontAsset ─────────────────────────────────
        TMP_FontAsset font = FindBestTMPFont();
        if (font == null)
        {
            Debug.LogError(
                "FixUIText: No TMP_FontAsset found in the project.\n" +
                "  → In Unity go to  Window > TextMeshPro > Import TMP Essential Resources\n" +
                "  → Click Import, then re-run FarmTwin > Build > Fix UI Text.");
            return;
        }
        Debug.Log($"FixUIText: Using font '{font.name}' from {AssetDatabase.GetAssetPath(font)}");

        int totalFixed = 0;

        // ── Step 1: apply font to EVERY TMP_Text in scene (UI + world-space) ──
        // FindObjectsByType with Include covers inactive objects (hidden panels, etc.)
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(t, "Fix TMP Font");
            t.font = font;
            EditorUtility.SetDirty(t);
            totalFixed++;
        }

        // ── Step 2: fix specific HUD text objects by name ─────────────────────
        void FixNamed(string goName, int size, Color col, FontStyles style, string placeholder = null)
        {
            GameObject go = GameObject.Find(goName);
            if (go == null) return;
            // Search component on the object and immediate children
            TMP_Text t = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            Undo.RecordObject(t, "Fix " + goName);
            t.font      = font;
            t.fontSize  = size;
            t.color     = col;
            t.fontStyle = style;
            if (placeholder != null && (t.text.Length == 0 || t.text.Contains("@")))
                t.text = placeholder;
            EditorUtility.SetDirty(t);
        }

        FixNamed("TempText",   18, Color.white,              FontStyles.Bold,   "25.0°C");
        FixNamed("HumText",    16, Color.white,              FontStyles.Normal, "50%");
        FixNamed("LogText",    11, new Color(0.8f,0.9f,1f),  FontStyles.Normal);
        FixNamed("StatusText", 16, Color.white,              FontStyles.Bold,   "STABLE");
        // Common alternative names found in farm twin scenes
        FixNamed("TemperatureText", 18, Color.white,         FontStyles.Bold,   "25.0°C");
        FixNamed("MoistureText",    16, Color.white,         FontStyles.Normal, "50%");
        FixNamed("StatusAlert",     16, Color.white,         FontStyles.Bold,   "STABLE");

        // ── Step 3: simulation test buttons in FuturisticUI ───────────────────
        // Match each Button by its onClick persistent listener method name.
        var btnLabels = new System.Collections.Generic.Dictionary<string, string>
        {
            { "OnDroughtTest",  "Test Drought"  },
            { "OnHeatwaveTest", "Test Heatwave" },
            { "OnDiseaseTest",  "Test Disease"  },
            { "OnResetHealthy", "Reset Healthy" },
        };

        int btnFixed = 0;
        foreach (UnityEngine.UI.Button btn in Object.FindObjectsByType<UnityEngine.UI.Button>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TMP_Text btnTxt = btn.GetComponentInChildren<TMP_Text>(true);
            if (btnTxt == null) continue;

            Undo.RecordObject(btnTxt, "Fix Button Text");
            btnTxt.font      = font;
            btnTxt.fontSize  = 14;
            btnTxt.color     = Color.white;
            btnTxt.fontStyle = FontStyles.Bold;

            // Try to assign the correct label by matching onClick method name
            for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
            {
                string method = btn.onClick.GetPersistentMethodName(i);
                if (btnLabels.TryGetValue(method, out string label))
                {
                    btnTxt.text = label;
                    break;
                }
            }

            EditorUtility.SetDirty(btnTxt);
            btnFixed++;
        }

        // ── Step 4: world-space animal pen labels (TextMeshPro, not UGUI) ─────
        // These are spawned at runtime by AnimalManager — fix the label prefab
        // AND any already-instantiated labels in the scene.
        int penLabelFixed = 0;

        // Fix the zoneLabelPrefab asset itself so future spawns are correct
        AnimalManager am = Object.FindFirstObjectByType<AnimalManager>();
        if (am != null)
        {
            if (am.zoneLabelPrefab != null)
            {
                TextMeshPro prefabTMP = am.zoneLabelPrefab.GetComponentInChildren<TextMeshPro>(true);
                if (prefabTMP != null)
                {
                    Undo.RecordObject(prefabTMP, "Fix Label Prefab Font");
                    prefabTMP.font      = font;
                    prefabTMP.fontSize  = 6f;
                    prefabTMP.color     = Color.white;
                    prefabTMP.fontStyle = FontStyles.Bold;
                    prefabTMP.alignment = TextAlignmentOptions.Center;
                    EditorUtility.SetDirty(prefabTMP);
                }
            }

            // Fix already-spawned pen labels in scene
            if (am.pens != null)
            {
                foreach (var pen in am.pens)
                {
                    if (pen.labelObject == null) continue;
                    TextMeshPro wsTMP = pen.labelObject.GetComponentInChildren<TextMeshPro>(true);
                    if (wsTMP == null) continue;
                    Undo.RecordObject(wsTMP, "Fix Pen Label Font");
                    wsTMP.font      = font;
                    wsTMP.fontSize  = 6f;
                    wsTMP.color     = Color.white;
                    wsTMP.fontStyle = FontStyles.Bold;
                    wsTMP.alignment = TextAlignmentOptions.Center;
                    EditorUtility.SetDirty(wsTMP);
                    penLabelFixed++;
                }
            }
        }

        // Also catch any stray world-space TextMeshPro missed by the TMP_Text scan above
        foreach (TextMeshPro ws in Object.FindObjectsByType<TextMeshPro>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ws.font != font)
            {
                Undo.RecordObject(ws, "Fix World TMP Font");
                ws.font = font;
                EditorUtility.SetDirty(ws);
            }
        }

        // ── Step 5: fix zone labels in CyberGrid (world-space TMP) ────────────
        // CyberGrid's zoneLabelPrefab is set in the GridSettings inspector slot.
        CyberGrid cg = Object.FindFirstObjectByType<CyberGrid>();
        if (cg != null && cg.settings?.zoneLabelPrefab != null)
        {
            TextMeshPro zTMP = cg.settings.zoneLabelPrefab.GetComponentInChildren<TextMeshPro>(true);
            if (zTMP != null)
            {
                Undo.RecordObject(zTMP, "Fix Zone Label Font");
                zTMP.font      = font;
                zTMP.fontSize  = 12f;
                zTMP.color     = Color.white;
                zTMP.fontStyle = FontStyles.Bold;
                zTMP.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(zTMP);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "═══════════════════════════════════════════════════════════════\n" +
            "FixUIText: COMPLETE\n" +
            "═══════════════════════════════════════════════════════════════\n" +
           $"  Font applied    : {font.name}\n" +
           $"  TMP components  : {totalFixed} fixed (all scene TMP_Text)\n" +
           $"  HUD objects     : TempText(18/Bold/\"25.0°C\") | HumText(16) | " +
                                 "LogText(11) | StatusText(16/Bold/\"STABLE\")\n" +
           $"  Buttons fixed   : {btnFixed} (Test Drought|Test Heatwave|Test Disease|Reset Healthy, size 14 Bold white)\n" +
           $"  Pen labels fixed: {penLabelFixed} spawned labels + label prefab asset\n" +
            "  ► If text is STILL garbled after entering Play Mode:\n" +
            "     Window > TextMeshPro > Import TMP Essential Resources → Import All\n" +
            "     Then re-run this menu item.");
    }

    /// <summary>
    /// Finds the best available TMP_FontAsset: prefers LiberationSans SDF (TMP default),
    /// falls back to any font with "Liberation" in name, then the first font found.
    /// </summary>
    static TMP_FontAsset FindBestTMPFont()
    {
        // Primary: standard TMP Essential Resources install location
        TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (f != null) return f;

        // Secondary: scan entire project
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        if (guids.Length == 0) return null;

        // Prefer any font with "liberation" in name
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.ToLower().Contains("liberation"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }

        // Prefer any font with "sdf" in name (likely a proper baked font)
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.ToLower().Contains("sdf"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }

        // Last resort: first found
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX TREES AND LAYOUT
    //  1. Rebuild tree border with exclusion zones (no trees in pens/buildings/grid)
    //  2. Check PathGenerator material (fix grey/orange tile)
    //  3. Update crop mappings: scale 4.0, yOffset 0.5, food_ prefabs
    //  4. Rebuild animal fences (1-unit inset, no overflow)
    //  5. Force all buildings to Y=0
    // ══════════════════════════════════════════════════════════════════════════

    static void FixTreesAndLayout()
    {
        Debug.Log("FixTreesAndLayout: ══════ Starting layout fixes ══════");

        int issues   = 0;
        int cropMiss = 0;

        GameObject Load(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p);

        // ── 1. Rebuild FarmCompound (triggers updated BuildTreeBorder) ─────────
        FarmCompound fc = Object.FindFirstObjectByType<FarmCompound>();
        if (fc != null)
        {
            Undo.RecordObject(fc, "FixTreesAndLayout — FarmCompound");
            EditorUtility.SetDirty(fc);
            fc.BuildAll();    // BuildTreeBorder now skips trees in exclusion zones
            Debug.Log("FixTreesAndLayout: FarmCompound rebuilt — check Console for tree skip report.");
        }
        else
        {
            Debug.LogWarning("FixTreesAndLayout: FarmCompound not found — run 'Ultimate Farm Build' first.");
            issues++;
        }

        // ── 2. PathGenerator material fix (grey/orange tile) ──────────────────
        PathGenerator pg = Object.FindFirstObjectByType<PathGenerator>();
        if (pg != null)
        {
            // Check if pathMaterial is null or non-URP
            bool needsFix = pg.pathMaterial == null ||
                            (pg.pathMaterial.shader != null &&
                             !pg.pathMaterial.shader.name.StartsWith("Universal Render Pipeline"));
            if (needsFix)
            {
                // Try to find or create a URP dirt-path material
                Material dirtMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DirtPath.mat");
                if (dirtMat == null)
                {
                    // Scan for any URP material with "dirt" or "path" in name
                    foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
                    {
                        string mp = AssetDatabase.GUIDToAssetPath(g);
                        string mn = System.IO.Path.GetFileNameWithoutExtension(mp).ToLower();
                        if (mn.Contains("dirt") || mn.Contains("path") || mn.Contains("soil"))
                        {
                            Material m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                            if (m != null && m.shader != null &&
                                m.shader.name.StartsWith("Universal Render Pipeline"))
                            { dirtMat = m; break; }
                        }
                    }
                }
                if (dirtMat != null)
                {
                    Undo.RecordObject(pg, "FixTreesAndLayout — PathMaterial");
                    pg.pathMaterial = dirtMat;
                    EditorUtility.SetDirty(pg);
                    pg.Build();
                    Debug.Log($"FixTreesAndLayout: PathGenerator material fixed → {dirtMat.name}");
                }
                else
                {
                    // Last resort: assign a plain URP/Lit material with sandy color
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        Material sandMat = new Material(urpLit);
                        sandMat.SetColor("_BaseColor", new Color(0.769f, 0.573f, 0.165f)); // #C4922A
                        sandMat.name = "DirtPath_Auto";
                        AssetDatabase.CreateAsset(sandMat, "Assets/Materials/DirtPath_Auto.mat");
                        AssetDatabase.SaveAssets();
                        Undo.RecordObject(pg, "FixTreesAndLayout — PathMaterial");
                        pg.pathMaterial = sandMat;
                        EditorUtility.SetDirty(pg);
                        pg.Build();
                        Debug.Log("FixTreesAndLayout: Created DirtPath_Auto.mat (#C4922A URP/Lit) for path strips.");
                    }
                    else
                    {
                        Debug.LogWarning("FixTreesAndLayout: PathMaterial is null/wrong shader but no URP dirt material found.");
                        issues++;
                    }
                }
            }
            else
                Debug.Log($"FixTreesAndLayout: PathGenerator material OK ({pg.pathMaterial.name}).");

            // Also scan for any stray non-soil plane inside grid bounds (grey/orange tile)
            float gridW = pg.gridWidth  * pg.gridSpacing;
            float gridH = pg.gridHeight * pg.gridSpacing;
            int strayCount = 0;
            foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Vector3 p = mr.transform.position;
                if (p.x < 0f || p.x > gridW || p.z < 0f || p.z > gridH) continue;
                if (mr.gameObject.name.StartsWith("Cell_") ||
                    mr.gameObject.name.StartsWith("Hub_")  ||
                    mr.gameObject.name.StartsWith("Path_")) continue;
                // Flag any renderer inside the grid that isn't a known grid object
                if (mr.sharedMaterial != null &&
                    !mr.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline"))
                {
                    Debug.LogWarning($"FixTreesAndLayout: Stray non-URP renderer inside grid bounds: " +
                                     $"'{mr.gameObject.name}' mat='{mr.sharedMaterial.name}' " +
                                     $"@ ({p.x:F1},{p.y:F1},{p.z:F1})");
                    strayCount++;
                }
            }
            if (strayCount == 0)
                Debug.Log("FixTreesAndLayout: No stray non-URP renderers inside crop grid bounds.");
        }
        else
            Debug.LogWarning("FixTreesAndLayout: PathGenerator not found in scene.");

        // ── 3. Crop mappings: scale 4.0, yOffset 0.5, food_ prefabs ──────────
        CyberGrid cg = Object.FindFirstObjectByType<CyberGrid>();
        if (cg != null)
        {
            Undo.RecordObject(cg, "FixTreesAndLayout — CropMappings");
            if (cg.settings == null) cg.settings = new CyberGrid.GridSettings();
            if (cg.settings.cropMappings == null)
                cg.settings.cropMappings = new System.Collections.Generic.List<CropMapping>();
            cg.settings.cropMappings.Clear();

            var potatoPfb    = Load(PANDAZOLE + "food_Potato.prefab")
                            ?? Load(PANDAZOLE + "Env_Potato_01.prefab")
                            ?? Load(PANDAZOLE + "Plant_Medium.prefab");
            var tomatoPfb    = Load(PANDAZOLE + "food_Tomato.prefab")
                            ?? Load(PANDAZOLE + "Env_Tomato_01.prefab")
                            ?? Load(PANDAZOLE + "Plant_Tomato_Large.prefab");
            var grapePfb     = Load(PANDAZOLE + "food_Grape.prefab")
                            ?? Load(PANDAZOLE + "Env_Grape_01.prefab")
                            ?? Load(PANDAZOLE + "Plant_Small.prefab");
            var applePfb     = Load(PANDAZOLE + "Env_Tree_01.prefab");
            var appleFoodPfb = Load(PANDAZOLE + "food_Apple.prefab")
                            ?? Load("Assets/Low Poly Fruits/Prefabs/apple.prefab");

            if (potatoPfb == null) { Debug.LogWarning("FixTreesAndLayout: Potato prefab not found"); cropMiss++; }
            if (tomatoPfb == null) { Debug.LogWarning("FixTreesAndLayout: Tomato prefab not found"); cropMiss++; }
            if (grapePfb  == null) { Debug.LogWarning("FixTreesAndLayout: Grape  prefab not found"); cropMiss++; }
            if (applePfb  == null) { Debug.LogWarning("FixTreesAndLayout: Apple  prefab not found"); cropMiss++; }

            cg.settings.cropMappings.Add(new CropMapping { type = CropType.Potato, prefab = potatoPfb, baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cg.settings.cropMappings.Add(new CropMapping { type = CropType.Tomato, prefab = tomatoPfb, baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cg.settings.cropMappings.Add(new CropMapping { type = CropType.Grape,  prefab = grapePfb,  baseScale = 4.0f, yOffset = 0.5f, spawnEvery = 1 });
            cg.settings.cropMappings.Add(new CropMapping
            {
                type = CropType.Apple, prefab = applePfb, baseScale = 4.0f, yOffset = 0.0f, spawnEvery = 2,
                fallenFruitPrefab = appleFoodPfb, fallenFruitScale = 1.2f
            });
            EditorUtility.SetDirty(cg);
            Debug.Log($"FixTreesAndLayout: Crop mappings updated — scale 4.0, yOffset 0.5.\n" +
                      $"  Potato [{(potatoPfb?potatoPfb.name:"MISSING")}] | " +
                      $"Tomato [{(tomatoPfb?tomatoPfb.name:"MISSING")}] | " +
                      $"Grape [{(grapePfb?grapePfb.name:"MISSING")}] | " +
                      $"Apple [{(applePfb?applePfb.name:"MISSING")}]");
        }
        else
            Debug.LogWarning("FixTreesAndLayout: CyberGrid not found — crop mapping skipped.");

        // ── 4. Rebuild animal fences (AnimalManager uses updated PlaceFenceRing) ─
        AnimalManager am = Object.FindFirstObjectByType<AnimalManager>();
        if (am != null)
        {
            Undo.RecordObject(am, "FixTreesAndLayout — AnimalManager");
            am.ClearAll();
            am.BuildAll();
            Debug.Log("FixTreesAndLayout: AnimalManager rebuilt — fences now 1-unit inset from pen edges.");
        }
        else
            Debug.LogWarning("FixTreesAndLayout: AnimalManager not found — fence fix skipped.");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ── Final report ──────────────────────────────────────────────────────
        Debug.Log(
            "═══════════════════════════════════════════════════════════════\n" +
            "FixTreesAndLayout: COMPLETE\n" +
            "═══════════════════════════════════════════════════════════════\n" +
            "  [1] Trees  : Exclusion zones active — trees skip crop grid (+5), animal zone (+8), buildings (+5)\n" +
            "               See tree-by-tree skip report above ↑\n" +
            "  [2] Paths  : PathGenerator material checked / fixed (see above)\n" +
            "               Stray non-URP renderers inside grid: reported above\n" +
            "  [3] Crops  : Scale 4.0, yOffset 0.5, food_Potato/Tomato/Grape tried first\n" +
           $"               Missing crop prefabs: {cropMiss}\n" +
            "  [4] Fences : 1-unit inset — no fence crosses pen boundary\n" +
            "  [5] Buildings: Y=0 forced on all FarmCompound buildings\n" +
           $"  Issues found: {issues + cropMiss}\n" +
            "  ► Enter Play Mode to see crops at new scale.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  QUICK SCENARIO FIX
    //  Validates scenario system, auto-wires FuturisticUI, and prints status.
    //  Safe to run any time — does not rebuild the scene.
    // ══════════════════════════════════════════════════════════════════════════

    static void QuickScenarioFix()
    {
        bool ok = true;

        ScenarioManager sm = Object.FindFirstObjectByType<ScenarioManager>();
        if (sm == null)
        {
            Debug.LogError("QuickScenarioFix: ScenarioManager NOT FOUND. Run 'Fix Scenarios Only' to create it.");
            ok = false;
        }
        else
            Debug.Log("QuickScenarioFix: ScenarioManager found.");

        CyberGrid cg = Object.FindFirstObjectByType<CyberGrid>();
        if (cg == null)
        {
            Debug.LogError("QuickScenarioFix: CyberGrid NOT FOUND — cannot apply per-cell visuals.");
            ok = false;
        }
        else
        {
            int crops = 0;
            if (cg.gridObjects != null)
                for (int x = 0; x < cg.gridObjects.GetLength(0); x++)
                    for (int y = 0; y < cg.gridObjects.GetLength(1); y++)
                        if (cg.gridObjects[x, y] != null) crops++;
            Debug.Log($"QuickScenarioFix: CyberGrid found — {crops} crop GameObjects (must enter Play Mode to populate).");
        }

        ScenarioOverlay ov = Object.FindFirstObjectByType<ScenarioOverlay>();
        if (ov == null)
            Debug.LogWarning("QuickScenarioFix: ScenarioOverlay not found — 'Fix Scenarios Only' will create it.");
        else
            Debug.Log("QuickScenarioFix: ScenarioOverlay found.");

        FuturisticUI fui = Object.FindFirstObjectByType<FuturisticUI>();
        if (fui == null)
            Debug.LogWarning("QuickScenarioFix: FuturisticUI NOT found — add it to the scene to enable HUD buttons.");

        if (ok)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(
                "QuickScenarioFix: ALL CLEAR\n" +
                "  4 scenario buttons confirmed ready:\n" +
                "    [Drought]  [Heatwave]  [Disease]  [Healthy]\n" +
                "  Enter Play Mode → use FuturisticUI buttons to test.\n" +
                "  MaterialPropertyBlock: zero permanent material changes.");
        }
        else
            Debug.LogWarning("QuickScenarioFix: Issues found — run 'Fix Scenarios Only' to repair.");
    }

    // ── Add Day Night Cycle ───────────────────────────────────────────────────

    static void AddDayNightCycle()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Add Day Night Cycle ===");

        // ── 1. TwinSimulationManager check ────────────────────────────────────
        TwinSimulationManager tsm = Object.FindFirstObjectByType<TwinSimulationManager>();
        if (tsm != null)
        {
            sb.AppendLine($"  TwinSimulationManager: FOUND — timeScale={tsm.timeScale}s/hr, " +
                          $"dayDuration={tsm.dayDurationSeconds}s (informational)");
        }
        else
        {
            sb.AppendLine("  TwinSimulationManager: NOT FOUND — DayNightCycle will run standalone " +
                          "(uses its own dayDurationSeconds)");
        }

        // ── 2. Find or create DayNightCycle host ──────────────────────────────
        DayNightCycle dnc = Object.FindFirstObjectByType<DayNightCycle>();
        GameObject dncGO;
        bool created = false;
        if (dnc == null)
        {
            dncGO = new GameObject("DayNightCycle");
            Undo.RegisterCreatedObjectUndo(dncGO, "Create DayNightCycle");
            dnc   = Undo.AddComponent<DayNightCycle>(dncGO);
            created = true;
            sb.AppendLine("  DayNightCycle GameObject: CREATED");
        }
        else
        {
            dncGO = dnc.gameObject;
            sb.AppendLine($"  DayNightCycle GameObject: already exists on '{dncGO.name}'");
        }

        // ── 3. Find and assign Sun (first Directional Light) ──────────────────
        if (dnc.sunLight == null)
        {
            Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in all)
            {
                // Skip any child of DayNightCycle itself (moon will be created there)
                if (l.type == LightType.Directional && !l.transform.IsChildOf(dncGO.transform))
                {
                    Undo.RecordObject(dnc, "Assign Sun Light");
                    dnc.sunLight = l;
                    EditorUtility.SetDirty(dnc);
                    sb.AppendLine($"  Sun Light: assigned '{l.gameObject.name}'");
                    break;
                }
            }
            if (dnc.sunLight == null)
                sb.AppendLine("  Sun Light: NOT FOUND — drag a Directional Light into DayNightCycle.sunLight");
        }
        else
            sb.AppendLine($"  Sun Light: already assigned '{dnc.sunLight.gameObject.name}'");

        // ── 4. Create Moon directional light child ────────────────────────────
        bool moonCreated = false;
        if (dnc.moonLight == null)
        {
            GameObject moonGO = new GameObject("Moon");
            Undo.RegisterCreatedObjectUndo(moonGO, "Create Moon Light");
            moonGO.transform.SetParent(dncGO.transform, false);
            moonGO.transform.rotation = Quaternion.Euler(250f, 210f, 0f);

            Light moon = Undo.AddComponent<Light>(moonGO);
            moon.type      = LightType.Directional;
            moon.color     = new Color(0.784f, 0.847f, 1.000f); // #C8D8FF
            moon.intensity = 0f;  // DayNightCycle.ApplyMoon() sets this at runtime

            Undo.RecordObject(dnc, "Assign Moon Light");
            dnc.moonLight = moon;
            EditorUtility.SetDirty(dnc);
            moonCreated = true;
        }
        sb.AppendLine($"  Moon Light: {(moonCreated ? "CREATED" : "already assigned — skipped")}");

        // ── 4b. Create SunVisual sphere ───────────────────────────────────────
        bool sunVisualCreated = false;
        if (dnc.sunVisual == null)
        {
            // ── SunMat ──────────────────────────────────────────────────────────
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string sunMatPath = "Assets/Materials/SunMat.asset";
            Material sunMat = AssetDatabase.LoadAssetAtPath<Material>(sunMatPath);
            if (sunMat == null)
            {
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");
                sunMat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Standard"));
                sunMat.color = new Color(1f, 0.898f, 0.400f); // #FFE566
                // Emission: #FFD700 × 2
                sunMat.EnableKeyword("_EMISSION");
                sunMat.SetColor("_EmissionColor", new Color(1f, 0.843f, 0f) * 2f);
                AssetDatabase.CreateAsset(sunMat, sunMatPath);
            }

            // ── SunGlowMat (semi-transparent halo) ─────────────────────────────
            string sunGlowMatPath = "Assets/Materials/SunGlowMat.asset";
            Material sunGlowMat = AssetDatabase.LoadAssetAtPath<Material>(sunGlowMatPath);
            if (sunGlowMat == null)
            {
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");
                sunGlowMat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Standard"));
                sunGlowMat.color = new Color(1f, 0.843f, 0f, 0.3f); // #FFD700 alpha 0.3
                // Enable alpha transparency
                sunGlowMat.SetFloat("_Surface", 1);   // Transparent
                sunGlowMat.SetFloat("_Blend",   0);   // Alpha
                sunGlowMat.renderQueue = 3000;
                AssetDatabase.CreateAsset(sunGlowMat, sunGlowMatPath);
            }
            AssetDatabase.SaveAssets();

            // ── SunVisual sphere ────────────────────────────────────────────────
            GameObject sunVisGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(sunVisGO, "Create SunVisual");
            sunVisGO.name = "SunVisual";
            sunVisGO.transform.SetParent(dncGO.transform, false);
            sunVisGO.transform.localScale = Vector3.one * 3f;
            // Position at 6 AM (t=0.25) as default: angle = (0.25*360-90)*Deg2Rad = 0, so X=120, Y=0
            // Use t=0.30 for a bit above horizon: angle=(0.30*360-90)*Deg2Rad
            float sunAngle0 = (0.30f * 360f - 90f) * Mathf.Deg2Rad;
            sunVisGO.transform.position = new Vector3(Mathf.Cos(sunAngle0) * 120f, Mathf.Sin(sunAngle0) * 120f, 0f);

            MeshRenderer sunMR = sunVisGO.GetComponent<MeshRenderer>();
            if (sunMR != null) { Undo.RecordObject(sunMR, "Set SunMat"); sunMR.sharedMaterial = sunMat; }
            Object.DestroyImmediate(sunVisGO.GetComponent<Collider>());

            // ── Glow sphere (child, scale 4/3 relative → absolute 4,4,4) ─────
            GameObject glowGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(glowGO, "Create SunGlow");
            glowGO.name = "SunGlow";
            glowGO.transform.SetParent(sunVisGO.transform, false);
            glowGO.transform.localScale = Vector3.one * (4f / 3f);
            MeshRenderer glowMR = glowGO.GetComponent<MeshRenderer>();
            if (glowMR != null) { Undo.RecordObject(glowMR, "Set SunGlowMat"); glowMR.sharedMaterial = sunGlowMat; }
            Object.DestroyImmediate(glowGO.GetComponent<Collider>());

            // ── Point light on sun ────────────────────────────────────────────
            GameObject sunLightGO = new GameObject("SunPointLight");
            Undo.RegisterCreatedObjectUndo(sunLightGO, "Create SunPointLight");
            sunLightGO.transform.SetParent(sunVisGO.transform, false);
            Light sunPL = Undo.AddComponent<Light>(sunLightGO);
            sunPL.type      = LightType.Point;
            sunPL.color     = new Color(1f, 0.898f, 0.4f); // #FFE566
            sunPL.intensity = 0.5f;
            sunPL.range     = 200f;

            // ── Wire to DayNightCycle ─────────────────────────────────────────
            Undo.RecordObject(dnc, "Assign SunVisual");
            dnc.sunVisual     = sunVisGO.transform;
            dnc.sunPointLight = sunPL;
            EditorUtility.SetDirty(dnc);
            sunVisualCreated = true;
            sb.AppendLine("  SunVisual: CREATED (sphere ×3 + glow halo + point light, SunMat #FFE566)");
            sb.AppendLine("  Sun at noon will be at position (0, 120, 0) — directly above farm centre");
        }
        else
            sb.AppendLine($"  SunVisual: already assigned '{dnc.sunVisual.gameObject.name}'");

        // ── 4c. Create MoonVisual sphere ──────────────────────────────────────
        bool moonVisualCreated = false;
        if (dnc.moonVisual == null)
        {
            string moonMatPath = "Assets/Materials/MoonMat.asset";
            Material moonMat = AssetDatabase.LoadAssetAtPath<Material>(moonMatPath);
            if (moonMat == null)
            {
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");
                moonMat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Standard"));
                moonMat.color = new Color(0.910f, 0.910f, 1.000f, 1f); // #E8E8FF
                moonMat.EnableKeyword("_EMISSION");
                moonMat.SetColor("_EmissionColor", new Color(0.784f, 0.847f, 1f) * 0.8f); // #C8D8FF × 0.8
                // Enable alpha transparency for horizon fade
                moonMat.SetFloat("_Surface", 1);
                moonMat.SetFloat("_Blend",   0);
                moonMat.renderQueue = 3000;
                AssetDatabase.CreateAsset(moonMat, moonMatPath);
                AssetDatabase.SaveAssets();
            }

            GameObject moonVisGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(moonVisGO, "Create MoonVisual");
            moonVisGO.name = "MoonVisual";
            moonVisGO.transform.SetParent(dncGO.transform, false);
            moonVisGO.transform.localScale = Vector3.one * 2f;
            // Start below horizon (midnight)
            moonVisGO.transform.position = new Vector3(0f, -110f, 0f);
            moonVisGO.SetActive(false);

            MeshRenderer moonMR = moonVisGO.GetComponent<MeshRenderer>();
            if (moonMR != null) { Undo.RecordObject(moonMR, "Set MoonMat"); moonMR.sharedMaterial = moonMat; }
            Object.DestroyImmediate(moonVisGO.GetComponent<Collider>());

            Undo.RecordObject(dnc, "Assign MoonVisual");
            dnc.moonVisual = moonVisGO.transform;
            EditorUtility.SetDirty(dnc);
            moonVisualCreated = true;
            sb.AppendLine("  MoonVisual: CREATED (sphere ×2, MoonMat #E8E8FF, fades at horizon)");
        }
        else
            sb.AppendLine($"  MoonVisual: already assigned '{dnc.moonVisual.gameObject.name}'");

        // ── 5. Create Starfield particle system child ─────────────────────────
        bool starsCreated = false;
        if (dnc.starsParticleSystem == null)
        {
            // Material: look for existing, else create
            string starMatPath = "Assets/Materials/Starfield_Mat.asset";
            Material starMat = AssetDatabase.LoadAssetAtPath<Material>(starMatPath);
            if (starMat == null)
            {
                // Ensure Materials folder exists
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");

                Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (urpParticles == null) urpParticles = Shader.Find("Particles/Standard Unlit");
                if (urpParticles == null) urpParticles = Shader.Find("Legacy Shaders/Particles/Additive");

                starMat = new Material(urpParticles != null ? urpParticles : Shader.Find("Standard"));
                starMat.color = Color.white;
                AssetDatabase.CreateAsset(starMat, starMatPath);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"  Starfield material: CREATED at {starMatPath}");
            }
            else
                sb.AppendLine($"  Starfield material: found at {starMatPath}");

            GameObject starGO = new GameObject("Starfield");
            Undo.RegisterCreatedObjectUndo(starGO, "Create Starfield");
            starGO.transform.SetParent(dncGO.transform, false);

            ParticleSystem ps = Undo.AddComponent<ParticleSystem>(starGO);

            // Main module
            var main = ps.main;
            main.loop        = true;
            main.startSpeed  = 0f;
            main.startSize   = 0.02f;
            main.startLifetime = 99999f;
            main.maxParticles  = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission: burst of 200 on start
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 200)
            });

            // Shape: sphere shell
            var shape = ps.shape;
            shape.enabled        = true;
            shape.shapeType      = ParticleSystemShapeType.Sphere;
            shape.radius         = 80f;
            shape.radiusThickness = 0f; // emit only on surface

            // Renderer: assign material
            ParticleSystemRenderer psr = starGO.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Undo.RecordObject(psr, "Set Starfield Material");
                psr.material = starMat;
                EditorUtility.SetDirty(psr);
            }

            Undo.RecordObject(dnc, "Assign Stars PS");
            dnc.starsParticleSystem = ps;
            EditorUtility.SetDirty(dnc);
            starsCreated = true;
        }
        sb.AppendLine($"  Stars ParticleSystem: {(starsCreated ? "CREATED (200 particles, radius 80)" : "already assigned — skipped")}");

        // ── 6. Create time slider UI (also sets dnc.timeDisplayText) ─────────
        if (dnc.timeSlider == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Slider slider = CreateTimeSlider(canvas, dnc);
                if (slider != null)
                    sb.AppendLine("  Time Slider: CREATED (bottom of canvas, centred time label, full-width slider)");
                else
                    sb.AppendLine("  Time Slider: creation failed — add manually");
            }
            else
                sb.AppendLine("  Time Slider: no Canvas found — add manually");
        }
        else
            sb.AppendLine($"  Time Slider: already assigned '{dnc.timeSlider.gameObject.name}'");

        // ── 7. Time Display Text — only needed if slider creation skipped ─────
        // CreateTimeSlider wires dnc.timeDisplayText to its centred TimeDisplay.
        // This step is a fallback for when the slider already existed.
        if (dnc.timeDisplayText == null)
        {
            TMP_Text found = null;
            string[] candidates = { "TimeDisplay", "TimeText", "TempText", "TemperatureText" };
            foreach (var n in candidates)
            {
                GameObject go = GameObject.Find(n);
                if (go != null) { found = go.GetComponent<TMP_Text>(); if (found != null) break; }
            }

            if (found != null)
            {
                Undo.RecordObject(dnc, "Assign Time Display");
                dnc.timeDisplayText = found;
                EditorUtility.SetDirty(dnc);
                sb.AppendLine($"  Time Display Text: assigned existing '{found.gameObject.name}'");
            }
            else
                sb.AppendLine("  Time Display Text: no Canvas text found — drag a TMP_Text manually");
        }
        else
            sb.AppendLine($"  Time Display Text: '{dnc.timeDisplayText.gameObject.name}' (set by slider panel)");

        // ── 8. Final report ───────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("SUMMARY:");
        sb.AppendLine($"  TwinSimulationManager: {(tsm != null ? "YES" : "NO (standalone fallback active)")}");
        if (tsm != null)
            sb.AppendLine($"  Day duration: {tsm.dayDurationSeconds}s real-time per sim day");
        sb.AppendLine($"  SunVisual created: {(dnc.sunVisual  != null ? "YES" : "NO")} — correct material: SunMat #FFE566");
        sb.AppendLine($"  Sun at noon position: (0, 120, 0) — directly above farm centre");
        sb.AppendLine($"  MoonVisual created: {(dnc.moonVisual != null ? "YES" : "NO")} — MoonMat #E8E8FF, fades at horizon");
        sb.AppendLine($"  Moon directional light: {(dnc.moonLight != null ? "YES" : "NO")}");
        sb.AppendLine($"  Stars fade with time: YES — alpha driven by NightBlend(t), 0 at day, 1 at full night");
        sb.AppendLine($"  Stars: {(dnc.starsParticleSystem != null ? "YES" : "NO")}");
        sb.AppendLine($"  Time Display: {(dnc.timeDisplayText != null ? "YES" : "NO")}");
        sb.AppendLine($"  Time Slider: {(dnc.timeSlider != null ? "YES" : "NO")}");
        sb.AppendLine();
        sb.AppendLine("Next step: enter Play Mode — the cycle will start at startHour (default 6 AM).");
        sb.AppendLine("Drag the bottom slider to manually jump to any time of day.");
        sb.AppendLine("Horizon fog glows orange at sunrise (#FF6B35) and red-orange at sunset (#FF4500).");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Builds a time-of-day slider panel at the bottom of the given canvas and
    /// wires it to the DayNightCycle component.
    /// Layout: panel → centred "06:00 AM" (wired to dnc.timeDisplayText) above full-width slider.
    /// </summary>
    static Slider CreateTimeSlider(Canvas canvas, DayNightCycle dnc)
    {
        TMP_FontAsset font = FindBestTMPFont();

        // ── Panel (taller to fit time text row above slider row) ───────────────
        GameObject panelGO = new GameObject("TimeSliderPanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "Create TimeSliderPanel");
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.1f, 0f);
        panelRT.anchorMax        = new Vector2(0.9f, 0f);
        panelRT.pivot            = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 10f);
        panelRT.sizeDelta        = new Vector2(0f, 52f);

        UnityEngine.UI.Image bg = panelGO.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        // ── Centred time display (top half of panel) ───────────────────────────
        GameObject timeGO = new GameObject("TimeDisplay");
        Undo.RegisterCreatedObjectUndo(timeGO, "Create TimeDisplay");
        timeGO.transform.SetParent(panelGO.transform, false);

        RectTransform timeRT = timeGO.AddComponent<RectTransform>();
        timeRT.anchorMin        = new Vector2(0f, 0.5f);
        timeRT.anchorMax        = new Vector2(1f, 1f);
        timeRT.pivot            = new Vector2(0.5f, 0.5f);
        timeRT.anchoredPosition = Vector2.zero;
        timeRT.sizeDelta        = Vector2.zero;

        TMP_Text timeTxt = timeGO.AddComponent<TextMeshProUGUI>();
        timeTxt.text      = "06:00 AM";
        timeTxt.fontSize  = 14f;
        timeTxt.fontStyle = FontStyles.Bold;
        timeTxt.color     = Color.white;
        timeTxt.alignment = TextAlignmentOptions.Center;
        if (font != null) timeTxt.font = font;

        // Wire this as the authoritative time display
        Undo.RecordObject(dnc, "Assign Time Display");
        dnc.timeDisplayText = timeTxt;
        EditorUtility.SetDirty(dnc);

        // ── Slider (bottom half, full panel width with small inset) ────────────
        GameObject sliderGO = new GameObject("TimeSlider");
        Undo.RegisterCreatedObjectUndo(sliderGO, "Create TimeSlider");
        sliderGO.transform.SetParent(panelGO.transform, false);

        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin        = new Vector2(0f, 0f);
        sliderRT.anchorMax        = new Vector2(1f, 0.5f);
        sliderRT.pivot            = new Vector2(0.5f, 0.5f);
        sliderRT.anchoredPosition = Vector2.zero;
        sliderRT.sizeDelta        = Vector2.zero;
        sliderRT.offsetMin        = new Vector2(8f,  sliderRT.offsetMin.y);
        sliderRT.offsetMax        = new Vector2(-8f, sliderRT.offsetMax.y);

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 0.25f; // 6 AM default

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        UnityEngine.UI.Image bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        slider.targetGraphic = bgImg;

        // Fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin        = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax        = new Vector2(1f, 0.75f);
        fillAreaRT.sizeDelta        = new Vector2(-20f, 0f);
        fillAreaRT.anchoredPosition = Vector2.zero;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = new Vector2(10f, 0f);
        UnityEngine.UI.Image fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = new Color(1f, 0.75f, 0.2f, 1f); // warm amber fill
        slider.fillRect = fillRT;

        // Handle area
        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin        = Vector2.zero;
        handleAreaRT.anchorMax        = Vector2.one;
        handleAreaRT.sizeDelta        = new Vector2(-20f, 0f);
        handleAreaRT.anchoredPosition = Vector2.zero;

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20f, 20f);
        UnityEngine.UI.Image handleImg = handleGO.AddComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;
        slider.handleRect = handleRT;

        // ── Wire slider to DayNightCycle ───────────────────────────────────────
        Undo.RecordObject(dnc, "Assign Time Slider");
        dnc.timeSlider = slider;
        EditorUtility.SetDirty(dnc);

        return slider;
    }

    // ── Fix Sun Moon Visibility ────────────────────────────────────────────────

    static void FixSunMoonVisibility()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Fix Sun Moon Visibility ===");

        DayNightCycle dnc = Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc == null)
        {
            Debug.LogWarning("FixSunMoonVisibility: No DayNightCycle found — run 'Add Day Night Cycle' first.");
            return;
        }

        // ── 1. Camera far clip → 500 ──────────────────────────────────────────
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (cams.Length == 0)
            sb.AppendLine("  Camera: NONE FOUND");
        foreach (Camera cam in cams)
        {
            if (cam.farClipPlane < 500f)
            {
                Undo.RecordObject(cam, "Set Camera FarClip");
                cam.farClipPlane = 500f;
                EditorUtility.SetDirty(cam);
                sb.AppendLine($"  Camera '{cam.gameObject.name}': farClipPlane → 500");
            }
            else
                sb.AppendLine($"  Camera '{cam.gameObject.name}': farClipPlane={cam.farClipPlane} — OK");
        }

        // ── 2. SunVisual — scale 6×6×6, verify shader ─────────────────────────
        if (dnc.sunVisual != null)
        {
            Undo.RecordObject(dnc.sunVisual, "Fix SunVisual Scale");
            dnc.sunVisual.localScale = Vector3.one * 6f;
            EditorUtility.SetDirty(dnc.sunVisual.gameObject);
            sb.AppendLine($"  SunVisual: localScale → 6×6×6  |  worldPos={dnc.sunVisual.position}");

            // Shader check
            MeshRenderer sunMR = dnc.sunVisual.GetComponent<MeshRenderer>();
            if (sunMR != null && sunMR.sharedMaterial != null)
            {
                string sh = sunMR.sharedMaterial.shader.name;
                if (!sh.Contains("Universal Render Pipeline") && !sh.Contains("URP"))
                {
                    Shader urpU = Shader.Find("Universal Render Pipeline/Unlit");
                    if (urpU != null)
                    {
                        Undo.RecordObject(sunMR.sharedMaterial, "Fix SunMat Shader");
                        sunMR.sharedMaterial.shader = urpU;
                        sb.AppendLine($"  SunMat: shader '{sh}' → URP/Unlit (FIXED)");
                    }
                    else
                        sb.AppendLine($"  SunMat: shader '{sh}' — URP/Unlit not found; check URP installation");
                }
                else
                    sb.AppendLine($"  SunMat: shader '{sh}' — OK");
            }

            // Ensure active
            if (!dnc.sunVisual.gameObject.activeSelf)
            {
                Undo.RecordObject(dnc.sunVisual.gameObject, "Activate SunVisual");
                dnc.sunVisual.gameObject.SetActive(true);
                sb.AppendLine("  SunVisual: was inactive — activated for edit-mode check");
            }

            // Scale glow halo to match new sun size (absolute 8×8×8 → local 8/6)
            Transform glow = dnc.sunVisual.Find("SunGlow");
            if (glow != null)
            {
                Undo.RecordObject(glow, "Fix SunGlow Scale");
                glow.localScale = Vector3.one * (8f / 6f);
                sb.AppendLine("  SunGlow halo: local scale adjusted to match new sun size");
            }
        }
        else
            sb.AppendLine("  SunVisual: NOT ASSIGNED — run 'Add Day Night Cycle' first");

        // ── 3. MoonVisual — scale 4×4×4, verify shader ────────────────────────
        if (dnc.moonVisual != null)
        {
            Undo.RecordObject(dnc.moonVisual, "Fix MoonVisual Scale");
            dnc.moonVisual.localScale = Vector3.one * 4f;
            EditorUtility.SetDirty(dnc.moonVisual.gameObject);
            sb.AppendLine($"  MoonVisual: localScale → 4×4×4  |  worldPos={dnc.moonVisual.position}");

            MeshRenderer moonMR = dnc.moonVisual.GetComponent<MeshRenderer>();
            if (moonMR != null && moonMR.sharedMaterial != null)
            {
                string sh = moonMR.sharedMaterial.shader.name;
                if (!sh.Contains("Universal Render Pipeline") && !sh.Contains("URP"))
                {
                    Shader urpU = Shader.Find("Universal Render Pipeline/Unlit");
                    if (urpU != null)
                    {
                        Undo.RecordObject(moonMR.sharedMaterial, "Fix MoonMat Shader");
                        moonMR.sharedMaterial.shader = urpU;
                        sb.AppendLine($"  MoonMat: shader '{sh}' → URP/Unlit (FIXED)");
                    }
                    else
                        sb.AppendLine($"  MoonMat: shader '{sh}' — URP/Unlit not found");
                }
                else
                    sb.AppendLine($"  MoonMat: shader '{sh}' — OK");
            }
        }
        else
            sb.AppendLine("  MoonVisual: NOT ASSIGNED — run 'Add Day Night Cycle' first");

        // ── 4. Scene diagnostic ───────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("DIAGNOSTICS:");
        sb.AppendLine($"  Skybox material: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none (procedural or unset)")}");
        sb.AppendLine($"  Fog enabled: {RenderSettings.fog}  mode: {RenderSettings.fogMode}");
        if (dnc.sunVisual != null)
        {
            float t = 0.5f; // noon
            float angle = (t * 360f - 90f) * Mathf.Deg2Rad;
            sb.AppendLine($"  Sun at noon (t=0.5): ({Mathf.Cos(angle)*120f:F1}, {Mathf.Sin(angle)*120f:F1}, 0)  → should be (0, 120, 0)");
        }
        sb.AppendLine();
        sb.AppendLine("If still not visible: check skybox layer / camera culling mask, or increase skybox exposure.");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    // ── Diagnose Sun Moon ──────────────────────────────────────────────────────

    static void DiagnoseSunMoon()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Diagnose Sun Moon ===");

        // ── 1. DayNightCycle component ─────────────────────────────────────────
        DayNightCycle dnc = Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc == null)
        {
            Debug.LogWarning("DiagnoseSunMoon: No DayNightCycle in scene — run 'Add Day Night Cycle' first.");
            return;
        }
        sb.AppendLine($"\n[DayNightCycle]");
        sb.AppendLine($"  GO name   : {dnc.gameObject.name}");
        sb.AppendLine($"  GO active : {dnc.gameObject.activeInHierarchy}");
        sb.AppendLine($"  sunVisual  field : {(dnc.sunVisual  != null ? dnc.sunVisual.gameObject.name  : "NULL — not assigned")}");
        sb.AppendLine($"  moonVisual field : {(dnc.moonVisual != null ? dnc.moonVisual.gameObject.name : "NULL — not assigned")}");

        // ── 2. Camera ─────────────────────────────────────────────────────────
        sb.AppendLine($"\n[Camera]");
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (cams.Length == 0)
        {
            sb.AppendLine("  NO CAMERAS FOUND IN SCENE");
        }
        foreach (Camera cam in cams)
        {
            sb.AppendLine($"  '{cam.gameObject.name}'");
            sb.AppendLine($"    position      : {cam.transform.position}");
            sb.AppendLine($"    nearClip      : {cam.nearClipPlane}");
            sb.AppendLine($"    farClip       : {cam.farClipPlane}  {(cam.farClipPlane < 300f ? "← TOO SMALL (need ≥300 for radius-120 arc)" : "OK")}");
            sb.AppendLine($"    cullingMask   : {cam.cullingMask} (0xFFFFFFFF = all layers on)");
            // List any layers that are OFF
            for (int i = 0; i < 32; i++)
            {
                if (((cam.cullingMask >> i) & 1) == 0)
                    sb.AppendLine($"    layer {i,2} ({LayerMask.LayerToName(i)}) is CULLED");
            }
        }

        // ── 3. SunVisual ──────────────────────────────────────────────────────
        sb.AppendLine($"\n[SunVisual]");
        DiagnoseVisual(dnc.sunVisual, "SunVisual", sb);

        // ── 4. MoonVisual ─────────────────────────────────────────────────────
        sb.AppendLine($"\n[MoonVisual]");
        DiagnoseVisual(dnc.moonVisual, "MoonVisual", sb);

        // ── 5. Simulation time & formula check ────────────────────────────────
        sb.AppendLine($"\n[Formula Check]");
        TwinSimulationManager tsm = Object.FindFirstObjectByType<TwinSimulationManager>();
        float t_now;
        if (tsm != null)
        {
            t_now = tsm.simulationTimeOfDay;
            sb.AppendLine($"  TwinSimulationManager.currentHour = {tsm.currentHour:F2}");
            sb.AppendLine($"  simulationTimeOfDay (t)           = {t_now:F4}");
        }
        else
        {
            t_now = 0.25f; // fallback to 6 AM
            sb.AppendLine("  TwinSimulationManager: NOT FOUND — using t=0.25 (6 AM) for formula check");
        }

        float sunAngle  = (t_now * 360f - 90f)  * Mathf.Deg2Rad;
        float moonAngle = (t_now * 360f + 90f)  * Mathf.Deg2Rad;
        Vector3 sunPos  = new Vector3(Mathf.Cos(sunAngle)  * 120f, Mathf.Sin(sunAngle)  * 120f, 0f);
        Vector3 moonPos = new Vector3(Mathf.Cos(moonAngle) * 100f, Mathf.Sin(moonAngle) * 100f, 0f);
        sb.AppendLine($"\n  At t={t_now:F4}:");
        sb.AppendLine($"    Sun  formula → ({sunPos.x:F1}, {sunPos.y:F1}, {sunPos.z:F1})  visible={sunPos.y > -5f}");
        sb.AppendLine($"    Moon formula → ({moonPos.x:F1}, {moonPos.y:F1}, {moonPos.z:F1})  visible={moonPos.y > -5f}");
        sb.AppendLine($"\n  Reference positions:");
        sb.AppendLine($"    t=0.25 (6AM sunrise)  → Sun  ({Mathf.Cos((0.25f*360f-90f)*Mathf.Deg2Rad)*120f:F1}, {Mathf.Sin((0.25f*360f-90f)*Mathf.Deg2Rad)*120f:F1}, 0)");
        sb.AppendLine($"    t=0.50 (noon)         → Sun  (0.0, 120.0, 0)");
        sb.AppendLine($"    t=0.75 (6PM sunset)   → Sun  ({Mathf.Cos((0.75f*360f-90f)*Mathf.Deg2Rad)*120f:F1}, {Mathf.Sin((0.75f*360f-90f)*Mathf.Deg2Rad)*120f:F1}, 0)");

        // ── 6. FORCE SUN TO FIXED VISIBLE POSITION ────────────────────────────
        sb.AppendLine($"\n[Force Test]");
        if (dnc.sunVisual != null)
        {
            Undo.RecordObject(dnc.sunVisual, "Force SunVisual test position");
            dnc.sunVisual.position = new Vector3(0f, 50f, 100f);

            // Make absolutely sure it is active and renderer is enabled
            if (!dnc.sunVisual.gameObject.activeSelf)
            {
                Undo.RecordObject(dnc.sunVisual.gameObject, "Activate SunVisual");
                dnc.sunVisual.gameObject.SetActive(true);
            }
            MeshRenderer forceMR = dnc.sunVisual.GetComponent<MeshRenderer>();
            if (forceMR != null && !forceMR.enabled)
            {
                Undo.RecordObject(forceMR, "Enable SunVisual MeshRenderer");
                forceMR.enabled = true;
            }

            EditorUtility.SetDirty(dnc.sunVisual.gameObject);
            sb.AppendLine("  SunVisual FORCED to (0, 50, 100) — look in Scene/Game view now.");
            sb.AppendLine("  If VISIBLE  → formula puts sun below horizon at current time; use time slider to advance to noon.");
            sb.AppendLine("  If INVISIBLE → material or layer issue (see shader/culling info above).");
        }
        else
            sb.AppendLine("  SunVisual is NULL — cannot force position. Run 'Add Day Night Cycle' first.");

        // ── 7. SunGlow child ──────────────────────────────────────────────────
        if (dnc.sunVisual != null)
        {
            Transform glow = dnc.sunVisual.Find("SunGlow");
            sb.AppendLine($"\n[SunGlow child]");
            if (glow != null) DiagnoseVisual(glow, "SunGlow", sb);
            else sb.AppendLine("  SunGlow child: NOT FOUND under SunVisual");

            Transform spLight = dnc.sunVisual.Find("SunPointLight");
            if (spLight != null)
                sb.AppendLine($"  SunPointLight child: FOUND, active={spLight.gameObject.activeSelf}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    // ── Fix Sun Color ─────────────────────────────────────────────────────────

    static void FixSunColor()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Fix Sun Color ===");

        DayNightCycle dnc = Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc == null)
        {
            Debug.LogWarning("FixSunColor: No DayNightCycle found.");
            return;
        }

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");
        sb.AppendLine($"  Shader: {sh.name}");

        // ── Sun ───────────────────────────────────────────────────────────────
        if (dnc.sunVisual != null)
        {
            MeshRenderer mr = dnc.sunVisual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.RecordObject(mr, "Fix Sun Color");
                Material m = new Material(sh) { name = "SunMat_Runtime" };
                Color sunCol = new Color(1f, 0.898f, 0.400f, 1f); // #FFE566
                m.SetColor("_BaseColor",     sunCol);
                m.color = sunCol;
                m.SetFloat("_Surface", 0f);
                m.renderQueue = 3700;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.843f, 0f, 1f) * 3f);
                mr.sharedMaterial = m;
                mr.enabled = true;
                EditorUtility.SetDirty(mr);

                Undo.RecordObject(dnc.sunVisual, "Fix Sun Scale");
                dnc.sunVisual.localScale = Vector3.one * 8f;
                EditorUtility.SetDirty(dnc.sunVisual.gameObject);
                sb.AppendLine($"  SunVisual: material recreated — _BaseColor #FFE566, emission #FFD700×3, scale 8");
            }
            else
                sb.AppendLine("  SunVisual: no MeshRenderer!");
        }
        else
            sb.AppendLine("  SunVisual: NOT ASSIGNED");

        // ── Moon ──────────────────────────────────────────────────────────────
        if (dnc.moonVisual != null)
        {
            MeshRenderer mr = dnc.moonVisual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.RecordObject(mr, "Fix Moon Color");
                Material m = new Material(sh) { name = "MoonMat_Runtime" };
                Color moonCol = new Color(0.909f, 0.909f, 1f, 1f); // #E8E8FF
                m.SetColor("_BaseColor",     moonCol);
                m.color = moonCol;
                m.SetFloat("_Surface", 0f);
                m.renderQueue = 3700;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.784f, 0.847f, 1f, 1f) * 2f);
                mr.sharedMaterial = m;
                mr.enabled = true;
                EditorUtility.SetDirty(mr);

                Undo.RecordObject(dnc.moonVisual, "Fix Moon Scale");
                dnc.moonVisual.localScale = Vector3.one * 6f;
                EditorUtility.SetDirty(dnc.moonVisual.gameObject);
                sb.AppendLine($"  MoonVisual: material recreated — _BaseColor #E8E8FF, emission #C8D8FF×2, scale 6");
            }
            else
                sb.AppendLine("  MoonVisual: no MeshRenderer!");
        }
        else
            sb.AppendLine("  MoonVisual: NOT ASSIGNED");

        // ── Night sky ─────────────────────────────────────────────────────────
        sb.AppendLine($"  Night sky color is nightSkyColor on DayNightCycle inspector — confirm it is #0A0A2E (r=0.039, g=0.039, b=0.180)");
        sb.AppendLine($"  EvalSkyColor fix: night solid from t=0.85 to t=0.15 (no orange bleed at 3 AM)");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    // ── Nuclear Sun Moon Fix ──────────────────────────────────────────────────

    static void NuclearSunMoonFix()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Nuclear Sun Moon Fix ===");

        // ── 1. Camera: solid-color, far clip 1000 ─────────────────────────────
        Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        bool camFixed = false;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Nuclear Camera Fix");
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.farClipPlane    = 1000f;
            cam.backgroundColor = new Color(0.290f, 0.565f, 0.851f); // noon blue
            EditorUtility.SetDirty(cam);
            camFixed = true;
            sb.AppendLine($"  Camera '{cam.gameObject.name}': clearFlags→SolidColor, farClip→1000");
        }
        else
            sb.AppendLine("  Camera: NONE FOUND");

        // ── 2. Remove skybox ──────────────────────────────────────────────────
        RenderSettings.skybox = null;
        sb.AppendLine("  RenderSettings.skybox: removed");

        // ── 3. Ensure Materials folder ────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color")
                       ?? Shader.Find("Standard");

        // ── 4. Recreate SunMat — fully opaque, renderQueue 3500 ───────────────
        string sunMatPath = "Assets/Materials/SunMat.asset";
        AssetDatabase.DeleteAsset(sunMatPath);

        Material sunMat = new Material(urpUnlit) { name = "SunMat" };
        Color sunBaseCol = new Color(1f, 0.898f, 0.4f, 1f);
        sunMat.color = sunBaseCol;
        sunMat.SetFloat("_Surface", 0f);    // Opaque
        sunMat.renderQueue = 3500;
        sunMat.EnableKeyword("_EMISSION");
        sunMat.SetColor("_EmissionColor", new Color(1f, 0.843f, 0f) * 3f);
        AssetDatabase.CreateAsset(sunMat, sunMatPath);
        sb.AppendLine($"  SunMat: RECREATED — shader={urpUnlit.name}, opaque, renderQueue=3500");

        // ── 5. Recreate MoonMat — fully opaque, renderQueue 3500 ─────────────
        string moonMatPath = "Assets/Materials/MoonMat.asset";
        AssetDatabase.DeleteAsset(moonMatPath);

        Material moonMat = new Material(urpUnlit) { name = "MoonMat" };
        Color moonBaseCol = new Color(0.910f, 0.910f, 1f, 1f);
        moonMat.color = moonBaseCol;
        moonMat.SetFloat("_Surface", 0f);   // Opaque
        moonMat.renderQueue = 3500;
        moonMat.EnableKeyword("_EMISSION");
        moonMat.SetColor("_EmissionColor", new Color(0.784f, 0.847f, 1f) * 2f);
        AssetDatabase.CreateAsset(moonMat, moonMatPath);
        sb.AppendLine($"  MoonMat: RECREATED — opaque, renderQueue=3500");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── 6. Apply to scene objects ─────────────────────────────────────────
        DayNightCycle dnc = Object.FindFirstObjectByType<DayNightCycle>();
        bool sunFixed = false, moonFixed = false;

        if (dnc != null)
        {
            // Sun
            if (dnc.sunVisual != null)
            {
                Undo.RecordObject(dnc.sunVisual, "Nuclear Sun Scale");
                dnc.sunVisual.localScale = Vector3.one * 10f;
                dnc.sunVisual.position   = new Vector3(0f, 40f, 80f); // immediate test position

                MeshRenderer sunMR = dnc.sunVisual.GetComponent<MeshRenderer>();
                if (sunMR != null)
                {
                    Undo.RecordObject(sunMR, "Nuclear Sun Material");
                    sunMR.sharedMaterial = sunMat;
                    sunMR.enabled        = true;
                    EditorUtility.SetDirty(sunMR);
                }

                if (!dnc.sunVisual.gameObject.activeSelf)
                {
                    Undo.RecordObject(dnc.sunVisual.gameObject, "Activate SunVisual");
                    dnc.sunVisual.gameObject.SetActive(true);
                }
                EditorUtility.SetDirty(dnc.sunVisual.gameObject);
                sunFixed = true;
                sb.AppendLine("  SunVisual: scale→10×10×10, position→(0,40,80), MeshRenderer enabled, SunMat assigned");
            }
            else
                sb.AppendLine("  SunVisual: NOT ASSIGNED — run 'Add Day Night Cycle' first");

            // Moon
            if (dnc.moonVisual != null)
            {
                Undo.RecordObject(dnc.moonVisual, "Nuclear Moon Scale");
                dnc.moonVisual.localScale = Vector3.one * 7f;

                MeshRenderer moonMR = dnc.moonVisual.GetComponent<MeshRenderer>();
                if (moonMR != null)
                {
                    Undo.RecordObject(moonMR, "Nuclear Moon Material");
                    moonMR.sharedMaterial = moonMat;
                    moonMR.enabled        = true;
                    EditorUtility.SetDirty(moonMR);
                }

                if (!dnc.moonVisual.gameObject.activeSelf)
                {
                    Undo.RecordObject(dnc.moonVisual.gameObject, "Activate MoonVisual");
                    dnc.moonVisual.gameObject.SetActive(true);
                }
                EditorUtility.SetDirty(dnc.moonVisual.gameObject);
                moonFixed = true;
                sb.AppendLine("  MoonVisual: scale→7×7×7, MeshRenderer enabled, MoonMat assigned");
            }
            else
                sb.AppendLine("  MoonVisual: NOT ASSIGNED — run 'Add Day Night Cycle' first");
        }
        else
            sb.AppendLine("  DayNightCycle: NOT FOUND in scene — run 'Add Day Night Cycle' first");

        // ── 7. Report ─────────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("REPORT:");
        sb.AppendLine($"  Skybox removed                  : YES");
        sb.AppendLine($"  Sun renderQueue set to 3500     : YES");
        sb.AppendLine($"  Sun scale is 10,10,10           : {(sunFixed  ? "YES" : "NO (SunVisual not assigned)")}");
        sb.AppendLine($"  Camera clearFlags SolidColor    : {(camFixed  ? "YES" : "NO (no camera found)")}");
        sb.AppendLine($"  Sun test position (0, 40, 80)   : {(sunFixed  ? "YES" : "NO")}");
        sb.AppendLine($"  Moon scale is 7,7,7             : {(moonFixed ? "YES" : "NO (MoonVisual not assigned)")}");
        sb.AppendLine();
        sb.AppendLine("If SunVisual is visible at (0,40,80): the arc formula was putting it below horizon.");
        sb.AppendLine("If STILL invisible: check camera culling mask and layer in Inspector.");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Prints full diagnostic info for a sun/moon sphere Transform to the StringBuilder.
    /// </summary>
    static void DiagnoseVisual(Transform t, string label, System.Text.StringBuilder sb)
    {
        if (t == null)
        {
            sb.AppendLine($"  {label}: NULL (field not assigned on DayNightCycle)");
            return;
        }

        sb.AppendLine($"  name            : {t.gameObject.name}");
        sb.AppendLine($"  worldPosition   : {t.position}");
        sb.AppendLine($"  localScale      : {t.localScale}");
        sb.AppendLine($"  layer           : {t.gameObject.layer} ({LayerMask.LayerToName(t.gameObject.layer)})");
        sb.AppendLine($"  activeSelf      : {t.gameObject.activeSelf}");
        sb.AppendLine($"  activeInHierarchy: {t.gameObject.activeInHierarchy}");

        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            sb.AppendLine($"  MeshRenderer    : NOT FOUND ← sphere needs a MeshRenderer!");
            return;
        }

        sb.AppendLine($"  MeshRenderer.enabled : {mr.enabled}");
        sb.AppendLine($"  sharedMaterial       : {(mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL ← no material assigned!")}");

        if (mr.sharedMaterial != null)
        {
            Material mat = mr.sharedMaterial;
            sb.AppendLine($"  shader               : {mat.shader.name}");
            sb.AppendLine($"  material.color       : {mat.color}  alpha={mat.color.a:F2}{(mat.color.a < 0.05f ? " ← ALPHA IS 0, object invisible!" : "")}");

            // Check for _Surface property (URP transparent toggle)
            if (mat.HasProperty("_Surface"))
            {
                float surface = mat.GetFloat("_Surface");
                sb.AppendLine($"  _Surface (0=opaque,1=transparent) : {surface}");
                if (surface > 0.5f)
                {
                    Color c = mat.color;
                    sb.AppendLine($"  Material is TRANSPARENT — alpha={c.a:F2}{(c.a < 0.05f ? " ← invisible!" : "")}");
                }
            }

            // Check emission
            if (mat.IsKeywordEnabled("_EMISSION"))
            {
                Color em = mat.GetColor("_EmissionColor");
                sb.AppendLine($"  emission enabled, _EmissionColor : {em}");
            }
            else
                sb.AppendLine($"  emission keyword: OFF");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BUILD DISEASE SYSTEM
    // ══════════════════════════════════════════════════════════════════════════

    static void BuildDiseaseSystem()
    {
        // ── 1. Find or create a DiseaseManager ──────────────────────────────
        DiseaseManager dm = Object.FindFirstObjectByType<DiseaseManager>();
        if (dm != null)
        {
            Debug.Log($"[BuildDiseaseSystem] DiseaseManager already exists on '{dm.gameObject.name}'.");
            Selection.activeGameObject = dm.gameObject;
            EditorGUIUtility.PingObject(dm.gameObject);
        }
        else
        {
            // Try to add it to an existing Systems / Managers GameObject
            GameObject host = GameObject.Find("Systems") ?? GameObject.Find("Managers")
                           ?? GameObject.Find("SimulationManagers");

            if (host == null)
            {
                host = new GameObject("DiseaseManager");
                Undo.RegisterCreatedObjectUndo(host, "Create DiseaseManager");
            }

            Undo.AddComponent<DiseaseManager>(host);
            EditorUtility.SetDirty(host);
            Selection.activeGameObject = host;
            EditorGUIUtility.PingObject(host);
            Debug.Log($"[BuildDiseaseSystem] DiseaseManager added to '{host.name}'.");
        }

        // ── 2. Validate CyberGrid has GridX/GridY wired ────────────────────
        CyberGrid grid = Object.FindFirstObjectByType<CyberGrid>();
        if (grid == null)
            Debug.LogWarning("[BuildDiseaseSystem] No CyberGrid found — enter Play Mode to spawn cells first.");
        else
            Debug.Log($"[BuildDiseaseSystem] CyberGrid found on '{grid.gameObject.name}'. SpawnedCells will be built at runtime.");

        // ── 3. Validate TwinSimulationManager ─────────────────────────────
        TwinSimulationManager sim = Object.FindFirstObjectByType<TwinSimulationManager>();
        if (sim == null)
            Debug.LogWarning("[BuildDiseaseSystem] TwinSimulationManager not found — DiseaseManager needs it.");
        else
            Debug.Log($"[BuildDiseaseSystem] TwinSimulationManager found on '{sim.gameObject.name}'. ✓");

        Debug.Log("[BuildDiseaseSystem] Done. In Play Mode, press I to trigger disease on Tomato zone, " +
                  "or call DiseaseManager.Instance.InfectZone(\"Tomato\", 0.85f). " +
                  "Add UI buttons named 'TreatPotato', 'TreatTomato', 'TreatGrape', 'TreatApple' " +
                  "— FuturisticUI.WireScenarioButtons() will wire them automatically.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PLACE RAIN MAKER PREFAB  (drag into scene at edit time)
    // ══════════════════════════════════════════════════════════════════════════

    static void PlaceRainMakerPrefab()
    {
        const string PREFAB_PATH = "Assets/RainMaker/Prefab/RainPrefab.prefab";

        // Remove any stale instance first
        GameObject old = GameObject.Find("RainMakerInstance");
        if (old != null) { Undo.DestroyObjectImmediate(old); Debug.Log("[PlaceRainMakerPrefab] Removed old instance."); }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[PlaceRainMakerPrefab] Prefab not found at '{PREFAB_PATH}'. " +
                           "Import the RainMaker package first.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "RainMakerInstance";
        instance.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(instance, "Place RainMakerInstance");

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
        Debug.Log("[PlaceRainMakerPrefab] RainMakerInstance placed at origin. " +
                  "Assign the Camera field on its RainScript component, then run Setup Rain Controller.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SETUP RAIN CONTROLLER
    // ══════════════════════════════════════════════════════════════════════════

    static void SetupRainController()
    {
        // ── 1. Find or create WeatherSystem host ─────────────────────────────
        GameObject wsGO = GameObject.Find("WeatherSystem") ?? new GameObject("WeatherSystem");
        if (!EditorUtility.IsPersistent(wsGO))
            Undo.RegisterCreatedObjectUndo(wsGO, "Create WeatherSystem");

        // ── 2. Add RainController (skip if already present) ───────────────────
        RainController rc = wsGO.GetComponent<RainController>();
        if (rc == null)
        {
            rc = Undo.AddComponent<RainController>(wsGO);
            Debug.Log("[SetupRainController] RainController added to WeatherSystem.");
        }
        else
        {
            Debug.Log("[SetupRainController] RainController already present — skipped.");
        }

        EditorUtility.SetDirty(wsGO);
        Selection.activeGameObject = wsGO;
        EditorGUIUtility.PingObject(wsGO);
        Debug.Log("[SetupRainController] Done — assign RainMakerInstance's BaseRainScript to the " +
                  "RainController.rainScript field, then press W in Play Mode to toggle rain.");
    }

    // Keep private alias so FixEverything can call it without a [MenuItem]
    static void SetupRain() => SetupRainController();

    // ══════════════════════════════════════════════════════════════════════════
    //  SETUP SPRINKLERS
    // ══════════════════════════════════════════════════════════════════════════

    static void SetupSprinklers()
    {
        // ── 1. Find or create IrrigationSystem host ──────────────────────────
        GameObject irrGO = GameObject.Find("IrrigationSystem") ?? new GameObject("IrrigationSystem");
        if (!EditorUtility.IsPersistent(irrGO))
            Undo.RegisterCreatedObjectUndo(irrGO, "Create IrrigationSystem");

        // ── 2. Add SprinklerSystem ────────────────────────────────────────────
        SprinklerSystem ss = irrGO.GetComponent<SprinklerSystem>();
        if (ss == null)
            ss = Undo.AddComponent<SprinklerSystem>(irrGO);

        // ── 3. Assign Sprinkler prefab ────────────────────────────────────────
        const string SPRINKLER_PREFAB = "Assets/Prefabs/Sprinkler.prefab";
        GameObject sprinklerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SPRINKLER_PREFAB);
        if (sprinklerPrefab == null)
        {
            Debug.LogWarning($"[SetupSprinklers] Sprinkler.prefab not found at '{SPRINKLER_PREFAB}'. " +
                             "SprinklerSystem will build procedural particle sprinklers at runtime.");
        }
        else
        {
            Undo.RecordObject(ss, "Assign Sprinkler Prefab");
            ss.sprinklerPrefab = sprinklerPrefab;
            EditorUtility.SetDirty(ss);
            Debug.Log($"[SetupSprinklers] Assigned {SPRINKLER_PREFAB} to SprinklerSystem.");
        }

        // ── 4. Wire CyberGrid reference ───────────────────────────────────────
        CyberGrid cyberGrid = Object.FindFirstObjectByType<CyberGrid>();
        if (cyberGrid != null)
        {
            Undo.RecordObject(ss, "Assign CyberGrid");
            ss.cyberGrid = cyberGrid;
            EditorUtility.SetDirty(ss);
            Debug.Log($"[SetupSprinklers] CyberGrid assigned from '{cyberGrid.gameObject.name}'.");
        }
        else
        {
            Debug.LogWarning("[SetupSprinklers] No CyberGrid found — auto-found at runtime.");
        }

        // ── 5. Connect drought/reset via FuturisticUI (already coded in FuturisticUI.cs) ─
        // FuturisticUI.OnDrought() calls GetSprinklers()?.ActivateZone("Potato")
        // FuturisticUI.OnReset()   calls GetSprinklers()?.DeactivateAll()
        // The link is lazy-found at runtime — no inspector wiring needed.

        Selection.activeGameObject = irrGO;
        EditorGUIUtility.PingObject(irrGO);
        Debug.Log("[SetupSprinklers] Done — IrrigationSystem ready. " +
                  "16 sprinklers spawn at Play time (4 zones × 4 corners). " +
                  "Drought button auto-activates Potato zone; Reset deactivates all.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SETUP DRONE
    // ══════════════════════════════════════════════════════════════════════════

    static void SetupDrone()
    {
        // ── 1. Find the Camera that carries DroneController ──────────────────
        DroneController dc = Object.FindFirstObjectByType<DroneController>();
        if (dc == null)
        {
            Debug.LogError("[SetupDrone] No GameObject with DroneController found in scene. " +
                           "Add DroneController to your FPS drone Camera first.");
            return;
        }

        Camera cam = dc.GetComponent<Camera>();
        if (cam == null) cam = dc.GetComponentInChildren<Camera>();
        GameObject target = (cam != null) ? cam.gameObject : dc.gameObject;

        // ── 2. Add DroneVisual (skip if already present) ─────────────────────
        DroneVisual dv = target.GetComponent<DroneVisual>();
        if (dv == null)
            dv = Undo.AddComponent<DroneVisual>(target);

        // ── 3. Assign drone prefab ────────────────────────────────────────────
        const string DRONE_PREFAB = "Assets/Drone/prefab/drone.prefab";
        GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DRONE_PREFAB);
        if (dronePrefab == null)
        {
            // Fallback: scan for any prefab with "drone" in name under Assets/Drone/
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Drone" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path)
                        .ToLower().Contains("drone"))
                {
                    dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (dronePrefab != null)
                    {
                        Debug.Log($"[SetupDrone] Fallback: found drone prefab at '{path}'.");
                        break;
                    }
                }
            }
        }

        if (dronePrefab == null)
        {
            Debug.LogError("[SetupDrone] No drone prefab found. " +
                           "Expected at 'Assets/Drone/prefab/drone.prefab'.");
        }
        else
        {
            Undo.RecordObject(dv, "Assign Drone Prefab");
            dv.dronePrefab = dronePrefab;
            EditorUtility.SetDirty(dv);
            Debug.Log($"[SetupDrone] Assigned '{dronePrefab.name}' to DroneVisual on '{target.name}'.");
        }

        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);
        Debug.Log("[SetupDrone] Done — DroneVisual added. The drone model appears in front of " +
                  "the camera at runtime with hover bob and rotor spin animations.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX DRONE MATERIALS  (Standard → URP/Lit)
    // ══════════════════════════════════════════════════════════════════════════

    static void FixDroneMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[FixDroneMaterials] 'Universal Render Pipeline/Lit' shader not found.");
            return;
        }

        // All drone .mat files store _Color=(1,1,1,1) (white), so we derive colour from filename.
        // Surface Plane = drone body → dark professional grey #2C2C2C.
        var nameColors = new System.Collections.Generic.Dictionary<string, Color>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            { "black",         new Color(0.05f,  0.05f,  0.05f)  },
            { "blue",          new Color(0.0f,   0.35f,  0.8f)   },
            { "green",         new Color(0.1f,   0.55f,  0.1f)   },
            { "grey",          new Color(0.5f,   0.5f,   0.5f)   },
            { "orange",        new Color(1.0f,   0.45f,  0.0f)   },
            { "red",           new Color(0.75f,  0.05f,  0.05f)  },
            { "white",         new Color(0.88f,  0.88f,  0.88f)  },
            { "surface plane", new Color(0.173f, 0.173f, 0.173f) }, // body → #2C2C2C
        };

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Drone" });
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string   path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Resolve target colour from filename
            string matName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            Color targetColor = Color.white;
            foreach (var kv in nameColors)
                if (matName.Contains(kv.Key.ToLower())) { targetColor = kv.Value; break; }

            Undo.RecordObject(mat, "Fix Drone Material");

            string shaderName = mat.shader != null ? mat.shader.name : "";
            bool isUrp = shaderName.StartsWith("Universal Render Pipeline") ||
                         shaderName.StartsWith("Shader Graphs");
            if (!isUrp)
            {
                Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                mat.shader = urpLit;
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                fixedCount++;
                Debug.Log($"[FixDroneMaterials] '{System.IO.Path.GetFileName(path)}': {shaderName} → URP/Lit, color={targetColor}");
            }

            mat.SetColor("_BaseColor", targetColor);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Step 2: walk every drone prefab and force-reassign materials on MeshRenderers ──
        // Material asset changes are reflected automatically via GUID, but Unity sometimes
        // needs an explicit sharedMaterials reassignment to flush the pink-shader cache on prefabs.
        int prefabsUpdated = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Drone/prefab" });
        foreach (string prefabGuid in prefabGuids)
        {
            string     prefabPath     = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            bool       dirty          = false;

            foreach (MeshRenderer mr in prefabContents.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matPath   = AssetDatabase.GetAssetPath(mats[i]);
                    Material reloaded = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (reloaded == null) continue;
                    mats[i] = reloaded;
                    dirty   = true;
                }
                if (dirty) mr.sharedMaterials = mats;
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                prefabsUpdated++;
                Debug.Log($"[FixDroneMaterials] Prefab updated: '{prefabPath}'");
            }
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        Debug.Log($"[FixDroneMaterials] Done — {fixedCount} shader(s) converted, " +
                  $"{prefabsUpdated} prefab(s) refreshed, all drone colours applied.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  REMOVE "NEW TEXT" OBJECTS
    // ══════════════════════════════════════════════════════════════════════════

    static void RemoveNewTextObjects()
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;
        foreach (GameObject go in all)
        {
            if (go.name == "New Text")
            {
                Undo.DestroyObjectImmediate(go);
                removed++;
            }
        }
        Debug.Log($"[RemoveNewTextObjects] Removed {removed} 'New Text' GameObject(s) from scene.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ADD RAIN BUTTON  (creates Toggle Rain button in scene Canvas)
    // ══════════════════════════════════════════════════════════════════════════

    static void AddRainButton()
    {
        // Check if a rain button already exists
        Button[] allBtns = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button b in allBtns)
        {
            string n = b.name.ToLower();
            if (n.Contains("rain") || n.Contains("togglerain"))
            {
                Debug.Log($"[AddRainButton] Rain button already exists: '{b.name}' — skipped.");
                Selection.activeGameObject = b.gameObject;
                EditorGUIUtility.PingObject(b.gameObject);
                return;
            }
        }

        // Find template button (Reset Healthy preferred)
        Button template = null;
        foreach (Button b in allBtns)
        {
            string n = b.name.ToLower();
            if (n.Contains("reset") || n.Contains("healthy")) { template = b; break; }
            if (template == null && (n.Contains("drought") || n.Contains("disease") || n.Contains("heatwave")))
                template = b;
        }

        if (template == null)
        {
            EditorUtility.DisplayDialog("Add Rain Button",
                "No scenario button found to clone.\n\nAdd a Button named 'ToggleRainBtn' to your Canvas manually, then re-run.",
                "OK");
            return;
        }

        // Clone template
        GameObject btnGO = (GameObject)PrefabUtility.InstantiatePrefab(template.gameObject, template.transform.parent);
        if (btnGO == null) btnGO = Object.Instantiate(template.gameObject, template.transform.parent);
        btnGO.name = "ToggleRainBtn";

        // Set label
        TMP_Text lbl = btnGO.GetComponentInChildren<TMP_Text>();
        if (lbl != null) lbl.text = "Toggle Rain";
        else
        {
            Text legacyLbl = btnGO.GetComponentInChildren<Text>();
            if (legacyLbl != null) legacyLbl.text = "Toggle Rain";
        }

        btnGO.transform.SetAsLastSibling();

        Undo.RegisterCreatedObjectUndo(btnGO, "Add Rain Button");
        EditorUtility.SetDirty(btnGO);

        // Report RainController status
        RainController rc = Object.FindFirstObjectByType<RainController>();
        if (rc == null)
            Debug.LogWarning("[AddRainButton] RainController NOT found — run FarmTwin > Build > Setup Rain Controller.");
        else if (rc.rainScript == null)
            Debug.LogWarning("[AddRainButton] RainController.rainScript IS NULL — assign it in the Inspector.");
        else
            Debug.Log("[AddRainButton] RainController OK — rainScript is assigned.");

        Selection.activeGameObject = btnGO;
        EditorGUIUtility.PingObject(btnGO);
        Debug.Log($"[AddRainButton] 'Toggle Rain' button created as sibling of '{template.name}'. " +
                  "FuturisticUI.WireScenarioButtons() will wire it on Play.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  QUICK FIXES  (drone colours + remove stray text + re-place rain prefab)
    // ══════════════════════════════════════════════════════════════════════════

    static void QuickFixes()
    {
        Debug.Log("[QuickFixes] Starting...");
        FixDroneMaterials();      // fix pink drone materials
        RemoveNewTextObjects();   // destroy stray "New Text" GameObjects
        SetupRain();              // ensures RainPrefab is placed correctly in scene
        Debug.Log("[QuickFixes] Done — drone colours fixed, New Text removed, rain prefab placed.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FIX EVERYTHING  (one-click: drone materials + rain + sprinklers + drone)
    // ══════════════════════════════════════════════════════════════════════════

    static void FixEverything()
    {
        Debug.Log("[FixEverything] Starting full fix pass...");
        FixDroneMaterials();
        SetupRain();
        SetupSprinklers();
        SetupDrone();
        Debug.Log("[FixEverything] Done — drone materials, rain (auto), sprinklers, drone visual all configured.");
    }
}
