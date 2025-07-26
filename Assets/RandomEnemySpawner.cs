using UnityEngine;
using UnityEngine.AI; 

public class RandomEnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;     // your Enemy.prefab here
    public int totalEnemies = 1;
    public Vector3 areaCenter;
    public Vector2 areaSize = new Vector2(40,40);
    public float navMeshSampleDistance = 100f;
    public Transform player;

    void Awake()
    {
      if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
        }
    }

    void Start()
    {
        int spawned  = 0, attempts = 0, maxTries = totalEnemies * 10;
        while (spawned < totalEnemies && attempts++ < maxTries)
        {
            var tryPos = areaCenter + new Vector3(
                Random.Range(-areaSize.x*.5f, areaSize.x*.5f),
                0,
                Random.Range(-areaSize.y*.5f, areaSize.y*.5f)
            );

            // 1) Always instantiate
            var e = Instantiate(enemyPrefab, tryPos, Quaternion.identity);
            e.name = "Enemy";
            e.tag = "Enemy";

            // 2) Ensure NavMeshAgent & warp it on‐mesh
            var agent = e.GetComponent<NavMeshAgent>()
                       ?? e.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;
            agent.updateUpAxis   = false;

            if (NavMesh.SamplePosition(tryPos, out var hit, navMeshSampleDistance, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning($"Spawn#{spawned} off–mesh");

            // 3) Hook up the chase script ONCE
            var chase = e.GetComponent<enemyScript>();
            if (chase != null)
                chase.target = player;
            else
                Debug.LogError("Enemy prefab missing enemyScript!");

            spawned++;
        }
        if (spawned < totalEnemies)
            Debug.LogWarning($"Only got {spawned}/{totalEnemies} enemies on the mesh.");
    }
}
