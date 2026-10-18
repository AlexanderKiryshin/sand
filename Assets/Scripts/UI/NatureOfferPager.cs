using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A looping portrait carousel snaps with unscaled time while gameplay is paused.</summary>
public sealed class NatureOfferPager : ScrollRect
{
    public event Action<int> PageChanged;
    public int Page { get; private set; }
    public float TargetPosition => padding-slot*stride;
    private int count,slot=1,startSlot;
    private float stride,padding,lastWidth,lastHeight;
    private bool dragging,snapping;
    private Vector2 startPointer;
    public void Initialize(int pages){count=pages;ResizePages();ShowPage(0,true);}
    public void ResizePages()
    {
        if(viewport==null || content==null)return;
        float width=viewport.rect.width,height=viewport.rect.height;
        if(width<=0 || height<=0 || (Mathf.Approximately(width,lastWidth) && Mathf.Approximately(height,lastHeight)))return;
        lastWidth=width;lastHeight=height;
        float cardWidth=Mathf.Min(width*.74f,height*9/16f);
        stride=cardWidth+width*.025f;padding=(width-cardWidth)*.5f;
        content.sizeDelta=new Vector2(stride*(count+2),0);
        for(int i=0;i<content.childCount;i++)
        {
            var card=(RectTransform)content.GetChild(i);
            card.anchorMin=card.anchorMax=new Vector2(0,.5f);card.pivot=new Vector2(.5f,.5f);
            card.sizeDelta=new Vector2(cardWidth,cardWidth*16/9f);
            card.anchoredPosition=new Vector2(i*stride+cardWidth*.5f,0);
        }
        ShowPage(Page,true);
    }
    public void ShowPage(int page,bool immediately=false)
    {
        int selected=((page%count)+count)%count;
        int destination=selected+1;
        if(!immediately && Page==0 && page<0)destination=0;
        if(!immediately && Page==count-1 && page>=count)destination=count+1;
        SetSlot(destination,immediately);
    }
    private void SetSlot(int destination,bool immediately=false)
    {
        StopMovement();slot=destination;
        int selected=((slot-1)%count+count)%count;
        bool changed=selected!=Page;Page=selected;snapping=!immediately;
        if(immediately && content!=null)content.anchoredPosition=new Vector2(TargetPosition,0);
        if(changed)PageChanged?.Invoke(Page);
    }
    public override void OnBeginDrag(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left)return;
        startPointer=data.position;startSlot=slot;dragging=true;snapping=false;base.OnBeginDrag(data);
    }
    public override void OnEndDrag(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left)return;
        base.OnEndDrag(data);dragging=false;
        Vector2 delta=data.position-startPointer;
        int selected=Mathf.RoundToInt((padding-content.anchoredPosition.x)/Mathf.Max(1,stride));
        if(Mathf.Abs(delta.x)>=40 && Mathf.Abs(delta.x)>Mathf.Abs(delta.y)*1.2f)selected=startSlot+(delta.x<0?1:-1);
        SetSlot(Mathf.Clamp(selected,0,count+1));
    }
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if(content==null)return;
        if(!dragging && snapping)
        {
            float x=Mathf.Lerp(content.anchoredPosition.x,TargetPosition,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            if(Mathf.Abs(x-TargetPosition)<.25f)
            {
                x=TargetPosition;snapping=false;
                if(slot==0 || slot==count+1){slot=Page+1;x=TargetPosition;}
            }
            content.anchoredPosition=new Vector2(x,0);
        }
        for(int i=0;i<content.childCount;i++)
        {
            var card=(RectTransform)content.GetChild(i);
            float focus=1-Mathf.Clamp01(Mathf.Abs(content.anchoredPosition.x+card.anchoredPosition.x-viewport.rect.width*.5f)/Mathf.Max(1,stride));
            card.localScale=Vector3.one*Mathf.Lerp(.9f,1,focus);
            card.GetComponent<CanvasGroup>().alpha=Mathf.Lerp(.68f,1,focus);
        }
    }
    protected override void OnDisable(){base.OnDisable();dragging=false;snapping=false;}
}
