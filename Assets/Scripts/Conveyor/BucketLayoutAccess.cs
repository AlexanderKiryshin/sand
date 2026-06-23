public static class BucketLayoutAccess
{
    public static bool IsAccessible(int layoutIndex, LevelData levelData, BucketInventory inventory)
    {
        if (levelData == null || inventory == null || layoutIndex < 0 || layoutIndex >= LevelData.BucketLayoutCellCount)
        {
            return false;
        }

        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;

        return IsReachableFromFront(x, y, levelData, inventory);
    }

    private static bool IsReachableFromFront(int x, int y, LevelData levelData, BucketInventory inventory)
    {
        if (y == 0)
        {
            return true;
        }

        int frontY = y - 1;
        int frontIndex = frontY * LevelData.BucketLayoutColumns + x;
        byte frontCell = levelData.GetBucketLayoutCell(x, frontY);

        if (LevelData.IsBucketLayoutBucket(frontCell))
        {
            if (inventory.GetBucketAtLayoutIndex(frontIndex) != null)
            {
                return false;
            }

            return IsReachableFromFront(x, frontY, levelData, inventory);
        }

        if (LevelData.IsBucketLayoutWall(frontCell))
        {
            return HasSideExit(x, y, levelData, inventory);
        }

        return IsReachableFromFront(x, frontY, levelData, inventory);
    }

    private static bool HasSideExit(int x, int bucketY, LevelData levelData, BucketInventory inventory)
    {
        int frontY = bucketY - 1;
        return IsSideExitOpen(x - 1, frontY, bucketY, levelData, inventory)
            || IsSideExitOpen(x + 1, frontY, bucketY, levelData, inventory);
    }

    private static bool IsSideExitOpen(int sideX, int frontY, int bucketY, LevelData levelData, BucketInventory inventory)
    {
        return IsOpenPassage(sideX, frontY, levelData, inventory)
            && IsOpenPassage(sideX, bucketY, levelData, inventory);
    }

    private static bool IsOpenPassage(int x, int y, LevelData levelData, BucketInventory inventory)
    {
        if (x < 0 || x >= LevelData.BucketLayoutColumns || y < 0 || y >= LevelData.BucketLayoutRows)
        {
            return false;
        }

        int index = y * LevelData.BucketLayoutColumns + x;
        byte cell = levelData.GetBucketLayoutCell(x, y);

        if (LevelData.IsBucketLayoutWall(cell))
        {
            return false;
        }

        if (LevelData.IsBucketLayoutBucket(cell))
        {
            return inventory.GetBucketAtLayoutIndex(index) == null;
        }

        return true;
    }
}
