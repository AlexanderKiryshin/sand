using UnityEditor;

[InitializeOnLoad]
internal static class LevelSegmentAssetBootstrap
{
    static LevelSegmentAssetBootstrap()
    {
        EditorApplication.delayCall += TryEnsureDefaultSegmentAssets;
    }

    private static void TryEnsureDefaultSegmentAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        LevelEditorUtility.EnsureDefaultSegmentSetup();
    }
}
