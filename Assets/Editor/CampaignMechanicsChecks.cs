using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CampaignMechanicsChecks
{
    [MenuItem("Window/Sand/Campaign/Check Mechanics")]
    public static void Run()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("MechanicsCheck");
        try
        {
            level.gridWidth = level.gridHeight = 8;
            level.palette = new[] { new Color32(220, 40, 50, 255), new Color32(40, 140, 230, 255) };
            level.cells = Enumerable.Range(0, 64).Select(i => (byte)(i < 32 ? 1 : 2)).ToArray();
            level.campaignLevel = true;
            level.beltCapacity = 4;
            rules.maxGrainsPerBucket = 2;
            var inventory = new BucketInventory();
            inventory.BuildFromLevel(level, 1, rules);
            Require(inventory.ReserveCount == 7 && inventory.Buckets.Count == 25, "Overflow stays in reserve");
            var belt = root.AddComponent<ConveyorBelt>();
            belt.Configure(inventory.TotalBucketCount);
            belt.ConfigureLevel(level);
            belt.OnBucketReturned += inventory.ReturnBucket;
            Bucket first = inventory.Buckets.First(b => inventory.IsBucketAccessible(b, level));
            first.SetModifiers(1, false);
            Require(!inventory.TryTakeBucket(first, belt, level), "Frozen buckets cannot move");
            Bucket other = inventory.Buckets.First(b => inventory.IsBucketAccessible(b, level));
            Require(inventory.TryTakeBucket(other, belt, level) && first.FrostMoves == 0, "A successful move melts one ice layer");
            Require(other.TryAddGrain(other.ColorIndex), "Partial fill accepted");
            Require(!other.TryAddGrain((byte)(other.ColorIndex == 1 ? 2 : 1)), "Wrong color rejected");
            Require(belt.TryRecallBucket(other) && other.CurrentFill == 1, "Recall preserves partial fill");
            Require(inventory.Buckets.Select(b => b.LayoutIndex).Distinct().Count() == inventory.Buckets.Count, "Recall does not overlap buckets");
            Dictionary<byte, int> before = Remaining(inventory);
            Require(BoosterMechanics.TryShuffleInventoryColors(inventory), "Shuffle runs");
            Dictionary<byte, int> after = Remaining(inventory);
            Require(before.All(pair => after[pair.Key] == pair.Value), "Shuffle preserves remaining sand per color");
            for (int i = 0; i < ConveyorBelt.MaxOnBelt; i++)
            {
                Bucket next = inventory.Buckets.First(b => inventory.IsBucketAccessible(b, level));
                Require(inventory.TryTakeBucket(next, belt, level), "Available slot accepts bucket");
            }
            Require(!belt.CanAcceptBucket() && belt.IsFull(), "Legacy four-slot level now allows five buckets");
            Debug.Log("CAMPAIGN_MECHANICS_PASS: overflow, frost, wrong-color rejection, partial recall, shuffle conservation, belt capacity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Object.DestroyImmediate(rules);
        }
    }

    private static Dictionary<byte, int> Remaining(BucketInventory inventory)
    {
        return inventory.Buckets.Concat(inventory.ReservedBuckets).Concat(inventory.ConveyorSlots.SelectMany(s => s.ReservedBuckets))
            .GroupBy(b => b.ColorIndex).ToDictionary(g => g.Key, g => g.Sum(b => b.RemainingCapacity));
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
