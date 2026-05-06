using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds all decorative elements outside the crop grid:
/// farm buildings, animal pens with fences, a tree border ring, and scattered props.
/// Call BuildAll() from FarmTwin/Build/Build Full Farm, or it auto-builds on Start.
/// All positions are computed relative to the crop grid centre.
/// </summary>
public class FarmCompound : MonoBehaviour
{
    // ── Grid reference ─────────────────────────────────────────────────────────
    [Header("Grid Reference")]
    public float gridSpacing  = 2.2f;
    public int   gridColumns  = 20;
    public int   gridRows     = 20;

    // ── Buildings ──────────────────────────────────────────────────────────────
    [Header("Buildings")]
    public GameObject barnPrefab;
    public GameObject farmerHousePrefab;
    public GameObject siloPrefab;
    public GameObject farmMillPrefab;
    public GameObject chickenCoopPrefab;
    public GameObject storeBuildingPrefab;
    public GameObject greenhousePrefab;
    public GameObject outhousesPrefab;

    // ── Animals ────────────────────────────────────────────────────────────────
    [Header("Animals")]
    public GameObject chickenPrefab;
    public GameObject horsePrefab;

    // ── Props ──────────────────────────────────────────────────────────────────
    [Header("Props")]
    public GameObject haystackPrefab;
    public GameObject wheelbarrowPrefab;
    public GameObject woodenCratesPrefab;
    public GameObject wellPrefab;

    // ── Fence & Trees ──────────────────────────────────────────────────────────
    [Header("Fence and Trees")]
    public GameObject fencePrefab;
    public List<GameObject> springTreePrefabs = new List<GameObject>();
    [Tooltip("Offset added to (gridColumns*spacing/2) for tree ring radius")]
    public float treeBorderOffset = 30f;

    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Auto-build at runtime if nothing was placed by the editor menu
        if (transform.childCount == 0)
            BuildAll();
    }

    /// <summary>Clears previous compound and rebuilds everything.</summary>
    public void BuildAll()
    {
        Random.InitState(42); // deterministic layout across rebuilds
        ClearChildren();

        // Grid centre in local space of FarmCompound (which sits at world origin).
        // We look for MainGrid to account for any non-zero scene position.
        Vector3 gridOrigin = Vector3.zero;
        GameObject mainGrid = GameObject.Find("MainGrid");
        if (mainGrid != null) gridOrigin = mainGrid.transform.position;

        Vector3 gridCenter = gridOrigin + new Vector3(
            (gridColumns / 2) * gridSpacing,
            0f,
            (gridRows  / 2) * gridSpacing);

        BuildFarmBuildings(gridCenter);
        BuildAnimalPens(gridCenter);
        BuildTreeBorder(gridCenter, gridOrigin);
        ScatterProps(gridCenter);
    }

    // ── Step 4: Farm buildings ─────────────────────────────────────────────────

    void BuildFarmBuildings(Vector3 gc)
    {
        Transform root = MakeParent("FarmCompound");

        // Force Y=0 so no building floats regardless of gc.y
        float bx = gc.x, bz = gc.z;

        // Minimum 8-unit spacing guaranteed by offset table below.
        // All Y forced to 0 via PlaceBuilding helper.
        PlaceBuilding(root, barnPrefab,          new Vector3(bx - 15f, 0f, bz + 55f), 180f);
        PlaceBuilding(root, farmerHousePrefab,   new Vector3(bx + 15f, 0f, bz + 55f), 180f);
        PlaceBuilding(root, siloPrefab,          new Vector3(bx + 25f, 0f, bz + 50f), 180f);
        PlaceBuilding(root, farmMillPrefab,      new Vector3(bx - 25f, 0f, bz + 50f), 180f);
        PlaceBuilding(root, chickenCoopPrefab,   new Vector3(bx +  0f, 0f, bz + 48f), 180f);
        PlaceBuilding(root, storeBuildingPrefab, new Vector3(bx +  0f, 0f, bz + 62f), 180f);
        PlaceBuilding(root, greenhousePrefab,    new Vector3(bx + 38f, 0f, bz + 48f), 180f);
        PlaceBuilding(root, outhousesPrefab,     new Vector3(bx - 38f, 0f, bz + 42f),   0f);
    }

    /// <summary>Instantiates a building prefab, always at Y=0 regardless of scene origin.</summary>
    GameObject PlaceBuilding(Transform parent, GameObject prefab, Vector3 worldPos, float yRot)
    {
        if (prefab == null) return null;
        worldPos.y = 0f;
        GameObject go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRot, 0f), parent);
        return go;
    }

    // ── Step 5: Animal pens ────────────────────────────────────────────────────

    void BuildAnimalPens(Vector3 gc)
    {
        Transform animals = MakeParent("Animals");
        Transform fences  = MakeParent("Fences");

        // --- Chicken pen: 3×2 grid, spacing 2, near ChickenCoop ---
        Vector3 chickenPenCenter = gc + new Vector3(0f, 0f, 45f);

        if (chickenPrefab != null)
        {
            for (int col = 0; col < 3; col++)
            for (int row = 0; row < 2; row++)
            {
                Vector3 pos = chickenPenCenter + new Vector3(
                    (col - 1) * 2f,   // -2, 0, +2
                    0f,
                    (row - 0.5f) * 2f // -1, +1
                );
                Place(animals, chickenPrefab, pos, Random.Range(0f, 360f));
            }
        }

        // Fence ring: 10 wide × 8 deep around chicken pen
        PlaceFenceRing(fences, chickenPenCenter, 10f, 8f, 2f);

        // --- Horse pen: 3 horses in a row, east side ---
        Vector3 horsePenCenter = gc + new Vector3(35f, 0f, 20f);

        if (horsePrefab != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = horsePenCenter + new Vector3((i - 1) * 4f, 0f, 0f);
                Place(animals, horsePrefab, pos, Random.Range(-30f, 30f));
            }
        }

        // Fence ring: 16 wide × 10 deep around horse pen
        PlaceFenceRing(fences, horsePenCenter, 16f, 10f, 2f);
    }

    // ── Step 6: Tree border ────────────────────────────────────────────────────

    void BuildTreeBorder(Vector3 gc, Vector3 gridOrigin)
    {
        if (springTreePrefabs == null || springTreePrefabs.Count == 0)
        {
            Debug.LogWarning("FarmCompound: No spring tree prefabs assigned — skipping tree border.");
            return;
        }

        Transform root = MakeParent("TreeBorder");

        float treeRadius = (gridColumns * gridSpacing * 0.5f) + treeBorderOffset;
        const int treeCount = 24;

        // ── Exclusion zones (XZ only) ─────────────────────────────────────────
        float gridW = gridColumns * gridSpacing;
        float gridD = gridRows    * gridSpacing;

        // Zone 1: Crop grid + 5-unit buffer on each side
        float cgMinX = gridOrigin.x - 5f;
        float cgMaxX = gridOrigin.x + gridW + 5f;
        float cgMinZ = gridOrigin.z - 5f;
        float cgMaxZ = gridOrigin.z + gridD + 5f;

        // Zone 2: Animal zone (east of grid) + 8-unit buffer
        float animStartX = gridOrigin.x + gridW + 10f;
        float animMinX   = animStartX - 8f;
        float animMaxX   = animStartX + 70f + 8f;
        float animCentZ  = gridOrigin.z + gridD * 0.5f;
        float animMinZ   = animCentZ - 25f - 8f;
        float animMaxZ   = animCentZ + 25f + 8f;

        // Zone 3: Farm compound buildings (north of grid) + 5-unit buffer
        float bldCentX = gridOrigin.x + gridW * 0.5f;
        float bldCentZ = gridOrigin.z + gridD + 25f;
        float bldMinX  = bldCentX - 35f;   // half of 60 + 5 buffer
        float bldMaxX  = bldCentX + 35f;
        float bldMinZ  = bldCentZ - 25f;   // half of 40 + 5 buffer
        float bldMaxZ  = bldCentZ + 25f;

        string GetSkipReason(float px, float pz)
        {
            if (px >= cgMinX   && px <= cgMaxX   && pz >= cgMinZ   && pz <= cgMaxZ)   return "inside crop grid zone";
            if (px >= animMinX && px <= animMaxX && pz >= animMinZ && pz <= animMaxZ) return "inside animal pen zone";
            if (px >= bldMinX  && px <= bldMaxX  && pz >= bldMinZ  && pz <= bldMaxZ)  return "inside buildings zone";
            return null;
        }

        int placed  = 0;
        int skipped = 0;
        var skipLog = new System.Text.StringBuilder();

        for (int i = 0; i < treeCount; i++)
        {
            float angle = i * (360f / treeCount) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                gc.x + Mathf.Sin(angle) * treeRadius,
                0f,
                gc.z + Mathf.Cos(angle) * treeRadius);

            string reason = GetSkipReason(pos.x, pos.z);
            if (reason != null)
            {
                skipLog.AppendLine($"    Tree {i:D2} @ ({pos.x:F1},{pos.z:F1}) — SKIPPED: {reason}");
                skipped++;
                continue;
            }

            GameObject prefab = springTreePrefabs[i % springTreePrefabs.Count];
            if (prefab == null) continue;

            GameObject tree = Place(root, prefab, pos, Random.Range(0f, 360f));
            if (tree != null)
            {
                float scale = Random.Range(0.8f, 1.4f);
                tree.transform.localScale    = Vector3.one * scale;
                tree.transform.localPosition = new Vector3(
                    tree.transform.localPosition.x, 0f, tree.transform.localPosition.z);
                placed++;
            }
        }

        Debug.Log($"[FarmCompound] TreeBorder: {placed} placed, {skipped} skipped (radius {treeRadius:F1}m).\n" +
                  (skipped > 0 ? skipLog.ToString() : "  All trees on open terrain."));
    }

    // ── Step 7: Scatter props ──────────────────────────────────────────────────

    void ScatterProps(Vector3 gc)
    {
        Transform root = MakeParent("Props");

        // 8 haystacks within 10 units of the barn (gc + (-15, 0, 55))
        Vector3 barnPos = gc + new Vector3(-15f, 0f, 55f);
        Vector3[] haystackOffsets = {
            new Vector3(-3f, 0f,  3f), new Vector3(-6f, 0f,  1f),
            new Vector3(-8f, 0f, -2f), new Vector3(-4f, 0f, -4f),
            new Vector3( 3f, 0f,  5f), new Vector3( 6f, 0f,  2f),
            new Vector3( 7f, 0f, -3f), new Vector3( 4f, 0f, -6f),
        };
        foreach (var offset in haystackOffsets)
            PlaceProp(root, haystackPrefab, barnPos + offset, Random.Range(0f, 360f));

        // 4 wheelbarrows at crop zone corner entrances (path edges)
        Vector3[] wheelbarrowPositions = {
            gc + new Vector3(-22f, 0f,  2f),   // west-centre path
            gc + new Vector3( 22f, 0f, -2f),   // east-centre path
            gc + new Vector3(  2f, 0f,-22f),   // south-centre path
            gc + new Vector3( -2f, 0f, 22f),   // north-centre path
        };
        foreach (var pos in wheelbarrowPositions)
            PlaceProp(root, wheelbarrowPrefab, pos, Random.Range(0f, 360f));

        // 6 wooden crates within 6 units of the store building (gc + (0, 0, 62))
        Vector3 storePos = gc + new Vector3(0f, 0f, 62f);
        Vector3[] crateOffsets = {
            new Vector3(-2f, 0f,  3f), new Vector3( 2f, 0f,  3f),
            new Vector3(-4f, 0f,  1f), new Vector3( 4f, 0f,  1f),
            new Vector3(-3f, 0f, -2f), new Vector3( 3f, 0f, -2f),
        };
        foreach (var offset in crateOffsets)
            PlaceProp(root, woodenCratesPrefab, storePos + offset, Random.Range(0f, 360f));

        // 1 well at the main path intersection (grid centre)
        Place(root, wellPrefab, new Vector3(gc.x, 0f, gc.z), 0f);
    }

    /// <summary>Instantiates prefab at worldPos, forcing Y=0 for all props.</summary>
    GameObject PlaceProp(Transform parent, GameObject prefab, Vector3 worldPos, float yRot)
    {
        if (prefab == null) return null;
        worldPos.y = 0f; // ALL props at ground level
        GameObject go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRot, 0f), parent);
        return go;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    Transform MakeParent(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    /// <summary>Instantiates prefab at worldPos with Y rotation. Returns null if prefab is null.</summary>
    GameObject Place(Transform parent, GameObject prefab, Vector3 worldPos, float yRot)
    {
        if (prefab == null) return null;
        GameObject go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRot, 0f), parent);
        return go;
    }

    /// <summary>
    /// Places fence prefabs along the perimeter of a rectangle centred at <paramref name="center"/>.
    /// </summary>
    void PlaceFenceRing(Transform parent, Vector3 center, float width, float depth, float interval)
    {
        if (fencePrefab == null) return;

        float hw = width  * 0.5f;
        float hd = depth  * 0.5f;

        // South and North edges (vary X, fixed Z)
        for (float x = center.x - hw; x <= center.x + hw + 0.01f; x += interval)
        {
            Place(parent, fencePrefab, new Vector3(x, 0f, center.z - hd), 0f);
            Place(parent, fencePrefab, new Vector3(x, 0f, center.z + hd), 0f);
        }

        // West and East edges (vary Z, skip corners already placed)
        for (float z = center.z - hd + interval; z < center.z + hd; z += interval)
        {
            Place(parent, fencePrefab, new Vector3(center.x - hw, 0f, z), 90f);
            Place(parent, fencePrefab, new Vector3(center.x + hw, 0f, z), 90f);
        }
    }

    void ClearChildren()
    {
        var children = new List<GameObject>();
        foreach (Transform t in transform)
            children.Add(t.gameObject);

        foreach (var go in children)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
