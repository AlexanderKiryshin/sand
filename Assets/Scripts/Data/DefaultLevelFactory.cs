using UnityEngine;

public static class DefaultLevelFactory
{
    public static LevelData CreateHeartLevel()
    {
        const int width = 32;
        const int height = 32;

        Color32 pink = new Color32(248, 198, 210, 255);
        Color32 red = new Color32(220, 50, 70, 255);
        Color32[] palette = { pink, red };

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x / (float)(width - 1)) * 2f - 1f;
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                bool isHeart = IsInsideHeart(nx, ny * 1.1f + 0.15f);
                pixels[y * width + x] = isHeart ? red : pink;
            }
        }

        byte[] cells = ColorQuantizer.MapToPalette(pixels, palette);
        BucketSpawn[] buckets = ColorQuantizer.GenerateBucketSpawns(cells, palette.Length);

        LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
        levelData.gridWidth = width;
        levelData.gridHeight = height;
        levelData.palette = palette;
        levelData.cells = cells;
        levelData.buckets = buckets;
        return levelData;
    }

    public static LevelDatabase CreateDatabaseWithHeartLevel()
    {
        LevelDatabase database = ScriptableObject.CreateInstance<LevelDatabase>();
        database.levels = new[] { CreateHeartLevel() };
        return database;
    }

    private static bool IsInsideHeart(float x, float y)
    {
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    }
}
