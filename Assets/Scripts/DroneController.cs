using UnityEngine;

/// <summary>
/// Simple Drone Flight script letting the user visually navigate through the digital twin farm using WASD + Mouse.
/// </summary>
public class DroneController : MonoBehaviour
{
    [Header("Flight Dynamics")]
    public float flySpeed = 15f;
    public float sprintMultiplier = 2.5f;
    public float rotSpeed = 3f;

    private float pitch = 0f;
    private float yaw = 0f;

    void Start()
    {
        // Setup initial rotation logic
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;

        // Hide and lock the cursor when clicking into the game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        TwinEventLogger.Log("DRONE", "Manual Flight Control Engaged. Press ESC to unlock mouse.", "info");
    }

    void Update()
    {
        // 1. Mouse Look (Pitch and Yaw)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += rotSpeed * Input.GetAxis("Mouse X");
            pitch -= rotSpeed * Input.GetAxis("Mouse Y");
            
            // Limit looking straight up/down
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // 2. Keyboard Movement (WASD + QE)
        Vector3 moveInput = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        
        if (Input.GetKey(KeyCode.E)) moveInput.y = 1f; // Fly up
        if (Input.GetKey(KeyCode.Q)) moveInput.y = -1f; // Fly down

        float currentSpeed = flySpeed;
        if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= sprintMultiplier;

        // Move relative to rotation
        transform.Translate(moveInput * currentSpeed * Time.deltaTime, Space.Self);

        // Escape cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        // Re-lock cursor on click
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
