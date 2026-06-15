using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BucketInventoryUI : MonoBehaviour
{
    private const int VisibleRows = 2;
    private const int VisibleColumns = 2;

    [SerializeField] private RectTransform bucketContainer;
    [SerializeField] private BucketView bucketViewPrefab;
    [SerializeField] private GridLayoutGroup gridLayout;

    private readonly List<BucketView> activeViews = new List<BucketView>();
    private BucketInventory inventory;
    private ConveyorBelt conveyor;
    private LevelData levelData;
    private GameManager gameManager;

    public void Bind(BucketInventory bucketInventory, ConveyorBelt conveyorBelt, LevelData level, GameManager manager)
    {
        inventory = bucketInventory;
        conveyor = conveyorBelt;
        levelData = level;
        gameManager = manager;

        if (gridLayout != null)
        {
            gridLayout.cellSize = new Vector2(44f, 44f);
            gridLayout.spacing = new Vector2(8f, 8f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = VisibleColumns;
        }

        Refresh();
    }

    public void Refresh()
    {
        ClearViews();

        if (inventory == null || bucketContainer == null || bucketViewPrefab == null || levelData == null)
        {
            return;
        }

        IReadOnlyList<Bucket> buckets = inventory.Buckets;
        int showCount = Mathf.Min(VisibleRows * VisibleColumns, buckets.Count);

        for (int i = 0; i < showCount; i++)
        {
            Bucket bucket = buckets[i];
            BucketView view = Instantiate(bucketViewPrefab, bucketContainer);
            Color32 color = levelData.GetPaletteColor(bucket.ColorIndex);
            view.Initialize(bucket, color, HandleBucketClicked);
            int row = i / VisibleColumns;
            view.SetSelectable(row == 0);
            activeViews.Add(view);
        }
    }

    private void HandleBucketClicked(Bucket bucket)
    {
        if (gameManager == null || conveyor == null || inventory == null)
        {
            return;
        }

        int index = -1;
        for (int i = 0; i < inventory.Buckets.Count; i++)
        {
            if (inventory.Buckets[i] == bucket)
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index >= VisibleColumns)
        {
            return;
        }

        if (!conveyor.CanAcceptBucket())
        {
            return;
        }

        GameAudio audio = FindObjectOfType<GameAudio>();
        if (audio != null)
        {
            audio.PlayTap();
        }

        gameManager.TryPlaceBucket(bucket);
    }

    private void ClearViews()
    {
        for (int i = 0; i < activeViews.Count; i++)
        {
            if (activeViews[i] != null)
            {
                Destroy(activeViews[i].gameObject);
            }
        }

        activeViews.Clear();
    }
}
