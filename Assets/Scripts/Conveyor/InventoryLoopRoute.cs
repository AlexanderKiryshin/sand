using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class InventoryLoopRoute
{
    private static readonly int[] OuterRing = { 0, 1, 2, 3, 4, 9, 14, 19, 24, 23, 22, 21, 20, 15, 10, 5 };
    private static readonly int[][] Variants = CreateVariants(false);
    private static readonly int[][] CompactVariants = CreateVariants(true);
    public static int[] Cells { get; private set; } = OuterRing;

    // Mix rectangles and bent paths while keeping each route connected to the front.
    private static int[][] CreateVariants(bool compact)
    {
        var result = new List<int[]>();
        if (!compact) result.Add(OuterRing);
        else
            for (int width = 3; width <= 5; width++)
                for (int height = 2; height <= 5; height++)
                    for (int left = 0; left <= 5 - width; left++)
                    {
                        if (2 * (width + height) - 4 < 8 || width == 5 && height == 5) continue;
                        var ring = new List<int>();
                        for (int x = left; x < left + width; x++) ring.Add(x);
                        for (int y = 1; y < height; y++) ring.Add(y * 5 + left + width - 1);
                        for (int x = left + width - 2; x >= left; x--) ring.Add((height - 1) * 5 + x);
                        for (int y = height - 2; y > 0; y--) ring.Add(y * 5 + left);
                        result.Add(ring.ToArray());
                    }
        var seen = new HashSet<string>(result.Select(r => string.Join(",", r.OrderBy(c => c))));
        for (int v = 0; v < result.Count && result.Count < 512; v++)
        {
            var route = result[v];
            if (route.Length >= (compact ? 18 : 22)) continue;
            for (int edge = 0; edge < route.Length; edge++)
            {
                int a = route[edge], b = route[(edge + 1) % route.Length];
                int dx = b % 5 - a % 5, dy = b / 5 - a / 5;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int nx = -dy * sign, ny = dx * sign;
                    int ax = a % 5 + nx, ay = a / 5 + ny, bx = b % 5 + nx, by = b / 5 + ny;
                    if (ax < 0 || ax > 4 || ay < 0 || ay > 4 || bx < 0 || bx > 4 || by < 0 || by > 4) continue;
                    int c = ay * 5 + ax, d = by * 5 + bx;
                    if (Array.IndexOf(route, c) >= 0 || Array.IndexOf(route, d) >= 0) continue;
                    var expanded = route.Take(edge + 1).Concat(new[] { c, d }).Concat(route.Skip(edge + 1)).ToArray();
                    if (compact && expanded.Select(cell => cell / 5).Distinct().Count() == 5
                        && expanded.Select(cell => cell % 5).Distinct().Count() == 5) continue;
                    if (seen.Add(string.Join(",", expanded.OrderBy(cell => cell)))) result.Add(expanded);
                }
            }
        }
        return result.ToArray();
    }

    public static void Bind(LevelData level)
    {
        if (level == null || !level.loopInventory) return;
        if (level.inventoryLoopCells == null)
            ApplyStructure(level, EstimateNumber(level));
        Cells = level.inventoryLoopCells;
    }

    public static int Next(int cell)
    {
        int i = Array.IndexOf(Cells, cell);
        return i < 0 ? cell : Cells[(i + 1) % Cells.Length];
    }

    public static void ApplyStructure(LevelData level) => ApplyStructure(level, EstimateNumber(level));
    public static void ApplyStructure(LevelData level, int levelNumber)
    {
        level.EnsureBucketLayout(); level.EnsureConveyorMetadata();
        // Use compact introductory routes, then vary the route as the campaign advances.
        int variant = Mathf.Max(0, levelNumber - CampaignProgression.LoopLevel);
        Cells = levelNumber < CampaignProgression.LoopLevel + 12 ? CompactVariants[variant % CompactVariants.Length]
            : variant % 3 == 0 ? Variants[(variant / 3) % Variants.Length]
            : CompactVariants[(variant - variant / 3 - 1) % CompactVariants.Length];
        level.inventoryLoopCells = (int[])Cells.Clone();
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            level.bucketLayout[i] = LevelData.BucketLayoutEmpty;
            level.conveyorDirections[i] = 0; level.conveyorBucketCounts[i] = 0;
        }
    }

    private static int EstimateNumber(LevelData level)
    {
        var rules = level?.bucketSettingsOverride;
        if (rules != null)
        {
            string id = rules.segmentId;
            if (!string.IsNullOrEmpty(id) && int.TryParse(id.Substring(id.LastIndexOf('_') + 1), out int number)) return number;
            return rules.startLevelIndex + 1;
        }
        return CampaignProgression.LoopLevel;
    }

}
