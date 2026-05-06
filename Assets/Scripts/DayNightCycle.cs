using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Full day/night cycle synchronised with TwinSimulationManager.currentHour.
///
/// Time mapping  (0 = midnight, 0.25 = 6 AM, 0.5 = noon, 0.75 = 6 PM)
///   0.00–0.25  Night → Dawn → Sunrise          (morning phase)
///   0.25–0.75  Full daylight                   (midday phase)
///   0.75–0.90  Sunset / evening transition     (evening phase)
///   0.90–1.00  Night                           (night phase, wraps to 0.00)
///
/// Skybox is replaced by camera.backgroundColor so sun/moon spheres are never
/// rendered behind it. Arc is camera-relative so the sun always arcs
/// overhead from the player's point of view.
///
/// Press F1 at runtime to print a full diagnostic to the Console.
/// </summary>
[DefaultExecutionOrder(10)]   // run after TwinSimulationManager
public class DayNightCycle : MonoBehaviour
{
    // ── References ─────────────────────────────────────────────────────────────
    [Header("Light References")]
    public Light sunLight;
    public Light moonLight;

    [Header("Sky Visuals")]
    public Transform sunVisual;
    public Transform moonVisual;
    public Light     sunPointLight;

    [Header("Stars")]
    public ParticleSystem starsParticleSystem;   // kept for backwards compat; disabled at Start

    [Header("UI")]
    public TMP_Text              timeDisplayText;
    public UnityEngine.UI.Slider timeSlider;

    // ── Time ───────────────────────────────────────────────────────────────────
    [Header("Time Settings")]
    [Tooltip("Simulation hour when the scene first loads (0=midnight, 6=sunrise, 12=noon).")]
    public float startHour = 6f;
    [Tooltip("Real-time seconds per full simulation day when TwinSimulationManager is absent.")]
    public float dayDurationSeconds = 120f;

    // ── Sun ────────────────────────────────────────────────────────────────────
    [Header("Sun Colors")]
    public Color sunriseColor = new Color(1.000f, 0.420f, 0.208f); // #FF6B35
    public Color daytimeColor = new Color(1.000f, 0.831f, 0.627f); // #FFD4A0
    public Color sunsetColor  = new Color(1.000f, 0.271f, 0.000f); // #FF4500
    public Color nightColor   = new Color(0.102f, 0.102f, 0.306f); // #1A1A4E

    [Header("Sun Intensity")]
    public float dayIntensity     = 1.2f;
    public float sunriseIntensity = 0.3f;
    public float nightIntensity   = 0.05f;

    // ── Sky ────────────────────────────────────────────────────────────────────
    [Header("Sky Ambient Colors")]
    public Color dawnSkyColor    = new Color(1.000f, 0.420f, 0.208f); // #FF6B35
    public Color morningSkyColor = new Color(0.529f, 0.808f, 0.922f); // #87CEEB
    public Color noonSkyColor    = new Color(0.290f, 0.565f, 0.851f); // #4A90D9
    public Color sunsetSkyColor  = new Color(1.000f, 0.271f, 0.000f); // #FF4500
    public Color nightSkyColor   = new Color(0.039f, 0.039f, 0.180f); // #0A0A2E

    // ── Fog ────────────────────────────────────────────────────────────────────
    [Header("Fog Colors")]
    public Color dayFogColor    = new Color(0.722f, 0.831f, 0.659f); // #B8D4A8
    public Color sunsetFogColor = new Color(1.000f, 0.702f, 0.278f); // #FFB347
    public Color nightFogColor  = new Color(0.039f, 0.039f, 0.180f); // #0A0A2E

    // ── Moon ───────────────────────────────────────────────────────────────────
    [Header("Moon")]
    public Color moonColor     = new Color(0.784f, 0.847f, 1.000f); // #C8D8FF
    public float moonIntensity = 0.08f;

    // ── Private ────────────────────────────────────────────────────────────────
    float        _t;            // 0..1 normalised time of day
    bool         _sliderDriven;
    MeshRenderer _sunRenderer;
    MeshRenderer _moonRenderer;
    Camera       _mainCam;
    Transform    _coronaQuad;   // billboard glow child of SunVisual
    Transform[]  _starSpheres;  // 50 simple sphere stars
    float        _frozenTimer;  // unused — kept to avoid serialization errors
    Texture2D    _sunTexture;
    Texture2D    _moonTexture;

    // Arc radii (camera-relative, so they always arc overhead regardless of cam position)
    const float SUN_RADIUS  = 80f;
    const float MOON_RADIUS = 80f;

    static readonly Color s_groundDay   = new Color(0.420f, 0.267f, 0.137f);
    static readonly Color s_groundNight = new Color(0.039f, 0.039f, 0.180f);

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        // ── Camera: solid-color background, no skybox, extended far clip ──────
        _mainCam = Camera.main;
        if (_mainCam == null) _mainCam = Object.FindFirstObjectByType<Camera>();
        if (_mainCam != null)
        {
            _mainCam.clearFlags   = CameraClearFlags.SolidColor;
            _mainCam.farClipPlane = 1000f;
            _mainCam.backgroundColor = noonSkyColor;
        }
        // Remove scene skybox so nothing renders over the sun/moon spheres
        RenderSettings.skybox = null;

        // ── Seed simulation time ──────────────────────────────────────────────
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (sim != null && sim.currentHour < 0.01f)
            sim.currentHour = Mathf.Clamp(startHour, 0f, 23.99f);
        _t = (sim != null ? sim.currentHour : startHour) / 24f;

        // ── Sun visual — material built same way as stars ─────────────────────
        if (sunVisual != null)
        {
            _sunRenderer = sunVisual.GetComponent<MeshRenderer>();
            FixVisualMaterial(_sunRenderer,
                baseColor:     new Color(1f, 0.898f, 0.400f, 1f),  // #FFE566
                emissionColor: new Color(1f, 0.843f, 0.000f, 1f) * 3f,
                scale:         8f);
            CreateCorona();
        }

        // ── Moon visual — material built same way as stars ────────────────────
        if (moonVisual != null)
        {
            _moonRenderer = moonVisual.GetComponent<MeshRenderer>();
            FixVisualMaterial(_moonRenderer,
                baseColor:     new Color(0.909f, 0.909f, 1.000f, 1f), // #E8E8FF
                emissionColor: new Color(0.784f, 0.847f, 1.000f, 1f) * 2f,
                scale:         6f);
        }

        // ── Disable 3D sun/moon spheres — replaced by OnGUI 2D circles ─────────
        if (sunVisual  != null) sunVisual.gameObject.SetActive(false);
        if (moonVisual != null) moonVisual.gameObject.SetActive(false);

        // ── Stars: replace particle system with sphere GameObjects ────────────
        if (starsParticleSystem != null)
            starsParticleSystem.gameObject.SetActive(false);
        CreateStarSpheres();

        // ── 2D GUI textures: radial gradient for soft glowing circles ─────────
        _sunTexture  = CreateRadialTexture(256, new Color(1f, 0.9f, 0.3f, 1f));
        _moonTexture = CreateRadialTexture(256, new Color(0.878f, 0.878f, 1f, 1f)); // #E0E0FF

        // ── UI ────────────────────────────────────────────────────────────────
        if (timeSlider != null)
            timeSlider.onValueChanged.AddListener(OnSliderMoved);

        ApplyAll();
    }

    /// <summary>
    /// Builds a brand-new Material from scratch — identical to how star materials
    /// are created — and assigns it via sharedMaterial.
    /// This bypasses any broken/stale asset material that may be on the renderer.
    /// renderQueue = 3700 to match stars (confirmed visible).
    /// </summary>
    void FixVisualMaterial(MeshRenderer mr, Color baseColor, Color emissionColor, float scale)
    {
        if (mr == null) return;

        mr.transform.localScale = Vector3.one * scale;

        // Build the shader the same way stars do
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");

        // new Material(sh) — exactly what stars use, guaranteed clean slate
        Material mat = new Material(sh);
        Color c = baseColor; c.a = 1f;
        // URP/Unlit uses _BaseColor; Standard uses _Color. Set both to be safe.
        mat.SetColor("_BaseColor", c);
        mat.color = c;
        mat.SetFloat("_Surface", 0f);   // Opaque
        mat.renderQueue = 3700;         // Same queue as stars (confirmed visible)
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);

        // sharedMaterial — same assignment path as stars (mr.sharedMaterial = starMat)
        mr.sharedMaterial = mat;
        mr.enabled = true;
    }

    /// <summary>Creates a billboard corona quad as a child of sunVisual.</summary>
    void CreateCorona()
    {
        // Re-use existing if it survived a domain reload
        Transform existing = sunVisual.Find("SunCorona");
        if (existing != null) { _coronaQuad = existing; return; }

        GameObject cGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cGO.name = "SunCorona";
        cGO.transform.SetParent(sunVisual, false);
        // localScale relative to parent (scale 10) → world size ≈ 15
        cGO.transform.localScale    = new Vector3(1.5f, 1.5f, 1f);
        cGO.transform.localPosition = Vector3.zero;

        Object.Destroy(cGO.GetComponent<Collider>());

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");

        Material coronaMat = new Material(sh);
        coronaMat.color = new Color(1f, 0.843f, 0f, 0.4f); // #FFD700 semi-transparent
        coronaMat.SetFloat("_Surface", 1);  // Transparent
        coronaMat.SetFloat("_Blend",   0);  // Alpha blend
        coronaMat.renderQueue = 3600;

        cGO.GetComponent<MeshRenderer>().material = coronaMat;
        _coronaQuad = cGO.transform;
    }

    /// <summary>Spawns 50 small sphere GameObjects on a hemisphere for stars.</summary>
    void CreateStarSpheres()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");

        // Shared star material (white + strong emission, opaque, high queue)
        Material starMat = new Material(sh);
        starMat.color = Color.white;
        starMat.SetFloat("_Surface", 0f);
        starMat.renderQueue = 3700;
        starMat.EnableKeyword("_EMISSION");
        starMat.SetColor("_EmissionColor", Color.white * 2f);

        _starSpheres = new Transform[50];
        for (int i = 0; i < 50; i++)
        {
            // Random point on upper hemisphere (avoid near-horizon clutter)
            float az  = Random.Range(0f, Mathf.PI * 2f);
            float el  = Random.Range(0.15f, Mathf.PI * 0.5f); // 8.6°–90° elevation
            Vector3 dir = new Vector3(
                Mathf.Sin(el) * Mathf.Cos(az),
                Mathf.Cos(el),
                Mathf.Sin(el) * Mathf.Sin(az));

            GameObject sGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sGO.name = "Star_" + i.ToString("D2");
            sGO.transform.SetParent(transform, false);
            sGO.transform.position   = dir * 200f;
            sGO.transform.localScale = Vector3.one * 0.8f;

            Object.Destroy(sGO.GetComponent<Collider>());
            MeshRenderer mr = sGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = starMat;

            sGO.SetActive(false);   // hidden until night
            _starSpheres[i] = sGO.transform;
        }
    }

    void OnDestroy()
    {
        if (timeSlider != null)
            timeSlider.onValueChanged.RemoveListener(OnSliderMoved);
    }

    void OnSliderMoved(float value)
    {
        _sliderDriven = true;
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (sim != null) sim.currentHour = value * 24f;
        else             _t = value;
    }

    void Update()
    {
        // ── Advance time ──────────────────────────────────────────────────────
        TwinSimulationManager sim = TwinSimulationManager.Instance;
        if (sim != null) _t = sim.simulationTimeOfDay;
        else             _t = (_t + Time.deltaTime / dayDurationSeconds) % 1f;

        ApplyAll();

        // ── Corona always faces camera ────────────────────────────────────────
        if (_coronaQuad != null && _mainCam != null && _coronaQuad.gameObject.activeSelf)
            _coronaQuad.rotation = _mainCam.transform.rotation;

        // ── Slider sync ───────────────────────────────────────────────────────
        if (!_sliderDriven && timeSlider != null)
            timeSlider.SetValueWithoutNotify(_t);
        _sliderDriven = false;

        // ── F1 diagnostic ─────────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.F1))
            DebugSunMoon();
    }

    // ── Apply all systems ─────────────────────────────────────────────────────

    void ApplyAll()
    {
        ApplySun();
        // ApplySunVisual / ApplyMoonVisual replaced by OnGUI 2D circles
        ApplySky();
        ApplyFog();
        ApplyMoon();
        ApplyStars();
        ApplyTimeDisplay();
    }

    // ── Sun directional light ──────────────────────────────────────────────────

    void ApplySun()
    {
        if (sunLight == null) return;
        float sunX = 10f + _t * 360f;
        sunLight.transform.rotation = Quaternion.Euler(sunX, 30f, 0f);
        sunLight.color     = EvalSunColor(_t);
        sunLight.intensity = EvalSunIntensity(_t);
    }

    Color EvalSunColor(float t)
    {
        if (t < 0.25f) return Color.Lerp(sunriseColor, daytimeColor, t / 0.25f);
        if (t < 0.75f) return daytimeColor;
        if (t < 0.90f) return Color.Lerp(daytimeColor, sunsetColor,  (t - 0.75f) / 0.15f);
                       return Color.Lerp(sunsetColor,  nightColor,   (t - 0.90f) / 0.10f);
    }

    float EvalSunIntensity(float t)
    {
        if (t < 0.25f) return Mathf.Lerp(sunriseIntensity, dayIntensity,     t / 0.25f);
        if (t < 0.75f) return dayIntensity;
        if (t < 0.90f) return Mathf.Lerp(dayIntensity,     sunriseIntensity, (t - 0.75f) / 0.15f);
                       return Mathf.Lerp(sunriseIntensity, nightIntensity,   (t - 0.90f) / 0.10f);
    }

    // ── Sun visual sphere (viewport-space placement) ──────────────────────────
    // Uses ViewportToWorldPoint so the sun always appears in the visible sky
    // area regardless of camera angle, position, or FOV.
    // viewY 0.65–0.90 = upper sky band (top ~35% of screen).
    // viewX 0–1 = left to right across the screen.
    // viewZ = depth from camera in world units.

    void ApplySunVisual()
    {
        if (sunVisual == null || _mainCam == null) return;

        float sunAngle = (_t * 360f - 90f) * Mathf.Deg2Rad;
        float sx = Mathf.Cos(sunAngle);   // -1 (west) to +1 (east)
        float sy = Mathf.Sin(sunAngle);   // -1 (below) to +1 (above)

        bool aboveHorizon = sy > -0.1f;
        sunVisual.gameObject.SetActive(aboveHorizon);
        if (_sunRenderer != null) _sunRenderer.enabled = aboveHorizon;

        if (aboveHorizon)
        {
            float viewX = (sx + 1f) * 0.5f;          // 0 = left edge, 1 = right edge
            float viewY = 0.72f + sy * 0.15f;        // 0.72 (horizon) to 0.87 (zenith) — never clipped
            float viewZ = 150f;                       // depth from camera

            Vector3 worldPos = _mainCam.ViewportToWorldPoint(
                new Vector3(viewX, viewY, viewZ));
            sunVisual.position = worldPos;

            if (_coronaQuad != null) _coronaQuad.gameObject.SetActive(true);

            if (sunPointLight != null)
            {
                sunPointLight.gameObject.SetActive(true);
                sunPointLight.intensity = Mathf.Clamp01(sy) * 0.5f;
            }
        }
        else
        {
            if (_coronaQuad  != null) _coronaQuad.gameObject.SetActive(false);
            if (sunPointLight != null) sunPointLight.gameObject.SetActive(false);
        }
    }

    // ── Sky ambient + camera background ───────────────────────────────────────

    void ApplySky()
    {
        float n   = NightBlend(_t);
        Color sky = EvalSkyColor(_t);
        Color eq  = Color.Lerp(new Color(0.545f, 0.765f, 0.290f), sky, 0.4f);
        Color gnd = Color.Lerp(s_groundDay, s_groundNight, n);

        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = sky;
        RenderSettings.ambientEquatorColor = eq;
        RenderSettings.ambientGroundColor  = gnd;

        // Drive the camera background colour — this IS the sky since skybox is removed
        if (_mainCam != null)
            _mainCam.backgroundColor = sky;
    }

    Color EvalSkyColor(float t)
    {
        // Night is solid #0A0A2E from t=0.85 (≈8.4 PM) through midnight to t=0.15 (≈3.6 AM).
        // This matches NightBlend() boundaries so sky and lighting stay in sync.
        if (t >= 0.85f || t < 0.15f) return nightSkyColor;
        if (t < 0.25f) return Color.Lerp(nightSkyColor,   morningSkyColor, (t - 0.15f) / 0.10f);
        if (t < 0.50f) return Color.Lerp(morningSkyColor, noonSkyColor,    (t - 0.25f) / 0.25f);
        if (t < 0.75f) return Color.Lerp(noonSkyColor,    sunsetSkyColor,  (t - 0.50f) / 0.25f);
                       return Color.Lerp(sunsetSkyColor,  nightSkyColor,   (t - 0.75f) / 0.10f);
    }

    // ── Fog ───────────────────────────────────────────────────────────────────

    void ApplyFog()
    {
        Color fog;
        if      (_t < 0.10f) fog = nightFogColor;
        else if (_t < 0.15f) fog = Color.Lerp(nightFogColor, sunriseColor,   (_t - 0.10f) / 0.05f);
        else if (_t < 0.25f) fog = Color.Lerp(sunriseColor,  dayFogColor,    (_t - 0.15f) / 0.10f);
        else if (_t < 0.75f) fog = dayFogColor;
        else if (_t < 0.85f) fog = Color.Lerp(dayFogColor,   sunsetColor,    (_t - 0.75f) / 0.10f);
        else if (_t < 0.90f) fog = Color.Lerp(sunsetColor,   nightFogColor,  (_t - 0.85f) / 0.05f);
        else                 fog = nightFogColor;

        RenderSettings.fog      = true;
        RenderSettings.fogColor = fog;
    }

    // ── Moon directional light ────────────────────────────────────────────────

    void ApplyMoon()
    {
        if (moonLight == null) return;
        float n = NightBlend(_t);
        moonLight.intensity = n * moonIntensity;
        moonLight.color     = moonColor;
        float moonX = 10f + ((_t + 0.5f) % 1f) * 360f;
        moonLight.transform.rotation = Quaternion.Euler(moonX, 210f, 0f);
        moonLight.gameObject.SetActive(n > 0.01f);
    }

    // ── Moon visual sphere ────────────────────────────────────────────────────

    // ── Moon visual sphere (viewport-space placement) ─────────────────────────

    void ApplyMoonVisual()
    {
        if (moonVisual == null || _mainCam == null) return;

        float sunAngle  = (_t * 360f - 90f) * Mathf.Deg2Rad;
        float moonAngle = sunAngle + Mathf.PI;          // exactly opposite the sun
        float mx = Mathf.Cos(moonAngle);
        float my = Mathf.Sin(moonAngle);

        float n      = NightBlend(_t);
        bool visible = my > -0.1f && n > 0.01f;
        moonVisual.gameObject.SetActive(visible);
        if (_moonRenderer != null) _moonRenderer.enabled = visible;

        if (visible)
        {
            float viewX = (mx + 1f) * 0.5f;
            float viewY = 0.72f + my * 0.15f;   // 0.72 to 0.87 — matches sun range
            float viewZ = 150f;

            Vector3 worldPos = _mainCam.ViewportToWorldPoint(
                new Vector3(viewX, viewY, viewZ));
            moonVisual.position = worldPos;
        }
    }

    // ── Stars (sphere GameObjects) ────────────────────────────────────────────

    void ApplyStars()
    {
        if (_starSpheres == null) return;
        bool nightTime = NightBlend(_t) > 0.5f;
        foreach (Transform s in _starSpheres)
            if (s != null) s.gameObject.SetActive(nightTime);
    }

    // ── Time display ──────────────────────────────────────────────────────────

    void ApplyTimeDisplay()
    {
        if (timeDisplayText == null) return;
        float total = _t * 24f;
        int   h     = Mathf.FloorToInt(total) % 24;
        int   m     = Mathf.FloorToInt((total - Mathf.Floor(total)) * 60f);
        int   h12   = h % 12; if (h12 == 0) h12 = 12;
        timeDisplayText.text = $"{h12:D2}:{m:D2} {(h < 12 ? "AM" : "PM")}";
    }

    // ── 2D GUI sun and moon ───────────────────────────────────────────────────

    void OnGUI()
    {
        float t        = _t;
        float sunAngle = (t * 360f - 90f) * Mathf.Deg2Rad;
        float sunSY    = Mathf.Sin(sunAngle);
        float sunSX    = Mathf.Cos(sunAngle);

        // ── Sun ───────────────────────────────────────────────────────────────
        if (sunSY > -0.1f)
        {
            float screenX = Screen.width  * ((sunSX + 1f) * 0.5f);
            float screenY = Screen.height * (1f - (0.72f + sunSY * 0.15f));

            // Glow halo (larger, semi-transparent)
            DrawCircle(screenX, screenY, 80f, new Color(1f, 0.9f, 0.3f, 0.35f), _sunTexture);
            // Core (smaller, fully opaque)
            DrawCircle(screenX, screenY, 45f, new Color(1f, 0.95f, 0.6f, 1f),   _sunTexture);
        }

        // ── Moon ─────────────────────────────────────────────────────────────
        float moonAngle  = sunAngle + Mathf.PI;
        float moonSY     = Mathf.Sin(moonAngle);
        float moonSX     = Mathf.Cos(moonAngle);
        float nightBlend = NightBlend(t);

        if (moonSY > -0.1f && nightBlend > 0.1f)
        {
            float screenX = Screen.width  * ((moonSX + 1f) * 0.5f);
            float screenY = Screen.height * (1f - (0.72f + moonSY * 0.15f));

            // Glow halo
            DrawCircle(screenX, screenY, 55f, new Color(0.8f, 0.85f, 1f, 0.25f * nightBlend), _moonTexture);
            // Core
            DrawCircle(screenX, screenY, 35f, new Color(0.909f, 0.909f, 1f, nightBlend),       _moonTexture);
        }
    }

    void DrawCircle(float x, float y, float radius, Color color, Texture2D tex)
    {
        if (tex == null) return;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x - radius, y - radius, radius * 2f, radius * 2f), tex);
        GUI.color = Color.white;
    }

    /// <summary>
    /// Generates a size×size Texture2D with a radial soft-glow gradient.
    /// Centre pixel = full baseColor, edges fade to transparent.
    /// alpha = (1 - dist * 1.2)^2  gives a smooth, slightly tight falloff.
    /// </summary>
    Texture2D CreateRadialTexture(int size, Color baseColor)
    {
        Texture2D tex    = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Vector2   center = new Vector2(size * 0.5f, size * 0.5f);
        float     radius = size * 0.5f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - dist * 1.2f);
                alpha       = alpha * alpha;   // squared = softer falloff
                Color c     = baseColor;
                c.a         = alpha;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ── Runtime F1 diagnostic ─────────────────────────────────────────────────

    void DebugSunMoon()
    {
        // ── Star material reference (what is actually working) ─────────────────
        string starShader = "no stars";
        string starQueue  = "n/a";
        if (_starSpheres != null && _starSpheres.Length > 0 && _starSpheres[0] != null)
        {
            MeshRenderer smr = _starSpheres[0].GetComponent<MeshRenderer>();
            if (smr != null && smr.sharedMaterial != null)
            {
                starShader = smr.sharedMaterial.shader.name;
                starQueue  = smr.sharedMaterial.renderQueue.ToString();
            }
        }
        Debug.Log($"[DayNightCycle] STAR REFERENCE (working) — shader: {starShader}  renderQueue: {starQueue}");

        // ── Viewport approach confirmation ─────────────────────────────────────
        float _sa = (_t * 360f - 90f) * Mathf.Deg2Rad;
        float _sx = Mathf.Cos(_sa), _sy = Mathf.Sin(_sa);
        float _vx = (_sx + 1f) * 0.5f;
        float _vy = 0.65f + _sy * 0.25f;
        Debug.Log(
            $"[DayNightCycle] ViewportToWorldPoint approach: YES\n" +
            $"  t={_t:F4} → sunAngle={_sa*Mathf.Rad2Deg:F1}°  sx={_sx:F3} sy={_sy:F3}\n" +
            $"  viewport X={_vx:F3} Y={_vy:F3} Z=150  aboveHorizon={_sy > -0.1f}\n" +
            (_mainCam != null && _sy > -0.1f
                ? $"  worldPos={_mainCam.ViewportToWorldPoint(new Vector3(_vx,_vy,150f))}"
                : "  (sun below horizon or no camera)")
        );

        // Sun expected position
        Vector3 camBase = _mainCam != null
            ? new Vector3(_mainCam.transform.position.x, 0f, _mainCam.transform.position.z)
            : Vector3.zero;
        float sa   = (_t * 360f - 90f) * Mathf.Deg2Rad;
        float sinA = Mathf.Sin(sa);
        Vector3 sunExp = camBase + new Vector3(Mathf.Cos(sa)*SUN_RADIUS, sinA*SUN_RADIUS,
                                               40f*Mathf.Max(0f,sinA));
        float ma = (_t * 360f + 90f) * Mathf.Deg2Rad;
        Vector3 moonExp = camBase + new Vector3(Mathf.Cos(ma)*MOON_RADIUS,
                                                Mathf.Sin(ma)*MOON_RADIUS, 0f);

        Debug.Log(
            $"=== DayNightCycle F1 Diagnostic ===\n" +
            $"  t = {_t:F4}  ({_t*24f:F1}h)\n" +
            $"  clearFlags  = {(_mainCam!=null?_mainCam.clearFlags.ToString():"no cam")}\n" +
            $"  farClip     = {(_mainCam!=null?_mainCam.farClipPlane.ToString("F0"):"no cam")}\n" +
            $"  skybox      = {(RenderSettings.skybox!=null?RenderSettings.skybox.name:"null (removed)")}\n" +
            $"\n  SUN expected pos  : {sunExp}  aboveHorizon={sunExp.y>-5f}\n" +
            $"  sunVisual assigned : {sunVisual != null}\n" +
            (sunVisual != null ?
                $"  sunVisual.pos      : {sunVisual.position}\n" +
                $"  sunVisual.scale    : {sunVisual.localScale}\n" +
                $"  sunVisual.active   : {sunVisual.gameObject.activeSelf}\n" +
                $"  _sunRenderer       : {(_sunRenderer!=null?_sunRenderer.enabled.ToString():"null")}\n" +
                $"  shader             : {(_sunRenderer!=null&&_sunRenderer.material!=null?_sunRenderer.material.shader.name:"none")}\n" +
                $"  renderQueue        : {(_sunRenderer!=null&&_sunRenderer.material!=null?_sunRenderer.material.renderQueue.ToString():"none")}\n" +
                $"  material alpha     : {(_sunRenderer!=null&&_sunRenderer.material!=null?_sunRenderer.material.color.a.ToString("F2"):"none")}\n"
                : "") +
            $"\n  MOON expected pos  : {moonExp}  visible={moonExp.y>-5f&&NightBlend(_t)>0.01f}\n" +
            (moonVisual != null ?
                $"  moonVisual.active  : {moonVisual.gameObject.activeSelf}\n" +
                $"  NightBlend         : {NightBlend(_t):F3}\n"
                : "  moonVisual: NOT ASSIGNED\n") +
            $"\n  Camera pos   : {(_mainCam!=null?_mainCam.transform.position.ToString():"no cam")}\n" +
            $"  Camera bgCol : {(_mainCam!=null?_mainCam.backgroundColor.ToString():"no cam")}"
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float NightBlend(float t)
    {
        if (t >= 0.85f && t <  0.90f) return (t - 0.85f) / 0.05f;
        if (t >= 0.90f || t <  0.10f) return 1f;
        if (t >= 0.10f && t <  0.15f) return 1f - (t - 0.10f) / 0.05f;
        return 0f;
    }
}
