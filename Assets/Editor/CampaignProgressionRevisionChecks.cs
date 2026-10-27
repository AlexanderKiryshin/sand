using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Progression migration and model-level route/budget regression checks.</summary>
public static class CampaignProgressionRevisionChecks
{
    static string Output => Environment.GetEnvironmentVariable("SAND_PROGRESSION_OUTPUT") ?? "tasks/progression-180";
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [MenuItem("Window/Sand/Campaign/Apply New Mechanic Progression")]
    public static void Migrate()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        for (int i = 0; i < db.LevelCount; i++)
        {
            var level = db.GetLevel(i);
            byte[] cells = (byte[])level.cells.Clone();
            Color32[] palette = (Color32[])level.palette.Clone();
            CampaignProgression.ApplyPersistentRules(level, level.GetProgressionIndex(i));
            Require(level.cells.SequenceEqual(cells) && level.palette.SequenceEqual(palette), "Migration changed a painting at " + (i + 1));
            EditorUtility.SetDirty(level);
            EditorUtility.SetDirty(level.bucketSettingsOverride);
        }
        AssetDatabase.SaveAssets();
    }
    public static void MigrateAndRun()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        try
        {
            Migrate();
            CheckSwapGroup();
            CheckLargeSwapGroups();
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "status.txt"), "FAIL\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    public static void Run()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        int routes = 0, grouped = 0, fullStock = 0, six = 0, eight = 0, ordinary = 0;
        int[] introductions = { 12, 20, 30, 40, 55, 70, 85, 100, 120, 140, 160, 180 };
        for (int i = 0; i < db.LevelCount; i++)
        {
            int number = db.GetLevel(i).GetProgressionIndex(i) + 1;
            for (int run = 0; run < (i < 1000 ? 2 : 1); run++)
            {
                var level = CampaignRunFactory.Create(db.GetLevel(i), i);
                var root = new GameObject("ProgressionRouteCheck");
                try
                {
                    level.campaignSeed = unchecked(7319 + i * 104729 + run);
                    UnityEngine.Random.InitState(level.campaignSeed);
                    Require(level.bucketLayout.All(c => !LevelData.IsBucketLayoutWall(c) && !LevelData.IsBucketLayoutConveyorSlot(c)), "Obstacle remains at " + (i + 1));
                    Require(level.conveyorDirections.All(c => c == 0) && level.conveyorBucketCounts.All(c => c == 0), "Feeder metadata remains.");
                    Require(level.beltCapacity == 5 && level.beltSpeedMultiplier == 1, "Conveyor settings at " + number);
                    CheckUnlocks(level, number);
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    Require(inventory.ConveyorSlots.Count == 0, "A feeder was generated.");
                    var all = All(inventory).ToArray();
                    Require(!level.linkedBuckets || all.Count(b => b.LinkedPartner != null) / 2 >= (level.loopInventory ? 1 : CampaignProgression.LinkedPairCount(number)), "Linked pairs missing at " + number);
                    var marked = all.Where(b => b.SwapPartner != null).ToArray();
                    Require(!level.swappingColors || marked.Length == CampaignProgression.SwapBucketCount(number, all.Length), "Swap count at " + number + ": " + marked.Length);
                    if (level.swappingColors && number >= CampaignProgression.MultiSwapLevel)
                    {
                        Require(marked.All(b => b.ColorSwapGroup != null && b.ColorSwapGroup.Count == marked.Length), "Incomplete shared group at " + number);
                        if (CampaignProgression.AllBucketsSwapColors(number))
                        {
                            Require(marked.Length == all.Length && marked.Length > 8, "Full-stock challenge has unmarked cups at " + number);
                            Require(!level.sectionBuckets && !level.linkedBuckets && !level.keyGates && !level.largeBuckets, "Conflicting special cup at " + number);
                            fullStock++;
                        }
                        else if (marked.Length == 6) six++;
                        else if (marked.Length == 8) eight++;
                        grouped++;
                    }
                    if (number > 180 && !level.swappingColors) ordinary++;
                    if (number == 120) Require(all.Any(b => b.HasSecondSection && b.IsMystery), "Hidden section lesson missing.");
                    if (number == 160) Require(all.Any(b => b.LinkedPartner != null && b.IsMystery), "Hidden linked lesson missing.");
                    var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
                    int[] demand = new int[256];
                    foreach (byte color in level.cells) if (color != 0) demand[color]++;
                    CheckBudget(inventory, belt, demand, number);
                    int moves = 0;
                    while (inventory.CountRemaining() > 0 && moves++ < 500)
                    {
                        int settling = 0;
                        while (inventory.NeedsSettling && inventory.TryDispenseAll(level) && ++settling < 100) { }
                        var next = inventory.Buckets.OrderByDescending(b => inventory.IsCrateCovered(b)).ThenByDescending(b => b.KeyId).ToArray()
                            .FirstOrDefault(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                        Require(next != null, "Blocked route: level=" + number + ", database=" + (i + 1) + ", seed=" + level.campaignSeed);
                        CheckBudget(inventory, belt, demand, number);
                        foreach (var entry in belt.Entries.ToArray())
                        {
                            demand[entry.Bucket.FirstColor] -= entry.Bucket.RemainingForColor(entry.Bucket.FirstColor);
                            if (entry.Bucket.HasSecondSection && entry.Bucket.SecondColor != entry.Bucket.FirstColor)
                                demand[entry.Bucket.SecondColor] -= entry.Bucket.RemainingForColor(entry.Bucket.SecondColor);
                            entry.Bucket.ForceFillToCapacity(); inventory.HandleBucketFilled(entry.Bucket);
                            belt.MarkBucketFilledByBooster(entry.Bucket);
                        }
                        belt.UpdateBelt(.01f);
                    }
                    Require(inventory.CountRemaining() == 0 && demand.All(n => n == 0), "Unfinished level " + number);
                    routes++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
            if ((i + 1) % 50 == 0) File.WriteAllText(Path.Combine(Output, "progress.txt"), "Checked " + (i + 1) + "/" + db.LevelCount + "; routes=" + routes);
        }
        foreach (int n in introductions) Require(CampaignProgression.IsLessonStart(n - 1), "Lesson missing at " + n);
        foreach (int n in new[] { 3, 14, 18, 24, 25, 28, 38, 48, 50, 58, 68, 80, 150, 203, 208 })
            Require(!CampaignProgression.IsLessonStart(n - 1), "Obsolete lesson remains at " + n);
        Require(CampaignProgression.GetLessonId(179) == "lesson_swap_group", "Group-swap tutorial missing.");
        Require(fullStock == 0 && six > 0 && eight > 0 && ordinary > grouped, "Missing occasional variants or ordinary levels.");
        File.WriteAllText(Path.Combine(Output, "status.txt"), "PASS\nlevels=" + db.LevelCount + "; routes=" + routes + "; groups=" + grouped
            + "; six=" + six + "; eight=" + eight + "; full-stock=" + fullStock + "; ordinary-late-routes=" + ordinary
            + "; no walls/feeders/speed-up; intro gates, bounded shuffle groups and per-color sand budgets verified.");
        Debug.Log("PROGRESSION_REVISION_PASS routes=" + routes + ", groups=" + grouped);
    }
    static IEnumerable<Bucket> All(BucketInventory inventory)
        => inventory.Buckets.Concat(inventory.ReservedBuckets).Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets));
    static void CheckBudget(BucketInventory inventory, ConveyorBelt belt, int[] demand, int number)
    {
        int[] actual = new int[256];
        foreach (var bucket in All(inventory).Concat(belt.Entries.Select(e => e.Bucket)))
        {
            actual[bucket.FirstColor] += bucket.RemainingForColor(bucket.FirstColor);
            if (bucket.HasSecondSection && bucket.SecondColor != bucket.FirstColor)
                actual[bucket.SecondColor] += bucket.RemainingForColor(bucket.SecondColor);
        }
        for (int color = 1; color < 256; color++)
            Require(actual[color] == demand[color], "Sand budget changed at " + number + ", color " + color);
    }
    static void CheckUnlocks(LevelData level, int n)
    {
        Require(n >= 12 || !level.mysteryBuckets, "Early mystery.");
        Require(n >= 20 || !level.linkedBuckets, "Early linked.");
        Require(n >= 30 || level.frostMoves == 0, "Early ice.");
        Require(n >= 40 || !level.keyGates, "Early key.");
        Require(n >= 55 || !level.sectionBuckets, "Early sections.");
        Require(n >= 70 || !level.alternatingCrates, "Early covers.");
        Require(n >= 85 || !level.largeBuckets, "Early large.");
        Require(n >= 100 || !level.swappingColors, "Early swap.");
        Require(n >= 140 || !level.loopInventory, "Early loop.");
        if (n == 12) Require(level.mysteryBuckets, "Mystery introduction.");
        if (n == 20) Require(level.linkedBuckets, "Linked introduction.");
        if (n == 30) Require(level.frostMoves == CampaignProgression.IntroFrostMoves, "Ice introduction.");
        if (n == 40) Require(level.keyGates, "Key introduction.");
        if (n == 55) Require(level.sectionBuckets, "Section introduction.");
        if (n == 70) Require(level.alternatingCrates, "Cover introduction.");
        if (n == 85) Require(level.largeBuckets, "Large introduction.");
        if (n == 100 || n == 180) Require(level.swappingColors, "Swap introduction.");
        if (n == 140) Require(level.loopInventory, "Loop introduction.");
    }
    public static void CheckSwapGroup()
    {
        var inventory = new BucketInventory();
        var cups = new[] { new Bucket(1, 11, 0), new Bucket(2, 17, 6), new Bucket(3, 23, 12), new Bucket(4, 29, 18) };
        for (int i = 0; i < 4; i++) { cups[i].ColorSwapGroup = cups; cups[i].SwapPartner = cups[(i + 1) % 4]; }
        var unmarked = new Bucket(5, 31, 4);
        ((List<Bucket>)inventory.Buckets).AddRange(cups.Concat(new[] { unmarked }));
        var apply = typeof(BucketInventory).GetMethod("ApplySuccessfulMoveRules", Hidden);
        var seen = new HashSet<byte>();
        for (int seed = 0; seed < 128; seed++)
        {
            for (int i = 0; i < 4; i++) cups[i].SetColorAndCapacity((byte)(i + 1), new[] { 11, 17, 23, 29 }[i]);
            UnityEngine.Random.InitState(seed); apply.Invoke(inventory, null);
            Require(cups.Select(c => c.ColorIndex + ":" + c.Capacity).OrderBy(s => s).SequenceEqual(new[] { "1:11", "2:17", "3:23", "4:29" }), "Group shuffle changed sand contents.");
            Require(cups.Where((b, i) => b.ColorIndex != i + 1).Any(), "The whole group stayed unchanged.");
            Require(unmarked.ColorIndex == 5 && unmarked.Capacity == 31, "Unmarked cup changed.");
            seen.Add(cups[0].ColorIndex);
        }
        Require(seen.Count == 4, "Swaps are restricted to independent pairs instead of the whole group.");
        ((List<Bucket>)inventory.Buckets).Remove(cups[0]); cups[0].MoveToConveyor();
        byte beltColor = cups[0].ColorIndex; int beltCapacity = cups[0].Capacity;
        cups[1].SetContents(cups[1].ColorIndex, cups[1].Capacity, 3);
        byte partialColor = cups[1].ColorIndex; int partialCapacity = cups[1].Capacity;
        apply.Invoke(inventory, null);
        Require(cups[0].ColorIndex == beltColor && cups[0].Capacity == beltCapacity, "Cup on conveyor changed color.");
        Require(cups[1].ColorIndex == partialColor && cups[1].Capacity == partialCapacity && cups[1].CurrentFill == 3, "Partially filled recalled cup changed.");
    }
    static void CheckLargeSwapGroups()
    {
        foreach (int count in new[] { 6, 8, 32 })
        {
            var inventory = new BucketInventory();
            var cups = Enumerable.Range(1, count).Select(n => new Bucket((byte)n, n * 7 + 1, n - 1)).ToArray();
            for (int i = 0; i < count; i++) { cups[i].ColorSwapGroup = cups; cups[i].SwapPartner = cups[(i + 1) % count]; }
            ((List<Bucket>)inventory.Buckets).AddRange(cups.Take(count - 1));
            ((Queue<Bucket>)typeof(BucketInventory).GetField("reserve", Hidden).GetValue(inventory)).Enqueue(cups[count - 1]);
            var apply = typeof(BucketInventory).GetMethod("ApplySuccessfulMoveRules", Hidden);
            var expected = cups.Select(c => c.ColorIndex + ":" + c.Capacity).OrderBy(v => v).ToArray();
            var seen = new HashSet<byte>();
            for (int seed = 0; seed < count * 32; seed++)
            {
                for (int i = 0; i < count; i++) cups[i].SetColorAndCapacity((byte)(i + 1), (i + 1) * 7 + 1);
                UnityEngine.Random.InitState(seed); apply.Invoke(inventory, null);
                Require(expected.SequenceEqual(cups.Select(c => c.ColorIndex + ":" + c.Capacity).OrderBy(v => v)), "Large group lost sand contents.");
                seen.Add(cups[0].ColorIndex);
            }
            Require(seen.Count == count, "Large group cannot exchange with every marked cup, including reserve: " + count);
        }
    }

}
