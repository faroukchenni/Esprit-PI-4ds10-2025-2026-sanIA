using System;
using TMPro;
using UnityEngine;

public enum ViewMode
{
    Moisture,
    Disease,
    Yield
}

public class MockTwinGrid : MonoBehaviour
{
    [Header("Scene References")]
    public Transform GridRoot;
    public Transform OverlayRoot;

    [Header("Grid Settings")]
    public int Width = 20;
    public int Height = 20;
    public float TileSize = 1f;
    public float HeightScale = 1.5f;
    public bool ShowPlants = true;

    [Header("UI")]
    public TMP_Text StepText;
    public TMP_Text LegendText;
    public TMP_Text LegendRangeText;

    int step;
    float[,] moisture;
    float[,] disease;
    float[,] yield;

    GameObject[,] tiles;
    GameObject[,] diseaseMarkers;

    ViewMode mode = ViewMode.Moisture;

    void Start()
    {
        Init();
        RenderAll();
    }

    void Init()
    {
        step = 0;

        moisture = new float[Width, Height];
        disease = new float[Width, Height];
        yield = new float[Width, Height];

        tiles = new GameObject[Width, Height];
        diseaseMarkers = new GameObject[Width, Height];

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                moisture[x, y] = UnityEngine.Random.Range(0.55f, 0.9f);
                disease[x, y] = 0f;
                yield[x, y] = UnityEngine.Random.Range(0.6f, 0.95f);

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.SetParent(GridRoot, false);
                tile.transform.localPosition = new Vector3(x * TileSize, 0f, y * TileSize);
                tile.transform.localScale = new Vector3(TileSize, 0.2f, TileSize);
                tiles[x, y] = tile;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = $"Disease_{x}_{y}";
                marker.transform.SetParent(OverlayRoot, false);
                marker.transform.localPosition = new Vector3(x * TileSize, 0.25f, y * TileSize);
                marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                marker.transform.localScale = new Vector3(TileSize, TileSize, TileSize);
                marker.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                marker.SetActive(false);
                diseaseMarkers[x, y] = marker;
            }

        CenterRoots();
        UpdateLegend();
        UpdateStepLabel();
    }

    void CenterRoots()
    {
        float cx = (Width - 1) * TileSize * 0.5f;
        float cy = (Height - 1) * TileSize * 0.5f;

        GridRoot.localPosition = new Vector3(-cx, 0f, -cy);
        OverlayRoot.localPosition = new Vector3(-cx, 0f, -cy);
    }

    // ---------------- Buttons (SIM) ----------------
    public void Step()
    {
        step++;

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                float loss = UnityEngine.Random.Range(0.01f, 0.05f);
                moisture[x, y] = Mathf.Clamp01(moisture[x, y] - loss);

                if (disease[x, y] > 0.01f)
                {
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                            if (UnityEngine.Random.value < 0.05f)
                                disease[nx, ny] = Mathf.Clamp01(disease[nx, ny] + 0.2f);
                        }
                }

                float stress = 1f - moisture[x, y];
                float diseasePenalty = disease[x, y] * 0.5f;
                yield[x, y] = Mathf.Clamp01(yield[x, y] - (stress * 0.02f + diseasePenalty * 0.02f));
            }

        UpdateStepLabel();
        RenderAll();
    }

    public void Drought()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                moisture[x, y] = Mathf.Clamp01(moisture[x, y] - UnityEngine.Random.Range(0.08f, 0.18f));

        RenderAll();
    }

    public void Outbreak()
    {
        int cx = Width / 2;
        int cy = Height / 2;

        for (int x = cx - 2; x <= cx + 2; x++)
            for (int y = cy - 2; y <= cy + 2; y++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                disease[x, y] = 1f;
            }

        RenderAll();
    }

    public void ResetSim()
    {
        foreach (Transform t in GridRoot) Destroy(t.gameObject);
        foreach (Transform t in OverlayRoot) Destroy(t.gameObject);
        Init();
        RenderAll();
    }

    // ---------------- Buttons (VIEW) ----------------
    public void SetMoistureView()
    {
        mode = ViewMode.Moisture;
        UpdateLegend();
        RenderAll();
    }

    public void SetDiseaseView()
    {
        mode = ViewMode.Disease;
        UpdateLegend();
        RenderAll();
    }

    public void SetYieldView()
    {
        mode = ViewMode.Yield;
        UpdateLegend();
        RenderAll();
    }

    void UpdateLegend()
    {
        if (!LegendText || !LegendRangeText) return;

        if (mode == ViewMode.Moisture)
        {
            LegendText.text = "Moisture";
            LegendRangeText.text = "Dry → Wet";
        }
        else if (mode == ViewMode.Disease)
        {
            LegendText.text = "Disease";
            LegendRangeText.text = "Healthy → Infected";
        }
        else
        {
            LegendText.text = "Yield";
            LegendRangeText.text = "Low → High";
        }
    }

    void UpdateStepLabel()
    {
        if (StepText) StepText.text = $"Day: {step}";
    }

    void RenderAll()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                RenderTile(x, y);
                RenderDiseaseMarker(x, y);
            }
    }

    void RenderTile(int x, int y)
    {
        var r = tiles[x, y].GetComponent<MeshRenderer>();

        float v =
            mode == ViewMode.Moisture ? moisture[x, y] :
            mode == ViewMode.Yield ? yield[x, y] :
            1f - disease[x, y]; // keep tiles darker when disease is high

        Color c = Color.Lerp(new Color(0.8f, 0.7f, 0.2f), new Color(0.2f, 0.8f, 0.3f), v);

        if (mode == ViewMode.Disease)
        {
            // base is dark so red markers pop
            c = new Color(0.25f, 0.25f, 0.25f);
        }

        // NOTE: this creates new materials per tile each update (fine for a mock),
        // later we can optimize.
        r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        r.material.color = c;

        float h = 0.2f;
        if (ShowPlants) h = 0.2f + yield[x, y] * HeightScale * 0.3f;

        tiles[x, y].transform.localScale = new Vector3(TileSize, h, TileSize);
        tiles[x, y].transform.localPosition = new Vector3(x * TileSize, h * 0.5f, y * TileSize);
    }

    void RenderDiseaseMarker(int x, int y)
    {
        bool infected = disease[x, y] > 0.6f;
        var m = diseaseMarkers[x, y];

        if (!infected)
        {
            m.SetActive(false);
            return;
        }

        m.SetActive(true);
        var mr = m.GetComponent<MeshRenderer>();
        mr.material.color = new Color(1f, 0f, 0f, 0.85f);
    }

    // ==========================================================
    // Helpers for FarmDecorVisualizer (THIS FIXES YOUR ERROR)
    // ==========================================================

    // Returns average value across a band of X columns using current mode (0..1)
    public float GetBandValueX(int xStart, int xEnd)
    {
        if (moisture == null || disease == null || yield == null) return 0f;
        if (Width <= 0 || Height <= 0) return 0f;

        xStart = Mathf.Clamp(xStart, 0, Width - 1);
        xEnd = Mathf.Clamp(xEnd, xStart + 1, Width);

        float sum = 0f;
        int count = 0;

        for (int x = xStart; x < xEnd; x++)
            for (int y = 0; y < Height; y++)
            {
                float v =
                    mode == ViewMode.Moisture ? moisture[x, y] :
                    mode == ViewMode.Yield ? yield[x, y] :
                    disease[x, y]; // disease directly (0..1)

                sum += v;
                count++;
            }

        return count == 0 ? 0f : Mathf.Clamp01(sum / count);
    }

    // Converts a 0..1 band value into the correct color for current view
    public Color GetBandColor(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        if (mode == ViewMode.Disease)
        {
            // Healthy -> Infected : grey -> red
            return Color.Lerp(new Color(0.25f, 0.25f, 0.25f), new Color(1f, 0.1f, 0.1f), v01);
        }

        // Moisture / Yield: yellow -> green
        return Color.Lerp(new Color(0.8f, 0.7f, 0.2f), new Color(0.2f, 0.8f, 0.3f), v01);
    }
}
