using UnityEngine;

/// <summary>
/// Attach this directly to RodA or RodB. It auto-generates small flat arrows
/// (reusing ArrowMeshBuilder) arranged in a grid around the rod's surface �
/// spaced along its length AND wrapped around its circumference � each
/// pointing in the current's direction.
///
/// This script assumes the rod is an unrotated-mesh Unity Cylinder primitive
/// (the default: radius 0.5, height 2, centered on its own local origin) �
/// i.e. Rod Radius / Rod Length below should normally stay at their Unity
/// primitive defaults regardless of how you've scaled or rotated the rod
/// itself, since child objects automatically inherit the rod's own scale.
/// Assumes the rod's local X and Z scale are equal (a normal round cylinder,
/// not squashed into an oval) � true for a default Cylinder primitive.
///
/// Each arrow's world size tracks the rod's actual radius (its X/Z scale) so a
/// thicker rod gets bigger surface arrows and a thinner one gets smaller ones,
/// while staying independent of the rod's length (Y scale) - stretching a rod
/// longer shouldn't blow up each individual arrow's size to match.
///
/// Direction is set for you: whenever a RodsChargeController is present on the
/// same object, its rodADirection/rodBDirection is the single source of truth and
/// overwrites the value below every Start()/OnValidate() (see
/// RodsChargeController.SyncRodDirectionArrows) - that keeps these surface arrows,
/// the circulation curtain, and the radial lines all pointing in agreement with
/// the right-hand rule relative to one shared direction, instead of three separate
/// settings that can drift out of sync. The field below only matters if this
/// component is used standalone, without a RodsChargeController driving it.
/// </summary>
public class RodDirectionArrows : MonoBehaviour
{
    public enum CurrentDirection { Forward, Reverse }

    [Header("Direction")]
    [Tooltip("Overwritten by RodsChargeController's rodA/rodBDirection when one is present on this object - only matters if this component is used standalone.")]
    public CurrentDirection direction = CurrentDirection.Forward;

    [Header("Layout")]
    [Tooltip("How many arrows to space along the rod's length.")]
    [Range(1, 12)] public int arrowsAlongLength = 4;
    [Tooltip("How many arrows to space around the rod's circumference (4 = every 90�, 8 = every 45�, etc).")]
    [Range(1, 16)] public int arrowsAroundCircumference = 4;
    [Tooltip("Length of the rod in its own LOCAL (unscaled) units. Leave at 2 for a default Unity Cylinder primitive.")]
    public float rodLocalLength = 2f;
    [Tooltip("Radius of the rod in its own LOCAL (unscaled) units. Leave at 0.5 for a default Unity Cylinder primitive.")]
    public float rodLocalRadius = 0.5f;

    [Header("Arrow Look")]
    [Tooltip("Size of each little arrow at rod radius scale 1 (i.e. a default 0.5-radius Cylinder) - scales up or down with the rod's actual radius from there.")]
    public float arrowSize = 0.05f;
    [Tooltip("Flat sticker-like thickness ratio for these arrows (much thinner than the main resultant arrow).")]
    public float arrowThicknessRatio = 0.02f;
    public Color arrowColor = Color.white;
    public Material arrowMaterial; // assign a simple Unlit or Lit material

    const string generatedPrefix = "DirArrow_";

    void Awake()
    {
        Build();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;

            // Build() reparents generated children via SetParent, which Unity disables for
            // objects living directly in a Prefab Asset on disk (outside Prefab Mode) to
            // prevent data corruption - triggering that error on every domain reload/recompile
            // for RodA/RodB even when RodsChargeContent.prefab wasn't touched. Skip in that
            // case; still rebuild for scene instances and when actually editing in Prefab Mode.
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject) &&
                UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null)
                return;

            Build();
        };
    }
#endif

    public void Build()
    {
        ClearGenerated();

        Vector3 localScale = transform.localScale;
        // Average of X/Z (the rod's radius scale) becomes the uniform final size on
        // all three axes below - cancels each axis's own scale, then reapplies this
        // radius-based scale evenly, so the arrow keeps its proportions and tracks
        // the rod's thickness instead of a fixed absolute size or its length stretch.
        float radiusScale = (Mathf.Abs(localScale.x) + Mathf.Abs(localScale.z)) / 2f;
        Vector3 counterScale = new Vector3(
            radiusScale / Mathf.Max(Mathf.Abs(localScale.x), 0.0001f),
            radiusScale / Mathf.Max(Mathf.Abs(localScale.y), 0.0001f),
            radiusScale / Mathf.Max(Mathf.Abs(localScale.z), 0.0001f));

        float sign = direction == CurrentDirection.Forward ? 1f : -1f;
        Vector3 pointingLocal = Vector3.up * sign;

        for (int i = 0; i < arrowsAlongLength; i++)
        {
            float t = (i + 0.5f) / arrowsAlongLength;
            float alongAxis = Mathf.Lerp(-rodLocalLength / 2f, rodLocalLength / 2f, t);

            for (int a = 0; a < arrowsAroundCircumference; a++)
            {
                float angle = a * (360f / arrowsAroundCircumference);
                Quaternion radialRot = Quaternion.Euler(0f, angle, 0f);
                Vector3 outwardDir = radialRot * Vector3.forward;
                Vector3 radialOffset = outwardDir * rodLocalRadius;

                GameObject arrowObj = new GameObject($"{generatedPrefix}{i}_{a}");
                // Editor-time preview only: DontSave keeps these out of the saved scene/prefab
                // (they're cheap to regenerate) instead of baking thousands of objects into the asset.
                if (!Application.isPlaying) arrowObj.hideFlags = HideFlags.DontSave;
                arrowObj.transform.SetParent(transform, false);
                arrowObj.transform.localPosition = new Vector3(0f, alongAxis, 0f) + radialOffset;
                arrowObj.transform.localRotation = Quaternion.LookRotation(outwardDir, pointingLocal);
                arrowObj.transform.localScale = counterScale;

                var meshFilter = arrowObj.AddComponent<MeshFilter>();
                var meshRenderer = arrowObj.AddComponent<MeshRenderer>();
                var builder = arrowObj.AddComponent<ArrowMeshBuilder>();
                builder.size = arrowSize;
                builder.thicknessRatio = arrowThicknessRatio;

                if (arrowMaterial != null)
                    meshRenderer.sharedMaterial = arrowMaterial;

                var propBlock = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", arrowColor);
                meshRenderer.SetPropertyBlock(propBlock);

                builder.Build();
            }
        }
    }

    void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(generatedPrefix))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}