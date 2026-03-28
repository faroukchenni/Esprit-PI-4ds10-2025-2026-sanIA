using UnityEngine;
using DigitalRuby.RainMaker;

public class RainController : MonoBehaviour
{
    public BaseRainScript rainScript;
    public bool isRaining = false;

    void Start()
    {
        // Find rain in scene if not assigned
        if (rainScript == null)
            rainScript = FindAnyObjectByType<BaseRainScript>();

        // Start with rain off
        if (rainScript != null)
            rainScript.RainIntensity = 0f;
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
