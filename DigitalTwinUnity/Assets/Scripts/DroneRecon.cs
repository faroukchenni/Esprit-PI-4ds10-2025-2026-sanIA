using System.Collections;
using UnityEngine;

/// <summary>
/// DroneRecon — autonomous patrol drone that circles all 4 crop zones.
///
/// Patrol loop (loops forever):
///   Overview → Potato → Tomato → Grape → Apple → Overview → ...
///
/// Zone world positions derived from CyberGrid layout (spacing=2.2, 20x20 grid):
///   Potato centre  ~( 9.9, 0,  9.9)
///   Tomato centre  ~(33.0, 0,  9.9)
///   Grape centre   ~( 9.9, 0, 33.0)
///   Apple centre   ~(33.0, 0, 33.0)
///   Overview       ~(21.5, 0, 21.5)
/// </summary>
public class DroneRecon : MonoBehaviour
{
    [Header("Flight Dynamics")]
    public float flyHeight     = 18f;
    public float moveSpeed     = 6f;
    public float arrivalRadius = 1.5f;

    [Header("Patrol Timing")]
    public float scanDwellSeconds     = 6f;
    public float overviewDwellSeconds = 4f;

    [Header("Scanner Effects")]
    public LineRenderer scanLaser;
    public float        scanRange = 12f;
    public Color        scanColor = new Color(0f, 0.9f, 1f, 1f);

    // ── Waypoints ─────────────────────────────────────────────────────────────

    private static readonly Vector3[] ZoneCentres =
    {
        new Vector3(21.5f, 0f, 21.5f),   // 0 Overview
        new Vector3( 9.9f, 0f,  9.9f),   // 1 Potato
        new Vector3(33.0f, 0f,  9.9f),   // 2 Tomato
        new Vector3( 9.9f, 0f, 33.0f),   // 3 Grape
        new Vector3(33.0f, 0f, 33.0f),   // 4 Apple
    };

    private static readonly string[] ZoneNames = { "Overview", "Potato", "Tomato", "Grape", "Apple" };

    [Header("Patrol Mode")]
    [Tooltip("Enable to start autonomous patrol loop. Disable to keep manual DroneController navigation.")]
    public bool autoPatrol = false;

    // ── State ──────────────────────────────────────────────────────────────────

    private int     _wpIndex    = 0;
    private bool    _isScanning = false;
    private Vector3 _targetPos;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        if (scanLaser != null)
        {
            scanLaser.startColor = scanColor;
            scanLaser.endColor   = new Color(scanColor.r, scanColor.g, scanColor.b, 0f);
            scanLaser.enabled    = false;
        }

        _targetPos = transform.position;   // stay put — don't warp to grid centre

        if (autoPatrol)
        {
            Vector3 start  = ZoneCentres[0];
            transform.position = new Vector3(start.x, flyHeight, start.z);
            _targetPos         = transform.position;
            StartCoroutine(PatrolLoop());
            TwinEventLogger.Log("DRONE", "Autonomous patrol engaged.", "info");
        }
    }

    // ── Patrol coroutine ──────────────────────────────────────────────────────

    private IEnumerator PatrolLoop()
    {
        while (true)
        {
            Vector3 wp    = ZoneCentres[_wpIndex];
            _targetPos    = new Vector3(wp.x, flyHeight, wp.z);
            bool overview = (_wpIndex == 0);

            TwinEventLogger.Log("DRONE", $"Flying to {ZoneNames[_wpIndex]}...", "info");

            // Fly until close enough
            while (Vector3.Distance(transform.position, _targetPos) > arrivalRadius)
                yield return null;

            // Arrived — scan
            _isScanning = true;
            if (scanLaser != null) scanLaser.enabled = !overview;

            if (!overview)
                TwinEventLogger.Log("DRONE", $"Scanning {ZoneNames[_wpIndex]} zone.", "info");

            yield return new WaitForSeconds(overview ? overviewDwellSeconds : scanDwellSeconds);

            _isScanning = false;
            if (scanLaser != null) scanLaser.enabled = false;

            _wpIndex = (_wpIndex + 1) % ZoneCentres.Length;
        }
    }

    // ── Per-frame movement ────────────────────────────────────────────────────

    private void Update()
    {
        if (!autoPatrol) return;   // manual navigation — DroneController drives the GO

        transform.position = Vector3.MoveTowards(
            transform.position, _targetPos, moveSpeed * Time.deltaTime);

        // Rotate to face travel direction
        Vector3 dir = _targetPos - transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);
        }

        if (_isScanning && scanLaser != null && scanLaser.enabled)
            UpdateScanEffect();
    }

    private void UpdateScanEffect()
    {
        scanLaser.SetPosition(0, transform.position);
        float    sweep  = Mathf.Sin(Time.time * 4f) * scanRange;
        Vector3  ground = new Vector3(
            _targetPos.x + sweep, 0f,
            _targetPos.z + Mathf.Cos(Time.time * 4f) * scanRange * 0.5f);
        scanLaser.SetPosition(1, ground);
    }

    /// <summary>Reroute the drone immediately to any zone (0=Overview, 1-4=crop zones).</summary>
    public void FlyToZone(int index)
    {
        _wpIndex   = Mathf.Clamp(index, 0, ZoneCentres.Length - 1);
        Vector3 wp = ZoneCentres[_wpIndex];
        _targetPos = new Vector3(wp.x, flyHeight, wp.z);
    }
}
