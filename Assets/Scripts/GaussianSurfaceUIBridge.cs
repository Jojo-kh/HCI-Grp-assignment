using TMPro;
using UnityEngine;

/// <summary>
/// Attach to the persistent Content Overlay Canvas. Routes the resolution buttons
/// and show-tiles toggle to whichever GaussianSurfaceController instance is
/// currently active - content is instantiated at runtime per marker, so there's
/// nothing to drag into the Inspector for it at edit time, same reasoning as
/// RodsChargeUIBridge - and keeps the live flux/tile readouts updated every frame
/// while that content is tracked.
/// </summary>
public class GaussianSurfaceUIBridge : MonoBehaviour
{
    public TMP_Text fluxText;
    public TMP_Text tileInfoText;
    public TMP_Text tilesToggleLabel;
    public TMP_Text crossSectionToggleLabel;

    void Update()
    {
        var controller = GaussianSurfaceController.Current;
        if (controller == null) return;

        if (fluxText != null)
            fluxText.text = $"Numerical flux: {controller.TotalNumericalFlux:F2} N·m²/C\nTheoretical Q/ε₀: {controller.TheoreticalFlux:F2} N·m²/C";

        if (tileInfoText != null)
        {
            tileInfoText.text = controller.HasSelectedTile
                ? $"Tile field: {controller.SelectedTileFieldVector.magnitude:F2} N/C\nDirection: ({controller.SelectedTileFieldVector.normalized.x:F2}, {controller.SelectedTileFieldVector.normalized.y:F2}, {controller.SelectedTileFieldVector.normalized.z:F2})\nArea: {controller.SelectedTileArea:F3} m²"
                : "Tap a tile to inspect it.";
        }

        if (tilesToggleLabel != null)
            tilesToggleLabel.text = controller.TilesVisible ? "Tiles: On" : "Tiles: Off";

        if (crossSectionToggleLabel != null)
            crossSectionToggleLabel.text = controller.CrossSectionActive ? "View: Cross-Section" : "View: Full";
    }

    public void SetResolutionLow() => GaussianSurfaceController.Current?.SetResolution(GaussianSurfaceController.Resolution.Low);
    public void SetResolutionMedium() => GaussianSurfaceController.Current?.SetResolution(GaussianSurfaceController.Resolution.Medium);
    public void SetResolutionHigh() => GaussianSurfaceController.Current?.SetResolution(GaussianSurfaceController.Resolution.High);
    public void ToggleTiles() => GaussianSurfaceController.Current?.ToggleTilesVisible();
    public void ToggleCrossSection() => GaussianSurfaceController.Current?.ToggleCrossSection();
}
