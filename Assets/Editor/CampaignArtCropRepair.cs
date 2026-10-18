using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CampaignArtCropRepair
{
    private static readonly string[] OriginalAtlases = { "01_Wilds", "02_Characters", "03_Journeys", "04_Wonders" };

    public static void Run()
    {
        CheckShiftedSeams();
        CampaignAtlasSlicer.ClearCache();
        var originals = new Dictionary<int, LevelData>();
        var destinations = new Dictionary<LevelData, LevelData>();
        for (int number = 23; number <= 100; number++)
            originals[number] = Load("CampaignSource100/Art_" + number.ToString("000"));
        // Resolve the shuffled pictures before any source palette/cells are replaced.
        for (int number = 1; number <= 100; number++)
        {
            var level = Load("Campaign100/Level_" + number.ToString("000"));
            var source = originals.Values.FirstOrDefault(art => art.gridWidth == level.gridWidth
                && art.gridHeight == level.gridHeight && art.palette.SequenceEqual(level.palette)
                && art.cells.SequenceEqual(level.cells));
            if (source != null) destinations.Add(level, source);
        }
        var textures = new Dictionary<string, Texture2D>();
        try
        {
            foreach (var pair in originals)
            {
                int tileNumber = pair.Key - 23;
                Apply(pair.Value, Texture(textures, "Campaign/" + OriginalAtlases[tileNumber / 20]), tileNumber % 20, 4);
            }
            foreach (var pair in destinations)
            {
                pair.Key.cells = (byte[])pair.Value.cells.Clone(); pair.Key.palette = (Color32[])pair.Value.palette.Clone();
                EditorUtility.SetDirty(pair.Key);
            }
            for (int number = 101; number <= 300; number++)
            {
                int tileNumber = number - 101;
                Apply(Load("Campaign100/Level_" + number.ToString("000")),
                    Texture(textures, "CampaignExpansion/Expansion_" + (tileNumber / 25 + 1).ToString("00")), tileNumber % 25, 5);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("tasks/atlas-crop-repair.txt", "PASS: shifted-seam fixture; recropped " + (destinations.Count + 200)
                + " campaign paintings and 78 source paintings using actual atlas seams. Authored rules and board templates retained.");
        }
        finally
        {
            foreach (var texture in textures.Values) UnityEngine.Object.DestroyImmediate(texture);
            CampaignAtlasSlicer.ClearCache();
        }
        LinkedBucketPresentationChecks.RunAll();
    }

    private static LevelData Load(string path)
        => AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/" + path + ".asset")
            ?? throw new InvalidOperationException("Missing " + path);
    private static Texture2D Texture(Dictionary<string, Texture2D> textures, string name)
    {
        if (!textures.TryGetValue(name, out var texture))
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes("Assets/Images/" + name + ".png"))) throw new InvalidOperationException(name);
            textures.Add(name, texture);
        }
        return texture;
    }
    private static void Apply(LevelData level, Texture2D texture, int tile, int rows)
    {
        RectInt crop = CampaignAtlasSlicer.TileRect(texture, tile, 5, rows);
        var settings = ImageProcessor.Settings.Create(64, 64, 12);
        settings.autoColorCount = settings.autoBackgroundColor = false;
        settings.backgroundColor = new Color32(32, 62, 71, 255);
        var result = ImageProcessor.Process(Array.ConvertAll(texture.GetPixels(crop.x, crop.y, crop.width, crop.height), c => (Color32)c), crop.width, crop.height, settings);
        level.cells = result.cellIndices; level.palette = result.palette;
        EditorUtility.SetDirty(level);
    }

    private static void CheckShiftedSeams()
    {
        // A grid with the last row boundary displaced by 19 pixels, as in Expansion_03.
        const int size = 1254;
        int[] edges = { 0, 251, 501, 752, 984, size };
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int column = Array.FindIndex(edges, edge => edge > x) - 1;
            int row = Array.FindIndex(edges, edge => edge > y) - 1;
            pixels[y * size + x] = new Color32((byte)(column * 50), (byte)(row * 50), 100, 255);
        }
        try
        {
            texture.SetPixels32(pixels); texture.Apply();
            for (int tile = 0; tile < 25; tile++)
            {
                RectInt crop = CampaignAtlasSlicer.TileRect(texture, tile, 5, 5);
                Color32 expected = new Color32((byte)(tile % 5 * 50), (byte)((4 - tile / 5) * 50), 100, 255);
                foreach (Color color in texture.GetPixels(crop.x, crop.y, crop.width, crop.height))
                    if (!((Color32)color).Equals(expected)) throw new InvalidOperationException("Atlas crop includes adjacent tile " + tile);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); CampaignAtlasSlicer.ClearCache(); }
        Debug.Log("ATLAS_SHIFTED_SEAMS_PASS: all 25 tiles exclude their neighbors with a 19-pixel shifted seam.");
    }
}
