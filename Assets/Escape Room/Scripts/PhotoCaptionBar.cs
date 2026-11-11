using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Build-4 team-photo caption: light bar inside the photo with team name, elapsed time, and score.
/// </summary>
public class PhotoCaptionBar : MonoBehaviour
{
    private TextMeshProUGUI teamLabel;
    private TextMeshProUGUI timeLabel;
    private TextMeshProUGUI scoreLabel;
    private float startFontSize;
    private bool liveUpdate;

    public static PhotoCaptionBar Ensure(RawImage photo)
    {
        if (photo == null) return null;

        Transform existing = photo.transform.Find("PhotoCaptionBar");
        if (existing != null)
            return existing.GetComponent<PhotoCaptionBar>() ?? existing.gameObject.AddComponent<PhotoCaptionBar>();

        GameObject barGo = new GameObject("PhotoCaptionBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PhotoCaptionBar));
        barGo.transform.SetParent(photo.transform, false);

        RectTransform rect = barGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 48f);

        Image bg = barGo.GetComponent<Image>();
        bg.color = new Color(245f / 255f, 245f / 255f, 245f / 255f, 0.92f);
        bg.raycastTarget = false;

        PhotoCaptionBar bar = barGo.GetComponent<PhotoCaptionBar>();
        bar.teamLabel = CreateItem(rect, "TeamName", TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0f));
        bar.timeLabel = CreateItem(rect, "ElapsedTime", TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        bar.scoreLabel = CreateItem(rect, "Score", TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 1f));
        barGo.SetActive(false);
        return bar;
    }

    private static TextMeshProUGUI CreateItem(RectTransform parent, string name, TextAlignmentOptions align, Vector2 pivot, Vector2 anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(280f, 40f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.alignment = align;
        tmp.color = new Color(33f / 255f, 33f / 255f, 33f / 255f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = "";
        return tmp;
    }

    public void Hide()
    {
        liveUpdate = false;
        gameObject.SetActive(false);
    }

    public void Show(StageData stage, TMP_FontAsset font, float photoWidth, float canvasWidth)
    {
        if (stage == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        liveUpdate = true;

        bool printedSlot = UiLayoutRules.Instance.FindPhotoOverlay(stage) != null;

        float designWidth = canvasWidth > 1f ? canvasWidth : 1920f;
        startFontSize = printedSlot ? designWidth / 64f : Mathf.Max(12f, photoWidth / 12f);

        ApplyFont(font, startFontSize);
        RefreshTexts();
        ShrinkToFit(photoWidth);
    }

    private void ApplyFont(TMP_FontAsset font, float size)
    {
        TextMeshProUGUI[] labels = { teamLabel, timeLabel, scoreLabel };
        foreach (TextMeshProUGUI tmp in labels)
        {
            if (tmp == null) continue;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
        }

        RectTransform rect = transform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(0f, Mathf.Max(36f, size * 1.7f));
    }

    private void LateUpdate()
    {
        if (!liveUpdate || !gameObject.activeInHierarchy) return;
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        string team = GameManager.Instance != null ? GameManager.Instance.teamName : "";
        if (string.IsNullOrWhiteSpace(team)) team = "Team";
        if (teamLabel != null) teamLabel.text = team;

        float seconds = TimerManager.Instance != null ? TimerManager.Instance.GetCurrentTime() : 0f;
        if (seconds < 0f) seconds = 0f;
        System.TimeSpan span = System.TimeSpan.FromSeconds(seconds);
        if (timeLabel != null) timeLabel.text = span.ToString(@"mm\:ss");

        int score = ScoreManager.Instance != null ? ScoreManager.Instance.GetCurrentScore() : 0;
        if (scoreLabel != null) scoreLabel.text = $"Score: {score}";
    }

    private void ShrinkToFit(float photoWidth)
    {
        if (photoWidth < 1f) return;

        float size = startFontSize;
        for (int i = 0; i < 12 && size >= 10f; i++)
        {
            ApplyFont(null, size);
            Canvas.ForceUpdateCanvases();

            float left = PreferredWidth(teamLabel);
            float mid = PreferredWidth(timeLabel);
            float right = PreferredWidth(scoreLabel);
            float needed = left + mid + right + 48f;
            if (needed <= photoWidth) break;
            size -= 2f;
        }
    }

    private static float PreferredWidth(TextMeshProUGUI tmp)
    {
        if (tmp == null) return 0f;
        return tmp.GetPreferredValues(tmp.text).x;
    }
}
