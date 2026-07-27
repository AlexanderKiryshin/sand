using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BucketInventoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform bucketContainer;
    [SerializeField] private BucketView bucketViewPrefab;
    [SerializeField] private GridLayoutGroup gridLayout;

    public BucketView BucketViewPrefab { get { return bucketViewPrefab; } }

    private readonly Dictionary<int, BucketView> activeViews = new Dictionary<int, BucketView>();
    private readonly Dictionary<int, ConveyorSlotView> activeConveyorViews = new Dictionary<int, ConveyorSlotView>();
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

        ApplyMobileLayout();
        Refresh();
    }

    public void ApplyMobileLayout()
    {
        if (gridLayout != null)
        {
            float cellSize = MobilePortraitLayout.BucketCellSize;
            float spacing = MobilePortraitLayout.BucketSpacing;
            gridLayout.cellSize = new Vector2(cellSize, cellSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = LevelData.BucketLayoutColumns;
        }
    }

    public void Refresh()
    {
        ClearViews();

        if (inventory == null || bucketContainer == null || bucketViewPrefab == null || levelData == null)
        {
            return;
        }

        List<Bucket> sortedBuckets = new List<Bucket>(inventory.Buckets);
        sortedBuckets.Sort((left, right) => left.LayoutIndex.CompareTo(right.LayoutIndex));

        for (int i = 0; i < sortedBuckets.Count; i++)
        {
            Bucket bucket = sortedBuckets[i];
            CreateBucketView(bucket, bucket.LayoutIndex);
        }

        CreateConveyorSlotViews();
    }

    /// <summary>
    /// Updates clickability / magnet pulse without rebuilding views (avoids inventory panel jump).
    /// </summary>
    public void RefreshInteractionState()
    {
        if (inventory == null || levelData == null)
        {
            return;
        }

        bool magnetMode = BoosterService.instance != null
            && BoosterService.instance.ActiveTargetMode == BoosterType.Magnet;

        foreach (KeyValuePair<int, BucketView> pair in activeViews)
        {
            BucketView view = pair.Value;
            if (view == null || view.Bucket == null)
            {
                continue;
            }

            Bucket bucket = view.Bucket;
            bool accessible = inventory.IsBucketAccessible(bucket, levelData);
            bool allowedByTutorial = gameManager == null || gameManager.IsBucketInteractionAllowed(bucket) || magnetMode;
            bool canClick = (magnetMode || accessible) && allowedByTutorial;
            bool magnetTapHint = magnetMode && !accessible;
            view.ConfigureInteraction(accessible, canClick, magnetTapHint);
        }
    }

    private void CreateConveyorSlotViews()
    {
        if (inventory == null || inventory.ConveyorSlots == null)
        {
            return;
        }

        for (int i = 0; i < inventory.ConveyorSlots.Count; i++)
        {
            ConveyorSlotState slot = inventory.ConveyorSlots[i];
            ConveyorSlotView view = CreateConveyorSlotView(slot);
            if (view != null)
            {
                activeConveyorViews[slot.LayoutIndex] = view;
            }
        }
    }

    private ConveyorSlotView CreateConveyorSlotView(ConveyorSlotState slot)
    {
        GameObject slotObject = new GameObject("ConveyorSlot", typeof(RectTransform), typeof(ConveyorSlotView));
        slotObject.transform.SetParent(bucketContainer, false);
        ConveyorSlotView view = slotObject.GetComponent<ConveyorSlotView>();
        view.Initialize(slot);
        PositionConveyorSlotView(view, slot.LayoutIndex);
        return view;
    }

    private void PositionConveyorSlotView(ConveyorSlotView view, int layoutIndex)
    {
        Vector2 cellSize = gridLayout != null ? gridLayout.cellSize : new Vector2(MobilePortraitLayout.BucketCellSize, MobilePortraitLayout.BucketCellSize);
        Vector2 spacing = gridLayout != null ? gridLayout.spacing : new Vector2(MobilePortraitLayout.BucketSpacing, MobilePortraitLayout.BucketSpacing);
        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;

        RectTransform rect = view.GetComponent<RectTransform>();
        LayoutElement layoutElement = view.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = view.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = true;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = cellSize;
        rect.anchoredPosition = new Vector2(
            x * (cellSize.x + spacing.x),
            -y * (cellSize.y + spacing.y));
    }

    public Bucket GetFirstAccessibleBucket()
    {
        if (inventory == null || levelData == null)
        {
            return null;
        }

        Bucket best = null;
        for (int i = 0; i < inventory.Buckets.Count; i++)
        {
            Bucket candidate = inventory.Buckets[i];
            if (!inventory.IsBucketAccessible(candidate, levelData))
            {
                continue;
            }

            if (best == null || candidate.LayoutIndex < best.LayoutIndex)
            {
                best = candidate;
            }
        }

        return best;
    }

    public void SetTutorialFocus(Bucket bucket)
    {
    }

    public void ClearTutorialFocus()
    {
    }

    public bool TryGetBucketCanvasPosition(Bucket bucket, out Vector2 canvasPosition)
    {
        float bucketSize;
        return TryGetBucketCanvasPosition(bucket, out canvasPosition, out bucketSize);
    }

    public bool TryGetBucketCanvasPosition(Bucket bucket, out Vector2 canvasPosition, out float bucketSize)
    {
        canvasPosition = Vector2.zero;
        bucketSize = MobilePortraitLayout.BucketCellSize;
        BucketView view = GetBucketView(bucket);
        if (view == null)
        {
            return false;
        }

        canvasPosition = view.GetCenterCanvasPosition();
        bucketSize = view.GetCanvasSize();
        return true;
    }

    public void SetTutorialBounce(Bucket bucket, float bounceOffset)
    {
        // Legacy no-op: bucket bounce moved layout and is replaced by overlay highlights.
    }

    public void ClearTutorialBounce()
    {
        ClearTutorialFocus();
    }

    private void CreateBucketView(Bucket bucket, int layoutIndex)
    {
        BucketView view = Instantiate(bucketViewPrefab, bucketContainer);
        Color32 color = levelData.GetPaletteColor(bucket.ColorIndex);
        view.Initialize(bucket, color, HandleBucketClicked);

        activeViews[layoutIndex] = view;
        PositionBucketView(view, layoutIndex);

        bool magnetMode = BoosterService.instance != null
            && BoosterService.instance.ActiveTargetMode == BoosterType.Magnet;
        bool accessible = inventory.IsBucketAccessible(bucket, levelData);
        bool allowedByTutorial = gameManager == null || gameManager.IsBucketInteractionAllowed(bucket) || magnetMode;
        bool canClick = (magnetMode || accessible) && allowedByTutorial;
        bool magnetTapHint = magnetMode && !accessible;
        // Keep locked visual when magnet targets closed buckets — only pulse a tap hint.
        view.ConfigureInteraction(accessible, canClick, magnetTapHint);
    }

    private void PositionBucketView(BucketView view, int layoutIndex)
    {
        Vector2 cellSize = gridLayout != null ? gridLayout.cellSize : new Vector2(MobilePortraitLayout.BucketCellSize, MobilePortraitLayout.BucketCellSize);
        Vector2 spacing = gridLayout != null ? gridLayout.spacing : new Vector2(MobilePortraitLayout.BucketSpacing, MobilePortraitLayout.BucketSpacing);
        int x = layoutIndex % LevelData.BucketLayoutColumns;
        int y = layoutIndex / LevelData.BucketLayoutColumns;

        RectTransform rect = view.GetComponent<RectTransform>();
        LayoutElement layoutElement = view.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = view.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = true;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = cellSize;
        rect.anchoredPosition = new Vector2(
            x * (cellSize.x + spacing.x),
            -y * (cellSize.y + spacing.y));
        view.CaptureLayoutPosition();
    }

    private void HandleBucketClicked(Bucket bucket)
    {
        if (gameManager == null || conveyor == null || inventory == null || bucket == null)
        {
            return;
        }

        if (SandEconomyHud.instance != null && SandEconomyHud.instance.TryHandleBucketClick(bucket))
        {
            return;
        }

        if (!gameManager.IsBucketInteractionAllowed(bucket))
        {
            return;
        }

        if (bucket.LayoutIndex < 0 || !inventory.IsBucketAccessible(bucket, levelData))
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
        activeConveyorViews.Clear();

        if (bucketContainer == null)
        {
            return;
        }

        for (int i = bucketContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(bucketContainer.GetChild(i).gameObject);
        }
    }

    private BucketView GetBucketView(Bucket bucket)
    {
        if (bucket == null)
        {
            return null;
        }

        BucketView view;
        if (activeViews.TryGetValue(bucket.LayoutIndex, out view) && view != null && view.Bucket == bucket)
        {
            return view;
        }

        foreach (KeyValuePair<int, BucketView> pair in activeViews)
        {
            if (pair.Value != null && pair.Value.Bucket == bucket)
            {
                return pair.Value;
            }
        }

        return null;
    }
}


