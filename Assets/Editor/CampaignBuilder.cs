using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CampaignBuilder
{
    private const string Root = "Assets/Levels/Campaign100";
    private const string DatabasePath = "Assets/Levels/LevelDatabase.asset";
    private static readonly string[] Atlases = { "01_Wilds", "02_Characters", "03_Journeys", "04_Wonders" };
    private static readonly string[] Titles = (
        "Snow Leopard|Autumn Stag|Moonlit Raccoon|Bamboo Panda|Forest Lynx|Island Dolphin|Coral Turtle|Sunken Ship|Seahorse Garden|Aurora Bear|Palace Peacock|River Kingfisher|Jungle Toucan|Sunset Flamingo|Phoenix|Emerald Dragon|Moonlit Unicorn|Mountain Griffin|Fire Salamander|Spirit Stag|" +
        "Desert Princess|Elven Archer|Dwarven Forge|Paladin|Sea Captain|Alchemist|Canyon Ranger|Forest Druid|Astronomer|Temple Explorer|Shieldmaiden|Dragon Rider|Town Musician|Desert Merchant|Emerald Queen|Shipwright|Potter|Beekeeper|Lighthouse Keeper|Sorceress|" +
        "Red Sails|Fjord Longship|Valley Airship|Mountain Train|Lavender Balloon|Lagoon Seaplane|Lighthouse Bay|Desert Caravan|Winter Carriage|Autumn Riverboat|Coastal Village|Maple Pagoda|Cloud Monastery|Sunflower Windmill|Forest Watermill|Venetian Bridge|Glass Garden|Stormy Cape|Market Street|Lake Castle|" +
        "Crystal Cavern|Desert Oasis|Mushroom Forest|Ancient Library|Floating Islands|Volcano Temple|Coral Palace|Winter Village|Forest Waterfall|Autumn Bridge|Star Observatory|Jungle Ruins|Moon Garden|Dragon Treasury|Sunken City|Golden Orchard|Mountain Lake|Festival Harbor|Sky Palace|Enchanted Door").Split('|');

    [MenuItem("Window/Sand/Campaign/Build 100 Levels")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database == null) throw new InvalidOperationException("Level database missing.");
        if (database.LevelCount > 100) { CampaignExpansionBuilder.Build(); return; }
        string backupPath = Root + "/OriginalDatabase.asset";
        var original = AssetDatabase.LoadAssetAtPath<LevelDatabase>(backupPath);
        if (original == null)
        {
            if (database.LevelCount != 22) throw new InvalidOperationException("Expected the audited 22 original levels.");
            original = UnityEngine.Object.Instantiate(database);
            original.name = "OriginalDatabase";
            AssetDatabase.CreateAsset(original, backupPath);
        }
        var levels = new List<LevelData>();
        var textures = new Dictionary<int, Texture2D>();
        try
        {
            for (int index = 0; index < 100; index++)
            {
                string path = Root + "/Level_" + (index + 1).ToString("000") + ".asset";
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = index < original.LevelCount
                        ? UnityEngine.Object.Instantiate(original.levels[index])
                        : ScriptableObject.CreateInstance<LevelData>();
                    level.name = Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(level, path);
                }
                if (index >= original.LevelCount)
                {
                    int number = index - original.LevelCount;
                    int atlasIndex = number / 20;
                    if (!textures.TryGetValue(atlasIndex, out Texture2D atlas))
                    {
                        atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        string source = "Assets/Images/Campaign/" + Atlases[atlasIndex] + ".png";
                        if (!atlas.LoadImage(File.ReadAllBytes(source))) throw new InvalidOperationException("Cannot read " + source);
                        textures.Add(atlasIndex, atlas);
                    }
                    int tile = number % 20;
                    int left = Mathf.RoundToInt((tile % 5) * atlas.width / 5f);
                    int right = Mathf.RoundToInt((tile % 5 + 1) * atlas.width / 5f);
                    int bottom = Mathf.RoundToInt((3 - tile / 5) * atlas.height / 4f);
                    int top = Mathf.RoundToInt((4 - tile / 5) * atlas.height / 4f);
                    Color[] colors = atlas.GetPixels(left, bottom, right - left, top - bottom);
                    Color32[] sourcePixels = Array.ConvertAll(colors, c => (Color32)c);
                    var settings = ImageProcessor.Settings.Create(64, 64, 12);
                    settings.autoColorCount = false;
                    settings.autoBackgroundColor = false;
                    settings.backgroundColor = new Color32(32, 62, 71, 255);
                    var processed = ImageProcessor.Process(sourcePixels, right - left, top - bottom, settings);
                    level.gridWidth = processed.width;
                    level.gridHeight = processed.height;
                    level.palette = processed.palette;
                    level.cells = processed.cellIndices;
                    level.subGrainsPerAxis = 1;
                    level.usesTiles = false;
                    level.tileSources = Array.Empty<TileSourceData>();
                    level.displayTitle = Titles[number];
                }
                var rules = AssetDatabase.LoadAssetAtPath<LevelSegmentRules>(Root + "/Rules_" + (index + 1).ToString("000") + ".asset");
                if (rules == null)
                {
                    rules = ScriptableObject.CreateInstance<LevelSegmentRules>();
                    AssetDatabase.CreateAsset(rules, Root + "/Rules_" + (index + 1).ToString("000") + ".asset");
                }
                CampaignProgression.Configure(level, rules, index);
                EditorUtility.SetDirty(level);
                EditorUtility.SetDirty(rules);
                levels.Add(level);
            }
            // Publish the complete database only after every asset has been generated successfully.
            database.levels = levels.ToArray();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
        finally { foreach (Texture2D texture in textures.Values) UnityEngine.Object.DestroyImmediate(texture); }
        Validate();
    }

    [MenuItem("Window/Sand/Campaign/Validate 100 Levels")]
    public static void Validate()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database != null && database.LevelCount == 300) { CampaignExpansionBuilder.Audit(); return; }
        Require(database != null && database.LevelCount == 100, "Exactly 100 levels required.");
        var fingerprints = new HashSet<string>();
        var report = new List<string> { "# Campaign validation", "", "Level | Colors | Grains (1x1) | Buckets | Reserve | Route cleared", "--- | --- | --- | --- | --- | ---" };
        int total = 0;
        for (int index = 0; index < database.LevelCount; index++)
        {
            LevelData level = database.levels[index];
            Require(level != null && level.cells.Length == level.gridWidth * level.gridHeight, "Invalid grid " + index);
            Require(level.cells.All(c => c > 0 && c <= level.palette.Length), "Missing background or invalid color " + index);
            string fingerprint = Convert.ToBase64String(level.cells) + string.Join(",", level.palette.Select(c => c.ToString()));
            Require(fingerprints.Add(fingerprint), "Duplicate painting " + index);
            if (index >= 22) Require(level.palette.Length >= 8, "Painting lost its color range " + index);
            var inventory = new BucketInventory();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(level.campaignSeed);
                inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            }
            finally { UnityEngine.Random.state = randomState; }
            int reserveCount = inventory.ReserveCount;
            var queue = ColorQuantizer.GenerateLevelBucketQueue(level.cells, level.gridWidth, level.gridHeight, 1, level.bucketSettingsOverride.maxGrainsPerBucket);
            foreach (byte color in level.GetColorsUsedInLevel())
            {
                Require(queue.Where(b => b.colorIndex == color).Sum(b => b.capacity) == level.CountLevelCellsForColor(color), "Grain budget mismatch " + index);
                Require(CampaignIterationChecks.AllBuckets(inventory).Sum(b => b.RemainingForColor(color)) == level.CountLevelCellsForColor(color), "Modified bucket budget mismatch " + index);
            }
            var beltObject = new GameObject("CampaignValidatorBelt");
            var belt = beltObject.AddComponent<ConveyorBelt>();
            try
            {
                belt.Configure(inventory.TotalBucketCount);
                belt.ConfigureLevel(level);
                int removed = 0;
                while (inventory.CountRemaining() > 0 && removed < 1000)
                {
                    inventory.TryDispenseAll(level);
                    Require(inventory.Buckets.Select(b => b.LayoutIndex).Distinct().Count() == inventory.Buckets.Count, "Overlapping buckets " + index);
                    Bucket next = inventory.Buckets.OrderByDescending(b => inventory.IsCrateCovered(b)).ThenByDescending(b => b.KeyId)
                        .ToArray().FirstOrDefault(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                    Require(next != null, "Blocked inventory, level " + (index + 1) + ", after " + removed + " moves");
                    foreach (var entry in belt.Entries) inventory.HandleBucketFilled(entry.Bucket);
                    removed += belt.Entries.Count;
                    belt.ResetConveyor();
                }
                Require(removed == inventory.TotalBucketCount && inventory.CountRemaining() == 0, "Lost reserve buckets " + index);
                total += removed;
                report.Add($"{index + 1} | {level.palette.Length} | {level.CountTotalLevelCells()} | {removed} | {reserveCount} | PASS");
            }
            finally { UnityEngine.Object.DestroyImmediate(beltObject); }
        }
        Directory.CreateDirectory("tasks");
        report.Add($"\nPASS: 100 unique paintings; {total} buckets; valid palettes, backgrounds, previews, capacities and complete inventory routes.\n\nThis validates inventory routes and grain budgets. It does not simulate sand physics or certify mobile performance.");
        File.WriteAllLines("tasks/campaign-validation.md", report);
        Debug.Log("CAMPAIGN_VALIDATION_PASS: 100 levels; " + total + " buckets.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("Window/Sand/Campaign/Update Rules and Validate")]
    public static void UpdateRules()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        for (int i = 0; i < database.LevelCount; i++)
        {
            LevelData level = database.levels[i];
            if (i < 10 || level.campaignTemplateVersion > 0) continue;
            CampaignProgression.Configure(level, level.bucketSettingsOverride, i);
            EditorUtility.SetDirty(level);
            EditorUtility.SetDirty(level.bucketSettingsOverride);
        }
        AssetDatabase.SaveAssets();
        Validate();
        CampaignMechanicsChecks.Run();
    }

    public static void VerifyCampaign()
    {
        UpdateRules();
        CampaignSimulationChecks.Run();
    }
}
