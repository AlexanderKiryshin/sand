using System.Collections.Generic;

public class ConveyorSlotState
{
    public int LayoutIndex { get; private set; }
    public ConveyorDirection Direction { get; private set; }
    public List<Bucket> ReservedBuckets { get; private set; }

    public int RemainingCount
    {
        get { return ReservedBuckets != null ? ReservedBuckets.Count : 0; }
    }

    public ConveyorSlotState(int layoutIndex, ConveyorDirection direction, List<Bucket> reservedBuckets)
    {
        LayoutIndex = layoutIndex;
        Direction = direction;
        ReservedBuckets = reservedBuckets ?? new List<Bucket>();
    }
}
