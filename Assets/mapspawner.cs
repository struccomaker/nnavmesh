using UnityEngine;
using Unity.AI.Navigation;   // for NavMeshSurface
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class MapSpawner : MonoBehaviour
{
    [Header("NavMesh (Runtime Bake)")]
    public NavMeshSurface surface;

    [Header("Prefabs")]
    public GameObject groundPrefab;
    public GameObject[] blockPrefabs;
    public GameObject playerPrefab;
    [Tooltip("Drag your EnemySpawner prefab here")]
    public GameObject enemySpawnerPrefab;

    [Header("Layout Settings")]
    public int blockCount = 20;  // number of obstacles
    public float blockSpacing = 4f;  // spacing between obstacle‐grid cells

    [Header("Triangle Visualization")]
    public bool showTriangles = true;
    public bool showTriangleCenters = true;
    public bool showAgentPaths = true;
    public float visualizationHeight = 0.1f;

    [Header("Colors")]
    public Color triangleColor = Color.cyan;
    public Color triangleCenterColor = Color.yellow;
    public Color agentPathColor = Color.red;

    // Store triangle data for access by other systems
    [System.NonSerialized]
    public List<NavMeshTriangle> navMeshTriangles = new List<NavMeshTriangle>();

    [System.Serializable]
    public struct NavMeshTriangle
    {
        public Vector3 vertexA;
        public Vector3 vertexB;
        public Vector3 vertexC;
        public Vector3 center;
        public int triangleIndex;
        public float area;

        public NavMeshTriangle(Vector3 a, Vector3 b, Vector3 c, int index)
        {
            vertexA = a;
            vertexB = b;
            vertexC = c;
            center = (a + b + c) / 3f;
            triangleIndex = index;
            area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
    }

  IEnumerator Start()
{
    // 1) Spawn the ground and grab its size & center
    GameObject ground = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity);
    var gr    = ground.GetComponent<MeshRenderer>();
    Vector3 center    = gr.bounds.center;
    float halfWidth   = gr.bounds.size.x * 0.5f;
    float halfDepth   = gr.bounds.size.z * 0.5f;

    // 2) For each prefab, measure its true half-extents in XZ
    //    We'll also use these to compute a “margin” and to clamp later
    var prefabExtents = new Dictionary<GameObject, Vector3>();
    float margin = 0f;

    foreach (var prefab in blockPrefabs)
    {
        // instantiate hidden off-screen
        var tmp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        tmp.SetActive(false);

        // combine all renderers' world-space bounds
        var rends = tmp.GetComponentsInChildren<Renderer>();
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        // record the half-extents
        Vector3 ext = b.extents;     // ext.x, ext.y, ext.z
        prefabExtents[prefab] = ext;

        // keep track of the max X/Z extent for our uniform grid
        margin = Mathf.Max(margin, Mathf.Max(ext.x, ext.z));

        Destroy(tmp);
    }

    // 3) Build a uniform grid of cells offset by that margin
    float minX = center.x - halfWidth + margin;
    float maxX = center.x + halfWidth - margin;
    float minZ = center.z - halfDepth + margin;
    float maxZ = center.z + halfDepth - margin;

    int cellsX = Mathf.FloorToInt((maxX - minX) / blockSpacing);
    int cellsZ = Mathf.FloorToInt((maxZ - minZ) / blockSpacing);

    var cells = new List<Vector2>( (cellsX+1)*(cellsZ+1) );
    for (int x = 0; x <= cellsX; x++)
        for (int z = 0; z <= cellsZ; z++)
            cells.Add(new Vector2(minX + x * blockSpacing,
                                  minZ + z * blockSpacing));

    // 4) Shuffle & pop off exactly blockCount cells
    var rng      = new System.Random();
    var shuffled = cells.OrderBy(_ => rng.Next()).ToList();
    int spawnNum = Mathf.Min(blockCount, shuffled.Count);

    for (int i = 0; i < spawnNum; i++)
    {
        // pick the cell center
        var c = shuffled[i];

        // pick a random prefab & its measured half-extents
        var prefab = blockPrefabs[Random.Range(0, blockPrefabs.Length)];
        var ext    = prefabExtents[prefab];

        // clamp so the prefab’s footprint never crosses the true border
        float x = Mathf.Clamp(c.x,
                              center.x - halfWidth + ext.x,
                              center.x + halfWidth - ext.x);
        float z = Mathf.Clamp(c.y,
                              center.z - halfDepth + ext.z,
                              center.z + halfDepth - ext.z);

        // set Y to the prefab’s own half-height (so it sits flush)
        float y = ext.y;

        Vector3 pos = new Vector3(x, y, z);
        Quaternion rot = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);

        Instantiate(prefab, pos, rot);
    }

    // 5) Bake the NavMesh …
    surface.BuildNavMesh();
    yield return null;

        // 6) Build our triangle data structure
        BuildTriangleDataStructure();

        // 7) Spawn the Player at left‐edge
        Vector3 playerPos = new Vector3(
            center.x - halfWidth + blockSpacing,
            0.5f,
            center.z
        );
        GameObject playerGO = Instantiate(playerPrefab, playerPos, Quaternion.identity);
        playerGO.name = "Player";
        playerGO.tag = "Player";   // so FindGameObjectWithTag will later work

        // 8) Finally, drop in your EnemySpawner prefab so it scatters enemies
        if (enemySpawnerPrefab != null)
        {
            GameObject spawnerGO = Instantiate(enemySpawnerPrefab, center, Quaternion.identity);
            var sp = spawnerGO.GetComponent<RandomEnemySpawner>();
            if (sp != null)
            {
                sp.areaCenter = center;
                sp.player = playerGO.transform;
                Debug.Log($"Spawner got player = {sp.player.name}");
            }
            else
            {
                Debug.LogError($"[{nameof(MapSpawner)}] '{enemySpawnerPrefab.name}' is missing a RandomEnemySpawner component!");
            }
        }
        else
        {
            Debug.LogError($"[{nameof(MapSpawner)}] No EnemySpawner prefab assigned!");
        }
    }

    void BuildTriangleDataStructure()
    {
        navMeshTriangles.Clear();

        var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();

        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[i]];
            Vector3 b = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[i + 2]];

            NavMeshTriangle triangle = new NavMeshTriangle(a, b, c, i / 3);
            navMeshTriangles.Add(triangle);
        }

        Debug.Log($"Built NavMesh triangle data: {navMeshTriangles.Count} triangles");
    }

    // void Update()
    // {
    //     // Removed triangle drawing - now handled by NavMeshTriangleRenderer
    //     if (showAgentPaths)
    //     {
    //         DrawAgentPaths();
    //     }
    // }

    void DrawNavMeshTriangles()
    {
        // Draw from our stored triangle data with duration for Game view
        foreach (var triangle in navMeshTriangles)
        {
            Vector3 a = triangle.vertexA + Vector3.up * visualizationHeight;
            Vector3 b = triangle.vertexB + Vector3.up * visualizationHeight;
            Vector3 c = triangle.vertexC + Vector3.up * visualizationHeight;

            // Use Time.deltaTime as duration to refresh every frame
            Debug.DrawLine(a, b, triangleColor, Time.deltaTime);
            Debug.DrawLine(b, c, triangleColor, Time.deltaTime);
            Debug.DrawLine(c, a, triangleColor, Time.deltaTime);

            // Draw triangle center
            if (showTriangleCenters)
            {
                Vector3 center = triangle.center + Vector3.up * visualizationHeight;
                Debug.DrawRay(center, Vector3.up * 0.5f, triangleCenterColor, Time.deltaTime);
            }
        }
    }

    // Use OnDrawGizmos for Scene view visualization
    void OnDrawGizmos()
    {
        DrawTriangleVisualization();
    }

    // This ensures visualization appears in Game view too
    void OnDrawGizmosSelected()
    {
        DrawTriangleVisualization();
    }

    void DrawTriangleVisualization()
    {
        if (!Application.isPlaying || navMeshTriangles == null || navMeshTriangles.Count == 0)
            return;

        if (showTriangles)
        {
            // Draw triangle edges
            Gizmos.color = triangleColor;
            foreach (var triangle in navMeshTriangles)
            {
                Vector3 a = triangle.vertexA + Vector3.up * visualizationHeight;
                Vector3 b = triangle.vertexB + Vector3.up * visualizationHeight;
                Vector3 c = triangle.vertexC + Vector3.up * visualizationHeight;

                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, a);
            }
        }

        if (showTriangleCenters)
        {
            // Draw triangle centers
            Gizmos.color = triangleCenterColor;
            foreach (var triangle in navMeshTriangles)
            {
                Vector3 center = triangle.center + Vector3.up * visualizationHeight;
                Gizmos.DrawWireSphere(center, 0.1f);
            }
        }
    }

    // void DrawAgentPaths()
    // {
    //     // Draw paths for all NavMesh agents in the scene
    //     foreach (var agent in FindObjectsOfType<UnityEngine.AI.NavMeshAgent>())
    //     {
    //         if (!agent.hasPath) continue;

    //         var pathCorners = agent.path.corners;
    //         for (int j = 0; j < pathCorners.Length - 1; j++)
    //         {
    //             Vector3 p1 = pathCorners[j] + Vector3.up * visualizationHeight;
    //             Vector3 p2 = pathCorners[j + 1] + Vector3.up * visualizationHeight;
    //             Debug.DrawLine(p1, p2, agentPathColor);
    //         }
    //     }
    // }

    // Public methods for accessing triangle data
    public NavMeshTriangle GetTriangleAtPosition(Vector3 position)
    {
        foreach (var triangle in navMeshTriangles)
        {
            if (IsPointInTriangle(position, triangle))
            {
                return triangle;
            }
        }
        return new NavMeshTriangle(); // Return empty if not found
    }

    public List<NavMeshTriangle> GetTrianglesInRadius(Vector3 center, float radius)
    {
        List<NavMeshTriangle> trianglesInRadius = new List<NavMeshTriangle>();
        float radiusSquared = radius * radius;

        foreach (var triangle in navMeshTriangles)
        {
            if (Vector3.SqrMagnitude(triangle.center - center) <= radiusSquared)
            {
                trianglesInRadius.Add(triangle);
            }
        }

        return trianglesInRadius;
    }

    bool IsPointInTriangle(Vector3 point, NavMeshTriangle triangle)
    {
        Vector3 a = triangle.vertexA;
        Vector3 b = triangle.vertexB;
        Vector3 c = triangle.vertexC;

        Vector3 v0 = c - a;
        Vector3 v1 = b - a;
        Vector3 v2 = point - a;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0) && (v >= 0) && (u + v < 1);
    }

    // Debug method to highlight specific triangles
    public void HighlightTriangles(List<int> triangleIndices, Color highlightColor, float duration = 1f)
    {
        foreach (int index in triangleIndices)
        {
            if (index < navMeshTriangles.Count)
            {
                var triangle = navMeshTriangles[index];
                Vector3 a = triangle.vertexA + Vector3.up * (visualizationHeight + 0.1f);
                Vector3 b = triangle.vertexB + Vector3.up * (visualizationHeight + 0.1f);
                Vector3 c = triangle.vertexC + Vector3.up * (visualizationHeight + 0.1f);

                Debug.DrawLine(a, b, highlightColor, duration);
                Debug.DrawLine(b, c, highlightColor, duration);
                Debug.DrawLine(c, a, highlightColor, duration);
            }
        }
    }
}