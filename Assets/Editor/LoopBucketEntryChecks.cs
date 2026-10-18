using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LoopBucketEntryChecks
{
    public static void Run()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var level = CampaignRunFactory.Create(db.levels[80], 80);
        var beltObject = new GameObject("LoopEntryChecks");
        try
        {
            var inventory = new BucketInventory();
            inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            var cups = (List<Bucket>)typeof(BucketInventory).GetField("buckets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            cups.Clear();
            ((Queue<Bucket>)typeof(BucketInventory).GetField("reserve", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory)).Clear();
            foreach (var feeder in inventory.ConveyorSlots) feeder.ReservedBuckets.Clear();
            var left = new Bucket(1, 10, 16);
            var right = new Bucket(1, 10, 17);
            var existing = new Bucket(1, 10, 0);
            cups.Add(left); cups.Add(right); cups.Add(existing);
            inventory.TryDispenseAll(level);
            Require(left.LayoutIndex == 11 && right.LayoutIndex == 12, "Buckets below the compact loop must rise into its empty cells");
            Require(existing.LayoutIndex == 0, "Settling must not move buckets already on the loop");
            inventory.TryDispenseAll(level);
            Require(left.LayoutIndex == 11 && right.LayoutIndex == 12, "Entering buckets stop on the loop until the next move");
            var lower = new Bucket(1, 10, 16);
            cups.Add(lower);
            var belt = beltObject.AddComponent<ConveyorBelt>();
            belt.Configure(inventory.TotalBucketCount);
            belt.ConfigureLevel(level);
            Require(inventory.TryTakeBucket(existing, belt, level, true), "First belt step rejected");
            Require(left.LayoutIndex == 10 && right.LayoutIndex == 11 && lower.LayoutIndex == 16,
                "Existing loop buckets move first and keep the lower bucket waiting when its entry is occupied");
            Require(inventory.TryTakeBucket(right, belt, level, true), "Second belt step rejected");
            Require(left.LayoutIndex == 5 && lower.LayoutIndex == 11,
                "Lower bucket enters after existing loop buckets have moved");
            Debug.Log("LOOP_BUCKET_ENTRY_PASS: lower buckets enter empty loop cells after existing belt buckets move, and wait for the next belt step.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(beltObject);
            UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride);
            UnityEngine.Object.DestroyImmediate(level);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
