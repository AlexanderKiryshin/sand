using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
[DefaultExecutionOrder(-100)]
public class MobilePortraitLayout : MonoBehaviour
{
    public const float ReferenceWidth = 1080f;
    public const float ReferenceHeight = 1920f;

    private const float HeaderHeight = 100f;
    private const float ControlsRowHeight = 88f;
    private const float HorizontalPadding = 32f;
    private const float SandWidthFraction = 0.96f;
    private const float SandMaxSize = 720f;
    private const float SandMinSize = 300f;
    private const float TopSectionGap = 12f;
    private const float ConveyorHeight = 220f;
    private const float ConveyorImageMargin = 28f;
    private const float HintWidth = 740f;
    private const float HintHeight = 148f;
    private const float BoosterBarHeight = 110f;
    private const float SectionGap = 20f;
    private const float BottomPadding = 52f;

    [SerializeField] private float bucketCellSize = 96f;
    [SerializeField] private float bucketSpacing = 10f;

    private CanvasScaler canvasScaler;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public static MobilePortraitLayout Instance { get; private set; }
    public static float BucketCellSize => Instance != null ? Instance.bucketCellSize : 96f;
    public static float BucketSpacing => Instance != null ? Instance.bucketSpacing : 10f;

    private void Awake()
    {
        Instance = this;
        canvasScaler = GetComponent<CanvasScaler>();
        ConfigureCanvasScaler();
        if (!YoutubePlatform.IsPlayer) LockPortraitOrientation();
        ApplyLayout();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        ApplyLayout();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y)
        {
            ApplyLayout();
        }
    }

    public void ApplyLayout()
    {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        SafeMargins margins = GetSafeMargins();
        float usableWidth = Screen.width / Mathf.Max(0.001f, GetCanvasScaleFactor()) - margins.left - margins.right;
        float bottomBase = margins.bottom + BottomPadding;

        LayoutHeader(margins);
        LayoutResetButton(margins);
        LayoutControlButton("LevelSelectButton", margins, 0);
        LayoutControlButton("RewardedSkipLevelBtn", margins, 1);
        LayoutControlButton("SkipLevelBtn", margins, 2);
        RectTransform coins = FindRect("CoinHud");
        if (coins != null) coins.anchoredPosition = new Vector2(-margins.right - 140f, -margins.top - 56f);
        // Fit the completion card on narrow phones and short editor game views.
        Transform completion = transform.Find("EconomyLevelCompletePanel/Card");
        if (completion != null)
        {
            float availableHeight = ReferenceHeight - margins.top - margins.bottom - 48f;
            float scale = Mathf.Min(1f, (usableWidth - 48f) / 620f, availableHeight / 660f);
            completion.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }
        float boosterReserve = LayoutBoosterBar(bottomBase, usableWidth);
        // Hints draw on top of content and must not reserve vertical space (no UI shrink).
        LayoutHintOverlay(bottomBase + boosterReserve, usableWidth);

        float hintTop = bottomBase + boosterReserve;
        float contentTop = ReferenceHeight - margins.top - HeaderHeight - ControlsRowHeight - TopSectionGap;
        float middleStackHeight = ConveyorHeight + SectionGap + EstimateBucketAreaHeight() + 42f;
        float maxSandSize = contentTop - hintTop - SectionGap - middleStackHeight;
        float sandSize = Mathf.Clamp(
            Mathf.Min(usableWidth * SandWidthFraction, maxSandSize, SandMaxSize),
            SandMinSize,
            SandMaxSize);

        float sandCenterY = contentTop - sandSize * 0.5f;
        LayoutSandPanel(sandCenterY, sandSize);

        float belowSand = contentTop - sandSize - SectionGap;
        float conveyorWidth = sandSize + ConveyorImageMargin;
        float conveyorCenterY = belowSand - ConveyorHeight * 0.5f;
        LayoutConveyor(conveyorCenterY, conveyorWidth);
        ConfigureConveyorGeometry(conveyorWidth);

        float bucketAreaHeight = EstimateBucketAreaHeight();
        float bucketCenterY = belowSand - ConveyorHeight - SectionGap - bucketAreaHeight * 0.5f;
        LayoutBucketInventory(bucketCenterY, usableWidth);
        // A tip above the boosters can hide the entire bottom bucket row.
        // Keep the board usable by placing overlapping tips over the painting instead.
        RectTransform hint = FindRect("Hint");
        if (hint != null)
        {
            float bucketBottom = bucketCenterY - bucketAreaHeight * .5f;
            float hintHeight = hint.sizeDelta.y > 1f ? hint.sizeDelta.y : HintHeight;
            if (bottomBase + boosterReserve + hintHeight + 8f > bucketBottom - 8f)
                LayoutHintOverlay(contentTop - sandSize + 12f, usableWidth);
        }
        // Debug LevelSelect panel is replaced by the in-game LevelSelectUI menu button.

        BucketInventoryUI inventoryUI = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUI != null)
        {
            inventoryUI.ApplyMobileLayout();
        }

        SandConveyorMapper mapper = FindObjectOfType<SandConveyorMapper>();
        if (mapper != null)
        {
            mapper.EnsureReferences();
        }
    }

    private void ConfigureCanvasScaler()
    {
        if (canvasScaler == null)
        {
            return;
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        // Layout coordinates are in a fixed 1920-unit height, including on tall phones.
        canvasScaler.matchWidthOrHeight = 1f;
    }

    private static void LockPortraitOrientation()
    {
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;

        if (Screen.orientation != ScreenOrientation.Portrait
            && Screen.orientation != ScreenOrientation.PortraitUpsideDown)
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }

    private static void LayoutHeader(SafeMargins margins)
    {
        RectTransform header = FindRect("Header");
        if (header == null)
        {
            return;
        }

        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, HeaderHeight);
        header.anchoredPosition = new Vector2(0f, -margins.top);
    }

    private static void LayoutResetButton(SafeMargins margins)
    {
        RectTransform resetButton = FindRect("ResetButton");
        if (resetButton == null)
        {
            return;
        }

        resetButton.anchorMin = new Vector2(0f, 1f);
        resetButton.anchorMax = new Vector2(0f, 1f);
        resetButton.pivot = new Vector2(0f, 1f);
        resetButton.sizeDelta = new Vector2(72f, 72f);
        resetButton.anchoredPosition = new Vector2(margins.left + 16f, -10f);
    }

    private static void LayoutControlButton(string name, SafeMargins margins, int slot)
    {
        RectTransform button = FindRect(name);
        if (button == null) return;
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-margins.right - 32f - slot * 88f, -margins.top - 104f);
        button.sizeDelta = new Vector2(72f, 72f);
    }

    private float LayoutBoosterBar(float bottomBase, float usableWidth)
    {
        RectTransform boosterBar = FindRect("BoosterBar");
        if (boosterBar == null || !boosterBar.gameObject.activeInHierarchy)
        {
            return 0f;
        }

        float height = boosterBar.sizeDelta.y > 1f ? boosterBar.sizeDelta.y : BoosterBarHeight;
        float width = Mathf.Min(
            boosterBar.sizeDelta.x > 1f ? boosterBar.sizeDelta.x : 520f,
            usableWidth - 40f);
        float centerY = bottomBase + height * 0.5f;
        SetCenterAnchor(boosterBar, centerY / ReferenceHeight, new Vector2(width, height));
        return height + SectionGap;
    }

    private void LayoutHintOverlay(float boosterTop, float usableWidth)
    {
        RectTransform hint = FindRect("Hint");
        if (hint == null)
        {
            return;
        }

        float height = hint.sizeDelta.y > 1f ? hint.sizeDelta.y : HintHeight;
        float width = hint.sizeDelta.x > 1f
            ? Mathf.Min(hint.sizeDelta.x, usableWidth - 80f)
            : Mathf.Min(HintWidth, usableWidth - 80f);
        // Sit just above the booster bar so the tip never covers boosters.
        float centerY = boosterTop + height * 0.5f + 8f;
        SetCenterAnchor(hint, centerY / ReferenceHeight, new Vector2(width, height));
        RectTransform boosterBar = FindRect("BoosterBar");
        if (boosterBar != null && hint.parent == boosterBar.parent)
        {
            hint.SetAsLastSibling();
            boosterBar.SetSiblingIndex(Mathf.Max(0, hint.GetSiblingIndex() - 1));
        }
    }

    private void LayoutBucketInventory(float centerY, float usableWidth)
    {
        RectTransform inventory = FindRect("BucketInventory");
        if (inventory == null)
        {
            return;
        }

        int columns = LevelData.BucketLayoutColumns;
        float width = columns * bucketCellSize + (columns - 1) * bucketSpacing;
        float height = EstimateBucketAreaHeight();
        SetCenterAnchor(inventory, centerY / ReferenceHeight, new Vector2(width, height));

        GridLayoutGroup grid = inventory.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(bucketCellSize, bucketCellSize);
            grid.spacing = new Vector2(bucketSpacing, bucketSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }
    }

    private void LayoutConveyor(float centerY, float width)
    {
        RectTransform conveyor = FindRect("ConveyorPanel");
        if (conveyor == null)
        {
            return;
        }

        SetCenterAnchor(conveyor, centerY / ReferenceHeight, new Vector2(width, ConveyorHeight));
    }

    private void ConfigureConveyorGeometry(float conveyorWidth)
    {
        ConveyorBelt.ConfigureGeometry(conveyorWidth, bucketCellSize);

        ConveyorBeltView beltView = FindObjectOfType<ConveyorBeltView>();
        if (beltView != null)
        {
            beltView.RefreshGeometry();
        }
    }

    private static void LayoutSandPanel(float centerY, float sandSize)
    {
        RectTransform sandPanel = FindRect("SandPanel");
        if (sandPanel == null)
        {
            return;
        }

        SetCenterAnchor(sandPanel, centerY / ReferenceHeight, new Vector2(sandSize, sandSize));
        RectTransform painting = FindRect("SandImage");
        if (painting != null)
        {
            painting.anchorMin = Vector2.zero;
            painting.anchorMax = Vector2.one;
            painting.offsetMin = new Vector2(12, 14);
            painting.offsetMax = new Vector2(-12, -12);
        }
    }

    private float EstimateBucketAreaHeight()
    {
        int columns = LevelData.BucketLayoutColumns;
        int rows = LevelData.BucketLayoutRows;
        return rows * bucketCellSize + (rows - 1) * bucketSpacing;
    }

    private static void HideDebugLevelSelect()
    {
        // Retained for callers; LevelSelectUI now owns the player-facing menu.
    }

    private static void SetCenterAnchor(RectTransform rect, float anchorY, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private static RectTransform FindRect(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj != null ? obj.GetComponent<RectTransform>() : null;
    }

    private SafeMargins GetSafeMargins()
    {
        Rect safe = Screen.safeArea;
        float scaleFactor = GetCanvasScaleFactor();

        return new SafeMargins
        {
            left = safe.xMin / scaleFactor,
            right = (Screen.width - safe.xMax) / scaleFactor,
            bottom = safe.yMin / scaleFactor,
            top = (Screen.height - safe.yMax) / scaleFactor
        };
    }

    private float GetCanvasScaleFactor()
    {
        if (canvasScaler == null)
        {
            return 1f;
        }

        float logWidth = Mathf.Log(Screen.width / ReferenceWidth, 2f);
        float logHeight = Mathf.Log(Screen.height / ReferenceHeight, 2f);
        float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, canvasScaler.matchWidthOrHeight);
        return Mathf.Pow(2f, logWeightedAverage);
    }

    private struct SafeMargins
    {
        public float left;
        public float right;
        public float bottom;
        public float top;
    }
}
