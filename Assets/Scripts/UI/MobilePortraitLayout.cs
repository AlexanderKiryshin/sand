using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
[DefaultExecutionOrder(-100)]
public class MobilePortraitLayout : MonoBehaviour
{
    public const float ReferenceWidth = 1080f;
    public const float ReferenceHeight = 1920f;

    private const float HeaderHeight = 100f;
    private const float HorizontalPadding = 32f;
    private const float SandWidthFraction = 0.96f;
    private const float SandMaxSize = 720f;
    private const float SandMinSize = 300f;
    private const float TopSectionGap = 12f;
    private const float ConveyorHeight = 104f;
    private const float ConveyorImageMargin = 28f;
    private const float HintWidth = 740f;
    private const float HintHeight = 148f;
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
        LockPortraitOrientation();
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
        float usableWidth = ReferenceWidth - margins.left - margins.right;
        float bottomBase = margins.bottom + BottomPadding;

        LayoutHeader(margins);
        LayoutResetButton(margins);
        LayoutHint(bottomBase, usableWidth);

        float hintLayoutHeight = GetHintLayoutHeight();
        float hintTop = bottomBase + hintLayoutHeight;
        float contentTop = ReferenceHeight - margins.top - HeaderHeight - TopSectionGap;
        float middleStackHeight = ConveyorHeight + SectionGap + EstimateBucketAreaHeight();
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
        HideDebugLevelSelect();

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
        canvasScaler.matchWidthOrHeight = 0.5f;
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
        resetButton.sizeDelta = new Vector2(140f, 52f);
        resetButton.anchoredPosition = new Vector2(margins.left + 12f, -12f);
    }

    private float GetHintLayoutHeight()
    {
        RectTransform hint = FindRect("Hint");
        if (hint == null || !hint.gameObject.activeInHierarchy)
        {
            return 0f;
        }

        return hint.sizeDelta.y > 1f ? hint.sizeDelta.y : HintHeight;
    }

    private void LayoutHint(float bottomBase, float usableWidth)
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
        float centerY = bottomBase + height * 0.5f;
        SetCenterAnchor(hint, centerY / ReferenceHeight, new Vector2(width, height));
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
    }

    private float EstimateBucketAreaHeight()
    {
        int columns = LevelData.BucketLayoutColumns;
        int rows = 4;
        return rows * bucketCellSize + (rows - 1) * bucketSpacing;
    }

    private static void HideDebugLevelSelect()
    {
        GameObject levelSelect = GameObject.Find("LevelSelect");
        if (levelSelect != null)
        {
            levelSelect.SetActive(false);
        }
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

        float logWidth = Mathf.Log(ReferenceWidth / Screen.width, 2f);
        float logHeight = Mathf.Log(ReferenceHeight / Screen.height, 2f);
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
