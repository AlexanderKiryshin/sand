using System;
using System.Collections;
using System.Collections.Generic;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    private Dictionary<LanguageType, Dictionary<string, string>> localizations;
    private LanguageType currentLanguage = LanguageType.English;
    public Action onLanguageChange;

    private static LocalizationManager instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LocalizationManager>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeLocalizations();
    }

    private void Start()
    {
        StartCoroutine(SetLanguageRoutine());
    }

    private IEnumerator SetLanguageRoutine()
    {
        yield return new WaitUntil(() => GameSdkReadiness.IsReady);
        LanguageType language = YoutubePlatform.IsPlayer ? YoutubePlatform.Language : MirraSDK.Language.Current;
        switch (language)
        {
            case LanguageType.Russian:
            case LanguageType.English:
            case LanguageType.Turkish:
            case LanguageType.German:
                break;
            default:
                language = LanguageType.English;
                break;
        }

        SetLanguage(language);
    }

    private void InitializeLocalizations()
    {
        localizations = new Dictionary<LanguageType, Dictionary<string, string>>
        {
            [LanguageType.English] = BuildEnglish(),
            [LanguageType.Russian] = BuildRussian(),
            [LanguageType.Turkish] = BuildTurkish(),
            [LanguageType.German] = BuildGerman()
        };
    }

    private static Dictionary<string, string> BuildEnglish()
    {
        return new Dictionary<string, string>
        {
            ["level"] = "Level",
            ["continue"] = "Continue",
            ["restart"] = "Restart",
            ["complete"] = "Complete",
            ["ads_not_available"] = "Ads not available",
            ["coins"] = "Coins: {0}",
            ["buy_for"] = "Buy for {0}",
            ["free"] = "Free",
            ["no_money"] = "Not enough coins",
            ["shop"] = "Shop",
            ["beginners_kit"] = "Large bundle",
            ["premium_set"] = "Small bundle",
            ["remove_ads_bundle"] = "Remove ads bundle",
            ["reamoveAdDescription"] = "Disables ads during gameplay",
            ["beginners_kit_info"] = "+{0} coins and +{1} to each booster",
            ["premium_set_info"] = "+{0} coins and +{1} to each booster",
            ["ads"] = "COFFEE BREAK IN ",
            ["watch_ad"] = "Watch Ad",
            ["magnet"] = "Magnet",
            ["transparent"] = "Fill",
            ["shuffle"] = "Shuffle",
            ["magnet_info"] = "Place any bucket on the conveyor, even locked ones",
            ["transparent_info"] = "Fill a conveyor bucket and remove matching sand",
            ["shuffle_info"] = "Shuffle bucket colors in the inventory",
            ["tutorial_magnet"] = "Tap Magnet, then pick a locked bucket",
            ["tutorial_transparent"] = "Tap Fill, then pick a bucket on the conveyor",
            ["tutorial_shuffle"] = "Tap Shuffle to mix bucket colors",
            ["unlock_at_level"] = "Lv.{0}",
            ["skip_level"] = "Skip",
            ["double_reward"] = "x2 Reward",
            ["next"] = "Next"
        };
    }

    private static Dictionary<string, string> BuildRussian()
    {
        return new Dictionary<string, string>
        {
            ["level"] = "Уровень",
            ["continue"] = "Продолжить",
            ["restart"] = "Заново",
            ["complete"] = "Пройден",
            ["ads_not_available"] = "Реклама недоступна",
            ["coins"] = "Монеты: {0}",
            ["buy_for"] = "Купить за {0}",
            ["free"] = "Бесплатно",
            ["no_money"] = "Недостаточно монет",
            ["shop"] = "Магазин",
            ["beginners_kit"] = "Большой набор",
            ["premium_set"] = "Малый набор",
            ["remove_ads_bundle"] = "Отключить рекламу",
            ["reamoveAdDescription"] = "Отключает рекламу во время игры",
            ["beginners_kit_info"] = "+{0} монет и +{1} к каждому бустеру",
            ["premium_set_info"] = "+{0} монет и +{1} к каждому бустеру",
            ["ads"] = "КОФЕ БРЕЙК ЧЕРЕЗ ",
            ["watch_ad"] = "Смотреть рекламу",
            ["magnet"] = "Магнит",
            ["transparent"] = "Заполнить",
            ["shuffle"] = "Перетасовка",
            ["magnet_info"] = "Поставьте любое ведро на конвейер, даже недоступное",
            ["transparent_info"] = "Заполните ведро на конвейере и уберите песок с поля",
            ["shuffle_info"] = "Перемешайте цвета вёдер в инвентаре",
            ["tutorial_magnet"] = "Нажмите Магнит, затем выберите закрытое ведро",
            ["tutorial_transparent"] = "Нажмите Заполнить, затем выберите ведро на конвейере",
            ["tutorial_shuffle"] = "Нажмите Перетасовка, чтобы смешать цвета",
            ["unlock_at_level"] = "Ур.{0}",
            ["skip_level"] = "Пропуск",
            ["double_reward"] = "Награда x2",
            ["next"] = "Далее"
        };
    }

    private static Dictionary<string, string> BuildTurkish()
    {
        return new Dictionary<string, string>
        {
            ["level"] = "Seviye",
            ["continue"] = "Devam",
            ["restart"] = "Yeniden",
            ["complete"] = "Tamamlandı",
            ["ads_not_available"] = "Reklam mevcut değil",
            ["coins"] = "Para: {0}",
            ["buy_for"] = "Satın al {0}",
            ["free"] = "Ücretsiz",
            ["no_money"] = "Yeterli para yok",
            ["shop"] = "Dükkan",
            ["beginners_kit"] = "Büyük paket",
            ["premium_set"] = "Küçük paket",
            ["remove_ads_bundle"] = "Reklamları kaldır",
            ["reamoveAdDescription"] = "Oyun sırasında reklamları kapatır",
            ["beginners_kit_info"] = "+{0} para ve her boostera +{1}",
            ["premium_set_info"] = "+{0} para ve her boostera +{1}",
            ["ads"] = "Kahve molası ",
            ["watch_ad"] = "Reklam izle",
            ["magnet"] = "Mıknatıs",
            ["transparent"] = "Doldur",
            ["shuffle"] = "Karıştır",
            ["magnet_info"] = "Kilitli olsa bile herhangi bir kovayı banta koy",
            ["transparent_info"] = "Banttaki kovayı doldur ve kumu kaldır",
            ["shuffle_info"] = "Envanterdeki kova renklerini karıştır",
            ["tutorial_magnet"] = "Mıknatısa bas, sonra kilitli kova seç",
            ["tutorial_transparent"] = "Doldura bas, sonra banttaki kovayı seç",
            ["tutorial_shuffle"] = "Karıştıra basarak renkleri karıştır",
            ["unlock_at_level"] = "Sv.{0}",
            ["skip_level"] = "Atla",
            ["double_reward"] = "x2 Ödül",
            ["next"] = "İleri"
        };
    }

    private static Dictionary<string, string> BuildGerman()
    {
        return new Dictionary<string, string>
        {
            ["level"] = "Stufe",
            ["continue"] = "Weiter",
            ["restart"] = "Neustart",
            ["complete"] = "Geschafft",
            ["ads_not_available"] = "Werbung nicht verfügbar",
            ["coins"] = "Münzen: {0}",
            ["buy_for"] = "Kaufen für {0}",
            ["free"] = "Kostenlos",
            ["no_money"] = "Nicht genug Münzen",
            ["shop"] = "Shop",
            ["beginners_kit"] = "Großes Paket",
            ["premium_set"] = "Kleines Paket",
            ["remove_ads_bundle"] = "Werbung entfernen",
            ["reamoveAdDescription"] = "Schaltet Werbung während des Spiels aus",
            ["beginners_kit_info"] = "+{0} Münzen und +{1} für jeden Booster",
            ["premium_set_info"] = "+{0} Münzen und +{1} für jeden Booster",
            ["ads"] = "Kaffeepause ",
            ["watch_ad"] = "Werbung ansehen",
            ["magnet"] = "Magnet",
            ["transparent"] = "Füllen",
            ["shuffle"] = "Mischen",
            ["magnet_info"] = "Stelle jeden Eimer aufs Band, auch gesperrte",
            ["transparent_info"] = "Fülle einen Band-Eimer und entferne Sand",
            ["shuffle_info"] = "Mische die Eimerfarben im Inventar",
            ["tutorial_magnet"] = "Tippe Magnet, dann wähle einen gesperrten Eimer",
            ["tutorial_transparent"] = "Tippe Füllen, dann wähle einen Eimer auf dem Band",
            ["tutorial_shuffle"] = "Tippe Mischen, um Farben zu mischen",
            ["unlock_at_level"] = "St.{0}",
            ["skip_level"] = "Skip",
            ["double_reward"] = "x2 Belohnung",
            ["next"] = "Weiter"
        };
    }

    public string GetText(string key)
    {
        if (localizations != null
            && localizations.ContainsKey(currentLanguage)
            && localizations[currentLanguage].ContainsKey(key))
        {
            return localizations[currentLanguage][key];
        }

        return "Missing_" + key;
    }

    public string GetText(string key, params object[] args)
    {
        return string.Format(GetText(key), args);
    }

    public void SetLanguage(LanguageType language)
    {
        currentLanguage = localizations != null && localizations.ContainsKey(language)
            ? language
            : LanguageType.English;
        onLanguageChange?.Invoke();
    }

    public LanguageType GetCurrentLanguage()
    {
        return currentLanguage;
    }
}
