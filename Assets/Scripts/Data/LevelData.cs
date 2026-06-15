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
    public int gridWidth = 32;
    public int gridHeight = 32;
    public Color32[] palette = new Color32[0];
    public byte[] cells = new byte[0];
    public int bucketCapacity = 64;
    public int subGrainsPerAxis = 4;
    public BucketSpawn[] buckets = new BucketSpawn[0];
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
        if (cells == null || cells.Length == 0)
        {
            return 0;
        }

        return ColorQuantizer.GenerateLevelBucketQueue(
            cells,
            gridWidth,
            gridHeight,
            bucketCapacity,
            GetGrainsPerLevelCell()).Length;
    }
}
