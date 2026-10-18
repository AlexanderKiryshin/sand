using UnityEngine;
using UnityEngine.UI;

/// <summary>Stretchable two-tone bar joining cup centers, rendered behind the cup bodies.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LinkedBucketLinkGraphic : MaskableGraphic
{
    private Color colorA = Color.white;
    private Color colorB = Color.white;
    private Vector2 leftEdge;
    private Vector2 rightEdge;
    private float thickness;

    public void SetLink(Vector2 leftCupCenter, Vector2 rightCupCenter, float cupSize, Color a, Color b, bool cupsUpright = true)
    {
        Vector2 leftCenter, rightCenter;
        if (leftCupCenter.x > rightCupCenter.x)
        {
            leftCenter = rightCupCenter;
            rightCenter = leftCupCenter;
            colorA = b;
            colorB = a;
        }
        else
        {
            leftCenter = leftCupCenter;
            rightCenter = rightCupCenter;
            colorA = a;
            colorB = b;
        }
        float cellSize = Mathf.Max(8f, cupSize);
        thickness = Mathf.Clamp(cellSize * 0.045f, 3.5f, 6f);
        leftEdge = leftCenter;
        rightEdge = rightCenter;
        SetVerticesDirty();
    }

    public void SetLink(RectTransform bodyA, RectTransform bodyB, Color a, Color b)
    {
        Vector2 centerA = rectTransform.InverseTransformPoint(bodyA.TransformPoint(bodyA.rect.center));
        Vector2 centerB = rectTransform.InverseTransformPoint(bodyB.TransformPoint(bodyB.rect.center));
        if (centerA.x > centerB.x)
        {
            RectTransform swap = bodyA; bodyA = bodyB; bodyB = swap;
            Vector2 centerSwap = centerA; centerA = centerB; centerB = centerSwap;
            Color colorSwap = a; a = b; b = colorSwap;
        }
        float sizeA = rectTransform.InverseTransformVector(bodyA.TransformVector(Vector3.right * bodyA.rect.width)).magnitude;
        float sizeB = rectTransform.InverseTransformVector(bodyB.TransformVector(Vector3.right * bodyB.rect.width)).magnitude;
        thickness = Mathf.Clamp(Mathf.Min(sizeA, sizeB) * .045f, 2f, 6f);
        leftEdge = centerA;
        rightEdge = centerB;
        colorA = a; colorB = b;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if ((rightEdge - leftEdge).sqrMagnitude < .01f) return;
        Vector2 left = leftEdge;
        Vector2 right = rightEdge;
        Vector2 mid = (left + right) * 0.5f;
        float half = thickness;

        Color a = colorA;
        Color b = colorB;
        a.a = b.a = 1f;
        AddSegment(vh, left, mid, a, half);
        AddSegment(vh, mid, right, b, half);
    }

    private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, Color color, float half)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.01f) return;
        Vector2 n = new Vector2(-dir.y, dir.x).normalized * half;
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
