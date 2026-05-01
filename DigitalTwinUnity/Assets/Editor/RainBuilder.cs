using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// FarmTwin → Build → Setup Rain System
/// Creates a rain ParticleSystem above the farm and wires it to RainController.
/// Run once; safe to re-run (skips if already present).
/// </summary>
public static class RainBuilder
{
    [MenuItem("FarmTwin/Build/Setup Rain System")]
    public static void SetupRainSystem()
    {
        // ── 1. Find or create RainController ─────────────────────────────────
        RainController ctrl = Object.FindFirstObjectByType<RainController>();
        if (ctrl == null)
        {
            GameObject ctrlGO = new GameObject("RainSystem");
            ctrl = ctrlGO.AddComponent<RainController>();
            Debug.Log("[RainBuilder] Created RainSystem GameObject with RainController.");
        }

        // ── 2. Find or create the "Rain" ParticleSystem child ────────────────
        Transform existing = ctrl.transform.Find("Rain");
        GameObject rainGO;
        if (existing != null)
        {
            rainGO = existing.gameObject;
            Debug.Log("[RainBuilder] Found existing Rain child — reconfiguring.");
        }
        else
        {
            rainGO = new GameObject("Rain");
            rainGO.transform.SetParent(ctrl.transform, worldPositionStays: false);
        }

        // Position above farm center (20×20 grid × 2.2 spacing ÷ 2 ≈ 22m centre)
        rainGO.transform.position = new Vector3(22f, 45f, 22f);
        // Rotate so the emitter's +Y faces down → particles fall toward ground
        rainGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ── 3. Configure ParticleSystem ───────────────────────────────────────
        ParticleSystem ps = rainGO.GetComponent<ParticleSystem>();
        if (ps == null) ps = rainGO.AddComponent<ParticleSystem>();

        // Main module
        var main          = ps.main;
        main.startSpeed   = new ParticleSystem.MinMaxCurve(18f, 22f);
        main.startSize    = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 2.8f);
        main.startColor   = new Color(0.55f, 0.80f, 1.00f, 0.55f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4000;
        main.loop         = true;

        // Emission — off by default (RainController.Start() will ensure it's stopped)
        var emission          = ps.emission;
        emission.rateOverTime = 600f;
        emission.enabled      = true;   // RainController.Start() calls StopRain() which stops it

        // Shape — wide flat Box that covers the whole farm
        var shape      = ps.shape;
        shape.enabled  = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale    = new Vector3(55f, 1f, 55f);

        // Renderer — Stretch mode for classic rain-streak look
        ParticleSystemRenderer psr = rainGO.GetComponent<ParticleSystemRenderer>();
        psr.renderMode    = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0.04f;
        psr.lengthScale   = 2.0f;

        // Material — try URP Particles/Unlit, fall back to Sprites/Default
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(0.55f, 0.80f, 1.00f, 0.55f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.55f, 0.80f, 1.00f, 0.55f));
            psr.sharedMaterial = mat;
        }

        // ── 4. Wire ParticleSystem into RainController ────────────────────────
        ctrl.rainParticles = ps;

        // Stop it immediately so it starts silent (WeatherSystem will call StartRain)
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        EditorUtility.SetDirty(ctrl.gameObject);
        EditorUtility.SetDirty(rainGO);

        Debug.Log("[RainBuilder] Rain system ready. RainController.rainParticles wired. " +
                  "WeatherSystem will activate rain when forecast or sim roll triggers it.");
    }
}
#endif
