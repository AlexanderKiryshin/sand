using System.Linq;
using UnityEngine;

public readonly struct CosmeticBundleItem
{
    public readonly int Theme;
    public readonly ThemeCatalog.Part Part;
    public CosmeticBundleItem(int theme, ThemeCatalog.Part part) { Theme = theme; Part = part; }
    public bool IsOwned => ThemeCatalog.IsUnlocked(Theme, Part);
    public string Name => CampaignText.Get(ThemeCatalog.NameKey(Theme, Part));
    public string PartName => CampaignText.Get(Part == ThemeCatalog.Part.Background ? "theme_background_tab"
        : Part == ThemeCatalog.Part.Conveyor ? "theme_conveyor_tab" : "theme_casings_tab");
}

public sealed class CosmeticBundle
{
    public readonly string Tag, TitleKey;
    public readonly int[] Backgrounds;
    public readonly CosmeticBundleItem[] Items;
    public bool IsStyleBundle => Items.Any(item => item.Part != ThemeCatalog.Part.Background);
    public CosmeticBundle(string tag, string title, params int[] backgrounds)
        : this(tag, title, backgrounds.Select(id => new CosmeticBundleItem(id, ThemeCatalog.Part.Background)).ToArray()) { }
    public CosmeticBundle(string tag, string title, CosmeticBundleItem[] items)
    {
        Tag = tag; TitleKey = title; Items = items;
        Backgrounds = items.Where(item => item.Part == ThemeCatalog.Part.Background).Select(item => item.Theme).ToArray();
    }
    public bool IsOwned => Items.All(item => item.IsOwned);
    public bool CanPurchase => !IsOwned && !Backgrounds.Any(id => ThemeCatalog.IsUnlocked(id, ThemeCatalog.Part.Background));
    public void GrantPurchased()
    {
        var newlyOwned = Items.Where(item => !item.IsOwned).ToArray();
        Grant();
        foreach (var item in newlyOwned)
            Analytic.CosmeticAcquired(ThemeCatalog.NameKey(item.Theme, item.Part), item.Part.ToString().ToLowerInvariant(), item.Theme, "bundle_purchase", Tag);
    }
    public void Grant()
    {
        foreach (var item in Items) ThemeCatalog.Unlock(item.Theme, item.Part);
        GameProgressStore.Save(); ThemeCatalog.Refresh();
    }
}
public static class CosmeticBundleCatalog
{
    public static readonly CosmeticBundle Nature = new CosmeticBundle("sand_background_nature_bundle", "nature_bundle_title", 27, 24, 29);
    private static CosmeticBundle Style(string name, string title, int background, int material)
        => new CosmeticBundle("sand_style_" + name + "_bundle", title, new[] {
            new CosmeticBundleItem(background, ThemeCatalog.Part.Background),
            new CosmeticBundleItem(material, ThemeCatalog.Part.Conveyor),
            new CosmeticBundleItem(material, ThemeCatalog.Part.Casings) });
    public static CosmeticBundle PickUnowned()
    {
        var available = All.Where(bundle => bundle.CanPurchase).ToArray();
        return available.Length == 0 ? null : available[Random.Range(0, available.Length)];
    }
    public static readonly CosmeticBundle[] All = {
        Nature,
        new CosmeticBundle("sand_background_botanical_bundle", "bundle_botanical", 12, 21, 28),
        new CosmeticBundle("sand_background_travel_bundle", "bundle_travel", 13, 15, 25),
        new CosmeticBundle("sand_background_cozy_bundle", "bundle_cozy", 14, 17, 30),
        new CosmeticBundle("sand_background_artist_bundle", "bundle_artist", 16, 20, 23),
        new CosmeticBundle("sand_background_material_bundle", "bundle_material", 18, 19, 22),
        Style("bamboo", "bundle_style_bamboo", 21, 21),
        Style("alpine", "bundle_style_alpine", 13, 13),
        Style("winter", "bundle_style_winter", 30, 1),
        Style("copper", "bundle_style_copper", 18, 18),
        Style("night", "bundle_style_night", 17, 17),
        Style("blueprint", "bundle_style_blueprint", 23, 23)
    };
}
