using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignIterationChecks
{
    static CampaignIterationChecks() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        const string request = "Temp/campaign-iteration-check";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string mode = File.ReadAllText(request).Trim();
        File.Delete(request);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene before checks.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConveyorPlacementChecks.Run();
            Run();
            if (mode == "simulation") CampaignSimulationChecks.RunInEditor();
            File.WriteAllText("tasks/campaign-iteration-status.txt", "PASS " + DateTime.Now.ToString("O") + " " + mode);
        }
        catch (Exception error)
        {
            File.WriteAllText("tasks/campaign-iteration-status.txt", error.ToString());
            Debug.LogException(error);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    public static IEnumerable<Bucket> AllBuckets(BucketInventory inventory)
        => inventory.Buckets.Concat(inventory.ReservedBuckets).Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets));

    public static void Run()
    {
        var sections = new Bucket(1, 3); sections.AddSection(2, 4); sections.MoveToConveyor();
        Require(!sections.TryAddGrain(2), "Second section waits for first");
        for (int i = 0; i < 3; i++) Require(sections.TryAddGrain(1), "First section accepts exact budget");
        Require(sections.ColorIndex == 2 && !sections.IsFull && !sections.TryAddGrain(1), "Section changes only after exact fill");
        sections.ReturnToInventory(); sections.MoveToConveyor();
        for (int i = 0; i < 4; i++) Require(sections.TryAddGrain(2), "Second section retains budget after recall");
        Require(sections.IsFull && sections.RemainingCapacity == 0, "Both sections complete");
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var report = new List<string> { "# Random campaign and mechanic checks", "", $"Three fresh runs of each of {db.LevelCount} levels. Exact per-color demand, required mechanics, unique occupied positions and complete dispatch routes. Route checks fill dispatched buckets synthetically; physics is tested separately.", "", "Level | Runs | Status", "--- | --- | ---" };
        for (int index = 0; index < db.LevelCount; index++)
        {
            string previous = null;
            for (int run = 0; run < 3; run++)
            {
                LevelData level = CampaignRunFactory.Create(db.levels[index], index);
                var root = new GameObject("RandomRouteCheck");
                var rng = UnityEngine.Random.state;
                try
                {
                    UnityEngine.Random.InitState(level.campaignSeed);
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    var all = AllBuckets(inventory).ToArray();
                    string signature = string.Join(",", level.bucketLayout) + ":" + string.Join(",", all.Select(b => b.ColorIndex + "/" + b.Capacity));
                    Require(signature != previous, $"Level {index + 1}: restart repeated layout"); previous = signature;
                    Require(level.cells.SequenceEqual(db.levels[index].cells), "Restart preserves painting");
                    Require(!level.sectionBuckets || all.Count(b => b.HasSecondSection) >= 2, $"Level {index + 1}: need at least two 1|2 buckets");
                    Require(!level.largeBuckets || all.Any(b => b.SlotCost == 2), $"Level {index + 1}: missing large bucket");
                    Require(!level.linkedBuckets || all.Any(b => b.LinkedPartner != null && b.ColorIndex != b.LinkedPartner.ColorIndex), $"Level {index + 1}: missing different-color linked pair");
                    Require(!level.swappingColors || all.Any(b => b.SwapPartner != null), $"Level {index + 1}: missing swap pair");
                    Require(!level.keyGates || all.Any(b => b.KeyId != 0) && all.Any(b => inventory.IsGateClosed(b)), "Key and locked gates exist");
                    if (level.keyGates && index + 1 >= CampaignProgression.LargeLevel)
                    {
                        foreach (var key in all.Where(b => b.KeyId != 0 && b.LayoutIndex >= 0))
                        {
                            int col = key.LayoutIndex % LevelData.BucketLayoutColumns;
                            int row = key.LayoutIndex / LevelData.BucketLayoutColumns;
                            Require(!inventory.Buckets.Any(b => b.LayoutIndex % LevelData.BucketLayoutColumns == col
                                && b.LayoutIndex / LevelData.BucketLayoutColumns > row),
                                $"Level {index + 1}: bucket stacked under key");
                        }
                    }
                    if (level.swappingColors && index + 1 >= CampaignProgression.SwapLevel)
                    {
                        var swap = all.First(b => b.SwapPartner != null);
                        Require(swap.LayoutIndex / LevelData.BucketLayoutColumns
                            != swap.SwapPartner.LayoutIndex / LevelData.BucketLayoutColumns,
                            $"Level {index + 1}: swap pair must sit on different rows");
                    }
                    var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
                    belt.OnBucketFilled += inventory.HandleBucketFilled;
                    int[] demand = new int[256];
                    foreach (byte color in level.GetColorsUsedInLevel())
                    {
                        demand[color] = level.CountLevelCellsForColor(color);
                        Require(all.Sum(b => b.RemainingForColor(color)) == demand[color], "Per-color build budget");
                    }
                    if (level.swappingColors)
                    {
                        var swap = all.First(b => b.SwapPartner != null);
                        int size = swap.Capacity;
                        BoosterMechanics.TryShuffleInventoryColors(inventory);
                        Require(swap.Capacity == size && swap.SwapPartner.Capacity == size, "Shuffle preserves swap compatibility");
                    }
                    int removed = 0, moves = 0;
                    while (inventory.CountRemaining() > 0 && moves++ < 500)
                    {
                        inventory.TryDispenseAll(level);
                        Require(inventory.Buckets.Select(b => b.LayoutIndex).Distinct().Count() == inventory.Buckets.Count, "No inventory overlap");
                        var positions = inventory.Buckets.ToDictionary(b => b, b => b.LayoutIndex);
                        bool cratesOpen = inventory.CratesOpen;
                        var swap = inventory.Buckets.FirstOrDefault(b => b.SwapPartner != null && inventory.Buckets.Contains(b.SwapPartner));
                        byte oldColor = swap != null ? swap.ColorIndex : (byte)0;
                        Bucket next = inventory.Buckets.OrderByDescending(b => inventory.IsCrateCovered(b)).ThenByDescending(b => b.KeyId)
                            .ToArray().FirstOrDefault(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                        Require(next != null, $"Level {index + 1}, seed {level.campaignSeed}, run {run}, move {moves}: blocked route: " + string.Join(",", inventory.Buckets.Select(b => $"{b.LayoutIndex}:{b.ColorIndex}:ice{b.FrostMoves}:key{b.KeyId}:gate{inventory.IsGateClosed(b)}:crate{inventory.IsCrateCovered(b)}:link{b.LinkedPartner?.LayoutIndex}")));
                        if (inventory.CrateBucketCount > 0) Require(inventory.CratesOpen != cratesOpen, "Crates toggle once per successful action");
                        if (level.loopInventory)
                            foreach (var bucket in inventory.Buckets)
                                if (positions.TryGetValue(bucket, out int old)) Require(bucket.LayoutIndex == InventoryLoopRoute.Next(old), "Every remaining bucket advances one clockwise slot");
                        if (swap != null && inventory.Buckets.Contains(swap) && inventory.Buckets.Contains(swap.SwapPartner))
                            Require(swap.SwapPartner.ColorIndex == oldColor, "Compatible pair swaps once per successful action");
                        bool after = inventory.CratesOpen;
                        Require(!inventory.TryTakeBucket(next, belt, level) && inventory.CratesOpen == after, "Rejected repeated click does not toggle crates");
                        foreach (var entry in belt.Entries.ToArray())
                        {
                            for (int color = 1; color < 256; color++) demand[color] -= entry.Bucket.RemainingForColor((byte)color);
                            entry.Bucket.ForceFillToCapacity(); belt.MarkBucketFilledByBooster(entry.Bucket); removed++;
                        }
                        belt.UpdateBelt(.01f);
                        Require(belt.Entries.Count == 0, "Completed dispatch frees its belt slots");
                        for (int color = 1; color < 256; color++) Require(AllBuckets(inventory).Sum(b => b.RemainingForColor((byte)color)) == demand[color], "Move preserves exact color demand");
                    }
                    Require(inventory.CountRemaining() == 0 && removed == inventory.TotalBucketCount, "All queues and compound buckets cleared");
                }
                finally
                {
                    UnityEngine.Random.state = rng;
                    UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level);
                }
            }
            report.Add($"{index + 1} | 3 | PASS");
        }
        File.WriteAllLines("tasks/campaign-random-mechanics.md", report);
        Debug.Log($"CAMPAIGN_RANDOM_MECHANICS_PASS: {db.LevelCount * 3} fresh runs, complete routes, per-color budgets and successful-action semantics.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
