using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent shop entry opens the nature carousel without consuming a milestone offer.</summary>
public sealed class NatureBundleShopCard : MonoBehaviour
{
    private Button button;
    private CosmeticBundle bundle;
    private TextMeshProUGUI title,hint,price,savings;
    private float nextRefresh;
    public void Initialize(Button entry,TextMeshProUGUI heading,TextMeshProUGUI caption,TextMeshProUGUI cost,TextMeshProUGUI discount,CosmeticBundle definition=null)
    {
        bundle=definition??CosmeticBundleCatalog.Nature;button=entry;title=heading;hint=caption;price=cost;savings=discount;
        button.onClick.AddListener(()=>FindObjectOfType<NatureBackgroundOffer>()?.ShowFromShop(bundle));
        Refresh();
    }
    private void OnEnable(){if(button!=null)Refresh();}
    private void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;Refresh();}}
    private void Refresh()
    {
        if(button==null)return;
        bool owned=bundle.IsOwned;
        title.text=CampaignText.Get(bundle.TitleKey);hint.text=CampaignText.Get(bundle.IsStyleBundle?"style_bundle_shop_hint":"nature_bundle_shop_hint");
        NatureBackgroundOffer.TryPrice(out string sdkPrice,bundle);
        price.text=owned?CampaignText.Get("purchased"):sdkPrice;
        savings.text=owned?"":NatureBackgroundOffer.GetSavingsText(bundle);
        button.interactable=!owned;
    }
}
