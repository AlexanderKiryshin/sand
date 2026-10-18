using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MirraGames.SDK.Common;

public static class GameReworkChecks
{
    public static void RebuildDynamicFallbackAtlas()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
        Require(font!=null,"Dynamic fallback font missing");
        var atlas=font.atlasTexture;
        font.ClearFontAssetData();
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        Debug.Log("Dynamic fallback font atlas cache rebuilt.");
    }
    [MenuItem("Window/Sand/Validate Game Rework")]
    public static void Run()
    {
        SessionState.SetBool("Sand.ReworkPreview",true);
        try
        {
            GameReworkBuilder.Validate();
            CheckColorFocus();CheckThemes();CheckPatternMotion();CheckBackgroundArtwork();CheckNatureBundle();CheckCosmeticBundles();CheckGoldBadge();CheckRewardedClassicBackgrounds();CheckShopOrder();
            ProgressSaveService.Initialize(1000);
            ProgressSaveService.MarkLevelCompleted(299);
            int main = ProgressSaveService.HighestUnlockedIndex;
            for (int i = 1; i <= CampaignProgression.WinterLevelCount; i++) GameProgressStore.SetBool("winter_completed_" + i, false);
            GameProgressStore.SetInt("winter_highest_unlocked", 0);
            GameProgressStore.SetInt("winter_current", 0);
            WinterProgress.Initialize(CampaignProgression.WinterLevelCount);
            Require(!WinterProgress.IsUnlocked(1), "Winter progression starts at one");
            WinterProgress.Complete(0);
            Require(WinterProgress.IsCompleted(0) && WinterProgress.IsUnlocked(1), "Winter unlocks next level");
            for (int i = 1; i < CampaignProgression.WinterLevelCount; i++) WinterProgress.Complete(i);
            Require(WinterProgress.IsCompleted(CampaignProgression.WinterLevelCount - 1), "Final winter level completes");
            Require(WinterProgress.HighestUnlocked == CampaignProgression.WinterLevelCount - 1 && !WinterProgress.IsUnlocked(CampaignProgression.WinterLevelCount), "Winter stops at campaign limit");
            WinterProgress.Initialize(CampaignProgression.WinterLevelCount);
            Require(WinterProgress.IsCompleted(CampaignProgression.WinterLevelCount - 1), "Winter completion persists after initialization");
            Require(ProgressSaveService.HighestUnlockedIndex == main, "Winter changed main progress");
            for (int i = 1; i <= CampaignProgression.WinterLevelCount; i++) GameProgressStore.SetBool("winter_completed_" + i, false);
            GameProgressStore.SetInt("winter_highest_unlocked", 0);
            File.WriteAllText("tasks/game-rework-checks.txt","PASS: level grids and inventory routes; color filter fades other colors and preserves the selected color; all-colors restores original pixels; hidden bucket colors remain undisclosed; filter follows color swaps and sections; themes persist; winter progression remains independent.");
            Debug.Log("PASS: game rework checks.");
        }
        finally { SessionState.SetBool("Sand.ReworkPreview",false); }
    }
    private static void CheckColorFocus()
    {
        var root=new GameObject("CueTest",typeof(RectTransform),typeof(Image));
        try
        {
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(120,120);
            Color32 original = new Color32(180, 100, 220, 170);
            Require(ColorFocusPanel.FilterColor(original, 2, 2).Equals(original), "Selected color altered");
            Require(ColorFocusPanel.FilterColor(original, 2, 0).Equals(original), "All colors altered");
            Color32 transparent = ColorFocusPanel.FilterColor(original, 2, 1);
            Require(transparent.r == original.r && transparent.g == original.g && transparent.b == original.b && transparent.a == (byte)(original.a * .25f), "Other colors must retain 25% alpha without darkening RGB");
            var renderer = root.AddComponent<SandRenderer>();
            var level = ScriptableObject.CreateInstance<LevelData>(); level.palette = new[] { original, original };
            var panel = root.AddComponent<ColorFocusPanel>(); panel.Bind(level, renderer); panel.Select(1);
            var bucket = new Bucket(2, 12); var focus = root.AddComponent<BucketColorFocus>(); focus.Initialize(bucket, null);
            Require(focus.IsDimmed && Mathf.Abs(root.GetComponent<CanvasRenderer>().GetAlpha()-.28f)<.001f, "Other bucket not transparent");
            bucket.SetColorAndCapacity(1, 12); focus.Refresh(); Require(!focus.IsDimmed, "Swap not followed");
            bucket.SetModifiers(0, true); focus.Refresh(); Require(!focus.IsDimmed, "Hidden color disclosed by filter");
            var sections = new Bucket(2, 6); sections.AddSection(1, 6); focus.Initialize(sections, null);
            Require(!focus.IsDimmed, "Second section not matched");
            panel.Select(0); focus.Refresh(); Require(!focus.IsDimmed && root.GetComponent<CanvasRenderer>().GetAlpha()==1, "Reset bucket opacity");
            UnityEngine.Object.DestroyImmediate(level);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void CheckThemes()
    {
        ThemeCatalog.Select(0);
        GameProgressStore.SetBool("sand_theme_owned_1",false);
        Require(!ThemeCatalog.Select(1) && ThemeCatalog.Selected==0,"Locked theme selection");
        ThemeCatalog.Unlock(1);Require(ThemeCatalog.IsUnlocked(1),"Theme reward persists");
        Require(ThemeCatalog.Select(1) && ThemeCatalog.Selected==1,"Theme selection persists");
        Require(ThemeCatalog.Count==50 && ThemeCatalog.MaterialCount==24 && ThemeCatalog.AccessFor(4)==ThemeCatalog.Access.Coins && ThemeCatalog.AccessFor(8)==ThemeCatalog.Access.Money,"Theme access types");
        ThemeCatalog.Unlock(2);ThemeCatalog.Unlock(3);
        ThemeCatalog.Unlock(4);
        Require(ThemeCatalog.Select(2,ThemeCatalog.Part.Casings) && ThemeCatalog.Select(4,ThemeCatalog.Part.Background),"Independent theme selection failed");
        Require(ThemeCatalog.Selected==1 && ThemeCatalog.SelectedCasings==2 && ThemeCatalog.SelectedBackground==4,"Theme categories changed each other");
        ThemeCatalog.Select(0,ThemeCatalog.Part.Conveyor);
        Require(ThemeCatalog.Selected==0 && ThemeCatalog.SelectedCasings==2 && ThemeCatalog.SelectedBackground==4,"Conveyor changed casings or background");
        foreach(int id in Enumerable.Range(0,4))
        {
            GameProgressStore.SetInt("sand_theme_background",id);
            Require(!ThemeCatalog.Select(id,ThemeCatalog.Part.Background) && ThemeCatalog.SelectedBackground==ThemeCatalog.DefaultBackground,"Retired background remained selectable or active in an old save");
        }
        ThemeCatalog.Unlock(12);
        GameProgressStore.SetBool("sand_theme_background_owned_12",false);
        Require(ThemeCatalog.AccessFor(12,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Money && ThemeCatalog.AccessFor(12,ThemeCatalog.Part.Conveyor)==ThemeCatalog.Access.Coins,"Detailed background access is not money-only");
        Require(!ThemeCatalog.IsUnlocked(12,ThemeCatalog.Part.Background) && !ThemeCatalog.Select(12,ThemeCatalog.Part.Background) && !ThemeCatalog.Select(12),"Coin style bypassed paid background ownership");
        GameProgressStore.SetInt("sand_theme_background",12);
        Require(ThemeCatalog.SelectedBackground==ThemeCatalog.DefaultBackground,"Unowned detailed background applied from saved selection");
        ThemeCatalog.Unlock(12,ThemeCatalog.Part.Background);
        Require(ThemeCatalog.Select(12,ThemeCatalog.Part.Background) && ThemeCatalog.SelectedBackground==12,"Purchased background did not apply");
        Require(ThemeCatalog.ProductTag(12,ThemeCatalog.Part.Background)=="sand_background_greenhouse","Background product tag");
        ThemeCatalog.Unlock(24,ThemeCatalog.Part.Background);
        Require(ThemeCatalog.Select(24,ThemeCatalog.Part.Background) && !ThemeCatalog.Select(24,ThemeCatalog.Part.Conveyor) && !ThemeCatalog.Select(24,ThemeCatalog.Part.Casings),"Nature background leaked into material selection");
        ThemeCatalog.Select(1);
        Require(!ThemeCatalog.Select(-1) && !ThemeCatalog.Select(ThemeCatalog.Count),"Invalid theme index");
    }
    private static void CheckPatternMotion()
    {
        for(int theme=6;theme<ThemeCatalog.MaterialCount;theme++)
        {
            using(var first=new VertexHelper())using(var moved=new VertexHelper())
            {
                ThemePatternMesh.Draw(first,(u,v)=>new Vector2(u,v),theme);
                ThemePatternMesh.Draw(moved,(u,v)=>new Vector2(u,v),theme,horizontalOffset:-.073f);
                var a=new System.Collections.Generic.List<UIVertex>();var b=new System.Collections.Generic.List<UIVertex>();
                first.GetUIVertexStream(a);moved.GetUIVertexStream(b);
                Require(a.Count>0&&b.Count>0,"Pattern geometry missing "+theme);
                Require(a.Count!=b.Count||a.Where((vertex,index)=>(vertex.position-b[index].position).sqrMagnitude>.000001f).Any(),"Pattern stayed stationary "+theme);
                Require(b.All(vertex=>vertex.position.x>=-.00001f&&vertex.position.x<=1.00001f&&vertex.position.y>=-.00001f&&vertex.position.y<=1.00001f),"Moving pattern escaped belt bounds "+theme);
            }
        }
    }
    private static void CheckBackgroundArtwork()
    {
        var textures=Enumerable.Range(4,ThemeCatalog.Count-4).Select(ThemeBackgroundAssets.Get).ToArray();
        Require(textures.All(t=>t!=null && t.width>=800 && t.height>=1200),"Detailed background artwork missing or too small");
        Require(textures.Distinct().Count()==ThemeCatalog.Count-4,"Background artwork repeated");
        Require(Enumerable.Range(12,ThemeCatalog.PremiumBackgroundEnd-12).All(i=>ThemeCatalog.AccessFor(i,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Money),"Detailed background not money-only");
        Require(Enumerable.Range(31,4).All(i=>ThemeCatalog.AccessFor(i,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Coins),"Coin nature tier unavailable");
        Require(Enumerable.Range(35,3).All(i=>ThemeCatalog.AccessFor(i,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Coins && ThemeCatalog.CoinPrice(i,ThemeCatalog.Part.Background)==1000),"Converted nature backgrounds are not 1000 gold");
    }
    private static void CheckNatureBundle()
    {
        Require(NatureBackgroundBundle.Backgrounds.SequenceEqual(new[]{27,24,29}),"Nature bundle must contain forest, lake and golden birch grove");
        var prices=NatureBackgroundBundle.Backgrounds.Select(id=>new ProductData(ThemeCatalog.ProductTag(id,ThemeCatalog.Part.Background),100f,"RUB")).ToArray();
        Require(NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,210f,"RUB"),prices,out string total,out int discount) && discount==30 && total.StartsWith("300 "),"Bundle discount does not match actual SDK prices");
        Require(NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,150f,"RUB"),prices,out _,out int halfPriceDiscount) && halfPriceDiscount==50,"Half-price bundle must display 50% savings");
        Require(!NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,300f,"RUB"),prices,out _,out _),"Undiscounted bundle advertised a discount");
        Require(!NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,210f,"USD"),prices,out _,out _) && !NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,210f,"RUB"),null,out _,out _),"Missing or mismatched prices advertised a discount");
        Require(!NatureBackgroundBundle.TrySavings(new ProductData(NatureBackgroundBundle.ProductTag,float.NaN,"RUB"),prices,out _,out _),"Invalid SDK price advertised a discount");
        foreach(int id in NatureBackgroundBundle.Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
        GameProgressStore.SetBool("sand_theme_background_owned_31",false);
        GameProgressStore.SetInt(NatureBackgroundBundle.LastShownKey,0);
        Require(!NatureBackgroundBundle.ShouldOffer(49,false) && NatureBackgroundBundle.ShouldOffer(50,false),"Nature offer starts at wrong level");
        Require(!NatureBackgroundBundle.ShouldOffer(69,false) && NatureBackgroundBundle.ShouldOffer(70,false) && NatureBackgroundBundle.ShouldOffer(90,false),"Nature offer interval is not 20 levels");
        Require(!NatureBackgroundBundle.ShouldOffer(50,true),"Nature offer appears in winter campaign");
        NatureBackgroundBundle.MarkShown(50);
        Require(!NatureBackgroundBundle.ShouldOffer(50,false) && NatureBackgroundBundle.ShouldOffer(70,false),"Nature offer repeats after reload or replay");
        foreach(int id in new[]{25,26,28,30})GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
        ThemeCatalog.Unlock(24,ThemeCatalog.Part.Background);
        int selected=ThemeCatalog.SelectedBackground;
        NatureBackgroundBundle.Grant();NatureBackgroundBundle.Grant();
        Require(NatureBackgroundBundle.IsOwned && NatureBackgroundBundle.ShouldOffer(70,false) && CosmeticBundleCatalog.PickUnowned()!=CosmeticBundleCatalog.Nature,"Purchased nature bundle must be excluded while other bundles remain available");
        Require(new[]{25,26,28,30}.All(id=>!ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background)),"Three-background bundle unlocked additional nature art");
        Require(ThemeCatalog.SelectedBackground==selected && !ThemeCatalog.IsUnlocked(31,ThemeCatalog.Part.Background),"Bundle changed selection or unlocked unrelated coin artwork");
        foreach(int id in NatureBackgroundBundle.Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
        GameProgressStore.SetBool("sand_theme_background_owned_31",false);
        GameProgressStore.SetInt(NatureBackgroundBundle.LastShownKey,0);
    }
    private static void CheckCosmeticBundles()
    {
        Require(CosmeticBundleCatalog.All.Select(bundle=>bundle.Tag).Distinct().Count()==6,"Bundle product tags are not unique");
        Require(CosmeticBundleCatalog.All.SelectMany(bundle=>bundle.Backgrounds).Distinct().Count()==CosmeticBundleCatalog.All.Length*3,"A background appears in multiple bundles");
        var prior=Enumerable.Range(12,19).ToDictionary(id=>id,id=>ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background));
        int selected=ThemeCatalog.SelectedBackground;
        try
        {
            foreach(var bundle in CosmeticBundleCatalog.All)
            {
                Require(bundle.Backgrounds.Length==3 && bundle.Backgrounds.Distinct().Count()==3 && bundle.Backgrounds.All(id=>ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Money),"Bundle must contain three distinct money-only backgrounds");
                foreach(int id in prior.Keys)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                Require(!bundle.IsOwned,"Unpurchased bundle is owned");bundle.Grant();bundle.Grant();
                Require(bundle.IsOwned && prior.Keys.All(id=>ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background)==bundle.Backgrounds.Contains(id)),"Bundle granted wrong backgrounds or grant was not idempotent");
                var prices=bundle.Backgrounds.Select(id=>new ProductData(ThemeCatalog.ProductTag(id,ThemeCatalog.Part.Background),100f,"RUB")).ToArray();
                Require(NatureBackgroundBundle.TrySavings(new ProductData(bundle.Tag,150f,"RUB"),prices,out _,out int percent,bundle) && percent==50,"Bundle SDK discount must be calculated for its own contents");
            }
        }
        finally {foreach(var state in prior)GameProgressStore.SetBool("sand_theme_background_owned_"+state.Key,state.Value);}
        Require(ThemeCatalog.SelectedBackground==selected,"Bundle grants changed current background selection");
    }
    private static void CheckGoldBadge()
    {
        var styles=Enumerable.Range(4,20).ToDictionary(id=>id,id=>ThemeCatalog.IsUnlocked(id));
        var backgrounds=Enumerable.Range(31,ThemeCatalog.Count-31).ToDictionary(id=>id,id=>ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background));
        try
        {
            foreach(int id in styles.Keys)GameProgressStore.SetBool("sand_theme_owned_"+id,false);
            foreach(int id in backgrounds.Keys)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
            Require(ShopGoldBadge.CountAffordable(99)==0 && ShopGoldBadge.CountAffordable(100)==1,"Badge ignores actual gold affordability or repeats shared unlocks");
            Require(ShopGoldBadge.CountAffordable(700)==11 && ShopGoldBadge.CountAffordable(700,ThemeCatalog.Part.Conveyor)==4 && ShopGoldBadge.CountAffordable(700,ThemeCatalog.Part.Casings)==4 && ShopGoldBadge.CountAffordable(700,ThemeCatalog.Part.Background)==11,"Tab counters do not match their purchasable cosmetics");
            Require(ShopGoldBadge.CountAffordable(4000)==30,"Badge includes money-only/rewarded styles or repeats shared products");
            GameProgressStore.SetBool("sand_theme_owned_4",true);Require(ShopGoldBadge.CountAffordable(700)==10,"Purchased style remains in counter");
            GameProgressStore.SetBool("sand_theme_background_owned_31",true);Require(ShopGoldBadge.CountAffordable(100)==0 && ShopGoldBadge.CountAffordable(1000)==28,"Counter includes owned backgrounds");
        }
        finally
        {
            foreach(var state in styles)GameProgressStore.SetBool("sand_theme_owned_"+state.Key,state.Value);
            foreach(var state in backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+state.Key,state.Value);
        }
    }
    private static void CheckRewardedClassicBackgrounds()
    {
        foreach(int id in Enumerable.Range(8,4))
        {
            bool shared=GameProgressStore.GetBool("sand_theme_owned_"+id),background=GameProgressStore.GetBool("sand_theme_background_owned_"+id);
            try
            {
                GameProgressStore.SetBool("sand_theme_owned_"+id,false);GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                Require(ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Rewarded && ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Conveyor)==ThemeCatalog.Access.Money,"Classic background was not changed to rewarded independently");
                ThemeCatalog.Unlock(id,ThemeCatalog.Part.Background);
                Require(ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background) && !ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Conveyor) && !ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Casings),"Rewarded background unlock granted paid conveyor/casings");
                GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);GameProgressStore.SetBool("sand_theme_owned_"+id,true);
                Require(ThemeCatalog.IsUnlocked(id,ThemeCatalog.Part.Background),"Previously purchased background ownership was lost");
            }
            finally {GameProgressStore.SetBool("sand_theme_owned_"+id,shared);GameProgressStore.SetBool("sand_theme_background_owned_"+id,background);}
        }
    }
    private static void CheckShopOrder()
    {
        foreach(var part in new[]{ThemeCatalog.Part.Conveyor,ThemeCatalog.Part.Casings,ThemeCatalog.Part.Background})
        {
            var order=ThemeCatalog.DisplayOrderFor(part);
            var available=Enumerable.Range(0,ThemeCatalog.CountFor(part)).Where(id=>ThemeCatalog.IsAvailable(id,part)).ToArray();
            Require(order.OrderBy(id=>id).SequenceEqual(available),"Shop sorting omits or repeats an available cosmetic");
            for(int i=1;i<order.Length;i++)Require(ThemeCatalog.ShopAccessRank(order[i-1],part)<=ThemeCatalog.ShopAccessRank(order[i],part),"Shop does not show money, gold, then rewarded cosmetics");
            var prices=order.Where(id=>ThemeCatalog.AccessFor(id,part)==ThemeCatalog.Access.Coins).Select(id=>ThemeCatalog.CoinPrice(id,part)).ToArray();
            Require(prices.SequenceEqual(prices.OrderBy(price=>price)),"Coin cosmetics are not ordered by price");
            if(part!=ThemeCatalog.Part.Background)Require(prices.Min()==400 && prices.Max()==1000,"Conveyor and casing gold prices must range from 400 to 1000");
        }
        var simplePrices=Enumerable.Range(0,ThemeCatalog.Count).Where(id=>(id>=4 && id<=7 || id>=ThemeCatalog.MinimalBackgroundStart) && ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Coins).Select(id=>ThemeCatalog.CoinPrice(id,ThemeCatalog.Part.Background)).OrderBy(price=>price).ToArray();
        Require(simplePrices.SequenceEqual(new[]{100,200,300}.Concat(Enumerable.Repeat(400,simplePrices.Length-3))),"Simple backgrounds must have one price each of 100, 200 and 300 gold, with all others at 400");
        Require(Enumerable.Range(42,4).All(id=>ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Background)==(id==ThemeCatalog.DefaultBackground?ThemeCatalog.Access.Free:ThemeCatalog.Access.Coins)) && Enumerable.Range(46,4).All(id=>ThemeCatalog.AccessFor(id,ThemeCatalog.Part.Background)==ThemeCatalog.Access.Rewarded),"Eight additional minimal backgrounds must be split four coins/four rewarded");
    }
    public static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
}
