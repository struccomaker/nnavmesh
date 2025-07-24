using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleWASDMovement : MonoBehaviour
{
    [Tooltip("Units per second")]
    public float speed = 5f;

    private CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1) Read WASD / arrow keys from the built-in axes
        float h = Input.GetAxis("Horizontal"); // A/D, ←/→
        float v = Input.GetAxis("Vertical");   // W/S, ↑/↓

        // 2) Build a movement vector on the XZ plane
        Vector3 move = new Vector3(h, 0f, v);

        // 3) Normalize so diagonal isn’t faster
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        // 4) Actually move the character
        cc.Move(move * speed * Time.deltaTime);

        // 5) Optional – rotate to face movement direction
        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                10f * Time.deltaTime
            );
        }
    }
}
