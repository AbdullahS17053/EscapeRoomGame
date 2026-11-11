using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JSON title_animation on the title box: static, spin, bounce, wobble, glow, sway, heartbeat, flip, pulse.
/// </summary>
public class TitleAnimator : MonoBehaviour
{
    private string mode = "static";
    private float speed = 1f;
    private Vector3 baseScale = Vector3.one;
    private Quaternion baseRotation = Quaternion.identity;
    private Color baseImageColor = Color.clear;
    private Image background;
    private TextMeshProUGUI[] labels;
    private Color[] baseLabelColors;

    public void Play(string animation, float animationSpeed)
    {
        mode = string.IsNullOrEmpty(animation) ? "static" : animation.Trim().ToLowerInvariant();
        speed = animationSpeed > 0f ? animationSpeed : 1f;
        baseScale = Vector3.one;
        baseRotation = Quaternion.identity;
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;

        background = GetComponent<Image>();
        if (background != null) baseImageColor = background.color;

        labels = GetComponentsInChildren<TextMeshProUGUI>(true);
        baseLabelColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            baseLabelColors[i] = labels[i].color;

        enabled = mode != "static";
    }

    public void Stop()
    {
        mode = "static";
        enabled = false;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        RestoreColors();
    }

    private void Update()
    {
        float t = Time.unscaledTime * Mathf.Max(0.01f, speed);
        switch (mode)
        {
            case "spin":
                transform.localRotation = Quaternion.Euler(0f, 0f, t * 120f);
                break;
            case "bounce":
                transform.localScale = baseScale * (1f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.08f);
                break;
            case "wobble":
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 6f) * 8f);
                break;
            case "glow":
                ApplyGlow(0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 3f)));
                break;
            case "sway":
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 6f);
                break;
            case "heartbeat":
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 3.5f)), 3f);
                transform.localScale = baseScale * (1f + beat * 0.12f);
                break;
            case "flip":
                transform.localRotation = Quaternion.Euler(0f, t * 180f, 0f);
                break;
            case "pulse":
                transform.localScale = baseScale * (1f + Mathf.Sin(t * 3f) * 0.06f);
                break;
            default:
                Stop();
                break;
        }
    }

    private void ApplyGlow(float amount)
    {
        if (labels == null) return;
        for (int i = 0; i < labels.Length; i++)
        {
            Color c = baseLabelColors[i];
            c.r = Mathf.Clamp01(c.r * amount + (1f - amount) * 0.4f);
            c.g = Mathf.Clamp01(c.g * amount + (1f - amount) * 0.4f);
            c.b = Mathf.Clamp01(c.b * amount + (1f - amount) * 0.4f);
            labels[i].color = c;
        }
    }

    private void RestoreColors()
    {
        if (background != null) background.color = baseImageColor;
        if (labels == null || baseLabelColors == null) return;
        for (int i = 0; i < labels.Length && i < baseLabelColors.Length; i++)
            labels[i].color = baseLabelColors[i];
    }

    private void OnDisable()
    {
        RestoreColors();
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }
}
