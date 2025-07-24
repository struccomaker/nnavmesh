using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyChase : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis   = false;
    }

    void Update()
    {
        // only call SetDestination if the agent is actually on the NavMesh
        if (target != null && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
        }
    }
}
