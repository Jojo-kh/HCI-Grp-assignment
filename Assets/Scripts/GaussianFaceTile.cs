using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One face of the cubic Gaussian surface. Handles tap-to-select: converts the tap's
/// hit point into a tile index within this face's grid, then reports that tile's
/// center/normal/area to the controller. Also exposes its own face geometry so the
/// controller can sum flux across every tile on every face without a second,
/// duplicated copy of the face layout living elsewhere.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GaussianFaceTile : MonoBehaviour, IPointerClickHandler
{
    public GaussianSurfaceController controller;
    [Tooltip("Local-space (relative to the content root) outward normal of this face.")]
    public Vector3 localNormal;
    [Tooltip("Local-space in-plane axis spanning the face; full length = one cube side.")]
    public Vector3 localAxisU;
    public Vector3 localAxisV;
    [Tooltip("Local-space center of this face.")]
    public Vector3 localOrigin;

    [Tooltip("If true, this face is hidden (not just tinted) while cross-section view is active, opening up that side so the interior is visible.")]
    public bool hiddenInCrossSection;
    [Tooltip("This face's own renderer, so the controller can tint it red in cross-section view without affecting the other faces.")]
    public Renderer faceRenderer;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null) return;

        Vector3 hitLocal = controller.transform.InverseTransformPoint(eventData.pointerCurrentRaycast.worldPosition);
        Vector3 axisU = localAxisU.normalized;
        Vector3 axisV = localAxisV.normalized;
        Vector3 offset = hitLocal - localOrigin;

        float u = Vector3.Dot(offset, axisU) / controller.cubeSize;
        float v = Vector3.Dot(offset, axisV) / controller.cubeSize;

        int n = controller.TilesPerSide;
        int i = Mathf.Clamp(Mathf.FloorToInt((u + 0.5f) * n), 0, n - 1);
        int j = Mathf.Clamp(Mathf.FloorToInt((v + 0.5f) * n), 0, n - 1);

        float tileU = (i + 0.5f) / n - 0.5f;
        float tileV = (j + 0.5f) / n - 0.5f;
        Vector3 tileCenter = localOrigin + axisU * (tileU * controller.cubeSize) + axisV * (tileV * controller.cubeSize);
        float tileArea = (controller.cubeSize / n) * (controller.cubeSize / n);

        controller.SelectTile(tileCenter, localNormal.normalized, tileArea);
    }
}
