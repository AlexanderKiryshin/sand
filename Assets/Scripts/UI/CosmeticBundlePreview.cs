using UnityEngine;

/// <summary>Draws the exact item from a bundle using the game's existing artwork and meshes.</summary>
public sealed class CosmeticBundlePreview : MonoBehaviour
{
    public CosmeticBundleItem Item { get; private set; }
    private ThemeBackdropGraphic background;
    private RectTransform scene;
    private ConveyorSurfaceGraphic deck;
    private ConveyorHoodGraphic[] hoods;
    public void SetItem(CosmeticBundleItem item, int backgroundTheme = ThemeCatalog.DefaultBackground, int conveyorTheme = 0)
    {
        Item = item;
        if (background == null)
        {
            background = gameObject.AddComponent<ThemeBackdropGraphic>();
            background.FitWholeImage = true; background.raycastTarget = false;
            scene = Child("ItemScene", (RectTransform)transform, new Vector2(600, 280));
            var belt = Child("Conveyor", scene, new Vector2(460, 160));
            belt.anchoredPosition = new Vector2(0, -32);
            deck = belt.gameObject.AddComponent<ConveyorSurfaceGraphic>(); deck.raycastTarget = false;
            hoods = new ConveyorHoodGraphic[2];
            for (int i = 0; i < hoods.Length; i++)
            {
                var hood = Child("Casings_" + i, belt, belt.sizeDelta);
                hoods[i] = hood.gameObject.AddComponent<ConveyorHoodGraphic>(); hoods[i].Initialize(deck, i == 0);
            }
        }
        background.PreviewTheme = item.Part == ThemeCatalog.Part.Background ? item.Theme : backgroundTheme;
        background.FitWholeImage = item.Part == ThemeCatalog.Part.Background;
        background.SetVerticesDirty();
        scene.gameObject.SetActive(item.Part != ThemeCatalog.Part.Background);
        deck.PreviewTheme = item.Part == ThemeCatalog.Part.Conveyor ? item.Theme : conveyorTheme;
        deck.SetVerticesDirty();
        foreach (var hood in hoods)
        {
            hood.PreviewTheme = item.Theme;
            hood.gameObject.SetActive(item.Part == ThemeCatalog.Part.Casings);
            hood.SetVerticesDirty();
        }
        Resize();
    }
    private void OnRectTransformDimensionsChange() => Resize();
    private void Resize()
    {
        if (scene == null) return;
        Rect area = ((RectTransform)transform).rect;
        scene.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(area.width / 600, area.height / 280));
    }
    private static RectTransform Child(string name, RectTransform parent, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform; rect.sizeDelta = size; return rect;
    }
}
