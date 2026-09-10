using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    [MenuItem("Window/Sand/Fix Sub Grains (set to 1)")]
    private static void FixSubGrainsOnAllLevels()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelData");
        int changed = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null || level.subGrainsPerAxis == 1)
            {
                continue;
            }

            level.subGrainsPerAxis = 1;
            EditorUtility.SetDirty(level);
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog(
            "Fix Sub Grains",
            string.Format("Updated {0} of {1} LevelData asset(s) to subGrainsPerAxis = 1.", changed, guids.Length),
            "OK");
    }

    private enum LayoutPaintMode
    {
        Color,
        Wall,
        Erase
    }

    private byte selectedLayoutColor;
    private LayoutPaintMode paintMode = LayoutPaintMode.Color;
    private bool layoutFoldout = true;
    private bool paletteFoldout = true;
    private bool bucketsFoldout = true;
    private bool recalcFoldout = true;

    private SandColorPalette recalcTargetPalette;
    private Texture2D recalcSourceTexture;
    private int recalcColorCount = 4;
    private bool recalcAutoColorCount = true;
    private bool recalcAutoBackground = true;
    private Color recalcBackgroundColor = Color.white;
    private readonly LevelPreviewCache previews = new LevelPreviewCache();

    private void OnDisable() { previews.Clear(); }

    public override void OnInspectorGUI()
    {
        LevelData levelData = (LevelData)target;
        serializedObject.Update();

        levelData.EnsureBucketLayout();

        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gridWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gridHeight"));

        SerializedProperty subGrainsProp = serializedObject.FindProperty("subGrainsPerAxis");
        if (subGrainsProp.intValue != 1)
        {
            subGrainsProp.intValue = 1;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(subGrainsProp);
        }

        if (levelData.usesTiles)
        {
            string splitInfo = levelData.usesUnitySpriteSheet
                ? string.Format(
                    "Unity sprite sheet: {0}×{1} tiles, cell {2}×{3}",
                    levelData.tilesX,
                    levelData.tilesY,
                    levelData.tileCellWidth,
                    levelData.tileCellHeight)
                : string.Format(
                    "Tiles: {0}×{1}, cell {2}×{3}, sources {4}",
                    levelData.tilesX,
                    levelData.tilesY,
                    levelData.tileCellWidth,
                    levelData.tileCellHeight,
                    levelData.tileSources != null ? levelData.tileSources.Length : 0);
            EditorGUILayout.LabelField(splitInfo, EditorStyles.miniLabel);
        }

        DrawPaletteEditor(levelData);
        DrawBucketRulesSection(levelData);
        DrawBucketsEditor(levelData);
        DrawCellsInfo(levelData);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Level Cells", levelData.CountTotalLevelCells().ToString());
        EditorGUILayout.LabelField("Total Grains", levelData.CountTotalGrains().ToString());
        EditorGUILayout.LabelField("Layout Buckets", levelData.CountBucketsInLayout().ToString());
        EditorGUILayout.LabelField("Total Buckets", levelData.GetTotalBucketCount().ToString());

        DrawBucketLayoutEditor(levelData);
        DrawRecalculateSection(levelData);
        DrawPreview(levelData);
    }

    private void DrawRecalculateSection(LevelData levelData)
    {
        EditorGUILayout.Space();
        recalcFoldout = EditorGUILayout.Foldout(recalcFoldout, "Recalculate Colors", true);
        if (!recalcFoldout)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            levelData.usesTiles
                ? "Tiled level from Unity sprite sheet: recalculate reloads sprites from the texture asset."
                : "If colors look wrong, assign the Color Palette and source image, then recalculate. "
                  + "Or use Window > Sand > Image to Level, select the image and click Regenerate Preview before Save.",
            MessageType.Info);

        recalcTargetPalette = (SandColorPalette)EditorGUILayout.ObjectField(
            "Color Palette",
            recalcTargetPalette,
            typeof(SandColorPalette),
            false);
        recalcSourceTexture = (Texture2D)EditorGUILayout.ObjectField(
            levelData.usesTiles ? "Source Image (optional)" : "Source Image",
            recalcSourceTexture,
            typeof(Texture2D),
            false);
        recalcAutoColorCount = EditorGUILayout.Toggle("Auto Color Count", recalcAutoColorCount);
        int recalcColorMax = recalcTargetPalette != null
            ? Mathf.Max(2, recalcTargetPalette.GetColorCount())
            : SandColorPalette.MaxColorCount;
        using (new EditorGUI.DisabledScope(recalcAutoColorCount))
        {
            recalcColorCount = EditorGUILayout.IntSlider("Color Count", recalcColorCount, 2, recalcColorMax);
        }

        if (recalcAutoColorCount)
        {
            EditorGUILayout.LabelField("Colors are detected automatically from the source", EditorStyles.miniLabel);
        }

        recalcAutoBackground = EditorGUILayout.Toggle("Auto Background", recalcAutoBackground);
        using (new EditorGUI.DisabledScope(recalcAutoBackground))
        {
            recalcBackgroundColor = EditorGUILayout.ColorField("Background Color", recalcBackgroundColor);
        }

        EditorGUILayout.BeginHorizontal();
        bool canRecalculate = levelData.usesTiles
            ? levelData.tileSources != null && levelData.tileSources.Length > 0
            : recalcSourceTexture != null;
        using (new EditorGUI.DisabledScope(!canRecalculate))
        {
            string recalcLabel = levelData.usesTiles ? "Recalculate from Tiles" : "Recalculate from Source Image";
            if (GUILayout.Button(recalcLabel, GUILayout.Height(28f)))
            {
                Undo.RecordObject(levelData, "Recalculate Colors From Source");
                bool success = LevelDataColorRecalculator.RecalculateFromTexture(
                    levelData,
                    recalcSourceTexture,
                    recalcTargetPalette,
                    recalcColorCount,
                    recalcAutoColorCount,
                    recalcAutoBackground,
                    recalcBackgroundColor);
                if (success)
                {
                    EditorUtility.SetDirty(levelData);
                }
            }
        }

        using (new EditorGUI.DisabledScope(recalcTargetPalette == null))
        {
            if (GUILayout.Button("Remap to Palette", GUILayout.Height(28f)))
            {
                Undo.RecordObject(levelData, "Remap Level To Palette");
                bool success = LevelDataColorRecalculator.RemapToPalette(
                    levelData,
                    recalcTargetPalette,
                    recalcColorCount);
                if (success)
                {
                    EditorUtility.SetDirty(levelData);
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPaletteEditor(LevelData levelData)
    {
        EditorGUILayout.Space();
        paletteFoldout = EditorGUILayout.Foldout(paletteFoldout, "Colors In Drawing", true);
        if (!paletteFoldout)
        {
            return;
        }

        SerializedProperty paletteProp = serializedObject.FindProperty("palette");
        if (paletteProp == null || !paletteProp.isArray)
        {
            EditorGUILayout.HelpBox("Palette is missing.", MessageType.Warning);
            return;
        }

        byte[] usedColors = levelData.GetUsedPaletteColorsByFrequency();
        if (usedColors.Length == 0)
        {
            EditorGUILayout.HelpBox("No palette colors found in the drawing.", MessageType.Info);
            return;
        }

        for (int i = 0; i < usedColors.Length; i++)
        {
            byte colorIndex = usedColors[i];
            int arrayIndex = colorIndex - 1;
            if (arrayIndex < 0 || arrayIndex >= paletteProp.arraySize)
            {
                continue;
            }

            SerializedProperty colorProp = paletteProp.GetArrayElementAtIndex(arrayIndex);
            Color32 paletteColor = colorProp.colorValue;
            int cellCount = levelData.CountLevelCellsForColor(colorIndex);

            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch(paletteColor, 24f);

            Color32 editedColor = PaletteColorField.DrawLayout(paletteColor, GUILayout.MinWidth(56f));
            if (!PaletteColorUtility.ColorsEqual(paletteColor, editedColor))
            {
                colorProp.colorValue = editedColor;
                paletteColor = editedColor;
            }

            EditorGUILayout.LabelField(
                string.Format("Color {0}  ({1} cells)  {2}", colorIndex, cellCount, FormatHex(paletteColor)),
                GUILayout.MinWidth(80f));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawBucketsEditor(LevelData levelData)
    {
        EditorGUILayout.Space();
        bucketsFoldout = EditorGUILayout.Foldout(bucketsFoldout, "Buckets", true);
        if (!bucketsFoldout)
        {
            return;
        }

        if (levelData.buckets == null || levelData.buckets.Length == 0)
        {
            EditorGUILayout.HelpBox("No buckets defined.", MessageType.Info);
            return;
        }

        for (int i = 0; i < levelData.buckets.Length; i++)
        {
            BucketSpawn spawn = levelData.buckets[i];
            if (!levelData.IsColorUsedInLevel(spawn.colorIndex))
            {
                continue;
            }

            Color32 paletteColor = levelData.GetPaletteColor(spawn.colorIndex);

            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch(paletteColor, 24f);
            EditorGUILayout.LabelField(
                string.Format(
                    "Color {0}: {1} bucket(s), {2} cells, {3}",
                    spawn.colorIndex,
                    spawn.count,
                    levelData.CountLevelCellsForColor(spawn.colorIndex),
                    FormatHex(paletteColor)));
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void DrawCellsInfo(LevelData levelData)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cells", EditorStyles.boldLabel);
        int expected = levelData.gridWidth * levelData.gridHeight;
        int actual = levelData.cells != null ? levelData.cells.Length : 0;
        EditorGUILayout.LabelField("Data Size", string.Format("{0} / {1}", actual, expected));
        if (actual != expected)
        {
            EditorGUILayout.HelpBox("Cell data size does not match grid dimensions.", MessageType.Warning);
        }
    }

    private void DrawPreview(LevelData levelData)
    {
        Texture2D texture = previews.Get(levelData);
        if (texture == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        float maxSize = 280f;
        float aspect = texture.height > 0
            ? texture.width / (float)texture.height
            : 1f;
        float displayWidth = aspect >= 1f ? maxSize : maxSize * aspect;
        float displayHeight = aspect >= 1f ? maxSize / aspect : maxSize;
        Rect rect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
    }

    private void DrawBucketLayoutEditor(LevelData levelData)
    {
        EditorGUILayout.Space();
        layoutFoldout = EditorGUILayout.Foldout(layoutFoldout, "Bucket Layout (5x5)", true);
        if (!layoutFoldout)
        {
            return;
        }

        byte[] usedColors = levelData.GetUsedPaletteColorsByFrequency();
        if (usedColors.Length == 0)
        {
            EditorGUILayout.HelpBox("No palette colors found in the drawing.", MessageType.Info);
            return;
        }

        if (selectedLayoutColor == 0 || !levelData.IsColorUsedInLevel(selectedLayoutColor))
        {
            selectedLayoutColor = usedColors[0];
        }

        EditorGUILayout.LabelField("Paint Color", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        bool colorMode = paintMode == LayoutPaintMode.Color;
        if (GUILayout.Toggle(colorMode, "Bucket", "MiniButton", GUILayout.Height(24f)))
        {
            paintMode = LayoutPaintMode.Color;
        }

        for (int i = 0; i < usedColors.Length; i++)
        {
            byte colorIndex = usedColors[i];
            Color32 paletteColor = levelData.GetPaletteColor(colorIndex);
            bool selected = paintMode == LayoutPaintMode.Color && selectedLayoutColor == colorIndex;

            Rect buttonRect = GUILayoutUtility.GetRect(36f, 24f, GUILayout.Width(36f));
            EditorGUI.DrawRect(buttonRect, (Color)paletteColor);
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(buttonRect.x, buttonRect.y, buttonRect.width, 2f), Color.white);
            }

            if (GUI.Button(buttonRect, colorIndex.ToString(), EditorStyles.centeredGreyMiniLabel))
            {
                paintMode = LayoutPaintMode.Color;
                selectedLayoutColor = colorIndex;
            }
        }

        if (GUILayout.Toggle(paintMode == LayoutPaintMode.Wall, "Wall", "MiniButton", GUILayout.Height(24f), GUILayout.Width(64f)))
        {
            paintMode = LayoutPaintMode.Wall;
        }

        if (GUILayout.Toggle(paintMode == LayoutPaintMode.Erase, "Erase", "MiniButton", GUILayout.Height(24f), GUILayout.Width(64f)))
        {
            paintMode = LayoutPaintMode.Erase;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Top row = front. Side exit needs a clear path in front and in the bucket row.",
            EditorStyles.miniLabel);
        for (int y = 0; y < LevelData.BucketLayoutRows; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < LevelData.BucketLayoutColumns; x++)
            {
                byte cell = levelData.GetBucketLayoutCell(x, y);
                DrawLayoutCellButton(levelData, x, y, cell);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Layout"))
        {
            Undo.RecordObject(levelData, "Clear Bucket Layout");
            for (int i = 0; i < levelData.bucketLayout.Length; i++)
            {
                levelData.bucketLayout[i] = 0;
            }

            EditorUtility.SetDirty(levelData);
        }

        if (GUILayout.Button("Regenerate From Rules"))
        {
            AutoFillLayoutFromSand(levelData);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLayoutCellButton(LevelData levelData, int x, int y, byte cell)
    {
        Color background;
        if (LevelData.IsBucketLayoutWall(cell))
        {
            background = new Color(0.35f, 0.35f, 0.38f, 0.95f);
        }
        else if (cell == LevelData.BucketLayoutEmpty)
        {
            background = new Color(1f, 1f, 1f, 0.12f);
        }
        else
        {
            background = (Color)levelData.GetPaletteColor(cell);
        }

        Rect rect = GUILayoutUtility.GetRect(36f, 36f, GUILayout.Width(36f), GUILayout.Height(36f));
        EditorGUI.DrawRect(rect, background);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            Undo.RecordObject(levelData, "Paint Bucket Layout");
            switch (paintMode)
            {
                case LayoutPaintMode.Wall:
                    levelData.SetBucketLayoutWall(x, y, true);
                    break;
                case LayoutPaintMode.Erase:
                    levelData.SetBucketLayoutCell(x, y, LevelData.BucketLayoutEmpty);
                    break;
                default:
                    levelData.SetBucketLayoutCell(x, y, selectedLayoutColor);
                    break;
            }

            EditorUtility.SetDirty(levelData);
        }

        if (LevelData.IsBucketLayoutBucket(cell))
        {
            GUI.Label(rect, cell.ToString(), EditorStyles.centeredGreyMiniLabel);
        }
        else if (LevelData.IsBucketLayoutWall(cell))
        {
            GUI.Label(rect, "W", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private static void AutoFillLayoutFromSand(LevelData levelData)
    {
        ApplyBucketRules(levelData);
    }

    private void DrawBucketRulesSection(LevelData levelData)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bucket Generation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bucketSettingsOverride"));

        if (LevelEditorUtility.TryFindLevelInDatabase(levelData, out LevelDatabase database, out int levelIndex))
        {
            EditorGUILayout.LabelField("Level Database Index", levelIndex.ToString());
            LevelSegmentRules segment = database.GetSegmentRulesForLevel(levelIndex);
            if (levelData.bucketSettingsOverride != null)
            {
                EditorGUILayout.LabelField(
                    "Resolved Settings",
                    levelData.bucketSettingsOverride.name + " (override)",
                    EditorStyles.miniLabel);
            }
            else if (segment != null)
            {
                string maxGrains = segment.maxGrainsPerBucket > 0
                    ? string.Format(", max {0} grains/bucket", segment.maxGrainsPerBucket)
                    : string.Empty;
                EditorGUILayout.LabelField(
                    "Resolved Settings",
                    string.Format("{0} ({1}){2}", segment.displayName, segment.fillMode, maxGrains),
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("No segment rules for this level index. Legacy auto-fill will be used.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Level is not in a Level Database. Assign bucketSettingsOverride or add to database.", MessageType.Info);
        }

        if (GUILayout.Button("Apply Bucket Rules", GUILayout.Height(24f)))
        {
            ApplyBucketRules(levelData);
        }
    }

    private static void ApplyBucketRules(LevelData levelData)
    {
        Undo.RecordObject(levelData, "Apply Bucket Rules");
        LevelEditorUtility.ApplyBucketLayoutRules(levelData);
        EditorUtility.SetDirty(levelData);
    }

    private static void DrawColorSwatch(Color32 color, float size)
    {
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(rect, (Color)color);
    }

    private static string FormatHex(Color32 color)
    {
        if (color.a == 255)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.r, color.g, color.b);
        }

        return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.r, color.g, color.b, color.a);
    }
}
