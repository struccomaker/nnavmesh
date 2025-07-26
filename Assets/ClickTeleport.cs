using UnityEngine;
using UnityEngine.AI;

public class ClickTeleport : MonoBehaviour
{
    [Tooltip("Your main camera (if blank, will use Camera.main)")]
    public Camera  cam;

    [Tooltip("Layer mask for your ground/plane")]
    public LayerMask groundMask;

    [Tooltip("If you have a NavMeshAgent, drag it here; otherwise leave null")]
    public NavMeshAgent agent;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();  // try to auto-find
    }

    void Update()
    {
        // on left mouse button down
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, groundMask))
            {
                Vector3 target = hit.point;
                
                if (agent != null)
                {
                    // snap the agent to the navmesh closest to your hit
                    if (NavMesh.SamplePosition(target, out var navHit, 1f, NavMesh.AllAreas))
                    {
                        agent.Warp(navHit.position);
                    }
                }
                else
                {
                    // no agent: just teleport your transform
                    transform.position = target;
                }
            }
        }
    }
}
