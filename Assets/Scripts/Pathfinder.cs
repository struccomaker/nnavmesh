using System.Collections.Generic;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using System.Linq;

public class Pathfinder : MonoBehaviour
{
    public Material navMeshMaterial; // Default legacy materials that are unlit can be used here. We want to use the vertex colors to visualize the triangles.

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    List<Node> nodeList = new List<Node>();

    void Start()
    {
        // Add MeshFilter and MeshRenderer components if they don't exist
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (navMeshMaterial != null)
        {
            meshRenderer.material = navMeshMaterial;
        }
        else
        {
            Debug.LogWarning("Please assign a material to the 'navMeshMaterial' field for visualization.");
            // Create a default material if none is assigned
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.color = Color.blue;
        }

        GenerateNavMeshMesh();
    }

    void GenerateNavMeshMesh()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        // Calculate triangles from the data
        for (int i = 0; i < triangulation.indices.Length / 3; i++)
        {
            int triIndex = i * 3;
            int v1Index = triangulation.indices[triIndex];
            int v2Index = triangulation.indices[triIndex + 1];
            int v3Index = triangulation.indices[triIndex + 2];

            Vector3 v1 = triangulation.vertices[v1Index];
            Vector3 v2 = triangulation.vertices[v2Index];
            Vector3 v3 = triangulation.vertices[v3Index];

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

        VisualizeNavmesh(triangulation);
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

    private void VisualizeNavmesh(NavMeshTriangulation triangulation)
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
            Vector3 v1 = triangulation.vertices[node.Vertices[0]];
            Vector3 v2 = triangulation.vertices[node.Vertices[1]];
            Vector3 v3 = triangulation.vertices[node.Vertices[2]];

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

        Debug.Log($"NavMesh triangulation generated: {triangulation.vertices.Length} vertices, {triangulation.indices.Length / 3} triangles.");
        Debug.Log($"Generated {nodeList.Count} nodes with {nodeList.Sum(n => n.Neighbors.Count)} total neighbor connections.");
    }
}

public class Node
{
    public int TriangleIndex;
    public Vector3 Center;
    public List<Node> Neighbors;
    public int[] Vertices; // The 3 vertex indices of this triangle

    // You can also add properties for your A* algorithm here
    public float GCost;
    public float HCost;
    public Node Parent;
    public float FCost => GCost + HCost;

    public Node(int triangleIndex, Vector3 center, int[] vertices)
    {
        TriangleIndex = triangleIndex;
        Center = center;
        Vertices = vertices;
        Neighbors = new List<Node>();
    }
}