using UnityEngine;

[CreateAssetMenu(fileName = "LevelSegmentRules", menuName = "Sand/Level Segment Rules")]
public class LevelSegmentRules : ScriptableObject
{
    public string segmentId = "segment_01";
    public string displayName = "Segment 1";

    [Tooltip("First level index in LevelDatabase (0-based, inclusive).")]
    public int startLevelIndex;

    [Tooltip("How many levels this segment covers. 0 = until the next segment starts.")]
    public int levelCount;

    public LevelBucketSettings bucketSettings;

    [Header("Image Defaults")]
    public SandColorPalette defaultPalette;
    public int defaultGridWidth = 32;
    public int defaultGridHeight = 32;
    public int defaultColorCount = 4;
    public bool defaultAutoColorCount = true;

    public bool ContainsLevelIndex(int levelIndex)
    {
        if (levelIndex < startLevelIndex)
        {
            return false;
        }

        if (levelCount > 0)
        {
            return levelIndex < startLevelIndex + levelCount;
        }

        return true;
    }

    public int GetEndLevelIndexExclusive(int nextSegmentStartIndex)
    {
        if (levelCount > 0)
        {
            return startLevelIndex + levelCount;
        }

        return nextSegmentStartIndex > startLevelIndex ? nextSegmentStartIndex : int.MaxValue;
    }
}
