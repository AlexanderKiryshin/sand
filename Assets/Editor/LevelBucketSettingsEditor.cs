using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelBucketSettings))]
public class LevelBucketSettingsEditor : Editor
{
    private enum TemplatePaintMode
    {
        AutoSlot,
        FixedColor,
        Wall
    }

    private TemplatePaintMode paintMode = TemplatePaintMode.AutoSlot;
    private byte selectedColorIndex = 1;
    private bool layoutFoldout = true;

    public override void OnInspectorGUI()
    {
        LevelBucketSettings settings = (LevelBucketSettings)target;
        serializedObject.Update();

        settings.EnsureTemplate();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fillMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxGrainsPerBucket"));

        EditorGUILayout.Space();
        layoutFoldout = EditorGUILayout.Foldout(layoutFoldout, "Layout Template (5x5)", true);
        if (layoutFoldout)
        {
            DrawTemplatePainter(settings);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTemplatePainter(LevelBucketSettings settings)
    {
        EditorGUILayout.HelpBox(
            "Auto = empty slot filled by rules. Wall = blocked cell. Fixed color = palette index locked in template.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(paintMode == TemplatePaintMode.AutoSlot, "Auto", "MiniButton", GUILayout.Height(24f)))
        {
            paintMode = TemplatePaintMode.AutoSlot;
        }

        if (GUILayout.Toggle(paintMode == TemplatePaintMode.Wall, "Wall", "MiniButton", GUILayout.Height(24f), GUILayout.Width(64f)))
        {
            paintMode = TemplatePaintMode.Wall;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Fixed Color Index", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        for (byte colorIndex = 1; colorIndex <= 8; colorIndex++)
        {
            bool selected = paintMode == TemplatePaintMode.FixedColor && selectedColorIndex == colorIndex;
            if (GUILayout.Toggle(selected, colorIndex.ToString(), "MiniButton", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                paintMode = TemplatePaintMode.FixedColor;
                selectedColorIndex = colorIndex;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Top row = front (same as runtime bucket layout).", EditorStyles.miniLabel);
        for (int y = 0; y < LevelData.BucketLayoutRows; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < LevelData.BucketLayoutColumns; x++)
            {
                DrawTemplateCellButton(settings, x, y);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Template"))
        {
            Undo.RecordObject(settings, "Clear Bucket Template");
            for (int i = 0; i < settings.layoutTemplate.Length; i++)
            {
                settings.layoutTemplate[i] = LevelData.BucketLayoutEmpty;
            }

            EditorUtility.SetDirty(settings);
        }

        if (GUILayout.Button("Fill Auto Slots"))
        {
            Undo.RecordObject(settings, "Fill Auto Slots");
            for (int i = 0; i < settings.layoutTemplate.Length; i++)
            {
                if (!LevelData.IsBucketLayoutWall(settings.layoutTemplate[i]))
                {
                    settings.layoutTemplate[i] = LevelData.BucketLayoutEmpty;
                }
            }

            EditorUtility.SetDirty(settings);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTemplateCellButton(LevelBucketSettings settings, int x, int y)
    {
        byte cell = settings.GetTemplateCell(x, y);
        Color background;
        string label;

        if (LevelData.IsBucketLayoutWall(cell))
        {
            background = new Color(0.2f, 0.2f, 0.2f, 1f);
            label = "W";
        }
        else if (LevelData.IsBucketLayoutBucket(cell))
        {
            background = Color.HSVToRGB((cell * 0.11f) % 1f, 0.55f, 0.95f);
            label = cell.ToString();
        }
        else
        {
            background = new Color(0.85f, 0.85f, 0.85f, 1f);
            label = "A";
        }

        Rect rect = GUILayoutUtility.GetRect(36f, 36f, GUILayout.Width(36f), GUILayout.Height(36f));
        EditorGUI.DrawRect(rect, background);
        GUI.Label(rect, label, EditorStyles.centeredGreyMiniLabel);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            Undo.RecordObject(settings, "Paint Bucket Template");
            switch (paintMode)
            {
                case TemplatePaintMode.Wall:
                    settings.SetTemplateCell(x, y, LevelData.BucketLayoutWall);
                    break;
                case TemplatePaintMode.FixedColor:
                    settings.SetTemplateCell(x, y, selectedColorIndex);
                    break;
                default:
                    settings.SetTemplateCell(x, y, LevelData.BucketLayoutEmpty);
                    break;
            }

            EditorUtility.SetDirty(settings);
        }
    }
}
