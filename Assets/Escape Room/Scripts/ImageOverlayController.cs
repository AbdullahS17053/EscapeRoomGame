using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Still-image / animation overlay with optional text box and duration auto-advance.
/// </summary>
public class ImageOverlayController : MonoBehaviour
{
    private TriggerData currentTrigger;
    private bool isEndGame;
    private string overlayOwner;
    private bool isShuttingDown;
    private Coroutine autoAdvance;

    public static ImageOverlayController Create(Transform parent)
    {
        GameObject root = new GameObject("ImageOverlay", typeof(RectTransform), typeof(ImageOverlayController));
        root.transform.SetParent(parent, false);
        VideoFitHelper.StretchFill(root.GetComponent<RectTransform>());
        return root.GetComponent<ImageOverlayController>();
    }

    public void Setup(TriggerData trigger, bool endGame, GameObject buttonPrefab, System.Action<GameObject, bool> styleButtons)
    {
        currentTrigger = trigger;
        isEndGame = endGame;
        overlayOwner = "image-" + GetInstanceID();

        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        string display = VideoFitHelper.ResolveMediaDisplay(trigger, stage);
        string fit = VideoFitHelper.ResolveMediaFit(trigger, stage);

        StartCoroutine(LoadImage(trigger.media_url, display, fit));
        BuildTextOverlay(trigger);
        BuildControls(trigger, buttonPrefab, styleButtons);

        float duration = TriggerMediaHelper.OverlayDuration(trigger);
        autoAdvance = StartCoroutine(AutoAdvance(duration));
    }

    private IEnumerator LoadImage(string url, string display, string fit)
    {
        if (string.IsNullOrEmpty(url) || GameManager.Instance == null) yield break;

        string localPath = "file://" + GameManager.Instance.resourceManager.GetLocalPath(url);
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(localPath))
        {
            yield return www.SendWebRequest();
            if (isShuttingDown) yield break;
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Image overlay failed: {www.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            float aspect = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : VideoFitHelper.DefaultAspect;

            StageLayoutController layout = StageLayoutController.Ensure(
                GameManager.Instance.uiManager != null ? GameManager.Instance.uiManager.transform : transform);
            layout.ShowOverlay(texture, display, fit, aspect, overlayOwner);
        }
    }

    private void BuildTextOverlay(TriggerData trigger)
    {
        if (trigger == null || string.IsNullOrEmpty(trigger.text_overlay)) return;

        GameObject box = new GameObject("TextOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(transform, false);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.08f);
        rect.anchorMax = new Vector2(0.85f, 0.22f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Color bg = new Color(0f, 0f, 0f, 0.5f);
        if (!string.IsNullOrEmpty(trigger.text_overlay_bg_color))
            ColorUtility.TryParseHtmlString(trigger.text_overlay_bg_color, out bg);
        bg.a = Mathf.Clamp01(trigger.text_overlay_bg_opacity <= 0 ? 0.5f : trigger.text_overlay_bg_opacity / 100f);
        box.GetComponent<Image>().color = bg;
        box.GetComponent<Image>().raycastTarget = false;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(box.transform, false);
        VideoFitHelper.StretchFill(textGo.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = trigger.text_overlay;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 32f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private void BuildControls(TriggerData trigger, GameObject buttonPrefab, System.Action<GameObject, bool> styleButtons)
    {
        GameObject bar = new GameObject("Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(transform, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.15f, 0f);
        barRect.anchorMax = new Vector2(0.85f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 24f);
        barRect.sizeDelta = new Vector2(0f, 90f);

        HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.padding = new RectOffset(8, 8, 8, 8);

        if (trigger.allow_back_button)
            CreateButton(bar.transform, buttonPrefab, "Back", CloseAndReturnToStage);
        if (trigger.allow_skip_button)
            CreateButton(bar.transform, buttonPrefab, "Skip", CloseAndNavigate);

        styleButtons?.Invoke(bar, true);
    }

    private void CreateButton(Transform parent, GameObject prefab, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, parent);
            TriggerButton existing = go.GetComponent<TriggerButton>();
            if (existing != null) Destroy(existing);
            Button btn = go.GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
        else
        {
            go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        go.name = label;
        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
        Button button = go.GetComponent<Button>();
        if (button != null) button.onClick.AddListener(onClick);
    }

    private IEnumerator AutoAdvance(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!isShuttingDown) CloseAndNavigate();
    }

    private void CloseAndNavigate()
    {
        TriggerData triggerRef = currentTrigger;
        bool endGame = isEndGame;
        Destroy(gameObject);
        if (GameManager.Instance == null) return;
        if (endGame) GameManager.Instance.ReturnToStage();
        else GameManager.Instance.NavigateFromTrigger(triggerRef);
    }

    private void CloseAndReturnToStage()
    {
        Destroy(gameObject);
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToStage();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        if (autoAdvance != null) StopCoroutine(autoAdvance);
        if (StageLayoutController.Instance != null)
            StageLayoutController.Instance.HideOverlay(overlayOwner);
    }
}
