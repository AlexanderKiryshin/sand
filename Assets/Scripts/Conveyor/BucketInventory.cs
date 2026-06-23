using System;
using System.Collections.Generic;
using UnityEngine;

public class BucketInventory
{
    private readonly List<Bucket> buckets = new List<Bucket>();

    public event Action OnInventoryChanged;

    public IReadOnlyList<Bucket> Buckets { get { return buckets; } }
    public int TotalBucketCount { get; private set; }

    public void BuildFromLevel(LevelData levelData, int subGrainsPerAxis)
    {
        buckets.Clear();
        TotalBucketCount = 0;

        if (levelData == null || levelData.cells == null || levelData.cells.Length == 0)
        {
            NotifyChanged();
            return;
        }

        int grainsPerLevelCell = Mathf.Max(1, subGrainsPerAxis) * Mathf.Max(1, subGrainsPerAxis);
        if (levelData.HasBucketLayout())
        {
            BuildFromBucketLayout(levelData, grainsPerLevelCell);
        }
        else
        {
            BuildFromGeneratedQueue(levelData, grainsPerLevelCell);
        }

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

        int layoutIndex = bucket.LayoutIndex >= 0 ? bucket.LayoutIndex : GetQueueLayoutIndex(bucket);
        if (layoutIndex < 0)
        {
            return false;
        }

        if (!levelData.HasBucketLayout())
        {
            return layoutIndex < LevelData.BucketLayoutColumns;
        }

        return BucketLayoutAccess.IsAccessible(layoutIndex, levelData, this);
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

    private void BuildFromGeneratedQueue(LevelData levelData, int grainsPerLevelCell)
    {
        ColorQuantizer.BucketQueueItem[] queue = ColorQuantizer.GenerateLevelBucketQueue(
            levelData.cells,
            levelData.gridWidth,
            levelData.gridHeight,
            grainsPerLevelCell);

        TotalBucketCount = queue.Length;
        for (int i = 0; i < queue.Length; i++)
        {
            ColorQuantizer.BucketQueueItem item = queue[i];
            buckets.Add(new Bucket(item.colorIndex, item.capacity));
        }
    }

    private void BuildFromBucketLayout(LevelData levelData, int grainsPerLevelCell)
    {
        levelData.EnsureBucketLayout();
        int[] grainsPerColor = new int[256];
        int[] bucketsPerColor = new int[256];
        int[] nextCapacityIndex = new int[256];

        for (int i = 0; i < levelData.cells.Length; i++)
        {
            byte colorIndex = levelData.cells[i];
            if (colorIndex > 0)
            {
                grainsPerColor[colorIndex] += grainsPerLevelCell;
            }
        }

        for (int layoutIndex = 0; layoutIndex < levelData.bucketLayout.Length; layoutIndex++)
        {
            byte colorIndex = levelData.bucketLayout[layoutIndex];
            if (!LevelData.IsBucketLayoutBucket(colorIndex))
            {
                continue;
            }

            bucketsPerColor[colorIndex]++;
        }

        int[][] capacitiesByColor = new int[256][];
        for (int colorIndex = 1; colorIndex < capacitiesByColor.Length; colorIndex++)
        {
            if (bucketsPerColor[colorIndex] <= 0)
            {
                continue;
            }

            capacitiesByColor[colorIndex] = ColorQuantizer.DistributeBucketCapacities(
                grainsPerColor[colorIndex],
                bucketsPerColor[colorIndex]);
        }

        for (int layoutIndex = 0; layoutIndex < levelData.bucketLayout.Length; layoutIndex++)
        {
            byte colorIndex = levelData.bucketLayout[layoutIndex];
            if (!LevelData.IsBucketLayoutBucket(colorIndex))
            {
                continue;
            }

            int[] capacities = capacitiesByColor[colorIndex];
            int capacityIndex = nextCapacityIndex[colorIndex]++;
            int capacity = capacities != null && capacityIndex < capacities.Length
                ? capacities[capacityIndex]
                : grainsPerColor[colorIndex];
            capacity = Mathf.Max(1, capacity);
            buckets.Add(new Bucket(colorIndex, capacity, layoutIndex));
        }

        buckets.Sort(CompareBucketsByLayout);
        TotalBucketCount = buckets.Count;
    }

    private int GetQueueLayoutIndex(Bucket bucket)
    {
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i] == bucket)
            {
                return i;
            }
        }

        return -1;
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
