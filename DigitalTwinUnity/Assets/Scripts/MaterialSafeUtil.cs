using UnityEngine;

/// <summary>
/// Avoids Material.color / blind GetColor — many shaders (skybox, procedural, particles)
/// do not expose _Color and throw, which breaks TreatZone and other runtime tint code.
/// </summary>
public static class MaterialSafeUtil
{
    public static void ApplyBaseTint(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.shader != null &&
            mat.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;
        try
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MaterialSafeUtil] ApplyBaseTint skipped for '{mat.name}': {ex.Message}");
        }
    }

    public static void ApplyEmission(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.shader != null &&
            mat.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;
        if (!mat.HasProperty("_EmissionColor")) return;
        try { mat.SetColor("_EmissionColor", c); }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MaterialSafeUtil] ApplyEmission skipped for '{mat.name}': {ex.Message}");
        }
    }
}
