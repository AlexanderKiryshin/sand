using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConveyorSlotView : MonoBehaviour
{
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image arrowImage;
    [SerializeField] private TextMeshProUGUI countLabel;

    private ConveyorSlotState slotState;
    private static Sprite directionSprite;

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

            bodyImage.sprite = HintUiSprites.RoundedPanel;
            bodyImage.type = Image.Type.Sliced;
            bodyImage.color = new Color32(63, 109, 116, 255);
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
        arrowRect.sizeDelta = new Vector2(64f, 64f);
        arrowRect.anchoredPosition = new Vector2(-7, -4);

        arrowImage = arrowObject.GetComponent<Image>();
        if (arrowImage == null)
        {
            arrowImage = arrowObject.AddComponent<Image>();
        }

        arrowImage.color = new Color32(255, 223, 126, 255);
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
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(1, 1);
        labelRect.sizeDelta = new Vector2(36f, 36f);
        labelRect.anchoredPosition = new Vector2(-2f, -2f);

        countLabel = labelObject.AddComponent<TextMeshProUGUI>();
        countLabel.alignment = TextAlignmentOptions.Center;
        countLabel.fontSize = 27f;
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

        if (directionSprite != null) { arrowImage.sprite = directionSprite; return; }
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 fill = new Color32(255, 255, 255, 255);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                bool inside = y >= 27 && y <= 57 && x >= 23 && x <= 40;
                bool tip = y >= 5 && y <= 31 && Mathf.Abs(x - 31.5f) <= y - 4;
                texture.SetPixel(x, y, inside || tip ? fill : clear);
            }
        }

        texture.Apply(false, true);
        directionSprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        arrowImage.sprite = directionSprite;
    }
}
