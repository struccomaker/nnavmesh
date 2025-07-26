using UnityEngine;

public class PlayerWeaponSystem : MonoBehaviour
{
    [Header("Weapon States")]
    public WeaponType currentWeapon = WeaponType.Melee;

    [Header("Weapon Properties")]
    public float meleeRange = 2f;
    public float rangedRange = 10f;
    public float meleeThreatRadius = 3f;
    public float rangedThreatRadius = 12f;

    [Header("Visual Feedback")]
    public Color meleeColor = Color.blue;
    public Color rangedColor = Color.green;

    [Header("Input")]
    public KeyCode weaponSwitchKey = KeyCode.Space;

    private MeshRenderer playerRenderer;
    private Material playerMaterial;

    public enum WeaponType
    {
        Melee,
        Ranged
    }

    // Properties for other systems to access
    public float CurrentWeaponRange => currentWeapon == WeaponType.Melee ? meleeRange : rangedRange;
    public float CurrentThreatRadius => currentWeapon == WeaponType.Melee ? meleeThreatRadius : rangedThreatRadius;
    public string CurrentWeaponName => currentWeapon.ToString();

    void Start()
    {
        // Get the player's renderer and create a material instance
        playerRenderer = GetComponent<MeshRenderer>();
        if (playerRenderer != null)
        {
            playerMaterial = new Material(playerRenderer.material);
            playerRenderer.material = playerMaterial;
        }

        // Set initial weapon state
        UpdateWeaponVisuals();

        Debug.Log($"Player Weapon System initialized. Press {weaponSwitchKey} to switch weapons.");
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(weaponSwitchKey))
        {
            SwitchWeapon();
        }
    }

    public void SwitchWeapon()
    {
        // Toggle between weapon types
        currentWeapon = currentWeapon == WeaponType.Melee ? WeaponType.Ranged : WeaponType.Melee;

        UpdateWeaponVisuals();
        NotifyWeaponChange();

        Debug.Log($"Switched to {currentWeapon} weapon. Range: {CurrentWeaponRange}m, Threat Radius: {CurrentThreatRadius}m");
    }

    void UpdateWeaponVisuals()
    {
        if (playerMaterial != null)
        {
            Color targetColor = currentWeapon == WeaponType.Melee ? meleeColor : rangedColor;
            playerMaterial.color = targetColor;
        }
    }

    void NotifyWeaponChange()
    {
        // Notify tactical weight system about weapon change
        TacticalWeightSystem tacticalSystem = FindObjectOfType<TacticalWeightSystem>();
        if (tacticalSystem != null)
        {
            tacticalSystem.OnPlayerWeaponChanged(this);
            Debug.Log("Notified TacticalWeightSystem of weapon change");
        }
        else
        {
            Debug.LogError("TacticalWeightSystem not found!");
        }

        // Notify all zombies about weapon change
        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        foreach (var zombie in zombies)
        {
            zombie.OnPlayerWeaponChanged(currentWeapon, transform.position, CurrentThreatRadius);
        }
    }

    public bool IsPlayerInRange(Vector3 targetPosition)
    {
        float distance = Vector3.Distance(transform.position, targetPosition);
        return distance <= CurrentWeaponRange;
    }

    public float GetThreatLevel(Vector3 position)
    {
        float distance = Vector3.Distance(transform.position, position);

        if (distance > CurrentThreatRadius)
            return 0f; // No threat outside radius

        // Threat decreases with distance, but is higher for ranged weapons
        float maxThreat = currentWeapon == WeaponType.Ranged ? 10f : 6f;
        float threatFalloff = 1f - (distance / CurrentThreatRadius);

        return maxThreat * threatFalloff;
    }

    // Visualization for debugging
    void OnDrawGizmosSelected()
    {
        // Draw weapon range
        Gizmos.color = currentWeapon == WeaponType.Melee ? meleeColor : rangedColor;
        Gizmos.DrawWireSphere(transform.position, CurrentWeaponRange);

        // Draw threat radius
        Gizmos.color = Color.red;
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
        Gizmos.DrawSphere(transform.position, CurrentThreatRadius);
    }
}