using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelSegmentRules))]
public class LevelSegmentRulesEditor : Editor
{
    private enum TemplatePaintMode
    {
        AutoSlot,
        FixedColor,
        Wall,
        ConveyorSlot
    }

    private TemplatePaintMode paintMode = TemplatePaintMode.AutoSlot;
    private byte selectedColorIndex = 1;
    private ConveyorDirection selectedConveyorDirection = ConveyorDirection.Front;
    private int selectedConveyorCount = LevelData.DefaultConveyorBucketCount;
    private bool layoutFoldout = true;

    public override void OnInspectorGUI()
    {
        LevelSegmentRules rules = (LevelSegmentRules)target;
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startLevelIndex"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelCount"));

        string rangeText = rules.levelCount > 0
            ? string.Format("Levels {0}..{1} ({2} levels)", rules.startLevelIndex, rules.startLevelIndex + rules.levelCount - 1, rules.levelCount)
            : string.Format("Levels {0}+ (until next segment)", rules.startLevelIndex);
        EditorGUILayout.HelpBox(rangeText, MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultPalette"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGridWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGridHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultColorCount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultAutoColorCount"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bucket Layout", EditorStyles.boldLabel);
        rules.EnsureTemplate();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fillMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxGrainsPerBucket"));

        BucketLayoutFillMode fillMode = (BucketLayoutFillMode)serializedObject.FindProperty("fillMode").enumValueIndex;
        if (fillMode == BucketLayoutFillMode.RandomComponents)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "RandomComponents: walls and conveyors from the list below are placed randomly on each level load. The 5x5 paint template is ignored.",
                MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomComponents"), true);
        }
        else
        {
            EditorGUILayout.Space();
            layoutFoldout = EditorGUILayout.Foldout(layoutFoldout, "Layout Template (5x5)", true);
            if (layoutFoldout)
            {
                DrawTemplatePainter(rules);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTemplatePainter(LevelSegmentRules settings)
    {
        EditorGUILayout.HelpBox(
            "Auto = empty slot filled by rules. Wall = blocked cell. Fixed color = palette index locked in template. Conveyor = dispenser with limited buckets.",
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

        if (GUILayout.Toggle(paintMode == TemplatePaintMode.ConveyorSlot, "Conveyor", "MiniButton", GUILayout.Height(24f), GUILayout.Width(88f)))
        {
            paintMode = TemplatePaintMode.ConveyorSlot;
        }

        EditorGUILayout.EndHorizontal();

        if (paintMode == TemplatePaintMode.ConveyorSlot)
        {
            EditorGUILayout.LabelField("Conveyor Direction", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            DrawDirectionButton(ConveyorDirection.Front, "Front");
            DrawDirectionButton(ConveyorDirection.Back, "Back");
            DrawDirectionButton(ConveyorDirection.Left, "Left");
            DrawDirectionButton(ConveyorDirection.Right, "Right");
            EditorGUILayout.EndHorizontal();

            selectedConveyorCount = EditorGUILayout.IntSlider(
                "Buckets Inside",
                selectedConveyorCount,
                1,
                20);
        }

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
                settings.conveyorDirectionTemplate[i] = 0;
                settings.conveyorBucketCountTemplate[i] = 0;
            }

            EditorUtility.SetDirty(settings);
        }

        if (GUILayout.Button("Fill Auto Slots"))
        {
            Undo.RecordObject(settings, "Fill Auto Slots");
            for (int i = 0; i < settings.layoutTemplate.Length; i++)
            {
                if (!LevelData.IsBucketLayoutWall(settings.layoutTemplate[i])
                    && !LevelData.IsBucketLayoutConveyorSlot(settings.layoutTemplate[i]))
                {
                    settings.layoutTemplate[i] = LevelData.BucketLayoutEmpty;
                }
            }

            EditorUtility.SetDirty(settings);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDirectionButton(ConveyorDirection direction, string label)
    {
        if (GUILayout.Toggle(selectedConveyorDirection == direction, label, "MiniButton", GUILayout.Height(22f)))
        {
            selectedConveyorDirection = direction;
        }
    }

    private void DrawTemplateCellButton(LevelSegmentRules settings, int x, int y)
    {
        byte cell = settings.GetTemplateCell(x, y);
        Color background;
        string label;

        if (LevelData.IsBucketLayoutWall(cell))
        {
            background = new Color(0.2f, 0.2f, 0.2f, 1f);
            label = "W";
        }
        else if (LevelData.IsBucketLayoutConveyorSlot(cell))
        {
            background = new Color(0.35f, 0.45f, 0.65f, 1f);
            label = "C" + settings.GetConveyorBucketCountTemplate(x, y);
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
                case TemplatePaintMode.ConveyorSlot:
                    settings.SetTemplateCell(x, y, LevelData.BucketLayoutConveyorSlot);
                    settings.SetConveyorDirectionTemplate(x, y, selectedConveyorDirection);
                    settings.SetConveyorBucketCountTemplate(x, y, selectedConveyorCount);
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
