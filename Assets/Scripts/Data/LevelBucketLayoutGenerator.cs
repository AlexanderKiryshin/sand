using System.Collections.Generic;
using UnityEngine;

public static class LevelBucketLayoutGenerator
{
    public static void Apply(LevelData level, LevelSegmentRules settings)
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
            case BucketLayoutFillMode.RandomComponents:
                ApplyRandomComponents(level, settings);
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

        LevelSegmentRules settings = level.bucketSettingsOverride;
        if (settings == null)
        {
            settings = segmentDatabase != null
                ? segmentDatabase.GetSegmentForLevelIndex(levelIndex)
                : null;
        }

        Apply(level, settings);
    }

    private static void ApplyTemplateAsIs(LevelData level, LevelSegmentRules settings)
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

    private static void ApplyTemplateSlots(LevelData level, LevelSegmentRules settings)
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

    private static void ApplyFrequencyByRow(LevelData level, LevelSegmentRules settings)
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

    private static void ApplyRandomComponents(LevelData level, LevelSegmentRules settings)
    {
        ClearStructuralLayout(level);

        BucketLayoutComponent[] components = settings.randomComponents;
        if (components == null || components.Length == 0)
        {
            return;
        }

        List<int> freeCells = new List<int>(LevelData.BucketLayoutCellCount);
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            freeCells.Add(i);
        }

        for (int i = 0; i < components.Length; i++)
        {
            BucketLayoutComponent component = components[i];
            if (component.type != BucketLayoutComponentType.Wall)
            {
                continue;
            }

            int placeCount = Mathf.Max(0, component.count);
            for (int n = 0; n < placeCount; n++)
            {
                if (freeCells.Count == 0)
                {
                    return;
                }

                int pick = UnityEngine.Random.Range(0, freeCells.Count);
                int layoutIndex = freeCells[pick];
                freeCells.RemoveAt(pick);
                level.bucketLayout[layoutIndex] = LevelData.BucketLayoutWall;
            }
        }

        for (int i = 0; i < components.Length; i++)
        {
            BucketLayoutComponent component = components[i];
            if (component.type != BucketLayoutComponentType.Conveyor)
            {
                continue;
            }

            int placeCount = Mathf.Max(0, component.count);
            ConveyorDirection direction = component.direction;
            if ((byte)direction > (byte)ConveyorDirection.Right)
            {
                direction = ConveyorDirection.Front;
            }

            int bucketCount = component.conveyorBucketCount > 0
                ? component.conveyorBucketCount
                : LevelData.DefaultConveyorBucketCount;

            for (int n = 0; n < placeCount; n++)
            {
                List<int> candidates = CollectConveyorCandidates(level, freeCells, direction);
                if (candidates.Count == 0)
                {
                    break;
                }

                int pick = UnityEngine.Random.Range(0, candidates.Count);
                int layoutIndex = candidates[pick];
                freeCells.Remove(layoutIndex);
                level.bucketLayout[layoutIndex] = LevelData.BucketLayoutConveyorSlot;
                level.SetConveyorDirection(layoutIndex, direction);
                level.SetConveyorBucketCount(layoutIndex, bucketCount);
            }
        }
    }

    private static void ClearStructuralLayout(LevelData level)
    {
        level.EnsureBucketLayout();
        level.EnsureConveyorMetadata();
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            level.bucketLayout[i] = LevelData.BucketLayoutEmpty;
            level.conveyorDirections[i] = 0;
            level.conveyorBucketCounts[i] = 0;
        }
    }

    private static List<int> CollectConveyorCandidates(
        LevelData level,
        List<int> freeCells,
        ConveyorDirection direction)
    {
        List<int> candidates = new List<int>(freeCells.Count);
        for (int i = 0; i < freeCells.Count; i++)
        {
            int layoutIndex = freeCells[i];
            int targetX;
            int targetY;
            if (!ConveyorDirectionUtility.TryGetTargetCell(layoutIndex, direction, out targetX, out targetY))
            {
                continue;
            }

            byte targetCell = level.GetBucketLayoutCell(targetX, targetY);
            if (LevelData.IsBucketLayoutWall(targetCell) || LevelData.IsBucketLayoutConveyorSlot(targetCell))
            {
                continue;
            }

            candidates.Add(layoutIndex);
        }

        return candidates;
    }
}
