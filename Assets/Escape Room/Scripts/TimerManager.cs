using UnityEngine;
using TMPro;
using System;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance;

    public TextMeshProUGUI timerText;

    private float currentTime;
    private bool isRunning;
    private bool countUp;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (isRunning)
        {
            if (countUp)
            {
                currentTime += Time.deltaTime;
            }
            else
            {
                currentTime -= Time.deltaTime;
                if (currentTime <= 0)
                {
                    currentTime = 0;
                    isRunning = false;
                    OnTimerZero();
                }
            }

            UpdateTimerUI();
        }
    }

    public void SetupTimer(StageData stage)
    {
        if (stage.timer_starts_here)
        {
            currentTime = stage.timer_duration_minutes * 60f;
            countUp = (stage.timer_direction != null && stage.timer_direction.ToLower() == "up");

            if (countUp)
            {
                currentTime = 0f;
            }

            isRunning = true;
        }

        if (stage.timer_stops_here)
        {
            isRunning = false;
        }

        UpdateTimerUI();
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public float GetCurrentTime()
    {
        return currentTime;
    }

    public bool IsCountingDown()
    {
        return !countUp;
    }

    /// <summary>
    /// Whole remaining minutes for a countdown timer. Returns 0 when counting up or stopped at zero.
    /// </summary>
    public int GetRemainingCountdownMinutes()
    {
        if (countUp || currentTime <= 0f) return 0;
        return (int)(currentTime / 60f);
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            TimeSpan time = TimeSpan.FromSeconds(currentTime);
            timerText.text = time.ToString(@"mm\:ss");
        }
    }

    private void OnTimerZero()
    {
        Debug.Log("Timer hit zero!");
    }
}
