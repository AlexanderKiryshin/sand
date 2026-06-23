public enum BucketState
{
    InInventory,
    OnConveyor,
    Full,
    Removed
}

public class Bucket
{
    public byte ColorIndex { get; private set; }
    public int Capacity { get; private set; }
    public int LayoutIndex { get; private set; }
    public int CurrentFill { get; private set; }
    public BucketState State { get; private set; }

    public bool IsFull
    {
        get { return CurrentFill >= Capacity; }
    }

    public float FillNormalized
    {
        get { return Capacity > 0 ? (float)CurrentFill / Capacity : 0f; }
    }

    public Bucket(byte colorIndex, int capacity, int layoutIndex = -1)
    {
        ColorIndex = colorIndex;
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
}
