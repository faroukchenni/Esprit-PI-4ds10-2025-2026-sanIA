using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public class SprinklerPrefabBuilder
{
    [MenuItem("FarmTwin/Build Sprinkler Prefab")]
    public static void BuildPrefab()
    {
        string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string path = folder + "/Sprinkler.prefab";

        GameObject go = new GameObject("Sprinkler");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        
        // Main
        var main = ps.main;
        main.startSpeed = 4f;
        main.startSize = 0.08f;
        main.startLifetime = 2f;
        main.startColor = new Color(0.529f, 0.808f, 0.922f, 0.8f); // #87CEEB light blue, alpha 0.8
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        // Emission
        var em = ps.emission;
        em.rateOverTime = 80f;
        em.enabled = false; // Off by default

        // Shape
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 60f;
        shape.radius = 0.05f;

        // Renderer
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        
        // Base URP Unlit material or Sprites-Default
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (mat.shader == null || !mat.shader.isSupported)
            mat = new Material(Shader.Find("Sprites/Default"));
            
        mat.color = new Color(0.529f, 0.808f, 0.922f, 0.8f);
        psr.sharedMaterial = mat;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"[SprinklerPrefabBuilder] Successfully created {path}");
    }
}
#endif
