using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// FarmTwin → Build → Add Farm Props
/// Adds a water tower (south of hub) and one IoT sensor pole per crop zone.
/// Safe to re-run: removes old FarmProps root first, then rebuilds from scratch.
/// </summary>
public static class FarmPropsBuilder
{
    // Grid constants — must match TwinSimulationManager / CyberGrid
    private const float SPACING   = 2.2f;
    private const int   GRID_SIZE = 20;

    [MenuItem("FarmTwin/Build/Add Farm Props")]
    public static void AddFarmProps()
    {
        // Remove previous run's props so this is idempotent
        GameObject old = GameObject.Find("FarmProps");
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject("FarmProps");

        BuildWaterTower(root.transform);
        BuildSensorPoles(root.transform);

        EditorUtility.SetDirty(root);
        Debug.Log("[FarmPropsBuilder] Done: 1 water tower + 4 IoT sensor poles added to scene.");
    }

    // ── Water Tower ───────────────────────────────────────────────────────────

    static void BuildWaterTower(Transform parent)
    {
        // Centre of farm: (gridSize/2) * spacing = 10 * 2.2 = 22
        // Place tower at south-west corner area — visible, outside crops
        float cx = GRID_SIZE * SPACING * 0.5f;   // 22
        Vector3 origin = new Vector3(cx, 0f, -6f); // just south of perimeter fence

        GameObject tower = new GameObject("WaterTower");
        tower.transform.SetParent(parent);
        tower.transform.position = origin;

        Color steel   = new Color(0.28f, 0.33f, 0.38f); // dark gunmetal
        Color tankCol = new Color(0.15f, 0.48f, 0.72f); // steel-blue tank

        float legH = 6f;  // leg height in metres

        // Base slab
        Prop(tower.transform, PrimitiveType.Cube,     "Base",
            new Vector3(0, 0.1f, 0), new Vector3(3.0f, 0.2f, 3.0f), steel);

        // Four angled legs
        float lo = 0.85f; // leg offset from centre
        Vector3[] legPos = {
            new Vector3(-lo, legH * 0.5f, -lo),
            new Vector3( lo, legH * 0.5f, -lo),
            new Vector3(-lo, legH * 0.5f,  lo),
            new Vector3( lo, legH * 0.5f,  lo),
        };
        foreach (var lp in legPos)
            Prop(tower.transform, PrimitiveType.Cylinder, "Leg",
                lp, new Vector3(0.12f, legH * 0.5f, 0.12f), steel);
        // (Cylinder native height = 2 units, so scale.y = legH/2 → actual height = legH)

        // Cross-braces (horizontal cylinders)
        float braceY = legH * 0.45f;
        Prop(tower.transform, PrimitiveType.Cylinder, "BraceX",
            new Vector3(0, braceY, 0),
            new Vector3(lo * 2f * 0.5f, 0.04f, 0.04f), steel,
            Quaternion.Euler(0, 0, 90));
        Prop(tower.transform, PrimitiveType.Cylinder, "BraceZ",
            new Vector3(0, braceY, 0),
            new Vector3(0.04f, 0.04f, lo * 2f * 0.5f), steel,
            Quaternion.Euler(90, 0, 0));

        // Tank body
        Prop(tower.transform, PrimitiveType.Cylinder, "Tank",
            new Vector3(0, legH + 0.9f, 0),
            new Vector3(1.8f, 0.9f, 1.8f), tankCol);

        // Dome cap
        Prop(tower.transform, PrimitiveType.Sphere, "Cap",
            new Vector3(0, legH + 1.9f, 0),
            new Vector3(1.8f, 0.5f, 1.8f), tankCol);

        // Vertical supply pipe from tank base to ground
        Prop(tower.transform, PrimitiveType.Cylinder, "SupplyPipe",
            new Vector3(0.6f, legH * 0.45f, 0.0f),
            new Vector3(0.07f, legH * 0.45f, 0.07f), steel);
    }

    // ── IoT Sensor Poles ─────────────────────────────────────────────────────

    static void BuildSensorPoles(Transform parent)
    {
        // Zone mid-points (world space) — one sensor per quadrant
        // Potato: x<10, y<10  →  centre ≈ (4*2.2, 0, 4*2.2)
        // Tomato: x>=10,y<10  →  centre ≈ (15*2.2, 0, 4*2.2)
        // Grape:  x<10, y>=10 →  centre ≈ (4*2.2, 0, 15*2.2)
        // Apple:  x>=10,y>=10 →  centre ≈ (15*2.2, 0, 15*2.2)
        float q1 = 4f  * SPACING;   // 8.8
        float q2 = 15f * SPACING;   // 33.0

        var poles = new (string zone, Vector3 pos)[]
        {
            ("Sensor_Potato", new Vector3(q1, 0f, q1)),
            ("Sensor_Tomato", new Vector3(q2, 0f, q1)),
            ("Sensor_Grape",  new Vector3(q1, 0f, q2)),
            ("Sensor_Apple",  new Vector3(q2, 0f, q2)),
        };

        Color aluminum = new Color(0.82f, 0.83f, 0.86f); // light aluminium
        Color housing  = new Color(0.12f, 0.18f, 0.22f); // dark enclosure
        Color ledGreen = new Color(0.10f, 1.00f, 0.25f); // bright green LED

        foreach (var (zone, pos) in poles)
        {
            GameObject pole = new GameObject(zone);
            pole.transform.SetParent(parent);
            pole.transform.position = pos;

            // Shaft (3 m tall, thin)
            Prop(pole.transform, PrimitiveType.Cylinder, "Shaft",
                new Vector3(0, 1.5f, 0), new Vector3(0.07f, 1.5f, 0.07f), aluminum);

            // Ground stake (thicker, short stub below grade)
            Prop(pole.transform, PrimitiveType.Cylinder, "Stake",
                new Vector3(0, 0.1f, 0), new Vector3(0.12f, 0.12f, 0.12f), housing);

            // Sensor head enclosure
            Prop(pole.transform, PrimitiveType.Cube, "Head",
                new Vector3(0, 3.25f, 0), new Vector3(0.28f, 0.22f, 0.18f), housing);

            // Horizontal arm (solar / antenna feel)
            Prop(pole.transform, PrimitiveType.Cube, "Arm",
                new Vector3(0.18f, 3.15f, 0), new Vector3(0.35f, 0.05f, 0.05f), aluminum);

            // LED indicator — emissive green
            GameObject led = PropGO(pole.transform, PrimitiveType.Sphere, "LED",
                new Vector3(0.12f, 3.28f, 0.10f), new Vector3(0.045f, 0.045f, 0.045f));
            SetEmissive(led, ledGreen);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject Prop(Transform parent, PrimitiveType type, string name,
        Vector3 localPos, Vector3 localScale, Color color,
        Quaternion? localRot = null)
    {
        GameObject go = PropGO(parent, type, name, localPos, localScale, localRot);
        ApplyColor(go, color);
        return go;
    }

    static GameObject PropGO(Transform parent, PrimitiveType type, string name,
        Vector3 localPos, Vector3 localScale, Quaternion? localRot = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        go.transform.localRotation = localRot ?? Quaternion.identity;

        // Remove collider — decorative only
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        return go;
    }

    static void ApplyColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(r.sharedMaterial);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
        mat.color = c;
        r.sharedMaterial = mat;
    }

    static void SetEmissive(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(r.sharedMaterial);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_BaseColor"))     mat.SetColor("_BaseColor",     c);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * 2.5f);
        if (mat.HasProperty("_Color"))         mat.SetColor("_Color",         c);
        mat.color = c;
        r.sharedMaterial = mat;
    }
}
#endif
