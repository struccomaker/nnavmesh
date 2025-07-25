using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class enemyScript : MonoBehaviour
{
    [Tooltip("Who to chase")]
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
        if (target != null && agent.isOnNavMesh)
            agent.SetDestination(target.position);
    }
}
