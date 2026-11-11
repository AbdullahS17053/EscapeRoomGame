using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public TextMeshProUGUI scoreText; // Score pill (show_score)

    private RectTransform pill;
    private int currentScore = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ResetScore(int starting = 0)
    {
        currentScore = starting;
        UpdateScoreUI();
    }

    /// <summary>
    /// Adds any integer (positive, negative, or zero). Score may go negative.
    /// </summary>
    public void ApplyPoints(int amount)
    {
        if (amount == 0) return;
        currentScore += amount;
        Debug.Log($"Score changed by {amount}. Total: {currentScore}");
        UpdateScoreUI();
    }

    public void AddScore(int amount)
    {
        ApplyPoints(amount);
    }

    public void SubtractScore(int amount)
    {
        ApplyPoints(-amount);
    }

    public int GetCurrentScore()
    {
        return currentScore;
    }

    public void SetPillVisible(bool visible)
    {
        RectTransform rect = GetPillRect();
        if (rect != null)
            rect.gameObject.SetActive(visible);
    }

    /// <summary>
    /// The score text has no background of its own, so it sits inside a pill
    /// created on first use, in the same place in the hierarchy.
    /// </summary>
    public RectTransform GetPillRect()
    {
        if (scoreText == null) return null;
        if (pill != null) return pill;

        GameObject pillGo = new GameObject("Score Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pill = pillGo.GetComponent<RectTransform>();
        pill.SetParent(scoreText.transform.parent, false);
        pill.SetSiblingIndex(scoreText.transform.GetSiblingIndex());
        scoreText.transform.SetParent(pill, false);
        scoreText.gameObject.SetActive(true);
        pillGo.SetActive(false);
        return pill;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE: {currentScore}";
        }
    }
}
