using System.Collections.Generic;
using System.Linq;
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
    private bool boardCreated;

    public void Bind(BucketInventory bucketInventory, ConveyorBelt conveyorBelt, LevelData level, GameManager manager)
    {
        if (inventory != bucketInventory || levelData != level) ClearViews();
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
        if (inventory == null || bucketContainer == null || bucketViewPrefab == null || levelData == null)
        {
            return;
        }

        if (!boardCreated) { CreateBoardCells(); boardCreated = true; }
        var previous = new Dictionary<Bucket, BucketView>();
        foreach (BucketView view in activeViews.Values)
            if (view != null && view.Bucket != null) previous[view.Bucket] = view;
        activeViews.Clear();
        List<Bucket> sortedBuckets = new List<Bucket>(inventory.Buckets);
        sortedBuckets.Sort((left, right) => left.LayoutIndex.CompareTo(right.LayoutIndex));

        for (int i = 0; i < sortedBuckets.Count; i++)
        {
            Bucket bucket = sortedBuckets[i];
            if (previous.TryGetValue(bucket, out BucketView view))
            {
                previous.Remove(bucket);
                activeViews[bucket.LayoutIndex] = view;
                PositionBucketView(view, bucket.LayoutIndex);
                view.RefreshAppearance(levelData.GetPaletteColor(bucket.ColorIndex));
            }
            else CreateBucketView(bucket, bucket.LayoutIndex);
        }
        foreach (BucketView old in previous.Values) Destroy(old.gameObject);
        foreach (ConveyorSlotView slot in activeConveyorViews.Values)
            if (slot != null) Destroy(slot.gameObject);
        activeConveyorViews.Clear();
        CreateConveyorSlotViews();
        RefreshLinkedPairLines();
        RefreshInteractionState();
    }

    private void RefreshLinkedPairLines()
    {
        Transform existing = bucketContainer != null ? bucketContainer.Find("LinkedPairLines") : null;
        if (existing != null) Destroy(existing.gameObject);
        if (inventory == null || levelData == null || bucketContainer == null) return;
        var drawn = new HashSet<Bucket>();
        GameObject root = new GameObject("LinkedPairLines", typeof(RectTransform));
        root.transform.SetParent(bucketContainer, false);
        root.transform.SetAsFirstSibling();
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        var ignore = root.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;
        foreach (Bucket bucket in inventory.Buckets)
        {
            if (bucket.LinkedPartner == null || drawn.Contains(bucket)) continue;
            Bucket partner = bucket.LinkedPartner;
            if (!inventory.Buckets.Contains(partner)) continue;
            drawn.Add(bucket); drawn.Add(partner);
            BucketView a = GetBucketView(bucket);
            BucketView b = GetBucketView(partner);
            if (a == null || b == null) continue;
            Vector2 from = a.GetComponent<RectTransform>().anchoredPosition
                + a.GetComponent<RectTransform>().sizeDelta * 0.5f * new Vector2(1, -1);
            Vector2 to = b.GetComponent<RectTransform>().anchoredPosition
                + b.GetComponent<RectTransform>().sizeDelta * 0.5f * new Vector2(1, -1);
            var lineObject = new GameObject("Link", typeof(RectTransform), typeof(LinkedBucketLinkGraphic));
            lineObject.transform.SetParent(root.transform, false);
            var line = lineObject.GetComponent<LinkedBucketLinkGraphic>();
            line.raycastTarget = false;
            RectTransform lineRect = line.rectTransform;
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = lineRect.offsetMax = Vector2.zero;
            Color colorA = SandDisplayColors.FromPalette(levelData.GetPaletteColor(bucket.ColorIndex));
            Color colorB = SandDisplayColors.FromPalette(levelData.GetPaletteColor(partner.ColorIndex));
            line.SetLink(from, to, colorA, colorB, 7f);
        }
    }

    private void CreateBoardCells()
    {
        if (levelData.loopInventory)
        {
            InventoryLoopRoute.Bind(levelData);
            var go = new GameObject("InventoryLoop", typeof(RectTransform), typeof(InventoryLoopGraphic), typeof(LayoutElement));
            go.transform.SetParent(bucketContainer, false); go.transform.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; go.GetComponent<InventoryLoopGraphic>().raycastTarget = false;
        }
        float size = MobilePortraitLayout.BucketCellSize;
        float spacing = MobilePortraitLayout.BucketSpacing;
        for (int index = 0; index < LevelData.BucketLayoutCellCount; index++)
        {
            bool wall = LevelData.IsBucketLayoutWall(levelData.bucketLayout[index]);
            if (!wall) continue;
            var obj = new GameObject(wall ? "Wall_" + index : "Slot_" + index, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(bucketContainer, false);
            obj.GetComponent<LayoutElement>().ignoreLayout = true;
            Image surface = obj.GetComponent<Image>();
            surface.sprite = HintUiSprites.RoundedPanel;
            surface.type = Image.Type.Sliced;
            surface.raycastTarget = false;
            surface.color = wall ? new Color(0.3f, 0.4f, 0.45f) : new Color(0.24f, 0.39f, 0.43f, 0.09f);
            RectTransform rect = surface.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(index % 5 * (size + spacing), -index / 5 * (size + spacing));
            if (!wall) continue;
            for (int row = 1; row <= 2; row++)
            {
                var seam = new GameObject("BrickSeam", typeof(RectTransform), typeof(Image));
                seam.transform.SetParent(obj.transform, false);
                Image line = seam.GetComponent<Image>();
                line.raycastTarget = false;
                line.color = new Color(0.12f, 0.22f, 0.28f, 0.55f);
                line.rectTransform.anchorMin = new Vector2(0.08f, row / 3f);
                line.rectTransform.anchorMax = new Vector2(0.92f, row / 3f);
                line.rectTransform.sizeDelta = new Vector2(0, 3);
            }
        }
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
        view.transform.SetAsFirstSibling();
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
        Vector2 previousPosition = rect.anchoredPosition;
        bool animateMove = levelData != null && levelData.loopInventory && view.HasLayout;
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
        if (animateMove && (previousPosition - rect.anchoredPosition).sqrMagnitude > 1) view.AnimateLayoutFrom(previousPosition);
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

        gameManager.TryPlaceBucket(bucket);
    }

    private void ClearViews()
    {
        boardCreated = false;
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


