using System;
using UnityEngine;

public class WinConditionChecker : MonoBehaviour
{
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private ConveyorBelt conveyorBelt;
    [SerializeField] private BucketInventory bucketInventory;

    public event Action OnLevelComplete;

    public void Initialize(LevelLoader loader, ConveyorBelt conveyor, BucketInventory inventory)
    {
        levelLoader = loader;
        conveyorBelt = conveyor;
        bucketInventory = inventory;
    }

    public void CheckConditions()
    {
        if (levelLoader == null || levelLoader.CurrentGrid == null)
        {
            return;
        }

        bool sandCleared = levelLoader.CurrentGrid.CountRemaining() == 0;
        if (sandCleared && conveyorBelt != null)
        {
            conveyorBelt.RemoveUnfilledBuckets();
        }

        bool inventoryEmpty = bucketInventory == null || bucketInventory.CountRemaining() == 0;
        bool conveyorEmpty = conveyorBelt == null || conveyorBelt.Entries.Count == 0;

        if (sandCleared && inventoryEmpty && conveyorEmpty)
        {
            if (OnLevelComplete != null)
            {
                OnLevelComplete();
            }
        }
    }
}
