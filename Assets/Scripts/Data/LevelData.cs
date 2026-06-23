using System;
using UnityEngine;

[Serializable]
public struct BucketSpawn
{
    public byte colorIndex;
    public int count;
}

[CreateAssetMenu(fileName = "LevelData", menuName = "Sand/Level Data")]
public class LevelData : ScriptableObject
{
    public const int BucketLayoutColumns = 5;
    public const int BucketLayoutRows = 5;
    public const int BucketLayoutCellCount = BucketLayoutColumns * BucketLayoutRows;
    public const byte BucketLayoutEmpty = 0;
    public const byte BucketLayoutWall = 255;

    public int gridWidth = 32;
    public int gridHeight = 32;
    public Color32[] palette = new Color32[0];
    public byte[] cells = new byte[0];
    public int subGrainsPerAxis = 4;
    public BucketSpawn[] buckets = new BucketSpawn[0];
    public byte[] bucketLayout = new byte[BucketLayoutCellCount];
    public Texture2D previewTexture;

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
        if (HasBucketLayout())
        {
            return CountBucketsInLayout();
        }

        if (cells == null || cells.Length == 0)
        {
            return 0;
        }

        return ColorQuantizer.GenerateLevelBucketQueue(
            cells,
            gridWidth,
            gridHeight,
            GetGrainsPerLevelCell()).Length;
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

    public static bool IsBucketLayoutBucket(byte cell)
    {
        return cell > BucketLayoutEmpty && cell < BucketLayoutWall;
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
