using System;

[Serializable]
public class PlayerProgress
{
    public const int SaveVersion = 3;

    public int saveVersion = SaveVersion;
    public int highestUnlockedIndex;
    public int highestCompletedIndex = -1;
    public int currentLevelIndex;
}
