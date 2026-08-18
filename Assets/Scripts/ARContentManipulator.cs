using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// The only touch manipulation allowed on AR content: one-finger drag repositions it,
/// two-finger pinch rescales it. No rotation or other gesture is exposed on purpose -
/// orientation should stay tied to the tracked marker.
/// </summary>
public class ARContentManipulator : MonoBehaviour
{
    [Header("Drag")]
    [Tooltip("Off for content where one-finger drag is used for something else instead (e.g. repositioning an object inside the content) - pinch zoom still works either way.")]
    public bool enableDrag = true;
    [Tooltip("Multiplier on how far the content moves per screen-space drag distance.")]
    public float dragSpeed = 1f;

    [Header("Pinch Zoom")]
    [Tooltip("Smallest allowed scale, as a multiple of the content's starting scale.")]
    public float minScale = 0.3f;
    [Tooltip("Largest allowed scale, as a multiple of the content's starting scale.")]
    public float maxScale = 3f;

    Vector3 startScale;
    Plane dragPlane;
    float previousPinchDistance;
    int previousTouchCount;
    bool activeTouchIsOnInteractable;

    // Absolute safety clamp on how far a single frame's drag can move the content -
    // the drag plane's raycast can become numerically unstable when the camera ends
    // up very close to (or nearly grazing) the plane, turning a tiny finger jitter
    // into a huge, jarring world-space jump ("clicking pulls me across the room").
    const float maxDragStepMeters = 0.5f;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        startScale = transform.localScale;
        previousTouchCount = 0;
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        var touches = Touch.activeTouches;
        int count = touches.Count;

        if (count == 1 && enableDrag)
        {
            if (previousTouchCount != 1)
            {
                // A touch that starts on a tappable object (e.g. a rod - see
                // RodTapToggle - or a charge - see ChargeTapToggle) is a tap meant
                // for that object, not a drag on the whole content sitting behind
                // it. EventSystem and this component both read the same raw touch
                // independently, so without this check tapping a rod to flip its
                // direction would also nudge the entire content underneath it.
                activeTouchIsOnInteractable = EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject(touches[0].touchId);
                dragPlane = new Plane(-Camera.main.transform.forward, transform.position);
            }
            else if (!activeTouchIsOnInteractable && touches[0].phase == TouchPhase.Moved)
                Drag(touches[0]);
        }
        else if (count >= 2)
        {
            float distance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
            if (previousTouchCount < 2)
            {
                // Record this frame's distance as the new baseline only - don't zoom
                // yet. Computing the ratio against this single first-contact reading
                // (as the old code did) was the bug: if the two fingers happened to
                // register a frame apart, or briefly overlapped on contact, that
                // reading could be tiny, and the very next real frame's distance
                // would produce a huge ratio - snapping straight to maxScale on a
                // single tap. Comparing frame-to-frame instead means a single bad
                // reading can only ever shift the scale by one frame's worth.
                previousPinchDistance = distance;
            }
            else if (previousPinchDistance > 1f)
            {
                float ratio = distance / previousPinchDistance;
                float newScaleFactor = Mathf.Clamp(transform.localScale.x / startScale.x * ratio, minScale, maxScale);
                transform.localScale = startScale * newScaleFactor;
            }
            previousPinchDistance = distance;
        }

        previousTouchCount = count;
    }

    void Drag(Touch touch)
    {
        Ray prevRay = Camera.main.ScreenPointToRay(touch.screenPosition - touch.delta);
        Ray currRay = Camera.main.ScreenPointToRay(touch.screenPosition);

        if (dragPlane.Raycast(prevRay, out float prevDist) && dragPlane.Raycast(currRay, out float currDist))
        {
            Vector3 worldDelta = (currRay.GetPoint(currDist) - prevRay.GetPoint(prevDist)) * dragSpeed;
            if (worldDelta.magnitude > maxDragStepMeters)
                worldDelta = worldDelta.normalized * maxDragStepMeters;
            transform.position += worldDelta;
        }
    }
}
