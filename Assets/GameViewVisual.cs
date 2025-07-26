using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]  // so it draws in Edit mode & Play mode
public class GameViewVisual : MonoBehaviour
{
    public Color meshColor = Color.cyan;
    public Color pathColor = Color.red;

    void OnDrawGizmos()
    {
        // Draw NavMesh triangles
        var nav = NavMesh.CalculateTriangulation();
        Gizmos.color = meshColor;
        for (int i = 0; i < nav.indices.Length; i += 3)
        {
            Vector3 a = nav.vertices[nav.indices[i + 0]];
            Vector3 b = nav.vertices[nav.indices[i + 1]];
            Vector3 c = nav.vertices[nav.indices[i + 2]];
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, a);
        }

        // Draw each agent’s computed path
        Gizmos.color = pathColor;
        foreach (var agent in FindObjectsOfType<NavMeshAgent>())
        {
            if (!agent.hasPath) continue;
            var corners = agent.path.corners;
            for (int j = 0; j < corners.Length - 1; j++)
                Gizmos.DrawLine(corners[j], corners[j + 1]);
        }
    }
}
