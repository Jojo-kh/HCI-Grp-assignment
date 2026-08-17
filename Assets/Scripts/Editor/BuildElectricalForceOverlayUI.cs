using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-time editor tool: clones the existing Rods & Charge InfoPanel (under
/// ContentOverLayCanvas in the open scene) into a new ElectricalForceInfoPanel -
/// same visual style, but with the position-buttons row removed since Electrical
/// Force only needs the kicker/title/explanation text. Wires both panels into
/// ARContentManager.uiPanels so it shows/hides them based on which marker is
/// actually tracked (previously the Rods & Charge panel was always visible
/// regardless of tracking state - this also fixes that). This is wired on
/// ARContentManager (a scene object) rather than on each MarkerContentEntry (an
/// asset), because a ScriptableObject asset can't hold a persisting reference to
/// a scene object - Unity silently drops it back to null on save.
/// </summary>
public static class BuildElectricalForceOverlayUI
{
    const string RodsChargeImageName = "rods_charge_marker";
    const string ElectricalForceImageName = "electrical_force_marker";

    [MenuItem("AR Tools/Build Electrical Force Overlay UI")]
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

        var existingElectrical = canvasGO.transform.Find("ElectricalForceInfoPanel");
        GameObject electricalPanel;
        if (existingElectrical != null)
        {
            electricalPanel = existingElectrical.gameObject;
        }
        else
        {
            electricalPanel = Object.Instantiate(infoPanel.gameObject, canvasGO.transform);
            electricalPanel.name = "ElectricalForceInfoPanel";
        }

        // Always strip this, regardless of whether the panel already existed or was
        // just cloned - the row's real name is PositionButtonRow, not PositionButtons.
        var buttonRow = electricalPanel.transform.Find("PositionButtonRow");
        if (buttonRow != null) Object.DestroyImmediate(buttonRow.gameObject);

        SetText(electricalPanel.transform, "KickerText", "ELECTROMAGNETIC THEORY");
        SetText(electricalPanel.transform, "TitleText", "Electrical Force");
        SetText(electricalPanel.transform, "DescriptionText",
            "F = kq₁q₂ / r²\nCoulomb's law: force between two point charges, acting along the line joining them.");

        infoPanel.gameObject.SetActive(false);
        electricalPanel.SetActive(false);

        WireUIPanel(RodsChargeImageName, infoPanel.gameObject);
        WireUIPanel(ElectricalForceImageName, electricalPanel);

        Debug.Log("ElectricalForceInfoPanel built. Both content panels now start hidden and are shown/hidden by ARContentManager based on tracking - scene saved.");
    }

    static void SetText(Transform root, string childName, string text)
    {
        var child = root.Find(childName);
        if (child == null)
        {
            Debug.LogWarning($"Could not find '{childName}' to set its text.");
            return;
        }

        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
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
