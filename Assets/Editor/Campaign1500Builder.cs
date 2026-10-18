using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class Campaign1500Builder
{
    private const string Root = "Assets/Levels/Campaign1500";
    [Serializable] public class Batch { public string name, kind; public string[] scenes; public int[] normalNumbers; }
    [Serializable] private class SeparationPlan { public Batch[] batches; }
    static Campaign1500Builder() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Library/build-campaign1500";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try { if(command == "openings") BuildOpenings(); else Build(); File.WriteAllText("tasks/campaign1500-build-status.txt", "PASS " + command + " " + DateTime.UtcNow.ToString("O")); }
        catch (Exception e) { File.WriteAllText("tasks/campaign1500-build-status.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Window/Sand/Import Approved Opening Paintings")]
    public static void BuildOpenings()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var batch = JsonUtility.FromJson<Batch>(File.ReadLines("tasks/campaign1500-art.jsonl").First());
        var atlas = new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!atlas.LoadImage(File.ReadAllBytes("Assets/Images/Campaign1500/ordinary_01.png"))) throw new Exception("Opening atlas missing.");
            int[] opening = { 5, 1, 6, 2, 4 };
            for(int i=0;i<5;i++)
            {
                RectInt crop = CampaignAtlasSlicer.TileRect(atlas, opening[i], 5, 5);
                var pixels = Array.ConvertAll(atlas.GetPixels(crop.x,crop.y,crop.width,crop.height),c=>(Color32)c);
                var settings = ImageProcessor.Settings.Create(64,64,8);
                settings.autoColorCount=false;settings.autoBackgroundColor=true;
                var art = ImageProcessor.Process(pixels,crop.width,crop.height,settings);
                var level = db.levels[i];
                level.gridWidth=art.width;level.gridHeight=art.height;level.palette=art.palette;level.cells=art.cellIndices;
                level.displayTitle=batch.scenes[opening[i]];level.artTheme="ordinary";level.seasonalNumber=0;
                level.buckets=ColorQuantizer.GenerateBucketSpawns(level.cells,level.palette.Length);
                level.bucketSettingsOverride.maxGrainsPerBucket=Mathf.CeilToInt(level.CountTotalLevelCells()/10f);
                EditorUtility.SetDirty(level);EditorUtility.SetDirty(level.bucketSettingsOverride);
            }
            AssetDatabase.SaveAssets();GameReworkBuilder.Validate();
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }
    [MenuItem("Window/Sand/Build Approved Campaigns (1000 + 200)")]
    public static void Build()
    {
        var batches = File.ReadAllLines("tasks/campaign1500-art.jsonl").Where(s => !string.IsNullOrWhiteSpace(s)).Select(JsonUtility.FromJson<Batch>).ToArray();
        if (batches.Where(b => b.kind == "ordinary").Sum(b => b.scenes.Length) != 700 || batches.Where(b => b.kind == "winter").Sum(b => b.scenes.Length) != 180)
            throw new Exception("Expected 700 new ordinary and 180 new winter paintings.");
        foreach (var batch in batches)
            if (!File.Exists("Assets/Images/Campaign1500/" + batch.name + ".png")) throw new Exception("Missing atlas " + batch.name);
        var separation = ReadSeparation();
        foreach (var batch in separation)
            if (!File.Exists("Assets/Images/Campaign1500/" + batch.name + ".png")) throw new Exception("Missing seasonal separation atlas " + batch.name);
        Directory.CreateDirectory(Root); AssetDatabase.Refresh(); CampaignAtlasSlicer.ClearCache();
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        var templates = db.levels.Take(300).ToArray();
        var originals = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/ReworkOriginals/Opening_" + i + ".asset")).ToArray();
        if (originals.Any(l => l == null)) throw new Exception("Opening art backups missing.");
        var main = templates.ToList();
        var winter = Enumerable.Range(301, 20).Select(n => AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/Campaign100/Level_" + n + ".asset")).ToList();
        if (winter.Any(l => l == null)) throw new Exception("Original winter paintings missing.");
        // Use five approved light paintings for the opening; retain all five older paintings later.
        int[] opening = { 5, 1, 6, 2, 4 };
        int ordinaryArt = 0;
        try
        {
            foreach (var batch in batches)
            {
                var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!atlas.LoadImage(File.ReadAllBytes("Assets/Images/Campaign1500/" + batch.name + ".png"))) throw new Exception("Invalid image " + batch.name);
                    for (int tile = 0; tile < batch.scenes.Length; tile++)
                    {
                        bool seasonal = batch.kind == "winter";
                        int start = seasonal ? -1 : Array.IndexOf(opening, ordinaryArt);
                        int number = seasonal ? winter.Count + 1 : start >= 0 ? start + 1 : main.Count + 1;
                        var level = start >= 0 ? main[start] : GetLevel(seasonal, number);
                        RectInt crop = CampaignAtlasSlicer.TileRect(atlas, tile, 5, 5);
                        var pixels = Array.ConvertAll(atlas.GetPixels(crop.x, crop.y, crop.width, crop.height), c => (Color32)c);
                        var settings = ImageProcessor.Settings.Create(64, 64, start >= 0 ? 8 : 10);
                        settings.autoColorCount = false; settings.autoBackgroundColor = true;
                        var art = ImageProcessor.Process(pixels, crop.width, crop.height, settings);
                        level.gridWidth = art.width; level.gridHeight = art.height; level.palette = art.palette; level.cells = art.cellIndices;
                        level.displayTitle = batch.scenes[tile]; level.artTheme = seasonal ? "winter" : "ordinary";
                        level.seasonalNumber = seasonal ? number : 0;
                        level.subGrainsPerAxis = 1; level.usesTiles = false; level.tileSources = Array.Empty<TileSourceData>();
                        if (start >= 0)
                        {
                            level.buckets = ColorQuantizer.GenerateBucketSpawns(level.cells, level.palette.Length);
                            level.bucketSettingsOverride.maxGrainsPerBucket = Mathf.CeilToInt(level.CountTotalLevelCells() / 10f);
                            EditorUtility.SetDirty(level.bucketSettingsOverride); EditorUtility.SetDirty(level);
                        }
                        else
                        {
                            Configure(level, number - 1, seasonal, templates);
                            if (seasonal) winter.Add(level); else main.Add(level);
                        }
                        if (!seasonal) ordinaryArt++;
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(atlas); }
                File.WriteAllText("tasks/campaign1500-build-progress.txt", batch.name + ": " + main.Count + " ordinary, " + winter.Count + " winter");
            }
            for (int i = 0; i < originals.Length; i++)
            {
                var level = GetLevel(false, main.Count + 1); var source = originals[i];
                level.gridWidth = source.gridWidth; level.gridHeight = source.gridHeight;
                level.palette = (Color32[])source.palette.Clone(); level.cells = (byte[])source.cells.Clone();
                level.displayTitle = "Original painting " + (i + 1); level.artTheme = "ordinary"; level.seasonalNumber = 0;
                level.subGrainsPerAxis = 1; level.usesTiles = false; level.tileSources = Array.Empty<TileSourceData>();
                Configure(level, main.Count, false, templates); main.Add(level);
            }
            if (main.Count != 1000 || winter.Count != 200) throw new Exception("Incorrect final campaign counts.");
            SeparateWinterArt(main, winter, templates, separation);
            db.levels = main.Concat(winter).ToArray(); EditorUtility.SetDirty(db); AssetDatabase.SaveAssets();
            GameReworkBuilder.Validate();
        }
        finally { EditorUtility.ClearProgressBar(); AssetDatabase.SaveAssets(); }
    }
    private static Batch[] ReadSeparation()
    {
        string json = File.ReadAllText("tasks/campaign-season-separation.json").TrimStart('\uFEFF');
        return JsonUtility.FromJson<SeparationPlan>("{\"batches\":" + json + "}").batches;
    }
    private static void SeparateWinterArt(List<LevelData> main, List<LevelData> winter, LevelData[] templates, Batch[] batches)
    {
        int total = batches.Sum(b=>b.scenes.Length), moved = 0;
        if(total != 32 || batches.SelectMany(b=>b.normalNumbers).Distinct().Count()!=total)
            throw new Exception("Seasonal separation plan must have 32 distinct ordinary entries.");
        foreach(var batch in batches)
        {
            var atlas = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!atlas.LoadImage(File.ReadAllBytes("Assets/Images/Campaign1500/"+batch.name+".png"))) throw new Exception("Invalid separation atlas");
                for(int tile=0;tile<batch.scenes.Length;tile++)
                {
                    int index = batch.normalNumbers[tile]-1;
                    var source = main[index];
                    string backupPath = "Assets/Levels/ReworkOriginals/Season_"+batch.normalNumbers[tile]+".asset";
                    var old = AssetDatabase.LoadAssetAtPath<LevelData>(backupPath);
                    if(old==null)
                    {
                        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),backupPath)) throw new Exception("Cannot preserve seasonal painting " + index);
                        old=AssetDatabase.LoadAssetAtPath<LevelData>(backupPath);
                    }
                    var target=winter[winter.Count-total+moved++];
                    target.gridWidth=old.gridWidth;target.gridHeight=old.gridHeight;
                    target.palette=(Color32[])old.palette.Clone();target.cells=(byte[])old.cells.Clone();
                    target.displayTitle=old.displayTitle;target.artTheme="winter";
                    target.subGrainsPerAxis=1;target.usesTiles=false;target.tileSources=Array.Empty<TileSourceData>();
                    Configure(target,target.seasonalNumber-1,true,templates);
                    RectInt crop=CampaignAtlasSlicer.TileRect(atlas,tile,5,5);
                    var pixels=Array.ConvertAll(atlas.GetPixels(crop.x,crop.y,crop.width,crop.height),c=>(Color32)c);
                    var settings=ImageProcessor.Settings.Create(64,64,10);settings.autoColorCount=false;settings.autoBackgroundColor=true;
                    var art=ImageProcessor.Process(pixels,crop.width,crop.height,settings);
                    source.gridWidth=art.width;source.gridHeight=art.height;source.palette=art.palette;source.cells=art.cellIndices;
                    source.displayTitle=batch.scenes[tile];source.artTheme="ordinary";source.seasonalNumber=0;
                    source.subGrainsPerAxis=1;source.usesTiles=false;source.tileSources=Array.Empty<TileSourceData>();
                    Configure(source,index,false,templates);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }
        File.WriteAllText("tasks/campaign-season-separation-status.txt", "PASS: " + moved + " existing winter paintings moved into the seasonal collection, ordinary slots replaced with distinct new non-winter paintings. " + DateTime.UtcNow.ToString("O"));
    }
    private static LevelData GetLevel(bool winter, int number)
    {
        string path = Root + "/" + (winter ? "Winter_" : "Ordinary_") + number.ToString("0000") + ".asset";
        var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
        return level;
    }
    private static void Configure(LevelData level, int index, bool winter, LevelData[] templates)
    {
        string path = Root + "/" + (winter ? "WinterRules_" : "OrdinaryRules_") + (index + 1).ToString("0000") + ".asset";
        var rules = AssetDatabase.LoadAssetAtPath<LevelSegmentRules>(path);
        if (rules == null) { rules = ScriptableObject.CreateInstance<LevelSegmentRules>(); AssetDatabase.CreateAsset(rules, path); }
        CampaignProgression.Configure(level, rules, index); level.campaignTemplateVersion = 1;
        level.buckets = ColorQuantizer.GenerateBucketSpawns(level.cells, level.palette.Length);
        var candidates = templates.Where(t => t.loopInventory == level.loopInventory && t.alternatingCrates == level.alternatingCrates).ToArray();
        for (int attempt = 0; attempt < 256; attempt++)
        {
            if (attempt > 0 && candidates.Length > 0)
            {
                var source = candidates[(index + attempt) % candidates.Length];
                level.bucketLayout = (byte[])source.bucketLayout.Clone();
                level.conveyorDirections = (byte[])source.conveyorDirections.Clone();
                level.conveyorBucketCounts = (byte[])source.conveyorBucketCounts.Clone();
            }
            CampaignProgression.ApplyPersistentRules(level, index);
            rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
            rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
            rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
            level.campaignSeed = unchecked(7319 + index * 104729 + attempt * 7919 + (winter ? 15485863 : 0));
            if (!CampaignExpansionBuilder.CanClear(level)) continue;
            EditorUtility.SetDirty(level); EditorUtility.SetDirty(rules); return;
        }
        throw new Exception("No clearable route for " + (winter ? "winter " : "ordinary ") + (index + 1));
    }
}
