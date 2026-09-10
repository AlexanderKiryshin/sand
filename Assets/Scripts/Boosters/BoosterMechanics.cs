using System.Collections.Generic;
using UnityEngine;

public static class BoosterMechanics
{
    public static bool TryForcePlaceBucket(GameManager gameManager, Bucket bucket)
    {
        if (gameManager == null || bucket == null || gameManager.State != GameState.Playing)
        {
            return false;
        }

        BucketInventory inventory = gameManager.Inventory;
        ConveyorBelt conveyor = Object.FindObjectOfType<ConveyorBelt>();
        LevelLoader loader = Object.FindObjectOfType<LevelLoader>();
        if (inventory == null || conveyor == null)
        {
            return false;
        }

        if (!inventory.TryTakeBucket(bucket, conveyor, loader != null ? loader.CurrentLevel : null, true))
        {
            return false;
        }

        FallingSandSimulator simulator = Object.FindObjectOfType<FallingSandSimulator>();
        if (simulator != null)
        {
            simulator.Wake();
        }

        return true;
    }

    public static bool TryFillConveyorBucket(Bucket bucket, FallingSandSimulator simulator, ConveyorBelt conveyor)
    {
        if (bucket == null || bucket.State != BucketState.OnConveyor || bucket.IsFull || conveyor == null)
        {
            return false;
        }

        int needed = bucket.RemainingCapacity;
        if (needed <= 0)
        {
            return false;
        }

        if (simulator != null && simulator.Grid != null)
        {
            RemoveSandOfColor(simulator.Grid, bucket.FirstColor, bucket.RemainingForColor(bucket.FirstColor));
            if (bucket.HasSecondSection) RemoveSandOfColor(simulator.Grid, bucket.SecondColor, bucket.RemainingForColor(bucket.SecondColor));
            simulator.NotifyExternalGridEdit();
        }

        bucket.ForceFillToCapacity();
        conveyor.MarkBucketFilledByBooster(bucket);
        return true;
    }

    public static bool TryShuffleInventoryColors(BucketInventory inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        List<Bucket> targets = new List<Bucket>();
        for (int i = 0; i < inventory.Buckets.Count; i++)
        {
            Bucket bucket = inventory.Buckets[i];
            if (bucket != null && bucket.State == BucketState.InInventory)
            {
                targets.Add(bucket);
            }
        }

        for (int i = 0; i < inventory.ConveyorSlots.Count; i++)
        {
            ConveyorSlotState slot = inventory.ConveyorSlots[i];
            if (slot == null || slot.ReservedBuckets == null)
            {
                continue;
            }

            for (int j = 0; j < slot.ReservedBuckets.Count; j++)
            {
                Bucket reserved = slot.ReservedBuckets[j];
                if (reserved != null && reserved.State == BucketState.InInventory)
                {
                    targets.Add(reserved);
                }
            }
        }

        targets.AddRange(inventory.ReservedBuckets);
        targets.RemoveAll(b => b.HasSecondSection || b.SlotCost > 1 || b.LinkedPartner != null || b.SwapPartner != null || b.KeyId != 0);
        if (targets.Count < 2)
        {
            return false;
        }

        List<byte> colors = new List<byte>(targets.Count);
        List<int> capacities = new List<int>(targets.Count);
        List<int> fills = new List<int>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            colors.Add(targets[i].ColorIndex);
            capacities.Add(targets[i].Capacity);
            fills.Add(targets[i].CurrentFill);
        }

        for (int i = colors.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            byte tempColor = colors[i];
            colors[i] = colors[swap];
            colors[swap] = tempColor;

            int tempCap = capacities[i];
            capacities[i] = capacities[swap];
            capacities[swap] = tempCap;
            int tempFill = fills[i];
            fills[i] = fills[swap];
            fills[swap] = tempFill;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].SetContents(colors[i], capacities[i], fills[i]);
        }

        inventory.NotifyInventoryChangedPublic();
        return true;
    }

    private static void RemoveSandOfColor(SandGrid grid, byte colorIndex, int amount)
    {
        if (grid == null || amount <= 0)
        {
            return;
        }

        int removed = 0;
        for (int y = 0; y < grid.Height && removed < amount; y++)
        {
            for (int x = 0; x < grid.Width && removed < amount; x++)
            {
                if (grid.GetCell(x, y) != colorIndex)
                {
                    continue;
                }

                grid.SetCell(x, y, 0);
                removed++;
            }
        }
    }
}
