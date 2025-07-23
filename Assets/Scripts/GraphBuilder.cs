using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

// Enhanced Node class with tactical weights
public class TacticalNode
{
    public int TriangleIndex;
    public Vector3 Center;
    public List<TacticalNode> Neighbors;
    public int[] Vertices;

    // A* properties
    public float GCost;
    public float HCost;
    public float TacticalWeight; // W in F = G + H + W
    public TacticalNode Parent;
    public float FCost => GCost + HCost + TacticalWeight;

    // Tactical properties
    public TacticalType TacticalType;
    public float CoverValue;     // -2 to -5 for cover bonus
    public float ThreatLevel;    // +3 to +10 for threat penalty
    public float StrategicValue; // Strategic modifiers
    public bool HasLineOfSight;
    public List<GameObject> NearbyEnemies;
    public List<GameObject> NearbyCovers;

    // Goal bounding
    public Dictionary<TacticalNode, BoundingBox> EdgeBoundingBoxes;

    public TacticalNode(int triangleIndex, Vector3 center, int[] vertices)
    {
        TriangleIndex = triangleIndex;
        Center = center;
        Vertices = vertices;
        Neighbors = new List<TacticalNode>();
        EdgeBoundingBoxes = new Dictionary<TacticalNode, BoundingBox>();
        NearbyEnemies = new List<GameObject>();
        NearbyCovers = new List<GameObject>();
        TacticalType = TacticalType.Neutral;
        UpdateTacticalWeights();
    }

    public void UpdateTacticalWeights()
    {
        TacticalWeight = 0f;

        // Cover bonus: -2 to -5
        if (CoverValue > 0)
        {
            TacticalWeight -= Mathf.Clamp(CoverValue * 2f, 2f, 5f);
        }

        // Threat penalty: +3 to +10
        if (ThreatLevel > 0)
        {
            TacticalWeight += Mathf.Clamp(ThreatLevel * 3f, 3f, 10f);
        }

        // Strategic value modifiers
        TacticalWeight += StrategicValue;

        // Update tactical type based on weights
        if (TacticalWeight < -2f)
            TacticalType = TacticalType.Safe;
        else if (TacticalWeight > 3f)
            TacticalType = TacticalType.Danger;
        else
            TacticalType = TacticalType.Neutral;
    }

    public Color GetTacticalColor()
    {
        switch (TacticalType)
        {
            case TacticalType.Safe: return Color.green;
            case TacticalType.Danger: return Color.red;
            case TacticalType.Cover: return Color.blue;
            default: return Color.gray;
        }
    }
}

public enum TacticalType
{
    Safe,     // Green - Low threat, good cover
    Neutral,  // Gray - Standard movement
    Danger,   // Red - High threat, avoid
    Cover     // Blue - Excellent cover position
}

public class TacticalNavMeshBuilder : MonoBehaviour
{
    [Header("NavMesh Visualization")]
    public Material navMeshMaterial;
    [SerializeField] private bool showTacticalWeights = true;
    [SerializeField] private bool showThreatZones = true;
    [SerializeField] private bool showCoverZones = true;

    [Header("Tactical Parameters")]
    [SerializeField] private float coverDetectionRadius = 3f;
    [SerializeField] private float threatDetectionRadius = 5f;
    [SerializeField] private LayerMask coverLayerMask = -1;
    [SerializeField] private LayerMask enemyLayerMask = -1;

    [Header("Goal Bounding")]
    [SerializeField] private bool enableGoalBounding = true;
    [SerializeField] private BoundingStrategy boundingStrategy = BoundingStrategy.Adaptive;
    [SerializeField] private float baseBoundingRadius = 10f;

    [Header("Performance Monitoring")]
    [SerializeField] private bool showPerformanceMetrics = true;

    // Components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Graph data
    private List<TacticalNode> nodeList = new List<TacticalNode>();
    public Vector3[] weldedVertices;
    public int[] newIndices;

    // Tactical data
    private List<GameObject> enemies = new List<GameObject>();
    private List<GameObject> coverObjects = new List<GameObject>();

    // Performance metrics
    public int LastSearchNodesExplored { get; private set; }
    public float LastSearchTime { get; private set; }

    private bool goalBoundingPreprocessed = false;

    void Start()
    {
        SetupComponents();
        GenerateNavMeshMesh();

        if (enableGoalBounding)
        {
            StartCoroutine(PreprocessGoalBounding());
        }
        else
        {
            goalBoundingPreprocessed = true;
        }

        // Update tactical weights periodically
        InvokeRepeating(nameof(UpdateAllTacticalWeights), 1f, 0.5f);
    }

    void SetupComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (navMeshMaterial)
        {
            meshRenderer.material = navMeshMaterial;
        }
        else
        {
            CreateDefaultTacticalMaterial();
        }
    }

    void CreateDefaultTacticalMaterial()
    {
        Material tacticalMat = new Material(Shader.Find("Standard"));
        tacticalMat.SetFloat("_Mode", 2); // Fade mode
        tacticalMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        tacticalMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        tacticalMat.SetInt("_ZWrite", 0);
        tacticalMat.EnableKeyword("_ALPHABLEND_ON");
        tacticalMat.renderQueue = 3000;

        Color matColor = Color.white;
        matColor.a = 0.7f;
        tacticalMat.color = matColor;

        meshRenderer.material = tacticalMat;
        navMeshMaterial = tacticalMat;
    }

    [ContextMenu("Regen NavMesh")]
    void GenerateNavMeshMesh()
    {
        nodeList.Clear();
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        (weldedVertices, newIndices) = WeldVertices(triangulation);

        // Create tactical nodes
        for (int i = 0; i < newIndices.Length / 3; i++)
        {
            int triIndex = i * 3;
            int v1Index = newIndices[triIndex];
            int v2Index = newIndices[triIndex + 1];
            int v3Index = newIndices[triIndex + 2];

            Vector3 v1 = weldedVertices[v1Index];
            Vector3 v2 = weldedVertices[v2Index];
            Vector3 v3 = weldedVertices[v3Index];

            Vector3 center = (v1 + v2 + v3) / 3f;
            int[] vertices = { v1Index, v2Index, v3Index };

            nodeList.Add(new TacticalNode(i, center, vertices));
        }

        // Connect neighbors
        ConnectNeighbors();

        // Calculate initial tactical weights
        UpdateAllTacticalWeights();

        // Visualize the navmesh with tactical colors
        VisualizeTacticalNavmesh();

        goalBoundingPreprocessed = false;
    }

    void ConnectNeighbors()
    {
        var edgeToTriangles = new Dictionary<string, List<TacticalNode>>();

        foreach (TacticalNode node in nodeList)
        {
            for (int i = 0; i < 3; i++)
            {
                int v1 = node.Vertices[i];
                int v2 = node.Vertices[(i + 1) % 3];
                string edgeKey = v1 < v2 ? $"{v1}-{v2}" : $"{v2}-{v1}";

                if (!edgeToTriangles.ContainsKey(edgeKey))
                {
                    edgeToTriangles[edgeKey] = new List<TacticalNode>();
                }
                edgeToTriangles[edgeKey].Add(node);
            }
        }

        foreach (var edgePair in edgeToTriangles.Values)
        {
            if (edgePair.Count == 2)
            {
                TacticalNode node1 = edgePair[0];
                TacticalNode node2 = edgePair[1];
                node1.Neighbors.Add(node2);
                node2.Neighbors.Add(node1);
            }
        }
    }

    void UpdateAllTacticalWeights()
    {
        // Find all enemies and cover objects
        RefreshTacticalObjects();

        foreach (TacticalNode node in nodeList)
        {
            UpdateNodeTacticalData(node);
        }
    }

    void RefreshTacticalObjects()
    {
        enemies.Clear();
        coverObjects.Clear();

        // Find enemies by tag or layer
        GameObject[] foundEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        enemies.AddRange(foundEnemies);

        // Find cover objects
        GameObject[] foundCovers = GameObject.FindGameObjectsWithTag("Cover");
        coverObjects.AddRange(foundCovers);
    }

    void UpdateNodeTacticalData(TacticalNode node)
    {
        // Reset values
        node.NearbyEnemies.Clear();
        node.NearbyCovers.Clear();
        node.CoverValue = 0f;
        node.ThreatLevel = 0f;
        node.StrategicValue = 0f;

        // Check for nearby cover
        foreach (GameObject cover in coverObjects)
        {
            float distance = Vector3.Distance(node.Center, cover.transform.position);
            if (distance <= coverDetectionRadius)
            {
                node.NearbyCovers.Add(cover);
                node.CoverValue += Mathf.Max(0, (coverDetectionRadius - distance) / coverDetectionRadius);
            }
        }

        // Check for nearby threats
        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(node.Center, enemy.transform.position);
            if (distance <= threatDetectionRadius)
            {
                node.NearbyEnemies.Add(enemy);
                node.ThreatLevel += Mathf.Max(0, (threatDetectionRadius - distance) / threatDetectionRadius);
            }
        }

        // Update tactical weights based on new data
        node.UpdateTacticalWeights();
    }

    // Tactical A* with Goal Bounding
    public List<TacticalNode> FindTacticalPath(Vector3 startPos, Vector3 endPos, GameObject agent = null)
    {
        var searchStartTime = Time.realtimeSinceStartup;

        int startTriangleIndex = GetTriangleIndexFromPosition(startPos);
        int endTriangleIndex = GetTriangleIndexFromPosition(endPos);

        if (startTriangleIndex == -1 || endTriangleIndex == -1)
        {
            Debug.LogError("Start or end position not on NavMesh!");
            return null;
        }

        TacticalNode startNode = nodeList[startTriangleIndex];
        TacticalNode endNode = nodeList[endTriangleIndex];

        var path = TacticalAStarWithGoalBounding(startNode, endNode, endPos, agent);

        LastSearchTime = (Time.realtimeSinceStartup - searchStartTime) * 1000f;

        if (showPerformanceMetrics && path != null)
        {
            string boundingStatus = enableGoalBounding ? "ON" : "OFF";
            Debug.Log($"Tactical Path: {path.Count} nodes, {LastSearchNodesExplored} explored, " +
                     $"{LastSearchTime:F2}ms, Goal Bounding: {boundingStatus}");
        }

        return path;
    }

    List<TacticalNode> TacticalAStarWithGoalBounding(TacticalNode startNode, TacticalNode targetNode, Vector3 goalPosition, GameObject agent)
    {
        var openSet = new List<TacticalNode>();
        var closedSet = new HashSet<TacticalNode>();
        LastSearchNodesExplored = 0;

        // Reset all nodes
        foreach (TacticalNode node in nodeList)
        {
            node.GCost = float.MaxValue;
            node.HCost = 0;
            node.Parent = null;
        }

        openSet.Add(startNode);
        startNode.GCost = 0;
        startNode.HCost = Vector3.Distance(startNode.Center, targetNode.Center);

        while (openSet.Count > 0)
        {
            TacticalNode currentNode = GetLowestFCostNode(openSet);

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
            LastSearchNodesExplored++;

            if (currentNode == targetNode)
            {
                return RetraceTacticalPath(startNode, targetNode);
            }

            foreach (TacticalNode neighbor in currentNode.Neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;

                // Goal bounding check
                if (enableGoalBounding && !WithinTacticalBounds(currentNode, neighbor, goalPosition, agent))
                {
                    continue;
                }

                float newCostToNeighbor = currentNode.GCost + GetTacticalMovementCost(currentNode, neighbor);

                if (newCostToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
                {
                    neighbor.GCost = newCostToNeighbor;
                    neighbor.HCost = Vector3.Distance(neighbor.Center, targetNode.Center);
                    neighbor.Parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // No path found
    }

    TacticalNode GetLowestFCostNode(List<TacticalNode> openSet)
    {
        TacticalNode lowest = openSet[0];
        for (int i = 1; i < openSet.Count; i++)
        {
            if (openSet[i].FCost < lowest.FCost ||
                (openSet[i].FCost == lowest.FCost && openSet[i].HCost < lowest.HCost))
            {
                lowest = openSet[i];
            }
        }
        return lowest;
    }

    float GetTacticalMovementCost(TacticalNode from, TacticalNode to)
    {
        float baseCost = Vector3.Distance(from.Center, to.Center);
        float tacticalCost = to.TacticalWeight;

        // Add transition costs (e.g., moving from cover to open)
        if (from.TacticalType == TacticalType.Safe && to.TacticalType == TacticalType.Danger)
        {
            tacticalCost += 5f; // Penalty for leaving safety
        }

        return baseCost + tacticalCost;
    }

    bool WithinTacticalBounds(TacticalNode currentNode, TacticalNode neighborNode, Vector3 goalPosition, GameObject agent)
    {
        if (currentNode.EdgeBoundingBoxes.ContainsKey(neighborNode))
        {
            BoundingBox boundingBox = currentNode.EdgeBoundingBoxes[neighborNode];

            // Adaptive bounding based on strategy
            switch (boundingStrategy)
            {
                case BoundingStrategy.Fixed:
                    return boundingBox.Contains(goalPosition);

                case BoundingStrategy.Adaptive:
                    // Expand bounds based on threat level
                    return IsWithinAdaptiveBounds(boundingBox, goalPosition, currentNode);

                case BoundingStrategy.Hierarchical:
                    // Use different bounds for different tactical scenarios
                    return IsWithinHierarchicalBounds(boundingBox, goalPosition, agent);
            }
        }

        return true; // Default to allowing exploration
    }

    bool IsWithinAdaptiveBounds(BoundingBox box, Vector3 goal, TacticalNode node)
    {
        float expansionFactor = 1f + (node.ThreatLevel * 0.2f); // Expand bounds in high-threat areas
        BoundingBox expandedBox = new BoundingBox(
            box.left * expansionFactor,
            box.right * expansionFactor,
            box.top * expansionFactor,
            box.bottom * expansionFactor
        );
        return expandedBox.Contains(goal);
    }

    bool IsWithinHierarchicalBounds(BoundingBox box, Vector3 goal, GameObject agent)
    {
        // Different bounding strategies based on agent type or game state
        // This would be expanded based on specific tactical requirements
        return box.Contains(goal);
    }

    List<TacticalNode> RetraceTacticalPath(TacticalNode startNode, TacticalNode endNode)
    {
        var path = new List<TacticalNode>();
        TacticalNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }
        path.Add(startNode);

        path.Reverse();
        return path;
    }

    void VisualizeTacticalNavmesh()
    {
        var combinedVertices = new List<Vector3>();
        var combinedTriangles = new List<int>();
        var combinedColors = new List<Color>();
        int vertexIndex = 0;

        foreach (TacticalNode node in nodeList)
        {
            Color tacticalColor = node.GetTacticalColor();
            tacticalColor.a = 0.7f;

            Vector3 v1 = weldedVertices[node.Vertices[0]];
            Vector3 v2 = weldedVertices[node.Vertices[1]];
            Vector3 v3 = weldedVertices[node.Vertices[2]];

            combinedVertices.Add(v1);
            combinedVertices.Add(v2);
            combinedVertices.Add(v3);

            combinedColors.Add(tacticalColor);
            combinedColors.Add(tacticalColor);
            combinedColors.Add(tacticalColor);

            combinedTriangles.Add(vertexIndex);
            combinedTriangles.Add(vertexIndex + 1);
            combinedTriangles.Add(vertexIndex + 2);

            vertexIndex += 3;
        }

        Mesh tacticalMesh = new Mesh();
        tacticalMesh.vertices = combinedVertices.ToArray();
        tacticalMesh.triangles = combinedTriangles.ToArray();
        tacticalMesh.colors = combinedColors.ToArray();
        tacticalMesh.RecalculateNormals();
        meshFilter.mesh = tacticalMesh;
    }

    void Update()
    {
        if (showTacticalWeights)
        {
            DrawTacticalVisualization();
        }
    }

    void DrawTacticalVisualization()
    {
        foreach (TacticalNode node in nodeList)
        {
            // Draw threat zones
            if (showThreatZones && node.ThreatLevel > 0)
            {
                Debug.DrawRay(node.Center, Vector3.up * node.ThreatLevel, Color.red, 0.1f);
            }

            // Draw cover zones
            if (showCoverZones && node.CoverValue > 0)
            {
                Debug.DrawRay(node.Center, Vector3.up * node.CoverValue, Color.blue, 0.1f);
            }
        }
    }

    // Goal bounding preprocessing (simplified for tactical context)
    System.Collections.IEnumerator PreprocessGoalBounding()
    {
        Debug.Log("Preprocessing tactical goal bounding...");

        foreach (TacticalNode startNode in nodeList)
        {
            var reachableNodes = TacticalDijkstraFloodfill(startNode);
            BuildTacticalBoundingBoxes(startNode, reachableNodes);
            yield return null;
        }

        goalBoundingPreprocessed = true;
        Debug.Log("Tactical goal bounding preprocessing complete!");
    }

    Dictionary<TacticalNode, TacticalNode> TacticalDijkstraFloodfill(TacticalNode startNode)
    {
        var distances = new Dictionary<TacticalNode, float>();
        var startingEdges = new Dictionary<TacticalNode, TacticalNode>();
        var openSet = new List<TacticalNode>();
        var closedSet = new HashSet<TacticalNode>();

        distances[startNode] = 0f;
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            TacticalNode current = GetLowestDistanceNode(openSet, distances);
            openSet.Remove(current);
            closedSet.Add(current);

            foreach (TacticalNode neighbor in current.Neighbors)
            {
                if (closedSet.Contains(neighbor)) continue;

                float newDistance = distances[current] + GetTacticalMovementCost(current, neighbor);

                if (!distances.ContainsKey(neighbor) || newDistance < distances[neighbor])
                {
                    distances[neighbor] = newDistance;

                    if (current == startNode)
                        startingEdges[neighbor] = neighbor;
                    else
                        startingEdges[neighbor] = startingEdges[current];

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return startingEdges;
    }

    TacticalNode GetLowestDistanceNode(List<TacticalNode> openSet, Dictionary<TacticalNode, float> distances)
    {
        TacticalNode lowest = openSet[0];
        for (int i = 1; i < openSet.Count; i++)
        {
            if (distances[openSet[i]] < distances[lowest])
                lowest = openSet[i];
        }
        return lowest;
    }

    void BuildTacticalBoundingBoxes(TacticalNode startNode, Dictionary<TacticalNode, TacticalNode> reachableNodes)
    {
        var edgeGroups = new Dictionary<TacticalNode, List<TacticalNode>>();

        foreach (var kvp in reachableNodes)
        {
            TacticalNode reachableNode = kvp.Key;
            TacticalNode startingEdge = kvp.Value;

            if (!edgeGroups.ContainsKey(startingEdge))
                edgeGroups[startingEdge] = new List<TacticalNode>();

            edgeGroups[startingEdge].Add(reachableNode);
        }

        foreach (var edgeGroup in edgeGroups)
        {
            TacticalNode edgeNode = edgeGroup.Key;
            List<TacticalNode> nodesInGroup = edgeGroup.Value;

            if (nodesInGroup.Count == 0) continue;

            BoundingBox boundingBox = BoundingBox.FromPoint(nodesInGroup[0].Center);
            foreach (TacticalNode node in nodesInGroup)
            {
                boundingBox.ExpandToInclude(node.Center);
            }

            startNode.EdgeBoundingBoxes[edgeNode] = boundingBox;
        }
    }

    // Helper methods
    public List<TacticalNode> GetAllNodes() => nodeList;
    public bool IsGoalBoundingReady() => goalBoundingPreprocessed;

    int GetTriangleIndexFromPosition(Vector3 pointToTest)
    {
        for (int i = 0; i < newIndices.Length; i += 3)
        {
            Vector3 v1 = weldedVertices[newIndices[i]];
            Vector3 v2 = weldedVertices[newIndices[i + 1]];
            Vector3 v3 = weldedVertices[newIndices[i + 2]];

            if (IsPointInTriangle(pointToTest, v1, v2, v3))
            {
                return i / 3;
            }
        }
        return -1;
    }

    bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(has_neg && has_pos);
    }

    float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return (p1.x - p3.x) * (p2.z - p3.z) - (p2.x - p3.x) * (p1.z - p3.z);
    }

    private (Vector3[] weldedVertices, int[] newTriangleIndices) WeldVertices(NavMeshTriangulation triangulation)
    {
        var uniquePositions = new Dictionary<Vector3, int>();
        var newVertices = new List<Vector3>();
        var oldToNewIndexMap = new int[triangulation.vertices.Length];
        int newIndex = 0;

        for (int i = 0; i < triangulation.vertices.Length; i++)
        {
            Vector3 pos = triangulation.vertices[i];
            if (!uniquePositions.ContainsKey(pos))
            {
                uniquePositions.Add(pos, newIndex);
                newVertices.Add(pos);
                oldToNewIndexMap[i] = newIndex;
                newIndex++;
            }
            else
            {
                oldToNewIndexMap[i] = uniquePositions[pos];
            }
        }

        var newTriangleIndices = new int[triangulation.indices.Length];
        for (int i = 0; i < triangulation.indices.Length; i++)
        {
            int oldIndex = triangulation.indices[i];
            newTriangleIndices[i] = oldToNewIndexMap[oldIndex];
        }

        return (newVertices.ToArray(), newTriangleIndices);
    }
}

public enum BoundingStrategy
{
    Fixed,        // Standard fixed-radius bounds
    Adaptive,     // Bounds that adapt to threat level
    Hierarchical  // Different bounds for different tactical scenarios
}