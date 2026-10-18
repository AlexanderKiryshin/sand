using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ThemeBackdropGraphic : MaskableGraphic
{
    private int previewTheme=-1;
    private bool fitWholeImage;
    public bool FitWholeImage { get=>fitWholeImage; set { if(fitWholeImage==value)return;fitWholeImage=value;SetVerticesDirty(); } }
    public int PreviewTheme
    {
        get=>previewTheme;
        set { if(previewTheme==value)return;previewTheme=value;SetVerticesDirty();SetMaterialDirty(); }
    }
    private int Theme=>PreviewTheme<0?ThemeCatalog.SelectedBackground:PreviewTheme;
    public override Texture mainTexture=>ThemeBackgroundAssets.Get(Theme) ?? base.mainTexture;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int theme = PreviewTheme < 0 ? ThemeCatalog.SelectedBackground : PreviewTheme;
        if (theme == 0 && PreviewTheme < 0) return;
        Rect r = rectTransform.rect;
        if(ThemeBackgroundAssets.Get(theme) is Texture2D texture)
        {
            float sourceAspect=ThemeBackgroundAssets.GetSourceAspect(theme),targetAspect=r.width/Mathf.Max(1,r.height);
            if (FitWholeImage)
            {
                Vector2 size=sourceAspect>targetAspect ? new Vector2(r.width,r.width/sourceAspect) : new Vector2(r.height*sourceAspect,r.height);
                r=new Rect(r.center-size*.5f,size);
                targetAspect=sourceAspect;
            }
            float u=1,v=1;
            if(sourceAspect>targetAspect)u=targetAspect/sourceAspect;else v=sourceAspect/targetAspect;
            float left=(1-u)*.5f,bottomUv=(1-v)*.5f;
            // Place the bamboo stems opposite the left-hand color palette, including in previews.
            bool mirror=theme==44;
            float leftUv=mirror?left+u:left,rightUv=mirror?left:left+u;
            vh.AddVert(new Vector2(r.xMin,r.yMin),Color.white,new Vector2(leftUv,bottomUv));
            vh.AddVert(new Vector2(r.xMax,r.yMin),Color.white,new Vector2(rightUv,bottomUv));
            vh.AddVert(new Vector2(r.xMax,r.yMax),Color.white,new Vector2(rightUv,bottomUv+v));
            vh.AddVert(new Vector2(r.xMin,r.yMax),Color.white,new Vector2(leftUv,bottomUv+v));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
            return;
        }
        Color top = ThemeCatalog.Background(theme), bottom = Color.Lerp(top, Color.white, .6f);
        vh.AddVert(new Vector2(r.xMin,r.yMin), bottom, Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMin), bottom, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax,r.yMax), top, Vector2.zero); vh.AddVert(new Vector2(r.xMin,r.yMax), top, Vector2.zero);
        vh.AddTriangle(0,1,2); vh.AddTriangle(0,2,3);
        ThemePatternMesh.Draw(vh, (u,v)=>new Vector2(Mathf.Lerp(r.xMin,r.xMax,u),Mathf.Lerp(r.yMin,r.yMax,v)),theme,8,12,.10f);
        for (int i=0; i<35; i++)
        {
            float x = r.xMin + Mathf.Repeat(i*.618f+.17f,1)*r.width, y = r.yMin + Mathf.Repeat(i*.381f+.23f,1)*r.height;
            float size = Mathf.Min(r.width,r.height)*(ThemeCatalog.Pattern(theme) > 0 ? .014f : .004f);
            int start = vh.currentVertCount;
            Color accent = theme == 1 || theme == 8 ? new Color(1,1,1,.85f) : new Color(top.r*.8f,top.g*.8f,top.b*.8f,.15f);
            vh.AddVert(new Vector2(x-size,y),accent,Vector2.zero); vh.AddVert(new Vector2(x,y+size),accent,Vector2.zero);
            vh.AddVert(new Vector2(x+size,y),accent,Vector2.zero); vh.AddVert(new Vector2(x,y-size),accent,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }
}
