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
/// IMPORTANT: this is a separate setting from RodsChargeController's
/// rodADirection/rodBDirection. Make sure the Direction you pick here
/// matches what you set there, or the arrows will point the wrong way
/// relative to the actual field calculation.
/// </summary>
public class RodDirectionArrows : MonoBehaviour
{
    public enum CurrentDirection { Forward, Reverse }

    [Header("Direction")]
    [Tooltip("Must match this rod's direction in RodsChargeController.")]
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
    [Tooltip("Absolute world-space size of each little arrow (not affected by the rod's own scale).")]
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
            Build();
        };
    }
#endif

    public void Build()
    {
        ClearGenerated();

        Vector3 localScale = transform.localScale;
        Vector3 counterScale = new Vector3(
            1f / Mathf.Max(Mathf.Abs(localScale.x), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(localScale.y), 0.0001f),
            1f / Mathf.Max(Mathf.Abs(localScale.z), 0.0001f));

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