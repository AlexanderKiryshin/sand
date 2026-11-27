using UnityEditor;

internal static class GameDebugMenu
{
    private const string UnlockBackgroundsMenu = "Tools/Sand/Unlock All Backgrounds";
    private const string UnlockLevelsMenu = "Tools/Sand/Unlock All Levels";

    [MenuItem(UnlockBackgroundsMenu)]
    private static void ToggleBackgrounds() =>
        Toggle(UnlockBackgroundsMenu, nameof(GameDebugSettings.unlockAllBackgrounds));

    [MenuItem(UnlockLevelsMenu)]
    private static void ToggleLevels() =>
        Toggle(UnlockLevelsMenu, nameof(GameDebugSettings.unlockAllLevels));

    [MenuItem(UnlockBackgroundsMenu, true)]
    private static bool ValidateBackgrounds() =>
        Validate(UnlockBackgroundsMenu, GameDebugSettings.UnlockAllBackgrounds);

    [MenuItem(UnlockLevelsMenu, true)]
    private static bool ValidateLevels() =>
        Validate(UnlockLevelsMenu, GameDebugSettings.UnlockAllLevels);

    private static bool Validate(string menuPath, bool enabled)
    {
        Menu.SetChecked(menuPath, enabled);
        return GameDebugSettings.Get() != null;
    }

    private static void Toggle(string menuPath, string fieldName)
    {
        GameDebugSettings settings = GameDebugSettings.Get();
        if (settings == null) return;

        using var serializedSettings = new SerializedObject(settings);
        using var option = serializedSettings.FindProperty(fieldName);
        bool enabled = !option.boolValue;
        option.boolValue = enabled;
        serializedSettings.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(settings);
        Menu.SetChecked(menuPath, enabled);
    }
}
