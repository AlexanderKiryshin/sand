using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CampaignColumnChecks
{
    [MenuItem("Window/Sand/Campaign/Check Filled Columns")]
    public static void Run()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var failures = new List<string>();
        var report = new List<string>();
        var state = UnityEngine.Random.state;
        try
        {
            for (int index = 0; index < database.LevelCount; index++)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var level = CampaignRunFactory.Create(database.levels[index], index);
                try
                {
                    UnityEngine.Random.InitState(7319 + index * 3 + attempt);
                    var inventory = new BucketInventory();
                    inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    var allBuckets = CampaignIterationChecks.AllBuckets(inventory).ToArray();
                    foreach (var group in allBuckets.Where(b => !b.HasSecondSection && b.SlotCost == 1).GroupBy(b => b.ColorIndex))
                        if (group.Max(b => b.Capacity) - group.Min(b => b.Capacity) > 1)
                            failures.Add($"Level {index + 1}: uneven ordinary buckets for color {group.Key}: {string.Join(",", group.Select(b => b.Capacity))}");
                    if (allBuckets.Any(b => b.Capacity == 1))
                        failures.Add($"Level {index + 1}: one-grain bucket");
                    for (int x = 0; x < LevelData.BucketLayoutColumns; x++)
                    {
                        var column = inventory.Buckets.Where(b => b.LayoutIndex % LevelData.BucketLayoutColumns == x).ToArray();
                        if (column.Length > 1 && level.GetColorsUsedInLevel().Length > 1 && column.Select(b => b.ColorIndex).Distinct().Count() == 1)
                            failures.Add($"Level {index + 1}: monochrome column {x + 1}");
                    }
                    var empty = Enumerable.Range(0, LevelData.BucketLayoutColumns)
                        .Where(x => !inventory.Buckets.Any(b => b.LayoutIndex % LevelData.BucketLayoutColumns == x)).ToArray();
                    if (empty.Length > 0)
                        failures.Add($"Level {index + 1}, attempt {attempt + 1}: empty columns {string.Join(",", empty.Select(x => x + 1))}");
                    for (int cell = 0; cell < LevelData.BucketLayoutCellCount; cell++)
                        if ((!level.loopInventory || Array.IndexOf(InventoryLoopRoute.Cells, cell) >= 0)
                            && !LevelData.IsBucketLayoutWall(level.bucketLayout[cell])
                            && !LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell])
                            && inventory.GetBucketAtLayoutIndex(cell) == null)
                            failures.Add($"Level {index + 1}, attempt {attempt + 1}: unfilled playable cell {cell}");
                    foreach (byte color in level.GetColorsUsedInLevel())
                    {
                        if (CampaignIterationChecks.AllBuckets(inventory).Sum(b => b.RemainingForColor(color)) != level.CountLevelCellsForColor(color))
                            failures.Add($"Level {index + 1}: sand budget changed for color {color}");
                        int units = allBuckets.Sum(b => (b.FirstColor == color ? b.SlotCost : 0) + (b.HasSecondSection && b.SecondColor == color ? 1 : 0));
                        int share = level.CountLevelCellsForColor(color) / units;
                        foreach (var cup in allBuckets)
                        {
                            if (cup.FirstColor == color)
                            {
                                int amount = cup.HasSecondSection ? cup.FirstSectionCapacity : cup.Capacity;
                                if (amount < share * cup.SlotCost || amount > (share + 1) * cup.SlotCost)
                                    failures.Add($"Level {index + 1}: unequal sand share for color {color}, capacity {amount}");
                            }
                            if (cup.HasSecondSection && cup.SecondColor == color
                                && (cup.Capacity - cup.FirstSectionCapacity < share || cup.Capacity - cup.FirstSectionCapacity > share + 1))
                                failures.Add($"Level {index + 1}: unequal second-section share for color {color}");
                        }
                    }
                    report.Add($"{index + 1}/{attempt + 1}: buckets={inventory.Buckets.Count}; empty={string.Join(",", empty.Select(x => x + 1))}; layout="
                        + string.Join("/", Enumerable.Range(0, LevelData.BucketLayoutRows).Select(y =>
                            string.Concat(Enumerable.Range(0, LevelData.BucketLayoutColumns).Select(x =>
                            {
                                int cell = y * LevelData.BucketLayoutColumns + x;
                                return inventory.GetBucketAtLayoutIndex(cell) != null ? "B"
                                    : LevelData.IsBucketLayoutWall(level.bucketLayout[cell]) ? "#"
                                    : LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell]) ? "F" : ".";
                            })))));
                    report.Add("  buckets: " + string.Join("; ", inventory.Buckets.OrderBy(b => b.LayoutIndex).Select(b => $"{b.LayoutIndex}={b.ColorIndex}:{b.Capacity}")));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);
                    UnityEngine.Object.DestroyImmediate(level);
                }
            }
        }
        finally { UnityEngine.Random.state = state; }
        Directory.CreateDirectory("tasks");
        File.WriteAllLines("tasks/filled-columns-checks.txt", report.Concat(failures));
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        Debug.Log($"CAMPAIGN_COLUMNS_PASS: {database.LevelCount} levels, 3 runs each; every playable cell filled, including tutorials, crate paths and rings; sand budgets preserved.");
    }

    public static void RunFull()
    {
        Run();
        CampaignQueueChecks.RunRoutes();
    }
}
