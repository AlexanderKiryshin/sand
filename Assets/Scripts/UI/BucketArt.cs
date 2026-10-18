using UnityEngine;

/// <summary>One cached bucket design for inventory, conveyor and booster targets.</summary>
public static class BucketArt
{
    // Center of the painted base ellipse, not its lowest antialiased pixel.
    public const float Footprint = -.34f;
    private static Sprite open;
    private static Sprite inverted;
    private static Sprite largeOpen, largeInverted;
    public const float Bottom = -.43f;
    public const float Mouth = .23f;
    public static Sprite Open => open != null ? open : (open = Create(false));
    public static Sprite Inverted => inverted != null ? inverted : (inverted = Create(true));
    public static Sprite LargeOpen => largeOpen != null ? largeOpen : (largeOpen = CreateSquare(false));
    public static Sprite LargeInverted => largeInverted != null ? largeInverted : (largeInverted = CreateSquare(true));

    private static Sprite CreateSquare(bool inverted)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
            if (inverted) p.y = -p.y;
            // Straight walls and a rectangular rim distinguish the two-slot bucket.
            float edge = Mathf.Max(Mathf.Abs(p.x) - .43f, Mathf.Max(-.43f - p.y, p.y - .39f));
            float alpha = Mathf.Clamp01(-edge * size);
            float shade = .74f + .23f * Mathf.Clamp01(1 - Mathf.Abs(p.x + .08f) * 1.8f);
            if (edge > -.02f) shade *= .62f;
            if (p.y > .12f)
            {
                bool inside = Mathf.Abs(p.x) < .37f && p.y < .33f && p.y > .18f;
                shade = inside ? (inverted ? .72f : .34f) : .98f;
            }
            if (p.y < -.30f) shade *= .85f;
            pixels[y * size + x] = new Color(shade, shade, shade, alpha);
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
    }
    private static Sprite Create(bool inverted)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
            if (inverted) p.y = -p.y;
            float bodyWidth = Mathf.Lerp(.31f, .43f, Mathf.InverseLerp(-.34f, .23f, p.y));
            float body = Mathf.Max(Mathf.Abs(p.x) - bodyWidth, Mathf.Max(-.34f - p.y, p.y - .23f));
            float bottom = (((p - new Vector2(0, -.34f)) / new Vector2(.31f, .09f)).magnitude - 1) * .09f;
            float silhouette = Mathf.Min(body, bottom);
            float shade = .72f + .26f * Mathf.Clamp01(1 - Mathf.Abs(p.x + .08f) * 2.1f);
            if (silhouette > -.018f) shade *= .62f;
            float alpha = Mathf.Clamp01(-silhouette * size);
            float mouth = ((p - new Vector2(0, .23f)) / new Vector2(.45f, .16f)).magnitude;
            if (mouth < 1)
            {
                alpha = Mathf.Max(alpha, Mathf.Clamp01((1 - mouth) * 20));
                shade = mouth > .90f ? .60f : mouth > .72f ? 1f : .32f + .22f * Mathf.Clamp01((p.y - .07f) / .32f);
            }
            if (inverted && mouth < .90f) shade = .70f;
            if (inverted && p.y < -.29f && bottom < 0) shade = bottom > -.015f ? .64f : .96f;
            pixels[y * size + x] = new Color(shade, shade, shade, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
    }

}
