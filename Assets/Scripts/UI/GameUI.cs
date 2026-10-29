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
    private const float HintHeightTutorial = 176f;
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
    private bool boosterTargetHintActive;
    private string previousHintText;
    private bool previousHintVisible;
    private bool previousHintTutorial;
    private int previousHintStep;
    private float hintPulseTime;
    private int tutorialStep;
    private CanvasGroup hintGroup;
    private TextMeshProUGUI hintStepLabel;
    private Button hintCloseButton;
    private LocalizationManager localization;
    private Button addSlotButton;
    private TextMeshProUGUI addSlotLabel;
    private bool slotAdPending;
    private bool restartAdPending;
    private float slotAvailabilityTimer;
    private int displayedLevelIndex = -1;

    public System.Action OnHintDismissed;

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
        StyleRestartButton();
        HideLevelComplete();
        localization = LocalizationManager.Instance;
        if (localization != null) localization.onLanguageChange += RefreshLanguage;
    }

    private void Start()
    {
        if(levelLabel!=null && levelLabel.GetComponent<HudLabelPlate>()==null)levelLabel.gameObject.AddComponent<HudLabelPlate>();
        if(conveyorLabel!=null && conveyorLabel.GetComponent<HudLabelPlate>()==null)conveyorLabel.gameObject.AddComponent<HudLabelPlate>();
    }

    private void OnDestroy()
    {
        if (localization != null) localization.onLanguageChange -= RefreshLanguage;
    }

    private void RefreshLanguage()
    {
        RefreshConveyor();
        RefreshLevelNumber();
    }

    private void RefreshLevelNumber()
    {
        if (gameManager != null && levelLabel != null)
        {
            displayedLevelIndex = gameManager.CurrentLevelIndex;
            levelLabel.text = gameManager.LevelCaption;
        }
    }

    private void LateUpdate()
    {
        // The caption follows the current level independently of the board/UI rebuild.
        if (gameManager != null && displayedLevelIndex != gameManager.CurrentLevelIndex)
            RefreshLevelNumber();
    }

    private void StyleRestartButton()
    {
        if (resetButton == null) return;
        Image background = resetButton.GetComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(.15f, .37f, .37f);
        foreach (Text legacy in resetButton.GetComponentsInChildren<Text>(true))
            legacy.gameObject.SetActive(false);
        foreach (TMP_Text text in resetButton.GetComponentsInChildren<TMP_Text>(true))
            text.gameObject.SetActive(false);
        var icon = new GameObject("RestartIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(resetButton.transform, false);
        var image = icon.GetComponent<Image>();
        image.sprite = GameUiSymbols.Get(GameUiSymbol.Restart);
        image.raycastTarget = false;
        image.color = new Color(1, .99f, .91f);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.rectTransform.sizeDelta = new Vector2(45, 45);
    }

    private void Update()
    {
        slotAvailabilityTimer -= Time.unscaledDeltaTime;
        if (slotAvailabilityTimer <= 0) { RefreshSlotButton(); slotAvailabilityTimer = .5f; }
        if (hintGroup == null || hintGroup.alpha >= 1) return;
        hintPulseTime += Time.unscaledDeltaTime;
        hintGroup.alpha = Mathf.SmoothStep(0, 1, hintPulseTime / .22f);
    }
    public void Bind(GameManager manager, ConveyorBelt conveyor, LevelData level)
    {
        boosterTargetHintActive = false;
        gameManager = manager;
        conveyorBelt = conveyor;
        var renderer = FindObjectOfType<SandRenderer>();
        if (renderer != null && renderer.DisplayRect != null)
        {
            Transform picture = renderer.DisplayRect.parent != null ? renderer.DisplayRect.parent : renderer.DisplayRect;
            var reference = picture.GetComponent<ColorFocusPanel>() ?? picture.gameObject.AddComponent<ColorFocusPanel>();
            reference.Bind(level, renderer);
        }
        if (localization == null)
        {
            localization = LocalizationManager.Instance;
            if (localization != null) localization.onLanguageChange += RefreshLanguage;
        }

        if (levelLabel != null)
        {
            RefreshLevelNumber();
            levelLabel.rectTransform.sizeDelta = new Vector2(420, 60);
            levelLabel.fontSize = 36;
            levelLabel.fontStyle = FontStyles.Bold;
            levelLabel.enableWordWrapping = false;
            levelLabel.color = new Color(0.13f, 0.25f, 0.28f);
            Button collection = levelLabel.GetComponent<Button>();
            if (collection == null) collection = levelLabel.gameObject.AddComponent<Button>();
            levelLabel.raycastTarget = true;
            collection.onClick.RemoveAllListeners();
            collection.onClick.AddListener(manager.OpenCollection);
        }

        if (hintLabel != null)
        {
            SetHintVisible(false);
        }

        RefreshConveyor();
        RefreshLanguage();
        HideLevelComplete();
    }

    public void RefreshConveyor()
    {
        if (conveyorLabel == null || conveyorBelt == null)
        {
            return;
        }

        conveyorLabel.text = string.Format(CampaignText.Get("belt_occupied"), conveyorBelt.OccupiedSlots, conveyorBelt.Capacity);
        conveyorLabel.color = new Color(.13f, .25f, .28f);
        conveyorLabel.fontSize = 27;
        conveyorLabel.fontStyle = FontStyles.Bold;
        conveyorLabel.enableAutoSizing = true;
        conveyorLabel.fontSizeMin = 22;
        conveyorLabel.fontSizeMax = 27;
        conveyorLabel.rectTransform.anchorMin = conveyorLabel.rectTransform.anchorMax = new Vector2(.5f, 0);
        conveyorLabel.rectTransform.sizeDelta = new Vector2(700, 38);
        conveyorLabel.rectTransform.anchoredPosition = new Vector2(0, 13);
        EnsureSlotButton();
        conveyorLabel.rectTransform.sizeDelta = new Vector2(330, 46);
        conveyorLabel.rectTransform.anchoredPosition = new Vector2(-100, 13);
        RefreshSlotButton();
    }

    private void EnsureSlotButton()
    {
        if (addSlotButton != null || conveyorLabel == null) return;
        var go = new GameObject("AddConveyorSlot", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(conveyorLabel.transform.parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(160, 13);
        rect.sizeDelta = new Vector2(220, 54);
        var bg = go.GetComponent<Image>(); bg.sprite = HintUiSprites.RoundedPanel; bg.type = Image.Type.Sliced;
        bg.color = new Color(1, .77f, .25f);
        addSlotButton = go.GetComponent<Button>(); addSlotButton.targetGraphic = bg;
        addSlotButton.onClick.AddListener(RequestExtraSlot);
        var icon = new GameObject("Video", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(rect, false);
        var art = icon.GetComponent<Image>(); art.sprite = BusesUiArt.Ad; art.preserveAspect = true; art.raycastTarget = false;
        art.rectTransform.anchoredPosition = new Vector2(-74, 0); art.rectTransform.sizeDelta = new Vector2(42, 42);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(rect, false);
        addSlotLabel = label.GetComponent<TextMeshProUGUI>(); addSlotLabel.fontSize = 25; addSlotLabel.fontStyle = FontStyles.Bold;
        addSlotLabel.color = new Color(.17f, .29f, .28f); addSlotLabel.alignment = TextAlignmentOptions.Center; addSlotLabel.raycastTarget = false;
        addSlotLabel.rectTransform.sizeDelta = new Vector2(154, 50); addSlotLabel.rectTransform.anchoredPosition = new Vector2(25, 0);
    }

    private void RefreshSlotButton()
    {
        if (addSlotButton == null || conveyorBelt == null) return;
        bool canGrow = conveyorBelt.CanAddRewardedSlot;
        addSlotLabel.text = CampaignText.Get(!canGrow ? "slots_max" : slotAdPending ? "ad_wait" : "slot_ad");
        addSlotButton.interactable = canGrow && !slotAdPending && AdManager.instance != null && AdManager.instance.CanStartRewarded(true);
    }

    private void RequestExtraSlot()
    {
        if (slotAdPending || conveyorBelt == null || AdManager.instance == null || !AdManager.instance.CanStartRewarded(true)) return;
        int request = conveyorBelt.BeginSlotReward(out int generation);
        if (request == 0) return;
        slotAdPending = true; RefreshSlotButton();
        AdManager.instance.StartRewarded(gameManager.CurrentLevelIndex.ToString(), "conveyor_slot",
            () => FinishSlotAd(true), () => FinishSlotAd(false), true);
        void FinishSlotAd(bool watched)
        {
            if (this == null || conveyorBelt == null) return;
            conveyorBelt.CompleteSlotReward(request, generation, watched);
            slotAdPending = false; RefreshConveyor();
        }
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
        if (gameManager == null || restartAdPending) return;
        if (AdManager.instance == null || YoutubePlatform.IsPlayer)
        {
            gameManager.RestartLevel();
            return;
        }

        restartAdPending = true;
        if (resetButton != null) resetButton.interactable = false;
        AdManager.instance.ShowInterstitial(() =>
        {
            if (this == null || !restartAdPending) return;
            restartAdPending = false;
            if (resetButton != null) resetButton.interactable = true;
            if (gameManager != null) gameManager.RestartLevel();
        });
    }

    private void HandleNextLevelClicked()
    {
        if (gameManager != null)
        {
            gameManager.LoadNextLevel();
        }
    }

    public void SetFillTargetHintVisible(bool visible)
    {
        if (hintLabel == null) return;
        if (visible)
        {
            if (!boosterTargetHintActive)
            {
                previousHintText = hintLabel.text;
                previousHintVisible = hintLabel.gameObject.activeInHierarchy;
                previousHintTutorial = tutorialHintActive;
                previousHintStep = tutorialStep;
                boosterTargetHintActive = true;
            }
            SetHint(CampaignText.Get("fill_choose_bucket"));
            if (hintCloseButton != null) hintCloseButton.gameObject.SetActive(false);
        }
        else if (boosterTargetHintActive)
        {
            boosterTargetHintActive = false;
            if (previousHintTutorial) SetTutorialHint(previousHintText, previousHintStep);
            else SetHint(previousHintText);
            SetHintVisible(previousHintVisible);
        }
    }

    public void SetHint(string text)
    {
        tutorialHintActive = false;
        hintPulseTime = 0f;
        ApplyHintLayout(false);
        SetHint(text, false);
    }

    public void SetTutorialHint(string text, int step = 0)
    {
        if (boosterTargetHintActive)
        {
            previousHintText = text;
            previousHintTutorial = true;
            previousHintStep = step;
            SetFillTargetHintVisible(true);
            return;
        }
        EnsureHintDecorations();
        tutorialHintActive = true;
        tutorialStep = step;
        hintPulseTime = 0f;
        ApplyHintLayout(true);
        SetHint(text, true);
    }

    private void SetHint(string text, bool tutorialEmphasis)
    {
        if (hintLabel == null) return;
        hintLabel.gameObject.SetActive(true);
        hintLabel.enabled = true;
        Stretch(hintLabel.rectTransform, Vector2.zero, Vector2.zero);
        bool swapLesson = tutorialEmphasis && gameManager != null
            && CampaignProgression.GetLessonId(gameManager.CampaignLevelIndex) == "lesson_swap"
            && text == CampaignProgression.GetLesson(gameManager.CampaignLevelIndex);
        hintLabel.text = text;
        hintLabel.fontSize = 29;
        hintLabel.enableAutoSizing = true;
        hintLabel.fontSizeMin = 25;
        hintLabel.fontSizeMax = 29;
        hintLabel.fontStyle = FontStyles.Normal;
        hintLabel.color = new Color(.18f, .31f, .32f);
        hintLabel.alignment = TextAlignmentOptions.MidlineLeft;
        hintLabel.raycastTarget = false;
        hintLabel.margin = tutorialEmphasis
            ? new Vector4(swapLesson ? 92 : 28, 66, 64, 18)
            : new Vector4(80, 12, 64, 12);
        hintShadow.gameObject.SetActive(true);
        hintShadow.sprite = HintUiSprites.RoundedPanel;
        hintShadow.type = Image.Type.Sliced;
        hintShadow.color = new Color(.10f, .26f, .26f, .14f);
        hintGlow.gameObject.SetActive(false);
        hintBorderOuter.gameObject.SetActive(true);
        hintBorderOuter.sprite = HintUiSprites.RoundedPanel;
        hintBorderOuter.type = Image.Type.Sliced;
        hintBorderOuter.color = new Color(.68f, .79f, .73f);
        hintBorderInner.gameObject.SetActive(false);
        hintBackground.sprite = HintUiSprites.RoundedPanel;
        hintBackground.type = Image.Type.Sliced;
        hintBackground.color = new Color(1f, .99f, .94f);
        hintTopBand.gameObject.SetActive(tutorialEmphasis);
        hintTopBand.color = Color.clear;
        hintIconBadge.gameObject.SetActive(!tutorialEmphasis || swapLesson);
        hintIconBadge.sprite = swapLesson ? HintUiSprites.RoundedPanel : HintUiSprites.CircleBadge;
        hintIconBadge.color = swapLesson ? new Color(.80f, .24f, .73f) : new Color(.25f, .53f, .48f);
        hintIcon.gameObject.SetActive(!tutorialEmphasis || swapLesson);
        hintIcon.sprite = swapLesson ? GameUiSymbols.Get(GameUiSymbol.Shuffle) : HintUiSprites.BucketIcon;
        hintIcon.color = Color.white;
        hintIcon.preserveAspect = true;
        hintArrow.gameObject.SetActive(false);
        hintBadgeLabel.text = CampaignText.Get(tutorialStep > 0 && tutorialEmphasis ? "tutorial_title_" + tutorialStep : "learn");
        hintBadgeLabel.fontSize = 29;
        hintBadgeLabel.color = new Color(.12f, .35f, .34f);
        hintBadgeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        hintBadgeLabel.margin = new Vector4(0, 0, 120, 0);
        hintStepLabel.gameObject.SetActive(tutorialEmphasis && tutorialStep > 0);
        hintStepLabel.text = tutorialStep >= 4 ? (tutorialStep - 3) + "  /  2" : tutorialStep + "  /  3";
        LayoutHintCloseButton(tutorialEmphasis && tutorialStep > 0);
        // Instruction changes must be readable immediately, including while gameplay is paused.
        hintGroup.alpha = tutorialEmphasis ? 1f : 0f;
        hintPulseTime = 0;
        SetHintVisible(!string.IsNullOrEmpty(text));
        hintLabel.ForceMeshUpdate();
    }

    private void HandleHintCloseClicked()
    {
        SetHintVisible(false);
        SetControlsInteractable(true);
        OnHintDismissed?.Invoke();
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

        if (hintCloseButton != null)
        {
            hintCloseButton.gameObject.SetActive(visible);
        }

        if (hintGroup != null)
        {
            hintGroup.blocksRaycasts = visible;
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
        Stretch(hintBackground.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

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
        hintGroup = hintRootRect.GetComponent<CanvasGroup>();
        if (hintGroup == null) hintGroup = hintRootRect.gameObject.AddComponent<CanvasGroup>();
        hintGroup.blocksRaycasts = false;
        Transform oldStep = hintRootRect.Find("StepProgress");
        if (oldStep == null)
        {
            var stepObject = new GameObject("StepProgress", typeof(RectTransform), typeof(TextMeshProUGUI));
            stepObject.transform.SetParent(hintRootRect, false);
            oldStep = stepObject.transform;
        }
        hintStepLabel = oldStep.GetComponent<TextMeshProUGUI>();
        hintStepLabel.fontSize = 23;
        hintStepLabel.fontStyle = FontStyles.Bold;
        hintStepLabel.color = new Color(.38f, .56f, .50f);
        hintStepLabel.alignment = TextAlignmentOptions.MidlineRight;
        hintStepLabel.raycastTarget = false;
        RectTransform stepRect = hintStepLabel.rectTransform;
        stepRect.anchorMin = stepRect.anchorMax = Vector2.one;
        stepRect.pivot = Vector2.one;
        stepRect.anchoredPosition = new Vector2(-72, -20);
        stepRect.sizeDelta = new Vector2(110, 38);

        EnsureHintCloseButton();

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
        hintLabel.raycastTarget = false;
        if (hintCloseButton != null) hintCloseButton.transform.SetAsLastSibling();

        foreach (Image image in new[] { hintShadow, hintGlow, hintBorderOuter, hintBorderInner, hintTopBand, hintBackground, hintIconBadge, hintIcon, hintArrow })
        {
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        ApplyHintLayout(tutorialHintActive);
    }

    private void EnsureHintCloseButton()
    {
        if (hintRootRect == null) return;

        Transform existing = hintRootRect.Find("HintClose");
        GameObject closeObject;
        if (existing != null)
        {
            closeObject = existing.gameObject;
        }
        else
        {
            closeObject = new GameObject("HintClose", typeof(RectTransform), typeof(Image), typeof(Button));
            closeObject.transform.SetParent(hintRootRect, false);
        }

        hintCloseButton = closeObject.GetComponent<Button>();
        Image background = closeObject.GetComponent<Image>();
        background.sprite = HintUiSprites.CircleBadge;
        background.type = Image.Type.Simple;
        background.color = new Color(.16f, .38f, .37f);
        background.raycastTarget = true;
        hintCloseButton.targetGraphic = background;
        hintCloseButton.onClick.RemoveAllListeners();
        hintCloseButton.onClick.AddListener(HandleHintCloseClicked);

        Transform iconTransform = closeObject.transform.Find("Icon");
        GameObject iconObject;
        if (iconTransform != null)
        {
            iconObject = iconTransform.gameObject;
        }
        else
        {
            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(closeObject.transform, false);
        }

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = GameUiSymbols.Get(GameUiSymbol.Close);
        icon.color = new Color(1f, .99f, .91f);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(10f, 10f);
        iconRect.offsetMax = new Vector2(-10f, -10f);

        LayoutHintCloseButton(tutorialHintActive && tutorialStep > 0);
    }

    private void LayoutHintCloseButton(bool withStep)
    {
        if (hintCloseButton == null) return;

        RectTransform rect = hintCloseButton.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(44f, 44f);
        rect.anchoredPosition = new Vector2(-18f, withStep ? -16f : -24f);
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
        const float horizontalInset = 28f;
        const float topInset = 20f;
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
