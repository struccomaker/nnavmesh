using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class Pathfinder : MonoBehaviour
{
    [SerializeField]
    private GraphBuilder graphBuilder;

    [Header("Pathfinding Visualization")]
    [SerializeField]
    private LineRenderer pathLineRenderer;
    [SerializeField]
    private GameObject startMarker;
    [SerializeField]
    private GameObject endMarker;

    [Header("Performance Monitoring")]
    [SerializeField]
    private bool showPerformanceStats = true;

    private List<Node> currentPath = new List<Node>();
    private Vector3? startPosition;
    private Vector3? endPosition;

    //performacce tracking to console
    private int nodesExploredLastSearch = 0;
    private float lastSearchTime = 0.0f;

    void Start()
    {
        if (!graphBuilder)
        {
            graphBuilder = FindObjectOfType<GraphBuilder>();
            if (!graphBuilder)
            {
                Debug.LogError("GraphBuilder not found in the scene. Please add a GraphBuilder component.");
                return;
            }
        }

        SetupPathVisualization();
    }

    void SetupPathVisualization()
    {
        if (!pathLineRenderer)
        {
            GameObject pathObj = new GameObject("PathLine");
            pathLineRenderer = pathObj.AddComponent<LineRenderer>();
            Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.color = Color.yellow;
            pathLineRenderer.material = lineMaterial;
            pathLineRenderer.startWidth = 0.2f;
            pathLineRenderer.endWidth = 0.2f;
            pathLineRenderer.positionCount = 0;
        }

        if (!startMarker)
        {
            startMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            startMarker.name = "StartMarker";
            startMarker.GetComponent<Renderer>().material.color = Color.green;
            startMarker.transform.localScale = Vector3.one * 0.5f;
            startMarker.SetActive(false);
        }

        if (!endMarker)
        {
            endMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            endMarker.name = "EndMarker";
            endMarker.GetComponent<Renderer>().material.color = Color.red;
            endMarker.transform.localScale = Vector3.one * 0.5f;
            endMarker.SetActive(false);
        }
    }

    void Update()
    {
        // Left click to set start point, right click to set end point (probably a bad way to do so in unity lmao)
        if (Input.GetMouseButtonDown(0))
        {
            SetPathPoint(true); // Set start point
        }
        else if (Input.GetMouseButtonDown(1))
        {
            SetPathPoint(false); // Set end point
        }

        // Press Space to calculate path
        if (Input.GetKeyDown(KeyCode.Space) && startPosition.HasValue && endPosition.HasValue)
        {
            CalculatePath(startPosition.Value, endPosition.Value);
        }

        // Press C to clear path
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearPath();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Goal bounding toggle");
        }
    }

    void SetPathPoint(bool isStart)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 worldClickPoint = hit.point;

            if (isStart)
            {
                startPosition = worldClickPoint;
                startMarker.transform.position = worldClickPoint + Vector3.up * 0.1f;
                startMarker.SetActive(true);
                Debug.Log($"Start position set at: {worldClickPoint}");
            }
            else
            {
                endPosition = worldClickPoint;
                endMarker.transform.position = worldClickPoint + Vector3.up * 0.1f;
                endMarker.SetActive(true);
                Debug.Log($"End position set at: {worldClickPoint}");
            }
        }
    }

    public void CalculatePath(Vector3 startPos, Vector3 endPos)
    {
        if (graphBuilder.IsGoalBoundingEnabled() && !graphBuilder.IsGoalBoundingReady())
        {
            Debug.LogWarning("Goal bounding is still preprocessing. Please wait...");
            return;
        }

        // find the triangle nodes for start and end positions
        int startTriangleIndex = GetTriangleIndexFromPosition(startPos);
        int endTriangleIndex = GetTriangleIndexFromPosition(endPos);

        if (startTriangleIndex == -1 || endTriangleIndex == -1)
        {
            Debug.LogError("Start or end position is not on the NavMesh!");
            return;
        }

        Node startNode = GetNodeByTriangleIndex(startTriangleIndex);
        Node endNode = GetNodeByTriangleIndex(endTriangleIndex);

        if (startNode == null || endNode == null)
        {
            Debug.LogError("Could not find start or end node!");
            return;
        }

        //  A* pathfinding (TODO GOAL BOUNDING)
        float searchStartTime = Time.realtimeSinceStartup;
        List<Node> path = AStarWithGoalBounding(startNode, endNode, endPos);
        lastSearchTime = (Time.realtimeSinceStartup - searchStartTime) * 1000f; // Convert to ms

        if (path != null && path.Count > 0)
        {
            currentPath = path;
            VisualizePath(startPos, endPos, path);

            if (showPerformanceStats)
            {
                string boundingStatus = graphBuilder.IsGoalBoundingEnabled() ? "ON" : "OFF";
                Debug.Log($"Path found! Nodes: {path.Count}, Explored: {nodesExploredLastSearch}, " +
                         $"Time: {lastSearchTime:F2}ms, Goal Bounding: {boundingStatus}");
            }
        }
        else
        {
            Debug.LogError("No path found!");
            ClearPath();
        }
    }

    // A* Algorithm 
    public List<Node> AStarWithGoalBounding(Node startNode, Node targetNode, Vector3 goalPosition)
    {
        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        nodesExploredLastSearch = 0;

        ResetNodes();

        openSet.Add(startNode);
        startNode.GCost = 0;
        startNode.HCost = GetDistance(startNode, targetNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                    (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
            nodesExploredLastSearch++;

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            // Check each neighbor with goal bounding optimization
            foreach (Node neighbor in currentNode.Neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;

                // GOAL BOUNDING CHECK - This is the key optimization!
                if (graphBuilder.IsGoalBoundingEnabled() &&
                    !WithinBoundingBox(currentNode, neighbor, goalPosition))
                {
                    // Skip this edge - it won't lead optimally to the goal
                    continue;
                }

                float newCostToNeighbor = currentNode.GCost + GetDistance(currentNode, neighbor);

                if (newCostToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
                {
                    neighbor.GCost = newCostToNeighbor;
                    neighbor.HCost = GetDistance(neighbor, targetNode);
                    neighbor.Parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // No path found
    }

    bool WithinBoundingBox(Node currentNode, Node neighborNode, Vector3 goalPosition)
    {
        // If goal bounding data exists for this edge, check if goal is within bounding box
        if (currentNode.EdgeBoundingBoxes.ContainsKey(neighborNode))
        {
            BoundingBox boundingBox = currentNode.EdgeBoundingBoxes[neighborNode];
            return boundingBox.Contains(goalPosition);
        }

        // If no bounding box data, allow exploration (fallback to standard A*)
        return true;
    }


    // calculate distance between two nodes (Euclidean distance)
    float GetDistance(Node nodeA, Node nodeB)
    {
        return Vector3.Distance(nodeA.Center, nodeB.Center);
    }

    // retrace the path from target back to start
    List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }
        path.Add(startNode);

        path.Reverse();
        return path;
    }

    // reset all nodes for a fresh pathfinding calculation
    void ResetNodes()
    {
        foreach (Node node in GetAllNodes())
        {
            node.GCost = float.MaxValue;
            node.HCost = 0;
            node.Parent = null;
        }
    }

    // Get all nodes from the graph builder
    List<Node> GetAllNodes()
    {
        return graphBuilder.GetAllNodes();
    }

    // getter triangle index
    Node GetNodeByTriangleIndex(int triangleIndex)
    {
        List<Node> allNodes = GetAllNodes();
        return allNodes.FirstOrDefault(node => node.TriangleIndex == triangleIndex);
    }

    // visualise the calculated path
    void VisualizePath(Vector3 startPos, Vector3 endPos, List<Node> path)
    {
        if (path == null || path.Count == 0)
        {
            pathLineRenderer.positionCount = 0;
            return;
        }

        List<Vector3> pathPoints = new List<Vector3>();
        pathPoints.Add(startPos + Vector3.up * 0.1f);

        foreach (Node node in path)
        {
            pathPoints.Add(node.Center + Vector3.up * 0.1f);
        }

        pathPoints.Add(endPos + Vector3.up * 0.1f);

        pathLineRenderer.positionCount = pathPoints.Count;
        pathLineRenderer.SetPositions(pathPoints.ToArray());
    }


    void ClearPath()
    {
        currentPath.Clear();
        pathLineRenderer.positionCount = 0;
        startMarker.SetActive(false);
        endMarker.SetActive(false);
        startPosition = null;
        endPosition = null;
        Debug.Log("Path cleared!");
    }

    [ContextMenu("Compare Performance")]
    void ComparePerformance()
    {
        if (!startPosition.HasValue || !endPosition.HasValue)
        {
            Debug.LogError("Set start and end positions first!");
            return;
        }

        Debug.Log("=== PERFORMANCE COMPARISON ===");

        // Test without goal bounding
        Debug.Log("Testing without Goal Bounding...");
        // You'd need to temporarily disable goal bounding here

        // Test with goal bounding  
        Debug.Log("Testing with Goal Bounding...");
        CalculatePath(startPosition.Value, endPosition.Value);
    }



    // Helper function to get triangle index from a world position so we can get the triangle target
    public int GetTriangleIndexFromPosition(Vector3 pointToTest)
    {
        // Iterate through all triangles in the NavMesh
        for (int i = 0; i < graphBuilder.newIndices.Length; i += 3)
        {
            // Get the vertices of the current triangle
            Vector3 v1 = graphBuilder.weldedVertices[graphBuilder.newIndices[i]];
            Vector3 v2 = graphBuilder.weldedVertices[graphBuilder.newIndices[i + 1]];
            Vector3 v3 = graphBuilder.weldedVertices[graphBuilder.newIndices[i + 2]];

            // Check if the point is inside this triangle
            if (IsPointInTriangle(pointToTest, v1, v2, v3))
            {
                // Return the index of the triangle (e.g., 0 for the first tri, 1 for the second, etc.)
                return i / 3;
            }
        }

        return -1; // No triangle found
    }

    // Cross product check
    private bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(has_neg && has_pos);
    }

    private float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return (p1.x - p3.x) * (p2.z - p3.z) - (p2.x - p3.x) * (p1.z - p3.z);
    }
}