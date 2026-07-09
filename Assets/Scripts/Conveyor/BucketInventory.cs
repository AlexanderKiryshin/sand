using System;
using System.Collections.Generic;
using UnityEngine;

public class BucketInventory
{
    private readonly List<Bucket> buckets = new List<Bucket>();

    public event Action OnInventoryChanged;

    public IReadOnlyList<Bucket> Buckets { get { return buckets; } }
    public int TotalBucketCount { get; private set; }

    public void BuildFromLevel(LevelData levelData, int subGrainsPerAxis, LevelBucketSettings bucketSettings = null)
    {
        buckets.Clear();
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
        return buckets.Count;
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

    private void BuildFromGeneratedQueue(LevelData levelData, int grainsPerLevelCell, int maxGrainsPerBucket)
    {
        ColorQuantizer.BucketQueueItem[] queue = ColorQuantizer.GenerateLevelBucketQueue(
            levelData.cells,
            levelData.gridWidth,
            levelData.gridHeight,
            grainsPerLevelCell,
            maxGrainsPerBucket);

        TotalBucketCount = queue.Length;
        int[] shuffledSlots = CreateShuffledSlotOrder(queue.Length);
        for (int i = 0; i < queue.Length; i++)
        {
            ColorQuantizer.BucketQueueItem item = queue[i];
            buckets.Add(new Bucket(item.colorIndex, item.capacity, shuffledSlots[i]));
        }
    }

    private static int[] CreateShuffledSlotOrder(int slotCount)
    {
        int[] slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            slots[i] = i;
        }

        for (int i = slotCount - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = slots[i];
            slots[i] = slots[swapIndex];
            slots[swapIndex] = temp;
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
