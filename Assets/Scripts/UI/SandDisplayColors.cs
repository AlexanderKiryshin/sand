using TMPro;
using UnityEngine;

public static class SandDisplayColors
{
    public static Color32 FromPalette(Color32 paletteColor)
    {
        if (paletteColor.a == 0)
        {
            return new Color32(180, 180, 180, 255);
        }

        return paletteColor;
    }

    public static Color32 FromPalette(LevelData level, byte colorIndex)
    {
        if (level == null)
        {
            return new Color32(180, 180, 180, 255);
        }

        return FromPalette(level.GetPaletteColor(colorIndex));
    }

    public static void ApplyCapacityLabelStyle(TextMeshProUGUI label, Color32 backgroundColor)
    {
        if (label == null)
        {
            return;
        }

        Color32 background = FromPalette(backgroundColor);
        float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
        bool useDarkText = luminance > 160f;

        if (useDarkText)
        {
            label.color = new Color32(25, 25, 25, 255);
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color(1f, 1f, 1f, 0.9f);
            return;
        }

        label.color = Color.white;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }
}
