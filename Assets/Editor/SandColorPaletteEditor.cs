using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SandColorPalette))]
public class SandColorPaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SandColorPalette palette = (SandColorPalette)target;
        serializedObject.Update();

        SerializedProperty colorsProp = serializedObject.FindProperty("colors");
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);

        int newCount = EditorGUILayout.IntField("Count", colorsProp.arraySize);
        colorsProp.arraySize = Mathf.Clamp(newCount, 0, SandColorPalette.MaxColorCount);

        for (int i = 0; i < colorsProp.arraySize; i++)
        {
            SerializedProperty element = colorsProp.GetArrayElementAtIndex(i);
            Color32 current = element.colorValue;
            Color32 edited = PaletteColorField.DrawLayout(new GUIContent("Color " + (i + 1)), current);
            if (!PaletteColorUtility.ColorsEqual(current, edited))
            {
                element.colorValue = edited;
            }
        }

        serializedObject.ApplyModifiedProperties();

        Color32[] colors = palette.GetColors();
        if (colors.Length == 0)
        {
            EditorGUILayout.HelpBox("Add at least one color.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < colors.Length; i++)
        {
            Rect rect = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(28f));
            EditorGUI.DrawRect(rect, colors[i]);
            Rect labelRect = new Rect(rect.x, rect.y + 30f, rect.width, 16f);
            GUI.Label(labelRect, (i + 1).ToString());
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(16f);
    }
}
