using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PathfindingAgent : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    private PathfindingController controller;
    private PerformanceTracker performanceTracker;
    private TacticalPathfinder tacticalPathfinder;
    private PathfindingMode currentMode;

    // Visual indicator
    private GameObject modeIndicator;

    // Current path data
    private Vector3[] currentPath;
    private int currentPathIndex = 0;
    private bool hasCustomPath = false;

    [Header("Debug")]
    public bool showDebugPath = false;
    public Color debugPathColor = Color.yellow;

    public void Initialize(PathfindingController controller, PerformanceTracker tracker, TacticalPathfinder pathfinder)
    {
        this.controller = controller;
        this.performanceTracker = tracker;
        this.tacticalPathfinder = pathfinder;

        navMeshAgent = GetComponent<NavMeshAgent>();

        // Configure NavMeshAgent for both modes
        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = false;

        // Create initial mode indicator
        UpdateModeIndicator();
    }

    public void SetPathfindingMode(PathfindingMode mode)
    {
        currentMode = mode;

        // Reset any custom pathing when switching modes
        hasCustomPath = false;
        currentPath = null;

        // Update visual indicator
        UpdateModeIndicator();

        UnityEngine.Debug.Log($"{name} switched to {mode} pathfinding");
    }

    void UpdateModeIndicator()
    {
        // Remove old indicator
        if (modeIndicator != null)
        {
            DestroyImmediate(modeIndicator);
        }

        // Create new indicator
        modeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        modeIndicator.name = $"{name}_ModeIndicator";
        modeIndicator.transform.SetParent(transform);
        modeIndicator.transform.localPosition = Vector3.up * 3f;
        modeIndicator.transform.localScale = Vector3.one * 0.5f; // Make it bigger so it's more visible

        // Remove collider
        Destroy(modeIndicator.GetComponent<BoxCollider>());

        // Set color based on mode using a simpler material approach
        Renderer renderer = modeIndicator.GetComponent<Renderer>();

        // Use Unlit/Color shader which is more reliable
        Material mat = new Material(Shader.Find("Unlit/Color"));

        if (currentMode == PathfindingMode.UnityNavMesh)
        {
            mat.color = Color.blue;
            UnityEngine.Debug.Log($"Set {name} indicator to BLUE (Unity NavMesh)");
        }
        else
        {
            mat.color = Color.yellow; // Changed from orange to yellow for better visibility
            UnityEngine.Debug.Log($"Set {name} indicator to YELLOW (Bounded A*)");
        }

        renderer.material = mat;
    }

    public bool RequestPath(Vector3 destination, out PathfindingController.PathfindingMetrics metrics)
    {
        metrics = new PathfindingController.PathfindingMetrics(currentMode);

        UnityEngine.Debug.Log($"{name} requesting {currentMode} path to {destination}");

        if (currentMode == PathfindingMode.UnityNavMesh)
        {
            return RequestUnityPath(destination, out metrics);
        }
        else
        {
            return RequestBoundedPath(destination, out metrics);
        }
    }

    private bool RequestUnityPath(Vector3 destination, out PathfindingController.PathfindingMetrics metrics)
    {
        metrics = new PathfindingController.PathfindingMetrics(PathfindingMode.UnityNavMesh);

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Use Unity's pathfinding
        navMeshAgent.SetDestination(destination);
        bool success = navMeshAgent.hasPath;

        sw.Stop();

        if (success)
        {
            // Estimate metrics (Unity doesn't expose internal data)
            metrics.searchTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            metrics.trianglesEvaluated = EstimateTrianglesEvaluated(transform.position, destination);
            metrics.pathLength = CalculatePathLength(navMeshAgent.path.corners);
            metrics.tacticalScore = 0f; // Unity doesn't use tactical weights

            performanceTracker.RecordMetrics(metrics);
        }

        hasCustomPath = false;
        return success;
    }

    private bool RequestBoundedPath(Vector3 destination, out PathfindingController.PathfindingMetrics metrics)
    {
        metrics = new PathfindingController.PathfindingMetrics(PathfindingMode.BoundedAStar);

        UnityEngine.Debug.Log($"{name} requesting BOUNDED A* path");

        if (tacticalPathfinder == null)
        {
            UnityEngine.Debug.LogError($"TacticalPathfinder not found for {name}!");
            return false;
        }

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Use your bounded A* pathfinding
        var result = tacticalPathfinder.FindBoundedPath(transform.position, destination, controller.boundingStrategy);

        sw.Stop();

        if (result.success)
        {
            metrics.searchTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            metrics.trianglesEvaluated = result.trianglesEvaluated;
            metrics.trianglesInBounds = result.trianglesInBounds;
            metrics.pathLength = result.pathLength;
            metrics.tacticalScore = result.tacticalScore;

            UnityEngine.Debug.Log($"BOUNDED A* SUCCESS: {metrics.searchTimeMs:F2}ms, {metrics.trianglesEvaluated} triangles");

            // For bounded A*, let the ZombieAI handle movement directly using Unity NavMesh
            // We just record the metrics but use the same movement as Unity mode
            if (navMeshAgent.isOnNavMesh && navMeshAgent.isActiveAndEnabled)
            {
                navMeshAgent.SetDestination(destination);
                UnityEngine.Debug.Log($"BOUNDED A* path set via Unity NavMesh to {destination}");
            }

            // Don't use custom path following - let ZombieAI control movement
            hasCustomPath = false;
            currentPath = null;

            performanceTracker.RecordMetrics(metrics);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"BOUNDED A* FAILED for {name}");
        }

        return result.success;
    }

    void Update()
    {
        // Handle custom path following for bounded A*
        if (hasCustomPath && currentPath != null && currentMode == PathfindingMode.BoundedAStar)
        {
            FollowCustomPath();
        }
    }

    private void FollowCustomPath()
    {
        if (currentPathIndex >= currentPath.Length)
        {
            hasCustomPath = false;
            return;
        }

        Vector3 targetPoint = currentPath[currentPathIndex];
        float distanceToTarget = Vector3.Distance(transform.position, targetPoint);

        if (distanceToTarget < 1f) // Close enough to current waypoint
        {
            currentPathIndex++;
            if (currentPathIndex < currentPath.Length)
            {
                navMeshAgent.SetDestination(currentPath[currentPathIndex]);
            }
        }
        else
        {
            navMeshAgent.SetDestination(targetPoint);
        }
    }

    private int EstimateTrianglesEvaluated(Vector3 start, Vector3 end)
    {
        // Rough estimation based on distance and NavMesh density
        float distance = Vector3.Distance(start, end);
        MapSpawner mapSpawner = FindObjectOfType<MapSpawner>();

        if (mapSpawner != null)
        {
            int totalTriangles = mapSpawner.navMeshTriangles.Count;
            float mapArea = 40f * 40f; // Estimated map size
            float searchArea = Mathf.PI * distance * distance; // Circular search area

            return Mathf.RoundToInt((searchArea / mapArea) * totalTriangles);
        }

        return Mathf.RoundToInt(distance * 20); // Fallback estimation
    }

    private float CalculatePathLength(Vector3[] pathPoints)
    {
        if (pathPoints == null || pathPoints.Length < 2)
            return 0f;

        float length = 0f;
        for (int i = 1; i < pathPoints.Length; i++)
        {
            length += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
        }
        return length;
    }

    void OnDrawGizmos()
    {
        if (showDebugPath && hasCustomPath && currentPath != null)
        {
            Gizmos.color = debugPathColor;

            // Draw custom path
            for (int i = 1; i < currentPath.Length; i++)
            {
                Gizmos.DrawLine(currentPath[i - 1], currentPath[i]);
            }

            // Highlight current target
            if (currentPathIndex < currentPath.Length)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(currentPath[currentPathIndex], 0.5f);
            }
        }

        // Draw mode indicator above zombie
        Vector3 labelPos = transform.position + Vector3.up * 3f;
        Gizmos.color = currentMode == PathfindingMode.UnityNavMesh ? Color.blue : Color.orange;
        Gizmos.DrawWireCube(labelPos, Vector3.one * 0.3f);
    }
}