using UnityEngine;
using TMPro;

public class LocalizedTextMeshProUGUI : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TextMeshProUGUI tmpText;

    private void Start()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        UpdateText();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange += UpdateText;
        }
    }

    private void UpdateText()
    {
        if (tmpText == null || LocalizationManager.Instance == null)
        {
            return;
        }

        tmpText.text = LocalizationManager.Instance.GetText(localizationKey);
    }

    public void SetKey(string key)
    {
        localizationKey = key;
        UpdateText();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange -= UpdateText;
        }
    }
}
