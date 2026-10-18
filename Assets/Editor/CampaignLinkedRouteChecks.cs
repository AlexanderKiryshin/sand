using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CampaignLinkedRouteChecks
{
    public static void Run()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var root = new GameObject("LinkedRouteRegression");
        try
        {
            level.EnsureBucketLayout();
            for (int i = 0; i < level.bucketLayout.Length; i++) level.bucketLayout[i] = LevelData.BucketLayoutWall;
            level.bucketLayout[0] = level.bucketLayout[5] = 0;
            var inventory = new BucketInventory();
            var a = new Bucket(1, 10, 0);
            var b = new Bucket(2, 10, 5);
            a.LinkedPartner = b; b.LinkedPartner = a;
            ((List<Bucket>)inventory.Buckets).AddRange(new[] { a, b });
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            Require(!inventory.IsBucketAccessible(a, level) && !inventory.IsBucketAccessible(b, level), "A linked partner above a cup blocks the whole pair.");
            Require(!inventory.TryTakeBucket(a, belt, level) && !inventory.TryTakeBucket(b, belt, level)
                && inventory.Buckets.Count == 2 && belt.Entries.Count == 0, "Blocked dispatch must preserve both cups and belt slots.");
            level.bucketLayout[1] = 0;
            b.SetLayoutIndex(1);
            Require(inventory.IsBucketAccessible(a, level) && inventory.IsBucketAccessible(b, level), "Both column-front cups become available together.");
            Require(inventory.TryTakeBucket(b, belt, level) && !inventory.Buckets.Contains(a) && !inventory.Buckets.Contains(b)
                && belt.Entries.Count == 2, "Either available cup dispatches the pair atomically.");
            Debug.Log("LINKED_FRONT_ACCESS_REGRESSION_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
