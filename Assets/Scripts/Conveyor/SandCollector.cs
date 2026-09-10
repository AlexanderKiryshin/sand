using System;
using UnityEngine;

public class SandCollector : MonoBehaviour
{
    [SerializeField] private ConveyorBelt conveyorBelt;

    public event Action<byte, int> OnGrainCollected;
    public event Action<byte, int, Bucket> OnBucketGrainCollected;

    public void Initialize(ConveyorBelt belt)
    {
        conveyorBelt = belt;
    }

    public bool CanCollectAt(int gridX, int gridWidth, byte colorIndex)
    {
        if (conveyorBelt == null)
        {
            return false;
        }

        return conveyorBelt.CanCollectAt(gridX, gridWidth, colorIndex);
    }

    public bool HasBucketAlignedAt(int gridX, int gridWidth)
    {
        if (conveyorBelt == null)
        {
            return false;
        }

        return conveyorBelt.HasBucketAlignedAt(gridX, gridWidth);
    }

    public bool TryCollectGrain(byte colorIndex, int simGridX, int levelGridWidth, int subGrainsPerAxis)
    {
        if (conveyorBelt == null)
        {
            return false;
        }

        int levelX = simGridX / Mathf.Max(1, subGrainsPerAxis);
        Bucket target = conveyorBelt.FindBucketForColumn(levelX, levelGridWidth, colorIndex)?.Bucket;
        if (!conveyorBelt.TryCollectGrain(colorIndex, levelX, levelGridWidth))
        {
            return false;
        }

        if (OnGrainCollected != null)
        {
            OnGrainCollected(colorIndex, simGridX);
        }
        OnBucketGrainCollected?.Invoke(colorIndex, simGridX, target);

        return true;
    }
}
