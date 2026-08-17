using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time editor tool: builds the camera-view Scan screen (ScanCanvas) - the
/// Searching / Found / NotRecognized / Loading states from the unity-handoff
/// design's screens 2a-2d. Sits above the AR camera passthrough (transparent
/// background, chrome only) and below DashboardCanvas in sorting order, so the
/// Dashboard's opaque panel still fully covers it whenever the Dashboard is
/// shown. Re-running this tool replaces any previous ScanCanvas.
/// </summary>
public static class BuildScanFlowUI
{
    static readonly Color Ink = new Color32(0x20, 0x1E, 0x1D, 0xFF);
    static readonly Color Accent = new Color32(0xEC, 0x30, 0x13, 0xFF);
    static readonly Color CameraGround = new Color32(0x2D, 0x2B, 0x2B, 0xFF);
    static readonly Color ChromeScrim = new Color(0f, 0f, 0f, 0.35f);
    static readonly Color MutedWhite = new Color(1f, 1f, 1f, 0.6f);

    const string IconsDir = "Assets/UI/Icons";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("AR Tools/Build Scan Flow UI")]
    public static void Build()
    {
        foreach (var icon in new[] { "arrow-left", "zap-off", "check", "circle-alert", "loader-circle" })
            ConfigureSpriteImport($"{IconsDir}/{icon}.png");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"Font asset not found at {FontPath}."); return; }

        var scene = EditorSceneManager.GetActiveScene();

        var existing = GameObject.Find("ScanCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var canvasGO = new GameObject("ScanCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5; // above AR/content overlay, below DashboardCanvas (10)

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 693);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.physicalUnit = CanvasScaler.Unit.Points;

        var panel = CreateUIObject("ScanPanel", canvasGO.transform); // no background - camera passthrough shows through
        StretchFull(panel.GetComponent<RectTransform>());
        canvasGO.SetActive(false); // hidden until AppFlowController.ShowScan()
        var root = panel.transform;

        // ---- Top bar: back + flash ----
        var backBtn = CreateIconButton(root, "BackButton", "arrow-left", new Vector2(0, 1), new Vector2(16, -16));
        var flashBtn = CreateIconButton(root, "FlashButton", "zap-off", new Vector2(1, 1), new Vector2(-16, -16));

        // ---- Center viewfinder frame ----
        var frame = CreateUIObject("CenterFrame", root);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(0, 20);
        frameRT.sizeDelta = new Vector2(190, 190);

        var barTop = CreateFrameBar(frame.transform, "BarTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1.5f), new Vector2(0, 3));
        var barBottom = CreateFrameBar(frame.transform, "BarBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1.5f), new Vector2(0, 3));
        var barLeft = CreateFrameBar(frame.transform, "BarLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(1.5f, 0), new Vector2(3, 0));
        var barRight = CreateFrameBar(frame.transform, "BarRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-1.5f, 0), new Vector2(3, 0));

        // ---- Bottom status card ----
        var card = CreateUIObject("BottomCard", root);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0, 0);
        cardRT.anchorMax = new Vector2(1, 0);
        cardRT.pivot = new Vector2(0.5f, 0);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(0, 100);
        card.AddComponent<Image>().color = CameraGround;

        var topRule = CreateUIObject("TopRule", card.transform);
        var ruleRT = topRule.GetComponent<RectTransform>();
        ruleRT.anchorMin = new Vector2(0, 1);
        ruleRT.anchorMax = new Vector2(1, 1);
        ruleRT.pivot = new Vector2(0.5f, 1f);
        ruleRT.anchoredPosition = Vector2.zero;
        ruleRT.sizeDelta = new Vector2(0, 3);
        var ruleImg = topRule.AddComponent<Image>();
        ruleImg.color = Accent;
        ruleImg.raycastTarget = false;

        var searching = CreateStatusBlock(card.transform, "StateSearching", font, "circle",
            "Searching for a marker…", "Point the camera at a diagram or page from Electromagnetic Theory.", Color.white, MutedWhite);
        var found = CreateStatusBlock(card.transform, "StateFound", font, "check",
            "Marker recognised", "Hold steady while the model loads.", Color.white, MutedWhite);
        var notRecognized = CreateStatusBlock(card.transform, "StateNotRecognized", font, "circle-alert",
            "Page not recognised", "Move closer, reduce glare, or try a flatter page.", Accent, MutedWhite);
        var loading = CreateStatusBlock(card.transform, "StateLoading", font, "loader-circle",
            "Loading AR content…", "Preparing your content.", Color.white, MutedWhite);
        loading.transform.Find("Icon").gameObject.AddComponent<UISpin>();

        found.SetActive(false);
        notRecognized.SetActive(false);
        loading.SetActive(false);

        // ---- Wire AppFlowController ----
        var flow = Object.FindFirstObjectByType<AppFlowController>();
        if (flow == null)
        {
            var flowGO = new GameObject("AppFlowController", typeof(AppFlowController));
            flow = flowGO.GetComponent<AppFlowController>();
        }
        flow.scanCanvas = canvasGO;
        flow.dashboardCanvas = GameObject.Find("DashboardCanvas");
        flow.scanStateSearching = searching;
        flow.scanStateFound = found;
        flow.scanStateNotRecognized = notRecognized;
        flow.scanStateLoading = loading;
        flow.scanFrameBars = new[] {
            barTop.GetComponent<Image>(), barBottom.GetComponent<Image>(),
            barLeft.GetComponent<Image>(), barRight.GetComponent<Image>()
        };
        EditorUtility.SetDirty(flow);

        var backButton = backBtn.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(backButton.onClick, flow.ShowDashboard);
        EditorUtility.SetDirty(backButton);

        var dashboardGO = GameObject.Find("DashboardCanvas");
        if (dashboardGO != null)
        {
            var scanTile = dashboardGO.transform.Find("DashboardPanel/Grid/TileScan");
            if (scanTile != null)
            {
                var scanTileBtn = scanTile.GetComponent<Button>();
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(scanTileBtn.onClick, flow.ShowScan);
                EditorUtility.SetDirty(scanTileBtn);
            }
            else Debug.LogWarning("TileScan not found under DashboardCanvas - Scan tile won't open the Scan screen.");
        }

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("ScanCanvas built, wired to Dashboard's Scan tile, and scene saved.");
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

    static GameObject CreateFrameBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = new Color32(0xFF, 0xFF, 0xFF, 0x99);
        img.raycastTarget = false;
        return go;
    }

    static GameObject CreateStatusBlock(Transform parent, string name, TMP_FontAsset font, string iconName, string title, string subtitle, Color titleColor, Color subtitleColor)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(16, 12);
        rt.offsetMax = new Vector2(-16, -16);

        var iconGO = CreateUIObject("Icon", go.transform);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{iconName}.png");
        iconImg.color = titleColor;
        iconImg.raycastTarget = false;
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 1);
        iconRT.anchorMax = new Vector2(0, 1);
        iconRT.pivot = new Vector2(0, 1);
        iconRT.anchoredPosition = new Vector2(0, 0);
        iconRT.sizeDelta = new Vector2(18, 18);

        var titleGO = CreateUIObject("Title", go.transform);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.text = title;
        titleTmp.fontSize = 14;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = titleColor;
        titleTmp.alignment = TextAlignmentOptions.TopLeft;
        titleTmp.raycastTarget = false;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0, 1);
        titleRT.anchoredPosition = new Vector2(26, -1);
        titleRT.sizeDelta = new Vector2(-26, 20);

        var subGO = CreateUIObject("Subtitle", go.transform);
        var subTmp = subGO.AddComponent<TextMeshProUGUI>();
        subTmp.font = font;
        subTmp.text = subtitle;
        subTmp.fontSize = 13;
        subTmp.fontStyle = FontStyles.Normal;
        subTmp.color = subtitleColor;
        subTmp.alignment = TextAlignmentOptions.TopLeft;
        subTmp.raycastTarget = false;
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0, 1);
        subRT.anchorMax = new Vector2(1, 1);
        subRT.pivot = new Vector2(0, 1);
        subRT.anchoredPosition = new Vector2(0, -26);
        subRT.sizeDelta = new Vector2(0, 40);

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
