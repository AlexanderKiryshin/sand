using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class InventoryLoopGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var route = InventoryLoopRoute.Cells;
        var points = new List<Vector2>();
        float radius = MobilePortraitLayout.BucketCellSize * .46f;
        for (int i = 0; i < route.Length; i++)
        {
            Vector2 previous = Point(route[(i + route.Length - 1) % route.Length]), corner = Point(route[i]), next = Point(route[(i + 1) % route.Length]);
            Vector2 incoming = (corner - previous).normalized, outgoing = (next - corner).normalized;
            if (Vector2.Dot(incoming, outgoing) > .99f) { points.Add(corner); continue; }
            Vector2 start = corner - incoming * radius, end = corner + outgoing * radius;
            for (int sample = 0; sample <= 12; sample++)
            {
                float t = sample / 12f;
                points.Add((1-t)*(1-t)*start + 2*(1-t)*t*corner + t*t*end);
            }
        }
        float size = MobilePortraitLayout.BucketCellSize;
        Strip(vh, points, size * .94f, new Color(.12f,.29f,.31f));
        Strip(vh, points, size * .80f, new Color(.43f,.62f,.59f));
        Strip(vh, points, size * .66f, new Color(.29f,.48f,.47f));
        float distance = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a=points[i], b=points[(i+1)%points.Count], direction=(b-a).normalized;
            Vector2 side=new Vector2(-direction.y,direction.x);
            float length=Vector2.Distance(a,b);
            for(float along=18-distance; along<length; along+=18)
                if(along>=0) Line(vh,a+direction*along-side*size*.32f,a+direction*along+side*size*.32f,2,new Color(.19f,.36f,.36f));
            distance=(distance+length)%18;
        }
        for(int i=0;i<route.Length;i++)
        {
            Vector2 a=Point(route[i]),b=Point(route[(i+1)%route.Length]), direction=(b-a).normalized, side=new Vector2(-direction.y,direction.x);
            Vector2 tip=(a+b)*.5f+direction*8;
            int first=vh.currentVertCount; Color tint=new Color(1,.85f,.34f);
            vh.AddVert(tip,tint,Vector2.zero);vh.AddVert(tip-direction*16+side*9,tint,Vector2.zero);vh.AddVert(tip-direction*16-side*9,tint,Vector2.zero);
            vh.AddTriangle(first,first+1,first+2);
        }
    }
    private Vector2 Point(int i)
    {
        float size=MobilePortraitLayout.BucketCellSize, step=size+MobilePortraitLayout.BucketSpacing;
        return new Vector2(rectTransform.rect.xMin+size*.5f+(i%5)*step,rectTransform.rect.yMax-size*.5f-(i/5)*step);
    }
    private static void Strip(VertexHelper vh,List<Vector2> points,float width,Color color)
    {
        int first=vh.currentVertCount;
        for(int i=0;i<points.Count;i++)
        {
            Vector2 incoming=(points[i]-points[(i+points.Count-1)%points.Count]).normalized, outgoing=(points[(i+1)%points.Count]-points[i]).normalized;
            Vector2 a=new Vector2(-incoming.y,incoming.x),b=new Vector2(-outgoing.y,outgoing.x), normal=(a+b).normalized;
            Vector2 offset=normal*width*.5f/Mathf.Max(.5f,Vector2.Dot(normal,b));
            vh.AddVert(points[i]-offset,color,Vector2.zero);vh.AddVert(points[i]+offset,color,Vector2.zero);
        }
        for(int i=0;i<points.Count;i++) {int a=first+i*2,b=first+((i+1)%points.Count)*2;vh.AddTriangle(a,a+1,b+1);vh.AddTriangle(a,b+1,b);}
    }
    private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
    {
        Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int first=vh.currentVertCount;
        vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
        vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first,first+2,first+3);
    }
}
