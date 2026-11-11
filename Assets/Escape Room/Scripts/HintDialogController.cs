using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;

public class HintDialogController : MonoBehaviour
{
    public TextMeshProUGUI hintText;
    public VideoPlayer videoPlayer;
    public AudioSource audioSource;
    
    [Header("Dynamic Buttons")]
    public Button closeButton; // Usually standard for hints
    public Button skipButton;
    public Button backButton;
    public Button replayButton;
    public Button playPauseButton;

    private TriggerData currentTrigger;
    private string displayMode;
    private string fitMode;
    private string overlayOwner;

    public void Setup(TriggerData trigger)
    {
        currentTrigger = trigger;
        overlayOwner = "hint-" + GetInstanceID();

        // For hints, we use text_overlay or a hardcoded string. 
        // Here we just use a default or the text overlay if provided.
        if (hintText != null) 
        {
            hintText.text = !string.IsNullOrEmpty(trigger.text_overlay) ? trigger.text_overlay : "Here is your hint!";
        }

        if (videoPlayer != null && !string.IsNullOrEmpty(trigger.media_url))
        {
            string localPath = GameManager.Instance.resourceManager.GetLocalPath(trigger.media_url);
            if (File.Exists(localPath))
            {
                StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
                displayMode = VideoFitHelper.ResolveMediaDisplay(trigger, stage);
                fitMode = VideoFitHelper.ResolveMediaFit(trigger, stage);

                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                VideoFitHelper.ConfigureAudio(videoPlayer, audioSource, trigger.video_volume);
                VideoSoundFallback.Attach(videoPlayer, audioSource, localPath);

                RenderTexture rt = VideoFitHelper.CreateRenderTexture();
                VideoFitHelper.AttachBinding(gameObject, rt);

                StageLayoutController layout = StageLayoutController.Ensure(
                    GameManager.Instance != null && GameManager.Instance.uiManager != null
                        ? GameManager.Instance.uiManager.transform
                        : null);
                RawImage dest = VideoFitHelper.IsTopWindow(displayMode) ? layout.TopContent : layout.OverlayContent;
                VideoFitHelper.BindPlayer(videoPlayer, dest, rt);
                layout.ShowOverlay(rt, displayMode, fitMode, VideoFitHelper.DefaultAspect, overlayOwner);

                videoPlayer.prepareCompleted += (player) =>
                {
                    VideoFitHelper.ApplyPreparedAspect(player, dest, fitMode);
                };

                videoPlayer.url = localPath;
                videoPlayer.Play();
            }
        }

        // Setup Close Button — navigates using trigger's next_stage_id
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseAndNavigate);
        }

        // allow_video_controls switches the video buttons on or off, as in the web player;
        // the allow_*_button flags then pick which ones show. Close always stays.
        bool showControls = trigger.allow_video_controls;

        // Setup Skip Button
        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(showControls && trigger.allow_skip_button);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(CloseAndNavigate);
        }

        // Setup Back Button
        if (backButton != null)
        {
            backButton.gameObject.SetActive(showControls && trigger.allow_back_button);
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseAndReturnToStage);
        }

        // Setup Replay Button
        if (replayButton != null)
        {
            bool enableReplay = showControls && trigger.allow_replay_button;
            replayButton.gameObject.SetActive(enableReplay);
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

    /// <summary>
    /// Closes and navigates using the trigger's next_stage_id.
    /// For hints this typically returns to the same stage.
    /// </summary>
    private void CloseAndNavigate()
    {
        TriggerData triggerRef = currentTrigger;
        Destroy(gameObject);
        if (GameManager.Instance != null)
            GameManager.Instance.NavigateFromTrigger(triggerRef);
    }

    /// <summary>
    /// Closes and returns to the current stage (re-renders stage UI).
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

    private void OnDestroy()
    {
        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.HideOverlay(overlayOwner);
    }
}
