using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One-time editor tool: builds ElectricalForceContent.prefab (4 charge spheres at
/// the corners of a square, each tappable to flip sign; one of them always shows
/// net F / Fx / Fy from the other three, and each of those other three shows its
/// own arrow aimed back at the observed charge), wires it into the
/// electrical_force_marker database entry, and makes sure the AR camera has a
/// PhysicsRaycaster so tapping a charge in 3D space actually registers. Re-running
/// this replaces the existing prefab from scratch - safe to use to pick up script
/// changes, but any manual per-instance tweaks in a dragged-in scene copy won't
/// carry over (edit the prefab asset itself for changes that should stick).
/// </summary>
public static class BuildElectricalForceContent
{
    const string PrefabPath = "Assets/Content/MarkerEntries/ElectricalForceContent.prefab";
    const string MarkerEntryPath = "Assets/Content/MarkerEntries/electrical_force_marker.asset";
    const string ChargeMatPath = "Assets/Material/TestCharge_Mat.mat";
    const string ArrowMatPath = "Assets/Material/Arrow_Mat.mat";

    const float HalfSquare = 0.08f;

    [MenuItem("AR Tools/Build Electrical Force Content")]
    public static void Build()
    {
        EnsurePhysicsRaycasterOnCamera();

        var chargeMat = AssetDatabase.LoadAssetAtPath<Material>(ChargeMatPath);
        var arrowMat = AssetDatabase.LoadAssetAtPath<Material>(ArrowMatPath);

        var root = new GameObject("ElectricalForceContent");
        var controller = root.AddComponent<ElectricalForceController>();
        root.AddComponent<ARContentManipulator>();

        // Four corners of a square, index 0 is the one that also gets the Fx/Fy breakdown.
        var positions = new[]
        {
            new Vector3(-HalfSquare, 0f, HalfSquare),
            new Vector3(HalfSquare, 0f, HalfSquare),
            new Vector3(HalfSquare, 0f, -HalfSquare),
            new Vector3(-HalfSquare, 0f, -HalfSquare),
        };
        var names = new[] { "ObservedCharge", "SourceCharge1", "SourceCharge2", "SourceCharge3" };

        var charges = new ElectricalCharge[4];
        for (int i = 0; i < 4; i++)
            charges[i] = CreateCharge(root.transform, names[i], positions[i], chargeMat);

        var (fArrow, fRenderer) = CreateArrow(root.transform, "ForceArrow", arrowMat, 0.05f);
        var (fxArrow, _) = CreateArrow(root.transform, "ForceXArrow", arrowMat, 0.035f);
        var (fyArrow, _) = CreateArrow(root.transform, "ForceYArrow", arrowMat, 0.035f);

        var sourceArrows = new Transform[3];
        var sourceArrowRenderers = new Renderer[3];
        for (int i = 0; i < 3; i++)
        {
            var (arrowT, arrowR) = CreateArrow(root.transform, $"SourceArrow{i + 1}", arrowMat, 0.04f);
            sourceArrows[i] = arrowT;
            sourceArrowRenderers[i] = arrowR;
        }

        controller.charges = charges;
        controller.observedIndex = 0;
        controller.forceArrow = fArrow;
        controller.forceArrowRenderer = fRenderer;
        controller.forceXArrow = fxArrow;
        controller.forceYArrow = fyArrow;
        controller.sourceArrows = sourceArrows;
        controller.sourceArrowRenderers = sourceArrowRenderers;
        controller.arrowScaleMultiplier = 0.025f;
        controller.minArrowScale = 1.2f;
        controller.maxArrowScale = 3.5f;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        WireMarkerEntry();

        Debug.Log("ElectricalForceContent.prefab built and wired to electrical_force_marker. Tap a charge sphere in Play Mode to toggle its sign.");
    }

    static ElectricalCharge CreateCharge(Transform parent, string name, Vector3 localPos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * 0.03f;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var charge = go.AddComponent<ElectricalCharge>();
        go.AddComponent<ChargeTapToggle>();
        return charge;
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

    static void EnsurePhysicsRaycasterOnCamera()
    {
        var cameraGO = GameObject.FindGameObjectWithTag("MainCamera");
        if (cameraGO == null)
        {
            Debug.LogWarning("No MainCamera-tagged object found in the open scene. Add a PhysicsRaycaster to the AR camera manually so tapping charges works: select it, Add Component > Physics Raycaster.");
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
            Debug.LogError("Could not find ElectricalForceContent.prefab or electrical_force_marker.asset.");
            return;
        }

        entry.contentPrefab = prefab;
        entry.subjectTitle = "Electrical Force";
        entry.explanationText = "Tap a charge to flip its sign. The center charge always shows the net pull/push from the other three.";
        EditorUtility.SetDirty(entry);
        AssetDatabase.SaveAssets();
    }
}
