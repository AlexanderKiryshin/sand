using UnityEngine;

public static class TutorialHandSprite
{
    private const string ResourcePath = "UI/TutorialHand";

    private static Sprite cachedSprite;
    private static Vector2 cachedPivot = new Vector2(0.22f, 0.12f);

    public static Vector2 Pivot => cachedPivot;

    public static Sprite Get()
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        UiSpriteUtility.ProcessedSprite result = UiSpriteUtility.LoadProcessedSprite(ResourcePath, fingertipPivot: true);
        cachedSprite = result.Sprite;
        if (cachedSprite != null)
        {
            cachedPivot = result.NormalizedPivot;
            return cachedSprite;
        }

        cachedSprite = CreateFallback();
        return cachedSprite;
    }

    private static Sprite CreateFallback()
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                texture.SetPixel(x, y, new Color(1f, 0.9f, 0.82f, 1f));
            }
        }

        texture.Apply();
        cachedPivot = new Vector2(0.15f, 0.1f);
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), cachedPivot, 100f);
    }
}
