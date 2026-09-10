using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ConveyorBeltView : MonoBehaviour
{
    private const float PlateGap = 10f;
    private const float PerspectivePitch = 32f;
    private const float PerspectiveYaw = 0f;
    private const float DefaultCoverInset = 52f;
    private const float RailThickness = 14f;

    [SerializeField] private BucketView bucketAppearanceSource;
    [SerializeField] private Image leftCover;
    [SerializeField] private Image rightCover;

    private RectTransform beltRect;
    private RectTransform beltViewport;
    private RectTransform perspectiveRoot;
    private RectTransform plateTrack;
    private RectTransform bucketContainer;
    private ConveyorSurfaceGraphic surfaceGraphic;
    private ConveyorBelt conveyor;
    private LevelLoader levelLoader;
    private float coverInset = DefaultCoverInset;
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly Dictionary<Bucket, BucketVisual> bucketVisuals = new Dictionary<Bucket, BucketVisual>();
    private readonly List<BucketCompletionAnimation> completingBuckets = new List<BucketCompletionAnimation>();

    private class BucketVisual
    {
        public RectTransform rect;
        public Image bucketImage;
        public TextMeshProUGUI capacityLabel;
        public CanvasGroup visibility;
        public int lastRemainingCapacity = -1;
    }

    private void Awake()
    {
        beltRect = GetComponent<RectTransform>();
        conveyor = FindObjectOfType<ConveyorBelt>();
        levelLoader = FindObjectOfType<LevelLoader>();
        ResolveBucketAppearanceSource();

        if (conveyor != null)
        {
            conveyor.OnConveyorChanged += SyncBucketVisuals;
            conveyor.OnBucketFilled += AnimateFilledBucket;
        }

        BuildBeltVisuals();
    }

    private void OnDestroy()
    {
        if (conveyor != null)
        {
            conveyor.OnConveyorChanged -= SyncBucketVisuals;
            conveyor.OnBucketFilled -= AnimateFilledBucket;
        }
        foreach (Sprite sprite in ownedSprites)
        {
            if (sprite == null) continue;
            Destroy(sprite.texture);
            Destroy(sprite);
        }
    }

    private void Start()
    {
        SyncBucketVisuals();
        RebuildPlates();
        ApplyCoverInsetAndMask();
    }

    private void Update()
    {
        SyncFillBoosterPause();

        if (conveyor != null)
        {
            conveyor.UpdateBelt(Time.deltaTime);
        }

        UpdatePlatePositions();
        UpdateBucketPositions();
    }

    private void SyncFillBoosterPause()
    {
        if (conveyor == null)
        {
            return;
        }

        bool pauseForFill = BoosterService.instance != null
            && BoosterService.instance.ActiveTargetMode == BoosterType.Transparent;
        bool pauseForTutorial = SandEconomyHud.instance != null
            && SandEconomyHud.instance.ShouldPauseConveyorForTutorial;
        bool shouldPause = pauseForFill || pauseForTutorial || CampaignGallery.IsOpen;
        if (conveyor.IsMovementPaused != shouldPause)
        {
            conveyor.SetMovementPaused(shouldPause);
        }
    }

    private void ResolveBucketAppearanceSource()
    {
        if (bucketAppearanceSource != null)
        {
            return;
        }

        BucketInventoryUI inventoryUI = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUI != null)
        {
            bucketAppearanceSource = inventoryUI.BucketViewPrefab;
        }
    }

    private void BuildBeltVisuals()
    {
        RawImage legacyRawImage = GetComponent<RawImage>();
        if (legacyRawImage != null)
        {
            Destroy(legacyRawImage);
        }

        RemoveRootMaskAndBackground();
        beltViewport = EnsureChild("BeltViewport", transform);
        StretchRect(beltViewport);
        beltViewport.SetAsFirstSibling();
        DestroyLegacyBeltChildren(beltViewport);

        BuildPerspectiveRoot(beltViewport);
        BuildPlateTrack();
        RectTransform surface = EnsureChild("PerspectiveSurface", transform);
        surface.anchorMin = new Vector2(0, .5f);
        surface.anchorMax = new Vector2(1, .5f);
        surface.sizeDelta = new Vector2(-52, 132);
        surface.anchoredPosition = new Vector2(0, -25);
        surface.SetAsFirstSibling();
        surfaceGraphic = surface.GetComponent<ConveyorSurfaceGraphic>();
        if (surfaceGraphic == null) surfaceGraphic = surface.gameObject.AddComponent<ConveyorSurfaceGraphic>();
        surfaceGraphic.raycastTarget = false;
        surfaceGraphic.Initialize(conveyor);
        RectTransform backs = EnsureChild("HoodBackWalls", transform);
        StretchRect(backs);
        var backGraphic = backs.GetComponent<ConveyorHoodGraphic>();
        if (backGraphic == null) backGraphic = backs.gameObject.AddComponent<ConveyorHoodGraphic>();
        backGraphic.Initialize(surfaceGraphic, true);
        backs.SetSiblingIndex(surface.GetSiblingIndex() + 1);
        BuildBucketContainer(beltViewport);
        RectTransform hoods = EnsureChild("ConveyorHoods", transform);
        StretchRect(hoods);
        var hoodGraphic = hoods.GetComponent<ConveyorHoodGraphic>();
        if (hoodGraphic == null) hoodGraphic = hoods.gameObject.AddComponent<ConveyorHoodGraphic>();
        hoodGraphic.Initialize(surfaceGraphic);
        hoods.SetAsLastSibling();

        SetupCoverImages();
        ApplyCoverInsetAndMask();
        DisableConveyorLabelRaycasts();
        RebuildPlates();
    }

    private void BuildPerspectiveRoot(RectTransform parent)
    {
        perspectiveRoot = EnsureChild("PerspectiveRoot", parent);
        StretchRect(perspectiveRoot);
        perspectiveRoot.offsetMin = new Vector2(4f, 20f);
        perspectiveRoot.offsetMax = new Vector2(-4f, -10f);
        perspectiveRoot.localRotation = Quaternion.Euler(PerspectivePitch, PerspectiveYaw, 0f);
        perspectiveRoot.localScale = new Vector3(1.02f, 1.12f, 1f);
    }

    private void BuildPlateTrack()
    {
        plateTrack = EnsureChild("PlateTrack", perspectiveRoot);
        StretchRect(plateTrack);
        plateTrack.offsetMin = new Vector2(2f, RailThickness + 2f);
        plateTrack.offsetMax = new Vector2(-2f, -(RailThickness + 2f));
        // Keep plates under the rails.
        plateTrack.SetSiblingIndex(1);
    }

    private void BuildBucketContainer(RectTransform viewport)
    {
        // Buckets stay outside the perspective root so they remain upright / billboarded.
        GameObject bucketsObject = ReparentOrCreateChild(viewport, "ConveyorBuckets");
        bucketContainer = bucketsObject.GetComponent<RectTransform>();
        StretchRect(bucketContainer);
        bucketContainer.offsetMin = new Vector2(0f, 24f);
        bucketContainer.offsetMax = new Vector2(0f, -8f);
        bucketContainer.localRotation = Quaternion.identity;
        bucketContainer.localScale = Vector3.one;
        bucketContainer.SetAsLastSibling();
    }

    private void ApplyCoverInsetAndMask()
    {
        if (beltViewport == null)
        {
            return;
        }

        beltViewport.offsetMin = new Vector2(-32, -12);
        beltViewport.offsetMax = new Vector2(32, 80);

        RectMask2D mask = beltViewport.GetComponent<RectMask2D>();
        if (mask == null)
        {
            mask = beltViewport.gameObject.AddComponent<RectMask2D>();
        }

        // The only clipping is at the OUTSIDE of the opaque hoods.
        mask.enabled = true;
    }

    private float ResolveCoverInset()
    {
        float inset = DefaultCoverInset;
        if (leftCover != null)
        {
            inset = Mathf.Max(inset, leftCover.rectTransform.rect.width * 0.42f);
        }

        if (rightCover != null)
        {
            inset = Mathf.Max(inset, rightCover.rectTransform.rect.width * 0.42f);
        }

        return inset;
    }

    private void RebuildPlates() => UpdatePlatePositions();

    private static float GetPlateSize()
    {
        float fromBucket = ConveyorBelt.BucketWidth;
        float fromSpacing = Mathf.Max(24f, ConveyorBelt.BucketSpacing - PlateGap);
        return Mathf.Min(fromBucket, fromSpacing);
    }

    private void UpdatePlatePositions()
    {
        if (surfaceGraphic != null) surfaceGraphic.SetTravel(conveyor != null ? conveyor.BeltTravel : 0);
    }

    private static void DestroyLegacyBeltChildren(RectTransform viewport)
    {
        string[] obsolete =
        {
            "BeltStripes",
            "ConveyorBuckets",
            "BeltShadow",
            "FarRail",
            "FrontFace",
            "BeltDeck",
            "PerspectiveRoot"
        };

        for (int i = 0; i < obsolete.Length; i++)
        {
            Transform child = viewport.Find(obsolete[i]);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private void DisableConveyorLabelRaycasts()
    {
        Transform label = transform.Find("ConveyorLabel");
        if (label == null)
        {
            return;
        }

        Graphic[] graphics = label.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private void RemoveRootMaskAndBackground()
    {
        Mask mask = GetComponent<Mask>();
        if (mask != null)
        {
            Destroy(mask);
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            Destroy(rootImage);
        }
    }

    private static RectTransform EnsureChild(string childName, Transform parent)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing.GetComponent<RectTransform>();
        }

        GameObject childObject = new GameObject(childName, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private static GameObject ReparentOrCreateChild(RectTransform parent, string childName)
    {
        Transform existingOnParent = parent.Find(childName);
        if (existingOnParent != null)
        {
            return existingOnParent.gameObject;
        }

        GameObject childObject = new GameObject(childName, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Image GetOrAddImage(RectTransform rect)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        return image;
    }

    private void SetupCoverImages()
    {
        DiscoverCoverImages();
        NormalizeCoverAssignment();
        ConfigureCover(leftCover, true);
        ConfigureCover(rightCover, false);
    }

    private void DiscoverCoverImages()
    {
        if (leftCover != null && rightCover != null)
        {
            return;
        }

        Transform label = transform.Find("ConveyorLabel");
        if (label == null)
        {
            return;
        }

        Image[] images = label.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            RectTransform rect = image.rectTransform;
            if (rect.anchorMin.x < 0.1f && leftCover == null)
            {
                leftCover = image;
            }
            else if (rect.anchorMax.x > 0.9f && rightCover == null)
            {
                rightCover = image;
            }
        }
    }

    private void NormalizeCoverAssignment()
    {
        if (leftCover == null || rightCover == null)
        {
            return;
        }

        // Scene wiring may swap sides; force leftCover to the visually left cap.
        float leftX = leftCover.rectTransform.position.x;
        float rightX = rightCover.rectTransform.position.x;
        if (leftX > rightX)
        {
            Image swap = leftCover;
            leftCover = rightCover;
            rightCover = swap;
        }
    }

    private void ConfigureCover(Image cover, bool isLeft)
    {
        if (cover == null || beltRect == null)
        {
            return;
        }

        RectTransform rect = cover.rectTransform;
        cover.transform.SetParent(transform, false);
        rect.anchorMin = isLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);

        float height = Mathf.Max(100f, beltRect.rect.height * 1.05f);
        float width = Mathf.Max(rect.sizeDelta.x > 1f ? rect.sizeDelta.x : 120f, height * 0.85f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;

        cover.maskable = false;
        cover.raycastTarget = false;
        cover.preserveAspect = true;

        // Retain the mask geometry while replacing the oversized machine artwork.
        cover.color = Color.clear;
        Transform obsoleteCap = rect.Find("MosaicEndCap");
        if (obsoleteCap != null) Destroy(obsoleteCap.gameObject);
        cover.transform.SetAsLastSibling();
    }

    private void SyncBucketVisuals()
    {
        if (conveyor == null || bucketContainer == null)
        {
            return;
        }

        // A level reset clears cosmetic leftovers as well as the logical belt.
        for (int i = completingBuckets.Count - 1; i >= 0; i--)
        {
            if (conveyor.FilledBuckets == 0 && completingBuckets[i] != null)
                Destroy(completingBuckets[i].gameObject);
            if (conveyor.FilledBuckets == 0 || completingBuckets[i] == null) completingBuckets.RemoveAt(i);
        }

        HashSet<Bucket> alive = new HashSet<Bucket>();
        IReadOnlyList<ConveyorBelt.BeltEntry> entries = conveyor.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            Bucket bucket = entries[i].Bucket;
            if (bucket == null)
            {
                continue;
            }

            alive.Add(bucket);
            if (!bucketVisuals.ContainsKey(bucket))
            {
                bucketVisuals[bucket] = CreateBucketVisual(bucket);
            }
        }

        List<Bucket> toRemove = new List<Bucket>();
        foreach (KeyValuePair<Bucket, BucketVisual> pair in bucketVisuals)
        {
            if (!alive.Contains(pair.Key))
            {
                if (pair.Value.rect != null)
                {
                    Destroy(pair.Value.rect.gameObject);
                }

                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            bucketVisuals.Remove(toRemove[i]);
        }
        // Pointer events can run after this view's Update. Apply the chosen visible
        // position immediately so a new bucket never flashes at the hidden entry.
        UpdateBucketPositions();
    }

    private void AnimateFilledBucket(Bucket bucket)
    {
        if (!bucketVisuals.TryGetValue(bucket, out BucketVisual visual) || visual.rect == null) return;
        bucketVisuals.Remove(bucket);
        if (visual.capacityLabel != null) visual.capacityLabel.text = "0";
        completingBuckets.Add(visual.rect.gameObject.AddComponent<BucketCompletionAnimation>());
    }

    private void UpdateBucketPositions()
    {
        if (conveyor == null || bucketContainer == null)
        {
            return;
        }

        IReadOnlyList<ConveyorBelt.BeltEntry> entries = conveyor.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            ConveyorBelt.BeltEntry entry = entries[i];
            BucketVisual visual;
            if (entry.Bucket == null || !bucketVisuals.TryGetValue(entry.Bucket, out visual))
            {
                continue;
            }

            if (visual.rect == null)
            {
                continue;
            }

            // Map plate-local X into the upright bucket container via world space,
            // so buckets stay flat while still sitting over their plates.
            Vector3 world = ProjectBeltXToBucketWorld(entry.PositionX);
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bucketContainer,
                screen,
                eventCamera,
                out local);

            visual.rect.anchoredPosition = local;
            visual.rect.localRotation = Quaternion.identity;
            visual.rect.localScale = Vector3.one;
            if (visual.visibility != null)
            {
                visual.visibility.alpha = 1;
                visual.visibility.blocksRaycasts = Mathf.Abs(entry.PositionX) < beltRect.rect.width * .5f - 30;
            }

            int remainingCapacity = entry.Bucket.RemainingCapacity;
            if (visual.capacityLabel != null && visual.lastRemainingCapacity != remainingCapacity)
            {
                visual.lastRemainingCapacity = remainingCapacity;
                visual.capacityLabel.text = remainingCapacity.ToString();
            }
        }
    }

    private Vector3 ProjectBeltXToBucketWorld(float beltX)
    {
        if (surfaceGraphic != null)
        {
            float bucketHeight = GetPlateSize() * .92f;
            return surfaceGraphic.MidlineWorld(beltX) + surfaceGraphic.rectTransform.TransformVector(0, -BucketArt.Footprint * bucketHeight, 0);
        }
        if (plateTrack != null)
        {
            return plateTrack.TransformPoint(new Vector3(beltX, GetPlateSize() * 0.15f, 0f));
        }

        if (perspectiveRoot != null)
        {
            return perspectiveRoot.TransformPoint(new Vector3(beltX, 0f, 0f));
        }

        return bucketContainer.TransformPoint(new Vector3(beltX, 0f, 0f));
    }

    public bool TryGetBucketPourTargetWorldPosition(Bucket bucket, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (bucket == null)
        {
            return false;
        }

        BucketVisual visual;
        if (!bucketVisuals.TryGetValue(bucket, out visual) || visual.rect == null)
        {
            return false;
        }

        // Aim the sand stream at the opening of the perspective bucket.
        float topY = visual.rect.rect.height * .23f;
        worldPosition = visual.rect.TransformPoint(new Vector3(0f, topY, 0f));
        return true;
    }

    public bool TryGetBucketCanvasPosition(Bucket bucket, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        Vector3 world;
        if (!TryGetBucketPourTargetWorldPosition(bucket, out world))
        {
            return false;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return false;
        }

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, world);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out canvasPosition);
    }

    public bool TryGetActiveBucketCanvasPosition(out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        if (conveyor == null)
        {
            return false;
        }

        Bucket activeBucket = conveyor.GetActiveBucket();
        if (activeBucket == null)
        {
            return false;
        }

        return TryGetBucketCanvasPosition(activeBucket, out canvasPosition);
    }

    private BucketVisual CreateBucketVisual(Bucket bucket)
    {
        ResolveBucketAppearanceSource();

        GameObject bucketObject = new GameObject("BeltBucket", typeof(RectTransform));
        bucketObject.transform.SetParent(bucketContainer, false);

        RectTransform rect = bucketObject.GetComponent<RectTransform>();
        float size = GetPlateSize() * 0.92f;
        rect.sizeDelta = new Vector2(size * (bucket.SlotCost > 1 ? 1.7f : 1), size);
        rect.anchoredPosition = new Vector2(ConveyorBelt.EntryX, 0f);
        rect.localRotation = Quaternion.identity;

        Image bucketImage = bucketObject.AddComponent<Image>();
        CanvasGroup visibility = bucketObject.AddComponent<CanvasGroup>();
        bucketImage.raycastTarget = true;
        bucketImage.preserveAspect = bucket.SlotCost == 1;

        Sprite openSprite = BucketArt.Open;
        if (openSprite != null)
        {
            bucketImage.sprite = openSprite;
        }

        if (levelLoader != null && levelLoader.CurrentLevel != null)
        {
            bucketImage.color = SandDisplayColors.FromPalette(
                levelLoader.CurrentLevel.GetPaletteColor(bucket.ColorIndex));
        }

        Button button = bucketObject.AddComponent<Button>();
        button.targetGraphic = bucketImage;
        Bucket captured = bucket;
        button.onClick.AddListener(() =>
        {
            GameAudio.Instance?.PlayTap();
            if (SandEconomyHud.instance != null)
            {
                if (SandEconomyHud.instance.TryHandleConveyorBucketClick(captured)) return;
                if (SandEconomyHud.instance.BlocksInventoryForTutorial) return;
            }
            if (BoosterService.instance != null && BoosterService.instance.ActiveTargetMode.HasValue) return;
            if (conveyor != null) conveyor.TryRecallBucket(captured);
        });

        GameObject labelObject = new GameObject("CapacityLabel", typeof(RectTransform));
        labelObject.transform.SetParent(bucketObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.anchoredPosition = new Vector2(0, -size * .12f);

        TextMeshProUGUI capacityLabel = labelObject.AddComponent<TextMeshProUGUI>();
        capacityLabel.text = bucket.RemainingCapacity.ToString();
        capacityLabel.alignment = TextAlignmentOptions.Center;
        capacityLabel.fontSize = GetBucketLabelFontSize();
        capacityLabel.raycastTarget = false;

        Color32 bucketColor = levelLoader != null && levelLoader.CurrentLevel != null
            ? SandDisplayColors.FromPalette(levelLoader.CurrentLevel.GetPaletteColor(bucket.ColorIndex))
            : new Color32(180, 180, 180, 255);
        SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, bucketColor);
        bucketObject.AddComponent<BucketMechanicVisual>().Initialize(bucket, bucketImage, capacityLabel, levelLoader.CurrentLevel);

        return new BucketVisual
        {
            rect = rect,
            bucketImage = bucketImage,
            capacityLabel = capacityLabel,
            visibility = visibility,
            lastRemainingCapacity = bucket.RemainingCapacity
        };
    }

    public void RefreshGeometry()
    {
        ApplyCoverInsetAndMask();
        SetupCoverImages();
        RebuildPlates();

        float size = GetPlateSize() * 0.92f;
        foreach (KeyValuePair<Bucket, BucketVisual> pair in bucketVisuals)
        {
            BucketVisual visual = pair.Value;
            if (visual.rect == null)
            {
                continue;
            }

            visual.rect.sizeDelta = new Vector2(size * (pair.Key.SlotCost > 1 ? 1.7f : 1), size);
            if (visual.capacityLabel != null)
            {
                visual.capacityLabel.fontSize = GetBucketLabelFontSize();
            }
        }
    }

    private static float GetBucketLabelFontSize()
    {
        return Mathf.Max(22f, GetPlateSize() * .30f);
    }

    private Sprite Own(Sprite sprite)
    {
        ownedSprites.Add(sprite);
        return sprite;
    }

}
