using System;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardedShop : MonoBehaviour
{
    [SerializeField] private int beginnerKitBonus = 5;
    [SerializeField] private int beginnerKitBonusGold = 4000;
    [SerializeField] private int premiumKitBonus = 2;
    [SerializeField] private int premiumKitBonusGold = 1000;
    [SerializeField] private int gold300 = 300;
    [SerializeField] private int gold700 = 700;
    [SerializeField] private int gold1200 = 1200;
    [SerializeField] private CoinManager coinManager;

    [SerializeField] private TextMeshProUGUI beginnerKitPriceText;
    [SerializeField] private TextMeshProUGUI premiumKitPriceText;
    [SerializeField] private TextMeshProUGUI gold300PriceText;
    [SerializeField] private TextMeshProUGUI gold700PriceText;
    [SerializeField] private TextMeshProUGUI gold1200PriceText;
    [SerializeField] private TextMeshProUGUI removeAdPriceText;

    [SerializeField] private Button beginnerKitButton;
    [SerializeField] private Button premiumKitButton;
    [SerializeField] private Button gold300Button;
    [SerializeField] private Button gold700Button;
    [SerializeField] private Button gold1200Button;
    [SerializeField] private Button removeAdButton;

    public static Action OnNoAdsBuy;
    public bool IsAdActive = true;

    private const string BeginnerKitTag = "beginerKit_150";
    private const string PremiumKitTag = "premium_kit_75";
    private const string Gold300Tag = "gold_300";
    private const string Gold700Tag = "gold_700";
    private const string Gold1200Tag = "gold_1200";
    private const string RemoveAdTag = "removeAd";

    private void Awake()
    {
        LoadDataOrInit();
        WireButton(beginnerKitButton, BuyBeginnerKit);
        WireButton(premiumKitButton, BuyPremiumKit);
        WireButton(gold300Button, BuyGold300);
        WireButton(gold700Button, BuyGold700);
        WireButton(gold1200Button, BuyGold1200);
        WireButton(removeAdButton, BuyRemoveAd);
    }

    private void Start()
    {
        StartCoroutine(InitPaymentsRoutine());
    }

    private System.Collections.IEnumerator InitPaymentsRoutine()
    {
        yield return new WaitUntil(() => MirraSDK.IsInitialized);
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
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }

        MirraSDK.Analytics.GameplayStop();
        gameObject.SetActive(true);
        SetCurrencies();
    }

    public void CloseShop()
    {
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }

        MirraSDK.Analytics.GameplayStart();
        gameObject.SetActive(false);
    }

    public void BindRuntime(
        CoinManager coins,
        Button beginner,
        Button premium,
        Button g300,
        Button g700,
        Button g1200,
        Button removeAds,
        TextMeshProUGUI beginnerPrice,
        TextMeshProUGUI premiumPrice,
        TextMeshProUGUI g300Price,
        TextMeshProUGUI g700Price,
        TextMeshProUGUI g1200Price,
        TextMeshProUGUI removePrice)
    {
        coinManager = coins;
        beginnerKitButton = beginner;
        premiumKitButton = premium;
        gold300Button = g300;
        gold700Button = g700;
        gold1200Button = g1200;
        removeAdButton = removeAds;
        beginnerKitPriceText = beginnerPrice;
        premiumKitPriceText = premiumPrice;
        gold300PriceText = g300Price;
        gold700PriceText = g700Price;
        gold1200PriceText = g1200Price;
        removeAdPriceText = removePrice;

        WireButton(beginnerKitButton, BuyBeginnerKit);
        WireButton(premiumKitButton, BuyPremiumKit);
        WireButton(gold300Button, BuyGold300);
        WireButton(gold700Button, BuyGold700);
        WireButton(gold1200Button, BuyGold1200);
        WireButton(removeAdButton, BuyRemoveAd);
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
                && !MirraSDK.Payments.IsAlreadyPurchased(productTag: RemoveAdTag);
        }
        catch
        {
            IsAdActive = !GameProgressStore.GetBool("no_ads");
        }
    }

    private void RestorePurchases()
    {
        MirraSDK.Payments.RestorePurchases(fetchData =>
        {
            fetchData.RestoreProduct(RemoveAdTag, () => ApplyPurchase(RemoveAdTag));
        });
    }

    private void SetCurrencies()
    {
        SetPrice(beginnerKitPriceText, BeginnerKitTag);
        SetPrice(premiumKitPriceText, PremiumKitTag);
        SetPrice(gold300PriceText, Gold300Tag);
        SetPrice(gold700PriceText, Gold700Tag);
        SetPrice(gold1200PriceText, Gold1200Tag);
        SetPrice(removeAdPriceText, RemoveAdTag);
    }

    private static void SetPrice(TextMeshProUGUI label, string tag)
    {
        if (label == null)
        {
            return;
        }

        try
        {
            ProductData productData = MirraSDK.Payments.GetProductData(tag);
            label.text = productData.GetFullPriceFloat() + productData.Currency;
        }
        catch (Exception)
        {
            label.text = tag;
        }
    }

    private void BuyBeginnerKit() => Purchase(BeginnerKitTag);
    private void BuyPremiumKit() => Purchase(PremiumKitTag);
    private void BuyGold300() => Purchase(Gold300Tag);
    private void BuyGold700() => Purchase(Gold700Tag);
    private void BuyGold1200() => Purchase(Gold1200Tag);
    private void BuyRemoveAd() => Purchase(RemoveAdTag);

    private void Purchase(string tag)
    {
        MirraSDK.Payments.Purchase(
            productTag: tag,
            onSuccess: () => ApplyPurchase(tag),
            onError: () => Debug.LogWarning("Failed to purchase " + tag));
    }

    private void ApplyPurchase(string purchaseKey)
    {
        BoosterService boosters = BoosterService.instance;
        CoinManager coins = coinManager != null ? coinManager : CoinManager.instance;

        switch (purchaseKey)
        {
            case BeginnerKitTag:
                boosters?.GrantChargesToAll(beginnerKitBonus);
                coins?.AddCoinsOnBuy(beginnerKitBonusGold);
                break;
            case PremiumKitTag:
                boosters?.GrantChargesToAll(premiumKitBonus);
                coins?.AddCoinsOnBuy(premiumKitBonusGold);
                break;
            case Gold300Tag:
                coins?.AddCoinsOnBuy(gold300);
                break;
            case Gold700Tag:
                coins?.AddCoinsOnBuy(gold700);
                break;
            case Gold1200Tag:
                coins?.AddCoinsOnBuy(gold1200);
                break;
            case RemoveAdTag:
                IsAdActive = false;
                GameProgressStore.SetBool("no_ads", true);
                GameProgressStore.Save();
                boosters?.GrantChargesToAll(beginnerKitBonus);
                coins?.AddCoinsOnBuy(beginnerKitBonusGold);
                OnNoAdsBuy?.Invoke();
                break;
        }

        Analytic.BuyInApp(ProgressSaveService.CurrentLevelIndex.ToString(), purchaseKey);
    }
}
