using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

public class LevelImportManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Assign a UI panel with a spinner to show during extraction.")]
    public GameObject importSpinnerPanel;

    /// <summary>
    /// Bind this method to a UI button to open the file picker.
    /// </summary>
    public void ImportLevelZip()
    {
        if (NativeFilePicker.IsFilePickerBusy())
            return;

        // Allow .zip files and our custom .escaperoom extension if used
        string[] fileTypes = new string[] { "application/zip", "public.archive", ".zip", ".escaperoom" };

        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                Debug.Log("Level import cancelled by user.");
                return;
            }

            Debug.Log("Picked file: " + path);
            ProcessZipFile(path);
        }, fileTypes);
    }

    private async void ProcessZipFile(string zipPath)
    {
        if (importSpinnerPanel != null)
            importSpinnerPanel.SetActive(true);

        // Unity APIs (persistentDataPath) must be read on the main thread.
        string gamesRoot = GameDataBootstrap.GamesRootPath;
        Directory.CreateDirectory(gamesRoot);

        (bool success, string error) = await Task.Run(() => ExtractZip(zipPath, gamesRoot));

        if (importSpinnerPanel != null)
            importSpinnerPanel.SetActive(false);

        if (success)
        {
            Debug.Log("Level package extracted successfully.");

            if (GameManager.Instance != null)
                GameManager.Instance.InitializeGameSelection();
        }
        else
        {
            Debug.LogError("Failed to extract the level package: " + error);
        }
    }

    private (bool success, string error) ExtractZip(string zipPath, string gamesRoot)
    {
        try
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(zipPath);
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
                fileNameWithoutExtension = "ImportedLevel";

            string baseExtractPath = Path.Combine(gamesRoot, fileNameWithoutExtension);
            string finalExtractPath = baseExtractPath;

            int counter = 1;
            while (Directory.Exists(finalExtractPath))
            {
                finalExtractPath = baseExtractPath + "_" + counter;
                counter++;
            }

            Directory.CreateDirectory(finalExtractPath);
            ZipFile.ExtractToDirectory(zipPath, finalExtractPath);
            return (true, null);
        }
        catch (System.Exception e)
        {
            return (false, e.ToString());
        }
    }

    /// <summary>
    /// Bind this method to a UI button to clear all loaded data in persistentDataPath and clear spawned game buttons.
    /// </summary>
    public void ClearAllData()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearAllPersistentData();
        }
        else
        {
            ClearPersistentPathDirectly();
        }
    }

    private void ClearPersistentPathDirectly()
    {
        string persistentPath = Application.persistentDataPath;
        if (Directory.Exists(persistentPath))
        {
            DirectoryInfo di = new DirectoryInfo(persistentPath);
            foreach (FileInfo file in di.GetFiles())
            {
                try { file.Delete(); } catch (System.Exception e) { Debug.LogWarning($"Failed to delete file {file.FullName}: {e.Message}"); }
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                try { dir.Delete(true); } catch (System.Exception e) { Debug.LogWarning($"Failed to delete dir {dir.FullName}: {e.Message}"); }
            }
        }
    }
}

