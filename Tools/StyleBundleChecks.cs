// Run from Assets/Editor in the disposable validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StyleBundleChecks
{
    const string Key = "Sand.StyleBundleChecks";
    public static string Output => Environment.GetEnvironmentVariable("SAND_STYLE_BUNDLE_OUTPUT") ?? "tasks/style-bundles-single-preview";
    static StyleBundleChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity"); EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<StyleBundleRunner>() == null)
            new GameObject("StyleBundleRunner").AddComponent<StyleBundleRunner>();
    }
    public static void Finish(bool success, string message)
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "status.txt"), (success ? "PASS\n" : "FAIL\n") + message);
        SessionState.SetBool(Key, false); EditorApplication.Exit(success ? 0 : 1);
    }
}
public sealed class StyleBundleRunner : MonoBehaviour
{
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
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
            catch (Exception error) { StyleBundleChecks.Finish(false, error.ToString()); yield break; }
            yield return step;
        }
        StyleBundleChecks.Finish(errors.Count == 0, "Six new background/conveyor/casing sets plus six legacy collections; all grants, partial ownership, repeat grants, SDK restore and failed/successful purchase callbacks verified. Four-language labels, twelve shop cards, one final three-part preview without navigation for each style bundle; legacy background carousel and random unowned offers verified. No live purchases or persistent SDK receipts created.\n" + string.Join("\n", errors));
    }
    static void ResetOwned()
    {
        for (int id = 0; id < ThemeCatalog.Count; id++)
        {
            GameProgressStore.SetBool("sand_theme_owned_" + id, false);
            GameProgressStore.SetBool("sand_theme_conveyor_owned_" + id, false);
            GameProgressStore.SetBool("sand_theme_casings_owned_" + id, false);
            GameProgressStore.SetBool("sand_theme_background_owned_" + id, false);
        }
        ThemeCatalog.Refresh();
    }
    IEnumerator Check()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Time.captureFramerate = 30;
        for (int i = 0; i < 110; i++) yield return null;
        Require(GameProgressStore.IsPreview && GameSdkReadiness.IsReady, "Expected an isolated profile and ready editor SDK.");
        Directory.CreateDirectory(StyleBundleChecks.Output);
        var all = CosmeticBundleCatalog.All;
        var styles = all.Where(b => b.IsStyleBundle).ToArray();
        Require(all.Length == 12 && styles.Length == 6 && all.Select(b => b.Tag).Distinct().Count() == 12, "Bundle catalog counts or IDs incorrect.");
        foreach (var bundle in all)
        {
            ResetOwned(); Require(!bundle.IsOwned, "Fresh bundle already owned.");
            Require(bundle.Items.Length == 3 && bundle.Items.All(i => ThemeCatalog.IsAvailable(i.Theme, i.Part)), "Invalid bundle contents.");
            int[] selected = Enum.GetValues(typeof(ThemeCatalog.Part)).Cast<ThemeCatalog.Part>().Select(ThemeCatalog.SelectedFor).ToArray();
            bundle.Grant(); bundle.Grant();
            Require(bundle.IsOwned && bundle.Items.All(i => i.IsOwned), "Missing item after grant: " + bundle.Tag);
            foreach (ThemeCatalog.Part part in Enum.GetValues(typeof(ThemeCatalog.Part)))
            for (int id = 0; id < ThemeCatalog.CountFor(part); id++)
            {
                if (!ThemeCatalog.IsAvailable(id, part)) continue;
                bool expected = part == ThemeCatalog.Part.Background ? id == ThemeCatalog.DefaultBackground || bundle.Backgrounds.Contains(id)
                    : id == 0 || bundle.Items.Any(item => item.Part == part && item.Theme == id);
                Require(ThemeCatalog.IsUnlocked(id, part) == expected, "Unexpected grant: " + bundle.Tag + "/" + part + "/" + id);
            }
            Require(selected.SequenceEqual(Enum.GetValues(typeof(ThemeCatalog.Part)).Cast<ThemeCatalog.Part>().Select(ThemeCatalog.SelectedFor)), "Purchase changed active cosmetics.");
            if (bundle.IsStyleBundle)
            {
                ResetOwned(); ThemeCatalog.Unlock(bundle.Backgrounds[0], ThemeCatalog.Part.Background);
                Require(!bundle.IsOwned, "Owning the background alone counts as a full set.");
                bundle.GrantPurchased(); Require(bundle.IsOwned, "Partial purchase failed.");
                Require(NatureBackgroundOffer.GetSavingsText(bundle) == "", "Mixed currencies advertised an invented discount.");
            }
        }
        ResetOwned(); var restore = new RestoreData(new MemorySupply(), styles.Select(b => b.Tag).ToArray());
        foreach (var bundle in all) restore.RestoreProduct(bundle.Tag, bundle.Grant);
        Require(styles.All(b => b.IsOwned), "SDK receipt restoration missed a style part.");
        foreach (var bundle in all) bundle.Grant();
        Require(CosmeticBundleCatalog.PickUnowned() == null, "Owned bundle selected for promotion.");
        ResetOwned(); styles[0].Grant();
        for (int i = 0; i < 128; i++) Require(!CosmeticBundleCatalog.PickUnowned().IsOwned, "Random promotion picked an owned set.");

        var shop = FindObjectsOfType<RewardedShop>(true).Single();
        var tabs = shop.GetComponent<CosmeticShopTabs>();
        var offer = FindObjectOfType<NatureBackgroundOffer>();
        var offerRoot = offer.GetComponentsInChildren<Transform>(true).Single(t => t.name == "NatureBundleOffer");
        var canvas = FindObjectsOfType<Canvas>().First(c => c.isRootCanvas);
        var capture = typeof(CollectionSelectionCheckRunner).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var language in new[] { LanguageType.Russian, LanguageType.English, LanguageType.Turkish, LanguageType.German })
        {
            ResetOwned(); LocalizationManager.Instance.SetLanguage(language); shop.OpenShop(); tabs.Select(1);
            for (int i = 0; i < 20; i++) yield return null;
            Require(shop.GetComponentsInChildren<NatureBundleShopCard>(true).Length == 12, "Shop is missing bundle cards.");
            foreach (var bundle in styles)
            {
                var card = shop.GetComponentsInChildren<Transform>(true).Single(t => t.name == bundle.Tag + "Card");
                var preview = card.GetComponentsInChildren<CosmeticBundlePreview>(true);
                Require(preview.Select(p => p.Item.Part).SequenceEqual(bundle.Items.Select(p => p.Part)) && preview.Select(p => p.Item.Theme).SequenceEqual(bundle.Items.Select(p => p.Theme)), "Card previews differ from contents.");
                Require(card.Find("NatureTitle").GetComponent<TextMeshProUGUI>().text == CampaignText.Get(bundle.TitleKey), "Wrong card title.");
                foreach (var partPreview in preview)
                {
                    Require(partPreview.GetComponent<ThemeBackdropGraphic>().PreviewTheme == bundle.Backgrounds[0], "Set preview uses an unrelated background.");
                    if (partPreview.Item.Part == ThemeCatalog.Part.Casings)
                        Require(partPreview.GetComponentInChildren<ConveyorSurfaceGraphic>(true).Theme == bundle.Items.Single(item => item.Part == ThemeCatalog.Part.Conveyor).Theme, "Set preview uses another conveyor.");
                }
                for (int i = 0; i < 3; i++) Require(card.Find("StylePart_" + i).GetComponent<TextMeshProUGUI>().text == bundle.Items[i].PartName, "Stale category caption.");
            }
            string code = language == LanguageType.Russian ? "RU" : language == LanguageType.English ? "EN" : null;
            if (code != null) capture.Invoke(null, new object[] { canvas, Path.Combine(StyleBundleChecks.Output, "shop-" + code + ".png"), 1440, 2560 });
            foreach (var bundle in styles)
            {
                Require(offer.ShowFromShop(bundle), "Cannot open style set.");
                for (int i = 0; i < 18; i++) yield return null;
                var pager = offer.GetComponentsInChildren<NatureOfferPager>(true).Single();
                Require(!pager.enabled && !pager.content.gameObject.activeSelf, "Style carousel remains active.");
                var previews = offerRoot.GetComponentsInChildren<CosmeticBundlePreview>().ToArray();
                Require(previews.Length == 1 && previews[0].name == "CompleteStylePreview", "Style offer must show one final composition.");
                var preview = previews[0];
                Require(preview.GetComponent<ThemeBackdropGraphic>().PreviewTheme == bundle.Backgrounds[0], "Wrong final background.");
                Require(preview.GetComponentInChildren<ConveyorSurfaceGraphic>().Theme == bundle.Items.Single(item => item.Part == ThemeCatalog.Part.Conveyor).Theme, "Wrong final conveyor.");
                var hoods = preview.GetComponentsInChildren<ConveyorHoodGraphic>();
                Require(hoods.Length == 2 && hoods.All(h => h.PreviewTheme == bundle.Items.Single(item => item.Part == ThemeCatalog.Part.Casings).Theme), "Final casings are missing.");
                foreach (string name in new[] { "PreviousBackground", "NextBackground", "SwipeHint", "Page_0", "Page_1", "Page_2" })
                    Require(!offer.GetComponentsInChildren<Transform>(true).Single(t => t.name == name).gameObject.activeInHierarchy, "Unneeded style navigation visible: " + name);
                offer.ShowPage(1); offer.ShowPage(-1);
                Require(offer.PreviewPage == 0 && offerRoot.GetComponentsInChildren<CosmeticBundlePreview>().Length == 1, "Style offer can change to separate item pages.");
                if (code != null)
                    capture.Invoke(null, new object[] { canvas, Path.Combine(StyleBundleChecks.Output, bundle.Tag + "-final-" + code + ".png"), 1440, 2560 });
                offer.Continue();
            }
            // Legacy background collections still expose all three original paintings.
            Require(offer.ShowFromShop(CosmeticBundleCatalog.Nature), "Legacy nature offer failed.");
            Require(offer.GetComponentsInChildren<NatureOfferPager>(true).Single().enabled, "Legacy background carousel was not restored.");
            Require(offerRoot.GetComponentsInChildren<CosmeticBundlePreview>().Length == 5, "Legacy background collection lost its pages.");
            for (int page = 0; page < 3; page++) { offer.ShowPage(page); yield return null; }
            offer.Continue(); shop.CloseShop();
        }
        // Exercise the real offer's payment callbacks without creating editor SDK receipts.
        object payments = MirraSDK.Payments;
        Require(payments.GetType().FullName.Contains("Prototype"), "Never invoke live payments in checks.");
        var paymentType = payments.GetType();
        var purchaseUi = (UnityEngine.UIElements.VisualElement)paymentType.GetField("purchaseElement", Hidden).GetValue(payments);
        foreach (var bundle in styles)
        {
            ResetOwned(); Require(offer.ShowFromShop(bundle), "Purchase offer could not open.");
            var buy = offer.GetComponentsInChildren<Button>().Single(b => b.name == "BuyNatureBundle");
            buy.onClick.Invoke();
            Require((string)paymentType.GetField("productTag", Hidden).GetValue(payments) == bundle.Tag, "Wrong payment product ID.");
            var cancel = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(purchaseUi, "Close");
            using (var click = UnityEngine.UIElements.ClickEvent.GetPooled()) { click.target = cancel; cancel.SendEvent(click); }
            Require(!bundle.IsOwned && offer.IsOpen && buy.interactable, "Failed purchase granted cosmetics or blocked retry.");
            buy.onClick.Invoke();
            var success = (Action)paymentType.GetField("purchaseSuccessCallback", Hidden).GetValue(payments);
            success(); success();
            Require(bundle.IsOwned && !offer.IsOpen, "Success callback did not grant all items or close offer.");
            using (var click = UnityEngine.UIElements.ClickEvent.GetPooled()) { click.target = cancel; cancel.SendEvent(click); }
            Require(!offer.ShowFromShop(bundle), "Owned set can still be offered for sale.");
        }
    }
    sealed class MemorySupply : IProductSupply
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        public string GetSupplyId(string tag) => tag;
        public int GetSupplyCount(string tag) => counts.TryGetValue(tag, out int n) ? n : 0;
        public void SetSupplyCount(string tag, int n) => counts[tag] = n;
        public void SupplyProduct(string tag, Action grant, bool count = true) { grant(); if (count) SetSupplyCount(tag, GetSupplyCount(tag) + 1); }
    }
}
