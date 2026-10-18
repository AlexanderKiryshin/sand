using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CampaignQueueChecks
{
    [MenuItem("Window/Sand/Campaign/Check Queue Progression")]
    public static void Run()
    {
        var failures = new List<string>();
        Check("forward movement", CheckForwardMovement);
        Check("blocked rear entry", CheckBlockedRearEntry);
        Check("movement beside a wall", CheckSideAccess);
        Check("movement through an initial access aisle", CheckAisleMovement);
        Check("campaign progression", CheckCampaign);
        Check("generated progression", CheckGeneratedProgression);
        Check("layout probe 28/38/49", CheckReportedLevels);
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        Debug.Log("CAMPAIGN_QUEUE_PASS: persistent ice, populated boards, clear rear rows, forward movement and rear-only reserve.");
        void Check(string name, Action action)
        {
            try { action(); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); Debug.LogError("QUEUE_FAIL " + name + ": " + error.Message); }
        }
    }

    private static void CheckReportedLevels()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        foreach (int number in new[] { 38, 44, 50 })
        {
            int index = number - 1;
            var level = CampaignRunFactory.Create(database.levels[index], index);
            try
            {
                UnityEngine.Random.InitState(level.campaignSeed);
                var inventory = new BucketInventory();
                inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                var aisle = (HashSet<int>)typeof(BucketInventory).GetField("reservedAccessPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
                Require(!level.keyGates || aisle.Count == 0 || level.alternatingCrates, number + ": key levels must not reserve an empty aisle");
                Require(!inventory.NeedsSettling, number + ": board settles before first paint");
                if (level.linkedBuckets)
                {
                    int wanted = CampaignProgression.LinkedPairCount(number);
                    var linked = inventory.Buckets.Where(b => b.LinkedPartner != null).ToList();
                    Require(linked.Count / 2 >= wanted, number + ": need " + wanted + " linked pairs, got " + (linked.Count / 2));
                    Require(linked.All(b => Mathf.Abs(b.LayoutIndex % LevelData.BucketLayoutColumns
                        - b.LinkedPartner.LayoutIndex % LevelData.BucketLayoutColumns) == 1),
                        number + ": linked pairs occupy neighboring columns");
                    var seats = linked.Where(b => b.LayoutIndex < b.LinkedPartner.LayoutIndex)
                        .Select(b => b.LayoutIndex + ":" + b.LinkedPartner.LayoutIndex).ToList();
                    Require(seats.Distinct().Count() == seats.Count, number + ": linked seats must be unique");
                }
                Require(!inventory.Buckets.Any(b => aisle.Contains(b.LayoutIndex)) || level.alternatingCrates,
                    number + ": non-crate boards must not keep an empty reserved aisle");
            }
            finally { UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
        }
    }

    private static void CheckForwardMovement()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("QueueRegression");
        var state = UnityEngine.Random.state;
        try
        {
            level.gridWidth = 30; level.gridHeight = 1;
            level.cells = Enumerable.Repeat((byte)1, 30).ToArray();
            level.palette = new Color32[] { Color.red };
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            // Match GameManager's synchronous inventory callback.
            inventory.OnInventoryChanged += () => inventory.TryDispenseAll(level);
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(30);
            var front = inventory.GetBucketAtLayoutIndex(0);
            var second = inventory.GetBucketAtLayoutIndex(5);
            var third = inventory.GetBucketAtLayoutIndex(10);
            var queued = inventory.ReservedBuckets.First();
            Require(inventory.TryTakeBucket(front, belt, level), "front bucket dispatch succeeds");
            Require(second.LayoutIndex == 0 && third.LayoutIndex == 5, "existing buckets must advance down the freed column");
            Require(queued.LayoutIndex >= 20, "a reserve bucket must enter on the last row");
            Require(inventory.CountRemaining() + belt.Entries.Count == 30, "movement preserves every bucket");
            Require(!inventory.TryDispenseAll(level), "settled queue is stable");
            belt.OnBucketReturned += inventory.ReturnBucket;
            Require(belt.TryRecallBucket(front), "recall succeeds");
            Require(front.LayoutIndex == -1 && inventory.CountRemaining() + belt.Entries.Count == 30, "a recalled cup waits when the rear row is full");
        }
        finally
        {
            UnityEngine.Random.state = state;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules);
        }
    }

    private static void CheckBlockedRearEntry()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("BlockedRearRegression");
        try
        {
            level.gridWidth = 30; level.gridHeight = 1;
            level.cells = Enumerable.Repeat((byte)1, 30).ToArray();
            level.palette = new Color32[] { Color.red };
            rules.maxGrainsPerBucket = 1;
            // An authored/custom obstacle must never be crossed to reach a front vacancy.
            level.bucketLayout[5] = LevelData.BucketLayoutWall;
            for (int cell = 20; cell < 25; cell++) level.bucketLayout[cell] = LevelData.BucketLayoutWall;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            int queued = inventory.ReserveCount;
            var behindWall = inventory.GetBucketAtLayoutIndex(10);
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(30);
            Require(inventory.TryTakeBucket(inventory.GetBucketAtLayoutIndex(0), belt, level), "front cup can leave");
            Require(behindWall.LayoutIndex == 10 && inventory.GetBucketAtLayoutIndex(0) == null, "buckets cannot jump across walls");
            Require(inventory.ReserveCount == queued, "reserve waits instead of appearing in a front vacancy");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules); }
    }

    private static void CheckGeneratedProgression()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        try
        {
            level.gridWidth = level.gridHeight = 8;
            level.cells = Enumerable.Repeat((byte)1, 64).ToArray();
            for (int index = 0; index < CampaignProgression.LevelCount; index++)
            {
                CampaignProgression.Configure(level, rules, index);
                Require(level.frostMoves == CampaignProgression.FrostMovesFor(index + 1), "generated ice progression at " + (index + 1));
                Require(!level.bucketLayout.Skip(15).Any(LevelData.IsBucketLayoutWall), "generated rear walls at " + (index + 1));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules); }
    }

    private static void CheckSideAccess()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("SideAccessRegression");
        try
        {
            level.gridWidth = 3; level.gridHeight = 1;
            level.cells = new byte[] { 1, 2, 3 };
            level.palette = new Color32[] { Color.red, Color.green, Color.blue };
            foreach (int cell in new[] { 7, 8, 14, 18 }) level.bucketLayout[cell] = LevelData.BucketLayoutWall;
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            var opening = inventory.Buckets[0]; opening.SetLayoutIndex(12);
            var waiting = inventory.Buckets[1]; waiting.SetLayoutIndex(13);
            var advancing = inventory.Buckets[2]; advancing.SetLayoutIndex(17);
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            Require(inventory.TryTakeBucket(opening, belt, level), "open the only side approach");
            Require(advancing.LayoutIndex == 12 && waiting.LayoutIndex == 13,
                "an empty forward cell must accept its next bucket regardless of a neighbouring route");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules); }
    }

    private static void CheckAisleMovement()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("IdleQueueRegression");
        try
        {
            level.gridWidth = 30; level.gridHeight = 1;
            level.cells = Enumerable.Repeat((byte)1, 30).ToArray();
            level.palette = new Color32[] { Color.red };
            level.campaignTemplateVersion = 1; level.alternatingCrates = true;
            level.bucketLayout[8] = LevelData.BucketLayoutConveyorSlot;
            level.SetConveyorDirection(8, ConveyorDirection.Left); level.SetConveyorBucketCount(8, 2);
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            var aisle = (HashSet<int>)typeof(BucketInventory).GetField("reservedAccessPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            Require(aisle.Count > 0, "crate boards keep an access aisle");
            Require(!inventory.NeedsSettling, "build settles the queue before the first paint");
            int aisleBuckets = inventory.Buckets.Count(b => aisle.Contains(b.LayoutIndex));
            Require(aisleBuckets <= 1, "only a feeder outlet may sit in the aisle after settle");
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(30);
            var front = inventory.Buckets.Where(b => b.LayoutIndex >= 0 && !aisle.Contains(b.LayoutIndex))
                .OrderBy(b => b.LayoutIndex).First();
            Require(inventory.TryTakeBucket(front, belt, level), "front cup can leave beside an aisle");
            inventory.TryDispenseAll(level);
            Require(!inventory.NeedsSettling, "the idle queue stops updating once settled");
            Require(inventory.Buckets.Count(b => aisle.Contains(b.LayoutIndex)) <= 1,
                "column advance must not flood the reserved aisle");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules); }
    }

    private static void CheckCampaign()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var missingIce = new List<int>(); var rearWalls = new List<int>(); var sparse = new List<string>();
        var state = UnityEngine.Random.state;
        try
        {
            for (int index = 0; index < database.LevelCount; index++)
            {
                var level = CampaignRunFactory.Create(database.levels[index], index);
                try
                {
                    UnityEngine.Random.InitState(7319 + index);
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    if (index + 1 >= CampaignProgression.IceLevel && !inventory.Buckets.Any(b => b.IsFrozen)) missingIce.Add(index + 1);
                    if (level.bucketLayout.Skip(15).Any(LevelData.IsBucketLayoutWall)) rearWalls.Add(index + 1);
                    var aisle = (HashSet<int>)typeof(BucketInventory).GetField("reservedAccessPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
                    int available = Enumerable.Range(0, 25).Count(cell => !aisle.Contains(cell)
                        && !LevelData.IsBucketLayoutWall(level.bucketLayout[cell]) && !LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell]));
                    if (index >= 2 && !level.loopInventory && inventory.Buckets.Count < available)
                        sparse.Add($"{index + 1}({inventory.Buckets.Count}/{available})");
                    foreach (byte color in level.GetColorsUsedInLevel())
                        Require(CampaignIterationChecks.AllBuckets(inventory).Sum(b => b.RemainingForColor(color)) == level.CountLevelCellsForColor(color), "grain budget at " + (index + 1));
                }
                finally { UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        finally { UnityEngine.Random.state = state; }
        Require(missingIce.Count + rearWalls.Count + sparse.Count == 0,
            "No ice: " + string.Join(",", missingIce) + "; rear walls: " + string.Join(",", rearWalls)
            + "; unfilled playable cells: " + string.Join(",", sparse));
    }

    public static void RunRoutes()
    {
        System.IO.Directory.CreateDirectory("tasks");
        Run();
        CampaignFeederQueueChecks.Run();
        CampaignMechanicsChecks.Run();
        CampaignIterationChecks.Run();
        CampaignCrateChecks.Run();
    }

    public static void RunFull()
    {
        RunRoutes();
        CampaignSimulationChecks.RunQueueProgression();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
