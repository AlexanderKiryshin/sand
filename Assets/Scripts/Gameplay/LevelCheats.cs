public static class LevelCheats
{
#if UNITY_EDITOR
    private const string UnlockAllKey = "sand_cheat_all_levels_unlocked";

    public static bool AllLevelsUnlocked => GameDebugSettings.UnlockAllLevels || GameProgressStore.GetBool(UnlockAllKey);

    public static void UnlockAllLevels()
    {
        ProgressSaveService.UnlockAllLevels();
        WinterProgress.UnlockAllLevels();
        GameProgressStore.SetBool(UnlockAllKey, true);
        GameProgressStore.Save();
    }

#else
    public static bool AllLevelsUnlocked => false;
#endif

    public static bool CanSelectLevel(bool completedLevelsOnly, bool completed, bool firstLevel, bool unlocked)
        => completedLevelsOnly && !AllLevelsUnlocked ? completed || firstLevel : unlocked;
}
