using System;
using System.Linq;
using UnityEngine;

public static class CampaignLinkedRouteChecks
{
    public static void Run()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        var root = new GameObject("LinkedRouteRegression");
        try
        {
            level.gridWidth = 3; level.gridHeight = 1;
            level.cells = new byte[] { 1, 2, 3 };
            level.palette = new Color32[] { Color.red, Color.green, Color.blue };
            level.EnsureBucketLayout();
            for (int i = 0; i < 25; i++) level.bucketLayout[i] = 255;
            level.bucketLayout[0] = level.bucketLayout[5] = level.bucketLayout[10] = 0;
            rules.maxGrainsPerBucket = 1;
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, rules);
            var a = inventory.GetBucketAtLayoutIndex(5);
            var b = inventory.GetBucketAtLayoutIndex(10);
            Require(a != null && b != null, "Two cups on the narrow route.");
            a.LinkedPartner = b; b.LinkedPartner = a;
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            Require(inventory.TryTakeBucket(inventory.GetBucketAtLayoutIndex(0), belt, level), "Remove the front cup.");
            Require(!BucketRouteAccess.IsAccessible(10, level, inventory), "A stationary cup really blocks the rear cup.");
            Require(inventory.IsBucketAccessible(a, level) && inventory.IsBucketAccessible(b, level), "Both linked cups vacate their cells together.");
            Require(inventory.TryTakeBucket(b, belt, level) && !inventory.Buckets.Contains(a) && !inventory.Buckets.Contains(b), "The linked pair dispatches atomically through the same exit.");
            Debug.Log("LINKED_ROUTE_REGRESSION_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(rules); UnityEngine.Object.DestroyImmediate(level); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
