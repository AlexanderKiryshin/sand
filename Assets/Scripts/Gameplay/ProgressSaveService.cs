using UnityEngine;

public static class ProgressSaveService
{
    private const string KeyVersion = "sand_progress_version";
    private const string KeyHighestUnlocked = "sand_progress_highest_unlocked";
    private const string KeyHighestCompleted = "sand_progress_highest_completed";
    private const string KeyCurrentLevel = "sand_progress_current_level";
    private const string KeyCompletedMaskLegacy = "sand_progress_completed_mask";

    private static PlayerProgress progress;
    private static int levelCount;

    public static int HighestUnlockedIndex => progress != null ? progress.highestUnlockedIndex : 0;
    public static int HighestCompletedIndex => progress != null ? progress.highestCompletedIndex : -1;
    public static int CurrentLevelIndex => progress != null ? progress.currentLevelIndex : 0;

    public static void Initialize(int totalLevels)
    {
        levelCount = Mathf.Max(1, totalLevels);
        Load();
    }

    public static void Load()
    {
        int savedVersion = GameProgressStore.HasKey(KeyVersion)
            ? GameProgressStore.GetInt(KeyVersion)
            : PlayerPrefs.GetInt(KeyVersion, 0);

        if (savedVersion == 1)
        {
            progress = LoadLegacyV1();
            progress.saveVersion = PlayerProgress.SaveVersion;
            ClampProgress();
            Save();
            return;
        }

        if (savedVersion != PlayerProgress.SaveVersion && savedVersion != 2 && savedVersion != 3)
        {
            // Prefer Mirra store; migrate from PlayerPrefs if present.
            if (PlayerPrefs.HasKey(KeyHighestUnlocked))
            {
                progress = new PlayerProgress
                {
                    saveVersion = PlayerProgress.SaveVersion,
                    highestUnlockedIndex = PlayerPrefs.GetInt(KeyHighestUnlocked, 0),
                    highestCompletedIndex = PlayerPrefs.GetInt(KeyHighestCompleted, -1),
                    currentLevelIndex = PlayerPrefs.GetInt(KeyCurrentLevel, 0)
                };
                ClampProgress();
                Save();
                return;
            }

            progress = CreateNewProgress();
            Save();
            return;
        }

        progress = new PlayerProgress
        {
            saveVersion = PlayerProgress.SaveVersion,
            highestUnlockedIndex = ReadInt(KeyHighestUnlocked, 0),
            highestCompletedIndex = ReadInt(KeyHighestCompleted, -1),
            currentLevelIndex = ReadInt(KeyCurrentLevel, 0)
        };

        ClampProgress();
    }

    public static void Save()
    {
        if (progress == null)
        {
            progress = CreateNewProgress();
        }

        ClampProgress();

        GameProgressStore.SetInt(KeyVersion, progress.saveVersion);
        GameProgressStore.SetInt(KeyHighestUnlocked, progress.highestUnlockedIndex);
        GameProgressStore.SetInt(KeyHighestCompleted, progress.highestCompletedIndex);
        GameProgressStore.SetInt(KeyCurrentLevel, progress.currentLevelIndex);
        GameProgressStore.Save();

        // Keep PlayerPrefs mirror for local editor fallback.
        PlayerPrefs.SetInt(KeyVersion, progress.saveVersion);
        PlayerPrefs.SetInt(KeyHighestUnlocked, progress.highestUnlockedIndex);
        PlayerPrefs.SetInt(KeyHighestCompleted, progress.highestCompletedIndex);
        PlayerPrefs.SetInt(KeyCurrentLevel, progress.currentLevelIndex);
        PlayerPrefs.Save();
    }

    public static bool IsLevelUnlocked(int levelIndex)
    {
        return levelIndex >= 0 && levelIndex <= HighestUnlockedIndex;
    }

    public static bool IsLevelCompleted(int levelIndex)
    {
        return progress != null
            && levelIndex >= 0
            && levelIndex <= progress.highestCompletedIndex;
    }

    public static void SetCurrentLevel(int levelIndex)
    {
        EnsureProgress();
        progress.currentLevelIndex = Mathf.Clamp(levelIndex, 0, levelCount - 1);
        Save();
    }

    public static void MarkLevelCompleted(int levelIndex)
    {
        EnsureProgress();
        levelIndex = Mathf.Clamp(levelIndex, 0, levelCount - 1);

        progress.highestCompletedIndex = Mathf.Max(progress.highestCompletedIndex, levelIndex);

        if (levelIndex + 1 < levelCount)
        {
            progress.highestUnlockedIndex = Mathf.Max(progress.highestUnlockedIndex, levelIndex + 1);
        }
        else
        {
            progress.highestUnlockedIndex = Mathf.Max(progress.highestUnlockedIndex, levelIndex);
        }

        Save();
    }

    public static int GetResumeLevelIndex()
    {
        EnsureProgress();

        if (!IsLevelCompleted(progress.currentLevelIndex))
        {
            return progress.currentLevelIndex;
        }

        int nextIndex = progress.highestCompletedIndex + 1;
        if (nextIndex < levelCount && IsLevelUnlocked(nextIndex))
        {
            return nextIndex;
        }

        return Mathf.Clamp(progress.currentLevelIndex, 0, levelCount - 1);
    }

    public static void ResetProgress()
    {
        progress = CreateNewProgress();
        Save();
    }

    private static int ReadInt(string key, int defaultValue)
    {
        if (GameProgressStore.HasKey(key))
        {
            return GameProgressStore.GetInt(key, defaultValue);
        }

        return PlayerPrefs.GetInt(key, defaultValue);
    }

    private static PlayerProgress LoadLegacyV1()
    {
        int legacyMask = PlayerPrefs.GetInt(KeyCompletedMaskLegacy, 0);
        return new PlayerProgress
        {
            highestUnlockedIndex = PlayerPrefs.GetInt(KeyHighestUnlocked, 0),
            highestCompletedIndex = GetHighestSetBitIndex(legacyMask),
            currentLevelIndex = PlayerPrefs.GetInt(KeyCurrentLevel, 0)
        };
    }

    private static int GetHighestSetBitIndex(int mask)
    {
        int highest = -1;
        for (int i = 0; i < 31; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                highest = i;
            }
        }

        return highest;
    }

    private static PlayerProgress CreateNewProgress()
    {
        return new PlayerProgress
        {
            saveVersion = PlayerProgress.SaveVersion,
            highestUnlockedIndex = 0,
            highestCompletedIndex = -1,
            currentLevelIndex = 0
        };
    }

    private static void EnsureProgress()
    {
        if (progress == null)
        {
            Load();
        }
    }

    private static void ClampProgress()
    {
        if (progress == null)
        {
            progress = CreateNewProgress();
        }

        int maxIndex = Mathf.Max(0, levelCount - 1);
        progress.highestUnlockedIndex = Mathf.Clamp(progress.highestUnlockedIndex, 0, maxIndex);
        progress.highestCompletedIndex = Mathf.Clamp(progress.highestCompletedIndex, -1, maxIndex);
        progress.currentLevelIndex = Mathf.Clamp(progress.currentLevelIndex, 0, progress.highestUnlockedIndex);
    }
}
