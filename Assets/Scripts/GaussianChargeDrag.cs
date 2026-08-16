using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Single-finger touch-drag for the point charge, constrained to a bounded volume
/// around the Gaussian surface so it can be dragged fully outside the cube (to see
/// the flux trend toward zero) without flying off arbitrarily far into the scene.
/// Goes through EventSystem (IDragHandler) - the same input path as UI - so it
/// responds identically to touch and mouse, and doesn't fight with
/// ARContentManipulator, which has its drag disabled on this content specifically.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GaussianChargeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("How far from the content's local origin (in local units) the charge can be dragged, on each axis.")]
    public float boundsHalfExtent = 0.9f;

    Camera cam;
    Transform contentRoot;

    void Awake()
    {
        cam = Camera.main;
        contentRoot = transform.parent;
    }

    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (cam == null || contentRoot == null) return;

        Plane dragPlane = new Plane(-cam.transform.forward, transform.position);
        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (!dragPlane.Raycast(ray, out float dist)) return;

        Vector3 worldPoint = ray.GetPoint(dist);
        Vector3 local = contentRoot.InverseTransformPoint(worldPoint);
        local = Vector3.Max(Vector3.one * -boundsHalfExtent, Vector3.Min(Vector3.one * boundsHalfExtent, local));
        transform.position = contentRoot.TransformPoint(local);
    }
}
