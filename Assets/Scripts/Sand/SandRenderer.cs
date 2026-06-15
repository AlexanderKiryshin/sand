using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SandRenderer : MonoBehaviour
{
    private const int NoiseTextureSize = 64;
    private const int NoiseRandomSeed = 92837111;

    [SerializeField] private RawImage targetImage;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private int pixelsPerCell = 16;
    [SerializeField] private int subGrainsPerAxis = 4;
    [SerializeField] [Range(0f, 1f)] private float grainStrength = 0.12f;

    public int DisplayScale { get { return pixelsPerCell; } }
    public int SubGrainsPerAxis { get { return subGrainsPerAxis; } }

    public float SubGrainScreenFraction
    {
        get { return 1f / subGrainsPerAxis; }
    }

    private SandGrid grid;
    private Texture2D texture;
    private Color32[] pixelBuffer;
    private Color32[] noisePixels;
    private int noiseBlockSimSize;
    private int pixelsPerSimCell;
    private int textureWidth;
    private int textureHeight;
    private static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    public void Initialize(SandGrid sandGrid)
    {
        grid = sandGrid;
        subGrainsPerAxis = grid.SubGrainsPerAxis;
        pixelsPerCell = Mathf.Max(subGrainsPerAxis, pixelsPerCell);
        pixelsPerSimCell = pixelsPerCell / subGrainsPerAxis;

        textureWidth = grid.Width * pixelsPerSimCell;
        textureHeight = grid.Height * pixelsPerSimCell;
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        pixelBuffer = new Color32[textureWidth * textureHeight];

        noiseBlockSimSize = Mathf.Max(
            1,
            Mathf.CeilToInt((float)Mathf.Max(grid.Width, grid.Height) / NoiseTextureSize));
        noisePixels = BuildNoisePixels(NoiseTextureSize, NoiseTextureSize);
        BakeInitialDisplayColors();

        DisableStaticGrainOverlay();

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
            : grid.GetDisplayColor(x, y);

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

    private void BakeInitialDisplayColors()
    {
        int gridWidth = grid.Width;
        int gridHeight = grid.Height;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                byte colorIndex = grid.GetCell(x, y);
                if (colorIndex == 0)
                {
                    continue;
                }

                grid.SetDisplayColor(x, y, MixGrainColor(colorIndex, x, y));
            }
        }
    }

    private Color32 MixGrainColor(byte colorIndex, int simX, int simY)
    {
        return ApplyGrainNoise(GetGrainColor(colorIndex, simX, simY), simX, simY);
    }

    private Color32 GetGrainColor(byte colorIndex, int simX, int simY)
    {
        if (colorIndex == 0)
        {
            return ClearPixel;
        }

        int paletteIndex = colorIndex - 1;
        if (paletteIndex < 0 || paletteIndex >= grid.Palette.Length)
        {
            return ClearPixel;
        }

        int sub = subGrainsPerAxis;
        int sx = simX % sub;
        int sy = simY % sub;
        return ApplyGrain(grid.Palette[paletteIndex], simX, simY, sx, sy);
    }

    private Color32 ApplyGrainNoise(Color32 baseColor, int simX, int simY)
    {
        if (grainStrength <= 0f || noisePixels == null)
        {
            return baseColor;
        }

        int noiseX = Mathf.Clamp(simX / noiseBlockSimSize, 0, NoiseTextureSize - 1);
        int noiseY = Mathf.Clamp(simY / noiseBlockSimSize, 0, NoiseTextureSize - 1);
        byte noise = noisePixels[noiseY * NoiseTextureSize + noiseX].r;
        float highlight = grainStrength * (noise / 255f);
        return new Color32(
            (byte)Mathf.Clamp(baseColor.r + (255 - baseColor.r) * highlight, 0, 255),
            (byte)Mathf.Clamp(baseColor.g + (255 - baseColor.g) * highlight, 0, 255),
            (byte)Mathf.Clamp(baseColor.b + (255 - baseColor.b) * highlight, 0, 255),
            255);
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

    private static Color32[] BuildNoisePixels(int width, int height)
    {
        Color32[] pixels = new Color32[width * height];
        Random.State previousState = Random.state;
        Random.InitState(NoiseRandomSeed);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = (byte)(Random.Range(0, 2) * 255);
                pixels[y * width + x] = new Color32(value, value, value, 255);
            }
        }

        Random.state = previousState;
        return pixels;
    }

    private static void DisableStaticGrainOverlay()
    {
        SandGrainOverlay overlay = FindObjectOfType<SandGrainOverlay>();
        if (overlay != null)
        {
            overlay.enabled = false;
        }
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
