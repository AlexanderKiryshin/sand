using UnityEngine;

public static class SandGrainColorBaker
{
    // Baked once; the subtle texture travels with each simulated grain.
    public const bool GrainNoiseEnabled = true;

    private const int NoiseTextureSize = 64;
    private const int NoiseRandomSeed = 92837111;
    private static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    public static void Bake(SandGrid grid, float grainStrength)
    {
        if (grid == null)
        {
            return;
        }

        int gridWidth = grid.Width;
        int gridHeight = grid.Height;
        int subGrains = grid.SubGrainsPerAxis;
        Color32[] palette = grid.Palette;

        int noiseBlockSimSize = Mathf.Max(
            1,
            Mathf.CeilToInt((float)Mathf.Max(gridWidth, gridHeight) / NoiseTextureSize));
        Color32[] noisePixels = BuildNoisePixels(NoiseTextureSize, NoiseTextureSize);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                byte colorIndex = grid.GetCell(x, y);
                if (colorIndex == 0)
                {
                    continue;
                }

                Color32 color = MixGrainColor(
                    palette,
                    subGrains,
                    colorIndex,
                    x,
                    y,
                    grainStrength,
                    noisePixels,
                    noiseBlockSimSize);
                grid.SetDisplayColorSilent(x, y, color);
            }
        }
    }

    private static Color32 MixGrainColor(
        Color32[] palette,
        int subGrainsPerAxis,
        byte colorIndex,
        int simX,
        int simY,
        float grainStrength,
        Color32[] noisePixels,
        int noiseBlockSimSize)
    {
        return ApplyGrainNoise(
            GetGrainColor(palette, subGrainsPerAxis, colorIndex, simX, simY),
            simX,
            simY,
            grainStrength,
            noisePixels,
            noiseBlockSimSize);
    }

    private static Color32 GetGrainColor(
        Color32[] palette,
        int subGrainsPerAxis,
        byte colorIndex,
        int simX,
        int simY)
    {
        if (colorIndex == 0)
        {
            return ClearPixel;
        }

        int paletteIndex = colorIndex - 1;
        if (paletteIndex < 0 || paletteIndex >= palette.Length)
        {
            return ClearPixel;
        }

        if (!GrainNoiseEnabled)
        {
            return palette[paletteIndex];
        }

        int sx = simX % subGrainsPerAxis;
        int sy = simY % subGrainsPerAxis;
        return ApplyGrain(palette[paletteIndex], simX, simY, sx, sy);
    }

    private static Color32 ApplyGrainNoise(
        Color32 baseColor,
        int simX,
        int simY,
        float grainStrength,
        Color32[] noisePixels,
        int noiseBlockSimSize)
    {
        if (!GrainNoiseEnabled || grainStrength <= 0f || noisePixels == null)
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
        if (!GrainNoiseEnabled)
        {
            return baseColor;
        }

        int noise = Hash(x, y, sx, sy) % 9 - 4;
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

    private static int Hash(int x, int y, int sx, int sy)
    {
        unchecked
        {
            uint hash = (uint)(x * 73856093 ^ y * 19349663 ^ 92837111);
            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            hash *= 0x846ca68b;
            hash ^= hash >> 16;
            return (int)(hash & int.MaxValue);
        }
    }
}
