using UnityEngine;

public static class WinterProgress
{
    private static int count;
    public static int HighestUnlocked => Mathf.Clamp(GameProgressStore.GetInt("winter_highest_unlocked", 0), 0, Mathf.Max(0, count - 1));
    public static int Current => Mathf.Clamp(GameProgressStore.GetInt("winter_current", 0), 0, HighestUnlocked);
    public static void Initialize(int levels)
    {
        count = levels;
        int migrated = HighestUnlocked;
        for (int i = 1; i <= count; i++)
            if (GameProgressStore.GetBool("winter_completed_" + i)) migrated = Mathf.Max(migrated, i);
        GameProgressStore.SetInt("winter_highest_unlocked", Mathf.Min(migrated, Mathf.Max(0, count - 1)));
    }
    public static bool IsUnlocked(int index) => index >= 0 && index < count && index <= HighestUnlocked;
    public static bool IsCompleted(int index) => index >= 0 && index < count && GameProgressStore.GetBool("winter_completed_" + (index + 1));
    public static void SetCurrent(int index)
    {
        if (!IsUnlocked(index)) return;
        GameProgressStore.SetInt("winter_current", index);
        GameProgressStore.Save();
    }
    public static void Complete(int index)
    {
        if (!IsUnlocked(index)) return;
        GameProgressStore.SetBool("winter_completed_" + (index + 1), true);
        GameProgressStore.SetInt("winter_highest_unlocked", Mathf.Min(count - 1, Mathf.Max(HighestUnlocked, index + 1)));
        GameProgressStore.Save();
    }
    public static int Resume => IsCompleted(Current) ? Mathf.Min(Current + 1, HighestUnlocked) : Current;
#if UNITY_EDITOR
    public static void UnlockAllLevels()
    {
        GameProgressStore.SetInt("winter_highest_unlocked", Mathf.Max(0, count - 1));
        GameProgressStore.Save();
    }
#endif
}
