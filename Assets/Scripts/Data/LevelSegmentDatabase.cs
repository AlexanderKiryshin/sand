using UnityEngine;

[CreateAssetMenu(fileName = "LevelSegmentDatabase", menuName = "Sand/Level Segment Database")]
public class LevelSegmentDatabase : ScriptableObject
{
    public LevelSegmentRules[] segments = new LevelSegmentRules[0];

    public LevelSegmentRules GetSegmentForLevelIndex(int levelIndex)
    {
        if (segments == null || segments.Length == 0 || levelIndex < 0)
        {
            return null;
        }

        LevelSegmentRules best = null;
        for (int i = 0; i < segments.Length; i++)
        {
            LevelSegmentRules segment = segments[i];
            if (segment == null || levelIndex < segment.startLevelIndex)
            {
                continue;
            }

            int nextStart = GetNextSegmentStart(segment.startLevelIndex);
            int endExclusive = segment.GetEndLevelIndexExclusive(nextStart);
            if (levelIndex >= endExclusive)
            {
                continue;
            }

            if (best == null || segment.startLevelIndex >= best.startLevelIndex)
            {
                best = segment;
            }
        }

        return best;
    }

    private int GetNextSegmentStart(int currentStart)
    {
        int nextStart = int.MaxValue;
        for (int i = 0; i < segments.Length; i++)
        {
            LevelSegmentRules segment = segments[i];
            if (segment == null || segment.startLevelIndex <= currentStart)
            {
                continue;
            }

            if (segment.startLevelIndex < nextStart)
            {
                nextStart = segment.startLevelIndex;
            }
        }

        return nextStart;
    }
}
