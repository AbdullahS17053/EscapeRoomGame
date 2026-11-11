using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zoom controls for stages with show_zoom_controls. Like the web player, the whole stage
/// (backgrounds, title, timer, buttons) zooms around the screen centre and pans by dragging,
/// while trigger media, dialogs and the zoom buttons themselves stay unscaled.
/// Zoom resets on every stage change.
///
/// Layout code keeps writing unzoomed positions to the stage elements. Each frame, after
/// that, this applies the zoom on top; a value it did not write is taken as the new base.
/// </summary>
[DefaultExecutionOrder(10000)]
public class StageZoomController : MonoBehaviour
{
    public static StageZoomController Instance { get; private set; }

    private class Target
    {
        public Vector3 basePosition;
        public Vector3 baseScale;
        public Vector3 appliedPosition;
        public Vector3 appliedScale;
        public bool applied;
    }

    private const float Tolerance = 0.01f;

    private UIManager ui;
    private RectTransform canvas;
    private RectTransform controls;
    private RectTransform zoomInButton;
    private RectTransform zoomOutButton;
    private RectTransform resetButton;
    private GameObject dragSurface;
    private StageZoomDrag dragRelay;

    private float zoom = 1f;
    private Vector2 pan;
    private StageData zoomStage;
    private float controlsTop;

    private readonly Dictionary<RectTransform, Target> targets = new Dictionary<RectTransform, Target>();
    private readonly List<RectTransform> currentTargets = new List<RectTransform>();
    private readonly List<RectTransform> staleTargets = new List<RectTransform>();

    public static StageZoomController Ensure(UIManager uiManager)
    {
        if (Instance != null) return Instance;
        if (uiManager == null) return null;

        GameObject go = new GameObject("ZoomControls", typeof(RectTransform));
        go.transform.SetParent(uiManager.transform, false);
        StageZoomController controller = go.AddComponent<StageZoomController>();
        controller.Build(uiManager);
        return controller;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build(UIManager uiManager)
    {
        ui = uiManager;
        canvas = ui.transform as RectTransform;
        // The controller stays active so LateUpdate keeps running; only the buttons hide.
        VideoFitHelper.StretchFill(transform as RectTransform);
        GameObject buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(transform, false);
        controls = buttons.GetComponent<RectTransform>();
        controls.anchorMin = Vector2.one;
        controls.anchorMax = Vector2.one;
        controls.pivot = Vector2.one;

        zoomInButton = CreateButton("ZoomIn", "+", ZoomIn);
        zoomOutButton = CreateButton("ZoomOut", "−", ZoomOut);
        resetButton = CreateButton("ZoomReset", "Reset", ResetZoom);

        // Catches drags that start on empty background, which has no raycast target.
        dragSurface = new GameObject("ZoomDragSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragSurface.transform.SetParent(canvas, false);
        VideoFitHelper.StretchFill(dragSurface.GetComponent<RectTransform>());
        Image surface = dragSurface.GetComponent<Image>();
        surface.color = new Color(0f, 0f, 0f, 0f);
        surface.raycastTarget = true;
        dragSurface.SetActive(false);

        // Drags bubble up to the canvas from anything without its own drag handler, buttons
        // included. The relay is only enabled while zoomed, so taps elsewhere are unchanged.
        dragRelay = canvas.gameObject.AddComponent<StageZoomDrag>();
        dragRelay.owner = this;
        dragRelay.enabled = false;

        controls.gameObject.SetActive(false);
    }

    private RectTransform CreateButton(string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(controls, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        VideoFitHelper.StretchFill(textGo.GetComponent<RectTransform>());
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return rect;
    }

    public void ZoomIn()
    {
        float max = Mathf.Max(1f, UiLayoutRules.Instance.zoomMax);
        zoom = Mathf.Min(max, Round(zoom + UiLayoutRules.Instance.zoomStep));
    }

    public void ZoomOut()
    {
        zoom = Mathf.Max(1f, Round(zoom - UiLayoutRules.Instance.zoomStep));
        ClampPan();
    }

    public void ResetZoom()
    {
        zoom = 1f;
        pan = Vector2.zero;
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    /// <summary>Pan by a screen-pixel delta while zoomed in.</summary>
    public void Pan(Vector2 screenDelta)
    {
        if (zoom <= 1f) return;
        Canvas root = canvas != null ? canvas.GetComponentInParent<Canvas>() : null;
        float factor = root != null && root.scaleFactor > 0f ? root.scaleFactor : 1f;
        pan += screenDelta / factor;
        ClampPan();
    }

    /// <summary>
    /// Keeps the zoomed stage covering the whole screen: it can move at most as far as it
    /// has grown past each edge, so no empty space ever shows beside it.
    /// </summary>
    private void ClampPan()
    {
        if (canvas == null) return;
        Vector2 size = canvas.rect.size;
        float spareX = size.x * (zoom - 1f) * 0.5f;
        float spareY = size.y * (zoom - 1f) * 0.5f;
        pan = new Vector2(
            Mathf.Clamp(pan.x, -spareX, spareX),
            Mathf.Clamp(pan.y, -spareY, spareY));
    }

    /// <summary>
    /// Styles and places the buttons for a stage. Called once the stage UI is laid out;
    /// top is how far down the top-right corner is already taken, in canvas units.
    /// </summary>
    public void Layout(float top)
    {
        controlsTop = top;
        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(canvas);
        GameConfig config = GameManager.Instance?.currentGameData?.game;
        float size = config != null && config.video_controls_size > 0f ? config.video_controls_size : 1f;

        Color color = rules.zoomDefaultColor;
        if (config != null && !string.IsNullOrEmpty(config.video_controls_color))
            ColorUtility.TryParseHtmlString(config.video_controls_color, out color);
        Color textColor = Color.white;
        if (config != null && !string.IsNullOrEmpty(config.video_controls_text_color))
            ColorUtility.TryParseHtmlString(config.video_controls_text_color, out textColor);
        TMP_FontAsset font = ui.GetFont(config != null ? config.video_controls_font : null);
        Sprite sprite = RoundedSprite.Get(Mathf.Round(rules.zoomCornerRadius * size) * scale);

        float width = Mathf.Round(rules.zoomButtonWidth * size) * scale;
        float gap = rules.zoomGap * scale;
        float y = 0f;
        y = StyleButton(zoomInButton, rules.zoomFontSize, rules.zoomPaddingY, y, width, size, scale, color, textColor, font, sprite) + gap;
        y = StyleButton(zoomOutButton, rules.zoomFontSize, rules.zoomPaddingY, y, width, size, scale, color, textColor, font, sprite) + gap;
        y = StyleButton(resetButton, rules.zoomResetFontSize, rules.zoomResetPaddingY, y, width, size, scale, color, textColor, font, sprite);

        float inset = rules.zoomInset * scale;
        controls.sizeDelta = new Vector2(width, y);
        controls.anchoredPosition = new Vector2(-inset, -Mathf.Max(inset, top));
    }

    private static float StyleButton(RectTransform button, float fontSize, float paddingY, float y, float width,
        float size, float scale, Color color, Color textColor, TMP_FontAsset font, Sprite sprite)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        float fontPx = Mathf.Round(fontSize * size) * scale;
        float height = fontPx * rules.zoomLineHeightRatio + Mathf.Round(paddingY * size) * scale * 2f;

        button.sizeDelta = new Vector2(width, height);
        button.anchoredPosition = new Vector2(0f, -y);

        Image image = button.GetComponent<Image>();
        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            if (font != null) label.font = font;
            label.fontSize = fontPx;
            label.color = textColor;
        }
        return y + height;
    }

    private void LateUpdate()
    {
        if (ui == null || canvas == null) return;

        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        if (stage != zoomStage)
        {
            zoomStage = stage;
            ResetZoom();
        }

        bool active = stage != null && stage.show_zoom_controls && ui.IsStageUiShowing;
        bool zoomed = active && zoom > 1f;

        if (controls.gameObject.activeSelf != active) controls.gameObject.SetActive(active);
        if (dragSurface.activeSelf != zoomed) dragSurface.SetActive(zoomed);
        dragRelay.enabled = zoomed;

        currentTargets.Clear();
        if (active) ui.GetZoomTargets(currentTargets);

        if (zoomed) ClampPan();
        float z = zoomed ? zoom : 1f;
        Vector2 offset = zoomed ? pan : Vector2.zero;
        foreach (RectTransform rect in currentTargets)
        {
            if (rect != null) Apply(rect, z, offset);
        }

        // Anything no longer zoomed goes back to where the layout put it.
        staleTargets.Clear();
        foreach (KeyValuePair<RectTransform, Target> pair in targets)
        {
            if (pair.Key == null || !currentTargets.Contains(pair.Key))
                staleTargets.Add(pair.Key);
        }
        foreach (RectTransform rect in staleTargets)
        {
            if (rect != null) Apply(rect, 1f, Vector2.zero);
            targets.Remove(rect);
        }

        if (active) KeepAbove(currentTargets);
        if (zoomed) PlaceDragSurface();
    }

    private void Apply(RectTransform rect, float z, Vector2 offset)
    {
        if (!targets.TryGetValue(rect, out Target target))
        {
            target = new Target();
            targets[rect] = target;
        }

        Vector3 position = rect.localPosition;
        Vector3 scale = rect.localScale;
        if (!target.applied || !Near(position, target.appliedPosition)) target.basePosition = position;
        if (!target.applied || !Near(scale, target.appliedScale)) target.baseScale = scale;

        if (Mathf.Approximately(z, 1f) && offset == Vector2.zero)
        {
            if (target.applied)
            {
                rect.localPosition = target.basePosition;
                rect.localScale = target.baseScale;
                target.applied = false;
            }
            return;
        }

        RectTransform parent = rect.parent as RectTransform;
        if (parent == null) return;

        Vector3 center = parent.InverseTransformPoint(canvas.TransformPoint(canvas.rect.center));
        Vector3 shift = parent.InverseTransformVector(canvas.TransformVector(offset));
        Vector3 placed = center + (target.basePosition - center) * z + shift;
        placed.z = target.basePosition.z;
        Vector3 scaled = new Vector3(target.baseScale.x * z, target.baseScale.y * z, target.baseScale.z);

        rect.localPosition = placed;
        rect.localScale = scaled;
        target.appliedPosition = rect.localPosition;
        target.appliedScale = rect.localScale;
        target.applied = true;
    }

    private static bool Near(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) < Tolerance
            && Mathf.Abs(a.y - b.y) < Tolerance
            && Mathf.Abs(a.z - b.z) < Tolerance;
    }

    /// <summary>Zoomed elements can grow under the buttons, so the buttons stay in front.</summary>
    private void KeepAbove(List<RectTransform> stageElements)
    {
        int highest = -1;
        foreach (RectTransform rect in stageElements)
        {
            Transform child = TopLevelChild(rect);
            if (child != null && child != transform) highest = Mathf.Max(highest, child.GetSiblingIndex());
        }
        if (highest > transform.GetSiblingIndex())
            transform.SetSiblingIndex(highest);
    }

    /// <summary>The drag surface sits just in front of the stage background.</summary>
    private void PlaceDragSurface()
    {
        StageLayoutController layout = StageLayoutController.Instance;
        if (layout == null || layout.Root == null || layout.Root.parent != canvas) return;
        int index = layout.Root.GetSiblingIndex() + 1;
        if (dragSurface.transform.GetSiblingIndex() != index)
            dragSurface.transform.SetSiblingIndex(index);
    }

    private Transform TopLevelChild(Transform t)
    {
        while (t != null && t.parent != canvas) t = t.parent;
        return t;
    }
}
