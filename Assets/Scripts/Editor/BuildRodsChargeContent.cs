using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time editor tool: builds RodsChargeContent.prefab (two charged rods with
/// independently toggleable current direction, small direction arrows wrapped
/// around each rod's surface, a circulation-curtain + 4-cardinal radial-line field
/// visualization per rod, a test charge that snaps between Above/Between/Below
/// presets, and a resultant-field arrow at the charge), and wires it into the
/// rods_charge_marker database entry. Both rods are built from the same helper
/// method so they always stay identical except for direction - re-running this
/// replaces the existing prefab from scratch, so it's safe to use to pick up script
/// changes or fix drift, but any manual per-instance tweaks in a dragged-in scene
/// copy won't carry over (edit the prefab asset itself for changes that should
/// stick, or re-run this tool after adjusting the constants below).
/// </summary>
public static class BuildRodsChargeContent
{
    const string PrefabPath = "Assets/Content/MarkerEntries/RodsChargeContent.prefab";
    const string MarkerEntryPath = "Assets/Content/MarkerEntries/rods_charge_marker.asset";
    const string ArrowMatPath = "Assets/Material/Arrow_Mat.mat";
    const string DirArrowMatPath = "Assets/Material/ArrowDirection_Mat.mat";
    const string ChargeMatPath = "Assets/Material/TestCharge_Mat.mat";

    const float RodLocalRadius = 0.5f; // default Cylinder primitive
    const float RodLocalLength = 2f;   // default Cylinder primitive
    static readonly Vector3 RodScale = new Vector3(0.86f, 8f, 0.86f);
    static readonly Quaternion RodRotation = Quaternion.Euler(0f, 0f, 90f);
    const float RodLocalXOffset = 0.15f;
    const float RodHalfSeparation = 2.755f;

    [MenuItem("AR Tools/Build Rods Charge Content")]
    public static void Build()
    {
        var arrowMat = AssetDatabase.LoadAssetAtPath<Material>(ArrowMatPath);
        var dirArrowMat = AssetDatabase.LoadAssetAtPath<Material>(DirArrowMatPath);
        var chargeMat = AssetDatabase.LoadAssetAtPath<Material>(ChargeMatPath);

        var root = new GameObject("RodsChargeContent");
        var controller = root.AddComponent<RodsChargeController>();
        root.AddComponent<ARContentManipulator>();

        // Rods intentionally keep Unity's plain default material - direction is
        // shown only by the yellow surface arrows (and the circulation/radial
        // arrows), not by tinting the rod body, which reads as too busy.
        var (rodA, rodARenderer) = CreateRod(root.transform, "RodA",
            new Vector3(RodLocalXOffset, 0f, -RodHalfSeparation),
            dirArrowMat, RodDirectionArrows.CurrentDirection.Forward);
        var (rodB, rodBRenderer) = CreateRod(root.transform, "RodB",
            new Vector3(RodLocalXOffset, 0f, RodHalfSeparation),
            dirArrowMat, RodDirectionArrows.CurrentDirection.Reverse);

        var posAbove = CreateMarker(root.transform, "PosAbove", new Vector3(0f, 0f, 6f));
        var posBetween = CreateMarker(root.transform, "PosInBetween", new Vector3(0f, 0f, 0.17f));
        var posBelow = CreateMarker(root.transform, "PosBelow", new Vector3(0f, 0f, -6f));

        var chargeGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chargeGO.name = "TestCharge";
        chargeGO.transform.SetParent(root.transform, false);
        chargeGO.transform.localPosition = new Vector3(0f, -0.05f, 0.17f);
        chargeGO.transform.localScale = Vector3.one * 0.5f;
        if (chargeMat != null) chargeGO.GetComponent<MeshRenderer>().sharedMaterial = chargeMat;

        var (resultantArrow, resultantRenderer) = CreateArrow(root.transform, "ResultantArrow", arrowMat, 0.3f);

        controller.rodA = rodA;
        controller.rodADirection = RodsChargeController.RodDirection.Forward;
        controller.rodARenderer = rodARenderer;
        controller.rodLocalRadius = RodLocalRadius;
        controller.rodLocalLength = RodLocalLength;

        controller.rodB = rodB;
        controller.rodBDirection = RodsChargeController.RodDirection.Reverse;
        controller.rodBRenderer = rodBRenderer;

        controller.testCharge = chargeGO.transform;
        controller.posAbove = posAbove;
        controller.posBetween = posBetween;
        controller.posBelow = posBelow;

        controller.resultantArrow = resultantArrow;
        controller.resultantArrowRenderer = resultantRenderer;

        controller.forwardColor = Color.blue;
        controller.reverseColor = Color.red;

        controller.circArrowMaterial = arrowMat;
        controller.radialArrowMaterial = arrowMat;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        WireMarkerEntry();

        Debug.Log("RodsChargeContent.prefab built and wired to rods_charge_marker. RodA/RodB share identical geometry and arrow tuning - only their direction differs.");
    }

    /// <summary>Builds one rod: cylinder mesh/collider + its surface direction arrows. Called once per rod so both always end up configured identically.</summary>
    static (Transform, Renderer) CreateRod(Transform parent, string name, Vector3 localPos, Material dirArrowMaterial, RodDirectionArrows.CurrentDirection dirArrowDirection)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = RodRotation;
        go.transform.localScale = RodScale;

        var renderer = go.GetComponent<MeshRenderer>();

        var dirArrows = go.AddComponent<RodDirectionArrows>();
        dirArrows.direction = dirArrowDirection;
        dirArrows.arrowsAlongLength = 7;
        dirArrows.arrowsAroundCircumference = 7;
        dirArrows.rodLocalLength = RodLocalLength;
        dirArrows.rodLocalRadius = RodLocalRadius;
        dirArrows.arrowSize = 0.15f;
        dirArrows.arrowThicknessRatio = 0.01f;
        dirArrows.arrowColor = new Color(0.867f, 0.831f, 0.235f);
        dirArrows.arrowMaterial = dirArrowMaterial;

        return (go.transform, renderer);
    }

    static Transform CreateMarker(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
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

    static void WireMarkerEntry()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var entry = AssetDatabase.LoadAssetAtPath<MarkerContentEntry>(MarkerEntryPath);
        if (prefab == null || entry == null)
        {
            Debug.LogError("Could not find RodsChargeContent.prefab or rods_charge_marker.asset.");
            return;
        }

        entry.contentPrefab = prefab;
        entry.subjectTitle = "Rods & Charge";
        entry.explanationText = "Move the test charge Above, Between, or Below the rods to see how their fields add up.";
        EditorUtility.SetDirty(entry);
        AssetDatabase.SaveAssets();
    }
}
