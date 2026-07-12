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

    private const float HintWidthNormal = 600f;
    private const float HintHeightNormal = 92f;
    private const float HintWidthTutorial = 740f;
    private const float HintHeightTutorial = 148f;
    private const float TutorialTopBandHeight = 38f;

    private GameManager gameManager;
    private ConveyorBelt conveyorBelt;
    private RectTransform hintRootRect;
    private Image hintShadow;
    private Image hintGlow;
    private Image hintBorderOuter;
    private Image hintBorderInner;
    private Image hintTopBand;
    private Image hintBackground;
    private Image hintIconBadge;
    private Image hintIcon;
    private Image hintArrow;
    private TextMeshProUGUI hintBadgeLabel;
    private bool tutorialHintActive;
    private float hintPulseTime;

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

        EnsureHintDecorations();
        HideLevelComplete();
    }

    private void Update()
    {
        if (!tutorialHintActive)
        {
            return;
        }

        hintPulseTime += Time.deltaTime;

        if (hintGlow != null)
        {
            float glowAlpha = 0.28f + Mathf.Sin(hintPulseTime * 3.4f) * 0.12f;
            Color glow = hintGlow.color;
            glow.a = glowAlpha;
            hintGlow.color = glow;
        }

        if (hintArrow != null)
        {
            float bob = Mathf.Sin(hintPulseTime * 4f) * 3f;
            RectTransform arrowRect = hintArrow.rectTransform;
            arrowRect.anchoredPosition = new Vector2(0f, -TutorialTopBandHeight * 0.5f - 6f + bob);
        }
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
            SetHintVisible(false);
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

    public void SetHint(string text)
    {
        tutorialHintActive = false;
        hintPulseTime = 0f;
        ApplyHintLayout(false);
        SetHint(text, false);
    }

    public void SetTutorialHint(string text)
    {
        EnsureHintDecorations();
        tutorialHintActive = true;
        hintPulseTime = 0f;
        ApplyHintLayout(true);
        SetHint(text, true);
    }

    private void SetHint(string text, bool tutorialEmphasis)
    {
        if (hintLabel != null)
        {
            hintLabel.text = text;
            hintLabel.fontSize = tutorialEmphasis ? 34f : 29f;
            hintLabel.fontStyle = tutorialEmphasis ? FontStyles.Bold : FontStyles.Normal;
            hintLabel.color = tutorialEmphasis
                ? new Color(0.1f, 0.16f, 0.3f, 1f)
                : new Color(0.2f, 0.18f, 0.14f, 1f);
            hintLabel.alignment = TextAlignmentOptions.Left;
            hintLabel.margin = tutorialEmphasis
                ? new Vector4(120f, 16f, 22f, 12f)
                : new Vector4(80f, 12f, 20f, 12f);
        }

        if (hintShadow != null)
        {
            hintShadow.gameObject.SetActive(true);
            hintShadow.sprite = HintUiSprites.RoundedPanel;
            hintShadow.type = Image.Type.Sliced;
            hintShadow.color = tutorialEmphasis
                ? new Color(0.05f, 0.1f, 0.22f, 0.34f)
                : new Color(0.1f, 0.08f, 0.06f, 0.22f);
        }

        if (hintGlow != null)
        {
            hintGlow.gameObject.SetActive(tutorialEmphasis);
            hintGlow.sprite = HintUiSprites.RoundedPanel;
            hintGlow.type = Image.Type.Sliced;
            hintGlow.color = new Color(0.35f, 0.72f, 1f, 0.28f);
        }

        if (hintBorderOuter != null)
        {
            hintBorderOuter.gameObject.SetActive(true);
            hintBorderOuter.sprite = HintUiSprites.RoundedOutline;
            hintBorderOuter.type = Image.Type.Sliced;
            hintBorderOuter.color = tutorialEmphasis
                ? new Color(0.18f, 0.48f, 0.95f, 1f)
                : new Color(0.78f, 0.62f, 0.28f, 0.9f);
        }

        if (hintBorderInner != null)
        {
            hintBorderInner.gameObject.SetActive(false);
        }

        if (hintTopBand != null)
        {
            hintTopBand.gameObject.SetActive(tutorialEmphasis);
            hintTopBand.sprite = HintUiSprites.FlatBar;
            hintTopBand.type = Image.Type.Simple;
            hintTopBand.color = new Color(0.24f, 0.56f, 0.98f, 1f);
        }

        if (hintBackground != null)
        {
            hintBackground.sprite = HintUiSprites.GradientPanel;
            hintBackground.type = Image.Type.Sliced;
            hintBackground.color = tutorialEmphasis
                ? new Color(0.96f, 0.98f, 1f, 1f)
                : new Color(1f, 0.98f, 0.93f, 1f);
        }

        if (hintIconBadge != null)
        {
            hintIconBadge.gameObject.SetActive(true);
            hintIconBadge.sprite = HintUiSprites.CircleBadge;
            hintIconBadge.color = tutorialEmphasis
                ? new Color(0.18f, 0.5f, 0.96f, 1f)
                : new Color(0.9f, 0.72f, 0.28f, 1f);
        }

        if (hintIcon != null)
        {
            hintIcon.gameObject.SetActive(true);
            hintIcon.sprite = tutorialEmphasis ? HintUiSprites.TapIcon : HintUiSprites.BucketIcon;
            hintIcon.color = Color.white;
            hintIcon.preserveAspect = true;
        }

        if (hintBadgeLabel != null)
        {
            hintBadgeLabel.gameObject.SetActive(tutorialEmphasis);
            hintBadgeLabel.text = "TUTORIAL";
        }

        if (hintArrow != null)
        {
            hintArrow.gameObject.SetActive(tutorialEmphasis);
            hintArrow.sprite = HintUiSprites.ArrowUp;
            hintArrow.color = new Color(0.25f, 0.62f, 1f, 0.95f);
            hintArrow.preserveAspect = true;
        }

        SetHintVisible(!string.IsNullOrEmpty(text));
    }

    public void SetHintVisible(bool visible)
    {
        if (hintLabel == null)
        {
            return;
        }

        Transform parent = hintLabel.transform.parent;
        if (parent != null)
        {
            parent.gameObject.SetActive(visible);
        }
        else
        {
            hintLabel.gameObject.SetActive(visible);
        }

        if (MobilePortraitLayout.Instance != null)
        {
            MobilePortraitLayout.Instance.ApplyLayout();
        }
    }

    public void SetControlsInteractable(bool interactable)
    {
        if (resetButton != null)
        {
            resetButton.interactable = interactable;
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.interactable = interactable;
        }
    }

    private void EnsureHintDecorations()
    {
        if (hintLabel == null)
        {
            return;
        }

        hintRootRect = hintLabel.transform.parent as RectTransform;
        if (hintRootRect == null)
        {
            return;
        }

        Image rootImage = hintRootRect.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = Color.clear;
            rootImage.raycastTarget = false;
        }

        hintShadow = EnsureChildImage(hintRootRect, "HintShadow");
        hintGlow = EnsureChildImage(hintRootRect, "HintGlow");
        hintBorderOuter = EnsureChildImage(hintRootRect, "HintBorderOuter");
        hintBorderInner = EnsureChildImage(hintRootRect, "HintBorderInner");
        hintTopBand = EnsureChildImage(hintRootRect, "HintTopBand");
        hintBackground = EnsureChildImage(hintRootRect, "HintBackground");
        hintIconBadge = EnsureChildImage(hintRootRect, "HintIconBadge");
        hintIcon = EnsureChildImage(hintRootRect, "HintIcon");
        hintArrow = EnsureChildImage(hintRootRect, "HintArrow");

        Stretch(hintShadow.rectTransform, new Vector2(8f, -10f), new Vector2(-8f, -18f));
        Stretch(hintGlow.rectTransform, new Vector2(-6f, -4f), new Vector2(6f, 4f));
        Stretch(hintBorderOuter.rectTransform, Vector2.zero, Vector2.zero);
        Stretch(hintBackground.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

        LayoutTopBandRect();

        RectTransform badgeRect = hintIconBadge.rectTransform;
        badgeRect.anchorMin = new Vector2(0f, 0f);
        badgeRect.anchorMax = new Vector2(0f, 0f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(52f, GetHintBodyCenterY(tutorialHintActive));
        badgeRect.sizeDelta = new Vector2(60f, 60f);

        RectTransform iconRect = hintIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 0f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(52f, GetHintBodyCenterY(tutorialHintActive));
        iconRect.sizeDelta = new Vector2(38f, 38f);

        RectTransform arrowRect = hintArrow.rectTransform;
        arrowRect.anchorMin = new Vector2(0.5f, 1f);
        arrowRect.anchorMax = new Vector2(0.5f, 1f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(36f, 22f);
        arrowRect.anchoredPosition = new Vector2(0f, -TutorialTopBandHeight * 0.5f - 6f);

        hintBadgeLabel = EnsureBadgeLabel(hintTopBand.transform);

        hintShadow.transform.SetAsFirstSibling();
        hintGlow.transform.SetSiblingIndex(1);
        hintBorderOuter.transform.SetSiblingIndex(2);
        hintBorderInner.transform.SetSiblingIndex(3);
        hintBackground.transform.SetSiblingIndex(4);
        hintTopBand.transform.SetSiblingIndex(5);
        hintIconBadge.transform.SetSiblingIndex(6);
        hintIcon.transform.SetSiblingIndex(7);
        hintArrow.transform.SetSiblingIndex(8);
        hintLabel.transform.SetAsLastSibling();

        foreach (Image image in new[] { hintShadow, hintGlow, hintBorderOuter, hintBorderInner, hintTopBand, hintBackground, hintIconBadge, hintIcon, hintArrow })
        {
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        ApplyHintLayout(tutorialHintActive);
    }

    private void ApplyHintLayout(bool tutorial)
    {
        if (hintRootRect == null)
        {
            return;
        }

        hintRootRect.sizeDelta = new Vector2(
            tutorial ? HintWidthTutorial : HintWidthNormal,
            tutorial ? HintHeightTutorial : HintHeightNormal);

        if (hintIconBadge != null)
        {
            RectTransform badgeRect = hintIconBadge.rectTransform;
            badgeRect.anchoredPosition = new Vector2(52f, GetHintBodyCenterY(tutorial));
        }

        if (hintIcon != null)
        {
            RectTransform iconRect = hintIcon.rectTransform;
            iconRect.anchoredPosition = new Vector2(52f, GetHintBodyCenterY(tutorial));
        }

        LayoutTopBandRect();

        if (MobilePortraitLayout.Instance != null)
        {
            MobilePortraitLayout.Instance.ApplyLayout();
        }
    }

    private void LayoutTopBandRect()
    {
        if (hintTopBand == null)
        {
            return;
        }

        RectTransform topBandRect = hintTopBand.rectTransform;
        const float horizontalInset = 4f;
        const float topInset = 4f;
        topBandRect.anchorMin = new Vector2(0f, 1f);
        topBandRect.anchorMax = new Vector2(1f, 1f);
        topBandRect.pivot = new Vector2(0.5f, 1f);
        topBandRect.offsetMin = new Vector2(horizontalInset, -(TutorialTopBandHeight + topInset));
        topBandRect.offsetMax = new Vector2(-horizontalInset, -topInset);
    }

    private static float GetHintBodyCenterY(bool tutorial)
    {
        if (!tutorial)
        {
            return HintHeightNormal * 0.5f;
        }

        return (HintHeightTutorial - TutorialTopBandHeight - 8f) * 0.5f;
    }

    private static TextMeshProUGUI EnsureBadgeLabel(Transform parent)
    {
        Transform existing = parent.Find("HintBadgeLabel");
        GameObject labelObject;
        if (existing != null)
        {
            labelObject = existing.gameObject;
        }
        else
        {
            labelObject = new GameObject("HintBadgeLabel", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
        }

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = labelObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rect = label.rectTransform;
        Stretch(rect, Vector2.zero, Vector2.zero);
        label.text = "TUTORIAL";
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static Image EnsureChildImage(RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        GameObject childObject;
        if (existing != null)
        {
            childObject = existing.gameObject;
        }
        else
        {
            childObject = new GameObject(childName, typeof(RectTransform), typeof(Image));
            childObject.transform.SetParent(parent, false);
        }

        Image image = childObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}

internal static class HintUiSprites
{
    private static Sprite roundedPanel;
    private static Sprite roundedOutline;
    private static Sprite gradientPanel;
    private static Sprite flatBar;
    private static Sprite circleBadge;
    private static Sprite tapIcon;
    private static Sprite bucketIcon;
    private static Sprite arrowUp;

    public static Sprite RoundedPanel => roundedPanel ?? (roundedPanel = CreateRoundedRect(64, 64, 20f, true, false));
    public static Sprite RoundedOutline => roundedOutline ?? (roundedOutline = CreateRoundedRect(64, 64, 20f, false, false));
    public static Sprite GradientPanel => gradientPanel ?? (gradientPanel = CreateGradientPanel(64, 64, 20f));
    public static Sprite FlatBar => flatBar ?? (flatBar = CreateFlatBar());
    public static Sprite CircleBadge => circleBadge ?? (circleBadge = CreateCircle(48));
    public static Sprite TapIcon => tapIcon ?? (tapIcon = LoadIcon("UI/HintTapIcon"));
    public static Sprite BucketIcon => bucketIcon ?? (bucketIcon = LoadIcon("UI/HintBucketIcon"));
    public static Sprite ArrowUp => arrowUp ?? (arrowUp = LoadIcon("UI/HintArrowUp"));

    private static Sprite LoadIcon(string resourcePath)
    {
        Sprite sprite = UiSpriteUtility.LoadProcessedIcon(resourcePath);
        return sprite != null ? sprite : CreateCircle(48);
    }

    private static Sprite CreateRoundedRect(int width, int height, float radius, bool filled, bool verticalGradient)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        float border = filled ? 0f : 3f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = GetRoundedAlpha(x + 0.5f, y + 0.5f, width, height, radius, border);
                float tone = verticalGradient ? Mathf.Lerp(1.05f, 0.92f, y / (float)height) : 1f;
                texture.SetPixel(x, y, new Color(tone, tone, tone, alpha));
            }
        }

        texture.Apply();
        Vector4 borderVec = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borderVec);
    }

    private static Sprite CreateGradientPanel(int width, int height, float radius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = GetRoundedAlpha(x + 0.5f, y + 0.5f, width, height, radius, 0f);
                float t = y / (float)(height - 1);
                Color top = new Color(1f, 1f, 1f, alpha);
                Color bottom = new Color(0.94f, 0.97f, 1f, alpha);
                texture.SetPixel(x, y, Color.Lerp(top, bottom, t));
            }
        }

        texture.Apply();
        Vector4 borderVec = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borderVec);
    }

    private static Sprite CreateFlatBar()
    {
        const int size = 4;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCircle(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - distance + 0.8f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float GetRoundedAlpha(float x, float y, float width, float height, float radius, float border)
    {
        float left = radius;
        float right = width - radius;
        float bottom = radius;
        float top = height - radius;

        float dx = x < left ? left - x : x > right ? x - right : 0f;
        float dy = y < bottom ? bottom - y : y > top ? y - top : 0f;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        float outer = Mathf.Clamp01(radius - distance + 0.5f);
        if (border <= 0f)
        {
            return outer;
        }

        float innerDistance = Mathf.Max(0f, distance - border);
        float inner = Mathf.Clamp01(radius - innerDistance + 0.5f);
        return Mathf.Clamp01(outer - inner);
    }
}
