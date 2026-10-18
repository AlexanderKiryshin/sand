using System;
using UnityEngine;
using UnityEngine.UI;

// Decorative scenery shares the same geometry in the shop and the live scene.
public static class ThemeSceneryMesh
{
    private sealed class Painter
    {
        private readonly VertexHelper mesh;
        private readonly Func<float, float, Vector2> point;
        public Painter(VertexHelper mesh, Func<float, float, Vector2> point) { this.mesh=mesh; this.point=point; }
        public void Polygon(Color color, params Vector2[] vertices)
        {
            int start=mesh.currentVertCount;
            foreach(var v in vertices)mesh.AddVert(point(Mathf.Clamp01(v.x),Mathf.Clamp01(v.y)),color,Vector2.zero);
            for(int i=1;i<vertices.Length-1;i++)mesh.AddTriangle(start,start+i,start+i+1);
        }
        public void Box(float x,float y,float w,float h,Color c) => Polygon(c,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
        public void Line(float x,float y,float xx,float yy,float width,Color c)
        {
            Vector2 a=new Vector2(x,y),b=new Vector2(xx,yy),n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Polygon(c,a-n,b-n,b+n,a+n);
        }
        public void Oval(float x,float y,float rx,float ry,Color c)
        {
            const int steps=20;int start=mesh.currentVertCount;
            mesh.AddVert(point(x,y),c,Vector2.zero);
            for(int i=0;i<=steps;i++){float angle=i*Mathf.PI*2/steps;mesh.AddVert(point(Mathf.Clamp01(x+Mathf.Cos(angle)*rx),Mathf.Clamp01(y+Mathf.Sin(angle)*ry)),c,Vector2.zero);}
            for(int i=0;i<steps;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
        }
        public void Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float width,Color color)
        {
            Vector2 previous=a;
            for(int i=1;i<=24;i++)
            {
                float t=i/24f,s=1-t;Vector2 current=s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;
                Line(previous.x,previous.y,current.x,current.y,width,color);previous=current;
            }
        }
    }

    public static void DrawCasing(VertexHelper mesh,Func<float,float,Vector2> point,int theme)
    {
        if(theme<12)return;
        var p=new Painter(mesh,point);Color body=ThemeCatalog.Casing(theme),dark=Color.Lerp(body,Color.black,.48f),shine=Color.Lerp(body,Color.white,.62f);
        p.Box(.08f,.12f,.84f,.77f,dark);p.Box(.10f,.15f,.80f,.71f,body);
        // Contrasting edge channels and inset fasteners make the fascia read as hardware.
        for(int x=0;x<2;x++)for(int y=0;y<2;y++){float u=.15f+x*.7f,v=.22f+y*.57f;p.Oval(u,v,.040f,.018f,dark);p.Oval(u-.008f,v+.003f,.027f,.011f,shine);p.Line(u-.018f,v,u+.018f,v,.006f,dark);}
        if(theme==14 || theme==21){for(int i=0;i<6;i++){float y=.26f+i*.085f;p.Line(.23f,y,.77f,y,.021f,dark);p.Line(.23f,y+.021f,.77f,y+.021f,.009f,shine);}}
        else if(theme==18 || theme==19 || theme==23){for(int i=0;i<3;i++){float y=.30f+i*.18f;p.Line(.21f,y,.45f,y,.024f,dark);p.Line(.45f,y,.55f,y+.08f,.024f,dark);p.Line(.55f,y+.08f,.78f,y+.08f,.024f,dark);p.Oval(.21f,y,.036f,.018f,shine);p.Oval(.78f,y+.08f,.036f,.018f,shine);}}
        else {for(int i=0;i<5;i++)p.Box(.23f+i*.11f,.32f,.045f,.38f,dark);p.Box(.22f,.26f,.57f,.014f,shine);}
        p.Box(.36f,.055f,.28f,.08f,dark);p.Box(.39f,.075f,.22f,.04f,shine);
    }
}
