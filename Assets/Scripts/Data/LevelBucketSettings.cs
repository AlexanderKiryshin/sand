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
    [Tooltip("0 = auto bucket slot, 255 = wall, 1..254 = fixed bucket color index.")]
    public byte[] layoutTemplate = new byte[LevelData.BucketLayoutCellCount];

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

        layoutTemplate[y * LevelData.BucketLayoutColumns + x] = value;
    }
}
