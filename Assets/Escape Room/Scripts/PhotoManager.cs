using UnityEngine;

/// <summary>
/// Singleton manager that handles taking photos via NativeCamera and storing
/// the result for display on any stage with show_team_photo: true.
/// This is the single source of truth for the team photo texture.
/// </summary>
public class PhotoManager : MonoBehaviour
{
    public static PhotoManager Instance;

    /// <summary>
    /// The team photo texture, captured via NativeCamera.
    /// Null if no photo has been taken yet.
    /// </summary>
    public Texture2D TeamPhoto { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Opens the device camera via NativeCamera plugin.
    /// Calls onPhotoTaken with the loaded Texture2D on success, or null on cancel/error.
    /// </summary>
    /// <param name="maxSize">Max image dimension in pixels. 1024 is a good default for memory.</param>
    /// <param name="onPhotoTaken">Callback with the Texture2D result (null if cancelled).</param>
    public void TakePhoto(System.Action<Texture2D> onPhotoTaken, int maxSize = 1024)
    {
        NativeCamera.TakePicture((string path) =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"Photo saved to: {path}");

                // Load the image into a Texture2D
                Texture2D texture = NativeCamera.LoadImageAtPath(path, maxSize, false);
                if (texture != null)
                {
                    // Store as the team photo (replace any previous one)
                    if (TeamPhoto != null)
                        Destroy(TeamPhoto);

                    TeamPhoto = texture;
                    onPhotoTaken?.Invoke(texture);
                }
                else
                {
                    Debug.LogWarning($"Failed to load texture from path: {path}");
                    onPhotoTaken?.Invoke(null);
                }
            }
            else
            {
                Debug.Log("Photo capture cancelled or permission denied.");
                onPhotoTaken?.Invoke(null);
            }
        }, maxSize);
    }

    /// <summary>
    /// Clears the stored team photo. Called when resetting the game.
    /// </summary>
    public void ClearPhoto()
    {
        if (TeamPhoto != null)
        {
            Destroy(TeamPhoto);
            TeamPhoto = null;
        }
    }
}
