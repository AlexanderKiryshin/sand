using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SandRenderer : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private int pixelsPerCell = 16;
    [SerializeField] private int subGrainsPerAxis = 4;

    public int DisplayScale { get { return pixelsPerCell; } }
    public int SubGrainsPerAxis { get { return subGrainsPerAxis; } }

    public float SubGrainScreenFraction
    {
        get { return 1f / subGrainsPerAxis; }
    }

    private SandGrid grid;
    private Texture2D texture;
    private Color32[] pixelBuffer;
    private Color32[] grainColorLut;
    private int pixelsPerSimCell;
    private int textureWidth;
    private int textureHeight;
    private int lutSlots;
    private static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    public void Initialize(SandGrid sandGrid)
    {
        grid = sandGrid;
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        pixelsPerCell = Mathf.Max(subGrainsPerAxis, pixelsPerCell);
        pixelsPerSimCell = pixelsPerCell / subGrainsPerAxis;
        lutSlots = subGrainsPerAxis * subGrainsPerAxis;

        textureWidth = grid.Width * pixelsPerSimCell;
        textureHeight = grid.Height * pixelsPerSimCell;
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        pixelBuffer = new Color32[textureWidth * textureHeight];

        BuildGrainColorLut();

        if (targetImage != null)
        {
            targetImage.texture = texture;
        }

        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                pixelsPerSimCell);
        }

        RefreshFull();
    }

    public void RefreshFull()
    {
        if (grid == null || texture == null)
        {
            return;
        }

        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = ClearPixel;
        }

        int gridWidth = grid.Width;
        int gridHeight = grid.Height;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                WriteSimCell(x, y, grid.GetCell(x, y));
            }
        }

        ApplyTexture();
        grid.ClearDirty();
    }

    public void RefreshDirty()
    {
        if (grid == null || texture == null)
        {
            return;
        }

        List<int> dirty = grid.TakeDirtyIndices();
        if (dirty.Count == 0)
        {
            return;
        }

        int gridWidth = grid.Width;
        for (int i = 0; i < dirty.Count; i++)
        {
            int index = dirty[i];
            int x = index % gridWidth;
            int y = index / gridWidth;
            WriteSimCell(x, y, grid.GetCell(x, y));
        }

        ApplyTexture();
        grid.ClearDirty();
    }

    private void WriteSimCell(int x, int y, byte colorIndex)
    {
        int gridHeight = grid.Height;
        int cellOriginX = x * pixelsPerSimCell;
        int displayRow = gridHeight - 1 - y;
        int cellOriginY = displayRow * pixelsPerSimCell;
        Color32 fillColor = colorIndex == 0
            ? ClearPixel
            : GetGrainColor(colorIndex, x, y);

        for (int py = 0; py < pixelsPerSimCell; py++)
        {
            int rowOffset = (cellOriginY + py) * textureWidth + cellOriginX;
            for (int px = 0; px < pixelsPerSimCell; px++)
            {
                pixelBuffer[rowOffset + px] = fillColor;
            }
        }
    }

    private void ApplyTexture()
    {
        texture.SetPixels32(pixelBuffer);
        texture.Apply(false);
    }

    private void BuildGrainColorLut()
    {
        Color32[] palette = grid.Palette;
        int levelCells = grid.LevelWidth * grid.LevelHeight;
        grainColorLut = new Color32[levelCells * lutSlots * palette.Length];

        for (int ly = 0; ly < grid.LevelHeight; ly++)
        {
            for (int lx = 0; lx < grid.LevelWidth; lx++)
            {
                int levelIndex = ly * grid.LevelWidth + lx;
                for (int sy = 0; sy < subGrainsPerAxis; sy++)
                {
                    for (int sx = 0; sx < subGrainsPerAxis; sx++)
                    {
                        int slot = sy * subGrainsPerAxis + sx;
                        for (int p = 0; p < palette.Length; p++)
                        {
                            int lutIndex = (levelIndex * lutSlots + slot) * palette.Length + p;
                            grainColorLut[lutIndex] = ApplyGrain(palette[p], lx, ly, sx, sy);
                        }
                    }
                }
            }
        }
    }

    private Color32 GetGrainColor(byte colorIndex, int simX, int simY)
    {
        if (colorIndex == 0 || grainColorLut == null)
        {
            return ClearPixel;
        }

        int paletteIndex = colorIndex - 1;
        if (paletteIndex < 0 || paletteIndex >= grid.Palette.Length)
        {
            return ClearPixel;
        }

        int sub = subGrainsPerAxis;
        int levelIndex = grid.ToLevelY(simY) * grid.LevelWidth + grid.ToLevelX(simX);
        int slot = (simY % sub) * sub + (simX % sub);
        return grainColorLut[(levelIndex * lutSlots + slot) * grid.Palette.Length + paletteIndex];
    }

    private static Color32 ApplyGrain(Color32 baseColor, int x, int y, int sx, int sy)
    {
        int noise = Hash(x, y, sx, sy) % 21 - 10;
        return new Color32(
            (byte)Mathf.Clamp(baseColor.r + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.g + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.b + noise, 0, 255),
            255);
    }

    private static int Hash(int x, int y, int sx, int sy)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + sx;
            hash = hash * 31 + sy;
            hash = hash * 31 + 92837111;
            return hash & int.MaxValue;
        }
    }
}
