using System;
using System.Collections.Generic;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardedShop : MonoBehaviour
{
    public const int SmallBundleGoldAmount = 1000;
    public const int RemoveAdsGoldAmount = 4000;
    public const int GoldSmallAmount = 800;
    public const int GoldMediumAmount = 1500;
    public const int GoldLargeAmount = 3000;
    public const int SmallBundleBoosterBonus = 5;
    public const int RemoveAdsBoosterBonus = 10;
    public const int SingleBoosterPackCount = 10;
    public const int RewardedGoldAmount = 100;
    private Button rewardedGoldButton;
    private TextMeshProUGUI rewardedGoldLabel;
    private bool rewardedGoldPending;
    private bool paymentsInitializationStarted;
    private bool purchasePending;
    private readonly Dictionary<BoosterType, (Button button, TextMeshProUGUI price)> boosterPacks
        = new Dictionary<BoosterType, (Button button, TextMeshProUGUI price)>();
    [SerializeField] private CoinManager coinManager;

    [SerializeField] private TextMeshProUGUI premiumKitPriceText;
    [SerializeField] private TextMeshProUGUI gold800PriceText;
    [SerializeField] private TextMeshProUGUI gold1500PriceText;
    [SerializeField] private TextMeshProUGUI gold3000PriceText;
    [SerializeField] private TextMeshProUGUI removeAdPriceText;

    [SerializeField] private Button premiumKitButton;
    [SerializeField] private Button gold800Button;
    [SerializeField] private Button gold1500Button;
    [SerializeField] private Button gold3000Button;
    [SerializeField] private Button removeAdButton;

    private float shopTime;
    public static Action OnNoAdsBuy;
    public bool IsAdActive = true;

    private const string PremiumKitTag = "premium_kit_75";
    // Product IDs match the amount granted by each gold pack.
    private const string Gold800Tag = "gold_800";
    private const string Gold1500Tag = "gold_1500";
    private const string Gold3000Tag = "gold_3000";
    private const string RemoveAdTag = "removeAd";
    private static readonly string[] ShopProductTags =
    {
        PremiumKitTag, Gold800Tag, Gold1500Tag, Gold3000Tag, RemoveAdTag,
        "booster_magnet_10", "booster_fill_10", "booster_shuffle_10"
    };

    private void Awake()
    {
        LoadDataOrInit();
        WireButton(premiumKitButton, BuyPremiumKit);
        WireButton(gold800Button, BuyGold800);
        WireButton(gold1500Button, BuyGold1500);
        WireButton(gold3000Button, BuyGold3000);
        WireButton(removeAdButton, BuyRemoveAd);
    }

    private void Start()
    {
        InitializePayments(this);
    }

    public void InitializePayments(MonoBehaviour coroutineHost)
    {
        if (paymentsInitializationStarted) return;
        paymentsInitializationStarted = true;
        // The HUD remains active while the shop is closed, including during SDK startup.
        coroutineHost.StartCoroutine(InitPaymentsRoutine());
    }

    private System.Collections.IEnumerator InitPaymentsRoutine()
    {
        yield return new WaitUntil(() => GameSdkReadiness.IsReady);
        if (this == null) yield break;
        LoadDataOrInit();
        RestorePurchases();
        SetCurrencies();
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange += SetCurrencies;
        }
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.onLanguageChange -= SetCurrencies;
        }
    }

    public void OpenShop()
    {
        if(!gameObject.activeSelf){shopTime=Time.timeScale;Time.timeScale=0;}
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }

        GameIsReady.SetWindowOpen(this, true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SetCurrencies();
    }

    public void CloseShop()
    {
        if(!gameObject.activeSelf)return;
        var offer=FindObjectOfType<NatureBackgroundOffer>();if(offer!=null && offer.IsOpen)return;
        var tabs=GetComponent<CosmeticShopTabs>();if(tabs!=null && !tabs.CanClose)return;
        Time.timeScale=shopTime;
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }

        gameObject.SetActive(false);
        GameIsReady.SetWindowOpen(this, false);
    }

    private void OnDisable() => GameIsReady.SetWindowOpen(this, false);

    public void BindRuntime(
        CoinManager coins,
        Button premium,
        Button g800,
        Button g1500,
        Button g3000,
        Button removeAds,
        TextMeshProUGUI premiumPrice,
        TextMeshProUGUI g800Price,
        TextMeshProUGUI g1500Price,
        TextMeshProUGUI g3000Price,
        TextMeshProUGUI removePrice)
    {
        coinManager = coins;
        premiumKitButton = premium;
        gold800Button = g800;
        gold1500Button = g1500;
        gold3000Button = g3000;
        removeAdButton = removeAds;
        premiumKitPriceText = premiumPrice;
        gold800PriceText = g800Price;
        gold1500PriceText = g1500Price;
        gold3000PriceText = g3000Price;
        removeAdPriceText = removePrice;

        WireButton(premiumKitButton, BuyPremiumKit);
        WireButton(gold800Button, BuyGold800);
        WireButton(gold1500Button, BuyGold1500);
        WireButton(gold3000Button, BuyGold3000);
        WireButton(removeAdButton, BuyRemoveAd);
    }

    public static string GetBoosterPackTag(BoosterType type)
    {
        switch (type)
        {
            case BoosterType.Magnet: return "booster_magnet_10";
            case BoosterType.Transparent: return "booster_fill_10";
            case BoosterType.Shuffle: return "booster_shuffle_10";
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public void BindBoosterPack(BoosterType type, Button button, TextMeshProUGUI price)
    {
        string tag = GetBoosterPackTag(type);
        boosterPacks[type] = (button, price);
        WireButton(button, () => Purchase(tag));
        SetPrice(price, tag, button);
    }

    public void BindRewardedGold(Button button, TextMeshProUGUI label)
    {
        rewardedGoldButton = button;
        rewardedGoldLabel = label;
        WireButton(button, WatchAdForGold);
        RefreshRewardedGold();
    }

    private void Update() => RefreshRewardedGold();

    private void RefreshRewardedGold()
    {
        if (rewardedGoldButton == null) return;
        rewardedGoldButton.interactable = !rewardedGoldPending && AdManager.instance != null
            && AdManager.instance.CanStartRewarded(true);
        if (rewardedGoldLabel != null)
            rewardedGoldLabel.text = CampaignText.Get(rewardedGoldPending ? "ad_wait" : "watch_ad");
    }

    private void WatchAdForGold()
    {
        CoinManager coins = coinManager != null ? coinManager : CoinManager.instance;
        if (rewardedGoldPending || coins == null || AdManager.instance == null
            || !AdManager.instance.CanStartRewarded(true)) { RefreshRewardedGold(); return; }
        rewardedGoldPending = true;
        RefreshRewardedGold();
        AdManager.instance.StartRewarded(ProgressSaveService.CurrentLevelIndex.ToString(), "shop_coins_100",
            () => { if (coins != null) coins.AddCoinsOnBuy(RewardedGoldAmount); FinishGoldAd(); },
            FinishGoldAd, true);
    }

    private void FinishGoldAd()
    {
        if (this == null) return;
        rewardedGoldPending = false;
        // The SDK resumes time after an ad; the open shop must keep the level paused.
        if (gameObject.activeInHierarchy) Time.timeScale = 0;
        RefreshRewardedGold();
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void LoadDataOrInit()
    {
        try
        {
            IsAdActive = !GameProgressStore.GetBool("no_ads")
                && (!GameSdkReadiness.IsReady
                    || !MirraSDK.Payments.IsAlreadyPurchased(productTag: RemoveAdTag));
        }
        catch
        {
            IsAdActive = !GameProgressStore.GetBool("no_ads");
        }
    }

    private void RestorePurchases()
    {
        if (!GameSdkReadiness.IsReady) return;
        MirraSDK.Payments.RestorePurchases(fetchData =>
        {
            if (fetchData == null || this == null) return;
            foreach (string tag in ShopProductTags)
                fetchData.RestoreProduct(tag, () => ApplyPurchase(tag));
            // A non-consumable remains owned even when its bonus was supplied earlier.
            if (fetchData.AllPurchases != null && Array.IndexOf(fetchData.AllPurchases, RemoveAdTag) >= 0
                && !GameProgressStore.GetBool("no_ads"))
            {
                GameProgressStore.SetBool("no_ads", true);
                GameProgressStore.Save();
            }
            LoadDataOrInit();
            SetCurrencies();
        });
    }

    private void SetCurrencies()
    {
        SetPrice(premiumKitPriceText, PremiumKitTag, premiumKitButton);
        SetPrice(gold800PriceText, Gold800Tag, gold800Button);
        SetPrice(gold1500PriceText, Gold1500Tag, gold1500Button);
        SetPrice(gold3000PriceText, Gold3000Tag, gold3000Button);
        SetPrice(removeAdPriceText, RemoveAdTag, removeAdButton);
        foreach (var pack in boosterPacks)
            SetPrice(pack.Value.price, GetBoosterPackTag(pack.Key), pack.Value.button);
        if (!IsAdActive)
        {
            if (removeAdPriceText != null) removeAdPriceText.text = CampaignText.Get("purchased");
            if (removeAdButton != null) removeAdButton.interactable = false;
        }
    }

    private static void SetPrice(TextMeshProUGUI label, string tag, Button button)
    {
        if (label == null)
        {
            return;
        }

        if (!GameSdkReadiness.IsReady)
        {
            label.text = CampaignText.Get("unavailable");
            if (button != null) button.interactable = false;
            return;
        }

        try
        {
            ProductData productData = MirraSDK.Payments.GetProductData(tag);
            label.text = productData.GetFullPriceFloat();
            if (button != null) button.interactable = true;
        }
        catch (Exception)
        {
            label.text = CampaignText.Get("unavailable");
            if (button != null) button.interactable = false;
        }
    }

    private void BuyPremiumKit() => Purchase(PremiumKitTag);
    private void BuyGold800() => Purchase(Gold800Tag);
    private void BuyGold1500() => Purchase(Gold1500Tag);
    private void BuyGold3000() => Purchase(Gold3000Tag);
    private void BuyRemoveAd() => Purchase(RemoveAdTag);

    private void Purchase(string tag)
    {
        if (purchasePending || !GameSdkReadiness.IsReady || !IsShopProduct(tag)) return;
        if (tag == RemoveAdTag)
        {
            LoadDataOrInit();
            if (!IsAdActive) { SetCurrencies(); return; }
        }
        purchasePending = true;
        bool settled = false;
        MirraSDK.Payments.Purchase(
            productTag: tag,
            onSuccess: () =>
            {
                if (settled) return;
                settled = true;
                purchasePending = false;
                ApplyPurchase(tag);
                Analytic.BuyInApp(ProgressSaveService.CurrentLevelIndex.ToString(), tag);
                SetCurrencies();
            },
            onError: () =>
            {
                if (settled) return;
                settled = true;
                purchasePending = false;
                Debug.LogWarning("Failed to purchase " + tag);
            });
    }

    private static bool IsShopProduct(string tag)
    {
        return Array.IndexOf(ShopProductTags, tag) >= 0;
    }

    private void ApplyPurchase(string purchaseKey)
    {
        BoosterService boosters = BoosterService.instance;
        CoinManager coins = coinManager != null ? coinManager : CoinManager.instance;

        switch (purchaseKey)
        {
            case PremiumKitTag:
                boosters?.GrantChargesToAll(SmallBundleBoosterBonus);
                coins?.AddCoinsOnBuy(SmallBundleGoldAmount);
                break;
            case "booster_magnet_10":
                boosters?.GrantCharges(BoosterType.Magnet, SingleBoosterPackCount);
                break;
            case "booster_fill_10":
                boosters?.GrantCharges(BoosterType.Transparent, SingleBoosterPackCount);
                break;
            case "booster_shuffle_10":
                boosters?.GrantCharges(BoosterType.Shuffle, SingleBoosterPackCount);
                break;
            case Gold800Tag:
                coins?.AddCoinsOnBuy(GoldSmallAmount);
                break;
            case Gold1500Tag:
                coins?.AddCoinsOnBuy(GoldMediumAmount);
                break;
            case Gold3000Tag:
                coins?.AddCoinsOnBuy(GoldLargeAmount);
                break;
            case RemoveAdTag:
                IsAdActive = false;
                if (GameProgressStore.GetBool("no_ads")) break;
                boosters?.GrantChargesToAll(RemoveAdsBoosterBonus);
                coins?.AddCoinsOnBuy(RemoveAdsGoldAmount);
                GameProgressStore.SetBool("no_ads", true);
                GameProgressStore.Save();
                OnNoAdsBuy?.Invoke();
                break;
        }
    }
}
