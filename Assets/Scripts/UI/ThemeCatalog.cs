using UnityEngine;
using System.Linq;

public static class ThemeCatalog
{
    public const int Count = 50;
    public const int DefaultBackground = 44;
    public const int MinimalRewardedStart = 46;
    public const int PremiumBackgroundEnd = 31;
    public const int MinimalBackgroundStart = 38;
    public const int MaterialCount = 24;
    public static int CountFor(Part part) => part == Part.Background ? Count : MaterialCount;
    public static bool IsAvailable(int index,Part part) => (YoutubePlatform.PurchasesAllowed || AccessFor(index,part) != Access.Money) && index>=0 && index<CountFor(part) && (part!=Part.Background || index>=4);
    public static readonly string[] Names = { "theme_classic", "theme_winter", "theme_sunset", "theme_garden", "theme_ocean", "theme_lavender", "theme_candy", "theme_checks", "theme_stars", "theme_artdeco", "theme_marble", "theme_wood", "theme_greenhouse", "theme_alpine", "theme_chalet", "theme_harbor", "theme_atelier", "theme_observatory", "theme_copper", "theme_circuit", "theme_mosaic", "theme_bamboo", "theme_honeycomb", "theme_blueprint", "theme_forest", "theme_birch", "theme_meadow", "theme_lake", "theme_tropical", "theme_autumn", "theme_snowforest", "theme_leafy", "theme_wildflowers", "theme_riverside", "theme_goldenleaves", "theme_sprigs", "theme_grasses", "theme_pinewash", "theme_mint_breeze", "theme_sunny_grass", "theme_blue_calm", "theme_peach_leaves", "theme_cloud_corners", "theme_gentle_waves", "theme_bamboo_strokes", "theme_lavender_marks", "theme_sand_dunes", "theme_berry_dots", "theme_snow_dashes", "theme_dawn_wash" };
    private static readonly string[] ReplacementNames={"theme_olive_marks","theme_sea_pebbles","theme_rose_petals","theme_fern_shadow","theme_moon_dots","theme_rain_strokes","theme_maple_marks","theme_reed_corners"};
    public static string NameKey(int index,Part part) => part==Part.Background && index>=4 && index<12 ? ReplacementNames[index-4] : Names[index];
    public enum Part { Conveyor, Casings, Background }
    public static int Selected => SelectedFor(Part.Conveyor);
    public static int SelectedCasings => SelectedFor(Part.Casings);
    private static string SelectionKey(Part part) => part == Part.Conveyor ? "sand_theme_conveyor" : part == Part.Casings ? "sand_theme_casings" : "sand_theme_background";
    public static int SelectedBackground => SelectedFor(Part.Background);
    public static int SelectedFor(Part part)
    {
        int fallback=part==Part.Background?DefaultBackground:0;
        int selected=Mathf.Clamp(GameProgressStore.GetInt(SelectionKey(part),GameProgressStore.GetInt("sand_theme_selected",fallback)),0,CountFor(part)-1);
        return IsAvailable(selected,part) && IsUnlocked(selected,part)?selected:fallback;
    }
    private static string OwnershipKey(int index, Part part) => SelectionKey(part) + "_owned_" + index;
    public static bool IsUnlocked(int index) => IsUnlocked(index, Part.Conveyor) && IsUnlocked(index, Part.Casings);
    public static bool IsUnlocked(int index, Part part)
    {
        if (!IsAvailable(index, part)) return false;
        if (part == Part.Background ? index == DefaultBackground : index == 0) return true;
        // Old saves and restored shared-theme receipts retain everything they originally unlocked.
        return GameProgressStore.GetBool(OwnershipKey(index, part))
            || ((part != Part.Background || index < 12) && GameProgressStore.GetBool("sand_theme_owned_" + index));
    }
    public static bool Select(int index)
    {
        if (index < 0 || index >= MaterialCount || !IsUnlocked(index)
            || (IsAvailable(index, Part.Background) && !IsUnlocked(index, Part.Background))) return false;
        var previous = new[] { Selected, SelectedCasings, SelectedBackground };
        GameProgressStore.SetInt("sand_theme_selected", index);
        GameProgressStore.SetInt("sand_theme_conveyor", index);
        GameProgressStore.SetInt("sand_theme_background", IsAvailable(index,Part.Background)?index:DefaultBackground);
        GameProgressStore.SetInt("sand_theme_casings", index);
        GameProgressStore.Save(); Refresh();
        for (int p = 0; p < previous.Length; p++)
        {
            var part = (Part)p;
            int selected = SelectedFor(part);
            if (selected != previous[p]) Analytic.CosmeticSelected(NameKey(selected,part),part.ToString().ToLowerInvariant(),selected);
        }
        return true;
    }
    public static bool Select(int index, Part part)
    {
        if(!IsAvailable(index,part)||!IsUnlocked(index,part))return false;
        bool changed=SelectedFor(part)!=index;
        GameProgressStore.SetInt(SelectionKey(part),index);
        GameProgressStore.Save();Refresh();
        if(changed)Analytic.CosmeticSelected(NameKey(index,part),part.ToString().ToLowerInvariant(),index);
        return true;
    }
    public static bool Acquire(int index,Part part,string source)
    {
        if(!IsAvailable(index,part))return false;
        bool owned=IsUnlocked(index,part);
        Unlock(index,part);
        if(!IsUnlocked(index,part))return false;
        if(!owned)Analytic.CosmeticAcquired(NameKey(index,part),part.ToString().ToLowerInvariant(),index,source,ProductTag(index,part));
        return Select(index,part);
    }
    // Legacy shared entitlement, used only for old receipts and editor fixtures.
    public static void Unlock(int index)
    {
        if (index <= 0 || index >= Count) return;
        GameProgressStore.SetBool("sand_theme_owned_" + index, true); GameProgressStore.Save();
    }
    public enum Access { Free, Rewarded, Coins, Money }
    public static void Unlock(int index,Part part)
    {
        if (!IsAvailable(index, part)) return;
        GameProgressStore.SetBool(OwnershipKey(index, part), true);
        GameProgressStore.Save();
    }
    private static Access OriginalAccessFor(int index,Part part)
    {
        if(part==Part.Background)
        {
            if(index==DefaultBackground)return Access.Free;
            if(index>=8 && index<12)return Access.Rewarded;
            if(index>=12)return index<PremiumBackgroundEnd?Access.Money:index>=MinimalRewardedStart?Access.Rewarded:Access.Coins;
        }
        return index==0?Access.Free:index<=3?Access.Rewarded:index>=8 && index<=11?Access.Money:Access.Coins;
    }
    // YouTube sells former individual IAP cosmetics for gold; bundles stay disabled separately.
    private static bool UsesGoldInsteadOfMoney(int index,Part part) => !YoutubePlatform.PurchasesAllowed && OriginalAccessFor(index,part)==Access.Money;
    public static Access AccessFor(int index) => AccessFor(index,Part.Conveyor);
    public static Access AccessFor(int index,Part part) => UsesGoldInsteadOfMoney(index,part)?Access.Coins:OriginalAccessFor(index,part);
    private static int BaseCoinPrice(int index) => index>=PremiumBackgroundEnd ? 1000 : index < 12 ? 600 + (index - 4) * 200 : 1400 + (index - 12) * 100;
    public static int CoinPrice(int index) => index<MaterialCount ? CoinPrice(index,Part.Conveyor) : BaseCoinPrice(index);
    public static int CoinPrice(int index,Part part)
    {
        if(UsesGoldInsteadOfMoney(index,part))return part==Part.Background?2000:1000;
        if(part!=Part.Background)
        {
            int step=index<8?index-4:index-8;
            return part==Part.Casings?Mathf.Clamp(100+step*100,100,600):Mathf.Clamp(400+step*100,400,1000);
        }
        return index>=MinimalBackgroundStart ? 400 : index>=4 && index<=7 ? (index-3)*100 : BaseCoinPrice(index);
    }
    public static int ShopAccessRank(int index,Part part)
    {
        switch(AccessFor(index,part)){case Access.Money:return 0;case Access.Coins:return 1;case Access.Rewarded:return 2;default:return 3;}
    }
    public static int[] DisplayOrderFor(Part part) => Enumerable.Range(0,CountFor(part)).Where(id=>IsAvailable(id,part)).OrderBy(id=>ShopAccessRank(id,part)).ThenBy(id=>AccessFor(id,part)==Access.Coins?CoinPrice(id,part):part==Part.Background && AccessFor(id,part)==Access.Rewarded?(id>=MinimalRewardedStart?id-MinimalRewardedStart:id>=8?id-4:id+8):id).ThenBy(id=>id).ToArray();
    // Retired shared products are restored, but are never offered for new purchases.
    public static string ProductTag(int index) => "sand_theme_" + Names[index].Substring(6);
    public static string ProductTag(int index, Part part)
    {
        string prefix = part == Part.Conveyor ? "sand_conveyor_" : part == Part.Casings ? "sand_casings_" : "sand_background_";
        return prefix + NameKey(index, part).Substring(6);
    }
    public static void RestorePurchases(System.Action<string, System.Action> restoreProduct)
    {
        foreach (Part part in new[] { Part.Conveyor, Part.Casings, Part.Background })
        {
            for (int index = 0; index < CountFor(part); index++)
            {
                if (!IsAvailable(index, part) || AccessFor(index, part) != Access.Money) continue;
                int owned = index;
                restoreProduct(ProductTag(owned, part), () => Unlock(owned, part));
            }
        }
        for (int index = 8; index <= 11; index++)
        {
            int owned = index;
            restoreProduct(ProductTag(owned), () => Unlock(owned));
        }
    }
    public static int Pattern(int index) => index < 6 ? 0 : index - 5;
    private static readonly Color[] Backgrounds = {
        new Color(.93f,.97f,.93f), new Color(.85f,.94f,.98f), new Color(1,.88f,.73f), new Color(.88f,.96f,.80f),
        new Color(.78f,.93f,.96f), new Color(.91f,.86f,.97f), new Color(1,.89f,.93f), new Color(.91f,.95f,.89f),
        new Color(.79f,.84f,.94f), new Color(.96f,.91f,.78f), new Color(.91f,.94f,.96f), new Color(.96f,.91f,.81f),
        new Color(.84f,.94f,.85f), new Color(.83f,.91f,.97f), new Color(.95f,.90f,.82f), new Color(.79f,.91f,.95f),
        new Color(.96f,.89f,.85f), new Color(.74f,.81f,.91f), new Color(.96f,.87f,.77f), new Color(.79f,.92f,.91f),
        new Color(.88f,.91f,.97f), new Color(.90f,.94f,.78f), new Color(.99f,.93f,.77f), new Color(.81f,.89f,.96f) };
    private static readonly Color[] Decks = {
        new Color(.29f,.43f,.43f), new Color(.30f,.49f,.61f), new Color(.57f,.37f,.28f), new Color(.29f,.46f,.30f),
        new Color(.14f,.48f,.60f), new Color(.44f,.35f,.61f), new Color(.74f,.39f,.52f), new Color(.31f,.43f,.35f),
        new Color(.23f,.29f,.48f), new Color(.27f,.30f,.29f), new Color(.45f,.54f,.58f), new Color(.55f,.38f,.23f),
        new Color(.26f,.47f,.36f), new Color(.37f,.52f,.65f), new Color(.52f,.32f,.24f), new Color(.17f,.40f,.52f),
        new Color(.61f,.38f,.36f), new Color(.25f,.28f,.46f), new Color(.64f,.37f,.22f), new Color(.18f,.40f,.39f),
        new Color(.39f,.38f,.60f), new Color(.41f,.48f,.22f), new Color(.61f,.44f,.17f), new Color(.22f,.40f,.61f) };
    public static Color Background(int index) => Backgrounds[Mathf.Clamp(index,0,Backgrounds.Length-1)];
    public static Color Deck(int index) => Decks[Mathf.Clamp(index,0,Decks.Length-1)];
    public static Color Casing(int index) => index == 9 ? new Color(.80f,.65f,.32f) : Color.Lerp(Deck(index), Background(index), .57f);
    public static void Refresh()
    {
        foreach (var graphic in Object.FindObjectsOfType<ThemeBackdropGraphic>()) { graphic.SetVerticesDirty();graphic.SetMaterialDirty(); }
        foreach (var graphic in Object.FindObjectsOfType<ConveyorSurfaceGraphic>()) graphic.SetVerticesDirty();
        foreach (var graphic in Object.FindObjectsOfType<ConveyorHoodGraphic>()) graphic.SetVerticesDirty();
    }
}
