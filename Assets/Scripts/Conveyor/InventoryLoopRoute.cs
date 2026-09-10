using System;
using System.Linq;
using UnityEngine;

public static class InventoryLoopRoute
{
    // Compact two-row rings (not the full 5-row perimeter). Variants keep layouts distinct.
    private static readonly int[][] Variants = CreateVariants();

    private static int[][] CreateVariants()
    {
        var variants = new System.Collections.Generic.List<int[]>();
        for (int row = 0; row < 4; row++)
            for (int width = 3; width <= 5; width++)
                for (int left = 0; left <= 5 - width; left++)
                {
                    var ring = new int[width * 2];
                    for (int x = 0; x < width; x++)
                    {
                        ring[x] = row * 5 + left + x;
                        ring[width + x] = (row + 1) * 5 + left + width - 1 - x;
                    }
                    variants.Add(ring);
                }
        return variants.ToArray();
    }

    public static int[] Cells { get; private set; } = Variants[0];

    public static void Bind(LevelData level)
    {
        if (level == null || !level.loopInventory) return;
        Cells = ResolveVariantFromLayout(level) ?? Variants[VariantIndex(level)];
    }

    public static int Next(int cell)
    {
        int i = Array.IndexOf(Cells, cell);
        return i < 0 ? cell : Cells[(i + 1) % Cells.Length];
    }

    public static void ApplyStructure(LevelData level)
    {
        ApplyStructure(level, CampaignProgression.LoopLevel);
    }

    public static void ApplyStructure(LevelData level, int levelNumber)
    {
        Cells = Variants[VariantIndex(level, levelNumber)];
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            level.bucketLayout[i] = Array.IndexOf(Cells, i) >= 0 ? (byte)0 : LevelData.BucketLayoutWall;
            level.conveyorDirections[i] = 0;
            level.conveyorBucketCounts[i] = 0;
        }

        // Feeders sit under the moving ring (inner/blocked cells), never on the path.
        PlaceInnerFeeder(level, Cells, 0, ConveyorDirection.Front, 3);
        PlaceInnerFeeder(level, Cells, Cells.Length / 2, ConveyorDirection.Front, 3);
    }

    private static int VariantIndex(LevelData level, int levelNumber = 0)
    {
        int number = levelNumber > 0 ? levelNumber : EstimateNumber(level);
        return (int)(unchecked((uint)(number * 13 + level.campaignSeed * 7)) % (uint)Variants.Length);
    }

    private static int EstimateNumber(LevelData level)
    {
        if (level?.bucketSettingsOverride != null)
        {
            string id = level.bucketSettingsOverride.segmentId;
            if (!string.IsNullOrEmpty(id))
            {
                int underscore = id.LastIndexOf('_');
                if (underscore >= 0 && int.TryParse(id.Substring(underscore + 1), out int number))
                    return number;
            }
            return level.bucketSettingsOverride.startLevelIndex + 1;
        }
        return CampaignProgression.LoopLevel;
    }

    private static int[] ResolveVariantFromLayout(LevelData level)
    {
        level.EnsureBucketLayout();
        var open = new bool[LevelData.BucketLayoutCellCount];
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
            open[i] = !LevelData.IsBucketLayoutWall(level.bucketLayout[i])
                && !LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[i]);
        for (int v = 0; v < Variants.Length; v++)
        {
            int[] ring = Variants[v];
            bool match = true;
            for (int i = 0; i < LevelData.BucketLayoutCellCount && match; i++)
            {
                bool onRing = Array.IndexOf(ring, i) >= 0;
                if (onRing != open[i]) match = false;
            }
            if (match) return ring;
        }
        // Fall back to any variant that covers all currently open cells.
        var openCells = Enumerable.Range(0, LevelData.BucketLayoutCellCount).Where(i => open[i]).ToArray();
        for (int v = 0; v < Variants.Length; v++)
            if (openCells.All(c => Array.IndexOf(Variants[v], c) >= 0)
                && Variants[v].All(c => open[c]))
                return Variants[v];
        return null;
    }

    private static void PlaceInnerFeeder(LevelData level, int[] ring, int nearIndex, ConveyorDirection preferred, int count)
    {
        int ringCell = ring[nearIndex % ring.Length];
        int x = ringCell % LevelData.BucketLayoutColumns;
        int y = ringCell / LevelData.BucketLayoutColumns;
        int[] candidates =
        {
            (y > 0 ? (y - 1) : y + 1) * LevelData.BucketLayoutColumns + x,
            (y < LevelData.BucketLayoutRows - 1 ? (y + 1) : y - 1) * LevelData.BucketLayoutColumns + x,
            y * LevelData.BucketLayoutColumns + Mathf.Clamp(x - 1, 0, 4),
            y * LevelData.BucketLayoutColumns + Mathf.Clamp(x + 1, 0, 4)
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            int cell = candidates[i];
            if (cell < 0 || cell >= LevelData.BucketLayoutCellCount) continue;
            if (Array.IndexOf(ring, cell) >= 0) continue;
            if (LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell])) continue;
            level.bucketLayout[cell] = LevelData.BucketLayoutConveyorSlot;
            ConveyorDirection dir = preferred;
            int exit = ConveyorDirectionUtility.GetTargetLayoutIndex(cell, dir);
            if (exit < 0 || Array.IndexOf(ring, exit) < 0)
            {
                foreach (ConveyorDirection tryDir in new[]
                         { ConveyorDirection.Front, ConveyorDirection.Back, ConveyorDirection.Left, ConveyorDirection.Right })
                {
                    exit = ConveyorDirectionUtility.GetTargetLayoutIndex(cell, tryDir);
                    if (exit >= 0 && Array.IndexOf(ring, exit) >= 0) { dir = tryDir; break; }
                }
            }
            level.SetConveyorDirection(cell, dir);
            level.SetConveyorBucketCount(cell, count);
            if (exit >= 0 && LevelData.IsBucketLayoutWall(level.bucketLayout[exit]))
                level.bucketLayout[exit] = 0;
            return;
        }
    }
}
