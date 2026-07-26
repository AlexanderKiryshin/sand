using UnityEngine;

public enum BucketLayoutFillMode
{
    FrequencyByRow,
    TemplateSlots,
    UseTemplateAsIs
}

[CreateAssetMenu(fileName = "LevelBucketSettings", menuName = "Sand/Level Bucket Settings")]
public class LevelBucketSettings : ScriptableObject
{
    [Tooltip("0 = auto bucket slot, 254 = conveyor slot, 255 = wall, 1..253 = fixed bucket color index.")]
    public byte[] layoutTemplate = new byte[LevelData.BucketLayoutCellCount];
    public byte[] conveyorDirectionTemplate = new byte[LevelData.BucketLayoutCellCount];
    public byte[] conveyorBucketCountTemplate = new byte[LevelData.BucketLayoutCellCount];

    public BucketLayoutFillMode fillMode = BucketLayoutFillMode.FrequencyByRow;

    [Tooltip("0 = distribute all grains across buckets automatically. >0 = each bucket holds at most this many grains.")]
    [Min(0)]
    public int maxGrainsPerBucket;

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
