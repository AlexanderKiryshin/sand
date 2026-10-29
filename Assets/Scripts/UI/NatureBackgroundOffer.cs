using System;
using System.Linq;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A dismissible nature collection replaces milestone interstitials.</summary>
public sealed class NatureBackgroundOffer : MonoBehaviour
{
    private Canvas canvas;
    private CosmeticBundle bundle=CosmeticBundleCatalog.Nature;
    private RectTransform overlay,dialog,header,footer;
    private NatureOfferPager pager;
    private CosmeticBundlePreview completeStylePreview;
    private Button previous,next;
    private TextMeshProUGUI selectedName,swipeHint,savingsLabel;
    private readonly Image[] pageDots=new Image[NatureBackgroundBundle.Backgrounds.Length];
    public CosmeticBundle CurrentBundle => bundle;
    public int PreviewPage => !bundle.IsStyleBundle && pager!=null?pager.Page:0;
    public void ShowPage(int page) { if(IsOpen && !pending && !bundle.IsStyleBundle)pager.ShowPage(page); }
    private TextMeshProUGUI title,description,buyLabel,continueLabel;
    private Button buy,continueButton;
    private Action onContinue;
    private bool pending,restored,purchaseFailed;
    private float previousTime,nextRefresh;
    private static readonly Color Ink=new Color(.10f,.27f,.29f),Cream=new Color(.97f,.96f,.88f);
    public bool IsOpen => overlay!=null && overlay.gameObject.activeSelf;
    private void Start()
    {
        canvas=GetComponent<Canvas>();if(canvas==null)return;
        overlay=Rect("NatureBundleOffer",transform,Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color=new Color(Ink.r,Ink.g,Ink.b,.72f);
        dialog=Rect("NatureOfferDialog",overlay,new Vector2(960,1560),Vector2.zero);Face(dialog,Cream);
        var viewport=Rect("NatureBackgroundViewport",dialog,new Vector2(896,900),new Vector2(0,80));
        viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("NatureBackgroundPages",viewport,Vector2.zero,Vector2.zero);
        content.anchorMin=Vector2.zero;content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,.5f);
        pager=viewport.gameObject.AddComponent<NatureOfferPager>();pager.viewport=viewport;pager.content=content;
        pager.horizontal=true;pager.vertical=false;pager.inertia=false;pager.movementType=ScrollRect.MovementType.Unrestricted;
        for(int i=0;i<NatureBackgroundBundle.Backgrounds.Length+2;i++)
        {
            int page=(i-1+NatureBackgroundBundle.Backgrounds.Length)%NatureBackgroundBundle.Backgrounds.Length;
            var art=Rect("Background_"+NatureBackgroundBundle.Backgrounds[page]+"_Slot_"+i,content,Vector2.zero,Vector2.zero);
            art.gameObject.AddComponent<CosmeticBundlePreview>().SetItem(CosmeticBundleCatalog.Nature.Items[page]);
            art.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        }
        pager.Initialize(NatureBackgroundBundle.Backgrounds.Length);pager.PageChanged+=RefreshPage;
        var completePreview=Rect("CompleteStylePreview",viewport,new Vector2(506,900),Vector2.zero);
        completeStylePreview=completePreview.gameObject.AddComponent<CosmeticBundlePreview>();
        completePreview.gameObject.SetActive(false);
        header=Rect("NatureOfferHeader",dialog,new Vector2(960,190),Vector2.zero);Face(header,new Color(Cream.r,Cream.g,Cream.b,.94f));
        title=Label(header,"Title",new Vector2(0,51),new Vector2(900,58),40);
        description=Label(header,"Description",new Vector2(0,-9),new Vector2(900,50),27);
        swipeHint=Label(header,"SwipeHint",new Vector2(0,-61),new Vector2(900,38),23);
        footer=Rect("NatureOfferFooter",dialog,new Vector2(960,350),Vector2.zero);Face(footer,new Color(Cream.r,Cream.g,Cream.b,.94f));
        selectedName=Label(footer,"SelectedBackground",new Vector2(0,121),new Vector2(720,48),32);
        previous=Navigation("PreviousBackground",footer,"‹",()=>ShowPage(PreviewPage-1));
        next=Navigation("NextBackground",footer,"›",()=>ShowPage(PreviewPage+1));
        for(int i=0;i<pageDots.Length;i++)
        {
            int page=i;
            var dot=Rect("Page_"+i,footer,new Vector2(18,18),new Vector2((i-1)*34,73));
            Face(dot,Ink);pageDots[i]=dot.GetComponent<Image>();
            dot.gameObject.AddComponent<Button>().onClick.AddListener(()=>ShowPage(page));
        }
        savingsLabel=Label(footer,"BundleSavings",new Vector2(0,29),new Vector2(850,44),25);
        savingsLabel.color=new Color(.37f,.30f,.16f);
        var purchase=Rect("BuyNatureBundle",footer,new Vector2(850,78),new Vector2(0,-33));Face(purchase,Ink);
        buy=purchase.gameObject.AddComponent<Button>();buy.onClick.AddListener(Purchase);
        buyLabel=Label(purchase,"Label",Vector2.zero,new Vector2(830,72),27);buyLabel.color=Color.white;
        var close=Rect("ContinueNatureBundle",footer,new Vector2(320,64),new Vector2(0,-126));Face(close,new Color(.79f,.85f,.77f));
        continueButton=close.gameObject.AddComponent<Button>();continueButton.onClick.AddListener(Continue);
        continueLabel=Label(close,"Label",Vector2.zero,new Vector2(310,60),27);
        overlay.gameObject.SetActive(false);
    }
    public bool TryShow(GameManager manager,Action continuation)
    {
        if(!YoutubePlatform.PurchasesAllowed)return false;
        if(overlay==null || IsOpen || manager==null || manager.State!=GameState.LevelComplete)return false;
        int number=manager.GetDisplayNumber(manager.CurrentLevelIndex);
        if(!NatureBackgroundBundle.ShouldOffer(number,manager.IsSeasonal(manager.CurrentLevelIndex)))return false;
        var selection=CosmeticBundleCatalog.PickUnowned();if(selection==null)return false;
        bundle=selection;Open(continuation);
        NatureBackgroundBundle.MarkShown(number);
        return true;
    }
    public bool ShowFromShop(CosmeticBundle selection=null)
    {
        if(!YoutubePlatform.PurchasesAllowed)return false;
        if(overlay==null || IsOpen || pending)return false;
        selection=selection??CosmeticBundleCatalog.Nature;if(selection.IsOwned)return false;
        bundle=selection;Open(null);return true;
    }
    private void Open(Action continuation)
    {
        GameIsReady.SetWindowOpen(this,true);
        previousTime=Time.timeScale;Time.timeScale=0;onContinue=continuation;
        bool style=bundle.IsStyleBundle;
        pager.enabled=!style;pager.content.gameObject.SetActive(!style);
        completeStylePreview.gameObject.SetActive(style);
        if(style)
            completeStylePreview.SetItem(bundle.Items.Single(item=>item.Part==ThemeCatalog.Part.Casings),bundle.Backgrounds[0],bundle.Items.Single(item=>item.Part==ThemeCatalog.Part.Conveyor).Theme);
        else
            for(int i=0;i<pager.content.childCount;i++)pager.content.GetChild(i).GetComponent<CosmeticBundlePreview>().SetItem(bundle.Items[(i-1+bundle.Items.Length)%bundle.Items.Length],bundle.Backgrounds[0]);
        pending=false;purchaseFailed=false;Refresh();overlay.SetAsLastSibling();overlay.gameObject.SetActive(true);
        Layout();Canvas.ForceUpdateCanvases();if(!style){pager.ResizePages();pager.ShowPage(0,true);}RefreshPage(0);
    }
    public void Continue()
    {
        if(!IsOpen || pending)return;
        overlay.gameObject.SetActive(false);Time.timeScale=previousTime;
        GameIsReady.SetWindowOpen(this,false);
        var continuation=onContinue;onContinue=null;continuation?.Invoke();
    }
    private void Purchase()
    {
        if(pending || !IsOpen)return;
        if(bundle.IsOwned){Continue();return;}
        if(!TryPrice(out _,bundle))return;
        pending=true;purchaseFailed=false;Refresh();bool settled=false;
        try
        {
            MirraSDK.Payments.Purchase(productTag:bundle.Tag,onSuccess:()=>
            {
                if(settled)return;settled=true;bundle.GrantPurchased();
                Analytic.BuyInApp(ProgressSaveService.CurrentLevelIndex.ToString(),bundle.Tag);
                if(this==null)return;pending=false;Continue();
            },onError:()=>
            {
                if(settled)return;settled=true;if(this==null)return;
                pending=false;purchaseFailed=true;Refresh();
            });
        }
        catch(Exception error)
        {
            if(settled || this==null)return;settled=true;pending=false;purchaseFailed=true;Refresh();
            Debug.LogWarning("Nature bundle purchase unavailable: "+error.Message);
        }
    }
    public static bool TryPrice(out string price,CosmeticBundle definition=null)
    {
        definition=definition??CosmeticBundleCatalog.Nature;
        price=CampaignText.Get("unavailable");if(!GameSdkReadiness.IsReady)return false;
        try
        {
            ProductData product=MirraSDK.Payments.GetProductData(definition.Tag);
            if(product==null || product.Tag!=definition.Tag)return false;
            price=product.GetFullPriceFloat();return !string.IsNullOrEmpty(price);
        }
        catch {return false;}
    }
    private void Refresh()
    {
        title.text=CampaignText.Get(bundle.TitleKey);description.text=CampaignText.Get(purchaseFailed?"nature_bundle_error":bundle.IsStyleBundle?"style_bundle_description":bundle==CosmeticBundleCatalog.Nature?"nature_bundle_description":"bundle_description");
        continueLabel.text=CampaignText.Get("nature_bundle_continue");
        swipeHint.gameObject.SetActive(!bundle.IsStyleBundle);
        swipeHint.text=bundle.IsStyleBundle?"":CampaignText.Get("nature_bundle_swipe");RefreshPage(PreviewPage);
        bool available=TryPrice(out string price,bundle);
        savingsLabel.text=available?GetSavingsText(bundle):"";
        buyLabel.text=pending?CampaignText.Get("ad_wait"):available?string.Format(CampaignText.Get("nature_bundle_buy"),price):price;
        buy.interactable=!pending&&available;continueButton.interactable=!pending;
    }
    public static string GetSavingsText(CosmeticBundle definition=null)
    {
        definition=definition??CosmeticBundleCatalog.Nature;
        if(definition.IsStyleBundle || !GameSdkReadiness.IsReady)return "";
        try
        {
            var individual=new ProductData[definition.Backgrounds.Length];
            for(int i=0;i<individual.Length;i++)individual[i]=MirraSDK.Payments.GetProductData(ThemeCatalog.ProductTag(definition.Backgrounds[i],ThemeCatalog.Part.Background));
            if(NatureBackgroundBundle.TrySavings(MirraSDK.Payments.GetProductData(definition.Tag),individual,out string regularPrice,out int percent,definition))
                return string.Format(CampaignText.Get("nature_bundle_savings"),regularPrice,percent);
        }
        catch { /* Individual product prices may not be available yet. */ }
        return "";
    }
    private void Update()
    {
        if(canvas==null || overlay==null)return;
        if(!restored && GameSdkReadiness.IsReady)
        {
            restored=true;
            try {MirraSDK.Payments.RestorePurchases(data=>{if(data!=null)foreach(var entry in CosmeticBundleCatalog.All)data.RestoreProduct(entry.Tag,entry.Grant);});}
            catch(Exception error) {Debug.LogWarning("Nature bundle restore unavailable: "+error.Message);}
        }
        if(!IsOpen)return;
        if(!pending && bundle.IsOwned){Continue();return;}
        Layout();if(!bundle.IsStyleBundle)pager.ResizePages();
        if(!pending&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;Refresh();}
        if(Input.GetKeyDown(KeyCode.Escape))Continue();
        if(Input.GetKeyDown(KeyCode.LeftArrow))ShowPage(PreviewPage-1);
        if(Input.GetKeyDown(KeyCode.RightArrow))ShowPage(PreviewPage+1);
    }
    private void Layout()
    {
        float scale=Mathf.Max(.001f,canvas.scaleFactor);Rect safe=Screen.safeArea;
        float panelScale=Mathf.Min(1,(safe.width/scale-80)/960,(safe.height/scale-160)/1560);
        dialog.sizeDelta=new Vector2(960,1560);dialog.localScale=Vector3.one*Mathf.Max(.1f,panelScale);
        dialog.anchoredPosition=(safe.center-new Vector2(Screen.width*.5f,Screen.height*.5f))/scale;
        header.anchorMin=header.anchorMax=new Vector2(.5f,1);footer.anchorMin=footer.anchorMax=new Vector2(.5f,0);
        header.sizeDelta=new Vector2(920,190);footer.sizeDelta=new Vector2(920,350);
        header.anchoredPosition=new Vector2(0,-115);footer.anchoredPosition=new Vector2(0,195);
        float panelWidth=footer.rect.width;
        foreach(var label in new[]{title,description,swipeHint})label.rectTransform.sizeDelta=new Vector2(panelWidth-40,label.rectTransform.sizeDelta.y);
        selectedName.rectTransform.sizeDelta=new Vector2(panelWidth-210,48);
        ((RectTransform)previous.transform).anchoredPosition=new Vector2(-panelWidth*.5f+55,121);
        ((RectTransform)next.transform).anchoredPosition=new Vector2(panelWidth*.5f-55,121);
        savingsLabel.rectTransform.sizeDelta=new Vector2(panelWidth-40,44);
        ((RectTransform)buy.transform).sizeDelta=new Vector2(panelWidth-40,78);buyLabel.rectTransform.sizeDelta=new Vector2(panelWidth-60,72);
    }
    private void RefreshPage(int page)
    {
        if(selectedName==null)return;
        bool style=bundle.IsStyleBundle;
        selectedName.text=style?CampaignText.Get("style_bundle_shop_hint"):bundle.Items[page].Name;
        previous.gameObject.SetActive(!style);next.gameObject.SetActive(!style);
        previous.interactable=next.interactable=!pending;
        for(int i=0;i<pageDots.Length;i++)
        {
            pageDots[i].gameObject.SetActive(!style);
            pageDots[i].color=i==page?Ink:new Color(Ink.r,Ink.g,Ink.b,.25f);
        }
    }
    private static Button Navigation(string name,Transform parent,string glyph,UnityEngine.Events.UnityAction click)
    {
        var root=Rect(name,parent,new Vector2(70,64),Vector2.zero);Face(root,new Color(.79f,.85f,.77f));
        var button=root.gameObject.AddComponent<Button>();button.onClick.AddListener(click);Label(root,"Label",Vector2.zero,new Vector2(64,60),44).text=glyph;return button;
    }
    private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private void OnDestroy(){if(IsOpen)Time.timeScale=previousTime;GameIsReady.SetWindowOpen(this,false);onContinue=null;}
    private static void Face(RectTransform root,Color tint){var image=root.gameObject.AddComponent<Image>();image.sprite=HintUiSprites.RoundedPanel;image.type=Image.Type.Sliced;image.color=tint;}
    private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;}
    private static TextMeshProUGUI Label(Transform parent,string name,Vector2 position,Vector2 size,float font){var label=Rect(name,parent,size,position).gameObject.AddComponent<TextMeshProUGUI>();label.fontSize=font;label.enableWordWrapping=true;label.enableAutoSizing=true;label.fontSizeMin=font*.7f;label.fontSizeMax=font;label.alignment=TextAlignmentOptions.Center;label.color=Ink;label.raycastTarget=false;return label;}
}
