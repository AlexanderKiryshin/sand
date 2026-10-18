using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    [SerializeField] private bool tutorialOnFirstLevelOnly = true;
    [SerializeField] private float ringPulseScale = 0.12f;
    [SerializeField] private float ringPulseSpeed = 3.2f;
    [SerializeField] private float handTapSpeed = 4.5f;
    [SerializeField] private float handSizeMultiplier = 1.15f;

    private GameManager gameManager;
    private GameUI gameUI;
    private BucketInventoryUI bucketInventoryUI;
    private LevelIntroSandAnimator introAnimator;
    private Canvas canvas;
    private RectTransform overlayRoot;
    private RectTransform highlightRingRect;
    private Image highlightRingImage;
    private RectTransform handRect;
    private Image handImage;
    private Bucket targetBucket;
    private bool tutorialEnabled;
    private bool stepActive;
    private bool completed;
    private float animationTime;
    private const string CompletedKey = "sand_tutorial_v2_complete";
    private int phase;
    private float watchTime;
    private bool levelHintActive;
    private SandEconomyHud economyHud;
    private LocalizationManager localization;
    private ColorFocusPanel filterPanel;
    public int FilterTutorialStep { get; private set; }

    public void HandleBucketFilled(Bucket bucket)
    {
        if (!tutorialEnabled || completed || phase != 1) return;
        phase = 2;
        BeginFirstStep();
    }

    public void HandleLevelCompleted()
    {
        FilterTutorialStep = 0;
        if (tutorialEnabled) CompleteTutorial();
        if (gameUI != null) gameUI.SetHintVisible(false);
    }

    private void Awake()
    {
        canvas = FindObjectOfType<Canvas>();
        EnsureOverlay();
    }

    private void Update()
    {
        if (Time.timeScale == 0 || CampaignGallery.IsOpen || (SandEconomyHud.instance != null && SandEconomyHud.instance.IsShopOpen))
        {
            SetTutorialVisualsVisible(false);
            return;
        }
        if (FilterTutorialStep > 0)
        {
            if (introAnimator != null && introAnimator.IsPlaying) { SetTutorialVisualsVisible(false); return; }
            var swatch = filterPanel != null ? filterPanel.GetSwatchRect(FilterTutorialStep == 1 ? filterPanel.RedColor : (byte)0) : null;
            if (swatch == null) { SetTutorialVisualsVisible(false); return; }
            EnsureOverlay();
            Vector2 center = overlayRoot.InverseTransformPoint(swatch.TransformPoint(swatch.rect.center));
            float size = swatch.rect.width * swatch.lossyScale.x / overlayRoot.lossyScale.x;
            animationTime += Time.unscaledDeltaTime;
            SetTutorialVisualsVisible(true); UpdateHighlightRing(center, size); UpdateHand(center, size);
            return;
        }
        if (tutorialEnabled && phase == 1)
        {
            watchTime += Time.unscaledDeltaTime;
            if (watchTime > 20f) { phase = 2; BeginFirstStep(); }
        }
        if (!stepActive || completed)
        {
            if (bucketInventoryUI != null)
            {
                bucketInventoryUI.ClearTutorialFocus();
            }

            SetTutorialVisualsVisible(false);
            return;
        }

        if (targetBucket == null && bucketInventoryUI != null)
        {
            targetBucket = bucketInventoryUI.GetFirstAccessibleBucket();
        }

        if (targetBucket == null || bucketInventoryUI == null)
        {
            CompleteTutorial();
            return;
        }

        animationTime += Time.deltaTime;

        Vector2 bucketCenter;
        float bucketSize;
        if (!bucketInventoryUI.TryGetBucketCanvasPosition(targetBucket, out bucketCenter, out bucketSize))
        {
            SetTutorialVisualsVisible(false);
            return;
        }

        EnsureOverlay();
        SetTutorialVisualsVisible(true);
        UpdateHighlightRing(bucketCenter, bucketSize);
        UpdateHand(bucketCenter, bucketSize);
    }

    private void OnDestroy()
    {
        if (filterPanel != null) filterPanel.SelectionChanged -= HandleFilterSelected;
        if (localization != null) localization.onLanguageChange -= RefreshLanguage;
        if (gameUI != null) gameUI.OnHintDismissed -= HandleHintDismissed;
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished -= HandleIntroFinished;
        }
        if (highlightRingImage != null && highlightRingImage.sprite != null)
        {
            Destroy(highlightRingImage.sprite.texture);
            Destroy(highlightRingImage.sprite);
        }
    }

    public void HandleLevelLoaded(
        GameManager manager,
        GameUI ui,
        BucketInventoryUI inventoryUI,
        ConveyorBelt conveyor)
    {
        if (gameUI != null) gameUI.OnHintDismissed -= HandleHintDismissed;
        if (filterPanel != null) filterPanel.SelectionChanged -= HandleFilterSelected;
        FilterTutorialStep = 0;
        gameManager = manager;
        gameUI = ui;
        if (gameUI != null) gameUI.OnHintDismissed += HandleHintDismissed;
        bucketInventoryUI = inventoryUI;
        economyHud = FindObjectOfType<SandEconomyHud>();
        if (localization != null) localization.onLanguageChange -= RefreshLanguage;
        localization = LocalizationManager.Instance;
        if (localization != null) localization.onLanguageChange += RefreshLanguage;
        completed = false;
        stepActive = false;
        animationTime = 0f;
        targetBucket = null;
        phase = 0;
        watchTime = 0f;
        levelHintActive = false;

        bool seasonal = gameManager != null && gameManager.IsSeasonal(gameManager.CurrentLevelIndex);
        tutorialEnabled = !seasonal && gameManager != null
            && (!tutorialOnFirstLevelOnly || gameManager.CampaignLevelIndex == 0);

        ResolveCanvas();
        EnsureOverlay();
        ResolveIntroAnimator();

        if (seasonal)
        {
            DisableTutorialVisuals();
            gameUI?.SetControlsInteractable(true);
            gameUI?.SetHintVisible(false);
            bucketInventoryUI?.Refresh();
            return;
        }

        if (!tutorialEnabled)
        {
            DisableTutorialVisuals();
            filterPanel = FindObjectOfType<ColorFocusPanel>();
            if (manager != null && manager.CampaignLevelIndex == 1 && filterPanel != null)
            {
                FilterTutorialStep = 1;
                filterPanel.SelectionChanged += HandleFilterSelected;
                gameUI.SetControlsInteractable(true);
                ShowFilterHint();
                bucketInventoryUI?.Refresh();
                return;
            }
            if (gameUI != null)
            {
                gameUI.SetControlsInteractable(true);
                gameUI.SetHintVisible(false);
            }

            // Always (re)show lesson hints on level load/restart — do not gate on persisted seen state.
            if (manager != null && manager.CampaignLevelIndex > 0 && CampaignProgression.IsLessonStart(manager.CampaignLevelIndex))
            {
                string lesson = CampaignProgression.GetLesson(manager.CampaignLevelIndex);
                if (!string.IsNullOrEmpty(lesson) && gameUI != null)
                {
                    gameUI.SetTutorialHint(lesson);
                    levelHintActive = true;
                }
            }

            if (bucketInventoryUI != null)
            {
                bucketInventoryUI.Refresh();
            }

            return;
        }

        if (gameUI != null)
        {
            gameUI.SetControlsInteractable(true);
            gameUI.SetHintVisible(false);
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
        }

        if (introAnimator != null && introAnimator.IsPlaying)
        {
            return;
        }

        BeginFirstStep();
    }

    public bool IsBucketInteractionAllowed(Bucket bucket)
    {
        if (FilterTutorialStep > 0) return false;
        if (!tutorialEnabled || completed)
        {
            return true;
        }

        if (!stepActive)
        {
            return phase == 1;
        }

        return bucket != null && bucket == targetBucket;
    }

    public static string GetLessonSeenKey(int index) => "sand_lesson_seen_" + CampaignProgression.GetLessonId(index);

    public void HandleHintDismissed()
    {
        levelHintActive = false;
    }

    public void HandleBucketPlaced(Bucket bucket)
    {
        if (!tutorialEnabled || completed || !stepActive)
        {
            return;
        }

        if (bucket == null || bucket != targetBucket)
        {
            return;
        }

        if (phase == 0)
        {
            phase = 1;
            stepActive = false;
            watchTime = 0f;
            SetTutorialVisualsVisible(false);
            if (gameUI != null) gameUI.SetTutorialHint(CampaignText.Get("tutorial_watch"), 2);
            if (bucketInventoryUI != null) bucketInventoryUI.Refresh();
        }
        else CompleteTutorial();
    }

    private void BeginFirstStep()
    {
        if (bucketInventoryUI == null)
        {
            return;
        }

        targetBucket = bucketInventoryUI.GetFirstAccessibleBucket();
        if (targetBucket == null)
        {
            CompleteTutorial();
            return;
        }

        stepActive = true;
        animationTime = 0f;

        if (gameUI != null)
        {
            gameUI.SetControlsInteractable(true);
            gameUI.SetTutorialHint(CampaignText.Get(phase == 0 ? "tutorial_pick" : "tutorial_repeat"), phase + 1);
        }

        bucketInventoryUI.Refresh();
    }

    private void CompleteTutorial()
    {
        if (tutorialEnabled && !GameProgressStore.GetBool(CompletedKey))
        {
            Analytic.Track("tutorial_completed");
            GameProgressStore.SetBool(CompletedKey, true);
            GameProgressStore.Save();
        }
        completed = true;
        stepActive = false;
        tutorialEnabled = false;
        targetBucket = null;

        DisableTutorialVisuals();

        if (gameUI != null)
        {
            gameUI.SetControlsInteractable(true);
            gameUI.SetTutorialHint(CampaignText.Get("tutorial_free"));
            levelHintActive = true;
        }

        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.Refresh();
        }
    }

    private void DisableTutorialVisuals()
    {
        if (bucketInventoryUI != null)
        {
            bucketInventoryUI.ClearTutorialFocus();
        }

        SetTutorialVisualsVisible(false);
    }

    private void ResolveCanvas()
    {
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }
    }

    private void ResolveIntroAnimator()
    {
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished -= HandleIntroFinished;
        }

        introAnimator = FindObjectOfType<LevelIntroSandAnimator>();
        if (introAnimator != null)
        {
            introAnimator.OnIntroFinished += HandleIntroFinished;
        }
    }

    private void HandleIntroFinished()
    {
        if (!tutorialEnabled || completed || stepActive)
        {
            return;
        }

        BeginFirstStep();
    }

    private void EnsureOverlay()
    {
        ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        if (overlayRoot == null)
        {
            Transform existing = canvas.transform.Find("TutorialOverlay");
            if (existing != null)
            {
                overlayRoot = existing.GetComponent<RectTransform>();
            }
        }

        if (overlayRoot == null)
        {
            GameObject overlayObject = new GameObject("TutorialOverlay", typeof(RectTransform));
            overlayObject.transform.SetParent(canvas.transform, false);
            overlayRoot = overlayObject.GetComponent<RectTransform>();
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
        }

        overlayRoot.SetAsLastSibling();
        EnsureHighlightRing();
        EnsureHand();
        DestroySkipButtonIfPresent();
    }

    private void DestroySkipButtonIfPresent()
    {
        if (overlayRoot == null) return;
        Transform existing = overlayRoot.Find("SkipTutorial");
        if (existing != null) Destroy(existing.gameObject);
        existing = canvas != null ? canvas.transform.Find("SkipTutorial") : null;
        if (existing != null) Destroy(existing.gameObject);
    }

    private void EnsureHighlightRing()
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (highlightRingRect == null)
        {
            Transform existing = overlayRoot.Find("TutorialHighlight");
            if (existing != null)
            {
                highlightRingRect = existing.GetComponent<RectTransform>();
            }
        }

        if (highlightRingRect == null)
        {
            GameObject ringObject = new GameObject("TutorialHighlight", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(overlayRoot, false);
            highlightRingRect = ringObject.GetComponent<RectTransform>();
            highlightRingRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRingRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRingRect.pivot = new Vector2(0.5f, 0.5f);

            highlightRingImage = ringObject.GetComponent<Image>();
            highlightRingImage.sprite = CreateRingSprite();
            highlightRingImage.color = new Color(.22f, .62f, .56f, .9f);
            highlightRingImage.raycastTarget = false;
        }
        else if (highlightRingImage == null)
        {
            highlightRingImage = highlightRingRect.GetComponent<Image>();
        }
    }

    private void EnsureHand()
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (handRect == null)
        {
            Transform existing = overlayRoot.Find("TutorialHand");
            if (existing != null)
            {
                handRect = existing.GetComponent<RectTransform>();
            }
        }

        if (handRect == null)
        {
            GameObject handObject = new GameObject("TutorialHand", typeof(RectTransform), typeof(Image));
            handObject.transform.SetParent(overlayRoot, false);
            handRect = handObject.GetComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.5f, 0.5f);
            handRect.anchorMax = new Vector2(0.5f, 0.5f);
            handRect.pivot = TutorialHandSprite.Pivot;
            handRect.localRotation = Quaternion.Euler(0f, 0f, -4f);

            handImage = handObject.GetComponent<Image>();
            handImage.sprite = TutorialHandSprite.Get();
            handImage.color = Color.white;
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
        else if (handImage == null)
        {
            handImage = handRect.GetComponent<Image>();
        }

        if (handImage != null)
        {
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
    }

    private void UpdateHighlightRing(Vector2 bucketCenter, float bucketSize)
    {
        if (highlightRingRect == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(animationTime * ringPulseSpeed) * Mathf.Min(.025f, ringPulseScale);
        float ringSize = bucketSize * 1.12f * pulse;
        highlightRingRect.anchoredPosition = bucketCenter;
        highlightRingRect.sizeDelta = new Vector2(ringSize, ringSize);

        if (highlightRingImage != null)
        {
            float alphaPulse = .85f + Mathf.Sin(animationTime * ringPulseSpeed) * .1f;
            highlightRingImage.color = new Color(.21f, .58f, .51f, alphaPulse);
        }
    }

    private void UpdateHand(Vector2 bucketCenter, float bucketSize)
    {
        if (handRect == null)
        {
            return;
        }

        handRect.pivot = TutorialHandSprite.Pivot;

        float handSize = bucketSize * Mathf.Min(.88f, handSizeMultiplier);
        handRect.sizeDelta = new Vector2(handSize, handSize);

        float tap = (Mathf.Sin(animationTime * Mathf.Min(3f, handTapSpeed)) + 1f) * 0.5f;
        Vector2 fingerTarget = bucketCenter + new Vector2(bucketSize * .33f, -bucketSize * .24f);
        Vector2 restOffset = new Vector2(handSize * .1f, -handSize * .10f);
        Vector2 pressedOffset = Vector2.zero;
        handRect.anchoredPosition = Vector2.Lerp(fingerTarget + restOffset, fingerTarget + pressedOffset, tap);

        float scalePulse = 1f - tap * 0.04f;
        handRect.localScale = new Vector3(scalePulse, scalePulse, 1f);
    }

    private void SetTutorialVisualsVisible(bool visible)
    {
        if (highlightRingRect != null)
        {
            highlightRingRect.gameObject.SetActive(visible);
        }

        if (handRect != null)
        {
            handRect.gameObject.SetActive(visible);
        }
    }

    private void ShowFilterHint()
    {
        gameUI?.SetTutorialHint(CampaignText.Get(FilterTutorialStep == 1 ? "tutorial_filter_pick" : "tutorial_filter_all"), FilterTutorialStep == 1 ? 4 : 5);
    }
    private void HandleFilterSelected(byte selected)
    {
        if (FilterTutorialStep == 1 && filterPanel != null && selected != 0 && selected == filterPanel.RedColor) { FilterTutorialStep = 2; ShowFilterHint(); }
        else if (FilterTutorialStep == 2 && selected == 0)
        {
            FilterTutorialStep = 0; SetTutorialVisualsVisible(false);
            gameUI?.SetHintVisible(false); bucketInventoryUI?.Refresh();
        }
    }
    private void RefreshLanguage()
    {
        if (FilterTutorialStep > 0) { ShowFilterHint(); return; }
        if (gameUI == null || (economyHud != null && economyHud.IsTutorialActive)) return;
        if (tutorialEnabled)
            gameUI.SetTutorialHint(CampaignText.Get(phase == 0 ? "tutorial_pick" : phase == 1 ? "tutorial_watch" : "tutorial_repeat"), phase + 1);
        else if (levelHintActive)
            gameUI.SetTutorialHint(gameManager.CampaignLevelIndex == 0 ? CampaignText.Get("tutorial_free") : CampaignProgression.GetLesson(gameManager.CampaignLevelIndex));
        if (bucketInventoryUI != null) bucketInventoryUI.Refresh();
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.5f - 1f;
        float innerRadius = outerRadius - 2.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(Mathf.Abs(x + .5f - center.x), Mathf.Abs(y + .5f - center.y));
                Vector2 q = p - new Vector2(21f, 21f);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) + 21f;
                float ring = Mathf.Clamp01(outerRadius - distance) * Mathf.Clamp01(distance - innerRadius);
                float softOuter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((outerRadius - distance) / 2f));
                float softInner = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - innerRadius) / 2f));
                float alpha = ring * softOuter * softInner;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
