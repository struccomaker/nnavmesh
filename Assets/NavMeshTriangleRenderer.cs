using UnityEngine;
using System.Collections.Generic;

public class NavMeshTriangleRenderer : MonoBehaviour
{
    [Header("Rendering Settings")]
    public bool showTriangles = true;
    public bool showTriangleCenters = false;
    public bool showWeightColors = true;
    public float lineWidth = 0.05f;
    public float centerSphereSize = 0.1f;
    public float visualizationHeight = 0.1f;

    [Header("Update Settings")]
    public bool updateInRealTime = true;
    public float updateInterval = 0.5f;

    private Dictionary<int, GameObject[]> triangleEdgeObjects = new Dictionary<int, GameObject[]>();
    private Dictionary<int, GameObject> triangleCenterObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, Material> triangleMaterials = new Dictionary<int, Material>();

    private MapSpawner mapSpawner;
    private TacticalWeightSystem tacticalSystem;
    private float lastUpdateTime;

    void Start()
    {
        mapSpawner = GetComponent<MapSpawner>();
        tacticalSystem = GetComponent<TacticalWeightSystem>();

        if (tacticalSystem == null)
        {
            Debug.LogWarning("TacticalTriangleRenderer: No TacticalWeightSystem found! Weight colors will not work.");
        }
    }

    void Update()
    {
        // Check if triangles are ready and we haven't rendered yet
        if (mapSpawner != null && mapSpawner.navMeshTriangles.Count > 0 && triangleEdgeObjects.Count == 0)
        {
            RenderTriangles();
        }

        // Update colors in real-time
        if (updateInRealTime && Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateTriangleColors();
            lastUpdateTime = Time.time;
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

        Debug.Log($"Rendered {triangleEdgeObjects.Count} triangles with tactical weight colors");
    }

    void CreateTriangleEdges(MapSpawner.NavMeshTriangle triangle)
    {
        Vector3 a = triangle.vertexA + Vector3.up * visualizationHeight;
        Vector3 b = triangle.vertexB + Vector3.up * visualizationHeight;
        Vector3 c = triangle.vertexC + Vector3.up * visualizationHeight;

        // Create three line segments for each triangle
        GameObject[] edges = new GameObject[3];
        edges[0] = CreateLineSegment(a, b, $"Triangle_{triangle.triangleIndex}_AB");
        edges[1] = CreateLineSegment(b, c, $"Triangle_{triangle.triangleIndex}_BC");
        edges[2] = CreateLineSegment(c, a, $"Triangle_{triangle.triangleIndex}_CA");

        triangleEdgeObjects[triangle.triangleIndex] = edges;

        // Create material for this triangle
        Color triangleColor = GetTriangleColor(triangle.triangleIndex);
        Material triangleMaterial = CreateLineMaterial(triangleColor);
        triangleMaterials[triangle.triangleIndex] = triangleMaterial;

        // Apply material to all edges
        foreach (var edge in edges)
        {
            edge.GetComponent<LineRenderer>().material = triangleMaterial;
        }
    }

    void CreateTriangleCenter(MapSpawner.NavMeshTriangle triangle)
    {
        Vector3 center = triangle.center + Vector3.up * visualizationHeight;
        GameObject centerSphere = CreateSphere(center, centerSphereSize, $"TriangleCenter_{triangle.triangleIndex}");
        triangleCenterObjects[triangle.triangleIndex] = centerSphere;
    }

    GameObject CreateLineSegment(Vector3 start, Vector3 end, string name)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        // Disable shadows for performance
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
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

        Destroy(sphere.GetComponent<SphereCollider>());
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

    Color GetTriangleColor(int triangleIndex)
    {
        if (tacticalSystem != null && showWeightColors)
        {
            return tacticalSystem.GetTriangleColor(triangleIndex);
        }
        return Color.cyan; // Default color if no tactical system
    }

    void UpdateTriangleColors()
    {
        if (tacticalSystem == null || !showWeightColors) return;

        var triangleColors = tacticalSystem.GetAllTriangleColors();

        foreach (var kvp in triangleColors)
        {
            int triangleIndex = kvp.Key;
            Color newColor = kvp.Value;

            if (triangleMaterials.ContainsKey(triangleIndex))
            {
                triangleMaterials[triangleIndex].color = newColor;
            }
        }
    }

    void SetTriangleVisibility(bool visible)
    {
        foreach (var kvp in triangleEdgeObjects)
        {
            foreach (var edge in kvp.Value)
            {
                if (edge != null)
                    edge.SetActive(visible);
            }
        }
    }

    void SetCenterVisibility(bool visible)
    {
        foreach (var kvp in triangleCenterObjects)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(visible);
        }
    }

    void ClearExistingGeometry()
    {
        foreach (var kvp in triangleEdgeObjects)
        {
            foreach (var edge in kvp.Value)
            {
                if (edge != null)
                    DestroyImmediate(edge);
            }
        }

        foreach (var kvp in triangleCenterObjects)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);
        }

        foreach (var kvp in triangleMaterials)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);
        }

        triangleEdgeObjects.Clear();
        triangleCenterObjects.Clear();
        triangleMaterials.Clear();
    }

    public void RefreshVisualization()
    {
        ClearExistingGeometry();
        if (mapSpawner != null && mapSpawner.navMeshTriangles.Count > 0)
        {
            RenderTriangles();
        }
    }

    // Method to highlight specific weight ranges
    public void HighlightWeightRanges()
    {
        if (tacticalSystem == null) return;

        Debug.Log("=== Triangle Weight Analysis ===");

        int safeCount = 0, dangerCount = 0, neutralCount = 0;

        foreach (var triangle in mapSpawner.navMeshTriangles)
        {
            float weight = tacticalSystem.GetTriangleWeight(triangle.triangleIndex);

            if (weight <= tacticalSystem.safeThreshold)
                safeCount++;
            else if (weight >= tacticalSystem.dangerThreshold)
                dangerCount++;
            else
                neutralCount++;
        }

        Debug.Log($"Safe triangles (Green): {safeCount}");
        Debug.Log($"Danger triangles (Red): {dangerCount}");
        Debug.Log($"Neutral triangles (Yellow): {neutralCount}");
        Debug.Log($"Total triangles: {mapSpawner.navMeshTriangles.Count}");
    }

    void OnDestroy()
    {
        ClearExistingGeometry();
    }

    // GUI for debugging
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== Tactical Triangle Renderer ===");

        bool newShowTriangles = GUILayout.Toggle(showTriangles, "Show Triangles");
        if (newShowTriangles != showTriangles)
        {
            showTriangles = newShowTriangles;
        }

        bool newShowWeightColors = GUILayout.Toggle(showWeightColors, "Show Weight Colors");
        if (newShowWeightColors != showWeightColors)
        {
            showWeightColors = newShowWeightColors;
            UpdateTriangleColors(); // Refresh colors immediately
        }

        bool newUpdateInRealTime = GUILayout.Toggle(updateInRealTime, "Real-time Updates");
        if (newUpdateInRealTime != updateInRealTime)
        {
            updateInRealTime = newUpdateInRealTime;
        }

        if (GUILayout.Button("Refresh Visualization"))
        {
            RefreshVisualization();
        }

        if (GUILayout.Button("Analyze Weight Distribution"))
        {
            HighlightWeightRanges();
        }

        // Display current stats
        if (tacticalSystem != null)
        {
            GUILayout.Label($"Player Weapon: {(tacticalSystem.GetComponent<PlayerWeaponSystem>()?.currentWeapon ?? PlayerWeaponSystem.WeaponType.Melee)}");
            GUILayout.Label($"Triangles Rendered: {triangleEdgeObjects.Count}");
        }

        GUILayout.EndArea();
    }
}