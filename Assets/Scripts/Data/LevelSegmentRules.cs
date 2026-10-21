using System;
using UnityEngine;

public enum BucketLayoutFillMode
{
    FrequencyByRow,
    TemplateSlots,
    UseTemplateAsIs,
    RandomComponents
}

public enum BucketLayoutComponentType
{
    Wall,
    Conveyor
}

[Serializable]
public struct BucketLayoutComponent
{
    public BucketLayoutComponentType type;
    [Min(1)]
    public int count;
    public ConveyorDirection direction;
    [Min(1)]
    public int conveyorBucketCount;
}

[CreateAssetMenu(fileName = "LevelSegmentRules", menuName = "Sand/Level Segment Rules")]
public class LevelSegmentRules : ScriptableObject
{
    public string segmentId = "segment_01";
    public string displayName = "Segment 1";

    [Tooltip("First level index in LevelDatabase (0-based, inclusive).")]
    public int startLevelIndex;

    [Tooltip("How many levels this segment covers. 0 = until the next segment starts.")]
    public int levelCount;

    [Header("Image Defaults")]
    public SandColorPalette defaultPalette;
    public int defaultGridWidth = 32;
    public int defaultGridHeight = 32;
    public int defaultColorCount = 4;
    public bool defaultAutoColorCount = true;

    [Header("Bucket Layout")]
    [Tooltip("0 = auto bucket slot, 254 = conveyor slot, 255 = wall, 1..253 = fixed bucket color index.")]
    public byte[] layoutTemplate = new byte[LevelData.BucketLayoutCellCount];
    public byte[] conveyorDirectionTemplate = new byte[LevelData.BucketLayoutCellCount];
    public byte[] conveyorBucketCountTemplate = new byte[LevelData.BucketLayoutCellCount];

    public BucketLayoutFillMode fillMode = BucketLayoutFillMode.FrequencyByRow;

    [Tooltip("Used when fillMode is RandomComponents. Walls and conveyors are placed randomly each level load.")]
    public BucketLayoutComponent[] randomComponents = new BucketLayoutComponent[0];

    [Tooltip("0 = distribute all grains across buckets automatically. >0 = each bucket holds at most this many grains.")]
    [Min(0)]
    public int maxGrainsPerBucket;

    [Tooltip("Queue bucket colors in the order their full sand budgets become available from the bottom of the painting.")]
    public bool matchBucketOrderToSand;

    [Tooltip("Nonzero pins a verified campaign layout. Zero keeps a fresh random layout on each run.")]
    public int verifiedLayoutSeed;

    public bool ContainsLevelIndex(int levelIndex)
    {
        if (levelIndex < startLevelIndex)
        {
            return false;
        }

        if (levelCount > 0)
        {
            return levelIndex < startLevelIndex + levelCount;
        }

        return true;
    }

    public int GetEndLevelIndexExclusive(int nextSegmentStartIndex)
    {
        if (levelCount > 0)
        {
            return startLevelIndex + levelCount;
        }

        return nextSegmentStartIndex > startLevelIndex ? nextSegmentStartIndex : int.MaxValue;
    }

    public void EnsureTemplate()
    {
        if (layoutTemplate == null || layoutTemplate.Length != LevelData.BucketLayoutCellCount)
        {
            layoutTemplate = new byte[LevelData.BucketLayoutCellCount];
        }

        if (conveyorDirectionTemplate == null || conveyorDirectionTemplate.Length != LevelData.BucketLayoutCellCount)
        {
            conveyorDirectionTemplate = new byte[LevelData.BucketLayoutCellCount];
        }

        if (conveyorBucketCountTemplate == null || conveyorBucketCountTemplate.Length != LevelData.BucketLayoutCellCount)
        {
            conveyorBucketCountTemplate = new byte[LevelData.BucketLayoutCellCount];
        }

        if (randomComponents == null)
        {
            randomComponents = new BucketLayoutComponent[0];
        }
    }

    public byte GetTemplateCell(int x, int y)
    {
        EnsureTemplate();
        if (x < 0 || x >= LevelData.BucketLayoutColumns || y < 0 || y >= LevelData.BucketLayoutRows)
        {
            return LevelData.BucketLayoutEmpty;
        }

        return layoutTemplate[y * LevelData.BucketLayoutColumns + x];
    }

    public void SetTemplateCell(int x, int y, byte value)
    {
        EnsureTemplate();
        if (x < 0 || x >= LevelData.BucketLayoutColumns || y < 0 || y >= LevelData.BucketLayoutRows)
        {
            return;
        }

        int index = y * LevelData.BucketLayoutColumns + x;
        layoutTemplate[index] = value;
        if (!LevelData.IsBucketLayoutConveyorSlot(value))
        {
            conveyorDirectionTemplate[index] = 0;
            conveyorBucketCountTemplate[index] = 0;
        }
    }

    public ConveyorDirection GetConveyorDirectionTemplate(int x, int y)
    {
        EnsureTemplate();
        int index = y * LevelData.BucketLayoutColumns + x;
        byte raw = conveyorDirectionTemplate[index];
        if (raw > (byte)ConveyorDirection.Right)
        {
            return ConveyorDirection.Front;
        }

        return (ConveyorDirection)raw;
    }

    public void SetConveyorDirectionTemplate(int x, int y, ConveyorDirection direction)
    {
        EnsureTemplate();
        int index = y * LevelData.BucketLayoutColumns + x;
        conveyorDirectionTemplate[index] = (byte)direction;
    }

    public int GetConveyorBucketCountTemplate(int x, int y)
    {
        EnsureTemplate();
        int index = y * LevelData.BucketLayoutColumns + x;
        byte count = conveyorBucketCountTemplate[index];
        return count > 0 ? count : LevelData.DefaultConveyorBucketCount;
    }

    public void SetConveyorBucketCountTemplate(int x, int y, int count)
    {
        EnsureTemplate();
        int index = y * LevelData.BucketLayoutColumns + x;
        conveyorBucketCountTemplate[index] = (byte)Mathf.Clamp(count, 1, 99);
    }
}
