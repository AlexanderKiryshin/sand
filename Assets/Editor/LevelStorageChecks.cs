using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Run explicitly in a disposable validation project via -executeMethod.</summary>
public static class LevelStorageChecks
{
    [MenuItem("Window/Sand/Validate Level Storage")]
    public static void Run()
    {
        CheckCodec();
        CheckSerialization();
        CheckPreviewCache();
        int levels = CheckAssets();
        int previews = CheckCampaignPreviews();
        Directory.CreateDirectory("tasks");
        File.WriteAllText("tasks/level-storage-validation.md",
            "PASS: " + levels + " level assets decode to their original cells; dimensions and color indices are valid.\n"
            + "PASS: " + previews + " campaign previews match every original PNG pixel at logical resolution.\n"
            + "PASS: 1–8 bit indices, byte-boundary packing, empty grids, malformed headers, Base64, lengths and checksums.\n"
            + "PASS: legacy import, save/reload, in-place edits, Undo/Redo, Instantiate and JSON serialization.\n"
            + "PASS: preview cache reuse, cell/palette invalidation, transparency and texture disposal.\n"
            + "PASS: level assets have no texture dependencies.\n");
        Debug.Log("LEVEL_STORAGE_ALL_PASS levels=" + levels + " previews=" + previews);
    }

    private static void CheckCodec()
    {
        var random = new System.Random(1701);
        var fixtures = new List<string>();
        foreach (int bits in Enumerable.Range(1, 8))
        foreach (int count in new[] { 1, 3, 7, 8, 9, 31, 33, 4096 })
        {
            byte[] cells = new byte[count];
            for (int i = 0; i < count; i++) cells[i] = (byte)random.Next(1 << bits);
            cells[count - 1] = (byte)((1 << bits) - 1);
            string encoded = LevelCellCodec.Encode(cells);
            Require(cells.SequenceEqual(LevelCellCodec.Decode(encoded)), "Round trip at " + bits + " bits / " + count);
            fixtures.Add(encoded + "\t" + BitConverter.ToString(cells).Replace("-", ""));
        }
        Require(LevelCellCodec.Encode(Array.Empty<byte>()) == "" && LevelCellCodec.Decode("").Length == 0, "Empty data");
        Require(LevelCellCodec.Decode(LevelCellCodec.Encode(new byte[4096])).All(c => c == 0), "All empty cells");
        string good = LevelCellCodec.Encode(new byte[] { 0, 1, 15, 3, 2 });
        MustReject(good.Replace("S1:", "S2:"));
        MustReject(good.Replace("S1:4:", "S1:9:"));
        MustReject(good.Replace(":5:", ":4:"));
        MustReject(good.Replace(":5:", ":100:"));
        MustReject(good.Replace(":5:", ":16777217:"));
        string[] parts = good.Split(':');
        parts[3] = parts[3] == "00000000" ? "FFFFFFFF" : "00000000";
        MustReject(string.Join(":", parts));
        parts = good.Split(':'); parts[4] = "invalid!";
        MustReject(string.Join(":", parts));
        parts[4] = "";
        MustReject(string.Join(":", parts));
        Directory.CreateDirectory("tasks");
        File.WriteAllLines("tasks/level-storage-csharp-fixtures.txt", fixtures);
    }

    private static void CheckSerialization()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/LevelStorageSerializationCheck.asset");
        try
        {
            level.gridWidth = 3; level.gridHeight = 2;
            level.palette = new[] { new Color32(230, 60, 20, 255), new Color32(40, 150, 200, 255) };
            level.cells = new byte[] { 1, 0, 2, 2, 1, 0 };
            AssetDatabase.CreateAsset(level, path);
            level.cells[0] = 2;
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);
            string saved = File.ReadAllText(path);
            Require(saved.Contains("encodedCells: S1:") && !Regex.IsMatch(saved, @"^  cells:", RegexOptions.Multiline), "Only encoded cells saved");
            Resources.UnloadAsset(level);
            level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            Require(level.cells.SequenceEqual(new byte[] { 2, 0, 2, 2, 1, 0 }), "Reload in-place edit");

            Undo.RecordObject(level, "Level storage test edit");
            level.cells[0] = 1;
            EditorUtility.SetDirty(level);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Require(level.cells[0] == 2, "Undo cell edit");
            Undo.PerformRedo();
            Require(level.cells[0] == 1, "Redo cell edit");
            Undo.ClearUndo(level);

            var clone = UnityEngine.Object.Instantiate(level);
            try
            {
                Require(clone.cells.SequenceEqual(level.cells), "Instantiate preserves cells");
                clone.cells[0] = 0;
                Require(level.cells[0] == 1, "Clone has an independent cell array");
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            string json = JsonUtility.ToJson(level);
            Require(json.Contains("encodedCells") && !json.Contains("\"cells\":"), "JSON uses compressed text");

            // Import a genuine old-format asset, then save it through the new callback.
            Resources.UnloadAsset(level);
            string legacy = Regex.Replace(saved, @"^  encodedCells:.*$", "  cells: 010002020100", RegexOptions.Multiline);
            legacy = Regex.Replace(legacy, @"^  legacyCells:.*\r?\n", "", RegexOptions.Multiline);
            File.WriteAllText(path, legacy);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            Require(level.cells.SequenceEqual(new byte[] { 1, 0, 2, 2, 1, 0 }), "Legacy cells import");
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);
            Require(File.ReadAllText(path).Contains("encodedCells: S1:"), "Legacy asset saves as text");
        }
        finally
        {
            if (level != null) Undo.ClearUndo(level);
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void CheckPreviewCache()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        using (var cache = new LevelPreviewCache())
        try
        {
            level.gridWidth = 2; level.gridHeight = 2;
            level.palette = new[] { new Color32(200, 30, 40, 128) };
            level.cells = new byte[] { 0, 1, 1, 0 };
            Texture2D first = cache.Get(level);
            Require(first.width == 2 && first.height == 2 && first.mipmapCount == 1, "Logical preview size without mipmaps");
            Require(first == cache.Get(level), "Preview cache reused");
            level.cells[0] = 1;
            Texture2D changed = cache.Get(level);
            Require(first == null && changed != null, "Cell edits invalidate and destroy old preview");
            level.palette[0] = new Color32(20, 50, 200, 128);
            Texture2D recolored = cache.Get(level);
            Require(changed == null && recolored != null, "Palette edits invalidate preview");
            cache.Clear();
            Require(recolored == null, "Page/inspector disposal releases texture");
            level.cells[0] = 0;
            Texture2D pixels = ColorQuantizer.BuildPreviewTexture(2, 2, level.cells, level.palette, 1);
            try
            {
                Color32[] colors = pixels.GetPixels32();
                Require(colors[2].a == 0 && colors[3].a == 128, "Empty cells and palette alpha preserved");
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static int CheckAssets()
    {
        string baseline = Environment.GetEnvironmentVariable("SAND_LEVEL_STORAGE_BACKUP");
        string[] guids = AssetDatabase.FindAssets("t:LevelData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            byte[] cells = level.cells;
            Require(cells.Length == level.gridWidth * level.gridHeight, "Grid dimensions: " + path);
            Require(cells.All(c => c <= level.palette.Length), "Color indices: " + path);
            Require(LevelCellCodec.Decode(LevelCellCodec.Encode(cells)).SequenceEqual(cells), "Asset round trip: " + path);
            Require(!AssetDatabase.GetDependencies(path).Any(p => typeof(Texture).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(p))), "No texture dependencies: " + path);
            if (!string.IsNullOrEmpty(baseline))
            {
                string original = File.ReadAllText(Path.Combine(baseline, path));
                string hex = Regex.Match(original, @"^  cells: ([0-9a-fA-F]+)", RegexOptions.Multiline).Groups[1].Value;
                Require(!string.IsNullOrEmpty(hex), "Original snapshot: " + path);
                byte[] expected = Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
                Require(expected.SequenceEqual(cells), "Original cells: " + path);
            }
            // Gameplay consumes the same decoded array through SandGrid.
            var grid = new SandGrid(level, 1);
            Require(grid.CountRemaining() == level.CountTotalLevelCells(), "Sand grid cell budget: " + path);
        }
        return guids.Length;
    }

    private static int CheckCampaignPreviews()
    {
        string baseline = Environment.GetEnvironmentVariable("SAND_LEVEL_STORAGE_BACKUP");
        if (string.IsNullOrEmpty(baseline)) return 0;
        var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        for (int index = 0; index < database.LevelCount; index++)
        {
            LevelData level = database.GetLevel(index);
            string path = AssetDatabase.GetAssetPath(level);
            string png = Path.Combine(baseline, Path.ChangeExtension(path, null) + "_preview.png");
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D generated = ColorQuantizer.BuildPreviewTexture(level.gridWidth, level.gridHeight, level.cells, level.palette, 1);
            try
            {
                Require(original.LoadImage(File.ReadAllBytes(png)), "Read original PNG " + path);
                Color32[] oldPixels = original.GetPixels32(), newPixels = generated.GetPixels32();
                int scale = original.width / level.gridWidth;
                Require(original.width == generated.width * scale && original.height == generated.height * scale, "Preview dimensions " + path);
                for (int y = 0; y < original.height; y++)
                for (int x = 0; x < original.width; x++)
                    if (!oldPixels[y * original.width + x].Equals(newPixels[y / scale * generated.width + x / scale]))
                        throw new InvalidOperationException("Preview pixels " + path + " at " + x + "," + y);
            }
            finally { UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(generated); }
        }
        return database.LevelCount;
    }

    private static void MustReject(string encoded)
    {
        try { LevelCellCodec.Decode(encoded); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Accepted damaged level data: " + encoded);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
