using System.Collections.Generic;
using UnityEngine;

public class AIManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private int initialAgentCount = 5;
    [SerializeField] private int maxAgentCount = 20;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private Transform spawnCenter;

    [Header("Agent Types")]
    [SerializeField] private bool spawnZombies = true;
    [SerializeField] private bool spawnTacticalEnemies = true;
    [SerializeField] private bool spawnScouts = true;

    [Header("Performance Monitoring")]
    [SerializeField] private bool showPerformanceStats = true;
    [SerializeField] private float statsUpdateInterval = 1f;

    // Agent management
    private List<BasicAIAgent> activeAgents = new List<BasicAIAgent>();
    private Transform player;

    // Performance tracking
    private float lastStatsUpdate;
    private int totalPathfindingCalls;
    private float totalPathfindingTime;

    void Start()
    {
        FindPlayer();

        if (!spawnCenter)
        {
            spawnCenter = transform;
        }

        SpawnInitialAgents();

        if (showPerformanceStats)
        {
            InvokeRepeating(nameof(UpdatePerformanceStats), 1f, statsUpdateInterval);
        }
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("No player found! Make sure player GameObject has 'Player' tag.");
        }
    }

    void Update()
    {
        HandleInput();
        CleanupDestroyedAgents();
    }

    void HandleInput()
    {
        // Spawn agent with + key
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
        {
            SpawnAgent();
        }

        // Remove agent with - key
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            RemoveRandomAgent();
        }

        // Force all agents to recalculate paths with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            ForceAllAgentsPathRecalculation();
        }

        // Clear all agents with C key
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllAgents();
        }
    }

    void SpawnInitialAgents()
    {
        for (int i = 0; i < initialAgentCount; i++)
        {
            SpawnAgent();
        }

        Debug.Log($"Spawned {initialAgentCount} initial AI agents");
    }

    public void SpawnAgent()
    {
        if (activeAgents.Count >= maxAgentCount)
        {
            Debug.Log($"Maximum agent count ({maxAgentCount}) reached!");
            return;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning("Could not find valid spawn position!");
            return;
        }

        GameObject newAgentObj = CreateAgentPrefab(spawnPosition);
        BasicAIAgent agent = newAgentObj.GetComponent<BasicAIAgent>();

        if (agent)
        {
            // Set random agent type
            SetRandomAgentType(agent);
            activeAgents.Add(agent);

            Debug.Log($"Spawned {agent.name} at {spawnPosition}");
        }
    }

    GameObject CreateAgentPrefab(Vector3 position)
    {
        GameObject agentObj;

        if (agentPrefab)
        {
            agentObj = Instantiate(agentPrefab, position, Quaternion.identity, transform);
        }
        else
        {
            // Create basic agent if no prefab assigned
            agentObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agentObj.transform.position = position;
            agentObj.transform.parent = transform;
            agentObj.name = "AI_Agent";

            // Remove default collider and add our script
            Destroy(agentObj.GetComponent<CapsuleCollider>());
            agentObj.AddComponent<BasicAIAgent>();

            // Scale down to human size
            agentObj.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        }

        return agentObj;
    }

    void SetRandomAgentType(BasicAIAgent agent)
    {
        List<BasicAIAgent.AIAgentType> availableTypes = new List<BasicAIAgent.AIAgentType>();

        if (spawnZombies) availableTypes.Add(BasicAIAgent.AIAgentType.Zombie);
        if (spawnTacticalEnemies) availableTypes.Add(BasicAIAgent.AIAgentType.TacticalEnemy);
        if (spawnScouts) availableTypes.Add(BasicAIAgent.AIAgentType.Scout);

        if (availableTypes.Count > 0)
        {
            var randomType = availableTypes[Random.Range(0, availableTypes.Count)];
            // Note: You'd need to add a SetAgentType method to BasicAIAgent to use this
            // For now, agents will use their default type set in the inspector
        }

        // Set random color variation
        Renderer renderer = agent.GetComponent<Renderer>();
        if (renderer)
        {
            Color baseColor = Color.red;
            Color variation = new Color(
                baseColor.r + Random.Range(-0.3f, 0.3f),
                baseColor.g + Random.Range(-0.3f, 0.3f),
                baseColor.b + Random.Range(-0.3f, 0.3f)
            );
            renderer.material.color = variation;
        }
    }

    Vector3 GetValidSpawnPosition()
    {
        int attempts = 0;
        int maxAttempts = 20;

        while (attempts < maxAttempts)
        {
            // Random position around spawn center
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = spawnCenter.position + new Vector3(randomCircle.x, 1f, randomCircle.y);

            // Simple validation - make sure not too close to player
            if (player && Vector3.Distance(spawnPos, player.position) < 3f)
            {
                attempts++;
                continue;
            }

            // Check if position is clear (simple overlap check)
            if (!Physics.CheckSphere(spawnPos, 0.5f))
            {
                return spawnPos;
            }

            attempts++;
        }

        return Vector3.zero; // Failed to find position
    }

    public void RemoveRandomAgent()
    {
        if (activeAgents.Count > 0)
        {
            int randomIndex = Random.Range(0, activeAgents.Count);
            BasicAIAgent agentToRemove = activeAgents[randomIndex];

            activeAgents.RemoveAt(randomIndex);

            if (agentToRemove != null)
            {
                Destroy(agentToRemove.gameObject);
                Debug.Log($"Removed agent. Remaining: {activeAgents.Count}");
            }
        }
    }

    void CleanupDestroyedAgents()
    {
        // Remove null references from destroyed agents
        activeAgents.RemoveAll(agent => agent == null);
    }

    public void ForceAllAgentsPathRecalculation()
    {
        foreach (var agent in activeAgents)
        {
            if (agent != null)
            {
                agent.ForcePathRecalculation();
            }
        }

        Debug.Log($"Forced path recalculation for {activeAgents.Count} agents");
    }

    public void ClearAllAgents()
    {
        foreach (var agent in activeAgents)
        {
            if (agent != null)
            {
                Destroy(agent.gameObject);
            }
        }

        activeAgents.Clear();
        Debug.Log("Cleared all AI agents");
    }

    void UpdatePerformanceStats()
    {
        if (activeAgents.Count == 0) return;

        // Collect performance data from agents
        totalPathfindingCalls = 0;
        totalPathfindingTime = 0f;

        foreach (var agent in activeAgents)
        {
            if (agent != null)
            {
                float agentPathTime = agent.GetLastPathfindingTime();
                if (agentPathTime > 0)
                {
                    totalPathfindingCalls++;
                    totalPathfindingTime += agentPathTime;
                }
            }
        }
    }

    void OnGUI()
    {
        if (!showPerformanceStats) return;

        // Performance display
        GUILayout.BeginArea(new Rect(10, 100, 300, 150));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("AI MANAGER", GUI.skin.label);
        GUILayout.Label($"Active Agents: {activeAgents.Count}/{maxAgentCount}");

        if (totalPathfindingCalls > 0)
        {
            float avgPathTime = totalPathfindingTime / totalPathfindingCalls;
            GUILayout.Label($"Avg Path Time: {avgPathTime:F2}ms");
            GUILayout.Label($"Pathfinding Calls: {totalPathfindingCalls}");
        }

        GUILayout.Space(10);
        GUILayout.Label("Controls:");
        GUILayout.Label("+ : Spawn Agent");
        GUILayout.Label("- : Remove Agent");
        GUILayout.Label("R : Recalc All Paths");
        GUILayout.Label("C : Clear All Agents");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn radius
        if (spawnCenter)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter.position, spawnRadius);
        }
    }

    // Public getters for external systems
    public int GetActiveAgentCount() => activeAgents.Count;
    public List<BasicAIAgent> GetActiveAgents() => new List<BasicAIAgent>(activeAgents);
}