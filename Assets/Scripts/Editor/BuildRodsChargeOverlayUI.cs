using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-time editor tool: wires the existing Rods & Charge InfoPanel (under
/// ContentOverLayCanvas in the open scene - the original hand-built panel, already
/// carrying its RodsChargeUIBridge and PositionButtonRow, that ElectricalForceInfoPanel
/// and GaussianSurfaceInfoPanel are cloned from) into ARContentManager.uiPanels, so
/// ARContentManager shows/hides it based on tracking state like the other two
/// content panels. This is wired on ARContentManager (a scene object) rather than
/// on rods_charge_marker's MarkerContentEntry (an asset), because a ScriptableObject
/// asset can't hold a persisting reference to a scene object - Unity silently drops
/// it back to null on save, which is why this never actually took effect before.
/// Doesn't touch the panel's layout or content - InfoPanel already has the right
/// look, so re-running this is safe any time the wiring goes missing.
/// </summary>
public static class BuildRodsChargeOverlayUI
{
    const string ReferenceImageName = "rods_charge_marker";

    [MenuItem("AR Tools/Build Rods Charge Overlay UI")]
    public static void Build()
    {
        var canvasGO = GameObject.Find("ContentOverLayCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("ContentOverLayCanvas not found in the open scene.");
            return;
        }

        var infoPanel = canvasGO.transform.Find("InfoPanel");
        if (infoPanel == null)
        {
            Debug.LogError("InfoPanel not found under ContentOverLayCanvas.");
            return;
        }

        infoPanel.gameObject.SetActive(false);

        WireUIPanel(ReferenceImageName, infoPanel.gameObject);

        Debug.Log("InfoPanel wired to ARContentManager for rods_charge_marker. Starts hidden, shown while that marker is tracked - scene saved.");
    }

    static void WireUIPanel(string referenceImageName, GameObject panel)
    {
        var contentManager = Object.FindFirstObjectByType<ARContentManager>();
        if (contentManager == null)
        {
            Debug.LogError("No ARContentManager found in the open scene - can't wire the UI panel.");
            return;
        }

        var panels = new List<ARContentManager.MarkerUIPanel>(contentManager.uiPanels ?? new ARContentManager.MarkerUIPanel[0]);
        int existingIndex = panels.FindIndex(p => p.referenceImageName == referenceImageName);
        var entry = new ARContentManager.MarkerUIPanel { referenceImageName = referenceImageName, panel = panel };
        if (existingIndex >= 0) panels[existingIndex] = entry;
        else panels.Add(entry);
        contentManager.uiPanels = panels.ToArray();

        EditorUtility.SetDirty(contentManager);
        EditorSceneManager.MarkSceneDirty(contentManager.gameObject.scene);
        EditorSceneManager.SaveScene(contentManager.gameObject.scene);
    }
}
