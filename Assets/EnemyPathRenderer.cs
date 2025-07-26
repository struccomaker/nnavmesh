using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[AddComponentMenu("Debug/Enemy Path Renderer")]
public class EnemyPathRenderer : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only draw paths for GameObjects tagged with this; blank = all NavMeshAgents")]
    public string agentTag = "Enemy";

    [Header("Line Settings")]
    [Tooltip("Unlit material so lines always show in Game view")]
    public Material pathMaterial;
    [Tooltip("Width of the path in world units")]
    public float lineWidth = 0.1f;
    [Tooltip("Vertical offset so lines hover above the ground")]
    public float heightOffset = 1f;

    // maps each agent to its LineRenderer
    private Dictionary<NavMeshAgent, LineRenderer> _agentLines = new();

    void Start()
    {
        // auto‐create a fallback unlit/red material if none assigned
        if (pathMaterial == null)
        {
            pathMaterial = new Material(Shader.Find("Unlit/Color"));
            pathMaterial.color = Color.red;
        }
    }

    void Update()
    {
        // 1) Gather the agents we care about
        NavMeshAgent[] agents;
        if (!string.IsNullOrEmpty(agentTag))
        {
            var gos = GameObject.FindGameObjectsWithTag(agentTag);
            var list = new List<NavMeshAgent>(gos.Length);
            foreach (var go in gos)
            {
                var a = go.GetComponent<NavMeshAgent>();
                if (a != null) list.Add(a);
            }
            agents = list.ToArray();
        }
        else
        {
            agents = FindObjectsOfType<NavMeshAgent>();
        }

        // 2) Clean up any destroyed agents
        var toRemove = new List<NavMeshAgent>();
        foreach (var kv in _agentLines)
        {
            if (kv.Key == null)
            {
                Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var dead in toRemove)
            _agentLines.Remove(dead);

        // 3) Ensure each agent with a path has a LineRenderer
        foreach (var agent in agents)
        {
            if (!agent.hasPath)
                continue;

            if (_agentLines.ContainsKey(agent))
                continue;

            // create a LineRenderer child
            var go = new GameObject($"Path_{agent.name}");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material       = pathMaterial;
            lr.startWidth     = lineWidth;
            lr.endWidth       = lineWidth;
            lr.positionCount  = 0;
            lr.useWorldSpace  = true;
            lr.alignment      = LineAlignment.View;
            lr.textureMode    = LineTextureMode.Tile;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;

            _agentLines[agent] = lr;
        }

        // 4) Update all existing LineRenderers with the latest path
        foreach (var kv in _agentLines)
        {
            var agent = kv.Key;
            var lr    = kv.Value;

            if (agent == null || !agent.hasPath)
            {
                lr.positionCount = 0;
                continue;
            }

            var corners = agent.path.corners;
            lr.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                lr.SetPosition(i, corners[i] + Vector3.up * heightOffset);
        }
    }

    void OnDisable()
    {
        // clean up all line renderers
        foreach (var kv in _agentLines)
            if (kv.Value) Destroy(kv.Value.gameObject);
        _agentLines.Clear();
    }
}
