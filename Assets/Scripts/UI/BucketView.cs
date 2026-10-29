using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BucketView : MonoBehaviour
{
    [SerializeField] private Image bucketImage;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI capacityLabel;

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
    private IceShellGraphic iceShell;
    private TextMeshProUGUI iceMovesLabel;
    private bool interactionInitialized;
    private bool flipping;
    private float flipElapsed;
    private Vector2 moveFrom;
    private float moveTime = 1;
    public bool HasLayout { get; private set; }
    public bool IsMoving => moveTime < 1;
    public void AnimateLayoutFrom(Vector2 from) { moveFrom = from; moveTime = 0; ApplyTutorialOffset(); }
    public bool IsFlipping => flipping;
    public float FlipProgress => flipping ? Mathf.Clamp01(flipElapsed / .65f) : 1;
    public bool LooksOpen => looksOpen;
    public RectTransform BodyRect => bucketImage != null ? bucketImage.rectTransform : (RectTransform)transform;

    public Bucket Bucket { get { return bucket; } }
    public Sprite OpenBucketSprite => bucket != null && bucket.SlotCost > 1 ? BucketArt.LargeOpen : BucketArt.Open;
    public Sprite LockedBucketSprite => bucket != null && bucket.SlotCost > 1 ? BucketArt.LargeInverted : BucketArt.Inverted;

    public void Initialize(Bucket targetBucket, Color32 color, Action<Bucket> clickHandler, LevelData levelData = null, BucketInventory stock = null)
    {
        bucket = targetBucket;
        onClick = clickHandler;
        baseColor = SandDisplayColors.FromPalette(color);
        cachedRectTransform = GetComponent<RectTransform>();
        EnsureBodyVisual();

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bucketImage;
        }

        ApplyVisual();
        UpdateRemainingCapacity();
        ApplyTutorialOffset();
        var mechanics = gameObject.GetComponent<BucketMechanicVisual>() ?? gameObject.AddComponent<BucketMechanicVisual>();
        mechanics.Initialize(bucket, bucketImage, capacityLabel,
            levelData != null ? levelData : FindObjectOfType<LevelLoader>()?.CurrentLevel,
            stock ?? FindObjectOfType<GameManager>()?.Inventory);
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
        bool unlock = interactionInitialized && !looksOpen && looksOpenVisual;
        looksOpen = looksOpenVisual;
        interactionInitialized = true;
        if (unlock) { flipping = true; flipElapsed = 0; }
        if (!looksOpen) { flipping = false; ResetBodyPose(); }
        if (looksOpen && bucket != null) bucket.Reveal();
        if (button != null)
        {
            button.interactable = canClick;
        }

        SetMagnetTapHint(showMagnetTapHint);
        ApplyVisual();
        UpdateRemainingCapacity();
    }

    private void Update()
    {
        if (moveTime < 1) { moveTime = Mathf.Min(1, moveTime + Time.unscaledDeltaTime / .3f); ApplyTutorialOffset(); }
        if (flipping)
        {
            flipElapsed += Time.unscaledDeltaTime;
            float t = FlipProgress;
            float turn = t < .5f ? -180 * t : 180 * (1 - t);
            bucketImage.sprite = t < .5f ? LockedBucketSprite : OpenBucketSprite;
            bucketImage.rectTransform.localRotation = Quaternion.Euler(0, 0, turn);
            bucketImage.rectTransform.anchoredPosition = new Vector2(0, Mathf.Sin(t * Mathf.PI) * 12);
            if (t >= 1) { flipping = false; ResetBodyPose(); ApplyVisual(); }
        }
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
        capacityLabel.fontSize = 26;
        capacityLabel.fontStyle = FontStyles.Bold;
        capacityLabel.enableWordWrapping = false;
        capacityLabel.rectTransform.anchoredPosition = new Vector2(0, -MobilePortraitLayout.BucketCellSize * .12f);
        if (bucket.IsMystery) capacityLabel.text = "?";
        SandDisplayColors.ApplyCapacityLabelStyle(capacityLabel, baseColor);
        if (bucket.IsMystery) capacityLabel.color = Color.white;
        RefreshIceVisual();
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
        HasLayout = true;
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

        if (bucket != null && (bucket.IsFrozen || bucket.IsMystery))
        {
            bucketImage.color = bucket.IsMystery ? new Color(0.3f, 0.36f, 0.48f) : Color.Lerp(baseColor, Color.white, .12f);
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

        Sprite sprite = looksOpen && (!flipping || FlipProgress >= .5f) ? OpenBucketSprite : LockedBucketSprite;
        if (sprite != null)
        {
            bucketImage.sprite = sprite;
        }

        if (bucket != null && (bucket.IsFrozen || bucket.IsMystery))
        {
            bucketImage.color = bucket.IsMystery ? new Color(0.3f, 0.36f, 0.48f) : Color.Lerp(baseColor, Color.white, .12f);
            if (pulseVisualImage != null) pulseVisualImage.color = bucketImage.color;
            return;
        }

        if (bucket != null && !bucket.IsMystery && bucketImage.rectTransform.localScale.x < 0.95f)
            bucketImage.rectTransform.localScale = Vector3.one;

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
            ApplyTutorialOffset();
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

    private void RefreshIceVisual()
    {
        if (bucket == null) return;
        if (iceShell == null && bucket.IsFrozen)
        {
            var shell = new GameObject("FrozenShell", typeof(RectTransform), typeof(IceShellGraphic));
            shell.transform.SetParent(transform, false);
            iceShell = shell.GetComponent<IceShellGraphic>();
            iceShell.raycastTarget = false;
            iceShell.rectTransform.anchorMin = Vector2.zero;
            iceShell.rectTransform.anchorMax = Vector2.one;
            iceShell.rectTransform.offsetMin = new Vector2(-2, -2);
            iceShell.rectTransform.offsetMax = new Vector2(2, 2);
            var badge = new GameObject("IceMoves", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(shell.transform, false);
            var image = badge.GetComponent<Image>();
            image.sprite = HintUiSprites.CircleBadge;
            image.color = new Color(.10f, .43f, .65f);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.83f, .16f);
            image.rectTransform.sizeDelta = new Vector2(30, 30);
            var label = new GameObject("Moves", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(badge.transform, false);
            iceMovesLabel = label.GetComponent<TextMeshProUGUI>();
            iceMovesLabel.rectTransform.anchorMin = Vector2.zero;
            iceMovesLabel.rectTransform.anchorMax = Vector2.one;
            iceMovesLabel.rectTransform.offsetMin = iceMovesLabel.rectTransform.offsetMax = Vector2.zero;
            iceMovesLabel.fontSize = 22;
            iceMovesLabel.fontStyle = FontStyles.Bold;
            iceMovesLabel.alignment = TextAlignmentOptions.Center;
            iceMovesLabel.raycastTarget = false;
        }
        if (iceShell == null) return;
        iceShell.SetLayers(bucket.FrostMoves);
        if (iceMovesLabel != null) iceMovesLabel.text = bucket.FrostMoves.ToString();
        if (capacityLabel != null) capacityLabel.transform.SetAsLastSibling();
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
        Sprite sprite = looksOpen ? OpenBucketSprite : LockedBucketSprite;
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
            GameAudio.Instance?.PlayTap();
            onClick(bucket);
        }
    }

    public void RefreshAppearance(Color32 color)
    {
        baseColor = SandDisplayColors.FromPalette(color);
        ApplyVisual();
        UpdateRemainingCapacity();
    }

    private void EnsureBodyVisual()
    {
        if (bucketImage != null && bucketImage.transform != transform) return;
        if (bucketImage != null) bucketImage.enabled = false;
        var body = new GameObject("BucketBody", typeof(RectTransform), typeof(Image));
        body.transform.SetParent(transform, false);
        body.transform.SetAsFirstSibling();
        bucketImage = body.GetComponent<Image>();
        bucketImage.preserveAspect = true;
        bucketImage.rectTransform.anchorMin = Vector2.zero;
        bucketImage.rectTransform.anchorMax = Vector2.one;
        bucketImage.rectTransform.offsetMin = bucketImage.rectTransform.offsetMax = Vector2.zero;
    }

    private void ResetBodyPose()
    {
        if (bucketImage == null) return;
        bucketImage.rectTransform.localRotation = Quaternion.identity;
        bucketImage.rectTransform.anchoredPosition = Vector2.zero;
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

        float movement = Mathf.SmoothStep(0, 1, moveTime);
        cachedRectTransform.localScale = Vector3.one * (1 - .32f * Mathf.Sin(movement * Mathf.PI));
        cachedRectTransform.anchoredPosition = Vector2.Lerp(moveFrom, layoutAnchoredPosition, movement) + new Vector2(0f, tutorialBounceOffset);
    }
}
