using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BasicAIAgent : MonoBehaviour
{
    [Header("AI Behavior")]
    [SerializeField] private AIAgentType agentType = AIAgentType.Zombie;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Pathfinding")]
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private bool showPath = true;
    [SerializeField] private Color pathColor = Color.yellow;

    [Header("Visual")]
    [SerializeField] private Color agentColor = Color.red;

    // References
    private Transform player;
    private NavMeshAgent navMeshAgent;

    // AI State
    private AIState currentState = AIState.Patrol;
    private Vector3 patrolTarget;
    private float lastPathUpdate;

    // Performance tracking
    private float lastPathfindingTime;

    public enum AIAgentType
    {
        Zombie,        // Simple aggressive AI
        TacticalEnemy, // Advanced AI with tactical behavior
        Scout          // Fast moving scout
    }

    public enum AIState
    {
        Patrol,    // Moving to patrol points
        Chase,     // Actively pursuing target
        Attack     // In combat range
    }

    void Start()
    {
        SetupAgent();
        FindReferences();
        SetRandomPatrolTarget();
        lastPathUpdate = -pathUpdateInterval; // Force immediate path calculation
    }

    void SetupAgent()
    {
        // Set agent tag
        gameObject.tag = "Enemy";

        // Set visual appearance
        Renderer renderer = GetComponent<Renderer>();
        if (renderer)
        {
            renderer.material.color = agentColor;
        }

        // Add NavMeshAgent if not present
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (!navMeshAgent)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        // Configure NavMeshAgent
        navMeshAgent.speed = moveSpeed;
        navMeshAgent.angularSpeed = rotationSpeed;
        navMeshAgent.acceleration = 8f;
        navMeshAgent.stoppingDistance = 0.5f;
        navMeshAgent.radius = 0.3f;
        navMeshAgent.height = 1.8f;
    }

    void FindReferences()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"No player found for agent {name}! Make sure player has 'Player' tag.");
        }
    }

    void Update()
    {
        UpdateAIBehavior();

        if (showPath && navMeshAgent && navMeshAgent.hasPath)
        {
            DrawPath();
        }
    }

    void UpdateAIBehavior()
    {
        switch (agentType)
        {
            case AIAgentType.Zombie:
                UpdateZombieBehavior();
                break;
            case AIAgentType.TacticalEnemy:
                UpdateTacticalEnemyBehavior();
                break;
            case AIAgentType.Scout:
                UpdateScoutBehavior();
                break;
        }

        // Update path if needed
        if (Time.time - lastPathUpdate > pathUpdateInterval)
        {
            UpdatePath();
            lastPathUpdate = Time.time;
        }
    }

    void UpdateZombieBehavior()
    {
        // Simple zombie AI - chase player aggressively
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= detectionRange)
            {
                if (distanceToPlayer <= attackRange)
                {
                    currentState = AIState.Attack;
                    // Stop moving and attack
                    if (navMeshAgent) navMeshAgent.isStopped = true;
                }
                else
                {
                    currentState = AIState.Chase;
                    if (navMeshAgent) navMeshAgent.isStopped = false;
                }
            }
            else if (currentState == AIState.Chase && distanceToPlayer > detectionRange * 1.5f)
            {
                // Lost player, return to patrol
                currentState = AIState.Patrol;
                SetRandomPatrolTarget();
                if (navMeshAgent) navMeshAgent.isStopped = false;
            }
        }

        if (currentState == AIState.Patrol && ReachedTarget())
        {
            SetRandomPatrolTarget();
        }
    }

    void UpdateTacticalEnemyBehavior()
    {
        // More advanced tactical behavior (simplified for now)
        UpdateZombieBehavior(); // Use zombie behavior as base

        // Could add tactical considerations here:
        // - Cover seeking
        // - Flanking maneuvers  
        // - Coordinated attacks
    }

    void UpdateScoutBehavior()
    {
        // Scout behavior - faster movement, maintain distance
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= detectionRange)
            {
                if (distanceToPlayer < detectionRange * 0.7f)
                {
                    // Too close, maintain distance
                    currentState = AIState.Patrol;
                    FindRetreatPosition();
                    if (navMeshAgent) navMeshAgent.isStopped = false;
                }
                else
                {
                    // Good distance, chase
                    currentState = AIState.Chase;
                    if (navMeshAgent) navMeshAgent.isStopped = false;
                }
            }
            else
            {
                currentState = AIState.Patrol;
                if (ReachedTarget())
                {
                    SetRandomPatrolTarget();
                }
                if (navMeshAgent) navMeshAgent.isStopped = false;
            }
        }
    }

    void UpdatePath()
    {
        if (!navMeshAgent) return;

        Vector3 targetPosition = GetCurrentTargetPosition();

        if (Vector3.Distance(navMeshAgent.destination, targetPosition) > 1f)
        {
            float startTime = Time.realtimeSinceStartup;

            // Use Unity's built-in NavMesh pathfinding for now
            navMeshAgent.SetDestination(targetPosition);

            // Track performance
            lastPathfindingTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        }
    }

    Vector3 GetCurrentTargetPosition()
    {
        switch (currentState)
        {
            case AIState.Chase:
            case AIState.Attack:
                return player ? player.position : transform.position;
            case AIState.Patrol:
            default:
                return patrolTarget;
        }
    }

    bool ReachedTarget()
    {
        if (!navMeshAgent) return true;

        return !navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f;
    }

    void SetRandomPatrolTarget()
    {
        // Find a random position within patrol range
        Vector3 randomDirection = Random.insideUnitSphere * 8f;
        randomDirection.y = 0;
        Vector3 newTarget = transform.position + randomDirection;

        // Sample the NavMesh to find a valid position
        if (NavMesh.SamplePosition(newTarget, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
        }
        else
        {
            patrolTarget = transform.position; // Stay in place if no valid position found
        }
    }

    void FindRetreatPosition()
    {
        if (player == null) return;

        // Move away from player
        Vector3 retreatDirection = (transform.position - player.position).normalized;
        Vector3 retreatTarget = transform.position + retreatDirection * 5f;

        // Sample the NavMesh to find a valid retreat position
        if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
        }
        else
        {
            patrolTarget = transform.position;
        }
    }

    void DrawPath()
    {
        if (!navMeshAgent || !navMeshAgent.hasPath) return;

        Vector3[] pathCorners = navMeshAgent.path.corners;
        for (int i = 0; i < pathCorners.Length - 1; i++)
        {
            Vector3 start = pathCorners[i] + Vector3.up * 0.2f;
            Vector3 end = pathCorners[i + 1] + Vector3.up * 0.2f;
            Debug.DrawLine(start, end, pathColor, 0.1f);
        }
    }

    // Public methods for performance monitoring
    public float GetLastPathfindingTime()
    {
        return lastPathfindingTime;
    }

    public AIState GetCurrentState()
    {
        return currentState;
    }

    public void ForcePathRecalculation()
    {
        if (navMeshAgent)
        {
            navMeshAgent.ResetPath();
        }
        lastPathUpdate = -pathUpdateInterval;
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw current target
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(patrolTarget, 0.5f);
    }
}