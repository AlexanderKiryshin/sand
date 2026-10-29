using System;
using TMPro;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using UnityEngine;
using UnityEngine.UI;

public sealed class ThemeCustomizationMenu : MonoBehaviour
{
    private Canvas canvas;
    private bool embedded;
    public bool IsPending => pending;
    private RectTransform opener, overlay, card;
    private readonly Button[] choices = new Button[ThemeCatalog.Count];
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[ThemeCatalog.Count], costs = new TextMeshProUGUI[ThemeCatalog.Count];
    private TextMeshProUGUI title, subtitle, selectedName, balance, actionLabel, closeLabel;
    private ThemeBackdropGraphic largeBackground;
    private ConveyorSurfaceGraphic largeDeck;
    private ConveyorHoodGraphic[] largeHoods;
    private readonly ThemeBackdropGraphic[] thumbnailBackgrounds = new ThemeBackdropGraphic[ThemeCatalog.Count];
    private readonly ConveyorSurfaceGraphic[] thumbnailDecks = new ConveyorSurfaceGraphic[ThemeCatalog.Count];
    private readonly ConveyorHoodGraphic[][] thumbnailHoods = new ConveyorHoodGraphic[ThemeCatalog.Count][];
    private readonly Image[] tabFaces = new Image[3];
    private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
    public ThemeCatalog.Part Category { get; private set; }
    private RectTransform backgroundViewer;
    private ThemeBackdropGraphic fullBackground;
    private TextMeshProUGUI fullTitle, fullCloseLabel, enlargeLabel;
    private Button enlarge;
    private float RowHeight => Category == ThemeCatalog.Part.Background ? (embedded?440f:342f) : (embedded?230f:194f);
    private float previewTravel;
    private Button action;
    private ScrollRect themeScroll;
    private int preview;
    private int[] displayOrder,displaySlots;
    private bool pending, restored;
    private float previousTime, readinessRefresh;
    private LocalizationManager localization;
    public bool IsOpen => embedded ? card!=null && card.gameObject.activeInHierarchy : overlay!=null && overlay.gameObject.activeSelf;
    public int PreviewTheme => preview;
    private static readonly Color Ink = new Color(.10f,.27f,.29f), Cream = new Color(.97f,.96f,.88f);
    private void Start()
    {
        PrepareFont(TMP_Settings.defaultFontAsset, new System.Collections.Generic.HashSet<TMP_FontAsset>());
        canvas = GetComponent<Canvas>(); if (canvas == null) return;
        opener = Rect("ThemeButton",transform,new Vector2(72,72),Vector2.zero);
        opener.anchorMin = opener.anchorMax = opener.pivot = new Vector2(0,1);
        Face(opener,Ink); opener.gameObject.AddComponent<Button>().onClick.AddListener(Open);
        var symbol = Rect("ThemeIcon",opener,new Vector2(43,43),Vector2.zero).gameObject.AddComponent<Image>();
        symbol.sprite = GameUiSymbols.Get(GameUiSymbol.Bundle); symbol.raycastTarget = false;
        overlay = Rect("ThemeMenu",transform,Vector2.zero,Vector2.zero); Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color(.03f,.10f,.14f,.83f);
        card = Rect("ThemeCard",overlay,new Vector2(700,1100),Vector2.zero); Face(card,Ink);
        Face(Rect("Paper",card,new Vector2(688,1088),Vector2.zero),Cream);
        title = Label(card,"Title","",new Vector2(0,500),new Vector2(640,54),38);
        for(int i=0;i<3;i++)
        {
            var category=(ThemeCatalog.Part)i;
            var tab=Rect(i==0?"ConveyorTab":i==1?"CasingsTab":"BackgroundTab",card,new Vector2(202,54),new Vector2((i-1)*214,444));
            Face(tab,Ink);tabFaces[i]=tab.GetComponent<Image>();
            tab.gameObject.AddComponent<Button>().onClick.AddListener(()=>SetCategory(category));
            tabLabels[i]=Label(tab,"Label","",Vector2.zero,new Vector2(196,50),24);
        }
        var previewBox = Rect("Preview",card,new Vector2(630,200),new Vector2(0,308));
        BuildPreview(previewBox,0,out largeBackground,out largeDeck,out largeHoods);
        var enlargeRect=Rect("EnlargeBackground",previewBox,new Vector2(180,64),new Vector2(210,0));
        Face(enlargeRect,Ink);enlarge=enlargeRect.gameObject.AddComponent<Button>();enlarge.onClick.AddListener(OpenBackgroundViewer);
        enlargeLabel=Label(enlargeRect,"Label","",Vector2.zero,new Vector2(174,60),24);enlargeLabel.color=Color.white;
        selectedName = Label(card,"SelectedName","",new Vector2(0,184),new Vector2(630,45),29);
        subtitle = Label(card,"PreviewDescription","",new Vector2(0,139),new Vector2(630,38),22);
        var viewport = Rect("Viewport",card,new Vector2(640,420),new Vector2(0,-98));
        viewport.gameObject.AddComponent<Image>().color = new Color(.87f,.89f,.82f); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Themes",viewport,new Vector2(640,((ThemeCatalog.Count+1)/2)*194+12),Vector2.zero);
        content.anchorMin = content.anchorMax = new Vector2(.5f,1); content.pivot = new Vector2(.5f,1);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); themeScroll=scroll; scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        for (int i=0;i<ThemeCatalog.Count;i++)
        {
            int index=i;
            var tile = Rect("Theme_"+i,content,new Vector2(306,182),new Vector2(i%2==0?-160:160,-103-(i/2)*194));
            tile.anchorMin=tile.anchorMax=new Vector2(.5f,1);
            Face(tile,new Color(.92f,.94f,.87f)); choices[i]=tile.gameObject.AddComponent<Button>(); choices[i].transition=Selectable.Transition.None;
            choices[i].onClick.AddListener(()=>Choose(index));
            var small = Rect("Preview",tile,new Vector2(286,103),new Vector2(0,27));
            BuildPreview(small,i,out thumbnailBackgrounds[i],out thumbnailDecks[i],out thumbnailHoods[i]);
            names[i] = Label(tile,"Name","",new Vector2(0,-42),new Vector2(296,34),23);
            costs[i] = Label(tile,"Cost","",new Vector2(0,-71),new Vector2(296,28),18);
        }
        balance = Label(card,"Balance","",new Vector2(0,-342),new Vector2(640,35),22);
        var apply = Rect("Apply",card,new Vector2(620,76),new Vector2(0,-402)); Face(apply,Ink);
        action = apply.gameObject.AddComponent<Button>(); action.onClick.AddListener(ApplyPreview);
        actionLabel = Label(apply,"Label","",Vector2.zero,new Vector2(600,72),27); actionLabel.color=Color.white;
        var close = Rect("Close",card,new Vector2(320,64),new Vector2(0,-490)); Face(close,new Color(.79f,.85f,.77f));
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close); closeLabel=Label(close,"Label","",Vector2.zero,new Vector2(310,60),27);
        BuildBackgroundViewer();
        overlay.gameObject.SetActive(false);
        localization=LocalizationManager.Instance; if(localization!=null)localization.onLanguageChange+=Refresh;
        Choose(ThemeCatalog.SelectedFor(Category));
        opener.gameObject.SetActive(false);
    }
    public bool EmbedInShop(RectTransform parent)
    {
        if(card==null)return false;
        if(!embedded)
        {
            embedded=true;card.SetParent(parent,false);card.anchoredPosition=new Vector2(0,-100);
            card.GetComponent<Image>().enabled=false;card.Find("Paper").gameObject.SetActive(false);
            title.gameObject.SetActive(false);subtitle.gameObject.SetActive(false);
            LayoutEmbedded(parent);
            backgroundViewer.SetParent(parent,false);Stretch(backgroundViewer);
            foreach(var face in tabFaces)face.gameObject.SetActive(false);
            closeLabel.transform.parent.gameObject.SetActive(false);overlay.gameObject.SetActive(false);
        }
        return true;
    }
    private void LayoutEmbedded(RectTransform parent)
    {
        float width=parent.rect.width-64,height=parent.rect.height-330;
        card.sizeDelta=new Vector2(width,height);card.localScale=Vector3.one;
        var previewRect=largeBackground.rectTransform;previewRect.sizeDelta=new Vector2(width,260);previewRect.anchoredPosition=new Vector2(0,height*.5f-140);
        previewRect.Find("Scene").localScale=Vector3.one*Mathf.Min(width/600,260/230f);
        ((RectTransform)enlarge.transform).anchoredPosition=new Vector2(width*.5f-110,0);
        selectedName.rectTransform.anchoredPosition=new Vector2(0,height*.5f-300);selectedName.rectTransform.sizeDelta=new Vector2(width,48);
        themeScroll.viewport.sizeDelta=new Vector2(width,height-520);themeScroll.viewport.anchoredPosition=new Vector2(0,-100);
        balance.rectTransform.anchoredPosition=new Vector2(0,-height*.5f+115);balance.rectTransform.sizeDelta=new Vector2(width,35);
        var apply=(RectTransform)action.transform;apply.anchoredPosition=new Vector2(0,-height*.5f+56);apply.sizeDelta=new Vector2(width,76);actionLabel.rectTransform.sizeDelta=new Vector2(width-20,72);
    }
    public void ShowEmbedded(ThemeCatalog.Part part) {card.gameObject.SetActive(true);SetCategory(part);}
    public void HideEmbedded() {if(embedded){backgroundViewer.gameObject.SetActive(false);card.gameObject.SetActive(false);}}
    private void BuildBackgroundViewer()
    {
        backgroundViewer=Rect("BackgroundViewer",overlay,Vector2.zero,Vector2.zero);Stretch(backgroundViewer);
        backgroundViewer.gameObject.AddComponent<Image>().color=new Color(.03f,.10f,.14f,.94f);
        var paper=Rect("BackgroundPaper",backgroundViewer,new Vector2(700,1240),Vector2.zero);Face(paper,Cream);
        fullTitle=Label(paper,"Title","",new Vector2(0,570),new Vector2(650,55),34);
        var art=Rect("FullBackground",paper,new Vector2(576,1024),new Vector2(0,10));
        fullBackground=art.gameObject.AddComponent<ThemeBackdropGraphic>();fullBackground.FitWholeImage=true;fullBackground.raycastTarget=false;
        var close=Rect("CloseBackground",paper,new Vector2(320,64),new Vector2(0,-555));Face(close,Ink);
        close.gameObject.AddComponent<Button>().onClick.AddListener(()=>backgroundViewer.gameObject.SetActive(false));
        fullCloseLabel=Label(close,"Label","",Vector2.zero,new Vector2(310,60),27);fullCloseLabel.color=Color.white;
        backgroundViewer.gameObject.SetActive(false);
    }
    public void OpenBackgroundViewer()
    {
        if(Category!=ThemeCatalog.Part.Background || !IsOpen)return;
        fullBackground.PreviewTheme=preview;fullTitle.text=CampaignText.Get(ThemeCatalog.NameKey(preview,Category));fullCloseLabel.text=CampaignText.Get("close");
        backgroundViewer.SetAsLastSibling();backgroundViewer.gameObject.SetActive(true);
    }
    private void LayoutChoices()
    {
        bool backgrounds=Category==ThemeCatalog.Part.Background;
        displayOrder=ThemeCatalog.DisplayOrderFor(Category);
        displaySlots=new int[ThemeCatalog.Count];int slot=0;
        foreach(int id in displayOrder)
        {
            displaySlots[id]=slot++;
        }
        float width=themeScroll.viewport.rect.width,tileWidth=width*.5f-14,tileHeight=RowHeight-12;
        float previewHeight=backgrounds?(embedded?340:240):(embedded?134:103);
        themeScroll.content.sizeDelta=new Vector2(width,((slot+1)/2)*RowHeight+12);
        for(int i=0;i<choices.Length;i++)
        {
            bool available=ThemeCatalog.IsAvailable(i,Category);
            choices[i].gameObject.SetActive(available);if(!available)continue;
            int position=displaySlots[i];
            var tile=(RectTransform)choices[i].transform;
            tile.sizeDelta=new Vector2(tileWidth,tileHeight);
            tile.anchoredPosition=new Vector2(position%2==0?-width*.25f:width*.25f,-tileHeight*.5f-12-(position/2)*RowHeight);
            var thumbnail=thumbnailBackgrounds[i].rectTransform;
            thumbnail.sizeDelta=new Vector2(tileWidth-20,previewHeight);thumbnail.anchoredPosition=new Vector2(0,backgrounds?32:27);
            thumbnail.Find("Scene").localScale=Vector3.one*Mathf.Min((tileWidth-20)/600,previewHeight/230);
            thumbnailBackgrounds[i].FitWholeImage=backgrounds;
            names[i].rectTransform.anchoredPosition=new Vector2(0,-tileHeight*.5f+57);names[i].rectTransform.sizeDelta=new Vector2(tileWidth-10,34);
            costs[i].rectTransform.anchoredPosition=new Vector2(0,-tileHeight*.5f+25);costs[i].rectTransform.sizeDelta=new Vector2(tileWidth-10,28);
        }
        largeBackground.FitWholeImage=backgrounds;
        largeDeck.gameObject.SetActive(!backgrounds);
        enlarge.gameObject.SetActive(backgrounds);
    }
    private static void BuildPreview(RectTransform root,int theme,out ThemeBackdropGraphic background,out ConveyorSurfaceGraphic deck,out ConveyorHoodGraphic[] hoods)
    {
        background=root.gameObject.AddComponent<ThemeBackdropGraphic>(); background.PreviewTheme=theme; background.raycastTarget=false;
        var scene=Rect("Scene",root,new Vector2(600,230),Vector2.zero); scene.localScale=Vector3.one*Mathf.Min(root.rect.width/600,root.rect.height/230);
        var belt=Rect("Conveyor",scene,new Vector2(460,120),new Vector2(0,-36));
        deck=belt.gameObject.AddComponent<ConveyorSurfaceGraphic>();deck.PreviewTheme=theme;deck.raycastTarget=false;
        hoods=new ConveyorHoodGraphic[2];
        for(int i=0;i<2;i++) { var hood=Rect("Casings_"+i,belt,new Vector2(460,120),Vector2.zero);hoods[i]=hood.gameObject.AddComponent<ConveyorHoodGraphic>();hoods[i].PreviewTheme=theme;hoods[i].Initialize(deck,i==0); }
    }
    private void Update()
    {
        if(opener==null)return;
        float scale=Mathf.Max(.001f,canvas.scaleFactor);Rect safe=Screen.safeArea;
        opener.anchoredPosition=new Vector2(safe.xMin/scale+104,-(Screen.height-safe.yMax)/scale-104);
        if(!embedded)card.localScale=Vector3.one*Mathf.Max(.1f,Mathf.Min(1,(safe.width/scale-40)/700,(safe.height/scale-40)/1100));
        if(YoutubePlatform.PurchasesAllowed && !restored && GameSdkReadiness.IsReady)
        {
            restored=true;
            try { MirraSDK.Payments.RestorePurchases(data=> {
                if(data==null)return;
                ThemeCatalog.RestorePurchases((tag,grant)=>data.RestoreProduct(tag,grant));
                Refresh();
            }); }
            catch(Exception e) { Debug.LogWarning("Theme restore unavailable: "+e.Message); }
        }
        if(IsOpen)
        {
            previewTravel+=Time.unscaledDeltaTime*100f;
            largeDeck.SetTravel(previewTravel);
        }
        if(backgroundViewer.gameObject.activeSelf)
        {
            var paper=(RectTransform)backgroundViewer.GetChild(0);
            paper.localScale=Vector3.one*Mathf.Max(.1f,Mathf.Min(1,(safe.width/scale-40)/700,(safe.height/scale-40)/1240));
        }
        if(IsOpen && Input.GetKeyDown(KeyCode.Escape)) {if(backgroundViewer.gameObject.activeSelf)backgroundViewer.gameObject.SetActive(false);else Close();}
        if(IsOpen && Time.unscaledTime>=readinessRefresh) {readinessRefresh=Time.unscaledTime+.5f;Refresh();}
    }
    public void Open() {var shop=FindObjectOfType<CosmeticShopTabs>(true);if(shop!=null){shop.OpenCategory(Category);return;}if(overlay==null||IsOpen)return;previousTime=Time.timeScale;Time.timeScale=0;overlay.SetAsLastSibling();overlay.gameObject.SetActive(true);Choose(ThemeCatalog.SelectedFor(Category));}
    public void Close() {if(embedded){FindObjectOfType<RewardedShop>()?.CloseShop();return;}if(!IsOpen)return;backgroundViewer.gameObject.SetActive(false);overlay.gameObject.SetActive(false);if(Mathf.Approximately(Time.timeScale,0))Time.timeScale=previousTime;}
    public void SetCategory(ThemeCatalog.Part part)
    {
        if(pending)return;
        Category=part;
        Choose(ThemeCatalog.SelectedFor(part));themeScroll.StopMovement();themeScroll.verticalNormalizedPosition=1;
    }
    public void Choose(int theme)
    {
        if(pending||!ThemeCatalog.IsAvailable(theme,Category))return;
        LayoutChoices();
        preview=theme;
        Canvas.ForceUpdateCanvases();
        float scrollable=Mathf.Max(1,themeScroll.content.rect.height-themeScroll.viewport.rect.height);
        themeScroll.StopMovement();
        themeScroll.verticalNormalizedPosition=1-Mathf.Clamp01((displaySlots[theme]/2*RowHeight)/scrollable);
        Canvas.ForceUpdateCanvases();
        var corners=new Vector3[4];((RectTransform)choices[theme].transform).GetWorldCorners(corners);
        float selectedTop=themeScroll.viewport.InverseTransformPoint(corners[1]).y;
        float clipped=selectedTop-(themeScroll.viewport.rect.yMax-12);
        if(clipped>0)themeScroll.verticalNormalizedPosition=Mathf.Clamp01(themeScroll.verticalNormalizedPosition+clipped/scrollable);

        largeBackground.PreviewTheme=Category==ThemeCatalog.Part.Background?theme:ThemeCatalog.SelectedBackground;largeDeck.PreviewTheme=Category==ThemeCatalog.Part.Conveyor?theme:ThemeCatalog.Selected;
        largeBackground.SetVerticesDirty();largeDeck.SetVerticesDirty();foreach(var hood in largeHoods){hood.PreviewTheme=Category==ThemeCatalog.Part.Casings?theme:ThemeCatalog.SelectedCasings;hood.SetVerticesDirty();}Refresh();
    }
    public void ApplyPreview()
    {
        if(pending)return;int theme=preview;var part=Category;
        if(ThemeCatalog.IsUnlocked(theme,part)) {ThemeCatalog.Select(theme,part);Refresh();return;}
        switch(ThemeCatalog.AccessFor(theme,part))
        {
            case ThemeCatalog.Access.Coins:
                if(CoinManager.instance!=null&&CoinManager.instance.TryToSpendCoins(ThemeCatalog.CoinPrice(theme,part))) {ThemeCatalog.Acquire(theme,part,"coins");} Refresh();break;
            case ThemeCatalog.Access.Rewarded:
                if(AdManager.instance==null||!AdManager.instance.CanStartRewarded())return;
                pending=true;Refresh();AdManager.instance.StartRewarded(ProgressSaveService.CurrentLevelIndex.ToString(),"cosmetic_theme_"+theme,()=> {ThemeCatalog.Acquire(theme,part,GameProgressStore.GetBool("no_ads")?"no_ads_bonus":"rewarded");Finish();},Finish);break;
            case ThemeCatalog.Access.Money:
                if(!TryPrice(theme,part,out _))return;
                pending=true;Refresh();bool settled=false;
                try { MirraSDK.Payments.Purchase(productTag:ThemeCatalog.ProductTag(theme,part),onSuccess:()=> {if(settled)return;settled=true;ThemeCatalog.Acquire(theme,part,"purchase");Analytic.BuyInApp(ProgressSaveService.CurrentLevelIndex.ToString(),ThemeCatalog.ProductTag(theme,part));Finish();},onError:()=> {if(settled)return;settled=true;Finish();}); }
                catch(Exception e) {Debug.LogWarning("Theme purchase unavailable: "+e.Message);Finish();}break;
        }
    }
    private void Finish() {pending=false;if(IsOpen)Time.timeScale=0;Refresh();}
    private static bool TryPrice(int theme,ThemeCatalog.Part part,out string price)
    {
        price=CampaignText.Get("unavailable");if(!GameSdkReadiness.IsReady)return false;
        try {ProductData data=MirraSDK.Payments.GetProductData(ThemeCatalog.ProductTag(theme,part));if(data==null||data.Tag!=ThemeCatalog.ProductTag(theme,part))return false;price=data.GetFullPriceFloat();return !string.IsNullOrEmpty(price);}
        catch(Exception) {return false;}
    }
    private string Cost(int index)
    {
        if(ThemeCatalog.SelectedFor(Category)==index)return CampaignText.Get("theme_selected");
        if(ThemeCatalog.AccessFor(index,Category)==ThemeCatalog.Access.Free)return CampaignText.Get("theme_free");
        if(ThemeCatalog.IsUnlocked(index,Category))return CampaignText.Get("purchased");
        switch(ThemeCatalog.AccessFor(index,Category))
        {case ThemeCatalog.Access.Coins:return string.Format(CampaignText.Get("theme_coins"),ThemeCatalog.CoinPrice(index,Category));case ThemeCatalog.Access.Money:TryPrice(index,Category,out string price);return price;case ThemeCatalog.Access.Rewarded:return CampaignText.Get("watch_ad");default:return CampaignText.Get("theme_free");}
    }
    private void Refresh()
    {
        if(card==null)return;
        enlargeLabel.text=CampaignText.Get("theme_enlarge");
        title.text=CampaignText.Get(embedded?(Category==ThemeCatalog.Part.Conveyor?"theme_conveyor_tab":Category==ThemeCatalog.Part.Casings?"theme_casings_tab":"theme_background_tab"):"themes");subtitle.text=CampaignText.Get(Category==ThemeCatalog.Part.Conveyor?"theme_conveyor_tab":Category==ThemeCatalog.Part.Casings?"theme_casings_tab":"theme_background_tab");closeLabel.text=CampaignText.Get("close");
        selectedName.text=CampaignText.Get(ThemeCatalog.NameKey(preview,Category));balance.text=string.Format(CampaignText.Get("theme_balance"),CoinManager.instance!=null?CoinManager.instance.Coins:0);
        for(int i=0;i<3;i++)
        {
            tabLabels[i].text=CampaignText.Get(i==0?"theme_conveyor_tab":i==1?"theme_casings_tab":"theme_background_tab");
            tabFaces[i].color=(int)Category==i?Ink:new Color(.79f,.85f,.77f);
            tabLabels[i].color=(int)Category==i?Color.white:Ink;
        }
        for(int i=0;i<choices.Length;i++)
        {
            int background=Category==ThemeCatalog.Part.Background?i:ThemeCatalog.SelectedBackground;
            if(thumbnailBackgrounds[i].PreviewTheme!=background){thumbnailBackgrounds[i].PreviewTheme=background;thumbnailBackgrounds[i].SetVerticesDirty();}
            int conveyor=Category==ThemeCatalog.Part.Conveyor?i:ThemeCatalog.Selected;
            if(thumbnailDecks[i].PreviewTheme!=conveyor){thumbnailDecks[i].PreviewTheme=conveyor;thumbnailDecks[i].SetVerticesDirty();}
            int casing=Category==ThemeCatalog.Part.Casings?i:ThemeCatalog.SelectedCasings;
            foreach(var hood in thumbnailHoods[i])if(hood.PreviewTheme!=casing){hood.PreviewTheme=casing;hood.SetVerticesDirty();}
            thumbnailDecks[i].gameObject.SetActive(Category!=ThemeCatalog.Part.Background);
        }
        for(int i=0;i<choices.Length;i++) {names[i].text=CampaignText.Get(ThemeCatalog.NameKey(i,Category));costs[i].text=Cost(i);choices[i].GetComponent<Image>().color=i==preview?new Color(.69f,.81f,.71f):new Color(.92f,.94f,.87f);choices[i].interactable=!pending;}
        bool owned=ThemeCatalog.IsUnlocked(preview,Category),available=owned;
        string label=ThemeCatalog.SelectedFor(Category)==preview?CampaignText.Get("theme_selected"):CampaignText.Get("theme_use");
        if(!owned) switch(ThemeCatalog.AccessFor(preview,Category))
        {
            case ThemeCatalog.Access.Coins:available=CoinManager.instance!=null&&CoinManager.instance.Coins>=ThemeCatalog.CoinPrice(preview,Category);label=available?Cost(preview):CampaignText.Get("theme_no_coins");break;
            case ThemeCatalog.Access.Rewarded:available=AdManager.instance!=null&&AdManager.instance.CanStartRewarded();label=CampaignText.Get("theme_unlock");break;
            case ThemeCatalog.Access.Money:available=TryPrice(preview,Category,out label);break;
        }
        actionLabel.text=pending?CampaignText.Get("ad_wait"):label;action.interactable=!pending&&available&&ThemeCatalog.SelectedFor(Category)!=preview;
    }
    private void OnDestroy() {Close();if(localization!=null)localization.onLanguageChange-=Refresh;}
    private static void PrepareFont(TMP_FontAsset font, System.Collections.Generic.HashSet<TMP_FontAsset> visited)
    {
        if(font==null||!visited.Add(font))return;
        // TMP 3.0 needs its primary atlas reference initialized before a dynamic fallback grows.
        var atlas=font.atlasTexture;
        if(font.fallbackFontAssetTable!=null)foreach(var fallback in font.fallbackFontAssetTable)PrepareFont(fallback,visited);
    }
    private static void Face(RectTransform rect,Color color) {var image=rect.gameObject.AddComponent<Image>();image.sprite=HintUiSprites.RoundedPanel;image.type=Image.Type.Sliced;image.color=color;}
    private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position) {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;}
    private static void Stretch(RectTransform rect) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float font)
    {var label=Rect(name,parent,size,position).gameObject.AddComponent<TextMeshProUGUI>();label.text=text;label.fontSize=font;label.enableAutoSizing=true;label.fontSizeMin=font*.7f;label.fontSizeMax=font;label.alignment=TextAlignmentOptions.Center;label.color=Ink;label.raycastTarget=false;return label;}
}
