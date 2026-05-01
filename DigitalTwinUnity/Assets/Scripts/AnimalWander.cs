using System.Collections;
using UnityEngine;

/// <summary>
/// AnimalWander — makes a single animal walk randomly inside its pen bounds.
///
/// Attach this component to each spawned animal GameObject.
/// Call Init() from AnimalManager after spawning to set the pen bounds.
///
/// Behaviour loop:
///   Pick random point inside bounds → walk toward it →
///   idle for idleDuration → repeat
///
/// The animal rotates smoothly toward its target before walking.
/// Movement uses Transform.Translate (no NavMesh required).
/// </summary>
public class AnimalWander : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed      = 1.2f;
    public float turnSpeed      = 4f;

    [Header("Timing")]
    public float idleMinSeconds = 2f;
    public float idleMaxSeconds = 5f;

    [Header("Bounds (set by AnimalManager.Init)")]
    public Bounds penBounds;

    // ── State ──────────────────────────────────────────────────────────────────

    private Vector3 _target;
    private bool    _walking     = false;
    private bool    _initialised = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Auto-init if AnimalManager never called Init() (e.g. manually placed animals)
        if (!_initialised)
            Init(new Bounds(transform.position, new Vector3(8f, 2f, 8f)));
    }

    // ── Public init ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by AnimalManager after spawning.
    /// bounds: the XZ rectangle of the pen in world space.
    /// </summary>
    public void Init(Bounds bounds)
    {
        penBounds    = bounds;
        _initialised = true;

        // Stagger start so not all animals move simultaneously
        float delay = Random.Range(0f, idleMaxSeconds);
        Invoke(nameof(PickNewTarget), delay);
    }

    // ── Patrol loop ───────────────────────────────────────────────────────────

    private void PickNewTarget()
    {
        // Random XZ inside pen, keep Y at current height
        float x = Random.Range(penBounds.min.x, penBounds.max.x);
        float z = Random.Range(penBounds.min.z, penBounds.max.z);
        _target  = new Vector3(x, transform.position.y, z);
        _walking = true;
    }

    private IEnumerator IdleThenMove()
    {
        _walking = false;
        yield return new WaitForSeconds(Random.Range(idleMinSeconds, idleMaxSeconds));
        PickNewTarget();
    }

    // ── Per-frame movement ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_initialised || !_walking) return;

        Vector3 dir = _target - transform.position;
        dir.y = 0f;

        float dist = dir.magnitude;

        if (dist < 0.15f)
        {
            // Arrived — idle then pick next target
            _walking = false;
            StartCoroutine(IdleThenMove());
            return;
        }

        // Smooth turn toward target
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation   = Quaternion.Slerp(
            transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        // Walk forward
        transform.Translate(Vector3.forward * walkSpeed * Time.deltaTime, Space.Self);
    }
}
