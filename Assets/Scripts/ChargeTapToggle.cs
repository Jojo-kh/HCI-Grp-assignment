using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tap this charge to flip its sign. Requires a Collider on this object (present
/// on primitive spheres by default) and a PhysicsRaycaster on the AR camera so
/// EventSystem can hit-test 3D world objects, not just UI.
/// </summary>
[RequireComponent(typeof(ElectricalCharge))]
public class ChargeTapToggle : MonoBehaviour, IPointerClickHandler
{
    ElectricalCharge charge;

    void Awake() => charge = GetComponent<ElectricalCharge>();

    public void OnPointerClick(PointerEventData eventData) => charge.ToggleSign();
}
