using UnityEngine;

public static class MaterialSafeUtil
{
    public static void ApplyBaseTint(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
    }

    public static void ApplyEmission(Material mat, Color c)
    {
        if (mat == null || !mat.HasProperty("_EmissionColor")) return;
        mat.SetColor("_EmissionColor", c);
    }
}
