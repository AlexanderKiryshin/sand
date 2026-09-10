using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Fresh bounded layouts use the existing component generator and the level's rules.</summary>
public static class CampaignRunFactory
{
    private static readonly Dictionary<int, string> Previous = new Dictionary<int, string>();
    private static readonly HashSet<string> RecentStructures = new HashSet<string>();

    public static LevelData Create(LevelData source, int index)
    {
        if (source.campaignTemplateVersion > 0) return CreateAuthoredRun(source, index);
        var level = UnityEngine.Object.Instantiate(source);
        var rules = UnityEngine.Object.Instantiate(source.bucketSettingsOverride);
        var randomState = UnityEngine.Random.state;
        try
        {
            CampaignProgression.Configure(level, rules, index);
            byte[] canonical = (byte[])level.bucketLayout.Clone();
            byte[] directions = (byte[])level.conveyorDirections.Clone();
            byte[] counts = (byte[])level.conveyorBucketCounts.Clone();
            var components = new List<BucketLayoutComponent>();
            int walls = canonical.Count(LevelData.IsBucketLayoutWall);
            if (walls > 0) components.Add(new BucketLayoutComponent { type = BucketLayoutComponentType.Wall, count = walls });
            for (int i = 0; i < canonical.Length; i++)
                if (LevelData.IsBucketLayoutConveyorSlot(canonical[i]))
                    components.Add(new BucketLayoutComponent { type = BucketLayoutComponentType.Conveyor, count = 1,
                        direction = level.GetConveyorDirection(i), conveyorBucketCount = level.GetConveyorBucketCount(i) });
            rules.randomComponents = components.ToArray();
            int feederCount = canonical.Count(LevelData.IsBucketLayoutConveyorSlot);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                level.campaignSeed = Guid.NewGuid().GetHashCode();
                UnityEngine.Random.InitState(level.campaignSeed);
                if (attempt < 48 && !level.loopInventory)
                {
                    rules.fillMode = BucketLayoutFillMode.RandomComponents;
                    LevelBucketLayoutGenerator.Apply(level, rules);
                    if (level.bucketLayout.Count(LevelData.IsBucketLayoutWall) != walls
                        || level.bucketLayout.Count(LevelData.IsBucketLayoutConveyorSlot) != feederCount || !HasOpenRoutes(level)
                        || level.alternatingCrates && level.bucketLayout.Skip(15).Any(c => LevelData.IsBucketLayoutWall(c) || LevelData.IsBucketLayoutConveyorSlot(c))) continue;
                }
                else
                {
                    // Bounded fallback keeps the validated structure, still shuffling the color queue.
                    level.bucketLayout = (byte[])canonical.Clone();
                    level.conveyorDirections = (byte[])directions.Clone();
                    level.conveyorBucketCounts = (byte[])counts.Clone();
                }
                string structure = string.Join(",", level.bucketLayout) + "|" + string.Join(",", level.conveyorDirections);
                if (attempt < 40 && RecentStructures.Contains(structure) && !level.loopInventory) continue;
                rules.fillMode = BucketLayoutFillMode.UseTemplateAsIs;
                rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
                rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
                rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
                UnityEngine.Random.InitState(level.campaignSeed);
                var inventory = new BucketInventory();
                inventory.BuildFromLevel(level, 1, rules);
                string signature = structure + ":" + string.Join(",", inventory.Buckets.Select(b => b.ColorIndex + "/" + b.Capacity));
                if (Previous.TryGetValue(index, out string previous) && signature == previous) continue;
                Previous[index] = signature;
                RecentStructures.Add(structure);
                if (RecentStructures.Count > 40)
                {
                    // Keep only a rolling window so long campaigns don't reject every layout.
                    RecentStructures.Clear();
                    RecentStructures.Add(structure);
                }
                level.name = source.name + " (Run " + level.campaignSeed + ")";
                return level;
            }
            throw new InvalidOperationException("Could not prepare a distinct valid campaign layout.");
        }
        catch { UnityEngine.Object.Destroy(level); UnityEngine.Object.Destroy(rules); throw; }
        finally { UnityEngine.Random.state = randomState; }
    }

    private static LevelData CreateAuthoredRun(LevelData source, int index)
    {
        var level = UnityEngine.Object.Instantiate(source);
        level.bucketSettingsOverride = UnityEngine.Object.Instantiate(source.bucketSettingsOverride);
        // Keep the audited per-level topology. Fresh attempts only reshuffle bucket colors.
        level.campaignSeed = Guid.NewGuid().GetHashCode();
        level.name = source.name + " (Run " + level.campaignSeed + ")";
        return level;
    }

    public static bool HasOpenRoutes(LevelData level)
    {
        int width = LevelData.BucketLayoutColumns;
        var seen = new bool[LevelData.BucketLayoutCellCount];
        var open = new Queue<int>();
        for (int x = 0; x < width; x++)
        {
            if (Blocked(x)) return false; // Keep five safe starting cells, including the tutorial.
            seen[x] = true; open.Enqueue(x);
        }
        while (open.Count > 0)
        {
            int cell = open.Dequeue(), x = cell % width, y = cell / width;
            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
        }
        for (int i = 0; i < seen.Length; i++)
        {
            if (!Blocked(i) && !seen[i]) return false;
            if (!LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[i])) continue;
            int exit = ConveyorDirectionUtility.GetTargetLayoutIndex(i, level.GetConveyorDirection(i));
            if (exit < 0 || !seen[exit]) return false;
        }
        return true;
        bool Blocked(int i) => LevelData.IsBucketLayoutWall(level.bucketLayout[i]) || LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[i]);
        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= LevelData.BucketLayoutRows) return;
            int i = y * width + x;
            if (seen[i] || Blocked(i)) return;
            seen[i] = true; open.Enqueue(i);
        }
    }
}
