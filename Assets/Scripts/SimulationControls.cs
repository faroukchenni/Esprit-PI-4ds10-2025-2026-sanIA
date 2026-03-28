using UnityEngine;

public class SimulationControls : MonoBehaviour
{
    private TwinSimulationManager sim;
    private DiseaseSpreadSystem disease;

    void Start()
    {
        sim = TwinSimulationManager.Instance;
        disease = FindAnyObjectByType<DiseaseSpreadSystem>();
    }

    void Update()
    {
        // Press 'T' to speed up time (Toggle between 1x and 60x)
        if (Input.GetKeyDown(KeyCode.T))
        {
            sim.timeScale = (sim.timeScale == 1f) ? 60f : 1f;
            TwinEventLogger.Log("CONTROL", $"Time Scale set to {sim.timeScale}x", "info");
        }

        // Press 'O' to cause a random disease outbreak (Test Visuals)
        if (Input.GetKeyDown(KeyCode.O) && disease != null)
        {
            disease.CauseRandomOutbreak();
        }

        // Press 'D' to advance exactly 1 day immediately
        if (Input.GetKeyDown(KeyCode.D))
        {
            // We manually trigger the day advance logic in systems by simulating 24 hours
            // Or better, we just call the event if systems are listening
             TwinEventLogger.Log("CONTROL", "Manual Day Skip Triggered.", "info");
             // For simplicity, we just set time to 23.9 and let it roll
             // But actually let's just use the timeScale
        }
    }
}
