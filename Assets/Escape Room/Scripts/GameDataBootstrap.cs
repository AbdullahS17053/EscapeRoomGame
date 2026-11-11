using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles first-run bootstrap: copies bundled game data from StreamingAssets
/// to Application.persistentDataPath/Games/ so the app has data to work with.
/// After bootstrap, all game data lives in the writable Games folder,
/// allowing new games to be added after build (via file manager, download, etc.).
/// </summary>
public class GameDataBootstrap : MonoBehaviour
{
    public static GameDataBootstrap Instance;

    /// <summary>
    /// The writable root folder where all game data lives at runtime.
    /// Structure: Games/*.json, Games/media/*
    /// </summary>
    public static string GamesRootPath => Path.Combine(Application.persistentDataPath, "Games");
    public static string MediaPath => Path.Combine(GamesRootPath, "media");

    /// <summary>
    /// An index file listing all bundled files that need to be copied on first run.
    /// This file lives in StreamingAssets and is read via UnityWebRequest on Android.
    /// </summary>
    private const string BOOTSTRAP_INDEX = "bootstrap_index.txt";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Called by GameManager on Start. Returns true when bootstrap is complete
    /// and the Games folder is ready for scanning.
    /// </summary>
    public IEnumerator EnsureGameDataReady()
    {
        // Create the Games and media directories if they don't exist
        Directory.CreateDirectory(GamesRootPath);
        Directory.CreateDirectory(MediaPath);

        // Check if we've already bootstrapped (marker file)
        string markerPath = Path.Combine(GamesRootPath, ".bootstrapped");
        if (File.Exists(markerPath))
        {
            Debug.Log("GameDataBootstrap: Already bootstrapped, skipping copy.");
            yield break;
        }

        Debug.Log("GameDataBootstrap: First run detected. Copying bundled game data...");

        // Read the bootstrap index from StreamingAssets
        string indexUrl = GetStreamingAssetsUrl(BOOTSTRAP_INDEX);
        using (UnityWebRequest www = UnityWebRequest.Get(indexUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"GameDataBootstrap: No bootstrap_index.txt found ({www.error}). Skipping bootstrap.");
                // Write marker anyway so we don't retry every launch
                File.WriteAllText(markerPath, "no_index");
                yield break;
            }

            string indexContent = www.downloadHandler.text;
            string[] files = indexContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string relativePath in files)
            {
                string trimmed = relativePath.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string destPath = Path.Combine(GamesRootPath, trimmed);

                // Skip if already exists (e.g., partial previous copy)
                if (File.Exists(destPath)) continue;

                // Ensure subdirectory exists
                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                // Read from StreamingAssets via UnityWebRequest (works on Android)
                string srcUrl = GetStreamingAssetsUrl(trimmed);
                using (UnityWebRequest fileReq = UnityWebRequest.Get(srcUrl))
                {
                    yield return fileReq.SendWebRequest();

                    if (fileReq.result == UnityWebRequest.Result.Success)
                    {
                        File.WriteAllBytes(destPath, fileReq.downloadHandler.data);
                        Debug.Log($"GameDataBootstrap: Copied {trimmed}");
                    }
                    else
                    {
                        Debug.LogWarning($"GameDataBootstrap: Failed to copy {trimmed}: {fileReq.error}");
                    }
                }
            }
        }

        // Write marker so we don't re-copy on next launch
        File.WriteAllText(markerPath, System.DateTime.UtcNow.ToString());
        Debug.Log("GameDataBootstrap: Bootstrap complete.");
    }

    /// <summary>
    /// Builds the correct URL for accessing a StreamingAssets file on any platform.
    /// Android uses jar:file:// protocol, others use file://
    /// </summary>
    private string GetStreamingAssetsUrl(string relativePath)
    {
        string path = Path.Combine(Application.streamingAssetsPath, relativePath);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android streamingAssetsPath already has the jar:file:// prefix
        return path;
#else
        return "file://" + path;
#endif
    }

    /// <summary>
    /// Scans the Games root folder and returns all JSON game files.
    /// Since this is persistentDataPath, standard C# IO works on all platforms.
    /// </summary>
    public string[] GetAvailableGameFiles()
    {
        if (!Directory.Exists(GamesRootPath))
            return new string[0];

        // Search recursively so it finds JSONs inside extracted level folders
        return Directory.GetFiles(GamesRootPath, "*.json", SearchOption.AllDirectories);
    }
}
