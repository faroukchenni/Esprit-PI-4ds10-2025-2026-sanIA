using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spawns two flat quad strips forming a cross-shaped dirt path between the 4 crop zones.
/// The strips are parented under a child GameObject called "Paths".
/// Call Build() at runtime or from the FarmTwin/Setup/Apply Phase 3 editor menu item.
/// </summary>
public class PathGenerator : MonoBehaviour
{
    [Tooltip("Material applied to both path strips (use DirtPath.mat)")]
    public Material pathMaterial;

    [Tooltip("Width of each path strip in world units")]
    public float pathWidth = 1.5f;

    [Tooltip("Cell spacing — must match CyberGrid.settings.spacing")]
    public float gridSpacing = 2.2f;

    [Tooltip("Number of columns in the grid")]
    public int gridWidth = 20;

    [Tooltip("Number of rows in the grid")]
    public int gridHeight = 20;

    void Start()
    {
        Build();
    }

    /// <summary>
    /// Destroys any existing Paths child and regenerates both strips.
    /// Safe to call from editor scripts via Apply Phase 3.
    /// </summary>
    public void Build()
    {
        // Remove previous build
        Transform existing = transform.Find("Paths");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        GameObject pathsRoot = new GameObject("Paths");
        pathsRoot.transform.SetParent(transform, false);
        pathsRoot.transform.localPosition = Vector3.zero;

        // The center of the path cross aligns with (gridWidth/2 * spacing).
        // Integer division intentionally matches TwinSimulationManager's center logic.
        float centerX = (gridWidth / 2) * gridSpacing;
        float centerZ = (gridHeight / 2) * gridSpacing;

        // Full span of the grid in each axis
        float spanX = gridWidth * gridSpacing;
        float spanZ = gridHeight * gridSpacing;

        // Horizontal strip: runs the full X span, thin in Z, sits at z = centerZ
        CreateStrip(
            pathsRoot.transform,
            "Path_Horizontal",
            new Vector3(spanX * 0.5f, 0.05f, centerZ),
            spanX,
            pathWidth
        );

        // Vertical strip: runs the full Z span, thin in X, sits at x = centerX
        CreateStrip(
            pathsRoot.transform,
            "Path_Vertical",
            new Vector3(centerX, 0.05f, spanZ * 0.5f),
            pathWidth,
            spanZ
        );
    }

    void CreateStrip(Transform parent, string stripName, Vector3 localPos, float width, float depth)
    {
        GameObject go = new GameObject(stripName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        float hw = width * 0.5f;
        float hd = depth * 0.5f;

        Mesh mesh = new Mesh { name = stripName + "_Mesh" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-hw, 0f, -hd),
            new Vector3( hw, 0f, -hd),
            new Vector3( hw, 0f,  hd),
            new Vector3(-hw, 0f,  hd)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        // Both triangles counter-clockwise from above so normals face +Y
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = pathMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }
}
