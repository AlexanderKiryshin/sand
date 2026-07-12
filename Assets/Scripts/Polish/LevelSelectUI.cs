using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private Button levelButtonPrefab;

    private void Start()
    {
        if (buttonContainer == null || levelDatabase == null || gameManager == null)
        {
            BuildDefaultUI();
        }

        BuildButtons();
    }

    private void BuildDefaultUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject panel = new GameObject("LevelSelect", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(180f, 300f);
        rect.anchoredPosition = new Vector2(-20f, -140f);

        buttonContainer = rect;
    }

    private void BuildButtons()
    {
        if (buttonContainer == null || levelDatabase == null)
        {
            return;
        }

        for (int i = 0; i < levelDatabase.LevelCount; i++)
        {
            int levelIndex = i;
            bool unlocked = gameManager == null || gameManager.IsLevelUnlocked(levelIndex);
            bool completed = gameManager != null && gameManager.IsLevelCompleted(levelIndex);
            Button button = CreateLevelButton(i + 1, unlocked, completed);
            if (!unlocked)
            {
                button.interactable = false;
                continue;
            }

            button.onClick.AddListener(() =>
            {
                if (gameManager != null)
                {
                    gameManager.LoadLevel(levelIndex);
                }
            });
        }
    }

    private Button CreateLevelButton(int levelNumber, bool unlocked, bool completed)
    {
        GameObject buttonObject = new GameObject("LevelButton_" + levelNumber, typeof(RectTransform));
        buttonObject.transform.SetParent(buttonContainer, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140f, 48f);

        Image image = buttonObject.AddComponent<Image>();
        if (!unlocked)
        {
            image.color = new Color(0.35f, 0.35f, 0.38f, 0.85f);
        }
        else if (completed)
        {
            image.color = new Color(0.28f, 0.72f, 0.42f, 1f);
        }
        else
        {
            image.color = new Color(0.35f, 0.55f, 0.85f, 1f);
        }
        Button button = buttonObject.AddComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = "L" + levelNumber;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }
}
