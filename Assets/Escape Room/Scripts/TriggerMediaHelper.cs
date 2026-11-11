using System;
using System.IO;

public static class TriggerMediaHelper
{
    public static bool IsStill(string mediaType, string url)
    {
        if (!string.IsNullOrEmpty(mediaType))
        {
            string type = mediaType.Trim().ToLowerInvariant();
            if (type == "image" || type == "animation") return true;
            if (type == "video" || type == "audio") return false;
        }

        string ext = Path.GetExtension(url ?? "").ToLowerInvariant();
        switch (ext)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".gif":
            case ".webp":
            case ".bmp":
                return true;
            default:
                return false;
        }
    }

    public static bool IsAudio(string mediaType, string url)
    {
        if (!string.IsNullOrEmpty(mediaType)
            && mediaType.Trim().Equals("audio", StringComparison.OrdinalIgnoreCase))
            return true;

        string ext = Path.GetExtension(url ?? "").ToLowerInvariant();
        return ext == ".mp3" || ext == ".wav" || ext == ".ogg";
    }

    public static bool IsStill(TriggerData trigger)
    {
        return trigger != null && IsStill(trigger.media_type, trigger.media_url);
    }

    public static bool IsAudio(TriggerData trigger)
    {
        return trigger != null && IsAudio(trigger.media_type, trigger.media_url);
    }

    /// <summary>
    /// A trigger video lowers the stage music unless the trigger opts out.
    /// Images and audio leave the music at full volume.
    /// </summary>
    public static bool DipsStageMusic(TriggerData trigger)
    {
        if (trigger == null || string.IsNullOrEmpty(trigger.media_url)) return false;
        if (trigger.prevent_stage_audio_dip || trigger.prevent_audio_dip) return false;
        return !IsStill(trigger) && !IsAudio(trigger);
    }

    public static float OverlayDuration(TriggerData trigger)
    {
        if (trigger == null || trigger.duration_seconds <= 0f) return 5f;
        return trigger.duration_seconds;
    }

    public static int FailVideoVolume(TriggerData trigger)
    {
        if (trigger == null) return 100;
        if (trigger.incorrect_video_volume > 0) return trigger.incorrect_video_volume;
        if (trigger.video_volume > 0) return trigger.video_volume;
        return 100;
    }
}
