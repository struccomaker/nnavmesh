using UnityEngine;
using Unity.AI.Navigation;   // for NavMeshSurface
using System.Collections.Generic;
using System.Linq;

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
    public int   blockCount   = 20;  // number of obstacles
    public float blockSpacing = 4f;  // spacing between obstacle‐grid cells

    void Start()
    {
        // 1) Spawn the ground and grab its size & center
        GameObject ground = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity);
        // var bounds = ground.GetComponent<MeshRenderer>().bounds;
        Vector3 center = ground.GetComponent<MeshRenderer>().bounds.center;
        float halfWidth  = ground.GetComponent<MeshRenderer>().bounds.size.x * 0.5f;
        float halfDepth  = ground.GetComponent<MeshRenderer>().bounds.size.z * 0.5f;

        // 2) Figure out a margin so blocks never overhang
        float margin = 0f;
        foreach (var prefab in blockPrefabs)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf == null) continue;
            Vector3 ext = mf.sharedMesh.bounds.extents;
            ext = Vector3.Scale(ext, prefab.transform.localScale);
            margin = Mathf.Max(margin, Mathf.Max(ext.x, ext.z));
        }

        // 3) Build a list of valid block‐spawn cells
        float minX = center.x - halfWidth  + margin;
        float maxX = center.x + halfWidth  - margin;
        float minZ = center.z - halfDepth  + margin;
        float maxZ = center.z + halfDepth  - margin;

        int cellsX = Mathf.FloorToInt((maxX - minX) / blockSpacing);
        int cellsZ = Mathf.FloorToInt((maxZ - minZ) / blockSpacing);

        var cells = new List<Vector2>(cellsX * cellsZ);
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
            var c = shuffled[i];
            Vector3 pos = new Vector3(c.x, 0.5f, c.y);
            GameObject prefab = blockPrefabs[Random.Range(0, blockPrefabs.Length)];
            Quaternion rot   = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);
            Instantiate(prefab, pos, rot);
        }

        // 5) Bake the NavMesh now that obstacles are in place
        surface.BuildNavMesh();

        // 6) Spawn the Player at left‐edge
        Vector3 playerPos = new Vector3(
            center.x - halfWidth + blockSpacing,
            0.5f,
            center.z
        );
        GameObject playerGO = Instantiate(playerPrefab, playerPos, Quaternion.identity);
        playerGO.name = "Player";
        playerGO.tag  = "Player";   // so FindGameObjectWithTag will later work

// 7) Finally, drop in your EnemySpawner prefab so it scatters enemies
if (enemySpawnerPrefab != null)
{
    // Instantiate the spawner at your map’s center
    GameObject spawnerGO = Instantiate(enemySpawnerPrefab, center, Quaternion.identity);

    // Try to get the spawner component
    var sp = spawnerGO.GetComponent<RandomEnemySpawner>();
    if (sp != null)
    {
        // Configure it _once_
        sp.areaCenter = center;
        sp.player     = playerGO.transform;

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
}
