using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BucketInventoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform bucketContainer;
    [SerializeField] private BucketView bucketViewPrefab;
    [SerializeField] private GridLayoutGroup gridLayout;

    private readonly Dictionary<int, BucketView> activeViews = new Dictionary<int, BucketView>();
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
            gridLayout.cellSize = new Vector2(36f, 36f);
            gridLayout.spacing = new Vector2(4f, 4f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = LevelData.BucketLayoutColumns;
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

        levelData.EnsureBucketLayout();
        if (levelData.HasBucketLayout())
        {
            for (int layoutIndex = 0; layoutIndex < LevelData.BucketLayoutCellCount; layoutIndex++)
            {
                byte cell = levelData.bucketLayout[layoutIndex];
                if (LevelData.IsBucketLayoutWall(cell))
                {
                    CreateWallPlaceholder(layoutIndex);
                    continue;
                }

                Bucket bucket = inventory.GetBucketAtLayoutIndex(layoutIndex);
                if (bucket == null)
                {
                    CreatePlaceholder(layoutIndex);
                    continue;
                }

                CreateBucketView(bucket, layoutIndex);
            }

            return;
        }

        IReadOnlyList<Bucket> buckets = inventory.Buckets;
        for (int i = 0; i < LevelData.BucketLayoutCellCount; i++)
        {
            if (i >= buckets.Count)
            {
                CreatePlaceholder(i);
                continue;
            }

            CreateBucketView(buckets[i], i);
        }
    }

    private void CreateBucketView(Bucket bucket, int layoutIndex)
    {
        BucketView view = Instantiate(bucketViewPrefab, bucketContainer);
        Color32 color = levelData.GetPaletteColor(bucket.ColorIndex);
        view.Initialize(bucket, color, HandleBucketClicked);
        bool selectable = inventory.IsBucketAccessible(bucket, levelData);
        view.SetSelectable(selectable);
        activeViews[layoutIndex] = view;
    }

    private void CreateWallPlaceholder(int layoutIndex)
    {
        GameObject wallObject = new GameObject(
            "WallSlot_" + layoutIndex,
            typeof(RectTransform),
            typeof(Image));
        wallObject.transform.SetParent(bucketContainer, false);

        Image image = wallObject.GetComponent<Image>();
        image.color = new Color(0.35f, 0.35f, 0.38f, 0.95f);
        image.raycastTarget = false;
    }

    private void CreatePlaceholder(int layoutIndex)
    {
        GameObject placeholderObject = new GameObject(
            "BucketSlot_" + layoutIndex,
            typeof(RectTransform),
            typeof(Image));
        placeholderObject.transform.SetParent(bucketContainer, false);

        Image image = placeholderObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);
        image.raycastTarget = false;
    }

    private void HandleBucketClicked(Bucket bucket)
    {
        if (gameManager == null || conveyor == null || inventory == null || bucket == null)
        {
            return;
        }

        int layoutIndex = bucket.LayoutIndex;
        if (layoutIndex < 0)
        {
            for (int i = 0; i < inventory.Buckets.Count; i++)
            {
                if (inventory.Buckets[i] == bucket)
                {
                    layoutIndex = i;
                    break;
                }
            }
        }

        if (layoutIndex < 0)
        {
            return;
        }

        if (!inventory.IsBucketAccessible(bucket, levelData))
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
        foreach (KeyValuePair<int, BucketView> pair in activeViews)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        activeViews.Clear();

        if (bucketContainer == null)
        {
            return;
        }

        for (int i = bucketContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(bucketContainer.GetChild(i).gameObject);
        }
    }
}
