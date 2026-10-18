using UnityEngine;
using UnityEngine.UI;

public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LevelDatabase levelDatabase;

    private Button openButton;
    private CampaignGallery menu;

    private void Start()
    {
        EnsureBuilt();
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.GetComponent<ThemeCustomizationMenu>() == null)
            canvas.gameObject.AddComponent<ThemeCustomizationMenu>();
        if (canvas != null && canvas.GetComponent<NatureBackgroundOffer>() == null)
            canvas.gameObject.AddComponent<NatureBackgroundOffer>();
    }

    private void EnsureBuilt()
    {
        if (openButton != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        openButton = CreateOpenButton(canvas.transform);
    }

    private Button CreateOpenButton(Transform canvas)
    {
        GameObject go = new GameObject("LevelSelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-32f, -104f);
        rect.sizeDelta = new Vector2(72f, 72f);
        Image bg = go.GetComponent<Image>();
        bg.sprite = HintUiSprites.RoundedPanel;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.15f, 0.37f, 0.37f, 0.96f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(OpenMenu);
        var iconGo = new GameObject("LevelsIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        Image icon = iconGo.GetComponent<Image>();
        icon.sprite = GameUiSymbols.Get(GameUiSymbol.Levels);
        icon.color = new Color(1f, .99f, .91f);
        icon.raycastTarget = false;
        icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
        MobilePortraitLayout.Instance?.ApplyLayout();
        return button;
    }

    public void OpenMenu()
    {
        EnsureBuilt();
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (levelDatabase == null)
        {
            LevelDatabase[] databases = Resources.FindObjectsOfTypeAll<LevelDatabase>();
            if (databases.Length > 0) levelDatabase = databases[0];
        }

        // Reuse the painting gallery while keeping replay limited to passed levels.
        menu = CampaignGallery.Show(levelDatabase, gameManager, completedLevelsOnly: true);
    }

    public void CloseMenu()
    {
        if (menu != null) menu.Close();
        menu = null;
    }

    private void OnDestroy()
    {
        CloseMenu();
        if (openButton != null) Destroy(openButton.gameObject);
    }
}
