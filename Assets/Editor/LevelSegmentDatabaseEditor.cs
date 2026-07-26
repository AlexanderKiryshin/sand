using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(LevelSegmentDatabase))]
public class LevelSegmentDatabaseEditor : Editor
{
    private ReorderableList segmentList;
    private SerializedProperty segmentsProp;

    private void OnEnable()
    {
        segmentsProp = serializedObject.FindProperty("segments");
        segmentList = new ReorderableList(serializedObject, segmentsProp, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Segments (ordered by start index)"),
            drawElementCallback = DrawSegmentElement,
            elementHeight = EditorGUIUtility.singleLineHeight + 6f
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LevelSegmentDatabase database = (LevelSegmentDatabase)target;
        segmentList.DoLayoutList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Coverage Preview", EditorStyles.boldLabel);
        if (database.segments == null || database.segments.Length == 0)
        {
            EditorGUILayout.HelpBox("Add segment rules to define bucket layout and import defaults per level range.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < database.segments.Length; i++)
            {
                LevelSegmentRules segment = database.segments[i];
                if (segment == null)
                {
                    continue;
                }

                int nextStart = GetNextStart(database.segments, segment.startLevelIndex);
                int endExclusive = segment.GetEndLevelIndexExclusive(nextStart);
                string endLabel = endExclusive == int.MaxValue ? "∞" : (endExclusive - 1).ToString();
                EditorGUILayout.LabelField(
                    string.Format(
                        "{0}: levels {1}..{2} | fill={3}",
                        string.IsNullOrEmpty(segment.displayName) ? segment.segmentId : segment.displayName,
                        segment.startLevelIndex,
                        endLabel,
                        segment.fillMode),
                    EditorStyles.miniLabel);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static int GetNextStart(LevelSegmentRules[] segments, int currentStart)
    {
        int nextStart = int.MaxValue;
        for (int i = 0; i < segments.Length; i++)
        {
            LevelSegmentRules segment = segments[i];
            if (segment == null || segment.startLevelIndex <= currentStart)
            {
                continue;
            }

            if (segment.startLevelIndex < nextStart)
            {
                nextStart = segment.startLevelIndex;
            }
        }

        return nextStart;
    }

    private void DrawSegmentElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        SerializedProperty element = segmentsProp.GetArrayElementAtIndex(index);
        EditorGUI.PropertyField(rect, element, GUIContent.none);
    }
}
