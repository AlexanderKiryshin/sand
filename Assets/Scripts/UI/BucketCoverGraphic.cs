using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BucketCoverGraphic : MaskableGraphic
{
    private bool crate;
    private float amount;
    public void Show(bool isCrate, float value)
    {
        if (crate == isCrate && Mathf.Approximately(amount, value)) return;
        crate = isCrate; amount = value; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (amount <= 0) return;
        Rect r = rectTransform.rect; float size = r.width * (crate ? 1.04f : .88f);
        Vector2 center = r.center + Vector2.up * ((1 - amount) * 115);
        if (crate)
        {
            // Only the closed panel is visible; open cells have no wooden outline.
            Color frame = new Color(.59f, .32f, .12f, 1);
            float lidHeight = size * amount;
            center = r.center;
            Quad(vh, center, new Vector2(size, lidHeight), frame);
            for (int i = -1; i <= 1; i++) Quad(vh, center + Vector2.up * (i * lidHeight / 3), new Vector2(size - 9, Mathf.Max(1, lidHeight / 3 - 5)), new Color(.88f, .61f, .27f, 1));
            Quad(vh, center, new Vector2(size * Mathf.Lerp(1, 1.12f, amount), Mathf.Lerp(2, 10, amount)), new Color(.98f, .78f, .42f, 1), 42 * amount);
            Quad(vh, center, new Vector2(size * Mathf.Lerp(1, 1.12f, amount), Mathf.Lerp(2, 10, amount)), new Color(.74f, .44f, .18f, 1), -42 * amount);
        }
        else
        {
            for (int i = -2; i <= 2; i++) Quad(vh, center + Vector2.right * (i * 17), new Vector2(9, size), new Color(.42f, .59f, .62f, amount));
            Quad(vh, center + Vector2.up * 30, new Vector2(size, 12), new Color(.71f, .81f, .80f, amount));
            Quad(vh, center - Vector2.up * 30, new Vector2(size, 12), new Color(.23f, .39f, .42f, amount));
        }
    }
    private static void Quad(VertexHelper vh, Vector2 c, Vector2 size, Color color, float angle = 0)
    {
        int first = vh.currentVertCount; Quaternion turn = Quaternion.Euler(0, 0, angle);
        vh.AddVert(c + (Vector2)(turn * new Vector3(-size.x, -size.y) * .5f), color, Vector2.zero);
        vh.AddVert(c + (Vector2)(turn * new Vector3(size.x, -size.y) * .5f), color, Vector2.zero);
        vh.AddVert(c + (Vector2)(turn * new Vector3(size.x, size.y) * .5f), color, Vector2.zero);
        vh.AddVert(c + (Vector2)(turn * new Vector3(-size.x, size.y) * .5f), color, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
    }
}
