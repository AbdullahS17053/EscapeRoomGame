using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Stage-placed collectibles plus an inventory panel. Hidden when collectibles[] is empty.
/// </summary>
public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance { get; private set; }

    private readonly HashSet<string> collected = new HashSet<string>();
    private readonly List<GameObject> stageIcons = new List<GameObject>();
    private Transform canvas;
    private GameObject inventoryButton;
    private GameObject inventoryPanel;
    private Transform listRoot;
    private bool stageContextVisible;

    public static CollectibleManager Ensure(Transform canvasTransform)
    {
        if (Instance != null)
        {
            Instance.canvas = canvasTransform;
            return Instance;
        }

        CollectibleManager existing = canvasTransform != null
            ? canvasTransform.GetComponent<CollectibleManager>()
            : null;
        if (existing == null && canvasTransform != null)
            existing = canvasTransform.gameObject.AddComponent<CollectibleManager>();
        existing.canvas = canvasTransform;
        Instance = existing;
        return existing;
    }

    private void Awake()
    {
        Instance = this;
        if (canvas == null) canvas = transform;
    }

    public void ResetAll()
    {
        collected.Clear();
        ClearStageIcons();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        stageContextVisible = visible;
        bool hasItems = HasAnyCollectibles();
        if (inventoryButton != null) inventoryButton.SetActive(visible && hasItems);
        if (inventoryPanel != null && (!visible || !hasItems)) inventoryPanel.SetActive(false);
        if (!visible) ClearStageIcons();
    }

    public void ShowForStage(StageData stage)
    {
        ClearStageIcons();
        bool hasItems = HasAnyCollectibles();
        if (!hasItems)
        {
            SetVisible(false);
            return;
        }

        EnsureHud();
        stageContextVisible = true;
        inventoryButton.SetActive(true);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        if (stage == null) return;
        List<Collectible> all = GameManager.Instance.currentGameData.collectibles;
        int shown = 0;
        foreach (Collectible item in all)
        {
            if (item == null || string.IsNullOrEmpty(item.stage_id) || item.stage_id != stage.id)
                continue;
            string key = KeyOf(item);
            bool already = collected.Contains(key);
            SpawnStageIcon(item, shown, already, item.is_hidden && !already);
            shown++;
        }
    }

    private static bool HasAnyCollectibles()
    {
        var list = GameManager.Instance != null ? GameManager.Instance.currentGameData?.collectibles : null;
        return list != null && list.Count > 0;
    }

    private void EnsureHud()
    {
        if (inventoryButton != null) return;

        UIManager ui = canvas != null ? canvas.GetComponent<UIManager>() : null;

        inventoryButton = new GameObject("CollectiblesButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        inventoryButton.transform.SetParent(canvas, false);
        RectTransform btnRect = inventoryButton.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(72f, 72f);
        if (ui != null) ui.SetUIPosition(btnRect, "top-left");
        btnRect.anchoredPosition += new Vector2(0f, -90f);
        inventoryButton.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(inventoryButton.transform, false);
        VideoFitHelper.StretchFill(label.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = "Items";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        inventoryButton.GetComponent<Button>().onClick.AddListener(TogglePanel);

        inventoryPanel = new GameObject("CollectiblesPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        inventoryPanel.transform.SetParent(canvas, false);
        RectTransform panelRect = inventoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 0.15f);
        panelRect.anchorMax = new Vector2(0.85f, 0.85f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        inventoryPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.94f);

        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        close.transform.SetParent(inventoryPanel.transform, false);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-12f, -12f);
        closeRect.sizeDelta = new Vector2(80f, 40f);
        close.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
        GameObject closeText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeText.transform.SetParent(close.transform, false);
        VideoFitHelper.StretchFill(closeText.GetComponent<RectTransform>());
        TextMeshProUGUI closeTmp = closeText.GetComponent<TextMeshProUGUI>();
        closeTmp.text = "Close";
        closeTmp.alignment = TextAlignmentOptions.Center;
        closeTmp.fontSize = 18f;
        close.GetComponent<Button>().onClick.AddListener(() => inventoryPanel.SetActive(false));

        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(inventoryPanel.transform, false);
        RectTransform listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(24f, 24f);
        listRect.offsetMax = new Vector2(-24f, -64f);
        VerticalLayoutGroup vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        ContentSizeFitter fitter = listGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        listRoot = listGo.transform;
        inventoryPanel.SetActive(false);
    }

    private void TogglePanel()
    {
        if (inventoryPanel == null) return;
        bool show = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(show);
        if (show) RebuildList();
    }

    private void RebuildList()
    {
        if (listRoot == null) return;
        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        var all = GameManager.Instance.currentGameData.collectibles;
        foreach (Collectible item in all)
        {
            if (item == null) continue;
            string key = KeyOf(item);
            bool have = collected.Contains(key);
            if (item.is_hidden && !have) continue;

            GameObject row = new GameObject(item.name ?? "Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(listRoot, false);
            row.GetComponent<Image>().color = have ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f);
            row.GetComponent<LayoutElement>().preferredHeight = 88f;
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.padding = new RectOffset(8, 8, 8, 8);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 72f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 72f;
            RawImage icon = iconGo.GetComponent<RawImage>();
            icon.color = have ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (!string.IsNullOrEmpty(item.icon_url))
                StartCoroutine(LoadIcon(icon, item.icon_url));

            GameObject textGo = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(row.transform, false);
            textGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TextMeshProUGUI info = textGo.GetComponent<TextMeshProUGUI>();
            info.fontSize = 22f;
            info.color = Color.white;
            string title = string.IsNullOrEmpty(item.name) ? "Collectible" : item.name;
            string desc = have ? (item.description ?? "") : "Not found yet";
            info.text = $"<b>{title}</b>\n{desc}";
        }
    }

    private void SpawnStageIcon(Collectible item, int index, bool alreadyCollected, bool hiddenHotspot)
    {
        GameObject go = new GameObject("Collectible_" + KeyOf(item), typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Button));
        go.transform.SetParent(canvas, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(96f, 96f);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-24f, 80f - index * 110f);
        RawImage image = go.GetComponent<RawImage>();
        if (hiddenHotspot)
            image.color = new Color(1f, 1f, 1f, 0.04f);
        else
            image.color = alreadyCollected ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
        if (!hiddenHotspot && !string.IsNullOrEmpty(item.icon_url))
            StartCoroutine(LoadIcon(image, item.icon_url));

        Button btn = go.GetComponent<Button>();
        string key = KeyOf(item);
        btn.interactable = !alreadyCollected;
        btn.onClick.AddListener(() => Collect(key, go));
        stageIcons.Add(go);
        if (GameManager.Instance != null && GameManager.Instance.resourceManager != null)
            GameManager.Instance.resourceManager.TrackStageObject(go);
    }

    private void Collect(string key, GameObject icon)
    {
        if (string.IsNullOrEmpty(key) || collected.Contains(key)) return;
        collected.Add(key);
        if (icon != null)
        {
            RawImage image = icon.GetComponent<RawImage>();
            if (image != null) image.color = new Color(1f, 1f, 1f, 0.4f);
            Button btn = icon.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    private void ClearStageIcons()
    {
        foreach (GameObject go in stageIcons)
        {
            if (go != null) Destroy(go);
        }
        stageIcons.Clear();
    }

    private IEnumerator LoadIcon(RawImage dest, string url)
    {
        if (dest == null || GameManager.Instance == null) yield break;
        string path = GameManager.Instance.resourceManager.GetLocalPath(url);
        if (!File.Exists(path)) yield break;
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + path))
        {
            yield return www.SendWebRequest();
            if (dest == null || www.result != UnityWebRequest.Result.Success) yield break;
            dest.texture = DownloadHandlerTexture.GetContent(www);
        }
    }

    private static string KeyOf(Collectible item)
    {
        if (item == null) return "";
        if (!string.IsNullOrEmpty(item.id)) return item.id;
        return (item.stage_id ?? "") + "|" + (item.name ?? "") + "|" + (item.icon_url ?? "");
    }
}
