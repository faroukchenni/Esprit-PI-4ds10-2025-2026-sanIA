using UnityEngine;
using System.Collections.Generic;

// Runs automatically when Play mode starts (AfterSceneLoad).
// Iterates every MeshRenderer in the scene and replaces white materials
// with properly-coloured instance materials — works even if the Library
// cache is stale or the asset .mat files haven't reimported yet.
public static class RuntimeMaterialFixer
{
    static readonly (string nameFragment, Color color)[] NAME_RULES =
    {
        ("PandaMat",      new Color(0.80f, 0.72f, 0.58f)), // Pandazole (generic tan)
        ("Panda Mat",     new Color(0.80f, 0.72f, 0.58f)),
        ("Mat_Farm",      new Color(0.30f, 0.68f, 0.41f)), // Gridness green
        ("FarmAnimals",   new Color(0.88f, 0.83f, 0.72f)), // Ursa goat/sheep
        ("Material.001",  new Color(0.85f, 0.75f, 0.30f)), // Low Poly Fruits
        ("Stylized Tomato", new Color(0.85f, 0.12f, 0.10f)),
    };

    // Fallback colour for any unrecognised white material
    static readonly Color DEFAULT_TAN = new Color(0.80f, 0.72f, 0.58f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void FixOnPlay()
    {
        int fixed2 = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine("[RuntimeMaterialFixer] Scanning scene renderers...");

        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null)
        {
            Debug.LogWarning("[RuntimeMaterialFixer] URP/Lit shader not found.");
            return;
        }

        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (m.shader != null &&
                    m.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (!IsEffectivelyWhite(m)) continue;

                // Create an instance so we don't modify the asset
                Material inst = new Material(urp);
                Color col = ColorForMaterialName(m.name);
                inst.SetColor("_BaseColor", col);
                inst.SetColor("_Color",     col);
                inst.name = m.name + "_fixed";
                mats[i] = inst;
                changed = true;
                fixed2++;

                log.AppendLine($"  FIXED  [{r.gameObject.name}] mat[{i}]='{m.name}' → {col}");
            }

            if (changed)
                r.sharedMaterials = mats;
        }

        // Also tone down any opaque-white particle system materials (e.g. RainMist)
        int fixedParticles = 0;
        foreach (ParticleSystemRenderer pr in Object.FindObjectsOfType<ParticleSystemRenderer>())
        {
            Material m = pr.sharedMaterial;
            if (m == null) continue;
            if (!IsParticleMaterialTooOpaque(m)) continue;

            Material inst = new Material(m);
            if (inst.HasProperty("_Color"))
            {
                try
                {
                    Color c = inst.GetColor("_Color");
                    c.a = Mathf.Min(c.a, 0.05f);
                    inst.SetColor("_Color", c);
                }
                catch { /* shader exposes name but not as color */ }
            }
            if (inst.HasProperty("_TintColor"))
            {
                try
                {
                    Color t = inst.GetColor("_TintColor");
                    t.a = Mathf.Min(t.a, 0.035f);
                    inst.SetColor("_TintColor", t);
                }
                catch { }
            }
            inst.name = m.name + "_faded";
            pr.sharedMaterial = inst;
            fixedParticles++;
            log.AppendLine($"  FADED  [{pr.gameObject.name}] particle mat '{m.name}'");
        }

        if (fixed2 > 0 || fixedParticles > 0)
            Debug.Log(log.ToString());
        else
            Debug.Log("[RuntimeMaterialFixer] No white materials found in scene — all good.");
    }

    static bool IsParticleMaterialTooOpaque(Material m)
    {
        if (m.shader != null &&
            m.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (!m.HasProperty("_Color")) return false;
        Color c;
        try { c = m.GetColor("_Color"); }
        catch { return false; }
        // White-ish AND more opaque than our desired threshold
        return c.r > 0.85f && c.g > 0.85f && c.b > 0.85f && c.a > 0.08f;
    }

    static bool IsEffectivelyWhite(Material m)
    {
        if (m.shader != null &&
            m.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (!m.HasProperty("_BaseColor")) return false;
        Color c;
        try { c = m.GetColor("_BaseColor"); }
        catch { return false; }
        if (c.r <= 0.92f || c.g <= 0.92f || c.b <= 0.92f) return false;
        // White _BaseColor is correct for textured materials (white = no tint).
        // Only flag materials that are BOTH white AND have no texture.
        try
        {
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return false;
            if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return false;
        }
        catch { return false; }
        return true;
    }

    static Color ColorForMaterialName(string matName)
    {
        foreach (var rule in NAME_RULES)
            if (matName.Contains(rule.nameFragment))
                return rule.color;
        return DEFAULT_TAN;
    }
}
