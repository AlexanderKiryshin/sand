using System.Collections;
using MirraGames.SDK;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds economy/booster/shop/complete HUD at runtime and wires BUSES-style flows into sand.
/// </summary>
public class SandEconomyHud : MonoBehaviour
{
    public static SandEconomyHud instance;

    private Canvas canvas;
    private GameManager gameManager;
    private TextMeshProUGUI coinText;
    private TextMeshProUGUI levelCompleteTitle;
    private GameObject completePanel;
    private GameObject shopPanel;
    private GameObject buyPopup;
    private GameObject noMoneyToast;
    private GameObject skipLevelButtonGo;
    private TextMeshProUGUI buyPopupTitle;
    private TextMeshProUGUI buyPopupPrice;
    private BoosterType pendingBuyType;
    private readonly Button[] boosterButtons = new Button[3];
    private readonly TextMeshProUGUI[] boosterTitles = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] boosterCharges = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] boosterUnlockLabels = new TextMeshProUGUI[3];
    private RectTransform handRect;
    private Image handImage;
    private Vector2 handTargetCenter;
    private float handTargetSize = 80f;
    private float handAnimTime;
    private bool handAnimating;
    private BoosterType? tutorialType;
    private bool tutorialWaitingFirstBucket;
    private bool tutorialWaitingActivation;
    private bool tutorialWaitingTarget;
    private bool tutorialGameplayFrozen;
    private ConveyorBelt tutorialConveyor;

    /// <summary>
    /// Conveyor should stay paused while Fill tutorial has frozen gameplay
    /// (waiting for booster tap) or while Fill target mode is active.
    /// </summary>
    public bool ShouldPauseConveyorForTutorial
    {
        get { return tutorialGameplayFrozen; }
    }

    /// <summary>
    /// Inventory taps are blocked after the Fill tutorial freezes on the first belt bucket.
    /// </summary>
    public bool BlocksInventoryForTutorial
    {
        get { return tutorialGameplayFrozen; }
    }

    public bool IsTutorialActive => tutorialType.HasValue;
    public bool IsShopOpen => shopPanel != null && shopPanel.activeInHierarchy;

    private void Awake()
    {
        instance = this;
    }

    public void Bootstrap(GameManager manager)
    {
        gameManager = manager;
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        EnsureServices();
        BuildCoinHud();
        BuildSkipLevelButton();
        BuildBoosterBar();
        BuildCompletePanel();
        BuildBuyPopup();
        BuildShopPanel();
        BuildNoMoneyToast();
        BuildTutorialHand();
        WireEvents();
        RefreshAll();
    }

    /// <summary>
    /// Enables or hides the skip-level debug button at runtime and persists to GameDebugSettings.
    /// </summary>
    public void SetSkipLevelButtonVisible(bool visible)
    {
        GameDebugSettings settings = GameDebugSettings.Get();
        if (settings != null)
        {
            settings.showSkipLevelButton = visible;
        }

        if (skipLevelButtonGo != null)
        {
            skipLevelButtonGo.SetActive(visible);
        }
    }

    private void EnsureServices()
    {
        if (FindObjectOfType<LocalizationManager>() == null)
        {
            gameObject.AddComponent<LocalizationManager>();
        }

        if (FindObjectOfType<AdManager>() == null)
        {
            gameObject.AddComponent<AdManager>();
        }

        if (FindObjectOfType<CoinManager>() == null)
        {
            gameObject.AddComponent<CoinManager>();
        }

        if (FindObjectOfType<BoosterService>() == null)
        {
            gameObject.AddComponent<BoosterService>();
        }

        if (FindObjectOfType<GameIsReady>() == null)
        {
            gameObject.AddComponent<GameIsReady>();
        }
    }

    private void WireEvents()
    {
        if (CoinManager.instance != null)
        {
            CoinManager.instance.OnCoinsChanged += HandleCoinsChanged;
            HandleCoinsChanged(CoinManager.instance.Coins);
        }

        if (BoosterService.instance != null)
        {
            BoosterService.instance.OnChargesChanged += RefreshBoosters;
            BoosterService.instance.OnBoosterActivated += HandleBoosterActivated;
            BoosterService.instance.OnBoosterModeCancelled += HandleBoosterModeCancelled;
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange += RefreshLocalized;
        }
    }

    private void Update()
    {
        if (IsShopOpen) return;
        if (!handAnimating || handRect == null || !handRect.gameObject.activeSelf)
        {
            return;
        }

        // Keep hand above the tutorial tip plate (layout may re-order siblings).
        handRect.SetAsLastSibling();
        RefreshActiveHandTarget();
        handAnimTime += Time.unscaledDeltaTime;
        ApplyHandTapPose(handAnimTime);
    }

    private void OnDestroy()
    {
        UnwireTutorialConveyor();
        ClearTutorialGameplayFreeze();

        if (CoinManager.instance != null)
        {
            CoinManager.instance.OnCoinsChanged -= HandleCoinsChanged;
        }

        if (BoosterService.instance != null)
        {
            BoosterService.instance.OnChargesChanged -= RefreshBoosters;
            BoosterService.instance.OnBoosterActivated -= HandleBoosterActivated;
            BoosterService.instance.OnBoosterModeCancelled -= HandleBoosterModeCancelled;
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange -= RefreshLocalized;
        }
    }

    public void OnLevelLoaded(int levelIndex)
    {
        if (BoosterService.instance != null)
        {
            BoosterService.instance.SyncUnlocks(levelIndex);
        }

        RefreshBoosters();
        TryStartBoosterTutorial(levelIndex);
        MirraSDK.Analytics.GameplayStart();
    }

    public void ShowLevelComplete()
    {
        RefreshCompletionText();
        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }

        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }

        MirraSDK.Analytics.GameplayStop();
        StartCoroutine(PlayStars());
    }

    public bool TryHandleBucketClick(Bucket bucket)
    {
        if (BoosterService.instance == null || !BoosterService.instance.ActiveTargetMode.HasValue)
        {
            if (tutorialWaitingTarget && tutorialType == BoosterType.Magnet)
            {
                return false;
            }

            return false;
        }

        BoosterType mode = BoosterService.instance.ActiveTargetMode.Value;
        if (mode == BoosterType.Magnet)
        {
            if (!BoosterMechanics.TryForcePlaceBucket(gameManager, bucket))
            {
                return true;
            }

            if (!BoosterService.instance.TryConsume(BoosterType.Magnet))
            {
                return true;
            }

            CompleteBoosterUse(BoosterType.Magnet);
            return true;
        }

        return false;
    }

    public bool TryHandleConveyorBucketClick(Bucket bucket)
    {
        if (BoosterService.instance == null || !BoosterService.instance.ActiveTargetMode.HasValue)
        {
            return false;
        }

        if (BoosterService.instance.ActiveTargetMode.Value != BoosterType.Transparent)
        {
            return false;
        }

        FallingSandSimulator simulator = FindObjectOfType<FallingSandSimulator>();
        ConveyorBelt conveyor = FindObjectOfType<ConveyorBelt>();
        if (!BoosterMechanics.TryFillConveyorBucket(bucket, simulator, conveyor))
        {
            return true;
        }

        if (!BoosterService.instance.TryConsume(BoosterType.Transparent))
        {
            return true;
        }

        CompleteBoosterUse(BoosterType.Transparent);
        return true;
    }

    private void CompleteBoosterUse(BoosterType type)
    {
        BoosterService.instance.ClearTargetMode();
        if (tutorialType == type)
        {
            FinishTutorial();
        }

        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUi != null)
        {
            inventoryUi.Refresh();
        }

        RefreshBoosters();
        HideHand();
    }

    private void HandleBoosterButton(BoosterType type)
    {
        if (BoosterService.instance == null || gameManager == null)
        {
            return;
        }

        if (!BoosterService.instance.IsUnlocked(type, gameManager.CurrentLevelIndex))
        {
            return;
        }

        // Fill tutorial: ignore Fill taps until the player places a belt bucket.
        if (tutorialWaitingFirstBucket && type == BoosterType.Transparent)
        {
            return;
        }

        // While frozen on Fill tutorial, only the Fill booster may be used.
        if (tutorialGameplayFrozen && tutorialType == BoosterType.Transparent && type != BoosterType.Transparent)
        {
            return;
        }

        if (BoosterService.instance.ActiveTargetMode == type)
        {
            // Keep Fill target mode locked during the guided tutorial.
            if (tutorialWaitingTarget && tutorialType == type)
            {
                return;
            }

            BoosterService.instance.CancelTargetMode();
            HideHand();
            return;
        }

        if (BoosterService.instance.GetCharges(type) <= 0)
        {
            OpenBuyPopup(type);
            return;
        }

        if (type == BoosterType.Shuffle)
        {
            if (!BoosterMechanics.TryShuffleInventoryColors(gameManager.Inventory))
            {
                return;
            }

            if (!BoosterService.instance.TryConsume(BoosterType.Shuffle))
            {
                return;
            }

            CompleteBoosterUse(BoosterType.Shuffle);
            return;
        }

        BoosterService.instance.BeginTargetMode(type);
        if (tutorialWaitingActivation && tutorialType == type)
        {
            tutorialWaitingActivation = false;
            tutorialWaitingTarget = true;
            UpdateTutorialHandForTarget();
        }
    }

    private void OpenBuyPopup(BoosterType type)
    {
        pendingBuyType = type;
        if (buyPopup != null)
        {
            buyPopup.SetActive(true);
        }

        Transform iconTransform = buyPopup != null ? buyPopup.transform.Find("BoosterIcon") : null;
        if (iconTransform == null && buyPopup != null)
        {
            GameObject iconGo = new GameObject("BoosterIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(buyPopup.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(0f, 40f);
            iconRt.sizeDelta = new Vector2(84f, 84f);
            iconTransform = iconGo.transform;
        }

        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            ApplySprite(iconImage, GameUiSymbols.Booster(type), Color.white);
            if (iconImage != null)
            {
                iconImage.type = Image.Type.Simple;
            }
        }

        if (buyPopupTitle != null && LocalizationManager.Instance != null)
        {
            buyPopupTitle.text = LocalizationManager.Instance.GetText(BoosterCatalog.GetLocalizationKey(type));
            buyPopupTitle.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        }

        if (buyPopupPrice != null && LocalizationManager.Instance != null)
        {
            buyPopupPrice.text = LocalizationManager.Instance.GetText("buy_for", BoosterCatalog.BuyPriceCoins);
            buyPopupPrice.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        }

        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }
    }

    private void BuyWithCoins()
    {
        if (CoinManager.instance == null)
        {
            return;
        }

        if (!CoinManager.instance.TryToSpendCoins(BoosterCatalog.BuyPriceCoins))
        {
            ShowNoMoney();
            return;
        }

        BoosterService.instance.GrantCharges(pendingBuyType, BoosterCatalog.AdGrantCharges);
        CloseBuyPopup();
    }

    private void BuyWithAd()
    {
        if (AdManager.instance == null)
        {
            BoosterService.instance.GrantCharges(pendingBuyType, BoosterCatalog.AdGrantCharges);
            CloseBuyPopup();
            return;
        }

        AdManager.instance.StartRewarded(
            ProgressSaveService.CurrentLevelIndex.ToString(),
            BoosterCatalog.GetAdPlacement(pendingBuyType),
            () =>
            {
                BoosterService.instance.GrantCharges(pendingBuyType, BoosterCatalog.AdGrantCharges);
                CloseBuyPopup();
            },
            () =>
            {
                if (LocalizationManager.Instance != null && buyPopupTitle != null)
                {
                    buyPopupTitle.text = LocalizationManager.Instance.GetText("ads_not_available");
                }
            });
    }

    private void CloseBuyPopup()
    {
        if (buyPopup != null)
        {
            buyPopup.SetActive(false);
        }

        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }

        RefreshBoosters();
    }

    private void ShowNoMoney()
    {
        if (noMoneyToast == null)
        {
            return;
        }

        noMoneyToast.SetActive(true);
        StopCoroutine(nameof(HideNoMoneyLater));
        StartCoroutine(HideNoMoneyLater());
    }

    private IEnumerator HideNoMoneyLater()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        if (noMoneyToast != null)
        {
            noMoneyToast.SetActive(false);
        }
    }

    private void OnCompleteContinue()
    {
        if (AdManager.instance != null)
        {
            AdManager.instance.ShowInterstitial(() =>
            {
                CoinManager.instance?.AddCoinsOnComplete();
                FinishCompleteAndNext();
            });
        }
        else
        {
            CoinManager.instance?.AddCoinsOnComplete();
            FinishCompleteAndNext();
        }
    }

    private void OnCompleteDouble()
    {
        if (AdManager.instance != null)
        {
            AdManager.instance.StartRewarded(
                ProgressSaveService.CurrentLevelIndex.ToString(),
                "x2_gold",
                () =>
                {
                    CoinManager.instance?.AddCoinsOnDoubleReward();
                    FinishCompleteAndNext();
                },
                () => { });
        }
        else
        {
            CoinManager.instance?.AddCoinsOnDoubleReward();
            FinishCompleteAndNext();
        }
    }

    private void FinishCompleteAndNext()
    {
        if (completePanel != null)
        {
            completePanel.SetActive(false);
        }

        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }

        MirraSDK.Analytics.GameplayStart();
        gameManager?.LoadNextLevel();
    }

    private void TryStartBoosterTutorial(int levelIndex)
    {
        UnwireTutorialConveyor();
        ClearTutorialGameplayFreeze();

        tutorialType = null;
        tutorialWaitingFirstBucket = false;
        tutorialWaitingActivation = false;
        tutorialWaitingTarget = false;
        HideHand();

        if (BoosterService.instance == null)
        {
            return;
        }

        BoosterType? pending = BoosterService.instance.GetPendingTutorial(levelIndex);
        if (!pending.HasValue)
        {
            return;
        }

        tutorialType = pending.Value;

        if (tutorialType == BoosterType.Transparent)
        {
            // Wait for the player to place the first belt bucket, then freeze and guide Fill.
            tutorialWaitingFirstBucket = true;
            WireTutorialConveyor();
            RefreshBoosters();
            if (tutorialConveyor != null && tutorialConveyor.Entries.Count > 0)
            {
                BeginTransparentTutorialAfterFirstBucket();
            }

            return;
        }

        BeginBoosterActivationTutorial(tutorialType.Value);
    }

    private void BeginBoosterActivationTutorial(BoosterType type)
    {
        tutorialWaitingActivation = true;

        if (gameManager != null)
        {
            GameUI ui = FindObjectOfType<GameUI>();
            if (ui != null && LocalizationManager.Instance != null)
            {
                ui.SetTutorialHint(LocalizationManager.Instance.GetText(BoosterCatalog.GetTutorialHintKey(type)));
                ui.SetControlsInteractable(false);
            }
        }

        StartCoroutine(PointHandAtBoosterNextFrame(type));
    }

    private void WireTutorialConveyor()
    {
        UnwireTutorialConveyor();
        tutorialConveyor = FindObjectOfType<ConveyorBelt>();
        if (tutorialConveyor != null)
        {
            tutorialConveyor.OnConveyorChanged += HandleTutorialConveyorChanged;
        }
    }

    private void UnwireTutorialConveyor()
    {
        if (tutorialConveyor != null)
        {
            tutorialConveyor.OnConveyorChanged -= HandleTutorialConveyorChanged;
            tutorialConveyor = null;
        }
    }

    private void HandleTutorialConveyorChanged()
    {
        if (!tutorialWaitingFirstBucket || tutorialType != BoosterType.Transparent)
        {
            return;
        }

        if (tutorialConveyor == null || tutorialConveyor.Entries.Count == 0)
        {
            return;
        }

        BeginTransparentTutorialAfterFirstBucket();
    }

    private void BeginTransparentTutorialAfterFirstBucket()
    {
        if (!tutorialWaitingFirstBucket || tutorialType != BoosterType.Transparent)
        {
            return;
        }

        tutorialWaitingFirstBucket = false;
        UnwireTutorialConveyor();
        FreezeTutorialGameplay();
        BeginBoosterActivationTutorial(BoosterType.Transparent);
        RefreshBoosters();

        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUi != null)
        {
            inventoryUi.RefreshInteractionState();
        }
    }

    private void FreezeTutorialGameplay()
    {
        tutorialGameplayFrozen = true;

        FallingSandSimulator simulator = FindObjectOfType<FallingSandSimulator>();
        if (simulator != null)
        {
            simulator.SetSimulationPaused(true);
        }

        ConveyorBelt conveyor = tutorialConveyor != null ? tutorialConveyor : FindObjectOfType<ConveyorBelt>();
        if (conveyor != null)
        {
            conveyor.SetMovementPaused(true);
        }
    }

    private void ClearTutorialGameplayFreeze()
    {
        if (!tutorialGameplayFrozen)
        {
            return;
        }

        tutorialGameplayFrozen = false;

        FallingSandSimulator simulator = FindObjectOfType<FallingSandSimulator>();
        if (simulator != null)
        {
            simulator.SetSimulationPaused(false);
        }
    }

    private IEnumerator PointHandAtBoosterNextFrame(BoosterType type)
    {
        yield return null;
        if (!tutorialWaitingActivation || tutorialType != type)
        {
            yield break;
        }

        if (MobilePortraitLayout.Instance != null)
        {
            MobilePortraitLayout.Instance.ApplyLayout();
        }

        Canvas.ForceUpdateCanvases();
        PointHandAtBooster(type);
    }

    private void FinishTutorial()
    {
        if (!tutorialType.HasValue)
        {
            return;
        }

        BoosterService.instance.MarkTutorialSeen(tutorialType.Value);
        tutorialType = null;
        tutorialWaitingFirstBucket = false;
        tutorialWaitingActivation = false;
        tutorialWaitingTarget = false;
        UnwireTutorialConveyor();
        ClearTutorialGameplayFreeze();
        HideHand();

        GameUI ui = FindObjectOfType<GameUI>();
        if (ui != null)
        {
            if (gameManager != null && gameManager.CurrentLevelIndex == 0)
                ui.SetTutorialHint(CampaignText.Get("tutorial_free"));
            else if (gameManager != null && CampaignProgression.IsLessonStart(gameManager.CurrentLevelIndex))
                ui.SetTutorialHint(CampaignProgression.GetLesson(gameManager.CurrentLevelIndex));
            else
                ui.SetHintVisible(false);
            ui.SetControlsInteractable(true);
        }

        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUi != null)
        {
            inventoryUi.RefreshInteractionState();
        }

        RefreshBoosters();
    }

    private void UpdateTutorialHandForTarget()
    {
        if (!tutorialType.HasValue)
        {
            return;
        }

        if (tutorialType == BoosterType.Magnet)
        {
            // Magnet targets use bucket pulse only — no hand on buckets.
            HideHand();
            return;
        }

        if (tutorialType == BoosterType.Transparent)
        {
            ConveyorBeltView beltView = FindObjectOfType<ConveyorBeltView>();
            ConveyorBelt conveyor = FindObjectOfType<ConveyorBelt>();
            if (beltView != null && conveyor != null && conveyor.Entries.Count > 0)
            {
                Bucket bucket = conveyor.Entries[0].Bucket;
                if (bucket != null && beltView.TryGetBucketCanvasPosition(bucket, out Vector2 canvasPos))
                {
                    ShowHandAt(canvasPos, 80f);
                    return;
                }
            }
        }

        PointHandAtBooster(tutorialType.Value);
    }

    private void PointHandAtBooster(BoosterType type)
    {
        int index = (int)type;
        if (index < 0 || index >= boosterButtons.Length || boosterButtons[index] == null)
        {
            return;
        }

        if (MobilePortraitLayout.Instance != null)
        {
            MobilePortraitLayout.Instance.ApplyLayout();
        }

        RectTransform rect = boosterButtons[index].GetComponent<RectTransform>();
        if (!TryGetCanvasCenter(rect, out Vector2 canvasPosition, out float size))
        {
            return;
        }

        ShowHandAt(canvasPosition, Mathf.Max(72f, size * 0.85f));
    }

    private void ShowHandAt(Vector2 canvasCenter, float targetSize)
    {
        if (handRect == null)
        {
            return;
        }

        handTargetCenter = canvasCenter;
        handTargetSize = Mathf.Max(48f, targetSize);
        handAnimTime = 0f;
        handAnimating = true;
        handRect.gameObject.SetActive(true);
        handRect.SetAsLastSibling();
        ApplyHandTapPose(0f);
    }

    private void HideHand()
    {
        handAnimating = false;
        if (handRect != null)
        {
            handRect.gameObject.SetActive(false);
        }
    }

    private void RefreshActiveHandTarget()
    {
        if (tutorialWaitingActivation && tutorialType.HasValue)
        {
            int index = (int)tutorialType.Value;
            if (index >= 0 && index < boosterButtons.Length && boosterButtons[index] != null)
            {
                RectTransform rect = boosterButtons[index].GetComponent<RectTransform>();
                if (TryGetCanvasCenter(rect, out Vector2 canvasPosition, out float size))
                {
                    handTargetCenter = canvasPosition;
                    handTargetSize = Mathf.Max(72f, size * 0.85f);
                }
            }

            return;
        }

        if (tutorialWaitingTarget && tutorialType == BoosterType.Transparent)
        {
            ConveyorBeltView beltView = FindObjectOfType<ConveyorBeltView>();
            ConveyorBelt conveyor = FindObjectOfType<ConveyorBelt>();
            if (beltView != null && conveyor != null && conveyor.Entries.Count > 0)
            {
                Bucket bucket = conveyor.Entries[0].Bucket;
                if (bucket != null && beltView.TryGetBucketCanvasPosition(bucket, out Vector2 canvasPos))
                {
                    handTargetCenter = canvasPos;
                    handTargetSize = 80f;
                }
            }
        }
    }

    private void ApplyHandTapPose(float time)
    {
        if (handRect == null)
        {
            return;
        }

        handRect.pivot = TutorialHandSprite.Pivot;
        float handSize = handTargetSize * 1.15f;
        handRect.sizeDelta = new Vector2(handSize, handSize);

        const float handTapSpeed = 4.5f;
        float tap = (Mathf.Sin(time * handTapSpeed) + 1f) * 0.5f;
        Vector2 fingerTarget = handTargetCenter;
        Vector2 restOffset = new Vector2(handSize * 0.1f, handSize * 0.32f);
        Vector2 pressedOffset = new Vector2(handSize * 0.03f, handSize * 0.06f);
        handRect.anchoredPosition = Vector2.Lerp(fingerTarget + restOffset, fingerTarget + pressedOffset, tap);

        float scalePulse = 1f - tap * 0.04f;
        handRect.localScale = new Vector3(scalePulse, scalePulse, 1f);
    }

    private bool TryGetCanvasCenter(RectTransform rect, out Vector2 canvasPosition, out float size)
    {
        canvasPosition = Vector2.zero;
        size = 80f;
        if (rect == null || canvas == null)
        {
            return false;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out canvasPosition);
        size = Mathf.Max(rect.rect.width, rect.rect.height);
        return true;
    }

    private void HandleBoosterActivated(BoosterType type)
    {
        RefreshBoosters();
        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUi != null)
        {
            inventoryUi.RefreshInteractionState();
        }

        if (type == BoosterType.Magnet)
        {
            HideHand();
            return;
        }

        if (tutorialWaitingTarget)
        {
            UpdateTutorialHandForTarget();
        }
    }

    private void HandleBoosterModeCancelled()
    {
        RefreshBoosters();
        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        if (inventoryUi != null)
        {
            inventoryUi.RefreshInteractionState();
        }

        if (tutorialWaitingActivation && tutorialType.HasValue)
        {
            PointHandAtBooster(tutorialType.Value);
            return;
        }

        if (tutorialWaitingTarget && tutorialType.HasValue)
        {
            // Player backed out of target mode — guide them back to the booster.
            tutorialWaitingTarget = false;
            tutorialWaitingActivation = true;
            PointHandAtBooster(tutorialType.Value);
            return;
        }

        HideHand();
    }

    private void HandleCoinsChanged(int value)
    {
        if (coinText != null)
        {
            coinText.text = value.ToString();
        }
    }

    private void RefreshLocalized()
    {
        RefreshBoosters();
        RefreshShopText();
        RefreshCompletionText();
        if (skipLevelButtonGo != null)
            skipLevelButtonGo.GetComponentInChildren<TextMeshProUGUI>().text = CampaignText.Get("debug_skip");
    }

    private void RefreshShopText()
    {
        if (shopPanel == null || LocalizationManager.Instance == null) return;
        foreach (var label in shopPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string parent = label.transform.parent.name;
            if (label.name == "ShopTitle") label.text = LocalizationManager.Instance.GetText("shop");
            if (label.name == "Description") label.text = LocalizationManager.Instance.GetText("reamoveAdDescription");
            if (label.name == "Title")
            {
                if (parent.StartsWith("Gold ")) label.text = parent.Replace("Gold ", "").Replace("Btn", "") + " " + CampaignText.Get("coins");
                else label.text = LocalizationManager.Instance.GetText(parent.Replace("Btn", ""));
            }
            if (label.name == "Caption")
            {
                string key = parent == "GoldChip" ? "coins" : parent == "MagnetChip" ? "magnet" : parent == "FillChip" ? "transparent" : "shuffle";
                label.text = key == "coins" ? CampaignText.Get(key) : LocalizationManager.Instance.GetText(key);
            }
        }
    }

    private void RefreshAll()
    {
        RefreshBoosters();
        RefreshShopText();
        if (CoinManager.instance != null)
        {
            HandleCoinsChanged(CoinManager.instance.Coins);
        }
    }

    private void RefreshBoosters()
    {
        if (BoosterService.instance == null || gameManager == null)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            BoosterType type = (BoosterType)i;
            bool unlocked = BoosterService.instance.IsUnlocked(type, gameManager.CurrentLevelIndex);
            int charges = BoosterService.instance.GetCharges(type);
            if (boosterTitles[i] != null) boosterTitles[i].text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(BoosterCatalog.GetLocalizationKey(type)) : type.ToString();
            if (boosterButtons[i] != null)
            {
                bool tutorialLocksOtherBoosters = tutorialGameplayFrozen
                    && tutorialType == BoosterType.Transparent
                    && type != BoosterType.Transparent;
                bool tutorialBlocksFillUntilBucket = tutorialWaitingFirstBucket
                    && type == BoosterType.Transparent;
                boosterButtons[i].interactable = unlocked
                    && !tutorialLocksOtherBoosters
                    && !tutorialBlocksFillUntilBucket;
                Image frame = boosterButtons[i].GetComponent<Image>();
                if (frame != null)
                {
                    Color frameColor = unlocked ? new Color(1, .99f, .93f) : new Color(.77f, .84f, .80f);
                    frameColor.a = 1;
                    if (BoosterService.instance.ActiveTargetMode == type)
                    {
                        frameColor = new Color(1f, 0.92f, 0.45f, frameColor.a);
                    }

                    frame.color = frameColor;
                }

                Transform iconTransform = boosterButtons[i].transform.Find("Icon");
                if (iconTransform != null)
                {
                    Image iconImage = iconTransform.GetComponent<Image>();
                    if (iconImage != null)
                    {
                        Sprite icon = GameUiSymbols.Booster(type);
                        if (icon != null)
                        {
                            iconImage.sprite = icon;
                            iconImage.preserveAspect = true;
                            iconImage.type = Image.Type.Simple;
                        }

                        iconImage.color = Color.white;
                    }
                }

                Transform lockIcon = boosterButtons[i].transform.Find("LockIcon");
                if (lockIcon != null)
                {
                    lockIcon.gameObject.SetActive(!unlocked);
                }
            }

            if (boosterCharges[i] != null)
            {
                boosterCharges[i].text = unlocked ? "× " + charges : string.Empty;
                boosterCharges[i].gameObject.SetActive(unlocked);
            }

            if (boosterUnlockLabels[i] != null)
            {
                int unlockLevel = BoosterCatalog.GetUnlockLevelIndex(type) + 1;
                string label = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("unlock_at_level", unlockLevel)
                    : ("Lv." + unlockLevel);
                boosterUnlockLabels[i].text = CampaignText.Get("level_short") + " " + unlockLevel;
                boosterUnlockLabels[i].gameObject.SetActive(!unlocked);
            }
        }
    }

    private IEnumerator PlayStars()
    {
        if (completePanel == null)
        {
            yield break;
        }

        Transform stars = completePanel.transform.Find("Card/Stars");
        if (stars == null)
        {
            yield break;
        }

        for (int i = 0; i < stars.childCount; i++)
        {
            stars.GetChild(i).gameObject.SetActive(false);
        }

        for (int i = 0; i < stars.childCount; i++)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            Transform star = stars.GetChild(i);
            star.gameObject.SetActive(true);
            star.localScale = Vector3.one * 0.2f;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                star.localScale = Vector3.Lerp(Vector3.one * 0.2f, Vector3.one * 1.2f, t / 0.25f);
                yield return null;
            }

            star.localScale = Vector3.one;
        }
    }

    private void BuildCoinHud()
    {
        RectTransform root = CreatePanel("CoinHud", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-140f, -56f), new Vector2(220f, 64f));
        Image bg = root.gameObject.AddComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameWhite, new Color(1f, 1f, 1f, 0.92f));

        GameObject goldGo = new GameObject("GoldIcon", typeof(RectTransform), typeof(Image));
        goldGo.transform.SetParent(root, false);
        RectTransform goldRt = goldGo.GetComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(0f, 0.5f);
        goldRt.anchorMax = new Vector2(0f, 0.5f);
        goldRt.anchoredPosition = new Vector2(34f, 0f);
        goldRt.sizeDelta = new Vector2(44f, 44f);
        Image goldImage = goldGo.GetComponent<Image>();
        ApplySprite(goldImage, BusesUiArt.Gold, Color.white);

        coinText = CreateTmp(root, "CoinText", "0", 28, TextAlignmentOptions.Center);
        coinText.rectTransform.offsetMin = new Vector2(60f, 0f);
        coinText.rectTransform.offsetMax = new Vector2(-12f, 0f);
        coinText.color = new Color(0.15f, 0.1f, 0.05f, 1f);

        Button shopBtn = CreateIconButton(root, "ShopBtn", GameUiSymbols.Get(GameUiSymbol.Shop), () =>
        {
            RewardedShop shop = shopPanel != null ? shopPanel.GetComponent<RewardedShop>() : null;
            if (shop != null)
            {
                shop.OpenShop();
            }
        });
        RectTransform shopRt = shopBtn.GetComponent<RectTransform>();
        shopRt.anchorMin = new Vector2(0f, 0.5f);
        shopRt.anchorMax = new Vector2(0f, 0.5f);
        shopRt.anchoredPosition = new Vector2(-40f, 0f);
        shopRt.sizeDelta = new Vector2(64, 64);
        shopBtn.GetComponent<Image>().color = new Color(.16f, .39f, .37f);

        CoinSpawner spawner = root.gameObject.AddComponent<CoinSpawner>();
        spawner.coinCount = 25;
        GameObject coinPrefab = Resources.Load<GameObject>("UI/Coin");
        if (coinPrefab == null)
        {
            coinPrefab = CreateFallbackCoinPrefab();
        }

        spawner.Configure(coinPrefab, root, goldRt, 0.07f);
    }

    private void BuildSkipLevelButton()
    {
        Button skip = CreateStyledButton(canvas.transform, "SkipLevelBtn", CampaignText.Get("debug_skip"), HintUiSprites.RoundedPanel, SkipCurrentLevel);
        skip.GetComponent<Image>().color = new Color(.15f, .37f, .37f);
        TextMeshProUGUI text = skip.GetComponentInChildren<TextMeshProUGUI>();
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1, .99f, .91f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 24;
        RectTransform rt = skip.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -100f);
        rt.sizeDelta = new Vector2(220f, 56f);
        skipLevelButtonGo = skip.gameObject;
        skipLevelButtonGo.SetActive(GameDebugSettings.ShowSkipLevelButton);
    }

    private void SkipCurrentLevel()
    {
        if (gameManager == null || !GameDebugSettings.ShowSkipLevelButton || CampaignGallery.IsOpen
            || IsShopOpen || (completePanel != null && completePanel.activeSelf))
        {
            return;
        }

        int current = gameManager.CurrentLevelIndex;
        Analytic.Track("level_debug_skipped");
        Analytic.AbandonLevel("debug_skip");
        ProgressSaveService.MarkLevelCompleted(current);
        if (BoosterService.instance != null)
        {
            BoosterService.instance.SyncUnlocks(current + 1);
        }

        gameManager.LoadNextLevel();
    }

    private GameObject CreateFallbackCoinPrefab()
    {
        GameObject coin = new GameObject("Coin", typeof(RectTransform), typeof(Image), typeof(CoinMover));
        RectTransform rt = coin.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(36f, 36f);
        Image image = coin.GetComponent<Image>();
        ApplySprite(image, BusesUiArt.Gold, Color.white);
        coin.SetActive(false);
        return coin;
    }

    private void BuildBoosterBar()
    {
        RectTransform root = CreatePanel("BoosterBar", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(700, 152));
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 16;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        for (int i = 0; i < 3; i++)
        {
            BoosterType type = (BoosterType)i;
            var buttonGo = new GameObject("Booster_" + type, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(root, false);
            var rt = (RectTransform)buttonGo.transform;
            rt.sizeDelta = new Vector2(212, 136);
            Image frame = buttonGo.GetComponent<Image>();
            frame.sprite = HintUiSprites.RoundedPanel;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(1, .99f, .93f);
            var icon = CreateFixedImage(rt, "Icon", GameUiSymbols.Booster(type), new Vector2(-43, 20), new Vector2(104, 104));
            icon.color = Color.white;
            icon.raycastTarget = false;
            var lockIcon = CreateFixedImage(rt, "LockIcon", GameUiSymbols.Get(GameUiSymbol.Lock), new Vector2(60, 32), new Vector2(64, 64));
            lockIcon.color = Color.white;
            lockIcon.raycastTarget = false;
            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(.90f, .94f, .91f, 1);
            colors.pressedColor = new Color(.75f, .90f, .84f, 1);
            button.colors = colors;
            int captured = i;
            button.onClick.AddListener(() => HandleBoosterButton((BoosterType)captured));
            boosterButtons[i] = button;
            boosterTitles[i] = CreateFixedTmp(rt, "BoosterName", "", 26, TextAlignmentOptions.Center, new Vector2(0, -46), new Vector2(192, 34));
            boosterTitles[i].color = new Color(.15f, .34f, .34f);
            boosterTitles[i].fontStyle = FontStyles.Bold;
            boosterTitles[i].enableAutoSizing = true;
            boosterTitles[i].fontSizeMin = 22;
            boosterTitles[i].fontSizeMax = 26;
            boosterCharges[i] = CreateFixedTmp(rt, "Charges", "0", 29, TextAlignmentOptions.Center, new Vector2(52, 20), new Vector2(74, 42));
            boosterCharges[i].color = new Color(.15f, .34f, .34f);
            boosterCharges[i].fontStyle = FontStyles.Bold;
            boosterUnlockLabels[i] = CreateFixedTmp(rt, "UnlockLevel", "", 27, TextAlignmentOptions.Center, new Vector2(57, -13), new Vector2(92, 32));
            boosterUnlockLabels[i].fontStyle = FontStyles.Bold;
            boosterUnlockLabels[i].color = new Color(.23f, .30f, .27f);
        }
        if (MobilePortraitLayout.Instance != null) MobilePortraitLayout.Instance.ApplyLayout();
    }
    private void BuildCompletePanel()
    {
        RectTransform root = CreatePanel("EconomyLevelCompletePanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(.06f, .14f, .18f, .72f);
        completePanel = root.gameObject;

        RectTransform card = CreatePanel("Card", Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(620, 660), root);
        Image background = card.gameObject.AddComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(1, .99f, .94f);
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.04f, .15f, .15f, .24f);
        shadow.effectDistance = new Vector2(0, -12);

        var subtitle = CreateFixedTmp(card, "Level", "", 25, TextAlignmentOptions.Center, new Vector2(0, 268), new Vector2(540, 40));
        subtitle.color = new Color(.38f, .56f, .50f);
        levelCompleteTitle = CreateFixedTmp(card, "Title", "", 44, TextAlignmentOptions.Center, new Vector2(0, 209), new Vector2(548, 72));
        levelCompleteTitle.fontStyle = FontStyles.Bold;
        levelCompleteTitle.color = new Color(.15f, .34f, .34f);
        levelCompleteTitle.enableAutoSizing = true;
        levelCompleteTitle.fontSizeMin = 30;
        levelCompleteTitle.fontSizeMax = 44;

        RectTransform stars = CreatePanel("Stars", Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 108), new Vector2(320, 100), card);
        for (int i = 0; i < 3; i++)
        {
            Image star = CreateFixedImage(stars, "Star" + i, BusesUiArt.Star,
                new Vector2((i - 1) * 105, i == 1 ? 10 : 0), Vector2.one * (i == 1 ? 96 : 76));
            star.raycastTarget = false;
            star.gameObject.SetActive(false);
        }

        Image reward = CreateFixedImage(card, "Reward", HintUiSprites.RoundedPanel, new Vector2(0, -4), new Vector2(300, 82));
        reward.type = Image.Type.Sliced;
        reward.preserveAspect = false;
        reward.color = new Color(.90f, .94f, .86f);
        CreateFixedImage(reward.rectTransform, "Coins", GameUiSymbols.Get(GameUiSymbol.Coins), new Vector2(-97, 0), new Vector2(56, 56)).raycastTarget = false;
        var amount = CreateFixedTmp(reward.rectTransform, "Amount", "", 29, TextAlignmentOptions.Center, new Vector2(30, 0), new Vector2(210, 60));
        amount.color = new Color(.15f, .34f, .34f);
        amount.fontStyle = FontStyles.Bold;

        CreateCompletionButton(card, "ContinueBtn", -119, new Color(.15f, .37f, .37f), new Color(1, .99f, .91f), OnCompleteContinue);
        CreateCompletionButton(card, "DoubleBtn", -219, new Color(1, .78f, .28f), new Color(.15f, .29f, .27f), OnCompleteDouble);
        RefreshCompletionText();
        completePanel.SetActive(false);
    }

    private void CreateCompletionButton(RectTransform card, string name, float y, Color background, Color foreground, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateStyledButton(card, name, "", HintUiSprites.RoundedPanel, action);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(0, y);
        rect.sizeDelta = new Vector2(520, 82);
        Image image = button.GetComponent<Image>();
        image.color = background;
        image.preserveAspect = false;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.color = foreground;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 23;
        label.fontSizeMax = 30;
        label.margin = new Vector4(20, 4, 20, 4);
        if (name == "DoubleBtn")
        {
            rect.sizeDelta = new Vector2(520, 94);
            label.fontSizeMax = 31;
            label.margin = new Vector4(88, 4, 12, 4);
            CreateFixedImage(rect, "RewardedVideoIcon", BusesUiArt.Ad, new Vector2(-206, 0), new Vector2(68, 68)).raycastTarget = false;
        }
    }

    private void RefreshCompletionText()
    {
        if (completePanel == null) return;
        Transform card = completePanel.transform.Find("Card");
        if (card == null) return;
        levelCompleteTitle.text = CampaignText.Get("victory");
        card.Find("Level").GetComponent<TextMeshProUGUI>().text = CampaignText.Get("level") + " " + (gameManager.CurrentLevelIndex + 1);
        int reward = CoinManager.instance != null ? CoinManager.instance.CoinsOnComplete : 50;
        card.Find("Reward/Amount").GetComponent<TextMeshProUGUI>().text = "+" + reward + " " + CampaignText.Get("coins");
        card.Find("ContinueBtn/Label").GetComponent<TextMeshProUGUI>().text = CampaignText.Get(gameManager.CurrentLevelIndex + 1 >= gameManager.LevelCount ? "gallery" : "victory_next");
        card.Find("DoubleBtn/Label").GetComponent<TextMeshProUGUI>().text = CampaignText.Get("victory_double");
    }
    private void BuildBuyPopup()
    {
        RectTransform root = CreatePanel("BoosterBuyPopup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 430f));
        Image bg = root.gameObject.AddComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameBlue, Color.white);
        buyPopup = root.gameObject;

        buyPopupTitle = CreateTmp(root, "Title", "Booster", 30, TextAlignmentOptions.Center);
        buyPopupTitle.rectTransform.anchoredPosition = new Vector2(0f, 110f);

        buyPopupPrice = CreateTmp(root, "Price", "Buy for 90", 22, TextAlignmentOptions.Center);
        buyPopupPrice.rectTransform.anchoredPosition = new Vector2(0f, 55f);

        Button adBtn = CreateStyledButton(root, "AdBtn", CampaignText.Get("watch_ad"), HintUiSprites.RoundedPanel, BuyWithAd);
        adBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -91);
        adBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(510, 88);
        adBtn.GetComponent<Image>().color = new Color(1, .78f, .28f);
        var adLabel = adBtn.GetComponentInChildren<TextMeshProUGUI>();
        adLabel.fontSize = 31; adLabel.fontStyle = FontStyles.Bold; adLabel.color = new Color(.15f, .29f, .27f);
        adLabel.margin = new Vector4(83, 4, 12, 4);
        CreateFixedImage((RectTransform)adBtn.transform, "RewardedVideoIcon", BusesUiArt.Ad, new Vector2(-204, 0), new Vector2(66, 66)).raycastTarget = false;
        Button coinBtn = CreateStyledButton(root, "CoinBtn", "Coins", BusesUiArt.BtnGreen, BuyWithCoins);
        coinBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -166);
        coinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 54);

        Button closeBtn = CreateIconButton(root, "CloseBtn", BusesUiArt.Close, CloseBuyPopup);
        RectTransform closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-28f, -28f);
        closeRt.sizeDelta = new Vector2(40f, 40f);

        buyPopup.SetActive(false);
    }

    private void BuildShopPanel()
    {
        RectTransform root = CreatePanel("ShopPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(.06f, .16f, .18f, .78f);
        shopPanel = root.gameObject;
        float width = Mathf.Min(900, Screen.safeArea.width / Mathf.Max(.001f, canvas.scaleFactor) - 64);
        width = Mathf.Max(640, width);
        RectTransform card = CreatePanel("ShopCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, 1500), root);
        Image background = card.gameObject.AddComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(.94f, .97f, .92f);
        var title = CreateFixedTmp(card, "ShopTitle", LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText("shop") : "Shop", 44,
            TextAlignmentOptions.MidlineLeft, new Vector2(-width * .5f + 280, 681), new Vector2(470, 70));
        title.color = new Color(.13f, .33f, .33f);
        title.fontStyle = FontStyles.Bold;
        RewardedShop shop = root.gameObject.AddComponent<RewardedShop>();
        Button close = CreateIconButton(card, "CloseShop", GameUiSymbols.Get(GameUiSymbol.Close), () => shop.CloseShop());
        close.GetComponent<Image>().color = new Color(.16f, .38f, .37f);
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(width * .5f - 64, 684);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(76, 76);
        RectTransform viewport = CreatePanel("ShopViewport", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(width - 48, 1290), card);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image scrollSurface = viewport.gameObject.AddComponent<Image>();
        scrollSurface.color = new Color(1, 1, 1, .001f);
        RectTransform content = CreatePanel("ShopContent", new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(width - 64, 1260), viewport);
        content.pivot = new Vector2(.5f, 1);
        ScrollRect scroll = card.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 48;
        float cursor = -8, rowWidth = width - 64;
        Button beginner = CreateKitShopRow(content, "beginners_kit", PlaceBlockCenter(ref cursor, 238, 18), GameUiSymbols.Get(GameUiSymbol.Bundle), 4000, 5, 238, rowWidth, out TextMeshProUGUI p1);
        Button premium = CreateKitShopRow(content, "premium_set", PlaceBlockCenter(ref cursor, 238, 18), GameUiSymbols.Get(GameUiSymbol.Bundle), 1000, 2, 238, rowWidth, out TextMeshProUGUI p2);
        Button g300 = CreateShopRow(content, "Gold 300", PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p3, 116, rowWidth);
        Button g700 = CreateShopRow(content, "Gold 700", PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p4, 116, rowWidth);
        Button g1200 = CreateShopRow(content, "Gold 1200", PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p5, 116, rowWidth);
        Button remove = CreateRemoveAdsShopRow(content, PlaceBlockCenter(ref cursor, 274, 18), 274, rowWidth, out TextMeshProUGUI p6);
        content.sizeDelta = new Vector2(rowWidth, -cursor);
        shop.BindRuntime(CoinManager.instance, beginner, premium, g300, g700, g1200, remove, p1, p2, p3, p4, p5, p6);
        root.gameObject.SetActive(false);
    }

    private static float PlaceBlockCenter(ref float cursor, float height, float gapAfter)
    {
        float center = cursor - height * .5f;
        cursor -= height + gapAfter;
        return center;
    }

    private Button CreateShopCard(RectTransform parent, string name, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0, y);
        Image background = obj.GetComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(1, .995f, .96f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.pressedColor = new Color(.82f, .93f, .86f);
        colors.disabledColor = new Color(.90f, .93f, .90f);
        button.colors = colors;
        return button;
    }

    private TextMeshProUGUI CreateShopPrice(RectTransform parent, Vector2 position)
    {
        Image badge = CreateFixedImage(parent, "PriceBadge", HintUiSprites.RoundedPanel, position, new Vector2(188, 70));
        badge.type = Image.Type.Sliced; badge.preserveAspect = false;
        badge.color = new Color(.17f, .40f, .39f); badge.raycastTarget = false;
        var price = CreateTmp(badge.rectTransform, "Price", "…", 30, TextAlignmentOptions.Center);
        price.color = new Color(1, .99f, .93f);
        price.fontStyle = FontStyles.Bold; price.raycastTarget = false;
        price.enableAutoSizing = true; price.fontSizeMin = 21; price.fontSizeMax = 30;
        price.enableWordWrapping = false;
        price.margin = new Vector4(8, 5, 8, 5);
        return price;
    }

    private Button CreateKitShopRow(RectTransform parent, string titleKey, float y, Sprite icon, int goldAmount, int boosterBonus, float height, float width, out TextMeshProUGUI price)
    {
        Button button = CreateShopCard(parent, titleKey + "Btn", y, width, height);
        var rt = (RectTransform)button.transform;
        bool removeAds = titleKey == "remove_ads_bundle";
        float headerY = height * .5f - 49;
        var symbol = CreateFixedImage(rt, "RowIcon", icon, new Vector2(-width * .5f + 53, headerY), new Vector2(58, 58));
        symbol.color = new Color(.19f, .45f, .41f); symbol.raycastTarget = false;
        string title = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(titleKey) : titleKey;
        var heading = CreateFixedTmp(rt, "Title", title, 32, TextAlignmentOptions.MidlineLeft,
            new Vector2(-width * .5f + 100 + (width - 338) * .5f, headerY), new Vector2(width - 338, 74));
        heading.color = new Color(.14f, .32f, .32f); heading.fontStyle = FontStyles.Bold;
        heading.enableAutoSizing = true; heading.fontSizeMin = 26; heading.fontSizeMax = 32;
        price = CreateShopPrice(rt, new Vector2(width * .5f - 114, headerY));
        if (removeAds)
        {
            var description = CreateFixedTmp(rt, "Description", LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText("reamoveAdDescription") : "Disables ads during gameplay", 25,
                TextAlignmentOptions.Left, new Vector2(0, 25), new Vector2(width - 52, 48));
            description.color = new Color(.28f, .43f, .40f);
        }
        float chipWidth = (width - 60) / 4;
        float chipY = removeAds ? -66 : -37;
        CreateKitChip(rt, "GoldChip", GameUiSymbols.Get(GameUiSymbol.Coins), goldAmount.ToString(), new Vector2(-1.5f * (chipWidth + 8), chipY), chipWidth, 94);
        CreateKitChip(rt, "MagnetChip", GameUiSymbols.Booster(BoosterType.Magnet), "+" + boosterBonus, new Vector2(-.5f * (chipWidth + 8), chipY), chipWidth, 94);
        CreateKitChip(rt, "FillChip", GameUiSymbols.Booster(BoosterType.Transparent), "+" + boosterBonus, new Vector2(.5f * (chipWidth + 8), chipY), chipWidth, 94);
        CreateKitChip(rt, "ShuffleChip", GameUiSymbols.Booster(BoosterType.Shuffle), "+" + boosterBonus, new Vector2(1.5f * (chipWidth + 8), chipY), chipWidth, 94);
        return button;
    }

    private Button CreateRemoveAdsShopRow(RectTransform parent, float y, float height, float width, out TextMeshProUGUI price)
        => CreateKitShopRow(parent, "remove_ads_bundle", y, GameUiSymbols.Get(GameUiSymbol.NoAds), 4000, 5, height, width, out price);

    private void CreateKitChip(RectTransform parent, string name, Sprite icon, string value, Vector2 anchored, float width, float height)
    {
        Image chip = CreateFixedImage(parent, name, HintUiSprites.RoundedPanel, anchored, new Vector2(width, height));
        chip.type = Image.Type.Sliced; chip.preserveAspect = false; chip.raycastTarget = false;
        chip.color = new Color(.89f, .94f, .88f);
        Image symbol = CreateFixedImage(chip.rectTransform, "Icon", icon, new Vector2(-width * .25f, 13), new Vector2(44, 44));
        symbol.color = Color.white;
        symbol.raycastTarget = false;
        var number = CreateFixedTmp(chip.rectTransform, "Value", value, 29, TextAlignmentOptions.Center, new Vector2(width * .14f, 13), new Vector2(width * .57f, 44));
        number.color = new Color(.14f, .33f, .32f); number.fontStyle = FontStyles.Bold;
        string key = name == "GoldChip" ? "coins" : name == "MagnetChip" ? "magnet" : name == "FillChip" ? "transparent" : "shuffle";
        string label = key == "coins" ? CampaignText.Get(key) : LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : key;
        var caption = CreateFixedTmp(chip.rectTransform, "Caption", label, 22, TextAlignmentOptions.Center, new Vector2(0, -25), new Vector2(width - 8, 30));
        caption.color = new Color(.26f, .43f, .38f); caption.enableAutoSizing = true; caption.fontSizeMin = 18; caption.fontSizeMax = 22;
    }
    private static Image CreateFixedImage(RectTransform parent, string name, Sprite sprite, Vector2 anchored, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        ApplySprite(image, sprite, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return image;
    }

    private static TextMeshProUGUI CreateFixedTmp(
        RectTransform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions align,
        Vector2 anchored,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = sizeDelta;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateShopRow(RectTransform parent, string label, float y, Sprite icon, out TextMeshProUGUI price, float height = 116, float width = 836)
    {
        Button button = CreateShopCard(parent, label + "Btn", y, width, height);
        var rt = (RectTransform)button.transform;
        Image symbol = CreateFixedImage(rt, "RowIcon", icon, new Vector2(-width * .5f + 60, 0), new Vector2(68, 68));
        symbol.color = Color.white; symbol.raycastTarget = false;
        var title = CreateFixedTmp(rt, "Title", label.Replace("Gold ", "") + " " + CampaignText.Get("coins"), 34, TextAlignmentOptions.MidlineLeft,
            new Vector2(-width * .5f + 112 + (width - 360) * .5f, 0), new Vector2(width - 360, 58));
        title.color = new Color(.14f, .33f, .32f); title.fontStyle = FontStyles.Bold;
        price = CreateShopPrice(rt, new Vector2(width * .5f - 114, 0));
        return button;
    }
    private void BuildNoMoneyToast()
    {
        RectTransform root = CreatePanel("NoMoneyToast", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(340f, 78f));
        Image bg = root.gameObject.AddComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameWhite, new Color(1f, 0.75f, 0.75f, 0.95f));
        TextMeshProUGUI text = CreateTmp(root, "Text", "Not enough coins", 22, TextAlignmentOptions.Center);
        text.color = new Color(0.45f, 0.05f, 0.05f, 1f);
        noMoneyToast = root.gameObject;
        noMoneyToast.SetActive(false);
    }

    private void BuildTutorialHand()
    {
        GameObject hand = new GameObject("BoosterTutorialHand", typeof(RectTransform), typeof(Image));
        hand.transform.SetParent(canvas.transform, false);
        handRect = hand.GetComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0.5f, 0.5f);
        handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = TutorialHandSprite.Pivot;
        handRect.localRotation = Quaternion.Euler(0f, 0f, -4f);
        handRect.sizeDelta = new Vector2(72f, 72f);
        handImage = hand.GetComponent<Image>();
        handImage.sprite = TutorialHandSprite.Get();
        handImage.color = Color.white;
        handImage.preserveAspect = true;
        handImage.raycastTarget = false;
        handImage.type = Image.Type.Simple;
        hand.SetActive(false);
    }

    private static void ApplySprite(Image image, Sprite sprite, Color color)
    {
        if (image == null)
        {
            return;
        }

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = true;
        }

        image.color = color;
    }

    private Button CreateStyledButton(Transform parent, string name, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 64f);
        Image image = go.GetComponent<Image>();
        ApplySprite(image, sprite != null ? sprite : BusesUiArt.BtnGreen, Color.white);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TextMeshProUGUI tmp = CreateTmp(rt, "Label", label, 22, TextAlignmentOptions.Center);
        tmp.color = Color.white;
        return button;
    }

    private Button CreateIconButton(Transform parent, string name, Sprite sprite, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48f, 48f);
        Image image = go.GetComponent<Image>();
        ApplySprite(image, sprite, Color.white);
        image.type = Image.Type.Simple;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        return button;
    }

    private RectTransform CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchored, Vector2 size, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        return rt;
    }

    private static TextMeshProUGUI CreateTmp(RectTransform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        // Kept for compatibility; prefer CreateStyledButton.
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180f, 56f);
        Image image = go.GetComponent<Image>();
        ApplySprite(image, BusesUiArt.BtnGreen, Color.white);
        Button button = go.GetComponent<Button>();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        CreateTmp(rt, "Label", label, 22, TextAlignmentOptions.Center);
        return button;
    }
}
