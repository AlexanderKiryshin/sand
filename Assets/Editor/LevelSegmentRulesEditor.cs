using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelSegmentRules))]
public class LevelSegmentRulesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startLevelIndex"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelCount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bucketSettings"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultPalette"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGridWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGridHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultColorCount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultAutoColorCount"));

        LevelSegmentRules rules = (LevelSegmentRules)target;
        string rangeText = rules.levelCount > 0
            ? string.Format("Levels {0}..{1} ({2} levels)", rules.startLevelIndex, rules.startLevelIndex + rules.levelCount - 1, rules.levelCount)
            : string.Format("Levels {0}+ (until next segment)", rules.startLevelIndex);
        EditorGUILayout.HelpBox(rangeText, MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }
}
