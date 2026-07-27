using UnityEngine;

[CreateAssetMenu(fileName = "GameDebugSettings", menuName = "Sand/Game Debug Settings")]
public class GameDebugSettings : ScriptableObject
{
    private const string ResourcesPath = "Settings/GameDebugSettings";

    private static GameDebugSettings cached;

    [Tooltip("Shows a skip-level button in the HUD for testing progression.")]
    public bool showSkipLevelButton;

    public static GameDebugSettings Get()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = Resources.Load<GameDebugSettings>(ResourcesPath);
        return cached;
    }

    public static bool ShowSkipLevelButton
    {
        get
        {
            GameDebugSettings settings = Get();
            return settings != null && settings.showSkipLevelButton;
        }
    }
}
