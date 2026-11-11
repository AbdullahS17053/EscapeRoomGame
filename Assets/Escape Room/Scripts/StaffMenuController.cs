using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime Staff/Admin menu overlay: Resume, Wi-Fi, Bluetooth, Volume, Brightness,
/// Android Settings, Exit Kiosk.
/// </summary>
public class StaffMenuController : MonoBehaviour
{
    public static StaffMenuController Current { get; private set; }

    Action onResume;
    Action onExitKiosk;
    Action onOpenSettings;

    TextMeshProUGUI bluetoothLabel;
    TextMeshProUGUI wifiStatusLabel;
    Slider volumeSlider;
    Slider brightnessSlider;
    GameObject wifiPanelRoot;
    bool bluetoothOn;

    public static StaffMenuController Show(Transform canvasParent, Action onResume, Action onExitKiosk, Action onOpenSettings)
    {
        if (Current != null)
            return Current;

        AndroidDeviceControls.GrantStaffPermissions();

        var go = new GameObject("StaffMenu", typeof(RectTransform));
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();
        var controller = go.AddComponent<StaffMenuController>();
        controller.onResume = onResume;
        controller.onExitKiosk = onExitKiosk;
        controller.onOpenSettings = onOpenSettings;
        controller.BuildUi();
        Current = controller;
        return controller;
    }

    void Close()
    {
        if (Current == this)
            Current = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    void BuildUi()
    {
        var root = GetComponent<RectTransform>();
        StretchFull(root);

        // Dim backdrop
        var backdrop = CreatePanel("Backdrop", root, new Color(0f, 0f, 0f, 0.65f));
        StretchFull(backdrop.GetComponent<RectTransform>());

        // Modal card
        var card = CreatePanel("Card", root, new Color(0.145f, 0.165f, 0.20f, 1f));
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(520f, 640f);

        var layoutGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        layoutGo.transform.SetParent(card.transform, false);
        var layoutRt = layoutGo.GetComponent<RectTransform>();
        StretchFull(layoutRt, 24f);
        var vlg = layoutGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        CreateLabel(layoutGo.transform, "Staff Menu", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateLabel(layoutGo.transform, "Admin access", 14, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.7f, 0.72f, 0.75f));

        CreateButton(layoutGo.transform, "Resume Game", new Color(0.18f, 0.55f, 0.32f), () =>
        {
            onResume?.Invoke();
            Close();
        });

        wifiStatusLabel = CreateButton(layoutGo.transform, WifiButtonLabel(), new Color(0.22f, 0.28f, 0.38f), OpenWifiPanel);

        bluetoothOn = AndroidDeviceControls.IsBluetoothEnabled;
        bluetoothLabel = CreateButton(layoutGo.transform, BluetoothLabel(), new Color(0.22f, 0.28f, 0.38f), ToggleBluetooth);

        CreateLabel(layoutGo.transform, "Volume", 14, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.75f, 0.78f, 0.82f));
        volumeSlider = CreateSlider(layoutGo.transform, 0f, AndroidDeviceControls.GetMaxVolume(), AndroidDeviceControls.GetVolume());
        volumeSlider.onValueChanged.AddListener(v => AndroidDeviceControls.SetVolume(Mathf.RoundToInt(v)));

        CreateLabel(layoutGo.transform, "Brightness", 14, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.75f, 0.78f, 0.82f));
        brightnessSlider = CreateSlider(layoutGo.transform, 0.05f, 1f, AndroidDeviceControls.GetBrightness());
        brightnessSlider.onValueChanged.AddListener(AndroidDeviceControls.SetBrightness);

        CreateButton(layoutGo.transform, "Android Settings", new Color(0.28f, 0.30f, 0.36f), () =>
        {
            onOpenSettings?.Invoke();
            Close();
        });

        CreateButton(layoutGo.transform, "Exit Kiosk", new Color(0.65f, 0.18f, 0.18f), () =>
        {
            onExitKiosk?.Invoke();
            Close();
        });
    }

    string WifiButtonLabel()
    {
        string ssid = AndroidDeviceControls.GetCurrentSsid();
        bool on = AndroidDeviceControls.IsWifiEnabled;
        if (!on)
            return "Wi-Fi  (Off)";
        if (string.IsNullOrEmpty(ssid))
            return "Wi-Fi  (On)";
        return $"Wi-Fi  ({ssid})";
    }

    string BluetoothLabel()
    {
        return bluetoothOn ? "Bluetooth  (On)" : "Bluetooth  (Off)";
    }

    void ToggleBluetooth()
    {
        bluetoothOn = !bluetoothOn;
        AndroidDeviceControls.SetBluetoothEnabled(bluetoothOn);
        bluetoothOn = AndroidDeviceControls.IsBluetoothEnabled;
        if (bluetoothLabel != null)
            bluetoothLabel.text = BluetoothLabel();
    }

    void OpenWifiPanel()
    {
        if (wifiPanelRoot != null)
            return;

        wifiPanelRoot = StaffWifiPanel.Show(transform, () =>
        {
            wifiPanelRoot = null;
            if (wifiStatusLabel != null)
                wifiStatusLabel.text = WifiButtonLabel();
        });
    }

    static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align, Color? color = null)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = size + 10f;
        go.GetComponent<LayoutElement>().minHeight = size + 8f;
        return tmp;
    }

    static TextMeshProUGUI CreateButton(Transform parent, string label, Color bg, Action onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<LayoutElement>().preferredHeight = 48f;
        go.GetComponent<LayoutElement>().minHeight = 48f;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bg * 1.15f;
        colors.pressedColor = bg * 0.85f;
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), 12f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        var root = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 36f;
        root.GetComponent<LayoutElement>().minHeight = 36f;

        var bg = CreatePanel("Background", root.transform, new Color(0.12f, 0.13f, 0.16f));
        StretchFull(bg.GetComponent<RectTransform>(), 0f, 12f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        StretchFull(fillArea.GetComponent<RectTransform>(), 0f, 12f);

        var fill = CreatePanel("Fill", fillArea.transform, new Color(0.35f, 0.55f, 0.95f));
        StretchFull(fill.GetComponent<RectTransform>());

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        StretchFull(handleArea.GetComponent<RectTransform>(), 0f, 12f);

        var handle = CreatePanel("Handle", handleArea.transform, Color.white);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 20f);

        var slider = root.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = Mathf.Max(min + 0.01f, max);
        slider.wholeNumbers = max > 1.5f;
        slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRt;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    static void StretchFull(RectTransform rt, float pad = 0f, float verticalInset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad + verticalInset);
        rt.offsetMax = new Vector2(-pad, -pad - verticalInset);
    }
}
