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
    private bool dispensing;
    private readonly HashSet<int> openGates = new HashSet<int>();
    private readonly HashSet<int> crateLayoutIndices = new HashSet<int>();
    private readonly HashSet<int> reservedAccessPath = new HashSet<int>();
    public int CrateRow { get; private set; } = -1;
    public bool CratesOpen { get; private set; }
    public int CrateBucketCount => buckets.Count(IsCrateCovered);
    public IEnumerable<int> CrateLayoutIndices => crateLayoutIndices;
    public bool IsGateClosed(Bucket bucket) => bucket != null && bucket.GateKeyId != 0 && !openGates.Contains(bucket.GateKeyId);
    public bool IsCrateCellClosed(int index) => activeLevel != null && activeLevel.alternatingCrates
        && index >= 0 && index % LevelData.BucketLayoutColumns % 2 == (CratesOpen ? 1 : 0);
    public bool IsCrateCovered(Bucket bucket) => bucket != null && IsCrateCellClosed(bucket.LayoutIndex);
    public void HandleBucketFilled(Bucket bucket)
    {
        if (bucket != null && bucket.KeyId != 0 && openGates.Add(bucket.KeyId)) NotifyChanged();
    }
    public int ReserveCount { get { return reserve.Count; } }
    public bool NeedsSettling { get; private set; }
    public IEnumerable<Bucket> ReservedBuckets { get { return reserve; } }

    public event Action OnInventoryChanged;

    public IReadOnlyList<Bucket> Buckets { get { return buckets; } }
    public IReadOnlyList<ConveyorSlotState> ConveyorSlots { get { return conveyorSlots; } }
    public int TotalBucketCount { get; private set; }

    public void BuildFromLevel(LevelData levelData, int subGrainsPerAxis, LevelSegmentRules bucketSettings = null)
    {
        buckets.Clear();
        openGates.Clear(); crateLayoutIndices.Clear(); CrateRow = -1; CratesOpen = false;
        reservedAccessPath.Clear();
        conveyorSlots.Clear();
        reserve.Clear();
        activeLevel = levelData;
        TotalBucketCount = 0;

        if (levelData == null || levelData.cells == null || levelData.cells.Length == 0)
        {
            NeedsSettling = false;
            NotifyChanged();
            return;
        }

        int maxGrainsPerBucket = bucketSettings != null ? bucketSettings.maxGrainsPerBucket : 0;
        int grainsPerLevelCell = Mathf.Max(1, subGrainsPerAxis) * Mathf.Max(1, subGrainsPerAxis);
        if (levelData.loopInventory) InventoryLoopRoute.Bind(levelData);
        BuildFromGeneratedQueue(levelData, grainsPerLevelCell, maxGrainsPerBucket, bucketSettings != null && bucketSettings.matchBucketOrderToSand);
        if (levelData.campaignLevel) ApplyCampaignBucketRules(levelData);
        SettleInitialLayout(levelData);
        if (levelData.campaignLevel)
        {
            BalanceBucketSand();
            MixColumnColors(levelData);
            EnsurePlayableOpening(levelData);
            ApplyAdvancedMysteryRules(levelData);
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
            // Empty neighboring columns never bypass cups in this bucket's own column.
            && BucketGridAccess.IsAccessible(bucket.LayoutIndex, this)
            && !IsCrateCovered(bucket) && BucketRouteAccess.IsAccessible(bucket.LayoutIndex, level, this, bucket.LinkedPartner);

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
        if (bucket.LinkedPartner != null)
        {
            buckets.Remove(bucket.LinkedPartner);
            bucket.LinkedPartner.Reveal();
        }
        if (levelData != null && levelData.alternatingCrates) CratesOpen = !CratesOpen;
        ApplySuccessfulMoveRules();
        bucket.Reveal();
        foreach (Bucket waiting in buckets) waiting.ThawOneMove();
        foreach (Bucket waiting in reserve) waiting.ThawOneMove();
        foreach (ConveyorSlotState slot in conveyorSlots)
            foreach (Bucket waiting in slot.ReservedBuckets) waiting.ThawOneMove();
        if (!TryDispenseAll(levelData))
        {
            EnsurePlayableOpening(levelData);
            NotifyChanged();
        }
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
        if (levelData == null || dispensing)
        {
            return false;
        }
        dispensing = true;
        try
        {
            bool changed = AdvanceBuckets(levelData);
            for (int i = 0; i < conveyorSlots.Count; i++)
            {
                changed |= TryDispenseFromSlot(conveyorSlots[i], levelData);
            }
            changed |= RefillReserve(levelData);
            // Advance the crate tick when only covered routes remain, including the final cup.
            if (!changed && levelData.alternatingCrates && buckets.Count > 0
                && !buckets.Any(b => IsBucketAccessible(b, levelData)))
            {
                CratesOpen = !CratesOpen;
                changed = true;
            }
            if (changed)
            {
                buckets.Sort(CompareBucketsByLayout);
                EnsurePlayableOpening(levelData);
                NotifyChanged();
            }
            NeedsSettling = changed;
            return changed;
        }
        finally { dispensing = false; }
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
        // Recalled cups join the same rear queue without crossing walls or other columns.
        if (activeLevel == null || !TryDispenseAll(activeLevel)) NotifyChanged();
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

    private void BuildFromGeneratedQueue(LevelData levelData, int grainsPerLevelCell, int maxGrainsPerBucket, bool matchSandSupply)
    {
        ColorQuantizer.BucketQueueItem[] queue = ColorQuantizer.GenerateLevelBucketQueue(
            levelData.cells,
            levelData.gridWidth,
            levelData.gridHeight,
            grainsPerLevelCell,
            maxGrainsPerBucket);

        List<ColorQuantizer.BucketQueueItem> remainingQueue = new List<ColorQuantizer.BucketQueueItem>(queue);
        if (levelData.campaignLevel)
            PopulateBoardQueue(levelData, remainingQueue);
        TotalBucketCount = remainingQueue.Count;
        if (levelData.campaignLevel) remainingQueue = matchSandSupply
            ? OrderBucketsBySandSupply(levelData, remainingQueue, grainsPerLevelCell)
            : InterleaveColors(remainingQueue);
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

    private void PopulateBoardQueue(LevelData level, List<ColorQuantizer.BucketQueueItem> queue)
    {
        int target = CollectAvailableSlotIndices(level, LevelData.BucketLayoutCellCount).Count;
        for (int cell = 0; cell < LevelData.BucketLayoutCellCount; cell++)
            if (LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[cell])) target += level.GetConveyorBucketCount(cell);
        if (level.sectionBuckets)
        {
            int sections = CampaignProgression.SectionBucketCount(EstimateLevelNumber(level));
            target += level.loopInventory ? Mathf.Min(2, sections) : sections;
        }
        if (level.largeBuckets) target++;
        // Split the actual sand budget; never create extra demand or reduce authored cup counts.
        while (queue.Count < target)
        {
            int largest = -1;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].capacity > 1 && (largest < 0 || queue[i].capacity > queue[largest].capacity)) largest = i;
            if (largest < 0) break;
            var item = queue[largest];
            int half = item.capacity / 2;
            queue[largest] = new ColorQuantizer.BucketQueueItem { colorIndex = item.colorIndex, capacity = item.capacity - half };
            queue.Add(new ColorQuantizer.BucketQueueItem { colorIndex = item.colorIndex, capacity = half });
        }
    }

    private void PopulateAccessPath(LevelData level)
    {
        // Split a seated plain cup after queue generation so existing colors,
        // feeder stock and special placements keep their authored order.
        foreach (int cell in reservedAccessPath.OrderBy(c => c))
        {
            if (GetBucketAtLayoutIndex(cell) != null) continue;
            Bucket donor = AllInitialBuckets().Where(b => !b.HasSecondSection && b.SlotCost == 1
                && b.LinkedPartner == null && b.SwapPartner == null && b.Capacity >= 4)
                .OrderBy(b => buckets.Count(other => other.LayoutIndex % LevelData.BucketLayoutColumns == cell % LevelData.BucketLayoutColumns
                    && other.ColorIndex == b.ColorIndex))
                .ThenByDescending(b => b.Capacity)
                .ThenBy(b => b.LayoutIndex).FirstOrDefault();
            if (donor == null) break;
            int share = donor.Capacity / 2;
            Bucket opening = new Bucket(donor.ColorIndex, share, cell);
            donor.SetColorAndCapacity(donor.ColorIndex, donor.Capacity - share);
            buckets.Add(opening);
            if (CrateRow >= 0 && cell / LevelData.BucketLayoutColumns == CrateRow)
            {

                crateLayoutIndices.Add(cell);
            }
            TotalBucketCount++;
        }
        buckets.Sort(CompareBucketsByLayout);
    }

    private IEnumerable<Bucket> AllInitialBuckets()
    {
        return buckets.Concat(reserve).Concat(conveyorSlots.SelectMany(slot => slot.ReservedBuckets));
    }

    private sealed class InitialSandShare
    {
        public Bucket Bucket;
        public bool Second;
        public int Weight;
        public int Capacity;
    }

    private void BalanceBucketSand()
    {
        var cups = AllInitialBuckets().ToArray();
        var shares = new Dictionary<byte, List<InitialSandShare>>();
        foreach (var cup in cups)
        {
            Add(cup.FirstColor, cup, false, cup.SlotCost, cup.HasSecondSection ? cup.FirstSectionCapacity : cup.Capacity);
            if (cup.HasSecondSection) Add(cup.SecondColor, cup, true, 1, cup.Capacity - cup.FirstSectionCapacity);
        }
        foreach (var group in shares.Values)
        {
            int units = group.Sum(s => s.Weight);
            int total = group.Sum(s => s.Capacity);
            int capacity = total / units;
            int remainder = total % units;
            foreach (var share in group)
            {
                int extra = Mathf.Min(remainder, share.Weight);
                share.Capacity = capacity * share.Weight + extra;
                remainder -= extra;
            }
        }
        foreach (var cup in cups)
        {
            int first = shares[cup.FirstColor].First(s => s.Bucket == cup && !s.Second).Capacity;
            if (cup.HasSecondSection)
                cup.SetSectionCapacities(first, shares[cup.SecondColor].First(s => s.Bucket == cup && s.Second).Capacity);
            else cup.SetColorAndCapacity(cup.FirstColor, first);
        }
        void Add(byte color, Bucket cup, bool second, int weight, int capacity)
        {
            if (!shares.TryGetValue(color, out var group)) shares[color] = group = new List<InitialSandShare>();
            group.Add(new InitialSandShare { Bucket = cup, Second = second, Weight = weight, Capacity = capacity });
        }
    }

    private void MixColumnColors(LevelData level)
    {
        if (level.GetColorsUsedInLevel().Length < 2) return;
        for (int x = 0; x < LevelData.BucketLayoutColumns; x++)
        {
            var column = buckets.Where(b => b.LayoutIndex % LevelData.BucketLayoutColumns == x).ToArray();
            if (column.Length < 2 || column.Select(b => b.ColorIndex).Distinct().Count() > 1) continue;
            bool mixed = false;
            foreach (var cup in column.Where(CanMix))
            {
                foreach (var other in buckets.Where(b => CanMix(b) && b.ColorIndex != cup.ColorIndex
                    && b.LayoutIndex / LevelData.BucketLayoutColumns == cup.LayoutIndex / LevelData.BucketLayoutColumns
                    && IsCrateCovered(b) == IsCrateCovered(cup)))
                {
                    int otherColumn = other.LayoutIndex % LevelData.BucketLayoutColumns;
                    var remaining = buckets.Where(b => b != other && b.LayoutIndex % LevelData.BucketLayoutColumns == otherColumn).ToArray();
                    if (remaining.Length > 0 && remaining.Select(b => b.ColorIndex).Concat(new[] { cup.ColorIndex }).Distinct().Count() < 2) continue;
                    int old = cup.LayoutIndex;
                    cup.SetLayoutIndex(other.LayoutIndex);
                    other.SetLayoutIndex(old);
                    mixed = true;
                    break;
                }
                if (mixed) break;
            }
        }
        buckets.Sort(CompareBucketsByLayout);
        bool CanMix(Bucket b) => b.LinkedPartner == null && b.SwapPartner == null && b.KeyId == 0 && b.GateKeyId == 0;
    }

    private bool AdvanceBuckets(LevelData level)
    {
        bool changed = false;
        foreach (var bucket in buckets.OrderBy(b => b.LayoutIndex))
        {
            // Belt cups have already taken their clockwise step. Only cups
            // outside the route rise through the free cells above them.
            if (level.loopInventory && Array.IndexOf(InventoryLoopRoute.Cells, bucket.LayoutIndex) >= 0) continue;
            // Recalled pairs may re-enter at separate rear slots. Preserve neighboring
            // columns without ever forcing the two halves back onto the same row.
            if (!level.loopInventory && bucket.LinkedPartner != null && buckets.Contains(bucket.LinkedPartner)
                && !AreInNeighboringColumns(bucket, bucket.LinkedPartner))
                ReseatLinkedPair(level, bucket, bucket.LinkedPartner);
            int target = bucket.LayoutIndex - LevelData.BucketLayoutColumns;
            while (target >= 0
                && (!level.loopInventory || Array.IndexOf(InventoryLoopRoute.Cells, bucket.LayoutIndex) < 0)
                && !IsWaitingFeederOutlet(target)
                && (!reservedAccessPath.Contains(target) || level.campaignLevel && reservedAccessPath.Contains(bucket.LayoutIndex))
                && CanReceiveDispensedBucket(target, level))
            {
                bucket.SetLayoutIndex(target);
                changed = true;
                target -= LevelData.BucketLayoutColumns;
            }
        }
        return changed;
    }

    private bool RefillReserve(LevelData level)
    {
        if (level == null || reserve.Count == 0) return false;
        bool changed = false;
        int rearStart = (LevelData.BucketLayoutRows - 1) * LevelData.BucketLayoutColumns;
        var refillSlots = level.loopInventory
            ? Enumerable.Range(0, LevelData.BucketLayoutCellCount).OrderByDescending(i => i / LevelData.BucketLayoutColumns).ThenBy(i => i)
            : Enumerable.Range(rearStart, LevelData.BucketLayoutColumns).OrderBy(i => i);
        int movingCount = level.loopInventory ? buckets.Count(b => Array.IndexOf(InventoryLoopRoute.Cells, b.LayoutIndex) >= 0) : 0;
        foreach (int index in refillSlots)
        {
            if (reserve.Count == 0) break;
            // Keep a gap on a moving ring so lower cups and feeder outlets can
            // become reachable. Feeders may fill it temporarily as their stock advances.
            bool onLoop = level.loopInventory && Array.IndexOf(InventoryLoopRoute.Cells, index) >= 0;
            if (onLoop && movingCount >= InventoryLoopRoute.Cells.Length - 1) continue;
            byte cell = level.bucketLayout[index];
            if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell)
                || reservedAccessPath.Contains(index) || GetBucketAtLayoutIndex(index) != null) continue;
            // A newly freed feeder outlet belongs to its waiting cup, before the
            // general recall queue. Otherwise recalls can starve a feeder forever.
            if (IsWaitingFeederOutlet(index)) continue;

            Bucket next = reserve.Dequeue();
            next.SetLayoutIndex(index);
            buckets.Add(next);
            if (onLoop) movingCount++;
            changed = true;
        }
        return changed;
    }

    private bool IsWaitingFeederOutlet(int index)
        => conveyorSlots.Any(s => s.RemainingCount > 0
            && ConveyorDirectionUtility.GetTargetLayoutIndex(s.LayoutIndex, s.Direction) == index);

    private int CountOutsideActions()
    {
        int count = 0;
        var pairs = new HashSet<Bucket>();
        foreach (var item in buckets)
        {
            if (IsCrateCovered(item) || pairs.Contains(item)) continue;
            count++;
            if (item.LinkedPartner != null) pairs.Add(item.LinkedPartner);
        }
        // Hidden stock cannot reopen closed crates while the covered rear row blocks entry.
        return count;
    }

    private void ApplySuccessfulMoveRules()
    {
        var swapped = new HashSet<Bucket>();
        var groups = new HashSet<IReadOnlyList<Bucket>>();
        foreach (var bucket in buckets)
        {
            if (bucket.ColorSwapGroup != null)
            {
                if (groups.Add(bucket.ColorSwapGroup)) ShuffleColorGroup(bucket.ColorSwapGroup);
                continue;
            }
            Bucket partner = bucket.SwapPartner;
            if (partner == null || swapped.Contains(bucket) || !buckets.Contains(partner)
                || bucket.CurrentFill != 0 || partner.CurrentFill != 0) continue;
            byte color = bucket.ColorIndex;
            int capacity = bucket.Capacity;
            bucket.SetColorAndCapacity(partner.ColorIndex, partner.Capacity);
            partner.SetColorAndCapacity(color, capacity);
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
        if (level.keyGates && buckets.Count >= 8)
            PlaceKeyAndLocks(level);
        PlaceSpecialsOnRows(level);
        if (level.linkedBuckets && buckets.Count >= 3)
            LinkAdjacentPairs(level);
        if (level.swappingColors)
            PlaceSwapPair(level);
        if (level.alternatingCrates)
        {
            CrateRow = 0;
            for (int index = 0; index < LevelData.BucketLayoutCellCount; index++)
                crateLayoutIndices.Add(index);
        }
        EnsureVisibleIce(level);
        EnsurePlayableOpening(level);
    }

    private void ApplyAdvancedMysteryRules(LevelData level)
    {
        int number = EstimateLevelNumber(level);
        int wanted = number < 230 ? 1 : 2;
        if (CampaignProgression.HasHiddenSections(level, number))
        {
            // The introduction must show a question mark before its route opens,
            // even when a changed picture makes all front sections accessible.
            if (number == CampaignProgression.HiddenSectionsLevel
                && !buckets.Any(b => b.HasSecondSection && !IsBucketAccessible(b, level)))
            {
                var section = buckets.FirstOrDefault(b => b.HasSecondSection);
                var rear = buckets.Where(b => !b.HasSecondSection && b.LinkedPartner == null
                    && b.KeyId == 0 && b.GateKeyId == 0 && b.SwapPartner == null && b.SlotCost == 1
                    && !BucketRouteAccess.IsAccessible(b.LayoutIndex, level, this, null))
                    .OrderByDescending(b => b.LayoutIndex).FirstOrDefault();
                if (section != null && rear != null) SwapBucketInto(section, rear.LayoutIndex);
            }
            // Prefer a blocked visible cup so the lesson is seen before its colors reveal.
            var sections = AllInitialBuckets().Where(b => b.HasSecondSection)
                .OrderBy(b => b.LayoutIndex < 0 ? 1 : IsBucketAccessible(b, level) ? 2 : 0)
                .ThenByDescending(b => b.LayoutIndex).Take(wanted);
            foreach (var bucket in sections) bucket.SetModifiers(bucket.FrostMoves, true);
        }
        if (CampaignProgression.HasHiddenLinked(level, number))
        {
            var pairs = buckets.Where(b => b.LinkedPartner != null && b.LayoutIndex < b.LinkedPartner.LayoutIndex)
                .OrderBy(b => IsBucketAccessible(b, level)).ThenByDescending(b => b.LayoutIndex).Take(wanted);
            foreach (var bucket in pairs)
            {
                bucket.SetModifiers(bucket.FrostMoves, true);
                bucket.LinkedPartner.SetModifiers(bucket.LinkedPartner.FrostMoves, true);
            }
        }
    }

    private void SettleInitialLayout(LevelData level)
    {
        if (level == null) { NeedsSettling = false; return; }
        dispensing = true;
        try
        {
            bool changed;
            do
            {
                changed = AdvanceBuckets(level);
                for (int i = 0; i < conveyorSlots.Count; i++)
                    changed |= TryDispenseFromSlot(conveyorSlots[i], level);
                changed |= RefillReserve(level);
                if (changed) buckets.Sort(CompareBucketsByLayout);
            }
            while (changed);
            NeedsSettling = false;
        }
        finally { dispensing = false; }
    }

    private void EnsureVisibleIce(LevelData level)
    {
        if (level.frostMoves <= 0 || buckets.Any(b => b.IsFrozen)) return;
        var candidate = buckets.Where(b => b.KeyId == 0 && b.GateKeyId == 0 && b.LinkedPartner == null
            && b.SwapPartner == null && !b.HasSecondSection).OrderByDescending(b => b.LayoutIndex).FirstOrDefault();
        if (candidate != null) candidate.SetModifiers(level.frostMoves, candidate.IsMystery);
    }

    private void EnsurePlayableOpening(LevelData level)
    {
        // Ice and denser key boards can freeze every front cup; thaw an opening so a move exists.
        if (level == null || buckets.Any(b => IsBucketAccessible(b, level))) return;
        foreach (var bucket in buckets.OrderBy(b => b.LayoutIndex))
        {
            if (!bucket.IsFrozen && (bucket.LinkedPartner == null || !bucket.LinkedPartner.IsFrozen)) continue;
            int frost = bucket.FrostMoves;
            bool mystery = bucket.IsMystery;
            bucket.SetModifiers(0, mystery);
            if (bucket.LinkedPartner != null && bucket.LinkedPartner.IsFrozen)
                bucket.LinkedPartner.SetModifiers(0, bucket.LinkedPartner.IsMystery);
            if (IsBucketAccessible(bucket, level))
            {
                if (level.frostMoves > 0 && !buckets.Any(b => b != bucket && b != bucket.LinkedPartner && b.IsFrozen))
                {
                    var rear = buckets.Where(b => b != bucket && b != bucket.LinkedPartner && b.KeyId == 0
                        && b.GateKeyId == 0 && b.LinkedPartner == null && b.SwapPartner == null && !b.HasSecondSection
                        && b.LayoutIndex / LevelData.BucketLayoutColumns >= 2)
                        .OrderByDescending(b => b.LayoutIndex).FirstOrDefault();
                    if (rear != null) rear.SetModifiers(level.frostMoves, rear.IsMystery);
                }
                return;
            }
            bucket.SetModifiers(frost, mystery);
        }
    }

    private void ShuffleColorGroup(IReadOnlyList<Bucket> group)
    {
        // Filled/recalled cups and cups already on the main belt keep their contents.
        var stock = new HashSet<Bucket>(AllInitialBuckets());
        var waiting = group.Where(b => stock.Contains(b) && b.State == BucketState.InInventory && b.CurrentFill == 0).ToArray();
        if (waiting.Length < 2) return;
        byte[] colors = waiting.Select(b => b.ColorIndex).ToArray();
        int[] capacities = waiting.Select(b => b.Capacity).ToArray();
        var order = Enumerable.Range(0, waiting.Length).ToList();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Shuffle(order);
            if (order.Where((source, target) => colors[source] != colors[target]).Any()) break;
        }
        if (colors.Distinct().Count() > 1 && !order.Where((source, target) => colors[source] != colors[target]).Any())
            order = Enumerable.Range(1, waiting.Length - 1).Concat(new[] { 0 }).ToList();
        for (int i = 0; i < waiting.Length; i++)
            waiting[i].SetColorAndCapacity(colors[order[i]], capacities[order[i]]);
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
            if (specials[i].LinkedPartner != null) continue;
            int target = front[UnityEngine.Random.Range(0, front.Count)];
            Bucket occupant = GetBucketAtLayoutIndex(target);
            if (occupant == null || occupant == specials[i] || occupant.LinkedPartner != null) continue;
            int old = specials[i].LayoutIndex;
            specials[i].SetLayoutIndex(target);
            occupant.SetLayoutIndex(old);
            front.Remove(target);
        }
    }

    private void LinkAdjacentPairs(LevelData level)
    {
        int wanted = CampaignProgression.LinkedPairCount(EstimateLevelNumber(level));
        if (level.loopInventory) wanted = Mathf.Min(wanted, 1);
        if (wanted <= 0) return;
        // Generation can relocate an unlinked special while trying a pair;
        // the full-layout check below keeps its key and exit routes clear.
        var seats = FindLinkedSeats(level, true);
        var candidates = buckets.Where(b => !b.HasSecondSection && b.SlotCost == 1
            && b.KeyId == 0 && b.GateKeyId == 0 && b.LinkedPartner == null).ToList();
        int[] originalPositions = buckets.Select(b => b.LayoutIndex).ToArray();
        var linked = new List<Bucket>();
        // Validate the entire set of pairs, so an early diagonal choice cannot
        // consume the only safe seats for the remaining required pairs.
        for (int attempt = 0; attempt < 64; attempt++)
        {
            Shuffle(seats);
            Shuffle(candidates);
            if (attempt >= 48)
                seats = seats.OrderBy(s => s.x / LevelData.BucketLayoutColumns != s.y / LevelData.BucketLayoutColumns).ToList();
            int made = 0;
            while (made < wanted)
            {
                Bucket first = null, second = null;
                for (int i = 0; i < candidates.Count && first == null; i++)
                {
                    if (candidates[i].LinkedPartner != null) continue;
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        if (candidates[j].LinkedPartner != null || candidates[i].ColorIndex == candidates[j].ColorIndex) continue;
                        first = candidates[i]; second = candidates[j]; break;
                    }
                }
                if (first == null) break;
                bool seated = false;
                foreach (var seat in seats)
                {
                    if (!CanHostLinkedSeat(level, seat.x, true) || !CanHostLinkedSeat(level, seat.y, true)) continue;
                    int[] positions = buckets.Select(b => b.LayoutIndex).ToArray();
                    SwapBucketInto(first, seat.x);
                    SwapBucketInto(second, seat.y);
                    first.LinkedPartner = second; second.LinkedPartner = first;
                    // Reject a blocked partial arrangement before it consumes seats
                    // needed by the remaining pairs, keys or feeder outlets.
                    if (!CanClearLinkedLayout(level))
                    {
                        first.LinkedPartner = second.LinkedPartner = null;
                        for (int i = 0; i < buckets.Count; i++) buckets[i].SetLayoutIndex(positions[i]);
                        continue;
                    }
                    linked.Add(first); linked.Add(second);
                    made++;
                    seated = true;
                    break;
                }
                if (!seated) break;
            }
            if (made == wanted && CanClearLinkedLayout(level))
            {
                foreach (var bucket in linked) bucket.SetModifiers(0, false);
                return;
            }
            foreach (var bucket in linked) bucket.LinkedPartner = null;
            linked.Clear();
            for (int i = 0; i < buckets.Count; i++) buckets[i].SetLayoutIndex(originalPositions[i]);
        }
    }

    // Check a private copy: diagonal pairs can otherwise block one another across
    // columns or trap a key. Ice is temporary and does not change the exit geometry.
    private bool CanClearLinkedLayout(LevelData level)
    {
        var trial = new BucketInventory { activeLevel = level };
        var copies = new Dictionary<Bucket, Bucket>();
        Bucket Copy(Bucket bucket)
        {
            if (!copies.TryGetValue(bucket, out var copy))
            {
                copy = new Bucket(bucket.ColorIndex, bucket.Capacity, bucket.LayoutIndex)
                    { KeyId = bucket.KeyId, GateKeyId = bucket.GateKeyId };
                copies.Add(bucket, copy);
            }
            return copy;
        }
        foreach (var bucket in buckets) trial.buckets.Add(Copy(bucket));
        foreach (var bucket in reserve) trial.reserve.Enqueue(Copy(bucket));
        foreach (var slot in conveyorSlots)
            trial.conveyorSlots.Add(new ConveyorSlotState(slot.LayoutIndex, slot.Direction, slot.ReservedBuckets.Select(Copy).ToList()));
        foreach (var entry in copies)
            if (entry.Key.LinkedPartner != null && copies.TryGetValue(entry.Key.LinkedPartner, out var partner))
                entry.Value.LinkedPartner = partner;
        trial.openGates.UnionWith(openGates);
        trial.reservedAccessPath.UnionWith(reservedAccessPath);
        trial.SettleInitialLayout(level);
        while (trial.CountRemaining() > 0)
        {
            var next = trial.buckets.OrderByDescending(b => b.KeyId).FirstOrDefault(b => trial.IsBucketAccessible(b, level));
            if (next == null) return false;
            trial.buckets.Remove(next);
            if (next.KeyId != 0) trial.openGates.Add(next.KeyId);
            if (next.LinkedPartner != null)
            {
                trial.buckets.Remove(next.LinkedPartner);
                if (next.LinkedPartner.KeyId != 0) trial.openGates.Add(next.LinkedPartner.KeyId);
            }
            trial.ApplySuccessfulMoveRules();
            trial.SettleInitialLayout(level);
        }
        return true;
    }

    private List<Vector2Int> CollectLinkedSeats(LevelData level) => FindLinkedSeats(level, false);

    private List<Vector2Int> FindLinkedSeats(LevelData level, bool allowSpecials)
    {
        var seats = new List<Vector2Int>();
        for (int row = 0; row < LevelData.BucketLayoutRows; row++)
        {
            for (int col = 0; col < LevelData.BucketLayoutColumns - 1; col++)
            {
                int left = row * LevelData.BucketLayoutColumns + col;
                if (!CanHostLinkedSeat(level, left, allowSpecials)) continue;
                for (int rightRow = 0; rightRow < LevelData.BucketLayoutRows; rightRow++)
                {
                    int right = rightRow * LevelData.BucketLayoutColumns + col + 1;
                    if (CanHostLinkedSeat(level, right, allowSpecials)) seats.Add(new Vector2Int(left, right));
                }
            }
        }
        return seats;
    }

    private void ReseatLinkedPair(LevelData level, Bucket first, Bucket second)
    {
        var seats = CollectLinkedSeats(level);
        Shuffle(seats);
        foreach (var seat in seats)
        {
            SwapBucketInto(first, seat.x);
            SwapBucketInto(second, seat.y);
            if (AreInNeighboringColumns(first, second)) return;
        }
    }

    private static bool AreInNeighboringColumns(Bucket a, Bucket b)
    {
        if (a == null || b == null || a.LayoutIndex < 0 || b.LayoutIndex < 0) return false;
        return Mathf.Abs(a.LayoutIndex % LevelData.BucketLayoutColumns
            - b.LayoutIndex % LevelData.BucketLayoutColumns) == 1;
    }

    private bool CanHostLinkedSeat(LevelData level, int index, bool allowSpecials = false)
    {
        if (index < 0 || index >= LevelData.BucketLayoutCellCount) return false;
        if (level.loopInventory && Array.IndexOf(InventoryLoopRoute.Cells, index) < 0) return false;
        if (reservedAccessPath.Contains(index)) return false;
        byte cell = level.bucketLayout[index];
        if (LevelData.IsBucketLayoutWall(cell) || LevelData.IsBucketLayoutConveyorSlot(cell)) return false;
        Bucket occupant = GetBucketAtLayoutIndex(index);
        return occupant == null || (occupant.LinkedPartner == null && (allowSpecials
            || occupant.KeyId == 0 && occupant.GateKeyId == 0 && !occupant.HasSecondSection && occupant.SlotCost == 1));
    }

    private void SwapBucketInto(Bucket bucket, int target)
    {
        if (bucket == null || bucket.LayoutIndex == target) return;
        Bucket occupant = GetBucketAtLayoutIndex(target);
        int old = bucket.LayoutIndex;
        bucket.SetLayoutIndex(target);
        if (occupant != null) occupant.SetLayoutIndex(old);
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
        if (EstimateLevelNumber(level) >= CampaignProgression.MultiSwapLevel)
        {
            PlaceSwapGroup(level);
            return;
        }
        bool preferDifferentRows = EstimateLevelNumber(level) >= CampaignProgression.SwapLevel;
        if (!TryAssignSwapPair(preferDifferentRows) && preferDifferentRows)
            TryAssignSwapPair(false);
    }

    private void PlaceSwapGroup(LevelData level)
    {
        int number = EstimateLevelNumber(level);
        var stock = CampaignProgression.AllBucketsSwapColors(number) ? AllInitialBuckets() : buckets;
        var candidates = stock.Where(b => !b.HasSecondSection && b.SlotCost == 1 && b.LinkedPartner == null
            && b.KeyId == 0 && b.GateKeyId == 0).ToList();
        Shuffle(candidates);
        int wanted = CampaignProgression.SwapBucketCount(number, candidates.Count);
        var selected = candidates.GroupBy(b => b.ColorIndex).Select(g => g.First()).Take(wanted).ToList();
        selected.AddRange(candidates.Where(b => !selected.Contains(b)).Take(wanted - selected.Count));
        if (selected.Count < 2) return;
        Bucket[] group = selected.ToArray();
        for (int i = 0; i < group.Length; i++)
        {
            group[i].ColorSwapGroup = group;
            group[i].SwapPartner = group[(i + 1) % group.Length];
            group[i].SetModifiers(0, false);
        }
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
            a.SwapPartner = b; b.SwapPartner = a; a.SetModifiers(0, false); b.SetModifiers(0, false);
            return true;
        }
        return false;
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

    private static List<ColorQuantizer.BucketQueueItem> OrderBucketsBySandSupply(
        LevelData level, List<ColorQuantizer.BucketQueueItem> queue, int grainsPerLevelCell)
    {
        var groups = new Dictionary<byte, Queue<ColorQuantizer.BucketQueueItem>>();
        foreach (var item in queue)
        {
            if (!groups.TryGetValue(item.colorIndex, out var group))
                groups[item.colorIndex] = group = new Queue<ColorQuantizer.BucketQueueItem>();
            group.Enqueue(item);
        }

        // Queue a cup when its full budget appears in a bottom-to-top sand scan.
        // Rare colors near the top must not occupy all five starting slots.
        var available = new int[256];
        var result = new List<ColorQuantizer.BucketQueueItem>(queue.Count);
        for (int y = level.gridHeight - 1; y >= 0; y--)
        for (int x = 0; x < level.gridWidth; x++)
        {
            byte color = level.cells[y * level.gridWidth + x];
            if (color == 0 || !groups.TryGetValue(color, out var group)) continue;
            available[color] += grainsPerLevelCell;
            while (group.Count > 0 && available[color] >= group.Peek().capacity)
            {
                var item = group.Dequeue();
                available[color] -= item.capacity;
                result.Add(item);
            }
        }
        // Preserve externally authored budgets if their grain scale differs.
        foreach (var item in queue)
            if (groups.TryGetValue(item.colorIndex, out var group) && group.Count > 0)
                while (group.Count > 0) result.Add(group.Dequeue());
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

        if (!hasStructuralLayout && !levelData.loopInventory)
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
        if (levelData.loopInventory && !levelData.campaignLevel)
        {
            int gap = slots.FindLastIndex(i => Array.IndexOf(InventoryLoopRoute.Cells, i) >= 0);
            if (gap >= 0) slots.RemoveAt(gap);
        }
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
