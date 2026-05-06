using UnityEngine;

/// <summary>
/// Cinematic Drone Camera with scanning effects.
/// Simulates a reconnaissance drone monitoring the field.
/// </summary>
public class DroneRecon : MonoBehaviour
{
    [Header("Flight Dynamics")]
    public float flyHeight = 15f;
    public float moveSpeed = 5f;
    public float rotSpeed = 2f;
    public Vector3 targetPos;

    [Header("Scanner Effects")]
    public LineRenderer scanLaser; // Assign a line renderer in the Inspector
    public float scanRange = 10f;
    public Color scanColor = Color.cyan;

    private bool isScanning = false;

    void Start()
    {
        targetPos = transform.position;
        if (scanLaser) scanLaser.enabled = false;
    }

    void Update()
    {
        // Smooth flying
        transform.position = Vector3.Lerp(transform.position, targetPos + (Vector3.up * flyHeight), Time.deltaTime * moveSpeed);
        
        // Always look at the center of the field
        transform.LookAt(Vector3.zero);

        if (isScanning)
        {
            UpdateScanEffect();
        }
    }

    public void MoveToZone(Vector3 zonePos)
    {
        targetPos = zonePos;
        isScanning = true;
        if (scanLaser) scanLaser.enabled = true;
    }

    void UpdateScanEffect()
    {
        if (!scanLaser) return;

        // Cast a laser from the drone downwards
        scanLaser.SetPosition(0, transform.position);
        
        // Animate a sweeping motion for the laser end point
        float sweep = Mathf.Sin(Time.time * 5f) * scanRange;
        Vector3 groundPoint = targetPos;
        groundPoint.x += sweep;

        scanLaser.SetPosition(1, groundPoint);
    }
}
