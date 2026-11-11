using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Shows a video's subtitles as a full-width bar at the top, centre or bottom of the
/// area the video plays in, following the web player's look. Lives on the VideoPlayer's
/// GameObject, so the bar goes away with the video.
/// </summary>
public class SubtitleDisplay : MonoBehaviour
{
    private VideoPlayer player;
    private List<SubtitleCue> cues;
    private RectTransform area;
    private RectTransform bar;
    private TextMeshProUGUI label;
    private string position;
    private bool stageOnly;
    private string shownText;
    private float shownWidth;

    /// <summary>
    /// Adds subtitles to a video when the game has a track for it. stageOnly hides them
    /// while a trigger or the menu covers the stage, for background videos.
    /// </summary>
    public static SubtitleDisplay Attach(VideoPlayer player, string videoUrl, RectTransform area, string position, bool stageOnly)
    {
        if (player == null || area == null) return null;
        List<SubtitleCue> cues = Subtitles.FindCues(videoUrl);
        if (cues == null) return null;

        SubtitleDisplay display = player.gameObject.GetComponent<SubtitleDisplay>();
        if (display == null) display = player.gameObject.AddComponent<SubtitleDisplay>();
        display.Build(player, cues, area, position, stageOnly);
        return display;
    }

    private void Build(VideoPlayer videoPlayer, List<SubtitleCue> trackCues, RectTransform parent, string where, bool onlyOnStage)
    {
        player = videoPlayer;
        cues = trackCues;
        area = parent;
        position = where;
        stageOnly = onlyOnStage;
        shownText = null;

        if (bar != null) Destroy(bar.gameObject);
        GameObject barGo = new GameObject("Subtitles", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barGo.transform.SetParent(area, false);
        bar = barGo.GetComponent<RectTransform>();
        Image background = barGo.GetComponent<Image>();
        background.color = UiLayoutRules.Instance.subtitleBackground;
        background.raycastTarget = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bar, false);
        VideoFitHelper.StretchFill(textGo.GetComponent<RectTransform>());
        label = textGo.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = UiLayoutRules.Instance.subtitleTextColor;
        label.enableAutoSizing = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        UIManager ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
        if (ui != null)
        {
            TMP_FontAsset font = ui.GetFont(null);
            if (font != null) label.font = font;
        }

        float anchorY = AnchorY(position);
        bar.anchorMin = new Vector2(0f, anchorY);
        bar.anchorMax = new Vector2(1f, anchorY);
        bar.pivot = new Vector2(0.5f, anchorY);
        bar.anchoredPosition = Vector2.zero;
        barGo.SetActive(false);
    }

    private static float AnchorY(string where)
    {
        switch ((where ?? "").Trim().ToLowerInvariant())
        {
            case "top": return 1f;
            case "centre":
            case "center":
            case "middle": return 0.5f;
            default: return 0f;
        }
    }

    private void LateUpdate()
    {
        if (bar == null) return;

        UIManager ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
        bool show = Subtitles.Enabled
            && player != null
            && (!stageOnly || (ui != null && ui.IsStageUiShowing));
        SubtitleCue cue = show ? Subtitles.CueAt(cues, player.time) : null;
        string text = cue != null ? cue.text : null;

        if (string.IsNullOrEmpty(text))
        {
            if (bar.gameObject.activeSelf) bar.gameObject.SetActive(false);
            shownText = null;
            return;
        }

        if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
        if (text != shownText || !Mathf.Approximately(area.rect.width, shownWidth))
            Layout(text);

        // Stay in front of the video and any controls added to the same area later.
        if (bar.GetSiblingIndex() != area.childCount - 1)
            bar.SetAsLastSibling();
    }

    private void Layout(string text)
    {
        shownText = text;
        shownWidth = area.rect.width;

        UiLayoutRules rules = UiLayoutRules.Instance;
        RectTransform canvas = GameManager.Instance != null && GameManager.Instance.uiManager != null
            ? GameManager.Instance.uiManager.transform as RectTransform
            : null;
        float scale = rules.Scale(canvas);
        float screenHeight = canvas != null ? canvas.rect.height : area.rect.height;
        float fontSize = Mathf.Clamp(screenHeight * rules.subtitleFontScreenRatio,
            rules.subtitleFontMin * scale, rules.subtitleFontMax * scale);
        float padX = fontSize * rules.subtitlePaddingXEm;
        float padY = fontSize * rules.subtitlePaddingYEm;

        // Narrow areas keep at least half their width for text.
        padX = Mathf.Min(padX, shownWidth * 0.25f);
        float textWidth = Mathf.Max(1f, shownWidth - padX * 2f);

        label.fontSize = fontSize;
        label.text = text;
        label.margin = new Vector4(padX, padY, padX, padY);
        float textHeight = label.GetPreferredValues(text, textWidth, 0f).y;
        bar.sizeDelta = new Vector2(0f, textHeight + padY * 2f);
    }

    private void OnDestroy()
    {
        if (bar != null) Destroy(bar.gameObject);
    }
}
