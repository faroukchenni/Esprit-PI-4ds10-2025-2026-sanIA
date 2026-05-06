using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public class FarmPropsBuilder : EditorWindow
{
    // Grid constants — must match TwinSimulationManager / CyberGrid
    private const float SPACING   = 2.2f;
    private const int   GRID_SIZE = 20;

    private bool _propsInScene;

    [MenuItem("FarmTwin/Build/Farm Props Builder...")]
    public static void OpenWindow()
    {
        var win = GetWindow<FarmPropsBuilder>("Farm Props Builder");
        win.minSize = new Vector2(260, 180);
        win.RefreshState();
    }

    // Keep the old menu item so existing workflows still work
    [MenuItem("FarmTwin/Build/Add Farm Props (quick)")]
    public static void AddFarmPropsQuick() => BuildProps();

    void OnEnable()  => RefreshState();
    void OnFocus()   => RefreshState();

    void RefreshState()
    {
        _propsInScene = GameObject.Find("FarmProps") != null;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("Farm Props Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds: 1 water tower + 4 IoT sensor poles (one per crop zone).\n" +
            "Safe to re-run — removes old FarmProps root first.",
            MessageType.Info);

        EditorGUILayout.Space(6);

        // Status indicator
        var statusStyle = new GUIStyle(EditorStyles.label);
        statusStyle.fontStyle = FontStyle.Bold;
        if (_propsInScene)
        {
            statusStyle.normal.textColor = new Color(0.15f, 0.65f, 0.25f);
            GUILayout.Label("Status:  Props present in scene", statusStyle);
        }
        else
        {
            statusStyle.normal.textColor = new Color(0.75f, 0.35f, 0.10f);
            GUILayout.Label("Status:  No props in scene", statusStyle);
        }

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build / Rebuild Props", GUILayout.Height(32)))
            {
                BuildProps();
                RefreshState();
            }

            GUI.enabled = _propsInScene;
            if (GUILayout.Button("Remove Props", GUILayout.Height(32)))
            {
                RemoveProps();
                RefreshState();
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "After building, press Play to see IoT LED pulses.\n" +
            "LEDs: green (healthy) · amber (drought) · red (disease).",
            MessageType.None);
    }

    // ── Public build helpers ──────────────────────────────────────────────────

    public static void BuildProps()
    {
        GameObject old = GameObject.Find("FarmProps");
        if (old != null) Object.DestroyImmediate(old);

        GameObject root = new GameObject("FarmProps");
        BuildWaterTower(root.transform);
        BuildSensorPoles(root.transform);

        EditorUtility.SetDirty(root);
        Debug.Log("[FarmPropsBuilder] Done: 1 water tower + 4 IoT sensor poles added to scene.");
    }

    static void RemoveProps()
    {
        GameObject old = GameObject.Find("FarmProps");
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[FarmPropsBuilder] FarmProps removed from scene.");
        }
    }

    // ── Water Tower ───────────────────────────────────────────────────────────

    static void BuildWaterTower(Transform parent)
    {
        float cx     = GRID_SIZE * SPACING * 0.5f;
        Vector3 origin = new Vector3(cx, 0f, -6f);

        GameObject tower = new GameObject("WaterTower");
        tower.transform.SetParent(parent);
        tower.transform.position = origin;

        Color steel   = new Color(0.28f, 0.33f, 0.38f);
        Color tankCol = new Color(0.15f, 0.48f, 0.72f);

        float legH = 6f;

        Prop(tower.transform, PrimitiveType.Cube, "Base",
            new Vector3(0, 0.1f, 0), new Vector3(3.0f, 0.2f, 3.0f), steel);

        float lo = 0.85f;
        Vector3[] legPos = {
            new Vector3(-lo, legH * 0.5f, -lo),
            new Vector3( lo, legH * 0.5f, -lo),
            new Vector3(-lo, legH * 0.5f,  lo),
            new Vector3( lo, legH * 0.5f,  lo),
        };
        foreach (var lp in legPos)
            Prop(tower.transform, PrimitiveType.Cylinder, "Leg",
                lp, new Vector3(0.12f, legH * 0.5f, 0.12f), steel);

        float braceY = legH * 0.45f;
        Prop(tower.transform, PrimitiveType.Cylinder, "BraceX",
            new Vector3(0, braceY, 0),
            new Vector3(lo * 2f * 0.5f, 0.04f, 0.04f), steel,
            Quaternion.Euler(0, 0, 90));
        Prop(tower.transform, PrimitiveType.Cylinder, "BraceZ",
            new Vector3(0, braceY, 0),
            new Vector3(0.04f, 0.04f, lo * 2f * 0.5f), steel,
            Quaternion.Euler(90, 0, 0));

        Prop(tower.transform, PrimitiveType.Cylinder, "Tank",
            new Vector3(0, legH + 0.9f, 0),
            new Vector3(1.8f, 0.9f, 1.8f), tankCol);

        Prop(tower.transform, PrimitiveType.Sphere, "Cap",
            new Vector3(0, legH + 1.9f, 0),
            new Vector3(1.8f, 0.5f, 1.8f), tankCol);

        Prop(tower.transform, PrimitiveType.Cylinder, "SupplyPipe",
            new Vector3(0.6f, legH * 0.45f, 0.0f),
            new Vector3(0.07f, legH * 0.45f, 0.07f), steel);
    }

    // ── IoT Sensor Poles ─────────────────────────────────────────────────────

    static void BuildSensorPoles(Transform parent)
    {
        float q1 = 4f  * SPACING;
        float q2 = 15f * SPACING;

        var poles = new (string zone, Vector3 pos)[]
        {
            ("Sensor_Potato", new Vector3(q1, 0f, q1)),
            ("Sensor_Tomato", new Vector3(q2, 0f, q1)),
            ("Sensor_Grape",  new Vector3(q1, 0f, q2)),
            ("Sensor_Apple",  new Vector3(q2, 0f, q2)),
        };

        Color aluminum = new Color(0.82f, 0.83f, 0.86f);
        Color housing  = new Color(0.12f, 0.18f, 0.22f);
        Color ledGreen = new Color(0.10f, 1.00f, 0.25f);

        foreach (var (zone, pos) in poles)
        {
            GameObject pole = new GameObject(zone);
            pole.transform.SetParent(parent);
            pole.transform.position = pos;

            Prop(pole.transform, PrimitiveType.Cylinder, "Shaft",
                new Vector3(0, 1.5f, 0), new Vector3(0.07f, 1.5f, 0.07f), aluminum);

            Prop(pole.transform, PrimitiveType.Cylinder, "Stake",
                new Vector3(0, 0.1f, 0), new Vector3(0.12f, 0.12f, 0.12f), housing);

            Prop(pole.transform, PrimitiveType.Cube, "Head",
                new Vector3(0, 3.25f, 0), new Vector3(0.28f, 0.22f, 0.18f), housing);

            Prop(pole.transform, PrimitiveType.Cube, "Arm",
                new Vector3(0.18f, 3.15f, 0), new Vector3(0.35f, 0.05f, 0.05f), aluminum);

            GameObject led = PropGO(pole.transform, PrimitiveType.Sphere, "LED",
                new Vector3(0.12f, 3.28f, 0.10f), new Vector3(0.045f, 0.045f, 0.045f));
            SetEmissive(led, ledGreen);

            string zName = zone.Replace("Sensor_", "");
            IoTSensorPulse pulse = led.AddComponent<IoTSensorPulse>();
            pulse.zoneName = zName;
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

        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        return go;
    }

    static Shader _urpLit;
    static Shader URPLit()
    {
        if (_urpLit != null) return _urpLit;
        _urpLit = Shader.Find("Universal Render Pipeline/Lit")
               ?? Shader.Find("URP/Lit")
               ?? Shader.Find("Standard");
        return _urpLit;
    }

    static void ApplyColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(URPLit());
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_Color",     c);
        r.sharedMaterial = mat;
    }

    static void SetEmissive(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(URPLit());
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_BaseColor",     c);
        mat.SetColor("_EmissionColor", c * 2.5f);
        r.sharedMaterial = mat;
    }
}
#endif
