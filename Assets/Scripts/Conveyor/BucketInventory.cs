using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BucketInventory
{
    private readonly List<Bucket> buckets = new List<Bucket>();
    private readonly List<ConveyorSlotState> conveyorSlots = new List<ConveyorSlotState>();
    private readonly Queue<Bucket> reserve = new Queue<Bucket>();
    private LevelData activeLevel;
    private readonly HashSet<int> openGates = new HashSet<int>();
    private readonly HashSet<Bucket> crateBuckets = new HashSet<Bucket>();
    private readonly HashSet<int> reservedAccessPath = new HashSet<int>();
    public int CrateRow { get; private set; } = -1;
    public bool CratesOpen { get; private set; }
    public int CrateBucketCount => crateBuckets.Count;
    public bool IsGateClosed(Bucket bucket) => bucket != null && bucket.GateKeyId != 0 && !openGates.Contains(bucket.GateKeyId);
    public bool IsCrateCovered(Bucket bucket) => crateBuckets.Contains(bucket);
    public void HandleBucketFilled(Bucket bucket)
    {
        if (bucket != null && bucket.KeyId != 0 && openGates.Add(bucket.KeyId)) NotifyChanged();
    }
    public int ReserveCount { get { return reserve.Count; } }
    public IEnumerable<Bucket> ReservedBuckets { get { return reserve; } }

    public event Action OnInventoryChanged;

    public IReadOnlyList<Bucket> Buckets { get { return buckets; } }
    public IReadOnlyList<ConveyorSlotState> ConveyorSlots { get { return conveyorSlots; } }
    public int TotalBucketCount { get; private set; }

    public void BuildFromLevel(LevelData levelData, int subGrainsPerAxis, LevelSegmentRules bucketSettings = null)
    {
        buckets.Clear();
        openGates.Clear(); crateBuckets.Clear(); CrateRow = -1; CratesOpen = false;
        reservedAccessPath.Clear();
        conveyorSlots.Clear();
        reserve.Clear();
        activeLevel = levelData;
        TotalBucketCount = 0;

        if (levelData == null || levelData.cells == null || levelData.cells.Length == 0)
        {
            NotifyChanged();
            return;
        }

        int maxGrainsPerBucket = bucketSettings != null ? bucketSettings.maxGrainsPerBucket : 0;
        if (levelData.alternatingCrates || (levelData.campaignTemplateVersion > 0 && levelData.keyGates))
            ReserveAccessPath(levelData);
        int grainsPerLevelCell = Mathf.Max(1, subGrainsPerAxis) * Mathf.Max(1, subGrainsPerAxis);
        if (levelData.loopInventory) InventoryLoopRoute.Bind(levelData);
        BuildFromGeneratedQueue(levelData, grainsPerLevelCell, maxGrainsPerBucket);
        if (levelData.campaignLevel) ApplyCampaignBucketRules(levelData);

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

        if (!IsSingleBucketAccessible(bucket, levelData)) return false;
        return bucket.LinkedPartner == null || IsSingleBucketAccessible(bucket.LinkedPartner, levelData);
    }

    private bool IsSingleBucketAccessible(Bucket bucket, LevelData level)
        => buckets.Contains(bucket) && !bucket.IsFrozen && !IsGateClosed(bucket)
            && (!crateBuckets.Contains(bucket) || CratesOpen) && BucketRouteAccess.IsAccessible(bucket.LayoutIndex, level, this, bucket.LinkedPartner);

    public bool TryTakeBucket(Bucket bucket, ConveyorBelt conveyor, LevelData levelData)
    {
        return TryTakeBucket(bucket, conveyor, levelData, false);
    }

    public bool TryTakeBucket(Bucket bucket, ConveyorBelt conveyor, LevelData levelData, bool ignoreAccessibility)
    {
        if (bucket == null || !buckets.Contains(bucket) || conveyor == null)
        {
            return false;
        }

        if (!ignoreAccessibility && levelData != null && !IsBucketAccessible(bucket, levelData))
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
        crateBuckets.Remove(bucket);
        if (bucket.LinkedPartner != null)
        {
            buckets.Remove(bucket.LinkedPartner);
            crateBuckets.Remove(bucket.LinkedPartner);
            bucket.LinkedPartner.Reveal();
        }
        if (crateBuckets.Count > 0)
        {
            CratesOpen = !CratesOpen;
            // With no outside buckets left there is no legal move that can reopen the row.
            // Remove the empty crate mechanism instead of rejecting otherwise legal moves.
            if (CountOutsideActions() == 0) { crateBuckets.Clear(); CratesOpen = true; }
        }
        ApplySuccessfulMoveRules();
        bucket.Reveal();
        foreach (Bucket waiting in buckets) waiting.ThawOneMove();
        foreach (Bucket waiting in reserve) waiting.ThawOneMove();
        foreach (ConveyorSlotState slot in conveyorSlots)
            foreach (Bucket waiting in slot.ReservedBuckets) waiting.ThawOneMove();
        TryDispenseAll(levelData);
        NotifyChanged();
        return true;
    }

    public void NotifyInventoryChangedPublic()
    {
        NotifyChanged();
    }

    public int CountRemaining()
    {
        return buckets.Count + CountConveyorReserved() + reserve.Count;
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
        if (levelData == null)
        {
            return false;
        }

        bool changed = RefillReserve(levelData);
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
        bucket.SetLayoutIndex(-1);
        reserve.Enqueue(bucket);
        // Recall advances the waiting queue. Replacing the same front bucket could
        // permanently hide a needed colour when the belt has fewer than five slots.
        if (activeLevel != null)
        {
            if (!activeLevel.loopInventory)
            {
                // Advance the uncovered queue on crate boards too. Otherwise returning the
                // same front cups can permanently hide a needed colour behind them.
                var moving = buckets.Where(b => !crateBuckets.Contains(b)).OrderBy(b => b.LayoutIndex).ToArray();
                var slots = CollectAvailableSlotIndices(activeLevel, LevelData.BucketLayoutCellCount)
                    .Where(i => crateBuckets.Count == 0 || i / LevelData.BucketLayoutColumns != CrateRow).ToArray();
                for (int i = 0; i < moving.Length; i++)
                {
                    if (i < slots.Length) moving[i].SetLayoutIndex(slots[i]);
                    else
                    {
                        // A feeder can temporarily occupy the reserved access aisle.
                        // Return surplus cups to the queue when compacting the board.
                        buckets.Remove(moving[i]);
                        moving[i].SetLayoutIndex(-1);
                        reserve.Enqueue(moving[i]);
                    }
                }
            }
            RefillReserve(activeLevel);
        }
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
        if (levelData.campaignLevel) remainingQueue = InterleaveColors(remainingQueue);
        BuildConveyorSlots(levelData, remainingQueue);

        List<int> availableSlots = CollectAvailableSlotIndices(levelData, remainingQueue.Count);
        // Overflow belongs in the reserve, never on top of another clickable bucket.
        int visibleCount = Mathf.Min(availableSlots.Count, remainingQueue.Count);
        int[] shuffledSlots = CreateShuffledSlotOrder(availableSlots, visibleCount);
        if (levelData.campaignLevel) System.Array.Sort(shuffledSlots);
        for (int i = 0; i < remainingQueue.Count; i++)
        {
            ColorQuantizer.BucketQueueItem item = remainingQueue[i];
            Bucket bucket = new Bucket(item.colorIndex, item.capacity, i < visibleCount ? shuffledSlots[i] : -1);
            if (levelData.campaignLevel)
                bucket.SetModifiers(i % 5 == 4 ? levelData.frostMoves : 0, levelData.mysteryBuckets && i >= 5);
            if (i < visibleCount) buckets.Add(bucket);
            else reserve.Enqueue(bucket);
        }
    }

    private bool RefillReserve(LevelData level)
    {
        if (level == null || reserve.Count == 0) return false;
        bool changed = false;
        for (int index = LevelData.BucketLayoutCellCount - 1; index >= 0 && reserve.Count > 0; index--)
        {
            // Keep a gap on a moving ring so lower cups and feeder outlets can
            // become reachable. Feeders may fill it temporarily as their stock advances.
            if (level.loopInventory && buckets.Count >= InventoryLoopRoute.Cells.Length - 1) break;
            byte cell = level.bucketLayout[index];
            if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell)
                || reservedAccessPath.Contains(index) || GetBucketAtLayoutIndex(index) != null) continue;
            // A newly freed feeder outlet belongs to its waiting cup, before the
            // general recall queue. Otherwise recalls can starve a feeder forever.
            if (conveyorSlots.Any(s => s.RemainingCount > 0
                && ConveyorDirectionUtility.GetTargetLayoutIndex(s.LayoutIndex, s.Direction) == index)) continue;
            if (crateBuckets.Count > 0 && index / LevelData.BucketLayoutColumns == CrateRow) continue;
            Bucket next = reserve.Dequeue();
            next.SetLayoutIndex(index);
            buckets.Add(next);
            changed = true;
        }
        return changed;
    }

    private int CountOutsideActions()
    {
        int count = 0;
        var pairs = new HashSet<Bucket>();
        foreach (var item in buckets)
        {
            if (crateBuckets.Contains(item) || pairs.Contains(item)) continue;
            count++;
            if (item.LinkedPartner != null) pairs.Add(item.LinkedPartner);
        }
        count += reserve.Count;
        foreach (var slot in conveyorSlots) count += slot.RemainingCount;
        return count;
    }

    private void ApplySuccessfulMoveRules()
    {
        var swapped = new HashSet<Bucket>();
        foreach (var bucket in buckets)
        {
            Bucket partner = bucket.SwapPartner;
            if (partner == null || swapped.Contains(bucket) || !buckets.Contains(partner)
                || bucket.CurrentFill != 0 || partner.CurrentFill != 0) continue;
            byte color = bucket.ColorIndex;
            bucket.SetColorAndCapacity(partner.ColorIndex, bucket.Capacity);
            partner.SetColorAndCapacity(color, partner.Capacity);
            swapped.Add(bucket); swapped.Add(partner);
        }
        if (activeLevel != null && activeLevel.loopInventory)
            foreach (var bucket in buckets) bucket.SetLayoutIndex(InventoryLoopRoute.Next(bucket.LayoutIndex));
    }

    private void ApplyCampaignBucketRules(LevelData level)
    {
        var pool = new List<Bucket>(buckets);
        pool.AddRange(reserve);
        if (level.largeBuckets)
        {
            bool merged = false;
            for (int i = 5; i < pool.Count && !merged; i++)
            for (int j = i + 1; j < pool.Count; j++)
                if (pool[i].ColorIndex == pool[j].ColorIndex && pool[i].Capacity == pool[j].Capacity)
                { pool[i].MakeLarge(pool[j].Capacity); pool.RemoveAt(j); TotalBucketCount--; merged = true; break; }
        }
        if (level.sectionBuckets)
        {
            int wanted = CampaignProgression.SectionBucketCount(EstimateLevelNumber(level));
            if (level.loopInventory) wanted = Mathf.Min(wanted, 2);
            int made = 0;
            for (int i = 0; i + 1 < pool.Count && made < wanted; i++)
            {
                if (pool[i].SlotCost != 1 || pool[i].HasSecondSection) continue;
                for (int j = i + 1; j < pool.Count; j++)
                {
                    if (pool[j].SlotCost != 1 || pool[j].HasSecondSection || pool[i].ColorIndex == pool[j].ColorIndex) continue;
                    pool[i].AddSection(pool[j].ColorIndex, pool[j].Capacity);
                    pool[i].SetModifiers(0, false);
                    pool.RemoveAt(j); TotalBucketCount--; made++; break;
                }
            }
        }
        // Repack into the existing structural template after merging, preserving every grain.
        var slots = CollectAvailableSlotIndices(level, pool.Count);
        slots.Sort();
        buckets.Clear(); reserve.Clear();
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].SetLayoutIndex(i < slots.Count ? slots[i] : -1);
            if (i < slots.Count) buckets.Add(pool[i]); else reserve.Enqueue(pool[i]);
        }
        PlaceSpecialsOnRows(level);
        if (level.linkedBuckets && buckets.Count >= 3)
            LinkDistantPair(level);
        if (level.keyGates && buckets.Count >= 8)
            PlaceKeyAndLocks(level);
        PlaceSpecialsOnRows(level);
        if (level.swappingColors)
            PlaceSwapPair(level);
        if (level.keyGates && EstimateLevelNumber(level) >= CampaignProgression.LargeLevel)
            ClearColumnUnderKeys();
        if (level.alternatingCrates && buckets.Count >= 10)
        {
            CrateRow = buckets.Max(b => b.LayoutIndex) / LevelData.BucketLayoutColumns;
            foreach (var bucket in buckets)
                if (bucket.LayoutIndex / LevelData.BucketLayoutColumns == CrateRow) crateBuckets.Add(bucket);
        }
    }

    private static int EstimateLevelNumber(LevelData level)
    {
        if (level == null) return 1;
        if (!string.IsNullOrEmpty(level.bucketSettingsOverride != null ? level.bucketSettingsOverride.segmentId : null))
        {
            string id = level.bucketSettingsOverride.segmentId;
            int underscore = id.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(id.Substring(underscore + 1), out int number)) return number;
        }
        return Mathf.Max(1, level.bucketSettingsOverride != null ? level.bucketSettingsOverride.startLevelIndex + 1 : 1);
    }

    private void PlaceSpecialsOnRows(LevelData level)
    {
        // Keys and 1|2 specials prefer rows 1-3 (indices 0-2); locks use rows 4-5 later.
        var front = new List<int>();
        var back = new List<int>();
        foreach (var bucket in buckets)
        {
            int row = bucket.LayoutIndex / LevelData.BucketLayoutColumns;
            if (row <= 2) front.Add(bucket.LayoutIndex); else back.Add(bucket.LayoutIndex);
        }
        if (front.Count == 0 || back.Count == 0) return;
        var specials = new List<Bucket>();
        foreach (var bucket in buckets)
            if (bucket.HasSecondSection || bucket.KeyId != 0) specials.Add(bucket);
        Shuffle(specials);
        for (int i = 0; i < specials.Count && front.Count > 0; i++)
        {
            if (specials[i].LayoutIndex / LevelData.BucketLayoutColumns <= 2) continue;
            int target = front[UnityEngine.Random.Range(0, front.Count)];
            Bucket occupant = GetBucketAtLayoutIndex(target);
            if (occupant == null || occupant == specials[i]) continue;
            int old = specials[i].LayoutIndex;
            specials[i].SetLayoutIndex(target);
            occupant.SetLayoutIndex(old);
            front.Remove(target);
        }
    }

    private void LinkDistantPair(LevelData level)
    {
        Bucket first = null, second = null;
        for (int i = 0; i < buckets.Count; i++)
        {
            Bucket a = buckets[i];
            if (a.HasSecondSection || a.SlotCost > 1 || a.KeyId != 0) continue;
            for (int j = i + 1; j < buckets.Count; j++)
            {
                Bucket b = buckets[j];
                if (b.HasSecondSection || b.SlotCost > 1 || b.KeyId != 0 || a.ColorIndex == b.ColorIndex) continue;
                if (a.LayoutIndex / LevelData.BucketLayoutColumns == b.LayoutIndex / LevelData.BucketLayoutColumns
                    && Mathf.Abs(a.LayoutIndex - b.LayoutIndex) == 1) continue;
                first = a; second = b; break;
            }
            if (first != null) break;
        }
        if (first == null || second == null)
        {
            if (buckets.Count < 3) return;
            first = buckets[1]; second = buckets[Mathf.Min(2, buckets.Count - 1)];
            if (first == second && buckets.Count > 3) second = buckets[3];
        }
        first.LinkedPartner = second; second.LinkedPartner = first;
        first.SetModifiers(0, false); second.SetModifiers(0, false);
    }

    private void PlaceKeyAndLocks(LevelData level)
    {
        var front = buckets.Where(b => b.LayoutIndex / LevelData.BucketLayoutColumns <= 2
            && !b.HasSecondSection && b.LinkedPartner == null && b.SlotCost == 1).ToList();
        if (front.Count == 0) front = buckets.Where(b => !b.HasSecondSection && b.LinkedPartner == null).ToList();
        if (front.Count == 0) return;
        Bucket key = front[UnityEngine.Random.Range(0, front.Count)];
        if (level.campaignTemplateVersion > 0 && !BucketRouteAccess.IsAccessible(key.LayoutIndex, level, this))
        {
            // A key behind its own locked approach makes an otherwise connected board insoluble.
            var exit = buckets.FirstOrDefault(b => b != key && b.LayoutIndex < LevelData.BucketLayoutColumns);
            if (exit != null)
            {
                int old = key.LayoutIndex;
                key.SetLayoutIndex(exit.LayoutIndex);
                exit.SetLayoutIndex(old);
            }
        }
        key.KeyId = 1; key.SetModifiers(0, false);
        var locks = buckets.Where(b => b != key && b.LayoutIndex / LevelData.BucketLayoutColumns >= 3
            && b.LinkedPartner == null && !b.HasSecondSection).ToList();
        if (locks.Count < 2)
            locks = buckets.Where(b => b != key && b.LinkedPartner == null && !b.HasSecondSection).Reverse().Take(3).ToList();
        int lockCount = Mathf.Min(3, locks.Count);
        for (int i = 0; i < lockCount; i++) locks[i].GateKeyId = 1;
    }

    private void PlaceSwapPair(LevelData level)
    {
        bool preferDifferentRows = EstimateLevelNumber(level) >= CampaignProgression.SwapLevel;
        if (!TryAssignSwapPair(preferDifferentRows) && preferDifferentRows)
            TryAssignSwapPair(false);
    }

    private bool TryAssignSwapPair(bool requireDifferentRows)
    {
        for (int i = 0; i < buckets.Count; i++)
        for (int j = i + 1; j < buckets.Count; j++)
        {
            Bucket a = buckets[i], b = buckets[j];
            if (a.HasSecondSection || b.HasSecondSection || a.SlotCost != 1 || b.SlotCost != 1
                || a.LinkedPartner != null || b.LinkedPartner != null || a.KeyId != 0 || b.KeyId != 0
                || a.GateKeyId != 0 || b.GateKeyId != 0 || a.ColorIndex == b.ColorIndex) continue;
            if (requireDifferentRows
                && a.LayoutIndex / LevelData.BucketLayoutColumns == b.LayoutIndex / LevelData.BucketLayoutColumns)
                continue;
            int sharedCapacity = Mathf.Min(a.Capacity, b.Capacity);
            Bucket larger = a.Capacity > b.Capacity ? a : b;
            int surplus = larger.Capacity - sharedCapacity;
            if (surplus > 0)
            {
                reserve.Enqueue(new Bucket(larger.ColorIndex, surplus));
                larger.SetColorAndCapacity(larger.ColorIndex, sharedCapacity);
                TotalBucketCount++;
            }
            a.SwapPartner = b; b.SwapPartner = a; a.SetModifiers(0, false); b.SetModifiers(0, false);
            return true;
        }
        return false;
    }

    private void ClearColumnUnderKeys()
    {
        foreach (var key in buckets.Where(b => b.KeyId != 0).ToList())
        {
            int col = key.LayoutIndex % LevelData.BucketLayoutColumns;
            int row = key.LayoutIndex / LevelData.BucketLayoutColumns;
            for (int y = row + 1; y < LevelData.BucketLayoutRows; y++)
            {
                int index = y * LevelData.BucketLayoutColumns + col;
                Bucket below = GetBucketAtLayoutIndex(index);
                if (below == null || below.KeyId != 0) continue;
                int free = FindAlternateSlot(below, col);
                if (free >= 0)
                {
                    Bucket occupant = GetBucketAtLayoutIndex(free);
                    below.SetLayoutIndex(free);
                    if (occupant != null) occupant.SetLayoutIndex(index);
                }
                else
                {
                    buckets.Remove(below);
                    below.SetLayoutIndex(-1);
                    reserve.Enqueue(below);
                }
            }
        }
    }

    private int FindAlternateSlot(Bucket moving, int avoidColumn)
    {
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            if (i % LevelData.BucketLayoutColumns == avoidColumn) continue;
            if (reservedAccessPath.Contains(i)) continue;
            if (activeLevel != null)
            {
                byte cell = activeLevel.bucketLayout[i];
                if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell)) continue;
            }
            if (GetBucketAtLayoutIndex(i) == null) return i;
        }
        return -1;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private static List<ColorQuantizer.BucketQueueItem> InterleaveColors(List<ColorQuantizer.BucketQueueItem> queue)
    {
        var groups = new List<Queue<ColorQuantizer.BucketQueueItem>>();
        var byColor = new Dictionary<byte, Queue<ColorQuantizer.BucketQueueItem>>();
        foreach (var item in queue)
        {
            if (!byColor.TryGetValue(item.colorIndex, out var group))
            {
                group = new Queue<ColorQuantizer.BucketQueueItem>();
                byColor.Add(item.colorIndex, group);
                groups.Add(group);
            }
            group.Enqueue(item);
        }
        var result = new List<ColorQuantizer.BucketQueueItem>(queue.Count);
        for (int i = groups.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var swap = groups[i]; groups[i] = groups[j]; groups[j] = swap;
        }
        while (result.Count < queue.Count)
            foreach (var group in groups)
                if (group.Count > 0) result.Add(group.Dequeue());
        return result;
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

    private List<int> CollectAvailableSlotIndices(LevelData levelData, int bucketCount)
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
                if (!reservedAccessPath.Contains(i)) slots.Add(i);
            }

            return slots;
        }

        for (int layoutIndex = 0; layoutIndex < LevelData.BucketLayoutCellCount; layoutIndex++)
        {
            byte cell = levelData.bucketLayout[layoutIndex];
            if (reservedAccessPath.Contains(layoutIndex) || LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell))
            {
                continue;
            }

            slots.Add(layoutIndex);
        }
        if (levelData.loopInventory && slots.Count > 1) slots.RemoveAt(slots.Count - 1);
        return slots;
    }

    private void ReserveAccessPath(LevelData level)
    {
        // Crates and key locks must not trap the colours needed to unlock them.
        // Reserve a connected aisle; feeder cups may enter it but can be dispatched
        // along the same aisle. Recalled/queued cups never refill these cells.
        level.EnsureBucketLayout();
        var previous = Enumerable.Repeat(-2, LevelData.BucketLayoutCellCount).ToArray();
        var queue = new Queue<int>();
        foreach (int cell in new[] { 2, 1, 3, 0, 4 })
            if (!Blocked(cell)) { previous[cell] = -1; queue.Enqueue(cell); }
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            if (cell / LevelData.BucketLayoutColumns == LevelData.BucketLayoutRows - 1)
            {
                for (int step = cell; step >= 0; step = previous[step]) reservedAccessPath.Add(step);
                return;
            }
            int x = cell % 5, y = cell / 5;
            Visit(x, y + 1, cell); Visit(x - 1, y, cell); Visit(x + 1, y, cell); Visit(x, y - 1, cell);
        }
        bool Blocked(int cell) => LevelData.IsBucketLayoutWall(level.bucketLayout[cell]) || LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell]);
        void Visit(int x, int y, int from)
        {
            if (x < 0 || x >= 5 || y < 0 || y >= 5) return;
            int cell = y * 5 + x;
            if (previous[cell] != -2 || Blocked(cell)) return;
            previous[cell] = from; queue.Enqueue(cell);
        }
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
