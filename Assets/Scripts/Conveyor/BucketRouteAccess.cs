using System.Collections.Generic;

/// <summary>Reach the front through empty cells; walls and feeders require an open side route.</summary>
public static class BucketRouteAccess
{
    public static bool IsAccessible(int layoutIndex, LevelData level, BucketInventory inventory, Bucket movingPartner = null)
    {
        if (level == null || inventory == null || layoutIndex < 0 || layoutIndex >= LevelData.BucketLayoutCellCount)
            return false;
        var visited = new bool[LevelData.BucketLayoutCellCount];
        var open = new Queue<int>();
        visited[layoutIndex] = true;
        open.Enqueue(layoutIndex);
        while (open.Count > 0)
        {
            int index = open.Dequeue();
            int x = index % LevelData.BucketLayoutColumns;
            int y = index / LevelData.BucketLayoutColumns;
            if (y == 0) return true;
            Visit(x, y - 1);
            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y + 1);
        }
        return false;

        void Visit(int x, int y)
        {
            if (x < 0 || x >= LevelData.BucketLayoutColumns || y < 0 || y >= LevelData.BucketLayoutRows) return;
            int next = y * LevelData.BucketLayoutColumns + x;
            byte cell = level.GetBucketLayoutCell(x, y);
            if (visited[next] || LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell)
                || (inventory.GetBucketAtLayoutIndex(next) != null && inventory.GetBucketAtLayoutIndex(next) != movingPartner)) return;
            visited[next] = true;
            open.Enqueue(next);
        }
    }
}
