using TMPro;
using System.Collections.Generic;
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
    private readonly LevelPreviewCache collectionPreviews = new LevelPreviewCache();
    private bool winterOnly;
    private readonly List<int> visibleLevels = new List<int>();
    private Button allTab, winterTab;

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
        gallery.winterOnly = manager.IsSeasonal(manager.CurrentLevelIndex);
        gallery.page = (manager.GetDisplayNumber(manager.CurrentLevelIndex) - 1) / PageSize;
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
        RectTransform panel = Rect("CollectionPanel", transform, new Vector2(width, 1400), Vector2.zero);
        panel.localScale = Vector3.one * Mathf.Clamp01((((RectTransform)canvas.transform).rect.height - 60) / 1400f);
        TextMeshProUGUI title = Label(panel, "Title", completedLevelsOnly ? CampaignText.Get("level_select")
            : manager.IsLevelCompleted(database.LevelCount - 1) ? CampaignText.Get("collection") : CampaignText.Get("gallery"), 40);
        title.rectTransform.anchoredPosition = new Vector2(0, 645);
        title.rectTransform.sizeDelta = new Vector2(width, 90);
        grid = Rect("Paintings", panel, new Vector2(width, 850), new Vector2(0, -50));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2((width - 48) / 5, 192);
        layout.spacing = new Vector2(12, 16);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        previousButton = ButtonAt(panel, "Previous", "<", new Vector2(-width * 0.32f, -530), () => { page = Mathf.Max(0, page - 1); RenderPage(); });
        nextButton = ButtonAt(panel, "Next", ">", new Vector2(width * 0.32f, -530), () => { page = Mathf.Min((database.LevelCount - 1) / PageSize, page + 1); RenderPage(); });
        float collectionWidth = (width - 20) / 2;
        allTab = CollectionCard(panel, false, collectionWidth, -(width + 20) / 4);
        winterTab = CollectionCard(panel, true, collectionWidth, (width + 20) / 4);
        pageLabel = Label(panel, "Page", "", 30);
        pageLabel.rectTransform.anchoredPosition = new Vector2(0, -530);
        ButtonAt(panel, "Close", "×", new Vector2(0, -635), Close);
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
        visibleLevels.Clear();
        for (int i = 0; i < database.LevelCount; i++)
            if (winterOnly == manager.IsSeasonal(i)) visibleLevels.Add(i);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleLevels.Count / (float)PageSize));
        page = Mathf.Clamp(page, 0, pageCount - 1);
        UpdateCollectionCard(allTab, !winterOnly);
        UpdateCollectionCard(winterTab, winterOnly);
        pageLabel.text = (page + 1) + " / " + pageCount;
        previousButton.interactable = page > 0;
        nextButton.interactable = page < pageCount - 1;
        for (int entry = page * PageSize; entry < Mathf.Min(visibleLevels.Count, (page + 1) * PageSize); entry++)
        {
            int index = visibleLevels[entry];
            int selected = index;
            bool completed = manager.IsLevelCompleted(index);
            bool unlocked = LevelCheats.CanSelectLevel(completedLevelsOnly, completed, manager.GetDisplayNumber(index) == 1, manager.IsLevelUnlocked(index));
            RectTransform card = Rect("Level_" + (index + 1), grid, Vector2.zero, Vector2.zero);
            Image frame = card.gameObject.AddComponent<Image>();
            frame.sprite = HintUiSprites.RoundedPanel;
            frame.type = Image.Type.Sliced;
            frame.color = unlocked ? new Color(0.96f, 0.94f, 0.84f) : new Color(0.75f, 0.81f, 0.80f);
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
            image.color = Color.white;
            image.raycastTarget = false;
            string caption = manager.GetDisplayNumber(index).ToString();
            TextMeshProUGUI label = Label(card, "Number", caption + (completed ? " •" : ""), 27);
            label.color = new Color(0.13f, 0.23f, 0.27f);
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(1, 0.22f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            if (!unlocked)
            {
                RectTransform lockRoot = Rect("Locked", card, new Vector2(34, 34), Vector2.zero);
                lockRoot.anchorMin = lockRoot.anchorMax = new Vector2(1, 0);
                lockRoot.anchoredPosition = new Vector2(-21, 21);
                Image plate = lockRoot.gameObject.AddComponent<Image>(); plate.sprite = HintUiSprites.RoundedPanel;
                plate.color = new Color(.08f, .20f, .23f); plate.raycastTarget = false;
                RectTransform icon = Rect("LockIcon", lockRoot, new Vector2(23, 23), Vector2.zero);
                Image lockImage = icon.gameObject.AddComponent<Image>(); lockImage.sprite = GameUiSymbols.Get(GameUiSymbol.Lock);
                lockImage.raycastTarget = false;
            }
        }
    }

    public void RefreshProgress() => RenderPage();

    private Button CollectionCard(Transform parent, bool winter, float width, float x)
    {
        RectTransform card = Rect(winter ? "WinterLevels" : "AllLevels", parent, new Vector2(width, 184), new Vector2(x, 500));
        Image frame = card.gameObject.AddComponent<Image>();
        frame.sprite = HintUiSprites.RoundedPanel;
        frame.type = Image.Type.Sliced;
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => { winterOnly = winter; page = 0; RenderPage(); });

        var levels = new List<int>();
        for (int i = 0; i < database.LevelCount; i++)
            if (manager.IsSeasonal(i) == winter) levels.Add(i);
        // Keep the regular pack's original fox, tropical coast and courtyard covers
        // at their new level positions (11, 78 and 117).
        int[] samples = winter ? new[] { 0, 6, 8 } : new[] { 10, 77, 116 };
        float paintingWidth = (width - 40) / 3;
        for (int i = 0; i < samples.Length && levels.Count > 0; i++)
        {
            RectTransform art = Rect("CollectionPainting_" + i, card, new Vector2(paintingWidth, 114),
                new Vector2((i - 1) * (paintingWidth + 8), 24));
            Image mat = art.gameObject.AddComponent<Image>();
            mat.sprite = HintUiSprites.RoundedPanel;
            mat.type = Image.Type.Sliced;
            mat.color = new Color(1, 1, 1, .9f);
            mat.raycastTarget = false;
            RectTransform inset = Rect("Inset", art, Vector2.zero, Vector2.zero);
            inset.anchorMin = Vector2.zero;
            inset.anchorMax = Vector2.one;
            inset.offsetMin = new Vector2(5, 5);
            inset.offsetMax = new Vector2(-5, -5);
            RectTransform painting = Rect("Painting", inset, Vector2.zero, Vector2.zero);
            RawImage image = painting.gameObject.AddComponent<RawImage>();
            image.texture = collectionPreviews.Get(database.GetLevel(levels[Mathf.Min(samples[i], levels.Count - 1)]));
            image.enabled = image.texture != null;
            image.raycastTarget = false;
            AspectRatioFitter aspect = painting.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = image.texture == null ? 1 : image.texture.width / (float)image.texture.height;
        }
        TextMeshProUGUI title = Label(card, "CollectionName", CampaignText.Get(winter ? "winter_collection" : "all_levels"), 30);
        title.rectTransform.anchoredPosition = new Vector2(0, -49);
        title.rectTransform.sizeDelta = new Vector2(width - 24, 36);
        title.color = new Color(.10f, .25f, .28f);
        TextMeshProUGUI count = Label(card, "CollectionCount", string.Format(CampaignText.Get("collection_level_count"), levels.Count), 20);
        count.rectTransform.anchoredPosition = new Vector2(0, -77);
        count.rectTransform.sizeDelta = new Vector2(width - 24, 28);
        count.color = new Color(.25f, .40f, .42f);
        RectTransform selection = Rect("SelectedOutline", card, Vector2.zero, Vector2.zero);
        selection.anchorMin = Vector2.zero;
        selection.anchorMax = Vector2.one;
        selection.offsetMin = selection.offsetMax = Vector2.zero;
        Image outline = selection.gameObject.AddComponent<Image>();
        outline.sprite = HintUiSprites.RoundedOutline;
        outline.type = Image.Type.Sliced;
        outline.color = new Color(1f, .76f, .24f);
        outline.raycastTarget = false;
        return button;
    }

    private static void UpdateCollectionCard(Button button, bool selected)
    {
        button.interactable = !selected;
        button.GetComponent<Image>().color = selected ? new Color(1f, .96f, .83f) : new Color(.80f, .88f, .89f);
        button.transform.Find("SelectedOutline").gameObject.SetActive(selected);
    }

    public static bool IsWinter(LevelData level)
    {
        if (level == null) return false;
        return level.seasonalNumber > 0;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        previews.Clear();
        collectionPreviews.Clear();
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
