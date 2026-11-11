using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Normalizes StageData and TriggerData into a common shape for UI rendering.
/// This is the single source of truth for what the UI should display at any moment.
/// </summary>
public class UIContext
{
    // Text
    public string title;       // Stage: stage.title,       Trigger: trigger.text_overlay
    public string subtitle;    // Stage: stage.title_line2, Trigger: null (no field)
    public string titleFontFamily; // Stage: stage.title_text_font_family

    // Visibility flags
    public bool showLogo;      // Stage: stage.show_logo,   Trigger: false (no field in JSON)
    public bool showTimer;     // Stage: stage.show_timer,  Trigger: false (no field in JSON)
    public string timerPosition;
    public bool showScoreLine; // Stage: title_line3_show_score, Trigger: false
    public bool showScorePill; // Stage: show_score, Trigger: false
    public bool showTime;      // Stage: title_line4_show_time,  Trigger: false
    public bool showTeamPhoto; // Stage: show_team_photo,        Trigger: false
    public bool showPhotoCaption;
    public bool showTeamName;  // Stage: show_team_name,         Trigger: false
    public string teamName;    // Sourced from GameManager
    public string backgroundImageUrl;
    public string stageLayout;
    public bool showVolumeControls;
    public string volumeControlsPosition;

    // Buttons to spawn — stages use their triggers list, triggers have none
    public List<TriggerData> buttons;

    // Whether this context owns media that replaces stage background
    public bool hasOwnMedia;
    public bool isStage;

    /// <summary>
    /// Creates a UI context from a stage. All stage JSON fields drive visibility.
    /// </summary>
    public static UIContext FromStage(StageData stage)
    {
        return new UIContext
        {
            title = stage.title,
            subtitle = stage.title_line2,
            titleFontFamily = stage.title_text_font_family,
            showLogo = stage.show_logo,
            showTimer = stage.show_timer,
            timerPosition = stage.timer_position,
            showScoreLine = stage.title_line3_show_score,
            showScorePill = stage.show_score,
            showTime = stage.title_line4_show_time,
            showTeamPhoto = stage.show_team_photo,
            showPhotoCaption = stage.show_photo_caption,
            showTeamName = stage.show_team_name,
            teamName = GameManager.Instance != null ? GameManager.Instance.teamName : "",
            backgroundImageUrl = stage.background_image_url,
            stageLayout = stage.stage_layout,
            showVolumeControls = stage.show_volume_controls,
            volumeControlsPosition = stage.volume_controls_position,
            buttons = GetStageButtons(stage),
            hasOwnMedia = false, // Stage media is handled by ResourceManager.SetupStageMedia
            isStage = true
        };
    }

    /// <summary>
    /// The stage's button triggers in slot order. trigger_value is the button number,
    /// so list order in the JSON does not decide layout, and slots above the game's
    /// button_count are not rendered.
    /// </summary>
    private static List<TriggerData> GetStageButtons(StageData stage)
    {
        if (stage.triggers == null) return new List<TriggerData>();

        int buttonCount = GameManager.Instance?.currentGameData?.game?.button_count ?? 0;

        return stage.triggers
            .Where(t => string.IsNullOrEmpty(t.trigger_type) || t.trigger_type == "button")
            .Where(t => buttonCount <= 0 || GetButtonSlot(t) == int.MaxValue || GetButtonSlot(t) <= buttonCount)
            .OrderBy(GetButtonSlot)
            .ToList();
    }

    private static int GetButtonSlot(TriggerData trigger)
    {
        return int.TryParse(trigger.trigger_value, out int slot) ? slot : int.MaxValue;
    }

    /// <summary>
    /// Creates a UI context from a trigger. Fields absent in trigger JSON default to hidden/false.
    /// </summary>
    public static UIContext FromTrigger(TriggerData trigger)
    {
        return new UIContext
        {
            title = trigger.text_overlay,
            subtitle = null,
            titleFontFamily = null,
            showLogo = false,
            showTimer = false,
            timerPosition = null,
            showScoreLine = false,
            showScorePill = false,
            showTime = false,
            showTeamPhoto = false,
            showPhotoCaption = false,
            showTeamName = false,
            teamName = "",
            backgroundImageUrl = null,
            stageLayout = "fullscreen",
            showVolumeControls = false,
            volumeControlsPosition = null,
            buttons = new List<TriggerData>(),
            hasOwnMedia = !string.IsNullOrEmpty(trigger.media_url),
            isStage = false
        };
    }
}
