using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time editor tool: clones the existing Rods & Charge InfoPanel (under
/// ContentOverLayCanvas in the open scene) into a new GaussianSurfaceInfoPanel -
/// same visual style and type scale (13-16px text, 44px-tall buttons, matching the
/// 320x693 reference-resolution design this canvas actually uses), kicker/title
/// kept, description repurposed as the live flux readout, plus a tile-inspection
/// readout and a single full-width "cross-section view" toggle button - no position
/// buttons, no tiles-visibility toggle, no resolution buttons; View is the only
/// interactive control this content needs. Wires the panel into
/// ARContentManager.uiPanels so it shows/hides it based on tracking state, same as
/// the other two content panels. This is wired on ARContentManager (a scene
/// object) rather than on gauss_law_marker's MarkerContentEntry (an asset),
/// because a ScriptableObject asset can't hold a persisting reference to a scene
/// object - Unity silently drops it back to null on save.
///
/// Re-running this always strips every auxiliary element it knows about before
/// rebuilding, whether the panel already existed or was just cloned, so repeated
/// runs can't leave stale buttons behind from an earlier version of this tool.
/// </summary>
public static class BuildGaussianSurfaceOverlayUI
{
    const string ReferenceImageName = "gauss_law_marker";

    static readonly Color BackgroundColor = new Color32(0xF3, 0xF2, 0xF2, 0xFF);
    static readonly Color InkColor = new Color32(0x20, 0x1E, 0x1D, 0xFF);
    static readonly Color AccentColor = new Color32(0xEC, 0x30, 0x13, 0xFF);

    // Kept broad on purpose: also cleans up rows from earlier versions of this tool
    // (resolution buttons, tiles toggle) that no longer get created, so a panel
    // built by an older run still self-heals to the current design on re-run.
    static readonly string[] AuxiliaryChildNames =
    {
        "PositionButtonRow", "TileInfoText", "ResolutionRow", "TogglesRow",
        "TilesToggleButton", "CrossSectionToggleButton", "CrossSectionRow",
    };

    [MenuItem("AR Tools/Build Gaussian Surface Overlay UI")]
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

        var existing = canvasGO.transform.Find("GaussianSurfaceInfoPanel");
        GameObject panel;
        if (existing != null)
        {
            panel = existing.gameObject;
        }
        else
        {
            panel = Object.Instantiate(infoPanel.gameObject, canvasGO.transform);
            panel.name = "GaussianSurfaceInfoPanel";
        }

        // Always normalize to a clean baseline, regardless of what an older version
        // of this tool (or a fresh clone of InfoPanel) left behind.
        foreach (var name in AuxiliaryChildNames)
        {
            var child = panel.transform.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
        var oldBridge = panel.GetComponent<GaussianSurfaceUIBridge>();
        if (oldBridge != null) Object.DestroyImmediate(oldBridge);

        SetText(panel.transform, "KickerText", "ELECTROMAGNETIC THEORY");
        SetText(panel.transform, "TitleText", "Gaussian Surfaces");
        SetText(panel.transform, "DescriptionText", "Numerical flux: —\nTheoretical Q/ε0: —");
        var fluxText = panel.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (fluxText != null) fluxText.color = AccentColor;

        var tileInfoText = CreateLabel(panel.transform, "TileInfoText", "Tap a tile to inspect it.", 13, FontStyles.Normal, 0.75f);

        var bridge = panel.AddComponent<GaussianSurfaceUIBridge>();
        bridge.fluxText = fluxText;
        bridge.tileInfoText = tileInfoText;

        // Single full-width action button, matching the size/weight of the existing
        // PositionButtonRow buttons (44px tall, 16px label) rather than the oversized
        // scale used earlier - this canvas runs at a 320x693 reference resolution.
        var crossSectionRow = CreateUIObject("CrossSectionRow", panel.transform);
        crossSectionRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
        var crossSectionLayout = crossSectionRow.AddComponent<HorizontalLayoutGroup>();
        crossSectionLayout.childControlWidth = true;
        crossSectionLayout.childControlHeight = true;
        crossSectionLayout.childForceExpandWidth = true;
        crossSectionLayout.childForceExpandHeight = true;

        var crossSectionToggle = CreateBorderedButton(crossSectionRow.transform, "CrossSectionToggleButton", "View: Full", bridge.ToggleCrossSection);
        bridge.crossSectionToggleLabel = crossSectionToggle.GetComponentInChildren<TextMeshProUGUI>();

        panel.SetActive(false);

        WireUIPanel(ReferenceImageName, panel);

        Debug.Log("GaussianSurfaceInfoPanel built and wired to ARContentManager for gauss_law_marker. Starts hidden, shown while that marker is tracked - scene saved.");
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

    static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style, float alpha)
    {
        var go = CreateUIObject(name, parent);
        go.AddComponent<LayoutElement>().preferredHeight = fontSize * 2.8f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(InkColor.r, InkColor.g, InkColor.b, alpha);
        return tmp;
    }

    static GameObject CreateBorderedButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction callback)
    {
        var go = CreateUIObject(name, parent);
        go.AddComponent<LayoutElement>();
        var border = go.AddComponent<Image>();
        border.color = InkColor;
        var button = go.AddComponent<Button>();

        var inner = CreateUIObject("Inner", go.transform);
        var innerRect = inner.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2);
        innerRect.offsetMax = new Vector2(-2, -2);
        var innerImage = inner.AddComponent<Image>();
        innerImage.color = BackgroundColor;
        button.targetGraphic = innerImage;

        var labelGO = CreateUIObject("Label", inner.transform);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = InkColor;
        tmp.alignment = TextAlignmentOptions.Center;

        UnityEventTools.AddPersistentListener(button.onClick, callback);
        return go;
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
