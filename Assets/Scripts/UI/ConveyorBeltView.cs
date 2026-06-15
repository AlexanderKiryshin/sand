using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ConveyorBeltView : MonoBehaviour
{
    [SerializeField] private float beltScrollSpeed = 90f;

    private RectTransform beltRect;
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
        public Image fillImage;
    }

    private void Awake()
    {
        beltRect = GetComponent<RectTransform>();
        conveyor = FindObjectOfType<ConveyorBelt>();
        levelLoader = FindObjectOfType<LevelLoader>();

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

    private void BuildBeltVisuals()
    {
        RawImage legacyRawImage = GetComponent<RawImage>();
        if (legacyRawImage != null)
        {
            Destroy(legacyRawImage);
        }

        Image background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        background.color = new Color(0.5f, 0.53f, 0.57f);
        background.raycastTarget = false;

        GameObject stripesObject = transform.Find("BeltStripes") != null
            ? transform.Find("BeltStripes").gameObject
            : new GameObject("BeltStripes", typeof(RectTransform));

        stripesObject.transform.SetParent(transform, false);
        RectTransform stripesRect = stripesObject.GetComponent<RectTransform>();
        stripesRect.anchorMin = Vector2.zero;
        stripesRect.anchorMax = Vector2.one;
        stripesRect.offsetMin = Vector2.zero;
        stripesRect.offsetMax = Vector2.zero;

        stripeImage = stripesObject.GetComponent<RawImage>();
        if (stripeImage == null)
        {
            stripeImage = stripesObject.AddComponent<RawImage>();
        }

        stripeImage.raycastTarget = false;
        stripeImage.color = new Color(1f, 1f, 1f, 0.55f);

        beltTexture = CreateBeltTexture(256, 32);
        stripeImage.texture = beltTexture;

        GameObject containerObject = transform.Find("ConveyorBuckets") != null
            ? transform.Find("ConveyorBuckets").gameObject
            : new GameObject("ConveyorBuckets", typeof(RectTransform));

        containerObject.transform.SetParent(transform, false);
        bucketContainer = containerObject.GetComponent<RectTransform>();
        bucketContainer.anchorMin = Vector2.zero;
        bucketContainer.anchorMax = Vector2.one;
        bucketContainer.offsetMin = Vector2.zero;
        bucketContainer.offsetMax = Vector2.zero;

        Mask mask = GetComponent<Mask>();
        if (mask == null)
        {
            mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
        }
    }

    private void AnimateBeltStripes()
    {
        if (stripeImage == null || beltTexture == null)
        {
            return;
        }

        beltOffset += beltScrollSpeed * Time.deltaTime;
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

            if (visual.fillImage != null)
            {
                visual.fillImage.fillAmount = entry.Bucket.FillNormalized;
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
        GameObject bucketObject = new GameObject("BeltBucket", typeof(RectTransform));
        bucketObject.transform.SetParent(bucketContainer, false);

        RectTransform rect = bucketObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = new Vector2(ConveyorBelt.EntryX, 0f);

        Image outline = bucketObject.AddComponent<Image>();
        outline.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        outline.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(bucketObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        Image fill = fillObject.AddComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.raycastTarget = false;

        if (levelLoader != null && levelLoader.CurrentLevel != null)
        {
            fill.color = SandDisplayColors.FromPalette(
                levelLoader.CurrentLevel.GetPaletteColor(bucket.ColorIndex));
        }

        return new BucketVisual { rect = rect, fillImage = fill };
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
