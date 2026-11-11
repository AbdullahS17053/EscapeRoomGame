using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// Text entry dialog with a built-in on-screen keyboard, used for team names, codes and
/// staff PINs. The input field only displays what is typed, so the device keyboard stays
/// hidden on the tablet. Keys follow the JSON default_button_color.
/// </summary>
public class InputDialogController : MonoBehaviour
{
    private static readonly string[] KeyRows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };

    public TextMeshProUGUI promptText;
    public TMP_InputField inputField;
    public Button submitButton;
    public Button closeButton;

    private Action<string> onSubmitCallback;
    private Action onCancelCallback;
    private bool submitOnEscapeWord;
    private readonly List<Button> keys = new List<Button>();

    public void Setup(string prompt, Action<string> onSubmit, Action onCancel = null, bool submitOnEscapeWord = true)
    {
        if (promptText != null) promptText.text = prompt;
        onSubmitCallback = onSubmit;
        onCancelCallback = onCancel;
        this.submitOnEscapeWord = submitOnEscapeWord;

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCancel);
        }

        BuildKeyboard();
    }

    /// <summary>
    /// Keys take the game's button colors. Submit stays a distinct color and close has
    /// no background. Call after the dialog's general button coloring.
    /// </summary>
    public void ApplyKeyboardColors()
    {
        UiLayoutRules rules = UiLayoutRules.Instance;
        GameConfig config = GameManager.Instance != null && GameManager.Instance.currentGameData != null
            ? GameManager.Instance.currentGameData.game
            : null;

        Color keyColor = rules.keyboardKeyColor;
        if (config != null && !string.IsNullOrEmpty(config.default_button_color)
            && ColorUtility.TryParseHtmlString(config.default_button_color, out Color parsed))
        {
            keyColor = parsed;
        }

        string pressedHex = config != null ? config.default_button_pressed_color : null;
        foreach (Button key in keys)
        {
            UIManager.ApplyButtonColors(key, keyColor, pressedHex);
            key.GetComponentInChildren<TextMeshProUGUI>().color = UIManager.ResolveButtonTextColor(null, keyColor);
        }

        if (submitButton != null)
        {
            UIManager.ApplyButtonColors(submitButton, rules.keyboardSubmitColor, pressedHex);
            submitButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        }

        if (closeButton != null)
        {
            UIManager.ApplyButtonColors(closeButton, Color.clear, null);
            closeButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        }
    }

    private void BuildKeyboard()
    {
        RectTransform canvas = transform.parent as RectTransform;
        if (canvas == null) return;

        RectTransform root = transform as RectTransform;
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        VideoFitHelper.StretchFill(root);

        UiLayoutRules rules = UiLayoutRules.Instance;
        int rowCount = KeyRows.Length + 1;
        float panelWidth = rules.keyboardPanelWidth;
        float panelHeight = rules.keyboardPadding * 2f
            + rules.keyboardTitleHeight + rules.keyboardSectionGap
            + rules.keyboardInputHeight + rules.keyboardInputGap
            + rules.keyboardKeySize * rowCount + rules.keyboardRowGap * (rowCount - 1)
            + rules.keyboardSectionGap + rules.keyboardActionHeight;

        // Fixed design size, reduced together only if the screen is too small for it.
        float scale = rules.Scale(canvas);
        float roomX = canvas.rect.width - rules.keyboardMargin * scale * 2f;
        float roomY = canvas.rect.height - rules.keyboardMargin * scale * 2f;
        float unit = scale * Mathf.Min(1f, roomX / (panelWidth * scale), roomY / (panelHeight * scale));

        Image backdrop = CreateImage("Backdrop", root, rules.keyboardBackdropColor);
        VideoFitHelper.StretchFill(backdrop.rectTransform);

        Image panelImage = CreateImage("Panel", root, rules.keyboardPanelColor);
        SetRounded(panelImage, rules.keyboardPanelCornerRadius * unit);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(panelWidth, panelHeight) * unit;

        float padding = rules.keyboardPadding;
        float innerWidth = panelWidth - padding * 2f;
        float y = padding;

        PlaceTitle(panel, padding, y, innerWidth - rules.keyboardTitleHeight, unit);
        PlaceCloseButton(panel, panelWidth - padding - rules.keyboardTitleHeight, y, unit);
        y += rules.keyboardTitleHeight + rules.keyboardSectionGap;

        PlaceInputField(panel, padding, y, innerWidth, unit);
        y += rules.keyboardInputHeight + rules.keyboardInputGap;

        float keySize = rules.keyboardKeySize;
        float keyRadius = rules.keyboardKeyCornerRadius * unit;
        foreach (string row in KeyRows)
        {
            float rowWidth = row.Length * keySize + (row.Length - 1) * rules.keyboardKeyGap;
            float x = (panelWidth - rowWidth) * 0.5f;
            foreach (char letter in row)
            {
                string key = letter.ToString();
                Button button = CreateKey(key, panel, rules.keyboardKeyFontSize * unit, true, keyRadius, () => Type(key));
                Place(button.transform as RectTransform, x, y, keySize, keySize, unit);
                x += keySize + rules.keyboardKeyGap;
            }
            y += keySize + rules.keyboardRowGap;
        }

        Button space = CreateKey("Space", panel, rules.keyboardKeyFontSize * unit, true, keyRadius, () => Type(" "));
        Place(space.transform as RectTransform, padding, y, innerWidth, keySize, unit);
        y += keySize + rules.keyboardSectionGap;

        float actionGap = rules.keyboardActionGap;
        float actionWidth = (innerWidth - actionGap * 2f) / 3f;
        float actionHeight = rules.keyboardActionHeight;
        float actionFont = rules.keyboardActionFontSize * unit;

        Button clear = CreateKey("Clear", panel, actionFont, false, keyRadius, Clear);
        Place(clear.transform as RectTransform, padding, y, actionWidth, actionHeight, unit);

        Button backspace = CreateKey("Backspace", panel, actionFont, false, keyRadius, Backspace);
        Place(backspace.transform as RectTransform, padding + actionWidth + actionGap, y, actionWidth, actionHeight, unit);

        if (submitButton != null)
        {
            submitButton.transform.SetParent(panel, false);
            Place(submitButton.transform as RectTransform, padding + (actionWidth + actionGap) * 2f, y, actionWidth, actionHeight, unit);
            SetRounded(submitButton.GetComponent<Image>(), keyRadius);
            StyleLabel(submitButton.GetComponentInChildren<TextMeshProUGUI>(), actionFont, false);
        }
    }

    private void PlaceTitle(RectTransform panel, float x, float y, float width, float unit)
    {
        if (promptText == null) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        promptText.transform.SetParent(panel, false);
        Place(promptText.rectTransform, x, y, width, rules.keyboardTitleHeight, unit);
        StyleLabel(promptText, rules.keyboardTitleFontSize * unit, true);
        promptText.alignment = TextAlignmentOptions.Left;
        promptText.color = Color.white;
    }

    private void PlaceCloseButton(RectTransform panel, float x, float y, float unit)
    {
        if (closeButton == null) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        float size = rules.keyboardTitleHeight;
        closeButton.transform.SetParent(panel, false);
        Place(closeButton.transform as RectTransform, x, y, size, size, unit);

        TextMeshProUGUI label = closeButton.GetComponentInChildren<TextMeshProUGUI>();
        label.text = "X";
        StyleLabel(label, rules.keyboardActionFontSize * unit, true);
    }

    private void PlaceInputField(RectTransform panel, float x, float y, float width, float unit)
    {
        if (inputField == null) return;

        UiLayoutRules rules = UiLayoutRules.Instance;
        inputField.transform.SetParent(panel, false);
        Place(inputField.transform as RectTransform, x, y, width, rules.keyboardInputHeight, unit);

        // Display only: not selecting the field is what keeps the device keyboard away.
        inputField.interactable = false;
        inputField.transition = Selectable.Transition.None;
        inputField.pointSize = rules.keyboardInputFontSize * unit;

        Image background = inputField.GetComponent<Image>();
        background.color = rules.keyboardInputColor;
        SetRounded(background, rules.keyboardKeyCornerRadius * unit);

        TMP_Text text = inputField.textComponent;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        if (inputField.placeholder is TMP_Text placeholder)
        {
            placeholder.text = rules.keyboardPlaceholder;
            placeholder.alignment = TextAlignmentOptions.Center;
            placeholder.color = rules.keyboardPlaceholderColor;
            placeholder.fontStyle = FontStyles.Normal;
        }
    }

    private Button CreateKey(string label, RectTransform parent, float fontSize, bool bold, float radius, UnityAction onClick)
    {
        GameObject keyGo = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        keyGo.transform.SetParent(parent, false);

        Image image = keyGo.GetComponent<Image>();
        SetRounded(image, radius);

        Button button = keyGo.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(keyGo.transform, false);
        VideoFitHelper.StretchFill(labelGo.GetComponent<RectTransform>());

        TextMeshProUGUI text = labelGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.raycastTarget = false;
        StyleLabel(text, fontSize, bold);

        keys.Add(button);
        return button;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void SetRounded(Image image, float radius)
    {
        if (image == null) return;
        image.sprite = RoundedSprite.Get(radius);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static void StyleLabel(TextMeshProUGUI label, float size, bool bold)
    {
        if (label == null) return;
        label.enableAutoSizing = false;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>Places a rect by its top-left corner inside the panel, in design pixels.</summary>
    private static void Place(RectTransform rect, float x, float y, float width, float height, float unit)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y) * unit;
        rect.sizeDelta = new Vector2(width, height) * unit;
        rect.localScale = Vector3.one;
    }

    private void Type(string key)
    {
        if (inputField == null) return;
        inputField.text += key;

        // The escape word acts as soon as it is typed, without needing Submit.
        if (submitOnEscapeWord && GameManager.Instance != null && GameManager.Instance.IsEscapeWord(inputField.text))
            OnSubmit();
    }

    private void Backspace()
    {
        if (inputField == null || inputField.text.Length == 0) return;
        inputField.text = inputField.text.Substring(0, inputField.text.Length - 1);
    }

    private void Clear()
    {
        if (inputField != null) inputField.text = "";
    }

    private void OnSubmit()
    {
        if (inputField != null)
        {
            onSubmitCallback?.Invoke(inputField.text);
        }
        CloseDialog();
    }

    private void OnCancel()
    {
        onCancelCallback?.Invoke();
        CloseDialog();
    }

    public void CloseDialog()
    {
        Destroy(gameObject);
    }
}
