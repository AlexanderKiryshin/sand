using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Uses the production inventory and a synthetic fill to verify every dispatch route.
// Actual falling-sand simulation runs separately through AllLevelPlayabilityChecks.
public static class CampaignPacingChecks
{
    [Serializable] class Case { public int index; public string path; }
    [Serializable] class Cases { public Case[] cases; }
    [Serializable] class PlanRow
    {
        public string path, rulesPath;
        public bool guidedQueue;
        public int number, frostMoves;
        public bool mysteryBuckets, linkedBuckets, keyGates, sectionBuckets, alternatingCrates,
            largeBuckets, swappingColors, loopInventory;
    }
    [Serializable] class PlanRows { public List<PlanRow> levels = new List<PlanRow>(); }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static IEnumerable<Bucket> Stock(BucketInventory inventory) => inventory.Buckets.Concat(inventory.ReservedBuckets)
        .Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets));
    static string Signature(LevelData l) => string.Join(",", new[] { l.mysteryBuckets, l.linkedBuckets,
        l.frostMoves > 0, l.keyGates, l.sectionBuckets, l.alternatingCrates, l.largeBuckets, l.swappingColors, l.loopInventory });
    static string BoardSignature(BucketInventory inventory) => string.Join(";", Enumerable.Range(0, 25).Select(i =>
    {
        var b = inventory.GetBucketAtLayoutIndex(i);
        return (inventory.CrateLayoutIndices.Contains(i) ? "C" : "") + (b == null ? "_" :
            (b.IsMystery ? "M" : "") + (b.IsFrozen ? "F" : "") + (b.HasSecondSection ? "S" : "")
            + (b.SlotCost > 1 ? "B" : "") + (b.KeyId > 0 ? "K" : "") + (b.GateKeyId > 0 ? "G" : "")
            + (b.LinkedPartner != null ? "L" + b.LinkedPartner.LayoutIndex : "")
            + (b.SwapPartner != null ? "W" + b.SwapPartner.LayoutIndex : "") + ".");
    }));
    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("SAND_PACING_OUTPUT") ?? "tasks/pacing-validation";
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.txt"), "RUNNING");
        var rows = new PlanRows();
        try
        {
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText("column-cases.json")).cases;
            string previous = null;
            int routes = 0, previousCrates = -100, previousLoop = -100, eligible = 0, active = 0, dense = 0, triples = 0, fours = 0;
            var lateCoverage = new List<int>();
            var placements = new List<string>();
            var report = new List<string> { "database,number,mystery,linked,frozen,locks,sections,crateCells,large,swap,loop,plain,total,mechanics,affectedCells" };
            foreach (var item in cases)
            {
                var source = AssetDatabase.LoadAssetAtPath<LevelData>(item.path);
                int number = source.GetProgressionIndex(item.index) + 1;
                if (number == 1) { previous = null; previousCrates = previousLoop = -100; }
                for (int run = 0; run < 3; run++)
                {
                    var level = CampaignRunFactory.Create(source, item.index);
                    var root = new GameObject("Pacing route");
                    try
                    {
                        string signature = Signature(level);
                        Require(level.cells.SequenceEqual(source.cells) && level.palette.SequenceEqual(source.palette), "Artwork changed");
                        int mechanics = new[] {level.mysteryBuckets, level.linkedBuckets, level.frostMoves > 0, level.keyGates,
                            level.sectionBuckets, level.alternatingCrates, level.largeBuckets, level.swappingColors, level.loopInventory}.Count(v => v);
                        Require(mechanics <= (number < 200 ? 2 : number < 220 ? 3 : 4), "Too many mechanics at " + number);
                        if (number >= 70) Require(mechanics > 0, "Empty mechanic plan after 70 at " + number);
                        if (number >= 220) Require(mechanics >= 3, "Sparse late level " + number);
                        Require(mechanics < 3 || !level.largeBuckets || !level.linkedBuckets, "Dense level mixes two-slot families at " + number);
                        if (CampaignProgression.IsLessonStart(number - 1))
                            Require(mechanics == (number == 120 || number == 160 ? 2 : number == 1 ? 0 : 1), "Crowded lesson at " + number);
                        level.campaignSeed = unchecked(7319 + item.index * 104729 + run);
                        UnityEngine.Random.InitState(level.campaignSeed);
                        var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                        signature += ":" + BoardSignature(inventory);
                        // Plain introductory boards share the full grid; later variety comes from mechanics.
                        Require(number < CampaignProgression.MysteryLevel || run != 0 || signature != previous,
                            "Adjacent identical layouts at " + item.index + "/" + number);
                        if (!level.loopInventory)
                            Require(inventory.Buckets.Count == LevelData.BucketLayoutCellCount,
                                "Incomplete initial board at " + item.index + "/" + number);
                        placements.Add((item.index + 1) + "," + number + "," + run + "," + BoardSignature(inventory));
                        var all = Stock(inventory).ToArray();
                        int hidden = all.Count(b => b.IsMystery), linked = all.Count(b => b.LinkedPartner != null),
                            frozen = all.Count(b => b.IsFrozen), locks = all.Count(b => b.GateKeyId != 0),
                            sections = all.Count(b => b.HasSecondSection), swap = all.Count(b => b.SwapPartner != null),
                            large = all.Count(b => b.SlotCost > 1), crates = inventory.CrateLayoutIndices.Count();
                        bool fullMystery = CampaignProgression.IsFullMysteryLevel(number);
                        if (fullMystery) Require(level.mysteryBuckets && hidden == all.Length, "Incomplete full mystery stock at " + number);
                        else Require(!level.mysteryBuckets || hidden >= 1 && hidden <= CampaignProgression.MysteryBucketCount(number) + 4, "Mystery density at " + number);
                        if (CampaignProgression.IsLinkedFocusLevel(number))
                            Require(level.linkedBuckets && linked / 2 >= 4, "Sparse linked focus at " + number);
                        Require(frozen == (level.frostMoves > 0 ? CampaignProgression.FrozenBucketCount(number) : 0), "Ice density at " + number);
                        Require(!CampaignProgression.UsesGuidedBucketOrder(number) || level.bucketSettingsOverride.matchBucketOrderToSand,
                            "Learning or recovery queue is not guided at " + number);
                        Require(!level.linkedBuckets || linked / 2 == CampaignProgression.LinkedPairCount(number), "Missing linked pairs at " + number);
                        Require(!level.sectionBuckets || sections == CampaignProgression.SectionBucketCount(number), "Missing sections at " + number);
                        Require(!level.swappingColors || swap == CampaignProgression.SwapBucketCount(number, all.Length), "Swap density at " + number);
                        if (number < CampaignProgression.MultiSwapLevel)
                            Require(swap <= 2, "More than one swap pair before the group lesson at " + number);
                        if (number == CampaignProgression.MultiSwapLevel)
                            Require(level.swappingColors && swap == 4, "Group swap lesson must introduce four cups");
                        Require(!level.alternatingCrates || crates == CampaignProgression.CrateCellCount(number), "Full crate coverage at " + number);
                        if (level.alternatingCrates)
                            Require(inventory.Buckets.Any(b => b.LayoutIndex < 5 && inventory.IsCrateCovered(b))
                                && inventory.Buckets.Any(b => b.LayoutIndex < 5 && !inventory.IsCrateCovered(b)), "Crate lesson needs covered and ordinary front cups at " + number);
                        Require(!level.largeBuckets || large == CampaignProgression.LargeBucketCount(number), "Missing large cup at " + number);
                        Require(new[] { fullMystery ? 0 : hidden, linked, frozen, locks, sections, swap }.All(count => count <= all.Length / 2), "Mechanic covers most stock at " + number);
                        int plain = all.Count(b => !b.IsMystery && !b.IsFrozen && b.LinkedPartner == null && b.SwapPartner == null
                            && b.KeyId == 0 && b.GateKeyId == 0 && !b.HasSecondSection && b.SlotCost == 1);
                        Require(fullMystery || plain >= (number < 200 ? all.Length / 3 : 4), "Too little ordinary practice at " + number);
                        int affected = inventory.Buckets.Count(b => b.IsMystery || b.IsFrozen || b.LinkedPartner != null || b.SwapPartner != null
                            || b.KeyId > 0 || b.GateKeyId > 0 || b.HasSecondSection || b.SlotCost > 1
                            || inventory.CrateLayoutIndices.Contains(b.LayoutIndex)
                            || level.loopInventory && level.inventoryLoopCells.Contains(b.LayoutIndex));
                        if (number >= 220) Require(affected >= 10, "Too few late mechanic cells at " + number + ": " + affected);
                        if (number == 22) Require(level.linkedBuckets && level.mysteryBuckets && linked == 2 && hidden == 2, "First familiar support should be one pair and two mystery cups");
                        if (number == 21) Require(level.linkedBuckets && mechanics == 1, "Familiar support must be intermittent");
                        if (number == 32) Require(level.linkedBuckets && level.frostMoves > 0 && linked == 2 && frozen == 1, "Ice practice must have a small familiar pair");
                        if (number == 182) Require(level.swappingColors && mechanics == 2 && swap == 4 && large == 1, "New shared swaps need one familiar large cup");
                        if (number == 122) Require(level.mysteryBuckets && level.sectionBuckets && sections == 1, "Repeat hidden sections in a small group");
                        if (number == 162) Require(level.mysteryBuckets && level.linkedBuckets && linked == 2, "Repeat hidden linked cups in one pair");
                        if (number == 42) Require(level.keyGates && level.frostMoves > 0 && locks == 1 && frozen == 1, "Key practice must have one familiar frozen cup");
                        if (number == 12) Require(hidden == 2, "Mystery lesson size");
                        if (number == 30) Require(frozen == 1, "Ice lesson size");
                        if (number == 40) Require(locks == 1, "Key lesson size");
                        if (level.alternatingCrates)
                            Require(crates == LevelData.BucketLayoutCellCount
                                && Enumerable.Range(0, LevelData.BucketLayoutCellCount).All(i =>
                                    inventory.IsCrateCellClosed(i) == (i % LevelData.BucketLayoutColumns % 2 == 0)),
                                "Crates must alternate full columns in every row at " + number);
                        if (number == 140) Require(level.inventoryLoopCells.Length == 8, "Loop lesson size");
                        if (run == 0)
                        {
                            if (level.alternatingCrates) { Require(number - previousCrates >= 3, "Crates too frequent"); previousCrates = number; }
                            if (level.loopInventory) { Require(number - previousLoop >= 3, "Loop too frequent"); previousLoop = number; }
                            previous = signature;
                            if (number >= 32) { eligible++; if (mechanics > 0) active++; }
                            if (number >= 220) { dense++; if (mechanics == 3) triples++; if (mechanics == 4) fours++; lateCoverage.Add(affected); }
                            rows.levels.Add(new PlanRow { path = item.path, rulesPath = AssetDatabase.GetAssetPath(source.bucketSettingsOverride),
                                guidedQueue = level.bucketSettingsOverride.matchBucketOrderToSand, number = number, frostMoves = level.frostMoves,
                                mysteryBuckets = level.mysteryBuckets, linkedBuckets = level.linkedBuckets, keyGates = level.keyGates,
                                sectionBuckets = level.sectionBuckets, alternatingCrates = level.alternatingCrates,
                                largeBuckets = level.largeBuckets, swappingColors = level.swappingColors, loopInventory = level.loopInventory });
                            report.Add(string.Join(",", new[] { item.index + 1, number, hidden, linked, frozen, locks, sections, crates, large,
                                swap, level.loopInventory ? level.inventoryLoopCells.Length : 0, plain, all.Length, mechanics, affected }));
                        }
                        var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
                        var remaining = new int[256]; foreach (byte c in level.cells) if (c > 0) remaining[c]++;
                        void Budget()
                        {
                            var actual = new int[256];
                            foreach (var cup in Stock(inventory).Concat(belt.Entries.Select(e => e.Bucket)))
                            {
                                actual[cup.FirstColor] += cup.RemainingForColor(cup.FirstColor);
                                if (cup.HasSecondSection && cup.SecondColor != cup.FirstColor) actual[cup.SecondColor] += cup.RemainingForColor(cup.SecondColor);
                            }
                            Require(actual.SequenceEqual(remaining), "Sand budget at " + number);
                        }
                        Budget();
                        for (int moves = 0; inventory.CountRemaining() > 0 && moves < 500; moves++)
                        {
                            for (int settle = 0; inventory.NeedsSettling && settle < 100 && inventory.TryDispenseAll(level); settle++) { }
                            var next = inventory.Buckets.OrderByDescending(b => b.KeyId).ThenByDescending(b => inventory.IsCrateCovered(b)).ToArray()
                                .FirstOrDefault(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                            Require(next != null, "Blocked dispatch at " + number + " database " + (item.index + 1) + " seed " + run);
                            Budget();
                            foreach (var entry in belt.Entries.ToArray())
                            {
                                var cup = entry.Bucket;
                                remaining[cup.FirstColor] -= cup.RemainingForColor(cup.FirstColor);
                                if (cup.HasSecondSection && cup.SecondColor != cup.FirstColor) remaining[cup.SecondColor] -= cup.RemainingForColor(cup.SecondColor);
                                cup.ForceFillToCapacity(); inventory.HandleBucketFilled(cup); belt.MarkBucketFilledByBooster(cup);
                            }
                            belt.UpdateBelt(.01f);
                        }
                        Require(inventory.CountRemaining() == 0 && remaining.All(n => n == 0), "Unfinished route at " + number);
                        routes++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
                }
                if ((item.index + 1) % 50 == 0) File.WriteAllText(Path.Combine(output, "progress.txt"), "Checked " + (item.index + 1) + "/" + cases.Length + "; routes=" + routes);
            }
            Require(active >= eligible * .90f, "Mechanics are too sparse: " + active + "/" + eligible);
            Require(dense > 0 && triples > 0 && fours > 0, "Late levels need both triple and four-way combinations");
            File.WriteAllText(Path.Combine(output, "mixing-summary.txt"), "late_levels=" + dense + "; triples=" + triples + "; four_types=" + fours
                + "; affected_min=" + lateCoverage.Min() + "; affected_mean=" + lateCoverage.Average().ToString("F1") + "; affected_max=" + lateCoverage.Max());
            CheckPlacementVariety(cases, output);
            File.WriteAllLines(Path.Combine(output, "placements.csv"), new[] { "database,number,run,cells" }.Concat(placements));
            File.WriteAllLines(Path.Combine(output, "levels.csv"), report);
            File.WriteAllText(Path.Combine(output, "plans.json"), JsonUtility.ToJson(rows, true));
            File.WriteAllText(Path.Combine(output, "status.txt"), "PASS: " + cases.Length + " levels; " + routes + " dispatch routes; full initial rows from level 1; no adjacent repeated mechanic layouts; mechanics on " + active + "/" + eligible + " levels after 31; varied special positions; original full-column crates; no empty mechanic levels after 70; full mystery and dense linked accents; small intermittent familiar support; dense 220+ combinations; bounded special counts; guided learning/recovery queues; exact sand budgets. Synthetic fills, no falling-sand physics.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "status.txt"), "FAIL\n" + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void CheckPlacementVariety(Case[] cases, string output)
    {
        var lines = new List<string>();
        int[] numbers = { 30, 50, 50, 60, 80, 90, 110, 130 };
        for (int kind = 0; kind < numbers.Length; kind++)
        {
            int number = numbers[kind];
            var source = AssetDatabase.LoadAssetAtPath<LevelData>(cases.First(c => c.index == number - 1).path);
            var patterns = new HashSet<string>();
            var keyColumns = new HashSet<int>();
            for (int seed = 0; seed < 24; seed++)
            {
                var level = CampaignRunFactory.Create(source, number - 1);
                try
                {
                    level.mysteryBuckets = kind == 0; level.linkedBuckets = kind == 1;
                    level.frostMoves = kind == 2 ? CampaignProgression.FrostMovesFor(number) : 0;
                    level.keyGates = kind == 3; level.sectionBuckets = kind == 4;
                    level.alternatingCrates = kind == 5; level.largeBuckets = kind == 6;
                    level.swappingColors = kind == 7; level.loopInventory = false;
                    level.inventoryLoopCells = null;
                    UnityEngine.Random.InitState(91001 + seed * 104729);
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    foreach (var key in inventory.Buckets.Where(b => b.KeyId > 0)) keyColumns.Add(key.LayoutIndex % 5);
                    patterns.Add(string.Join(";", inventory.Buckets.Where(b => kind == 0 ? b.IsMystery : kind == 1 ? b.LinkedPartner != null
                        : kind == 2 ? b.IsFrozen : kind == 3 ? b.KeyId > 0 || b.GateKeyId > 0 : kind == 4 ? b.HasSecondSection
                        : kind == 6 ? b.SlotCost > 1 : kind == 7 ? b.SwapPartner != null : false)
                        .Select(b => b.LayoutIndex).Concat(kind == 5 ? inventory.CrateLayoutIndices : Enumerable.Empty<int>()).OrderBy(i => i)));
                }
                finally { UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
            if (kind == 3) Require(keyColumns.Count >= 4, "Key is pinned to one column");
            if (kind == 5) Require(patterns.Count == 1, "Crates must retain their full-board coverage");
            else Require(patterns.Count >= 8, "Fixed special positions for kind " + kind + ": " + patterns.Count + "/24");
            lines.Add("kind=" + kind + "; distinct_positions=" + patterns.Count + "/24");
        }
        File.WriteAllLines(Path.Combine(output, "position-variety.txt"), lines);
    }

}
