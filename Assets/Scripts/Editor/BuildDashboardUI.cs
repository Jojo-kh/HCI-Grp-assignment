using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time editor tool: builds the home Dashboard screen (DashboardCanvas) in the
/// open scene, matching the unity-handoff design (Downloads/Dashboard layout
/// design.zip). Reference resolution matches ContentOverLayCanvas's (320x693) so
/// spacing/font-size values can be taken directly from the design's README (which
/// specifies them at that same 320x693 mock scale).
///
/// Icons were rasterized from the handoff's Lucide SVGs into Assets/UI/Icons as
/// white-on-transparent PNGs (so a single Image tint recolors them per tile); the
/// recent-scans placeholder uses a procedurally generated seamless diagonal-hatch
/// tile at Assets/UI/Textures. Re-running this tool replaces any previous
/// DashboardCanvas, so it's safe to run again after edits.
/// </summary>
public static class BuildDashboardUI
{
    static readonly Color Bg = new Color32(0xF3, 0xF2, 0xF2, 0xFF);
    static readonly Color Ink = new Color32(0x20, 0x1E, 0x1D, 0xFF);
    static readonly Color Accent = new Color32(0xEC, 0x30, 0x13, 0xFF);
    static readonly Color AccentPressed = new Color32(0xDD, 0x2B, 0x0F, 0xFF);
    static readonly Color Divider = new Color32(0x20, 0x1E, 0x1D, 0x66); // ink @ 40%
    static readonly Color MutedInk = new Color32(0x20, 0x1E, 0x1D, 0x99); // ink @ ~60%, secondary text

    const string IconsDir = "Assets/UI/Icons";
    const string TexturesDir = "Assets/UI/Textures";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("AR Tools/Build Dashboard UI")]
    public static void Build()
    {
        ConfigureSpriteImport(IconsDir + "/zap.png");
        ConfigureSpriteImport(IconsDir + "/orbit.png");
        ConfigureSpriteImport(IconsDir + "/magnet.png");
        ConfigureSpriteImport(IconsDir + "/camera.png");
        ConfigureSpriteImport(TexturesDir + "/hatch_placeholder.png", wrapRepeat: true);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"Font asset not found at {FontPath}.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();

        var existing = GameObject.Find("DashboardCanvas");
        if (existing != null) Object.DestroyImmediate(existing);

        var canvasGO = new GameObject("DashboardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10; // above ContentOverLayCanvas - dashboard is the entry screen

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 693);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.physicalUnit = CanvasScaler.Unit.Points;

        var panel = CreateUIObject("DashboardPanel", canvasGO.transform);
        StretchFull(panel.GetComponent<RectTransform>());
        panel.AddComponent<Image>().color = Bg;
        var root = panel.transform;

        // ---- Header ----
        var header = CreateUIObject("Header", root);
        AnchorTopStretch(header.GetComponent<RectTransform>(), yTop: -20, height: 67);

        var title = CreateText(header.transform, "TitleText", "Good morning", font, 24, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft);
        AnchorWithin(title.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -8), new Vector2(-32, 30));

        var subtitle = CreateText(header.transform, "SubtitleText", "Electromagnetic Theory · 3 chapters · ready to scan", font, 13, FontStyles.Normal, MutedInk, TextAlignmentOptions.TopLeft);
        AnchorWithin(subtitle.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -40), new Vector2(-32, 20));

        CreateHDivider(root, "DividerHeader", yTop: -89);

        // ---- Grid (2x2 tiles) ----
        var grid = CreateUIObject("Grid", root);
        AnchorTopStretch(grid.GetComponent<RectTransform>(), yTop: -91, height: 495);

        CreateTile(grid.transform, "TileElectricalForce", new Vector2(0, 0.5f), new Vector2(0.5f, 1f), font,
            "zap", "Electrical\nForce (F, E)", Bg, Ink);
        CreateTile(grid.transform, "TileGaussianSurfaces", new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), font,
            "orbit", "Gaussian\nSurfaces", Bg, Ink);
        CreateTile(grid.transform, "TileRodsCharge", new Vector2(0, 0f), new Vector2(0.5f, 0.5f), font,
            "magnet", "Rods &\nCharge", Bg, Ink);
        var tileScan = CreateTile(grid.transform, "TileScan", new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), font,
            "camera", "Scan", Accent, Color.white);
        var scanBtn = tileScan.GetComponent<Button>();
        var scanColors = scanBtn.colors;
        scanColors.normalColor = Accent;
        scanColors.highlightedColor = Accent;
        scanColors.pressedColor = AccentPressed;
        scanColors.selectedColor = Accent;
        scanBtn.colors = scanColors;

        // Dividers last so they render on top of the tile backgrounds.
        CreateVDividerLocal(grid.transform, "DividerGridV");
        CreateHDividerLocal(grid.transform, "DividerGridH");

        // ---- Recent scans ----
        var recent = CreateUIObject("RecentScans", root);
        AnchorTopStretch(recent.GetComponent<RectTransform>(), yTop: -586, height: 107);

        var kicker = CreateText(recent.transform, "KickerText", "RECENT SCANS", font, 13, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft);
        kicker.GetComponent<TextMeshProUGUI>().characterSpacing = 4;
        AnchorWithin(kicker.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(-32, 18));

        var thumbRow = CreateUIObject("ThumbnailRow", recent.transform);
        AnchorWithin(thumbRow.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -42), new Vector2(-32, 56));
        var hlg = thumbRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var hatchSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/hatch_placeholder.png");
        for (int i = 0; i < 3; i++)
        {
            var thumb = CreateUIObject($"Thumb{i + 1}", thumbRow.transform);
            var img = thumb.AddComponent<Image>();
            img.sprite = hatchSprite;
            img.type = Image.Type.Tiled;
        }

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("DashboardCanvas built and scene saved.");
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

    static void AnchorTopStretch(RectTransform rt, float yTop, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, yTop);
        rt.sizeDelta = new Vector2(0, height);
    }

    static void AnchorWithin(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static GameObject CreateText(Transform parent, string name, string text, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = CreateUIObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return go;
    }

    static GameObject CreateTile(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, string iconName, string label, Color bgColor, Color fgColor)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = Color.Lerp(bgColor, Color.black, 0.05f);
        colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.1f);
        colors.selectedColor = bgColor;
        btn.colors = colors;

        var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{iconName}.png");
        var iconGO = CreateUIObject("Icon", go.transform);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.color = fgColor;
        iconImg.raycastTarget = false;
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 1);
        iconRT.anchorMax = new Vector2(0, 1);
        iconRT.pivot = new Vector2(0, 1);
        iconRT.anchoredPosition = new Vector2(16, -16);
        iconRT.sizeDelta = new Vector2(24, 24);

        var labelGO = CreateText(go.transform, "Label", label, font, 16, FontStyles.Bold, fgColor, TextAlignmentOptions.BottomLeft);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 0);
        labelRT.pivot = new Vector2(0, 0);
        labelRT.anchoredPosition = new Vector2(16, 16);
        labelRT.sizeDelta = new Vector2(-32, 44);

        return go;
    }

    static void CreateHDivider(Transform parent, string name, float yTop)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, yTop);
        rt.sizeDelta = new Vector2(0, 2);
        var img = go.AddComponent<Image>();
        img.color = Divider;
        img.raycastTarget = false;
    }

    static void CreateHDividerLocal(Transform parent, string name)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 2);
        var img = go.AddComponent<Image>();
        img.color = Divider;
        img.raycastTarget = false;
    }

    static void CreateVDividerLocal(Transform parent, string name)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(2, 0);
        var img = go.AddComponent<Image>();
        img.color = Divider;
        img.raycastTarget = false;
    }

    static void ConfigureSpriteImport(string assetPath, bool wrapRepeat = false)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"No importer found for {assetPath}.");
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
