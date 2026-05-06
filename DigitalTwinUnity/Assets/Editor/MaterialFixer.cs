using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class MaterialFixer
{
    // Every folder that contains asset-pack materials
    static readonly string[] ASSET_FOLDERS = {
        "Assets/Pandazole_Lowpoly_Asset_Bundle",
        "Assets/Pandazole_Ultimate_Pack",
        "Assets/PolyOne",
        "Assets/51+ LowPolyTrees",
        "Assets/Drone",
        "Assets/ithappy",
        "Assets/Gridness Studios",
        "Assets/Low Poly Fruits",
        "Assets/UrsaAnimation",
        "Assets/RainMaker",
        "Assets/Materials",
        "Assets/Prefabs",
        "Assets/Resources",
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // ONE BUTTON — relinks broken textures + fixes trees + drone + scene
    // Run once in Edit mode (NOT play mode), then Ctrl+S.
    // ═══════════════════════════════════════════════════════════════════════════
    [MenuItem("FarmTwin/★ FIX EVERYTHING PINK ★")]
    public static void FixEverythingPink()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Fix Everything Pink", "Step 1/5 – Relink broken textures...", 0.0f);
            _RelinkBrokenTextures();

            EditorUtility.DisplayProgressBar("Fix Everything Pink", "Step 2/5 – Tree prefabs...", 0.20f);
            _FixTreePrefabs();

            EditorUtility.DisplayProgressBar("Fix Everything Pink", "Step 3/5 – Drone materials...", 0.50f);
            _FixDrone();

            EditorUtility.DisplayProgressBar("Fix Everything Pink", "Step 4/5 – All scene Standard shaders...", 0.70f);
            _FixSceneStandard();

            EditorUtility.DisplayProgressBar("Fix Everything Pink", "Step 5/5 – Save...", 0.90f);
            EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }

        EditorUtility.DisplayDialog("Done", "All pink/beige fixed.\nSave scene: Ctrl+S", "OK");
    }

    // ── step 1: relink materials whose _BaseMap GUID broke (deleted .meta files) ─
    // For each pack material with a missing texture: search the same pack sub-folder
    // for any Texture2D and link it. Same fix that made the farm field work.
    static void _RelinkBrokenTextures()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");

        // Each entry: top-level pack folder → the one texture atlas name to look for
        var packTexture = new Dictionary<string, string> {
            { "Assets/Pandazole_Lowpoly_Asset_Bundle", "PandaMat" },
            { "Assets/Pandazole_Ultimate_Pack",        "PandaMat" },
            { "Assets/ithappy",                        "Texture_1" },
            { "Assets/Gridness Studios",               "GridnessColorPack" },
            { "Assets/Low Poly Fruits",                "Fruits_Texture" },
            { "Assets/UrsaAnimation",                  "GoatSheep" },
        };

        int fixed2 = 0;
        foreach (var kv in packTexture)
        {
            string packFolder = kv.Key;

            // Build a map: sub-pack folder prefix → first texture found in its Textures dir
            // Strategy: for each material, search its own sub-folder tree for any Texture2D
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { packFolder });
            foreach (string mg in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(mg);
                Material mat   = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                // Upgrade shader if needed
                if (urp != null && mat.shader != null &&
                    (mat.shader.name == "Standard" || mat.shader.name.StartsWith("Legacy")))
                { mat.shader = urp; EditorUtility.SetDirty(mat); }

                // Skip if texture already linked
                if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) continue;

                // Find the nearest sub-pack folder (2 levels up from material file)
                string matDir   = System.IO.Path.GetDirectoryName(matPath).Replace('\\', '/');
                string parent   = System.IO.Path.GetDirectoryName(matDir).Replace('\\', '/');
                string grandpar = System.IO.Path.GetDirectoryName(parent).Replace('\\', '/');

                // Search for textures in the sub-pack folder (same level as Materials folder)
                Texture2D tex = null;
                foreach (string searchRoot in new[]{ matDir, parent, grandpar })
                {
                    if (!searchRoot.StartsWith("Assets")) continue;
                    string[] tgs = AssetDatabase.FindAssets("t:Texture2D", new[]{ searchRoot });
                    foreach (string tg in tgs)
                    {
                        string tp = AssetDatabase.GUIDToAssetPath(tg);
                        if (!tp.StartsWith(packFolder)) continue;
                        // Prefer the named atlas, but accept any texture in the sub-pack
                        string tn = System.IO.Path.GetFileNameWithoutExtension(tp);
                        if (tn.IndexOf(kv.Value, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                            break;
                        }
                    }
                    if (tex != null) break;
                    // Second pass: accept ANY texture in the sub-pack folder
                    foreach (string tg in AssetDatabase.FindAssets("t:Texture2D", new[]{ searchRoot }))
                    {
                        string tp = AssetDatabase.GUIDToAssetPath(tg);
                        if (!tp.StartsWith(packFolder)) continue;
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                        if (tex != null) break;
                    }
                    if (tex != null) break;
                }

                if (tex != null)
                {
                    mat.SetTexture("_BaseMap",  tex);
                    mat.SetTexture("_MainTex",   tex);
                    mat.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(mat);
                    fixed2++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MaterialFixer] Relinked textures on {fixed2} material(s).");
    }

    // ── step 2: patch every tree prefab asset to use Gradient Pallete256.mat ──
    static void _FixTreePrefabs()
    {
        const string matPath      = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.mat";
        const string prefabFolder = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Prefab";
        const string treeFbxPath  = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/BundleLowPolyTreesV1.fbx";

        // Ensure gradient mat is URP/Lit with texture
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        Texture2D grad = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.png");
        Material treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (treeMat == null) { treeMat = new Material(urp); AssetDatabase.CreateAsset(treeMat, matPath); }
        if (urp != null) treeMat.shader = urp;
        if (grad != null) { treeMat.SetTexture("_BaseMap", grad); treeMat.SetTexture("_MainTex", grad); }
        treeMat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(treeMat);
        AssetDatabase.SaveAssets();
        treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath); // reload to get stable ref

        // Patch prefab assets
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != treeMat) { mats[i] = treeMat; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Patch scene instances whose mesh comes from the tree FBX
        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (AssetDatabase.GetAssetPath(mf.sharedMesh) != treeFbxPath) continue;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != treeMat) { mats[i] = treeMat; changed = true; }
            if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r.gameObject); }
        }
    }

    // ── step 2: drone materials ────────────────────────────────────────────────
    static void _FixDrone()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;
        var nameColors = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "black",  new Color(0.05f, 0.05f, 0.05f) },
            { "blue",   new Color(0.00f, 0.35f, 0.80f) },
            { "green",  new Color(0.10f, 0.55f, 0.10f) },
            { "grey",   new Color(0.50f, 0.50f, 0.50f) },
            { "orange", new Color(1.00f, 0.45f, 0.00f) },
            { "red",    new Color(0.75f, 0.05f, 0.05f) },
            { "white",  new Color(0.88f, 0.88f, 0.88f) },
            { "surface plane", new Color(0.17f, 0.17f, 0.17f) },
        };
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Drone" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            string n = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            Color col = new Color(0.88f, 0.88f, 0.88f);
            foreach (var kv in nameColors) if (n.Contains(kv.Key)) { col = kv.Value; break; }
            mat.shader = urp;
            mat.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(mat);
        }
        // Fix scene instances — load the actual material assets and force-apply to scene renderers
        // whose parent chain contains "Drone" or "drone"
        Material redMat   = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drone/Art/material/Red.mat");
        Material greyMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drone/Art/material/grey.mat");
        Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drone/Art/material/White.mat");
        Material blackMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drone/Art/material/black.mat");

        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            // Check if this renderer is part of the drone hierarchy
            Transform t = r.transform;
            bool isDrone = false;
            while (t != null) { if (t.name.ToLower().Contains("drone")) { isDrone = true; break; } t = t.parent; }
            if (!isDrone) continue;

            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string sn = mats[i].shader != null ? mats[i].shader.name : "";
                // Fix any material still on Standard shader or showing pink (magenta)
                bool isPink = mats[i].HasProperty("_BaseColor") &&
                              mats[i].GetColor("_BaseColor").b > 0.5f &&
                              mats[i].GetColor("_BaseColor").r > 0.5f &&
                              mats[i].GetColor("_BaseColor").g < 0.3f;
                if (sn == "Standard" || sn.StartsWith("Legacy") || isPink)
                {
                    // Use red for body parts, grey for arms
                    string rName = r.gameObject.name.ToLower();
                    if (rName.Contains("body") || rName.Contains("cap") || rName.Contains("top"))
                        mats[i] = redMat ?? mats[i];
                    else if (rName.Contains("arm") || rName.Contains("leg") || rName.Contains("frame"))
                        mats[i] = greyMat ?? mats[i];
                    else
                        mats[i] = greyMat ?? mats[i];
                    changed = true;
                }
            }
            if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r.gameObject); }
        }
        AssetDatabase.SaveAssets();
    }

    // ── step 3: ONLY fix truly broken (pink/error-shader) materials in scene ─────
    // Does NOT touch Standard-shader materials that have a valid texture — those
    // are asset-pack materials that just haven't been upgraded yet; touching them
    // wipes the color to white because Standard _Color defaults to white.
    static void _FixSceneStandard()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;
        int count = 0;
        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            bool dirty = false;
            foreach (Material mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                string sn = mat.shader != null ? mat.shader.name : "";

                // Only fix the error shader (shows pink) — skip valid Standard/Legacy
                // materials because they likely have textures that would be lost.
                if (sn != "Hidden/InternalErrorShader") continue;

                mat.shader = urp;
                mat.SetColor("_BaseColor", new Color(0.82f, 0.75f, 0.60f));
                EditorUtility.SetDirty(mat);
                dirty = true;
                count++;
            }
            if (dirty) EditorUtility.SetDirty(r.gameObject);
        }
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log($"[MaterialFixer] Fixed {count} error-shader material(s) in scene.");
    }

    // ── Main fix ──────────────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Fix All White & Pink Materials")]
    public static void FixAllMaterials()
    {
        int total = 0;

        EditorUtility.DisplayProgressBar("Material Fixer", "Scanning asset pack folders...", 0f);

        try
        {
            // Step 1 — reimport every material in every known asset-pack folder
            for (int fi = 0; fi < ASSET_FOLDERS.Length; fi++)
            {
                string folder = ASSET_FOLDERS[fi];
                EditorUtility.DisplayProgressBar("Material Fixer",
                    $"Reimporting {folder}...", (float)fi / ASSET_FOLDERS.Length);

                // Materials
                string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { folder });
                foreach (string g in matGuids)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g),
                        ImportAssetOptions.ForceUpdate);
                    total++;
                }

                // Textures — reimport so material references restore
                string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                foreach (string g in texGuids)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g),
                        ImportAssetOptions.ForceUpdate);
                    total++;
                }

                // Prefabs — so instantiated scene GOs pick up restored materials
                string[] prefGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                foreach (string g in prefGuids)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g),
                        ImportAssetOptions.ForceUpdate);
                    total++;
                }
            }

            EditorUtility.DisplayProgressBar("Material Fixer", "Refreshing database...", 0.95f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[MaterialFixer] Done — reimported {total} assets across all pack folders.");
        EditorUtility.DisplayDialog("Material Fixer",
            $"Reimported {total} assets (materials, textures, prefabs).\n\n" +
            "Now run:\n  FarmTwin → Build → Farm Props Builder → Build/Rebuild Props\n" +
            "Then press Play.", "OK");
    }

    // ── Crop food prefab materials ────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Fix Crop Food Materials (Pandazole Farm Ranch)")]
    public static void FixCropFoodMaterials()
    {
        // Food prefabs (food_Potato, food_Tomato, food_Grape, food_Apple, …)
        // live in Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack.
        // Their PandaMat.mat must reference the texture atlas or crops show white.
        string[] matFolders = {
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack",
            "Assets/Pandazole_Lowpoly_Asset_Bundle/Pandazole Farm Ranch Pack",
        };

        int count = 0;
        foreach (string folder in matFolders)
        {
            // Force-reimport textures first, then materials, then prefabs
            foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
                count++;
            }
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log($"[MaterialFixer] Reimported {count} Farm Ranch material(s). Crop food prefabs should now show atlas colours.");
        EditorUtility.DisplayDialog("Crop Materials Fixed",
            $"Reimported {count} Farm Ranch material(s) with texture atlas.\nPress Play to see coloured crops.", "OK");
    }

    // ── Sprinkler ─────────────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Rebuild Sprinkler Material")]
    public static void RebuildSprinklerMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("SprinklerParticle");
        foreach (string g in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g),
                ImportAssetOptions.ForceUpdate);

        AssetDatabase.Refresh();
        SprinklerPrefabBuilder.BuildPrefab();
        Debug.Log("[MaterialFixer] Sprinkler prefab rebuilt with URP shader.");
    }

    // ── Drone materials ───────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Fix Drone Materials")]
    public static void FixDroneMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) { Debug.LogError("[MaterialFixer] URP/Lit shader not found."); return; }

        var nameColors = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "black",         new Color(0.05f,  0.05f,  0.05f)  },
            { "blue",          new Color(0.00f,  0.35f,  0.80f)  },
            { "green",         new Color(0.10f,  0.55f,  0.10f)  },
            { "grey",          new Color(0.50f,  0.50f,  0.50f)  },
            { "orange",        new Color(1.00f,  0.45f,  0.00f)  },
            { "red",           new Color(0.75f,  0.05f,  0.05f)  },
            { "white",         new Color(0.88f,  0.88f,  0.88f)  },
            { "surface plane", new Color(0.17f,  0.17f,  0.17f)  },
        };

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Drone" });
        int fixed2 = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string matName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            Color col = new Color(0.88f, 0.88f, 0.88f);
            foreach (var kv in nameColors)
                if (matName.Contains(kv.Key.ToLower())) { col = kv.Value; break; }

            mat.shader = urpLit;
            mat.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(mat);
            fixed2++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MaterialFixer] Fixed {fixed2} drone material(s).");
        EditorUtility.DisplayDialog("Drone Materials Fixed",
            $"Fixed {fixed2} drone material(s) → URP/Lit with correct colours.\nPress Play to verify.", "OK");
    }

    // ── Pandazole colour patch (workaround for missing texture atlas) ─────────

    [MenuItem("FarmTwin/Fix/Patch Pandazole Building Colours")]
    public static void PatchPandazoleColours()
    {
        // Each PandaMat in a different sub-pack gets a distinct low-poly colour
        // so buildings aren't pure white when the texture atlas PNG is missing.
        var packColours = new Dictionary<string, Color>
        {
            { "City Town",     new Color(0.85f, 0.78f, 0.62f) }, // warm sandstone
            { "Nature",        new Color(0.42f, 0.62f, 0.38f) }, // foliage green
            { "Survival",      new Color(0.55f, 0.42f, 0.30f) }, // earthy brown
            { "Home Interior", new Color(0.90f, 0.87f, 0.80f) }, // off-white interior
            { "Kitchen",       new Color(0.94f, 0.88f, 0.75f) }, // cream
            { "Ultimate",      new Color(0.80f, 0.72f, 0.58f) }, // tan
        };

        string[] guids = AssetDatabase.FindAssets("t:Material",
            new[] { "Assets/Pandazole_Lowpoly_Asset_Bundle", "Assets/Pandazole_Ultimate_Pack" });

        int patched = 0;
        foreach (string guid in guids)
        {
            string   path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Pick colour based on which sub-pack the .mat lives in
            Color col = new Color(0.82f, 0.75f, 0.60f); // default warm tan
            foreach (var kv in packColours)
                if (path.Contains(kv.Key)) { col = kv.Value; break; }

            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null) mat.shader = urp;

            mat.SetColor("_BaseColor", col);
            mat.SetTexture("_BaseMap",  null); // clear broken texture ref
            EditorUtility.SetDirty(mat);
            patched++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MaterialFixer] Patched {patched} Pandazole material(s) with flat colours.");
        EditorUtility.DisplayDialog("Pandazole Patched",
            $"Set flat colours on {patched} Pandazole material(s).\n\n" +
            "For original colours: Window → Package Manager → My Assets → re-import Pandazole.", "OK");
    }

    // ── Missing scripts ───────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Remove Missing Scripts from Scene")]
    public static void RemoveMissingScripts()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid()) continue;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[MaterialFixer] Missing script components removed.");
        EditorUtility.DisplayDialog("Done",
            "Missing scripts removed.\nSave scene (Ctrl+S).", "OK");
    }

    // ── Nuke all Standard-shader materials in the scene (catches any remaining pink) ──
    [MenuItem("FarmTwin/Fix/Fix ALL Pink in Scene (Standard → URP)")]
    public static void FixAllPinkInScene()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogError("[MaterialFixer] URP/Lit not found."); return; }

        const string treeFbxPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/BundleLowPolyTreesV1.fbx";
        const string treeMatPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.mat";
        Material treeMat = AssetDatabase.LoadAssetAtPath<Material>(treeMatPath);

        int fixed2 = 0;
        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            bool dirty = false;
            Material[] mats = r.sharedMaterials;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                // Is this renderer using a mesh from the tree FBX?
                MeshFilter mf = r.GetComponent<MeshFilter>();
                bool isTreeMesh = mf != null && mf.sharedMesh != null &&
                                  AssetDatabase.GetAssetPath(mf.sharedMesh) == treeFbxPath;

                if (isTreeMesh && treeMat != null && m != treeMat)
                {
                    mats[i] = treeMat;
                    dirty = true;
                }
                else if (!isTreeMesh && m.shader != null &&
                         (m.shader.name == "Standard" || m.shader.name.StartsWith("Legacy")))
                {
                    // Standard shader → replace with URP copy of same colour
                    Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    Material fresh = new Material(urp);
                    fresh.name = m.name;
                    fresh.SetColor("_BaseColor", col);
                    fresh.SetColor("_Color", col);
                    mats[i] = fresh;
                    dirty = true;
                }
            }

            if (dirty)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r.gameObject);
                fixed2++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        Debug.Log($"[MaterialFixer] Fixed {fixed2} renderer(s) with Standard/Legacy shaders in scene.");
        EditorUtility.DisplayDialog("Scene Fixed",
            $"Replaced Standard/Legacy shader on {fixed2} renderer(s) → URP/Lit.\n\nSave scene (Ctrl+S).", "OK");
    }

    // ── Tree materials ────────────────────────────────────────────────────────

    // ── Fix tree PREFABS (the actual fix — patches baked material in each prefab) ─
    [MenuItem("FarmTwin/Fix/Fix Pink Trees (Patch Prefabs)")]
    public static void FixTreePrefabMaterials()
    {
        const string matPath     = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.mat";
        const string prefabFolder = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Prefab";

        Material treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (treeMat == null)
        {
            Debug.LogError($"[MaterialFixer] Gradient Pallete256.mat not found at {matPath}. Run 'Fix Tree Materials (URP Gradient)' first.");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        int patched = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // Edit the prefab asset directly
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            foreach (MeshRenderer r in prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    // Replace any material that is NOT already our gradient mat
                    if (mats[i] == null || mats[i] != treeMat)
                    {
                        mats[i] = treeMat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                patched++;
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Fix ALL scene instances that reference any mesh from the tree FBX
        // (covers overrides baked directly on scene objects, not just FarmDecor)
        int sceneFixed = 0;
        const string treeFbxPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/BundleLowPolyTreesV1.fbx";

        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            string meshAssetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (meshAssetPath != treeFbxPath) continue;

            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i] != treeMat)
                { mats[i] = treeMat; changed = true; }
            }
            if (changed)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r.gameObject);
                sceneFixed++;
            }
        }

        if (sceneFixed > 0) EditorSceneManager.MarkAllScenesDirty();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MaterialFixer] Patched {patched} tree prefab(s), fixed {sceneFixed} scene renderer(s).");
        EditorUtility.DisplayDialog("Tree Prefabs Patched",
            $"Patched {patched} tree prefab(s) + {sceneFixed} scene renderer(s) → Gradient Pallete256.mat.\n\nSave scene (Ctrl+S).", "OK");
    }

    [MenuItem("FarmTwin/Fix/Fix Tree Materials (URP Gradient)")]
    public static void FixTreeMaterials()
    {
        const string fbxPath  = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/BundleLowPolyTreesV1.fbx";
        const string gradPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.png";
        // Unity's materialName:0 (By Base Texture Name) looks for a .mat whose filename
        // matches the texture name — must be exactly "Gradient Pallete256.mat"
        const string matPath  = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.mat";

        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogError("[MaterialFixer] URP/Lit not found."); return; }

        Texture2D grad = AssetDatabase.LoadAssetAtPath<Texture2D>(gradPath);
        if (grad == null) { Debug.LogError($"[MaterialFixer] Gradient texture missing at {gradPath}"); return; }

        // Create or update "Gradient Pallete256.mat" — the name Unity auto-searches for
        Material treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (treeMat == null)
        {
            treeMat = new Material(urp);
            AssetDatabase.CreateAsset(treeMat, matPath);
            AssetDatabase.SaveAssets();
            treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
        treeMat.shader = urp;
        treeMat.SetTexture("_BaseMap", grad);
        treeMat.SetTexture("_MainTex",  grad);
        treeMat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(treeMat);
        AssetDatabase.SaveAssets();

        // Also add explicit remap on the FBX importer for every embedded material
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            bool remapped = false;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is Material embMat)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embMat), treeMat);
                    Debug.Log($"[MaterialFixer] Remapped '{embMat.name}' → Gradient Pallete256.mat");
                    remapped = true;
                }
            }
            // Force reimport so the remap + new material file are both picked up
            importer.SaveAndReimport();
            if (!remapped)
                Debug.Log("[MaterialFixer] No embedded mats in FBX — Unity will find mat by name on next import.");
        }

        // Force-reimport the gradient texture so its GUID resolves correctly
        AssetDatabase.ImportAsset(gradPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("[MaterialFixer] Tree material created/updated. Trees should now show gradient colours.");
        EditorUtility.DisplayDialog("Tree Materials Fixed",
            "Created 'Gradient Pallete256.mat' with URP/Lit + gradient texture.\n" +
            "FBX reimported. Trees in scene should now be coloured.\n\nIf still pink, press Play.", "OK");
    }

    // ── Rain materials ────────────────────────────────────────────────────────

    [MenuItem("FarmTwin/Fix/Fix Rain Materials (URP Particles)")]
    public static void FixRainMaterials()
    {
        Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleUnlit == null) particleUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (particleUnlit == null) { Debug.LogError("[MaterialFixer] URP Particles/Unlit shader not found."); return; }

        Texture2D rainTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RainMaker/Prefab/RainTexture.png");

        var rainMats = new (string path, Color tint, float alpha)[]
        {
            ("Assets/RainMaker/Prefab/RainMaterial.mat",          new Color(0.75f, 0.90f, 1.00f), 0.35f),
            ("Assets/RainMaker/Prefab/RainMaterial2D.mat",        new Color(0.75f, 0.90f, 1.00f), 0.35f),
            ("Assets/RainMaker/Prefab/RainExplosionMaterial.mat",  new Color(0.80f, 0.92f, 1.00f), 0.50f),
            ("Assets/RainMaker/Prefab/RainExplosionMaterial2D.mat",new Color(0.80f, 0.92f, 1.00f), 0.50f),
            ("Assets/RainMaker/Prefab/RainMistMaterial.mat",       new Color(0.88f, 0.94f, 1.00f), 0.20f),
            ("Assets/RainMaker/Prefab/RainMistMaterial2D.mat",     new Color(0.88f, 0.94f, 1.00f), 0.20f),
        };

        int fixed2 = 0;
        foreach (var (path, tint, alpha) in rainMats)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            mat.shader = particleUnlit;
            // URP Particles/Unlit uses _BaseMap
            if (rainTex != null) mat.SetTexture("_BaseMap", rainTex);
            mat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));

            // Enable alpha blending
            mat.SetFloat("_Surface", 1);      // Transparent
            mat.SetFloat("_Blend",   0);      // Alpha
            mat.SetFloat("_SrcBlend", 5);
            mat.SetFloat("_DstBlend", 10);
            mat.SetFloat("_ZWrite",   0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(mat);
            fixed2++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MaterialFixer] Fixed {fixed2} rain material(s) → URP Particles/Unlit.");
        EditorUtility.DisplayDialog("Rain Materials Fixed",
            $"Converted {fixed2} rain materials to URP Particles/Unlit.\nRain droplets should now be translucent blue.", "OK");
    }

    // ── FBX material import mode fix (THE BIG ONE) ────────────────────────────
    // Root cause of pink buildings/animals: old Pandazole FBX files have
    // materialImportMode:0 which means Unity uses the embedded STANDARD shader
    // material from the FBX binary. In URP that always renders as pink.
    // This fix sets every pack FBX to use external materials and remaps the
    // embedded material to the correct URP PandaMat.mat via SearchAndRemapMaterials.

    [MenuItem("FarmTwin/Fix/Fix FBX Material Import (Pink Buildings + Animals)")]
    public static void FixFbxMaterialImport()
    {
        string[] PACK_FOLDERS = {
            "Assets/Pandazole_Lowpoly_Asset_Bundle",
            "Assets/Pandazole_Ultimate_Pack",
            "Assets/UrsaAnimation",
            "Assets/ithappy",
            "Assets/Gridness Studios",
            "Assets/Low Poly Fruits",
            "Assets/PolyOne",
        };

        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogError("[MaterialFixer] URP/Lit not found."); return; }

        int fixedCount = 0;
        int skipped    = 0;

        EditorUtility.DisplayProgressBar("Fix FBX Material Import", "Scanning models...", 0f);

        try
        {
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", PACK_FOLDERS);

            for (int i = 0; i < fbxGuids.Length; i++)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                EditorUtility.DisplayProgressBar("Fix FBX Material Import",
                    System.IO.Path.GetFileName(fbxPath),
                    (float)i / fbxGuids.Length);

                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) { skipped++; continue; }

                // Always switch to external-material mode
                bool changed = false;

                if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    changed = true;
                }
                if (importer.materialLocation != ModelImporterMaterialLocation.External)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    changed = true;
                }

                // Recursive-up finds the nearest PandaMat.mat for this pack first
                importer.materialName   = ModelImporterMaterialName.BasedOnTextureName;
                importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;

                // Let Unity find and remap automatically
                bool remapped = importer.SearchAndRemapMaterials(
                    ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.RecursiveUp);

                if (changed || remapped)
                {
                    importer.SaveAndReimport();
                    fixedCount++;
                }
                else
                {
                    skipped++;
                }
            }

            // ── Trees: fix the remap that points to the wrong material GUID ──
            FixTreeFbxRemap();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[MaterialFixer] FBX import fix: updated {fixedCount}, skipped {skipped}.");
        EditorUtility.DisplayDialog("FBX Materials Fixed",
            $"Updated {fixedCount} FBX model(s) to use external URP materials.\n\n" +
            "Buildings, animals, and trees should no longer be pink.\n" +
            "Press Play to verify.", "OK");
    }

    static void FixTreeFbxRemap()
    {
        const string fbxPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/BundleLowPolyTreesV1.fbx";
        const string matPath = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.mat";

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) { Debug.LogWarning("[MaterialFixer] Tree FBX not found."); return; }

        Material treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (treeMat == null)
        {
            // Create it if missing
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            Texture2D grad = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.png");
            treeMat = new Material(urp);
            if (grad != null) { treeMat.SetTexture("_BaseMap", grad); treeMat.SetTexture("_MainTex", grad); }
            treeMat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(treeMat, matPath);
            AssetDatabase.SaveAssets();
            treeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        if (treeMat == null) { Debug.LogError("[MaterialFixer] Could not load/create tree mat."); return; }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation   = ModelImporterMaterialLocation.External;
        importer.materialName       = ModelImporterMaterialName.BasedOnTextureName;
        importer.materialSearch     = ModelImporterMaterialSearch.Everywhere;

        // Remap ALL embedded materials in this FBX to the gradient mat
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (asset is Material embMat)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embMat), treeMat);
                Debug.Log($"[MaterialFixer] Tree remap: '{embMat.name}' → Gradient Pallete256.mat");
            }
        }

        importer.SaveAndReimport();
        Debug.Log("[MaterialFixer] Tree FBX remap fixed → Gradient Pallete256.mat");
    }

    // ── Nuclear one-click fix ─────────────────────────────────────────────────
    // Runs ALL fixes in sequence: FBX import modes + material keyword cleanup.
    // Use this when multiple things are pink.

    [MenuItem("FarmTwin/Fix/NUCLEAR FIX - Fix Everything Pink")]
    public static void NuclearFix()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogError("[MaterialFixer] URP/Lit shader not found."); return; }

        EditorUtility.DisplayProgressBar("Nuclear Fix", "Step 1: Recreating broken materials...", 0.1f);

        // Step 1 — Recreate all asset-pack .mat files from scratch with a clean
        // URP/Lit base. This fixes specular-workflow keyword conflicts, empty
        // EditorClassIdentifier, and any other metadata that trips up Unity 6.
        string[] matFolders = {
            "Assets/Pandazole_Lowpoly_Asset_Bundle",
            "Assets/Pandazole_Ultimate_Pack",
            "Assets/UrsaAnimation",
            "Assets/ithappy",
            "Assets/Gridness Studios",
            "Assets/Low Poly Fruits",
            "Assets/PolyOne",
        };

        int matFixed = 0;
        string[] matGuids = AssetDatabase.FindAssets("t:Material", matFolders);
        for (int i = 0; i < matGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
            EditorUtility.DisplayProgressBar("Nuclear Fix", $"Material {i+1}/{matGuids.Length}", 0.1f + 0.4f * i / matGuids.Length);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Preserve what we need
            Color   baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Texture baseTex   = mat.HasProperty("_BaseMap")   ? mat.GetTexture("_BaseMap") : null;
            string  matName   = mat.name;

            // Create a fresh URP/Lit material (correct Unity 6 metadata)
            Material fresh = new Material(urp) { name = matName };
            fresh.SetColor("_BaseColor", baseColor);
            fresh.SetColor("_Color",     baseColor);
            if (baseTex != null) { fresh.SetTexture("_BaseMap", baseTex); fresh.SetTexture("_MainTex", baseTex); }

            // Overwrite the asset with the clean version
            EditorUtility.CopySerialized(fresh, mat);
            mat.name = matName;
            // Restore properties CopySerialized may have defaulted
            mat.SetColor("_BaseColor", baseColor);
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);

            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(mat);
            matFixed++;
        }

        AssetDatabase.SaveAssets();

        // Step 2 — Fix FBX import modes
        EditorUtility.DisplayProgressBar("Nuclear Fix", "Step 2: Fixing FBX import modes...", 0.55f);
        FixFbxMaterialImport();   // this also calls FixTreeFbxRemap internally

        // Step 3 — Fix crop food materials
        EditorUtility.DisplayProgressBar("Nuclear Fix", "Step 3: Fixing crop food materials...", 0.85f);
        FixCropFoodMaterials();

        // Step 4 — Fix tree material
        EditorUtility.DisplayProgressBar("Nuclear Fix", "Step 4: Fixing tree materials...", 0.92f);
        FixTreeMaterials();

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        Debug.Log($"[MaterialFixer] Nuclear fix: recreated {matFixed} materials + fixed FBX import modes.");
        EditorUtility.DisplayDialog("Nuclear Fix Complete",
            $"Recreated {matFixed} materials with clean URP/Lit format.\n" +
            "Fixed FBX import modes for buildings and animals.\n\n" +
            "Press Play — buildings, animals, and trees should now have colour.", "OK");
    }
}
