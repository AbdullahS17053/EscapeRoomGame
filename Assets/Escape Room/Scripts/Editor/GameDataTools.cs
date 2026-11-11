using UnityEngine;
using UnityEditor;
using System.IO;

public class GameDataTools
{
    [MenuItem("Escape Room/Reset Game Data (Force Re-Bootstrap)")]
    public static void ResetGameData()
    {
        string gamesRoot = Path.Combine(Application.persistentDataPath, "Games");
        string markerPath = Path.Combine(gamesRoot, ".bootstrapped");

        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
            Debug.Log($"[Escape Room] Deleted bootstrap marker at {markerPath}. Game will recopy from StreamingAssets on next play.");
        }
        else
        {
            Debug.Log("[Escape Room] No bootstrap marker found. Game will already copy data on next play.");
        }
    }

    [MenuItem("Escape Room/Clear All Extracted Game Data")]
    public static void ClearAllGameData()
    {
        string gamesRoot = Path.Combine(Application.persistentDataPath, "Games");

        if (Directory.Exists(gamesRoot))
        {
            Directory.Delete(gamesRoot, true);
            Debug.Log($"[Escape Room] Deleted entire Games folder at {gamesRoot}. It will be completely rebuilt on next play.");
        }
        else
        {
            Debug.Log("[Escape Room] No Games folder found to delete.");
        }
    }

    [MenuItem("Escape Room/Generate Bootstrap Index")]
    public static void GenerateBootstrapIndex()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        string indexPath = Path.Combine(streamingAssetsPath, "bootstrap_index.txt");

        if (!Directory.Exists(streamingAssetsPath))
        {
            Debug.LogWarning("[Escape Room] StreamingAssets folder does not exist.");
            return;
        }

        // Get all files recursively, ignoring meta files and the index itself
        string[] allFiles = Directory.GetFiles(streamingAssetsPath, "*.*", SearchOption.AllDirectories);
        using (StreamWriter writer = new StreamWriter(indexPath))
        {
            foreach (string file in allFiles)
            {
                if (file.EndsWith(".meta") || file.EndsWith("bootstrap_index.txt"))
                    continue;

                // Make the path relative to StreamingAssets
                string relativePath = file.Substring(streamingAssetsPath.Length + 1).Replace("\\", "/");
                writer.WriteLine(relativePath);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Escape Room] Successfully generated bootstrap_index.txt with {File.ReadAllLines(indexPath).Length} files.");
    }
}

