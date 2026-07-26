using System;
using System.Collections.Generic;
using UnityEngine;

public class BucketInventory
{
    private readonly List<Bucket> buckets = new List<Bucket>();
    private readonly List<ConveyorSlotState> conveyorSlots = new List<ConveyorSlotState>();

    public event Action OnInventoryChanged;

    public IReadOnlyList<Bucket> Buckets { get { return buckets; } }
    public IReadOnlyList<ConveyorSlotState> ConveyorSlots { get { return conveyorSlots; } }
    public int TotalBucketCount { get; private set; }

    public void BuildFromLevel(LevelData levelData, int subGrainsPerAxis, LevelBucketSettings bucketSettings = null)
    {
        buckets.Clear();
        conveyorSlots.Clear();
        TotalBucketCount = 0;

        if (levelData == null || levelData.cells == null || levelData.cells.Length == 0)
        {
            NotifyChanged();
            return;
        }

        int maxGrainsPerBucket = bucketSettings != null ? bucketSettings.maxGrainsPerBucket : 0;
        int grainsPerLevelCell = Mathf.Max(1, subGrainsPerAxis) * Mathf.Max(1, subGrainsPerAxis);
        BuildFromGeneratedQueue(levelData, grainsPerLevelCell, maxGrainsPerBucket);

        NotifyChanged();
    }

    public Bucket GetBucketAtLayoutIndex(int layoutIndex)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].LayoutIndex == layoutIndex)
            {
                return buckets[i];
            }
        }

        return null;
    }

    public bool IsConveyorSlotAt(int layoutIndex)
    {
        for (int i = 0; i < conveyorSlots.Count; i++)
        {
            if (conveyorSlots[i].LayoutIndex == layoutIndex)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsBucketAccessible(Bucket bucket, LevelData levelData)
    {
        if (bucket == null || !buckets.Contains(bucket) || levelData == null)
        {
            return false;
        }

        if (bucket.LayoutIndex < 0)
        {
            return false;
        }

        return BucketGridAccess.IsAccessible(bucket.LayoutIndex, this);
    }

    public bool TryTakeBucket(Bucket bucket, ConveyorBelt conveyor, LevelData levelData)
    {
        if (bucket == null || !buckets.Contains(bucket) || conveyor == null)
        {
            return false;
        }

        if (levelData != null && !IsBucketAccessible(bucket, levelData))
        {
            return false;
        }

        if (!conveyor.CanAcceptBucket())
        {
            return false;
        }

        if (!conveyor.TryPlaceBucket(bucket))
        {
            return false;
        }

        buckets.Remove(bucket);
        NotifyChanged();
        return true;
    }

    public int CountRemaining()
    {
        return buckets.Count + CountConveyorReserved();
    }

    public int CountConveyorReserved()
    {
        int count = 0;
        for (int i = 0; i < conveyorSlots.Count; i++)
        {
            count += conveyorSlots[i].RemainingCount;
        }

        return count;
    }

    public bool TryDispenseAll(LevelData levelData)
    {
        if (levelData == null || conveyorSlots.Count == 0)
        {
            return false;
        }

        bool changed = false;
        bool progressed;
        int safety = 64;
        do
        {
            progressed = false;
            for (int i = 0; i < conveyorSlots.Count; i++)
            {
                if (TryDispenseFromSlot(conveyorSlots[i], levelData))
                {
                    changed = true;
                    progressed = true;
                }
            }

            safety--;
        }
        while (progressed && safety > 0);

        if (changed)
        {
            buckets.Sort(CompareBucketsByLayout);
            NotifyChanged();
        }

        return changed;
    }

    public void ReturnBucket(Bucket bucket)
    {
        if (bucket == null || bucket.State == BucketState.Removed || buckets.Contains(bucket))
        {
            return;
        }

        bucket.ReturnToInventory();
        buckets.Add(bucket);
        buckets.Sort(CompareBucketsByLayout);

        NotifyChanged();
    }

    private bool TryDispenseFromSlot(ConveyorSlotState slot, LevelData levelData)
    {
        if (slot == null || slot.RemainingCount == 0)
        {
            return false;
        }

        int targetIndex = ConveyorDirectionUtility.GetTargetLayoutIndex(slot.LayoutIndex, slot.Direction);
        if (targetIndex < 0 || !CanReceiveDispensedBucket(targetIndex, levelData))
        {
            return false;
        }

        Bucket bucket = slot.ReservedBuckets[0];
        slot.ReservedBuckets.RemoveAt(0);
        bucket.SetLayoutIndex(targetIndex);
        buckets.Add(bucket);
        return true;
    }

    private bool CanReceiveDispensedBucket(int layoutIndex, LevelData levelData)
    {
        if (GetBucketAtLayoutIndex(layoutIndex) != null || IsConveyorSlotAt(layoutIndex))
        {
            return false;
        }

        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;
        byte cell = levelData.GetBucketLayoutCell(x, y);
        return !LevelData.IsBucketLayoutWall(cell) && !LevelData.IsBucketLayoutConveyorSlot(cell);
    }

    private void BuildFromGeneratedQueue(LevelData levelData, int grainsPerLevelCell, int maxGrainsPerBucket)
    {
        ColorQuantizer.BucketQueueItem[] queue = ColorQuantizer.GenerateLevelBucketQueue(
            levelData.cells,
            levelData.gridWidth,
            levelData.gridHeight,
            grainsPerLevelCell,
            maxGrainsPerBucket);

        TotalBucketCount = queue.Length;
        List<ColorQuantizer.BucketQueueItem> remainingQueue = new List<ColorQuantizer.BucketQueueItem>(queue);
        BuildConveyorSlots(levelData, remainingQueue);

        List<int> availableSlots = CollectAvailableSlotIndices(levelData, remainingQueue.Count);
        int[] shuffledSlots = CreateShuffledSlotOrder(availableSlots, remainingQueue.Count);
        for (int i = 0; i < remainingQueue.Count; i++)
        {
            ColorQuantizer.BucketQueueItem item = remainingQueue[i];
            buckets.Add(new Bucket(item.colorIndex, item.capacity, shuffledSlots[i]));
        }
    }

    private void BuildConveyorSlots(LevelData levelData, List<ColorQuantizer.BucketQueueItem> remainingQueue)
    {
        levelData.EnsureBucketLayout();
        for (int layoutIndex = 0; layoutIndex < LevelData.BucketLayoutCellCount; layoutIndex++)
        {
            int x = layoutIndex % LevelData.BucketLayoutColumns;
            int y = layoutIndex / LevelData.BucketLayoutColumns;
            byte cell = levelData.GetBucketLayoutCell(x, y);
            if (!LevelData.IsBucketLayoutConveyorSlot(cell))
            {
                continue;
            }

            int reserveCount = Mathf.Min(levelData.GetConveyorBucketCount(layoutIndex), remainingQueue.Count);
            List<Bucket> reservedBuckets = new List<Bucket>(reserveCount);
            for (int i = 0; i < reserveCount; i++)
            {
                ColorQuantizer.BucketQueueItem item = remainingQueue[0];
                remainingQueue.RemoveAt(0);
                reservedBuckets.Add(new Bucket(item.colorIndex, item.capacity, -1));
            }

            ConveyorDirection direction = levelData.GetConveyorDirection(layoutIndex);
            conveyorSlots.Add(new ConveyorSlotState(layoutIndex, direction, reservedBuckets));
        }
    }

    private static List<int> CollectAvailableSlotIndices(LevelData levelData, int bucketCount)
    {
        List<int> slots = new List<int>(LevelData.BucketLayoutCellCount);
        levelData.EnsureBucketLayout();

        bool hasStructuralLayout = false;
        for (int layoutIndex = 0; layoutIndex < LevelData.BucketLayoutCellCount; layoutIndex++)
        {
            byte cell = levelData.bucketLayout[layoutIndex];
            if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell))
            {
                hasStructuralLayout = true;
                break;
            }
        }

        if (!hasStructuralLayout)
        {
            int slotCount = Mathf.Min(bucketCount, LevelData.BucketLayoutCellCount);
            for (int i = 0; i < slotCount; i++)
            {
                slots.Add(i);
            }

            return slots;
        }

        for (int layoutIndex = 0; layoutIndex < LevelData.BucketLayoutCellCount; layoutIndex++)
        {
            byte cell = levelData.bucketLayout[layoutIndex];
            if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell))
            {
                continue;
            }

            slots.Add(layoutIndex);
        }

        return slots;
    }

    private static int[] CreateShuffledSlotOrder(List<int> availableSlots, int slotCount)
    {
        if (slotCount <= 0)
        {
            return new int[0];
        }

        List<int> slots = new List<int>(availableSlots);
        if (slots.Count == 0)
        {
            slots.AddRange(CreateFallbackSlotIndices(slotCount));
        }

        while (slots.Count < slotCount)
        {
            slots.Add(slots[slots.Count % Mathf.Max(1, availableSlots.Count)]);
        }

        for (int i = slots.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = slots[i];
            slots[i] = slots[swapIndex];
            slots[swapIndex] = temp;
        }

        int[] result = new int[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            result[i] = slots[i];
        }

        return result;
    }

    private static List<int> CreateFallbackSlotIndices(int count)
    {
        List<int> slots = new List<int>(count);
        for (int i = 0; i < count && i < LevelData.BucketLayoutCellCount; i++)
        {
            slots.Add(i);
        }

        return slots;
    }

    private static int CompareBucketsByLayout(Bucket left, Bucket right)
    {
        return left.LayoutIndex.CompareTo(right.LayoutIndex);
    }

    private void NotifyChanged()
    {
        if (OnInventoryChanged != null)
        {
            OnInventoryChanged();
        }
    }
}
