using UnityEngine;

/// <summary>
/// RainController — drives rain visuals.
///
/// Primary:   DigitalRuby.RainMaker.BaseRainScript (if the RainMaker asset is in the scene).
/// Fallback:  A ParticleSystem named or tagged "Rain" (or assign rainParticles directly).
///
/// WeatherSystem calls StartRain() / StopRain() when real or simulated rain occurs.
/// </summary>
public class RainController : MonoBehaviour
{
    // ── RainMaker (optional) ───────────────────────────────────────────────────
    // Assign in Inspector if DigitalRuby RainMaker is in the scene.
    // If null, auto-discovery is attempted in Start().
    [Header("RainMaker (assign if available)")]
    public DigitalRuby.RainMaker.BaseRainScript rainScript;

    // ── Fallback particle system ───────────────────────────────────────────────
    [Header("Fallback ParticleSystem (used if RainMaker is absent)")]
    [Tooltip("Assign any ParticleSystem here. WeatherSystem will Play()/Stop() it.")]
    public ParticleSystem rainParticles;

    public bool isRaining = false;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        // Auto-discover RainMaker
        if (rainScript == null)
            rainScript = FindAnyObjectByType<DigitalRuby.RainMaker.BaseRainScript>();

        // Auto-discover fallback PS if not assigned
        if (rainParticles == null)
        {
            // Check children first
            rainParticles = GetComponentInChildren<ParticleSystem>();

            // Then search scene by name
            if (rainParticles == null)
            {
                GameObject rainGO = GameObject.Find("Rain")
                               ?? GameObject.Find("RainParticles")
                               ?? GameObject.Find("RainEffect")
                               ?? GameObject.FindWithTag("Rain");
                if (rainGO != null)
                    rainParticles = rainGO.GetComponent<ParticleSystem>();
            }
        }

        // Start with everything off
        SetRainOff();

        if (rainScript == null && rainParticles == null)
            Debug.LogWarning("[RainController] No RainMaker script and no ParticleSystem found. " +
                             "Assign rainParticles in the Inspector for rain visuals.");
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void StartRain()
    {
        isRaining = true;

        bool any = false;
        if (rainScript != null)   { rainScript.RainIntensity = 0.6f; any = true; }
        if (rainParticles != null) { if (!rainParticles.isPlaying) rainParticles.Play(); any = true; }
        if (!any) Debug.LogWarning("[RainController] StartRain called but no rain visual is configured.");
        else      Debug.Log("[RainController] Rain ON");
    }

    public void StopRain()
    {
        isRaining = false;
        SetRainOff();
    }

    public void Toggle()
    {
        if (isRaining) StopRain();
        else           StartRain();
    }

    /// <summary>Sets intensity to zero on all rain visuals without changing isRaining.</summary>
    public void SetRainIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        if (rainScript != null)
            rainScript.RainIntensity = intensity;

        if (rainParticles != null)
        {
            if (intensity <= 0f)
            {
                if (rainParticles.isPlaying) rainParticles.Stop();
            }
            else
            {
                var emission = rainParticles.emission;
                emission.rateOverTimeMultiplier = intensity * 300f;   // scale to particle count
                if (!rainParticles.isPlaying) rainParticles.Play();
            }
        }
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private void SetRainOff()
    {
        if (rainScript  != null) rainScript.RainIntensity = 0f;
        if (rainParticles != null && rainParticles.isPlaying) rainParticles.Stop();
    }
}
