using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays video into a RenderTexture shown on a masked RawImage, and sizes that
/// image for JSON fit modes: contain (letterbox) vs cover (crop).
/// </summary>
public static class VideoFitHelper
{
    public const float DefaultAspect = 16f / 9f;

    public static bool IsCover(string mode)
    {
        return !string.IsNullOrEmpty(mode)
            && mode.Trim().Equals("cover", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeFit(string mode)
    {
        return IsCover(mode) ? "cover" : "contain";
    }

    public static string ResolveMediaDisplay(TriggerData trigger, StageData stage)
    {
        if (trigger != null && !string.IsNullOrEmpty(trigger.media_display))
            return trigger.media_display.Trim().ToLowerInvariant();
        if (stage != null && !string.IsNullOrEmpty(stage.trigger_media_display))
            return stage.trigger_media_display.Trim().ToLowerInvariant();
        return "fullscreen";
    }

    public static string ResolveMediaFit(TriggerData trigger, StageData stage)
    {
        if (trigger != null && !string.IsNullOrEmpty(trigger.media_video_fit_mode))
            return NormalizeFit(trigger.media_video_fit_mode);
        return "contain";
    }

    public static bool IsTopWindow(string display)
    {
        return !string.IsNullOrEmpty(display)
            && display.Trim().Equals("top_window", StringComparison.OrdinalIgnoreCase);
    }

    public static void StretchFill(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// contain = FitInParent (letterbox/pillarbox). cover = EnvelopeParent (fill + crop).
    /// Picture is always centered in its pane. Parent must have a Mask.
    /// </summary>
    public static void ApplyFit(RawImage image, string mode, float aspectRatio)
    {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        RectTransform parent = rect.parent as RectTransform;
        if (parent != null)
            rect.sizeDelta = parent.rect.size;

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = image.gameObject.AddComponent<AspectRatioFitter>();

        fitter.aspectRatio = aspectRatio > 0.05f ? aspectRatio : DefaultAspect;
        fitter.aspectMode = IsCover(mode)
            ? AspectRatioFitter.AspectMode.EnvelopeParent
            : AspectRatioFitter.AspectMode.FitInParent;
    }

    public static RenderTexture CreateRenderTexture(int width = 1920, int height = 1080)
    {
        RenderTexture rt = new RenderTexture(width, height, 0);
        rt.Create();
        return rt;
    }

    public static void BindPlayer(VideoPlayer player, RawImage destination, RenderTexture rt)
    {
        if (player == null) return;

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = rt;
        player.targetCamera = null;
        player.aspectRatio = VideoAspectRatio.Stretch;

        if (destination != null)
            destination.texture = rt;
    }

    public static VideoRenderBinding AttachBinding(GameObject host, RenderTexture rt)
    {
        if (host == null) return null;
        VideoRenderBinding binding = host.GetComponent<VideoRenderBinding>();
        if (binding == null) binding = host.AddComponent<VideoRenderBinding>();
        binding.Replace(rt);
        return binding;
    }

    public static void ConfigureAudio(VideoPlayer player, AudioSource source, int volumePercent)
    {
        if (player == null) return;
        float volume = Mathf.Clamp01(volumePercent / 100f);

        if (source != null)
        {
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.SetTargetAudioSource(0, source);
            source.volume = volume;
            source.mute = false;
        }
        else
        {
            player.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        player.SetDirectAudioMute(0, false);
        player.SetDirectAudioVolume(0, volume);
    }

    public static void ApplyPreparedAspect(VideoPlayer player, RawImage destination, string mode)
    {
        if (player == null || destination == null) return;
        float aspect = DefaultAspect;
        if (player.texture != null && player.texture.height > 0)
            aspect = (float)player.texture.width / player.texture.height;
        ApplyFit(destination, mode, aspect);
    }
}

/// <summary>
/// Releases a RenderTexture when the owning GameObject is destroyed.
/// </summary>
public class VideoRenderBinding : MonoBehaviour
{
    public RenderTexture RenderTexture { get; private set; }

    public void Replace(RenderTexture rt)
    {
        Release();
        RenderTexture = rt;
    }

    private void OnDestroy()
    {
        Release();
    }

    public void Release()
    {
        if (RenderTexture == null) return;
        RenderTexture.Release();
        Destroy(RenderTexture);
        RenderTexture = null;
    }
}
