using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Преобразует изображение в изображение заданного размера с ограниченной палитрой.
/// Пайплайн: вписывание с сохранением пропорций → палитра (ScriptableObject или авто) → сборка результата.
/// Координаты результата: y = 0 — верх, y растёт вниз (как в LevelData).
/// </summary>
public static class ImageProcessor
{
    private const int MinBucketPixelsDivisor = 400;
    public struct Settings
    {
        public int width;
        public int height;
        public int colorCount;
        public byte alphaThreshold;
        public Color32 backgroundColor;
        public bool autoBackgroundColor;
        public bool autoColorCount;
        public SandColorPalette targetPalette;

        public static Settings Create(int width, int height, int colorCount)
        {
            return new Settings
            {
                width = width,
                height = height,
                colorCount = colorCount,
                alphaThreshold = 128,
                backgroundColor = new Color32(0, 0, 0, 0),
                autoBackgroundColor = true,
                autoColorCount = true,
                targetPalette = null
            };
        }
    }

    public struct Result
    {
        public int width;
        public int height;
        public Color32 letterboxColor;
        public Color32[] resizedPixels;
        public Color32[] quantizedPixels;
        public Color32[] palette;
        public byte[] cellIndices;
    }

    private struct FitResult
    {
        public Color32[] pixels;
        public bool[] isPadding;
        public Color32 letterboxColor;
    }

    public static Result Process(Color32[] sourcePixels, int sourceWidth, int sourceHeight, Settings settings)
    {
        settings.width = Mathf.Clamp(settings.width, 1, LevelData.MaxGridDimension);
        settings.height = Mathf.Clamp(settings.height, 1, LevelData.MaxGridDimension);
        settings.colorCount = Mathf.Clamp(settings.colorCount, 1, SandColorPalette.MaxColorCount);

        if (settings.targetPalette != null)
        {
            int paletteSize = settings.targetPalette.GetColorCount();
            if (paletteSize > 0)
            {
                settings.colorCount = Mathf.Clamp(settings.colorCount, 1, paletteSize);
            }
        }

        Color32 letterboxColor = settings.autoBackgroundColor
            ? DetectBackgroundColor(sourcePixels, sourceWidth, sourceHeight)
            : settings.backgroundColor;

        FitResult fit = FitToCanvas(
            sourcePixels,
            sourceWidth,
            sourceHeight,
            settings.width,
            settings.height,
            letterboxColor,
            settings.alphaThreshold,
            GetSourcePalette(settings));

        Color32[] palette = BuildPalette(fit.pixels, fit.isPadding, settings);
        byte[] indices = MapToPalette(fit.pixels, fit.isPadding, palette, settings.alphaThreshold);
        ColorQuantizer.FillEdgeCellsFromNeighbors(indices, settings.width, settings.height, fit.isPadding);
        int contentCells = CountContentCells(fit.isPadding, indices);
        int minPaletteUsage = Mathf.Max(2, contentCells / MinBucketPixelsDivisor);
        ColorQuantizer.PruneRarePaletteColors(indices, ref palette, minPaletteUsage);
        Color32[] quantized = ApplyPalette(indices, palette, fit.pixels, fit.isPadding, letterboxColor);

        return new Result
        {
            width = settings.width,
            height = settings.height,
            letterboxColor = letterboxColor,
            resizedPixels = fit.pixels,
            quantizedPixels = quantized,
            palette = palette,
            cellIndices = indices
        };
    }

    public static Result Process(Texture2D source, Settings settings)
    {
        Color32[] sourcePixels = source.GetPixels32();
        return Process(sourcePixels, source.width, source.height, settings);
    }

    public static Result BuildFromCells(byte[] cells, Color32[] palette, int width, int height)
    {
        int cellCount = width * height;
        Color32[] quantized = new Color32[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            byte colorIndex = cells[i];
            quantized[i] = colorIndex == 0
                ? new Color32(0, 0, 0, 0)
                : palette[colorIndex - 1];
        }

        return new Result
        {
            width = width,
            height = height,
            letterboxColor = new Color32(0, 0, 0, 0),
            resizedPixels = quantized,
            quantizedPixels = quantized,
            palette = palette,
            cellIndices = cells
        };
    }

    public static Texture2D BuildTexture(Result result, bool flipY = true)
    {
        return BuildTexture(result, 1, flipY);
    }

    public static Texture2D BuildTexture(Result result, int pixelsPerCell, bool flipY = true)
    {
        pixelsPerCell = Mathf.Max(1, pixelsPerCell);
        int outWidth = result.width * pixelsPerCell;
        int outHeight = result.height * pixelsPerCell;
        Color32[] pixels = new Color32[outWidth * outHeight];

        for (int y = 0; y < result.height; y++)
        {
            for (int x = 0; x < result.width; x++)
            {
                Color32 color = result.quantizedPixels[y * result.width + x];
                for (int py = 0; py < pixelsPerCell; py++)
                {
                    for (int px = 0; px < pixelsPerCell; px++)
                    {
                        int destX = x * pixelsPerCell + px;
                        int destY = flipY
                            ? ((result.height - 1 - y) * pixelsPerCell) + py
                            : (y * pixelsPerCell) + py;
                        pixels[destY * outWidth + destX] = color;
                    }
                }
            }
        }

        Texture2D texture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Определяет цвет фона по наиболее частым пикселям на краях исходного изображения.
    /// </summary>
    public static Color32 DetectBackgroundColor(Color32[] source, int sourceWidth, int sourceHeight)
    {
        if (source == null || source.Length == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        int border = Mathf.Max(1, Mathf.Min(sourceWidth, sourceHeight) / 16);
        List<Color32> borderPixels = new List<Color32>();

        for (int y = 0; y < sourceHeight; y++)
        {
            bool onVerticalEdge = y < border || y >= sourceHeight - border;
            int row = y * sourceWidth;
            for (int x = 0; x < sourceWidth; x++)
            {
                bool onHorizontalEdge = x < border || x >= sourceWidth - border;
                if (!onVerticalEdge && !onHorizontalEdge)
                {
                    continue;
                }

                Color32 pixel = source[row + x];
                if (pixel.a >= 128)
                {
                    borderPixels.Add(pixel);
                }
            }
        }

        if (borderPixels.Count == 0)
        {
            return source[0];
        }

        Color32[] palette = ColorQuantizer.SelectPopularColors(borderPixels.ToArray(), 1);
        return palette.Length > 0 ? palette[0] : borderPixels[0];
    }

    private static Color32[] GetSourcePalette(Settings settings)
    {
        if (settings.targetPalette == null)
        {
            return null;
        }

        Color32[] sourceColors = settings.targetPalette.GetColors();
        return sourceColors != null && sourceColors.Length > 0 ? sourceColors : null;
    }

    /// <summary>
    /// Вписывает изображение в холст заданного размера с сохранением пропорций.
    /// Свободное пространство заполняется сдвинутым копированием крайней строки/столбца.
    /// </summary>
    private static FitResult FitToCanvas(
        Color32[] source,
        int sourceWidth,
        int sourceHeight,
        int canvasWidth,
        int canvasHeight,
        Color32 letterboxColor,
        byte alphaThreshold,
        Color32[] snapPalette)
    {
        FitResult result = new FitResult
        {
            pixels = new Color32[canvasWidth * canvasHeight],
            isPadding = new bool[canvasWidth * canvasHeight],
            letterboxColor = letterboxColor
        };

        if (source == null || source.Length == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            for (int i = 0; i < result.pixels.Length; i++)
            {
                result.pixels[i] = letterboxColor;
                result.isPadding[i] = true;
            }

            return result;
        }

        float scale = Mathf.Min(canvasWidth / (float)sourceWidth, canvasHeight / (float)sourceHeight);
        int fittedWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
        int fittedHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        int offsetX = (canvasWidth - fittedWidth) / 2;
        int offsetY = (canvasHeight - fittedHeight) / 2;

        Color32[] fitted = ResizeDominantColor(
            source,
            sourceWidth,
            sourceHeight,
            fittedWidth,
            fittedHeight,
            alphaThreshold,
            snapPalette);

        FillCanvasWithEdgeExtension(
            result.pixels,
            canvasWidth,
            canvasHeight,
            fitted,
            fittedWidth,
            fittedHeight,
            offsetX,
            offsetY);

        for (int i = 0; i < result.isPadding.Length; i++)
        {
            result.isPadding[i] = false;
        }

        return result;
    }

    /// <summary>
    /// Заполняет холст копированием крайней строки/столбца со сдвигом на 1 px на каждый шаг.
    /// Верх/низ — первая/последняя строка, сдвиг по X; лево/право — первый/последний столбец, сдвиг по Y.
    /// </summary>
    public static void FillCanvasWithEdgeExtension(
        Color32[] canvas,
        int canvasWidth,
        int canvasHeight,
        Color32[] fitted,
        int fittedWidth,
        int fittedHeight,
        int offsetX,
        int offsetY)
    {
        if (canvas == null || fitted == null || canvasWidth <= 0 || canvasHeight <= 0
            || fittedWidth <= 0 || fittedHeight <= 0)
        {
            return;
        }

        for (int y = 0; y < canvasHeight; y++)
        {
            int localY = y - offsetY;
            int canvasRow = y * canvasWidth;

            for (int x = 0; x < canvasWidth; x++)
            {
                int localX = x - offsetX;
                GetShiftedEdgeRowColumnSample(
                    localX,
                    localY,
                    fittedWidth,
                    fittedHeight,
                    out int fx,
                    out int fy);
                canvas[canvasRow + x] = fitted[fy * fittedWidth + fx];
            }
        }
    }

    private static void GetShiftedEdgeRowColumnSample(
        int localX,
        int localY,
        int fittedWidth,
        int fittedHeight,
        out int fx,
        out int fy)
    {
        if (localX >= 0 && localX < fittedWidth && localY >= 0 && localY < fittedHeight)
        {
            fx = localX;
            fy = localY;
            return;
        }

        if (localY < 0)
        {
            int depth = -localY - 1;
            fx = Mathf.Clamp(localX + depth, 0, fittedWidth - 1);
            fy = 0;
            return;
        }

        if (localY >= fittedHeight)
        {
            int depth = localY - fittedHeight;
            fx = Mathf.Clamp(localX + depth, 0, fittedWidth - 1);
            fy = fittedHeight - 1;
            return;
        }

        if (localX < 0)
        {
            int depth = -localX - 1;
            fx = 0;
            fy = Mathf.Clamp(localY + depth, 0, fittedHeight - 1);
            return;
        }

        int depthRight = localX - fittedWidth;
        fx = fittedWidth - 1;
        fy = Mathf.Clamp(localY + depthRight, 0, fittedHeight - 1);
    }

    /// <summary>
    /// Алиас для FillCanvasWithEdgeExtension (смещённое продолжение краёв).
    /// </summary>
    public static void FillCanvasWithEdgeClamp(
        Color32[] canvas,
        int canvasWidth,
        int canvasHeight,
        Color32[] fitted,
        int fittedWidth,
        int fittedHeight,
        int offsetX,
        int offsetY)
    {
        FillCanvasWithEdgeExtension(canvas, canvasWidth, canvasHeight, fitted, fittedWidth, fittedHeight, offsetX, offsetY);
    }

    /// <summary>
    /// Изменяет размер, выбирая доминирующий цвет в каждой ячейке (без смешивания на границах).
    /// y = 0 — верх изображения (как в LevelData).
    /// </summary>
    public static Color32[] ResizeDominantColor(
        Color32[] source,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        byte alphaThreshold,
        Color32[] snapPalette = null)
    {
        if (source == null || source.Length == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Color32[targetWidth * targetHeight];
        }

        Color32[] result = new Color32[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            float srcY0 = (1f - (y + 1f) / targetHeight) * sourceHeight;
            float srcY1 = (1f - y / (float)targetHeight) * sourceHeight;
            int iy0 = Mathf.Clamp(Mathf.FloorToInt(srcY0), 0, sourceHeight - 1);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt(srcY1), iy0 + 1, sourceHeight);

            for (int x = 0; x < targetWidth; x++)
            {
                float srcX0 = x * sourceWidth / (float)targetWidth;
                float srcX1 = (x + 1) * sourceWidth / (float)targetWidth;
                int ix0 = Mathf.Clamp(Mathf.FloorToInt(srcX0), 0, sourceWidth - 1);
                int ix1 = Mathf.Clamp(Mathf.CeilToInt(srcX1), ix0 + 1, sourceWidth);

                result[y * targetWidth + x] = ColorQuantizer.SelectDominantColorFromRegion(
                    source,
                    sourceWidth,
                    sourceHeight,
                    ix0,
                    iy0,
                    ix1,
                    iy1,
                    alphaThreshold,
                    snapPalette);
            }
        }

        return result;
    }

    /// <summary>
    /// Изменяет размер с box filter. y = 0 — верх изображения (как в LevelData).
    /// </summary>
    public static Color32[] ResizeBoxFilter(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (source == null || source.Length == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Color32[targetWidth * targetHeight];
        }

        Color32[] result = new Color32[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            float srcY0 = (1f - (y + 1f) / targetHeight) * sourceHeight;
            float srcY1 = (1f - y / (float)targetHeight) * sourceHeight;
            int iy0 = Mathf.Clamp(Mathf.FloorToInt(srcY0), 0, sourceHeight - 1);
            int iy1 = Mathf.Clamp(Mathf.CeilToInt(srcY1), iy0 + 1, sourceHeight);

            for (int x = 0; x < targetWidth; x++)
            {
                float srcX0 = x * sourceWidth / (float)targetWidth;
                float srcX1 = (x + 1) * sourceWidth / (float)targetWidth;
                int ix0 = Mathf.Clamp(Mathf.FloorToInt(srcX0), 0, sourceWidth - 1);
                int ix1 = Mathf.Clamp(Mathf.CeilToInt(srcX1), ix0 + 1, sourceWidth);

                float sumR = 0f;
                float sumG = 0f;
                float sumB = 0f;
                float sumA = 0f;
                int count = 0;

                for (int sy = iy0; sy < iy1; sy++)
                {
                    int row = sy * sourceWidth;
                    for (int sx = ix0; sx < ix1; sx++)
                    {
                        Color32 pixel = source[row + sx];
                        sumR += pixel.r;
                        sumG += pixel.g;
                        sumB += pixel.b;
                        sumA += pixel.a;
                        count++;
                    }
                }

                if (count > 0)
                {
                    result[y * targetWidth + x] = new Color32(
                        (byte)(sumR / count),
                        (byte)(sumG / count),
                        (byte)(sumB / count),
                        (byte)(sumA / count));
                }
            }
        }

        return result;
    }

    private static Color32[] BuildPalette(Color32[] pixels, bool[] isPadding, Settings settings)
    {
        int colorCount = ColorQuantizer.EstimateColorCount(
            pixels,
            isPadding,
            settings.alphaThreshold,
            settings.targetPalette,
            settings.colorCount,
            settings.autoColorCount);
        colorCount = Mathf.Clamp(colorCount, 1, SandColorPalette.MaxColorCount);

        if (settings.targetPalette != null)
        {
            Color32[] sourceColors = settings.targetPalette.GetColors();
            if (sourceColors.Length > 0)
            {
                return ColorQuantizer.SelectPaletteFromSource(
                    pixels,
                    isPadding,
                    sourceColors,
                    colorCount,
                    settings.alphaThreshold);
            }
        }

        List<Color32> contentPixels = new List<Color32>(pixels.Length);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isPadding[i])
            {
                continue;
            }

            Color32 pixel = pixels[i];
            if (pixel.a >= settings.alphaThreshold)
            {
                contentPixels.Add(pixel);
            }
        }

        if (contentPixels.Count == 0)
        {
            return new Color32[0];
        }

        // Exact-color popularity discards gradients and often reduces painted art to two dark colors.
        // Cluster the full color distribution instead; keep imports reproducible and preserve game RNG.
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(42173);
            return ColorQuantizer.KMeansQuantize(contentPixels.ToArray(), colorCount, 24);
        }
        finally { UnityEngine.Random.state = previousRandom; }
    }

    private static byte[] MapToPalette(Color32[] pixels, bool[] isPadding, Color32[] palette, byte alphaThreshold)
    {
        byte[] indices = new byte[pixels.Length];

        if (palette == null || palette.Length == 0)
        {
            return indices;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (isPadding[i] || pixels[i].a < alphaThreshold)
            {
                indices[i] = 0;
                continue;
            }

            indices[i] = (byte)(ColorQuantizer.FindNearestPaletteIndex(pixels[i], palette) + 1);
        }

        return indices;
    }

    private static int CountContentCells(bool[] isPadding, byte[] indices)
    {
        int count = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            if (isPadding != null && isPadding[i])
            {
                continue;
            }

            if (indices[i] > 0)
            {
                count++;
            }
        }

        return count;
    }

    private static Color32[] ApplyPalette(
        byte[] indices,
        Color32[] palette,
        Color32[] sourcePixels,
        bool[] isPadding,
        Color32 letterboxColor)
    {
        Color32[] result = new Color32[indices.Length];

        for (int i = 0; i < indices.Length; i++)
        {
            if (isPadding[i])
            {
                result[i] = letterboxColor;
                continue;
            }

            byte colorIndex = indices[i];
            if (colorIndex == 0)
            {
                result[i] = sourcePixels[i];
            }
            else
            {
                result[i] = palette[colorIndex - 1];
            }
        }

        return result;
    }
}
