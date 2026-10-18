// Compile together with the real AdManager, Analytic, LevelAnalyticsSession and RewardedAttempt.
// SDK doubles exercise callbacks without showing ads or making network requests.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class GameObject { public bool activeSelf=true; public void SetActive(bool active) { activeSelf=active; } }
    public class Transform { public void SetAsLastSibling() {} }
    public class MonoBehaviour
    {
        protected GameObject gameObject = new GameObject(); protected Transform transform = new Transform();
        protected void Destroy(object value) {} protected void StartCoroutine(System.Collections.IEnumerator value) {}
        protected static T FindObjectOfType<T>() where T:class => null;
        protected T GetComponent<T>() where T:class => null;
    }
    public class WaitUntil { public Func<bool> Predicate; public WaitUntil(Func<bool> value) { Predicate = value; } }
    public static class Debug { public static void LogWarning(string value) {} }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
    public class SerializeField : Attribute {}
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType phase) {} }
    public static class Time { public static float unscaledTime, realtimeSinceStartup, timeScale; public static double realtimeSinceStartupAsDouble; }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static NetworkReachability internetReachability; }
}
namespace UnityEngine.Events { public delegate void UnityAction(); }
namespace UnityEngine.UI
{
    public class Button
    {
        public bool interactable; public ClickEvent onClick = new ClickEvent();
        public class ClickEvent { public void RemoveAllListeners() {} public void AddListener(UnityEngine.Events.UnityAction action) {} }
    }
}
namespace TMPro { public class TextMeshProUGUI { public string text; } }
namespace MirraGames.SDK.Common { public class ProductData { public string GetFullPriceFloat() => "100"; } }
namespace MirraGames.SDK
{
    public static class MirraSDK
    {
        private static FakeAds ads = new FakeAds();
        public static int AdsAccesses;
        public static FakeAds Ads { get { AdsAccesses++; return ads; } set { ads = value; } }
        public static FakePayments Payments = new FakePayments();
        public static bool IsInitialized = true;
        public static class Analytics
        {
            public static int Calls;
            public static void GameIsReady() { Calls++; }
            public static void GameplayStart() { Calls++; }
            public static void GameplayStop() { Calls++; }
        }
        public static class Time { public static int Writes; public static float Scale { set { Writes++; } } }
    }
    public sealed class FakeAds
    {
        public bool IsRewardedReady = true, IsInterstitialReady = true, Throw;
        public Action Open;
        public Action<bool> Close;
        public void InvokeRewarded(Action onOpen = null, Action<bool> onClose = null, string rewardTag = null)
        { if (Throw) throw new Exception(); Open = onOpen; Close = onClose; }
        public void InvokeInterstitial(Action onOpen = null, Action<bool> onClose = null)
        { if (Throw) throw new Exception(); Open = onOpen; Close = onClose; }
    }
    public sealed class FakePayments
    {
        public int PurchasedChecks;
        public int PriceChecks, PurchaseRequests;
        public int RestoreRequests;
        public bool AlreadyPurchased;
        public Action Success, Error;
        public void Purchase(string productTag, Action onSuccess, Action onError) { PurchaseRequests++; Success = onSuccess; Error = onError; }
        public bool IsAlreadyPurchased(string productTag)
        {
            PurchasedChecks++;
            if (!MirraSDK.IsInitialized) throw new Exception("Cannot read properties of undefined (reading 'payments')");
            return AlreadyPurchased;
        }
        public Common.ProductData GetProductData(string tag) { PriceChecks++; return new Common.ProductData(); }
        public void RestorePurchases(Action<RestoredProducts> callback) { RestoreRequests++; callback(new RestoredProducts()); }
        public class RestoredProducts { public void RestoreProduct(string tag, Action apply) => apply(); }
    }
}
public static class GameProgressStore
{
    public static bool NoAds; public static bool GetBool(string key) => NoAds;
    public static void SetBool(string key, bool value) { NoAds = value; } public static void Save() {}
    private static Dictionary<string, int> ints = new Dictionary<string, int>();
    public static int GetInt(string key, int fallback = 0) => ints.TryGetValue(key, out int value) ? value : fallback;
    public static void SetInt(string key, int value) { ints[key] = value; }
}
public class CoinManager { public static CoinManager instance = new CoinManager(); public void AddCoinsOnBuy(int count) {} }
public class LocalizationManager { public static LocalizationManager Instance; public Action onLanguageChange; }
public static class CampaignText { public static string Get(string value) => value; }
public static class ProgressSaveService { public static int CurrentLevelIndex = 299, HighestUnlockedIndex; }
public class LevelData
{
    public bool alternatingCrates, loopInventory, sectionBuckets, keyGates, linkedBuckets, largeBuckets, swappingColors, mysteryBuckets;
    public int frostMoves;
    public string displayTitle, artTheme;
}
public static class WebGameAnalytics
{
    public static List<KeyValuePair<string, Dictionary<string, object>>> Events = new List<KeyValuePair<string, Dictionary<string, object>>>();
    public static void Report(string name, Dictionary<string, object> data) => Events.Add(new KeyValuePair<string, Dictionary<string, object>>(name, data));
}
public static class AdAnalyticsChecks
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static int Count(string name) => WebGameAnalytics.Events.Count(e => e.Key == name);
    private static AdManager Reset()
    {
        Analytic.ResetSession(); WebGameAnalytics.Events.Clear(); GameProgressStore.NoAds = false;
        MirraGames.SDK.MirraSDK.Ads = new MirraGames.SDK.FakeAds();
        Analytic.BeginLevel(299, new LevelData(), false);
        return new AdManager();
    }
    public static void Main()
    {
        var bootingManager = Reset();
        MirraGames.SDK.MirraSDK.IsInitialized = true;
        GameSdkReadiness.CheckBridgeReady = () => false;
        MirraGames.SDK.MirraSDK.AdsAccesses = 0;
        MirraGames.SDK.MirraSDK.Time.Writes = 0;
        bootingManager.CanStartRewarded(true);
        Require(MirraGames.SDK.MirraSDK.AdsAccesses == 0,
            "C# SDK instance exists, but ads must not cross the JS bridge before providers are ready");
        int unavailableAtBoot = 0, rewardedAtBoot = 0, closedAtBoot = 0;
        bootingManager.StartRewarded("0", "magnet", () => rewardedAtBoot++, () => unavailableAtBoot++, true);
        bootingManager.ShowInterstitial(() => closedAtBoot++);
        Require(MirraGames.SDK.MirraSDK.AdsAccesses == 0 && MirraGames.SDK.MirraSDK.Time.Writes == 0
            && rewardedAtBoot == 0 && unavailableAtBoot == 1 && closedAtBoot == 1,
            "Missing JS bridge completes ad requests without reward or unsafe time calls");
        MirraGames.SDK.MirraSDK.Analytics.Calls = 0;
        new OpenWindow().Open(); new OpenWindow().Close();
        Require(MirraGames.SDK.MirraSDK.Analytics.Calls == 0, "Missing JS analytics cannot be called by UI");
        MirraGames.SDK.MirraSDK.Payments = new MirraGames.SDK.FakePayments();
        var bootingShop = new RewardedShop();
        var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(RewardedShop).GetMethod("LoadDataOrInit", privateInstance).Invoke(bootingShop, null);
        typeof(RewardedShop).GetMethod("Purchase", privateInstance).Invoke(bootingShop, new object[] { "gold_300" });
        typeof(RewardedShop).GetMethod("RestorePurchases", privateInstance).Invoke(bootingShop, null);
        Require(MirraGames.SDK.MirraSDK.Payments.PurchasedChecks == 0
            && MirraGames.SDK.MirraSDK.Payments.PurchaseRequests == 0 && MirraGames.SDK.MirraSDK.Payments.RestoreRequests == 0,
            "C# ready flag must not release purchases or restore while JS bridge is missing");
        GameSdkReadiness.CheckBridgeReady = () => true;
        var earlyManager = Reset();
        WebGameAnalytics.Events.Clear();
        MirraGames.SDK.MirraSDK.IsInitialized = false;
        MirraGames.SDK.MirraSDK.AdsAccesses = 0;
        MirraGames.SDK.MirraSDK.Time.Writes = 0;
        int earlySuccess = 0, earlyUnavailable = 0, earlyClosed = 0;
        Require(!earlyManager.CanStartRewarded(true), "Rewarded is unavailable until SDK initialization");
        earlyManager.StartRewarded("299", "magnet", () => earlySuccess++, () => earlyUnavailable++, true);
        UnityEngine.Time.timeScale = 0;
        earlyManager.ShowInterstitial(() => earlyClosed++);
        Require(MirraGames.SDK.MirraSDK.AdsAccesses == 0 && MirraGames.SDK.MirraSDK.Time.Writes == 0,
            "Before SDK initialization, ads and time must not cross WebGL bridge");
        Require(earlySuccess == 0 && earlyUnavailable == 1 && earlyClosed == 1 && UnityEngine.Time.timeScale == 1,
            "Early ads request completes without reward and restores local time");
        Require(WebGameAnalytics.Events.Count == 0, "Early ads request must not produce ad analytics");
        MirraGames.SDK.MirraSDK.Analytics.Calls = 0;
        var earlyWindow = new OpenWindow();
        earlyWindow.Open(); earlyWindow.Close();
        var readyRoutine = (System.Collections.IEnumerator)typeof(GameIsReady).GetMethod("Start",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(new GameIsReady(), null);
        Require(readyRoutine.MoveNext() && !((UnityEngine.WaitUntil)readyRoutine.Current).Predicate()
            && MirraGames.SDK.MirraSDK.Analytics.Calls == 0, "Startup and early UI must wait before calling analytics");
        MirraGames.SDK.MirraSDK.IsInitialized = true;
        Require(((UnityEngine.WaitUntil)readyRoutine.Current).Predicate(), "Ready event awaits SDK initialization");
        Require(!readyRoutine.MoveNext() && MirraGames.SDK.MirraSDK.Analytics.Calls == 2,
            "Startup sends GameIsReady and GameplayStart after SDK initialization");
        earlyWindow.Open(); earlyWindow.Close();
        Require(MirraGames.SDK.MirraSDK.Analytics.Calls == 4, "Window analytics work after SDK initialization");
        Console.WriteLine("SDK_STARTUP_ADS_AND_ANALYTICS_CHECKS_PASS");
        var manager = Reset(); int success = 0, failure = 0;
        Analytic.CompleteLevel(); Analytic.CompleteLevel();
        Require(Count("level_completed") == 1 && (int)WebGameAnalytics.Events.Last().Value["level"] == 300, "Completion once, level 300");
        manager.StartRewarded("299", "magnet", () => success++, () => failure++);
        var ads = MirraGames.SDK.MirraSDK.Ads;
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 0, "Request is not an impression");
        ads.Open(); ads.Open();
        var impression = WebGameAnalytics.Events.Last().Value;
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 1 && (int)impression["level"] == 300
            && (string)impression["adType"] == "rewarded" && (string)impression["placement"] == "magnet", "Rewarded opening once with context");
        ads.Close(true); ads.Close(true);
        Require(success == 1 && failure == 0 && Count("video_ads_success") == 1, "Watched once");
        Require(Count("rewarded_shown") == 1 && Count("interstitial_shown") == 0, "Rewarded has its own goal");
        manager = Reset(); manager.StartRewarded("299", "magnet", () => success++, () => failure++);
        ads = MirraGames.SDK.MirraSDK.Ads; ads.Open(); ads.Close(false);
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 1 && Count("video_ads_success") == 0 && failure == 1, "Cancelled after opening is an impression without reward");
        manager = Reset(); MirraGames.SDK.MirraSDK.Ads.IsRewardedReady = false;
        manager.StartRewarded("299", "magnet", null, null);
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 0, "Unavailable rewarded");
        manager = Reset(); GameProgressStore.NoAds = true;
        manager.StartRewarded("299", "magnet", () => success++, null); manager.ShowInterstitial();
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 0 && Count("video_ads_success") == 0 && Count("video_ads_available") == 0, "No-ads bonus is not an ad");
        manager.StartRewarded("299", "slot", null, null, true);
        MirraGames.SDK.MirraSDK.Ads.Open(); MirraGames.SDK.MirraSDK.Ads.Close(true);
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 1, "Required ad still records an impression with no-ads enabled");
        manager = Reset(); int closed = 0; manager.ShowInterstitial(() => closed++);
        ads = MirraGames.SDK.MirraSDK.Ads;
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 0, "Interstitial request is not an impression");
        ads.Open(); ads.Open();
        Require(closed == 0 && (Count("interstitial_shown") + Count("rewarded_shown")) == 1
            && (string)WebGameAnalytics.Events.Last().Value["adType"] == "interstitial", "Opening records once and does not close");
        ads.Close(true); ads.Close(true); Require(closed == 1, "Close once");
        Require(Count("interstitial_shown") == 1 && Count("rewarded_shown") == 0, "Interstitial has its own goal");
        manager = Reset(); MirraGames.SDK.MirraSDK.Ads.IsInterstitialReady = false;
        manager.ShowInterstitial(() => closed++); Require(closed == 2 && (Count("interstitial_shown") + Count("rewarded_shown")) == 0, "Unavailable interstitial closes without impression");
        manager = Reset(); MirraGames.SDK.MirraSDK.Ads.Throw = true;
        manager.ShowInterstitial(); manager.StartRewarded("299", "magnet", null, null);
        Require((Count("interstitial_shown") + Count("rewarded_shown")) == 0, "SDK exceptions without impression");
        Reset(); Analytic.BeginLevel(0, new LevelData(), false); Analytic.CompleteLevel();
        Require((int)WebGameAnalytics.Events.Last().Value["level"] == 1, "One-based first level");
        var shop = new RewardedShop();
        MirraGames.SDK.MirraSDK.Payments = new MirraGames.SDK.FakePayments();
        MirraGames.SDK.MirraSDK.IsInitialized = false;
        GameProgressStore.NoAds = false;
        typeof(RewardedShop).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shop, null);
        Require(MirraGames.SDK.MirraSDK.Payments.PurchasedChecks == 0,
            "Awake must not cross the WebGL payments bridge before SDK initialization");
        var earlyPrice = new TMPro.TextMeshProUGUI();
        var earlyButton = new UnityEngine.UI.Button { interactable = true };
        typeof(RewardedShop).GetMethod("SetPrice", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Invoke(null, new object[] { earlyPrice, "gold_300", earlyButton });
        typeof(RewardedShop).GetMethod("Purchase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(shop, new object[] { "gold_300" });
        Require(MirraGames.SDK.MirraSDK.Payments.PriceChecks == 0 && MirraGames.SDK.MirraSDK.Payments.PurchaseRequests == 0
            && !earlyButton.interactable, "Early shop interaction must not cross payments bridge");
        var init = (System.Collections.IEnumerator)typeof(RewardedShop).GetMethod("InitPaymentsRoutine",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shop, null);
        Require(init.MoveNext() && !((UnityEngine.WaitUntil)init.Current).Predicate(), "Payments initialization waits for SDK");
        MirraGames.SDK.MirraSDK.IsInitialized = true;
        MirraGames.SDK.MirraSDK.Payments.AlreadyPurchased = true;
        Require(((UnityEngine.WaitUntil)init.Current).Predicate(), "SDK readiness releases initialization");
        init.MoveNext();
        Require(MirraGames.SDK.MirraSDK.Payments.PurchasedChecks == 1 && !shop.IsAdActive,
            "After SDK initialization, refresh purchased entitlement");
        Console.WriteLine("PAYMENTS_INITIALIZATION_CHECKS_PASS");
        MirraGames.SDK.MirraSDK.Payments = new MirraGames.SDK.FakePayments();
        BoosterService.instance = new BoosterService();
        var purchase = typeof(RewardedShop).GetMethod("Purchase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var restore = typeof(RewardedShop).GetMethod("RestorePurchases", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var productGoals = new Dictionary<string, string>
        {
            ["beginerKit_150"] = "purchase_beginner_kit", ["premium_kit_75"] = "purchase_premium_kit",
            ["gold_300"] = "purchase_gold_300", ["gold_700"] = "purchase_gold_700",
            ["gold_1200"] = "purchase_gold_1200", ["removeAd"] = "purchase_remove_ads"
        };
        foreach (string product in productGoals.Keys)
        {
            Reset(); purchase.Invoke(shop, new object[] { product });
            Require(Count("buy_inapp") == 0, "Request is not a purchase");
            MirraGames.SDK.MirraSDK.Payments.Success(); MirraGames.SDK.MirraSDK.Payments.Success();
            var data = WebGameAnalytics.Events.Last().Value;
            Require(Count("buy_inapp") == 1 && (string)data["product_id"] == product
                && (int)data["level"] == 300 && (string)data["result"] == "success", "Confirmed product once with level");
            Require(Count(productGoals[product]) == 1 && WebGameAnalytics.Events.Count(e => e.Key.StartsWith("purchase_")) == 1,
                "One common purchase event and exactly one matching product event");
        }
        Reset(); purchase.Invoke(shop, new object[] { "gold_300" });
        MirraGames.SDK.MirraSDK.Payments.Error(); MirraGames.SDK.MirraSDK.Payments.Success();
        Require(Count("buy_inapp") == 0, "Failed payment does not count");
        Require(!WebGameAnalytics.Events.Any(e => e.Key.StartsWith("purchase_")), "Failed payment has no product event");
        Reset(); restore.Invoke(shop, null); restore.Invoke(shop, null);
        Require(Count("buy_inapp") == 0 && GameProgressStore.NoAds, "Restoration applies entitlement without counting a new purchase");
        Require(!WebGameAnalytics.Events.Any(e => e.Key.StartsWith("purchase_")), "Restoration has no product event");
        foreach (int index in new[] { 0, 29, 30, 299 })
        {
            Reset(); ProgressSaveService.CurrentLevelIndex = index;
            Analytic.BeginLevel(index, new LevelData(), false); WebGameAnalytics.Events.Clear();
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            {
                string key = BoosterCatalog.GetSaveKey(type), bonus = BoosterCatalog.GetLocalizationKey(type);
                GameProgressStore.SetInt(key, 0);
                Require(!BoosterService.instance.TryConsume(type), "No charge is not a booster use");
                GameProgressStore.SetInt(key, 1);
                Require(BoosterService.instance.TryConsume(type) && !BoosterService.instance.TryConsume(type), "Only consumed charge records use");
                var data = WebGameAnalytics.Events.Last().Value;
                Require(Count("booster_" + bonus + "_used") == 1 && (int)data["level"] == index + 1
                    && (string)data["level_booster"] == "level_" + (index + 1).ToString("000") + ":" + bonus, "Booster and level preserved");
            }
            Require(Count("bonus_used") == 3, "Three actual booster uses");
            Analytic.CompleteLevel();
            Require((int)WebGameAnalytics.Events.Last().Value["boosters"] == 3, "Completion contains consumed booster count");
        }
        Reset(); WebGameAnalytics.Events.Clear();
        for (int index = 0; index < 31; index++)
        {
            Analytic.BeginLevel(index, new LevelData(), false); Analytic.CompleteLevel(); Analytic.CompleteLevel();
            string id = "level_" + (index + 1).ToString("000");
            Require(Count(id + "_started") == (index < 30 ? 1 : 0)
                && Count(id + "_completed") == (index < 30 ? 1 : 0), "Funnel first 30 levels, no duplicate victories");
        }
        Analytic.BeginLevel(4, new LevelData(), false); Analytic.RestartLevel();
        Require((int)WebGameAnalytics.Events.First(e => e.Key == "level_restarted").Value["level"] == 5, "Restart keeps its level");
        Analytic.ConveyorExpanded("4");
        Analytic.ConveyorExpanded("4");
        var expansion = WebGameAnalytics.Events.Last().Value;
        Require(Count("conveyor_expanded") == 2 && (int)expansion["level"] == 5, "Each expansion counts on its level");
        Console.WriteLine("AD_ANALYTICS_CHECKS_PASS");
        Console.WriteLine("PURCHASE_ANALYTICS_CHECKS_PASS");
        Console.WriteLine("BOOSTER_LEVEL_FUNNEL_CHECKS_PASS");
    }
}

public class NatureBackgroundOffer { public bool IsOpen; }
public class CosmeticShopTabs { public bool CanClose=true; }
