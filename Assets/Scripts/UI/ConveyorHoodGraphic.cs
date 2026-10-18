using UnityEngine;
using UnityEngine.UI;

/// <summary>Closed tunnel backs under the buckets, compact roofs and fronts over them.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ConveyorHoodGraphic : MaskableGraphic
{
    public int PreviewTheme = -1;
    private int Theme => PreviewTheme < 0 ? ThemeCatalog.SelectedCasings : PreviewTheme;
    private ConveyorSurfaceGraphic surface;
    private bool backLayer;
    public void Initialize(ConveyorSurfaceGraphic deck, bool backs = false)
    { surface = deck; backLayer = backs; raycastTarget = false; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (surface == null) return;
        DrawHood(vh, false);
        DrawHood(vh, true);
    }

    private void DrawHood(VertexHelper vh, bool right)
    {
        Color casing = ThemeCatalog.Casing(Theme);
        float sign = right ? 1 : -1;
        float u = right ? .975f : .025f;
        Vector2 near = rectTransform.InverseTransformPoint(surface.rectTransform.TransformPoint(surface.DeckPoint(u, 0)));
        Vector2 far = rectTransform.InverseTransformPoint(surface.rectTransform.TransformPoint(surface.DeckPoint(u, 1)));
        float outside = sign * (rectTransform.rect.width * .5f + 34);
        Vector2 outerNear = new Vector2(outside, near.y);
        Vector2 outerFar = new Vector2(outside + far.x - near.x, far.y);
        Vector2 rise = Vector2.up * ConveyorBelt.BucketWidth * .82f;
        Vector2 drop = Vector2.down * surface.rectTransform.rect.height * .20f;
        if (backLayer)
        {
            // A dark, bounded entrance and a full rear wall close the tunnel. They sit
            // behind passing buckets; only the roof and front fascia can occlude them.
            Quad(vh, near, far, far + rise, near + rise, new Color(.08f, .20f, .23f));
            Quad(vh, far, outerFar, outerFar + rise, far + rise, Color.Lerp(casing, Color.black, .55f));
            Quad(vh, near + drop, outerNear + drop, outerFar, far, Color.Lerp(casing, Color.black, .60f));
            return;
        }
        Quad(vh, near + drop, outerNear + drop, outerNear + rise, near + rise, Color.Lerp(casing, Color.black, .30f));
        Quad(vh, near + rise, outerNear + rise, outerFar + rise, far + rise, casing);
        ThemeSceneryMesh.DrawCasing(vh,(x,y)=>Vector2.Lerp(Vector2.Lerp(near+drop,outerNear+drop,x),Vector2.Lerp(near+rise,outerNear+rise,x),y),Theme);
        ThemePatternMesh.Draw(vh,(x,y)=>Vector2.Lerp(Vector2.Lerp(near+rise,outerNear+rise,x),Vector2.Lerp(far+rise,outerFar+rise,x),y),Theme,2,2,.30f);
        // Rear edge thickness seals the roof against the back wall (no daylight gap).
        Quad(vh, far + rise, outerFar + rise, outerFar + rise + Vector2.up * 5,
            far + rise + Vector2.up * 5, Color.Lerp(casing, Color.white, .25f));
        // Inner roof lip follows the same depth direction as the deck.
        Vector2 lip = new Vector2(sign * 8, 0);
        Quad(vh, near + rise, near + rise + lip, far + rise + lip, far + rise, Color.Lerp(casing, Color.white, .40f));
        Quad(vh, near + drop, near + drop + lip, near + rise + lip, near + rise, Color.Lerp(casing, Color.black, .60f));
        Quad(vh, near + Vector2.up * 6 + lip, outerNear + Vector2.up * 6,
            outerNear + Vector2.up * 10, near + Vector2.up * 10 + lip, casing);
        // A broad bevel makes the front face read as a casing, not a floating tile.
        Quad(vh, near + rise - Vector2.up * 7 + lip, outerNear + rise - Vector2.up * 7,
            outerNear + rise, near + rise + lip, Color.Lerp(casing, Color.black, .15f));
    }

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
