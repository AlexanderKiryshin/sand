using System;
using UnityEngine;
using UnityEngine.UI;

public class BucketView : MonoBehaviour
{
    [SerializeField] private Image bucketImage;
    [SerializeField] private Button button;

    private Bucket bucket;
    private Action<Bucket> onClick;
    private Color32 baseColor;

    public Bucket Bucket { get { return bucket; } }

    public void Initialize(Bucket targetBucket, Color32 color, Action<Bucket> clickHandler)
    {
        bucket = targetBucket;
        onClick = clickHandler;
        baseColor = SandDisplayColors.FromPalette(color);

        if (bucketImage != null)
        {
            bucketImage.color = baseColor;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
            button.colors = colors;
        }
    }

    public void SetSelectable(bool selectable)
    {
        if (button != null)
        {
            button.interactable = selectable;
        }

        if (bucketImage != null)
        {
            if (selectable)
            {
                bucketImage.color = baseColor;
            }
            else
            {
                Color dimmed = baseColor;
                dimmed.a = 160;
                bucketImage.color = dimmed;
            }
        }
    }

    private void HandleClick()
    {
        if (onClick != null && bucket != null)
        {
            onClick(bucket);
        }
    }
}
