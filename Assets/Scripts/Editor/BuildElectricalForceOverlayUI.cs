using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time editor tool: clones the existing Rods & Charge InfoPanel (under
/// ContentOverLayCanvas in the open scene) into a new ElectricalForceInfoPanel -
/// same visual style, but with the position-buttons row removed since Electrical
/// Force only needs the kicker/title/explanation text. Wires both panels into
/// their MarkerContentEntry's uiPanel field so ARContentManager shows/hides them
/// based on which marker is actually tracked (previously the Rods & Charge panel
/// was always visible regardless of tracking state - this also fixes that).
/// </summary>
public static class BuildElectricalForceOverlayUI
{
    const string RodsChargeMarkerPath = "Assets/Content/MarkerEntries/rods_charge_marker.asset";
    const string ElectricalForceMarkerPath = "Assets/Content/MarkerEntries/electrical_force_marker.asset";

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
            Debug.LogWarning("ElectricalForceInfoPanel already exists - reusing it instead of duplicating.");
            electricalPanel = existingElectrical.gameObject;
        }
        else
        {
            electricalPanel = Object.Instantiate(infoPanel.gameObject, canvasGO.transform);
            electricalPanel.name = "ElectricalForceInfoPanel";

            var buttonRow = electricalPanel.transform.Find("PositionButtons");
            if (buttonRow != null) Object.DestroyImmediate(buttonRow.gameObject);
        }

        SetText(electricalPanel.transform, "KickerText", "ELECTROMAGNETIC THEORY");
        SetText(electricalPanel.transform, "TitleText", "Electrical Force");
        SetText(electricalPanel.transform, "DescriptionText",
            "F = kq₁q₂ / r²\nCoulomb's law: force between two point charges, acting along the line joining them.");

        infoPanel.gameObject.SetActive(false);
        electricalPanel.SetActive(false);

        WireUIPanel(RodsChargeMarkerPath, infoPanel.gameObject);
        WireUIPanel(ElectricalForceMarkerPath, electricalPanel);

        Debug.Log("ElectricalForceInfoPanel built. Both content panels now start hidden and are shown/hidden by ARContentManager based on tracking - save the scene.");
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

    static void WireUIPanel(string markerEntryPath, GameObject panel)
    {
        var entry = AssetDatabase.LoadAssetAtPath<MarkerContentEntry>(markerEntryPath);
        if (entry == null)
        {
            Debug.LogError($"Could not find marker entry at {markerEntryPath}.");
            return;
        }

        entry.uiPanel = panel;
        EditorUtility.SetDirty(entry);
        AssetDatabase.SaveAssets();
    }
}
