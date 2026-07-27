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
    private readonly TextMeshProUGUI[] boosterCharges = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] boosterUnlockLabels = new TextMeshProUGUI[3];
    private RectTransform handRect;
    private Image handImage;
    private Vector2 handTargetCenter;
    private float handTargetSize = 80f;
    private float handAnimTime;
    private bool handAnimating;
    private BoosterType? tutorialType;
    private bool tutorialWaitingActivation;
    private bool tutorialWaitingTarget;

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
        Analytic.LevelStarted(levelIndex.ToString());
        MirraSDK.Analytics.GameplayStart();
    }

    public void ShowLevelComplete()
    {
        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }

        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }

        Analytic.LevelCompleted(ProgressSaveService.CurrentLevelIndex.ToString());
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

        if (BoosterService.instance.ActiveTargetMode == type)
        {
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
            ApplySprite(iconImage, BusesUiArt.GetBoosterIcon(type), Color.white);
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
        tutorialType = null;
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
        tutorialWaitingActivation = true;

        if (tutorialType == BoosterType.Transparent)
        {
            EnsureBucketOnConveyorForTutorial();
        }

        if (gameManager != null)
        {
            GameUI ui = FindObjectOfType<GameUI>();
            if (ui != null && LocalizationManager.Instance != null)
            {
                ui.SetTutorialHint(LocalizationManager.Instance.GetText(BoosterCatalog.GetTutorialHintKey(tutorialType.Value)));
                ui.SetControlsInteractable(false);
            }
        }

        StartCoroutine(PointHandAtBoosterNextFrame(tutorialType.Value));
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

    private void EnsureBucketOnConveyorForTutorial()
    {
        if (gameManager == null || gameManager.Inventory == null)
        {
            return;
        }

        ConveyorBelt conveyor = FindObjectOfType<ConveyorBelt>();
        if (conveyor == null || conveyor.Entries.Count > 0)
        {
            return;
        }

        BucketInventoryUI inventoryUi = FindObjectOfType<BucketInventoryUI>();
        Bucket bucket = inventoryUi != null ? inventoryUi.GetFirstAccessibleBucket() : null;
        if (bucket == null && gameManager.Inventory.Buckets.Count > 0)
        {
            bucket = gameManager.Inventory.Buckets[0];
        }

        if (bucket != null)
        {
            BoosterMechanics.TryForcePlaceBucket(gameManager, bucket);
        }
    }

    private void FinishTutorial()
    {
        if (!tutorialType.HasValue)
        {
            return;
        }

        BoosterService.instance.MarkTutorialSeen(tutorialType.Value);
        tutorialType = null;
        tutorialWaitingActivation = false;
        tutorialWaitingTarget = false;
        HideHand();

        GameUI ui = FindObjectOfType<GameUI>();
        if (ui != null)
        {
            ui.SetHintVisible(false);
            ui.SetControlsInteractable(true);
        }
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

        if (!tutorialWaitingActivation)
        {
            HideHand();
        }
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
    }

    private void RefreshAll()
    {
        RefreshBoosters();
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
            if (boosterButtons[i] != null)
            {
                boosterButtons[i].interactable = unlocked;
                Image frame = boosterButtons[i].GetComponent<Image>();
                if (frame != null)
                {
                    Color frameColor = Color.white;
                    frameColor.a = unlocked ? 1f : 0.55f;
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
                        Sprite icon = BusesUiArt.GetBoosterIcon(type);
                        if (icon != null)
                        {
                            iconImage.sprite = icon;
                            iconImage.preserveAspect = true;
                            iconImage.type = Image.Type.Simple;
                        }

                        Color c = Color.white;
                        c.a = unlocked ? 1f : 0.4f;
                        iconImage.color = c;
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
                boosterCharges[i].text = unlocked ? charges.ToString() : string.Empty;
                boosterCharges[i].gameObject.SetActive(unlocked);
            }

            if (boosterUnlockLabels[i] != null)
            {
                int unlockLevel = BoosterCatalog.GetUnlockLevelIndex(type) + 1;
                string label = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("unlock_at_level", unlockLevel)
                    : ("Lv." + unlockLevel);
                boosterUnlockLabels[i].text = label;
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

        Transform stars = completePanel.transform.Find("Stars");
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

        Button shopBtn = CreateIconButton(root, "ShopBtn", BusesUiArt.Store, () =>
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
        shopRt.sizeDelta = new Vector2(52f, 52f);

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
        string label = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("skip_level")
            : "Skip";
        Button skip = CreateStyledButton(canvas.transform, "SkipLevelBtn", label, BusesUiArt.BtnBlue, SkipCurrentLevel);
        RectTransform rt = skip.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(140f, 56f);
        skipLevelButtonGo = skip.gameObject;
        skipLevelButtonGo.SetActive(GameDebugSettings.ShowSkipLevelButton);
    }

    private void SkipCurrentLevel()
    {
        if (gameManager == null)
        {
            return;
        }

        int current = gameManager.CurrentLevelIndex;
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
        RectTransform root = CreatePanel("BoosterBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(520f, 110f));
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        if (MobilePortraitLayout.Instance != null)
        {
            MobilePortraitLayout.Instance.ApplyLayout();
        }

        for (int i = 0; i < 3; i++)
        {
            BoosterType type = (BoosterType)i;
            GameObject buttonGo = new GameObject("Booster_" + type, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(root, false);
            RectTransform rt = buttonGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 96f);

            Image frame = buttonGo.GetComponent<Image>();
            ApplySprite(frame, BusesUiArt.FrameBlue, Color.white);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(buttonGo.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 8f);
            iconRt.sizeDelta = new Vector2(72f, 72f);
            Image iconImage = iconGo.GetComponent<Image>();
            ApplySprite(iconImage, BusesUiArt.GetBoosterIcon(type), Color.white);

            GameObject lockGo = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(buttonGo.transform, false);
            RectTransform lockRt = lockGo.GetComponent<RectTransform>();
            lockRt.anchoredPosition = Vector2.zero;
            lockRt.sizeDelta = new Vector2(40f, 40f);
            ApplySprite(lockGo.GetComponent<Image>(), BusesUiArt.Lock, Color.white);
            lockGo.SetActive(false);

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = frame;
            int captured = i;
            button.onClick.AddListener(() => HandleBoosterButton((BoosterType)captured));
            boosterButtons[i] = button;

            boosterCharges[i] = CreateTmp(rt, "Charges", "0", 20, TextAlignmentOptions.Center);
            boosterCharges[i].rectTransform.anchorMin = new Vector2(1f, 0f);
            boosterCharges[i].rectTransform.anchorMax = new Vector2(1f, 0f);
            boosterCharges[i].rectTransform.pivot = new Vector2(1f, 0f);
            boosterCharges[i].rectTransform.anchoredPosition = new Vector2(-6f, 4f);
            boosterCharges[i].rectTransform.sizeDelta = new Vector2(40f, 28f);
            boosterCharges[i].color = Color.white;

            int unlockLevel = BoosterCatalog.GetUnlockLevelIndex(type) + 1;
            string unlockText = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("unlock_at_level", unlockLevel)
                : ("Lv." + unlockLevel);
            boosterUnlockLabels[i] = CreateTmp(rt, "UnlockLevel", unlockText, 16, TextAlignmentOptions.Center);
            boosterUnlockLabels[i].rectTransform.anchorMin = new Vector2(0.5f, 0f);
            boosterUnlockLabels[i].rectTransform.anchorMax = new Vector2(0.5f, 0f);
            boosterUnlockLabels[i].rectTransform.pivot = new Vector2(0.5f, 0f);
            boosterUnlockLabels[i].rectTransform.anchoredPosition = new Vector2(0f, 4f);
            boosterUnlockLabels[i].rectTransform.sizeDelta = new Vector2(110f, 24f);
            boosterUnlockLabels[i].color = new Color(1f, 0.95f, 0.75f, 1f);
            boosterUnlockLabels[i].fontStyle = FontStyles.Bold;
        }
    }

    private void BuildCompletePanel()
    {
        RectTransform root = CreatePanel("LevelCompletePanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        completePanel = root.gameObject;

        RectTransform card = CreatePanel("Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 460f), root);
        Image cardImage = card.gameObject.AddComponent<Image>();
        ApplySprite(cardImage, BusesUiArt.FrameBlue, Color.white);

        GameObject ribbon = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
        ribbon.transform.SetParent(card, false);
        RectTransform ribbonRt = ribbon.GetComponent<RectTransform>();
        ribbonRt.anchoredPosition = new Vector2(0f, 170f);
        ribbonRt.sizeDelta = new Vector2(360f, 70f);
        ApplySprite(ribbon.GetComponent<Image>(), BusesUiArt.RibbonYellow, Color.white);

        levelCompleteTitle = CreateTmp(card, "Title", "Complete", 36, TextAlignmentOptions.Center);
        levelCompleteTitle.rectTransform.anchoredPosition = new Vector2(0f, 170f);
        levelCompleteTitle.color = new Color(0.2f, 0.1f, 0.05f, 1f);

        GameObject trophy = new GameObject("Trophy", typeof(RectTransform), typeof(Image));
        trophy.transform.SetParent(card, false);
        RectTransform trophyRt = trophy.GetComponent<RectTransform>();
        trophyRt.anchoredPosition = new Vector2(0f, 95f);
        trophyRt.sizeDelta = new Vector2(64f, 64f);
        ApplySprite(trophy.GetComponent<Image>(), BusesUiArt.Trophy, Color.white);

        RectTransform stars = CreatePanel("Stars", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(300f, 80f), card);
        for (int i = 0; i < 3; i++)
        {
            GameObject star = new GameObject("Star" + i, typeof(RectTransform), typeof(Image));
            star.transform.SetParent(stars, false);
            RectTransform srt = star.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(72f, 72f);
            srt.anchoredPosition = new Vector2((i - 1) * 96f, 0f);
            ApplySprite(star.GetComponent<Image>(), BusesUiArt.Star, Color.white);
            star.SetActive(false);
        }

        Button continueBtn = CreateStyledButton(card, "ContinueBtn", "Continue", BusesUiArt.BtnGreen, OnCompleteContinue);
        continueBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-120f, -140f);
        Button doubleBtn = CreateStyledButton(card, "DoubleBtn", "x2", BusesUiArt.BtnBlue, OnCompleteDouble);
        doubleBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(120f, -140f);

        GameObject adIcon = new GameObject("AdIcon", typeof(RectTransform), typeof(Image));
        adIcon.transform.SetParent(doubleBtn.transform, false);
        RectTransform adRt = adIcon.GetComponent<RectTransform>();
        adRt.anchorMin = new Vector2(0f, 0.5f);
        adRt.anchorMax = new Vector2(0f, 0.5f);
        adRt.anchoredPosition = new Vector2(28f, 0f);
        adRt.sizeDelta = new Vector2(28f, 28f);
        ApplySprite(adIcon.GetComponent<Image>(), BusesUiArt.Ad, Color.white);

        completePanel.SetActive(false);
    }

    private void BuildBuyPopup()
    {
        RectTransform root = CreatePanel("BoosterBuyPopup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 320f));
        Image bg = root.gameObject.AddComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameBlue, Color.white);
        buyPopup = root.gameObject;

        buyPopupTitle = CreateTmp(root, "Title", "Booster", 30, TextAlignmentOptions.Center);
        buyPopupTitle.rectTransform.anchoredPosition = new Vector2(0f, 110f);

        buyPopupPrice = CreateTmp(root, "Price", "Buy for 90", 22, TextAlignmentOptions.Center);
        buyPopupPrice.rectTransform.anchoredPosition = new Vector2(0f, 55f);

        Button adBtn = CreateStyledButton(root, "AdBtn", "Watch Ad", BusesUiArt.BtnBlue, BuyWithAd);
        adBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110f, -30f);
        Button coinBtn = CreateStyledButton(root, "CoinBtn", "Coins", BusesUiArt.BtnGreen, BuyWithCoins);
        coinBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(110f, -30f);

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
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        shopPanel = root.gameObject;

        RectTransform card = CreatePanel("ShopCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 820f), root);
        Image cardImage = card.gameObject.AddComponent<Image>();
        ApplySprite(cardImage, BusesUiArt.FrameBlue, Color.white);

        string shopTitle = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("shop")
            : "Shop";
        CreateFixedTmp(card, "ShopTitle", shopTitle, 36, TextAlignmentOptions.Center, new Vector2(0f, 370f), new Vector2(400f, 48f));

        // Scroll area so last block never leaves the panel.
        RectTransform viewport = CreatePanel("ShopViewport", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(560f, 680f), card);
        Image viewportMask = viewport.gameObject.AddComponent<Image>();
        viewportMask.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentGo = new GameObject("ShopContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewport, false);
        RectTransform content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(540f, 100f);

        ScrollRect scroll = card.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        const float gap = 14f;
        const float kitHeight = 170f;
        const float goldHeight = 60f;
        const float removeHeight = 186f;
        const float closeHeight = 60f;
        const float rowWidth = 520f;

        float cursor = -8f;
        float beginnerY = PlaceBlockCenter(ref cursor, kitHeight, gap);
        float premiumY = PlaceBlockCenter(ref cursor, kitHeight, gap);
        float g300Y = PlaceBlockCenter(ref cursor, goldHeight, gap);
        float g700Y = PlaceBlockCenter(ref cursor, goldHeight, gap);
        float g1200Y = PlaceBlockCenter(ref cursor, goldHeight, gap);
        float removeY = PlaceBlockCenter(ref cursor, removeHeight, gap);
        float closeY = PlaceBlockCenter(ref cursor, closeHeight, 0f);
        content.sizeDelta = new Vector2(rowWidth, Mathf.Abs(cursor) + 12f);

        RewardedShop shop = shopPanel.AddComponent<RewardedShop>();
        Button beginner = CreateKitShopRow(content, "beginners_kit", beginnerY, BusesUiArt.Trophy, 4000, 5, kitHeight, rowWidth, out TextMeshProUGUI p1);
        Button premium = CreateKitShopRow(content, "premium_set", premiumY, BusesUiArt.Star, 1000, 2, kitHeight, rowWidth, out TextMeshProUGUI p2);
        Button g300 = CreateShopRow(content, "Gold 300", g300Y, BusesUiArt.Gold, out TextMeshProUGUI p3, goldHeight, rowWidth);
        Button g700 = CreateShopRow(content, "Gold 700", g700Y, BusesUiArt.Gold, out TextMeshProUGUI p4, goldHeight, rowWidth);
        Button g1200 = CreateShopRow(content, "Gold 1200", g1200Y, BusesUiArt.Gold, out TextMeshProUGUI p5, goldHeight, rowWidth);
        Button remove = CreateRemoveAdsShopRow(content, removeY, removeHeight, rowWidth, out TextMeshProUGUI p6);
        shop.BindRuntime(CoinManager.instance, beginner, premium, g300, g700, g1200, remove, p1, p2, p3, p4, p5, p6);

        Button close = CreateStyledButton(content, "CloseShop", "Close", BusesUiArt.BtnRed, () => shop.CloseShop());
        RectTransform closeRt = close.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 1f);
        closeRt.anchorMax = new Vector2(0.5f, 1f);
        closeRt.pivot = new Vector2(0.5f, 0.5f);
        closeRt.sizeDelta = new Vector2(220f, closeHeight);
        closeRt.anchoredPosition = new Vector2(0f, closeY);
        shopPanel.SetActive(false);
    }

    private static float PlaceBlockCenter(ref float cursor, float height, float gapAfter)
    {
        // Content is top-pivoted; return center Y for center-anchored blocks.
        float centerY = cursor - height * 0.5f;
        cursor -= height + gapAfter;
        return centerY;
    }

    private Button CreateKitShopRow(
        RectTransform parent,
        string titleKey,
        float y,
        Sprite icon,
        int goldAmount,
        int boosterBonus,
        float height,
        float width,
        out TextMeshProUGUI price)
    {
        string title = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(titleKey)
            : titleKey;

        GameObject go = new GameObject(titleKey + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, y);
        Image bg = go.GetComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameWhite, Color.white);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;

        CreateFixedImage(rt, "RowIcon", icon, new Vector2(-200f, 48f), new Vector2(48f, 48f));
        CreateFixedTmp(rt, "Title", title, 22, TextAlignmentOptions.Left, new Vector2(-20f, 52f), new Vector2(280f, 30f))
            .color = new Color(0.15f, 0.1f, 0.05f, 1f);

        // 2x2 chips fully inside padded bounds (half-height ~85)
        const float chipW = 148f;
        const float chipH = 48f;
        const float chipGapX = 10f;
        float leftX = -(chipW + chipGapX) * 0.5f;
        float rightX = (chipW + chipGapX) * 0.5f;
        float row1Y = 2f;
        float row2Y = -54f;

        CreateKitChip(rt, "GoldChip", BusesUiArt.Gold, goldAmount.ToString(), new Vector2(leftX, row1Y), chipW, chipH);
        CreateKitChip(rt, "MagnetChip", BusesUiArt.Magnet, "+" + boosterBonus, new Vector2(rightX, row1Y), chipW, chipH);
        CreateKitChip(rt, "FillChip", BusesUiArt.Transparent, "+" + boosterBonus, new Vector2(leftX, row2Y), chipW, chipH);
        CreateKitChip(rt, "ShuffleChip", BusesUiArt.Shuffle, "+" + boosterBonus, new Vector2(rightX, row2Y), chipW, chipH);

        price = CreateFixedTmp(rt, "Price", "...", 20, TextAlignmentOptions.Right, new Vector2(200f, 52f), new Vector2(100f, 32f));
        price.color = new Color(0.15f, 0.1f, 0.05f, 1f);
        return button;
    }

    private Button CreateRemoveAdsShopRow(RectTransform parent, float y, float height, float width, out TextMeshProUGUI price)
    {
        string title = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("remove_ads_bundle")
            : "Remove ads bundle";
        string info = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("reamoveAdDescription")
            : "Disables ads during gameplay";

        GameObject go = new GameObject("remove_ads_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, y);
        Image bg = go.GetComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameWhite, Color.white);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;

        CreateFixedImage(rt, "RowIcon", BusesUiArt.RemoveAds, new Vector2(-200f, 56f), new Vector2(48f, 48f));
        CreateFixedTmp(rt, "Title", title, 22, TextAlignmentOptions.Left, new Vector2(-20f, 60f), new Vector2(280f, 28f))
            .color = new Color(0.15f, 0.1f, 0.05f, 1f);
        CreateFixedTmp(rt, "Description", info, 14, TextAlignmentOptions.Left, new Vector2(-10f, 32f), new Vector2(300f, 24f))
            .color = new Color(0.25f, 0.2f, 0.15f, 1f);

        const float chipW = 148f;
        const float chipH = 48f;
        const float chipGapX = 10f;
        float leftX = -(chipW + chipGapX) * 0.5f;
        float rightX = (chipW + chipGapX) * 0.5f;
        float row1Y = -8f;
        float row2Y = -64f;

        CreateKitChip(rt, "GoldChip", BusesUiArt.Gold, "4000", new Vector2(leftX, row1Y), chipW, chipH);
        CreateKitChip(rt, "MagnetChip", BusesUiArt.Magnet, "+5", new Vector2(rightX, row1Y), chipW, chipH);
        CreateKitChip(rt, "FillChip", BusesUiArt.Transparent, "+5", new Vector2(leftX, row2Y), chipW, chipH);
        CreateKitChip(rt, "ShuffleChip", BusesUiArt.Shuffle, "+5", new Vector2(rightX, row2Y), chipW, chipH);

        price = CreateFixedTmp(rt, "Price", "...", 20, TextAlignmentOptions.Right, new Vector2(200f, 56f), new Vector2(100f, 32f));
        price.color = new Color(0.15f, 0.1f, 0.05f, 1f);
        return button;
    }

    private void CreateKitChip(RectTransform parent, string name, Sprite icon, string value, Vector2 anchored, float width, float height)
    {
        GameObject chip = new GameObject(name, typeof(RectTransform), typeof(Image));
        chip.transform.SetParent(parent, false);
        RectTransform rt = chip.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = new Vector2(width, height);
        ApplySprite(chip.GetComponent<Image>(), BusesUiArt.FrameWhite, new Color(0.95f, 0.95f, 0.95f, 0.98f));

        CreateFixedImage(rt, "Icon", icon, new Vector2(-width * 0.28f, 0f), new Vector2(34f, 34f));
        TextMeshProUGUI valueTmp = CreateFixedTmp(
            rt,
            "Value",
            value,
            22,
            TextAlignmentOptions.Left,
            new Vector2(width * 0.12f, 0f),
            new Vector2(70f, 36f));
        valueTmp.color = new Color(0.12f, 0.08f, 0.04f, 1f);
        valueTmp.fontStyle = FontStyles.Bold;
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

    private Button CreateShopRow(
        RectTransform parent,
        string label,
        float y,
        Sprite icon,
        out TextMeshProUGUI price,
        float height = 60f,
        float width = 520f)
    {
        GameObject go = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, y);
        Image bg = go.GetComponent<Image>();
        ApplySprite(bg, BusesUiArt.FrameWhite, Color.white);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;

        CreateFixedImage(rt, "RowIcon", icon, new Vector2(-200f, 0f), new Vector2(40f, 40f));
        CreateFixedTmp(rt, "Label", label, 22, TextAlignmentOptions.Left, new Vector2(-40f, 0f), new Vector2(280f, 36f))
            .color = new Color(0.15f, 0.1f, 0.05f, 1f);

        price = CreateFixedTmp(rt, "Price", "...", 18, TextAlignmentOptions.Right, new Vector2(200f, 0f), new Vector2(100f, 36f));
        price.color = new Color(0.15f, 0.1f, 0.05f, 1f);
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
