using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Sand/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public LevelData[] levels = new LevelData[0];

    public LevelData GetLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
        {
            return null;
        }

        return levels[index];
    }

    public int LevelCount
    {
        get { return levels != null ? levels.Length : 0; }
    }
}
