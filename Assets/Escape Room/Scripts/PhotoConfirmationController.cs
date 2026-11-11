using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for the photo confirmation dialog.
/// Shows the photo prompt, opens the camera, displays the taken photo,
/// and provides Confirm/Retake buttons. All behavior driven by TriggerData.
/// </summary>
public class PhotoConfirmationController : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI promptText;       // Displays photo_prompt from JSON
    public RawImage photoPreview;            // Shows the captured photo
    public GameObject previewContainer;      // Parent container for the photo preview (hidden until photo taken)

    [Header("Buttons")]
    public Button takePhotoButton;           // "Take Photo" — opens camera
    public Button confirmButton;             // "Confirm" — accepts photo and navigates
    public Button retakeButton;              // "Retake" — opens camera again

    private TriggerData currentTrigger;

    public void Setup(TriggerData trigger)
    {
        currentTrigger = trigger;

        // Show the photo prompt from JSON
        if (promptText != null)
        {
            string prompt = !string.IsNullOrEmpty(trigger.photo_prompt)
                ? trigger.photo_prompt
                : "Take a photo";
            promptText.text = prompt;
        }

        // Initially hide preview and confirm/retake, show take photo button
        if (previewContainer != null) previewContainer.SetActive(false);
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (retakeButton != null) retakeButton.gameObject.SetActive(false);

        // Setup Take Photo button
        if (takePhotoButton != null)
        {
            takePhotoButton.onClick.RemoveAllListeners();
            takePhotoButton.onClick.AddListener(OpenCamera);
        }

        // Setup Confirm button
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }

        // Setup Retake button
        if (retakeButton != null)
        {
            retakeButton.onClick.RemoveAllListeners();
            retakeButton.onClick.AddListener(OpenCamera);
        }
    }

    private void OpenCamera()
    {
        if (PhotoManager.Instance == null)
        {
            Debug.LogError("PhotoManager not found in scene!");
            return;
        }

        PhotoManager.Instance.TakePhoto((Texture2D photo) =>
        {
            if (photo != null)
            {
                // Show the captured photo for confirmation
                if (photoPreview != null)
                {
                    photoPreview.texture = photo;
                }

                // Switch to confirmation mode
                if (previewContainer != null) previewContainer.SetActive(true);
                if (takePhotoButton != null) takePhotoButton.gameObject.SetActive(false);
                if (confirmButton != null) confirmButton.gameObject.SetActive(true);
                if (retakeButton != null) retakeButton.gameObject.SetActive(true);
            }
            else
            {
                Debug.Log("Photo capture cancelled. Staying on photo dialog.");
                // User cancelled — stay on the dialog, they can try again
            }
        });
    }

    private void OnConfirm()
    {
        // Photo confirmed — navigate based on trigger's JSON data
        TriggerData triggerRef = currentTrigger;
        Destroy(gameObject);

        if (GameManager.Instance != null)
        {
            // Use photo_next_stage_number if specified, otherwise use standard navigation
            if (triggerRef.photo_next_stage_number.HasValue)
                GameManager.Instance.LoadStage(triggerRef.photo_next_stage_number.Value);
            else
                GameManager.Instance.NavigateFromTrigger(triggerRef);
        }
    }

    public void CloseDialog()
    {
        Destroy(gameObject);
    }
}
