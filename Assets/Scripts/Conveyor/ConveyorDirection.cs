public enum ConveyorDirection : byte
{
    Front = 0,
    Back = 1,
    Left = 2,
    Right = 3
}

public static class ConveyorDirectionUtility
{
    public static bool TryGetTargetCell(int layoutIndex, ConveyorDirection direction, out int targetX, out int targetY)
    {
        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;
        targetX = x;
        targetY = y;

        switch (direction)
        {
            case ConveyorDirection.Front:
                targetY = y - 1;
                break;
            case ConveyorDirection.Back:
                targetY = y + 1;
                break;
            case ConveyorDirection.Left:
                targetX = x - 1;
                break;
            case ConveyorDirection.Right:
                targetX = x + 1;
                break;
            default:
                return false;
        }

        return targetX >= 0
            && targetX < LevelData.BucketLayoutColumns
            && targetY >= 0
            && targetY < LevelData.BucketLayoutRows;
    }

    public static int GetTargetLayoutIndex(int layoutIndex, ConveyorDirection direction)
    {
        int targetX;
        int targetY;
        if (!TryGetTargetCell(layoutIndex, direction, out targetX, out targetY))
        {
            return -1;
        }

        return targetY * LevelData.BucketLayoutColumns + targetX;
    }

    public static float GetArrowRotationDegrees(ConveyorDirection direction)
    {
        switch (direction)
        {
            case ConveyorDirection.Front:
                return 0f;
            case ConveyorDirection.Back:
                return 180f;
            case ConveyorDirection.Left:
                return 90f;
            case ConveyorDirection.Right:
                return -90f;
            default:
                return 0f;
        }
    }
}
