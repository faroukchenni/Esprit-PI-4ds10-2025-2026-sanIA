using UnityEngine;
using DigitalRuby.RainMaker;

public class RainController : MonoBehaviour
{
    public BaseRainScript rainScript;
    public bool isRaining = false;

    /// <summary>
    /// True if rain is actually falling — checks both the flag AND the prefab's
    /// RainIntensity so external changes to the prefab are always detected.
    /// Use this instead of isRaining for gameplay logic.
    /// </summary>
    public bool IsRainingNow =>
        isRaining || (rainScript != null && rainScript.RainIntensity > 0.01f);

    void Start()
    {
        // Find rain in scene if not assigned
        if (rainScript == null)
            rainScript = FindAnyObjectByType<BaseRainScript>();

        // Start with rain off
        if (rainScript != null)
            rainScript.RainIntensity = 0f;
    }

    void Update()
    {
        // Keep the isRaining flag in sync with the actual prefab intensity.
        // This means if something external turns rain on/off via the prefab directly,
        // the flag and IsRainingNow both stay accurate.
        if (rainScript != null)
            isRaining = rainScript.RainIntensity > 0.01f;
    }

    public void StartRain()
    {
        if (rainScript == null) return;
        isRaining = true;
        rainScript.RainIntensity = 0.5f;
        Debug.Log("RAIN ON - intensity 0.5");
    }

    public void StopRain()
    {
        if (rainScript == null) return;
        isRaining = false;
        rainScript.RainIntensity = 0f;
        Debug.Log("RAIN OFF");
    }

    public void Toggle()
    {
        if (isRaining) StopRain();
        else StartRain();
    }
}
