using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    [SerializeField]
    private GraphBuilder graphBuilder;

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
    }

    void Update()
    {
        // Just to test if identifying triangles works when sampling positions on the NavMesh.
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Perform a raycast. Check if it hit something.
            if (Physics.Raycast(ray, out hit))
            {
                // We hit a collider! The world position is hit.point.
                Vector3 worldClickPoint = hit.point;

                int triangleIndex = GetTriangleIndexFromPosition(worldClickPoint);
                if (triangleIndex != -1)
                {
                    Debug.Log($"Triangle index at mouse position: {triangleIndex}");
                }
                else
                {
                    Debug.Log("No NavMesh triangle found at mouse position (check ground collider).");
                }
            }
        }
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
