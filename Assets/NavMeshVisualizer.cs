using UnityEngine;
using UnityEngine.AI;

[ExecuteInEditMode]
public class NavMeshVisualizer : MonoBehaviour
{
    [Tooltip("Color for the NavMesh wireframe")]
    public Color lineColor = Color.cyan;

    void OnDrawGizmos()
    {
        // 1) Grab the raw triangulation
        var navMeshData = NavMesh.CalculateTriangulation();
        var verts = navMeshData.vertices;
        var tris  = navMeshData.indices;

        // 2) Set your color
        Gizmos.color = lineColor;

        // 3) For each triangle… draw its three edges
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, a);
        }
    }
}
