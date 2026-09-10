using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CampaignGallery : MonoBehaviour
{
    private const int PageSize = 20;
    public static bool IsOpen { get; private set; }
    private LevelDatabase database;
    private GameManager manager;
    private RectTransform grid;
    private TextMeshProUGUI pageLabel;
    private int page;
    private ConveyorBelt belt;
    private FallingSandSimulator sand;
    private bool beltWasPaused;
    private bool sandWasPaused;
    private bool completedLevelsOnly;
    private bool pauseCaptured;
    private float previousTimeScale;
    private Button previousButton;
    private Button nextButton;
    private readonly LevelPreviewCache previews = new LevelPreviewCache();

    public static CampaignGallery Show(LevelDatabase database, GameManager manager, bool completedLevelsOnly = false)
    {
        CampaignGallery existing = FindObjectOfType<CampaignGallery>();
        if (existing != null) return existing;
        if (database == null || database.LevelCount == 0 || manager == null) return null;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;
        GameObject root = new GameObject("CampaignCollection", typeof(RectTransform), typeof(Image), typeof(CampaignGallery));
        root.transform.SetParent(canvas.transform, false);
        Image dimmer = root.GetComponent<Image>();
        dimmer.color = new Color(0.06f, 0.14f, 0.18f, 0.98f);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        CampaignGallery gallery = root.GetComponent<CampaignGallery>();
        Canvas overlay = root.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 100;
        root.AddComponent<GraphicRaycaster>();
        IsOpen = true;
        gallery.database = database;
        gallery.manager = manager;
        gallery.completedLevelsOnly = completedLevelsOnly;
        gallery.page = Mathf.Clamp(manager.CurrentLevelIndex / PageSize, 0, (database.LevelCount - 1) / PageSize);
        gallery.Build(canvas);
        return gallery;
    }

    private void Build(Canvas canvas)
    {
        belt = FindObjectOfType<ConveyorBelt>();
        sand = FindObjectOfType<FallingSandSimulator>();
        beltWasPaused = belt != null && belt.IsMovementPaused;
        sandWasPaused = sand != null && sand.IsSimulationPaused;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pauseCaptured = true;
        if (belt != null) belt.SetMovementPaused(true);
        if (sand != null) sand.SetSimulationPaused(true);
        float width = Mathf.Min(880, ((RectTransform)canvas.transform).rect.width - 60);
        RectTransform panel = Rect("CollectionPanel", transform, new Vector2(width, 1200), Vector2.zero);
        panel.localScale = Vector3.one * Mathf.Clamp01((((RectTransform)canvas.transform).rect.height - 60) / 1200f);
        TextMeshProUGUI title = Label(panel, "Title", completedLevelsOnly ? CampaignText.Get("level_select")
            : manager.IsLevelCompleted(database.LevelCount - 1) ? CampaignText.Get("collection") : CampaignText.Get("gallery"), 40);
        title.rectTransform.anchoredPosition = new Vector2(0, 535);
        title.rectTransform.sizeDelta = new Vector2(width, 90);
        grid = Rect("Paintings", panel, new Vector2(width, 850), new Vector2(0, 20));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2((width - 48) / 5, 192);
        layout.spacing = new Vector2(12, 16);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        previousButton = ButtonAt(panel, "Previous", "<", new Vector2(-width * 0.32f, -460), () => { page = Mathf.Max(0, page - 1); RenderPage(); });
        nextButton = ButtonAt(panel, "Next", ">", new Vector2(width * 0.32f, -460), () => { page = Mathf.Min((database.LevelCount - 1) / PageSize, page + 1); RenderPage(); });
        pageLabel = Label(panel, "Page", "", 30);
        pageLabel.rectTransform.anchoredPosition = new Vector2(0, -460);
        ButtonAt(panel, "Close", "×", new Vector2(0, -565), Close);
        RenderPage();
    }

    private void RenderPage()
    {
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            grid.GetChild(i).gameObject.SetActive(false);
            Destroy(grid.GetChild(i).gameObject);
        }
        previews.Clear();
        int pageCount = Mathf.CeilToInt(database.LevelCount / (float)PageSize);
        pageLabel.text = (page + 1) + " / " + pageCount;
        previousButton.interactable = page > 0;
        nextButton.interactable = page < pageCount - 1;
        for (int index = page * PageSize; index < Mathf.Min(database.LevelCount, (page + 1) * PageSize); index++)
        {
            int selected = index;
            bool completed = manager.IsLevelCompleted(index);
            bool unlocked = completedLevelsOnly ? completed : manager.IsLevelUnlocked(index);
            RectTransform card = Rect("Level_" + (index + 1), grid, Vector2.zero, Vector2.zero);
            Image frame = card.gameObject.AddComponent<Image>();
            frame.sprite = HintUiSprites.RoundedPanel;
            frame.type = Image.Type.Sliced;
            frame.color = unlocked ? new Color(0.96f, 0.94f, 0.84f) : new Color(0.28f, 0.37f, 0.4f);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.interactable = unlocked;
            button.onClick.AddListener(() => { Close(); manager.LoadLevel(selected); });
            RectTransform art = Rect("Preview", card, Vector2.zero, Vector2.zero);
            art.anchorMin = new Vector2(0, 0.22f);
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(6, 0);
            art.offsetMax = new Vector2(-6, -6);
            RectTransform painting = Rect("Painting", art, Vector2.zero, Vector2.zero);
            RawImage image = painting.gameObject.AddComponent<RawImage>();
            LevelData level = database.GetLevel(index);
            image.texture = previews.Get(level);
            image.enabled = image.texture != null;
            AspectRatioFitter aspect = painting.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 1f;
            image.color = unlocked ? Color.white : new Color(0.35f, 0.4f, 0.43f);
            image.raycastTarget = false;
            TextMeshProUGUI label = Label(card, "Number", (index + 1).ToString() + (completed ? " •" : ""), 27);
            label.color = unlocked ? new Color(0.13f, 0.23f, 0.27f) : Color.white;
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(1, 0.22f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        previews.Clear();
        if (pauseCaptured)
        {
            if (belt != null) belt.SetMovementPaused(beltWasPaused);
            if (sand != null) sand.SetSimulationPaused(sandWasPaused);
            if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previousTimeScale;
            pauseCaptured = false;
        }
        IsOpen = false;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, float size)
    {
        var label = Rect(name, parent, new Vector2(180, 70), Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button ButtonAt(Transform parent, string name, string text, Vector2 position, UnityEngine.Events.UnityAction click)
    {
        RectTransform rect = Rect(name, parent, new Vector2(150, 72), position);
        Image background = rect.gameObject.AddComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.23f, 0.54f, 0.57f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(click);
        Label(rect, "Label", text, 38);
        return button;
    }
}
