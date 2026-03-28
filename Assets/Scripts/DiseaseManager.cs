using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Disease spread simulation, treatment management, and sensor integration.
///
/// Architecture:
///   - This class mutates GridCellData (IsInfected, InfectionDay, etc.) on day ticks.
///   - CyberGrid.UpdateVisuals() reads that data each frame to render crop/soil colours.
///   - No per-frame visual work happens here — only data + event-driven updates.
/// </summary>
public class DiseaseManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static DiseaseManager Instance { get; private set; }

    // ── Spread parameters ─────────────────────────────────────────────────────
    [Header("Spread Settings")]
    [Range(0f, 0.5f)] public float baseSpreadChance      = 0.15f; // 15 % per neighbour per day
    [Range(1f, 4f)]   public float humidityMultiplier    = 2.0f;  // × 2.0 when cell humidity > 70 %
    [Range(1f, 3f)]   public float rainMultiplier        = 1.5f;  // × 1.5 while rain is active
    [Range(0f, 1f)]   public float droughtMultiplier     = 0.5f;  // × 0.5 during drought
    [Range(1f, 6f)]   public float windMultiplier        = 3.0f;  // × 3.0 for downwind neighbour
    [Range(1, 10)]    public int   treatmentImmunityDays = 3;     // days of reinfection immunity

    // ── Internal ──────────────────────────────────────────────────────────────
    private TwinSimulationManager _sim;
    private CyberGrid             _grid;
    private WeatherSystem         _weather;

    /// <summary>[x, y] fast lookup into CyberGrid.spawnedCells (null for paths / hub cells).</summary>
    private SpawnedCellInfo[,] _cellGrid;

    private Vector2Int _windDir; // refreshed each day

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    IEnumerator Start()
    {
        // Wait for TwinSimulationManager to initialise its grid
        _sim = TwinSimulationManager.Instance;
        while (_sim == null || _sim.gridData == null)
        {
            yield return null;
            _sim = TwinSimulationManager.Instance;
        }

        _grid    = Object.FindFirstObjectByType<CyberGrid>();
        _weather = Object.FindFirstObjectByType<WeatherSystem>();

        // Wait for CyberGrid to finish spawning (it also uses IEnumerator Start)
        while (_grid == null || _grid.spawnedCells == null || _grid.spawnedCells.Count == 0)
        {
            yield return null;
            if (_grid == null) _grid = Object.FindFirstObjectByType<CyberGrid>();
        }

        BuildCellGrid();
        _sim.OnDayAdvanced += OnDayAdvanced;
        _windDir = RandomWindDir();

        Debug.Log($"[DiseaseManager] Ready — {_sim.gridWidth}×{_sim.gridHeight} grid, " +
                  $"{_grid.spawnedCells.Count} tracked cells.");
    }

    void OnDestroy()
    {
        if (_sim != null) _sim.OnDayAdvanced -= OnDayAdvanced;
    }

    // ─── Cell-grid lookup ────────────────────────────────────────────────────
    void BuildCellGrid()
    {
        _cellGrid = new SpawnedCellInfo[_sim.gridWidth, _sim.gridHeight];
        foreach (var cell in _grid.spawnedCells)
        {
            if (cell.GridX >= 0 && cell.GridX < _sim.gridWidth &&
                cell.GridY >= 0 && cell.GridY < _sim.gridHeight)
                _cellGrid[cell.GridX, cell.GridY] = cell;
        }
    }

    // ─── Day tick ─────────────────────────────────────────────────────────────
    void OnDayAdvanced(int day)
    {
        _windDir = RandomWindDir();
        SpreadDisease(day);
        CheckAlerts();
    }

    // ─── Spread algorithm ─────────────────────────────────────────────────────
    /// <summary>
    /// Spread formula per neighbour:
    ///   chance = baseSpreadChance
    ///          × humidityMultiplier  (if target humidity > 70 %)
    ///          × rainMultiplier      (if rain active)
    ///          × droughtMultiplier   (if drought active)
    ///          × windMultiplier      (if neighbour is downwind)
    /// Treated cells are immune for treatmentImmunityDays after treatment.
    /// </summary>
    void SpreadDisease(int day)
    {
        // Snapshot so new infections this tick don't chain-spread in the same pass
        bool[,] wasInfected = new bool[_sim.gridWidth, _sim.gridHeight];
        for (int x = 0; x < _sim.gridWidth; x++)
            for (int y = 0; y < _sim.gridHeight; y++)
                wasInfected[x, y] = _sim.gridData[x, y].IsInfected;

        for (int x = 0; x < _sim.gridWidth; x++)
        {
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                if (!wasInfected[x, y]) continue;

                GridCellData src = _sim.gridData[x, y];

                // Advance existing infection
                src.InfectionSeverity = Mathf.Min(1f, src.InfectionSeverity + 0.1f);
                src.DiseaseLevel      = Mathf.Min(1f, src.DiseaseLevel      + 0.05f);

                // Try to spread to 4 neighbours
                TryInfect(x + 1, y,     day, _windDir == Vector2Int.right);
                TryInfect(x - 1, y,     day, _windDir == Vector2Int.left);
                TryInfect(x,     y + 1, day, _windDir == Vector2Int.up);
                TryInfect(x,     y - 1, day, _windDir == Vector2Int.down);
            }
        }
    }

    void TryInfect(int tx, int ty, int day, bool isDownwind)
    {
        if (tx < 0 || tx >= _sim.gridWidth || ty < 0 || ty >= _sim.gridHeight) return;

        GridCellData target = _sim.gridData[tx, ty];
        if (target.Crop == CropType.Empty) return; // paths cannot be infected
        if (target.IsInfected)            return; // already infected

        // Respect treatment immunity window
        if (target.IsTreated && (_sim.currentDay - target.TreatmentDay) < treatmentImmunityDays)
            return;

        // Build spread probability
        float chance = baseSpreadChance;
        if (target.MoistureLevel * 100f > 70f)             chance *= humidityMultiplier;
        bool isRaining = Object.FindFirstObjectByType<RainController>()?.isRaining ?? false;
        if (isRaining)                                      chance *= rainMultiplier;
        if (_weather != null && _weather.isDrought)         chance *= droughtMultiplier;
        if (isDownwind)                                     chance *= windMultiplier;

        if (Random.value < chance)
        {
            target.IsInfected        = true;
            target.InfectionDay      = day;
            target.InfectionSeverity = 0.15f;
            target.IsTreated         = false;
            target.DiseaseLevel      = Mathf.Max(target.DiseaseLevel, 0.15f);

            TwinEventLogger.Log("DISEASE", $"Spread to [{tx},{ty}] ({target.Crop})", "warn");
            _grid?.StartInfectionFlash(tx, ty);
        }
    }

    // ─── Alert system ─────────────────────────────────────────────────────────
    void CheckAlerts()
    {
        int count = CountActiveInfections();
        if (count > 15)
            TwinEventLogger.Log("DISEASE",
                $"CRITICAL: Disease outbreak — {count} cells infected. Treat immediately!", "error");
        else if (count > 5)
            TwinEventLogger.Log("DISEASE",
                $"WARNING: Disease spreading — {count} cells infected", "warn");
    }

    int CountActiveInfections()
    {
        int n = 0;
        for (int x = 0; x < _sim.gridWidth; x++)
            for (int y = 0; y < _sim.gridHeight; y++)
                if (_sim.gridData[x, y].IsInfected && !_sim.gridData[x, y].IsTreated)
                    n++;
        return n;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Manually infect a single cell.
    /// row = grid Y index, col = grid X index.
    /// severity 0–1 sets initial InfectionSeverity.
    /// </summary>
    public void InfectCell(int row, int col, float severity)
    {
        if (_sim == null || _sim.gridData == null) return;
        if (col < 0 || col >= _sim.gridWidth || row < 0 || row >= _sim.gridHeight)
        {
            Debug.LogWarning($"[DiseaseManager] InfectCell out of range: row={row} col={col}");
            return;
        }

        GridCellData cell = _sim.gridData[col, row];
        if (cell.Crop == CropType.Empty) return;

        cell.IsInfected        = true;
        cell.InfectionDay      = _sim.currentDay;
        cell.InfectionSeverity = Mathf.Clamp01(severity);
        cell.IsTreated         = false;
        cell.DiseaseLevel      = Mathf.Max(cell.DiseaseLevel, severity);

        _grid?.StartInfectionFlash(col, row);

        Debug.Log($"[DiseaseManager] DISEASE REPORTED at row:{row} col:{col} severity:{severity * 100f:F0}%");
        TwinEventLogger.Log("DISEASE",
            $"Outbreak at [{col},{row}] {cell.Crop} — severity {severity * 100f:F0}%", "error");
    }

    /// <summary>Infects the centre cell of the named crop zone (e.g. "Tomato").</summary>
    public void InfectZone(string zoneName, float severity)
    {
        if (_sim == null || _grid == null) return;

        long sumX = 0, sumY = 0;
        int  count = 0;
        foreach (var cell in _grid.spawnedCells)
        {
            if (cell.zoneName == zoneName) { sumX += cell.GridX; sumY += cell.GridY; count++; }
        }

        if (count == 0)
        {
            Debug.LogWarning($"[DiseaseManager] InfectZone: zone '{zoneName}' not found.");
            return;
        }

        InfectCell((int)(sumY / count), (int)(sumX / count), severity); // row = y, col = x
    }

    /// <summary>Applies treatment to all infected cells in the named zone.</summary>
    public void TreatZone(string zoneName)
    {
        if (_sim == null || _grid == null) return;

        int treated = 0;
        foreach (var cell in _grid.spawnedCells)
        {
            if (cell.zoneName != zoneName) continue;
            GridCellData data = _sim.gridData[cell.GridX, cell.GridY];
            if (!data.IsInfected) continue;

            data.IsTreated    = true;
            data.TreatmentDay = _sim.currentDay;

            // Restore soil that was darkened by late infection (CyberGrid.UpdateVisuals
            // stops darkening the moment IsTreated = true, but material instance keeps
            // the old colour until explicitly reset)
            if (_grid.soilRenderers != null)
            {
                Renderer sr = _grid.soilRenderers[cell.GridX, cell.GridY];
                if (sr != null)
                {
                    sr.material.SetColor("_BaseColor", cell.originalColor);
                    sr.material.color = cell.originalColor;
                }
            }
            treated++;
        }

        TwinEventLogger.Log("TREATMENT", $"TREATMENT APPLIED — {zoneName}: {treated} cells treated", "info");
        Debug.Log($"[DiseaseManager] TREATMENT APPLIED - {zoneName} ({treated} cells)");
    }

    /// <summary>
    /// Clears ALL disease state from the entire grid and restores soil colours.
    /// Called by FuturisticUI.OnReset().
    /// </summary>
    public void ClearAllDiseases()
    {
        if (_sim == null) return;

        for (int x = 0; x < _sim.gridWidth; x++)
        {
            for (int y = 0; y < _sim.gridHeight; y++)
            {
                GridCellData d = _sim.gridData[x, y];
                if (!d.IsInfected) continue;

                // Restore darkened soil (if soil renderer exists for this cell)
                if (_cellGrid != null && _grid?.soilRenderers != null)
                {
                    SpawnedCellInfo ci = _cellGrid[x, y];
                    Renderer        sr = _grid.soilRenderers[x, y];
                    if (ci != null && sr != null)
                    {
                        sr.material.SetColor("_BaseColor", ci.originalColor);
                        sr.material.color = ci.originalColor;
                    }
                }

                d.IsInfected        = false;
                d.IsTreated         = false;
                d.InfectionDay      = 0;
                d.InfectionSeverity = 0f;
                d.DiseaseLevel      = 0f;
            }
        }

        Debug.Log("[DiseaseManager] All diseases cleared.");
        TwinEventLogger.Log("TREATMENT", "All diseases cleared — system reset", "info");
    }

    // ─── Sensor integration hook ──────────────────────────────────────────────

    /// <summary>
    /// Call from MQTT / REST / IoT bridge when a sensor reports disease.
    /// Ready for real-data integration — wire to your data pipeline here.
    /// </summary>
    public void ReportDiseaseFromSensor(
        int    row,
        int    col,
        string cropType,
        float  severity,
        string diseaseType)
    {
        InfectCell(row, col, severity);
        Debug.Log($"[DiseaseManager] SENSOR REPORT: {diseaseType} on {cropType} " +
                  $"at [{row},{col}] severity:{severity:F2}");
        TwinEventLogger.Log("SENSOR",
            $"{diseaseType} detected on {cropType} [{row},{col}] — {severity * 100f:F0}%", "error");
    }

    // ─── Utility ──────────────────────────────────────────────────────────────
    static Vector2Int RandomWindDir()
    {
        int d = Random.Range(0, 4);
        if (d == 0) return Vector2Int.right;
        if (d == 1) return Vector2Int.left;
        if (d == 2) return Vector2Int.up;
        return Vector2Int.down;
    }
}
