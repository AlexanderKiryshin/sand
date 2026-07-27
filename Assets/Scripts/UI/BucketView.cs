using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BucketView : MonoBehaviour
{
    [SerializeField] private Image bucketImage;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI capacityLabel;
    [Header("Sprites")]
    [Tooltip("White or grayscale sprite tinted with the bucket color when the bucket can be taken.")]
    [SerializeField] private Sprite openBucketSprite;
    [Tooltip("White or grayscale sprite tinted with the bucket color when the bucket is blocked.")]
    [SerializeField] private Sprite lockedBucketSprite;

    private Bucket bucket;
    private Action<Bucket> onClick;
    private Color32 baseColor;
    private bool looksOpen = true;
    private bool magnetTapHint;
    private float magnetPulseTime;
    private RectTransform cachedRectTransform;
    private Vector2 layoutAnchoredPosition;
    private float tutorialBounceOffset;
    private RectTransform pulseVisualRect;
    private Image pulseVisualImage;

    public Bucket Bucket { get { return bucket; } }
    public Sprite OpenBucketSprite { get { return openBucketSprite; } }
    public Sprite LockedBucketSprite { get { return lockedBucketSprite; } }

    public void Initialize(Bucket targetBucket, Color32 color, Action<Bucket> clickHandler)
    {
        bucket = targetBucket;
        onClick = clickHandler;
        baseColor = SandDisplayColors.FromPalette(color);
        cachedRectTransform = GetComponent<RectTransform>();

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
            button.transition = Selectable.Transition.None;
        }

        ApplyVisual();
        UpdateRemainingCapacity();
        ApplyTutorialOffset();
    }

    public void SetSelectable(bool selectable)
    {
        ConfigureInteraction(selectable, selectable, false);
    }

    /// <summary>
    /// looksOpen controls open/locked sprite. canClick enables taps. magnetTapHint pulses on locked targets.
    /// </summary>
    public void ConfigureInteraction(bool looksOpenVisual, bool canClick, bool showMagnetTapHint)
    {
        looksOpen = looksOpenVisual;
        if (button != null)
        {
            button.interactable = canClick;
        }

        SetMagnetTapHint(showMagnetTapHint);
        ApplyVisual();
    }

    private void Update()
    {
        if (!magnetTapHint || pulseVisualRect == null)
        {
            return;
        }

        magnetPulseTime += Time.unscaledDeltaTime;
        float pulse = 1f + Mathf.Sin(magnetPulseTime * 2.1f) * 0.055f;
        pulseVisualRect.localScale = new Vector3(pulse, pulse, 1f);
    }

    public void UpdateRemainingCapacity()
    {
        EnsureCapacityLabel();
        if (capacityLabel == null || bucket == null)
        {
            return;
        }

        capacityLabel.text = bucket.RemainingCapacity.ToString();
        SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, baseColor);
    }

    public void CaptureLayoutPosition()
    {
        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        if (cachedRectTransform == null)
        {
            return;
        }

        layoutAnchoredPosition = cachedRectTransform.anchoredPosition;
        ApplyTutorialOffset();
    }

    public void SetTutorialBounceOffset(float offsetY)
    {
        tutorialBounceOffset = offsetY;
        ApplyTutorialOffset();
    }

    public void ClearTutorialBounce()
    {
        tutorialBounceOffset = 0f;
        ApplyTutorialOffset();
    }

    public Vector2 GetCanvasPosition()
    {
        return GetCenterCanvasPosition();
    }

    public Vector2 GetCenterCanvasPosition()
    {
        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (cachedRectTransform == null || canvasRect == null)
        {
            return Vector2.zero;
        }

        Vector3[] corners = new Vector3[4];
        cachedRectTransform.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out canvasPosition);
        return canvasPosition;
    }

    public float GetCanvasSize()
    {
        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        if (cachedRectTransform == null)
        {
            return MobilePortraitLayout.BucketCellSize;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        return cachedRectTransform.rect.height * cachedRectTransform.lossyScale.y / Mathf.Max(scale, 0.001f);
    }

    public void SetTutorialDimmed(bool dimmed)
    {
        if (bucketImage == null)
        {
            return;
        }

        if (!dimmed)
        {
            ApplyVisual();
            return;
        }

        Color dimmedColor = baseColor;
        dimmedColor.r = (byte)Mathf.RoundToInt(baseColor.r * 0.45f);
        dimmedColor.g = (byte)Mathf.RoundToInt(baseColor.g * 0.45f);
        dimmedColor.b = (byte)Mathf.RoundToInt(baseColor.b * 0.45f);
        dimmedColor.a = 120;
        bucketImage.color = dimmedColor;
    }

    private void ApplyVisual()
    {
        if (bucketImage == null)
        {
            return;
        }

        Sprite sprite = looksOpen ? openBucketSprite : lockedBucketSprite;
        if (sprite != null)
        {
            bucketImage.sprite = sprite;
        }

        if (magnetTapHint)
        {
            Color hidden = baseColor;
            hidden.a = 0f;
            bucketImage.color = hidden;
            SyncPulseVisualFromBucketImage();
            return;
        }

        if (sprite != null)
        {
            bucketImage.color = baseColor;
            return;
        }

        if (looksOpen)
        {
            bucketImage.color = baseColor;
            return;
        }

        Color dimmed = baseColor;
        dimmed.a = 160;
        bucketImage.color = dimmed;
    }

    private void SetMagnetTapHint(bool enabled)
    {
        magnetTapHint = enabled;
        magnetPulseTime = 0f;

        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        // Layout root must stay fixed — pulse a centered child visual instead.
        if (cachedRectTransform != null)
        {
            cachedRectTransform.localScale = Vector3.one;
            cachedRectTransform.anchoredPosition = layoutAnchoredPosition + new Vector2(0f, tutorialBounceOffset);
        }

        if (enabled)
        {
            EnsurePulseVisual();
            SyncPulseVisualFromBucketImage();
            if (bucketImage != null)
            {
                Color hidden = baseColor;
                hidden.a = 0f;
                bucketImage.color = hidden;
            }

            if (pulseVisualRect != null)
            {
                pulseVisualRect.gameObject.SetActive(true);
                pulseVisualRect.localScale = Vector3.one;
            }
        }
        else
        {
            if (pulseVisualRect != null)
            {
                pulseVisualRect.localScale = Vector3.one;
                pulseVisualRect.gameObject.SetActive(false);
            }

            if (bucketImage != null)
            {
                ApplyVisual();
            }
        }
    }

    private void EnsurePulseVisual()
    {
        if (pulseVisualRect != null)
        {
            return;
        }

        GameObject visual = new GameObject("MagnetPulseVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visual.transform.SetParent(transform, false);
        pulseVisualRect = visual.GetComponent<RectTransform>();
        pulseVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        pulseVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        pulseVisualRect.pivot = new Vector2(0.5f, 0.5f);
        pulseVisualRect.anchoredPosition = Vector2.zero;
        pulseVisualImage = visual.GetComponent<Image>();
        pulseVisualImage.raycastTarget = false;
        pulseVisualImage.preserveAspect = true;
        pulseVisualImage.type = Image.Type.Simple;
        visual.SetActive(false);

        if (capacityLabel != null)
        {
            capacityLabel.transform.SetAsLastSibling();
        }
    }

    private void SyncPulseVisualFromBucketImage()
    {
        if (pulseVisualRect == null || pulseVisualImage == null)
        {
            return;
        }

        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        Vector2 size = cachedRectTransform != null ? cachedRectTransform.sizeDelta : new Vector2(56f, 56f);
        pulseVisualRect.sizeDelta = size;
        Sprite sprite = looksOpen ? openBucketSprite : lockedBucketSprite;
        if (sprite == null && bucketImage != null)
        {
            sprite = bucketImage.sprite;
        }

        pulseVisualImage.sprite = sprite;
        pulseVisualImage.color = baseColor;
        if (bucketImage != null)
        {
            pulseVisualImage.preserveAspect = bucketImage.preserveAspect;
        }
    }

    private void EnsureCapacityLabel()
    {
        if (capacityLabel != null)
        {
            return;
        }

        Transform existing = transform.Find("CapacityLabel");
        if (existing != null)
        {
            capacityLabel = existing.GetComponent<TextMeshProUGUI>();
            SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, baseColor);
            return;
        }

        GameObject labelObject = new GameObject("CapacityLabel", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        capacityLabel = labelObject.AddComponent<TextMeshProUGUI>();
        capacityLabel.alignment = TextAlignmentOptions.Center;
        capacityLabel.fontSize = 11f;
        capacityLabel.raycastTarget = false;
        SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, baseColor);
    }

    private void HandleClick()
    {
        if (onClick != null && bucket != null)
        {
            onClick(bucket);
        }
    }

    private void ApplyTutorialOffset()
    {
        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        if (cachedRectTransform == null)
        {
            return;
        }

        cachedRectTransform.localScale = Vector3.one;
        cachedRectTransform.anchoredPosition = layoutAnchoredPosition + new Vector2(0f, tutorialBounceOffset);
    }
}
