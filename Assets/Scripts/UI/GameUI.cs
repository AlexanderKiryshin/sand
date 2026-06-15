using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI conveyorLabel;
    [SerializeField] private TextMeshProUGUI hintLabel;
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button nextLevelButton;

    private GameManager gameManager;
    private ConveyorBelt conveyorBelt;

    private void Awake()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(HandleResetClicked);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
        }

        HideLevelComplete();
    }

    public void Bind(GameManager manager, ConveyorBelt conveyor, LevelData level)
    {
        gameManager = manager;
        conveyorBelt = conveyor;

        if (levelLabel != null)
        {
            levelLabel.text = "Level " + (manager.CurrentLevelIndex + 1);
        }

        if (hintLabel != null)
        {
            hintLabel.text = "Tap a bucket of matching color to collect sand";
        }

        RefreshConveyor();
        HideLevelComplete();
    }

    public void RefreshConveyor()
    {
        if (conveyorLabel == null || conveyorBelt == null)
        {
            return;
        }

        conveyorLabel.text = conveyorBelt.Entries.Count + "/" + ConveyorBelt.MaxOnBelt;
    }

    public void ShowLevelComplete()
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }
    }

    public void HideLevelComplete()
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(false);
        }
    }

    private void HandleResetClicked()
    {
        if (gameManager != null)
        {
            gameManager.RestartLevel();
        }
    }

    private void HandleNextLevelClicked()
    {
        if (gameManager != null)
        {
            gameManager.LoadNextLevel();
        }
    }
}
