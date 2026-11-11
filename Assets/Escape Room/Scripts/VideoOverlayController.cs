using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;

public class VideoOverlayController : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public AudioSource audioSource;
    
    [Header("Dynamic Buttons")]
    public Button skipButton;
    public Button backButton;
    public Button replayButton;
    public Button playPauseButton;

    private TriggerData currentTrigger;
    private bool isEndGame;
    private string displayMode;
    private string fitMode;
    private string overlayOwner;
    private bool isShuttingDown;
    private Texture2D posterTexture;

    public void Setup(TriggerData trigger, bool endGame = false)
    {
        currentTrigger = trigger;
        isEndGame = endGame;
        overlayOwner = "overlay-" + GetInstanceID();

        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        displayMode = VideoFitHelper.ResolveMediaDisplay(trigger, stage);
        fitMode = VideoFitHelper.ResolveMediaFit(trigger, stage);

        if (videoPlayer != null && trigger != null && !string.IsNullOrEmpty(trigger.media_url))
        {
            string localPath = GameManager.Instance.resourceManager.GetLocalPath(trigger.media_url);
            if (File.Exists(localPath))
            {
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                VideoFitHelper.ConfigureAudio(videoPlayer, audioSource, trigger.video_volume);
                VideoSoundFallback.Attach(videoPlayer, audioSource, localPath);

                RenderTexture rt = VideoFitHelper.CreateRenderTexture();
                VideoFitHelper.AttachBinding(gameObject, rt);

                RawImage dest = ResolveDestination();
                VideoFitHelper.BindPlayer(videoPlayer, dest, rt);
                VideoFitHelper.ApplyFit(dest, fitMode, VideoFitHelper.DefaultAspect);

                StageLayoutController layout = StageLayoutController.Instance;
                if (layout != null)
                {
                    layout.ShowOverlay(rt, displayMode, fitMode, VideoFitHelper.DefaultAspect, overlayOwner);
                    RectTransform area = VideoFitHelper.IsTopWindow(displayMode) ? layout.TopPane : layout.OverlayPane;
                    SubtitleDisplay.Attach(videoPlayer, trigger.media_url, area, Subtitles.TriggerPosition(trigger), false);
                }

                videoPlayer.prepareCompleted -= OnPrepared;
                videoPlayer.prepareCompleted += OnPrepared;
                videoPlayer.loopPointReached -= OnVideoEnd;
                videoPlayer.loopPointReached += OnVideoEnd;
                videoPlayer.isLooping = false;
                videoPlayer.url = localPath;
                videoPlayer.Play();

                if (!string.IsNullOrEmpty(trigger.poster_url))
                    GameManager.Instance.resourceManager.LoadTexture(trigger.poster_url, ShowPoster);
            }
        }

        // allow_video_controls switches the whole control bar on or off, as in the web
        // player; the allow_*_button flags then pick which buttons it shows.
        bool showControls = trigger.allow_video_controls;

        // Setup Skip Button — navigates forward using trigger's next_stage_id
        if (skipButton != null)
        {
            bool enableSkip = showControls && trigger.allow_skip_button;
            skipButton.gameObject.SetActive(enableSkip);
            skipButton.interactable = enableSkip;
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => {
                CloseAndNavigate();
            });
        }

        // Setup Back Button — returns to the current stage context
        if (backButton != null)
        {
            bool enableBack = showControls && trigger.allow_back_button;
            backButton.gameObject.SetActive(enableBack);
            backButton.interactable = enableBack;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => {
                CloseAndReturnToStage();
            });
        }

        // Setup Replay Button
        if (replayButton != null)
        {
            bool enableReplay = showControls && trigger.allow_replay_button;
            replayButton.gameObject.SetActive(enableReplay);
            replayButton.interactable = enableReplay;
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(() => {
                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                    videoPlayer.Play();
                }
            });
        }

        // Setup Play/Pause Button
        if (playPauseButton != null)
        {
            bool enablePlayPause = showControls && trigger.allow_play_pause_button;
            playPauseButton.gameObject.SetActive(enablePlayPause);
            playPauseButton.interactable = enablePlayPause;
            playPauseButton.onClick.RemoveAllListeners();
            playPauseButton.onClick.AddListener(() => {
                if (videoPlayer != null)
                {
                    if (videoPlayer.isPlaying) videoPlayer.Pause();
                    else videoPlayer.Play();
                }
            });
        }
    }

    private RawImage ResolveDestination()
    {
        StageLayoutController layout = StageLayoutController.Instance;
        if (layout == null) return null;
        return VideoFitHelper.IsTopWindow(displayMode) ? layout.TopContent : layout.OverlayContent;
    }

    /// <summary>Shows poster_url until the video is ready to take over.</summary>
    private void ShowPoster(Texture2D poster)
    {
        if (isShuttingDown || videoPlayer == null || videoPlayer.isPrepared)
        {
            Destroy(poster);
            return;
        }

        posterTexture = poster;
        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.ShowContent(ResolveDestination(), poster, fitMode, (float)poster.width / poster.height);
    }

    private void ReleasePoster()
    {
        if (posterTexture == null) return;
        Destroy(posterTexture);
        posterTexture = null;
    }

    private void OnPrepared(VideoPlayer player)
    {
        RawImage dest = ResolveDestination();
        VideoFitHelper.ApplyPreparedAspect(player, dest, fitMode);
        if (StageLayoutController.Instance != null && player.targetTexture != null)
            StageLayoutController.Instance.ShowOverlay(player.targetTexture, displayMode, fitMode, VideoFitHelper.DefaultAspect, overlayOwner);
        ReleasePoster();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.prepareCompleted -= OnPrepared;
        }
        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.HideOverlay(overlayOwner);
        ReleasePoster();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (isShuttingDown || !isActiveAndEnabled) return;
        CloseAndNavigate();
    }

    /// <summary>
    /// Closes the overlay and navigates forward using the trigger's next_stage_id.
    /// </summary>
    private void CloseAndNavigate()
    {
        TriggerData triggerRef = currentTrigger;
        bool endGame = isEndGame;
        Destroy(gameObject);
        if (GameManager.Instance == null) return;
        if (endGame)
            GameManager.Instance.ReturnToStage();
        else
            GameManager.Instance.NavigateFromTrigger(triggerRef);
    }

    /// <summary>
    /// Closes the overlay and returns to the current stage (re-renders stage UI).
    /// </summary>
    private void CloseAndReturnToStage()
    {
        Destroy(gameObject);
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToStage();
    }

    public void CloseDialog()
    {
        Destroy(gameObject);
    }
}
