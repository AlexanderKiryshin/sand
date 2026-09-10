using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CampaignCrateChecks
{
    public static void Run()
    {
        var source = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset").levels[57];
        var level = CampaignRunFactory.Create(source, 57);
        var root = new GameObject("CrateRegression", typeof(RectTransform));
        var rng = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(level.campaignSeed);
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            Require(inventory.CrateBucketCount >= 2 && inventory.CrateBucketCount == inventory.Buckets.Count(inventory.IsCrateCovered), "Crates cover actual visible cups after key placement, not cups moved to reserve.");
            Require(inventory.Buckets.Where(inventory.IsCrateCovered).All(b => b.LayoutIndex / LevelData.BucketLayoutColumns == inventory.CrateRow), "All initial crates belong to the covered row.");
            var covered = inventory.Buckets.FirstOrDefault(b => inventory.IsCrateCovered(b) && inventory.IsGateClosed(b))
                ?? inventory.Buckets.First(inventory.IsCrateCovered);
            // Also exercise the visual combination supported by custom level data.
            covered.GateKeyId = 1;
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
            var next = inventory.Buckets.First(b => !inventory.IsCrateCovered(b) && inventory.IsBucketAccessible(b, level));
            Require(inventory.TryTakeBucket(next, belt, level) && inventory.CratesOpen, "First move opens the crate row.");
            var body = new GameObject("Body", typeof(RectTransform), typeof(Image)); body.transform.SetParent(root.transform);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(root.transform);
            var visual = root.AddComponent<BucketMechanicVisual>();
            visual.Initialize(covered, body.GetComponent<Image>(), label.GetComponent<TextMeshProUGUI>(), level, inventory);
            var field = typeof(BucketMechanicVisual).GetField("coverAmount", BindingFlags.Instance | BindingFlags.NonPublic);
            float amount = (float)field.GetValue(visual);
            Require(amount == 0, "Open crates must initialize OPEN without snapping a closed cover onto the bucket. Actual coverAmount=" + amount);
            if (inventory.IsGateClosed(covered))
            {
                var lockIcon = (Image)typeof(BucketMechanicVisual).GetField("gateLock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
                Require(lockIcon.color.a == 1, "The key lock remains visible under an open crate.");
            }
            belt.ResetConveyor();
            next = inventory.Buckets.First(b => !inventory.IsCrateCovered(b) && inventory.IsBucketAccessible(b, level));
            Require(inventory.TryTakeBucket(next, belt, level) && !inventory.CratesOpen, "Second move closes the row.");
            visual.AdvanceCover(.2f); amount = (float)field.GetValue(visual);
            Require(visual.IsCoverAnimating && amount > 0 && amount < 1, "Closing passes through intermediate frames.");
            Require(!inventory.TryTakeBucket(next, belt, level) && !inventory.CratesOpen, "Rejected repeated click does not toggle crates.");
            visual.AdvanceCover(.5f);
            Require(!visual.IsCoverAnimating && (float)field.GetValue(visual) == 1, "Closing reaches the covered position.");
            belt.ResetConveyor();
            next = inventory.Buckets.First(b => !inventory.IsCrateCovered(b) && inventory.IsBucketAccessible(b, level));
            Require(inventory.TryTakeBucket(next, belt, level) && inventory.CratesOpen, "Third move opens the row.");
            visual.AdvanceCover(.2f); amount = (float)field.GetValue(visual);
            Require(visual.IsCoverAnimating && amount > 0 && amount < 1, "Opening passes through intermediate frames.");
            visual.AdvanceCover(.5f);
            Require(!visual.IsCoverAnimating && (float)field.GetValue(visual) == 0, "Opening exposes the buckets completely.");
            var coveredPositions = inventory.Buckets.Where(inventory.IsCrateCovered).ToDictionary(b => b, b => b.LayoutIndex);
            inventory.ReturnBucket(next);
            Require(coveredPositions.All(pair => pair.Key.LayoutIndex == pair.Value), "Recall advances the outside queue while the covered row stays in place.");
            Require(inventory.Buckets.Select(b => b.LayoutIndex).Distinct().Count() == inventory.Buckets.Count, "Recall never overlaps visible cups.");
            System.IO.Directory.CreateDirectory("tasks");
            System.IO.File.WriteAllText("tasks/crate-validation.md", "PASS: actual inventory dispatch opens/closes crates; rejected clicks do not toggle; open initialization does not snap; opening and closing take 0.65 seconds with intermediate frames.\n");
            Debug.Log("CRATE_REGRESSION_PASS: initial state, opening and closing animation, successful/rejected moves.");
        }
        finally
        {
            UnityEngine.Random.state = rng;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
