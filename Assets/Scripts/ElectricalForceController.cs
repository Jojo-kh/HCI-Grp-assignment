using UnityEngine;

/// <summary>
/// Coulomb-style force visualization for four point charges. One designated charge
/// (observedIndex into Charges) always shows the vector sum of the other three's
/// forces on it, plus that sum's components along the content's local X and Z axes
/// (Fx/Fy in the flat diagram plane). Each of the other three charges also gets its
/// own arrow, positioned at that charge, showing the reaction force it feels from
/// the observed one (Newton's third law - equal and opposite to the force the
/// observed charge feels from it): pointing at the observed charge when pulled
/// toward it (opposite signs), pointing away when pushed apart (same signs).
/// Arrows only - magnitudes are illustrative (1/r^2 falloff), not physically
/// calibrated, and nothing here does or displays an actual numeric calculation.
///
/// Arrows are billboarded: their length axis always points along their displayed
/// direction, but they're rotated around that axis every frame so their flat face
/// keeps facing the camera, regardless of how the AR device moves.
/// </summary>
[ExecuteAlways]
public class ElectricalForceController : MonoBehaviour
{
    [Header("Charges (exactly 4)")]
    public ElectricalCharge[] charges = new ElectricalCharge[4];

    [Header("Observed Charge")]
    [Tooltip("Index into Charges for the one charge whose net force (from the other three) is visualized.")]
    public int observedIndex = 0;
    public Transform forceArrow;
    public Renderer forceArrowRenderer;
    public Transform forceXArrow;
    public Transform forceYArrow;
    public Color netForceColor = Color.white;

    [Header("Other Charges' Arrows (reaction force each one feels)")]
    [Tooltip("One arrow per non-observed charge, in Charges order skipping observedIndex - positioned at that charge, pointing toward the observed one when pulled, away when pushed.")]
    public Transform[] sourceArrows = new Transform[3];
    public Renderer[] sourceArrowRenderers = new Renderer[3];
    [Tooltip("Color when that charge repels the observed one (same sign).")]
    public Color repelColor = Color.red;
    [Tooltip("Color when that charge attracts the observed one (opposite sign).")]
    public Color attractColor = Color.blue;

    [Header("Tuning")]
    [Tooltip("Raw 1/r^2 magnitude gets multiplied by this before becoming an arrow scale - tune visually, not physically meaningful.")]
    public float arrowScaleMultiplier = 0.025f;
    public float minArrowScale = 1.2f;
    public float maxArrowScale = 3.5f;

    MaterialPropertyBlock propBlock;

    void Update()
    {
        UpdateForces();
    }

    void UpdateForces()
    {
        if (observedIndex < 0 || observedIndex >= charges.Length) return;
        var target = charges[observedIndex];
        if (target == null) return;

        Vector3 origin = target.transform.position;
        Vector3 net = Vector3.zero;
        int sourceSlot = 0;

        for (int j = 0; j < charges.Length; j++)
        {
            if (j == observedIndex || charges[j] == null) continue;
            var source = charges[j];

            Vector3 force = ForceOn(target, source);
            net += force;

            if (sourceSlot < sourceArrows.Length)
            {
                // Newton's third law: the force this source itself feels is equal and
                // opposite to the force it exerts on the observed charge - pulled toward
                // it when attracting, pushed away when repelling.
                Vector3 reactionOnSource = -force;
                Color color = source.isPositive == target.isPositive ? repelColor : attractColor;
                var renderer = sourceSlot < sourceArrowRenderers.Length ? sourceArrowRenderers[sourceSlot] : null;
                PlaceArrow(sourceArrows[sourceSlot], renderer, source.transform.position, reactionOnSource, color);
            }
            sourceSlot++;
        }

        PlaceArrow(forceArrow, forceArrowRenderer, origin, net, netForceColor);

        Vector3 xComponent = Vector3.Project(net, transform.right);
        Vector3 yComponent = Vector3.Project(net, transform.forward);
        PlaceArrow(forceXArrow, null, origin, xComponent, Color.red);
        PlaceArrow(forceYArrow, null, origin, yComponent, Color.green);
    }

    Vector3 ForceOn(ElectricalCharge target, ElectricalCharge source)
    {
        Vector3 toTarget = target.transform.position - source.transform.position;
        float r = toTarget.magnitude;
        if (r < 0.001f) return Vector3.zero;

        // Same sign repels (force points away from source); opposite sign attracts.
        float sign = target.isPositive == source.isPositive ? 1f : -1f;
        return toTarget.normalized * sign * (1f / (r * r));
    }

    void PlaceArrow(Transform arrow, Renderer arrowRenderer, Vector3 origin, Vector3 vector, Color color)
    {
        if (arrow == null) return;

        arrow.position = origin;
        float mag = vector.magnitude;
        if (mag > 0.0001f)
            arrow.rotation = BillboardRotation(vector.normalized, origin);

        float scale = Mathf.Clamp(mag * arrowScaleMultiplier, minArrowScale, maxArrowScale);
        arrow.localScale = new Vector3(arrow.localScale.x, scale, arrow.localScale.z);

        if (arrowRenderer != null)
        {
            propBlock ??= new MaterialPropertyBlock();
            arrowRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", color);
            arrowRenderer.SetPropertyBlock(propBlock);
        }
    }

    /// <summary>
    /// Rotation with local +Y locked exactly to lengthDir while local +Z (the
    /// arrow's flat face normal) is rotated around that axis to point as closely as
    /// possible at the camera, so the arrow always reads as a flat shape rather than
    /// edge-on, however the AR device turns.
    /// </summary>
    Quaternion BillboardRotation(Vector3 lengthDir, Vector3 origin)
    {
        var cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - origin).normalized : Vector3.back;

        Vector3 xAxis = Vector3.Cross(toCamera, lengthDir);
        if (xAxis.sqrMagnitude < 0.0001f) xAxis = Vector3.Cross(Vector3.up, lengthDir);
        if (xAxis.sqrMagnitude < 0.0001f) xAxis = Vector3.right;
        xAxis.Normalize();

        Vector3 zAxis = Vector3.Cross(xAxis, lengthDir).normalized;
        return Quaternion.LookRotation(zAxis, lengthDir);
    }
}
