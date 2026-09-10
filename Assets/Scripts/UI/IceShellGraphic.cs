using UnityEngine;
using UnityEngine.UI;

/// <summary>A small, event-updated UI mesh: translucent ice, bevels and progressive cracks.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class IceShellGraphic : MaskableGraphic
{
    private int layers;
    public int Layers => layers;
    private static readonly Vector2[] Outline =
    {
        new Vector2(-.36f,-.48f), new Vector2(.35f,-.48f), new Vector2(.49f,-.31f), new Vector2(.47f,.34f),
        new Vector2(.31f,.49f), new Vector2(-.32f,.47f), new Vector2(-.48f,.31f), new Vector2(-.49f,-.30f)
    };

    public void SetLayers(int value)
    {
        value = Mathf.Max(0, value);
        if (layers == value) return;
        layers = value;
        gameObject.SetActive(value > 0);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (layers <= 0) return;
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        Vector2 size = rect.size;
        Color fill = new Color(.48f, .87f, 1f, .19f + Mathf.Min(layers, 3) * .04f);
        for (int i = 0; i < Outline.Length; i++)
        {
            Vector2 a = Outline[i], b = Outline[(i + 1) % Outline.Length];
            Triangle(mesh, center, size, Vector2.zero, a * .82f, b * .82f, fill);
            Color facet = i >= 3 && i <= 6 ? new Color(.87f, 1f, 1f, .87f) : new Color(.21f, .63f, .85f, .72f);
            Quad(mesh, center, size, a, b, b * .82f, a * .82f, facet);
        }
        Color shine = new Color(.93f, 1f, 1f, .75f);
        Line(mesh, center, size, new Vector2(-.35f,.18f), new Vector2(-.16f,.39f), .025f, shine);
        Line(mesh, center, size, new Vector2(-.33f,.04f), new Vector2(-.20f,.18f), .014f, shine);
        // More cracks mean fewer moves until thawing. No particle systems or frame updates.
        if (layers <= 2)
        {
            Line(mesh, center, size, new Vector2(.40f,.29f), new Vector2(.23f,.11f), .018f, shine);
            Line(mesh, center, size, new Vector2(.23f,.11f), new Vector2(.32f,-.05f), .018f, shine);
            Line(mesh, center, size, new Vector2(.23f,.11f), new Vector2(.08f,.06f), .012f, shine);
        }
        if (layers == 1)
        {
            Line(mesh, center, size, new Vector2(-.42f,-.26f), new Vector2(-.15f,-.17f), .018f, shine);
            Line(mesh, center, size, new Vector2(-.15f,-.17f), new Vector2(-.03f,-.34f), .018f, shine);
            Line(mesh, center, size, new Vector2(-.15f,-.17f), new Vector2(-.10f,.01f), .012f, shine);
        }
        Vector2 flake = new Vector2(-.31f,.31f);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.PI / 3;
            Vector2 arm = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .105f;
            Line(mesh, center, size, flake - arm, flake + arm, .022f, Color.white);
        }
    }

    private static void Line(VertexHelper mesh, Vector2 center, Vector2 size, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
        Quad(mesh, center, size, a - normal, b - normal, b + normal, a + normal, color);
    }

    private static void Triangle(VertexHelper mesh, Vector2 center, Vector2 size, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center + Vector2.Scale(a, size), color, Vector2.zero);
        mesh.AddVert(center + Vector2.Scale(b, size), color, Vector2.zero);
        mesh.AddVert(center + Vector2.Scale(c, size), color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }

    private static void Quad(VertexHelper mesh, Vector2 center, Vector2 size, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center + Vector2.Scale(a, size), color, Vector2.zero);
        mesh.AddVert(center + Vector2.Scale(b, size), color, Vector2.zero);
        mesh.AddVert(center + Vector2.Scale(c, size), color, Vector2.zero);
        mesh.AddVert(center + Vector2.Scale(d, size), color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
