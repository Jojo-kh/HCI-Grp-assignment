using UnityEngine;
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
    [Tooltip("Multiplier on how far the content moves per screen-space drag distance.")]
    public float dragSpeed = 1f;

    [Header("Pinch Zoom")]
    [Tooltip("Smallest allowed scale, as a multiple of the content's starting scale.")]
    public float minScale = 0.3f;
    [Tooltip("Largest allowed scale, as a multiple of the content's starting scale.")]
    public float maxScale = 3f;

    Vector3 startScale;
    Plane dragPlane;
    float pinchStartDistance;
    Vector3 pinchStartScale;
    int previousTouchCount;

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

        if (count == 1)
        {
            if (previousTouchCount != 1)
                dragPlane = new Plane(-Camera.main.transform.forward, transform.position);
            else if (touches[0].phase == TouchPhase.Moved)
                Drag(touches[0]);
        }
        else if (count >= 2)
        {
            if (previousTouchCount < 2)
            {
                pinchStartDistance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                pinchStartScale = transform.localScale;
            }
            else
            {
                Pinch(touches[0], touches[1]);
            }
        }

        previousTouchCount = count;
    }

    void Drag(Touch touch)
    {
        Ray prevRay = Camera.main.ScreenPointToRay(touch.screenPosition - touch.delta);
        Ray currRay = Camera.main.ScreenPointToRay(touch.screenPosition);

        if (dragPlane.Raycast(prevRay, out float prevDist) && dragPlane.Raycast(currRay, out float currDist))
            transform.position += (currRay.GetPoint(currDist) - prevRay.GetPoint(prevDist)) * dragSpeed;
    }

    void Pinch(Touch a, Touch b)
    {
        if (pinchStartDistance < 0.01f) return;

        float distance = Vector2.Distance(a.screenPosition, b.screenPosition);
        float ratio = distance / pinchStartDistance;

        float baseRatio = pinchStartScale.x / startScale.x;
        float clampedRatio = Mathf.Clamp(baseRatio * ratio, minScale, maxScale);
        transform.localScale = startScale * clampedRatio;
    }
}
