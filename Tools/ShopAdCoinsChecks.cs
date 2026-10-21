// Run from Assets/Editor in the disposable validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MirraGames.SDK.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ShopAdCoinsChecks
{
    const string Key = "Sand.ShopAdCoinsChecks";
    static ShopAdCoinsChecks() { EditorApplication.update += Tick; }
    public static string Output => Environment.GetEnvironmentVariable("SAND_SHOP_AD_OUTPUT") ?? "tasks/shop-ad-coins";
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<ShopAdCoinsRunner>() == null)
            new GameObject("ShopAdCoinsRunner").AddComponent<ShopAdCoinsRunner>();
    }
    public static void Finish(bool success, string message)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "status.txt"), (success ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
public sealed class ShopAdCoinsRunner : MonoBehaviour
{
    readonly List<string> errors = new List<string>();
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string trace, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception) && !trace.Contains("UnityEditor.Search.")) errors.Add(message); }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    IEnumerator Start()
    {
        var steps = Check();
        while (true)
        {
            object step;
            try { if (!steps.MoveNext()) break; step = steps.Current; }
            catch (Exception error) { ShopAdCoinsChecks.Finish(false, error.ToString()); yield break; }
            yield return step;
        }
        ShopAdCoinsChecks.Finish(errors.Count == 0, "Rewarded shop +100 coins: complete/cancel/duplicate callbacks/repeated clicks/no-SDK/no-ads/pause checks passed; Small Bundle +5 each / 1000 coins; Remove Ads +10 each / 4000 coins and ad disable; individual repeatable x10 packs grant only their selected booster; purchased charges survive mechanic unlock; actual reward handlers and RU/EN shop cards verified.\n" + string.Join("\n", errors));
    }
    IEnumerator Check()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Time.captureFramerate = 30;
        for (int i = 0; i < 110; i++) yield return null;
        Require(GameProgressStore.IsPreview, "Expected an isolated profile.");
        var shop = FindObjectsOfType<RewardedShop>(true).Single();
        var canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        var capture = typeof(CollectionSelectionCheckRunner).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        Directory.CreateDirectory(ShopAdCoinsChecks.Output);
        foreach (string language in new[] { "RU", "EN" })
        {
            LocalizationManager.Instance.SetLanguage(language == "RU" ? LanguageType.Russian : LanguageType.English);
            yield return null;
            shop = FindObjectsOfType<RewardedShop>(true).Single();
            shop.OpenShop();
            for (int i = 0; i < 15; i++) yield return null;
            foreach (var pair in new[] { new { Name = "premium_setBtn", Count = 5, Gold = 1000 }, new { Name = "remove_ads_bundleBtn", Count = 10, Gold = 4000 } })
            {
                var row = shop.GetComponentsInChildren<Transform>(true).Single(t => t.name == pair.Name);
                foreach (string chip in new[] { "MagnetChip", "FillChip", "ShuffleChip" })
                    Require(row.Find(chip + "/Value").GetComponent<TextMeshProUGUI>().text == "+" + pair.Count, "Wrong UI booster count: " + pair.Name + "/" + chip);
                Require(row.Find("GoldChip/Value").GetComponent<TextMeshProUGUI>().text == pair.Gold.ToString(), "Gold display changed.");
            }
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            {
                var row = shop.GetComponentsInChildren<Transform>(true).Single(t => t.name == RewardedShop.GetBoosterPackTag(type) + "Btn");
                string label = LocalizationManager.Instance.GetText(BoosterCatalog.GetLocalizationKey(type));
                Require(row.Find("Title").GetComponent<TextMeshProUGUI>().text == label + " ×10", "Wrong pack label for " + type);
                var price = row.Find("PriceBadge/Price").GetComponent<TextMeshProUGUI>();
                Require(!string.IsNullOrEmpty(price.text), "No pack price or availability label.");
            }
            var adRow = shop.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Gold 100Btn");
            Require(adRow.Find("Title").GetComponent<TextMeshProUGUI>().text == "100 " + CampaignText.Get("coins"), "Wrong rewarded gold title.");
            Require(adRow.Find("PriceBadge/Price").GetComponent<TextMeshProUGUI>().text == CampaignText.Get("watch_ad"), "Wrong rewarded gold CTA.");
            Require(adRow.Find("PriceBadge/VideoIcon").GetComponent<Image>().sprite != null, "Missing ad icon.");
            capture.Invoke(null, new object[] { canvas, Path.Combine(ShopAdCoinsChecks.Output, "shop-" + language + ".png"), 1440, 2560 });
            shop.CloseShop();
        }
        var grant = typeof(RewardedShop).GetMethod("ApplyPurchase", BindingFlags.Instance | BindingFlags.NonPublic);
        var boosters = BoosterService.instance;
        var coins = CoinManager.instance;
        foreach (var product in new[] { new { Tag = "premium_kit_75", Count = 5, Gold = 1000 }, new { Tag = "removeAd", Count = 10, Gold = 4000 } })
        {
            int[] before = Enum.GetValues(typeof(BoosterType)).Cast<BoosterType>().Select(boosters.GetCharges).ToArray();
            int beforeCoins = coins.Coins;
            grant.Invoke(shop, new object[] { product.Tag });
            foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
                Require(boosters.GetCharges(type) == before[(int)type] + product.Count, "Wrong reward for " + product.Tag + "/" + type);
            Require(coins.Coins == beforeCoins + product.Gold, "Gold reward changed for " + product.Tag);
        }
        Require(!shop.IsAdActive && GameProgressStore.GetBool("no_ads"), "Remove Ads did not disable ads.");
        foreach (BoosterType chosen in Enum.GetValues(typeof(BoosterType)))
        {
            int[] before = Enum.GetValues(typeof(BoosterType)).Cast<BoosterType>().Select(boosters.GetCharges).ToArray();
            int beforeCoins = coins.Coins;
            for (int repeat = 1; repeat <= 2; repeat++)
            {
                grant.Invoke(shop, new object[] { RewardedShop.GetBoosterPackTag(chosen) });
                foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
                    Require(boosters.GetCharges(type) == before[(int)type] + (type == chosen ? repeat * 10 : 0), "Wrong individual pack reward for " + chosen + "/" + type);
                Require(coins.Coins == beforeCoins, "Individual pack added coins.");
            }
        }
        int[] purchased = Enum.GetValues(typeof(BoosterType)).Cast<BoosterType>().Select(boosters.GetCharges).ToArray();
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            GameProgressStore.SetBool(BoosterCatalog.GetGrantedKey(type), false);
        boosters.SyncUnlocks(999);
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
            Require(boosters.GetCharges(type) == purchased[(int)type], "Unlock discarded purchased charges for " + type);
        float resumeTime = Time.timeScale;
        shop.OpenShop();
        yield return null;
        Require(MirraGames.SDK.MirraSDK.Ads.GetType().FullName.Contains("Prototype"), "Expected editor prototype ads; never invoke production ads in checks.");
        object ads = MirraGames.SDK.MirraSDK.Ads;
        var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var callbacks = (Stack<Action<bool>>)ads.GetType().GetField("rewardedCloseCallbacks", hidden).GetValue(ads);
        var element = (UnityEngine.UIElements.VisualElement)ads.GetType().GetField("rewardedElement", hidden).GetValue(ads);
        var adButton = shop.GetComponentsInChildren<Button>().Single(b => b.name == "Gold 100Btn");
        Require(GameProgressStore.GetBool("no_ads"), "Remove Ads setup missing.");
        foreach (bool watched in new[] { false, true, true })
        {
            int before = coins.Coins;
            Require(adButton.interactable, "Ad coin offer not available.");
            adButton.onClick.Invoke();
            Require(callbacks.Count == 1 && coins.Coins == before, "Coins granted before ad completion (or no-ads bypassed view).");
            Require(!adButton.interactable, "Pending request button is still active.");
            adButton.onClick.Invoke();
            Require(callbacks.Count == 1 && coins.Coins == before, "Duplicate click started another ad.");
            var callback = callbacks.Peek();
            var control = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(element, watched ? "Success" : "Close");
            Require(control != null, "Prototype completion button missing.");
            using (var click = UnityEngine.UIElements.ClickEvent.GetPooled()) { click.target = control; control.SendEvent(click); }
            Require(coins.Coins == before + (watched ? 100 : 0), "Incorrect reward after complete/cancel.");
            callback(true); callback(false);
            Require(coins.Coins == before + (watched ? 100 : 0), "Duplicate SDK callback granted extra coins.");
            Require(Time.timeScale == 0 && shop.gameObject.activeInHierarchy, "Open shop did not remain paused after ad.");
            yield return null;
            Require(adButton.interactable, "Offer cannot be used again.");
        }
        var readiness = typeof(GameSdkReadiness).GetField("CheckBridgeReady", BindingFlags.Static | BindingFlags.NonPublic);
        object savedReady = readiness.GetValue(null);
        try
        {
            readiness.SetValue(null, (Func<bool>)(() => false));
            yield return null;
            Require(!adButton.interactable, "Unavailable ad offer remained enabled.");
            int before = coins.Coins;
            adButton.onClick.Invoke();
            Require(coins.Coins == before && callbacks.Count == 0, "Unavailable ad still granted coins.");
        }
        finally { readiness.SetValue(null, savedReady); }
        shop.CloseShop();
        Require(Time.timeScale == resumeTime, "Closing the shop failed to restore the previous game speed.");
    }
}
