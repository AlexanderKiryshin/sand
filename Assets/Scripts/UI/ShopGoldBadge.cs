using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Counts affordable, unowned gold cosmetics independently in each category.</summary>
public sealed class ShopGoldBadge : MonoBehaviour
{
    public int AvailableCount {get;private set;}
    private RectTransform badge;
    private TextMeshProUGUI label;
    private float nextRefresh,changedAt;
    public static int CountAffordable(int gold,ThemeCatalog.Part? category=null)
    {
        int count=0;
        for(int p=0;p<3;p++)
        {
            var part=(ThemeCatalog.Part)p;if(category.HasValue && category.Value!=part)continue;
            for(int id=0;id<ThemeCatalog.CountFor(part);id++)
                if(ThemeCatalog.IsAvailable(id,part) && ThemeCatalog.AccessFor(id,part)==ThemeCatalog.Access.Coins && !ThemeCatalog.IsUnlocked(id,part) && gold>=ThemeCatalog.CoinPrice(id,part))
                    count++;
        }
        return count;
    }
    public static RectTransform CreateBadge(Transform parent,string name,Vector2 size,Vector2 anchor,Vector2 position,out TextMeshProUGUI text)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=anchor;rect.sizeDelta=size;rect.anchoredPosition=position;
        var face=go.GetComponent<Image>();face.sprite=HintUiSprites.RoundedPanel;face.type=Image.Type.Sliced;face.color=new Color(.87f,.13f,.18f);face.raycastTarget=false;
        var caption=new GameObject("Count",typeof(RectTransform),typeof(TextMeshProUGUI));caption.transform.SetParent(go.transform,false);
        var tr=(RectTransform)caption.transform;tr.sizeDelta=size;
        text=caption.GetComponent<TextMeshProUGUI>();text.fontSize=size.y*.63f;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
        return rect;
    }
    private void Start()
    {
        badge=CreateBadge(transform,"ShopGoldCounter",new Vector2(52,46),new Vector2(.84f,1),new Vector2(0,-20),out label);
        RefreshCount();
    }
    public void RefreshCount()
    {
        if(badge==null)return;
        int count=CountAffordable(CoinManager.instance!=null?CoinManager.instance.Coins:0);
        if(count!=AvailableCount)changedAt=Time.unscaledTime;
        AvailableCount=count;badge.gameObject.SetActive(count>0);label.text=count.ToString();
    }
    private void Update()
    {
        if(badge==null)return;
        if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;RefreshCount();}
        if(AvailableCount>0)
        {
            float pop=Mathf.Max(0,1-(Time.unscaledTime-changedAt)/.6f)*.14f;
            badge.localScale=Vector3.one*(1+.055f*Mathf.Sin(Time.unscaledTime*4.5f)+pop);
        }
    }
}
