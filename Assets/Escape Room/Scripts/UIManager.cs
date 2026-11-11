using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    private List<GameObject> activeDialogs = new List<GameObject>();
    [Header("UI Text Elements")]
    public TextMeshProUGUI stageTitleText;      // Line 1: title
    public TextMeshProUGUI stageSubtitleText;   // Line 2: title_line2
    public TextMeshProUGUI stageScoreLine;      // Line 3: score (shown when title_line3_show_score == true)
    public TextMeshProUGUI stageTimeLine;       // Line 4: time  (shown when title_line4_show_time  == true)
    public TextMeshProUGUI teamNameText;        // Line 5: team name (shown when show_team_name == true)
    public GameObject timerObject;

    [Header("Font Management")]
    [Tooltip("The default font to use if the JSON font is not found.")]
    public TMP_FontAsset fallbackFont;
    [Tooltip("List of all available fonts for the game.")]
    public List<TMP_FontAsset> availableFonts = new List<TMP_FontAsset>();

    [Header("Branding")]
    public RawImage logoImage; // Use RawImage for easier loading from path
    public RectTransform logoContainer; // The parent or the RawImage itself for positioning

    [Header("Main Menu UI Elements")]
    public GameObject importButton; // Reference to the import zip button to show/hide it
    public GameObject clearButton;  // Reference to the clear data button to show/hide it

    [Header("Team Photo")]
    public RawImage teamPhotoImage; // Displays the captured team photo on stages with show_team_photo: true

    [Header("Button Container Setup")]
    public Transform buttonContainer; // A GameObject with a Vertical/Grid Layout Group
    public GameObject buttonPrefab;   // A Button prefab with a TriggerButton script

    [Header("Default Styles")]
    public Color defaultButtonColor = Color.red;

    [Header("Dynamic Overlays (Prefabs)")]
    public GameObject inputDialogPrefab;
    public GameObject hintDialogPrefab;
    public GameObject videoOverlayPrefab;
    public GameObject photoConfirmationPrefab;
    public GameObject stageVideoControlsPrefab;

    private PhotoCaptionBar photoCaptionBar;
    private GameObject volumeControlsRoot;
    private Image volumeFill;
    private Image volumeTrackImage;
    private Image volumeUpImage;
    private Image volumeDownImage;
    private TextMeshProUGUI volumePercentLabel;
    private TextMeshProUGUI volumeUpLabel;
    private TextMeshProUGUI volumeDownLabel;
    private int volumePercent = 100;
    private const int VolumeStep = 5;
    private GameObject progressRoot;
    private Image progressFill;
    private TextMeshProUGUI progressLabel;
    private Image titleBoxImage;
    private TitleAnimator titleAnimator;
    private RectTransform titleBoxRect;
    private VerticalLayoutGroup titleBoxLayout;
    private ContentSizeFitter titleBoxFitter;
    private bool titleBoxUsesStageLayout;
    private Vector2 titleBoxMenuPosition;
    private Vector2 titleBoxMenuSize;
    private float titleBoxMenuSpacing;
    private TextAnchor titleBoxMenuAlignment;
    private bool titleBoxMenuControlsSize;
    private bool titleBoxMenuExpandsHeight;
    private float titleMenuFontSize;

    private void Awake()
    {
        StageLayoutController.Ensure(transform);
        TransitionController.Ensure(transform);
        CollectibleManager.Ensure(transform);
        StageZoomController.Ensure(this);
    }

    /// <summary>True while a stage's own UI is on screen, not a trigger's or the menu.</summary>
    public bool IsStageUiShowing { get; private set; }

    /// <summary>
    /// The parts of the stage that zoom together, as the web player's stage container does.
    /// Trigger media, dialogs and the progress indicator are outside it and stay unscaled.
    /// </summary>
    public void GetZoomTargets(List<RectTransform> targets)
    {
        StageLayoutController layout = StageLayoutController.Instance;
        if (layout != null)
        {
            AddZoomTarget(targets, layout.TopPane);
            AddZoomTarget(targets, layout.LowerPane);
        }
        AddZoomTarget(targets, titleBoxRect != null ? titleBoxRect : stageTitleText != null ? stageTitleText.transform.parent : null);
        AddZoomTarget(targets, timerObject != null ? timerObject.transform : null);
        AddZoomTarget(targets, ScoreManager.Instance != null ? ScoreManager.Instance.GetPillRect() : null);
        AddZoomTarget(targets, buttonContainer);
        AddZoomTarget(targets, logoContainer);
        AddZoomTarget(targets, teamPhotoImage != null ? teamPhotoImage.transform.parent : null);
        AddZoomTarget(targets, volumeControlsRoot != null ? volumeControlsRoot.transform : null);
    }

    private void AddZoomTarget(List<RectTransform> targets, Transform target)
    {
        RectTransform rect = target as RectTransform;
        if (rect != null && rect != transform && !targets.Contains(rect))
            targets.Add(rect);
    }

    /// <summary>
    /// Lays out the zoom buttons at the top-right, below the timer and score or the volume
    /// controls when the JSON puts those in the same corner.
    /// </summary>
    private void PlaceZoomControls(UIContext ctx, StageData stage)
    {
        StageZoomController zoom = StageZoomController.Instance;
        if (zoom == null || stage == null || !stage.show_zoom_controls) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);
        float taken = 0f;

        bool showTimer = ctx.showTimer && timerObject != null;
        bool showPill = ctx.showScorePill && ScoreManager.Instance != null && ScoreManager.Instance.GetPillRect() != null;
        string hudPosition = showTimer && !string.IsNullOrEmpty(ctx.timerPosition) ? ctx.timerPosition : rules.defaultHudPosition;
        int pills = (showTimer ? 1 : 0) + (showPill ? 1 : 0);
        if (pills > 0 && IsTopRight(hudPosition))
        {
            float pillHeight = rules.hudLineHeight + rules.hudPaddingY * 2f;
            taken = (rules.hudInset + pills * pillHeight + (pills - 1) * rules.hudGap + rules.zoomGap) * scale;
        }

        if (ctx.showVolumeControls && volumeControlsRoot != null && IsTopRight(ctx.volumeControlsPosition)
            && !StageLayoutController.IsInLowerBand(stage, ctx.volumeControlsPosition))
        {
            RectTransform volume = volumeControlsRoot.GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(volume);
            float volumeBottom = -volume.anchoredPosition.y + volume.rect.height;
            taken = Mathf.Max(taken, volumeBottom + rules.zoomGap * scale);
        }

        zoom.Layout(taken);
    }

    private static bool IsTopRight(string position)
    {
        return !string.IsNullOrEmpty(position) && position.Trim().Equals("top-right", StringComparison.OrdinalIgnoreCase);
    }

    public GameObject ShowInputDialog(string promptText, System.Action<string> onSubmit, System.Action onCancel = null, bool submitOnEscapeWord = true)
    {
        if (inputDialogPrefab == null)
        {
            Debug.LogError("Input Dialog Prefab is not assigned in UIManager!");
            return null;
        }

        GameObject dialog = Instantiate(inputDialogPrefab, transform);
        activeDialogs.Add(dialog);
        GameManager.Instance.resourceManager.TrackStageObject(dialog);

        var controller = dialog.GetComponent<InputDialogController>();
        if (controller != null)
        {
            controller.Setup(promptText, onSubmit, onCancel, submitOnEscapeWord);
        }
        else
        {
            Debug.LogWarning("InputDialogPrefab is missing InputDialogController! Automatically triggering submit.");
            onSubmit?.Invoke("TEST_SUBMIT");
            Destroy(dialog, 2f);
        }

        ApplyFontToAllText(dialog);
        ApplyColorToAllButtons(dialog);
        if (controller != null) controller.ApplyKeyboardColors();
        return dialog;
    }

    public void ShowHintDialog(TriggerData trigger)
    {
        if (hintDialogPrefab == null)
        {
            Debug.LogError("Hint Dialog Prefab is not assigned in UIManager!");
            return;
        }

        GameObject dialog = Instantiate(hintDialogPrefab, transform);
        activeDialogs.Add(dialog);
        GameManager.Instance.resourceManager.TrackStageObject(dialog);

        var controller = dialog.GetComponent<HintDialogController>();
        if (controller != null)
        {
            controller.Setup(trigger);
        }

        ApplyFontToAllText(dialog);
        ApplyColorToAllButtons(dialog);
    }

    public void ShowTriggerMedia(TriggerData trigger, bool isEndGame = false)
    {
        if (trigger == null) return;
        StartCoroutine(ShowTriggerMediaRoutine(trigger, isEndGame));
    }

    private IEnumerator ShowTriggerMediaRoutine(TriggerData trigger, bool isEndGame)
    {
        GameManager.Instance.resourceManager.SetMusicDip(TriggerMediaHelper.DipsStageMusic(trigger));

        TransitionController trans = TransitionController.Ensure(transform);
        if (trans != null && !string.IsNullOrEmpty(trigger.video_transition_type)
            && !trigger.video_transition_type.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            yield return trans.PlayOverlay(
                trigger.video_transition_type,
                trigger.video_transition_duration,
                trigger.video_transition_direction,
                null);
        }

        if (TriggerMediaHelper.IsStill(trigger))
        {
            ImageOverlayController overlay = ImageOverlayController.Create(transform);
            activeDialogs.Add(overlay.gameObject);
            GameManager.Instance.resourceManager.TrackStageObject(overlay.gameObject);
            overlay.Setup(trigger, isEndGame, buttonPrefab, (root, video) => ApplyColorToAllButtons(root, video));
            ApplyFontToAllText(overlay.gameObject);
            Transform imageControls = overlay.transform.Find("Controls");
            if (imageControls != null)
                StyleVideoControlBar(imageControls, false, GetOverlayControlBottom(trigger));
            yield break;
        }

        if (TriggerMediaHelper.IsAudio(trigger) && !string.IsNullOrEmpty(trigger.media_url))
        {
            GameManager.Instance.resourceManager.PlayOneShot(
                trigger.media_url,
                trigger.video_volume,
                () =>
                {
                    if (isEndGame) GameManager.Instance.ReturnToStage();
                    else GameManager.Instance.NavigateFromTrigger(trigger);
                });
            yield break;
        }

        ShowVideoOverlay(trigger, isEndGame);
    }

    public void ShowVideoOverlay(TriggerData trigger, bool isEndGame = false)
    {
        if (videoOverlayPrefab == null)
        {
            Debug.LogError("Video Overlay Prefab is not assigned in UIManager!");
            return;
        }

        GameObject dialog = Instantiate(videoOverlayPrefab, transform);
        activeDialogs.Add(dialog);
        GameManager.Instance.resourceManager.TrackStageObject(dialog);

        var controller = dialog.GetComponent<VideoOverlayController>();
        if (controller != null)
        {
            controller.Setup(trigger, isEndGame);
        }

        ApplyFontToAllText(dialog);
        ApplyColorToAllButtons(dialog, isVideoOverlay: true);

        Button anyControl = dialog.GetComponentInChildren<Button>(true);
        if (anyControl != null)
            StyleVideoControlBar(anyControl.transform.parent, false, GetOverlayControlBottom(trigger));
    }

    /// <summary>
    /// Bottom edge of an overlay's control bar. top_window media plays inside the
    /// video_top window, so its controls sit above the lower band.
    /// </summary>
    private float GetOverlayControlBottom(TriggerData trigger)
    {
        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        StageLayoutController layout = StageLayoutController.Instance;
        bool topWindow = StageLayoutController.IsVideoTop(stage)
            && VideoFitHelper.IsTopWindow(VideoFitHelper.ResolveMediaDisplay(trigger, stage));
        return topWindow && layout != null && layout.LowerPane != null ? layout.LowerPane.rect.height : 0f;
    }

    /// <summary>
    /// Applies the JSON video_controls_* settings to a row of playback buttons. Overlay
    /// labels follow video_controls_font_size; on-stage bars scale with video_controls_size.
    /// </summary>
    private void StyleVideoControlBar(Transform bar, bool stageVideo, float bottomOffset = 0f)
    {
        RectTransform barRect = bar as RectTransform;
        if (barRect == null) return;

        GameConfig config = GameManager.Instance?.currentGameData?.game;
        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);
        float size = GetControlSize(config);
        float fontScale = GetControlFontScale(config);

        float fontSize = rules.videoControlFontSize * (stageVideo ? size : fontScale) * scale;
        float height = rules.videoControlHeight * size * scale;
        float paddingX = rules.videoControlPaddingX * size * scale;
        float bottom = (stageVideo ? rules.stageControlBottom : rules.overlayControlBottom) * scale + bottomOffset;

        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, bottom);
        barRect.sizeDelta = new Vector2(0f, height);

        HorizontalLayoutGroup group = bar.GetComponent<HorizontalLayoutGroup>();
        if (group == null) group = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = rules.videoControlGap * size * scale;
        group.padding = new RectOffset(0, 0, 0, 0);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        TMP_FontAsset font = GetFont(config != null ? config.video_controls_font : null);
        Sprite sprite = RoundedSprite.Get(rules.videoControlCornerRadius * scale);

        foreach (Button button in bar.GetComponentsInChildren<Button>(true))
        {
            ApplySlicedSprite(button.GetComponent<Image>(), sprite);

            float width = paddingX * 2f;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                if (font != null) label.font = font;
                label.enableAutoSizing = false;
                label.fontSize = fontSize;
                label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Overflow;
                label.alignment = TextAlignmentOptions.Center;
                width += label.GetPreferredValues(label.text).x;
            }

            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
        }
    }

    private static float GetControlSize(GameConfig config)
    {
        return config != null && config.video_controls_size > 0f ? config.video_controls_size : 1f;
    }

    private static float GetControlFontScale(GameConfig config)
    {
        return config != null && config.video_controls_font_size > 0f ? config.video_controls_font_size : 1f;
    }

    private static float GetControlSpacing(GameConfig config)
    {
        return config != null && config.video_controls_spacing > 0f ? config.video_controls_spacing : 1f;
    }

    /// <summary>
    /// Spawns the on-stage background-video control bar. Uses stageVideoControlsPrefab
    /// when assigned; otherwise builds four buttons from buttonPrefab.
    /// </summary>
    public GameObject ShowStageVideoControls(
        StageVideoController owner,
        bool looping,
        bool showSkip,
        bool showBack,
        bool showReplay,
        bool showPlayPause,
        Transform parent = null)
    {
        if (owner == null) return null;

        Transform host = parent != null ? parent : transform;

        GameObject root;
        if (stageVideoControlsPrefab != null)
        {
            root = Instantiate(stageVideoControlsPrefab, host);
            ApplyStageVideoControlBarRect(root);
            if (root.GetComponentInChildren<Button>(true) == null)
            {
                if (showBack) CreateControlButton(root.transform, "Back");
                if (showReplay) CreateControlButton(root.transform, "Replay");
                if (showPlayPause) CreateControlButton(root.transform, "Play/Pause");
                if (showSkip) CreateControlButton(root.transform, "Skip");
            }
        }
        else
        {
            root = BuildStageVideoControlBar(showSkip, showBack, showReplay, showPlayPause, host);
        }

        GameManager.Instance.resourceManager.TrackStageObject(root);

        WireControlButton(root, "Skip", showSkip, owner.Skip);
        WireControlButton(root, "Back", showBack, owner.GoBack);
        WireControlButton(root, "Replay", showReplay, owner.Replay);
        WireControlButton(root, "PlayPause", showPlayPause, owner.TogglePlayPause);
        WireControlButton(root, "Play/Pause", showPlayPause, owner.TogglePlayPause);

        GameConfig config = GameManager.Instance?.currentGameData?.game;
        string overlayFont = config != null ? config.video_controls_font : null;
        if (!string.IsNullOrEmpty(overlayFont))
        {
            TMP_FontAsset font = GetFont(overlayFont);
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.font = font;
        }

        ApplyColorToAllButtons(root, isVideoOverlay: true);
        StyleVideoControlBar(root.transform, true);
        return root;
    }

    private GameObject BuildStageVideoControlBar(bool showSkip, bool showBack, bool showReplay, bool showPlayPause, Transform parent)
    {
        GameObject root = new GameObject("StageVideoControls", typeof(RectTransform));
        root.transform.SetParent(parent != null ? parent : transform, false);
        ApplyStageVideoControlBarRect(root);

        if (root.GetComponent<HorizontalLayoutGroup>() == null)
        {
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(8, 8, 8, 8);
        }

        if (showBack) CreateControlButton(root.transform, "Back");
        if (showReplay) CreateControlButton(root.transform, "Replay");
        if (showPlayPause) CreateControlButton(root.transform, "Play/Pause");
        if (showSkip) CreateControlButton(root.transform, "Skip");

        return root;
    }

    private static void ApplyStageVideoControlBarRect(GameObject root)
    {
        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null) rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0f);
        rect.anchorMax = new Vector2(0.85f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(0f, 90f);
    }

    private void CreateControlButton(Transform parent, string label)
    {
        GameObject go;
        if (buttonPrefab != null)
        {
            go = Instantiate(buttonPrefab, parent);
            TriggerButton triggerScript = go.GetComponent<TriggerButton>();
            if (triggerScript != null) DestroyImmediate(triggerScript);
            Button existing = go.GetComponent<Button>();
            if (existing != null) existing.onClick.RemoveAllListeners();
        }
        else
        {
            go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        go.name = label;
        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    private void WireControlButton(GameObject root, string name, bool visible, UnityEngine.Events.UnityAction onClick)
    {
        Transform child = root.transform.Find(name);
        if (child == null)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    child = t;
                    break;
                }
            }
        }

        if (child == null) return;

        child.gameObject.SetActive(visible);
        Button btn = child.GetComponent<Button>();
        if (btn == null) return;
        btn.interactable = visible;
        btn.onClick.RemoveAllListeners();
        if (visible && onClick != null)
            btn.onClick.AddListener(onClick);
    }

    public void ShowPhotoConfirmation(TriggerData trigger)
    {
        if (photoConfirmationPrefab == null)
        {
            Debug.LogError("Photo Confirmation Prefab is not assigned in UIManager!");
            return;
        }

        GameObject dialog = Instantiate(photoConfirmationPrefab, transform);
        activeDialogs.Add(dialog);
        GameManager.Instance.resourceManager.TrackStageObject(dialog);

        var controller = dialog.GetComponent<PhotoConfirmationController>();
        if (controller != null)
        {
            controller.Setup(trigger);
        }

        ApplyFontToAllText(dialog);
        ApplyColorToAllButtons(dialog);
    }

    public void CloseAllDialogs()
    {
        // Clean up any empty/null references before proceeding
        activeDialogs.RemoveAll(d => d == null);

        foreach (var dialog in activeDialogs)
        {
            if (dialog != null)
            {
                Destroy(dialog);
            }
        }
        activeDialogs.Clear();
    }

    /// <summary>
    /// Applies the current game font to ALL TextMeshProUGUI components inside a GameObject.
    /// Used after spawning dialog prefabs (input, video, hint, photo) so their text
    /// matches the JSON-driven font for the loaded game.
    /// </summary>
    public void ApplyFontToAllText(GameObject root)
    {
        string currentFontName = GetCurrentGameFontName();
        TMP_FontAsset font = GetFont(currentFontName);
        if (font == null) return;

        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            tmp.font = font;
        }
    }

    /// <summary>
    /// Applies button colors from the current game config to all Button components inside a GameObject.
    /// For video overlays, uses video_controls_color and video_controls_text_color.
    /// For other dialogs (input, hint, photo), uses default_button_color.
    /// </summary>
    public void ApplyColorToAllButtons(GameObject root, bool isVideoOverlay = false)
    {
        GameConfig config = GameManager.Instance?.currentGameData?.game;
        if (config == null) return;

        // Determine which color set to use
        string colorHex = isVideoOverlay ? config.video_controls_color : config.default_button_color;
        string textColorHex = isVideoOverlay ? config.video_controls_text_color : null;
        string pressedHex = isVideoOverlay ? null : config.default_button_pressed_color;

        Color buttonColor = defaultButtonColor;
        if (!string.IsNullOrEmpty(colorHex))
            ColorUtility.TryParseHtmlString(colorHex, out buttonColor);

        Color textColor = Color.white;
        bool hasTextColor = !string.IsNullOrEmpty(textColorHex) && ColorUtility.TryParseHtmlString(textColorHex, out textColor);

        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            ApplyButtonColors(btn, buttonColor, pressedHex);

            if (hasTextColor)
            {
                TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.color = textColor;
            }
        }
    }

    /// <summary>
    /// Sets a button's resting color and, when the JSON gives one, its exact pressed color.
    /// Tinting multiplies with the image color, so the image is kept white in that case.
    /// </summary>
    public static void ApplyButtonColors(Button button, Color normal, string pressedHex)
    {
        Image image = button.GetComponent<Image>();
        Color pressed = normal;
        bool hasPressed = !string.IsNullOrEmpty(pressedHex) && ColorUtility.TryParseHtmlString(pressedHex, out pressed);
        if (!hasPressed || image == null)
        {
            if (image != null) image.color = normal;
            return;
        }

        image.color = Color.white;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = normal;
        colors.selectedColor = normal;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.5f);
        button.colors = colors;
    }

    /// <summary>
    /// Returns the font name for the currently active stage.
    /// Falls back to the video_controls_font from the game config, then null.
    /// </summary>
    private string GetCurrentGameFontName()
    {
        // Prefer the current stage's font
        if (GameManager.Instance != null && GameManager.Instance.currentStage != null)
        {
            string stageFont = GameManager.Instance.currentStage.title_text_font_family;
            if (!string.IsNullOrEmpty(stageFont))
                return stageFont;
        }

        // Fall back to game-level video controls font
        if (GameManager.Instance != null &&
            GameManager.Instance.currentGameData != null &&
            GameManager.Instance.currentGameData.game != null)
        {
            string gameFont = GameManager.Instance.currentGameData.game.video_controls_font;
            if (!string.IsNullOrEmpty(gameFont))
                return gameFont;
        }

        return null; // Will use fallbackFont via GetFont(null)
    }

    /// <summary>
    /// Looks up a JSON font name in the available fonts list, resolving CSS names such as
    /// sans-serif first. Missing fonts use the default sans-serif, then the fallback font.
    /// </summary>
    public TMP_FontAsset GetFont(string fontName)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        TMP_FontAsset font = FindFont(rules.ResolveFontName(fontName));
        if (font != null) return font;

        if (!string.IsNullOrEmpty(fontName))
            Debug.LogWarning($"Font '{fontName}' not found in availableFonts. Using default font.");

        // fallbackFont might be null if not assigned, which is fine, TMPro has a built-in default
        return FindFont(rules.ResolveFontName(null)) ?? fallbackFont;
    }

    private TMP_FontAsset FindFont(string fontName)
    {
        foreach (var font in availableFonts)
        {
            if (font != null && font.name.Equals(fontName, StringComparison.OrdinalIgnoreCase))
                return font;
        }
        return null;
    }

    public void ShowGameSelection(string[] jsonFiles)
    {
        IsStageUiShowing = false;
        stageTitleText.text = "Select a Game";
        stageSubtitleText.text = "";
        timerObject.SetActive(false);
        logoImage.gameObject.SetActive(false);
        if (stageScoreLine != null) stageScoreLine.gameObject.SetActive(false);
        if (stageTimeLine != null) stageTimeLine.gameObject.SetActive(false);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.SetPillVisible(false);

        if (importButton != null) importButton.SetActive(true);
        if (clearButton != null) clearButton.SetActive(true);

        StageLayoutController layout = StageLayoutController.Ensure(transform);
        layout.ResetToMenu();
        HidePhotoCaption();
        SetVolumeControlsVisible(false);
        SetProgressVisible(false);
        ApplyTitlePresentation(null);
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.SetVisible(false);

        // Clear game selection buttons (not tracked by ResourceManager)
        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = buttonContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        foreach (string filePath in jsonFiles)
        {
            GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = System.IO.Path.GetFileNameWithoutExtension(filePath);
            
            // Remove any existing trigger script and just add a click listener
            Destroy(btnObj.GetComponent<TriggerButton>());
            
            Button btn = btnObj.GetComponent<Button>();
            string fileToLoad = filePath; // local copy for closure
            btn.onClick.AddListener(() => GameManager.Instance.LoadGameData(fileToLoad));
        }
    }

    /// <summary>
    /// Single unified method that renders UI based on a UIContext.
    /// Works for both stages (all elements visible per JSON) and triggers
    /// (most elements hidden since triggers don't own those fields).
    /// </summary>
    public void ApplyUIContext(UIContext ctx)
    {
        // 0. Close any open dialogs from a previous trigger
        CloseAllDialogs();

        // 1. Destroy all existing buttons (stage buttons or leftover game-selection buttons)
        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = buttonContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        // Hide import & clear buttons when a game/stage/trigger starts
        if (importButton != null) importButton.SetActive(false);
        if (clearButton != null) clearButton.SetActive(false);

        StageLayoutController layout = StageLayoutController.Ensure(transform);
        StageData currentStage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        if (ctx.isStage && currentStage != null)
            layout.ApplyPaneLayout(currentStage);

        // 2. Title (Line 1) — disable if null/empty
        if (stageTitleText != null)
        {
            stageTitleText.gameObject.SetActive(!string.IsNullOrEmpty(ctx.title));
            stageTitleText.text = ctx.title ?? "";
            if (stageTitleText.gameObject.activeSelf)
                stageTitleText.font = GetFont(ctx.titleFontFamily);
        }

        // 3. Subtitle (Line 2) — disable if null/empty
        if (stageSubtitleText != null)
        {
            stageSubtitleText.gameObject.SetActive(!string.IsNullOrEmpty(ctx.subtitle));
            stageSubtitleText.text = ctx.subtitle ?? "";
            if (stageSubtitleText.gameObject.activeSelf)
                stageSubtitleText.font = GetFont(ctx.titleFontFamily);
        }

        // 4. Score (Line 3) — title_line3_show_score, independent of the score pill
        if (stageScoreLine != null)
        {
            if (ctx.showScoreLine && ScoreManager.Instance != null)
            {
                stageScoreLine.gameObject.SetActive(true);
                stageScoreLine.text = $"Score: {ScoreManager.Instance.GetCurrentScore()}";
                stageScoreLine.font = GetFont(ctx.titleFontFamily);
            }
            else
            {
                stageScoreLine.gameObject.SetActive(false);
            }
        }

        // 5. Time (Line 4)
        if (stageTimeLine != null)
        {
            if (ctx.showTime && TimerManager.Instance != null)
            {
                stageTimeLine.gameObject.SetActive(true);
                System.TimeSpan time = System.TimeSpan.FromSeconds(TimerManager.Instance.GetCurrentTime());
                stageTimeLine.text = $"TIME: {time.ToString(@"mm\:ss")}";
                stageTimeLine.font = GetFont(ctx.titleFontFamily);
            }
            else
            {
                stageTimeLine.gameObject.SetActive(false);
            }
        }

        // 5b. Team Name
        if (teamNameText != null)
        {
            if (ctx.showTeamName && !string.IsNullOrEmpty(ctx.teamName))
            {
                teamNameText.gameObject.SetActive(true);
                teamNameText.text = ctx.teamName;
                teamNameText.font = GetFont(ctx.titleFontFamily);
            }
            else
            {
                teamNameText.gameObject.SetActive(false);
            }
        }

        // 6. Timer and score pill — show_timer and show_score, stacked at timer_position
        ApplyHud(ctx);

        // 7. Logo
        if (logoImage != null)
        {
            bool hasValidLogoUrl = false;
            if (GameManager.Instance != null && 
                GameManager.Instance.currentGameData != null && 
                GameManager.Instance.currentGameData.game != null)
            {
                hasValidLogoUrl = !string.IsNullOrWhiteSpace(GameManager.Instance.currentGameData.game.logo_url);
            }
            logoImage.gameObject.SetActive(ctx.showLogo && hasValidLogoUrl);
        }

        // 8. Team Photo — show if stage has show_team_photo:true and a photo was captured
        bool photoBesideTitle = false;
        if (teamPhotoImage != null)
        {
            bool showPhoto = ctx.showTeamPhoto
                && PhotoManager.Instance != null
                && PhotoManager.Instance.TeamPhoto != null;

            teamPhotoImage.gameObject.SetActive(showPhoto);
            if (showPhoto)
            {
                teamPhotoImage.texture = PhotoManager.Instance.TeamPhoto;
                photoBesideTitle = PlaceTeamPhoto(currentStage);
                if (ctx.showPhotoCaption)
                    ShowPhotoCaption(currentStage, ctx.titleFontFamily);
                else
                    HidePhotoCaption();
            }
            else
            {
                HidePhotoCaption();
            }
        }
        else
        {
            HidePhotoCaption();
        }

        ApplyTitlePresentation(ctx.isStage ? currentStage : null, photoBesideTitle);

        // 9. Spawn buttons from context (stages have triggers list, triggers have empty list)
        foreach (var trigger in ctx.buttons)
        {
            CreateTriggerButton(trigger, ctx.titleFontFamily);
        }

        if (ctx.isStage && currentStage != null)
            layout.PlaceButtonContainer(currentStage);

        SetVolumeControlsVisible(ctx.showVolumeControls, ctx.volumeControlsPosition);
        UpdateProgressIndicator(ctx.isStage);

        if (ctx.isStage && currentStage != null)
            CollectibleManager.Ensure(transform).ShowForStage(currentStage);
        else if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.SetVisible(false);

        IsStageUiShowing = ctx.isStage && currentStage != null;
        if (IsStageUiShowing)
            PlaceZoomControls(ctx, currentStage);
    }

    private void CreateTriggerButton(TriggerData trigger, string fontName = null)
    {
        GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
        // Register with ResourceManager so it gets destroyed on stage change
        GameManager.Instance.resourceManager.TrackStageObject(btnObj);

        // Set the color
        GameConfig config = GameManager.Instance?.currentGameData?.game;
        Color buttonColor = defaultButtonColor;
        if (Subtitles.IsToggle(trigger))
        {
            buttonColor = Subtitles.ToggleColor(trigger);
        }
        else if (!string.IsNullOrEmpty(trigger.button_color)
            && ColorUtility.TryParseHtmlString(trigger.button_color, out Color newColor))
        {
            buttonColor = newColor;
        }
        else if (config != null && !string.IsNullOrEmpty(config.default_button_color)
            && ColorUtility.TryParseHtmlString(config.default_button_color, out Color gameColor))
        {
            buttonColor = gameColor;
        }

        string pressedHex = !string.IsNullOrEmpty(trigger.button_pressed_color)
            ? trigger.button_pressed_color
            : config?.default_button_pressed_color;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null) ApplyButtonColors(btn, buttonColor, pressedHex);

        UiLayoutRules rules = UiLayoutRules.Instance;
        ApplySlicedSprite(
            btnObj.GetComponent<Image>(),
            RoundedSprite.Get(rules.buttonCornerRadius * rules.Scale(transform as RectTransform)));

        // Set the text and font
        TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = Subtitles.IsToggle(trigger) ? Subtitles.ToggleText(trigger) : trigger.button_text;
            btnText.font = GetFont(fontName);
            btnText.color = ResolveButtonTextColor(trigger.button_text_color, buttonColor);
        }

        // Initialize the logic (We will create this script next)
        TriggerButton script = btnObj.GetComponent<TriggerButton>();
        if (script != null) script.Initialize(trigger);
    }

    /// <summary>
    /// Relabels and recolours toggle_subtitles buttons after the setting flips, then
    /// re-lays the row since the label width may change.
    /// </summary>
    public void RefreshSubtitleToggles()
    {
        if (buttonContainer == null) return;
        GameConfig config = GameManager.Instance?.currentGameData?.game;
        bool changed = false;

        foreach (TriggerButton triggerButton in buttonContainer.GetComponentsInChildren<TriggerButton>())
        {
            TriggerData trigger = triggerButton.Data;
            if (!Subtitles.IsToggle(trigger)) continue;

            Color color = Subtitles.ToggleColor(trigger);
            string pressedHex = !string.IsNullOrEmpty(trigger.button_pressed_color)
                ? trigger.button_pressed_color
                : config?.default_button_pressed_color;
            Button btn = triggerButton.GetComponent<Button>();
            if (btn != null) ApplyButtonColors(btn, color, pressedHex);

            TextMeshProUGUI label = triggerButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = Subtitles.ToggleText(trigger);
                label.color = ResolveButtonTextColor(trigger.button_text_color, color);
            }
            changed = true;
        }

        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        if (changed && IsStageUiShowing && stage != null && StageLayoutController.Instance != null)
            StageLayoutController.Instance.PlaceButtonContainer(stage);
    }

    /// <summary>
    /// Uses JSON button_text_color when set. Otherwise: white button → black text, any other button → white text.
    /// </summary>
    public static Color ResolveButtonTextColor(string textColorHex, Color buttonColor)
    {
        if (!string.IsNullOrEmpty(textColorHex) && ColorUtility.TryParseHtmlString(textColorHex, out Color parsed))
            return parsed;

        return IsWhite(buttonColor) ? Color.black : Color.white;
    }

    private static bool IsWhite(Color color)
    {
        return color.r >= 0.95f && color.g >= 0.95f && color.b >= 0.95f;
    }

    public void SetupGameLogo(GameConfig config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.logo_url))
        {
            Debug.Log("No logo found in JSON. Hiding logo element.");
            logoImage.gameObject.SetActive(false);
            return;
        }

        // If it exists, make sure the object is active
        logoImage.gameObject.SetActive(true);

        // Handle Size and Position. logo_size_percentage is the logo's width in design pixels.
        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);
        float width = (config.logo_size_percentage > 0 ? config.logo_size_percentage : 100) * scale;
        logoContainer.localScale = Vector3.one;
        logoContainer.sizeDelta = new Vector2(width, width);
        SetUIPosition(logoContainer, config.logo_position, rules.edgeInset * scale);

        // Load the image (coroutine runs on UIManager so CleanupStage won't kill it)
        string fullPath = GameManager.Instance.resourceManager.GetLocalPath(config.logo_url);
        if (File.Exists(fullPath))
        {
            StartCoroutine(LoadLogoRoutine(config.logo_url));
        }
        else
        {
            Debug.LogWarning($"Logo file missing from media folder: {fullPath}");
            logoImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator LoadLogoRoutine(string url)
    {
        string localPath = "file://" + GameManager.Instance.resourceManager.GetLocalPath(url);

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(localPath))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Logo failed to load: {www.error}");
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                logoImage.texture = texture;
                //logoImage.SetNativeSize();

                if (texture != null && texture.width > 0)
                {
                    Vector2 size = logoContainer.sizeDelta;
                    size.y = size.x * texture.height / texture.width;
                    logoContainer.sizeDelta = size;
                }
            }
        }
    }

    public void SetUIPosition(RectTransform container, string position, float inset = 20f)
    {
        if (container == null) return;

        // Default fallback to top-center if the string is null/empty
        if (string.IsNullOrEmpty(position)) position = "top-centre";

        switch (position.ToLower())
        {
            // --- TOP ROW ---
            case "top-left":
                container.anchorMin = new Vector2(0, 1);
                container.anchorMax = new Vector2(0, 1);
                container.pivot = new Vector2(0, 1);
                container.anchoredPosition = new Vector2(inset, -inset); // Small inset from edge
                break;

            case "top-centre":
            case "top-center":
                container.anchorMin = new Vector2(0.5f, 1);
                container.anchorMax = new Vector2(0.5f, 1);
                container.pivot = new Vector2(0.5f, 1);
                container.anchoredPosition = new Vector2(0, -inset);
                break;

            case "top-right":
                container.anchorMin = new Vector2(1, 1);
                container.anchorMax = new Vector2(1, 1);
                container.pivot = new Vector2(1, 1);
                container.anchoredPosition = new Vector2(-inset, -inset);
                break;

            // --- MIDDLE ROW ---
            case "middle-left":
                container.anchorMin = new Vector2(0, 0.5f);
                container.anchorMax = new Vector2(0, 0.5f);
                container.pivot = new Vector2(0, 0.5f);
                container.anchoredPosition = new Vector2(inset, 0);
                break;

            case "middle-centre":
            case "middle-center":
            case "middle":
                container.anchorMin = new Vector2(0.5f, 0.5f);
                container.anchorMax = new Vector2(0.5f, 0.5f);
                container.pivot = new Vector2(0.5f, 0.5f);
                container.anchoredPosition = Vector2.zero;
                break;

            case "middle-right":
                container.anchorMin = new Vector2(1, 0.5f);
                container.anchorMax = new Vector2(1, 0.5f);
                container.pivot = new Vector2(1, 0.5f);
                container.anchoredPosition = new Vector2(-inset, 0);
                break;

            // --- BOTTOM ROW ---
            case "bottom-left":
                container.anchorMin = new Vector2(0, 0);
                container.anchorMax = new Vector2(0, 0);
                container.pivot = new Vector2(0, 0);
                container.anchoredPosition = new Vector2(inset, inset);
                break;

            case "bottom-centre":
            case "bottom-center":
                container.anchorMin = new Vector2(0.5f, 0);
                container.anchorMax = new Vector2(0.5f, 0);
                container.pivot = new Vector2(0.5f, 0);
                container.anchoredPosition = new Vector2(0, inset);
                break;

            case "bottom-right":
                container.anchorMin = new Vector2(1, 0);
                container.anchorMax = new Vector2(1, 0);
                container.pivot = new Vector2(1, 0);
                container.anchoredPosition = new Vector2(-inset, inset);
                break;

            default:
                Debug.LogWarning($"Unknown UI position: {position}. Defaulting to top-center.");
                SetUIPosition(container, "top-centre", inset);
                break;
        }
    }

    /// <summary>
    /// Shows the timer and score pills. Both follow the JSON timer_position, stacked with
    /// the timer first; a score with no timer has no JSON position and uses the default.
    /// </summary>
    private void ApplyHud(UIContext ctx)
    {
        RectTransform timerRect = timerObject != null ? timerObject.GetComponent<RectTransform>() : null;
        RectTransform pillRect = ScoreManager.Instance != null ? ScoreManager.Instance.GetPillRect() : null;
        bool showTimer = ctx.showTimer && timerRect != null;
        bool showPill = ctx.showScorePill && pillRect != null;

        if (timerObject != null) timerObject.SetActive(showTimer);
        if (ScoreManager.Instance != null) ScoreManager.Instance.SetPillVisible(showPill);
        if (!showTimer && !showPill) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);
        float inset = rules.hudInset * scale;
        float stackStep = (rules.hudLineHeight + rules.hudPaddingY * 2f + rules.hudGap) * scale;

        string position = showTimer && !string.IsNullOrEmpty(ctx.timerPosition)
            ? ctx.timerPosition
            : rules.defaultHudPosition;
        bool top = position.StartsWith("top", StringComparison.OrdinalIgnoreCase);

        if (showTimer)
        {
            StyleHudPill(timerRect, TimerManager.Instance != null ? TimerManager.Instance.timerText : null);
            SetUIPosition(timerRect, position, inset);
            if (showPill && !top) timerRect.anchoredPosition += new Vector2(0f, stackStep);
        }

        if (showPill)
        {
            StyleHudPill(pillRect, ScoreManager.Instance.scoreText);
            SetUIPosition(pillRect, position, inset);
            if (showTimer && top) pillRect.anchoredPosition -= new Vector2(0f, stackStep);
        }
    }

    private void StyleHudPill(RectTransform pill, TextMeshProUGUI label)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);

        Image background = pill.GetComponent<Image>();
        if (background == null) background = pill.gameObject.AddComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;
        ApplySlicedSprite(background, RoundedSprite.Get(rules.hudCornerRadius * scale));

        HorizontalLayoutGroup group = pill.GetComponent<HorizontalLayoutGroup>();
        if (group == null) group = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        int paddingX = Mathf.RoundToInt(rules.hudPaddingX * scale);
        int paddingY = Mathf.RoundToInt(rules.hudPaddingY * scale);
        group.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        ContentSizeFitter fitter = pill.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = pill.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (label == null) return;

        TMP_FontAsset font = GetFont(rules.hudFontName);
        if (font != null) label.font = font;
        label.enableAutoSizing = false;
        label.fontSize = rules.hudFontSize * scale;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        LayoutElement element = label.GetComponent<LayoutElement>();
        if (element == null) element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = rules.hudLineHeight * scale;
    }

    /// <summary>
    /// Sizes and places the team photo. A background with a printed photo slot takes the
    /// photo inside that slot; otherwise it fills a column left of the title.
    /// Returns true when the title must move beside the photo.
    /// </summary>
    private bool PlaceTeamPhoto(StageData stage)
    {
        RectTransform container = teamPhotoImage.rectTransform.parent as RectTransform;
        RectTransform canvas = transform as RectTransform;
        Texture photo = teamPhotoImage.texture;
        if (container == null || canvas == null || photo == null) return false;

        UiLayoutRules rules = UiLayoutRules.Instance;
        float canvasWidth = canvas.rect.width;
        float canvasHeight = canvas.rect.height;
        bool videoTop = StageLayoutController.IsVideoTop(stage);
        float lowerHeight = videoTop && StageLayoutController.Instance != null
            ? StageLayoutController.Instance.LowerPane.rect.height
            : 0f;
        float photoAspect = (float)photo.width / photo.height;

        UiLayoutRules.PhotoOverlayRule overlay = rules.FindPhotoOverlay(stage);
        if (overlay != null)
        {
            float windowHeight = canvasHeight - lowerHeight;
            float fit = Mathf.Min(canvasWidth / overlay.backgroundSize.x, windowHeight / overlay.backgroundSize.y);
            float left = (canvasWidth - overlay.backgroundSize.x * fit) * 0.5f;
            float down = (windowHeight - overlay.backgroundSize.y * fit) * 0.5f;
            Rect slot = overlay.photoRect;

            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(0f, 1f);
            container.pivot = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(left + slot.x * fit, -(down + slot.y * fit));
            container.sizeDelta = new Vector2(slot.width * fit, slot.height * fit);

            // Centre-crop the photo to fill the slot.
            float slotAspect = slot.width / slot.height;
            teamPhotoImage.uvRect = photoAspect > slotAspect
                ? new Rect((1f - slotAspect / photoAspect) * 0.5f, 0f, slotAspect / photoAspect, 1f)
                : new Rect(0f, (1f - photoAspect / slotAspect) * 0.5f, 1f, photoAspect / slotAspect);
            return false;
        }

        float scale = rules.Scale(canvas);
        float margin = rules.titleSideMargin * scale;
        float columnWidth = (canvasWidth - margin * 2f) * rules.photoColumnRatio;
        float maxHeight = videoTop ? lowerHeight : canvasHeight * rules.photoMaxHeightRatio;
        float width = Mathf.Min(columnWidth, maxHeight * photoAspect);

        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.zero;
        container.pivot = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = new Vector2(
            margin + columnWidth * 0.5f,
            videoTop ? lowerHeight * 0.5f : canvasHeight * 0.5f);
        container.sizeDelta = new Vector2(width, width / photoAspect);
        teamPhotoImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        return true;
    }

    private void ShowPhotoCaption(StageData stage, string fontFamily)
    {
        if (teamPhotoImage == null || stage == null)
        {
            HidePhotoCaption();
            return;
        }

        photoCaptionBar = PhotoCaptionBar.Ensure(teamPhotoImage);
        float photoWidth = teamPhotoImage.rectTransform.rect.width;
        RectTransform canvasRect = transform as RectTransform;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 1920f;
        photoCaptionBar.Show(stage, GetFont(fontFamily), photoWidth, canvasWidth);
    }

    private void HidePhotoCaption()
    {
        if (photoCaptionBar == null && teamPhotoImage != null)
        {
            Transform existing = teamPhotoImage.transform.Find("PhotoCaptionBar");
            if (existing != null) photoCaptionBar = existing.GetComponent<PhotoCaptionBar>();
        }
        if (photoCaptionBar != null) photoCaptionBar.Hide();
    }

    private void SetVolumeControlsVisible(bool visible, string position = null)
    {
        if (!visible)
        {
            if (volumeControlsRoot != null) volumeControlsRoot.SetActive(false);
            return;
        }

        EnsureVolumeControls();
        volumeControlsRoot.SetActive(true);
        volumePercent = Mathf.Clamp(Mathf.RoundToInt(AudioListener.volume * 100f), 0, 100);

        string target = string.IsNullOrEmpty(position) ? "bottom-right" : position;
        StageData stage = GameManager.Instance != null ? GameManager.Instance.currentStage : null;
        bool inLowerBand = StageLayoutController.IsInLowerBand(stage, target);

        ApplyVolumeChrome(inLowerBand);
        ApplyVolumeDisplay();
        PlaceVolumeControls(target, inLowerBand);
    }

    /// <summary>
    /// Follows the JSON volume_controls_position. In a video_top lower band the
    /// controls stay inside the band, vertically centered.
    /// </summary>
    private void PlaceVolumeControls(string position, bool inLowerBand)
    {
        RectTransform root = volumeControlsRoot.GetComponent<RectTransform>();
        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);

        if (!inLowerBand)
        {
            SetUIPosition(root, position, rules.edgeInset * scale);
            return;
        }

        SetUIPosition(root, position, rules.hudInset * scale);
        float bandHeight = StageLayoutController.Instance.LowerPane.rect.height;
        Vector2 placed = root.anchoredPosition;
        placed.y = (bandHeight - root.sizeDelta.y) * 0.5f;
        root.anchoredPosition = placed;
    }

    private void EnsureVolumeControls()
    {
        if (volumeControlsRoot != null) return;

        volumeControlsRoot = new GameObject("VolumeControls", typeof(RectTransform), typeof(VerticalLayoutGroup));
        volumeControlsRoot.transform.SetParent(transform, false);
        RectTransform rootRect = volumeControlsRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(240f, 260f);

        VerticalLayoutGroup layout = volumeControlsRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Sprite rounded = GetHudButtonSprite();
        volumeUpImage = CreateVolumeButton("Up", "↑ UP", VolumeStep, rounded, out volumeUpLabel);
        CreateVolumeFillPanel(rounded);
        volumeDownImage = CreateVolumeButton("Down", "↓ DOWN", -VolumeStep, rounded, out volumeDownLabel);

        volumeControlsRoot.transform.SetAsLastSibling();
        volumeControlsRoot.SetActive(false);
    }

    private Image CreateVolumeButton(string name, string label, int delta, Sprite sprite, out TextMeshProUGUI labelTmp)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(volumeControlsRoot.transform, false);

        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;
        image.color = defaultButtonColor;
        ApplySlicedSprite(image, sprite);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => AdjustVolume(delta));

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        VideoFitHelper.StretchFill(textGo.GetComponent<RectTransform>());
        labelTmp = textGo.GetComponent<TextMeshProUGUI>();
        labelTmp.richText = true;
        labelTmp.text = label;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = Color.white;
        labelTmp.raycastTarget = false;
        labelTmp.enableWordWrapping = false;
        labelTmp.overflowMode = TextOverflowModes.Overflow;
        return image;
    }

    private void CreateVolumeFillPanel(Sprite sprite)
    {
        GameObject panel = new GameObject("Fill", typeof(RectTransform), typeof(LayoutElement));
        panel.transform.SetParent(volumeControlsRoot.transform, false);

        GameObject track = CreateUiImage("Track", panel.transform, new Color(0f, 0f, 0f, 0.35f));
        VideoFitHelper.StretchFill(track.GetComponent<RectTransform>());
        volumeTrackImage = track.GetComponent<Image>();
        ApplySlicedSprite(volumeTrackImage, sprite);
        track.AddComponent<Outline>();

        GameObject fillGo = CreateUiImage("Amount", panel.transform, defaultButtonColor);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        volumeFill = fillGo.GetComponent<Image>();
        ApplySlicedSprite(volumeFill, sprite);

        GameObject textGo = new GameObject("Percent", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        VideoFitHelper.StretchFill(textGo.GetComponent<RectTransform>());
        volumePercentLabel = textGo.GetComponent<TextMeshProUGUI>();
        volumePercentLabel.text = "Volume 100%";
        volumePercentLabel.alignment = TextAlignmentOptions.Center;
        volumePercentLabel.fontStyle = FontStyles.Bold;
        volumePercentLabel.color = Color.white;
        volumePercentLabel.raycastTarget = false;
        volumePercentLabel.enableWordWrapping = false;
    }

    private void AdjustVolume(int delta)
    {
        volumePercent = Mathf.Clamp(volumePercent + delta, 0, 100);
        AudioListener.volume = volumePercent / 100f;
        ApplyVolumeDisplay();
    }

    private void ApplyVolumeDisplay()
    {
        if (volumeFill != null)
        {
            RectTransform fillRect = volumeFill.rectTransform;
            float amount = volumePercent / 100f;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(amount, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            volumeFill.enabled = volumePercent > 0;
        }
        if (volumePercentLabel != null)
            volumePercentLabel.text = "Volume " + volumePercent + "%";
    }

    /// <summary>
    /// Sizes and colors the volume controls from the JSON video_controls_* settings.
    /// The lower band of a video_top stage uses the narrower volume layout.
    /// </summary>
    private void ApplyVolumeChrome(bool inLowerBand)
    {
        GameConfig config = GameManager.Instance != null ? GameManager.Instance.currentGameData?.game : null;

        Color buttonColor = defaultButtonColor;
        if (config != null)
        {
            string colorHex = !string.IsNullOrEmpty(config.video_controls_color)
                ? config.video_controls_color
                : config.default_button_color;
            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color parsed))
                buttonColor = parsed;
        }

        Color textColor = ResolveButtonTextColor(config?.video_controls_text_color, buttonColor);
        TMP_FontAsset font = GetFont(config != null ? config.video_controls_font : null);

        UiLayoutRules rules = UiLayoutRules.Instance;
        float scale = rules.Scale(transform as RectTransform);
        float size = GetControlSize(config);
        float fontScale = GetControlFontScale(config);

        float width = (inLowerBand ? rules.volumeBandWidth : rules.volumeWidth) * size * scale;
        float paddingY = (inLowerBand ? rules.volumeBandPaddingY : rules.volumePaddingY) * size * scale;
        float fontSize = rules.volumeFontSize * size * fontScale * scale;
        float buttonHeight = fontSize * rules.buttonLineHeightRatio + paddingY * 2f;
        float barHeight = rules.volumeBarHeight * size * scale;
        float barFontSize = Mathf.Min(
            (inLowerBand ? rules.volumeFontSize : rules.volumeBarFontSize) * size * fontScale * scale,
            width / 7f);
        float gap = rules.volumeGap * GetControlSpacing(config) * scale;

        volumeControlsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(width, buttonHeight * 2f + barHeight + gap * 2f);
        volumeControlsRoot.GetComponent<VerticalLayoutGroup>().spacing = gap;

        Sprite sprite = RoundedSprite.Get(rules.volumeCornerRadius * size * scale);
        ApplyVolumeButton(volumeUpImage, buttonColor, sprite, buttonHeight, config);
        ApplyVolumeButton(volumeDownImage, buttonColor, sprite, buttonHeight, config);

        if (volumeFill != null)
        {
            volumeFill.color = buttonColor;
            ApplySlicedSprite(volumeFill, sprite);
        }
        if (volumeTrackImage != null)
        {
            ApplySlicedSprite(volumeTrackImage, sprite);
            Outline border = volumeTrackImage.GetComponent<Outline>();
            float borderWidth = rules.volumeBarBorder * size * scale;
            border.effectColor = buttonColor;
            border.effectDistance = new Vector2(borderWidth, -borderWidth);
            volumeTrackImage.transform.parent.GetComponent<LayoutElement>().minHeight = barHeight;
        }

        ApplyVolumeLabel(volumeUpLabel, textColor, font, fontSize);
        ApplyVolumeLabel(volumeDownLabel, textColor, font, fontSize);
        ApplyVolumeLabel(volumePercentLabel, textColor, font, barFontSize);
    }

    private void ApplyVolumeButton(Image image, Color color, Sprite sprite, float height, GameConfig config)
    {
        if (image == null) return;
        image.color = color;
        ApplySlicedSprite(image, sprite);
        image.GetComponent<LayoutElement>().minHeight = height;

        float borderWidth = UiLayoutRules.GetControlBorder(config?.video_controls_style, Color.white, out Color borderColor);
        Outline border = image.GetComponent<Outline>();
        if (borderWidth <= 0f)
        {
            if (border != null) border.enabled = false;
            return;
        }

        if (border == null) border = image.gameObject.AddComponent<Outline>();
        float distance = borderWidth * UiLayoutRules.Instance.Scale(transform as RectTransform);
        border.enabled = true;
        border.effectColor = borderColor;
        border.effectDistance = new Vector2(distance, -distance);
    }

    private static void ApplyVolumeLabel(TextMeshProUGUI tmp, Color color, TMP_FontAsset font, float size)
    {
        if (tmp == null) return;
        tmp.color = color;
        tmp.fontSize = size;
        if (font != null) tmp.font = font;
    }

    private Sprite GetHudButtonSprite()
    {
        if (buttonPrefab == null) return null;
        Image image = buttonPrefab.GetComponent<Image>();
        return image != null ? image.sprite : null;
    }

    private static void ApplySlicedSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private void UpdateProgressIndicator(bool stageContext)
    {
        GameConfig config = GameManager.Instance != null ? GameManager.Instance.currentGameData?.game : null;
        bool visible = stageContext && config != null && config.progress_indicator_visible;
        if (!visible)
        {
            SetProgressVisible(false);
            return;
        }

        EnsureProgressIndicator();
        progressRoot.SetActive(true);

        var stages = GameManager.Instance.currentGameData.stages;
        StageData stage = GameManager.Instance.currentStage;
        int total = stages != null ? stages.Count : 0;
        int index = 0;
        if (stages != null && stage != null)
            index = stages.IndexOf(stage);
        int current = index >= 0 ? index + 1 : (stage != null ? stage.stage_number : 1);
        if (total < 1) total = 1;

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01((float)current / total);
        if (progressLabel != null)
            progressLabel.text = $"Stage {current} of {total}";
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressRoot != null) progressRoot.SetActive(visible);
    }

    private void EnsureProgressIndicator()
    {
        if (progressRoot != null) return;

        progressRoot = new GameObject("ProgressIndicator", typeof(RectTransform));
        progressRoot.transform.SetParent(transform, false);
        RectTransform rootRect = progressRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(0f, 48f);

        GameObject barBg = CreateUiImage("BarBackground", progressRoot.transform, new Color(0f, 0f, 0f, 0.35f));
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 1f);
        barBgRect.anchorMax = new Vector2(1f, 1f);
        barBgRect.pivot = new Vector2(0.5f, 1f);
        barBgRect.anchoredPosition = Vector2.zero;
        barBgRect.sizeDelta = new Vector2(0f, 8f);

        GameObject fillGo = CreateUiImage("BarFill", barBg.transform, new Color(1f, 1f, 1f, 0.9f));
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        VideoFitHelper.StretchFill(fillRect);
        progressFill = fillGo.GetComponent<Image>();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(progressRoot.transform, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(0f, 0f);
        labelRect.offsetMax = new Vector2(0f, -10f);
        progressLabel = labelGo.GetComponent<TextMeshProUGUI>();
        progressLabel.alignment = TextAlignmentOptions.Top;
        progressLabel.fontSize = 18f;
        progressLabel.color = Color.white;
        progressLabel.raycastTarget = false;
        progressRoot.transform.SetAsLastSibling();
        progressRoot.SetActive(false);
    }

    private static GameObject CreateUiImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private void ApplyTitlePresentation(StageData stage, bool photoBesideTitle = false)
    {
        EnsureTitleBox();
        if (titleBoxRect == null) return;

        bool hasLine = (stageTitleText != null && stageTitleText.gameObject.activeSelf)
            || (stageSubtitleText != null && stageSubtitleText.gameObject.activeSelf)
            || (stageScoreLine != null && stageScoreLine.gameObject.activeSelf)
            || (stageTimeLine != null && stageTimeLine.gameObject.activeSelf)
            || (teamNameText != null && teamNameText.gameObject.activeSelf);

        if (stage == null || !hasLine)
        {
            if (titleBoxImage != null) titleBoxImage.enabled = false;
            if (titleAnimator != null) titleAnimator.Stop();
            if (stage == null) RestoreTitleBoxForMenu();
            return;
        }

        UiLayoutRules rules = UiLayoutRules.Instance;
        RectTransform canvas = transform as RectTransform;
        float scale = rules.Scale(canvas);

        float fontSize = stage.title_text_font_size * scale;
        ApplyTitleLine(stageTitleText, fontSize);
        ApplyTitleLine(stageSubtitleText, fontSize);
        ApplyTitleLine(stageScoreLine, fontSize);
        ApplyTitleLine(stageTimeLine, fontSize);
        ApplyTitleLine(teamNameText, fontSize * rules.teamNameFontRatio);

        Color bg = Color.black;
        if (!string.IsNullOrEmpty(stage.title_text_bg_color))
            ColorUtility.TryParseHtmlString(stage.title_text_bg_color, out bg);
        float opacity = stage.title_text_bg_opacity;
        bg.a = Mathf.Clamp01(opacity / 100f);
        titleBoxImage.enabled = true;
        titleBoxImage.color = bg;
        titleBoxImage.raycastTarget = false;
        ApplySlicedSprite(titleBoxImage, RoundedSprite.Get(rules.titleCornerRadius * scale));

        int padX = Mathf.RoundToInt(stage.title_box_padding_x * scale);
        int padY = Mathf.RoundToInt(stage.title_box_padding_y * scale);
        titleBoxUsesStageLayout = true;
        titleBoxFitter.enabled = true;
        titleBoxFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        titleBoxFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        titleBoxLayout.padding = new RectOffset(padX, padX, padY, padY);
        titleBoxLayout.spacing = rules.titleLineGap * scale;
        titleBoxLayout.childAlignment = TextAnchor.UpperCenter;
        titleBoxLayout.childControlWidth = true;
        titleBoxLayout.childControlHeight = true;
        titleBoxLayout.childForceExpandWidth = true;
        titleBoxLayout.childForceExpandHeight = false;

        PlaceTitleBox(stage, padX, photoBesideTitle);

        if (titleAnimator != null)
            titleAnimator.Play(stage.title_animation, stage.title_animation_speed);
    }

    /// <summary>
    /// Centers the title box in the area it belongs to: the whole stage, the lower band on
    /// video_top, or the column right of the team photo, where it sits against the photo.
    /// JSON title_box_width sets the width; "auto" fits the text up to the area width.
    /// </summary>
    private void PlaceTitleBox(StageData stage, int padX, bool photoBesideTitle)
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        RectTransform canvas = transform as RectTransform;
        float scale = rules.Scale(canvas);
        float canvasWidth = canvas.rect.width;
        float margin = rules.titleSideMargin * scale;

        bool videoTop = StageLayoutController.IsVideoTop(stage);
        float areaHeight = videoTop ? StageLayoutController.Instance.LowerPane.rect.height : canvas.rect.height;
        float areaLeft = margin;
        float areaWidth = canvasWidth - margin * 2f;
        if (photoBesideTitle)
        {
            float gap = rules.photoColumnGap * scale;
            float columnWidth = areaWidth * rules.photoColumnRatio;
            areaLeft = margin + columnWidth + gap;
            areaWidth = canvasWidth - margin - areaLeft;
        }

        float textWidth = Mathf.Max(
            GetLineWidth(stageTitleText),
            GetLineWidth(stageSubtitleText),
            GetLineWidth(stageScoreLine),
            GetLineWidth(stageTimeLine),
            GetLineWidth(teamNameText));

        string value = string.IsNullOrEmpty(stage.title_box_width) ? "auto" : stage.title_box_width.Trim();
        float boxWidth = Mathf.Min(textWidth + padX * 2f, areaWidth);
        if (value == "100%")
            boxWidth = areaWidth;
        else if (value != "auto" && float.TryParse(value, out float px) && px > 0f)
            boxWidth = Mathf.Min(px * scale, areaWidth);

        float centerX = photoBesideTitle ? areaLeft + boxWidth * 0.5f : canvasWidth * 0.5f;
        float centerY = areaHeight * 0.5f;

        titleBoxRect.anchorMin = Vector2.zero;
        titleBoxRect.anchorMax = Vector2.zero;
        titleBoxRect.pivot = new Vector2(0.5f, 0.5f);
        titleBoxRect.sizeDelta = new Vector2(boxWidth, titleBoxRect.sizeDelta.y);
        titleBoxRect.anchoredPosition = new Vector2(centerX, centerY);
    }

    private static float GetLineWidth(TextMeshProUGUI tmp)
    {
        if (tmp == null || !tmp.gameObject.activeSelf) return 0f;
        return tmp.GetPreferredValues(tmp.text).x;
    }

    private void RestoreTitleBoxForMenu()
    {
        if (!titleBoxUsesStageLayout) return;
        titleBoxUsesStageLayout = false;

        titleBoxFitter.enabled = false;
        titleBoxLayout.padding = new RectOffset(0, 0, 0, 0);
        titleBoxLayout.spacing = titleBoxMenuSpacing;
        titleBoxLayout.childAlignment = titleBoxMenuAlignment;
        titleBoxLayout.childControlWidth = titleBoxMenuControlsSize;
        titleBoxLayout.childControlHeight = titleBoxMenuControlsSize;
        titleBoxLayout.childForceExpandHeight = titleBoxMenuExpandsHeight;

        titleBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleBoxRect.pivot = new Vector2(0.5f, 0.5f);
        titleBoxRect.anchoredPosition = titleBoxMenuPosition;
        titleBoxRect.sizeDelta = titleBoxMenuSize;

        if (stageTitleText != null) stageTitleText.fontSize = titleMenuFontSize;
    }

    private void EnsureTitleBox()
    {
        if (titleBoxRect != null || stageTitleText == null) return;
        titleBoxRect = stageTitleText.transform.parent as RectTransform;
        if (titleBoxRect == null) return;

        titleBoxImage = titleBoxRect.GetComponent<Image>();
        if (titleBoxImage == null) titleBoxImage = titleBoxRect.gameObject.AddComponent<Image>();
        titleAnimator = titleBoxRect.GetComponent<TitleAnimator>();
        if (titleAnimator == null) titleAnimator = titleBoxRect.gameObject.AddComponent<TitleAnimator>();

        titleBoxLayout = titleBoxRect.GetComponent<VerticalLayoutGroup>();
        if (titleBoxLayout == null) titleBoxLayout = titleBoxRect.gameObject.AddComponent<VerticalLayoutGroup>();
        titleBoxFitter = titleBoxRect.GetComponent<ContentSizeFitter>();
        if (titleBoxFitter == null) titleBoxFitter = titleBoxRect.gameObject.AddComponent<ContentSizeFitter>();
        titleBoxFitter.enabled = false;

        titleBoxMenuPosition = titleBoxRect.anchoredPosition;
        titleBoxMenuSize = titleBoxRect.sizeDelta;
        titleBoxMenuSpacing = titleBoxLayout.spacing;
        titleBoxMenuAlignment = titleBoxLayout.childAlignment;
        titleBoxMenuControlsSize = titleBoxLayout.childControlHeight;
        titleBoxMenuExpandsHeight = titleBoxLayout.childForceExpandHeight;
        titleMenuFontSize = stageTitleText.fontSize;
    }

    private static void ApplyTitleLine(TextMeshProUGUI tmp, float size)
    {
        if (tmp == null) return;
        tmp.enableAutoSizing = false;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;

        LayoutElement element = tmp.GetComponent<LayoutElement>();
        if (element == null) element = tmp.gameObject.AddComponent<LayoutElement>();
        element.minHeight = size * UiLayoutRules.Instance.titleLineHeightRatio;
    }
}