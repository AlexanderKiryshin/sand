using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LevelDatabase levelDatabase;

    private Button openButton;
    private CampaignGallery menu;

    private void Start()
    {
        EnsureBuilt();
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
        rect.anchoredPosition = new Vector2(-30f, -170f);
        rect.sizeDelta = new Vector2(168f, 56f);
        Image bg = go.GetComponent<Image>();
        bg.sprite = HintUiSprites.RoundedPanel;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.15f, 0.37f, 0.37f, 0.96f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(OpenMenu);
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = CampaignText.Get("levels");
        label.fontSize = 24;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.99f, 0.91f);
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
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
