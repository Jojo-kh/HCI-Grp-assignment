using UnityEngine;

/// <summary>
/// Builds a single solid, flat, arrow-shaped mesh (rectangle shaft + triangular
/// head) at runtime and in the Editor. Attach directly to the object whose
/// MeshFilter/MeshRenderer should show the arrow � no child objects needed.
///
/// The arrow points along local +Y, with its base (pivot) at local (0,0,0).
/// Proportions are fixed internally; the single "Size" field scales the
/// whole arrow uniformly so there's only one control to worry about.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ArrowMeshBuilder : MonoBehaviour
{
    [Header("Size")]
    [Tooltip("Uniform scale for the whole arrow � the only shape control you need.")]
    public float size = 0.3f;
    [Tooltip("Leaves this much empty, invisible length (in the same units as Size) between the pivot at local (0,0,0) and where the visible shaft actually starts - so the object's transform can sit exactly at a surface (e.g. a charge) while the drawn arrow still starts a little further out, reading as a gap without moving the pivot itself. 0 = shaft starts right at the pivot, the original behavior.")]
    public float baseOffset = 0f;

    [Header("Troubleshooting")]
    [Tooltip("If the arrow looks invisible or 'inside out' from the front, tick this.")]
    public bool flipWinding = false;

    [Header("Advanced")]
    [Tooltip("How thick the arrow is, as a fraction of Size. Lower = flatter/sticker-like, higher = more 3D/chunky. Default gives a solid 3D look.")]
    public float thicknessRatio = 0.15f;

    // Fixed proportions (relative to `size`). Edit these constants directly
    // in the script if you ever want a different arrow silhouette � you
    // won't normally need to, since `size` handles everyday resizing.
    const float shaftLengthRatio = 0.6f;
    const float shaftWidthRatio = 0.15f;
    const float headLengthRatio = 0.4f;
    const float headWidthRatio = 0.4f;

    MeshFilter meshFilter;

    void Awake()
    {
        Build();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        }
        Build();
    }
#endif

    public void Build()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        float shaftLength = shaftLengthRatio * size;
        float shaftWidth = shaftWidthRatio * size;
        float headLength = headLengthRatio * size;
        float headWidth = headWidthRatio * size;
        float thickness = thicknessRatio * size;

        Vector2[] outline = new Vector2[]
        {
            new Vector2(-shaftWidth / 2f, baseOffset),
            new Vector2( shaftWidth / 2f, baseOffset),
            new Vector2( shaftWidth / 2f, baseOffset + shaftLength),
            new Vector2( headWidth  / 2f, baseOffset + shaftLength),
            new Vector2( 0f,              baseOffset + shaftLength + headLength),
            new Vector2(-headWidth  / 2f, baseOffset + shaftLength),
            new Vector2(-shaftWidth / 2f, baseOffset + shaftLength),
        };

        int[][] capTris = new int[][]
        {
            new int[]{0,1,2},
            new int[]{0,2,6},
            new int[]{2,3,4},
            new int[]{2,4,5},
            new int[]{2,5,6},
        };

        int n = outline.Length;
        float halfDepth = thickness / 2f;

        Vector3[] verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            verts[i] = new Vector3(outline[i].x, outline[i].y, halfDepth);
            verts[n + i] = new Vector3(outline[i].x, outline[i].y, -halfDepth);
        }

        var tris = new System.Collections.Generic.List<int>();

        foreach (var t in capTris)
            tris.AddRange(new[] { t[0], t[1], t[2] });

        foreach (var t in capTris)
            tris.AddRange(new[] { n + t[2], n + t[1], n + t[0] });

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int fA = i, fB = next, bA = n + i, bB = n + next;
            tris.AddRange(new[] { fA, fB, bB });
            tris.AddRange(new[] { fA, bB, bA });
        }

        int[] finalTris = tris.ToArray();
        if (flipWinding)
        {
            for (int i = 0; i < finalTris.Length; i += 3)
            {
                (finalTris[i + 1], finalTris[i + 2]) = (finalTris[i + 2], finalTris[i + 1]);
            }
        }

        Mesh mesh = new Mesh { name = "ArrowMesh" };
        mesh.vertices = verts;
        mesh.triangles = finalTris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }
}