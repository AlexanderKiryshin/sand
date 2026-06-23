using System.IO;
using UnityEditor;
using UnityEngine;

public class ImageToLevelConverterWindow : EditorWindow
{
    private Texture2D sourceTexture;
    private int gridWidth = 32;
    private int gridHeight = 32;
    private int colorCount = 2;
    private int previewScale = 8;
    private string levelName = "Level_01";

    private Texture2D previewTexture;
    private Color32[] generatedPalette = new Color32[0];
    private byte[] generatedCells = new byte[0];
    private BucketSpawn[] generatedBuckets = new BucketSpawn[0];
    private Vector2 scrollPosition;

    [MenuItem("Window/Sand/Image to Level")]
    public static void ShowWindow()
    {
        ImageToLevelConverterWindow window = GetWindow<ImageToLevelConverterWindow>("Image to Level");
        window.minSize = new Vector2(640f, 480f);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", sourceTexture, typeof(Texture2D), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        levelName = EditorGUILayout.TextField("Level Name", levelName);
        gridWidth = EditorGUILayout.IntSlider("Grid Width", gridWidth, 8, 64);
        gridHeight = EditorGUILayout.IntSlider("Grid Height", gridHeight, 8, 64);
        colorCount = EditorGUILayout.IntSlider("Color Count", colorCount, 2, 16);
        previewScale = EditorGUILayout.IntSlider("Preview Scale", previewScale, 4, 16);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(sourceTexture == null))
        {
            if (GUILayout.Button("Generate Preview", GUILayout.Height(30f)))
            {
                GeneratePreview();
            }
        }

        DrawPreviewSection();
        DrawPaletteSection();

        using (new EditorGUI.DisabledScope(generatedCells == null || generatedCells.Length == 0))
        {
            if (GUILayout.Button("Save Level Asset", GUILayout.Height(30f)))
            {
                SaveLevelAsset();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void GeneratePreview()
    {
        Color32[] sampledPixels = SampleSourceTexture(sourceTexture, gridWidth, gridHeight);
        generatedPalette = ColorQuantizer.KMeansQuantize(sampledPixels, colorCount, 20);
        generatedCells = ColorQuantizer.MapToPalette(sampledPixels, generatedPalette);
        generatedBuckets = ColorQuantizer.GenerateBucketSpawns(generatedCells, generatedPalette.Length);

        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }

        previewTexture = ColorQuantizer.BuildPreviewTexture(gridWidth, gridHeight, generatedCells, generatedPalette, previewScale);
        Repaint();
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
        EditorGUILayout.LabelField("Source");
        if (sourceTexture != null)
        {
            DrawTexturePreview(sourceTexture);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
        EditorGUILayout.LabelField("Generated");
        if (previewTexture != null)
        {
            DrawTexturePreview(previewTexture);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPaletteSection()
    {
        if (generatedPalette == null || generatedPalette.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette & Buckets", EditorStyles.boldLabel);

        for (int i = 0; i < generatedPalette.Length; i++)
        {
            byte colorIndex = (byte)(i + 1);
            int grainCount = CountGrains(colorIndex);
            int bucketCount = GetBucketCount(colorIndex);

            EditorGUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
            EditorGUI.DrawRect(colorRect, generatedPalette[i]);
            EditorGUILayout.LabelField(string.Format("Color {0}: {1} grains, {2} buckets", colorIndex, grainCount, bucketCount));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void SaveLevelAsset()
    {
        if (generatedCells == null || generatedCells.Length == 0)
        {
            EditorUtility.DisplayDialog("Save Level", "Generate preview before saving.", "OK");
            return;
        }

        string levelsFolder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(levelsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(levelsFolder, levelName + ".asset"));
        LevelData levelData = CreateInstance<LevelData>();
        levelData.gridWidth = gridWidth;
        levelData.gridHeight = gridHeight;
        levelData.palette = generatedPalette;
        levelData.cells = generatedCells;
        levelData.buckets = generatedBuckets;
        levelData.previewTexture = ColorQuantizer.BuildPreviewTexture(gridWidth, gridHeight, generatedCells, generatedPalette, previewScale);
        levelData.EnsureBucketLayout();
        AutoFillBucketLayout(levelData);

        AssetDatabase.CreateAsset(levelData, assetPath);

        string previewPath = Path.ChangeExtension(assetPath, null) + "_preview.png";
        SavePreviewTexture(levelData.previewTexture, previewPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Save Level", "Level saved to:\n" + assetPath, "OK");
        Selection.activeObject = levelData;
    }

    private static Color32[] SampleSourceTexture(Texture2D texture, int width, int height)
    {
        Texture2D readableTexture = EnsureReadable(texture);
        Color32[] result = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;
                int sourceX = Mathf.Clamp(Mathf.FloorToInt(u * readableTexture.width), 0, readableTexture.width - 1);
                int sourceY = Mathf.Clamp(Mathf.FloorToInt(v * readableTexture.height), 0, readableTexture.height - 1);
                result[y * width + x] = readableTexture.GetPixel(sourceX, sourceY);
            }
        }

        if (!ReferenceEquals(readableTexture, texture))
        {
            DestroyImmediate(readableTexture);
        }

        return result;
    }

    private static Texture2D EnsureReadable(Texture2D texture)
    {
        try
        {
            texture.GetPixel(0, 0);
            return texture;
        }
        catch
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, renderTexture);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return copy;
        }
    }

    private static void SavePreviewTexture(Texture2D texture, string assetPath)
    {
        byte[] pngData = texture.EncodeToPNG();
        File.WriteAllBytes(assetPath, pngData);
    }

    private static void DrawTexturePreview(Texture2D texture)
    {
        float maxSize = 256f;
        float scale = Mathf.Min(maxSize / texture.width, maxSize / texture.height, 1f);
        Rect rect = GUILayoutUtility.GetRect(texture.width * scale, texture.height * scale);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
    }

    private int CountGrains(byte colorIndex)
    {
        int count = 0;
        for (int i = 0; i < generatedCells.Length; i++)
        {
            if (generatedCells[i] == colorIndex)
            {
                count++;
            }
        }

        return count;
    }

    private int GetBucketCount(byte colorIndex)
    {
        for (int i = 0; i < generatedBuckets.Length; i++)
        {
            if (generatedBuckets[i].colorIndex == colorIndex)
            {
                return generatedBuckets[i].count;
            }
        }

        return 0;
    }

    private static void AutoFillBucketLayout(LevelData levelData)
    {
        levelData.EnsureBucketLayout();
        for (int i = 0; i < levelData.bucketLayout.Length; i++)
        {
            levelData.bucketLayout[i] = 0;
        }

        byte[] usedColors = levelData.GetColorsUsedInLevel();
        int layoutIndex = 0;
        for (int colorSlot = 0; colorSlot < usedColors.Length && layoutIndex < LevelData.BucketLayoutCellCount; colorSlot++)
        {
            byte colorIndex = usedColors[colorSlot];
            int bucketCount = 1;

            for (int i = 0; i < bucketCount && layoutIndex < LevelData.BucketLayoutCellCount; i++)
            {
                int x = layoutIndex % LevelData.BucketLayoutColumns;
                int y = layoutIndex / LevelData.BucketLayoutColumns;
                levelData.SetBucketLayoutCell(x, y, colorIndex);
                layoutIndex++;
            }
        }
    }
}
