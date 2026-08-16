using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// One-time editor tool: builds GaussianSurfaceContent.prefab (a translucent 1m cube
/// with a draggable point charge inside, six tap-to-inspect tile faces, and toggleable
/// grid-line overlays), wires it into the gauss_law_marker database entry, and makes
/// sure the AR camera has a PhysicsRaycaster so tapping/dragging in 3D space actually
/// registers. Re-running this replaces the existing prefab from scratch.
/// </summary>
public static class BuildGaussianSurfaceContent
{
    const string PrefabPath = "Assets/Content/MarkerEntries/GaussianSurfaceContent.prefab";
    const string MarkerEntryPath = "Assets/Content/MarkerEntries/gauss_law_marker.asset";
    const string FaceMatPath = "Assets/Material/GaussSurface_Mat.mat";
    const string GridMatPath = "Assets/Material/Arrow_Mat.mat";
    const string ChargeMatPath = "Assets/Material/TestCharge_Mat.mat";

    const float CubeSize = 1.0f;

    [MenuItem("AR Tools/Build Gaussian Surface Content")]
    public static void Build()
    {
        EnsurePhysicsRaycasterOnCamera();

        var faceMat = GetOrCreateFaceMaterial();
        var gridMat = AssetDatabase.LoadAssetAtPath<Material>(GridMatPath);
        var chargeMat = AssetDatabase.LoadAssetAtPath<Material>(ChargeMatPath);

        var root = new GameObject("GaussianSurfaceContent");
        var controller = root.AddComponent<GaussianSurfaceController>();
        var manipulator = root.AddComponent<ARContentManipulator>();
        manipulator.enableDrag = false; // one-finger drag repositions the charge, not the whole surface
        controller.cubeSize = CubeSize;

        var chargeGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chargeGO.name = "Charge";
        chargeGO.transform.SetParent(root.transform, false);
        chargeGO.transform.localScale = Vector3.one * 0.06f;
        if (chargeMat != null) chargeGO.GetComponent<MeshRenderer>().sharedMaterial = chargeMat;
        var chargeDrag = chargeGO.AddComponent<GaussianChargeDrag>();
        chargeDrag.boundsHalfExtent = CubeSize * 0.9f;
        controller.charge = chargeGO.transform;

        float half = CubeSize / 2f;
        var faceNormals = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        var upHints = new[] { Vector3.up, Vector3.up, Vector3.forward, Vector3.forward, Vector3.up, Vector3.up };

        var faces = new GaussianFaceTile[6];
        var overlays = new TileGridMeshBuilder[6];

        for (int f = 0; f < 6; f++)
        {
            Vector3 normal = faceNormals[f];
            Quaternion rot = Quaternion.LookRotation(normal, upHints[f]);
            Vector3 axisU = rot * Vector3.right;
            Vector3 axisV = rot * Vector3.up;
            Vector3 origin = normal * half;

            var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            faceGO.name = $"Face_{f}";
            faceGO.transform.SetParent(root.transform, false);
            faceGO.transform.localPosition = origin;
            faceGO.transform.localRotation = rot;
            faceGO.transform.localScale = Vector3.one * CubeSize;
            var faceRenderer = faceGO.GetComponent<MeshRenderer>();
            if (faceMat != null) faceRenderer.sharedMaterial = faceMat;

            var faceTile = faceGO.AddComponent<GaussianFaceTile>();
            faceTile.controller = controller;
            faceTile.localNormal = normal;
            faceTile.localAxisU = axisU;
            faceTile.localAxisV = axisV;
            faceTile.localOrigin = origin;
            faceTile.faceRenderer = faceRenderer;
            // The +X, +Y, +Z faces (indices 0, 2, 4) open up in cross-section view,
            // revealing the interior from that corner; the other three stay and tint red.
            faceTile.hiddenInCrossSection = f == 0 || f == 2 || f == 4;
            faces[f] = faceTile;

            var overlayGO = new GameObject($"Grid_{f}");
            overlayGO.transform.SetParent(root.transform, false);
            overlayGO.AddComponent<MeshFilter>();
            var overlayRenderer = overlayGO.AddComponent<MeshRenderer>();
            if (gridMat != null) overlayRenderer.sharedMaterial = gridMat;
            var gridBuilder = overlayGO.AddComponent<TileGridMeshBuilder>();
            gridBuilder.localAxisU = axisU;
            gridBuilder.localAxisV = axisV;
            gridBuilder.localOrigin = origin + normal * 0.003f;
            gridBuilder.Build(4, CubeSize);
            overlays[f] = gridBuilder;
        }

        controller.faces = faces;
        controller.gridOverlays = overlays;

        var arrowMat = AssetDatabase.LoadAssetAtPath<Material>(GridMatPath);
        var fieldArrowDirs = FieldArrowDirections();
        var fieldArrows = new Transform[fieldArrowDirs.Length];
        var fieldArrowRenderers = new Renderer[fieldArrowDirs.Length];
        for (int i = 0; i < fieldArrowDirs.Length; i++)
        {
            var (arrowT, arrowR) = CreateArrow(root.transform, $"FieldArrow{i}", arrowMat, 0.12f);
            fieldArrows[i] = arrowT;
            fieldArrowRenderers[i] = arrowR;
        }
        controller.fieldArrowDirections = fieldArrowDirs;
        controller.fieldArrows = fieldArrows;
        controller.fieldArrowRenderers = fieldArrowRenderers;

        var highlightGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        highlightGO.name = "SelectedTileHighlight";
        Object.DestroyImmediate(highlightGO.GetComponent<Collider>());
        highlightGO.transform.SetParent(root.transform, false);
        if (chargeMat != null) highlightGO.GetComponent<MeshRenderer>().sharedMaterial = chargeMat;
        highlightGO.SetActive(false);
        controller.selectedTileHighlight = highlightGO.transform;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        WireMarkerEntry();

        Debug.Log("GaussianSurfaceContent.prefab built and wired to gauss_law_marker. Drag the charge to move it, tap a face to inspect a tile.");
    }

    /// <summary>
    /// Six face directions, eight corner diagonals, and twelve edge midpoints - the
    /// full 26-direction "Moore neighborhood" of a cube, for a denser, evenly-spread
    /// radiating pattern.
    /// </summary>
    static Vector3[] FieldArrowDirections()
    {
        var dirs = new System.Collections.Generic.List<Vector3>();
        var steps = new[] { -1f, 0f, 1f };
        foreach (var sx in steps)
        foreach (var sy in steps)
        foreach (var sz in steps)
        {
            if (sx == 0f && sy == 0f && sz == 0f) continue;
            dirs.Add(new Vector3(sx, sy, sz).normalized);
        }
        return dirs.ToArray();
    }

    static (Transform, Renderer) CreateArrow(Transform parent, string name, Material mat, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        if (mat != null) renderer.sharedMaterial = mat;

        var builder = go.AddComponent<ArrowMeshBuilder>();
        builder.size = size;
        builder.thicknessRatio = 0.15f;
        builder.Build();

        return (go.transform, renderer);
    }

    static Material GetOrCreateFaceMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(FaceMatPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader) { name = "GaussSurface_Mat" };
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0.3f, 0.6f, 1f, 0.18f));

        AssetDatabase.CreateAsset(mat, FaceMatPath);
        return mat;
    }

    static void EnsurePhysicsRaycasterOnCamera()
    {
        var cameraGO = GameObject.FindGameObjectWithTag("MainCamera");
        if (cameraGO == null)
        {
            Debug.LogWarning("No MainCamera-tagged object found in the open scene. Add a PhysicsRaycaster to the AR camera manually so tapping/dragging works: select it, Add Component > Physics Raycaster.");
            return;
        }

        if (cameraGO.GetComponent<PhysicsRaycaster>() == null)
            cameraGO.AddComponent<PhysicsRaycaster>();
    }

    static void WireMarkerEntry()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var entry = AssetDatabase.LoadAssetAtPath<MarkerContentEntry>(MarkerEntryPath);
        if (prefab == null || entry == null)
        {
            Debug.LogError("Could not find GaussianSurfaceContent.prefab or gauss_law_marker.asset.");
            return;
        }

        entry.contentPrefab = prefab;
        entry.subjectTitle = "Gaussian Surfaces";
        entry.explanationText = "Drag the charge, tap a tile to inspect it. The numerical flux should track Q/ε₀ while the charge stays inside the cube.";
        EditorUtility.SetDirty(entry);
        AssetDatabase.SaveAssets();
    }
}
