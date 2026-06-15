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
        ColorQuantizer.BucketQueueItem[] queue = ColorQuantizer.GenerateLevelBucketQueue(
            levelData.cells,
            levelData.gridWidth,
            levelData.gridHeight,
            levelData.bucketCapacity,
            grainsPerLevelCell);

        TotalBucketCount = queue.Length;
        int totalGrains = 0;
        for (int i = 0; i < queue.Length; i++)
        {
            ColorQuantizer.BucketQueueItem item = queue[i];
            buckets.Add(new Bucket(item.colorIndex, item.capacity));
            totalGrains += item.capacity;
        }

        NotifyChanged();
    }

    public bool TryTakeBucket(Bucket bucket, ConveyorBelt conveyor)
    {
        if (bucket == null || !buckets.Contains(bucket) || conveyor == null)
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
        buckets.Insert(0, bucket);

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (OnInventoryChanged != null)
        {
            OnInventoryChanged();
        }
    }
}
