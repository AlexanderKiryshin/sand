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
    private bool isSelectable = true;
    private RectTransform cachedRectTransform;
    private Vector2 layoutAnchoredPosition;
    private float tutorialBounceOffset;

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
        isSelectable = selectable;

        if (button != null)
        {
            button.interactable = selectable;
        }

        ApplyVisual();
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

        Sprite sprite = isSelectable ? openBucketSprite : lockedBucketSprite;
        if (sprite != null)
        {
            bucketImage.sprite = sprite;
            bucketImage.color = baseColor;
            return;
        }

        if (isSelectable)
        {
            bucketImage.color = baseColor;
            return;
        }

        Color dimmed = baseColor;
        dimmed.a = 160;
        bucketImage.color = dimmed;
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

        cachedRectTransform.anchoredPosition = layoutAnchoredPosition + new Vector2(0f, tutorialBounceOffset);
    }
}
