// Thanks Gemini, too lazy to make this myself
// Script implements a basic fly camera that follos Unity control schemes
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Speed of the camera when panning.")]
    public float moveSpeed = 50f;

    [Header("Look Settings")]
    [Tooltip("Sensitivity of the mouse for looking around.")]
    public float mouseSensitivity = 100f;

    // Internal variables to track camera rotation
    private float yaw = 0.0f;
    private float pitch = 0.0f;

    void Start()
    {
        // Initialize rotation values from the camera's starting orientation
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;
    }

    void Update()
    {
        // Check if the right mouse button is held down
        if (Input.GetMouseButton(1))
        {
            // --- FREE FLY MODE ---

            // Lock and hide the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Get mouse input for rotation
            yaw += mouseSensitivity * Input.GetAxis("Mouse X") * Time.deltaTime;
            pitch -= mouseSensitivity * Input.GetAxis("Mouse Y") * Time.deltaTime;

            // Clamp the pitch to prevent flipping upside down
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            // Apply the rotation
            transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);

            // Get keyboard input for movement
            Vector3 moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            moveDirection = transform.TransformDirection(moveDirection); // Move relative to where the camera is looking

            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
        else
        {
            // --- STANDARD PAN MODE ---

            // Unlock and show the cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Get the camera's forward and right vectors, flattened onto the XZ plane
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Calculate the desired movement direction based on WASD input
            Vector3 desiredMoveDirection = forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal");

            // Apply movement
            transform.position += desiredMoveDirection * moveSpeed * Time.deltaTime;
        }
    }
}