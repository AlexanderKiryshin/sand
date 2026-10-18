using System;
using UnityEngine;

public static class ThemeBackgroundAssets
{
    private static readonly Texture2D[] Textures=new Texture2D[ThemeCatalog.Count];
    private static float[] sourceAspects;
    [Serializable] private sealed class SourceDimension { public int theme, width, height; }
    [Serializable] private sealed class SourceDimensions { public SourceDimension[] backgrounds; }

    // Import compression can resample to power-of-two dimensions. Draw using the
    // original aspect ratio so the composition and shop previews stay identical.
    public static float GetSourceAspect(int theme)
    {
        if (sourceAspects == null)
        {
            sourceAspects = new float[ThemeCatalog.Count];
            var metadata = Resources.Load<TextAsset>("ThemeBackgrounds/source-dimensions");
            if (metadata != null)
            {
                var dimensions = JsonUtility.FromJson<SourceDimensions>(metadata.text);
                if (dimensions?.backgrounds != null)
                    foreach (var entry in dimensions.backgrounds)
                        if (entry.theme >= 0 && entry.theme < sourceAspects.Length && entry.width > 0 && entry.height > 0)
                            sourceAspects[entry.theme] = entry.width / (float)entry.height;
                Resources.UnloadAsset(metadata);
            }
        }
        if (theme >= 0 && theme < sourceAspects.Length && sourceAspects[theme] > 0)
            return sourceAspects[theme];
        var texture = Get(theme);
        return texture == null ? 1f : texture.width / (float)texture.height;
    }
    public static Texture2D Get(int theme)
    {
        if(theme<4 || theme>=ThemeCatalog.Count)return null;
        if(Textures[theme]==null)Textures[theme]=Resources.Load<Texture2D>("ThemeBackgrounds/background_"+theme);
        return Textures[theme];
    }
}
