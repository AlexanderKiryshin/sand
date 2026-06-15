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
}
