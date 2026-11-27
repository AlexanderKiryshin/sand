// Actual shop, wallet, progress store, boosters and SDK receipt restoration; no real purchases.
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;

namespace UnityEngine
{
    public class GameObject { public bool activeSelf, activeInHierarchy; public void SetActive(bool value) { activeSelf = value; } }
    public class Transform { public void SetAsLastSibling() {} }
    public class MonoBehaviour
    {
        protected GameObject gameObject = new GameObject(); protected Transform transform = new Transform();
        public readonly List<IEnumerator> Coroutines = new List<IEnumerator>();
        public void StartCoroutine(IEnumerator routine) { Coroutines.Add(routine); }
        protected void Destroy(object value) {}
        protected T GetComponent<T>() where T : class => null;
        protected static T FindObjectOfType<T>() where T : class => null;
    }
    public class SerializeField : Attribute {}
    public class WaitUntil { public Func<bool> Predicate; public WaitUntil(Func<bool> predicate) { Predicate = predicate; } }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
    public static class Time { public static float timeScale; }
    public static class Debug { public static void LogWarning(string value) {} }
    public enum KeyCode { Escape }
    public static class Input { public static bool GetKeyDown(KeyCode key) => false; }
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static int GetInt(string key, int fallback = 0) => HasKey(key) ? (int)Values[key] : fallback;
        public static float GetFloat(string key, float fallback = 0) => HasKey(key) ? (float)Values[key] : fallback;
        public static void SetInt(string key, int value) { Values[key] = value; }
        public static void SetFloat(string key, float value) { Values[key] = value; }
        public static void Save() {}
    }
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
namespace MirraGames.SDK.Common
{
    public class ProductData { public string GetFullPriceFloat() => "100"; }
    public static class Logger { public static void CreateText(object owner, string message) {} public static void CreateError(object owner, string message) {} }
}
namespace MirraGames.SDK
{
    public static class MirraSDK
    {
        public static bool IsInitialized;
        public static FakeData Data = new FakeData();
        public static FakePayments Payments = new FakePayments();
    }
    public class FakeData
    {
        public Dictionary<string, object> Values = new Dictionary<string, object>();
        public Dictionary<string, object> Server = new Dictionary<string, object>();
        public int Saves;
        public bool HasKey(string key) => Values.ContainsKey(key);
        public int GetInt(string key) => HasKey(key) ? (int)Values[key] : 0;
        public float GetFloat(string key) => HasKey(key) ? (float)Values[key] : 0;
        public bool GetBool(string key) => HasKey(key) && (bool)Values[key];
        public void SetInt(string key, int value) { Values[key] = value; }
        public void SetFloat(string key, float value) { Values[key] = value; }
        public void SetBool(string key, bool value) { Values[key] = value; }
        public void Save() { Server = new Dictionary<string, object>(Values); Saves++; }
        public void Reload() { Values = new Dictionary<string, object>(Server); }
    }
    public class FakePayments : Common.IProductSupply
    {
        public readonly List<string> Receipts = new List<string>();
        public Action Success, Error;
        public int Requests;
        public string GetSupplyId(string tag) => "consume-count-" + tag;
        public int GetSupplyCount(string tag) => MirraSDK.Data.GetInt(GetSupplyId(tag));
        public void SetSupplyCount(string tag, int count) { MirraSDK.Data.SetInt(GetSupplyId(tag), count); MirraSDK.Data.Save(); }
        public void SupplyProduct(string tag, Action grant, bool countProduct = true)
        { grant(); if (countProduct) SetSupplyCount(tag, GetSupplyCount(tag) + 1); }
        public void Purchase(string productTag, Action onSuccess, Action onError)
        { Requests++; Success = () => { Receipts.Add(productTag); SupplyProduct(productTag, onSuccess); }; Error = onError; }
        public bool IsAlreadyPurchased(string productTag) => Receipts.Contains(productTag);
        public Common.ProductData GetProductData(string tag) => new Common.ProductData();
        public void RestorePurchases(Action<Common.IRestoreData> callback) => callback(new Common.RestoreData(this, Receipts.ToArray()));
    }
}
public static class YoutubePlatform
{
    public static bool IsPlayer => false;
    public static bool Ready => false;
    public static bool PurchasesAllowed => true;
    public static bool HasKey(string key) => false;
    public static double Get(string key, double fallback) => fallback;
    public static void Set(string key, double value) {} public static void Save() {}
}
public class CoinSpawner
{
    public static CoinSpawner Instance; public int coinCount;
    public event Action CoinReachedTarget;
    public void SpawnCoins() {} public void Arrive() { CoinReachedTarget?.Invoke(); }
}
public static class Analytic
{
    public static void GoldChanged(string level, float coins) {}
    public static void BuyInApp(string level, string tag) {}
    public static void BonusUsed(string level, string tag) {}
}
public class LocalizationManager { public static LocalizationManager Instance; public Action onLanguageChange; }
public static class CampaignText { public static string Get(string key) => key; }
public static class ProgressSaveService { public static int CurrentLevelIndex, HighestUnlockedIndex; }
public static class GameIsReady { public static void SetWindowOpen(object owner, bool open) {} }
public class AdManager
{
    public static AdManager instance;
    public bool CanStartRewarded(bool allowNoAds) => false;
    public void OnWindowEnabled(bool value) {}
    public void StartRewarded(string level, string tag, Action reward, Action failed, bool allowNoAds) {}
}
public class NatureBackgroundOffer { public bool IsOpen; }
public class CosmeticShopTabs { public bool CanClose; }

public static class PurchasePersistenceChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(object instance, string name, params object[] args)
        => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    static void Start(CoinManager coins)
    {
        var routine = Call(coins, "Start") as IEnumerator;
        if (routine == null) return;
        while (routine.MoveNext())
        {
            var wait = routine.Current as UnityEngine.WaitUntil;
            if (wait != null && !wait.Predicate()) { coins.Coroutines.Add(routine); return; }
        }
    }
    static CoinManager NewWallet()
    {
        CoinManager.instance = null;
        var coins = new CoinManager(); Call(coins, "Awake"); Start(coins); return coins;
    }
    static void FinishStartup(CoinManager coins)
    {
        foreach (var routine in coins.Coroutines.ToArray()) while (routine.MoveNext()) {}
        coins.Coroutines.Clear();
    }
    static void Reset(int cloudCoins, int localCoins = 0)
    {
        MirraGames.SDK.MirraSDK.Data = new MirraGames.SDK.FakeData();
        MirraGames.SDK.MirraSDK.Payments = new MirraGames.SDK.FakePayments();
        MirraGames.SDK.MirraSDK.Data.SetInt("Coins", cloudCoins);
        MirraGames.SDK.MirraSDK.Data.SetInt("CoinsInPreviosLevel", cloudCoins);
        MirraGames.SDK.MirraSDK.Data.Save();
        UnityEngine.PlayerPrefs.Values.Clear();
        UnityEngine.PlayerPrefs.SetInt("Coins", localCoins);
        UnityEngine.PlayerPrefs.SetInt("CoinsInPreviosLevel", localCoins);
        BoosterService.instance = new BoosterService();
    }
    static void CheckEarlyRewards()
    {
        Reset(4000, 9000);
        MirraGames.SDK.MirraSDK.IsInitialized = false;
        var coins = NewWallet();
        int saves = MirraGames.SDK.MirraSDK.Data.Saves;
        coins.AddCoinsOnComplete(); coins.AddCoinsOnDoubleReward();
        Require(!coins.TryToSpendCoins(100), "Spending used an uninitialized/local balance");
        Require(MirraGames.SDK.MirraSDK.Data.Saves == saves, "Early rewards wrote to the cloud before initialization");
        MirraGames.SDK.MirraSDK.IsInitialized = true; FinishStartup(coins);
        Require(coins.Coins == 4150 && MirraGames.SDK.MirraSDK.Data.GetInt("Coins") == 4150,
            "Rewards earned during SDK startup were lost or replaced the cloud balance");
        Require(UnityEngine.PlayerPrefs.GetInt("Coins") == 9000, "Wallet wrote a premature local fallback balance");
        Console.WriteLine("PASS: early rewards merge with the cloud balance; stale local preferences and premature spending cannot overwrite it.");
    }
    static void CheckPurchaseBeforeWalletStart()
    {
        foreach (bool restore in new[] { false, true })
        {
            Reset(700);
            MirraGames.SDK.MirraSDK.IsInitialized = false;
            CoinManager.instance = null;
            var coins = new CoinManager(); Call(coins, "Awake");
            MirraGames.SDK.MirraSDK.IsInitialized = true;
            var shop = new RewardedShop();
            if (restore)
            {
                MirraGames.SDK.MirraSDK.Payments.Receipts.Add("removeAd");
                Call(shop, "RestorePurchases");
            }
            else
            {
                Call(shop, "Purchase", "removeAd");
                MirraGames.SDK.MirraSDK.Payments.Success();
            }
            Start(coins);
            Require(coins.Coins == 4700 && MirraGames.SDK.MirraSDK.Data.GetInt("Coins") == 4700,
                "Purchase/restore before wallet Start lost the existing balance or the bonus");
            Call(shop, "RestorePurchases");
            Require(coins.Coins == 4700, "Wallet Start or receipt replay duplicated a bonus");
        }
        Console.WriteLine("PASS: purchase and pending receipt before wallet Start preserve both the existing balance and one-time bonus.");
    }
    static void CheckEmptyProfile()
    {
        MirraGames.SDK.MirraSDK.Data = new MirraGames.SDK.FakeData();
        MirraGames.SDK.MirraSDK.IsInitialized = true;
        var coins = NewWallet();
        Require(coins.Coins == 0 && MirraGames.SDK.MirraSDK.Data.Saves == 0
            && !MirraGames.SDK.MirraSDK.Data.HasKey("Coins"), "An empty cloud read saved a default zero balance");
        coins.AddCoinsOnComplete();
        Require(MirraGames.SDK.MirraSDK.Data.GetInt("Coins") == 50, "A new profile cannot save its first earned coins");
        Console.WriteLine("PASS: empty profiles save only after an actual balance change, as in buses.");
    }
    public static int Main()
    {
        try
        {
            // Buy, close, then boot with SDK still loading and stale local preferences.
            MirraGames.SDK.MirraSDK.IsInitialized = true;
            var coins = NewWallet(); BoosterService.instance = new BoosterService();
            var shop = new RewardedShop();
            Call(shop, "Purchase", "removeAd"); MirraGames.SDK.MirraSDK.Payments.Success();
            Require(coins.Coins == 4000 && (int)MirraGames.SDK.MirraSDK.Data.Server["Coins"] == 4000,
                "Purchase did not save its 4000 coins");
            MirraGames.SDK.MirraSDK.Data.Reload();
            MirraGames.SDK.MirraSDK.IsInitialized = false;
            UnityEngine.PlayerPrefs.Values.Clear();
            coins = NewWallet();
            MirraGames.SDK.MirraSDK.IsInitialized = true; FinishStartup(coins);
            shop = new RewardedShop(); Call(shop, "RestorePurchases");
            coins.AddCoinsOnComplete();
            Require(coins.Coins == 4050 && MirraGames.SDK.MirraSDK.Data.GetInt("Coins") == 4050,
                "FAIL: no_ads restored but purchased coins disappeared after delayed SDK startup: wallet=" + coins.Coins
                + ", server=" + MirraGames.SDK.MirraSDK.Data.GetInt("Coins"));
            Require(coins.TryToSpendCoins(100), "Saved purchased coins cannot be spent");
            MirraGames.SDK.MirraSDK.Data.Reload(); coins = NewWallet();
            Require(coins.Coins == 3950, "Spending overwrote the restored purchase balance");
            Call(shop, "RestorePurchases"); Call(shop, "RestorePurchases");
            Require(coins.Coins == 3950, "Receipt restoration duplicated an already delivered bonus");
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
                Require(BoosterService.instance.GetCharges(type) == 10, "Purchased booster was lost or duplicated");
            Console.WriteLine("PASS: removeAd 4000 coins and x10 boosters survive delayed SDK startup, spending and repeated restoration.");
            CheckEarlyRewards();
            CheckPurchaseBeforeWalletStart();
            CheckEmptyProfile();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.GetBaseException().Message); return 1; }
    }
}
