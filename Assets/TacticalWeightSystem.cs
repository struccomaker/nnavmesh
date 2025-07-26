using UnityEngine;
using System.Collections.Generic;

public class TacticalWeightSystem : MonoBehaviour
{
    [Header("Weight Values")]
    public float coverBonus = -3f;        // -2 to -5 (safer areas)
    public float threatPenalty = 5f;      // +3 to +10 (dangerous areas)
    public float exposurePenalty = 2f;    // Open area penalty
    public float wallProximityBonus = -2f; // Bonus for being near cover

    [Header("Weight Thresholds for Colors")]
    public float safeThreshold = -2f;     // Green if weight < this
    public float dangerThreshold = 1f;    // LOWERED from 3f to 1f - Red if weight > this
    public float neutralRange = 1f;       // Yellow if between safe and danger

    [Header("Visualization Colors")]
    public Color safeColor = Color.green;
    public Color dangerColor = Color.red;
    public Color neutralColor = Color.yellow;
    public Color defaultColor = Color.cyan;

    [Header("Detection Settings")]
    public LayerMask obstacleLayerMask = 1; // Layer for walls/obstacles
    public float coverCheckDistance = 3f;
    public float exposureCheckDistance = 5f;

    [Header("Visualization")]
    public bool showWeightColors = true;
    public bool updateInRealTime = true;
    public float updateInterval = 0.5f;

    private PlayerWeaponSystem playerWeapon;
    private Transform player;
    private NavMeshTriangleRenderer triangleRenderer; // Back to NavMeshTriangleRenderer
    private MapSpawner mapSpawner;
    private Dictionary<int, float> triangleWeights = new Dictionary<int, float>();
    private float lastUpdateTime;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerWeapon = player?.GetComponent<PlayerWeaponSystem>();
        triangleRenderer = GetComponent<NavMeshTriangleRenderer>(); // Back to NavMeshTriangleRenderer
        mapSpawner = GetComponent<MapSpawner>();

        Debug.Log("Tactical Weight System initialized");
    }

    void Update()
    {
        // Retry finding player if we don't have one
        if (player == null || playerWeapon == null)
        {
            TryFindPlayer();
        }

        if (updateInRealTime && Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateAllTriangleWeights();
            lastUpdateTime = Time.time;

            // DEBUG: Print system status every few seconds
            if (Time.time % 4f < updateInterval)
            {
                DebugWeightSystem();
            }
        }
    }

    void TryFindPlayer()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                // Try finding by name
                GameObject playerGO = GameObject.Find("Player");
                if (playerGO != null)
                {
                    player = playerGO.transform;
                    Debug.Log("TacticalWeightSystem: Found Player by name (retry)");
                }
            }
            else
            {
                Debug.Log("TacticalWeightSystem: Found Player by tag (retry)");
            }
        }

        if (player != null && playerWeapon == null)
        {
            playerWeapon = player.GetComponent<PlayerWeaponSystem>();
            if (playerWeapon != null)
            {
                Debug.Log("TacticalWeightSystem: Found PlayerWeaponSystem (retry)");
            }
        }
    }

    void DebugWeightSystem()
    {
        if (player == null || playerWeapon == null)
        {
            Debug.LogError("TacticalWeightSystem: Player or PlayerWeapon is NULL!");
            return;
        }

        Debug.Log($"=== TACTICAL WEIGHT DEBUG ===");
        Debug.Log($"Player Position: {player.position}");
        Debug.Log($"Player Weapon: {playerWeapon.currentWeapon}");
        Debug.Log($"Threat Radius: {playerWeapon.CurrentThreatRadius}");
        Debug.Log($"Triangle Weights Calculated: {triangleWeights.Count}");

        // Test weight calculation at player position
        Vector3 testPos = player.position + Vector3.forward * 2f; // 2 units in front of player
        float testWeight = GetTacticalWeight(testPos);
        Debug.Log($"Test weight 2m from player: {testWeight:F2}");

        // Count dangerous triangles manually
        int dangerCount = 0;
        float maxWeight = 0f;
        foreach (var kvp in triangleWeights)
        {
            if (kvp.Value > dangerThreshold)
                dangerCount++;
            if (kvp.Value > maxWeight)
                maxWeight = kvp.Value;
        }
        Debug.Log($"Manual danger count: {dangerCount}, Max weight found: {maxWeight:F2}");
    }

    public float GetTacticalWeight(Vector3 position)
    {
        float weight = 0f;

        // Cover assessment
        weight += CalculateCoverBonus(position);

        // Threat assessment from player
        weight += CalculateThreatPenalty(position);

        // Exposure penalty
        weight += CalculateExposurePenalty(position);

        return weight;
    }

    float CalculateCoverBonus(Vector3 position)
    {
        // Check for nearby walls/obstacles that provide cover
        Collider[] nearbyObstacles = Physics.OverlapSphere(position, coverCheckDistance, obstacleLayerMask);

        if (nearbyObstacles.Length > 0)
        {
            // More obstacles = better cover
            float coverValue = Mathf.Clamp(nearbyObstacles.Length * wallProximityBonus, -5f, 0f);
            return coverValue;
        }

        return 0f;
    }

    float CalculateThreatPenalty(Vector3 position)
    {
        if (player == null || playerWeapon == null)
        {
            Debug.LogWarning("TacticalWeightSystem: Player or PlayerWeapon is null in CalculateThreatPenalty");
            return 0f;
        }

        float distance = Vector3.Distance(position, player.position);
        float threatRadius = playerWeapon.CurrentThreatRadius;

        //// Debug every 100th calculation to avoid spam
        //if (Random.Range(0, 100) == 0)
        //{
        //    Debug.Log($"Threat calc: pos={position}, player={player.position}, distance={distance:F1}, threatRadius={threatRadius:F1}");
        //}

        if (distance > threatRadius) return 0f;

        // Check line of sight to player (simplified for now)
        bool hasLineOfSight = true; // Temporarily disable LOS check to see if this is the issue

        // Calculate threat based on weapon type and distance
        float threatMultiplier = playerWeapon.currentWeapon == PlayerWeaponSystem.WeaponType.Ranged ? 1.5f : 1f;
        float distanceFactor = 1f - (distance / threatRadius);

        float finalThreat = threatPenalty * threatMultiplier * distanceFactor;

        // Debug high threat values
        if (finalThreat > 2f)
        {
            Debug.Log($"HIGH THREAT: pos={position}, distance={distance:F1}, finalThreat={finalThreat:F1}");
        }

        return finalThreat;
    }

    float CalculateExposurePenalty(Vector3 position)
    {
        // Check how exposed the position is (no cover in multiple directions)
        int exposedDirections = 0;
        Vector3[] checkDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 direction in checkDirections)
        {
            if (!Physics.Raycast(position + Vector3.up * 0.5f, direction, exposureCheckDistance, obstacleLayerMask))
            {
                exposedDirections++;
            }
        }

        // More exposed directions = higher penalty
        return (exposedDirections / 4f) * exposurePenalty;
    }

    bool HasLineOfSightToPlayer(Vector3 position)
    {
        if (player == null) return false;

        Vector3 directionToPlayer = (player.position - position).normalized;
        float distanceToPlayer = Vector3.Distance(position, player.position);

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 0.5f, directionToPlayer, out hit, distanceToPlayer, obstacleLayerMask))
        {
            return false; // Obstacle blocks line of sight
        }

        return true; // Clear line of sight
    }

    void UpdateAllTriangleWeights()
    {
        if (mapSpawner == null || mapSpawner.navMeshTriangles.Count == 0) return;

        triangleWeights.Clear();

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            float weight = GetTacticalWeight(triangle.center);
            triangleWeights[triangle.triangleIndex] = weight;
        }

        if (showWeightColors)
        {
            UpdateTriangleColors();
        }
    }

    void UpdateTriangleColors()
    {
        if (triangleRenderer == null) return;

        // The NavMeshTriangleRenderer handles color updates automatically
        // Just trigger a refresh if needed
        if (showWeightColors)
        {
            // Colors are updated automatically by the NavMeshTriangleRenderer
            // This method is kept for compatibility but functionality moved to renderer
        }
    }

    Color GetWeightColor(float weight)
    {
        if (weight <= safeThreshold) // Safe (good cover)
            return safeColor;
        else if (weight >= dangerThreshold) // Dangerous (high threat)
            return dangerColor;
        else if (Mathf.Abs(weight) <= neutralRange) // Neutral
            return neutralColor;
        else
            return defaultColor; // Default triangle color
    }

    // Public method to get color for external systems
    public Color GetTriangleColor(int triangleIndex)
    {
        if (triangleWeights.ContainsKey(triangleIndex))
        {
            float weight = triangleWeights[triangleIndex];
            return GetWeightColor(weight);
        }
        return defaultColor;
    }

    // Get all triangle colors for batch updates
    public Dictionary<int, Color> GetAllTriangleColors()
    {
        Dictionary<int, Color> triangleColors = new Dictionary<int, Color>();

        foreach (var kvp in triangleWeights)
        {
            triangleColors[kvp.Key] = GetWeightColor(kvp.Value);
        }

        return triangleColors;
    }

    // Called by PlayerWeaponSystem when weapon changes
    public void OnPlayerWeaponChanged(PlayerWeaponSystem weaponSystem)
    {
        Debug.Log($"Tactical system updating for weapon change: {weaponSystem.currentWeapon}");

        // Force immediate update of all triangle weights
        UpdateAllTriangleWeights();
    }

    // Public method to get weight for a specific triangle
    public float GetTriangleWeight(int triangleIndex)
    {
        if (triangleWeights.ContainsKey(triangleIndex))
            return triangleWeights[triangleIndex];

        return 0f;
    }

    // Method to highlight triangles based on weight ranges
    public void HighlightWeightRanges()
    {
        if (triangleRenderer == null || mapSpawner == null) return;

        // Use the NavMeshTriangleRenderer's analysis method
        triangleRenderer.HighlightWeightRanges();
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // Draw player threat radius
        Gizmos.color = Color.red;
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        if (playerWeapon != null)
        {
            Gizmos.DrawSphere(player.position, playerWeapon.CurrentThreatRadius);
        }

        // Draw cover check radius for selected position
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.y = 0f;

        Gizmos.color = Color.blue;
        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
        Gizmos.DrawSphere(mouseWorldPos, coverCheckDistance);
    }
}