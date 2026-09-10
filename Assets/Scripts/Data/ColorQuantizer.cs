using System;
using System.Collections.Generic;
using UnityEngine;

public static class ColorQuantizer
{
    private const float PaletteMergeDistanceSq = 36f;
    private const int MinBucketPixelsDivisor = 400;

    /// <summary>
    /// Выбирает палитру из наиболее частых цветов изображения.
    /// Похожие оттенки группируются в корзины, затем берутся top-N по популярности.
    /// </summary>
    public static Color32[] SelectPopularColors(Color32[] pixels, int colorCount, int bucketShift = 3)
    {
        if (pixels == null || pixels.Length == 0 || colorCount <= 0)
        {
            return new Color32[0];
        }

        colorCount = Mathf.Clamp(colorCount, 1, SandColorPalette.MaxColorCount);

        Dictionary<int, int> exactCounts = new Dictionary<int, int>();
        Dictionary<int, Color32> exactSamples = new Dictionary<int, Color32>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.a < 128)
            {
                continue;
            }

            int key = MakeExactColorKey(pixel);
            exactCounts.TryGetValue(key, out int count);
            exactCounts[key] = count + 1;
            exactSamples[key] = new Color32(pixel.r, pixel.g, pixel.b, 255);
        }

        if (exactCounts.Count == 0)
        {
            return new Color32[0];
        }

        List<KeyValuePair<int, int>> rankedColors = new List<KeyValuePair<int, int>>(exactCounts);
        rankedColors.Sort((a, b) => b.Value.CompareTo(a.Value));

        int minPixels = Mathf.Max(4, pixels.Length / MinBucketPixelsDivisor);
        List<Color32> palette = new List<Color32>(colorCount);
        for (int i = 0; i < rankedColors.Count && palette.Count < colorCount; i++)
        {
            if (rankedColors[i].Value < minPixels && palette.Count >= 2)
            {
                continue;
            }

            Color32 candidate = exactSamples[rankedColors[i].Key];
            if (IsSimilarToAny(candidate, palette))
            {
                continue;
            }

            palette.Add(candidate);
        }

        if (palette.Count == 0)
        {
            palette.Add(exactSamples[rankedColors[0].Key]);
        }

        return palette.ToArray();
    }

    /// <summary>
    /// Выбирает доминирующий цвет в прямоугольной области исходного изображения.
    /// Если задана палитра — голосует по ближайшему цвету таблицы; иначе берёт самый частый исходный пиксель.
    /// </summary>
    public static Color32 SelectDominantColorFromRegion(
        Color32[] source,
        int sourceWidth,
        int sourceHeight,
        int ix0,
        int iy0,
        int ix1,
        int iy1,
        byte alphaThreshold,
        Color32[] snapPalette = null)
    {
        if (source == null || source.Length == 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        ix0 = Mathf.Clamp(ix0, 0, sourceWidth);
        ix1 = Mathf.Clamp(ix1, 0, sourceWidth);
        iy0 = Mathf.Clamp(iy0, 0, sourceHeight);
        iy1 = Mathf.Clamp(iy1, 0, sourceHeight);

        if (ix1 <= ix0 || iy1 <= iy0)
        {
            return new Color32(0, 0, 0, 0);
        }

        if (snapPalette != null && snapPalette.Length > 0)
        {
            return SelectDominantPaletteColorFromRegion(
                source,
                sourceWidth,
                ix0,
                iy0,
                ix1,
                iy1,
                alphaThreshold,
                snapPalette);
        }

        return SelectDominantExactColorFromRegion(
            source,
            sourceWidth,
            ix0,
            iy0,
            ix1,
            iy1,
            alphaThreshold);
    }

    private static Color32 SelectDominantPaletteColorFromRegion(
        Color32[] source,
        int sourceWidth,
        int ix0,
        int iy0,
        int ix1,
        int iy1,
        byte alphaThreshold,
        Color32[] palette)
    {
        int[] votes = new int[palette.Length];
        for (int sy = iy0; sy < iy1; sy++)
        {
            int row = sy * sourceWidth;
            for (int sx = ix0; sx < ix1; sx++)
            {
                Color32 pixel = source[row + sx];
                if (pixel.a < alphaThreshold)
                {
                    continue;
                }

                votes[FindNearestPaletteIndex(pixel, palette)]++;
            }
        }

        int bestIndex = -1;
        int bestVotes = 0;
        for (int i = 0; i < votes.Length; i++)
        {
            if (votes[i] > bestVotes)
            {
                bestVotes = votes[i];
                bestIndex = i;
            }
        }

        if (bestIndex < 0 || bestVotes <= 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        Color32 paletteColor = palette[bestIndex];
        return new Color32(paletteColor.r, paletteColor.g, paletteColor.b, 255);
    }

    private static Color32 SelectDominantExactColorFromRegion(
        Color32[] source,
        int sourceWidth,
        int ix0,
        int iy0,
        int ix1,
        int iy1,
        byte alphaThreshold)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        Dictionary<int, Color32> samples = new Dictionary<int, Color32>();

        for (int sy = iy0; sy < iy1; sy++)
        {
            int row = sy * sourceWidth;
            for (int sx = ix0; sx < ix1; sx++)
            {
                Color32 pixel = source[row + sx];
                if (pixel.a < alphaThreshold)
                {
                    continue;
                }

                int key = MakeExactColorKey(pixel);
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
                samples[key] = new Color32(pixel.r, pixel.g, pixel.b, 255);
            }
        }

        if (counts.Count == 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        int bestKey = 0;
        int bestCount = -1;
        foreach (KeyValuePair<int, int> pair in counts)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestKey = pair.Key;
            }
        }

        return samples[bestKey];
    }

    /// <summary>
    /// Выбирает доминирующий цвет среди пикселей (для ресайза пиксель-арта без смешивания на границах).
    /// </summary>
    public static Color32 SelectDominantColor(Color32[] pixels, byte alphaThreshold, Color32[] snapPalette = null)
    {
        if (pixels == null || pixels.Length == 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        if (snapPalette != null && snapPalette.Length > 0)
        {
            int[] votes = new int[snapPalette.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a < alphaThreshold)
                {
                    continue;
                }

                votes[FindNearestPaletteIndex(pixel, snapPalette)]++;
            }

            int bestIndex = -1;
            int bestVotes = 0;
            for (int i = 0; i < votes.Length; i++)
            {
                if (votes[i] > bestVotes)
                {
                    bestVotes = votes[i];
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestVotes <= 0)
            {
                return new Color32(0, 0, 0, 0);
            }

            Color32 paletteColor = snapPalette[bestIndex];
            return new Color32(paletteColor.r, paletteColor.g, paletteColor.b, 255);
        }

        Dictionary<int, int> counts = new Dictionary<int, int>();
        Dictionary<int, Color32> samples = new Dictionary<int, Color32>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.a < alphaThreshold)
            {
                continue;
            }

            int key = MakeExactColorKey(pixel);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
            samples[key] = new Color32(pixel.r, pixel.g, pixel.b, 255);
        }

        if (counts.Count == 0)
        {
            return new Color32(0, 0, 0, 0);
        }

        int bestKey = 0;
        int bestCount = -1;
        foreach (KeyValuePair<int, int> pair in counts)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestKey = pair.Key;
            }
        }

        return samples[bestKey];
    }

    /// <summary>
    /// Удаляет редкие цвета палитры и переназначает затронутые ячейки на ближайший оставшийся цвет.
    /// </summary>
    public static void PruneRarePaletteColors(byte[] cells, ref Color32[] palette, int minUsage)
    {
        if (cells == null || palette == null || palette.Length == 0 || minUsage <= 1)
        {
            return;
        }

        int[] usage = new int[palette.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex > 0 && colorIndex <= palette.Length)
            {
                usage[colorIndex - 1]++;
            }
        }

        List<Color32> keptPalette = new List<Color32>(palette.Length);
        int[] remap = new int[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            if (usage[i] >= minUsage)
            {
                remap[i] = keptPalette.Count;
                keptPalette.Add(palette[i]);
            }
            else
            {
                remap[i] = -1;
            }
        }

        if (keptPalette.Count == palette.Length)
        {
            return;
        }

        if (keptPalette.Count == 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < usage.Length; i++)
            {
                if (usage[i] > usage[bestIndex])
                {
                    bestIndex = i;
                }
            }

            keptPalette.Add(palette[bestIndex]);
            remap[bestIndex] = 0;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex == 0)
            {
                continue;
            }

            int oldIndex = colorIndex - 1;
            if (oldIndex < 0 || oldIndex >= remap.Length)
            {
                cells[i] = 0;
                continue;
            }

            if (remap[oldIndex] >= 0)
            {
                cells[i] = (byte)(remap[oldIndex] + 1);
                continue;
            }

            cells[i] = (byte)(FindNearestPaletteIndex(palette[oldIndex], keptPalette.ToArray()) + 1);
        }

        palette = keptPalette.ToArray();
    }

    public static Color32[] KMeansQuantize(Color32[] pixels, int colorCount, int maxIterations)
    {
        if (pixels == null || pixels.Length == 0 || colorCount <= 0)
        {
            return new Color32[0];
        }

        colorCount = Mathf.Clamp(colorCount, 1, SandColorPalette.MaxColorCount);
        maxIterations = Mathf.Max(1, maxIterations);

        Vector3[] centers = InitializeCenters(pixels, colorCount);
        int[] assignments = new int[pixels.Length];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool changed = AssignPixels(pixels, centers, assignments);
            RecalculateCenters(pixels, centers, assignments, colorCount);
            if (!changed)
            {
                break;
            }
        }

        Color32[] palette = new Color32[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            palette[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(centers[i].x), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(centers[i].y), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(centers[i].z), 0, 255),
                255);
        }

        return palette;
    }

    public static byte[] MapToPalette(Color32[] pixels, Color32[] palette)
    {
        byte[] result = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            result[i] = (byte)(FindNearestPaletteIndex(pixels[i], palette) + 1);
        }

        return result;
    }

    /// <summary>
    /// Заполняет пустые ячейки на границах рисунка цветом соседних ячеек.
    /// </summary>
    public static void FillEdgeCellsFromNeighbors(byte[] cells, int width, int height, bool[] isPadding = null)
    {
        if (cells == null || width <= 0 || height <= 0)
        {
            return;
        }

        int cellCount = width * height;
        if (cells.Length != cellCount)
        {
            return;
        }

        byte[] work = (byte[])cells.Clone();
        for (int pass = 0; pass < cellCount; pass++)
        {
            bool changed = false;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (IsPaddingCell(isPadding, index) || work[index] != 0)
                    {
                        continue;
                    }

                    byte neighborColor = GetMostCommonNeighborColor(work, isPadding, width, height, x, y);
                    if (neighborColor == 0)
                    {
                        continue;
                    }

                    work[index] = neighborColor;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        Array.Copy(work, cells, cellCount);
    }

    private static bool IsPaddingCell(bool[] isPadding, int index)
    {
        return isPadding != null && index >= 0 && index < isPadding.Length && isPadding[index];
    }

    private static byte GetMostCommonNeighborColor(
        byte[] cells,
        bool[] isPadding,
        int width,
        int height,
        int x,
        int y)
    {
        int[] counts = new int[256];
        byte bestColor = 0;
        int bestCount = 0;

        for (int direction = 0; direction < 8; direction++)
        {
            int nx = x + NeighborOffsetX[direction];
            int ny = y + NeighborOffsetY[direction];
            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            {
                continue;
            }

            int neighborIndex = ny * width + nx;
            if (IsPaddingCell(isPadding, neighborIndex))
            {
                continue;
            }

            byte colorIndex = cells[neighborIndex];
            if (colorIndex == 0)
            {
                continue;
            }

            counts[colorIndex]++;
            if (counts[colorIndex] > bestCount)
            {
                bestCount = counts[colorIndex];
                bestColor = colorIndex;
            }
        }

        return bestColor;
    }

    private static readonly int[] NeighborOffsetX = { -1, 1, 0, 0, -1, 1, -1, 1 };
    private static readonly int[] NeighborOffsetY = { 0, 0, -1, 1, -1, -1, 1, 1 };

    public static int FindNearestPaletteIndex(Color32 color, Color32[] palette)
    {
        int exactIndex = FindExactPaletteIndex(color, palette);
        if (exactIndex >= 0)
        {
            return exactIndex;
        }

        int bestIndex = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < palette.Length; i++)
        {
            float distance = ColorDistance(color, palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public static int FindExactPaletteIndex(Color32 color, Color32[] palette)
    {
        if (palette == null || palette.Length == 0)
        {
            return -1;
        }

        for (int i = 0; i < palette.Length; i++)
        {
            Color32 paletteColor = palette[i];
            if (paletteColor.r == color.r && paletteColor.g == color.g && paletteColor.b == color.b)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Определяет число цветов: вручную или автоматически по содержимому изображения.
    /// </summary>
    public static int EstimateColorCount(
        Color32[] pixels,
        bool[] isPadding,
        byte alphaThreshold,
        SandColorPalette targetPalette,
        int manualColorCount,
        bool autoColorCount)
    {
        int maxColors = SandColorPalette.MaxColorCount;
        Color32[] sourcePalette = null;
        if (targetPalette != null)
        {
            sourcePalette = targetPalette.GetColors();
            if (sourcePalette.Length > 0)
            {
                maxColors = Mathf.Min(maxColors, sourcePalette.Length);
            }
        }

        if (!autoColorCount)
        {
            return Mathf.Clamp(manualColorCount, 2, maxColors);
        }

        if (sourcePalette != null && sourcePalette.Length > 0)
        {
            return EstimateUsedPaletteColorCount(pixels, isPadding, sourcePalette, alphaThreshold, maxColors);
        }

        return EstimateDistinctColorCount(pixels, isPadding, alphaThreshold, maxColors);
    }

    /// <summary>
    /// Считает заметные цветовые группы в изображении (без целевой палитры).
    /// </summary>
    public static int EstimateDistinctColorCount(
        Color32[] pixels,
        bool[] isPadding,
        byte alphaThreshold,
        int maxColors)
    {
        maxColors = Mathf.Clamp(maxColors, 2, SandColorPalette.MaxColorCount);
        if (pixels == null || pixels.Length == 0)
        {
            return 2;
        }

        const int bucketShift = 3;
        Dictionary<int, ColorBucket> buckets = CollectBucketsFromMasked(pixels, isPadding, alphaThreshold, bucketShift);
        if (buckets.Count == 0)
        {
            return 2;
        }

        int contentPixels = 0;
        foreach (KeyValuePair<int, ColorBucket> pair in buckets)
        {
            contentPixels += pair.Value.count;
        }

        List<KeyValuePair<int, ColorBucket>> rankedBuckets = new List<KeyValuePair<int, ColorBucket>>(buckets);
        rankedBuckets.Sort(CompareBucketsByCount);

        int minPixels = Mathf.Max(4, contentPixels / MinBucketPixelsDivisor);
        int result = 0;
        List<Color32> acceptedColors = new List<Color32>(maxColors);
        for (int i = 0; i < rankedBuckets.Count; i++)
        {
            ColorBucket bucket = rankedBuckets[i].Value;
            if (bucket.count < minPixels && result >= 2)
            {
                break;
            }

            Color32 candidate = BucketToColor(bucket);
            if (IsSimilarToAny(candidate, acceptedColors))
            {
                continue;
            }

            acceptedColors.Add(candidate);
            result++;
            if (result >= maxColors)
            {
                break;
            }
        }

        return Mathf.Clamp(Mathf.Max(result, 2), 2, maxColors);
    }

    /// <summary>
    /// Считает, сколько цветов из sourcePalette реально используется в изображении.
    /// </summary>
    public static int EstimateUsedPaletteColorCount(
        Color32[] pixels,
        bool[] isPadding,
        Color32[] sourcePalette,
        byte alphaThreshold,
        int maxColors)
    {
        maxColors = Mathf.Clamp(maxColors, 2, SandColorPalette.MaxColorCount);
        if (pixels == null || pixels.Length == 0 || sourcePalette == null || sourcePalette.Length == 0)
        {
            return 2;
        }

        int[] usage = new int[sourcePalette.Length];
        int contentPixels = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isPadding != null && isPadding[i])
            {
                continue;
            }

            if (pixels[i].a < alphaThreshold)
            {
                continue;
            }

            contentPixels++;
            int nearestIndex = FindNearestPaletteIndex(pixels[i], sourcePalette);
            usage[nearestIndex]++;
        }

        if (contentPixels == 0)
        {
            return 2;
        }

        List<int> rankedUsage = new List<int>(sourcePalette.Length);
        for (int i = 0; i < usage.Length; i++)
        {
            if (usage[i] > 0)
            {
                rankedUsage.Add(usage[i]);
            }
        }

        if (rankedUsage.Count == 0)
        {
            return 2;
        }

        rankedUsage.Sort((int a, int b) => b.CompareTo(a));
        int minUsage = Mathf.Max(4, contentPixels / MinBucketPixelsDivisor);
        int result = 0;
        for (int i = 0; i < rankedUsage.Count; i++)
        {
            if (rankedUsage[i] < minUsage && result >= 2)
            {
                break;
            }

            result++;
            if (result >= maxColors)
            {
                break;
            }
        }

        return Mathf.Clamp(Mathf.Max(result, 2), 2, Mathf.Min(maxColors, sourcePalette.Length));
    }

    /// <summary>
    /// Выбирает colorCount цветов из sourcePalette, которые чаще всего
    /// соответствуют пикселям изображения (после сопоставления со всей палитрой).
    /// </summary>
    public static Color32[] SelectPaletteFromSource(
        Color32[] pixels,
        bool[] isPadding,
        Color32[] sourcePalette,
        int colorCount,
        byte alphaThreshold)
    {
        if (sourcePalette == null || sourcePalette.Length == 0)
        {
            return new Color32[0];
        }

        colorCount = Mathf.Clamp(colorCount, 1, SandColorPalette.MaxColorCount);
        colorCount = Mathf.Min(colorCount, sourcePalette.Length);

        if (colorCount >= sourcePalette.Length)
        {
            Color32[] allColors = new Color32[sourcePalette.Length];
            for (int i = 0; i < sourcePalette.Length; i++)
            {
                allColors[i] = sourcePalette[i];
            }

            return allColors;
        }

        int[] usage = new int[sourcePalette.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isPadding != null && isPadding[i])
            {
                continue;
            }

            if (pixels[i].a < alphaThreshold)
            {
                continue;
            }

            int nearestIndex = FindNearestPaletteIndex(pixels[i], sourcePalette);
            usage[nearestIndex]++;
        }

        List<int> rankedIndices = new List<int>(sourcePalette.Length);
        for (int i = 0; i < sourcePalette.Length; i++)
        {
            rankedIndices.Add(i);
        }

        rankedIndices.Sort((a, b) =>
        {
            int usageCompare = usage[b].CompareTo(usage[a]);
            if (usageCompare != 0)
            {
                return usageCompare;
            }

            return a.CompareTo(b);
        });

        int contentPixels = 0;
        for (int i = 0; i < usage.Length; i++)
        {
            contentPixels += usage[i];
        }

        int minUsage = Mathf.Max(4, contentPixels / MinBucketPixelsDivisor);
        List<int> selected = new List<int>(colorCount);
        for (int i = 0; i < rankedIndices.Count && selected.Count < colorCount; i++)
        {
            int paletteIndex = rankedIndices[i];
            if (usage[paletteIndex] < minUsage && selected.Count >= 2)
            {
                continue;
            }

            selected.Add(paletteIndex);
        }

        selected.Sort();

        Color32[] result = new Color32[selected.Count];
        for (int i = 0; i < selected.Count; i++)
        {
            result[i] = sourcePalette[selected[i]];
        }

        return result;
    }

    public static BucketSpawn[] GenerateBucketSpawns(byte[] cells, int paletteLength)
    {
        int[] counts = new int[paletteLength];

        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex > 0 && colorIndex <= paletteLength)
            {
                counts[colorIndex - 1]++;
            }
        }

        int spawnCount = 0;
        for (int i = 0; i < paletteLength; i++)
        {
            if (counts[i] > 0)
            {
                spawnCount++;
            }
        }

        BucketSpawn[] spawns = new BucketSpawn[spawnCount];
        int spawnIndex = 0;
        for (int i = 0; i < paletteLength; i++)
        {
            if (counts[i] <= 0)
            {
                continue;
            }

            spawns[spawnIndex++] = new BucketSpawn
            {
                colorIndex = (byte)(i + 1),
                count = 1
            };
        }

        return spawns;
    }

    public struct BucketQueueItem
    {
        public byte colorIndex;
        public int capacity;
    }

    /// <summary>
    /// Splits total grains across buckets as equally as possible.
    /// The first (totalGrains % bucketCount) buckets receive one extra grain
    /// so the sum matches totalGrains exactly and no bucket stays partially filled.
    /// </summary>
    public static int[] DistributeBucketCapacities(int totalGrains, int bucketCount, int maxGrainsPerBucket = 0)
    {
        bucketCount = Mathf.Max(0, bucketCount);
        int[] capacities = new int[bucketCount];

        if (bucketCount == 0)
        {
            return capacities;
        }

        if (maxGrainsPerBucket <= 0)
        {
            int baseCapacity = totalGrains / bucketCount;
            int extraBuckets = totalGrains % bucketCount;

            for (int i = 0; i < bucketCount; i++)
            {
                int capacity = baseCapacity + (i < extraBuckets ? 1 : 0);
                capacities[i] = capacity > 0 ? capacity : 1;
            }

            return capacities;
        }

        int remaining = totalGrains;
        int lastFilledIndex = -1;
        for (int i = 0; i < bucketCount && remaining > 0; i++)
        {
            int bucketsLeft = bucketCount - i;
            int share = Mathf.CeilToInt((float)remaining / bucketsLeft);
            int capacity = Mathf.Min(share, maxGrainsPerBucket);
            capacities[i] = Mathf.Max(1, capacity);
            remaining -= capacities[i];
            lastFilledIndex = i;
        }

        if (remaining > 0 && lastFilledIndex >= 0)
        {
            capacities[lastFilledIndex] += remaining;
        }

        return capacities;
    }

    public static int ResolveEffectiveMaxGrainsPerBucket(int maxGrainsPerBucket, int totalGrains)
    {
        if (maxGrainsPerBucket > 0)
        {
            return maxGrainsPerBucket;
        }

        if (totalGrains <= 0)
        {
            return 1;
        }

        return Mathf.Max(1, Mathf.CeilToInt((float)totalGrains / LevelData.BucketLayoutColumns));
    }

    public static BucketQueueItem[] GenerateLevelBucketQueue(
        byte[] cells,
        int width,
        int height,
        int grainsPerLevelCell = 1,
        int maxGrainsPerBucket = 0)
    {
        grainsPerLevelCell = Mathf.Max(1, grainsPerLevelCell);
        if (cells == null || cells.Length == 0 || width <= 0 || height <= 0)
        {
            return new BucketQueueItem[0];
        }

        int[] remaining = new int[256];
        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex > 0)
            {
                remaining[colorIndex] += grainsPerLevelCell;
            }
        }

        List<byte> colorOrder = new List<byte>();
        bool[] seen = new bool[256];
        for (int x = 0; x < width; x++)
        {
            for (int y = height - 1; y >= 0; y--)
            {
                byte colorIndex = cells[y * width + x];
                if (colorIndex > 0 && !seen[colorIndex])
                {
                    seen[colorIndex] = true;
                    colorOrder.Add(colorIndex);
                }
            }
        }

        for (int colorIndex = 1; colorIndex < remaining.Length; colorIndex++)
        {
            if (remaining[colorIndex] > 0 && !seen[colorIndex])
            {
                colorOrder.Add((byte)colorIndex);
            }
        }

        List<BucketQueueItem> buckets = new List<BucketQueueItem>();
        for (int i = 0; i < colorOrder.Count; i++)
        {
            byte colorIndex = colorOrder[i];
            int grainsRemaining = remaining[colorIndex];
            if (grainsRemaining <= 0)
            {
                continue;
            }

            int effectiveMax = ResolveEffectiveMaxGrainsPerBucket(maxGrainsPerBucket, grainsRemaining);
            int bucketCount = Mathf.Max(1, Mathf.CeilToInt((float)grainsRemaining / effectiveMax));
            int[] capacities = DistributeBucketCapacities(grainsRemaining, bucketCount, 0);
            for (int bucketIndex = 0; bucketIndex < capacities.Length; bucketIndex++)
            {
                int capacity = capacities[bucketIndex];
                if (capacity <= 0)
                {
                    continue;
                }

                buckets.Add(new BucketQueueItem
                {
                    colorIndex = colorIndex,
                    capacity = capacity
                });
            }
        }

        return buckets.ToArray();
    }

    [System.Obsolete("Use GenerateLevelBucketQueue with maxGrainsPerBucket")]
    public static BucketQueueItem[] GenerateDrainOrderBuckets(byte[] cells, int width, int height, int maxBucketCapacity)
    {
        return GenerateLevelBucketQueue(cells, width, height, 1, maxBucketCapacity);
    }

    public static Texture2D BuildPreviewTexture(int gridWidth, int gridHeight, byte[] cells, Color32[] palette, int pixelsPerCell)
    {
        pixelsPerCell = Mathf.Max(1, pixelsPerCell);
        int textureWidth = gridWidth * pixelsPerCell;
        int textureHeight = gridHeight * pixelsPerCell;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color32[] pixels = new Color32[textureWidth * textureHeight];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                byte colorIndex = cells[y * gridWidth + x];
                Color32 baseColor = colorIndex > 0 && colorIndex <= palette.Length
                    ? palette[colorIndex - 1]
                    : new Color32(0, 0, 0, 0);
                Color32 grainColor = ApplyGrain(baseColor, x, y);

                for (int py = 0; py < pixelsPerCell; py++)
                {
                    for (int px = 0; px < pixelsPerCell; px++)
                    {
                        int targetX = x * pixelsPerCell + px;
                        int displayRow = gridHeight - 1 - y;
                        int targetY = displayRow * pixelsPerCell + py;
                        pixels[targetY * textureWidth + targetX] = grainColor;
                    }
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private struct ColorBucket
    {
        public int count;
        public long sumR;
        public long sumG;
        public long sumB;
    }

    private static Dictionary<int, ColorBucket> CollectBuckets(Color32[] pixels, int bucketShift, byte alphaThreshold)
    {
        Dictionary<int, ColorBucket> buckets = new Dictionary<int, ColorBucket>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.a < alphaThreshold)
            {
                continue;
            }

            AddPixelToBucket(buckets, pixel, bucketShift);
        }

        return buckets;
    }

    private static Dictionary<int, ColorBucket> CollectBucketsFromMasked(
        Color32[] pixels,
        bool[] isPadding,
        byte alphaThreshold,
        int bucketShift)
    {
        Dictionary<int, ColorBucket> buckets = new Dictionary<int, ColorBucket>();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isPadding != null && isPadding[i])
            {
                continue;
            }

            Color32 pixel = pixels[i];
            if (pixel.a < alphaThreshold)
            {
                continue;
            }

            AddPixelToBucket(buckets, pixel, bucketShift);
        }

        return buckets;
    }

    private static void AddPixelToBucket(Dictionary<int, ColorBucket> buckets, Color32 pixel, int bucketShift)
    {
        int key = MakeBucketKey(pixel, bucketShift);
        ColorBucket bucket;
        if (!buckets.TryGetValue(key, out bucket))
        {
            bucket = new ColorBucket();
        }

        bucket.count++;
        bucket.sumR += pixel.r;
        bucket.sumG += pixel.g;
        bucket.sumB += pixel.b;
        buckets[key] = bucket;
    }

    private static Color32 BucketToColor(ColorBucket bucket)
    {
        return new Color32(
            (byte)(bucket.sumR / bucket.count),
            (byte)(bucket.sumG / bucket.count),
            (byte)(bucket.sumB / bucket.count),
            255);
    }

    private static bool IsSimilarToAny(Color32 candidate, List<Color32> palette)
    {
        for (int i = 0; i < palette.Count; i++)
        {
            if (ColorDistance(candidate, palette[i]) < PaletteMergeDistanceSq)
            {
                return true;
            }
        }

        return false;
    }

    private static int MakeExactColorKey(Color32 color)
    {
        return (color.r << 16) | (color.g << 8) | color.b;
    }

    private static int MakeBucketKey(Color32 color, int bucketShift)
    {
        int r = color.r >> bucketShift;
        int g = color.g >> bucketShift;
        int b = color.b >> bucketShift;
        return (r << 12) | (g << 6) | b;
    }

    private static int CompareBucketsByCount(KeyValuePair<int, ColorBucket> a, KeyValuePair<int, ColorBucket> b)
    {
        return b.Value.count.CompareTo(a.Value.count);
    }

    private static Vector3[] InitializeCenters(Color32[] pixels, int colorCount)
    {
        Vector3[] centers = new Vector3[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            Color32 sample = pixels[UnityEngine.Random.Range(0, pixels.Length)];
            centers[i] = new Vector3(sample.r, sample.g, sample.b);
        }

        return centers;
    }

    private static bool AssignPixels(Color32[] pixels, Vector3[] centers, int[] assignments)
    {
        bool changed = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            Vector3 pixel = new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);

            for (int c = 0; c < centers.Length; c++)
            {
                float distance = Vector3.SqrMagnitude(pixel - centers[c]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = c;
                }
            }

            if (assignments[i] != nearest)
            {
                assignments[i] = nearest;
                changed = true;
            }
        }

        return changed;
    }

    private static void RecalculateCenters(Color32[] pixels, Vector3[] centers, int[] assignments, int colorCount)
    {
        Vector3[] sums = new Vector3[colorCount];
        int[] counts = new int[colorCount];

        for (int i = 0; i < pixels.Length; i++)
        {
            int cluster = assignments[i];
            sums[cluster] += new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);
            counts[cluster]++;
        }

        for (int i = 0; i < colorCount; i++)
        {
            if (counts[i] == 0)
            {
                Color32 sample = pixels[UnityEngine.Random.Range(0, pixels.Length)];
                centers[i] = new Vector3(sample.r, sample.g, sample.b);
            }
            else
            {
                centers[i] = sums[i] / counts[i];
            }
        }
    }

    private static float ColorDistance(Color32 a, Color32 b)
    {
        Vector3 labA = RgbToLab(a);
        Vector3 labB = RgbToLab(b);
        float dL = labA.x - labB.x;
        float da = labA.y - labB.y;
        float db = labA.z - labB.z;
        return dL * dL + da * da + db * db;
    }

    private static Vector3 RgbToLab(Color32 color)
    {
        float r = SrgbToLinear(color.r / 255f);
        float g = SrgbToLinear(color.g / 255f);
        float b = SrgbToLinear(color.b / 255f);

        float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
        float y = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
        float z = r * 0.0193339f + g * 0.1191920f + b * 0.9503041f;

        x /= 0.95047f;
        y /= 1.00000f;
        z /= 1.08883f;

        float fx = x > 0.008856f ? Mathf.Pow(x, 1f / 3f) : (7.787f * x) + (16f / 116f);
        float fy = y > 0.008856f ? Mathf.Pow(y, 1f / 3f) : (7.787f * y) + (16f / 116f);
        float fz = z > 0.008856f ? Mathf.Pow(z, 1f / 3f) : (7.787f * z) + (16f / 116f);

        return new Vector3((116f * fy) - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    private static float SrgbToLinear(float channel)
    {
        return channel > 0.04045f
            ? Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f)
            : channel / 12.92f;
    }

    private static Color32 ApplyGrain(Color32 baseColor, int x, int y)
    {
        if (baseColor.a == 0) return baseColor;
        int noise = Hash(x, y) % 31 - 15;
        return new Color32(
            (byte)Mathf.Clamp(baseColor.r + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.g + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.b + noise, 0, 255),
            baseColor.a);
    }

    private static int Hash(int x, int y)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + 92837111;
            return hash & int.MaxValue;
        }
    }
}
