using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ConveyorBeltView : MonoBehaviour
{
    [SerializeField] private BucketView bucketAppearanceSource;
    [SerializeField] private Image leftCover;
    [SerializeField] private Image rightCover;

    private RectTransform beltRect;
    private RectTransform beltViewport;
    private RawImage stripeImage;
    private ConveyorBelt conveyor;
    private LevelLoader levelLoader;
    private Texture2D beltTexture;
    private float beltOffset;
    private RectTransform bucketContainer;
    private readonly Dictionary<Bucket, BucketVisual> bucketVisuals = new Dictionary<Bucket, BucketVisual>();

    private class BucketVisual
    {
        public RectTransform rect;
        public Image bucketImage;
        public TextMeshProUGUI capacityLabel;
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
        }

        BuildBeltVisuals();
    }

    private void OnDestroy()
    {
        if (conveyor != null)
        {
            conveyor.OnConveyorChanged -= SyncBucketVisuals;
        }
    }

    private void Start()
    {
        SyncBucketVisuals();
    }

    private void Update()
    {
        if (conveyor != null)
        {
            conveyor.UpdateBelt(Time.deltaTime);
        }

        AnimateBeltStripes();
        UpdateBucketPositions();
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
        beltViewport = EnsureBeltViewport();

        Image background = beltViewport.GetComponent<Image>();
        if (background == null)
        {
            background = beltViewport.gameObject.AddComponent<Image>();
        }

        background.color = new Color(0.5f, 0.53f, 0.57f, 1f);
        background.raycastTarget = false;

        GameObject stripesObject = ReparentOrCreateChild(beltViewport, "BeltStripes");
        RectTransform stripesRect = stripesObject.GetComponent<RectTransform>();
        StretchRect(stripesRect);

        stripeImage = stripesObject.GetComponent<RawImage>();
        if (stripeImage == null)
        {
            stripeImage = stripesObject.AddComponent<RawImage>();
        }

        stripeImage.raycastTarget = false;
        stripeImage.color = new Color(1f, 1f, 1f, 0.55f);

        beltTexture = CreateBeltTexture(256, 32);
        stripeImage.texture = beltTexture;

        GameObject containerObject = ReparentOrCreateChild(beltViewport, "ConveyorBuckets");
        bucketContainer = containerObject.GetComponent<RectTransform>();
        StretchRect(bucketContainer);

        RectMask2D viewportMask = beltViewport.GetComponent<RectMask2D>();
        if (viewportMask == null)
        {
            viewportMask = beltViewport.gameObject.AddComponent<RectMask2D>();
        }

        SetupCoverImages();
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

    private RectTransform EnsureBeltViewport()
    {
        Transform existing = transform.Find("BeltViewport");
        GameObject viewportObject;
        if (existing != null)
        {
            viewportObject = existing.gameObject;
        }
        else
        {
            viewportObject = new GameObject("BeltViewport", typeof(RectTransform));
            viewportObject.transform.SetParent(transform, false);
        }

        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        StretchRect(viewport);
        viewport.SetAsFirstSibling();
        return viewport;
    }

    private static GameObject ReparentOrCreateChild(RectTransform parent, string childName)
    {
        Transform existingOnParent = parent.Find(childName);
        if (existingOnParent != null)
        {
            return existingOnParent.gameObject;
        }

        Transform existingOnRoot = parent.parent != null ? parent.parent.Find(childName) : null;
        if (existingOnRoot != null)
        {
            existingOnRoot.SetParent(parent, false);
            return existingOnRoot.gameObject;
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

    private void SetupCoverImages()
    {
        DiscoverCoverImages();

        ConfigureCover(leftCover);
        ConfigureCover(rightCover);
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

    private void ConfigureCover(Image cover)
    {
        if (cover == null)
        {
            return;
        }

        cover.transform.SetParent(transform, true);
        cover.maskable = false;
        cover.raycastTarget = false;
        cover.preserveAspect = true;

        Color color = cover.color;
        color.a = 1f;
        cover.color = color;

        cover.transform.SetAsLastSibling();
    }

    private void AnimateBeltStripes()
    {
        if (stripeImage == null || beltTexture == null)
        {
            return;
        }

        float scrollSpeed = conveyor != null
            ? conveyor.BeltScrollSpeed
            : ConveyorSettings.DefaultBeltScrollSpeed;
        if (conveyor != null && conveyor.IsFastSpeed)
        {
            scrollSpeed *= conveyor.FullSpeedMultiplier;
        }

        beltOffset += scrollSpeed * Time.deltaTime;
        float normalized = (beltOffset / beltTexture.width) % 1f;
        stripeImage.uvRect = new Rect(normalized, 0f, 1f, 1f);
    }

    private void SyncBucketVisuals()
    {
        if (conveyor == null || bucketContainer == null)
        {
            return;
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
    }

    private void UpdateBucketPositions()
    {
        if (conveyor == null)
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

            if (visual.rect != null)
            {
                visual.rect.anchoredPosition = new Vector2(entry.PositionX, 0f);
            }

            int remainingCapacity = entry.Bucket.RemainingCapacity;
            if (visual.capacityLabel != null && visual.lastRemainingCapacity != remainingCapacity)
            {
                visual.lastRemainingCapacity = remainingCapacity;
                visual.capacityLabel.text = remainingCapacity.ToString();
            }
        }
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

        float topY = visual.rect.rect.height * 0.5f - 2f;
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
        rect.sizeDelta = new Vector2(ConveyorBelt.BucketWidth, ConveyorBelt.BucketWidth);
        rect.anchoredPosition = new Vector2(ConveyorBelt.EntryX, 0f);

        Image bucketImage = bucketObject.AddComponent<Image>();
        bucketImage.raycastTarget = true;
        bucketImage.preserveAspect = true;

        Sprite openSprite = bucketAppearanceSource != null
            ? bucketAppearanceSource.OpenBucketSprite
            : null;
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
            if (SandEconomyHud.instance != null)
            {
                SandEconomyHud.instance.TryHandleConveyorBucketClick(captured);
            }
        });

        GameObject labelObject = new GameObject("CapacityLabel", typeof(RectTransform));
        labelObject.transform.SetParent(bucketObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI capacityLabel = labelObject.AddComponent<TextMeshProUGUI>();
        capacityLabel.text = bucket.RemainingCapacity.ToString();
        capacityLabel.alignment = TextAlignmentOptions.Center;
        capacityLabel.fontSize = GetBucketLabelFontSize();
        capacityLabel.raycastTarget = false;

        Color32 bucketColor = levelLoader != null && levelLoader.CurrentLevel != null
            ? SandDisplayColors.FromPalette(levelLoader.CurrentLevel.GetPaletteColor(bucket.ColorIndex))
            : new Color32(180, 180, 180, 255);
        SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, bucketColor);

        return new BucketVisual
        {
            rect = rect,
            bucketImage = bucketImage,
            capacityLabel = capacityLabel,
            lastRemainingCapacity = bucket.RemainingCapacity
        };
    }

    public void RefreshGeometry()
    {
        foreach (KeyValuePair<Bucket, BucketVisual> pair in bucketVisuals)
        {
            BucketVisual visual = pair.Value;
            if (visual.rect == null)
            {
                continue;
            }

            visual.rect.sizeDelta = new Vector2(ConveyorBelt.BucketWidth, ConveyorBelt.BucketWidth);
            if (visual.capacityLabel != null)
            {
                visual.capacityLabel.fontSize = GetBucketLabelFontSize();
            }
        }
    }

    private static float GetBucketLabelFontSize()
    {
        return Mathf.Max(14f, ConveyorBelt.BucketWidth * 0.28f);
    }

    private static Texture2D CreateBeltTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color dark = new Color(0.35f, 0.38f, 0.42f, 1f);
        Color light = new Color(0.55f, 0.58f, 0.62f, 1f);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool stripe = (x / 10) % 2 == 0;
                pixels[y * width + x] = stripe ? dark : light;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
