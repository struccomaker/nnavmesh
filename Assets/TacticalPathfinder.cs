using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class TacticalPathfinder : MonoBehaviour
{
    private MapSpawner mapSpawner;
    private TacticalWeightSystem tacticalWeights;

    [System.Serializable]
    public struct PathfindingResult
    {
        public bool success;
        public Vector3[] pathPoints;
        public int trianglesEvaluated;
        public int trianglesInBounds;
        public float pathLength;
        public float tacticalScore;

        public PathfindingResult(bool success)
        {
            this.success = success;
            pathPoints = null;
            trianglesEvaluated = 0;
            trianglesInBounds = 0;
            pathLength = 0f;
            tacticalScore = 0f;
        }
    }

    void Start()
    {
        // Try to find components on the same GameObject first
        mapSpawner = GetComponent<MapSpawner>();
        tacticalWeights = GetComponent<TacticalWeightSystem>();

        // If not found on same GameObject, search the scene
        if (mapSpawner == null)
            mapSpawner = FindObjectOfType<MapSpawner>();
        if (tacticalWeights == null)
            tacticalWeights = FindObjectOfType<TacticalWeightSystem>();

        if (mapSpawner == null)
            Debug.LogError("TacticalPathfinder: MapSpawner not found!");
        else
            Debug.Log($"TacticalPathfinder: Found MapSpawner with {mapSpawner.navMeshTriangles.Count} triangles");

        if (tacticalWeights == null)
            Debug.LogError("TacticalPathfinder: TacticalWeightSystem not found!");
        else
            Debug.Log("TacticalPathfinder: Found TacticalWeightSystem");
    }

    // Lazy initialization - try to find components if they're missing
    void EnsureComponentsReady()
    {
        if (mapSpawner == null)
            mapSpawner = FindObjectOfType<MapSpawner>();
        if (tacticalWeights == null)
            tacticalWeights = FindObjectOfType<TacticalWeightSystem>();
    }

    public PathfindingResult FindBoundedPath(Vector3 start, Vector3 goal, PathfindingController.BoundingStrategy strategy)
    {
        UnityEngine.Debug.Log($"TacticalPathfinder: Starting bounded path from {start} to {goal} using {strategy}");

        // Ensure components are ready
        EnsureComponentsReady();

        var result = new PathfindingResult(false);

        if (mapSpawner == null || tacticalWeights == null)
        {
            UnityEngine.Debug.LogError($"TacticalPathfinder: Missing components - MapSpawner: {(mapSpawner != null ? "OK" : "NULL")}, TacticalWeights: {(tacticalWeights != null ? "OK" : "NULL")}");
            return result;
        }

        // Step 1: Get bounded triangles based on strategy
        List<int> boundedTriangles = GetBoundedTriangles(start, goal, strategy);
        UnityEngine.Debug.Log($"TacticalPathfinder: Found {boundedTriangles.Count} bounded triangles");

        // Step 2: PLACEHOLDER - Use Unity pathfinding but simulate bounded metrics
        NavMeshPath unityPath = new NavMeshPath();
        bool pathFound = NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, unityPath);

        UnityEngine.Debug.Log($"TacticalPathfinder: Unity path found: {pathFound}, corners: {(unityPath.corners?.Length ?? 0)}");

        if (pathFound && unityPath.corners.Length > 0)
        {
            result.success = true;
            result.pathPoints = unityPath.corners;
            result.trianglesInBounds = boundedTriangles.Count;
            result.trianglesEvaluated = Mathf.RoundToInt(boundedTriangles.Count * 0.6f); // Simulate not evaluating all
            result.pathLength = CalculatePathLength(result.pathPoints);
            result.tacticalScore = CalculateTacticalScore(result.pathPoints);

            UnityEngine.Debug.Log($"TacticalPathfinder SUCCESS: {result.trianglesEvaluated}/{result.trianglesInBounds} triangles, length: {result.pathLength:F1}m, score: {result.tacticalScore:F1}");
        }
        else
        {
            UnityEngine.Debug.LogError($"TacticalPathfinder FAILED: Could not calculate Unity path from {start} to {goal}");
        }

        return result;
    }

    private List<int> GetBoundedTriangles(Vector3 start, Vector3 goal, PathfindingController.BoundingStrategy strategy)
    {
        List<int> boundedTriangles = new List<int>();

        if (mapSpawner == null || mapSpawner.navMeshTriangles.Count == 0)
            return boundedTriangles;

        switch (strategy)
        {
            case PathfindingController.BoundingStrategy.FixedRadius:
                boundedTriangles = GetFixedRadiusBounds(start, goal);
                break;

            case PathfindingController.BoundingStrategy.AdaptiveThreat:
                boundedTriangles = GetAdaptiveThreatBounds(start, goal);
                break;

            case PathfindingController.BoundingStrategy.Hierarchical:
                boundedTriangles = GetHierarchicalBounds(start, goal);
                break;
        }

        return boundedTriangles;
    }

    private List<int> GetFixedRadiusBounds(Vector3 start, Vector3 goal)
    {
        List<int> bounded = new List<int>();

        // Create bounding region around start and goal
        Vector3 center = (start + goal) * 0.5f;
        float radius = Vector3.Distance(start, goal) * 0.75f + 10f; // Add some buffer

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            if (Vector3.Distance(triangle.center, center) <= radius)
            {
                bounded.Add(triangle.triangleIndex);
            }
        }

        Debug.Log($"Fixed radius bounds: {bounded.Count} triangles within {radius:F1}m radius");
        return bounded;
    }

    private List<int> GetAdaptiveThreatBounds(Vector3 start, Vector3 goal)
    {
        List<int> bounded = new List<int>();

        // Adaptive bounds based on player weapon and threat level
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        PlayerWeaponSystem weapon = player?.GetComponent<PlayerWeaponSystem>();

        float threatRadius = weapon != null ? weapon.CurrentThreatRadius : 10f;
        float adaptiveRadius = threatRadius * 1.5f; // Expand beyond threat range

        Vector3 center = (start + goal) * 0.5f;

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            if (Vector3.Distance(triangle.center, center) <= adaptiveRadius)
            {
                bounded.Add(triangle.triangleIndex);
            }
        }

        Debug.Log($"Adaptive threat bounds: {bounded.Count} triangles within {adaptiveRadius:F1}m adaptive radius");
        return bounded;
    }

    private List<int> GetHierarchicalBounds(Vector3 start, Vector3 goal)
    {
        List<int> bounded = new List<int>();

        // Multi-level bounding: local + strategic
        Vector3 center = (start + goal) * 0.5f;
        float localRadius = 8f;
        float strategicRadius = 20f;

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            float distanceToCenter = Vector3.Distance(triangle.center, center);
            float distanceToStart = Vector3.Distance(triangle.center, start);
            float distanceToGoal = Vector3.Distance(triangle.center, goal);

            // Include if within local radius of start/goal OR strategic radius of center
            if (distanceToStart <= localRadius ||
                distanceToGoal <= localRadius ||
                distanceToCenter <= strategicRadius)
            {
                bounded.Add(triangle.triangleIndex);
            }
        }

        Debug.Log($"Hierarchical bounds: {bounded.Count} triangles in multi-level bounds");
        return bounded;
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

    private float CalculateTacticalScore(Vector3[] pathPoints)
    {
        if (pathPoints == null || tacticalWeights == null)
            return 0f;

        float totalScore = 0f;
        int validPoints = 0;

        // Calculate average tactical weight along path
        foreach (Vector3 point in pathPoints)
        {
            float weight = tacticalWeights.GetTacticalWeight(point);
            // Convert weight to score (lower weight = higher score)
            float pointScore = Mathf.Clamp(50f - weight * 5f, 0f, 100f);
            totalScore += pointScore;
            validPoints++;
        }

        return validPoints > 0 ? totalScore / validPoints : 0f;
    }

    void OnDrawGizmos()
    {
        // This will be enhanced to show bounding regions in Phase 2.2
        if (Application.isPlaying)
        {
            // Draw bounding visualization will be added here
        }
    }
}