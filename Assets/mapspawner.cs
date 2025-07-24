using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public class MapSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject groundPrefab;     // scaled howevesr you like in the Project
    public GameObject[] blockPrefabs;   // e.g. cube and L‐block
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("Layout Settings")]
    public int   blockCount   = 20;     // how many obstacles
    public float blockSpacing = 4f;     // grid cell size

    void Start()
    {
        // 1) Spawn ground & measure its world bounds
        var ground = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity);
        var groundMR = ground.GetComponent<MeshRenderer>();
        Vector3 groundSize = groundMR.bounds.size;   // X/Z is full world size
        Vector3 center     = ground.transform.position;

        // 2) Compute a “margin” so no block can overhang
        float margin = 0f;
foreach (var prefab in blockPrefabs)
{
    var mf = prefab.GetComponentInChildren<MeshFilter>();
    if (mf != null)
    {
        // get half-size in local space, then apply prefab’s scale
        Vector3 ext = mf.sharedMesh.bounds.extents;
        ext = Vector3.Scale(ext, prefab.transform.localScale);

        // pick the larger of the two horizontal axes
        float maxHalfSize = Mathf.Max(ext.x, ext.z);
        margin = Mathf.Max(margin, maxHalfSize);
    }
}

// 3) Now shrink your spawn bounds by that single margin on all sides:
float minX = center.x - groundSize.x * 0.5f + margin;
float maxX = center.x + groundSize.x * 0.5f - margin;
float minZ = center.z - groundSize.z * 0.5f + margin;
float maxZ = center.z + groundSize.z * 0.5f - margin;

        // 4) Tile that area into blockSpacing × blockSpacing cells
        int cellsX = Mathf.FloorToInt((maxX - minX) / blockSpacing);
        int cellsZ = Mathf.FloorToInt((maxZ - minZ) / blockSpacing);
        var cells = new List<Vector2>(cellsX * cellsZ);

        for (int ix = 0; ix <= cellsX; ix++)
        {
            for (int iz = 0; iz <= cellsZ; iz++)
            {
                float x = minX + ix * blockSpacing;
                float z = minZ + iz * blockSpacing;
                cells.Add(new Vector2(x, z));
            }
        }

        // 5) Shuffle & spawn exactly blockCount cells
        var rng      = new System.Random();
        var shuffled = cells.OrderBy(c => rng.Next()).ToList();
        int spawnNum = Mathf.Min(blockCount, shuffled.Count);

        for (int i = 0; i < spawnNum; i++)
        {
            var cell = shuffled[i];
            var pos  = new Vector3(cell.x, 0.5f, cell.y);
            var prefab = blockPrefabs[Random.Range(0, blockPrefabs.Length)];
            var rot    = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);
            Instantiate(prefab, pos, rot);
        }

        // 6) Spawn Player at left edge, centered in Z
        Vector3 playerPos = new Vector3(
            center.x - groundSize.x * 0.5f + blockSpacing,
            0.5f,
            center.z
        );
        var player = Instantiate(playerPrefab, playerPos, Quaternion.identity);
        player.name = "Player";

        // 7) Spawn Enemy at right edge
        Vector3 enemyPos = new Vector3(
            center.x + groundSize.x * 0.5f - blockSpacing,
            0.5f,
            center.z
        );
        var enemy = Instantiate(enemyPrefab, enemyPos, Quaternion.identity);
        enemy.name = "Enemy";

        // 8) Ensure the Enemy has a NavMeshAgent
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = enemy.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;
            agent.updateUpAxis   = false;
        }

        // 9) Hook up the chase script
        enemy.GetComponent<EnemyChase>().target = player.transform;
    }
}
