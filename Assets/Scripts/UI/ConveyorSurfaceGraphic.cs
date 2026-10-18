using UnityEngine;
using UnityEngine.UI;

/// <summary>A shallow conveyor drawn in perspective, with one plane shared by its ribs and end housings.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ConveyorSurfaceGraphic : MaskableGraphic
{
    // Extend the deck beneath the hoods without moving their entrance anchors.
    private const float DeckStart = -.04f;
    private const float DeckEnd = 1.04f;
    public int PreviewTheme = -1;
    public int Theme => PreviewTheme < 0 ? ThemeCatalog.Selected : PreviewTheme;
    private float travel;
    private ConveyorBelt conveyor;
    public void Initialize(ConveyorBelt belt) { conveyor = belt; }
    public Vector2 DeckPoint(float u, float depth) => Plane(u, depth);
    public Vector3 MidlineWorld(float x) => rectTransform.TransformPoint(new Vector3(x, Plane(.5f, .5f).y, 0));
    public void SetTravel(float value)
    {
        if (Mathf.Approximately(travel, value)) return;
        travel = value;
        SetVerticesDirty();
    }

    private Vector2 Plane(float u, float depth)
    {
        Rect r = rectTransform.rect;
        // Near edge is wider; both ends share the same vanishing direction. Orthographic UI
        // needs explicit geometry: rotating a flat rectangle cannot create this view.
        float left = Mathf.Lerp(-.50f, -.46f, depth);
        float right = Mathf.Lerp(.50f, .46f, depth);
        return r.center + new Vector2(Mathf.LerpUnclamped(left, right, u) * r.width,
            Mathf.Lerp(-.12f, .42f, depth) * r.height);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float h = rectTransform.rect.height;
        Vector2 drop = Vector2.down * h * .20f;
        Vector2 a = Plane(DeckStart, 0), b = Plane(DeckEnd, 0), c = Plane(DeckEnd, 1), d = Plane(DeckStart, 1);
        Quad(vh, a + drop + Vector2.down * 7, b + drop + Vector2.down * 7,
            c + Vector2.down * 7, d + Vector2.down * 7, new Color(.08f, .19f, .21f, .13f));
        Quad(vh, a + drop, b + drop, b, a, Color.Lerp(ThemeCatalog.Deck(Theme), Color.black, .35f));
        Quad(vh, b + drop, c + drop * .65f, c, b, Color.Lerp(ThemeCatalog.Deck(Theme), Color.black, .50f));
        Quad(vh, a, b, c, d, ThemeCatalog.Deck(Theme));

        // Moving seams use the same projection as the stationary frame.
        const float spacing = 1f / 22f;
        float phase = Mathf.Repeat(-travel / Mathf.Max(1, rectTransform.rect.width), spacing);
        for (int i = -2; i < 24; i++)
        {
            float u = i * spacing + phase;
            float lo = Mathf.Clamp(u, DeckStart, DeckEnd), hi = Mathf.Clamp(u + .003f, DeckStart, DeckEnd);
            if (hi <= lo) continue;
            Quad(vh, Plane(lo, 0), Plane(hi, 0), Plane(hi, 1), Plane(lo, 1), Color.Lerp(ThemeCatalog.Deck(Theme), Color.black, .25f));
            Quad(vh, Plane(hi, 0), Plane(Mathf.Min(DeckEnd, hi + .0015f), 0),
                Plane(Mathf.Min(DeckEnd, hi + .0015f), 1), Plane(hi, 1), Color.Lerp(ThemeCatalog.Deck(Theme), Color.white, .20f));
        }
        ThemePatternMesh.Draw(vh, Plane, Theme, horizontalOffset: -travel / Mathf.Max(1, rectTransform.rect.width));
        // Contact shadows follow the center of the painted base ellipse. This exposes
        // the footprint on the plane instead of making the upright cup look suspended.
        if (conveyor != null)
        {
            foreach (var entry in conveyor.Entries)
            {
                if (Mathf.Abs(entry.PositionX) > rectTransform.rect.width * .5f) continue;
                Vector2 center = new Vector2(entry.PositionX, Plane(.5f, .5f).y);
                float radius = ConveyorBelt.BucketWidth * .31f * (entry.Bucket.SlotCost > 1 ? 1.7f : 1);
                for (int segment = 0; segment < 20; segment++)
                {
                    float a0 = segment * Mathf.PI / 10, a1 = (segment + 1) * Mathf.PI / 10;
                    Quad(vh, center, center + new Vector2(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * 7),
                        center + new Vector2(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * 7), center, new Color(.03f, .12f, .14f, .24f));
                }
            }
        }
        // Narrow rails and matching end housings sit ON the plane, never upright.
        Band(vh, DeckStart, DeckEnd, .94f, 1, ThemeCatalog.Casing(Theme));
        Band(vh, DeckStart, DeckEnd, 0, .065f, Color.Lerp(ThemeCatalog.Casing(Theme), Color.white, .35f));
        Quad(vh, a + drop * .30f, b + drop * .30f, b + drop * .22f,
            a + drop * .22f, Color.Lerp(ThemeCatalog.Casing(Theme), Color.black, .25f));
    }

    private void Band(VertexHelper vh, float left, float right, float near, float far, Color tint)
        => Quad(vh, Plane(left, near), Plane(right, near), Plane(right, far), Plane(left, far), tint);

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
