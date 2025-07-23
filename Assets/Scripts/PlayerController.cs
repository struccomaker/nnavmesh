using UnityEngine;

public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Visual")]
    [SerializeField] private Color playerColor = Color.green;

    private CharacterController characterController;
    private Vector3 moveDirection;
    private TacticalCameraController cameraController;

    void Start()
    {
        SetupPlayer();

        // Find camera controller
        cameraController = Camera.main.GetComponent<TacticalCameraController>();
    }

    void SetupPlayer()
    {
        // Add CharacterController if not present
        characterController = GetComponent<CharacterController>();
        if (!characterController)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.3f;
            characterController.height = 1.8f;
            characterController.center = new Vector3(0, 0.9f, 0);
        }

        // Set player tag
        gameObject.tag = "Player";

        // Set visual appearance
        Renderer renderer = GetComponent<Renderer>();
        if (renderer)
        {
            renderer.material.color = playerColor;
        }
    }

    void Update()
    {
        // Only handle movement if camera is in player follow or first person mode
        bool shouldMovePlayer = true;

        if (cameraController != null)
        {
            var mode = cameraController.GetCurrentMode();
            shouldMovePlayer = (mode == TacticalCameraController.CameraMode.PlayerFollow ||
                               mode == TacticalCameraController.CameraMode.FirstPerson);
        }

        if (shouldMovePlayer)
        {
            HandleMovement();
            HandleRotation();
        }
        else
        {
            // Stop movement when in tactical camera mode
            moveDirection = Vector3.zero;
        }
    }

    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection;

        // Different movement based on camera mode
        if (cameraController && cameraController.GetCurrentMode() == TacticalCameraController.CameraMode.FirstPerson)
        {
            // First person: move relative to player's facing direction
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Flatten directions to prevent flying
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Create movement direction relative to player's orientation
            inputDirection = (forward * vertical + right * horizontal).normalized;
        }
        else
        {
            // Third person (PlayerFollow): move relative to world coordinates
            inputDirection = new Vector3(horizontal, 0, vertical).normalized;
        }

        if (inputDirection.magnitude > 0.1f)
        {
            moveDirection = inputDirection * moveSpeed;
        }
        else
        {
            moveDirection = Vector3.zero;
        }

        // Add gravity
        moveDirection.y = -9.81f;

        // Move the character
        characterController.Move(moveDirection * Time.deltaTime);
    }

    void HandleRotation()
    {
        // Only handle rotation in follow mode (first person handles rotation in camera)
        if (cameraController && cameraController.GetCurrentMode() == TacticalCameraController.CameraMode.PlayerFollow)
        {
            // Rotate towards movement direction (only if actually moving)
            Vector3 worldMoveDirection = new Vector3(moveDirection.x, 0, moveDirection.z);
            if (worldMoveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw movement range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}