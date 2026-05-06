using UnityEngine;

public class DroneCameraController : MonoBehaviour
{
    public Transform cameraTransform;

    public float panSpeed = 20f;
    public float rotateSpeed = 120f;
    public float zoomSpeed = 30f;

    public float minHeight = 5f;
    public float maxHeight = 60f;

    void Update()
    {
        if (cameraTransform == null) return;

        float dt = Time.deltaTime;

        Vector3 dir = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;

        transform.position += dir * panSpeed * dt;

        if (Input.GetMouseButton(1))
        {
            float mx = Input.GetAxis("Mouse X");
            transform.Rotate(Vector3.up, mx * rotateSpeed * dt, Space.World);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f)
        {
            Vector3 pos = cameraTransform.position;
            pos += cameraTransform.forward * (scroll * zoomSpeed);
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            cameraTransform.position = pos;
        }
    }
}
