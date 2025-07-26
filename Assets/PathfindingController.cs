using UnityEngine;
using System.Collections.Generic;

public enum PathfindingMode
{
    UnityNavMesh,    // Standard Unity NavMeshAgent
    BoundedAStar     // Your research: Bounded A* with tactical weights
}

public class PathfindingController : MonoBehaviour
{
    [Header("Pathfinding Mode")]
    public PathfindingMode currentMode = PathfindingMode.UnityNavMesh;
    public KeyCode toggleModeKey = KeyCode.P;

    [Header("Performance Tracking")]
    public bool showPerformanceMetrics = true;
    public int maxMetricsSamples = 100;

    [Header("Bounding Settings")]
    public BoundingStrategy boundingStrategy = BoundingStrategy.FixedRadius;
    public float fixedBoundRadius = 10f;
    public float adaptiveThreatMultiplier = 1.5f;

    private Dictionary<ZombieAI, PathfindingAgent> zombieAgents = new Dictionary<ZombieAI, PathfindingAgent>();
    private PerformanceTracker performanceTracker;
    private TacticalPathfinder tacticalPathfinder;
    private GameObject boundingSphere; // Visual bounding indicator

    public enum BoundingStrategy
    {
        FixedRadius,
        AdaptiveThreat,
        Hierarchical
    }

    [System.Serializable]
    public struct PathfindingMetrics
    {
        public float searchTimeMs;
        public int trianglesEvaluated;
        public int trianglesInBounds;
        public float pathLength;
        public float tacticalScore;
        public PathfindingMode mode;

        public PathfindingMetrics(PathfindingMode mode)
        {
            this.mode = mode;
            searchTimeMs = 0f;
            trianglesEvaluated = 0;
            trianglesInBounds = 0;
            pathLength = 0f;
            tacticalScore = 0f;
        }
    }

    void Start()
    {
        performanceTracker = GetComponent<PerformanceTracker>() ?? gameObject.AddComponent<PerformanceTracker>();
        tacticalPathfinder = GetComponent<TacticalPathfinder>() ?? gameObject.AddComponent<TacticalPathfinder>();

        // Try to register zombies now
        RegisterAllZombies();

        // If no zombies found, try again in a few seconds (they might be spawning)
        if (zombieAgents.Count == 0)
        {
            UnityEngine.Debug.Log("No zombies found initially, will retry in 2 seconds...");
            Invoke(nameof(RetryRegisterZombies), 2f);
        }

        UnityEngine.Debug.Log($"PathfindingController initialized. Press {toggleModeKey} to toggle modes.");
    }

    void RetryRegisterZombies()
    {
        UnityEngine.Debug.Log("Retrying zombie registration...");
        RegisterAllZombies();

        if (zombieAgents.Count == 0)
        {
            UnityEngine.Debug.LogWarning("Still no zombies found after retry. Check your enemy spawning setup.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            TogglePathfindingMode();
        }

        // Auto-register new zombies
        RegisterNewZombies();
    }

    public void TogglePathfindingMode()
    {
        currentMode = currentMode == PathfindingMode.UnityNavMesh ?
                     PathfindingMode.BoundedAStar :
                     PathfindingMode.UnityNavMesh;

        UnityEngine.Debug.Log($"Switched to {currentMode} pathfinding mode");

        // Update all zombie agents
        foreach (var kvp in zombieAgents)
        {
            kvp.Value.SetPathfindingMode(currentMode);
        }

        // Update bounding sphere visibility
        UpdateBoundingSphere();

        // Clear performance metrics when switching
        performanceTracker.ClearMetrics();
    }

    void UpdateBoundingSphere()
    {
        // Remove sphere - user doesn't want it blocking the view
        if (boundingSphere != null)
        {
            DestroyImmediate(boundingSphere);
        }

        // Optional: Could add a subtle wireframe circle or other non-blocking indicator here
        // For now, just remove it entirely
    }

    float GetCurrentBoundingRadius(Transform player)
    {
        switch (boundingStrategy)
        {
            case BoundingStrategy.FixedRadius:
                return fixedBoundRadius;

            case BoundingStrategy.AdaptiveThreat:
                PlayerWeaponSystem weapon = player.GetComponent<PlayerWeaponSystem>();
                return weapon != null ? weapon.CurrentThreatRadius * adaptiveThreatMultiplier : fixedBoundRadius;

            case BoundingStrategy.Hierarchical:
                return fixedBoundRadius * 2f; // Largest hierarchical bound

            default:
                return fixedBoundRadius;
        }
    }

    void RegisterAllZombies()
    {
        // Debug: Check all objects in scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        UnityEngine.Debug.Log($"Total GameObjects in scene: {allObjects.Length}");

        // Look for enemies by tag first
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        UnityEngine.Debug.Log($"Found {enemies.Length} objects with 'Enemy' tag");

        int registeredCount = 0;
        foreach (GameObject enemy in enemies)
        {
            // Check if it has ZombieAI component
            ZombieAI zombieAI = enemy.GetComponent<ZombieAI>();
            if (zombieAI != null)
            {
                RegisterZombie(zombieAI);
                registeredCount++;
                UnityEngine.Debug.Log($"Registered zombie: {enemy.name} with ZombieAI component");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Enemy {enemy.name} found but missing ZombieAI component!");

                // Try to add ZombieAI component if missing
                if (enemy.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
                {
                    zombieAI = enemy.AddComponent<ZombieAI>();
                    RegisterZombie(zombieAI);
                    registeredCount++;
                    UnityEngine.Debug.Log($"Added ZombieAI component to {enemy.name} and registered");
                }
                else
                {
                    UnityEngine.Debug.LogError($"Enemy {enemy.name} also missing NavMeshAgent - cannot add ZombieAI");
                }
            }
        }

        // Also try the old method as backup
        ZombieAI[] directZombies = FindObjectsOfType<ZombieAI>();
        foreach (var zombie in directZombies)
        {
            if (!zombieAgents.ContainsKey(zombie))
            {
                RegisterZombie(zombie);
                registeredCount++;
            }
        }

        UnityEngine.Debug.Log($"Total registered zombies: {registeredCount}");
    }

    void RegisterNewZombies()
    {
        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        foreach (var zombie in zombies)
        {
            if (!zombieAgents.ContainsKey(zombie))
            {
                RegisterZombie(zombie);
            }
        }
    }

    void RegisterZombie(ZombieAI zombie)
    {
        PathfindingAgent agent = zombie.GetComponent<PathfindingAgent>();
        if (agent == null)
        {
            agent = zombie.gameObject.AddComponent<PathfindingAgent>();
        }

        agent.Initialize(this, performanceTracker, tacticalPathfinder);
        agent.SetPathfindingMode(currentMode);
        zombieAgents[zombie] = agent;
    }

    // Public method for zombies to request paths
    public bool RequestPath(ZombieAI requester, Vector3 destination, out PathfindingMetrics metrics)
    {
        metrics = new PathfindingMetrics(currentMode);

        UnityEngine.Debug.Log($"PathfindingController: {requester.name} requesting {currentMode} path to {destination}");

        if (!zombieAgents.ContainsKey(requester))
        {
            UnityEngine.Debug.LogWarning($"Zombie {requester.name} not registered for pathfinding!");
            return false;
        }

        bool success = zombieAgents[requester].RequestPath(destination, out metrics);
        UnityEngine.Debug.Log($"PathfindingController: Path request {(success ? "SUCCESS" : "FAILED")} for {requester.name}");

        return success;
    }

    // Get current performance comparison
    public (PathfindingMetrics unity, PathfindingMetrics bounded) GetPerformanceComparison()
    {
        return performanceTracker.GetAverageMetrics();
    }

    void OnGUI()
    {
        if (!showPerformanceMetrics) return;

        GUILayout.BeginArea(new Rect(Screen.width - 350, 10, 340, 300));
        GUILayout.Label("=== Pathfinding Performance ===");

        GUILayout.Label($"Current Mode: {currentMode}");
        GUILayout.Label($"Press {toggleModeKey} to toggle modes");
        GUILayout.Space(10);

        var (unityMetrics, boundedMetrics) = GetPerformanceComparison();

        // Unity NavMesh Performance
        GUILayout.Label("Unity NavMesh:");
        GUILayout.Label($"  Avg Search Time: {unityMetrics.searchTimeMs:F2}ms");
        GUILayout.Label($"  Triangles Evaluated: {unityMetrics.trianglesEvaluated}");
        GUILayout.Label($"  Path Length: {unityMetrics.pathLength:F1}m");

        GUILayout.Space(10);

        // Bounded A* Performance
        GUILayout.Label("Bounded A* (Your Research):");
        GUILayout.Label($"  Avg Search Time: {boundedMetrics.searchTimeMs:F2}ms");
        GUILayout.Label($"  Triangles Evaluated: {boundedMetrics.trianglesEvaluated}");
        GUILayout.Label($"  Triangles In Bounds: {boundedMetrics.trianglesInBounds}");
        GUILayout.Label($"  Path Length: {boundedMetrics.pathLength:F1}m");
        GUILayout.Label($"  Tactical Score: {boundedMetrics.tacticalScore:F1}/100");

        GUILayout.Space(10);

        // Performance Comparison
        if (unityMetrics.searchTimeMs > 0 && boundedMetrics.searchTimeMs > 0)
        {
            float speedup = unityMetrics.searchTimeMs / boundedMetrics.searchTimeMs;
            float triangleReduction = 1f - (boundedMetrics.trianglesEvaluated / (float)unityMetrics.trianglesEvaluated);

            GUILayout.Label("Performance Improvement:");
            GUILayout.Label($"  Speed: {speedup:F1}x faster");
            GUILayout.Label($"  Triangle Reduction: {triangleReduction * 100:F1}%");

            if (speedup >= 3f)
            {
                GUI.color = Color.green;
                GUILayout.Label("? Research Goal Achieved!");
                GUI.color = Color.white;
            }
        }

        GUILayout.Space(10);
        GUILayout.Label($"Registered Zombies: {zombieAgents.Count}");

        if (GUILayout.Button("Clear Metrics"))
        {
            performanceTracker.ClearMetrics();
        }

        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        // Draw current bounding strategy visualization
        if (currentMode == PathfindingMode.BoundedAStar && Application.isPlaying)
        {
            DrawBoundingVisualization();
        }
    }

    void DrawBoundingVisualization()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange with transparency

        switch (boundingStrategy)
        {
            case BoundingStrategy.FixedRadius:
                Gizmos.DrawSphere(player.position, fixedBoundRadius);
                break;

            case BoundingStrategy.AdaptiveThreat:
                PlayerWeaponSystem weapon = player.GetComponent<PlayerWeaponSystem>();
                if (weapon != null)
                {
                    float adaptiveRadius = weapon.CurrentThreatRadius * adaptiveThreatMultiplier;
                    Gizmos.DrawSphere(player.position, adaptiveRadius);
                }
                break;

            case BoundingStrategy.Hierarchical:
                // Draw multiple nested bounds
                for (int i = 1; i <= 3; i++)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f * i);
                    Gizmos.DrawSphere(player.position, fixedBoundRadius * i);
                }
                break;
        }
    }
}