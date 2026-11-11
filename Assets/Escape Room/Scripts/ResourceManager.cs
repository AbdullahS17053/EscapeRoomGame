using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

public class ResourceManager : MonoBehaviour
{
    // Centralized tracking of ALL objects spawned for the current stage
    private List<GameObject> stageObjects = new List<GameObject>();

    // Audio is tracked separately to support "allow_previous_stage_audio_to_continue"
    private GameObject currentAudioObject;

    // Stage music follows the web player: it is decided only when play moves to a
    // different stage, so returning from a trigger or reloading the same stage keeps it.
    private StageData musicStage;
    private bool musicCarriedOver;
    private float musicVolume = 1f;
    private bool musicDipped;

    // Background video tracked separately so triggers can stop it without full stage cleanup
    private GameObject currentBackgroundVideoObject;
    private GameObject currentLowerVideoObject;

    /// <summary>
    /// Registers a GameObject to be destroyed when the stage changes.
    /// Call this from any manager (UIManager, etc.) for anything spawned per-stage.
    /// </summary>
    public void TrackStageObject(GameObject obj)
    {
        if (obj != null && !stageObjects.Contains(obj))
            stageObjects.Add(obj);
    }

    /// <summary>
    /// Destroys ALL tracked stage objects (video, audio, buttons, dialogs, etc.).
    /// Pass the incoming stage to check audio continuation rules.
    /// </summary>
    public void CleanupStage(StageData incomingStage = null)
    {
        // Stop any running coroutines (audio loading, logo loading, etc.)
        StopAllCoroutines();

        // Clean up null references first
        stageObjects.RemoveAll(obj => obj == null);

        // Destroy all tracked stage objects (unparent first for instant UI removal)
        foreach (var obj in stageObjects)
        {
            if (obj != null)
            {
                obj.transform.SetParent(null);
                Destroy(obj);
            }
        }
        stageObjects.Clear();

        currentBackgroundVideoObject = null;
        currentLowerVideoObject = null;

        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.ClearMedia();

        // Handle audio separately for continuation support. The incoming stage decides
        // both whether the old audio stays and how long it takes to fade out.
        if (incomingStage == null)
        {
            musicStage = null;
            musicCarriedOver = false;
            musicDipped = false;
        }

        if (currentAudioObject != null)
        {
            bool keepAudio = incomingStage != null
                && (incomingStage == musicStage || incomingStage.allow_previous_stage_audio_to_continue);

            if (!keepAudio)
            {
                StageAudioFader.FadeOutAndDestroy(currentAudioObject, MusicFadeOutSeconds(incomingStage));
                currentAudioObject = null;
            }
        }
    }

    /// <summary>
    /// The incoming stage's fade-out wins when the JSON sets one. Otherwise music carried
    /// over from earlier stages fades out like the web player's default, and a stage's
    /// own music stops at once, as it does on the web.
    /// </summary>
    private float MusicFadeOutSeconds(StageData incomingStage)
    {
        if (incomingStage == null) return 0f;
        if (incomingStage.stage_audio_fade_out_duration > 0f)
            return incomingStage.stage_audio_fade_out_duration / 1000f;
        return musicCarriedOver ? UiLayoutRules.Instance.carriedMusicFadeOutMs / 1000f : 0f;
    }

    /// <summary>
    /// Lowers the stage music while a trigger video plays. Cleared when play
    /// returns to the stage or moves to another one.
    /// </summary>
    public void SetMusicDip(bool dipped)
    {
        musicDipped = dipped;
        ApplyMusicVolume();
    }

    private float MusicTargetVolume =>
        musicVolume * (musicDipped ? UiLayoutRules.Instance.videoOverlayMusicLevel : 1f);

    private void ApplyMusicVolume()
    {
        if (currentAudioObject == null) return;
        AudioSource source = currentAudioObject.GetComponent<AudioSource>();
        if (source != null && source.clip != null)
            StageAudioFader.SetVolume(source, MusicTargetVolume);
    }

    /// <summary>
    /// Stops stage background video without doing a full stage cleanup.
    /// Called when a trigger with its own media activates. Stage music keeps playing
    /// under trigger media (SetMusicDip lowers it for videos); without a trigger, as for
    /// the end-game video, the music stops too.
    /// </summary>
    public void StopStageMedia(TriggerData trigger = null)
    {
        DestroyTracked(ref currentBackgroundVideoObject);
        DestroyTracked(ref currentLowerVideoObject);

        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.ClearMedia();

        if (trigger == null)
        {
            // Forget the music's stage too, so returning to the stage starts its music again.
            musicStage = null;
            if (currentAudioObject != null)
            {
                Destroy(currentAudioObject);
                currentAudioObject = null;
            }
        }
    }

    public void RestartBackgroundVideo()
    {
        if (currentBackgroundVideoObject == null) return;
        StageVideoController controller = currentBackgroundVideoObject.GetComponent<StageVideoController>();
        if (controller != null)
        {
            controller.Replay();
            return;
        }

        VideoPlayer vp = currentBackgroundVideoObject.GetComponent<VideoPlayer>();
        if (vp == null) return;
        vp.Stop();
        vp.Play();
    }

    /// <summary>
    /// Stops stage music and background-video audio. Leaves the video picture running.
    /// </summary>
    public void StopAllAudio()
    {
        if (currentAudioObject != null)
        {
            Destroy(currentAudioObject);
            currentAudioObject = null;
        }

        MuteVideoAudio(currentBackgroundVideoObject);
        MuteVideoAudio(currentLowerVideoObject);
    }

    /// <summary>
    /// Reads the stage JSON data and spawns the required media objects.
    /// </summary>
    public void SetupStageMedia(StageData stage)
    {
        DestroyTracked(ref currentBackgroundVideoObject);
        DestroyTracked(ref currentLowerVideoObject);

        StageLayoutController layout = StageLayoutController.Ensure(
            GameManager.Instance != null && GameManager.Instance.uiManager != null
                ? GameManager.Instance.uiManager.transform
                : transform);
        layout.ApplyPaneLayout(stage);
        layout.ClearMedia();

        if (!string.IsNullOrEmpty(stage.background_video_url))
        {
            currentBackgroundVideoObject = PlayVideoToPane(
                "Stage_BackgroundVideo",
                stage.background_video_url,
                layout.TopContent,
                stage.background_video_fit_mode,
                stage.background_video_loop,
                stage.background_video_volume,
                (player) =>
                {
                    StageVideoController controller = player.gameObject.AddComponent<StageVideoController>();
                    controller.Setup(stage, player, false);
                    SubtitleDisplay.Attach(player, stage.background_video_url, layout.TopPane, Subtitles.StagePosition(), true);
                });
        }
        else if (!string.IsNullOrEmpty(stage.background_image_url))
        {
            // background_video_fit_mode only describes video. Images fill the stage, except
            // in a video_top window or with a printed photo slot, where they must stay whole.
            bool keepWhole = StageLayoutController.IsVideoTop(stage)
                || UiLayoutRules.Instance.FindPhotoOverlay(stage) != null;
            StartCoroutine(LoadImageToPane(layout.TopContent, stage.background_image_url, keepWhole ? "contain" : "cover"));
        }

        if (!string.IsNullOrEmpty(stage.lower_background_video_url))
        {
            currentLowerVideoObject = PlayVideoToPane(
                "Stage_LowerBackgroundVideo",
                stage.lower_background_video_url,
                layout.LowerContent,
                stage.lower_background_video_fit_mode,
                stage.lower_background_video_loop,
                stage.lower_background_video_volume,
                (player) =>
                {
                    StageVideoController controller = player.gameObject.AddComponent<StageVideoController>();
                    controller.Setup(stage, player, true);
                    SubtitleDisplay.Attach(player, stage.lower_background_video_url, layout.LowerPane, Subtitles.StagePosition(), true);
                });
        }
        else if (!string.IsNullOrEmpty(stage.lower_background_image_url))
        {
            StartCoroutine(LoadImageToPane(layout.LowerContent, stage.lower_background_image_url, "cover"));
        }

        SetupStageMusic(stage);

        layout.PlaceButtonContainer(stage);
    }

    /// <summary>
    /// Stage Audio (Music). Arriving at a different stage spawns a fresh AudioSource,
    /// unless the stage lets whatever is already playing continue. Coming back to the
    /// same stage leaves the music as it is. Either way it plays at this stage's volume.
    /// </summary>
    private void SetupStageMusic(StageData stage)
    {
        if (stage != musicStage)
        {
            bool keepPlaying = stage.allow_previous_stage_audio_to_continue && currentAudioObject != null;
            musicCarriedOver = keepPlaying;
            musicStage = stage;

            if (!keepPlaying)
            {
                if (currentAudioObject != null)
                {
                    Destroy(currentAudioObject);
                    currentAudioObject = null;
                }

                if (!string.IsNullOrEmpty(stage.stage_audio_url))
                {
                    GameObject audioGO = new GameObject("Stage_Audio");
                    currentAudioObject = audioGO;

                    AudioSource audioSource = audioGO.AddComponent<AudioSource>();
                    // Load on the music's own object, so stage cleanup cannot cut the
                    // load short when the music is meant to keep playing.
                    audioGO.AddComponent<StageAudioHost>().StartCoroutine(LoadAndPlayAudio(
                        audioSource,
                        stage.stage_audio_url,
                        stage.stage_audio_loop,
                        stage.stage_audio_fade_in_duration / 1000f));
                }
            }
        }

        musicVolume = stage.stage_audio_volume / 100f;
        musicDipped = false;
        ApplyMusicVolume();
    }

    private GameObject PlayVideoToPane(
        string objectName,
        string url,
        RawImage destination,
        string fitMode,
        bool loop,
        int volume,
        System.Action<VideoPlayer> onReady)
    {
        GameObject videoGO = new GameObject(objectName);
        TrackStageObject(videoGO);

        VideoPlayer vp = videoGO.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.waitForFirstFrame = true;
        vp.isLooping = loop;

        string videoPath = GetLocalPath(url);
        AudioSource videoAudio = videoGO.AddComponent<AudioSource>();
        VideoFitHelper.ConfigureAudio(vp, videoAudio, volume);
        VideoSoundFallback.Attach(vp, videoAudio, videoPath);

        RenderTexture rt = VideoFitHelper.CreateRenderTexture();
        VideoFitHelper.AttachBinding(videoGO, rt);
        VideoFitHelper.BindPlayer(vp, destination, rt);
        if (destination != null) destination.gameObject.SetActive(true);
        VideoFitHelper.ApplyFit(destination, fitMode, VideoFitHelper.DefaultAspect);

        vp.prepareCompleted += (player) =>
        {
            VideoFitHelper.ApplyPreparedAspect(player, destination, fitMode);
        };

        vp.url = videoPath;
        vp.Play();

        onReady?.Invoke(vp);
        return videoGO;
    }

    private IEnumerator LoadImageToPane(RawImage destination, string url, string fitMode)
    {
        string localPath = "file://" + GetLocalPath(url);

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(localPath))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error loading background image: {www.error} at {localPath}");
            }
            else if (destination != null)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                float aspect = texture != null && texture.height > 0
                    ? (float)texture.width / texture.height
                    : VideoFitHelper.DefaultAspect;
                if (StageLayoutController.Instance != null)
                    StageLayoutController.Instance.ShowContent(destination, texture, fitMode, aspect);
            }
        }
    }

    private void DestroyTracked(ref GameObject obj)
    {
        if (obj == null) return;
        stageObjects.Remove(obj);
        obj.transform.SetParent(null);
        Destroy(obj);
        obj = null;
    }

    private static void MuteVideoAudio(GameObject videoObject)
    {
        if (videoObject == null) return;
        VideoPlayer vp = videoObject.GetComponent<VideoPlayer>();
        if (vp != null) vp.SetDirectAudioMute(0, true);
        AudioSource src = videoObject.GetComponent<AudioSource>();
        if (src != null) src.mute = true;
    }

    // ──────────────────────────────────────────────
    //  Utility Methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Strips the remote URL and returns the local persistentDataPath/Games/media/ path.
    /// Since this is in the writable Games folder, standard C# IO works on all platforms.
    /// </summary>
    public string GetLocalPath(string url)
    {
        string fileName = Path.GetFileName(url);
        if (string.IsNullOrEmpty(GameManager.Instance.CurrentLevelPath))
        {
            // Fallback for any legacy loading or before game fully starts
            return Path.Combine(GameDataBootstrap.MediaPath, fileName);
        }
        return Path.Combine(GameManager.Instance.CurrentLevelPath, "media", fileName);
    }

    private static AudioType AudioTypeFromUrl(string url)
    {
        string ext = Path.GetExtension(url ?? "").ToLowerInvariant();
        switch (ext)
        {
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.MPEG;
        }
    }

    private IEnumerator LoadAndPlayAudio(AudioSource audioSource, string url, bool loop, float fadeInSeconds)
    {
        string localPath = "file://" + GetLocalPath(url);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(localPath, AudioTypeFromUrl(url)))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error loading audio: {www.error} at {localPath}");
            }
            else
            {
                // Guard: AudioSource may have been destroyed during async load
                if (audioSource != null)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    audioSource.clip = clip;
                    audioSource.loop = loop;
                    // The current target, since the stage or a dip may have changed during the load
                    audioSource.volume = MusicTargetVolume;
                    audioSource.Play();
                    StageAudioFader.FadeIn(audioSource, fadeInSeconds);
                }
            }
        }
    }

    /// <summary>Loads an image from the game's media folder and hands it to onLoaded.</summary>
    public void LoadTexture(string url, System.Action<Texture2D> onLoaded)
    {
        StartCoroutine(LoadTextureRoutine(url, onLoaded));
    }

    private IEnumerator LoadTextureRoutine(string url, System.Action<Texture2D> onLoaded)
    {
        string localPath = "file://" + GetLocalPath(url);

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(localPath))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Could not load image: {www.error} at {localPath}");
                yield break;
            }

            onLoaded?.Invoke(DownloadHandlerTexture.GetContent(www));
        }
    }

    public void PlayOneShot(string url, int volume, System.Action onComplete)
    {
        StartCoroutine(PlayOneShotRoutine(url, volume, onComplete));
    }

    private IEnumerator PlayOneShotRoutine(string url, int volume, System.Action onComplete)
    {
        GameObject go = new GameObject("OneShotAudio");
        TrackStageObject(go);
        AudioSource source = go.AddComponent<AudioSource>();
        string localPath = "file://" + GetLocalPath(url);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(localPath, AudioTypeFromUrl(url)))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success || source == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            source.clip = clip;
            source.loop = false;
            source.volume = Mathf.Clamp01(volume / 100f);
            source.Play();
            if (clip != null)
                yield return new WaitForSeconds(clip.length);
        }

        if (go != null)
        {
            stageObjects.Remove(go);
            Destroy(go);
        }
        onComplete?.Invoke();
    }
}

/// <summary>
/// Fades stage audio from its own object, so a fade survives ResourceManager
/// stopping its coroutines at every stage change.
/// </summary>
public class StageAudioFader : MonoBehaviour
{
    private AudioSource source;
    private float startVolume;
    private float endVolume;
    private float duration;
    private float elapsed;
    private bool destroyWhenDone;

    public static void FadeIn(AudioSource source, float seconds)
    {
        if (source == null || seconds <= 0f) return;
        Begin(source, 0f, source.volume, seconds, false);
    }

    /// <summary>
    /// Sets the volume, or retargets a running fade-in. A source that is fading out
    /// on its way to being destroyed is left alone.
    /// </summary>
    public static void SetVolume(AudioSource source, float volume)
    {
        if (source == null) return;

        StageAudioFader fader = source.GetComponent<StageAudioFader>();
        if (fader == null)
        {
            source.volume = volume;
            return;
        }

        if (fader.destroyWhenDone) return;

        fader.endVolume = volume;
        // A fade that finished this frame removes itself before its next Update
        if (fader.elapsed >= fader.duration)
            source.volume = volume;
    }

    /// <summary>Destroys the audio object straight away when seconds is zero.</summary>
    public static void FadeOutAndDestroy(GameObject audioObject, float seconds)
    {
        if (audioObject == null) return;

        AudioSource source = audioObject.GetComponent<AudioSource>();
        if (source == null || seconds <= 0f)
        {
            Destroy(audioObject);
            return;
        }

        Begin(source, source.volume, 0f, seconds, true);
    }

    private static void Begin(AudioSource source, float from, float to, float seconds, bool destroyWhenDone)
    {
        StageAudioFader fader = source.GetComponent<StageAudioFader>();
        if (fader == null) fader = source.gameObject.AddComponent<StageAudioFader>();

        fader.source = source;
        fader.startVolume = from;
        fader.endVolume = to;
        fader.duration = seconds;
        fader.elapsed = 0f;
        fader.destroyWhenDone = destroyWhenDone;
        source.volume = from;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        source.volume = Mathf.Lerp(startVolume, endVolume, progress);
        if (progress < 1f) return;

        if (destroyWhenDone) Destroy(gameObject);
        else Destroy(this);
    }
}

/// <summary>Runs the stage music's load coroutine on the music's own object.</summary>
public class StageAudioHost : MonoBehaviour
{
}
