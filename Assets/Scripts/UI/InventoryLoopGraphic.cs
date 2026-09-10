using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryLoopGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < InventoryLoopRoute.Cells.Length; i++)
        {
            Vector2 a = Point(InventoryLoopRoute.Cells[i]), b = Point(InventoryLoopRoute.Cells[(i + 1) % InventoryLoopRoute.Cells.Length]);
            Line(vh, a, b, 96, new Color(.15f, .34f, .36f));
            Line(vh, a, b, 80, new Color(.43f, .62f, .59f));
            Vector2 direction = (b - a).normalized, side = new Vector2(-direction.y, direction.x);
            Vector2 tip = Vector2.Lerp(a, b, .5f) + direction * 10;
            int first = vh.currentVertCount;
            vh.AddVert(tip, new Color(1, .85f, .34f), Vector2.zero);
            vh.AddVert(tip - direction * 18 + side * 10, new Color(1, .85f, .34f), Vector2.zero);
            vh.AddVert(tip - direction * 18 - side * 10, new Color(1, .85f, .34f), Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
        }
    }
    private Vector2 Point(int i)
    {
        float size = MobilePortraitLayout.BucketCellSize;
        float spacing = MobilePortraitLayout.BucketSpacing;
        float step = size + spacing;
        return new Vector2(rectTransform.rect.xMin + size * 0.5f + (i % 5) * step,
            rectTransform.rect.yMax - size * 0.5f - (i / 5) * step);
    }
    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized; Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
        int first = vh.currentVertCount;
        vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
        vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
    }
}
