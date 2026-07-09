using UnityEngine;

/// <summary>
/// Цвета палитры песка всегда непрозрачные (alpha = 255).
/// </summary>
public static class PaletteColorUtility
{
    public static Color ToColorFieldValue(Color32 color)
    {
        return new Color(
            color.r / 255f,
            color.g / 255f,
            color.b / 255f,
            1f);
    }

    public static Color32 FromColorFieldValue(Color color)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
            255);
    }

    public static void EnsureOpaque(ref Color32 color)
    {
        color.a = 255;
    }

    public static bool ColorsEqual(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
