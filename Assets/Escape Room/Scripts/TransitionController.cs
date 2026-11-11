using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage and overlay transitions driven by JSON type / duration / direction / easing.
/// </summary>
public class TransitionController : MonoBehaviour
{
    public static TransitionController Instance { get; private set; }

    private RectTransform root;
    private Image fadeImage;
    private RawImage snapshotImage;
    private RectTransform wipeMask;
    private Image wipeFill;
    private Texture2D lastSnapshot;

    public static TransitionController Ensure(Transform canvas)
    {
        if (Instance != null) return Instance;
        TransitionController existing = canvas != null ? canvas.GetComponent<TransitionController>() : null;
        if (existing == null && canvas != null)
            existing = canvas.gameObject.AddComponent<TransitionController>();
        existing.Build(canvas);
        return existing;
    }

    private void Awake()
    {
        Instance = this;
        Build(transform);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseSnapshot();
    }

    private void Build(Transform canvas)
    {
        if (root != null) return;
        if (canvas == null) canvas = transform;

        GameObject go = new GameObject("TransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas, false);
        go.transform.SetAsLastSibling();
        root = go.GetComponent<RectTransform>();
        VideoFitHelper.StretchFill(root);
        fadeImage = go.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;

        GameObject snapGo = new GameObject("Snapshot", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        snapGo.transform.SetParent(root, false);
        snapshotImage = snapGo.GetComponent<RawImage>();
        VideoFitHelper.StretchFill(snapshotImage.rectTransform);
        snapshotImage.color = Color.white;
        snapshotImage.raycastTarget = false;
        snapGo.SetActive(false);

        GameObject maskGo = new GameObject("WipeMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGo.transform.SetParent(root, false);
        wipeMask = maskGo.GetComponent<RectTransform>();
        VideoFitHelper.StretchFill(wipeMask);
        Image maskImg = maskGo.GetComponent<Image>();
        maskImg.color = Color.white;
        maskImg.raycastTarget = false;
        maskGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject fillGo = new GameObject("WipeFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(maskGo.transform, false);
        wipeFill = fillGo.GetComponent<Image>();
        wipeFill.color = Color.black;
        wipeFill.raycastTarget = false;
        VideoFitHelper.StretchFill(wipeFill.rectTransform);
        maskGo.SetActive(false);

        go.SetActive(false);
    }

    public IEnumerator PlayOut(string type, float duration, string direction, string easing)
    {
        type = Normalize(type);
        if (type == "none") yield break;
        duration = duration > 0f ? duration : 0.5f;
        AnimationCurve curve = CurveFor(easing);
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();

        switch (type)
        {
            case "fade":
            case "blur":
                yield return Fade(0f, 1f, duration * 0.5f, curve);
                break;
            case "crossfade":
            case "dissolve":
            case "morph":
            case "slide":
            case "push":
            case "zoom":
            case "scale":
            case "flip":
            case "rotate":
            case "bounce":
            case "swirl":
            case "wipe":
            case "reveal":
                yield return CaptureSnapshot();
                break;
            default:
                yield return Fade(0f, 1f, duration * 0.5f, curve);
                break;
        }
    }

    public IEnumerator PlayIn(string type, float duration, string direction, string easing)
    {
        type = Normalize(type);
        if (type == "none")
        {
            Hide();
            yield break;
        }

        duration = duration > 0f ? duration : 0.5f;
        AnimationCurve curve = CurveFor(easing);
        Vector2 dir = DirectionVector(direction);
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();

        switch (type)
        {
            case "fade":
                yield return Fade(1f, 0f, duration * 0.5f, curve);
                break;
            case "blur":
                yield return Fade(1f, 0f, duration * 0.5f, curve);
                break;
            case "crossfade":
            case "dissolve":
            case "morph":
                yield return FadeSnapshot(1f, 0f, duration, curve);
                break;
            case "slide":
            case "push":
                yield return SlideSnapshot(dir, duration, curve);
                break;
            case "zoom":
            case "scale":
                yield return ScaleSnapshot(1f, 1.4f, duration, curve, true);
                break;
            case "bounce":
                yield return BounceIn(duration, curve);
                break;
            case "flip":
            case "rotate":
                yield return FlipSnapshot(duration, curve, type == "rotate");
                break;
            case "wipe":
            case "reveal":
                yield return Wipe(duration, curve, dir, type == "reveal");
                break;
            case "swirl":
                yield return SwirlSnapshot(duration, curve);
                break;
            default:
                yield return Fade(1f, 0f, duration * 0.5f, curve);
                break;
        }

        Hide();
    }

    public void Cancel()
    {
        StopAllCoroutines();
        Hide();
    }

    public IEnumerator PlayOverlay(string type, float duration, string direction, string easing)
    {
        type = Normalize(type);
        if (type == "none") yield break;
        duration = duration > 0f ? duration : 0.35f;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        if (type == "fade" || type == "blur")
        {
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
            yield return Fade(1f, 0f, duration, CurveFor(easing));
            Hide();
            yield break;
        }

        yield return PlayIn(type, duration, direction, easing);
    }

    private IEnumerator Fade(float from, float to, float duration, AnimationCurve curve)
    {
        snapshotImage.gameObject.SetActive(false);
        wipeMask.gameObject.SetActive(false);
        fadeImage.enabled = true;
        float t = 0f;
        Color c = fadeImage.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            c.a = Mathf.Lerp(from, to, k);
            fadeImage.color = c;
            yield return null;
        }
        c.a = to;
        fadeImage.color = c;
    }

    private IEnumerator FadeSnapshot(float from, float to, float duration, AnimationCurve curve)
    {
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        snapshotImage.gameObject.SetActive(true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            snapshotImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, k));
            yield return null;
        }
    }

    private IEnumerator SlideSnapshot(Vector2 dir, float duration, AnimationCurve curve)
    {
        snapshotImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        RectTransform rect = snapshotImage.rectTransform;
        Vector2 start = Vector2.zero;
        Vector2 end = new Vector2(dir.x * Screen.width, dir.y * Screen.height);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            rect.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        rect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator ScaleSnapshot(float from, float to, float duration, AnimationCurve curve, bool fadeOut)
    {
        snapshotImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            float s = Mathf.Lerp(from, to, k);
            snapshotImage.rectTransform.localScale = Vector3.one * s;
            if (fadeOut) snapshotImage.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        snapshotImage.rectTransform.localScale = Vector3.one;
        snapshotImage.color = Color.white;
    }

    private IEnumerator BounceIn(float duration, AnimationCurve curve)
    {
        fadeImage.enabled = true;
        snapshotImage.gameObject.SetActive(false);
        float t = 0f;
        Color c = fadeImage.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float bounce = 1f - Mathf.Sin(k * Mathf.PI);
            c.a = bounce;
            fadeImage.color = c;
            root.localScale = Vector3.one * (0.92f + 0.08f * curve.Evaluate(k));
            yield return null;
        }
        root.localScale = Vector3.one;
        c.a = 0f;
        fadeImage.color = c;
    }

    private IEnumerator FlipSnapshot(float duration, AnimationCurve curve, bool fullRotate)
    {
        snapshotImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        float t = 0f;
        float target = fullRotate ? 360f : 90f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            snapshotImage.rectTransform.localRotation = Quaternion.Euler(0f, target * k, 0f);
            snapshotImage.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        snapshotImage.rectTransform.localRotation = Quaternion.identity;
        snapshotImage.color = Color.white;
    }

    private IEnumerator SwirlSnapshot(float duration, AnimationCurve curve)
    {
        snapshotImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            snapshotImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 180f);
            snapshotImage.rectTransform.localScale = Vector3.one * (1f + k * 0.25f);
            snapshotImage.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        snapshotImage.rectTransform.localRotation = Quaternion.identity;
        snapshotImage.rectTransform.localScale = Vector3.one;
        snapshotImage.color = Color.white;
    }

    private IEnumerator Wipe(float duration, AnimationCurve curve, Vector2 dir, bool reveal)
    {
        snapshotImage.gameObject.SetActive(false);
        wipeMask.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        RectTransform fill = wipeFill.rectTransform;
        VideoFitHelper.StretchFill(fill);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / duration));
            float amount = reveal ? 1f - k : k;
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                fill.anchorMin = dir.x >= 0 ? new Vector2(0f, 0f) : new Vector2(1f - amount, 0f);
                fill.anchorMax = dir.x >= 0 ? new Vector2(amount, 1f) : new Vector2(1f, 1f);
            }
            else
            {
                fill.anchorMin = dir.y >= 0 ? new Vector2(0f, 0f) : new Vector2(0f, 1f - amount);
                fill.anchorMax = dir.y >= 0 ? new Vector2(1f, amount) : new Vector2(1f, 1f);
            }
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            yield return null;
        }
        wipeMask.gameObject.SetActive(false);
    }

    private IEnumerator CaptureSnapshot()
    {
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        root.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();
        ReleaseSnapshot();
        lastSnapshot = ScreenCapture.CaptureScreenshotAsTexture();
        snapshotImage.texture = lastSnapshot;
        snapshotImage.color = Color.white;
        snapshotImage.rectTransform.anchoredPosition = Vector2.zero;
        snapshotImage.rectTransform.localScale = Vector3.one;
        snapshotImage.rectTransform.localRotation = Quaternion.identity;
        snapshotImage.gameObject.SetActive(true);
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
    }

    private void Hide()
    {
        if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);
        if (snapshotImage != null)
        {
            snapshotImage.gameObject.SetActive(false);
            snapshotImage.rectTransform.anchoredPosition = Vector2.zero;
            snapshotImage.rectTransform.localScale = Vector3.one;
            snapshotImage.rectTransform.localRotation = Quaternion.identity;
            snapshotImage.color = Color.white;
        }
        if (wipeMask != null) wipeMask.gameObject.SetActive(false);
        if (root != null)
        {
            root.localScale = Vector3.one;
            root.gameObject.SetActive(false);
        }
        ReleaseSnapshot();
    }

    private void ReleaseSnapshot()
    {
        if (lastSnapshot == null) return;
        Destroy(lastSnapshot);
        lastSnapshot = null;
        if (snapshotImage != null) snapshotImage.texture = null;
    }

    private static string Normalize(string type)
    {
        if (string.IsNullOrEmpty(type)) return "fade";
        return type.Trim().ToLowerInvariant();
    }

    private static Vector2 DirectionVector(string direction)
    {
        switch ((direction ?? "").Trim().ToLowerInvariant())
        {
            case "right": return Vector2.right;
            case "up": return Vector2.up;
            case "down": return Vector2.down;
            case "out": return Vector2.right;
            case "in": return Vector2.left;
            case "left":
            default: return Vector2.left;
        }
    }

    public static AnimationCurve CurveFor(string easing)
    {
        switch ((easing ?? "").Trim().ToLowerInvariant())
        {
            case "linear": return AnimationCurve.Linear(0f, 0f, 1f, 1f);
            case "ease-in": return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));
            case "ease-out": return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            case "ease-in-back":
            case "ease-out-back":
            case "ease-in-out-back":
                return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 1.12f), new Keyframe(1f, 1f, 0f, 0f));
            case "ease-in-circ":
            case "ease-out-circ":
            case "ease-in-out-circ":
                return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            case "ease-in-elastic":
            case "ease-out-elastic":
            case "ease-in-out-elastic":
                return new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 1.15f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 1f));
            case "ease":
            case "ease-in-out":
            default:
                return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
}
