using System;
using UnityEngine;

[Serializable]
public struct BucketSpawn
{
    public byte colorIndex;
    public int count;
}

[Serializable]
public struct TileSourceData
{
    public string assetPath;
    public string spriteName;
    public int tileX;
    public int tileY;
    public int colorCount;
    public bool autoColorCount;
    public bool autoBackgroundColor;
    public Color32 backgroundColor;
    public string targetPaletteAssetPath;
    public Color32[] tilePalette;
}

[CreateAssetMenu(fileName = "LevelData", menuName = "Sand/Level Data")]
public class LevelData : ScriptableObject
{
    public const int BucketLayoutColumns = 5;
    public const int BucketLayoutRows = 5;
    public const int BucketLayoutCellCount = BucketLayoutColumns * BucketLayoutRows;
    public const byte BucketLayoutEmpty = 0;
    public const byte BucketLayoutConveyorSlot = 254;
    public const byte BucketLayoutWall = 255;
    public const byte DefaultConveyorBucketCount = 3;

    public const int MinGridDimension = 8;
    public const int MaxGridDimension = 64;
    public const int MinTileCellDimension = 4;
    public const int MaxTileCellDimension = 64;

    public int gridWidth = 32;
    public int gridHeight = 32;
    public Color32[] palette = new Color32[0];
    public byte[] cells = new byte[0];
    public int subGrainsPerAxis = 1;
    public BucketSpawn[] buckets = new BucketSpawn[0];
    public byte[] bucketLayout = new byte[BucketLayoutCellCount];
    public byte[] conveyorDirections = new byte[BucketLayoutCellCount];
    public byte[] conveyorBucketCounts = new byte[BucketLayoutCellCount];
    public Texture2D previewTexture;
    [Tooltip("Optional per-level override for bucket layout generation (uses segment layout fields).")]
    public LevelSegmentRules bucketSettingsOverride;

    public bool usesTiles;
    public bool usesUnitySpriteSheet;
    public string spriteSheetAssetPath;
    public int tilesX = 1;
    public int tilesY = 1;
    public int tileCellWidth = 16;
    public int tileCellHeight = 16;
    public TileSourceData[] tileSources = new TileSourceData[0];

    public int GetGrainsPerLevelCell()
    {
        int sub = Mathf.Max(1, subGrainsPerAxis);
        return sub * sub;
    }

    public int GetColorCount()
    {
        return palette != null ? palette.Length : 0;
    }

    public Color32 GetPaletteColor(byte colorIndex)
    {
        if (palette == null || colorIndex == 0 || colorIndex > palette.Length)
        {
            return new Color32(0, 0, 0, 0);
        }

        return palette[colorIndex - 1];
    }

    public int CountLevelCellsForColor(byte colorIndex)
    {
        if (cells == null || colorIndex == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == colorIndex)
            {
                count++;
            }
        }

        return count;
    }

    public int CountGrainsForColor(byte colorIndex)
    {
        return CountLevelCellsForColor(colorIndex) * GetGrainsPerLevelCell();
    }

    public int CountTotalLevelCells()
    {
        if (cells == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] > 0)
            {
                count++;
            }
        }

        return count;
    }

    public int CountTotalGrains()
    {
        return CountTotalLevelCells() * GetGrainsPerLevelCell();
    }

    public int GetTotalBucketCount()
    {
        return GetDrainOrderBucketCount();
    }

    public int GetDrainOrderBucketCount()
    {
        return GetDrainOrderBucketCount(0);
    }

    public int GetDrainOrderBucketCount(int maxGrainsPerBucket)
    {
        if (cells == null || cells.Length == 0)
        {
            return 0;
        }

        return ColorQuantizer.GenerateLevelBucketQueue(
            cells,
            gridWidth,
            gridHeight,
            GetGrainsPerLevelCell(),
            maxGrainsPerBucket).Length;
    }

    public void EnsureBucketLayout()
    {
        if (bucketLayout == null || bucketLayout.Length != BucketLayoutCellCount)
        {
            bucketLayout = new byte[BucketLayoutCellCount];
        }
    }

    public bool HasBucketLayout()
    {
        if (bucketLayout == null || bucketLayout.Length != BucketLayoutCellCount)
        {
            return false;
        }

        for (int i = 0; i < bucketLayout.Length; i++)
        {
            if (IsBucketLayoutBucket(bucketLayout[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsBucketLayoutWall(byte cell)
    {
        return cell == BucketLayoutWall;
    }

    public static bool IsBucketLayoutConveyorSlot(byte cell)
    {
        return cell == BucketLayoutConveyorSlot;
    }

    public static bool IsBucketLayoutBucket(byte cell)
    {
        return cell > BucketLayoutEmpty && cell < BucketLayoutConveyorSlot;
    }

    public void EnsureConveyorMetadata()
    {
        if (conveyorDirections == null || conveyorDirections.Length != BucketLayoutCellCount)
        {
            conveyorDirections = new byte[BucketLayoutCellCount];
        }

        if (conveyorBucketCounts == null || conveyorBucketCounts.Length != BucketLayoutCellCount)
        {
            conveyorBucketCounts = new byte[BucketLayoutCellCount];
        }
    }

    public ConveyorDirection GetConveyorDirection(int layoutIndex)
    {
        EnsureConveyorMetadata();
        if (layoutIndex < 0 || layoutIndex >= BucketLayoutCellCount)
        {
            return ConveyorDirection.Front;
        }

        byte raw = conveyorDirections[layoutIndex];
        if (raw > (byte)ConveyorDirection.Right)
        {
            return ConveyorDirection.Front;
        }

        return (ConveyorDirection)raw;
    }

    public ConveyorDirection GetConveyorDirectionAt(int x, int y)
    {
        if (x < 0 || x >= BucketLayoutColumns || y < 0 || y >= BucketLayoutRows)
        {
            return ConveyorDirection.Front;
        }

        return GetConveyorDirection(y * BucketLayoutColumns + x);
    }

    public void SetConveyorDirection(int layoutIndex, ConveyorDirection direction)
    {
        EnsureConveyorMetadata();
        if (layoutIndex < 0 || layoutIndex >= BucketLayoutCellCount)
        {
            return;
        }

        conveyorDirections[layoutIndex] = (byte)direction;
    }

    public int GetConveyorBucketCount(int layoutIndex)
    {
        EnsureConveyorMetadata();
        if (layoutIndex < 0 || layoutIndex >= BucketLayoutCellCount)
        {
            return DefaultConveyorBucketCount;
        }

        byte count = conveyorBucketCounts[layoutIndex];
        return count > 0 ? count : DefaultConveyorBucketCount;
    }

    public void SetConveyorBucketCount(int layoutIndex, int count)
    {
        EnsureConveyorMetadata();
        if (layoutIndex < 0 || layoutIndex >= BucketLayoutCellCount)
        {
            return;
        }

        conveyorBucketCounts[layoutIndex] = (byte)Mathf.Clamp(count, 1, 99);
    }

    public bool HasConveyorSlots()
    {
        EnsureBucketLayout();
        for (int i = 0; i < bucketLayout.Length; i++)
        {
            if (IsBucketLayoutConveyorSlot(bucketLayout[i]))
            {
                return true;
            }
        }

        return false;
    }

    public int CountBucketsInLayout()
    {
        if (bucketLayout == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bucketLayout.Length; i++)
        {
            if (IsBucketLayoutBucket(bucketLayout[i]))
            {
                count++;
            }
        }

        return count;
    }

    public int CountBucketsInLayoutForColor(byte colorIndex)
    {
        if (bucketLayout == null || colorIndex == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < bucketLayout.Length; i++)
        {
            if (bucketLayout[i] == colorIndex)
            {
                count++;
            }
        }

        return count;
    }

    public int[] GetLayoutBucketCapacitiesForColor(byte colorIndex)
    {
        return ColorQuantizer.DistributeBucketCapacities(
            CountGrainsForColor(colorIndex),
            CountBucketsInLayoutForColor(colorIndex));
    }

    public byte GetBucketLayoutCell(int x, int y)
    {
        EnsureBucketLayout();
        if (x < 0 || x >= BucketLayoutColumns || y < 0 || y >= BucketLayoutRows)
        {
            return 0;
        }

        return bucketLayout[y * BucketLayoutColumns + x];
    }

    public void SetBucketLayoutCell(int x, int y, byte cellValue)
    {
        EnsureBucketLayout();
        if (x < 0 || x >= BucketLayoutColumns || y < 0 || y >= BucketLayoutRows)
        {
            return;
        }

        bucketLayout[y * BucketLayoutColumns + x] = cellValue;
    }

    public void SetBucketLayoutWall(int x, int y, bool hasWall)
    {
        SetBucketLayoutCell(x, y, hasWall ? BucketLayoutWall : BucketLayoutEmpty);
    }

    public byte[] GetColorsUsedInLevel()
    {
        if (cells == null || cells.Length == 0)
        {
            return new byte[0];
        }

        bool[] seen = new bool[256];
        int count = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex > 0 && !seen[colorIndex])
            {
                seen[colorIndex] = true;
                count++;
            }
        }

        byte[] colors = new byte[count];
        int writeIndex = 0;
        for (int colorIndex = 1; colorIndex < seen.Length; colorIndex++)
        {
            if (seen[colorIndex])
            {
                colors[writeIndex++] = (byte)colorIndex;
            }
        }

        return colors;
    }

    public byte[] GetColorsUsedInLevelByFrequency()
    {
        return GetUsedColorIndicesByFrequency(cells);
    }

    /// <summary>
    /// Индексы цветов из palette (1-based), которые есть в рисунке, по убыванию частоты.
    /// </summary>
    public byte[] GetUsedPaletteColorsByFrequency()
    {
        if (cells == null || palette == null || palette.Length == 0)
        {
            return new byte[0];
        }

        byte[] used = GetUsedColorIndicesByFrequency(cells);
        int paletteCount = palette.Length;
        int validCount = 0;
        for (int i = 0; i < used.Length; i++)
        {
            if (used[i] > 0 && used[i] <= paletteCount)
            {
                used[validCount++] = used[i];
            }
        }

        if (validCount == 0)
        {
            return new byte[0];
        }

        if (validCount == used.Length)
        {
            return used;
        }

        byte[] trimmed = new byte[validCount];
        for (int i = 0; i < validCount; i++)
        {
            trimmed[i] = used[i];
        }

        return trimmed;
    }

    public static byte[] FilterUsedPaletteColors(byte[] cells, int paletteLength)
    {
        if (cells == null || paletteLength <= 0)
        {
            return new byte[0];
        }

        byte[] used = GetUsedColorIndicesByFrequency(cells);
        int validCount = 0;
        for (int i = 0; i < used.Length; i++)
        {
            if (used[i] > 0 && used[i] <= paletteLength)
            {
                used[validCount++] = used[i];
            }
        }

        if (validCount == 0)
        {
            return new byte[0];
        }

        if (validCount == used.Length)
        {
            return used;
        }

        byte[] trimmed = new byte[validCount];
        for (int i = 0; i < validCount; i++)
        {
            trimmed[i] = used[i];
        }

        return trimmed;
    }

    public static byte[] GetUsedColorIndicesByFrequency(byte[] cells)
    {
        if (cells == null || cells.Length == 0)
        {
            return new byte[0];
        }

        int[] counts = new int[256];
        bool[] seen = new bool[256];
        int uniqueCount = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            byte colorIndex = cells[i];
            if (colorIndex == 0)
            {
                continue;
            }

            counts[colorIndex]++;
            if (!seen[colorIndex])
            {
                seen[colorIndex] = true;
                uniqueCount++;
            }
        }

        byte[] colors = new byte[uniqueCount];
        int writeIndex = 0;
        for (int colorIndex = 1; colorIndex < counts.Length; colorIndex++)
        {
            if (seen[colorIndex])
            {
                colors[writeIndex++] = (byte)colorIndex;
            }
        }

        if (colors.Length <= 1)
        {
            return colors;
        }

        System.Array.Sort(colors, (byte a, byte b) =>
        {
            int countCompare = counts[b].CompareTo(counts[a]);
            if (countCompare != 0)
            {
                return countCompare;
            }

            return a.CompareTo(b);
        });

        return colors;
    }

    public void AutoFillBucketLayoutFromCells()
    {
        EnsureBucketLayout();
        for (int i = 0; i < bucketLayout.Length; i++)
        {
            bucketLayout[i] = BucketLayoutEmpty;
        }

        byte[] colors = GetUsedPaletteColorsByFrequency();
        for (int i = 0; i < colors.Length && i < BucketLayoutCellCount; i++)
        {
            int x = i % BucketLayoutColumns;
            int y = i / BucketLayoutColumns;
            SetBucketLayoutCell(x, y, colors[i]);
        }

        buckets = BuildBucketSpawnsFromLayout();
    }

    public BucketSpawn[] BuildBucketSpawnsFromLayout()
    {
        byte[] colors = GetUsedPaletteColorsByFrequency();
        if (colors.Length == 0)
        {
            return new BucketSpawn[0];
        }

        BucketSpawn[] spawns = new BucketSpawn[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            byte colorIndex = colors[i];
            spawns[i] = new BucketSpawn
            {
                colorIndex = colorIndex,
                count = Mathf.Max(1, CountBucketsInLayoutForColor(colorIndex))
            };
        }

        return spawns;
    }

    public bool IsColorUsedInLevel(byte colorIndex)
    {
        if (colorIndex == 0 || cells == null)
        {
            return false;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == colorIndex)
            {
                return true;
            }
        }

        return false;
    }
}
