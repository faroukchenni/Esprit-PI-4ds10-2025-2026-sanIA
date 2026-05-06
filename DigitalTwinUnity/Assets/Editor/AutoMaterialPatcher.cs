using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

// Runs on every recompile + has a manual menu item.
// Scans all known asset-pack materials, fixes white ones, force-imports
// so the Library cache is updated immediately.
[InitializeOnLoad]
public static class AutoMaterialPatcher
{
    static readonly (string pathFragment, Color color)[] RULES =
    {
        ("Pandazole City Town",     new Color(0.85f, 0.78f, 0.62f)),
        ("Pandazole Home Interior", new Color(0.90f, 0.87f, 0.80f)),
        ("Pandazole Kitchen",       new Color(0.94f, 0.88f, 0.75f)),
        ("Pandazole Nature",        new Color(0.42f, 0.62f, 0.38f)),
        ("Pandazole Survival",      new Color(0.55f, 0.42f, 0.30f)),
        ("Pandazole Farm Ranch",    new Color(0.80f, 0.72f, 0.58f)),
        ("Pandazole_Lowpoly",       new Color(0.82f, 0.75f, 0.60f)),
        ("Pandazole_Ultimate",      new Color(0.80f, 0.72f, 0.58f)),
        ("ithappy",                 new Color(0.78f, 0.62f, 0.42f)),
        ("UrsaAnimation",           new Color(0.88f, 0.83f, 0.72f)),
        ("PolyOne",                 new Color(0.85f, 0.12f, 0.10f)),
        ("Low Poly Fruits",         new Color(0.85f, 0.75f, 0.30f)),
        ("Gridness",                new Color(0.30f, 0.68f, 0.41f)),
    };

    static readonly string[] SCAN_FOLDERS =
    {
        "Assets/Pandazole_Lowpoly_Asset_Bundle",
        "Assets/Pandazole_Ultimate_Pack",
        "Assets/ithappy",
        "Assets/UrsaAnimation",
        "Assets/PolyOne",
        "Assets/Low Poly Fruits",
        "Assets/Gridness Studios",
    };

    static AutoMaterialPatcher() =>
        EditorApplication.delayCall += PatchAll;

    [MenuItem("FarmTwin/Fix/Force-Reimport Trees and Rain")]
    public static void ForceReimportTreesAndRain()
    {
        int count = 0;

        // Reimport the gradient texture the trees depend on
        string gradTex = "Assets/51+ LowPolyTrees/LowPolyTrees/Models/Material/Gradient Pallete256.png";
        if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../" + gradTex)))
        {
            AssetDatabase.ImportAsset(gradTex, ImportAssetOptions.ForceUpdate);
            count++;
        }

        // Reimport every FBX in the LowPolyTrees folder so they pick up the texture
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/51+ LowPolyTrees" }))
        {
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            count++;
        }

        // Reimport RainTexture + RainMistMaterial
        string[] rainPaths =
        {
            "Assets/RainMaker/Prefab/RainTexture.png",
            "Assets/RainMaker/Prefab/RainMistMaterial.mat",
        };
        foreach (string p in rainPaths)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../" + p)))
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AutoMaterialPatcher] Force-reimported {count} tree/rain assets. Restart Play Mode.");
    }

    [MenuItem("FarmTwin/Fix/Auto-Patch All Broken Materials (Force)")]
    public static void PatchAll()
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null)
        {
            Debug.LogError("[AutoMaterialPatcher] URP/Lit shader not found — is URP installed?");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", SCAN_FOLDERS);
        var patched  = new List<string>();
        var skipped  = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            bool isWhite = IsEffectivelyWhite(mat);

            Color col = ColorForPath(path);

            if (isWhite)
            {
                mat.shader = urp;
                mat.SetColor("_BaseColor", col);
                mat.SetColor("_Color",     col);
                mat.SetTexture("_BaseMap", null);
                EditorUtility.SetDirty(mat);
                patched.Add(path);
            }
            else
            {
                skipped.Add($"  SKIP (already coloured {mat.GetColor("_BaseColor"):F2}): {path}");
            }
        }

        if (patched.Count > 0)
        {
            AssetDatabase.SaveAssets();
            foreach (string p in patched)
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }

        // --- Diagnostic report ---
        var sb = new StringBuilder();
        sb.AppendLine($"[AutoMaterialPatcher] PATCHED {patched.Count} / {guids.Length} materials.");
        foreach (string p in patched)
            sb.AppendLine($"  FIXED: {p}");
        foreach (string s in skipped)
            sb.AppendLine(s);

        if (patched.Count > 0)
            Debug.Log(sb.ToString());
        else
            Debug.Log("[AutoMaterialPatcher] All materials already have colour — nothing to patch.");
    }

    // --- helpers ---

    static bool IsEffectivelyWhite(Material mat)
    {
        if (!mat.HasProperty("_BaseColor")) return false;
        // If a texture is already assigned, the white base color is intentional (atlas tint)
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) return false;
        if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null) return false;
        Color c = mat.GetColor("_BaseColor");
        return c.r > 0.92f && c.g > 0.92f && c.b > 0.92f;
    }

    static Color ColorForPath(string path)
    {
        foreach (var rule in RULES)
            if (path.Contains(rule.pathFragment))
                return rule.color;
        return new Color(0.82f, 0.75f, 0.60f);
    }
}
