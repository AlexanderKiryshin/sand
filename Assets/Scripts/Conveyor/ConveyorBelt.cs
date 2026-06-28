using System;
using System.Collections.Generic;
using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public const int MaxOnBelt = 5;
    public const float CollectionX = -95f;
    public const float EntryX = 200f;
    public const float ExitX = -220f;
    public const float CollectionTolerance = 35f;
    public const float BucketSpacing = 72f;
    public const float BucketWidth = 40f;

    [SerializeField] private ConveyorSettings settings;
    [SerializeField] private int requiredFilledBuckets = 5;

    private readonly List<BeltEntry> entries = new List<BeltEntry>();
    private int filledBuckets;
    private bool allBucketsDeployed;
    private SandConveyorMapper mapper;
    private SandPourAnimator pourAnimator;

    public event Action OnConveyorChanged;
    public event Action<Bucket> OnBucketFilled;
    public event Action<Bucket> OnBucketReturned;

    public int FilledBuckets { get { return filledBuckets; } }
    public int RequiredFilledBuckets { get { return requiredFilledBuckets; } }
    public IReadOnlyList<BeltEntry> Entries { get { return entries; } }
    public bool IsFastSpeed { get { return entries.Count > 0 && (allBucketsDeployed || IsFull()); } }
    public float MoveSpeed { get { return settings != null ? settings.moveSpeed : ConveyorSettings.DefaultMoveSpeed; } }
    public float FullSpeedMultiplier { get { return settings != null ? settings.fullSpeedMultiplier : ConveyorSettings.DefaultFullSpeedMultiplier; } }
    public float BeltScrollSpeed { get { return settings != null ? settings.beltScrollSpeed : ConveyorSettings.DefaultBeltScrollSpeed; } }

    public class BeltEntry
    {
        public Bucket Bucket;
        public float PositionX;
        public bool PendingRemoval;
    }

    private void Awake()
    {
        mapper = FindObjectOfType<SandConveyorMapper>();
        EnsureSettings();
    }

    private void EnsureSettings()
    {
        if (settings != null)
        {
            return;
        }

        ConveyorSettings[] loaded = Resources.FindObjectsOfTypeAll<ConveyorSettings>();
        if (loaded.Length > 0)
        {
            settings = loaded[0];
        }
    }

    public void Configure(int requiredBuckets)
    {
        requiredFilledBuckets = Mathf.Max(1, requiredBuckets);
        ResetConveyor();
    }

    public void ResetConveyor()
    {
        entries.Clear();
        filledBuckets = 0;
        allBucketsDeployed = false;
        NotifyChanged();
    }

    public void SetAllBucketsDeployed(bool deployed)
    {
        allBucketsDeployed = deployed;
    }

    public bool CanAcceptBucket()
    {
        return entries.Count < MaxOnBelt;
    }

    public bool IsFull()
    {
        return entries.Count >= MaxOnBelt;
    }

    public bool TryPlaceBucket(Bucket bucket)
    {
        if (bucket == null || !CanAcceptBucket() || bucket.State != BucketState.InInventory)
        {
            return false;
        }

        bucket.MoveToConveyor();
        entries.Add(new BeltEntry
        {
            Bucket = bucket,
            PositionX = GetSpawnPositionX(null)
        });

        NotifyChanged();
        return true;
    }

    private float GetSpawnPositionX(BeltEntry exclude)
    {
        float spawnX = EntryX;
        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry == exclude)
            {
                continue;
            }

            spawnX = Mathf.Max(spawnX, entry.PositionX + BucketSpacing);
        }

        return spawnX;
    }

    public void UpdateBelt(float deltaTime)
    {
        if (pourAnimator == null)
        {
            pourAnimator = FindObjectOfType<SandPourAnimator>();
        }

        ProcessPendingRemovals();

        if (entries.Count == 0)
        {
            return;
        }

        float speed = MoveSpeed * (IsFastSpeed ? FullSpeedMultiplier : 1f);
        float move = speed * deltaTime;
        bool changed = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BeltEntry entry = entries[i];
            entry.PositionX -= move;

            if (entry.PositionX < ExitX && !entry.Bucket.IsFull && !entry.PendingRemoval)
            {
                float newX = GetSpawnPositionX(entry);
                entry.PositionX = newX;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void RemoveUnfilledBuckets()
    {
        bool changed = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket != null && !entry.Bucket.IsFull)
            {
                entry.Bucket.MarkRemoved();
                entries.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public BeltEntry GetActiveBucketEntry()
    {
        BeltEntry best = null;
        float bestDistance = float.MaxValue;
        int gridWidth = GetLevelGridWidth();
        SandConveyorMapper activeMapper = EnsureMapper();

        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket == null || entry.Bucket.IsFull || entry.Bucket.State != BucketState.OnConveyor)
            {
                continue;
            }

            if (!IsInCollectionZone(entry))
            {
                continue;
            }

            float distance = activeMapper != null
                ? activeMapper.GetColumnAlignmentError(entry.PositionX, gridWidth / 2, gridWidth)
                : float.MaxValue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entry;
            }
        }

        return best;
    }

    public Bucket GetActiveBucket()
    {
        BeltEntry entry = GetActiveBucketEntry();
        return entry != null ? entry.Bucket : null;
    }

    public bool IsColumnUnderBucket(int gridX, int gridWidth, BeltEntry entry)
    {
        if (entry == null || entry.Bucket == null || entry.Bucket.IsFull)
        {
            return false;
        }

        if (mapper == null)
        {
            mapper = FindObjectOfType<SandConveyorMapper>();
        }

        if (mapper != null)
        {
            return mapper.IsColumnUnderBucket(entry.PositionX, gridX, gridWidth);
        }

        return false;
    }

    public bool IsInCollectionZone(BeltEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        SandConveyorMapper activeMapper = EnsureMapper();
        if (activeMapper == null)
        {
            return false;
        }

        return activeMapper.IsBucketOverSand(entry.PositionX, GetLevelGridWidth());
    }

    public BeltEntry FindBucketForColumn(int gridX, int gridWidth, byte colorIndex)
    {
        BeltEntry best = null;
        float bestDistance = float.MaxValue;
        SandConveyorMapper activeMapper = EnsureMapper();

        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket == null || entry.Bucket.IsFull || entry.Bucket.State != BucketState.OnConveyor)
            {
                continue;
            }

            if (entry.Bucket.ColorIndex != colorIndex)
            {
                continue;
            }

            if (!IsColumnUnderBucket(gridX, gridWidth, entry))
            {
                continue;
            }

            if (!IsBucketStableForCollection(entry, gridWidth))
            {
                continue;
            }

            float distance = activeMapper != null
                ? activeMapper.GetColumnAlignmentError(entry.PositionX, gridX, gridWidth)
                : float.MaxValue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entry;
            }
        }

        return best;
    }

    public bool CanCollectAt(int gridX, int gridWidth, byte colorIndex)
    {
        return FindBucketForColumn(gridX, gridWidth, colorIndex) != null;
    }

    public bool IsBucketStableForCollection(BeltEntry entry, int gridWidth)
    {
        return GetAlignedColumn(entry, gridWidth) >= 0;
    }

    public void FillSandCollectionLookup(
        bool[] lookup,
        int levelWidth,
        List<int> activeColumns,
        bool[] columnAddedScratch)
    {
        if (lookup == null || activeColumns == null || levelWidth <= 0)
        {
            return;
        }

        activeColumns.Clear();
        int lookupLength = Mathf.Min(lookup.Length, 256 * levelWidth);
        Array.Clear(lookup, 0, lookupLength);

        if (columnAddedScratch == null || columnAddedScratch.Length != levelWidth)
        {
            columnAddedScratch = new bool[levelWidth];
        }
        else
        {
            Array.Clear(columnAddedScratch, 0, levelWidth);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            Bucket bucket = entry.Bucket;
            if (bucket == null || bucket.IsFull || bucket.State != BucketState.OnConveyor)
            {
                continue;
            }

            if (!IsBucketStableForCollection(entry, levelWidth))
            {
                continue;
            }

            int centerColumn = GetAlignedColumn(entry, levelWidth);
            if (centerColumn < 0)
            {
                continue;
            }

            int colorOffset = bucket.ColorIndex * levelWidth;
            AddCollectionColumnsUnderBucket(
                entry,
                levelWidth,
                centerColumn,
                colorOffset,
                lookup,
                activeColumns,
                columnAddedScratch);
        }
    }

    private void AddCollectionColumnsUnderBucket(
        BeltEntry entry,
        int levelWidth,
        int centerColumn,
        int colorOffset,
        bool[] lookup,
        List<int> activeColumns,
        bool[] columnAddedScratch)
    {
        for (int levelX = centerColumn; levelX >= 0; levelX--)
        {
            if (!IsColumnUnderBucket(levelX, levelWidth, entry))
            {
                break;
            }

            MarkCollectionColumn(levelX, colorOffset, lookup, activeColumns, columnAddedScratch);
        }

        for (int levelX = centerColumn + 1; levelX < levelWidth; levelX++)
        {
            if (!IsColumnUnderBucket(levelX, levelWidth, entry))
            {
                break;
            }

            MarkCollectionColumn(levelX, colorOffset, lookup, activeColumns, columnAddedScratch);
        }
    }

    private static void MarkCollectionColumn(
        int levelX,
        int colorOffset,
        bool[] lookup,
        List<int> activeColumns,
        bool[] columnAddedScratch)
    {
        int lookupIndex = colorOffset + levelX;
        if (lookupIndex < 0 || lookupIndex >= lookup.Length || lookup[lookupIndex])
        {
            return;
        }

        lookup[lookupIndex] = true;
        if (!columnAddedScratch[levelX])
        {
            columnAddedScratch[levelX] = true;
            activeColumns.Add(levelX);
        }
    }

    private int GetAlignedColumn(BeltEntry entry, int gridWidth)
    {
        if (entry == null || entry.Bucket == null || entry.Bucket.IsFull
            || entry.Bucket.State != BucketState.OnConveyor || !IsInCollectionZone(entry))
        {
            return -1;
        }

        SandConveyorMapper activeMapper = EnsureMapper();
        int bestColumn = -1;
        float bestDistance = float.MaxValue;

        for (int gridX = 0; gridX < gridWidth; gridX++)
        {
            if (!IsColumnUnderBucket(gridX, gridWidth, entry))
            {
                continue;
            }

            float distance = activeMapper != null
                ? activeMapper.GetColumnAlignmentError(entry.PositionX, gridX, gridWidth)
                : float.MaxValue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestColumn = gridX;
            }
        }

        return bestColumn;
    }

    public bool HasBucketAlignedAt(int gridX, int gridWidth)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket == null || entry.Bucket.IsFull || entry.PendingRemoval
                || entry.Bucket.State != BucketState.OnConveyor)
            {
                continue;
            }

            if (!IsColumnUnderBucket(gridX, gridWidth, entry))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private SandConveyorMapper EnsureMapper()
    {
        if (mapper == null)
        {
            mapper = FindObjectOfType<SandConveyorMapper>();
        }

        return mapper;
    }

    private int GetLevelGridWidth()
    {
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        return loader != null && loader.CurrentLevel != null ? loader.CurrentLevel.gridWidth : 32;
    }

    public bool TryCollectGrain(byte colorIndex, int gridX, int gridWidth)
    {
        BeltEntry entry = FindBucketForColumn(gridX, gridWidth, colorIndex);
        if (entry == null)
        {
            return false;
        }

        if (!entry.Bucket.TryAddGrain(colorIndex))
        {
            return false;
        }

        if (entry.Bucket.IsFull)
        {
            entry.PendingRemoval = true;
            NotifyChanged();
        }

        return true;
    }

    private void ReturnBucketToInventory(BeltEntry entry)
    {
        if (entry == null || entry.Bucket == null)
        {
            return;
        }

        entry.Bucket.ReturnToInventory();

        if (OnBucketReturned != null)
        {
            OnBucketReturned(entry.Bucket);
        }
    }

    public void NotifyPourFinished(Bucket bucket)
    {
        ProcessPendingRemovals();
    }

    private void ProcessPendingRemovals()
    {
        if (pourAnimator == null)
        {
            pourAnimator = FindObjectOfType<SandPourAnimator>();
        }

        bool changed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BeltEntry entry = entries[i];
            if (!entry.PendingRemoval || entry.Bucket == null)
            {
                continue;
            }

            if (pourAnimator != null && pourAnimator.HasActivePourTo(entry.Bucket))
            {
                continue;
            }

            CompleteFilledBucket(entry);
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    private void CompleteFilledBucket(BeltEntry entry)
    {
        if (entry == null || entry.Bucket == null)
        {
            return;
        }

        entries.Remove(entry);
        filledBuckets++;

        entry.PendingRemoval = false;
        entry.Bucket.MarkRemoved();

        if (OnBucketFilled != null)
        {
            OnBucketFilled(entry.Bucket);
        }

        NotifyChanged();
    }

    public bool IsComplete()
    {
        return filledBuckets >= requiredFilledBuckets && entries.Count == 0;
    }

    public bool HasBucketWithColor(byte colorIndex)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket != null && !entry.Bucket.IsFull && entry.Bucket.ColorIndex == colorIndex)
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyChanged()
    {
        if (OnConveyorChanged != null)
        {
            OnConveyorChanged();
        }
    }
}
