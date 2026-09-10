using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Append-only art import and deterministic, route-checked campaign templates.</summary>
[InitializeOnLoad]
public static class CampaignExpansionBuilder
{
    private const string Root = "Assets/Levels/Campaign100";
    private const string DatabasePath = "Assets/Levels/LevelDatabase.asset";
    [Serializable] public class Manifest { public Atlas[] atlases; }
    [Serializable] public class Atlas { public string name; public SceneArt[] scenes; }
    [Serializable] public class SceneArt { public int number; public string title, theme; }
    static CampaignExpansionBuilder() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        const string request = "Temp/campaign300-request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (command == "build") Build();
            else if (command == "audit") Audit();
            else if (command == "routes") CampaignIterationChecks.Run();
            else if (command == "crates") CampaignCrateChecks.Run();
            else if (command == "analytics") { WebAnalyticsSetup.Configure(); CampaignAnalyticsChecks.Run(); }
            else if (command == "simulation") CampaignSimulationChecks.RunInEditor();
            else throw new ArgumentException(command);
            File.WriteAllText("tasks/campaign300-status.txt", "PASS " + command + " " + DateTime.Now.ToString("O"));
        }
        catch (Exception error) { File.WriteAllText("tasks/campaign300-status.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Window/Sand/Campaign/Append 200 Levels and Mix Themes")]
    public static void Build()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        Require(database != null && (database.LevelCount == 100 || database.LevelCount == 300), "Expected 100 or 300 campaign levels.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("tasks/campaign-expansion-art.json"));
        Require(manifest.atlases.Length == 8 && manifest.atlases.All(a => a.scenes.Length == 25), "Eight complete 25-scene atlases required.");
        foreach (var atlas in manifest.atlases) Require(File.Exists("Assets/Images/CampaignExpansion/" + atlas.name + ".png"), "Missing atlas " + atlas.name);
        // Back up original art once as independent assets. Re-running never shuffles a shuffled list.
        const string backup = "Assets/Levels/CampaignSource100";
        Directory.CreateDirectory(backup); AssetDatabase.Refresh();
        if (!File.Exists(backup + "/complete.txt"))
        {
            Require(database.LevelCount == 100, "Original art backup missing.");
            for (int i = 10; i < 100; i++)
                if (!File.Exists(backup + "/Art_" + (i + 1).ToString("000") + ".asset"))
                {
                    var copy = UnityEngine.Object.Instantiate(database.levels[i]);
                    AssetDatabase.CreateAsset(copy, backup + "/Art_" + (i + 1).ToString("000") + ".asset");
                }
            AssetDatabase.SaveAssets(); File.WriteAllText(backup + "/complete.txt", "Original art, levels 11-100. Do not regenerate from shuffled output.");
        }
        var levels = database.levels.Take(100).ToList();
        var mix = MixedOriginalOrder();
        for (int i = 10; i < 100; i++)
        {
            int sourceIndex = mix[i - 10];
            var source = AssetDatabase.LoadAssetAtPath<LevelData>(backup + "/Art_" + (sourceIndex + 1).ToString("000") + ".asset");
            CopyArt(source, levels[i]); levels[i].artTheme = OriginalTheme(sourceIndex);
        }
        foreach (var atlas in manifest.atlases)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes("Assets/Images/CampaignExpansion/" + atlas.name + ".png")), "Invalid atlas " + atlas.name);
                for (int tile = 0; tile < 25; tile++)
                {
                    var art = atlas.scenes[tile]; int index = art.number - 1;
                    string path = Root + "/Level_" + art.number.ToString("000") + ".asset";
                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
                    int left = Mathf.RoundToInt(tile % 5 * texture.width / 5f), right = Mathf.RoundToInt((tile % 5 + 1) * texture.width / 5f);
                    int bottom = Mathf.RoundToInt((4 - tile / 5) * texture.height / 5f), top = Mathf.RoundToInt((5 - tile / 5) * texture.height / 5f);
                    var colors = Array.ConvertAll(texture.GetPixels(left, bottom, right - left, top - bottom), c => (Color32)c);
                    var settings = ImageProcessor.Settings.Create(64, 64, 12);
                    settings.autoColorCount = false; settings.autoBackgroundColor = false;
                    settings.backgroundColor = new Color32(32, 62, 71, 255);
                    var processed = ImageProcessor.Process(colors, right - left, top - bottom, settings);
                    level.gridWidth = processed.width; level.gridHeight = processed.height;
                    level.cells = processed.cellIndices; level.palette = processed.palette; level.subGrainsPerAxis = 1;
                    level.displayTitle = art.title; level.artTheme = art.theme; level.usesTiles = false;
                    level.tileSources = Array.Empty<TileSourceData>();
                    Require(index == levels.Count, "Sequential, append-only numbering required."); levels.Add(level);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        var structures = new HashSet<string>(levels.Take(10).Select(Structure));
        for (int index = 10; index < levels.Count; index++)
        {
            var level = levels[index];
            string rulesPath = Root + "/Rules_" + (index + 1).ToString("000") + ".asset";
            var rules = AssetDatabase.LoadAssetAtPath<LevelSegmentRules>(rulesPath);
            if (rules == null) { rules = ScriptableObject.CreateInstance<LevelSegmentRules>(); AssetDatabase.CreateAsset(rules, rulesPath); }
            CampaignProgression.Configure(level, rules, index);
            level.campaignTemplateVersion = 1;
            CreateUniqueTemplate(level, rules, index, structures);
            level.campaignTemplateVersion = 1;
            EditorUtility.SetDirty(level); EditorUtility.SetDirty(rules);
        }
        database.levels = levels.ToArray(); EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
        Audit();
    }

    private static void CopyArt(LevelData source, LevelData destination)
    {
        destination.gridWidth = source.gridWidth; destination.gridHeight = source.gridHeight;
        destination.cells = (byte[])source.cells.Clone(); destination.palette = (Color32[])source.palette.Clone();
        destination.subGrainsPerAxis = source.subGrainsPerAxis; destination.displayTitle = source.displayTitle;
        destination.usesTiles = source.usesTiles; destination.usesUnitySpriteSheet = source.usesUnitySpriteSheet;
        destination.spriteSheetAssetPath = source.spriteSheetAssetPath; destination.tilesX = source.tilesX; destination.tilesY = source.tilesY;
        destination.tileCellWidth = source.tileCellWidth; destination.tileCellHeight = source.tileCellHeight;
        destination.tileSources = (TileSourceData[])source.tileSources.Clone();
    }

    public static string OriginalTheme(int index)
    {
        if (index < 22) return "legacy";
        if (index < 37) return "wildlife";
        if (index < 42) return "fantasy";
        if (index < 62) return "people";
        if (index < 72) return "journeys";
        if (index < 82) return "places";
        return "wonders";
    }

    private static int[] MixedOriginalOrder()
    {
        var rng = new System.Random(380213);
        var remaining = Enumerable.Range(10, 90).OrderBy(_ => rng.Next()).ToList();
        var output = new List<int>(); string last = null, beforeLast = null;
        while (remaining.Count > 0)
        {
            var group = remaining.GroupBy(OriginalTheme).Where(g => g.Key != last)
                .OrderBy(g => g.Key == beforeLast).ThenByDescending(g => g.Count()).First();
            int value = group.First(); remaining.Remove(value); output.Add(value);
            beforeLast = last; last = OriginalTheme(value);
        }
        return output.ToArray();
    }

    private static void CreateUniqueTemplate(LevelData level, LevelSegmentRules rules, int index, HashSet<string> seen)
    {
        int wallCount = level.bucketLayout.Count(LevelData.IsBucketLayoutWall);
        int feeders = level.bucketLayout.Count(LevelData.IsBucketLayoutConveyorSlot);
        var rng = new System.Random(17939 + index * 7919);
        for (int attempt = 0; attempt < 20000; attempt++)
        {
            Array.Clear(level.conveyorDirections, 0, 25); Array.Clear(level.conveyorBucketCounts, 0, 25);
            if (level.loopInventory)
            {
                level.campaignSeed = rng.Next(); InventoryLoopRoute.ApplyStructure(level, index + 1);
                for (int cell = 0; cell < 25; cell++)
                    if (LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell])) level.bucketLayout[cell] = 255;
                Array.Clear(level.conveyorDirections, 0, 25); Array.Clear(level.conveyorBucketCounts, 0, 25);
                var exits = new List<(int cell, ConveyorDirection direction)>();
                for (int cell = 0; cell < 25; cell++)
                    if (level.bucketLayout[cell] == 255)
                        foreach (ConveyorDirection direction in Enum.GetValues(typeof(ConveyorDirection)))
                        {
                            int exit = ConveyorDirectionUtility.GetTargetLayoutIndex(cell, direction);
                            if (exit >= 0 && level.bucketLayout[exit] == 0) exits.Add((cell, direction));
                        }
                foreach (var exit in exits.OrderBy(_ => rng.Next()).GroupBy(e => e.cell).Take(2).Select(g => g.First()))
                { level.bucketLayout[exit.cell] = 254; level.SetConveyorDirection(exit.cell, exit.direction); level.SetConveyorBucketCount(exit.cell, 2 + rng.Next(3)); }
            }
            else
            {
                Array.Clear(level.bucketLayout, 0, 25);
                int count = Mathf.Min(wallCount, 3);
                var cells = Enumerable.Range(5, level.alternatingCrates ? 10 : 20).OrderBy(_ => rng.Next()).ToArray();
                foreach (int cell in cells.Take(count)) level.bucketLayout[cell] = 255;
                foreach (int cell in cells.Skip(count).Take(feeders))
                { level.bucketLayout[cell] = 254; level.SetConveyorDirection(cell, (ConveyorDirection)rng.Next(4)); level.SetConveyorBucketCount(cell, 2 + rng.Next(3)); }
                if (!CampaignRunFactory.HasOpenRoutes(level)) continue;
            }
            string signature = Structure(level);
            if (seen.Contains(signature)) continue;
            level.campaignSeed = 7319 + index * 104729;
            rules.fillMode = BucketLayoutFillMode.UseTemplateAsIs;
            rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
            rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
            rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
            if (!CanClear(level)) continue;
            seen.Add(signature); return;
        }
        throw new InvalidOperationException("No unique clearable template for level " + (index + 1));
    }

    public static bool CanClear(LevelData level)
    {
        var state = UnityEngine.Random.state; var root = new GameObject("CampaignRouteCheck");
        try
        {
            UnityEngine.Random.InitState(level.campaignSeed);
            var inventory = new BucketInventory(); inventory.BuildFromLevel(level, 1, level.bucketSettingsOverride);
            if (level.alternatingCrates && inventory.CrateBucketCount < 2) return false;
            var all = CampaignIterationChecks.AllBuckets(inventory).ToArray();
            if (level.sectionBuckets && all.Count(b => b.HasSecondSection) < 2) return false;
            if (level.largeBuckets && !all.Any(b => b.SlotCost == 2)) return false;
            if (level.linkedBuckets && !all.Any(b => b.LinkedPartner != null && b.ColorIndex != b.LinkedPartner.ColorIndex)) return false;
            if (level.swappingColors && !all.Any(b => b.SwapPartner != null)) return false;
            if (level.keyGates && (!all.Any(b => b.KeyId != 0) || !all.Any(inventory.IsGateClosed))) return false;
            var belt = root.AddComponent<ConveyorBelt>(); belt.Configure(inventory.TotalBucketCount); belt.ConfigureLevel(level);
            int moves = 0;
            while (inventory.CountRemaining() > 0 && moves++ < 500)
            {
                inventory.TryDispenseAll(level);
                var next = inventory.Buckets.OrderByDescending(inventory.IsCrateCovered).ThenByDescending(b => b.KeyId).ToArray()
                    .FirstOrDefault(b => inventory.IsBucketAccessible(b, level) && inventory.TryTakeBucket(b, belt, level));
                if (next == null) return false;
                foreach (var entry in belt.Entries) inventory.HandleBucketFilled(entry.Bucket);
                belt.ResetConveyor();
            }
            return inventory.CountRemaining() == 0;
        }
        finally { UnityEngine.Random.state = state; UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string Structure(LevelData level) => Convert.ToBase64String(level.bucketLayout);
    [MenuItem("Window/Sand/Campaign/Audit 300 Levels")]
    public static void Audit()
    {
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        Require(database.LevelCount == 300, "Expected exactly 300 levels.");
        var seen = new HashSet<string>(database.levels.Take(10).Select(Structure));
        var paintings = new HashSet<string>();
        var report = new List<string> { "# Campaign 300 audit", "", "First ten levels preserved. Unique wall/feeder positions from level 11, including comparison with the first ten. Neighboring art themes differ.", "", "Level | Theme | Title | Crates | Route", "--- | --- | --- | --- | ---" };
        for (int index = 0; index < 300; index++)
        {
            var level = database.levels[index];
            Require(level.cells.Length == level.gridWidth * level.gridHeight && level.cells.All(c => c > 0 && c <= level.palette.Length), "Invalid painting " + (index + 1));
            Require(paintings.Add(Convert.ToBase64String(level.cells) + string.Join(",", level.palette.Select(c => c.ToString()))), "Duplicate painting " + (index + 1));
            if (index >= 10)
            {
                Require(seen.Add(Structure(level)), "Repeated board " + (index + 1));
                Require(level.campaignTemplateVersion > 0, "Missing authored template " + (index + 1));
                if (index > 10) Require(database.levels[index - 1].artTheme != level.artTheme, "Adjacent same-theme art " + (index + 1));
                Require(CanClear(level), "Blocked route " + (index + 1));
            }
            report.Add($"{index + 1} | {level.artTheme} | {level.displayTitle} | {level.alternatingCrates} | {(index < 10 ? "preserved" : "PASS")}");
        }
        File.WriteAllLines("tasks/campaign300-audit.md", report);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
