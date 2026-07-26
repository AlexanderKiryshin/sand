using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Sand/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public LevelData[] levels = new LevelData[0];
    public LevelSegmentDatabase segmentDatabase;

    public LevelData GetLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
        {
            return null;
        }

        return levels[index];
    }

    public LevelSegmentRules GetSegmentRulesForLevel(int index)
    {
        if (segmentDatabase == null)
        {
            return null;
        }

        return segmentDatabase.GetSegmentForLevelIndex(index);
    }

    public void ApplyBucketLayoutRules(LevelData level, int levelIndex)
    {
        if (level == null)
        {
            return;
        }

        LevelSegmentRules settings = GetBucketSettingsForLevel(levelIndex, level);
        LevelBucketLayoutGenerator.Apply(level, settings);
    }

    public LevelSegmentRules GetBucketSettingsForLevel(int levelIndex, LevelData level = null)
    {
        if (level == null)
        {
            level = GetLevel(levelIndex);
        }

        if (level != null && level.bucketSettingsOverride != null)
        {
            return level.bucketSettingsOverride;
        }

        return GetSegmentRulesForLevel(levelIndex);
    }

    public int LevelCount
    {
        get { return levels != null ? levels.Length : 0; }
    }
}
