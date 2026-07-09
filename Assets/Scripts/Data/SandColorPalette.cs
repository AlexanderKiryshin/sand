using UnityEngine;

[CreateAssetMenu(fileName = "SandColorPalette", menuName = "Sand/Color Palette")]
public class SandColorPalette : ScriptableObject
{
    public const int MaxColorCount = 255;

    [SerializeField] private Color32[] colors = new Color32[]
    {
        new Color32(220, 50, 50, 255),
        new Color32(240, 180, 200, 255)
    };

    public int GetColorCount()
    {
        return colors != null ? colors.Length : 0;
    }

    public Color32[] GetColors()
    {
        if (colors == null || colors.Length == 0)
        {
            return new Color32[0];
        }

        int count = Mathf.Min(colors.Length, MaxColorCount);
        Color32[] result = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = colors[i];
        }

        return result;
    }

    public Color32 GetColor(int index)
    {
        if (colors == null || index < 0 || index >= colors.Length)
        {
            return new Color32(0, 0, 0, 0);
        }

        return colors[index];
    }

    public int FindNearestIndex(Color32 color)
    {
        if (colors == null || colors.Length == 0)
        {
            return -1;
        }

        return ColorQuantizer.FindNearestPaletteIndex(color, GetColors());
    }

    private void OnValidate()
    {
        if (colors == null)
        {
            return;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].a = 255;
        }

        if (colors.Length > MaxColorCount)
        {
            Color32[] trimmed = new Color32[MaxColorCount];
            for (int i = 0; i < MaxColorCount; i++)
            {
                trimmed[i] = colors[i];
            }

            colors = trimmed;
        }
    }
}
