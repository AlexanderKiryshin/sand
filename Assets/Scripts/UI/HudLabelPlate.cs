using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Artwork can be light or dark; these small plates preserve caption contrast.
public sealed class HudLabelPlate : MonoBehaviour
{
    private TMP_Text label;
    private RectTransform plate;
    private void LateUpdate()
    {
        if(label==null)label=GetComponent<TMP_Text>();
        if(label==null)return;
        if(plate==null)
        {
            var go=new GameObject(name+"Plate",typeof(RectTransform),typeof(Image));
            go.transform.SetParent(transform.parent,false);go.transform.SetSiblingIndex(transform.GetSiblingIndex());
            plate=(RectTransform)go.transform;
            var image=go.GetComponent<Image>();image.sprite=HintUiSprites.RoundedPanel;image.type=Image.Type.Sliced;
            image.color=new Color(.98f,.97f,.91f,.95f);image.raycastTarget=false;
        }
        plate.gameObject.SetActive(ThemeCatalog.SelectedBackground>=12);
        if(!plate.gameObject.activeSelf)return;
        plate.sizeDelta=new Vector2(Mathf.Min(label.rectTransform.rect.width,label.preferredWidth+32),label.rectTransform.rect.height+4);
        plate.position=label.rectTransform.TransformPoint(label.rectTransform.rect.center);
    }
    private void OnDestroy(){if(plate!=null)Destroy(plate.gameObject);}
}
