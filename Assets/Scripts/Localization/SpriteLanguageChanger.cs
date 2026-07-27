using MirraGames.SDK.Common;
using UnityEngine;
using UnityEngine.UI;

public class SpriteLanguageChanger : MonoBehaviour
{
    [SerializeField] private Sprite engSprite;
    [SerializeField] private Sprite rusSprite;
    [SerializeField] private Sprite turkSprite;
    [SerializeField] private Sprite germanSprite;

    private Image currentImage;

    private void Start()
    {
        currentImage = GetComponent<Image>();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange += UpdateSprite;
        }

        UpdateSprite();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange -= UpdateSprite;
        }
    }

    private void UpdateSprite()
    {
        if (currentImage == null || LocalizationManager.Instance == null)
        {
            return;
        }

        switch (LocalizationManager.Instance.GetCurrentLanguage())
        {
            case LanguageType.Russian:
                currentImage.sprite = rusSprite != null ? rusSprite : engSprite;
                break;
            case LanguageType.Turkish:
                currentImage.sprite = turkSprite != null ? turkSprite : engSprite;
                break;
            case LanguageType.German:
                currentImage.sprite = germanSprite != null ? germanSprite : engSprite;
                break;
            default:
                currentImage.sprite = engSprite;
                break;
        }
    }
}
