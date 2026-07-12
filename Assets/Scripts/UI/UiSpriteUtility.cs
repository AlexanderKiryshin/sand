using UnityEngine;

public static class UiSpriteUtility
{
    public struct ProcessedSprite
    {
        public Sprite Sprite;
        public Vector2 NormalizedPivot;
    }

    private const float BackgroundMatchTolerance = 22f;
    private const byte AlphaCutoff = 12;

    public static ProcessedSprite LoadProcessedSprite(string resourcePath, Vector2? customPivot = null, bool fingertipPivot = false)
    {
        Texture2D source = Resources.Load<Texture2D>(resourcePath);
        if (source == null)
        {
            return default;
        }

        Texture2D processed = ProcessTexture(source);
        if (processed == null)
        {
            return default;
        }

        Rect bounds = GetOpaqueBounds(processed);
        if (bounds.width <= 1f || bounds.height <= 1f)
        {
            Object.Destroy(processed);
            return default;
        }

        Texture2D trimmed = CropTexture(processed, bounds);
        Object.Destroy(processed);

        Vector2 pivot = customPivot ?? (fingertipPivot ? ComputeFingertipPivot(trimmed) : new Vector2(0.5f, 0.5f));
        Sprite sprite = Sprite.Create(trimmed, new Rect(0f, 0f, trimmed.width, trimmed.height), pivot, 100f);
        return new ProcessedSprite { Sprite = sprite, NormalizedPivot = pivot };
    }

    public static Sprite LoadProcessedIcon(string resourcePath)
    {
        return LoadProcessedSprite(resourcePath).Sprite;
    }

    private static Texture2D ProcessTexture(Texture2D source)
    {
        Texture2D readable = EnsureReadable(source);
        if (readable == null)
        {
            return null;
        }

        int width = readable.width;
        int height = readable.height;
        Color32[] pixels = readable.GetPixels32();
        Color32 corner = pixels[0];
        Color32 corner2 = pixels[width - 1];

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (IsBackgroundPixel(pixel, corner) || IsBackgroundPixel(pixel, corner2) || IsCheckerboardPixel(pixel))
            {
                pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 0);
            }
        }

        readable.SetPixels32(pixels);
        readable.Apply();
        return readable;
    }

    private static Texture2D EnsureReadable(Texture2D source)
    {
        if (source.isReadable)
        {
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(source.GetPixels32());
            copy.Apply();
            return copy;
        }

        RenderTexture renderTarget = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, renderTarget);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTarget;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTarget);
        return readable;
    }

    private static bool IsBackgroundPixel(Color32 pixel, Color32 reference)
    {
        if (pixel.a < AlphaCutoff)
        {
            return true;
        }

        int dr = Mathf.Abs(pixel.r - reference.r);
        int dg = Mathf.Abs(pixel.g - reference.g);
        int db = Mathf.Abs(pixel.b - reference.b);
        return dr <= BackgroundMatchTolerance && dg <= BackgroundMatchTolerance && db <= BackgroundMatchTolerance;
    }

    private static bool IsCheckerboardPixel(Color32 pixel)
    {
        if (pixel.a < AlphaCutoff)
        {
            return true;
        }

        int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
        int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
        if (max - min > 10)
        {
            return false;
        }

        return pixel.r >= 185 && pixel.g >= 185 && pixel.b >= 185;
    }

    private static Rect GetOpaqueBounds(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= AlphaCutoff)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new Rect(0f, 0f, width, height);
        }

        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Texture2D CropTexture(Texture2D source, Rect bounds)
    {
        int width = Mathf.RoundToInt(bounds.width);
        int height = Mathf.RoundToInt(bounds.height);
        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.filterMode = FilterMode.Bilinear;

        Color[] pixels = source.GetPixels(Mathf.RoundToInt(bounds.x), Mathf.RoundToInt(bounds.y), width, height);
        cropped.SetPixels(pixels);
        cropped.Apply();
        return cropped;
    }

    private static Vector2 ComputeFingertipPivot(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;
        int searchHeight = Mathf.Max(4, height / 3);
        int bestX = 0;
        int bestY = 0;
        bool found = false;

        for (int y = 0; y < searchHeight; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= AlphaCutoff)
                {
                    continue;
                }

                if (!found || x < bestX || (x == bestX && y < bestY))
                {
                    bestX = x;
                    bestY = y;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return new Vector2(0.5f, 0.1f);
        }

        return new Vector2((bestX + 0.5f) / width, (bestY + 0.5f) / height);
    }
}
