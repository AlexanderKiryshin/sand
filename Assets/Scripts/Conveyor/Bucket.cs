using UnityEngine;

public enum BucketState
{
    InInventory,
    OnConveyor,
    Full,
    Removed
}

public class Bucket
{
    private byte firstColor;
    public byte ColorIndex => HasSecondSection && CurrentFill >= FirstSectionCapacity ? SecondColor : firstColor;
    public byte FirstColor => firstColor;
    public byte SecondColor { get; private set; }
    public int FirstSectionCapacity { get; private set; }
    public bool HasSecondSection => SecondColor != 0;
    public int SlotCost { get; private set; } = 1;
    public int KeyId { get; set; }
    public int GateKeyId { get; set; }
    public Bucket LinkedPartner { get; set; }
    public Bucket SwapPartner { get; set; }
    public System.Collections.Generic.IReadOnlyList<Bucket> ColorSwapGroup { get; set; }
    public int RemainingForColor(byte color)
    {
        if (!HasSecondSection) return color == firstColor ? RemainingCapacity : 0;
        int first = Mathf.Max(0, FirstSectionCapacity - CurrentFill);
        int second = Capacity - Mathf.Max(FirstSectionCapacity, CurrentFill);
        return (color == firstColor ? first : 0) + (color == SecondColor ? second : 0);
    }
    public void AddSection(byte color, int capacity)
    { FirstSectionCapacity = Capacity; SecondColor = color; Capacity += capacity; }
    public void MakeLarge(int addedCapacity) { Capacity += addedCapacity; SlotCost = 2; }
    public int Capacity { get; private set; }
    public int LayoutIndex { get; private set; }
    public int CurrentFill { get; private set; }
    public BucketState State { get; private set; }
    public int FrostMoves { get; private set; }
    public bool IsFrozen { get { return FrostMoves > 0; } }
    public bool IsMystery { get; private set; }

    public void SetModifiers(int frostMoves, bool mystery)
    {
        FrostMoves = Mathf.Max(0, frostMoves);
        IsMystery = mystery;
    }

    public void ThawOneMove() { FrostMoves = Mathf.Max(0, FrostMoves - 1); }
    public void Reveal() { IsMystery = false; }

    public bool IsFull
    {
        get { return CurrentFill >= Capacity; }
    }

    public float FillNormalized
    {
        get { return Capacity > 0 ? (float)CurrentFill / Capacity : 0f; }
    }

    public int RemainingCapacity
    {
        get { return Capacity > CurrentFill ? Capacity - CurrentFill : 0; }
    }

    public Bucket(byte colorIndex, int capacity, int layoutIndex = -1)
    {
        firstColor = colorIndex;
        Capacity = capacity;
        LayoutIndex = layoutIndex;
        CurrentFill = 0;
        State = BucketState.InInventory;
    }

    public bool TryAddGrain(byte colorIndex)
    {
        if (State != BucketState.OnConveyor || IsFull || colorIndex != ColorIndex)
        {
            return false;
        }

        CurrentFill++;
        if (IsFull)
        {
            State = BucketState.Full;
        }

        return true;
    }

    public void SetLayoutIndex(int layoutIndex)
    {
        LayoutIndex = layoutIndex;
    }

    public void MoveToConveyor()
    {
        State = BucketState.OnConveyor;
    }

    public void ReturnToInventory()
    {
        if (State == BucketState.Removed)
        {
            return;
        }

        State = BucketState.InInventory;
    }

    public void MarkRemoved()
    {
        State = BucketState.Removed;
    }

    public void ForceFillToCapacity()
    {
        if (State != BucketState.OnConveyor && State != BucketState.Full)
        {
            return;
        }

        CurrentFill = Capacity;
        State = BucketState.Full;
    }

    public void SetColorAndCapacity(byte colorIndex, int capacity)
    {
        firstColor = colorIndex;
        Capacity = Mathf.Max(1, capacity);
        if (CurrentFill > Capacity)
        {
            CurrentFill = Capacity;
        }
    }

    public void SetSectionCapacities(int firstCapacity, int secondCapacity)
    {
        FirstSectionCapacity = Mathf.Max(1, firstCapacity);
        Capacity = FirstSectionCapacity + Mathf.Max(1, secondCapacity);
    }

    public void SetContents(byte colorIndex, int capacity, int currentFill)
    {
        firstColor = colorIndex;
        Capacity = Mathf.Max(1, capacity);
        CurrentFill = Mathf.Clamp(currentFill, 0, Capacity);
    }
}
