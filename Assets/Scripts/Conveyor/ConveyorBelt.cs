using System;
using System.Collections.Generic;
using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public const int MaxOnBelt = 5;
    public const int MaxRewardedCapacity = 7;
    public const float CollectionX = -95f;
    public static float EntryX { get; private set; } = 200f;
    public static float ExitX { get; private set; } = -220f;
    public const float CollectionTolerance = 35f;
    public static float BucketSpacing { get; private set; } = 72f;
    public static float BucketWidth { get; private set; } = 40f;

    [SerializeField] private ConveyorSettings settings;
    [SerializeField] private int requiredFilledBuckets = 5;

    private readonly List<BeltEntry> entries = new List<BeltEntry>();
    private int filledBuckets;
    private bool allBucketsDeployed;
    private bool movementPaused;
    private float beltTravel;
    private SandConveyorMapper mapper;
    private SandPourAnimator pourAnimator;
    private int capacity = MaxOnBelt;
    private float levelSpeedMultiplier = 1f;
    private static float visibleSpawnLimit = 140f;
    private int levelCapacity = MaxOnBelt;
    private int levelGeneration;
    private int nextRewardRequest;
    private int pendingRewardRequest;
    public int Capacity { get { return capacity; } }
    public int OccupiedSlots { get { int total = 0; foreach (var entry in entries) total += entry.Bucket.SlotCost; return total; } }
    public bool CanAddRewardedSlot => capacity < MaxRewardedCapacity;
    public int FreeSlots => Mathf.Max(0, capacity - OccupiedSlots);
    public int AvailableSpawnSlots => FindVisibleSpawnSlots(out _);
    public static float VisibleSpawnLimit => visibleSpawnLimit;

    public event Action OnConveyorChanged;
    public event Action<Bucket> OnBucketFilled;
    public event Action<Bucket> OnBucketReturned;

    public int FilledBuckets { get { return filledBuckets; } }
    public int RequiredFilledBuckets { get { return requiredFilledBuckets; } }
    public IReadOnlyList<BeltEntry> Entries { get { return entries; } }
    public bool IsFastSpeed { get { return entries.Count > 0 && (allBucketsDeployed || IsFull()); } }
    public bool IsMovementPaused { get { return movementPaused; } }
    public float BeltTravel { get { return beltTravel; } }
    public float MoveSpeed { get { return (settings != null ? settings.moveSpeed : ConveyorSettings.DefaultMoveSpeed) * levelSpeedMultiplier; } }
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

    public void ConfigureLevel(LevelData level)
    {
        capacity = level != null && level.campaignLevel ? Mathf.Clamp(level.beltCapacity, 3, MaxOnBelt) : MaxOnBelt;
        levelCapacity = capacity;
        levelGeneration++;
        pendingRewardRequest = 0;
        levelSpeedMultiplier = level != null && level.campaignLevel ? Mathf.Clamp(level.beltSpeedMultiplier, 0.6f, 1.5f) : 1f;
        NotifyChanged();
    }

    public static void ConfigureGeometry(float beltWidthPixels, float bucketWidthPixels)
    {
        BucketWidth = Mathf.Max(16f, bucketWidthPixels);
        float halfBelt = beltWidthPixels * 0.5f;
        // Initial placement stays fully inside the exposed deck, including the rear rim.
        visibleSpawnLimit = Mathf.Max(0, (halfBelt - 26f) * .90f - BucketWidth * .50f - 10f);
        float sidePadding = Mathf.Max(52f, BucketWidth * 0.45f);
        float usableWidth = Mathf.Max(BucketWidth * MaxOnBelt, beltWidthPixels - sidePadding * 2f);
        float minPitch = BucketWidth + 10f;
        BucketSpacing = Mathf.Max(minPitch, usableWidth / MaxOnBelt);
        // Sweep the complete painting, including its outermost columns. The old half-pitch
        // inset wrapped buckets before they could reach edge grains, leaving unwinnable residue.
        // Wrap only after the whole bucket has travelled inside an opaque end hood.
        EntryX = halfBelt + BucketWidth * .65f;
        ExitX = -EntryX;
    }

    public static float GetSlotCenterX(int slotIndexFromEntry)
    {
        return EntryX - slotIndexFromEntry * BucketSpacing;
    }

    public void ResetConveyor()
    {
        entries.Clear();
        filledBuckets = 0;
        allBucketsDeployed = false;
        movementPaused = false;
        beltTravel = 0;
        capacity = levelCapacity;
        levelGeneration++;
        pendingRewardRequest = 0;
        NotifyChanged();
    }

    public void SetMovementPaused(bool paused)
    {
        movementPaused = paused;
    }

    public void SetAllBucketsDeployed(bool deployed)
    {
        allBucketsDeployed = deployed;
    }

    public bool CanAcceptBucket()
    {
        return FreeSlots > 0;
    }

    public bool IsFull()
    {
        return OccupiedSlots >= capacity;
    }

    public bool TryPlaceBucket(Bucket bucket)
    {
        if (bucket == null || bucket.State != BucketState.InInventory)
        {
            return false;
        }

        Bucket partner = bucket.LinkedPartner;
        if (partner != null && partner.State != BucketState.InInventory) return false;
        int needed = bucket.SlotCost + (partner != null ? partner.SlotCost : 0);
        if (FreeSlots < needed) return false;
        Place(bucket);
        if (partner != null) Place(partner);
        NotifyChanged();
        return true;

        void Place(Bucket item)
        {
            if (FindVisibleSpawnSlots(out float x, item.SlotCost) == 0) x = GetWrapPositionX(null, item.SlotCost);
            item.MoveToConveyor();
            entries.Add(new BeltEntry { Bucket = item, PositionX = x, PendingRemoval = item.IsFull });
        }
    }

    // Greedily fit new buckets into actual visible gaps. No allocation, no extra inventory,
    // and no inventory mutation until a position has been found. Up to five occupants.
    private int FindVisibleSpawnSlots(out float firstX, int slotCost = 1)
    {
        firstX = 0;
        int slots = 0;
        float limit = visibleSpawnLimit - (slotCost - 1) * BucketWidth * .5f;
        float candidate = limit;
        while (slots < FreeSlots / slotCost && candidate >= -limit)
        {
            bool moved;
            do
            {
                moved = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    float occupied = entries[i].PositionX;
                    float separation = BucketSpacing * (entries[i].Bucket.SlotCost + slotCost) * .5f;
                    if (Mathf.Abs(candidate - occupied) < separation - .01f)
                    {
                        candidate = occupied - separation;
                        moved = true;
                    }
                }
            } while (moved && candidate >= -limit);
            if (candidate < -limit) break;
            if (slots == 0) firstX = candidate;
            slots++;
            candidate -= BucketSpacing * slotCost;
        }
        return slots;
    }

    private float GetWrapPositionX(BeltEntry exclude, int slotCost = 1)
    {
        float spawnX = EntryX;
        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry == exclude)
            {
                continue;
            }

            spawnX = Mathf.Max(spawnX, entry.PositionX + BucketSpacing * (entry.Bucket.SlotCost + slotCost) * .5f);
        }

        return spawnX;
    }

    public float SnapToCell(float x)
    {
        float spacing = Mathf.Max(1f, BucketSpacing);
        int cellIndex = Mathf.RoundToInt((EntryX - beltTravel - x) / spacing);
        return EntryX - beltTravel - cellIndex * spacing;
    }

    public void UpdateBelt(float deltaTime)
    {
        if (pourAnimator == null)
        {
            pourAnimator = FindObjectOfType<SandPourAnimator>();
        }

        ProcessPendingRemovals();

        if (movementPaused)
        {
            return;
        }

        float speed = MoveSpeed * (IsFastSpeed ? FullSpeedMultiplier : 1f);
        float move = speed * deltaTime;
        beltTravel += move;

        if (entries.Count == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            BeltEntry entry = entries[i];
            entry.PositionX -= move;

            bool waitingForPartner = entry.Bucket.LinkedPartner != null && !entry.Bucket.LinkedPartner.IsFull;
            if (entry.PositionX < ExitX && (!entry.Bucket.IsFull || waitingForPartner))
            {
                entry.PositionX = GetWrapPositionX(entry, entry.Bucket.SlotCost);
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public int BeginSlotReward(out int generation)
    {
        generation = levelGeneration;
        if (!CanAddRewardedSlot || pendingRewardRequest != 0) return 0;
        pendingRewardRequest = ++nextRewardRequest;
        return pendingRewardRequest;
    }

    public bool CompleteSlotReward(int request, int generation, bool successful)
    {
        if (request == 0 || request != pendingRewardRequest || generation != levelGeneration) return false;
        pendingRewardRequest = 0;
        if (!successful || !CanAddRewardedSlot) return false;
        capacity++;
        NotifyChanged();
        return true;
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

    public bool TryRecallBucket(Bucket bucket)
    {
        if (movementPaused || bucket == null || (bucket.IsFull && bucket.LinkedPartner == null)) return false;
        if (pourAnimator != null && pourAnimator.HasActivePourTo(bucket)) return false;
        BeltEntry entry = entries.Find(e => e.Bucket == bucket);
        if (entry == null || (entry.PendingRemoval && bucket.LinkedPartner == null)) return false;
        BeltEntry partner = bucket.LinkedPartner != null ? entries.Find(e => e.Bucket == bucket.LinkedPartner) : null;
        if (bucket.LinkedPartner != null && (partner == null || (pourAnimator != null && pourAnimator.HasActivePourTo(partner.Bucket)))) return false;
        entries.Remove(entry);
        if (partner != null) entries.Remove(partner);
        ReturnBucketToInventory(entry);
        if (partner != null) ReturnBucketToInventory(partner);
        NotifyChanged();
        return true;
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
            if (i >= entries.Count) continue;
            BeltEntry entry = entries[i];
            if (!entry.PendingRemoval || entry.Bucket == null)
            {
                continue;
            }

            BeltEntry partner = entry.Bucket.LinkedPartner != null ? entries.Find(e => e.Bucket == entry.Bucket.LinkedPartner) : null;
            if (entry.Bucket.LinkedPartner != null && (partner == null || !partner.Bucket.IsFull
                || (pourAnimator != null && pourAnimator.HasActivePourTo(partner.Bucket)))) continue;

            if (pourAnimator != null && pourAnimator.HasActivePourTo(entry.Bucket))
            {
                continue;
            }

            CompleteFilledBucket(entry);
            if (partner != null) CompleteFilledBucket(partner);
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

    public void MarkBucketFilledByBooster(Bucket bucket)
    {
        if (bucket == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            BeltEntry entry = entries[i];
            if (entry.Bucket != bucket)
            {
                continue;
            }

            entry.PendingRemoval = true;
            NotifyChanged();
            return;
        }
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
