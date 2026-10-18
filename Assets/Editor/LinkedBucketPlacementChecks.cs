using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LinkedBucketPlacementChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [MenuItem("Window/Sand/Campaign/Check Stretchable Linked Buckets")]
    public static void Run()
    {
        var random = UnityEngine.Random.state;
        try
        {
            CheckEverySeat();
            CheckIndependentMovement();
            CheckRecall();
            CheckBlockedPairs();
            CheckMesh();
            LinkedBucketPresentationChecks.Run();
            CampaignLinkedRouteChecks.Run();
            CheckCampaignRoutes();
            File.WriteAllText("tasks/linked-columns-checks.txt", "PASS: all 100 combinations of rows in adjacent columns; independent movement; atomic dispatch; stretched mesh; links behind cups; campaign routes.");
            Debug.Log("STRETCHABLE_LINKED_BUCKETS_PASS");
        }
        finally { UnityEngine.Random.state = random; }
    }

    private static void CheckEverySeat()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
        try
        {
            level.EnsureBucketLayout();
            rules.segmentId = "Rules_38";
            level.bucketSettingsOverride = rules;
            for (int col = 0; col < LevelData.BucketLayoutColumns - 1; col++)
            for (int rowA = 0; rowA < LevelData.BucketLayoutRows; rowA++)
            for (int rowB = 0; rowB < LevelData.BucketLayoutRows; rowB++)
            {
                int seatA = rowA * LevelData.BucketLayoutColumns + col;
                int seatB = rowB * LevelData.BucketLayoutColumns + col + 1;
                for (int i = 0; i < level.bucketLayout.Length; i++) level.bucketLayout[i] = LevelData.BucketLayoutWall;
                level.bucketLayout[seatA] = level.bucketLayout[seatB] = (byte)0;
                var inventory = new BucketInventory();
                var a = new Bucket(1, 10, seatA);
                var b = new Bucket(2, 10, seatB);
                ((List<Bucket>)inventory.Buckets).AddRange(new[] { a, b });
                var seats = (List<Vector2Int>)Call(inventory, "CollectLinkedSeats", level);
                Require(seats.Count == 1 && seats[0] == new Vector2Int(seatA, seatB),
                    "Pair must fit rows " + rowA + "/" + rowB + " in columns " + col + "/" + (col + 1));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(rules); }
    }

    private static void CheckIndependentMovement()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var root = new GameObject("Stretch movement check");
        try
        {
            level.EnsureBucketLayout();
            for (int i = 0; i < level.bucketLayout.Length; i++)
                level.bucketLayout[i] = i % LevelData.BucketLayoutColumns <= 1 ? (byte)0 : LevelData.BucketLayoutWall;
            var inventory = new BucketInventory();
            var cups = (List<Bucket>)inventory.Buckets;
            var a = new Bucket(1, 10, 10);
            var b = new Bucket(2, 10, 21);
            a.LinkedPartner = b; b.LinkedPartner = a;
            var blockers = new[] { new Bucket(3, 10, 0), new Bucket(3, 10, 1), new Bucket(3, 10, 5), new Bucket(3, 10, 11), new Bucket(3, 10, 15) };
            cups.AddRange(blockers); cups.Add(a); cups.Add(b);
            Call(inventory, "AdvanceBuckets", level);
            Require(a.LayoutIndex == 10 && b.LayoutIndex == 11, "One half advances independently while the other remains blocked");
            cups.Remove(blockers[0]); cups.Remove(blockers[2]);
            Call(inventory, "AdvanceBuckets", level);
            Require(a.LayoutIndex == 0 && b.LayoutIndex == 11, "Advancing a half must preserve its column and leave its partner in place");
            Require(!inventory.IsBucketAccessible(a, level), "The pair waits until both halves can leave");
            cups.Remove(blockers[1]); cups.Remove(blockers[3]); cups.Remove(blockers[4]);
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            Require(inventory.IsBucketAccessible(a, level) && inventory.TryTakeBucket(b, belt, level), "Either half dispatches the accessible pair");
            Require(cups.Count == 0 && belt.Entries.Count == 2, "Dispatch removes both halves atomically");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void CheckRecall()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        var root = new GameObject("Linked recall check");
        try
        {
            level.EnsureBucketLayout();
            var inventory = new BucketInventory();
            typeof(BucketInventory).GetField("activeLevel", Hidden).SetValue(inventory, level);
            var a = new Bucket(1, 10, 0); var b = new Bucket(2, 10, 21);
            a.LinkedPartner = b; b.LinkedPartner = a;
            ((List<Bucket>)inventory.Buckets).AddRange(new[] { a, b });
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(3);
            belt.OnBucketReturned += inventory.ReturnBucket;
            Require(inventory.TryTakeBucket(a, belt, level) && belt.TryRecallBucket(b), "The stretched pair can be recalled together");
            int ticks = 0;
            while (inventory.TryDispenseAll(level) && ++ticks < 20) { }
            Require(inventory.Buckets.Count == 2 && inventory.ReserveCount == 0 && belt.Entries.Count == 0
                && Mathf.Abs(a.LayoutIndex % LevelData.BucketLayoutColumns - b.LayoutIndex % LevelData.BucketLayoutColumns) == 1,
                "Recalled halves settle in neighboring columns");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void CheckBlockedPairs()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        try
        {
            level.EnsureBucketLayout();
            for (int i = 0; i < level.bucketLayout.Length; i++) level.bucketLayout[i] = LevelData.BucketLayoutWall;
            foreach (int index in new[] { 0, 1, 5, 6 }) level.bucketLayout[index] = 0;
            var inventory = new BucketInventory();
            var a = new Bucket(1, 10, 0); var b = new Bucket(2, 10, 6);
            var c = new Bucket(1, 10, 1); var d = new Bucket(2, 10, 5);
            ((List<Bucket>)inventory.Buckets).AddRange(new[] { a, b, c, d });
            a.LinkedPartner = b; b.LinkedPartner = a;
            c.LinkedPartner = d; d.LinkedPartner = c;
            Require(!(bool)Call(inventory, "CanClearLinkedLayout", level), "Generation rejects pairs that block each other's rear half");
            Require(a.LayoutIndex == 0 && b.LayoutIndex == 6 && inventory.Buckets.Count == 4,
                "Generation validation preserves live cups and positions");
            a.LinkedPartner = c; c.LinkedPartner = a;
            b.LinkedPartner = d; d.LinkedPartner = b;
            Require((bool)Call(inventory, "CanClearLinkedLayout", level), "Generation accepts a clear dispatch order");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void CheckMesh()
    {
        var root = new GameObject("Diagonal mesh check", typeof(RectTransform), typeof(LinkedBucketLinkGraphic));
        try
        {
            var line = root.GetComponent<LinkedBucketLinkGraphic>();
            foreach (Vector2 end in new[] { new Vector2(112, 448), new Vector2(112, -448), new Vector2(-112, 448), new Vector2(0, 448), new Vector2(112, 0) })
            {
                line.SetLink(Vector2.zero, end, 96, Color.red, Color.blue);
                using (var mesh = new VertexHelper())
                {
                    typeof(LinkedBucketLinkGraphic).GetMethod("OnPopulateMesh", Hidden).Invoke(line, new object[] { mesh });
                    Require(mesh.currentVertCount == 8, "Both colored halves remain visible for every direction");
                    var a = new UIVertex(); var b = new UIVertex();
                    mesh.PopulateUIVertex(ref a, 0); mesh.PopulateUIVertex(ref b, 1);
                    Vector2 start = ((Vector2)a.position + (Vector2)b.position) * .5f;
                    mesh.PopulateUIVertex(ref a, 6); mesh.PopulateUIVertex(ref b, 7);
                    Vector2 finish = ((Vector2)a.position + (Vector2)b.position) * .5f;
                    Require(Mathf.Abs(Vector2.Distance(start, finish) - end.magnitude) < .01f, "Link stretches to the full distance between centers");
                    Require((Vector2.Distance(start, Vector2.zero) < .01f && Vector2.Distance(finish, end) < .01f)
                        || (Vector2.Distance(finish, Vector2.zero) < .01f && Vector2.Distance(start, end) < .01f), "No gap opens at either cup");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckCampaignRoutes()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        int runs = 0, diagonal = 0;
        foreach (int number in Enumerable.Range(CampaignProgression.LinkedLevel, db.LevelCount - CampaignProgression.LinkedLevel + 1))
        {
            if (number > db.LevelCount) continue;
            for (int run = 0; run < (number <= 100 ? 8 : 2); run++)
            {
                var level = CampaignRunFactory.Create(db.levels[number - 1], number - 1);
                var root = new GameObject("Linked campaign route check");
                try
                {
                    level.campaignSeed = number == 325 && run == 0 ? -215794952 : unchecked(7319 + number * 104729 + run);
                    UnityEngine.Random.InitState(level.campaignSeed);
                    if (!level.linkedBuckets) continue;
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    var pairs = inventory.Buckets.Where(b => b.LinkedPartner != null).ToArray();
                    Require(pairs.Length / 2 >= (level.loopInventory ? 1 : CampaignProgression.LinkedPairCount(level.GetProgressionIndex(number - 1) + 1)), "Missing pairs on level " + number + ": got=" + pairs.Length / 2 + ", seed=" + level.campaignSeed + ", segment=" + level.bucketSettingsOverride.segmentId);
                    Require(pairs.All(b => Mathf.Abs(b.LayoutIndex % LevelData.BucketLayoutColumns - b.LinkedPartner.LayoutIndex % LevelData.BucketLayoutColumns) == 1), "Generated pairs stay in adjacent columns");
                    diagonal += pairs.Count(b => b.LayoutIndex / LevelData.BucketLayoutColumns != b.LinkedPartner.LayoutIndex / LevelData.BucketLayoutColumns) / 2;
                    var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(level.beltCapacity);
                    int moves = 0;
                    while (inventory.CountRemaining() > 0 && moves++ < 500)
                    {
                        // Match GameManager.LateUpdate: finish rear/outlet movement before another click.
                        int settling = 0;
                        while (inventory.NeedsSettling && inventory.TryDispenseAll(level) && ++settling < 100) { }
                        bool dispatched = inventory.Buckets.OrderByDescending(b => b.KeyId).ToArray()
                            .Any(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                        Require(dispatched, "Blocked route: level " + number + ", run " + run + ": " + string.Join(",", inventory.Buckets.Select(b => b.LayoutIndex + ":link" + b.LinkedPartner?.LayoutIndex + ":key" + b.KeyId + ":gate" + b.GateKeyId)));
                        foreach (var entry in belt.Entries.ToArray())
                        {
                            entry.Bucket.ForceFillToCapacity();
                            inventory.HandleBucketFilled(entry.Bucket);
                            belt.MarkBucketFilledByBooster(entry.Bucket);
                        }
                        belt.UpdateBelt(.01f);
                    }
                    Require(inventory.CountRemaining() == 0, "Campaign route must finish");
                    runs++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        Require(diagonal > 0, "Campaign generation must use different rows");
        Debug.Log("LINKED_COLUMN_CAMPAIGN_ROUTES_PASS: runs=" + runs + ", diagonal pairs=" + diagonal);
    }

    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

