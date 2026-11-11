using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Layout rules the game JSON does not carry. The JSON always wins: these values only
/// fill gaps where a game has no field of its own, and none of them are game-specific.
/// All sizes are pixels at designWidth, converted to canvas units with Scale().
/// Editable on the UiLayoutRules asset in Resources.
/// </summary>
[CreateAssetMenu(fileName = "UiLayoutRules", menuName = "Escape Room/UI Layout Rules")]
public class UiLayoutRules : ScriptableObject
{
    [Serializable]
    public class FontAlias
    {
        public string jsonName;
        public string fontAsset;
    }

    /// <summary>A background image with a printed photo slot, matched by file name.</summary>
    [Serializable]
    public class PhotoOverlayRule
    {
        public string backgroundFileName;
        public Vector2 backgroundSize = new Vector2(1920f, 1080f);
        [Tooltip("Photo slot in background pixels, measured from the top-left corner.")]
        public Rect photoRect = new Rect(64f, 350f, 960f, 540f);
    }

    public struct ButtonMetrics
    {
        public float fontSize;
        public float paddingX;
        public float paddingY;
        public float gap;
        public float minWidth;
        public float maxWidth;
    }

    [Header("Canvas")]
    [Tooltip("Width in pixels that JSON sizes such as title_text_font_size are authored against.")]
    public float designWidth = 1600f;

    [Header("Trigger Buttons")]
    public float buttonFontSize = 38f;
    public float buttonPaddingXRatio = 1.4f;
    public float buttonPaddingYRatio = 0.95f;
    public float buttonGapRatio = 0.5f;
    public float buttonMinWidthRatio = 6f;
    [Range(0.1f, 1f)] public float buttonMaxRowWidthRatio = 0.85f;
    public float buttonLineHeightRatio = 1.5f;
    public float buttonFullscreenBottom = 96f;
    public float buttonCornerRadius = 8f;

    [Header("Stage Layout")]
    [Tooltip("Canvas units kept for buttons when a video_top window would otherwise fill the screen.")]
    public float minLowerBandHeight = 200f;

    [Header("Title")]
    public float titleLineGap = 8f;
    public float titleLineHeightRatio = 1.5f;
    public float titleSideMargin = 32f;
    public float titleCornerRadius = 8f;
    [Range(0.1f, 1f)] public float teamNameFontRatio = 0.7f;

    [Header("Team Photo")]
    [Range(0.1f, 1f)] public float photoColumnRatio = 0.45f;
    [Range(0.1f, 1f)] public float photoMaxHeightRatio = 0.65f;
    public float photoColumnGap = 32f;
    public List<PhotoOverlayRule> photoOverlays = new List<PhotoOverlayRule>
    {
        new PhotoOverlayRule { backgroundFileName = "SPECTRUM_Cunard_News_Unity_Background_V3.png" }
    };

    [Header("Edges")]
    public float edgeInset = 32f;

    [Header("Timer And Score")]
    [Tooltip("Used for the score when the stage has no timer position to follow.")]
    public string defaultHudPosition = "bottom-left";
    public string hudFontName = "monospace";
    public float hudInset = 16f;
    public float hudGap = 8f;
    public float hudFontSize = 20f;
    public float hudLineHeight = 28f;
    public float hudPaddingX = 16f;
    public float hudPaddingY = 8f;
    public float hudCornerRadius = 8f;

    [Header("Volume Controls")]
    public float volumeWidth = 100f;
    public float volumeBandWidth = 80f;
    public float volumePaddingY = 6f;
    public float volumeBandPaddingY = 4f;
    public float volumeFontSize = 16f;
    public float volumeBarHeight = 20f;
    public float volumeBarFontSize = 13f;
    public float volumeGap = 4f;
    public float volumeCornerRadius = 8f;
    public float volumeBarBorder = 2f;
    public float volumeReservedWidth = 150f;

    [Header("Video Controls")]
    public float videoControlFontSize = 12f;
    public float videoControlHeight = 32f;
    public float videoControlPaddingX = 16f;
    public float videoControlGap = 8f;
    public float videoControlCornerRadius = 4f;
    public float overlayControlBottom = 32f;
    public float stageControlBottom = 16f;

    [Header("Zoom Controls")]
    [Tooltip("Zoom added or removed by one press of + or −.")]
    public float zoomStep = 0.25f;
    public float zoomMax = 4f;
    public float zoomInset = 16f;
    public float zoomGap = 8f;
    public float zoomButtonWidth = 56f;
    public float zoomFontSize = 22f;
    public float zoomPaddingY = 6f;
    public float zoomResetFontSize = 13f;
    public float zoomResetPaddingY = 8f;
    public float zoomLineHeightRatio = 1.5f;
    public float zoomCornerRadius = 8f;
    [Tooltip("Used only when the game JSON has no video_controls_color.")]
    public Color zoomDefaultColor = new Color(59f / 255f, 130f / 255f, 246f / 255f);

    [Header("Subtitles")]
    [Tooltip("Font size as a share of screen height, kept between the min and max below.")]
    public float subtitleFontScreenRatio = 0.026f;
    public float subtitleFontMin = 14f;
    public float subtitleFontMax = 34f;
    [Tooltip("Padding in multiples of the font size.")]
    public float subtitlePaddingXEm = 5.5f;
    public float subtitlePaddingYEm = 0.45f;
    public Color subtitleBackground = new Color(0f, 0f, 0f, 0.74f);
    public Color subtitleTextColor = Color.white;
    [Tooltip("Used for a toggle_subtitles button that sets no colours of its own.")]
    public Color subtitleToggleOnColor = new Color(22f / 255f, 163f / 255f, 74f / 255f);
    public Color subtitleToggleOffColor = new Color(220f / 255f, 38f / 255f, 38f / 255f);
    public string subtitleToggleOnText = "SUBTITLES ON";
    public string subtitleToggleOffText = "SUBTITLES OFF";
    [Tooltip("Where trigger media shows subtitles when the trigger sets no subtitle_position.")]
    public string subtitleTriggerDefaultPosition = "top";
    [Tooltip("Where background videos show subtitles when the game sets no subtitle_position.")]
    public string subtitleStageDefaultPosition = "below";

    [Header("Stage Music")]
    [Tooltip("Share of the stage music volume left while a trigger video plays, unless the trigger sets prevent_stage_audio_dip.")]
    [Range(0f, 1f)] public float videoOverlayMusicLevel = 0.05f;
    [Tooltip("Fade-out for music carried over by allow_previous_stage_audio_to_continue, used when the incoming stage sets no stage_audio_fade_out_duration.")]
    public float carriedMusicFadeOutMs = 1000f;

    [Header("On-Screen Keyboard")]
    public float keyboardPanelWidth = 896f;
    public float keyboardMargin = 32f;
    public float keyboardPadding = 32f;
    public float keyboardTitleHeight = 36f;
    public float keyboardTitleFontSize = 30f;
    public float keyboardInputHeight = 80f;
    public float keyboardInputFontSize = 36f;
    public float keyboardKeySize = 64f;
    public float keyboardKeyGap = 8f;
    public float keyboardRowGap = 12f;
    public float keyboardKeyFontSize = 24f;
    public float keyboardActionHeight = 64f;
    public float keyboardActionGap = 12f;
    public float keyboardActionFontSize = 20f;
    public float keyboardSectionGap = 24f;
    public float keyboardInputGap = 32f;
    public float keyboardPanelCornerRadius = 16f;
    public float keyboardKeyCornerRadius = 6f;
    public string keyboardPlaceholder = "Type code...";
    public Color keyboardBackdropColor = new Color(0f, 0f, 0f, 0.8f);
    public Color keyboardPanelColor = new Color(15f / 255f, 23f / 255f, 42f / 255f);
    public Color keyboardInputColor = new Color(30f / 255f, 41f / 255f, 59f / 255f);
    public Color keyboardPlaceholderColor = new Color(148f / 255f, 163f / 255f, 184f / 255f);
    [Tooltip("Used only when the game JSON has no default_button_color.")]
    public Color keyboardKeyColor = new Color(37f / 255f, 99f / 255f, 235f / 255f);
    public Color keyboardSubmitColor = new Color(22f / 255f, 163f / 255f, 74f / 255f);

    [Header("Fonts")]
    [Tooltip("Maps CSS font names in the JSON to font assets. sans-serif is a browser keyword, not a font.")]
    public List<FontAlias> fontAliases = new List<FontAlias>
    {
        new FontAlias { jsonName = "sans-serif", fontAsset = "Arial" }
    };

    private static UiLayoutRules instance;

    public static UiLayoutRules Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<UiLayoutRules>("UiLayoutRules");
                if (instance == null) instance = CreateInstance<UiLayoutRules>();
            }
            return instance;
        }
    }

    /// <summary>Canvas units per design pixel.</summary>
    public float Scale(RectTransform canvas)
    {
        if (canvas == null || canvas.rect.width < 1f || designWidth < 1f) return 1f;
        return canvas.rect.width / designWidth;
    }

    /// <summary>
    /// Button sizes in design pixels. Everything grows from one font size, then shrinks
    /// together when the buttons stacked would not fit the available height, so any
    /// number of buttons works without special cases.
    /// </summary>
    public ButtonMetrics GetButtonMetrics(int count, float buttonScale, float availableHeight, float rowWidth)
    {
        float size = buttonFontSize * (buttonScale > 0f ? buttonScale : 1f);
        float paddingX = Mathf.Round(size * buttonPaddingXRatio);
        float paddingY = Mathf.Round(size * buttonPaddingYRatio);
        float gap = Mathf.Round(size * buttonGapRatio);
        float minWidth = Mathf.Round(size * buttonMinWidthRatio);

        float stacked = count * (size + 2f * paddingY + 2f) + (count - 1) * gap;
        float fit = stacked > availableHeight && availableHeight > 0f ? availableHeight / stacked : 1f;

        return new ButtonMetrics
        {
            fontSize = Mathf.Round(size * fit),
            paddingX = Mathf.Round(paddingX * fit),
            paddingY = Mathf.Round(paddingY * fit),
            gap = gap,
            minWidth = Mathf.Round(minWidth * fit),
            maxWidth = Mathf.Round(rowWidth * buttonMaxRowWidthRatio)
        };
    }

    /// <summary>Resolves a JSON font name to a font asset name. Empty means sans-serif.</summary>
    public string ResolveFontName(string jsonName)
    {
        string name = string.IsNullOrWhiteSpace(jsonName) ? "sans-serif" : jsonName.Trim();
        foreach (FontAlias alias in fontAliases)
        {
            if (alias != null && name.Equals(alias.jsonName, StringComparison.OrdinalIgnoreCase))
                return alias.fontAsset;
        }
        return name;
    }

    /// <summary>
    /// The printed photo slot for this stage, or null. Matched by background file name,
    /// never by stage number, so it holds wherever that background is reused.
    /// </summary>
    public PhotoOverlayRule FindPhotoOverlay(StageData stage)
    {
        if (stage == null || !stage.show_team_photo) return null;
        if (!string.IsNullOrEmpty(stage.background_video_url)) return null;
        if (string.IsNullOrEmpty(stage.background_image_url)) return null;

        string file = stage.background_image_url.Split('?', '#')[0];
        foreach (PhotoOverlayRule rule in photoOverlays)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.backgroundFileName)
                && file.EndsWith(rule.backgroundFileName, StringComparison.OrdinalIgnoreCase))
                return rule;
        }
        return null;
    }

    /// <summary>Border drawn for a JSON video_controls_style. Width is in design pixels.</summary>
    public static float GetControlBorder(string style, Color textColor, out Color color)
    {
        color = textColor;
        switch ((style ?? "").Trim().ToLowerInvariant())
        {
            case "glass":
                color = new Color(1f, 1f, 1f, 0.2f);
                return 1f;
            case "minimal": return 1f;
            case "outline": return 2f;
            case "retro": return 4f;
            default: return 0f;
        }
    }
}
