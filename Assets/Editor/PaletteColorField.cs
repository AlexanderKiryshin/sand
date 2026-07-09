using UnityEditor;
using UnityEngine;

/// <summary>
/// ColorField для палитры. showAlpha = true обязателен: иначе Unity 2019 при закрытии
/// пикера часто сбрасывает RGB в ноль и цвет сохраняется чёрным.
/// </summary>
public static class PaletteColorField
{
    public static Color32 DrawLayout(Color32 value, params GUILayoutOption[] options)
    {
        return DrawLayout(GUIContent.none, value, options);
    }

    public static Color32 DrawLayout(GUIContent label, Color32 value, params GUILayoutOption[] options)
    {
        Color edited = EditorGUILayout.ColorField(
            label,
            PaletteColorUtility.ToColorFieldValue(value),
            true,
            true,
            false,
            options);
        return PaletteColorUtility.FromColorFieldValue(edited);
    }

    public static Color32 DrawRect(Rect rect, GUIContent label, Color32 value)
    {
        Color edited = EditorGUI.ColorField(
            rect,
            label,
            PaletteColorUtility.ToColorFieldValue(value),
            true,
            true,
            false);
        return PaletteColorUtility.FromColorFieldValue(edited);
    }
}
