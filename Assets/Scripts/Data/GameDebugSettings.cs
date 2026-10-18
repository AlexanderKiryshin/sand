using UnityEngine;

[CreateAssetMenu(fileName = "GameDebugSettings", menuName = "Sand/Game Debug Settings")]
public class GameDebugSettings : ScriptableObject
{
    private const string ResourcesPath = "Settings/GameDebugSettings";

    private static GameDebugSettings cached;

    [InspectorName("Show Free Skip Level Button")]
    [Tooltip("Shows the free test skip button only in the Unity Editor; rewarded skip stays available in builds.")]
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
#if UNITY_EDITOR
            GameDebugSettings settings = Get();
            return settings != null && settings.showSkipLevelButton;
#else
            return false;
#endif
        }
    }
}
