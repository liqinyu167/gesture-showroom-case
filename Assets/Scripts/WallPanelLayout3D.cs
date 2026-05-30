using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds a reusable 3D wall panel with normalized text and image slots.
/// Attach it to a child object under a wall mesh and edit the fields on this component
/// instead of dragging the generated child objects by hand.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class WallPanelLayout3D : MonoBehaviour
{
    [Serializable]
    public class TextSlot
    {
        public bool Enabled = true;
        public string ObjectName = "Text";

        [TextArea(1, 4)]
        public string Text = string.Empty;

        public TMP_FontAsset Font;
        [Min(0.1f)] public float FontSize = 8f;
        public FontStyles FontStyle = FontStyles.Normal;
        public Color Color = Color.white;
        public TextAlignmentOptions Alignment = TextAlignmentOptions.Center;
        public bool WordWrap;
        public TextOverflowModes OverflowMode = TextOverflowModes.Overflow;
        public bool AutoSizeToFit = true;
        [Min(0.1f)] public float MinFontSize = 1.2f;
        public bool KeepGlyphsInsideBox = true;
        public float CharacterSpacing;
        public float LineSpacing;

        [Tooltip("Normalized center position inside the virtual 16:9 panel. -1/-1 is bottom-left, 1/1 is top-right.")]
        public Vector2 Anchor = Vector2.zero;

        [Tooltip("Box size in panel units, before Layout Scale is applied.")]
        public Vector2 BoxSize = new Vector2(20f, 4f);

        [Tooltip("Local z inside the panel in panel units. Larger values float further away from the wall.")]
        public float Depth = 0.25f;
    }

    [Serializable]
    public class ImageLayer
    {
        public bool Enabled = true;
        public string ObjectName = "Image Layer";
        public Texture Texture;
        public Color Tint = new Color(1f, 1f, 1f, 0.25f);
        public bool PreserveTextureColors = true;
        [Range(0f, 1f)] public float TextureOpacity = 1f;

        [Tooltip("Normalized center position inside the virtual 16:9 panel. -1/-1 is bottom-left, 1/1 is top-right.")]
        public Vector2 Anchor = Vector2.zero;

        [Tooltip("Max box size in panel units, before Layout Scale is applied.")]
        public Vector2 BoxSize = new Vector2(24f, 13.5f);

        [Tooltip("Local z inside the panel in panel units. Larger values float further away from the wall.")]
        public float Depth = 0.02f;
        public bool PreserveAspect = true;
    }

    [Header("Panel")]
    [Tooltip("Virtual layout canvas size. Keep this in a 16:9 ratio for consistent wall composition.")]
    public Vector2 LayoutSize = new Vector2(32f, 18f);
    [Min(0.001f)] public float LayoutScale = 0.085f;
    [Tooltip("Center of the panel on the parent wall mesh in the wall's local X/Y space.")]
    public Vector2 PanelCenter = new Vector2(0f, 1.6f);
    [Min(0f)] public float SurfaceOffset = 0.03f;
    public bool ClampInsideWallBounds = true;
    [Tooltip("Flips the virtual canvas horizontally while keeping text readable. Useful when a wall mesh's local X runs opposite to the perceived left/right on camera.")]
    public bool MirrorLayoutX = false;
    [Tooltip("Keeps text and image boxes inset from the virtual canvas edge, in panel units.")]
    public Vector2 CanvasPadding = new Vector2(0.8f, 0.8f);

    [Header("Behavior")]
    [Tooltip("Automatically reapplies the generated layout while editing in the Unity Editor.")]
    public bool AutoApplyInEditor = false;
    [Tooltip("Automatically reapplies the generated layout when entering Play Mode. Disable this if you hand-tune generated child transforms and do not want them snapped back at runtime.")]
    public bool AutoApplyOnPlay = false;

    [Header("Text")]
    public TextSlot Title = new TextSlot
    {
        ObjectName = "Title",
        Text = "ALIBABA AI",
        FontSize = 12f,
        FontStyle = FontStyles.Bold,
        Color = new Color(0.96f, 0.97f, 1f, 1f),
        CharacterSpacing = 8f,
        Anchor = new Vector2(0f, 0.72f),
        BoxSize = new Vector2(26f, 3.4f),
        Depth = 0.28f
    };

    public TextSlot Subtitle = new TextSlot
    {
        ObjectName = "Subtitle",
        Text = "Gesture Experience Space",
        FontSize = 4.4f,
        Color = new Color(0.80f, 0.88f, 0.98f, 0.95f),
        CharacterSpacing = 1.3f,
        Anchor = new Vector2(0f, 0.28f),
        BoxSize = new Vector2(24f, 2.6f),
        Depth = 0.26f
    };

    public TextSlot Accent = new TextSlot
    {
        ObjectName = "Accent",
        Text = "DOUBLE TAP TO EXPLORE",
        FontSize = 3.1f,
        Color = new Color(0.57f, 0.75f, 0.96f, 0.9f),
        CharacterSpacing = 2.3f,
        Anchor = new Vector2(0f, -0.02f),
        BoxSize = new Vector2(22f, 2f),
        Depth = 0.24f
    };

    public TextSlot Body = new TextSlot
    {
        ObjectName = "Body",
        Text = string.Empty,
        FontSize = 2.2f,
        Color = new Color(0.14f, 0.15f, 0.17f, 0.96f),
        Alignment = TextAlignmentOptions.TopLeft,
        WordWrap = true,
        AutoSizeToFit = true,
        MinFontSize = 1.4f,
        KeepGlyphsInsideBox = true,
        Anchor = new Vector2(-0.10f, -0.06f),
        BoxSize = new Vector2(12.2f, 4.4f),
        Depth = 0.22f
    };

    public TextSlot Caption = new TextSlot
    {
        ObjectName = "Caption",
        Text = string.Empty,
        FontSize = 1.55f,
        Color = new Color(0.34f, 0.34f, 0.34f, 0.92f),
        Alignment = TextAlignmentOptions.TopLeft,
        WordWrap = true,
        AutoSizeToFit = true,
        MinFontSize = 1.1f,
        KeepGlyphsInsideBox = true,
        Anchor = new Vector2(0.30f, -0.38f),
        BoxSize = new Vector2(8.4f, 2.6f),
        Depth = 0.24f
    };

    [Header("Images")]
    public List<ImageLayer> ImageLayers = new List<ImageLayer>
    {
        new ImageLayer
        {
            ObjectName = "PosterLayer 01",
            Texture = null,
            Tint = new Color(0.80f, 0.90f, 1f, 0.18f),
            Anchor = new Vector2(0f, -0.22f),
            BoxSize = new Vector2(26f, 14.625f),
            Depth = 0.06f,
            PreserveAspect = true
        }
    };

    private const string ContentRootName = "_GeneratedContentRoot";
    private const string TextRootName = "_GeneratedText";
    private const string ImageRootName = "_GeneratedImages";

    private readonly Dictionary<string, TextMeshPro> _textCache = new Dictionary<string, TextMeshPro>();
    private readonly List<Material> _imageMaterials = new List<Material>();
    private static Mesh _quadMesh;

    private Transform _contentRoot;
    private Transform _textRoot;
    private Transform _imageRoot;

    private void Reset()
    {
        ConfigureEditorialPreset(true);
        ApplyLayout();
    }

    private void OnEnable()
    {
        if (!ShouldAutoApplyNow())
        {
            return;
        }

        ApplyLayout();
    }

    private void OnValidate()
    {
        ClampFields();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!ShouldAutoApplyInEditor())
            {
                return;
            }

            EditorApplication.delayCall -= ApplyLayoutDelayed;
            EditorApplication.delayCall += ApplyLayoutDelayed;
            return;
        }
#endif
        if (!AutoApplyOnPlay)
        {
            return;
        }

        ApplyLayout();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= ApplyLayoutDelayed;
#endif
        DisposeImageMaterials();
    }

#if UNITY_EDITOR
    private void ApplyLayoutDelayed()
    {
        if (this == null || !ShouldAutoApplyInEditor())
        {
            return;
        }

        ApplyLayout();
    }

    private bool ShouldAutoApplyInEditor()
    {
        if (Application.isPlaying || !AutoApplyInEditor)
        {
            return false;
        }

        if (EditorUtility.IsPersistent(gameObject))
        {
            return false;
        }

        return gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.path);
    }
#endif

    private bool ShouldAutoApplyNow()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return AutoApplyOnPlay;
        }

        return ShouldAutoApplyInEditor();
#else
        return AutoApplyOnPlay;
#endif
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        ClampFields();
        EnsureRoots();
        ApplyPanelTransform();
        ApplyTextSlot(Title, "Title");
        ApplyTextSlot(Subtitle, "Subtitle");
        ApplyTextSlot(Accent, "Accent");
        ApplyTextSlot(Body, "Body");
        ApplyTextSlot(Caption, "Caption");
        DeactivateUnusedTextObjects();
        ApplyImageLayers();
    }

    [ContextMenu("Preset/Editorial Sample")]
    public void ApplyEditorialSamplePreset()
    {
        ConfigureEditorialPreset(true);
        ApplyLayout();
    }

    [ContextMenu("Preset/Editorial Blank")]
    public void ApplyEditorialBlankPreset()
    {
        ConfigureEditorialPreset(false);
        ApplyLayout();
    }

    public void ConfigureEditorialPreset(bool includeSampleContent)
    {
        LayoutSize = new Vector2(32f, 18f);
        LayoutScale = 0.085f;
        PanelCenter = new Vector2(0f, 1.6f);
        SurfaceOffset = 0.03f;
        ClampInsideWallBounds = true;
        MirrorLayoutX = true;
        CanvasPadding = new Vector2(0.8f, 0.8f);

        Accent.Enabled = includeSampleContent;
        Accent.ObjectName = "Meta";
        Accent.Text = includeSampleContent ? "DENSITY / MEDIUM" : string.Empty;
        Accent.Font = null;
        Accent.FontSize = 1.9f;
        Accent.FontStyle = FontStyles.Bold;
        Accent.Color = new Color(0.17f, 0.19f, 0.23f, 0.95f);
        Accent.Alignment = TextAlignmentOptions.TopLeft;
        Accent.WordWrap = false;
        Accent.OverflowMode = TextOverflowModes.Overflow;
        Accent.AutoSizeToFit = true;
        Accent.MinFontSize = 1.1f;
        Accent.KeepGlyphsInsideBox = true;
        Accent.CharacterSpacing = 2.2f;
        Accent.LineSpacing = 0f;
        Accent.Anchor = new Vector2(-0.57f, 0.82f);
        Accent.BoxSize = new Vector2(15f, 1.4f);
        Accent.Depth = 0.24f;

        Title.Enabled = includeSampleContent;
        Title.ObjectName = "Title";
        Title.Text = includeSampleContent ? "ALIBABA\nAI" : string.Empty;
        Title.Font = null;
        Title.FontSize = 12.4f;
        Title.FontStyle = FontStyles.Bold;
        Title.Color = new Color(0.05f, 0.05f, 0.06f, 1f);
        Title.Alignment = TextAlignmentOptions.TopLeft;
        Title.WordWrap = true;
        Title.OverflowMode = TextOverflowModes.Overflow;
        Title.AutoSizeToFit = true;
        Title.MinFontSize = 5f;
        Title.KeepGlyphsInsideBox = true;
        Title.CharacterSpacing = -1.2f;
        Title.LineSpacing = -10f;
        Title.Anchor = new Vector2(-0.56f, 0.50f);
        Title.BoxSize = new Vector2(13.6f, 8.6f);
        Title.Depth = 0.26f;

        Subtitle.Enabled = includeSampleContent;
        Subtitle.ObjectName = "Subtitle";
        Subtitle.Text = includeSampleContent
            ? "通义大模型驱动 · 全栈 AI 基础设施 · 开源生态 · 应用落地"
            : string.Empty;
        Subtitle.Font = null;
        Subtitle.FontSize = 3.1f;
        Subtitle.FontStyle = FontStyles.Normal;
        Subtitle.Color = new Color(0.33f, 0.33f, 0.33f, 0.98f);
        Subtitle.Alignment = TextAlignmentOptions.TopLeft;
        Subtitle.WordWrap = true;
        Subtitle.OverflowMode = TextOverflowModes.Overflow;
        Subtitle.AutoSizeToFit = true;
        Subtitle.MinFontSize = 1.5f;
        Subtitle.KeepGlyphsInsideBox = true;
        Subtitle.CharacterSpacing = 0f;
        Subtitle.LineSpacing = 4f;
        Subtitle.Anchor = new Vector2(-0.15f, 0.32f);
        Subtitle.BoxSize = new Vector2(18.5f, 2.3f);
        Subtitle.Depth = 0.24f;

        Body.Enabled = includeSampleContent;
        Body.ObjectName = "Body";
        Body.Text = includeSampleContent
            ? "将复杂信息转化为具有现代杂志排版高度与瑞士国际主义质感的空间信息卡。"
            : string.Empty;
        Body.Font = null;
        Body.FontSize = 2.2f;
        Body.FontStyle = FontStyles.Normal;
        Body.Color = new Color(0.14f, 0.15f, 0.17f, 0.96f);
        Body.Alignment = TextAlignmentOptions.TopLeft;
        Body.WordWrap = true;
        Body.OverflowMode = TextOverflowModes.Overflow;
        Body.AutoSizeToFit = true;
        Body.MinFontSize = 1.4f;
        Body.KeepGlyphsInsideBox = true;
        Body.CharacterSpacing = 0f;
        Body.LineSpacing = 6f;
        Body.Anchor = new Vector2(-0.18f, -0.02f);
        Body.BoxSize = new Vector2(10.0f, 3.8f);
        Body.Depth = 0.22f;

        Caption.Enabled = includeSampleContent;
        Caption.ObjectName = "Caption";
        Caption.Text = includeSampleContent
            ? "双击任意展项进入观察模式"
            : string.Empty;
        Caption.Font = null;
        Caption.FontSize = 1.55f;
        Caption.FontStyle = FontStyles.Normal;
        Caption.Color = new Color(0.35f, 0.35f, 0.35f, 0.92f);
        Caption.Alignment = TextAlignmentOptions.TopLeft;
        Caption.WordWrap = true;
        Caption.OverflowMode = TextOverflowModes.Overflow;
        Caption.AutoSizeToFit = true;
        Caption.MinFontSize = 1.1f;
        Caption.KeepGlyphsInsideBox = true;
        Caption.CharacterSpacing = 0f;
        Caption.LineSpacing = 2f;
        Caption.Anchor = new Vector2(0.30f, -0.38f);
        Caption.BoxSize = new Vector2(8.4f, 2.6f);
        Caption.Depth = 0.24f;

        EnsureImageLayerCount(4);

        ImageLayer paper = ImageLayers[0];
        paper.Enabled = includeSampleContent;
        paper.ObjectName = "PaperLayer";
        paper.Texture = null;
        paper.Tint = new Color(0.94f, 0.93f, 0.90f, 0.86f);
        paper.PreserveTextureColors = false;
        paper.Anchor = new Vector2(0f, 0f);
        paper.BoxSize = new Vector2(28.4f, 15.8f);
        paper.Depth = 0.02f;
        paper.PreserveAspect = false;

        ImageLayer hero = ImageLayers[1];
        hero.Enabled = includeSampleContent;
        hero.ObjectName = "HeroImage";
        hero.Texture = null;
        hero.Tint = new Color(0.44f, 0.50f, 0.60f, 0.22f);
        hero.PreserveTextureColors = true;
        hero.TextureOpacity = 0.95f;
        hero.Anchor = new Vector2(0.33f, 0.23f);
        hero.BoxSize = new Vector2(11.4f, 8.2f);
        hero.Depth = 0.08f;
        hero.PreserveAspect = true;

        ImageLayer rule = ImageLayers[2];
        rule.Enabled = includeSampleContent;
        rule.ObjectName = "AccentRule";
        rule.Texture = null;
        rule.Tint = new Color(0.10f, 0.11f, 0.14f, 0.98f);
        rule.PreserveTextureColors = false;
        rule.Anchor = new Vector2(0.00f, 0.17f);
        rule.BoxSize = new Vector2(23.4f, 0.34f);
        rule.Depth = 0.20f;
        rule.PreserveAspect = false;

        ImageLayer note = ImageLayers[3];
        note.Enabled = includeSampleContent;
        note.ObjectName = "CaptionPanel";
        note.Texture = null;
        note.Tint = new Color(0.10f, 0.11f, 0.14f, 0.08f);
        note.PreserveTextureColors = false;
        note.Anchor = new Vector2(0.30f, -0.38f);
        note.BoxSize = new Vector2(8.4f, 2.6f);
        note.Depth = 0.06f;
        note.PreserveAspect = false;
    }

    private void ClampFields()
    {
        LayoutSize.x = Mathf.Max(1f, LayoutSize.x);
        LayoutSize.y = Mathf.Max(1f, LayoutSize.y);
        LayoutScale = Mathf.Max(0.001f, LayoutScale);
        SurfaceOffset = Mathf.Max(0f, SurfaceOffset);
        CanvasPadding.x = Mathf.Max(0f, CanvasPadding.x);
        CanvasPadding.y = Mathf.Max(0f, CanvasPadding.y);

        ClampTextSlot(Title);
        ClampTextSlot(Subtitle);
        ClampTextSlot(Accent);
        ClampTextSlot(Body);
        ClampTextSlot(Caption);

        if (ImageLayers == null)
        {
            ImageLayers = new List<ImageLayer>();
        }

        foreach (var layer in ImageLayers)
        {
            if (layer == null)
            {
                continue;
            }

            layer.Anchor.x = Mathf.Clamp(layer.Anchor.x, -1f, 1f);
            layer.Anchor.y = Mathf.Clamp(layer.Anchor.y, -1f, 1f);
            layer.BoxSize.x = Mathf.Max(0.1f, layer.BoxSize.x);
            layer.BoxSize.y = Mathf.Max(0.1f, layer.BoxSize.y);
            layer.TextureOpacity = Mathf.Clamp01(layer.TextureOpacity);
        }
    }

    private void ClampTextSlot(TextSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.Anchor.x = Mathf.Clamp(slot.Anchor.x, -1f, 1f);
        slot.Anchor.y = Mathf.Clamp(slot.Anchor.y, -1f, 1f);
        slot.BoxSize.x = Mathf.Max(0.1f, slot.BoxSize.x);
        slot.BoxSize.y = Mathf.Max(0.1f, slot.BoxSize.y);
        slot.FontSize = Mathf.Max(0.1f, slot.FontSize);
        slot.MinFontSize = Mathf.Clamp(slot.MinFontSize, 0.1f, slot.FontSize);
    }

    private void EnsureImageLayerCount(int count)
    {
        if (ImageLayers == null)
        {
            ImageLayers = new List<ImageLayer>();
        }

        while (ImageLayers.Count < count)
        {
            ImageLayers.Add(new ImageLayer());
        }
    }

    private void EnsureRoots()
    {
        _contentRoot = FindOrCreateChild(transform, ContentRootName);
        _textRoot = FindOrCreateChild(_contentRoot, TextRootName);
        _imageRoot = FindOrCreateChild(_contentRoot, ImageRootName);

        _contentRoot.localPosition = Vector3.zero;
        _contentRoot.localRotation = Quaternion.identity;
        _contentRoot.localScale = Vector3.one;

        _textRoot.localPosition = Vector3.zero;
        _textRoot.localRotation = Quaternion.identity;
        _textRoot.localScale = Vector3.one * LayoutScale;

        _imageRoot.localPosition = Vector3.zero;
        _imageRoot.localRotation = Quaternion.identity;
        _imageRoot.localScale = Vector3.one * LayoutScale;
    }

    private void ApplyPanelTransform()
    {
        Bounds wallBounds = GetParentWallLocalBounds();
        Vector2 panelSizeOnWall = new Vector2(LayoutSize.x * LayoutScale, LayoutSize.y * LayoutScale);
        Vector2 center = PanelCenter;

        if (ClampInsideWallBounds)
        {
            float minX = wallBounds.center.x - Mathf.Max(0f, wallBounds.extents.x - (panelSizeOnWall.x * 0.5f));
            float maxX = wallBounds.center.x + Mathf.Max(0f, wallBounds.extents.x - (panelSizeOnWall.x * 0.5f));
            float minY = wallBounds.center.y - Mathf.Max(0f, wallBounds.extents.y - (panelSizeOnWall.y * 0.5f));
            float maxY = wallBounds.center.y + Mathf.Max(0f, wallBounds.extents.y - (panelSizeOnWall.y * 0.5f));

            center.x = Mathf.Clamp(center.x, minX, maxX);
            center.y = Mathf.Clamp(center.y, minY, maxY);
            PanelCenter = center;
        }

        float wallFrontZ = wallBounds.center.z + wallBounds.extents.z + SurfaceOffset;

        transform.localPosition = new Vector3(center.x, center.y, wallFrontZ);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void ApplyTextSlot(TextSlot slot, string fallbackObjectName)
    {
        if (slot == null)
        {
            return;
        }

        string objectName = string.IsNullOrWhiteSpace(slot.ObjectName) ? fallbackObjectName : slot.ObjectName;
        bool createdNow;
        var tmp = GetOrCreateText(objectName, out createdNow);

        tmp.gameObject.SetActive(slot.Enabled);
        if (!slot.Enabled)
        {
            return;
        }

        TMP_FontAsset fontAsset = slot.Font != null ? slot.Font : TMP_Settings.defaultFontAsset;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }

        if (createdNow || string.IsNullOrEmpty(tmp.text))
        {
            tmp.text = slot.Text ?? string.Empty;
            tmp.fontSize = slot.FontSize;
            tmp.enableAutoSizing = slot.AutoSizeToFit;
            tmp.fontSizeMin = Mathf.Min(slot.MinFontSize, slot.FontSize);
            tmp.fontSizeMax = slot.FontSize;
            tmp.fontStyle = slot.FontStyle;
            tmp.color = slot.Color;
            tmp.alignment = slot.Alignment;
            tmp.enableWordWrapping = slot.WordWrap;
            tmp.overflowMode = slot.KeepGlyphsInsideBox ? TextOverflowModes.Ellipsis : slot.OverflowMode;
            tmp.characterSpacing = slot.CharacterSpacing;
            tmp.lineSpacing = slot.LineSpacing;
            tmp.margin = Vector4.zero;
            tmp.extraPadding = true;
            tmp.richText = true;
        }

        var rectTransform = tmp.rectTransform;
        Vector2 clampedBoxSize = GetClampedBoxSize(slot.BoxSize);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        rectTransform.sizeDelta = clampedBoxSize;
        rectTransform.localPosition = BuildAnchoredLocalPosition(slot.Anchor, clampedBoxSize, slot.Depth);

        var renderer = tmp.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void ApplyImageLayers()
    {
        int requiredCount = ImageLayers != null ? ImageLayers.Count : 0;

        while (_imageMaterials.Count < requiredCount)
        {
            _imageMaterials.Add(null);
        }

        for (int i = 0; i < requiredCount; i++)
        {
            ImageLayer layer = ImageLayers[i];
            if (layer == null)
            {
                continue;
            }

            string objectName = string.IsNullOrWhiteSpace(layer.ObjectName) ? $"ImageLayer {i + 1}" : layer.ObjectName;
            Transform layerTransform = GetOrCreateImageLayerTransform(i, objectName);
            var renderer = GetOrCreateQuadRenderer(layerTransform);

            layerTransform.name = objectName;
            layerTransform.SetSiblingIndex(i);
            Vector2 finalSize = ResolveImageSize(layer, GetClampedBoxSize(layer.BoxSize));
            layerTransform.localScale = Vector3.one;
            layerTransform.localRotation = Quaternion.identity;
            layerTransform.localPosition = BuildAnchoredLocalPosition(layer.Anchor, finalSize, layer.Depth);
            layerTransform.localScale = new Vector3(finalSize.x, finalSize.y, 1f);

            bool visible = layer.Enabled && (layer.Texture != null || layer.Tint.a > 0.001f);
            renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            Material material = GetOrCreateImageMaterial(i);
            ApplyTextureAndColor(material, layer.Texture, layer.Tint, layer.PreserveTextureColors, layer.TextureOpacity);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        for (int i = requiredCount; i < _imageRoot.childCount; i++)
        {
            Transform unusedChild = _imageRoot.GetChild(i);
            unusedChild.gameObject.SetActive(false);
        }
    }

    private Vector3 BuildAnchoredLocalPosition(Vector2 anchor, Vector2 boxSize, float depth)
    {
        Vector2 halfCanvas = GetAvailableCanvasSize() * 0.5f;
        float maxX = Mathf.Max(0f, halfCanvas.x - (boxSize.x * 0.5f));
        float maxY = Mathf.Max(0f, halfCanvas.y - (boxSize.y * 0.5f));
        float x = anchor.x * maxX;
        float y = anchor.y * maxY;
        if (MirrorLayoutX)
        {
            x = -x;
        }
        return new Vector3(x, y, depth);
    }

    private Vector2 ResolveImageSize(ImageLayer layer, Vector2 clampedBoxSize)
    {
        Vector2 finalSize = clampedBoxSize;

        if (!layer.PreserveAspect || layer.Texture == null || layer.Texture.height <= 0)
        {
            return finalSize;
        }

        float textureAspect = (float)layer.Texture.width / layer.Texture.height;
        float boxAspect = clampedBoxSize.x / clampedBoxSize.y;

        if (textureAspect >= boxAspect)
        {
            finalSize.y = clampedBoxSize.x / textureAspect;
        }
        else
        {
            finalSize.x = clampedBoxSize.y * textureAspect;
        }

        return finalSize;
    }

    private Vector2 GetAvailableCanvasSize()
    {
        return new Vector2(
            Mathf.Max(0.1f, LayoutSize.x - (CanvasPadding.x * 2f)),
            Mathf.Max(0.1f, LayoutSize.y - (CanvasPadding.y * 2f)));
    }

    private Vector2 GetClampedBoxSize(Vector2 requestedSize)
    {
        Vector2 available = GetAvailableCanvasSize();
        return new Vector2(
            Mathf.Clamp(requestedSize.x, 0.1f, available.x),
            Mathf.Clamp(requestedSize.y, 0.1f, available.y));
    }

    private TextMeshPro GetOrCreateText(string objectName, out bool createdNow)
    {
        createdNow = false;
        if (_textCache.TryGetValue(objectName, out var cached) && cached != null)
        {
            return cached;
        }

        Transform child = FindOrCreateChild(_textRoot, objectName);
        var tmp = child.GetComponent<TextMeshPro>();
        if (tmp == null)
        {
            tmp = child.gameObject.AddComponent<TextMeshPro>();
            createdNow = true;
        }

        _textCache[objectName] = tmp;
        return tmp;
    }

    private void DeactivateUnusedTextObjects()
    {
        if (_textRoot == null)
        {
            return;
        }

        var configuredNames = new HashSet<string>(StringComparer.Ordinal)
        {
            string.IsNullOrWhiteSpace(Title?.ObjectName) ? "Title" : Title.ObjectName,
            string.IsNullOrWhiteSpace(Subtitle?.ObjectName) ? "Subtitle" : Subtitle.ObjectName,
            string.IsNullOrWhiteSpace(Accent?.ObjectName) ? "Accent" : Accent.ObjectName,
            string.IsNullOrWhiteSpace(Body?.ObjectName) ? "Body" : Body.ObjectName,
            string.IsNullOrWhiteSpace(Caption?.ObjectName) ? "Caption" : Caption.ObjectName
        };

        for (int i = 0; i < _textRoot.childCount; i++)
        {
            Transform child = _textRoot.GetChild(i);
            bool shouldStayVisible = configuredNames.Contains(child.name);
            child.gameObject.SetActive(shouldStayVisible);
        }
    }

    private Transform GetOrCreateImageLayerTransform(int index, string objectName)
    {
        if (index < _imageRoot.childCount)
        {
            Transform existing = _imageRoot.GetChild(index);
            existing.gameObject.SetActive(true);
            existing.name = objectName;
            return existing;
        }

        Transform child = FindOrCreateChild(_imageRoot, objectName);
        child.gameObject.SetActive(true);
        return child;
    }

    private MeshRenderer GetOrCreateQuadRenderer(Transform layerTransform)
    {
        var meshFilter = layerTransform.GetComponent<MeshFilter>();
        var meshRenderer = layerTransform.GetComponent<MeshRenderer>();

        if (meshFilter == null)
        {
            meshFilter = layerTransform.gameObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = GetQuadMesh();
        }

        if (meshRenderer == null)
        {
            meshRenderer = layerTransform.gameObject.AddComponent<MeshRenderer>();
        }

        return meshRenderer;
    }

    private Mesh GetQuadMesh()
    {
        if (_quadMesh != null)
        {
            return _quadMesh;
        }

        _quadMesh = new Mesh
        {
            name = "WallPanelQuad"
        };
        _quadMesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        });
        _quadMesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        });
        _quadMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        _quadMesh.RecalculateNormals();
        return _quadMesh;
    }

    private Material GetOrCreateImageMaterial(int index)
    {
        Material material = _imageMaterials[index];
        if (material != null)
        {
            return material;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Transparent") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Legacy Shaders/Transparent/Diffuse");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        ConfigureTransparentMaterial(material);
        _imageMaterials[index] = material;
        return material;
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        if (material.HasProperty("_CullMode"))
        {
            material.SetFloat("_CullMode", (float)CullMode.Off);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void ApplyTextureAndColor(Material material, Texture texture, Color tint, bool preserveTextureColors, float textureOpacity)
    {
        if (material == null)
        {
            return;
        }

        Texture finalTexture = texture != null ? texture : Texture2D.whiteTexture;

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", finalTexture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", finalTexture);
        }

        Color finalColor = tint;
        if (texture != null && preserveTextureColors)
        {
            finalColor = new Color(1f, 1f, 1f, Mathf.Clamp01(textureOpacity));
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", finalColor);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", finalColor);
        }
    }

    private Bounds GetParentWallLocalBounds()
    {
        Transform wallTransform = transform.parent;
        if (wallTransform == null)
        {
            return new Bounds(new Vector3(0f, 1.25f, 0.04f), new Vector3(4f, 2.5f, 0.2f));
        }

        var meshFilter = wallTransform.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return meshFilter.sharedMesh.bounds;
        }

        var renderer = wallTransform.GetComponent<Renderer>();
        if (renderer != null)
        {
            Vector3 size = wallTransform.InverseTransformVector(renderer.bounds.size);
            Vector3 center = wallTransform.InverseTransformPoint(renderer.bounds.center);
            return new Bounds(center, size);
        }

        return new Bounds(new Vector3(0f, 1.25f, 0.04f), new Vector3(4f, 2.5f, 0.2f));
    }

    private Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private void DisposeImageMaterials()
    {
        for (int i = 0; i < _imageMaterials.Count; i++)
        {
            if (_imageMaterials[i] != null)
            {
                DestroyImmediate(_imageMaterials[i]);
            }
        }

        _imageMaterials.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.42f, 0.78f, 1f, 0.55f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 size = new Vector3(LayoutSize.x * LayoutScale, LayoutSize.y * LayoutScale, 0.01f);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;
    }
}
