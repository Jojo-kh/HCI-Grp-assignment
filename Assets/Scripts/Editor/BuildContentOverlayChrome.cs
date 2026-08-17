using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time editor tool: adds the dashboard-matching chrome to the content
/// overlay screen (unity-handoff design's 3a_content_overlay.png) - a shared
/// close/bookmark top bar over the AR view, and a "Back to dashboard" row
/// appended to each of the three marker InfoPanels (InfoPanel, ElectricalForce,
/// GaussianSurface). Wires both into AppFlowController. Re-running this tool
/// replaces the previous top bar and any previously-added back buttons, so it's
/// safe to re-run after design tweaks.
/// </summary>
public static class BuildContentOverlayChrome
{
    static readonly Color Ink = new Color32(0x20, 0x1E, 0x1D, 0xFF);
    static readonly Color Bg = new Color32(0xF3, 0xF2, 0xF2, 0xFF);
    static readonly Color Accent = new Color32(0xEC, 0x30, 0x13, 0xFF);
    static readonly Color ChromeScrim = new Color(0f, 0f, 0f, 0.35f);

    const string IconsDir = "Assets/UI/Icons";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    static readonly string[] PanelNames = { "InfoPanel", "ElectricalForceInfoPanel", "GaussianSurfaceInfoPanel" };

    [MenuItem("AR Tools/Build Content Overlay Chrome")]
    public static void Build()
    {
        foreach (var icon in new[] { "x", "bookmark", "arrow-left" })
            ConfigureSpriteImport($"{IconsDir}/{icon}.png");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"Font asset not found at {FontPath}."); return; }

        var canvasGO = GameObject.Find("ContentOverLayCanvas");
        if (canvasGO == null) { Debug.LogError("ContentOverLayCanvas not found in the open scene."); return; }

        var scene = EditorSceneManager.GetActiveScene();

        // ---- Shared top bar (close + bookmark) ----
        var existingBar = canvasGO.transform.Find("ContentTopBar");
        if (existingBar != null) Object.DestroyImmediate(existingBar.gameObject);

        var topBar = CreateUIObject("ContentTopBar", canvasGO.transform);
        StretchFull(topBar.GetComponent<RectTransform>());
        topBar.SetActive(false); // AppFlowController toggles this on when a marker panel is showing

        var closeBtn = CreateIconButton(topBar.transform, "CloseButton", "x", new Vector2(0, 1), new Vector2(16, -16));
        var bookmarkToggle = CreateBookmarkToggle(topBar.transform, new Vector2(1, 1), new Vector2(-16, -16));

        // ---- "Back to dashboard" row on each info panel ----
        var panelGOs = new System.Collections.Generic.List<GameObject>();
        foreach (var panelName in PanelNames)
        {
            var panelT = canvasGO.transform.Find(panelName);
            if (panelT == null) { Debug.LogWarning($"{panelName} not found under ContentOverLayCanvas - skipped."); continue; }
            panelGOs.Add(panelT.gameObject);

            var oldBtn = panelT.Find("BackToDashboardButton");
            if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);

            var backRow = CreateBackToDashboardButton(panelT, font);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelT.GetComponent<RectTransform>());
            EditorUtility.SetDirty(panelT.gameObject);
        }

        // ---- Wire AppFlowController ----
        var flow = Object.FindFirstObjectByType<AppFlowController>();
        if (flow == null)
        {
            var flowGO = new GameObject("AppFlowController", typeof(AppFlowController));
            flow = flowGO.GetComponent<AppFlowController>();
        }
        flow.contentTopBar = topBar;
        flow.contentInfoPanels = panelGOs.ToArray();
        if (flow.dashboardCanvas == null) flow.dashboardCanvas = GameObject.Find("DashboardCanvas");
        EditorUtility.SetDirty(flow);

        var closeButton = closeBtn.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(closeButton.onClick, flow.ShowDashboard);
        EditorUtility.SetDirty(closeButton);

        foreach (var panelGO in panelGOs)
        {
            var backBtn = panelGO.transform.Find("BackToDashboardButton").GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(backBtn.onClick, flow.ShowDashboard);
            EditorUtility.SetDirty(backBtn);
        }

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Content overlay chrome built (top bar + Back to dashboard on all 3 panels) and scene saved.");
    }

    // ---------- helpers ----------

    static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static GameObject CreateIconButton(Transform parent, string name, string iconName, Vector2 anchor, Vector2 anchoredPos)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(40, 40);

        var img = go.AddComponent<Image>();
        img.color = ChromeScrim;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = ChromeScrim;
        colors.highlightedColor = new Color(0, 0, 0, 0.45f);
        colors.pressedColor = new Color(0, 0, 0, 0.55f);
        colors.selectedColor = ChromeScrim;
        btn.colors = colors;

        var iconGO = CreateUIObject("Icon", go.transform);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{iconName}.png");
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta = new Vector2(20, 20);

        return go;
    }

    static GameObject CreateBookmarkToggle(Transform parent, Vector2 anchor, Vector2 anchoredPos)
    {
        var go = CreateUIObject("BookmarkButton", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(40, 40);

        var img = go.AddComponent<Image>();
        img.color = ChromeScrim;
        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = img;
        var colors = toggle.colors;
        colors.normalColor = ChromeScrim;
        colors.highlightedColor = new Color(0, 0, 0, 0.45f);
        colors.pressedColor = new Color(0, 0, 0, 0.55f);
        colors.selectedColor = ChromeScrim;
        toggle.colors = colors;

        var iconGO = CreateUIObject("Icon", go.transform);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/bookmark.png");
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        SquareIcon(iconGO, 20);

        var filledGO = CreateUIObject("IconFilled", go.transform);
        var filledImg = filledGO.AddComponent<Image>();
        filledImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/bookmark.png");
        filledImg.color = Accent;
        filledImg.raycastTarget = false;
        SquareIcon(filledGO, 20);

        toggle.graphic = filledImg;
        toggle.isOn = false;

        return go;
    }

    static void SquareIcon(GameObject go, float size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
    }

    static GameObject CreateBackToDashboardButton(Transform parent, TMP_FontAsset font)
    {
        var go = CreateUIObject("BackToDashboardButton", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0, 44);

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 44;

        var img = go.AddComponent<Image>();
        img.color = Bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Bg;
        colors.highlightedColor = Color.Lerp(Bg, Color.black, 0.05f);
        colors.pressedColor = Color.Lerp(Bg, Color.black, 0.1f);
        colors.selectedColor = Bg;
        btn.colors = colors;

        var iconGO = CreateUIObject("Icon", go.transform);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/arrow-left.png");
        iconImg.color = Ink;
        iconImg.raycastTarget = false;
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.pivot = new Vector2(0, 0.5f);
        iconRT.anchoredPosition = new Vector2(4, 0);
        iconRT.sizeDelta = new Vector2(16, 16);

        var labelGO = CreateUIObject("Label", go.transform);
        var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.font = font;
        labelTmp.text = "Back to dashboard";
        labelTmp.fontSize = 14;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = Ink;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.raycastTarget = false;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(28, 0);
        labelRT.offsetMax = Vector2.zero;

        return go;
    }

    static void ConfigureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"No importer found for {assetPath}."); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
