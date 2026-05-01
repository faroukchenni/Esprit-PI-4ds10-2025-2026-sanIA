using System;
using System.Linq;
using UnityEngine;

public class FarmDecorVisualizer : MonoBehaviour
{
    [Header("References")]
    public MockTwinGrid grid;          // drag DigitalTwin (MockTwinGrid) here
    public Transform farmDecorRoot;    // drag FarmDecor here (or leave empty to auto-use this transform)

    [Header("Row Settings")]
    [Range(0.01f, 1f)] public float rowThickness = 0.15f;
    public float rowLift = 0.05f;
    public float maxHeightAdd = 0.25f;

    Renderer[] rowRenderers;
    Material[] rowMats;

    void Awake()
    {
        if (!farmDecorRoot) farmDecorRoot = transform;
    }

    void Start()
    {
        CacheRows();
        UpdateRows();
    }

    void LateUpdate()
    {
        if (grid) UpdateRows();
    }

    void CacheRows()
    {
        // Grab ONLY direct children renderers (Row_01 ... Row_10)
        int count = farmDecorRoot.childCount;
        var temp = new Renderer[count];

        int idx = 0;
        for (int i = 0; i < count; i++)
        {
            var child = farmDecorRoot.GetChild(i);
            var r = child.GetComponent<Renderer>();
            if (r == null) continue;
            temp[idx++] = r;
        }

        rowRenderers = temp.Where(r => r != null)
                           .OrderBy(r => r.gameObject.name) // Row_01 .. Row_10 stable order
                           .ToArray();

        rowMats = new Material[rowRenderers.Length];

        for (int i = 0; i < rowRenderers.Length; i++)
        {
            var r = rowRenderers[i];

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.sharedMaterial = mat;
            rowMats[i] = mat;
        }
    }

    void UpdateRows()
    {
        if (!grid || grid.Width <= 0 || grid.Height <= 0) return;

        if (rowRenderers == null || rowRenderers.Length == 0)
            CacheRows();

        int rowCount = rowRenderers.Length;
        if (rowCount <= 0) return;

        int bandSize = Mathf.CeilToInt((float)grid.Width / rowCount);

        for (int i = 0; i < rowCount; i++)
        {
            int xStart = i * bandSize;
            int xEnd = Mathf.Min(grid.Width, xStart + bandSize);

            if (xStart >= grid.Width)
            {
                xStart = grid.Width - 1;
                xEnd = grid.Width;
            }

            float v = grid.GetBandValueX(xStart, xEnd);
            Color c = grid.GetBandColor(v);

            rowMats[i].color = c;

            var t = rowRenderers[i].transform;

            Vector3 s = t.localScale;
            s.y = rowThickness + (v * maxHeightAdd);
            t.localScale = s;

            Vector3 p = t.localPosition;
            p.y = rowLift + (s.y * 0.5f);
            t.localPosition = p;
        }
    }
}
