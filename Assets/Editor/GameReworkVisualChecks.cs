using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class GameReworkVisualChecks
{
    private const string Key="Sand.ReworkPreview";
    private const string Folder="tasks/previews/game-rework";
    private static int stage;
    private static int offerCoinsBefore,offerReward;
    private static float shopTimeBeforeOffer;
    private static double carouselWaitDeadline;
    private static double next;
    private static Action afterCapture;
    private static readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
    static GameReworkVisualChecks()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,t,k)=>{if(SessionState.GetBool(Key,false)&&(k==LogType.Exception||k==LogType.Error))errors.Add(m);};
    }
    public static void Run()
    {
        SessionState.SetBool("Sand.ThemePreviewOnly",false);
        SessionState.SetBool("Sand.BackgroundPreviewOnly",false);
        SessionState.SetBool("Sand.OfferPreviewOnly",false);
        SessionState.SetBool("Sand.GoldPreviewOnly",false);
        stage = 0; next = 0; afterCapture = null; errors.Clear();
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        CampaignVisualChecks.SetPortraitGameView();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    public static void RunThemePreview() { Run(); SessionState.SetBool("Sand.ThemePreviewOnly",true); }
    public static void RunBackgroundPreview() { RunThemePreview(); SessionState.SetBool("Sand.BackgroundPreviewOnly",true); }
    public static void RunOfferPreview() { RunBackgroundPreview();SessionState.SetBool("Sand.OfferPreviewOnly",true); }
    public static void RunGoldPreview() {RunThemePreview();SessionState.SetBool("Sand.GoldPreviewOnly",true);}
    private static void Tick()
    {
        bool requested=File.Exists("Library/run-game-rework-checks") || File.Exists("Library/run-theme-visual-checks");
        if(requested && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Key,false);SessionState.SetBool("Sand.CampaignPreview",false);
            EditorApplication.isPlaying=false;return;
        }
        if (!EditorApplication.isPlaying && (File.Exists("Library/run-game-rework-checks") || File.Exists("Library/run-theme-visual-checks")))
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool themesOnly=File.Exists("Library/run-theme-visual-checks");
            string requestMode=themesOnly?File.ReadAllText("Library/run-theme-visual-checks").Trim():"";
            bool backgroundsOnly=requestMode=="nature-only",offerOnly=requestMode=="offer-only",goldOnly=requestMode=="gold-only";
            if(themesOnly)File.Delete("Library/run-theme-visual-checks");
            if(File.Exists("Library/run-game-rework-checks"))File.Delete("Library/run-game-rework-checks");
            try { GameReworkChecks.Run(); }
            catch (Exception error) { File.WriteAllText("tasks/game-rework-checks.txt", error.ToString()); throw; }
            if(goldOnly)RunGoldPreview();else if(offerOnly)RunOfferPreview();else if(backgroundsOnly)RunBackgroundPreview();else if(themesOnly)RunThemePreview();else Run();
            return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
        Application.runInBackground=true;
        var themeMenu=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();
        var natureOffer=UnityEngine.Object.FindObjectOfType<NatureBackgroundOffer>();
        if(!CampaignGallery.IsOpen && (SandEconomyHud.instance==null || !SandEconomyHud.instance.IsShopOpen) && (themeMenu==null || !themeMenu.IsOpen) && (natureOffer==null || !natureOffer.IsOpen)) Time.timeScale=1;
        if(next==0)next=EditorApplication.timeSinceStartup+6;
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(afterCapture!=null)
            {
                var action=afterCapture;afterCapture=null;action();next=EditorApplication.timeSinceStartup+2.5;return;
            }
            var manager=UnityEngine.Object.FindObjectOfType<GameManager>();
            if(manager==null)throw new Exception("Game did not start.");
            if(SessionState.GetBool("Sand.ThemePreviewOnly",false))
            {
                if(stage==1 && SessionState.GetBool("Sand.GoldPreviewOnly",false)){manager.LoadLevel(14);stage=62;}
                else if(stage==1 && SessionState.GetBool("Sand.OfferPreviewOnly",false)) {ThemeCatalog.Select(0);ThemeCatalog.Unlock(35,ThemeCatalog.Part.Background);ThemeCatalog.Select(35,ThemeCatalog.Part.Background);manager.LoadLevel(14);stage=44;}
                else if(stage==1 && SessionState.GetBool("Sand.BackgroundPreviewOnly",false)) {ThemeCatalog.Select(0);manager.LoadLevel(14);stage=33;}
                else if(stage==1) { ThemeCatalog.Unlock(1);ThemeCatalog.Select(1);UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Open();stage=8; }
                else if(stage==9) {UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Open();UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Choose(6);stage=14;}
            }
            if(stage==48 && natureOffer.IsOpen)
            {
                var settling=UnityEngine.Object.FindObjectOfType<NatureOfferPager>();
                if(Mathf.Abs(settling.content.anchoredPosition.x-settling.TargetPosition)>=1)
                {
                    if(carouselWaitDeadline==0)carouselWaitDeadline=EditorApplication.timeSinceStartup+20;
                    if(EditorApplication.timeSinceStartup<carouselWaitDeadline){next=EditorApplication.timeSinceStartup+.25;return;}
                }
                carouselWaitDeadline=0;
            }
            switch(stage++)
            {
                case 0:
                    GameProgressStore.SetInt("sand_progress_highest_unlocked",14);
                    GameProgressStore.SetInt("sand_progress_highest_completed", -1);
                    GameProgressStore.SetInt("sand_progress_current_level", 0);
                    ProgressSaveService.Load();
                    LocalizationManager.Instance.SetLanguage(MirraGames.SDK.Common.LanguageType.Russian);
                    manager.LoadLevel(0);next=EditorApplication.timeSinceStartup+3;return;
                case 1: Capture("01-starter",()=>manager.LoadLevel(14));break;
                case 2: Capture("02-all-colors",()=>UnityEngine.Object.FindObjectOfType<ColorFocusPanel>().Select(2));break;
                case 3: Capture("02-selected-color",manager.OpenCollection);break;
                case 4:
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectsOfType<RawImage>().Where(i=>i.name=="Painting").All(i=>i.color==Color.white),"Gallery art is dimmed");
                    Capture("03-gallery",()=>UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="WinterLevels").onClick.Invoke());break;
                case 5: Capture("04-winter-collection",()=>{UnityEngine.Object.FindObjectOfType<CampaignGallery>().Close();manager.LoadLevel(FirstWinter(manager));});break;
                case 6:
                    int winter = FirstWinter(manager);
                    GameReworkChecks.Require(manager.IsSeasonal(winter),"Seasonal entry");
                    int highest=ProgressSaveService.HighestUnlockedIndex,current=ProgressSaveService.CurrentLevelIndex;
                    typeof(GameManager).GetMethod("HandleLevelComplete",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                    GameReworkChecks.Require(ProgressSaveService.HighestUnlockedIndex==highest && ProgressSaveService.CurrentLevelIndex==current && manager.IsLevelCompleted(winter),"Seasonal completion changed main save");
                    manager.RestartLevel();ThemeCatalog.Unlock(1);ThemeCatalog.Select(1);break;
                case 7: Capture("05-winter-game",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Open());break;
                case 8:
                    var viewport=(RectTransform)GameObject.Find("ThemeCard").transform.Find("Viewport");
                    var first=(RectTransform)viewport.Find("Themes/Theme_"+ThemeCatalog.DisplayOrderFor(ThemeCatalog.Part.Conveyor)[0]);
                    GameReworkChecks.Require(viewport.rect.Contains(viewport.InverseTransformPoint(first.TransformPoint(first.rect.center))),"Theme cards outside visible viewport: first="+viewport.InverseTransformPoint(first.TransformPoint(first.rect.center))+", viewport="+viewport.rect+", scroll="+GameObject.Find("ThemeCard").transform.Find("Viewport").GetComponent<ScrollRect>().verticalNormalizedPosition);
                    Capture("06-themes",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Close());break;
                case 9:
                    GameReworkChecks.Require(manager.LevelCount == CampaignProgression.LevelCount + CampaignProgression.WinterLevelCount, "Campaign target counts");
                    GameProgressStore.SetInt("sand_progress_highest_unlocked", CampaignProgression.LevelCount - 1);
                    ProgressSaveService.Load();
                    manager.LoadLevel(CampaignProgression.LevelCount - 1);
                    GameReworkChecks.Require(manager.GetDisplayNumber(manager.CurrentLevelIndex)==1000 && !manager.HasNextLevelInCampaign,"Ordinary campaign boundary");
                    manager.LoadNextLevel();
                    GameReworkChecks.Require(CampaignGallery.IsOpen && manager.CurrentLevelIndex==999,"Ordinary campaign crossed into winter");
                    UnityEngine.Object.FindObjectOfType<CampaignGallery>().Close();
                    GameProgressStore.SetInt("winter_highest_unlocked",CampaignProgression.WinterLevelCount-1);
                    manager.LoadLevel(manager.LevelCount-1);
                    GameReworkChecks.Require(manager.GetDisplayNumber(manager.CurrentLevelIndex)==200 && !manager.HasNextLevelInCampaign,"Winter campaign boundary");
                    manager.LoadNextLevel();
                    GameReworkChecks.Require(CampaignGallery.IsOpen && manager.CurrentLevelIndex==manager.LevelCount-1,"Winter campaign overflow");
                    Capture("07-winter-last-page",()=>UnityEngine.Object.FindObjectOfType<CampaignGallery>().Close());break;
                case 10:
                    manager.LoadLevel(1);next=EditorApplication.timeSinceStartup+4;return;
                case 11:
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectOfType<TutorialController>().FilterTutorialStep==1,"Second level filter tutorial missing");
                    Capture("08-filter-tutorial-pick",()=>{var palette=UnityEngine.Object.FindObjectOfType<ColorFocusPanel>();palette.Select(palette.RedColor);});break;
                case 12:
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectOfType<TutorialController>().FilterTutorialStep==2,"Color choice did not advance tutorial");
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectsOfType<BucketColorFocus>().Any(b=>b.IsDimmed&&b.FilterAlpha<.4f),"Unrelated buckets not transparent");
                    Capture("09-filter-tutorial-reset",()=>UnityEngine.Object.FindObjectOfType<ColorFocusPanel>().Select(0));break;
                case 13:
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectOfType<TutorialController>().FilterTutorialStep==0,"All colors did not finish tutorial");
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectsOfType<BucketColorFocus>().All(b=>!b.IsDimmed),"Buckets not restored");
                    UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Open();UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Choose(6);break;
                case 14:
                    GameReworkChecks.Require(ThemeCatalog.Selected==1,"Preview applied locked theme");
                    Capture("10-pattern-theme-preview",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Choose(8));break;
                case 15: Capture("11-paid-theme-preview",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Close());break;
                case 16:
                    var menu=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();
                    GameProgressStore.SetBool("sand_theme_owned_4",false);
                    GameProgressStore.SetBool("sand_theme_conveyor_owned_4",false);
                    GameProgressStore.SetBool("sand_theme_casings_owned_4",false);
                    int coinStart=CoinManager.instance.Coins;CoinManager.instance.AddCoinsOnBuy(ThemeCatalog.CoinPrice(4));
                    menu.Open();menu.Choose(4);menu.ApplyPreview();
                    GameReworkChecks.Require(ThemeCatalog.IsUnlocked(4,ThemeCatalog.Part.Conveyor)&&!ThemeCatalog.IsUnlocked(4,ThemeCatalog.Part.Casings)&&ThemeCatalog.Selected==4&&CoinManager.instance.Coins==coinStart,"Coin purchase leaked to casings or exact debit failed");
                    menu.ApplyPreview();GameReworkChecks.Require(CoinManager.instance.Coins==coinStart,"Owned theme charged twice");
                    GameProgressStore.SetBool("sand_theme_owned_8",false);menu.Choose(8);
                    GameReworkChecks.Require(!ThemeCatalog.IsUnlocked(8)&&CoinManager.instance.Coins==coinStart,"Paid-only theme bypassed payment");
                    menu.Close();ThemeCatalog.Select(1);break;
                case 17:
                    var customization=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();
                    manager.LoadLevel(14);customization.Open();customization.SetCategory(ThemeCatalog.Part.Conveyor);ThemeCatalog.Unlock(2);ThemeCatalog.Unlock(3);ThemeCatalog.Unlock(6);customization.Choose(6);customization.ApplyPreview();
                    GameReworkChecks.Require(ThemeCatalog.Selected==6&&ThemeCatalog.SelectedCasings==1&&ThemeCatalog.SelectedBackground==ThemeCatalog.DefaultBackground,"Conveyor applied to other categories");
                    break;
                case 18:
                    Capture("12-conveyor-tab",()=>{var m=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();m.SetCategory(ThemeCatalog.Part.Casings);m.Choose(3);m.ApplyPreview();});break;
                case 19:
                    GameReworkChecks.Require(ThemeCatalog.Selected==6&&ThemeCatalog.SelectedCasings==3&&ThemeCatalog.SelectedBackground==ThemeCatalog.DefaultBackground,"Casings applied to other categories");
                    Capture("13-casings-tab",()=>{var m=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();m.SetCategory(ThemeCatalog.Part.Background);m.Choose(4);m.ApplyPreview();});break;
                case 20:
                    GameReworkChecks.Require(ThemeCatalog.Selected==6&&ThemeCatalog.SelectedCasings==3&&ThemeCatalog.SelectedBackground==4,"Background applied to other categories");
                    Capture("14-background-tab",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Close());break;
                case 21:
                    Capture("15-mixed-customization-game");break;
                case 22:
                    var liveBackdrop=GameObject.Find("ThemeBackground").GetComponent<ThemeBackdropGraphic>();
                    GameReworkChecks.Require(liveBackdrop.transform.parent.name=="MosaicBackdrop" && liveBackdrop.PreviewTheme<0,"Live background is covered by base gradient");
                    var expanded=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();
                    expanded.Open();expanded.SetCategory(ThemeCatalog.Part.Background);ThemeCatalog.Unlock(12,ThemeCatalog.Part.Background);expanded.Choose(12);expanded.ApplyPreview();
                    break;
                case 23:
                    Capture("16-detailed-backgrounds",()=>{var m=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();m.SetCategory(ThemeCatalog.Part.Casings);ThemeCatalog.Unlock(18);m.Choose(18);m.ApplyPreview();});break;
                case 24:
                    Capture("17-detailed-casings",()=>{var m=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();m.SetCategory(ThemeCatalog.Part.Conveyor);ThemeCatalog.Unlock(22);m.Choose(22);m.ApplyPreview();});break;
                case 25:
                    Capture("18-more-conveyors",()=>UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>().Close());break;
                case 26:
                    GameReworkChecks.Require(ThemeCatalog.Selected==22&&ThemeCatalog.SelectedCasings==18&&ThemeCatalog.SelectedBackground==12,"New theme selections not independent");
                    Capture("19-detailed-customization-game");break;
                case 27:
                    ThemeCatalog.Unlock(15,ThemeCatalog.Part.Background);ThemeCatalog.Select(15,ThemeCatalog.Part.Background);break;
                case 28:
                    Capture("20-harbor-game");break;
                case 29:
                    ThemeCatalog.Unlock(17,ThemeCatalog.Part.Background);ThemeCatalog.Select(17,ThemeCatalog.Part.Background);break;
                case 30:
                    Capture("21-observatory-game",()=>{ThemeCatalog.Select(0);manager.LoadLevel(14);});break;
                case 31:
                    GameReworkChecks.Require(GameObject.Find("InventoryRack")==null && GameObject.Find("ConveyorSupport")==null,"Removed stock backing or conveyor frame remains in gameplay");
                    Capture("22-classic-clean-layout",()=>
                    {
                        var db=(LevelDatabase)typeof(GameManager).GetField("levelDatabase",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
                        int loop=Enumerable.Range(0,db.LevelCount).First(i=>db.GetLevel(i).loopInventory);
                        GameProgressStore.SetInt("sand_progress_highest_unlocked",loop);ProgressSaveService.Load();manager.LoadLevel(loop);
                    });break;
                case 32:
                    var loopBelt=UnityEngine.Object.FindObjectOfType<InventoryLoopGraphic>();
                    GameReworkChecks.Require(loopBelt!=null && GameObject.Find("InventoryRack")==null && GameObject.Find("ConveyorSupport")==null,"Loop inventory missing or removed frames remain");
                    Capture("23-loop-clean-layout");break;
                case 33:
                    manager.LoadLevel(14);themeMenu.SetCategory(ThemeCatalog.Part.Background);themeMenu.Open();themeMenu.Choose(24);break;
                case 34:
                    GameReworkChecks.Require(GameObject.Find("Theme_24")!=null,"New nature background not visible in background tab");
                    var previewDeck=(ConveyorSurfaceGraphic)typeof(ThemeCustomizationMenu).GetField("largeDeck",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(themeMenu);
                    GameReworkChecks.Require(!previewDeck.gameObject.activeInHierarchy,"Belt covers background preview");
                    Capture("24-nature-background-cards",themeMenu.OpenBackgroundViewer);break;
                case 35:
                    var full=GameObject.Find("FullBackground").GetComponent<ThemeBackdropGraphic>();
                    GameReworkChecks.Require(full.FitWholeImage && full.PreviewTheme==24,"Large preview crops background or shows wrong texture");
                    Capture("25-full-forest-preview",()=>{GameObject.Find("CloseBackground").GetComponent<Button>().onClick.Invoke();themeMenu.Choose(30);});break;
                case 36:
                    Capture("26-winter-background-card",themeMenu.OpenBackgroundViewer);break;
                case 37:
                    Capture("27-full-winter-preview",()=>{themeMenu.Close();ThemeCatalog.Unlock(30,ThemeCatalog.Part.Background);ThemeCatalog.Select(30,ThemeCatalog.Part.Background);});break;
                case 38:
                    GameReworkChecks.Require(ThemeCatalog.SelectedBackground==30 && ThemeCatalog.Selected==0,"New winter background changed conveyor");
                    Capture("28-winter-nature-game",()=>{ThemeCatalog.Unlock(28,ThemeCatalog.Part.Background);ThemeCatalog.Select(28,ThemeCatalog.Part.Background);});break;
                case 39:
                    Capture("29-tropical-nature-game",()=>{themeMenu.SetCategory(ThemeCatalog.Part.Conveyor);themeMenu.Open();});break;
                case 40:
                    GameReworkChecks.Require(GameObject.Find("Theme_24")==null && GameObject.Find("Theme_23")!=null,"Background-only assets leaked into conveyor tab");themeMenu.Close();break;
                case 41:
                    themeMenu.SetCategory(ThemeCatalog.Part.Background);themeMenu.Open();themeMenu.Choose(31);
                    GameProgressStore.SetBool("sand_theme_background_owned_31",false);
                    CoinManager.instance.AddCoinsOnBuy(ThemeCatalog.CoinPrice(31));
                    int before=CoinManager.instance.Coins;themeMenu.ApplyPreview();
                    GameReworkChecks.Require(ThemeCatalog.SelectedBackground==31 && CoinManager.instance.Coins==before-ThemeCatalog.CoinPrice(31),"Coin nature background did not unlock for exact price");
                    themeMenu.ApplyPreview();GameReworkChecks.Require(CoinManager.instance.Coins==before-ThemeCatalog.CoinPrice(31),"Owned nature background charged twice");
                    Capture("30-coin-nature-backgrounds",()=>{themeMenu.Close();});break;
                case 42:
                    var backing=GameObject.Find("PaletteBacking").GetComponent<Image>();
                    GameReworkChecks.Require(backing.color.a>0 && backing.color.a<.5f && !backing.raycastTarget,"Palette backing opaque or intercepts taps");
                    Capture("31-coin-nature-game",()=>{themeMenu.SetCategory(ThemeCatalog.Part.Background);themeMenu.Open();themeMenu.Choose(8);});break;
                case 43:
                    GameProgressStore.SetBool("sand_theme_owned_8",false);GameProgressStore.SetBool("sand_theme_background_owned_8",false);GameProgressStore.SetBool("no_ads",false);
                    var bridge=typeof(GameSdkReadiness).GetField("CheckBridgeReady",BindingFlags.NonPublic|BindingFlags.Static);var previousBridge=bridge.GetValue(null);
                    try {bridge.SetValue(null,new Func<bool>(()=>false));themeMenu.ApplyPreview();}
                    finally {bridge.SetValue(null,previousBridge);}
                    GameReworkChecks.Require(!ThemeCatalog.IsUnlocked(8,ThemeCatalog.Part.Background),"Unavailable rewarded ad granted background");
                    Capture("32-rewarded-nature-backgrounds",()=>{ThemeCatalog.Unlock(35,ThemeCatalog.Part.Background);themeMenu.ApplyPreview();themeMenu.Close();});break;
                case 44:
                    Capture("33-rewarded-nature-game",()=>
                    {
                        foreach(int id in NatureBackgroundBundle.Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                        GameProgressStore.SetInt(NatureBackgroundBundle.LastShownKey,0);
                        GameProgressStore.SetInt("sand_progress_highest_unlocked",99);ProgressSaveService.Load();manager.LoadLevel(49);
                    });break;
                case 45:
                    typeof(GameManager).GetMethod("HandleLevelComplete",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,null);break;
                case 46:
                    offerCoinsBefore=CoinManager.instance.Coins;offerReward=(int)typeof(CoinManager).GetField("coinsOnComplete",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(CoinManager.instance);
                    var continueMethod=typeof(SandEconomyHud).GetMethod("OnCompleteContinue",BindingFlags.NonPublic|BindingFlags.Instance);
                    continueMethod.Invoke(SandEconomyHud.instance,null);continueMethod.Invoke(SandEconomyHud.instance,null);
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectOfType<NatureBackgroundOffer>().IsOpen && manager.CurrentLevelIndex==49,"50th-level completion did not open an unowned bundle before proceeding");
                    var offerViewport=GameObject.Find("NatureBackgroundViewport").GetComponent<RectTransform>();
                    var offerArt=offerViewport.GetComponentsInChildren<ThemeBackdropGraphic>();
                    GameReworkChecks.Require(offerArt.Length==5 && offerArt.All(art=>art.rectTransform.rect.width<offerViewport.rect.width*.8f && art.FitWholeImage),"Carousel must have narrower whole-portrait cards and two loop copies");
                    GameReworkChecks.Require(offerArt.Skip(1).Take(3).Select(art=>art.PreviewTheme).SequenceEqual(natureOffer.CurrentBundle.Backgrounds) && !natureOffer.CurrentBundle.IsOwned,"Offer art differs from purchased backgrounds");
                    GameReworkChecks.Require(offerArt[1].PreviewTheme==natureOffer.CurrentBundle.Backgrounds[0] && natureOffer.PreviewPage==0,"Selected bundle must start on its first background");
                    var offerDialog=GameObject.Find("NatureOfferDialog").GetComponent<RectTransform>();
                    var corners=new Vector3[4];offerDialog.GetWorldCorners(corners);
                    GameReworkChecks.Require(corners[0].y>0 && corners[2].y<Screen.height,"Promo dialog fills entire screen");
                    var leftCorners=new Vector3[4];var rightCorners=new Vector3[4];var viewportCorners=new Vector3[4];
                    offerArt[0].rectTransform.GetWorldCorners(leftCorners);offerArt[2].rectTransform.GetWorldCorners(rightCorners);offerViewport.GetWorldCorners(viewportCorners);
                    GameReworkChecks.Require(leftCorners[2].x>viewportCorners[0].x && rightCorners[0].x<viewportCorners[2].x,"Both neighboring images must peek into carousel viewport");
                    var savingsText=GameObject.Find("BundleSavings").GetComponent<TextMeshProUGUI>().text;
                    GameReworkChecks.Require(savingsText.Contains("<s>") && savingsText.Contains("%"),"SDK bundle savings or crossed-out individual total missing");
                    Capture("34-nature-bundle-offer");break;
                case 47:
                    SwipeNatureOffer(true);break;
                case 48:
                    var naturePager=UnityEngine.Object.FindObjectOfType<NatureOfferPager>();
                    GameReworkChecks.Require(natureOffer.PreviewPage==1 && Time.timeScale==0 && Mathf.Abs(naturePager.content.anchoredPosition.x-naturePager.TargetPosition)<1,$"Swipe did not snap to the second background while game paused: page {natureOffer.PreviewPage}, time {Time.timeScale}, position {naturePager.content.anchoredPosition.x}, target {naturePager.TargetPosition}");
                    Capture("35-nature-offer-forest-carousel",()=>SwipeNatureOffer(true));break;
                case 49:
                    GameReworkChecks.Require(natureOffer.PreviewPage==2,"Swipe did not select the third background");
                    Capture("36-nature-offer-autumn-carousel",()=>SwipeNatureOffer(true));break;
                case 50:
                    var offer=UnityEngine.Object.FindObjectOfType<NatureBackgroundOffer>();
                    GameReworkChecks.Require(offer.PreviewPage==0,"Last-page swipe did not loop back to the first background");
                    offer.ShowPage(2);
                    GameObject.Find("PreviousBackground").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(offer.PreviewPage==1,"Offer navigation arrow did not select previous page");
                    GameObject.Find("Page_0").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(offer.PreviewPage==0,"Page indicator did not select the first background");
                    offer.Continue();offer.Continue();
                    GameReworkChecks.Require(!offer.IsOpen && manager.CurrentLevelIndex==50 && CoinManager.instance.Coins==offerCoinsBefore+offerReward,"Closing offer failed to continue once or duplicated reward");
                    GameReworkChecks.Require(!NatureBackgroundBundle.ShouldOffer(50,false) && NatureBackgroundBundle.ShouldOffer(70,false),"Offer repeat schedule did not persist");break;
                case 51:
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();
                    GameObject.Find("ShopTab_1").GetComponent<Button>().onClick.Invoke();
                    shopTimeBeforeOffer=Time.timeScale;
                    GameReworkChecks.Require(SandEconomyHud.instance.IsShopOpen && manager.State!=GameState.LevelComplete,"Shop offer requires completed level");
                    GameReworkChecks.Require(GameObject.Find("NatureBundleShopCard").GetComponentsInChildren<ThemeBackdropGraphic>().Select(art=>art.PreviewTheme).SequenceEqual(NatureBackgroundBundle.Backgrounds),"Shop card previews differ from bundle");
                    Capture("37-nature-bundle-shop");break;
                case 52:
                    GameObject.Find("NatureBundleShopCard").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(natureOffer.IsOpen && natureOffer.PreviewPage==0 && GameProgressStore.GetInt(NatureBackgroundBundle.LastShownKey,0)==50,"Shop offer failed or consumed milestone cadence");break;
                case 53:
                    Capture("38-nature-offer-from-shop",natureOffer.Continue);break;
                case 54:
                    GameReworkChecks.Require(!natureOffer.IsOpen && SandEconomyHud.instance.IsShopOpen && Time.timeScale==shopTimeBeforeOffer && manager.CurrentLevelIndex==50 && CoinManager.instance.Coins==offerCoinsBefore+offerReward,"Closing shop offer changed level, reward or previous time scale");
                    NatureBackgroundBundle.Grant();break;
                case 55:
                    GameReworkChecks.Require(!GameObject.Find("NatureBundleShopCard").GetComponent<Button>().interactable && GameObject.Find("NatureBundleShopCard").GetComponentsInChildren<TextMeshProUGUI>().Any(label=>label.text==CampaignText.Get("purchased")),"Purchased shop bundle remains buyable");
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                case 56:
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();
                    GameObject.Find("ShopTab_1").GetComponent<Button>().onClick.Invoke();
                    foreach(int id in CosmeticBundleCatalog.All[1].Backgrounds)GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectsOfType<NatureBundleShopCard>(true).Length==6,"Shop does not contain all six bundles");
                    GameObject.Find("BundlesViewport").GetComponent<ScrollRect>().verticalNormalizedPosition=1;
                    Capture("39-shop-bundle-tab");break;
                case 57:
                    natureOffer.ShowFromShop(CosmeticBundleCatalog.All[1]);
                    GameReworkChecks.Require(GameObject.Find("NatureBackgroundPages").GetComponentsInChildren<ThemeBackdropGraphic>().Skip(1).Take(3).Select(art=>art.PreviewTheme).SequenceEqual(CosmeticBundleCatalog.All[1].Backgrounds),"Additional bundle carousel shows wrong contents");
                    Capture("40-botanical-bundle",natureOffer.Continue);break;
                case 58:
                    GameObject.Find("ShopTab_2").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(themeMenu.IsOpen && themeMenu.Category==ThemeCatalog.Part.Conveyor && GameObject.Find("ThemeButton")==null,"Conveyor customization is not embedded in shop or standalone button remains visible");
                    Capture("41-shop-conveyors");break;
                case 59:
                    GameObject.Find("ShopTab_3").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(themeMenu.IsOpen && themeMenu.Category==ThemeCatalog.Part.Casings,"Casings shop tab failed");Capture("42-shop-casings");break;
                case 60:
                    GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(themeMenu.IsOpen && themeMenu.Category==ThemeCatalog.Part.Background && GameObject.Find("Themes").GetComponentsInChildren<Button>().Length==ThemeCatalog.Count,"Background shop tab does not contain all backgrounds");
                    Capture("43-shop-backgrounds");break;
                case 61:
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();
                    GameReworkChecks.Require(!SandEconomyHud.instance.IsShopOpen && !themeMenu.IsOpen && Time.timeScale==1,"Closing integrated shop left customization visible or game paused");break;
                case 62:
                    foreach(int id in Enumerable.Range(4,20))GameProgressStore.SetBool("sand_theme_owned_"+id,true);
                    foreach(int id in Enumerable.Range(31,ThemeCatalog.Count-31))GameProgressStore.SetBool("sand_theme_background_owned_"+id,true);
                    GameProgressStore.SetBool("sand_theme_owned_4",false);GameProgressStore.SetBool("sand_theme_background_owned_31",false);
                    GameProgressStore.SetBool("sand_theme_conveyor_owned_4",false);GameProgressStore.SetBool("sand_theme_casings_owned_4",false);GameProgressStore.SetBool("sand_theme_background_owned_4",false);
                    if(CoinManager.instance.Coins>0)CoinManager.instance.TryToSpendCoins(CoinManager.instance.Coins);CoinManager.instance.AddCoinsOnBuy(1000);
                    UnityEngine.Object.FindObjectOfType<ShopGoldBadge>().RefreshCount();
                    GameReworkChecks.Require(UnityEngine.Object.FindObjectOfType<ShopGoldBadge>().AvailableCount==4 && GameObject.Find("ShopGoldCounter").GetComponent<Image>().color.r>.8f,"Affordable gold counter not visible on shop icon");
                    var badgeBounds=new Vector3[4];GameObject.Find("ShopGoldCounter").GetComponent<RectTransform>().GetWorldCorners(badgeBounds);
                    GameReworkChecks.Require(badgeBounds.All(point=>Screen.safeArea.Contains(point)),"Shop gold badge is clipped by safe area");
                    Capture("44-shop-gold-badge");break;
                case 63:
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();
                    Capture("46-resource-shop-replacement");
                    GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();
                    UnityEngine.Object.FindObjectOfType<CosmeticShopTabs>().RefreshGoldCounters();
                    GameReworkChecks.Require(GameObject.Find("TabGoldCounter_0").GetComponentInChildren<TextMeshProUGUI>().text=="1" && GameObject.Find("TabGoldCounter_1").GetComponentInChildren<TextMeshProUGUI>().text=="1" && GameObject.Find("TabGoldCounter_2").GetComponentInChildren<TextMeshProUGUI>().text=="2","Gold counters missing from relevant shop tabs");
                    GameReworkChecks.Require(!GameObject.Find("ThemeCard").GetComponent<Image>().enabled && !GameObject.Find("ThemeCard").transform.Find("Paper").gameObject.activeSelf,"Cosmetic shop still contains an inner dialog frame");
                    var expandedShop=GameObject.Find("ShopCard").GetComponent<RectTransform>();GameReworkChecks.Require(expandedShop.rect.height>1600 && expandedShop.rect.width>900,"Shop window did not expand");
                    themeMenu.Choose(31);Capture("45-expanded-shop-backgrounds");break;
                case 64:
                    var animatedCounter=GameObject.Find("TabGoldCounter_2").GetComponent<RectTransform>();
                    GameReworkChecks.Require(Time.timeScale==0 && !Mathf.Approximately(animatedCounter.localScale.x,1),"Counter animation stops when shop pauses game");
                    themeMenu.ApplyPreview();
                    GameReworkChecks.Require(ThemeCatalog.IsUnlocked(31,ThemeCatalog.Part.Background) && CoinManager.instance.Coins==1000-ThemeCatalog.CoinPrice(31,ThemeCatalog.Part.Background),"Integrated cosmetic gold purchase failed");break;
                case 65:
                    UnityEngine.Object.FindObjectOfType<ShopGoldBadge>().RefreshCount();UnityEngine.Object.FindObjectOfType<CosmeticShopTabs>().RefreshGoldCounters();
                    GameReworkChecks.Require(GameObject.Find("ShopGoldCounter")==null && GameObject.Find("TabGoldCounter_0")==null && GameObject.Find("TabGoldCounter_2")==null,"Counters remain visible with no affordable purchases");
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                case 66:
                    foreach(int id in Enumerable.Range(8,4)){GameProgressStore.SetBool("sand_theme_owned_"+id,false);GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);}
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();themeMenu.Choose(8);
                    GameReworkChecks.Require(Enumerable.Range(8,4).All(id=>GameObject.Find("Theme_"+id).transform.Find("Cost").GetComponent<TextMeshProUGUI>().text==CampaignText.Get("watch_ad")),"Classic background cards still show cash price instead of rewarded ad");
                    Capture("47-rewarded-classic-backgrounds");break;
                case 67:
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                case 68:
                    foreach(int id in Enumerable.Range(38,4))GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();themeMenu.Choose(38);
                    Capture("48-minimal-gold-backgrounds");break;
                case 69:
                    themeMenu.Choose(40);Capture("49-minimal-blue-peach");break;
                case 70:
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                case 71:
                    foreach(int id in Enumerable.Range(42,8))GameProgressStore.SetBool("sand_theme_background_owned_"+id,false);
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();themeMenu.Choose(42);
                    Capture("50-extra-minimal-gold");break;
                case 72:
                    themeMenu.Choose(44);Capture("51-extra-bamboo-lavender");break;
                case 73:
                    themeMenu.Choose(46);
                    GameReworkChecks.Require(Enumerable.Range(46,4).All(id=>GameObject.Find("Theme_"+id).transform.Find("Cost").GetComponent<TextMeshProUGUI>().text==CampaignText.Get("watch_ad")),"Four extra minimal backgrounds do not show rewarded unlock");
                    Capture("52-extra-minimal-rewarded");break;
                case 74:
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                case 75:
                    GameObject.Find("ShopBtn").GetComponent<Button>().onClick.Invoke();GameObject.Find("ShopTab_4").GetComponent<Button>().onClick.Invoke();themeMenu.Choose(4);
                    var orderedTiles=ThemeCatalog.DisplayOrderFor(ThemeCatalog.Part.Background).Select(id=>GameObject.Find("Theme_"+id).GetComponent<RectTransform>()).ToArray();
                    for(int i=1;i<orderedTiles.Length;i++)GameReworkChecks.Require(i%2==1?Mathf.Approximately(orderedTiles[i].anchoredPosition.y,orderedTiles[i-1].anchoredPosition.y):orderedTiles[i].anchoredPosition.y<orderedTiles[i-1].anchoredPosition.y,"Shop grid contains blank cells between purchase types");
                    Capture("53-replaced-classic-gold-backgrounds");break;
                case 76:
                    themeMenu.Choose(8);Capture("54-replaced-classic-rewarded");break;
                case 77:
                    GameObject.Find("CloseShop").GetComponent<Button>().onClick.Invoke();break;
                default:
                    GameReworkChecks.Require(errors.Count==0,"Runtime errors: "+string.Join("\n",errors));
                    File.WriteAllText("tasks/game-rework-visual-checks.txt","PASS: portrait gameplay captures; left-hand color squares; selected color fades other painting colors and unrelated buckets; second-level interactive filter tutorial; 24 conveyor/casing variants and 50 backgrounds in three independent customization tabs; animated belt patterns; exclusive IAP themes cannot unlock for coins; coin purchase debits once; readable locked paintings; separate ordinary/winter collections; seasonal completion preserves main save; selected winter theme; live background layering and rendered-pixel change verified; 46 distinct illustrated backgrounds across IAP/coin/rewarded tiers; exact coin debit and unavailable rewarded-ad denial; translucent palette backing; compact looping water-first portrait carousel with visible neighbors and SDK-based bundle savings; expanded shop without nested cosmetic frame; animated red affordable-gold counters on shop icon and relevant tabs; counters clear after purchase; five integrated shop tabs, all cosmetic variants, six disjoint three-background cash bundles; persistent shop entry opens without consuming milestone cadence, returns to shop with prior time scale without changing level/reward, purchased entry disabled; drag gestures snap with gameplay paused; arrow and indicator navigation; random unowned milestone bundle at level 50, persistent 20-level cadence, dismiss resumes once with one completion reward; full portrait thumbnails and enlarged previews; live background texture changes verified; stock backing and conveyor frame removed; loop inventory preserved. No runtime errors.");
                    SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;
            }
            next=EditorApplication.timeSinceStartup+2.5;
        }
        catch(Exception error)
        {
            File.WriteAllText("tasks/game-rework-visual-checks.txt",error.ToString());SessionState.SetBool(Key,false);
            EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(1);
        }
    }
    private static void SwipeNatureOffer(bool left)
    {
        var pager=UnityEngine.Object.FindObjectOfType<NatureOfferPager>();
        var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        data.pointerPressRaycast=new RaycastResult{module=pager.GetComponentInParent<GraphicRaycaster>()};
        data.position=new Vector2(Screen.width*(left?.75f:.25f),Screen.height*.5f);data.pressPosition=data.position;
        ExecuteEvents.Execute(pager.gameObject,data,ExecuteEvents.beginDragHandler);
        data.position=new Vector2(Screen.width*(left?.25f:.75f),Screen.height*.5f);
        ExecuteEvents.Execute(pager.gameObject,data,ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(pager.gameObject,data,ExecuteEvents.endDragHandler);
    }
    private static void Capture(string name,Action action=null)
    {
        var camera=Camera.main;
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();
        var modes=canvases.Select(c=>c.renderMode).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();
        var target=new RenderTexture(540,960,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var texture=new Texture2D(540,960,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;
            foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100; }
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,540,960),0,0);texture.Apply();

            if(name=="20-harbor-game")
            {
                var previousBackground=new Texture2D(2,2);
                try
                {
                    previousBackground.LoadImage(File.ReadAllBytes(Folder+"/19-detailed-customization-game.png"));
                    int changed=0;
                    for(int y=200;y<850;y++)
                    {
                        Color a=previousBackground.GetPixel(3,y),b=texture.GetPixel(3,y);
                        if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.04f)changed++;
                    }
                    GameReworkChecks.Require(changed>100,"Background selection did not change visible gameplay pixels");
                }
                finally { UnityEngine.Object.DestroyImmediate(previousBackground); }
            }
            if(name=="16-detailed-backgrounds")
            {
                var menu=UnityEngine.Object.FindObjectOfType<ThemeCustomizationMenu>();
                var viewport=(RectTransform)GameObject.Find("ThemeCard").transform.Find("Viewport");
                var selected=(RectTransform)viewport.Find("Themes/Theme_12");
                var corners=new Vector3[4];selected.GetWorldCorners(corners);
                GameReworkChecks.Require(corners.All(c=>viewport.rect.Contains(viewport.InverseTransformPoint(c))),"Selected detailed background card is clipped");
            }
            File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=active;
            for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i]; }
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
        }
        afterCapture=action;
    }
    private static int FirstWinter(GameManager manager)
    {
        for(int i=0;i<manager.LevelCount;i++) if(manager.IsSeasonal(i)) return i;
        throw new Exception("Winter campaign missing");
    }
}
