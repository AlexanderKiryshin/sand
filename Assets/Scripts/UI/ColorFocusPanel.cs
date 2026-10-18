using UnityEngine;
using UnityEngine.UI;

public sealed class ColorFocusPanel : MonoBehaviour
{
    public event System.Action<byte> SelectionChanged;
    public RectTransform GetSwatchRect(byte index) => frames != null && index < frames.Length ? frames[index].rectTransform : null;
    public static byte SelectedColor { get; private set; }
    public byte RedColor { get; private set; }
    private RectTransform strip;
    private Image[] frames;
    private CanvasGroup[] swatchVisibility;
    private SandRenderer renderer;
    public void Bind(LevelData level, SandRenderer view)
    {
        renderer = view;
        RedColor = FindRedColor(level.palette);
        SelectedColor = 0;
        renderer.SelectColor(0);
        if (strip != null) { strip.gameObject.SetActive(false); Destroy(strip.gameObject); }
        strip = Rect("ColorFocus", transform);
        strip.anchorMin = new Vector2(0, 0);
        strip.anchorMax = new Vector2(0, 1);
        strip.pivot = new Vector2(1, .5f);
        strip.sizeDelta = new Vector2(156, -24);
        strip.anchoredPosition = new Vector2(-14, 0);
        strip.gameObject.AddComponent<RectMask2D>();
        var backing=Rect("PaletteBacking",strip);
        backing.anchorMin=backing.anchorMax=new Vector2(.5f,1);backing.pivot=new Vector2(.5f,1);
        backing.sizeDelta=new Vector2(156,Mathf.Min(strip.rect.height,Mathf.CeilToInt((level.palette.Length+1)/2f)*80+4));
        backing.anchoredPosition=Vector2.zero;
        var backingImage=backing.gameObject.AddComponent<Image>();backingImage.sprite=HintUiSprites.RoundedPanel;
        backingImage.type=Image.Type.Sliced;backingImage.color=new Color(.96f,.97f,.91f,.34f);backingImage.raycastTarget=false;
        var content = Rect("Swatches", strip);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(0, Mathf.CeilToInt((level.palette.Length + 1) / 2f) * 80);
        var layout = content.gameObject.AddComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.cellSize = new Vector2(72, 72);
        layout.spacing = new Vector2(8, 8);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        var scroll = strip.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = strip; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        frames = new Image[level.palette.Length + 1];
        swatchVisibility = new CanvasGroup[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            int index = i;
            RectTransform square = Rect(i == 0 ? "AllColors" : "Color_" + i, content);
            swatchVisibility[i] = square.gameObject.AddComponent<CanvasGroup>();
            frames[i] = square.gameObject.AddComponent<Image>(); frames[i].sprite = HintUiSprites.RoundedPanel;
            var button = square.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select((byte)index));
            var fill = Rect("Fill", square); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.one * 3; fill.offsetMax = Vector2.one * -3;
            if (i == 0)
            {
                for (int quadrant = 0; quadrant < 4; quadrant++)
                {
                    var part = Rect("All_" + quadrant, fill);
                    part.anchorMin = new Vector2(quadrant % 2 * .5f, quadrant / 2 * .5f);
                    part.anchorMax = part.anchorMin + Vector2.one * .5f;
                    part.offsetMin = part.offsetMax = Vector2.zero;
                    var image = part.gameObject.AddComponent<Image>();
                    image.color = level.palette[quadrant * level.palette.Length / 4]; image.raycastTarget = false;
                }
            }
            else { var image = fill.gameObject.AddComponent<Image>(); image.color = level.palette[i - 1]; image.raycastTarget = false; }
        }
        Select(0);
        RefreshSwatchAvailability();
    }
    private void LateUpdate() => RefreshSwatchAvailability();
    private void RefreshSwatchAvailability()
    {
        if (renderer == null || swatchVisibility == null) return;
        for (int i = 1; i < swatchVisibility.Length; i++)
        {
            float alpha = renderer.HasRemainingColor((byte)i) ? 1f : .25f;
            if (swatchVisibility[i].alpha != alpha) swatchVisibility[i].alpha = alpha;
        }
    }
    public void Select(byte index)
    {
        if (frames == null || index >= frames.Length) return;
        SelectedColor = index;
        renderer.SelectColor(index);
        for (int i = 0; i < frames.Length; i++) frames[i].color = i == index ? Color.white : Color.clear;
        foreach (var bucket in FindObjectsOfType<BucketColorFocus>()) bucket.Refresh();
        SelectionChanged?.Invoke(index);
    }
    public static byte FindRedColor(Color32[] palette)
    {
        int bestDistance = int.MaxValue;
        byte best = 0;
        if (palette == null) return best;
        for (int i = 0; i < palette.Length; i++)
        {
            Color32 color = palette[i];
            if (color.a == 0) continue;
            int distance = (255 - color.r) * (255 - color.r) + color.g * color.g + color.b * color.b;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = (byte)(i + 1);
        }
        return best;
    }
    public static Color32 FilterColor(Color32 color, byte index, byte selected)
        => selected == 0 || index == selected || index == 0 ? color
            : new Color32(color.r, color.g, color.b, (byte)(color.a * .25f));
    private static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }
}
