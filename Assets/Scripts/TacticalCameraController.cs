// Enhanced camera that switches between tactical view and player follow
using UnityEngine;

public class TacticalCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Speed of the camera when panning.")]
    public float moveSpeed = 50f;

    [Header("Look Settings")]
    [Tooltip("Sensitivity of the mouse for looking around.")]
    public float mouseSensitivity = 200f;

    [Header("Camera Modes")]
    [SerializeField] private CameraMode currentMode = CameraMode.Tactical;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Vector3 playerFollowOffset = new Vector3(0, 5, -5);
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0, 1.6f, 0);
    [SerializeField] private float followSmoothTime = 0.3f;
    [SerializeField] private Vector3 tacticalOverviewPosition = new Vector3(0, 25, 0);
    [SerializeField] private Vector3 tacticalOverviewRotation = new Vector3(90, 0, 0);

    // Internal variables for tactical mode
    private float yaw = 0.0f;
    private float pitch = 0.0f;

    // Internal variables for player follow mode
    private Vector3 followVelocity = Vector3.zero;
    private Vector3 currentFollowOffset;

    // Mode switching
    private Vector3 tacticalPosition;
    private Vector3 tacticalRotationEuler;
    private bool isTransitioning = false;
    private float transitionSpeed = 2f;

    public enum CameraMode
    {
        Tactical,      // Free-flying tactical overview
        PlayerFollow,  // Follow player with offset
        FirstPerson    // First-person view from player's eyes
    }

    void Start()
    {
        // Find player if not assigned
        if (!playerTarget)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player)
                playerTarget = player.transform;
        }

        // Initialize based on starting mode
        if (currentMode == CameraMode.Tactical)
        {
            InitializeTacticalMode();
        }
        else if (currentMode == CameraMode.PlayerFollow)
        {
            InitializePlayerFollowMode();
        }
        else
        {
            InitializeFirstPersonMode();
        }
    }

    void Update()
    {
        // Handle mode switching
        if (Input.GetKeyDown(KeyCode.Z))
        {
            SwitchCameraMode();
        }

        // Update camera based on current mode
        switch (currentMode)
        {
            case CameraMode.Tactical:
                UpdateTacticalMode();
                break;
            case CameraMode.PlayerFollow:
                UpdatePlayerFollowMode();
                break;
            case CameraMode.FirstPerson:
                UpdateFirstPersonMode();
                break;
        }
    }

    void SwitchCameraMode()
    {
        if (currentMode == CameraMode.Tactical)
        {
            // Switch to player follow
            currentMode = CameraMode.PlayerFollow;
            InitializePlayerFollowMode();
            Debug.Log("Camera Mode: Player Follow");
        }
        else if (currentMode == CameraMode.PlayerFollow)
        {
            // Switch to first person
            currentMode = CameraMode.FirstPerson;
            InitializeFirstPersonMode();
            Debug.Log("Camera Mode: First Person");
        }
        else
        {
            // Switch back to tactical
            currentMode = CameraMode.Tactical;
            InitializeTacticalMode();
            Debug.Log("Camera Mode: Tactical Overview");
        }
    }

    void InitializeTacticalMode()
    {
        // Initialize tactical free-fly mode
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;

        // Store current position for tactical mode
        tacticalPosition = transform.position;
        tacticalRotationEuler = transform.eulerAngles;
    }

    void InitializePlayerFollowMode()
    {
        if (playerTarget)
        {
            // Initialize follow mode
            currentFollowOffset = playerFollowOffset;
            transform.position = playerTarget.position + currentFollowOffset;
            transform.LookAt(playerTarget.position + Vector3.up * 1.5f);
        }
    }

    void InitializeFirstPersonMode()
    {
        if (playerTarget)
        {
            // Position camera at player's eye level
            transform.position = playerTarget.position + firstPersonOffset;
            transform.rotation = playerTarget.rotation;
        }
    }

    void UpdateTacticalMode()
    {
        // Original tactical camera code
        if (Input.GetMouseButton(1))
        {
            // --- FREE FLY MODE ---
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
            moveDirection = transform.TransformDirection(moveDirection);

            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
        else
        {
            // --- STANDARD PAN MODE ---
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

        // Store position for when we switch back
        tacticalPosition = transform.position;
        tacticalRotationEuler = transform.eulerAngles;
    }

    void UpdatePlayerFollowMode()
    {
        if (!playerTarget) return;

        // Smooth follow player
        Vector3 targetPosition = playerTarget.position + currentFollowOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);

        // Look at player
        Vector3 lookTarget = playerTarget.position + Vector3.up * 1.5f;
        transform.LookAt(lookTarget);

        // Optional: Allow camera rotation around player with mouse
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 0.5f * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 0.5f * Time.deltaTime;

            // Rotate the offset around the player
            currentFollowOffset = Quaternion.AngleAxis(mouseX, Vector3.up) * currentFollowOffset;

            // Adjust height with mouse Y
            float currentHeight = currentFollowOffset.y;
            currentHeight = Mathf.Clamp(currentHeight - mouseY, 2f, 15f);
            currentFollowOffset.y = currentHeight;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void UpdateFirstPersonMode()
    {
        if (!playerTarget) return;

        // Position camera at player's eye level with collision check
        Vector3 desiredPosition = playerTarget.position + firstPersonOffset;

        // Check for wall collision and adjust camera position
        Vector3 adjustedPosition = CheckCameraCollision(desiredPosition);
        transform.position = adjustedPosition;

        // First-person mouse look
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 0.5f * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 0.5f * Time.deltaTime;

            // Rotate camera for mouse look
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0);

            // Also rotate the player to match camera yaw
            if (playerTarget)
            {
                playerTarget.rotation = Quaternion.Euler(0, yaw, 0);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Sync camera rotation with player rotation when not mouse looking
            Vector3 playerRotation = playerTarget.rotation.eulerAngles;
            yaw = playerRotation.y;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
    }

    Vector3 CheckCameraCollision(Vector3 desiredPosition)
    {
        // Cast from player position to desired camera position
        Vector3 playerPos = playerTarget.position + Vector3.up * 0.5f; // Slightly above ground
        Vector3 direction = (desiredPosition - playerPos).normalized;
        float maxDistance = Vector3.Distance(playerPos, desiredPosition);

        // Buffer distance to keep camera away from walls
        float bufferDistance = 0.3f;

        // Raycast to check for walls
        if (Physics.Raycast(playerPos, direction, out RaycastHit hit, maxDistance))
        {
            // If we hit something, position camera at hit point minus buffer
            float safeDistance = Mathf.Max(0.1f, hit.distance - bufferDistance);
            return playerPos + direction * safeDistance;
        }

        // No collision, use desired position
        return desiredPosition;
    }

    public CameraMode GetCurrentMode()
    {
        return currentMode;
    }

    public void SetPlayerTarget(Transform newTarget)
    {
        playerTarget = newTarget;
    }

    void OnGUI()
    {
        // Display current camera mode
        GUILayout.BeginArea(new Rect(1, 0, 10, 200, 80));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label($"Camera Mode: {currentMode}", GUI.skin.label);
        GUILayout.Label("Press Z to cycle modes", GUI.skin.label);

        if (currentMode == CameraMode.FirstPerson)
        {
            GUILayout.Label("Right-click: Mouse look", GUI.skin.label);
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}