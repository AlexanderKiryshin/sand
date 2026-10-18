using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime styling works with existing scenes and with the scene builder.</summary>
public class LevelPresentation : MonoBehaviour
{
    private Texture2D backdropTexture;
    private Sprite backdropSprite;
    private Image progressFill;
    private LevelLoader loader;
    private LevelData displayedLevel;
    private float totalGrains;

    private void Start()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        loader = FindObjectOfType<LevelLoader>();
        if (canvas == null) return;
        Image backdrop = NewImage("MosaicBackdrop", canvas.transform);
        backdrop.transform.SetAsFirstSibling();
        Stretch(backdrop.rectTransform, 0);
        backdropTexture = new Texture2D(2, 128, TextureFormat.RGBA32, false);
        backdropTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 128; y++)
        {
            Color color = Color.Lerp(new Color(0.76f, 0.84f, 0.85f), new Color(0.94f, 0.95f, 0.91f), y / 127f);
            backdropTexture.SetPixel(0, y, color);
            backdropTexture.SetPixel(1, y, color);
        }
        backdropTexture.Apply();
        backdropSprite = Sprite.Create(backdropTexture, new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f));
        backdrop.sprite = backdropSprite;
        // Keep the selected skin inside the base background, above its gradient.
        // Separate Start callbacks must not race to place opaque siblings on top.
        var theme = new GameObject("ThemeBackground", typeof(RectTransform), typeof(ThemeBackdropGraphic));
        theme.transform.SetParent(backdrop.transform, false);
        Stretch((RectTransform)theme.transform, 0);
        theme.GetComponent<ThemeBackdropGraphic>().raycastTarget = false;


        var board = GameObject.Find("SandPanel");
        if (board != null)
        {
            Image frame = board.GetComponent<Image>() ?? board.AddComponent<Image>();
            if (frame != null)
            {
                frame.enabled = true;
                frame.sprite = HintUiSprites.RoundedPanel;
                frame.type = Image.Type.Sliced;
                frame.color = new Color(1f, 0.97f, 0.88f);
                frame.raycastTarget = false;
                Shadow shadow = board.GetComponent<Shadow>() ?? board.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.08f, 0.19f, 0.22f, 0.24f);
                shadow.effectDistance = new Vector2(0, -12);
            }
            Image well = NewImage("SandWell", board.transform);
            well.transform.SetAsFirstSibling();
            // Keep the dark well behind the painting only — the bottom chrome stays
            // the panel color so it does not draw a strip over the mosaic edge.
            well.rectTransform.anchorMin = Vector2.zero;
            well.rectTransform.anchorMax = Vector2.one;
            well.rectTransform.offsetMin = new Vector2(12, 14);
            well.rectTransform.offsetMax = new Vector2(-12, -12);
            well.color = new Color(0.13f, 0.22f, 0.25f);
            Image track = NewImage("PaintingProgressTrack", board.transform);
            track.rectTransform.anchorMin = new Vector2(0, 0);
            track.rectTransform.anchorMax = new Vector2(1, 0);
            track.rectTransform.pivot = new Vector2(0.5f, 0);
            track.rectTransform.offsetMin = new Vector2(12, 3);
            track.rectTransform.offsetMax = new Vector2(-12, 12);
            track.color = new Color(0.73f, 0.78f, 0.74f);
            progressFill = NewImage("PaintingProgress", track.transform);
            Stretch(progressFill.rectTransform, 0);
            progressFill.color = new Color(0.17f, 0.66f, 0.55f);
        }
    }

    private void LateUpdate()
    {
        if (loader != null && loader.CurrentGrid != null && loader.CurrentLevel != null && progressFill != null)
        {
            if (displayedLevel != loader.CurrentLevel)
            {
                displayedLevel = loader.CurrentLevel;
                totalGrains = displayedLevel.CountTotalLevelCells() * loader.CurrentGrid.SubGrainsPerAxis * loader.CurrentGrid.SubGrainsPerAxis;
            }
            float progress = 1f - loader.CurrentGrid.CountRemaining() / Mathf.Max(1, totalGrains);
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        }
    }

    private void OnDestroy()
    {
        if (backdropSprite != null) Destroy(backdropSprite);
        if (backdropTexture != null) Destroy(backdropTexture);
    }

    private static Image NewImage(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
