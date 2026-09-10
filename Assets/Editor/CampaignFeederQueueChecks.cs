using System;
using System.Linq;
using UnityEngine;

public static class CampaignFeederQueueChecks
{
    public static void Run()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("FeederQueueRegression");
        try
        {
            level.gridWidth = 6; level.gridHeight = 1;
            level.cells = new byte[] { 1, 2, 3, 4, 5, 6 };
            level.palette = new Color32[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };
            level.EnsureBucketLayout(); level.EnsureConveyorMetadata();
            for (int i = 0; i < 25; i++) level.bucketLayout[i] = 255;
            for (int i = 0; i < 4; i++) level.bucketLayout[i] = 0;
            level.bucketLayout[6] = 254;
            level.SetConveyorDirection(6, ConveyorDirection.Front); level.SetConveyorBucketCount(6, 1);
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            var feeder = inventory.ConveyorSlots.Single();
            var waiting = feeder.ReservedBuckets[0];
            Require(inventory.ReserveCount == 1, "There is a general queued cup competing with the feeder.");
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(6);
            Require(inventory.TryTakeBucket(inventory.GetBucketAtLayoutIndex(1), belt, level), "Dispatch the cup blocking the feeder.");
            Require(feeder.RemainingCount == 0 && inventory.GetBucketAtLayoutIndex(1) == waiting, "The feeder gets its freed outlet immediately.");
            Require(inventory.ReserveCount == 1, "The general queue waits without losing its cup.");
            Debug.Log("FEEDER_QUEUE_REGRESSION_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(rules); UnityEngine.Object.DestroyImmediate(level); }
        RunAccessPathRecall();
    }
    private static void RunAccessPathRecall()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("FeederRecallRegression");
        try
        {
            level.gridWidth = 26; level.gridHeight = 1;
            level.cells = Enumerable.Range(0, 26).Select(i => (byte)(i % 6 + 1)).ToArray();
            level.palette = new Color32[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };
            level.campaignTemplateVersion = 1; level.keyGates = true;
            level.EnsureBucketLayout(); level.EnsureConveyorMetadata();
            level.bucketLayout[3] = 254;
            level.SetConveyorDirection(3, ConveyorDirection.Left); level.SetConveyorBucketCount(3, 1);
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            inventory.TryDispenseAll(level);
            Require(inventory.GetBucketAtLayoutIndex(2) != null, "The feeder may enter the reserved aisle.");
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(26);
            belt.OnBucketReturned += inventory.ReturnBucket;
            var cup = inventory.GetBucketAtLayoutIndex(0);
            Require(inventory.TryTakeBucket(cup, belt, level), "Dispatch before recalling.");
            Require(belt.TryRecallBucket(cup), "Recall while the aisle is occupied.");
            Require(inventory.CountRemaining() == 26 && belt.Entries.Count == 0, "Recall preserves every cup, including overflow.");
            Require(inventory.Buckets.Select(b => b.LayoutIndex).Distinct().Count() == inventory.Buckets.Count, "Recall leaves no overlapping cups.");
            Debug.Log("FEEDER_RECALL_REGRESSION_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(rules); UnityEngine.Object.DestroyImmediate(level); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
