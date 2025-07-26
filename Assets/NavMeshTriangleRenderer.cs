using UnityEngine;
using System.Collections.Generic;

public class NavMeshTriangleRenderer : MonoBehaviour
{
    [Header("Rendering Settings")]
    public Material triangleLineMaterial;
    public Material triangleCenterMaterial;
    public bool showTriangles = true;
    public bool showTriangleCenters = true;
    public float lineWidth = 0.2f;
    public float centerSphereSize = 0.1f;
    public float visualizationHeight = 0.1f;

    private List<GameObject> triangleEdges = new List<GameObject>();
    private List<GameObject> triangleCenters = new List<GameObject>();
    private MapSpawner mapSpawner;

    void Start()
    {
        mapSpawner = GetComponent<MapSpawner>();

        // Create default materials if none assigned
        if (triangleLineMaterial == null)
        {
            triangleLineMaterial = CreateLineMaterial(Color.cyan);
        }

        if (triangleCenterMaterial == null)
        {
            triangleCenterMaterial = CreateSphereMaterial(Color.yellow);
        }
    }

    void Update()
    {
        // Check if triangles are ready and we haven't rendered yet
        if (mapSpawner != null && mapSpawner.navMeshTriangles.Count > 0 && triangleEdges.Count == 0)
        {
            RenderTriangles();
        }

        // Toggle visibility
        SetTriangleVisibility(showTriangles);
        SetCenterVisibility(showTriangleCenters);
    }

    void RenderTriangles()
    {
        ClearExistingGeometry();

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            if (showTriangles)
            {
                CreateTriangleEdges(triangle);
            }

            if (showTriangleCenters)
            {
                CreateTriangleCenter(triangle);
            }
        }

        Debug.Log($"Rendered {triangleEdges.Count} triangle edges and {triangleCenters.Count} centers");
    }

    void CreateTriangleEdges(MapSpawner.NavMeshTriangle triangle)
    {
        Vector3 a = triangle.vertexA + Vector3.up * visualizationHeight;
        Vector3 b = triangle.vertexB + Vector3.up * visualizationHeight;
        Vector3 c = triangle.vertexC + Vector3.up * visualizationHeight;

        // Create three line segments for each triangle
        triangleEdges.Add(CreateLineSegment(a, b, $"Triangle_{triangle.triangleIndex}_AB"));
        triangleEdges.Add(CreateLineSegment(b, c, $"Triangle_{triangle.triangleIndex}_BC"));
        triangleEdges.Add(CreateLineSegment(c, a, $"Triangle_{triangle.triangleIndex}_CA"));
    }

    void CreateTriangleCenter(MapSpawner.NavMeshTriangle triangle)
    {
        Vector3 center = triangle.center + Vector3.up * visualizationHeight;
        GameObject centerSphere = CreateSphere(center, centerSphereSize, $"TriangleCenter_{triangle.triangleIndex}");
        triangleCenters.Add(centerSphere);
    }

    GameObject CreateLineSegment(Vector3 start, Vector3 end, string name)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = triangleLineMaterial;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = true;

        // Make sure we set positions correctly
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        // Disable shadows and lighting for performance
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Make sure line renderer draws properly
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Tile;

        return lineObj;
    }

    GameObject CreateSphere(Vector3 position, float size, string name)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(transform);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;

        // Remove collider for performance
        Destroy(sphere.GetComponent<SphereCollider>());

        // Apply material
        sphere.GetComponent<MeshRenderer>().material = triangleCenterMaterial;
        sphere.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sphere.GetComponent<MeshRenderer>().receiveShadows = false;

        return sphere;
    }

    Material CreateLineMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        return mat;
    }

    Material CreateSphereMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.2f);
        return mat;
    }

    void SetTriangleVisibility(bool visible)
    {
        foreach (var edge in triangleEdges)
        {
            if (edge != null)
                edge.SetActive(visible);
        }
    }

    void SetCenterVisibility(bool visible)
    {
        foreach (var center in triangleCenters)
        {
            if (center != null)
                center.SetActive(visible);
        }
    }

    void ClearExistingGeometry()
    {
        foreach (var edge in triangleEdges)
        {
            if (edge != null)
                DestroyImmediate(edge);
        }

        foreach (var center in triangleCenters)
        {
            if (center != null)
                DestroyImmediate(center);
        }

        triangleEdges.Clear();
        triangleCenters.Clear();
    }

    public void RefreshVisualization()
    {
        ClearExistingGeometry();
        if (mapSpawner != null && mapSpawner.navMeshTriangles.Count > 0)
        {
            RenderTriangles();
        }
    }

    void OnDestroy()
    {
        ClearExistingGeometry();
    }

    // Public method to highlight specific triangles
    public void HighlightTriangles(List<int> triangleIndices, Color highlightColor, float duration = 5f)
    {
        StartCoroutine(HighlightCoroutine(triangleIndices, highlightColor, duration));
    }

    System.Collections.IEnumerator HighlightCoroutine(List<int> triangleIndices, Color highlightColor, float duration)
    {
        List<GameObject> highlightObjects = new List<GameObject>();
        Material highlightMaterial = CreateLineMaterial(highlightColor);

        foreach (int index in triangleIndices)
        {
            if (index < mapSpawner.navMeshTriangles.Count)
            {
                var triangle = mapSpawner.navMeshTriangles[index];
                Vector3 a = triangle.vertexA + Vector3.up * (visualizationHeight + 0.05f);
                Vector3 b = triangle.vertexB + Vector3.up * (visualizationHeight + 0.05f);
                Vector3 c = triangle.vertexC + Vector3.up * (visualizationHeight + 0.05f);

                GameObject highlightLine1 = CreateLineSegment(a, b, $"Highlight_{index}_AB");
                GameObject highlightLine2 = CreateLineSegment(b, c, $"Highlight_{index}_BC");
                GameObject highlightLine3 = CreateLineSegment(c, a, $"Highlight_{index}_CA");

                highlightLine1.GetComponent<LineRenderer>().material = highlightMaterial;
                highlightLine2.GetComponent<LineRenderer>().material = highlightMaterial;
                highlightLine3.GetComponent<LineRenderer>().material = highlightMaterial;

                highlightLine1.GetComponent<LineRenderer>().startWidth = lineWidth * 2f;
                highlightLine1.GetComponent<LineRenderer>().endWidth = lineWidth * 2f;
                highlightLine2.GetComponent<LineRenderer>().startWidth = lineWidth * 2f;
                highlightLine2.GetComponent<LineRenderer>().endWidth = lineWidth * 2f;
                highlightLine3.GetComponent<LineRenderer>().startWidth = lineWidth * 2f;
                highlightLine3.GetComponent<LineRenderer>().endWidth = lineWidth * 2f;

                highlightObjects.Add(highlightLine1);
                highlightObjects.Add(highlightLine2);
                highlightObjects.Add(highlightLine3);
            }
        }

        yield return new WaitForSeconds(duration);

        foreach (var obj in highlightObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
    }
}