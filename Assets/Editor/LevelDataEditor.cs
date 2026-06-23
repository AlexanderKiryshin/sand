using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum LayoutPaintMode
    {
        Color,
        Wall,
        Erase
    }

    private byte selectedLayoutColor;
    private LayoutPaintMode paintMode = LayoutPaintMode.Color;
    private bool layoutFoldout = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelData levelData = (LevelData)target;
        levelData.EnsureBucketLayout();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Level Cells", levelData.CountTotalLevelCells().ToString());
        EditorGUILayout.LabelField("Total Grains", levelData.CountTotalGrains().ToString());
        EditorGUILayout.LabelField("Layout Buckets", levelData.CountBucketsInLayout().ToString());
        EditorGUILayout.LabelField("Total Buckets", levelData.GetTotalBucketCount().ToString());

        DrawBucketLayoutEditor(levelData);

        if (levelData.previewTexture != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            float size = 256f;
            Rect rect = GUILayoutUtility.GetRect(size, size);
            EditorGUI.DrawPreviewTexture(rect, levelData.previewTexture);
        }
    }

    private void DrawBucketLayoutEditor(LevelData levelData)
    {
        EditorGUILayout.Space();
        layoutFoldout = EditorGUILayout.Foldout(layoutFoldout, "Bucket Layout (5x5)", true);
        if (!layoutFoldout)
        {
            return;
        }

        byte[] usedColors = levelData.GetColorsUsedInLevel();
        if (usedColors.Length == 0)
        {
            EditorGUILayout.HelpBox("No sand colors on the level image yet.", MessageType.Info);
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
            Color buttonColor = SandDisplayColors.FromPalette(paletteColor);
            bool selected = paintMode == LayoutPaintMode.Color && selectedLayoutColor == colorIndex;

            GUI.backgroundColor = buttonColor;
            if (GUILayout.Toggle(selected, " " + colorIndex, "MiniButton", GUILayout.Height(24f)))
            {
                paintMode = LayoutPaintMode.Color;
                selectedLayoutColor = colorIndex;
            }
        }

        GUI.backgroundColor = Color.white;
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

        if (GUILayout.Button("Auto Fill From Sand"))
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
            background = (Color)SandDisplayColors.FromPalette(levelData.GetPaletteColor(cell));
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
        Undo.RecordObject(levelData, "Auto Fill Bucket Layout");
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

        EditorUtility.SetDirty(levelData);
    }
}
