using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary helper spawned at runtime by "Test All Scenarios" menu.
/// Cycles all 4 scenarios with a 3-second delay between each.
/// Self-destructs when done.
/// </summary>
public class ScenarioTester : MonoBehaviour
{
    public void RunAll(ScenarioManager sm)
    {
        StartCoroutine(Cycle(sm));
    }

    IEnumerator Cycle(ScenarioManager sm)
    {
        yield return new WaitForSeconds(0.2f);
        sm.RunDrought();
        yield return new WaitForSeconds(3f);

        sm.RunHeatwave();
        yield return new WaitForSeconds(3f);

        sm.RunDisease();
        yield return new WaitForSeconds(3f);

        sm.RunHealthy();
        yield return new WaitForSeconds(1f);

        Destroy(gameObject);
    }
}
