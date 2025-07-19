using System.Collections.Generic;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using System.Linq;

public class Node
{
    public int TriangleIndex;
    public Vector3 Center;
    public List<Node> Neighbors;
    public int[] Vertices; // The 3 vertex indices of this triangle

    // A* algorithm properties
    public float GCost;
    public float HCost;
    public Node Parent;
    public float FCost => GCost + HCost;

    // Goal bounding data - bounding box for each neighbor edge
    public Dictionary<Node, BoundingBox> EdgeBoundingBoxes;

    public Node(int triangleIndex, Vector3 center, int[] vertices)
    {
        TriangleIndex = triangleIndex;
        Center = center;
        Vertices = vertices;
        Neighbors = new List<Node>();
        EdgeBoundingBoxes = new Dictionary<Node, BoundingBox>();
    }
}

// Bounding box structure for goal bounding
[System.Serializable]
public struct BoundingBox
{
    public float left, right, top, bottom;

    public BoundingBox(float left, float right, float top, float bottom)
    {
        this.left = left;
        this.right = right;
        this.top = top;
        this.bottom = bottom;
    }

    public bool Contains(Vector3 point)
    {
        return point.x >= left && point.x <= right &&
               point.z >= bottom && point.z <= top;
    }

    public void ExpandToInclude(Vector3 point)
    {
        left = Mathf.Min(left, point.x);
        right = Mathf.Max(right, point.x);
        bottom = Mathf.Min(bottom, point.z);
        top = Mathf.Max(top, point.z);
    }

    public static BoundingBox FromPoint(Vector3 point)
    {
        return new BoundingBox(point.x, point.x, point.z, point.z);
    }

}


public class GraphBuilder : MonoBehaviour
{
    public Material navMeshMaterial; // Default legacy materials that are unlit can be used here. We want to use the vertex colors to visualize the triangles.
    [Header("Goal Bounding")]
    [SerializeField] private bool enableGoalBounding = true;
    [SerializeField] private bool showPreprocessingProgress = true;
    // Draw debugs
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    // We must represent the Nodes ourselves so that we can perform A* and goal bounding on our own
    List<Node> nodeList = new List<Node>();

    // Triangulation data
    public Vector3[] weldedVertices;
    public int[] newIndices;

    private bool goalBoundingPreprocessed = false;
    
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (!navMeshMaterial) meshRenderer.material = navMeshMaterial;
        else
        {
            Debug.LogWarning("Please assign a material to the 'navMeshMaterial' field for visualization.");
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.color = Color.blue;
        }

        GenerateNavMeshMesh();
        if (enableGoalBounding)
        {
            StartCoroutine(PreprocessGoalBounding());
        }
        else
        {
            goalBoundingPreprocessed = true; 
        }
    }

    [ContextMenu("Regen Navmesh")]
    void GenerateNavMeshMesh()
    {
        nodeList.Clear();
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        (weldedVertices, newIndices) = WeldVertices(triangulation);

        // Calculate triangles from the data
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

            nodeList.Add(new Node(i, center, vertices));
        }

        // Calculate neighbor edges
        var edgeToTriangles = new Dictionary<string, List<Node>>();
        foreach (Node node in nodeList)
        {
            for (int i = 0; i < 3; i++)
            {
                // Get the two vertices that form an edge
                int v1 = node.Vertices[i];
                int v2 = node.Vertices[(i + 1) % 3];

                // Create a consistent key for the edge regardless of vertex order
                string edgeKey = v1 < v2 ? $"{v1}-{v2}" : $"{v2}-{v1}";

                if (!edgeToTriangles.ContainsKey(edgeKey))
                {
                    edgeToTriangles[edgeKey] = new List<Node>();
                }
                edgeToTriangles[edgeKey].Add(node);
            }
        }

        // Connect neighbors on shared edges
        foreach (var edgePair in edgeToTriangles.Values)
        {
            // If an edge is shared by two triangles, they are neighbors
            if (edgePair.Count == 2)
            {
                Node node1 = edgePair[0];
                Node node2 = edgePair[1];

                node1.Neighbors.Add(node2);
                node2.Neighbors.Add(node1);
            }
        }

        VisualizeNavmesh(weldedVertices, newIndices);
        goalBoundingPreprocessed = false;
    }

    System.Collections.IEnumerator PreprocessGoalBounding()
    {
        Debug.Log("Starting Goal Bounding preprocessing...");
        float startTime = Time.realtimeSinceStartup;

        int processedNodes = 0;
        int totalNodes = nodeList.Count;

        foreach (Node startNode in nodeList)
        {
            // Run Dijkstra floodfill from this node
            var reachableNodes = DijkstraFloodfill(startNode);

            // Build bounding boxes for each edge
            BuildBoundingBoxesForNode(startNode, reachableNodes);

            processedNodes++;

            if (showPreprocessingProgress && processedNodes % 10 == 0)
            {
                Debug.Log($"Goal Bounding Progress: {processedNodes}/{totalNodes} nodes processed");
                yield return null; // Allow Unity to update
            }
        }

        float endTime = Time.realtimeSinceStartup;
        goalBoundingPreprocessed = true;

        Debug.Log($"Goal Bounding preprocessing completed in {endTime - startTime:F2} seconds");
        Debug.Log($"Processed {totalNodes} nodes with {nodeList.Sum(n => n.EdgeBoundingBoxes.Count)} bounding boxes");
    }

    // Dijkstra floodfill to find all reachable nodes and their optimal starting edges
    Dictionary<Node, Node> DijkstraFloodfill(Node startNode)
    {
        var distances = new Dictionary<Node, float>();
        var startingEdges = new Dictionary<Node, Node>(); // Maps node to the neighbor from start that leads to it
        var openSet = new List<Node>();
        var closedSet = new HashSet<Node>();

        // Initialize
        distances[startNode] = 0f;
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            // Find node with minimum distance
            Node current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (distances[openSet[i]] < distances[current])
                    current = openSet[i];
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Node neighbor in current.Neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;

                float newDistance = distances[current] + Vector3.Distance(current.Center, neighbor.Center);

                if (!distances.ContainsKey(neighbor) || newDistance < distances[neighbor])
                {
                    distances[neighbor] = newDistance;

                    // Track which starting edge leads to this neighbor
                    if (current == startNode)
                    {
                        startingEdges[neighbor] = neighbor; // Direct neighbor
                    }
                    else
                    {
                        startingEdges[neighbor] = startingEdges[current]; // Inherit starting edge
                    }

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return startingEdges;
    }

    // Build bounding boxes for each edge of a node
    void BuildBoundingBoxesForNode(Node startNode, Dictionary<Node, Node> reachableNodes)
    {
        // Group reachable nodes by their starting edge
        var edgeGroups = new Dictionary<Node, List<Node>>();

        foreach (var kvp in reachableNodes)
        {
            Node reachableNode = kvp.Key;
            Node startingEdge = kvp.Value;

            if (!edgeGroups.ContainsKey(startingEdge))
                edgeGroups[startingEdge] = new List<Node>();

            edgeGroups[startingEdge].Add(reachableNode);
        }

        // Create bounding box for each edge group
        foreach (var edgeGroup in edgeGroups)
        {
            Node edgeNode = edgeGroup.Key;
            List<Node> nodesInGroup = edgeGroup.Value;

            if (nodesInGroup.Count == 0) continue;

            // Create bounding box containing all nodes reachable through this edge
            BoundingBox boundingBox = BoundingBox.FromPoint(nodesInGroup[0].Center);

            foreach (Node node in nodesInGroup)
            {
                boundingBox.ExpandToInclude(node.Center);
            }

            startNode.EdgeBoundingBoxes[edgeNode] = boundingBox;
        }
    }


    void Update()
    {
        // Draw all edge connections between nodes
        foreach (Node node in nodeList)
        {
            foreach (Node neighbor in node.Neighbors)
            {
                Debug.DrawLine(node.Center, neighbor.Center, Color.red);
            }
        }
    }

    // Helper methods
    public List<Node> GetAllNodes()
    {
        return nodeList;
    }

    public bool IsGoalBoundingReady()
    {
        return goalBoundingPreprocessed;
    }

    public bool IsGoalBoundingEnabled()
    {
        return enableGoalBounding;
    }


    // Helper welder function to merge vertices in the same spot that aren't the same index
    private (Vector3[] weldedVertices, int[] newTriangleIndices) WeldVertices(NavMeshTriangulation triangulation)
    {
        var uniquePositions = new Dictionary<Vector3, int>();
        var newVertices = new List<Vector3>();
        var oldToNewIndexMap = new int[triangulation.vertices.Length];
        int newIndex = 0;

        // First pass: Find all unique vertex positions
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

        // Second pass: Create the new triangle index list using the remapped indices
        var newTriangleIndices = new int[triangulation.indices.Length];
        for (int i = 0; i < triangulation.indices.Length; i++)
        {
            int oldIndex = triangulation.indices[i];
            newTriangleIndices[i] = oldToNewIndexMap[oldIndex];
        }

        Debug.Log($"Vertex welding complete: {triangulation.vertices.Length} original -> {newVertices.Count} unique.");

        return (newVertices.ToArray(), newTriangleIndices);
    }

    private void VisualizeNavmesh(Vector3[] vertices, int[] indices)
    {
        // Draw a triangle for each node
        List<Vector3> combinedVertices = new List<Vector3>();
        List<int> combinedTriangles = new List<int>();
        List<Color> combinedColors = new List<Color>();
        int vertexIndex = 0;
        foreach (Node node in nodeList)
        {
            Color triangleColor = new Color(Random.value, Random.value, Random.value);

            // Get the vertices for the current triangle
            Vector3 v1 = vertices[node.Vertices[0]];
            Vector3 v2 = vertices[node.Vertices[1]];
            Vector3 v3 = vertices[node.Vertices[2]];

            // Add these vertices to our combined list
            combinedVertices.Add(v1);
            combinedVertices.Add(v2);
            combinedVertices.Add(v3);

            combinedColors.Add(triangleColor);
            combinedColors.Add(triangleColor);
            combinedColors.Add(triangleColor);

            // Add the indices for this new triangle.
            // The indices are relative to the vertices we just added.
            combinedTriangles.Add(vertexIndex);
            combinedTriangles.Add(vertexIndex + 1);
            combinedTriangles.Add(vertexIndex + 2);

            // Increment our vertex index for the next triangle
            vertexIndex += 3;
        }

        Mesh finalMesh = new Mesh();
        finalMesh.vertices = combinedVertices.ToArray();
        finalMesh.triangles = combinedTriangles.ToArray();
        finalMesh.colors = combinedColors.ToArray();
        finalMesh.RecalculateNormals();
        meshFilter.mesh = finalMesh;
        meshRenderer.material = navMeshMaterial;

        Debug.Log($"NavMesh triangulation generated: {vertices.Length} vertices, {indices.Length / 3} triangles.");
        Debug.Log($"Generated {nodeList.Count} nodes with {nodeList.Sum(n => n.Neighbors.Count)} total neighbor connections.");
    }


}

