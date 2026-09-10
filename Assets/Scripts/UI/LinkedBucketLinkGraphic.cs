using UnityEngine;
using UnityEngine.UI;

/// <summary>Two-tone connector between linked inventory buckets (each half uses a partner color).</summary>
public sealed class LinkedBucketLinkGraphic : MaskableGraphic
{
    private Vector2 start;
    private Vector2 end;
    private Color colorA = Color.white;
    private Color colorB = Color.white;
    private float thickness = 8f;

    public void SetLink(Vector2 from, Vector2 to, Color a, Color b, float width = 8f)
    {
        start = from;
        end = to;
        colorA = a;
        colorB = b;
        thickness = width;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 mid = (start + end) * 0.5f;
        AddSegment(vh, start, mid, colorA);
        AddSegment(vh, mid, end, colorB);
    }

    private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.01f) return;
        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (thickness * 0.5f);
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = a - n; vh.AddVert(v);
        v.position = a + n; vh.AddVert(v);
        v.position = b + n; vh.AddVert(v);
        v.position = b - n; vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
