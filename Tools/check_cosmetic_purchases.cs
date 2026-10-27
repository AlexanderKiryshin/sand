// Compiled with the production catalog and extracted UI handlers by check_cosmetic_analytics.ps1.
using System;
using System.Linq;
using System.Collections.Generic;
using MirraGames.SDK.Common;

namespace MirraGames.SDK
{
    public static class MirraSDK { public static PaymentFixture Payments = new PaymentFixture(); }
    public sealed class PaymentFixture
    {
        public string RequestedTag;
        public Action Success, Error;
        public int Requests;
        public bool Available = true;
        public ProductData GetProductData(string tag) => Available ? new ProductData(tag, 100, "RUB") : null;
        public void Purchase(string productTag, Action onSuccess, Action onError)
        { RequestedTag = productTag; Success = onSuccess; Error = onError; Requests++; }
    }
}
public static class GameSdkReadiness { public static bool IsReady = true; }
public static class ProgressSaveService { public static int CurrentLevelIndex = 0; }
public sealed class CoinManager
{
    public static CoinManager instance = new CoinManager();
    public int Coins;
    public bool TryToSpendCoins(int price)
    { if (Coins < price) return false; Coins -= price; return true; }
}
public sealed class AdManager
{
    public static AdManager instance = new AdManager();
    public Action Success;
    public bool CanStartRewarded() => true;
    public void StartRewarded(string level, string placement, Action onSuccess, Action onNotReady) { Success = onSuccess; }
}
public sealed class CosmeticMenuFixture
{
    public int preview;
    public ThemeCatalog.Part Category;
    public bool pending;
    private void Refresh() { }
    private void Finish() { pending = false; }
    PURCHASE_HANDLER
    PRICE_LOOKUP
}
public static class CosmeticBadgeFixture { AFFORDABLE_COUNT }
public static class CosmeticPurchaseChecks
{
    private static readonly ThemeCatalog.Part[] Parts = { ThemeCatalog.Part.Conveyor, ThemeCatalog.Part.Casings, ThemeCatalog.Part.Background };
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Reset()
    {
        GameProgressStore.Clear(); WebGameAnalytics.Events.Clear(); WebGameAnalytics.Throw = false;
        MirraGames.SDK.MirraSDK.Payments = new MirraGames.SDK.PaymentFixture();
        CoinManager.instance.Coins = 0; AdManager.instance.Success = null;
    }
    private static void OnlyOwned(int index, ThemeCatalog.Part part)
    {
        foreach (var other in Parts)
            Require(ThemeCatalog.IsUnlocked(index, other) == (other == part), "Purchase leaked or missed ownership: " + index + "/" + part + " -> " + other);
        Require(!GameProgressStore.GetBool("sand_theme_owned_" + index), "New purchase wrote a shared legacy entitlement");
    }
    public static void Run()
    {
        // Run the actual UI purchase handler against each separately priced item.
        foreach (var part in Parts)
        for (int index = 0; index < ThemeCatalog.CountFor(part); index++)
        {
            if (!ThemeCatalog.IsAvailable(index, part) || ThemeCatalog.AccessFor(index, part) == ThemeCatalog.Access.Free) continue;
            Reset(); var menu = new CosmeticMenuFixture { preview = index, Category = part };
            var access = ThemeCatalog.AccessFor(index, part);
            if (access == ThemeCatalog.Access.Coins)
            {
                int price = ThemeCatalog.CoinPrice(index, part);
                CoinManager.instance.Coins = price - 1; menu.ApplyPreview();
                Require(!ThemeCatalog.IsUnlocked(index, part) && CoinManager.instance.Coins == price - 1, "Unaffordable item granted or charged");
                CoinManager.instance.Coins = price; menu.ApplyPreview();
                Require(CoinManager.instance.Coins == 0, "Incorrect coin debit");
            }
            else if (access == ThemeCatalog.Access.Money)
            {
                var payments = MirraGames.SDK.MirraSDK.Payments;
                payments.Available = false; menu.ApplyPreview();
                Require(payments.Requests == 0 && !ThemeCatalog.IsUnlocked(index, part), "Missing product was purchasable");
                payments.Available = true; menu.ApplyPreview(); menu.ApplyPreview();
                Require(payments.Requests == 1 && menu.pending && !ThemeCatalog.IsUnlocked(index, part), "Pending payment granted ownership or duplicated purchase");
                string prefix = part == ThemeCatalog.Part.Conveyor ? "sand_conveyor_" : part == ThemeCatalog.Part.Casings ? "sand_casings_" : "sand_background_";
                Require(payments.RequestedTag == prefix + ThemeCatalog.Names[index].Substring(6), "UI used wrong product ID");
                payments.Error(); payments.Success();
                Require(!ThemeCatalog.IsUnlocked(index, part) && !menu.pending, "Failed payment or late success granted item");
                menu.ApplyPreview(); payments.Success(); payments.Success(); payments.Error();
                Require(WebGameAnalytics.Events.Count(e => e.Key == "cosmetic_acquired") == 1, "Repeated payment callback duplicated acquisition");
            }
            else { menu.ApplyPreview(); Require(!ThemeCatalog.IsUnlocked(index, part), "Reward granted before ad completion"); AdManager.instance.Success(); }
            OnlyOwned(index, part);
            Require(ThemeCatalog.SelectedFor(part) == index, "Purchased item was not selected");
            menu.ApplyPreview(); Require(CoinManager.instance.Coins == 0, "Owned item charged again");
            var reopened = new CosmeticMenuFixture { preview = index, Category = part }; reopened.ApplyPreview(); OnlyOwned(index, part);
        }
        // Register real restoration callbacks, then run them after enumeration to catch closure errors.
        var callbacks = new Dictionary<string, Action>();
        ThemeCatalog.RestorePurchases((tag, grant) => callbacks.Add(tag, grant));
        Require(callbacks.Count == 31, "Restore registration must cover 27 independent and four legacy products");
        foreach (var part in Parts)
        for (int index = 0; index < ThemeCatalog.CountFor(part); index++)
        {
            if (!ThemeCatalog.IsAvailable(index, part) || ThemeCatalog.AccessFor(index, part) != ThemeCatalog.Access.Money) continue;
            Reset(); callbacks[ThemeCatalog.ProductTag(index, part)](); callbacks[ThemeCatalog.ProductTag(index, part)](); OnlyOwned(index, part);
            Require(WebGameAnalytics.Events.Count == 0, "Restoring a receipt emitted a purchase event");
        }
        for (int index = 1; index < ThemeCatalog.MaterialCount; index++)
        {
            Reset(); GameProgressStore.SetBool("sand_theme_owned_" + index, true);
            Require(ThemeCatalog.IsUnlocked(index, ThemeCatalog.Part.Conveyor) && ThemeCatalog.IsUnlocked(index, ThemeCatalog.Part.Casings), "Old material ownership was lost");
            Require(ThemeCatalog.IsUnlocked(index, ThemeCatalog.Part.Background) == (index >= 4 && index < 12), "Old background entitlement changed");
        }
        for (int index = 8; index <= 11; index++)
        {
            Reset(); callbacks["sand_theme_" + ThemeCatalog.Names[index].Substring(6)]();
            Require(Parts.All(part => ThemeCatalog.IsUnlocked(index, part)), "Old paid theme receipt lost a part");
            Require(!ThemeCatalog.IsUnlocked(index == 11 ? 8 : index + 1, ThemeCatalog.Part.Conveyor), "Legacy receipt restored the wrong theme");
        }
        foreach (var bundle in CosmeticBundleCatalog.All)
        {
            Reset(); bundle.Grant(); bundle.Grant();
            foreach (var part in Parts)
            for (int index = 0; index < ThemeCatalog.CountFor(part); index++)
            {
                if (!ThemeCatalog.IsAvailable(index, part)) continue;
                bool expected = ThemeCatalog.AccessFor(index, part) == ThemeCatalog.Access.Free || bundle.Items.Any(item => item.Theme == index && item.Part == part);
                Require(ThemeCatalog.IsUnlocked(index, part) == expected, "Bundle granted an unrelated item: " + bundle.Tag);
            }
            if (!bundle.IsStyleBundle) continue;
            Reset(); var conveyor = bundle.Items.Single(item => item.Part == ThemeCatalog.Part.Conveyor);
            ThemeCatalog.Unlock(conveyor.Theme, conveyor.Part); Require(!bundle.IsOwned, "One material counts as an entire bundle");
            bundle.GrantPurchased(); Require(bundle.IsOwned && WebGameAnalytics.Events.Count(e => e.Key == "cosmetic_acquired") == 2, "Partial bundle did not grant exactly two missing parts");
        }
        Reset();
        Require(CosmeticBadgeFixture.CountAffordable(99) == 0 && CosmeticBadgeFixture.CountAffordable(100) == 2, "Entry price counters merge separate products");
        Require(CosmeticBadgeFixture.CountAffordable(700) == 31 && CosmeticBadgeFixture.CountAffordable(4000) == 50, "Global counter merges categories");
        Require(CosmeticBadgeFixture.CountAffordable(700, ThemeCatalog.Part.Conveyor) == 4 && CosmeticBadgeFixture.CountAffordable(700, ThemeCatalog.Part.Casings) == 16 && CosmeticBadgeFixture.CountAffordable(700, ThemeCatalog.Part.Background) == 11, "Category counters changed");
        ThemeCatalog.Unlock(4, ThemeCatalog.Part.Casings);
        Require(CosmeticBadgeFixture.CountAffordable(700) == 30 && CosmeticBadgeFixture.CountAffordable(100) == 1, "One purchase removed multiple offers from counter");
        Require(!ThemeCatalog.Select(4), "Combined selection applied an unowned conveyor");
        Require(ThemeCatalog.Select(0), "Default style selection failed");
        Reset();
    }
}
