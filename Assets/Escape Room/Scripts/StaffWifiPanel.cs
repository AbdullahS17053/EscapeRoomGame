using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full in-app Wi-Fi panel: toggle, scan, select network, password, connect.
/// </summary>
public class StaffWifiPanel : MonoBehaviour
{
    Action onClosed;
    Toggle wifiToggle;
    TextMeshProUGUI statusText;
    Transform listContent;
    TMP_InputField passwordField;
    TextMeshProUGUI selectedLabel;
    AndroidDeviceControls.WifiNetwork? selected;
    bool scanning;

    public static GameObject Show(Transform parent, Action onClosed)
    {
        var go = new GameObject("StaffWifiPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var panel = go.AddComponent<StaffWifiPanel>();
        panel.onClosed = onClosed;
        panel.BuildUi();
        panel.StartCoroutine(panel.InitialScanRoutine());
        return go;
    }

    void BuildUi()
    {
        var root = GetComponent<RectTransform>();
        StretchFull(root);

        var backdrop = CreatePanel("Backdrop", root, new Color(0f, 0f, 0f, 0.55f));
        StretchFull(backdrop.GetComponent<RectTransform>());
        var backdropBtn = backdrop.AddComponent<Button>();
        backdropBtn.onClick.AddListener(Close);

        var card = CreatePanel("Card", root, new Color(0.145f, 0.165f, 0.20f, 1f));
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(560f, 620f);

        var layoutGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        layoutGo.transform.SetParent(card.transform, false);
        StretchFull(layoutGo.GetComponent<RectTransform>(), 20f);
        var vlg = layoutGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateLabel(layoutGo.transform, "Wi-Fi", 26, FontStyles.Bold, TextAlignmentOptions.Center);

        // Toggle row
        var toggleRow = new GameObject("ToggleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        toggleRow.transform.SetParent(layoutGo.transform, false);
        toggleRow.GetComponent<LayoutElement>().preferredHeight = 40f;
        var hlg = toggleRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 12f;
        CreateLabel(toggleRow.transform, "Enabled", 16, FontStyles.Normal, TextAlignmentOptions.Left);
        wifiToggle = CreateToggle(toggleRow.transform, AndroidDeviceControls.IsWifiEnabled);
        wifiToggle.onValueChanged.AddListener(OnWifiToggled);

        statusText = CreateLabel(layoutGo.transform, StatusLine(), 14, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.75f, 0.78f, 0.82f));

        CreateButton(layoutGo.transform, "Refresh Networks", new Color(0.22f, 0.28f, 0.38f), () =>
        {
            if (!scanning)
                StartCoroutine(ScanRoutine());
        });

        // Scroll list
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(layoutGo.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);
        scrollGo.GetComponent<LayoutElement>().preferredHeight = 260f;
        scrollGo.GetComponent<LayoutElement>().flexibleHeight = 1f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        StretchFull(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var contentVlg = content.GetComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 4f;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childControlHeight = false;
        contentVlg.padding = new RectOffset(6, 6, 6, 6);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        listContent = content.transform;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;

        selectedLabel = CreateLabel(layoutGo.transform, "Selected: (none)", 14, FontStyles.Normal, TextAlignmentOptions.Left);

        passwordField = CreateInputField(layoutGo.transform, "Password (if required)");
        CreateButton(layoutGo.transform, "Connect", new Color(0.18f, 0.55f, 0.32f), ConnectSelected);
        CreateButton(layoutGo.transform, "Back", new Color(0.28f, 0.30f, 0.36f), Close);
    }

    IEnumerator InitialScanRoutine()
    {
        yield return ScanRoutine();
    }

    IEnumerator ScanRoutine()
    {
        scanning = true;
        if (statusText != null)
            statusText.text = "Scanning…";

        if (!AndroidDeviceControls.IsWifiEnabled)
            AndroidDeviceControls.SetWifiEnabled(true);

        AndroidDeviceControls.StartWifiScan();
        yield return new WaitForSecondsRealtime(2.2f);

        PopulateList(AndroidDeviceControls.GetWifiScanResults());
        if (statusText != null)
            statusText.text = StatusLine();
        scanning = false;
    }

    void PopulateList(List<AndroidDeviceControls.WifiNetwork> networks)
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        if (networks == null || networks.Count == 0)
        {
            CreateLabel(listContent, "No networks found", 14, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        foreach (var net in networks)
        {
            var captured = net;
            string lockMark = captured.secure ? " [secured]" : " [open]";
            string label = $"{captured.ssid}  ({captured.levelDb} dBm){lockMark}";

            CreateButton(listContent, label, new Color(0.20f, 0.24f, 0.32f), () =>
            {
                selected = captured;
                if (selectedLabel != null)
                    selectedLabel.text = $"Selected: {captured.ssid}";
                if (passwordField != null)
                {
                    passwordField.gameObject.SetActive(captured.secure);
                    if (!captured.secure)
                        passwordField.text = "";
                }
            });
        }
    }

    void OnWifiToggled(bool on)
    {
        AndroidDeviceControls.SetWifiEnabled(on);
        if (statusText != null)
            statusText.text = StatusLine();
        if (on && !scanning)
            StartCoroutine(ScanRoutine());
    }

    void ConnectSelected()
    {
        if (selected == null)
        {
            if (statusText != null)
                statusText.text = "Select a network first.";
            return;
        }

        var net = selected.Value;
        string password = passwordField != null ? passwordField.text : "";
        string result = AndroidDeviceControls.ConnectToWifi(net.ssid, password, !net.secure);
        if (statusText != null)
        {
            statusText.text = result == "ok"
                ? $"Connecting to {net.ssid}…"
                : $"Connect failed: {result}";
        }

        if (result == "ok")
            StartCoroutine(RefreshStatusSoon());
    }

    IEnumerator RefreshStatusSoon()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (statusText != null)
            statusText.text = StatusLine();
    }

    string StatusLine()
    {
        bool on = AndroidDeviceControls.IsWifiEnabled;
        string ssid = AndroidDeviceControls.GetCurrentSsid();
        if (!on)
            return "Wi-Fi is off";
        if (string.IsNullOrEmpty(ssid))
            return "Wi-Fi on — not connected";
        return $"Connected: {ssid}";
    }

    void Close()
    {
        onClosed?.Invoke();
        Destroy(gameObject);
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
        go.GetComponent<LayoutElement>().preferredHeight = size + 8f;
        return tmp;
    }

    static TextMeshProUGUI CreateButton(Transform parent, string label, Color bg, Action onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<LayoutElement>().preferredHeight = 44f;
        go.GetComponent<LayoutElement>().minHeight = 40f;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), 10f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Toggle CreateToggle(Transform parent, bool isOn)
    {
        var go = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredWidth = 48f;
        go.GetComponent<LayoutElement>().preferredHeight = 28f;

        var bg = CreatePanel("Background", go.transform, new Color(0.25f, 0.28f, 0.34f));
        StretchFull(bg.GetComponent<RectTransform>());

        var check = CreatePanel("Checkmark", go.transform, new Color(0.35f, 0.75f, 0.45f));
        StretchFull(check.GetComponent<RectTransform>(), 4f);

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = bg.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = isOn;
        return toggle;
    }

    static TMP_InputField CreateInputField(Transform parent, string placeholder)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(go.transform, false);
        StretchFull(textArea.GetComponent<RectTransform>(), 10f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textArea.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 16;
        text.color = Color.white;

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(textArea.transform, false);
        StretchFull(phGo.GetComponent<RectTransform>());
        var ph = phGo.GetComponent<TextMeshProUGUI>();
        ph.text = placeholder;
        ph.fontSize = 16;
        ph.fontStyle = FontStyles.Italic;
        ph.color = new Color(1f, 1f, 1f, 0.35f);

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = ph;
        input.contentType = TMP_InputField.ContentType.Password;
        input.pointSize = 16;
        return input;
    }

    static void StretchFull(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    static void StretchFull(RectTransform rt, float pad, float verticalInset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad + verticalInset);
        rt.offsetMax = new Vector2(-pad, -pad - verticalInset);
    }
}
