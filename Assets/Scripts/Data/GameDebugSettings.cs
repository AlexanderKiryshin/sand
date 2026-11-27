using UnityEngine;

[CreateAssetMenu(fileName = "GameDebugSettings", menuName = "Sand/Game Debug Settings")]
public class GameDebugSettings : ScriptableObject
{
    private const string ResourcesPath = "Settings/GameDebugSettings";

    private static GameDebugSettings cached;

    [InspectorName("Show Free Skip Level Button")]
    [Tooltip("Shows the free test skip button only in the Unity Editor; rewarded skip stays available in builds.")]
    public bool showSkipLevelButton;

    [Tooltip("Makes every background selectable for free in the Unity Editor.")]
    public bool unlockAllBackgrounds;

    [Tooltip("Makes every main and winter level selectable in the Unity Editor.")]
    public bool unlockAllLevels;

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
#if UNITY_EDITOR
            GameDebugSettings settings = Get();
            return settings != null && settings.showSkipLevelButton;
#else
            return false;
#endif
        }
    }

    public static bool UnlockAllBackgrounds
    {
        get
        {
#if UNITY_EDITOR
            GameDebugSettings settings = Get();
            return settings != null && settings.unlockAllBackgrounds;
#else
            return false;
#endif
        }
    }

    public static bool UnlockAllLevels
    {
        get
        {
#if UNITY_EDITOR
            GameDebugSettings settings = Get();
            return settings != null && settings.unlockAllLevels;
#else
            return false;
#endif
        }
    }
}
