using UnityEngine;

/// <summary>
/// WindSwaySystem — applies a gentle sine-wave sway to all spawned crop GameObjects.
///
/// Each crop gets a unique random phase and frequency offset so they don't all
/// move in perfect sync — giving a natural field-of-wheat feel.
///
/// Works by storing each crop's original local rotation at startup, then applying
/// a small per-frame rotation offset on the X and Z axes.
///
/// Setup: Add this component to any persistent GameObject (e.g. GameManager).
///        It auto-discovers CyberGrid.gridObjects at Start.
/// </summary>
public class WindSwaySystem : MonoBehaviour
{
    [Header("Wind Settings")]
    [Tooltip("Maximum sway angle in degrees")]
    public float swayAngle     = 3.5f;

    [Tooltip("Base sway speed (cycles per second)")]
    public float swaySpeed     = 0.8f;

    [Tooltip("Random variation added to each crop's speed")]
    public float speedVariance = 0.4f;

    [Tooltip("Wind direction bias — 1 = purely forward, 0 = omnidirectional")]
    [Range(0f, 1f)]
    public float windBias      = 0.6f;

    [Tooltip("Animate wind gusts that temporarily increase sway angle")]
    public bool  enableGusts   = true;

    [Tooltip("Gust cycle period in real seconds")]
    public float gustPeriod    = 8f;

    [Tooltip("How much the gust multiplies the base sway angle")]
    public float gustMultiplier = 2.2f;

    // ── Internal ──────────────────────────────────────────────────────────────

    private struct CropSway
    {
        public Transform  transform;
        public Quaternion baseRotation;
        public float      phase;        // random offset so crops don't sync
        public float      speedMult;    // per-crop speed variation
    }

    private CropSway[] _crops;
    private float      _gustTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Wait one frame so CyberGrid.InitializeGrid() has finished spawning
        Invoke(nameof(CollectCrops), 0.1f);
    }

    private void CollectCrops()
    {
        CyberGrid grid = FindAnyObjectByType<CyberGrid>();
        if (grid == null || grid.gridObjects == null)
        {
            Debug.LogWarning("[WindSwaySystem] CyberGrid not found — wind sway disabled.");
            return;
        }

        var list = new System.Collections.Generic.List<CropSway>();

        // Iterate the full 2D array directly — no TwinSimulationManager needed
        int w = grid.gridObjects.GetLength(0);
        int h = grid.gridObjects.GetLength(1);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                GameObject go = grid.gridObjects[x, y];
                if (go == null) continue;

                list.Add(new CropSway
                {
                    transform    = go.transform,
                    baseRotation = go.transform.localRotation,
                    phase        = Random.Range(0f, Mathf.PI * 2f),
                    speedMult    = 1f + Random.Range(-speedVariance, speedVariance),
                });
            }
        }

        _crops = list.ToArray();
        if (_crops.Length > 0)
            TwinEventLogger.Log("WIND", $"Wind sway active on {_crops.Length} crop objects.", "info");
        else
            Debug.LogWarning("[WindSwaySystem] CyberGrid found but gridObjects are all null — sway disabled.");
    }

    // ── Per-frame sway ────────────────────────────────────────────────────────

    private void Update()
    {
        if (_crops == null || _crops.Length == 0) return;

        // Gust envelope: slow sine wave that periodically boosts amplitude
        float gustEnvelope = 1f;
        if (enableGusts)
        {
            _gustTimer += Time.deltaTime;
            // Maps 0→gustPeriod to a smooth [1, gustMultiplier] ramp
            float t = (Mathf.Sin(_gustTimer * Mathf.PI * 2f / gustPeriod) + 1f) * 0.5f;
            gustEnvelope = Mathf.Lerp(1f, gustMultiplier, t * t);  // ease-in
        }

        float t2 = Time.time;

        for (int i = 0; i < _crops.Length; i++)
        {
            ref CropSway c = ref _crops[i];
            if (c.transform == null) continue;

            float s    = Mathf.Sin(t2 * swaySpeed * c.speedMult + c.phase) * swayAngle * gustEnvelope;
            float bias = Mathf.Cos(t2 * swaySpeed * c.speedMult * 0.5f + c.phase) * swayAngle * (1f - windBias) * gustEnvelope;

            Quaternion sway = Quaternion.Euler(s, 0f, bias);
            c.transform.localRotation = c.baseRotation * sway;
        }
    }

    /// <summary>
    /// Called when a scenario resets crop scales — refresh base rotations so sway
    /// doesn't fight the scenario's orientation.
    /// </summary>
    public void RefreshBaseRotations()
    {
        if (_crops == null) return;
        for (int i = 0; i < _crops.Length; i++)
        {
            if (_crops[i].transform != null)
                _crops[i].baseRotation = _crops[i].transform.localRotation;
        }
    }
}
