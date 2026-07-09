using UnityEngine;

public static class BucketGridAccess
{
    public static bool IsAccessible(int layoutIndex, BucketInventory inventory)
    {
        if (inventory == null || layoutIndex < 0)
        {
            return false;
        }

        if (inventory.GetBucketAtLayoutIndex(layoutIndex) == null)
        {
            return false;
        }

        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;
        return IsColumnReachable(x, y, inventory);
    }

    private static bool IsColumnReachable(int x, int y, BucketInventory inventory)
    {
        if (y == 0)
        {
            return true;
        }

        int frontIndex = (y - 1) * LevelData.BucketLayoutColumns + x;
        if (inventory.GetBucketAtLayoutIndex(frontIndex) != null)
        {
            return false;
        }

        return IsColumnReachable(x, y - 1, inventory);
    }
}
