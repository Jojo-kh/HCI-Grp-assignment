using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tap a rod to flip its current direction (Forward/Reverse) - same tap-to-toggle
/// concept as ChargeTapToggle on the electrical charges. Requires a Collider on
/// this rod (present by default on a Cylinder primitive) and a PhysicsRaycaster on
/// the AR camera so EventSystem can hit-test 3D world objects, not just UI.
///
/// Finding the controller via GetComponentInParent (rather than a direct
/// Inspector reference) means this keeps working no matter how the whole content
/// gets repositioned, rescaled, or reparented at runtime by ARContentManipulator -
/// it always resolves against whatever the current hierarchy actually is.
/// </summary>
public class RodTapToggle : MonoBehaviour, IPointerClickHandler
{
    public enum WhichRod { RodA, RodB }

    [Tooltip("Which of RodsChargeController's two rods this is.")]
    public WhichRod rod = WhichRod.RodA;

    RodsChargeController controller;

    void Awake() => controller = GetComponentInParent<RodsChargeController>();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null) return;
        if (rod == WhichRod.RodA) controller.ToggleRodADirection();
        else controller.ToggleRodBDirection();
    }
}
