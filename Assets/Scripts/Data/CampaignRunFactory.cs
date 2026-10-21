using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>Fresh or verified layouts use the existing component generator and the level's rules.</summary>
public static class CampaignRunFactory
{
    public static LevelData Create(LevelData source, int index)
    {
        var level = UnityEngine.Object.Instantiate(source);
        level.bucketSettingsOverride = UnityEngine.Object.Instantiate(source.bucketSettingsOverride);
        try
        {
            CampaignProgression.ApplyPersistentRules(level, source.GetProgressionIndex(index));
            level.campaignSeed = level.bucketSettingsOverride.verifiedLayoutSeed != 0
                ? level.bucketSettingsOverride.verifiedLayoutSeed
                : Guid.NewGuid().GetHashCode();
            level.name = source.name + " (Run " + level.campaignSeed + ")";
            return level;
        }
        catch
        {
            UnityEngine.Object.Destroy(level.bucketSettingsOverride);
            UnityEngine.Object.Destroy(level);
            throw;
        }
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
