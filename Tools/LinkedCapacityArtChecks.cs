// Run only from Assets/Editor in the disposable validation project.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LinkedCapacityArtChecks
{
    static string Output => Environment.GetEnvironmentVariable("SAND_GAME_FIX_OUTPUT");
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        try
        {
            RepairPaintings();
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Paintings repaired; checking linked pairs.");
            CampaignLinkedRouteChecks.Run();
            LinkedBucketPlacementChecks.Run();
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Linked pair checks passed; checking conveyor capacity.");
            CampaignMechanicsChecks.Run();
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
            var root = new GameObject("AllCampaignCapacityCheck");
            try
            {
                var belt = root.AddComponent<ConveyorBelt>();
                for (int i = 0; i < db.LevelCount; i++)
                {
                    var level = CampaignRunFactory.Create(db.GetLevel(i), i);
                    try
                    {
                        Require(level.beltCapacity == 5, "Campaign capacity at " + (i + 1));
                        belt.ConfigureLevel(level);
                        Require(belt.Capacity == 5, "Runtime capacity at " + (i + 1));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
                }
                Require(!CampaignProgression.IsLessonStart(149), "Obsolete four-slot lesson remains.");
                File.WriteAllText(Path.Combine(Output, "status.txt"), "PASS\nLinked front access, generated pair routes, mechanics, five-slot runtime capacity across " + db.LevelCount + " levels, and repaired paintings 361-365.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "status.txt"), "FAIL\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    public static void VerifyAllLinked()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        try
        {
            LinkedBucketPlacementChecks.Run();
            File.WriteAllText(Path.Combine(Output, "all-linked-status.txt"), "PASS\nEvery campaign level with linked buckets checked with fixed seeds; column-front access, pair counts and full inventory dispatch routes verified.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(Output, "all-linked-status.txt"), "FAIL\n" + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void RepairOnly()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        try
        {
            RepairPaintings();
            int routes = 0;
            for (int number = 361; number <= 365; number++)
            for (int run = 0; run < 8; run++)
            {
                var source = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/Campaign1500/Ordinary_" + number.ToString("0000") + ".asset");
                var level = CampaignRunFactory.Create(source, number - 1);
                var root = new GameObject("RepairedPaintingRoute");
                try
                {
                    level.campaignSeed = unchecked(7319 + number * 104729 + run);
                    UnityEngine.Random.InitState(level.campaignSeed);
                    var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
                    var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
                    Require(belt.Capacity == 5, "Repaired level lost five slots.");
                    if (level.linkedBuckets)
                        Require(inventory.Buckets.Count(b => b.LinkedPartner != null) / 2 >= CampaignProgression.LinkedPairCount(number), "Repaired level lost linked pairs: " + number);
                    int moves = 0;
                    while (inventory.CountRemaining() > 0 && moves++ < 500)
                    {
                        int settling = 0;
                        while (inventory.NeedsSettling && inventory.TryDispenseAll(level) && ++settling < 100) { }
                        bool moved = inventory.Buckets.OrderByDescending(b => b.KeyId).ToArray()
                            .Any(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                        Require(moved, "Repaired level has a blocked route: " + number + "/" + run);
                        foreach (var entry in belt.Entries.ToArray())
                        {
                            entry.Bucket.ForceFillToCapacity(); inventory.HandleBucketFilled(entry.Bucket);
                            belt.MarkBucketFilledByBooster(entry.Bucket);
                        }
                        belt.UpdateBelt(.01f);
                    }
                    Require(inventory.CountRemaining() == 0, "Repaired level did not clear: " + number);
                    routes++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(level.bucketSettingsOverride); UnityEngine.Object.DestroyImmediate(level); }
            }
            File.WriteAllText(Path.Combine(Output, "art-status.txt"), "PASS\nNo edge-extension padding on paintings 361-365; " + routes + " deterministic inventory routes cleared.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(Output, "art-status.txt"), "FAIL\n" + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void RepairPaintings()
    {
        var atlas = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            Require(atlas.LoadImage(File.ReadAllBytes("Assets/Images/Campaign1500/ordinary_03.png")), "Missing source atlas.");
            CampaignAtlasSlicer.ClearCache();
            for (int number = 361; number <= 365; number++)
            {
                string path = "Assets/Levels/Campaign1500/Ordinary_" + number.ToString("0000") + ".asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                Require(level != null, "Missing level " + number);
                int tile = number - 346;
                RectInt crop = CampaignAtlasSlicer.TileRect(atlas, tile, 5, 5);
                int top = atlas.height - crop.yMax, bottom = atlas.height - crop.y;
                Require(top >= 755 && bottom <= 975, "Crop includes another atlas row at " + number + ": " + top + ".." + bottom);
                // This atlas row is shorter than the neighboring rows. Sample a
                // real square of art instead of extending its top/bottom pixels.
                int edge = Mathf.Min(crop.width, crop.height);
                crop = new RectInt(crop.x + (crop.width - edge) / 2, crop.y + (crop.height - edge) / 2, edge, edge);
                var source = Array.ConvertAll(atlas.GetPixels(crop.x, crop.y, crop.width, crop.height), c => (Color32)c);
                var settings = ImageProcessor.Settings.Create(64, 64, 10);
                settings.autoColorCount = false; settings.autoBackgroundColor = true;
                var art = ImageProcessor.Process(source, crop.width, crop.height, settings);
                Require(art.cellIndices.Length == 4096 && art.cellIndices.All(c => c > 0 && c <= art.palette.Length), "Invalid repaired pixels.");
                level.gridWidth = art.width; level.gridHeight = art.height;
                level.palette = art.palette; level.cells = art.cellIndices;
                level.buckets = ColorQuantizer.GenerateBucketSpawns(level.cells, level.palette.Length);
                EditorUtility.SetDirty(level);
                var preview = ColorQuantizer.BuildPreviewTexture(level.gridWidth, level.gridHeight, level.cells, level.palette, 4);
                File.WriteAllBytes(Path.Combine(Output, "level-" + number + "-after.png"), preview.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(preview);
            }
            AssetDatabase.SaveAssets();
        }
        finally { CampaignAtlasSlicer.ClearCache(); UnityEngine.Object.DestroyImmediate(atlas); }
    }
}
