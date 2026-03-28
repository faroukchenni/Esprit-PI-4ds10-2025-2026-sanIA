using UnityEngine;

/// <summary>
/// Attaches a drone visual model as a child of the DroneController Camera GameObject.
/// The drone model follows the camera so it's always visible during first-person flight.
///
/// HOW TO USE:
///   1. Add this script to the Camera GameObject that has DroneController.cs.
///   2. Assign the dronePrefab field: drag Assets/Drone/prefab/drone.prefab into it.
///   3. The model is spawned at localPosition (0, -0.5, 2) and scaled to droneScale.
///   4. A gentle hover animation (bob + rotor spin) runs automatically.
///
/// The drone model sits slightly below and in front of the camera, simulating
/// the view from a camera mounted on a real farm survey drone.
/// </summary>
public class DroneVisual : MonoBehaviour
{
    [Header("Drone Prefab")]
    [Tooltip("Drag Assets/Drone/prefab/drone.prefab (or any variant) here")]
    public GameObject dronePrefab;

    [Header("Drone Position (local to Camera)")]
    [Tooltip("Z=3: 3 units in front of camera. Y=-0.3: slightly below camera centre.")]
    public Vector3 localOffset = new Vector3(0f, -0.3f, 3f);

    [Header("Drone Scale")]
    [Tooltip("Adjusts visual size. 0.3 looks like a real compact survey drone.")]
    public float droneScale = 0.3f;

    [Header("Hover Animation")]
    [Tooltip("Vertical bob amplitude (meters)")]
    public float bobAmplitude = 0.015f;

    [Tooltip("Vertical bob speed (cycles/sec)")]
    public float bobFrequency = 2f;

    [Header("Rotor Spin")]
    [Tooltip("Spin speed in degrees/second for rotor visual")]
    public float rotorSpinSpeed = 720f;

    [Tooltip("Name substring to identify rotor child GameObjects (e.g. 'rotor', 'prop', 'blade')")]
    public string rotorNameFilter = "rotor";

    // ── Runtime ───────────────────────────────────────────────────────────

    private GameObject droneInstance;
    private Transform[] rotors;
    private Vector3 baseLocalPos;

    void Start()
    {
        if (dronePrefab == null)
        {
            Debug.LogError("[DroneVisual] dronePrefab is not assigned! " +
                           "Drag Assets/Drone/prefab/drone.prefab into the Inspector on the Camera.");
            return;
        }

        SpawnDroneModel();
    }

    void SpawnDroneModel()
    {
        // Instantiate as child of the Camera (this.gameObject)
        droneInstance = Instantiate(dronePrefab, transform);
        droneInstance.name = "DroneVisual_Model";

        // Position slightly in front of and below camera (local space)
        droneInstance.transform.localPosition = localOffset;
        droneInstance.transform.localRotation = Quaternion.identity;
        droneInstance.transform.localScale    = Vector3.one * droneScale;

        baseLocalPos = localOffset;

        // Find rotor children for spin animation
        FindRotors();

        Debug.Log($"[DroneVisual] Drone model spawned: prefab='{dronePrefab.name}', " +
                  $"scale={droneScale}, offset={localOffset}, rotors found={rotors.Length}");
    }

    void FindRotors()
    {
        var allChildren = droneInstance.GetComponentsInChildren<Transform>(true);
        var rotorList   = new System.Collections.Generic.List<Transform>();

        foreach (var t in allChildren)
        {
            if (t.name.ToLower().Contains(rotorNameFilter.ToLower()) ||
                t.name.ToLower().Contains("prop") ||
                t.name.ToLower().Contains("blade") ||
                t.name.ToLower().Contains("spin"))
            {
                rotorList.Add(t);
            }
        }

        rotors = rotorList.ToArray();

        if (rotors.Length == 0)
            Debug.Log("[DroneVisual] No rotors found by name filter – skipping spin animation.");
        else
            Debug.Log($"[DroneVisual] Found {rotors.Length} rotor(s): " +
                      string.Join(", ", System.Array.ConvertAll(rotors, r => r.name)));
    }

    void Update()
    {
        if (droneInstance == null) return;

        // ── Hover bob ─────────────────────────────────────────────────────
        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        droneInstance.transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);

        // ── Rotor spin ────────────────────────────────────────────────────
        if (rotors != null)
        {
            for (int i = 0; i < rotors.Length; i++)
            {
                if (rotors[i] == null) continue;
                // Alternate direction every other rotor (realistic quad-rotor behaviour)
                float dir = (i % 2 == 0) ? 1f : -1f;
                rotors[i].Rotate(Vector3.up, dir * rotorSpinSpeed * Time.deltaTime, Space.Self);
            }
        }
    }

    // ── Editor helper ─────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw a wire sphere showing drone position in Scene view
        Gizmos.color = Color.cyan;
        Vector3 worldPos = transform.TransformPoint(localOffset);
        Gizmos.DrawWireSphere(worldPos, droneScale * 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, worldPos);
    }
#endif
}
