using TMPro;
using UnityEngine;

/// <summary>
/// Attach this to your persistent Content Overlay Canvas (the one that stays
/// in the scene whether or not a Rods & Charge marker is currently tracked).
///
/// Wire your 3 screen-space UI buttons' OnClick to the methods here instead
/// of directly to RodsChargeController � the AR content (and its
/// RodsChargeController) is instantiated at runtime per marker, so there's
/// nothing to drag into the Inspector for it at edit time. This bridge finds
/// whichever instance is currently active via RodsChargeController.Current.
///
/// If no Rods & Charge marker is being tracked, these calls are safely
/// ignored (nothing happens) rather than throwing an error.
/// </summary>
public class RodsChargeUIBridge : MonoBehaviour
{
    const string BaseTitle = "Rods & Charge";

    [Tooltip("The Rods & Charge InfoPanel's TitleText - a scene object, wired here directly. Shows the live field relationship (Adds/Cancels/Partial) alongside the title, since the AR content itself is scaled far too small for any label on it to be legible.")]
    public TMP_Text titleText;

    void Update()
    {
        if (titleText == null) return;
        var current = RodsChargeController.Current;
        titleText.text = current != null && !string.IsNullOrEmpty(current.CurrentRelationship)
            ? $"{BaseTitle} - {current.CurrentRelationship}"
            : BaseTitle;
    }

    public void OnAboveButton()
    {
        
        RodsChargeController.Current?.OnAboveButton();
    }

    public void OnBetweenButton()
    {
        
        RodsChargeController.Current?.OnBetweenButton();
    }

    public void OnBelowButton()
    {
        
        RodsChargeController.Current?.OnBelowButton();
    }
}