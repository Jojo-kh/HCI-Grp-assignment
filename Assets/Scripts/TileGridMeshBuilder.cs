using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a flat grid-line mesh across one face of the Gaussian surface cube, purely
/// for the "Show surface tiles" overlay - thin double-sided quads along each grid
/// line. Tap-to-select raycasting goes through the face's own collider
/// (GaussianFaceTile), not this mesh, so it doesn't need a collider itself.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TileGridMeshBuilder : MonoBehaviour
{
    [Tooltip("Local-space in-plane axes spanning the face; full length = one cube side.")]
    public Vector3 localAxisU = Vector3.right;
    public Vector3 localAxisV = Vector3.up;
    [Tooltip("Local-space center of the face, nudged slightly along its normal to avoid z-fighting with the face quad underneath.")]
    public Vector3 localOrigin;
    [Tooltip("Grid line thickness as a fraction of one tile's width.")]
    public float lineThicknessRatio = 0.05f;

    [SerializeField] int tilesPerSide = 4;
    [SerializeField] float faceSize = 1f;

    MeshFilter meshFilter;

    void Awake()
    {
        Build();
    }

    public void Build(int newTilesPerSide, float newFaceSize)
    {
        tilesPerSide = Mathf.Max(1, newTilesPerSide);
        faceSize = newFaceSize;
        Build();
    }

    public void Build()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        Vector3 axisU = localAxisU.normalized;
        Vector3 axisV = localAxisV.normalized;
        float half = faceSize / 2f;
        float lineWidth = (faceSize / tilesPerSide) * lineThicknessRatio;

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void AddQuad(Vector3 center, Vector3 uDir, Vector3 vDir, float uLen, float vLen)
        {
            int baseIndex = verts.Count;
            Vector3 hu = uDir * (uLen / 2f);
            Vector3 hv = vDir * (vLen / 2f);
            verts.Add(center - hu - hv);
            verts.Add(center + hu - hv);
            verts.Add(center + hu + hv);
            verts.Add(center - hu + hv);
            tris.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });
            tris.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1, baseIndex, baseIndex + 3, baseIndex + 2 });
        }

        for (int i = 0; i <= tilesPerSide; i++)
        {
            float u = Mathf.Lerp(-half, half, (float)i / tilesPerSide);
            AddQuad(localOrigin + axisU * u, axisV, axisU, faceSize, lineWidth);
        }
        for (int j = 0; j <= tilesPerSide; j++)
        {
            float v = Mathf.Lerp(-half, half, (float)j / tilesPerSide);
            AddQuad(localOrigin + axisV * v, axisU, axisV, faceSize, lineWidth);
        }

        var mesh = new Mesh { name = "TileGrid" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }
}
