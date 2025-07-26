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

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Make sure we’ve cached our agent
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        // Only draw if there’s actually a path
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            var corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
#endif
}
