using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds nine-sliced rounded-rectangle sprites at runtime so corner radius can follow
/// the layout rules. At 100 pixels per unit one sprite pixel equals one canvas unit.
/// </summary>
public static class RoundedSprite
{
    private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

    public static Sprite Get(float radius)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 128);
        if (Cache.TryGetValue(r, out Sprite cached) && cached != null) return cached;

        int size = r * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RoundedSprite_" + r,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(r - (x + 0.5f), x + 0.5f - (size - r), 0f);
                float dy = Mathf.Max(r - (y + 0.5f), y + 0.5f - (size - r), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r));
        sprite.name = texture.name;
        Cache[r] = sprite;
        return sprite;
    }
}
