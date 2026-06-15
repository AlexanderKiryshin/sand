using System;
using System.Collections.Generic;
using UnityEngine;

public static class ColorQuantizer
{
    public static Color32[] KMeansQuantize(Color32[] pixels, int colorCount, int maxIterations)
    {
        if (pixels == null || pixels.Length == 0 || colorCount <= 0)
        {
            return new Color32[0];
        }

        colorCount = Mathf.Clamp(colorCount, 1, 16);
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

    public static int FindNearestPaletteIndex(Color32 color, Color32[] palette)
    {
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

    public static BucketSpawn[] GenerateBucketSpawns(byte[] cells, int paletteLength, int bucketCapacity)
    {
        bucketCapacity = Mathf.Max(1, bucketCapacity);
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
                count = Mathf.CeilToInt(counts[i] / (float)bucketCapacity)
            };
        }

        return spawns;
    }

    public struct BucketQueueItem
    {
        public byte colorIndex;
        public int capacity;
    }

    public static BucketQueueItem[] GenerateLevelBucketQueue(
        byte[] cells,
        int width,
        int height,
        int maxBucketCapacity,
        int grainsPerLevelCell = 1)
    {
        maxBucketCapacity = Mathf.Max(1, maxBucketCapacity);
        grainsPerLevelCell = Mathf.Max(1, grainsPerLevelCell);
        if (cells == null || cells.Length == 0 || width <= 0 || height <= 0)
        {
            return new BucketQueueItem[0];
        }

        int[] remaining = new int[17];
        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex > 0 && colorIndex < remaining.Length)
            {
                remaining[colorIndex] += grainsPerLevelCell;
            }
        }

        List<byte> colorOrder = new List<byte>();
        bool[] seen = new bool[remaining.Length];
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
        bool anyRemaining = true;
        while (anyRemaining)
        {
            anyRemaining = false;
            for (int i = 0; i < colorOrder.Count; i++)
            {
                byte colorIndex = colorOrder[i];
                if (remaining[colorIndex] <= 0)
                {
                    continue;
                }

                anyRemaining = true;
                int take = Mathf.Min(remaining[colorIndex], maxBucketCapacity);
                buckets.Add(new BucketQueueItem
                {
                    colorIndex = colorIndex,
                    capacity = take
                });
                remaining[colorIndex] -= take;
            }
        }

        return buckets.ToArray();
    }

    [System.Obsolete("Use GenerateLevelBucketQueue")]
    public static BucketQueueItem[] GenerateDrainOrderBuckets(byte[] cells, int width, int height, int maxBucketCapacity)
    {
        return GenerateLevelBucketQueue(cells, width, height, maxBucketCapacity);
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
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    private static Color32 ApplyGrain(Color32 baseColor, int x, int y)
    {
        int noise = Hash(x, y) % 31 - 15;
        return new Color32(
            (byte)Mathf.Clamp(baseColor.r + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.g + noise, 0, 255),
            (byte)Mathf.Clamp(baseColor.b + noise, 0, 255),
            255);
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
