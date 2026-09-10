using UnityEngine;

/// <summary>The illustrated pointer shared by all tutorial gestures.</summary>
public static class TutorialHandSprite
{
    private static Sprite cachedSprite;

    // Read the fingertip anchor from the sprite so both tutorials track the art.
    public static Vector2 Pivot
    {
        get
        {
            Sprite sprite = Get();
            return sprite != null ? sprite.pivot / sprite.rect.size : new Vector2(.18f, .89f);
        }
    }

    public static Sprite Get()
    {
        if (cachedSprite == null) cachedSprite = Resources.Load<Sprite>("UI/TutorialHand");
        return cachedSprite;
    }
}
