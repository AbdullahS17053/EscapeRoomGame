using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent TopPane / LowerPane split driven by stage_layout.
/// Creates the hierarchy at runtime under the main canvas so HUD stays in front.
/// </summary>
public class StageLayoutController : MonoBehaviour
{
    public static StageLayoutController Instance { get; private set; }

    public RectTransform Root { get; private set; }
    public RectTransform TopPane { get; private set; }
    public RectTransform LowerPane { get; private set; }
    public RectTransform OverlayPane { get; private set; }
    public RawImage TopContent { get; private set; }
    public RawImage LowerContent { get; private set; }
    public RawImage OverlayContent { get; private set; }

    private RectTransform buttonContainer;
    private Transform buttonOriginalParent;
    private Vector2 buttonOriginalAnchorMin;
    private Vector2 buttonOriginalAnchorMax;
    private Vector2 buttonOriginalPivot;
    private Vector2 buttonOriginalPosition;
    private Vector2 buttonOriginalSizeDelta;
    private Vector3 buttonOriginalScale;
    private bool buttonOriginalCaptured;

    private string overlayOwner;
    private bool overlayUsedTopWindow;

    public static bool IsVideoTop(StageData stage)
    {
        return stage != null
            && !string.IsNullOrEmpty(stage.stage_layout)
            && stage.stage_layout.Trim().Equals("video_top", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when a JSON bottom position lands in the lower band of a video_top stage.</summary>
    public static bool IsInLowerBand(StageData stage, string position)
    {
        return IsVideoTop(stage)
            && !string.IsNullOrEmpty(position)
            && position.StartsWith("bottom", StringComparison.OrdinalIgnoreCase);
    }

    public static StageLayoutController Ensure(Transform canvasTransform)
    {
        if (Instance != null)
        {
            Instance.EnsureBuilt(canvasTransform);
            return Instance;
        }

        StageLayoutController existing = canvasTransform != null
            ? canvasTransform.GetComponent<StageLayoutController>()
            : null;
        if (existing == null && canvasTransform != null)
            existing = canvasTransform.gameObject.AddComponent<StageLayoutController>();
        if (existing == null)
        {
            GameObject host = new GameObject("StageLayoutHost");
            existing = host.AddComponent<StageLayoutController>();
        }

        existing.EnsureBuilt(canvasTransform);
        return existing;
    }

    private void Awake()
    {
        Instance = this;
        EnsureBuilt(transform);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void EnsureBuilt(Transform canvasTransform)
    {
        if (canvasTransform == null) canvasTransform = transform;
        CaptureButtonContainer(canvasTransform);

        if (Root != null) return;

        GameObject rootGo = new GameObject("StageLayout", typeof(RectTransform));
        rootGo.transform.SetParent(canvasTransform, false);
        rootGo.transform.SetAsFirstSibling();
        Root = rootGo.GetComponent<RectTransform>();
        VideoFitHelper.StretchFill(Root);
        rootGo.AddComponent<CanvasRenderer>();

        TopPane = CreateMaskedPane("TopPane", Root, new Color(0f, 0f, 0f, 1f), out RawImage topContent);
        TopContent = topContent;

        LowerPane = CreateMaskedPane("LowerPane", Root, new Color(0f, 0f, 0f, 1f), out RawImage lowerContent);
        LowerContent = lowerContent;

        OverlayPane = CreateMaskedPane("OverlayPane", Root, new Color(0f, 0f, 0f, 1f), out RawImage overlayContent);
        OverlayContent = overlayContent;
        OverlayPane.gameObject.SetActive(false);

        ResetToMenu();
    }

    private void CaptureButtonContainer(Transform canvasTransform)
    {
        if (buttonOriginalCaptured) return;

        UIManager ui = canvasTransform != null ? canvasTransform.GetComponent<UIManager>() : null;
        if (ui != null && ui.buttonContainer != null)
            buttonContainer = ui.buttonContainer as RectTransform ?? ui.buttonContainer.GetComponent<RectTransform>();

        if (buttonContainer == null) return;

        buttonOriginalParent = buttonContainer.parent;
        buttonOriginalAnchorMin = buttonContainer.anchorMin;
        buttonOriginalAnchorMax = buttonContainer.anchorMax;
        buttonOriginalPivot = buttonContainer.pivot;
        buttonOriginalPosition = buttonContainer.anchoredPosition;
        buttonOriginalSizeDelta = buttonContainer.sizeDelta;
        buttonOriginalScale = buttonContainer.localScale;
        buttonOriginalCaptured = true;
    }

    private static RectTransform CreateMaskedPane(string name, RectTransform parent, Color bg, out RawImage content)
    {
        GameObject paneGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        paneGo.transform.SetParent(parent, false);
        RectTransform pane = paneGo.GetComponent<RectTransform>();
        VideoFitHelper.StretchFill(pane);

        Image bgImage = paneGo.GetComponent<Image>();
        bgImage.color = bg;
        bgImage.raycastTarget = false;

        Mask mask = paneGo.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        contentGo.transform.SetParent(pane, false);
        RawImage raw = contentGo.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        VideoFitHelper.StretchFill(raw.rectTransform);
        content = raw;
        contentGo.SetActive(false);

        return pane;
    }

    public void ResetToMenu()
    {
        HideOverlay();
        ClearMedia();
        if (Root != null) Root.gameObject.SetActive(false);
        RestoreButtonContainer();
        if (buttonContainer != null)
            buttonContainer.localScale = buttonOriginalCaptured ? buttonOriginalScale : Vector3.one;
    }

    public void ApplyPaneLayout(StageData stage)
    {
        if (Root == null) EnsureBuilt(transform);
        if (Root == null) return;

        Root.gameObject.SetActive(true);
        HideOverlay();
        Canvas.ForceUpdateCanvases();

        if (!IsVideoTop(stage))
        {
            VideoFitHelper.StretchFill(TopPane);
            TopPane.gameObject.SetActive(true);
            LowerPane.gameObject.SetActive(false);
            return;
        }

        float width = Root.rect.width;
        float height = Root.rect.height;
        if (width < 1f) width = 2160f;
        if (height < 1f) height = 1080f;

        float minLowerHeight = UiLayoutRules.Instance.minLowerBandHeight;
        float topHeight = width * 9f / 16f;
        if (topHeight > height - minLowerHeight)
            topHeight = Mathf.Max(height * 0.55f, height - minLowerHeight);

        TopPane.gameObject.SetActive(true);
        TopPane.anchorMin = new Vector2(0f, 1f);
        TopPane.anchorMax = new Vector2(1f, 1f);
        TopPane.pivot = new Vector2(0.5f, 1f);
        TopPane.anchoredPosition = Vector2.zero;
        TopPane.sizeDelta = new Vector2(0f, topHeight);

        LowerPane.gameObject.SetActive(true);
        LowerPane.anchorMin = Vector2.zero;
        LowerPane.anchorMax = new Vector2(1f, 1f);
        LowerPane.pivot = new Vector2(0.5f, 0f);
        LowerPane.offsetMin = Vector2.zero;
        LowerPane.offsetMax = new Vector2(0f, -topHeight);
    }

    /// <summary>
    /// Sizes and places the stage's buttons in rows, centered, wrapping when a row is full.
    /// button_scale and button offsets come from the JSON; on video_top the row is
    /// centered in the lower band and keeps clear of volume controls placed there.
    /// </summary>
    public void PlaceButtonContainer(StageData stage)
    {
        CaptureButtonContainer(transform);
        if (buttonContainer == null) return;

        RestoreButtonContainer();
        SetContainerLayoutEnabled(false);

        bool videoTop = IsVideoTop(stage) && LowerPane != null && LowerPane.gameObject.activeSelf;

        buttonContainer.anchorMin = Vector2.zero;
        buttonContainer.anchorMax = new Vector2(1f, 0f);
        buttonContainer.pivot = new Vector2(0.5f, 0f);
        buttonContainer.anchoredPosition = Vector2.zero;
        buttonContainer.sizeDelta = Vector2.zero;
        buttonContainer.localScale = Vector3.one;
        if (videoTop) buttonContainer.SetAsLastSibling();

        List<RectTransform> buttons = new List<RectTransform>();
        foreach (Transform child in buttonContainer)
        {
            if (child.gameObject.activeSelf)
                buttons.Add(child as RectTransform);
        }
        if (buttons.Count == 0 || stage == null) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(Root);
        float areaHeight = videoTop ? LowerPane.rect.height : Root.rect.height;

        float reservedLeft = 0f;
        float reservedRight = 0f;
        if (stage.show_volume_controls && IsInLowerBand(stage, stage.volume_controls_position))
        {
            float reserved = Mathf.Round(rules.volumeReservedWidth * GetVideoControlSize()) * scale;
            if (stage.volume_controls_position.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)
                reservedLeft = reserved;
            else
                reservedRight = reserved;
        }

        float rowWidth = Root.rect.width - reservedLeft - reservedRight;
        UiLayoutRules.ButtonMetrics metrics = rules.GetButtonMetrics(
            buttons.Count, stage.button_scale, areaHeight / scale, rowWidth / scale);

        float gap = metrics.gap * scale;
        List<List<RectTransform>> rows = new List<List<RectTransform>> { new List<RectTransform>() };
        float usedWidth = 0f;
        foreach (RectTransform button in buttons)
        {
            SizeButton(button, metrics, scale);
            float width = button.sizeDelta.x;
            bool rowHasRoom = rows[rows.Count - 1].Count == 0 || usedWidth + gap + width <= rowWidth;
            if (!rowHasRoom)
            {
                rows.Add(new List<RectTransform>());
                usedWidth = 0f;
            }
            usedWidth += (rows[rows.Count - 1].Count > 0 ? gap : 0f) + width;
            rows[rows.Count - 1].Add(button);
        }

        float totalHeight = gap * (rows.Count - 1);
        foreach (List<RectTransform> row in rows)
            totalHeight += GetRowHeight(row);

        float bottom = videoTop
            ? (areaHeight - totalHeight) * 0.5f
            : rules.buttonFullscreenBottom * scale;

        Vector2 offset = new Vector2(stage.button_offset_x, stage.button_offset_y) * scale;
        float rowTop = bottom + totalHeight + offset.y;
        float centerX = reservedLeft + rowWidth * 0.5f + offset.x;

        foreach (List<RectTransform> row in rows)
        {
            float width = gap * (row.Count - 1);
            foreach (RectTransform button in row)
                width += button.sizeDelta.x;

            float rowHeight = GetRowHeight(row);
            float x = centerX - width * 0.5f;
            foreach (RectTransform button in row)
            {
                button.anchoredPosition = new Vector2(x, rowTop - (rowHeight + button.sizeDelta.y) * 0.5f);
                x += button.sizeDelta.x + gap;
            }
            rowTop -= rowHeight + gap;
        }
    }

    private static float GetRowHeight(List<RectTransform> row)
    {
        float height = 0f;
        foreach (RectTransform button in row)
            height = Mathf.Max(height, button.sizeDelta.y);
        return height;
    }

    private static float GetVideoControlSize()
    {
        GameConfig config = GameManager.Instance?.currentGameData?.game;
        return config != null && config.video_controls_size > 0f ? config.video_controls_size : 1f;
    }

    /// <summary>
    /// Fits one button to its label: as wide as the text plus padding, never narrower than
    /// the minimum, and wrapping onto extra lines once it reaches the maximum width.
    /// </summary>
    private static void SizeButton(RectTransform button, UiLayoutRules.ButtonMetrics metrics, float scale)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        float fontSize = metrics.fontSize * scale;
        float paddingX = metrics.paddingX * scale;
        float paddingY = metrics.paddingY * scale;
        float maxWidth = metrics.maxWidth * scale;
        float lineHeight = fontSize * rules.buttonLineHeightRatio;

        float textWidth = 0f;
        int lines = 1;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.enableAutoSizing = false;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;

            float maxTextWidth = Mathf.Max(1f, maxWidth - paddingX * 2f);
            Vector2 singleLine = label.GetPreferredValues(label.text);
            textWidth = singleLine.x;
            if (textWidth > maxTextWidth)
            {
                Vector2 wrapped = label.GetPreferredValues(label.text, maxTextWidth, 0f);
                textWidth = Mathf.Min(wrapped.x, maxTextWidth);
                lines = Mathf.Max(1, Mathf.RoundToInt(wrapped.y / Mathf.Max(1f, singleLine.y)));
            }
            label.margin = new Vector4(paddingX, 0f, paddingX, 0f);
        }

        button.anchorMin = Vector2.zero;
        button.anchorMax = Vector2.zero;
        button.pivot = Vector2.zero;
        button.localScale = Vector3.one;
        button.sizeDelta = new Vector2(
            Mathf.Clamp(textWidth + paddingX * 2f, metrics.minWidth * scale, maxWidth),
            lineHeight * lines + paddingY * 2f);
    }

    public void RestoreButtonContainer()
    {
        if (buttonContainer == null || !buttonOriginalCaptured) return;
        if (buttonOriginalParent != null)
            buttonContainer.SetParent(buttonOriginalParent, false);
        buttonContainer.anchorMin = buttonOriginalAnchorMin;
        buttonContainer.anchorMax = buttonOriginalAnchorMax;
        buttonContainer.pivot = buttonOriginalPivot;
        buttonContainer.anchoredPosition = buttonOriginalPosition;
        buttonContainer.sizeDelta = buttonOriginalSizeDelta;
        buttonContainer.localScale = buttonOriginalScale;
        SetContainerLayoutEnabled(true);
    }

    /// <summary>
    /// The menu lays buttons out with the container's own components. Stages place them
    /// directly, which needs those components and the panel background switched off.
    /// </summary>
    private void SetContainerLayoutEnabled(bool enabled)
    {
        HorizontalLayoutGroup group = buttonContainer.GetComponent<HorizontalLayoutGroup>();
        if (group != null) group.enabled = enabled;
        ContentSizeFitter fitter = buttonContainer.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = enabled;
        Image panel = buttonContainer.GetComponent<Image>();
        if (panel != null) panel.enabled = enabled;
    }

    public void ClearMedia()
    {
        ClearPane(TopContent);
        ClearPane(LowerContent);
        ClearPane(OverlayContent);
        if (OverlayPane != null) OverlayPane.gameObject.SetActive(false);
        overlayOwner = null;
        overlayUsedTopWindow = false;
    }

    public void ShowContent(RawImage image, Texture texture, string fitMode, float aspect)
    {
        if (image == null) return;
        image.gameObject.SetActive(texture != null);
        image.texture = texture;
        if (texture != null)
            VideoFitHelper.ApplyFit(image, fitMode, aspect);
    }

    public void HideContent(RawImage image)
    {
        ClearPane(image);
    }

    public void ShowOverlay(Texture texture, string display, string fitMode, float aspect, string owner)
    {
        overlayOwner = owner;
        overlayUsedTopWindow = VideoFitHelper.IsTopWindow(display);

        if (overlayUsedTopWindow)
        {
            if (OverlayPane != null) OverlayPane.gameObject.SetActive(false);
            ShowContent(TopContent, texture, fitMode, aspect);
        }
        else
        {
            if (OverlayPane != null)
            {
                OverlayPane.gameObject.SetActive(true);
                OverlayPane.SetAsLastSibling();
                VideoFitHelper.StretchFill(OverlayPane);
            }
            ShowContent(OverlayContent, texture, fitMode, aspect);
        }
    }

    public void HideOverlay(string owner = null)
    {
        if (!string.IsNullOrEmpty(owner) && overlayOwner != owner) return;

        if (OverlayPane != null) OverlayPane.gameObject.SetActive(false);
        ClearPane(OverlayContent);
        if (overlayUsedTopWindow)
            ClearPane(TopContent);
        overlayUsedTopWindow = false;
        overlayOwner = null;
    }

    private static void ClearPane(RawImage image)
    {
        if (image == null) return;
        image.texture = null;
        image.gameObject.SetActive(false);
    }
}
