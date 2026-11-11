using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Drives a stage background VideoPlayer: end-of-video actions and optional on-screen controls.
/// Render target is assigned by ResourceManager via VideoFitHelper.
/// </summary>
public class StageVideoController : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private StageData stage;
    private bool isLower;
    private bool endActionFired;
    private bool isShuttingDown;
    private GameObject controlsRoot;

    public void Setup(StageData stageData, VideoPlayer player, bool lower = false)
    {
        stage = stageData;
        videoPlayer = player;
        isLower = lower;
        endActionFired = false;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.loopPointReached += OnVideoEnd;
        }

        if (stage == null || GameManager.Instance == null || GameManager.Instance.uiManager == null)
            return;

        bool showControls = isLower ? stage.lower_background_video_show_controls : stage.background_video_show_controls;
        if (!showControls) return;

        bool looping = isLower ? stage.lower_background_video_loop : stage.background_video_loop;
        bool allowSkip = (isLower ? stage.lower_background_video_allow_skip : stage.background_video_allow_skip) && !looping;
        bool allowBack = (isLower ? stage.lower_background_video_allow_back : stage.background_video_allow_back) && !looping;
        bool allowReplay = isLower ? stage.lower_background_video_allow_replay : stage.background_video_allow_replay;
        bool allowPlayPause = isLower ? stage.lower_background_video_allow_play_pause : stage.background_video_allow_play_pause;

        Transform parent = null;
        if (StageLayoutController.Instance != null)
        {
            parent = isLower
                ? (Transform)StageLayoutController.Instance.LowerPane
                : (Transform)StageLayoutController.Instance.TopPane;
        }

        controlsRoot = GameManager.Instance.uiManager.ShowStageVideoControls(
            this, looping, allowSkip, allowBack, allowReplay, allowPlayPause, parent);
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoEnd;
        if (controlsRoot != null)
            Destroy(controlsRoot);
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (isShuttingDown || !isActiveAndEnabled) return;
        bool looping = isLower ? stage != null && stage.lower_background_video_loop : stage != null && stage.background_video_loop;
        if (looping) return;
        FireEndAction();
    }

    public void Skip()
    {
        FireEndAction();
    }

    public void GoBack()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GoBack();
    }

    public void Replay()
    {
        endActionFired = false;
        if (videoPlayer == null) return;
        videoPlayer.Stop();
        videoPlayer.Play();
    }

    public void TogglePlayPause()
    {
        if (videoPlayer == null) return;
        if (videoPlayer.isPlaying) videoPlayer.Pause();
        else videoPlayer.Play();
    }

    private void FireEndAction()
    {
        if (endActionFired) return;
        endActionFired = true;
        if (GameManager.Instance != null && stage != null)
            GameManager.Instance.RunBackgroundVideoEndAction(stage);
    }
}
