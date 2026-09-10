using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(LevelDatabase))]
public class LevelDatabaseEditor : Editor
{
    private const float RowHeight = 92f;
    private const float ThumbSize = 60f;
    private const float ButtonColumnWidth = 26f;
    private const float SwatchSize = 16f;
    private const float SwatchSpacing = 2f;
    private const int MaxSwatchRows = 2;

    private ReorderableList levelList;
    private SerializedProperty levelsProp;
    private LevelData levelToAdd;

    private readonly LevelPreviewCache previews = new LevelPreviewCache();

    private void OnEnable()
    {
        levelsProp = serializedObject.FindProperty("levels");
        BuildList();
    }

    private void OnDisable()
    {
        previews.Clear();
    }

    private void BuildList()
    {
        levelList = new ReorderableList(serializedObject, levelsProp, true, true, false, false)
        {
            elementHeight = RowHeight,
            drawHeaderCallback = DrawHeader,
            drawElementCallback = DrawElement
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (levelList == null || levelList.serializedProperty != levelsProp)
        {
            BuildList();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentDatabase"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            string.Format("Levels ({0})", levelsProp.arraySize),
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Drag the handle to reorder, or use ▲▼ for quick swaps.",
            EditorStyles.miniLabel);

        levelList.DoLayoutList();

        DrawAddRemoveControls();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(levelsProp.arraySize == 0))
        {
            if (GUILayout.Button("Apply Bucket Rules To All Levels", GUILayout.Height(24f)))
            {
                ApplyBucketRulesToAll((LevelDatabase)target);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void ApplyBucketRulesToAll(LevelDatabase database)
    {
        if (database == null || database.levels == null)
        {
            return;
        }

        for (int i = 0; i < database.levels.Length; i++)
        {
            LevelData level = database.levels[i];
            if (level == null)
            {
                continue;
            }

            database.ApplyBucketLayoutRules(level, i);
            EditorUtility.SetDirty(level);
        }

        AssetDatabase.SaveAssets();
    }

    private void DrawHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "Level Order");
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < 0 || index >= levelsProp.arraySize)
        {
            return;
        }

        SerializedProperty element = levelsProp.GetArrayElementAtIndex(index);
        LevelData level = element.objectReferenceValue as LevelData;

        Rect thumbRect = new Rect(rect.x + 2f, rect.y + 6f, ThumbSize, ThumbSize);
        DrawThumbnail(thumbRect, level);

        float buttonsX = rect.xMax - ButtonColumnWidth;
        float contentX = thumbRect.xMax + 8f;
        float contentWidth = buttonsX - contentX - 6f;

        Rect indexRect = new Rect(contentX, rect.y + 6f, 28f, 18f);
        EditorGUI.LabelField(indexRect, index.ToString(), EditorStyles.boldLabel);

        Rect objectRect = new Rect(contentX + 30f, rect.y + 6f, contentWidth - 30f, 18f);
        EditorGUI.BeginChangeCheck();
        Object newLevel = EditorGUI.ObjectField(objectRect, level, typeof(LevelData), false);
        if (EditorGUI.EndChangeCheck())
        {
            element.objectReferenceValue = newLevel;
            level = newLevel as LevelData;
        }

        if (level != null)
        {
            Rect infoRect = new Rect(contentX, rect.y + 27f, contentWidth, 16f);
            EditorGUI.LabelField(infoRect, BuildLevelInfo(level), EditorStyles.miniLabel);

            Rect swatchRect = new Rect(
                contentX,
                rect.y + 46f,
                contentWidth,
                SwatchSize * MaxSwatchRows + SwatchSpacing * (MaxSwatchRows - 1));
            DrawColorSwatches(swatchRect, level);
        }
        else
        {
            Rect missingRect = new Rect(contentX, rect.y + 30f, contentWidth, 18f);
            EditorGUI.LabelField(missingRect, "Missing level reference", EditorStyles.miniLabel);
        }

        DrawRowButtons(new Rect(buttonsX, rect.y + 4f, ButtonColumnWidth, RowHeight - 8f), index);
    }

    private void DrawRowButtons(Rect rect, int index)
    {
        float h = 20f;
        Rect upRect = new Rect(rect.x, rect.y, rect.width, h);
        Rect downRect = new Rect(rect.x, rect.y + h + 2f, rect.width, h);
        Rect removeRect = new Rect(rect.x, rect.y + (h + 2f) * 2f, rect.width, h);

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUI.Button(upRect, "▲"))
            {
                levelsProp.MoveArrayElement(index, index - 1);
            }
        }

        using (new EditorGUI.DisabledScope(index >= levelsProp.arraySize - 1))
        {
            if (GUI.Button(downRect, "▼"))
            {
                levelsProp.MoveArrayElement(index, index + 1);
            }
        }

        if (GUI.Button(removeRect, "✕"))
        {
            RemoveAt(index);
        }
    }

    private void DrawThumbnail(Rect rect, LevelData level)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
        if (level == null)
        {
            GUI.Label(rect, "No\npreview", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Texture2D texture = previews.Get(level);
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Label(rect, "No\npreview", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawColorSwatches(Rect rect, LevelData level)
    {
        byte[] usedColors = level.GetUsedPaletteColorsByFrequency();
        if (usedColors.Length == 0)
        {
            EditorGUI.LabelField(rect, "No colors", EditorStyles.miniLabel);
            return;
        }

        float step = SwatchSize + SwatchSpacing;
        int columns = Mathf.Max(1, Mathf.FloorToInt((rect.width + SwatchSpacing) / step));
        int capacity = columns * MaxSwatchRows;
        int reserveForMore = usedColors.Length > capacity ? 1 : 0;
        int maxSwatches = capacity - reserveForMore;

        float x = rect.x;
        int row = 0;
        int drawn = 0;
        for (int i = 0; i < usedColors.Length && drawn < maxSwatches; i++)
        {
            if (drawn > 0 && drawn % columns == 0)
            {
                row++;
                x = rect.x;
            }

            float y = rect.y + row * step;
            Rect swatch = new Rect(x, y, SwatchSize, SwatchSize);
            EditorGUI.DrawRect(swatch, level.GetPaletteColor(usedColors[i]));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y, swatch.width, 1f), new Color(0f, 0f, 0f, 0.3f));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.yMax - 1f, swatch.width, 1f), new Color(0f, 0f, 0f, 0.3f));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y, 1f, swatch.height), new Color(0f, 0f, 0f, 0.3f));
            EditorGUI.DrawRect(new Rect(swatch.xMax - 1f, swatch.y, 1f, swatch.height), new Color(0f, 0f, 0f, 0.3f));
            x += step;
            drawn++;
        }

        int remaining = usedColors.Length - drawn;
        if (remaining > 0)
        {
            float y = rect.y + row * step;
            Rect moreRect = new Rect(x + 2f, y, 40f, SwatchSize);
            EditorGUI.LabelField(moreRect, "+" + remaining, EditorStyles.miniLabel);
        }
    }

    private static string BuildLevelInfo(LevelData level)
    {
        int colorCount = level.GetUsedPaletteColorsByFrequency().Length;
        return string.Format(
            "{0}×{1}  •  {2} colors  •  {3} cells",
            level.gridWidth,
            level.gridHeight,
            colorCount,
            level.CountTotalLevelCells());
    }

    private void DrawAddRemoveControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Level", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        levelToAdd = (LevelData)EditorGUILayout.ObjectField(levelToAdd, typeof(LevelData), false);
        using (new EditorGUI.DisabledScope(levelToAdd == null))
        {
            if (GUILayout.Button("Add", GUILayout.Width(60f)))
            {
                AddLevel(levelToAdd);
                levelToAdd = null;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove Empty Entries"))
        {
            RemoveEmptyEntries();
        }

        if (GUILayout.Button("Remove Duplicates"))
        {
            RemoveDuplicates();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void AddLevel(LevelData level)
    {
        int index = levelsProp.arraySize;
        levelsProp.arraySize++;
        levelsProp.GetArrayElementAtIndex(index).objectReferenceValue = level;
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= levelsProp.arraySize)
        {
            return;
        }

        levelsProp.GetArrayElementAtIndex(index).objectReferenceValue = null;
        levelsProp.DeleteArrayElementAtIndex(index);
    }

    private void RemoveEmptyEntries()
    {
        for (int i = levelsProp.arraySize - 1; i >= 0; i--)
        {
            if (levelsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                levelsProp.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private void RemoveDuplicates()
    {
        HashSet<Object> seen = new HashSet<Object>();
        for (int i = 0; i < levelsProp.arraySize; i++)
        {
            Object value = levelsProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value == null)
            {
                continue;
            }

            if (!seen.Add(value))
            {
                levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                levelsProp.DeleteArrayElementAtIndex(i);
                i--;
            }
        }
    }
}
