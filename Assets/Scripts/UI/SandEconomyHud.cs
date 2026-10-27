using System;
using System.Collections;
using System.Linq;
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
    private ConveyorBelt boosterConveyor;
    private TextMeshProUGUI coinText;
    private TextMeshProUGUI levelCompleteTitle;
    private GameObject completePanel;
    private GameObject shopPanel;
    private GameObject buyPopup;
    private GameObject noMoneyToast;
    private GameObject skipLevelButtonGo;
    private Button rewardedSkipButton;
    private RectTransform rewardedSkipIcon;
    private bool rewardedSkipPending;
    private bool completionContinuePending;
    private int levelLoadVersion;
    private TextMeshProUGUI buyPopupTitle;
    private TextMeshProUGUI buyPopupPrice;
    private TextMeshProUGUI buyPopupAdLabel;
    private TextMeshProUGUI buyPopupCloseLabel;
    private RectTransform buyPopupCard;
    private Image buyPopupIcon;
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
#if UNITY_EDITOR
        GameDebugSettings settings = GameDebugSettings.Get();
        if (settings != null)
        {
            settings.showSkipLevelButton = visible;
        }

        if (skipLevelButtonGo != null)
        {
            skipLevelButtonGo.SetActive(visible);
            MobilePortraitLayout.Instance?.ApplyLayout();
        }
#endif
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
        boosterConveyor = FindObjectOfType<ConveyorBelt>();
        if (boosterConveyor != null)
        {
            boosterConveyor.OnConveyorChanged += HandleBoosterConveyorChanged;
        }

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
        if (skipLevelButtonGo != null && skipLevelButtonGo.activeSelf != GameDebugSettings.ShowSkipLevelButton)
        {
            skipLevelButtonGo.SetActive(GameDebugSettings.ShowSkipLevelButton);
            MobilePortraitLayout.Instance?.ApplyLayout();
        }
        RefreshSkipButtons();
        if (buyPopup != null && buyPopup.activeSelf)
        {
            ApplyBuyPopupLayout();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseBuyPopup();
        }
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
        if (boosterConveyor != null)
        {
            boosterConveyor.OnConveyorChanged -= HandleBoosterConveyorChanged;
        }

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
        completionContinuePending=false;
        levelLoadVersion++;
        if (BoosterService.instance != null)
        {
            BoosterService.instance.SyncUnlocks(levelIndex);
        }

        RefreshBoosters();
        TryStartBoosterTutorial(levelIndex);
        if (GameSdkReadiness.IsReady) MirraSDK.Analytics.GameplayStart();
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

        if (GameSdkReadiness.IsReady) MirraSDK.Analytics.GameplayStop();
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
        FindObjectOfType<GameUI>()?.SetFillTargetHintVisible(false);
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

        if (!BoosterService.instance.IsUnlocked(type, gameManager.CampaignLevelIndex))
        {
            return;
        }

        if (type == BoosterType.Transparent && !HasFillTarget())
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
            TrackBoosterTutorialStep("activate", "completed");
            TrackBoosterTutorialStep("target", "started");
            tutorialWaitingActivation = false;
            tutorialWaitingTarget = true;
            UpdateTutorialHandForTarget();
        }
    }

    private void OpenBuyPopup(BoosterType type)
    {
        if (buyPopup == null) return;
        pendingBuyType = type;
        buyPopupIcon.sprite = GameUiSymbols.Booster(type);
        RefreshBuyPopupText();
        ApplyBuyPopupLayout();
        buyPopup.transform.SetAsLastSibling();
        buyPopup.SetActive(true);

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
        ShowToast("Not enough coins");
    }

    private void ShowToast(string message)
    {
        if (noMoneyToast == null)
        {
            return;
        }

        noMoneyToast.GetComponentInChildren<TextMeshProUGUI>().text = message;
        noMoneyToast.SetActive(true);
        noMoneyToast.transform.SetAsLastSibling();
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
        if(completionContinuePending)return;
        completionContinuePending=true;
        var offer=FindObjectOfType<NatureBackgroundOffer>();
        if(offer!=null && offer.TryShow(gameManager,ContinueAfterInterstitial))return;
        if(AdManager.instance!=null)AdManager.instance.ShowInterstitial(ContinueAfterInterstitial);
        else ContinueAfterInterstitial();
    }
    private void ContinueAfterInterstitial()
    {
        if(this==null || !completionContinuePending)return;
        completionContinuePending=false;
        CoinManager.instance?.AddCoinsOnComplete();FinishCompleteAndNext();
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

        if (GameSdkReadiness.IsReady) MirraSDK.Analytics.GameplayStart();
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

        if (gameManager != null && gameManager.IsSeasonal(gameManager.CurrentLevelIndex)) return;
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
            TrackBoosterTutorialStep("place_bucket", "started");
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
        TrackBoosterTutorialStep("activate", "started");

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

        TrackBoosterTutorialStep("place_bucket", "completed");
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

    private void TrackBoosterTutorialStep(string stepId, string status)
    {
        if (!tutorialType.HasValue) return;
        bool fill = tutorialType.Value == BoosterType.Transparent;
        string id = tutorialType.Value == BoosterType.Magnet ? "booster_magnet" : fill ? "booster_fill" : "booster_shuffle";
        int step = stepId == "place_bucket" ? 1 : stepId == "activate" ? (fill ? 2 : 1) : (fill ? 3 : 2);
        Analytic.TutorialStep(id, step, stepId, status);
    }

    private void FinishTutorial()
    {
        if (!tutorialType.HasValue)
        {
            return;
        }

        TrackBoosterTutorialStep(tutorialWaitingTarget ? "target" : "activate", "completed");
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
            if (gameManager != null && gameManager.CampaignLevelIndex == 0)
                ui.SetTutorialHint(CampaignText.Get("tutorial_free"));
            else if (gameManager != null && CampaignProgression.IsLessonStart(gameManager.CampaignLevelIndex))
                ui.SetTutorialHint(CampaignProgression.GetLesson(gameManager.CampaignLevelIndex));
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
        FindObjectOfType<GameUI>()?.SetFillTargetHintVisible(type == BoosterType.Transparent);
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
        FindObjectOfType<GameUI>()?.SetFillTargetHintVisible(false);
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
        if (BoosterService.instance != null && BoosterService.instance.ActiveTargetMode == BoosterType.Transparent)
            FindObjectOfType<GameUI>()?.SetFillTargetHintVisible(true);
        RefreshBoosters();
        RefreshShopText();
        RefreshCompletionText();
        RefreshBuyPopupText();
    }

    private void RefreshShopText()
    {
        if (shopPanel == null || LocalizationManager.Instance == null) return;
        foreach (var label in shopPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string parent = label.transform.parent.name;
            if(parent=="ThemeCard" || parent=="BackgroundPaper")continue;
            if (label.name == "ShopTitle") label.text = LocalizationManager.Instance.GetText("shop");
            if (label.name == "Description") label.text = LocalizationManager.Instance.GetText("reamoveAdDescription");
            if (label.name == "Title")
            {
                if (parent.StartsWith("Gold ")) label.text = parent.Replace("Gold ", "").Replace("Btn", "") + " " + CampaignText.Get("coins");
                else if (parent.StartsWith("booster_"))
                {
                    foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
                        if (parent == RewardedShop.GetBoosterPackTag(type) + "Btn")
                            label.text = LocalizationManager.Instance.GetText(BoosterCatalog.GetLocalizationKey(type)) + " ×" + RewardedShop.SingleBoosterPackCount;
                }
                else label.text = LocalizationManager.Instance.GetText(parent.Replace("Btn", ""));
            }
            if (label.name.StartsWith("StylePart_"))
            {
                int part=int.Parse(label.name.Substring("StylePart_".Length));
                label.text=CampaignText.Get(part==0?"theme_background_tab":part==1?"theme_conveyor_tab":"theme_casings_tab");
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

    private bool HasFillTarget()
    {
        ConveyorBelt conveyor = boosterConveyor != null ? boosterConveyor : FindObjectOfType<ConveyorBelt>();
        if (conveyor == null)
        {
            return false;
        }

        foreach (ConveyorBelt.BeltEntry entry in conveyor.Entries)
        {
            Bucket bucket = entry.Bucket;
            if (bucket != null && bucket.State == BucketState.OnConveyor && !bucket.IsFull)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleBoosterConveyorChanged()
    {
        if (BoosterService.instance != null
            && BoosterService.instance.ActiveTargetMode == BoosterType.Transparent
            && !HasFillTarget())
        {
            BoosterService.instance.CancelTargetMode();
        }

        RefreshBoosters();
    }

    private void RefreshBoosters()
    {
        if (BoosterService.instance == null || gameManager == null)
        {
            return;
        }

        bool hasFillTarget = HasFillTarget();
        for (int i = 0; i < 3; i++)
        {
            BoosterType type = (BoosterType)i;
            bool unlocked = BoosterService.instance.IsUnlocked(type, gameManager.CampaignLevelIndex);
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
                    && !tutorialBlocksFillUntilBucket
                    && (type != BoosterType.Transparent || hasFillTarget);
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
        shopRt.anchoredPosition = new Vector2(-56f, 0f);
        shopRt.sizeDelta = new Vector2(88, 88);
        ApplySprite(shopBtn.GetComponent<Image>(), HintUiSprites.RoundedPanel, new Color(1f, .76f, .22f));
        RectTransform cart = CreatePanel("CartIcon", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(70, 70), shopRt);
        Image cartImage = cart.gameObject.AddComponent<Image>();
        cartImage.sprite = GameUiSymbols.Get(GameUiSymbol.Shop);
        cartImage.color = new Color(.08f, .24f, .23f);
        cartImage.preserveAspect = true;
        cartImage.raycastTarget = false;

        shopBtn.gameObject.AddComponent<ShopGoldBadge>();
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
        rewardedSkipButton = CreateSkipButton("RewardedSkipLevelBtn", SkipCurrentLevelRewarded, true, 1);
#if UNITY_EDITOR
        Button skip = CreateSkipButton("SkipLevelBtn", SkipCurrentLevel, false, 2);
        skipLevelButtonGo = skip.gameObject;
        skipLevelButtonGo.SetActive(GameDebugSettings.ShowSkipLevelButton);
#endif
        MobilePortraitLayout.Instance?.ApplyLayout();
    }

    private Button CreateSkipButton(string name, UnityEngine.Events.UnityAction onClick, bool rewarded, int slot)
    {
        Button skip = CreateIconButton(canvas.transform, name, HintUiSprites.RoundedPanel, onClick);
        ApplySprite(skip.GetComponent<Image>(), HintUiSprites.RoundedPanel, new Color(.15f, .37f, .37f));
        RectTransform rt = skip.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-32f - slot * 88f, -104f);
        rt.sizeDelta = new Vector2(72f, 72f);
        RectTransform icon = CreatePanel("SkipIcon", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(46f, 46f), rt);
        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = GameUiSymbols.Get(GameUiSymbol.Skip);
        image.color = new Color(1, .99f, .91f);
        image.raycastTarget = false;
        if (rewarded)
        {
            rewardedSkipIcon = icon;
            icon.sizeDelta = new Vector2(46, 46);
            icon.anchoredPosition = Vector2.zero;
            RectTransform video = CreatePanel("VideoIcon", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), new Vector2(50, 36), rt);
            Image videoImage = video.gameObject.AddComponent<Image>();
            videoImage.sprite = BusesUiArt.Ad;
            videoImage.preserveAspect = true;
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
        }
        return skip;
    }

    private bool CanSkipCurrentLevel()
    {
        return gameManager != null && !CampaignGallery.IsOpen && !IsShopOpen
            && (completePanel == null || !completePanel.activeSelf)
            && (buyPopup == null || !buyPopup.activeSelf);
    }

    private void RefreshSkipButtons()
    {
        bool canSkip = !rewardedSkipPending && CanSkipCurrentLevel();
        if (rewardedSkipButton != null) rewardedSkipButton.interactable = canSkip;
        if (skipLevelButtonGo != null) skipLevelButtonGo.GetComponent<Button>().interactable = canSkip;
        if (rewardedSkipIcon != null)
        {
            rewardedSkipIcon.GetComponent<Image>().sprite = GameUiSymbols.Get(rewardedSkipPending ? GameUiSymbol.Restart : GameUiSymbol.Skip);
            rewardedSkipIcon.localRotation = rewardedSkipPending
                ? Quaternion.Euler(0, 0, -Time.unscaledTime * 180f) : Quaternion.identity;
        }
    }

    private void SkipCurrentLevelRewarded()
    {
        if (rewardedSkipPending || !CanSkipCurrentLevel()) return;
        if (AdManager.instance == null)
        {
            ShowAdUnavailable();
            return;
        }

        int current = gameManager.CurrentLevelIndex;
        int version = levelLoadVersion;
        rewardedSkipPending = true;
        RefreshLocalized();
        RefreshSkipButtons();
        AdManager.instance.StartRewarded(current.ToString(), "skip_level", () =>
        {
            if (this == null) return;
            rewardedSkipPending = false;
            RefreshLocalized();
            // Ignore an old reward if the player loaded or restarted a level during the ad.
            if (gameManager != null && version == levelLoadVersion
                && current == gameManager.CurrentLevelIndex && CanSkipCurrentLevel())
                AdvanceSkippedLevel("level_rewarded_skipped", "rewarded_skip");
            RefreshSkipButtons();
        }, () =>
        {
            if (this == null) return;
            rewardedSkipPending = false;
            RefreshLocalized();
            RefreshSkipButtons();
            ShowAdUnavailable();
        }, requireAdView: true);
    }

    private void ShowAdUnavailable()
    {
        if (noMoneyToast == null) return;
        ShowToast(CampaignText.Get("ad_unavailable"));
    }

#if UNITY_EDITOR
    private void SkipCurrentLevel()
    {
        if (rewardedSkipPending || !GameDebugSettings.ShowSkipLevelButton || !CanSkipCurrentLevel())
        {
            return;
        }

        AdvanceSkippedLevel("level_debug_skipped", "debug_skip");
    }
#endif

    private void AdvanceSkippedLevel(string eventName, string reason)
    {
        int current = gameManager.CurrentLevelIndex;
        Analytic.Track(eventName);
        Analytic.AbandonLevel(reason);
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
        card.Find("Level").GetComponent<TextMeshProUGUI>().text = gameManager.LevelCaption;
        int reward = CoinManager.instance != null ? CoinManager.instance.CoinsOnComplete : 50;
        card.Find("Reward/Amount").GetComponent<TextMeshProUGUI>().text = "+" + reward + " " + CampaignText.Get("coins");
        card.Find("ContinueBtn/Label").GetComponent<TextMeshProUGUI>().text = CampaignText.Get(gameManager.HasNextLevelInCampaign ? "victory_next" : "gallery");
        card.Find("DoubleBtn/Label").GetComponent<TextMeshProUGUI>().text = CampaignText.Get("victory_double");
    }
    private void BuildBuyPopup()
    {
        RectTransform root = CreatePanel("BoosterBuyPopup", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(.04f, .12f, .15f, .72f);
        root.gameObject.AddComponent<Button>().onClick.AddListener(CloseBuyPopup);
        buyPopup = root.gameObject;

        buyPopupCard = CreatePanel("Card", Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(620, 650), root);
        Image background = buyPopupCard.gameObject.AddComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(.97f, .98f, .93f);
        // Consume clicks inside the card, as in the settings menu.
        buyPopupCard.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        buyPopupTitle = CreateFixedTmp(buyPopupCard, "Title", "", 43, TextAlignmentOptions.Center,
            new Vector2(0, 246), new Vector2(540, 70));
        buyPopupTitle.color = new Color(.13f, .29f, .29f);
        buyPopupTitle.fontStyle = FontStyles.Bold;
        buyPopupTitle.enableAutoSizing = true;
        buyPopupTitle.fontSizeMin = 30;
        buyPopupTitle.fontSizeMax = 43;

        Image preview = CreateFixedImage(buyPopupCard, "BoosterPreview", HintUiSprites.RoundedPanel,
            new Vector2(0, 109), new Vector2(530, 150));
        preview.type = Image.Type.Sliced;
        preview.preserveAspect = false;
        preview.color = new Color(.89f, .94f, .90f);
        preview.raycastTarget = false;
        buyPopupIcon = CreateFixedImage(preview.rectTransform, "BoosterIcon", GameUiSymbols.Booster(BoosterType.Magnet),
            new Vector2(-38, 0), new Vector2(128, 128));
        buyPopupIcon.raycastTarget = false;
        var amount = CreateFixedTmp(preview.rectTransform, "Amount", "+" + BoosterCatalog.AdGrantCharges, 38,
            TextAlignmentOptions.Center, new Vector2(83, 0), new Vector2(100, 70));
        amount.color = new Color(.13f, .29f, .29f);
        amount.fontStyle = FontStyles.Bold;

        buyPopupAdLabel = CreateBuyPopupButton("AdBtn", -42, new Vector2(510, 96), new Color(1, .78f, .28f), new Color(.15f, .29f, .27f),
            BusesUiArt.Ad, BuyWithAd, 32);
        buyPopupPrice = CreateBuyPopupButton("CoinBtn", -150, new Vector2(510, 96), new Color(.89f, .94f, .90f), new Color(.13f, .29f, .29f),
            GameUiSymbols.Get(GameUiSymbol.Coins), BuyWithCoins, 32);
        buyPopupCloseLabel = CreateBuyPopupButton("CloseBtn", -246, new Vector2(510, 82), new Color(.97f, .98f, .93f), new Color(.13f, .29f, .29f),
            null, CloseBuyPopup);

        RefreshBuyPopupText();
        ApplyBuyPopupLayout();
        buyPopup.SetActive(false);
    }

    private TextMeshProUGUI CreateBuyPopupButton(string name, float y, Vector2 size, Color background, Color foreground,
        Sprite icon, UnityEngine.Events.UnityAction action, float fontSize = 30)
    {
        Button button = CreateStyledButton(buyPopupCard, name, "", HintUiSprites.RoundedPanel, action);
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(0, y);
        rect.sizeDelta = size;
        Image surface = button.GetComponent<Image>();
        surface.color = background;
        surface.preserveAspect = false;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.color = foreground;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 21;
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        float iconSize = Mathf.Min(56, size.y - 20);
        label.margin = new Vector4(icon != null ? iconSize + 40 : 20, 6, 20, 6);
        if (icon != null)
            CreateFixedImage(rect, "Icon", icon, new Vector2(-size.x * .5f + 24 + iconSize * .5f, 0), Vector2.one * iconSize).raycastTarget = false;
        return label;
    }

    private void RefreshBuyPopupText()
    {
        if (buyPopupTitle == null) return;
        buyPopupTitle.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(BoosterCatalog.GetLocalizationKey(pendingBuyType)) : pendingBuyType.ToString();
        buyPopupPrice.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("buy_for", BoosterCatalog.BuyPriceCoins) : "Buy for " + BoosterCatalog.BuyPriceCoins;
        buyPopupAdLabel.text = CampaignText.Get("watch_ad");
        buyPopupCloseLabel.text = CampaignText.Get("close");
    }

    private void ApplyBuyPopupLayout()
    {
        if (buyPopupCard == null || canvas == null) return;
        Rect safe = Screen.safeArea;
        float factor = Mathf.Max(.001f, canvas.scaleFactor);
        float scale = Mathf.Min(1f, (safe.width / factor - 40) / 620, (safe.height / factor - 40) / 650);
        buyPopupCard.localScale = Vector3.one * Mathf.Max(.1f, scale);
        buyPopupCard.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) * .5f) / factor;
    }

    private void BuildShopPanel()
    {
        RectTransform root = CreatePanel("ShopPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(.06f, .16f, .18f, .78f);
        shopPanel = root.gameObject;
        float width = Mathf.Min(1000, Screen.safeArea.width / Mathf.Max(.001f, canvas.scaleFactor) - 40);
        width = Mathf.Max(520, width);
        float height = Mathf.Min(1740, Screen.safeArea.height / Mathf.Max(.001f, canvas.scaleFactor) - 80);
        RectTransform card = CreatePanel("ShopCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, height), root);
        Image background = card.gameObject.AddComponent<Image>();
        background.sprite = HintUiSprites.RoundedPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(.94f, .97f, .92f);
        var title = CreateFixedTmp(card, "ShopTitle", LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText("shop") : "Shop", 44,
            TextAlignmentOptions.MidlineLeft, new Vector2(-width * .5f + 280, height*.5f-69), new Vector2(470, 70));
        title.color = new Color(.13f, .33f, .33f);
        title.fontStyle = FontStyles.Bold;
        RewardedShop shop = root.gameObject.AddComponent<RewardedShop>();
        Button close = CreateIconButton(card, "CloseShop", GameUiSymbols.Get(GameUiSymbol.Close), () => shop.CloseShop());
        close.GetComponent<Image>().color = new Color(.16f, .38f, .37f);
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(width * .5f - 64, height*.5f-66);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(76, 76);
        RectTransform viewport = CreatePanel("ShopViewport", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -100), new Vector2(width - 48, height-300), card);
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
        Button adGold = CreateRewardedGoldShopRow(content, PlaceBlockCenter(ref cursor, 116, 18), rowWidth, out TextMeshProUGUI adGoldLabel);
        shop.BindRewardedGold(adGold, adGoldLabel);
        Button premium = CreateKitShopRow(content, "premium_set", PlaceBlockCenter(ref cursor, 238, 18), GameUiSymbols.Get(GameUiSymbol.Bundle), RewardedShop.SmallBundleGoldAmount, RewardedShop.SmallBundleBoosterBonus, 238, rowWidth, out TextMeshProUGUI p2);
        Button remove = CreateRemoveAdsShopRow(content, PlaceBlockCenter(ref cursor, 274, 18), 274, rowWidth, out TextMeshProUGUI p6);
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
        {
            Button pack = CreateBoosterPackShopRow(content, type, PlaceBlockCenter(ref cursor, 132, 18), rowWidth, out TextMeshProUGUI packPrice);
            shop.BindBoosterPack(type, pack, packPrice);
        }
        Button g800 = CreateShopRow(content, "Gold " + RewardedShop.GoldSmallAmount, PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p3, 116, rowWidth);
        Button g1500 = CreateShopRow(content, "Gold " + RewardedShop.GoldMediumAmount, PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p4, 116, rowWidth);
        Button g3000 = CreateShopRow(content, "Gold " + RewardedShop.GoldLargeAmount, PlaceBlockCenter(ref cursor, 116, 18), GameUiSymbols.Get(GameUiSymbol.Coins), out TextMeshProUGUI p5, 116, rowWidth);
        content.sizeDelta = new Vector2(rowWidth, -cursor);
        var bundlesViewport=CreatePanel("BundlesViewport",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-100),new Vector2(width-48,height-300),card);
        bundlesViewport.gameObject.AddComponent<RectMask2D>();bundlesViewport.gameObject.AddComponent<Image>().color=new Color(1,1,1,.001f);
        var bundlesContent=CreatePanel("BundlesContent",new Vector2(.5f,1),new Vector2(.5f,1),Vector2.zero,new Vector2(rowWidth,1),bundlesViewport);
        bundlesContent.pivot=new Vector2(.5f,1);float bundleCursor=-8;
        foreach(var bundle in CosmeticBundleCatalog.All.OrderByDescending(entry=>entry.IsStyleBundle))CreateNatureBundleShopRow(bundlesContent,PlaceBlockCenter(ref bundleCursor,420,18),rowWidth,bundle);
        bundlesContent.sizeDelta=new Vector2(rowWidth,-bundleCursor);
        var bundlesScroll=bundlesViewport.gameObject.AddComponent<ScrollRect>();bundlesScroll.viewport=bundlesViewport;bundlesScroll.content=bundlesContent;bundlesScroll.horizontal=false;bundlesScroll.movementType=ScrollRect.MovementType.Clamped;
        root.gameObject.AddComponent<CosmeticShopTabs>().Initialize(card,viewport,bundlesViewport,width);
        shop.BindRuntime(CoinManager.instance, premium, g800, g1500, g3000, remove, p2, p3, p4, p5, p6);
        root.gameObject.SetActive(false);
    }

    private void CreateNatureBundleShopRow(RectTransform parent,float y,float width,CosmeticBundle bundle)
    {
        var button=CreateShopCard(parent,bundle==CosmeticBundleCatalog.Nature?"NatureBundleShopCard":bundle.Tag+"Card",y,width,420);
        var root=(RectTransform)button.transform;
        var title=CreateFixedTmp(root,"NatureTitle","",34,TextAlignmentOptions.MidlineLeft,new Vector2(-105,156),new Vector2(width-270,58));
        title.color=new Color(.13f,.33f,.33f);title.fontStyle=FontStyles.Bold;
        var price=CreateShopPrice(root,new Vector2(width*.5f-114,156));
        var hint=CreateFixedTmp(root,"NatureHint","",24,TextAlignmentOptions.Center,new Vector2(0,106),new Vector2(width-32,44));
        hint.color=new Color(.13f,.33f,.33f);
        float spacing=(width-64)/3;
        for(int i=0;i<bundle.Items.Length;i++)
        {
            var art=CreatePanel("NatureThumbnail_"+i,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2((i-1)*spacing,-34),new Vector2(spacing-18,210),root);
            art.gameObject.AddComponent<CosmeticBundlePreview>().SetItem(bundle.Items[i],bundle.Backgrounds[0],bundle.Items.FirstOrDefault(item=>item.Part==ThemeCatalog.Part.Conveyor).Theme);
            if(bundle.IsStyleBundle)
            {
                var caption=CreateFixedTmp(root,"StylePart_"+i,bundle.Items[i].PartName,21,TextAlignmentOptions.Center,
                    new Vector2((i-1)*spacing,-153),new Vector2(spacing-10,32));
                caption.color=new Color(.13f,.33f,.33f);
            }
        }
        var savings=CreateFixedTmp(root,"NatureSavings","",23,TextAlignmentOptions.Center,new Vector2(0,-165),new Vector2(width-32,44));
        savings.color=new Color(.37f,.30f,.16f);
        root.gameObject.AddComponent<NatureBundleShopCard>().Initialize(button,title,hint,price,savings,bundle);
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
        => CreateKitShopRow(parent, "remove_ads_bundle", y, GameUiSymbols.Get(GameUiSymbol.NoAds), RewardedShop.RemoveAdsGoldAmount, RewardedShop.RemoveAdsBoosterBonus, height, width, out price);

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

    private Button CreateRewardedGoldShopRow(RectTransform parent, float y, float width, out TextMeshProUGUI label)
    {
        Button button = CreateShopRow(parent, "Gold " + RewardedShop.RewardedGoldAmount, y, GameUiSymbols.Get(GameUiSymbol.Coins), out label, 116, width);
        var title = button.transform.Find("Title").GetComponent<RectTransform>();
        title.sizeDelta = new Vector2(width - 440, 58);
        title.anchoredPosition = new Vector2(-width * .5f + 112 + (width - 440) * .5f, 0);
        var badge = (RectTransform)label.transform.parent;
        badge.sizeDelta = new Vector2(260, 70);
        badge.anchoredPosition = new Vector2(width * .5f - 156, 0);
        CreateFixedImage(badge, "VideoIcon", GameUiSymbols.Get(GameUiSymbol.Video), new Vector2(-96, 0), new Vector2(42, 42)).raycastTarget = false;
        label.fontSizeMin = 18; label.fontSizeMax = 26;
        label.margin = new Vector4(50, 5, 8, 5);
        label.text = CampaignText.Get("watch_ad");
        return button;
    }

    private Button CreateBoosterPackShopRow(RectTransform parent, BoosterType type, float y, float width, out TextMeshProUGUI price)
    {
        Button button = CreateShopCard(parent, RewardedShop.GetBoosterPackTag(type) + "Btn", y, width, 132);
        var rt = (RectTransform)button.transform;
        var icon = CreateFixedImage(rt, "RowIcon", GameUiSymbols.Booster(type), new Vector2(-width * .5f + 60, 0), new Vector2(80, 80));
        icon.color = Color.white; icon.raycastTarget = false;
        string key = BoosterCatalog.GetLocalizationKey(type);
        string label = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : key;
        var title = CreateFixedTmp(rt, "Title", label + " ×" + RewardedShop.SingleBoosterPackCount, 34, TextAlignmentOptions.MidlineLeft,
            new Vector2(-width * .5f + 112 + (width - 360) * .5f, 0), new Vector2(width - 360, 70));
        title.color = new Color(.14f, .33f, .32f); title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true; title.fontSizeMin = 22; title.fontSizeMax = 34;
        price = CreateShopPrice(rt, new Vector2(width * .5f - 114, 0));
        return button;
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
