using System;
using System.Linq;
using UnityEngine;

public static class ConveyorPlacementChecks
{
    public static void Run()
    {
        var root = new GameObject("PlacementChecks");
        try
        {
            var belt = root.AddComponent<ConveyorBelt>();
            ConveyorBelt.ConfigureGeometry(680, 100);
            belt.Configure(100);
            int visible = belt.AvailableSpawnSlots;
            Require(visible > 0 && visible < belt.Capacity, "Visible deck has fewer gaps than total capacity");
            for (int i = 0; i < visible; i++)
            {
                Require(belt.TryPlaceBucket(new Bucket(1, 100)), "Visible gap accepts new bucket");
                Require(Mathf.Abs(belt.Entries.Last().PositionX) <= ConveyorBelt.VisibleSpawnLimit, "Prefer exposed gaps");
            }
            var hidden = new Bucket(2, 100);
            Require(belt.CanAcceptBucket() && belt.TryPlaceBucket(hidden), "Hidden entry uses remaining capacity");
            var probe = belt.Entries.Last();
            Require(probe.PositionX >= ConveyorBelt.EntryX, "Overflow starts under right hood");
            while (belt.FreeSlots > 0) Require(belt.TryPlaceBucket(new Bucket(1, 100)), "Fill available capacity");
            var rejected = new Bucket(2, 100);
            Require(!belt.TryPlaceBucket(rejected) && rejected.State == BucketState.InInventory, "Full belt rejects without mutation");
            bool emerged = false, wrapped = false;
            for (int tick = 0; tick < 500; tick++)
            {
                float before = probe.PositionX;
                belt.UpdateBelt(.04f);
                emerged |= Mathf.Abs(probe.PositionX) < ConveyorBelt.VisibleSpawnLimit;
                wrapped |= probe.PositionX > before;
                AssertSpacing(belt);
                Require(belt.OccupiedSlots == belt.Capacity && belt.FreeSlots == 0, "Occupied count includes hidden buckets");
            }
            Require(emerged && wrapped, "Hidden entry emerges and completes a lap");
            Require(belt.TryRecallBucket(hidden) && belt.FreeSlots == 1, "Recall restores capacity");
            belt.ResetConveyor();
            var large = new Bucket(1, 100); large.MakeLarge(100);
            Require(belt.TryPlaceBucket(large) && belt.OccupiedSlots == 2 && belt.FreeSlots == 3, "Large bucket costs two slots");
            belt.TryPlaceBucket(new Bucket(2, 100)); belt.TryPlaceBucket(new Bucket(3, 100));
            var a = new Bucket(1, 2); var b = new Bucket(2, 2); a.LinkedPartner = b; b.LinkedPartner = a;
            Require(!belt.TryPlaceBucket(a) && a.State == BucketState.InInventory && b.State == BucketState.InInventory, "Pair placement is atomic when only one slot remains");
            AssertSpacing(belt);
            belt.ResetConveyor();
            Require(belt.TryPlaceBucket(a) && belt.OccupiedSlots == 2, "Linked pair dispatches together");
            a.ForceFillToCapacity(); belt.MarkBucketFilledByBooster(a);
            for (int i = 0; i < 200; i++) belt.UpdateBelt(.04f);
            Require(belt.Entries.Count == 2 && belt.OccupiedSlots == 2, "Full first partner keeps its slot");
            b.ForceFillToCapacity(); belt.MarkBucketFilledByBooster(b); belt.UpdateBelt(.01f);
            Require(belt.Entries.Count == 0 && belt.FilledBuckets == 2, "Both full partners leave together");
            CheckRewards(belt);
            Debug.Log("CONVEYOR_PLACEMENT_PASS: exposed preference, hidden overflow, bounded occupied slots, spacing, large costs, atomic linked placement/removal and reward lifecycle.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckRewards(ConveyorBelt belt)
    {
        belt.ResetConveyor();
        int request = belt.BeginSlotReward(out int generation);
        Require(request > 0 && belt.BeginSlotReward(out _) == 0, "One pending ad request");
        belt.CompleteSlotReward(request, generation, false);
        Require(belt.Capacity == 5, "Cancellation grants no slot");
        request = belt.BeginSlotReward(out generation);
        belt.CompleteSlotReward(request, generation, true);
        belt.CompleteSlotReward(request, generation, true);
        Require(belt.Capacity == 6, "Success grants exactly once");
        request = belt.BeginSlotReward(out generation);
        belt.ResetConveyor();
        belt.CompleteSlotReward(request, generation, true);
        Require(belt.Capacity == 5, "Previous level callback cannot grant a slot");
        for (int i = 0; i < 2; i++) { request = belt.BeginSlotReward(out generation); belt.CompleteSlotReward(request, generation, true); }
        Require(belt.Capacity == 7 && belt.BeginSlotReward(out _) == 0, "Reward capacity is bounded");
        int rewards = 0, failures = 0;
        var success = new RewardedAttempt(() => rewards++, () => failures++);
        success.Complete(true); success.Complete(false); success.Complete(true);
        var cancelled = new RewardedAttempt(() => rewards++, () => failures++);
        cancelled.Complete(false); cancelled.Complete(true);
        Require(rewards == 1 && failures == 1, "SDK callback adapter settles each attempt once");
    }

    private static void AssertSpacing(ConveyorBelt belt)
    {
        foreach (var entry in belt.Entries)
            foreach (var other in belt.Entries)
                if (entry != other) Require(Mathf.Abs(entry.PositionX - other.PositionX) >= ConveyorBelt.BucketSpacing * (entry.Bucket.SlotCost + other.Bucket.SlotCost) * .5f - .1f, "No overlap during placement or wrap");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
