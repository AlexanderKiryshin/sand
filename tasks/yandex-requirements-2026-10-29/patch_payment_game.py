from pathlib import Path
import uuid

stage = Path('E:/myProjects/sand/Temp/YandexPaymentsChanges')
def replace(path, old, new):
    file = stage / path
    text = file.read_text(encoding='utf-8-sig')
    assert old in text, (path, old)
    file.write_text(text.replace(old, new), encoding='utf-8')

shop = 'Assets/Scripts/UI/RewardedShop.cs'
replace(shop, 'fetchData.RestoreProduct(RemoveAdTag, () => ApplyPurchase(RemoveAdTag));', '''if (fetchData == null) return;
            foreach (string tag in new[] { PremiumKitTag, Gold800Tag, Gold1500Tag, Gold3000Tag,
                RemoveAdTag, GetBoosterPackTag(BoosterType.Magnet), GetBoosterPackTag(BoosterType.Transparent),
                GetBoosterPackTag(BoosterType.Shuffle) })
                fetchData.RestoreProduct(tag, () => ApplyPurchase(tag));''')
replace(shop, 'private void Update() => RefreshRewardedGold();', '''private float priceRefreshAt;
    private void Update()
    {
        RefreshRewardedGold();
        if (GameSdkReadiness.IsReady && Time.unscaledTime >= priceRefreshAt)
        {
            priceRefreshAt = Time.unscaledTime + 1f;
            SetCurrencies();
        }
    }''')
replace(shop, 'label.text = productData.GetFullPriceFloat();', '''if (productData == null || productData.Tag != tag || productData.PriceFloat < 0)
                throw new InvalidOperationException("Product is unavailable");
            label.text = productData.GetFullPriceFloat();
            YandexCurrencyIcon.Set(label, tag);''')
replace(shop, 'label.text = CampaignText.Get("unavailable");', 'label.text = CampaignText.Get("unavailable");\n            YandexCurrencyIcon.Set(label, null);')
replace(shop, 'if (removeAdPriceText != null) removeAdPriceText.text = CampaignText.Get("purchased");', 'if (removeAdPriceText != null) removeAdPriceText.text = CampaignText.Get("purchased");\n            YandexCurrencyIcon.Set(removeAdPriceText, null);')

menu = 'Assets/Scripts/UI/ThemeCustomizationMenu.cs'
replace(menu, 'data==null||data.Tag!=ThemeCatalog.ProductTag(theme,part)', 'data==null||data.PriceFloat<0||data.Tag!=ThemeCatalog.ProductTag(theme,part)')
replace(menu, 'costs[i].text=Cost(i);', 'costs[i].text=Cost(i);YandexCurrencyIcon.Set(costs[i],!ThemeCatalog.IsUnlocked(i,Category)&&ThemeCatalog.AccessFor(i,Category)==ThemeCatalog.Access.Money?ThemeCatalog.ProductTag(i,Category):null);')
replace(menu, 'actionLabel.text=pending?CampaignText.Get("ad_wait"):label;', 'actionLabel.text=pending?CampaignText.Get("ad_wait"):label;YandexCurrencyIcon.Set(actionLabel,!pending&&!owned&&available&&ThemeCatalog.AccessFor(preview,Category)==ThemeCatalog.Access.Money?ThemeCatalog.ProductTag(preview,Category):null);')

icon = stage / 'Assets/Scripts/UI/YandexCurrencyIcon.cs'
icon.with_name(icon.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8')

test = stage / 'Tools/check_ad_analytics.cs'
with test.open('a', encoding='utf-8') as f:
    f.write('\npublic static class YandexCurrencyIcon { public static void Set(TMPro.TextMeshProUGUI label, string tag) {} }\n')
print('Patched shop restoration, price validity and currency icons.')
