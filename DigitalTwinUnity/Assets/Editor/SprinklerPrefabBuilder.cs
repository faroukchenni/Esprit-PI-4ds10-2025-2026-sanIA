using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public class SprinklerPrefabBuilder
{
    [MenuItem("FarmTwin/Build/Rebuild Sprinkler Prefab")]
    public static void BuildPrefab()
    {
        string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string path = folder + "/Sprinkler.prefab";

        GameObject go = new GameObject("Sprinkler");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        Color waterBlue = new Color(0.529f, 0.808f, 0.922f, 0.85f); // #87CEEB

        // Main
        var main              = ps.main;
        main.startSpeed       = 4f;
        main.startSize        = 0.08f;
        main.startLifetime    = 2f;
        main.startColor       = waterBlue;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.gravityModifier  = 0.4f;   // particles arc down naturally

        // Emission — off by default, SprinklerSystem enables on demand
        var em          = ps.emission;
        em.rateOverTime = 80f;
        em.enabled      = false;

        // Shape — upward cone, like a sprinkler head
        var shape        = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Cone;
        shape.angle      = 60f;
        shape.radius     = 0.05f;

        // Color over lifetime: solid blue → fade out at end
        var col          = ps.colorOverLifetime;
        col.enabled      = true;
        Gradient grad    = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.53f, 0.81f, 0.92f), 0f),
                new GradientColorKey(new Color(0.40f, 0.70f, 0.90f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.0f, 1f)   // fully transparent at end of life
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Renderer
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        // ── Material — check shader exists BEFORE calling new Material() ─────
        // new Material(null) creates an invisible pink error material.
        // We check each candidate shader for null first.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");

        Material mat;
        if (shader != null)
        {
            mat = new Material(shader);
        }
        else
        {
            // Absolute last resort — at least not pink
            Debug.LogWarning("[SprinklerPrefabBuilder] No particle shader found — using Standard.");
            mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
        }

        // Apply water color to both URP (_BaseColor) and legacy (_Color) properties
        if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor",   waterBlue);
        if (mat.HasProperty("_Color"))       mat.SetColor("_Color",       waterBlue);
        mat.color = waterBlue;

        psr.sharedMaterial = mat;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        // Auto-assign the saved prefab to SprinklerSystem in the open scene so
        // sprinklerPrefab is never null at runtime (null → procedural path → pink URP material).
        SprinklerSystem ss = Object.FindFirstObjectByType<SprinklerSystem>();
        if (ss != null)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ss.sprinklerPrefab = prefabAsset;
            EditorUtility.SetDirty(ss.gameObject);
            Debug.Log("[SprinklerPrefabBuilder] Auto-assigned prefab to SprinklerSystem in scene — save the scene to persist.");
        }
        else
        {
            Debug.LogWarning("[SprinklerPrefabBuilder] No SprinklerSystem found in scene — drag Assets/Prefabs/Sprinkler.prefab onto SprinklerSystem.sprinklerPrefab manually.");
        }

        Debug.Log($"[SprinklerPrefabBuilder] Rebuilt {path} — shader: {(shader != null ? shader.name : "Standard fallback")}");
    }
}
#endif
