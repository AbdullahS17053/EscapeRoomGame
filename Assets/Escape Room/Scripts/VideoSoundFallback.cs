using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

/// <summary>
/// Plays a video's PCM sound from a WAV copy, in step with the muted video. Unity and
/// Android cannot play PCM sound inside MP4 or QuickTime files, so PcmSoundExtractor
/// copies it out once and this keeps it following the picture. The video stays in charge:
/// the sound follows its play, pause, replay, skip and loop.
/// </summary>
public class VideoSoundFallback : MonoBehaviour
{
    // Picture and sound further apart than this are pulled back together
    private const float ResyncSeconds = 0.12f;
    // Near the end of the sound, a restart would only repeat its last moment
    private const float EndMarginSeconds = 0.05f;

    private static readonly object cacheLock = new object();
    private static readonly Dictionary<string, bool> pcmByWavPath = new Dictionary<string, bool>();
    private static readonly Dictionary<string, Task<string>> wavTasks = new Dictionary<string, Task<string>>();

    private VideoPlayer player;
    private AudioSource source;
    private Task<string> wavTask;
    private AudioClip clip;
    private bool loading;

    /// <summary>
    /// Call after VideoFitHelper.ConfigureAudio and before the video starts. Clips whose
    /// own sound plays are left alone, so this is safe to call for every video.
    /// </summary>
    public static void Attach(VideoPlayer player, AudioSource source, string videoPath)
    {
        if (player == null) return;

        VideoSoundFallback existing = player.GetComponent<VideoSoundFallback>();
        if (existing != null) Destroy(existing);

        if (source == null || string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath)) return;
        if (!HasPcmSound(videoPath)) return;

        // The video's own track cannot be decoded; the WAV plays through the same
        // AudioSource, so its volume and mute settings still apply.
        player.audioOutputMode = VideoAudioOutputMode.None;

        VideoSoundFallback fallback = player.gameObject.AddComponent<VideoSoundFallback>();
        fallback.player = player;
        fallback.source = source;
        fallback.wavTask = GetWav(videoPath);
    }

    /// <summary>
    /// Copies the sound out of every PCM video in the folder on a worker thread, so it is
    /// ready before the clips play. Call when a game loads.
    /// </summary>
    public static void PrepareFolder(string mediaFolder)
    {
        if (string.IsNullOrEmpty(mediaFolder) || !Directory.Exists(mediaFolder)) return;

        Task.Run(() =>
        {
            foreach (string file in Directory.GetFiles(mediaFolder))
            {
                if (!IsVideoFile(file)) continue;
                try
                {
                    if (HasPcmSound(file))
                        GetWav(file).Wait();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"VideoSoundFallback: could not prepare the sound of {Path.GetFileName(file)}: {e.GetBaseException().Message}");
                }
            }
        });
    }

    private static bool IsVideoFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".mp4" || ext == ".mov" || ext == ".m4v" || ext == ".3gp";
    }

    private static bool HasPcmSound(string videoPath)
    {
        // The WAV path changes with the video's size and time, so a replaced video is checked again
        string key = PcmSoundExtractor.WavPathFor(videoPath);
        lock (cacheLock)
        {
            if (pcmByWavPath.TryGetValue(key, out bool known)) return known;
        }

        bool pcm;
        try { pcm = PcmSoundExtractor.FindPcmTrack(videoPath) != null; }
        catch (IOException) { pcm = false; }

        lock (cacheLock) pcmByWavPath[key] = pcm;
        return pcm;
    }

    private static Task<string> GetWav(string videoPath)
    {
        string key = PcmSoundExtractor.WavPathFor(videoPath);
        lock (cacheLock)
        {
            if (!wavTasks.TryGetValue(key, out Task<string> task) || task.IsFaulted)
            {
                task = Task.Run(() => PcmSoundExtractor.EnsureWav(videoPath));
                wavTasks[key] = task;
            }
            return task;
        }
    }

    private void Update()
    {
        if (clip == null)
        {
            if (!loading && wavTask != null && wavTask.IsCompleted)
                StartCoroutine(LoadClip());
            return;
        }
        if (player == null || source == null) return;

        bool videoRunning = player.isPrepared && player.isPlaying && player.frame >= 0;
        if (!videoRunning)
        {
            if (source.isPlaying) source.Pause();
            return;
        }

        float videoTime = (float)player.time;
        if (videoTime >= clip.length - EndMarginSeconds)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        if (!source.isPlaying)
        {
            source.time = videoTime;
            source.Play();
        }
        else if (Mathf.Abs(source.time - videoTime) > ResyncSeconds)
        {
            source.time = videoTime;
        }
    }

    private IEnumerator LoadClip()
    {
        loading = true;

        if (wavTask.IsFaulted || string.IsNullOrEmpty(wavTask.Result))
        {
            if (wavTask.IsFaulted)
                Debug.LogWarning($"VideoSoundFallback: could not copy the sound out of {player.url}: {wavTask.Exception?.GetBaseException().Message}");
            enabled = false;
            yield break;
        }

        string wavPath = wavTask.Result;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + wavPath, AudioType.WAV))
        {
            // Streams from disk, so long clips do not sit in memory
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"VideoSoundFallback: could not load {wavPath}: {www.error}");
                enabled = false;
                yield break;
            }
            clip = DownloadHandlerAudioClip.GetContent(www);
        }

        if (source == null) yield break;
        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
    }

    private void OnDestroy()
    {
        if (clip == null) return;
        if (source != null && source.clip == clip)
        {
            source.Stop();
            source.clip = null;
        }
        Destroy(clip);
    }
}
