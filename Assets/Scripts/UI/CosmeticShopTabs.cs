using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CosmeticShopTabs : MonoBehaviour
{
    public int SelectedTab {get;private set;}
    public bool CanClose => menu==null || !menu.IsPending;
    private RectTransform card,resources,bundles;
    private ThemeCustomizationMenu menu;
    private readonly Image[] faces=new Image[5];
    private readonly TextMeshProUGUI[] labels=new TextMeshProUGUI[5];
    private readonly RectTransform[] counters=new RectTransform[3];
    private readonly TextMeshProUGUI[] counterLabels=new TextMeshProUGUI[3];
    private readonly string[] keys={"shop_resources","shop_bundles","theme_conveyor_tab","theme_casings_tab","theme_background_tab"};
    public void Initialize(RectTransform root,RectTransform resourceView,RectTransform bundleView,float width)
    {
        card=root;resources=resourceView;bundles=bundleView;
        float step=(width-64)/5;
        for(int i=0;i<5;i++)
        {
            int tab=i;
            var go=new GameObject("ShopTab_"+i,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(card,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(step-8,64);rect.anchoredPosition=new Vector2((i-2)*step,card.rect.height*.5f-160);
            faces[i]=go.GetComponent<Image>();faces[i].sprite=HintUiSprites.RoundedPanel;faces[i].type=Image.Type.Sliced;
            go.GetComponent<Button>().onClick.AddListener(()=>Select(tab));
            var text=new GameObject("TabLabel",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(go.transform,false);
            var textRect=(RectTransform)text.transform;textRect.sizeDelta=new Vector2(step-14,60);
            labels[i]=text.GetComponent<TextMeshProUGUI>();labels[i].fontSize=25;labels[i].enableAutoSizing=true;labels[i].fontSizeMin=18;labels[i].fontSizeMax=25;labels[i].alignment=TextAlignmentOptions.Center;labels[i].raycastTarget=false;
            if(i>=2)counters[i-2]=ShopGoldBadge.CreateBadge(go.transform,"TabGoldCounter_"+(i-2),new Vector2(34,32),Vector2.one,new Vector2(-8,-2),out counterLabels[i-2]);
        }
        Select(0);
    }
    private bool Attach()
    {
        if(menu==null)menu=FindObjectOfType<ThemeCustomizationMenu>();
        return menu!=null && menu.EmbedInShop(card);
    }
    public void OpenCategory(ThemeCatalog.Part category)
    {
        GetComponent<RewardedShop>().OpenShop();Select((int)category+2);
    }
    public void Select(int tab)
    {
        if(!CanClose)return;
        SelectedTab=Mathf.Clamp(tab,0,4);resources.gameObject.SetActive(SelectedTab==0);bundles.gameObject.SetActive(SelectedTab==1);
        if(Attach()){if(SelectedTab>=2)menu.ShowEmbedded((ThemeCatalog.Part)(SelectedTab-2));else menu.HideEmbedded();}
        RefreshLabels();
    }
    private void OnEnable(){if(card!=null)Select(SelectedTab);}
    private void Update(){if(Attach() && SelectedTab<2)menu.HideEmbedded();RefreshLabels();RefreshGoldCounters();}
    public void RefreshGoldCounters()
    {
        int gold=CoinManager.instance!=null?CoinManager.instance.Coins:0;
        for(int i=0;i<3;i++)if(counters[i]!=null)
        {
            int count=ShopGoldBadge.CountAffordable(gold,(ThemeCatalog.Part)i);
            counters[i].gameObject.SetActive(count>0);counterLabels[i].text=count.ToString();
            counters[i].localScale=Vector3.one*(1+.055f*Mathf.Sin(Time.unscaledTime*4.5f+i*.6f));
        }
    }
    private void RefreshLabels()
    {
        for(int i=0;i<5;i++)if(labels[i]!=null)
        {
            labels[i].text=CampaignText.Get(keys[i]);labels[i].color=i==SelectedTab?Color.white:new Color(.10f,.27f,.29f);
            faces[i].color=i==SelectedTab?new Color(.10f,.27f,.29f):new Color(.79f,.85f,.77f);
        }
    }
}
