using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelData levelData = (LevelData)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Level Cells", levelData.CountTotalLevelCells().ToString());
        EditorGUILayout.LabelField("Total Grains", levelData.CountTotalGrains().ToString());
        EditorGUILayout.LabelField("Total Buckets", levelData.GetTotalBucketCount().ToString());

        if (levelData.previewTexture != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            float size = 256f;
            Rect rect = GUILayoutUtility.GetRect(size, size);
            EditorGUI.DrawPreviewTexture(rect, levelData.previewTexture);
        }
    }
}
