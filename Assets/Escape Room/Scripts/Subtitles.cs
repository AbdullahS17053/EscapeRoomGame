using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subtitle on/off state and track lookup. As in the web player, subtitles start off,
/// a toggle_subtitles button flips them, and the choice is remembered on the device.
/// Tracks come from the game JSON's subtitle_tracks and are matched by video_url.
/// </summary>
public static class Subtitles
{
    private const string PrefKey = "subtitles_enabled";

    public static event Action Changed;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        set
        {
            if (Enabled == value) return;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static void Toggle()
    {
        Enabled = !Enabled;
    }

    /// <summary>
    /// The cues for a video, sorted by start time, or null when it has none.
    /// Tracks marked not_needed or with no cues are skipped. When several tracks match
    /// one video, a reviewed track wins, then the one with the most cues.
    /// </summary>
    public static List<SubtitleCue> FindCues(string videoUrl)
    {
        List<SubtitleTrack> tracks = GameManager.Instance?.currentGameData?.subtitle_tracks;
        if (tracks == null || string.IsNullOrEmpty(videoUrl)) return null;

        SubtitleTrack best = null;
        foreach (SubtitleTrack track in tracks)
        {
            if (track == null || track.not_needed || track.cues == null || track.cues.Count == 0) continue;
            if (!SameVideo(track.video_url, videoUrl)) continue;
            if (best == null
                || (track.reviewed && !best.reviewed)
                || (track.reviewed == best.reviewed && track.cues.Count > best.cues.Count))
                best = track;
        }
        if (best == null) return null;

        List<SubtitleCue> cues = new List<SubtitleCue>(best.cues);
        cues.RemoveAll(c => c == null);
        cues.Sort((a, b) => a.start.CompareTo(b.start));
        return cues;
    }

    /// <summary>The cue showing at a time in seconds: start inclusive, end exclusive.</summary>
    public static SubtitleCue CueAt(List<SubtitleCue> cues, double time)
    {
        if (cues == null) return null;
        foreach (SubtitleCue cue in cues)
        {
            if (time >= cue.start && time < cue.end) return cue;
        }
        return null;
    }

    /// <summary>Where background videos show subtitles: game subtitle_position "above" means top.</summary>
    public static string StagePosition()
    {
        string position = GameManager.Instance?.currentGameData?.game?.subtitle_position;
        if (string.IsNullOrWhiteSpace(position)) position = UiLayoutRules.Instance.subtitleStageDefaultPosition;
        return position.Trim().Equals("above", StringComparison.OrdinalIgnoreCase) ? "top" : "bottom";
    }

    /// <summary>Where a trigger's media shows subtitles.</summary>
    public static string TriggerPosition(TriggerData trigger)
    {
        string position = trigger != null ? trigger.subtitle_position : null;
        return string.IsNullOrWhiteSpace(position) ? UiLayoutRules.Instance.subtitleTriggerDefaultPosition : position.Trim();
    }

    public static bool IsToggle(TriggerData trigger)
    {
        return trigger != null && string.Equals(trigger.action_type, "toggle_subtitles", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Label of a toggle_subtitles button for the current state.</summary>
    public static string ToggleText(TriggerData trigger)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        string text = Enabled ? trigger.subtitle_enabled_text : trigger.subtitle_disabled_text;
        if (!string.IsNullOrEmpty(text)) return text;
        return Enabled ? rules.subtitleToggleOnText : rules.subtitleToggleOffText;
    }

    /// <summary>Colour of a toggle_subtitles button for the current state.</summary>
    public static Color ToggleColor(TriggerData trigger)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        string hex = Enabled ? trigger.subtitle_enabled_color : trigger.subtitle_disabled_color;
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
        return Enabled ? rules.subtitleToggleOnColor : rules.subtitleToggleOffColor;
    }

    private static bool SameVideo(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal)) return true;
        return string.Equals(FileName(a), FileName(b), StringComparison.Ordinal);
    }

    private static string FileName(string url)
    {
        string path = url.Trim().Split('?', '#')[0].Replace('\\', '/');
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }
}
