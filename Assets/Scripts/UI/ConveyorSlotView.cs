using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConveyorSlotView : MonoBehaviour
{
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image arrowImage;
    [SerializeField] private TextMeshProUGUI countLabel;

    private ConveyorSlotState slotState;

    public ConveyorSlotState SlotState { get { return slotState; } }

    public void Initialize(ConveyorSlotState state)
    {
        slotState = state;
        EnsureVisuals();
        Refresh();
    }

    public void Refresh()
    {
        EnsureVisuals();
        if (slotState == null)
        {
            return;
        }

        if (countLabel != null)
        {
            countLabel.text = slotState.RemainingCount.ToString();
        }

        if (arrowImage != null)
        {
            arrowImage.rectTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                ConveyorDirectionUtility.GetArrowRotationDegrees(slotState.Direction));
        }
    }

    private void EnsureVisuals()
    {
        if (bodyImage == null)
        {
            bodyImage = GetComponent<Image>();
            if (bodyImage == null)
            {
                bodyImage = gameObject.AddComponent<Image>();
            }

            bodyImage.color = new Color32(96, 96, 112, 255);
            bodyImage.raycastTarget = false;
        }

        EnsureArrow();
        EnsureCountLabel();
    }

    private void EnsureArrow()
    {
        if (arrowImage != null)
        {
            return;
        }

        Transform existing = transform.Find("DirectionArrow");
        GameObject arrowObject;
        if (existing != null)
        {
            arrowObject = existing.gameObject;
        }
        else
        {
            arrowObject = new GameObject("DirectionArrow", typeof(RectTransform));
            arrowObject.transform.SetParent(transform, false);
        }

        RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(18f, 18f);
        arrowRect.anchoredPosition = Vector2.zero;

        arrowImage = arrowObject.GetComponent<Image>();
        if (arrowImage == null)
        {
            arrowImage = arrowObject.AddComponent<Image>();
        }

        arrowImage.color = new Color32(230, 230, 240, 255);
        arrowImage.raycastTarget = false;
        CreateArrowSprite();
    }

    private void EnsureCountLabel()
    {
        if (countLabel != null)
        {
            return;
        }

        Transform existing = transform.Find("CountLabel");
        GameObject labelObject;
        if (existing != null)
        {
            labelObject = existing.gameObject;
            countLabel = labelObject.GetComponent<TextMeshProUGUI>();
            return;
        }

        labelObject = new GameObject("CountLabel", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(36f, 20f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);

        countLabel = labelObject.AddComponent<TextMeshProUGUI>();
        countLabel.alignment = TextAlignmentOptions.Center;
        countLabel.fontSize = 14f;
        countLabel.fontStyle = FontStyles.Bold;
        countLabel.color = Color.white;
        countLabel.raycastTarget = false;
    }

    private void CreateArrowSprite()
    {
        if (arrowImage.sprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 fill = new Color32(255, 255, 255, 255);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool inside = y >= 4 && y <= 11 && x >= 6 && x <= 9;
                bool tip = y <= 3 && x >= 8 - y && x <= 7 + y;
                texture.SetPixel(x, y, inside || tip ? fill : clear);
            }
        }

        texture.Apply();
        arrowImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
    }
}
