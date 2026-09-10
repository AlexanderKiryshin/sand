using UnityEngine;
using UnityEngine.UI;

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
        Rect r = rectTransform.rect; float size = r.width * .88f;
        Vector2 center = r.center + Vector2.up * ((1 - amount) * 115);
        float visibility = Mathf.Clamp01(amount * 4);
        if (crate)
        {
            Quad(vh, center, new Vector2(size, size), new Color(.59f, .32f, .12f, visibility));
            for (int i = -1; i <= 1; i++) Quad(vh, center + Vector2.up * (i * size / 3), new Vector2(size - 9, size / 3 - 5), new Color(.88f, .61f, .27f, visibility));
            Quad(vh, center, new Vector2(size * 1.12f, 10), new Color(.98f, .78f, .42f, visibility), 42);
            Quad(vh, center, new Vector2(size * 1.12f, 10), new Color(.74f, .44f, .18f, visibility), -42);
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
