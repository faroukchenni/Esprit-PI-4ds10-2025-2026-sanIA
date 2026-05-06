using UnityEngine;
using System.Collections.Generic;
using System;

public class TwinSimulationManager : MonoBehaviour
{
    public static TwinSimulationManager Instance { get; private set; }

    public event Action<int> OnDayAdvanced;
    public event Action<float> OnHourAdvanced;

    [Header("Grid Dimensions")]
    public int gridWidth = 20;
    public int gridHeight = 20;

    [Header("Simulation Time")]
    public int   currentDay  = 0;
    public float currentHour = 0f;
    [Tooltip("How many real seconds equal 1 simulation hour")]
    public float timeScale = 10.0f;  // 1 day = 24 × 10 = 240 real seconds by default

    [Tooltip("Reference: real seconds for one full simulation day (informational — actual speed set by timeScale).")]
    public float dayDurationSeconds = 120f;

    /// <summary>Normalized time of day: 0=midnight, 0.25=6AM, 0.5=noon, 0.75=6PM, 1=midnight.</summary>
    public float simulationTimeOfDay => currentHour / 24f;

    public GridCellData[,] gridData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        gridData = new GridCellData[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Create 2-unit wide paths for a "Real Farm Road" look
                if (Mathf.Abs(x - gridWidth / 2) <= 1 || Mathf.Abs(y - gridHeight / 2) <= 1)
                {
                    gridData[x, y] = new GridCellData(25f, CropType.Empty);
                    continue;
                }

                // Assign crops matching your dataset
                CropType type = CropType.Tomato;
                if (x < gridWidth / 2 && y < gridHeight / 2) type = CropType.Potato;
                else if (x >= gridWidth / 2 && y < gridHeight / 2) type = CropType.Tomato;
                else if (x < gridWidth / 2 && y >= gridHeight / 2) type = CropType.Grape;
                else type = CropType.Apple;

                gridData[x, y] = new GridCellData(25.0f, type);
            }
        }
        TwinEventLogger.Log("SYSTEM", $"Initialized {gridWidth}x{gridHeight} Field with 4 Zones.", "info");
    }

    private void Update()
    {
        AdvanceTime(Time.deltaTime * timeScale);
    }

    private void AdvanceTime(float hoursDelta)
    {
        float previousHour = currentHour;
        currentHour += hoursDelta;

        // Trigger hourly event if we crossed a whole number
        if (Mathf.FloorToInt(currentHour) > Mathf.FloorToInt(previousHour))
        {
            OnHourAdvanced?.Invoke(currentHour);
        }

        if (currentHour >= 24f)
        {
            currentHour -= 24f;
            currentDay++;
            OnDayCompleted();
        }
    }

    private void OnDayCompleted()
    {
        TwinEventLogger.Log("SIMULATION", $"Day advanced to {currentDay}", "info");
        OnDayAdvanced?.Invoke(currentDay);
    }
}
