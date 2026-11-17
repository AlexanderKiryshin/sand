using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MirraGames.SDK.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run only in the disposable validation project; never touches a player profile.
public static class UiLanguageAndThemeChecks
{
    private static readonly List<string> results = new List<string>();
    private static int failures;
    private static void Check(bool passed, string message)
    {
        results.Add((passed ? "PASS: " : "FAIL: ") + message);
        if (!passed) failures++;
    }
    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    public static void Run()
    {
        SessionState.SetBool("Sand.CampaignPreview", true);
        try
        {
            var language = new GameObject("Localization").AddComponent<LocalizationManager>();
            Call(language, "Awake");
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
            var menu = canvas.gameObject.AddComponent<ThemeCustomizationMenu>();
            Call(menu, "Start");
            menu.Open(); menu.SetCategory(ThemeCatalog.Part.Background); menu.OpenBackgroundViewer();
            language.SetLanguage(LanguageType.Russian);
            Check(canvas.transform.Find("ThemeMenu/BackgroundViewer/BackgroundPaper/Title").GetComponent<TextMeshProUGUI>().text == CampaignText.Get(ThemeCatalog.NameKey(menu.PreviewTheme, ThemeCatalog.Part.Background)), "open background viewer title changes language");
            Check(canvas.transform.Find("ThemeMenu/BackgroundViewer/BackgroundPaper/CloseBackground/Label").GetComponent<TextMeshProUGUI>().text == CampaignText.Get("close"), "open background viewer close changes language");
            menu.Close();

            var database = ScriptableObject.CreateInstance<LevelDatabase>();
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.cells = new byte[64]; level.gridWidth = level.gridHeight = 8;
            database.levels = new[] { level };
            var manager = new GameObject("Manager").AddComponent<GameManager>();
            Set(manager, "levelDatabase", database);
            language.SetLanguage(LanguageType.English);
            var gallery = CampaignGallery.Show(database, manager, true);
            language.SetLanguage(LanguageType.German);
            Check(gallery.transform.Find("CollectionPanel/Title").GetComponent<TextMeshProUGUI>().text == CampaignText.Get("level_select"), "open gallery title changes language");
            foreach (var label in gallery.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.name == "CollectionName")
                    Check(label.text == CampaignText.Get(label.transform.parent.name.Contains("Winter") ? "winter_collection" : "all_levels"), "collection name changes language: " + label.transform.parent.name);
                if (label.name == "CollectionCount")
                    Check(label.text == string.Format(CampaignText.Get("collection_level_count"), label.transform.parent.name.Contains("Winter") ? 0 : 1), "collection count changes language");
            }
            // Unity does not dispatch play-mode OnDisable for these edit-mode fixtures.
            Call(gallery, "OnDisable");
            Object.DestroyImmediate(gallery.gameObject);

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            hint.transform.SetParent(canvas.transform, false);
            var ui = canvas.gameObject.AddComponent<GameUI>();
            Set(ui, "hintLabel", hint);
            Call(ui, "Awake");
            var hud = canvas.gameObject.AddComponent<SandEconomyHud>();
            Set(hud, "gameManager", manager);
            Set(hud, "tutorialType", (BoosterType?)BoosterType.Magnet);
            Set(hud, "tutorialWaitingActivation", true);
            Call(hud, "WireEvents");
            ui.SetTutorialHint(language.GetText("tutorial_magnet"));
            language.SetLanguage(LanguageType.Turkish);
            Check(hint.text == language.GetText("tutorial_magnet"), "active booster tutorial changes language");

            var toast = new GameObject("Toast", typeof(RectTransform), typeof(TextMeshProUGUI));
            toast.transform.SetParent(canvas.transform, false);
            Set(hud, "noMoneyToast", toast);
            Set(hud, "toastLocalizationKey", "no_money");
            Call(hud, "RefreshToastText");
            Check(toast.GetComponent<TextMeshProUGUI>().text == language.GetText("no_money"), "insufficient-coins toast is localized");
            language.SetLanguage(LanguageType.Russian);
            Check(toast.GetComponent<TextMeshProUGUI>().text == language.GetText("no_money"), "visible toast changes language");

            var offer = canvas.gameObject.AddComponent<NatureBackgroundOffer>();
            Call(offer, "Start");
            Set(offer, "pending", true);
            Call(offer, "Refresh");
            language.SetLanguage(LanguageType.German);
            Check(canvas.transform.Find("NatureBundleOffer/NatureOfferDialog/NatureOfferFooter/BuyNatureBundle/Label").GetComponent<TextMeshProUGUI>().text == CampaignText.Get("ad_wait"), "pending bundle purchase changes language immediately");

            var background = new GameObject("FieldBackground", typeof(RectTransform), typeof(ThemeBackdropGraphic)).GetComponent<ThemeBackdropGraphic>();
            background.Rebuild(CanvasUpdate.PreRender);
            int vertices = 0, material = 0;
            background.RegisterDirtyVerticesCallback(() => vertices++);
            background.RegisterDirtyMaterialCallback(() => material++);
            // The renderer exists before async SDK/cloud data becomes available.
            GameProgressStore.SetBool("sand_theme_background_owned_46", true);
            GameProgressStore.SetInt("sand_theme_background", 46);
            Check(ThemeCatalog.SelectedBackground == 46, "saved background is reported selected");
            Call(background, "Update"); Call(background, "LateUpdate");
            Check(vertices > 0 && material > 0, "late-loaded selected background invalidates BOTH mesh and texture");
            vertices = material = 0;
            Call(background, "Update"); Call(background, "LateUpdate");
            Check(vertices == 0 && material == 0, "unchanged background does not rebuild every frame");
            background.PreviewTheme = 44;
            background.Rebuild(CanvasUpdate.PreRender);
            Call(background, "Update"); Call(background, "LateUpdate");
            vertices = material = 0;
            GameProgressStore.SetInt("sand_theme_background", 44);
            Call(background, "Update"); Call(background, "LateUpdate");
            Check(vertices == 0 && material == 0 && background.PreviewTheme == 44, "saved selection does not override an explicit shop preview");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally
        {
            SessionState.SetBool("Sand.CampaignPreview", false);
            Directory.CreateDirectory("tasks");
            File.WriteAllLines("tasks/ui-language-theme-checks.txt", results);
            foreach (var result in results) Debug.Log(result);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }
}
