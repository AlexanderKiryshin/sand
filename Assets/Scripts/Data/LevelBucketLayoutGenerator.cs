using UnityEngine;

public static class LevelBucketLayoutGenerator
{
    public static void Apply(LevelData level, LevelBucketSettings settings)
    {
        if (level == null)
        {
            return;
        }

        level.EnsureBucketLayout();
        if (settings == null)
        {
            level.AutoFillBucketLayoutFromCells();
            return;
        }

        settings.EnsureTemplate();
        switch (settings.fillMode)
        {
            case BucketLayoutFillMode.UseTemplateAsIs:
                ApplyTemplateAsIs(level, settings);
                break;
            case BucketLayoutFillMode.TemplateSlots:
                ApplyTemplateSlots(level, settings);
                break;
            default:
                ApplyFrequencyByRow(level, settings);
                break;
        }

        level.buckets = level.BuildBucketSpawnsFromLayout();
    }

    public static void ApplyForLevelIndex(LevelData level, int levelIndex, LevelSegmentDatabase segmentDatabase)
    {
        if (level == null)
        {
            return;
        }

        LevelBucketSettings settings = level.bucketSettingsOverride;
        if (settings == null)
        {
            LevelSegmentRules segment = segmentDatabase != null
                ? segmentDatabase.GetSegmentForLevelIndex(levelIndex)
                : null;
            settings = segment != null ? segment.bucketSettings : null;
        }

        Apply(level, settings);
    }

    private static void ApplyTemplateAsIs(LevelData level, LevelBucketSettings settings)
    {
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            byte templateCell = settings.layoutTemplate[i];
            if (LevelData.IsBucketLayoutWall(templateCell))
            {
                level.bucketLayout[i] = LevelData.BucketLayoutWall;
                continue;
            }

            if (LevelData.IsBucketLayoutConveyorSlot(templateCell))
            {
                level.bucketLayout[i] = LevelData.BucketLayoutConveyorSlot;
                level.SetConveyorDirection(i, (ConveyorDirection)settings.conveyorDirectionTemplate[i]);
                level.SetConveyorBucketCount(i, settings.conveyorBucketCountTemplate[i]);
                continue;
            }

            if (LevelData.IsBucketLayoutBucket(templateCell) && level.IsColorUsedInLevel(templateCell))
            {
                level.bucketLayout[i] = templateCell;
                continue;
            }

            level.bucketLayout[i] = LevelData.BucketLayoutEmpty;
        }
    }

    private static void ApplyTemplateSlots(LevelData level, LevelBucketSettings settings)
    {
        byte[] colors = level.GetUsedPaletteColorsByFrequency();
        int colorCursor = 0;

        for (int y = 0; y < LevelData.BucketLayoutRows; y++)
        {
            for (int x = 0; x < LevelData.BucketLayoutColumns; x++)
            {
                int index = y * LevelData.BucketLayoutColumns + x;
                byte templateCell = settings.layoutTemplate[index];

                if (LevelData.IsBucketLayoutWall(templateCell))
                {
                    level.bucketLayout[index] = LevelData.BucketLayoutWall;
                    continue;
                }

                if (LevelData.IsBucketLayoutConveyorSlot(templateCell))
                {
                    level.bucketLayout[index] = LevelData.BucketLayoutConveyorSlot;
                    level.SetConveyorDirection(index, settings.GetConveyorDirectionTemplate(x, y));
                    level.SetConveyorBucketCount(index, settings.GetConveyorBucketCountTemplate(x, y));
                    continue;
                }

                if (LevelData.IsBucketLayoutBucket(templateCell))
                {
                    level.bucketLayout[index] = level.IsColorUsedInLevel(templateCell)
                        ? templateCell
                        : LevelData.BucketLayoutEmpty;
                    continue;
                }

                if (colorCursor < colors.Length)
                {
                    level.bucketLayout[index] = colors[colorCursor++];
                }
                else
                {
                    level.bucketLayout[index] = LevelData.BucketLayoutEmpty;
                }
            }
        }
    }

    private static void ApplyFrequencyByRow(LevelData level, LevelBucketSettings settings)
    {
        level.AutoFillBucketLayoutFromCells();
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            if (LevelData.IsBucketLayoutWall(settings.layoutTemplate[i]))
            {
                level.bucketLayout[i] = LevelData.BucketLayoutWall;
            }
            else if (LevelData.IsBucketLayoutConveyorSlot(settings.layoutTemplate[i]))
            {
                level.bucketLayout[i] = LevelData.BucketLayoutConveyorSlot;
                level.SetConveyorDirection(i, settings.GetConveyorDirectionTemplate(
                    i % LevelData.BucketLayoutColumns,
                    i / LevelData.BucketLayoutColumns));
                level.SetConveyorBucketCount(i, settings.GetConveyorBucketCountTemplate(
                    i % LevelData.BucketLayoutColumns,
                    i / LevelData.BucketLayoutColumns));
            }
        }
    }
}
