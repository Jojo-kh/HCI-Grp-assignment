using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time editor tool: shrinks the three marker InfoPanels (InfoPanel,
/// ElectricalForceInfoPanel, GaussianSurfaceInfoPanel) under ContentOverLayCanvas
/// to 25% of their current size, pivoted so they shrink toward the bottom-left
/// corner instead of the center - so they stop spanning the full screen and
/// leave most of the AR view visible. Background alpha is also lowered so the
/// panel reads as semi-transparent. Also switches Rods & Charge's
/// PositionButtonRow (Above/Between/Below) from a horizontal row to a vertical
/// stack of narrower, taller, centered buttons, since three buttons no longer
/// fit side by side at the new width and a slimmer stack reads more deliberate.
/// Safe to re-run - it always applies the same target values rather than
/// compounding on top of a previous run.
/// </summary>
public static class ResizeContentPanels
{
    const float PanelScale = 0.5f;
    const float PanelBgAlpha = 0.6f;
    const float ButtonWidth = 240f;
    const float ButtonHeight = 60f;
    const float ButtonSpacing = 10f;
    static readonly string[] PanelNames = { "InfoPanel", "ElectricalForceInfoPanel", "GaussianSurfaceInfoPanel" };

    [MenuItem("AR Tools/Resize And Fade Content Panels")]
    public static void Build()
    {
        var canvasGO = GameObject.Find("ContentOverLayCanvas");
        if (canvasGO == null) { Debug.LogError("ContentOverLayCanvas not found in the open scene."); return; }

        foreach (var panelName in PanelNames)
        {
            var panelT = canvasGO.transform.Find(panelName);
            if (panelT == null) { Debug.LogWarning($"{panelName} not found under ContentOverLayCanvas - skipped."); continue; }

            var rt = panelT.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, rt.pivot.y);
            rt.localScale = new Vector3(PanelScale, PanelScale, 1f);

            var bg = panelT.GetComponent<Image>();
            if (bg != null)
            {
                var c = bg.color;
                c.a = PanelBgAlpha;
                bg.color = c;
            }

            EditorUtility.SetDirty(panelT.gameObject);
        }

        StackPositionButtonsVertically(canvasGO.transform);

        EditorUtility.SetDirty(canvasGO);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"Content panels resized to {PanelScale:P0} (pinned bottom-left, semi-transparent) and Rods & Charge position buttons stacked vertically. Scene saved.");
    }

    static void StackPositionButtonsVertically(Transform canvasT)
    {
        var row = canvasT.Find("InfoPanel/PositionButtonRow");
        if (row == null) { Debug.LogWarning("PositionButtonRow not found under InfoPanel - skipped."); return; }

        var horizontal = row.GetComponent<HorizontalLayoutGroup>();
        var vertical = row.GetComponent<VerticalLayoutGroup>();
        if (horizontal != null)
        {
            var padding = horizontal.padding;
            var childAlignment = horizontal.childAlignment;
            Object.DestroyImmediate(horizontal);

            vertical = row.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = padding;
            vertical.childAlignment = childAlignment;
        }

        // Buttons no longer stretch to fill the row's width - each gets a fixed,
        // narrower size via its own LayoutElement instead, centered in the row.
        vertical.spacing = ButtonSpacing;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childForceExpandWidth = false;
        vertical.childForceExpandHeight = false;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;

        int buttonCount = row.childCount;
        for (int i = 0; i < buttonCount; i++)
        {
            var button = row.GetChild(i);
            var buttonLayout = button.GetComponent<LayoutElement>();
            if (buttonLayout == null) buttonLayout = button.gameObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = ButtonWidth;
            buttonLayout.preferredHeight = ButtonHeight;
            EditorUtility.SetDirty(button.gameObject);
        }

        float stackedHeight = buttonCount * ButtonHeight + Mathf.Max(0, buttonCount - 1) * ButtonSpacing;

        // The row itself stays full-width (InfoPanel's own layout group controls
        // and stretches it) - only its height changes. Buttons are the ones drawn
        // narrower and centered within that full-width row via ChildAlignment above.
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(rowRT.sizeDelta.x, stackedHeight);

        var layoutElement = row.GetComponent<LayoutElement>();
        if (layoutElement != null) layoutElement.preferredHeight = stackedHeight;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRT);
        EditorUtility.SetDirty(row.gameObject);
    }
}
