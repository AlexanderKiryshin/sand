using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class ThemePatternMesh
{
    public static void Draw(VertexHelper vh, Func<float,float,Vector2> point, int theme, int columns=12, int rows=3, float opacity=.45f, float horizontalOffset=0)
    {
        int pattern=ThemeCatalog.Pattern(theme);if(pattern==0)return;
        float offset=Mathf.Repeat(horizontalOffset,1f);
        Color tint=Color.Lerp(ThemeCatalog.Casing(theme),Color.white,.32f);tint.a=opacity;
        if(pattern==1 || pattern==5 || pattern==6)
        {
            int lines=pattern==6?rows*4:rows*2;
            for(int line=-1;line<lines;line++) for(int part=0;part<40;part++)
            {
                float x=part/40f, nx=(part+1)/40f;
                float y=(line+.5f)/lines, ny=y;
                if(pattern==1) { y+=x/lines; ny+=nx/lines; }
                else {y+=Mathf.Sin(x*Mathf.PI*(pattern==6?6:4)+line*.71f)*(.08f/rows);ny+=Mathf.Sin(nx*Mathf.PI*(pattern==6?6:4)+line*.71f)*(.08f/rows);}
                float thickness=pattern==6?.004f:.025f;
                Quad(vh,point,offset,new Vector2(x,y),new Vector2(nx,ny),new Vector2(nx,ny+thickness),new Vector2(x,y+thickness),tint);
            }
            return;
        }
        for(int x=0;x<columns;x++)for(int y=0;y<rows;y++)
        {
            float u=x/(float)columns,v=y/(float)rows,w=1f/columns,h=1f/rows;
            if(pattern>=7)
            {
                DrawMaterial(vh,point,offset,pattern,u,v,w,h,tint,x,y);
                continue;
            }
            if(pattern==2) {if((x+y)%2==0)Quad(vh,point,offset,new Vector2(u,v),new Vector2(u+w,v),new Vector2(u+w,v+h),new Vector2(u,v+h),tint);}
            else if(pattern==3)
            {
                float cx=u+w*.5f,cy=v+h*.5f;
                Quad(vh,point,offset,new Vector2(cx-w*.24f,cy),new Vector2(cx,cy+h*.30f),new Vector2(cx+w*.24f,cy),new Vector2(cx,cy-h*.30f),tint);
                Quad(vh,point,offset,new Vector2(cx-w*.035f,cy-h*.4f),new Vector2(cx+w*.035f,cy-h*.4f),new Vector2(cx+w*.035f,cy+h*.4f),new Vector2(cx-w*.035f,cy+h*.4f),tint);
            }
            else
            {
                Vector2[] outside={new Vector2(u+w*.5f,v),new Vector2(u+w,v+h*.5f),new Vector2(u+w*.5f,v+h),new Vector2(u,v+h*.5f)};
                Vector2 center=new Vector2(u+w*.5f,v+h*.5f);
                for(int i=0;i<4;i++)Quad(vh,point,offset,outside[i],outside[(i+1)%4],Vector2.Lerp(outside[(i+1)%4],center,.1f),Vector2.Lerp(outside[i],center,.1f),tint);
            }
        }
    }
    private static void DrawMaterial(VertexHelper vh,Func<float,float,Vector2> point,float offset,int pattern,float u,float v,float w,float h,Color tint,int column,int row)
    {
        Action<float,float,float,float,float> line=(x,y,xx,yy,width)=>
        {
            Vector2 a=new Vector2(u+x*w,v+y*h),b=new Vector2(u+xx*w,v+yy*h);
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*Mathf.Min(w,h);
            Quad(vh,point,offset,a-n,b-n,b+n,a+n,tint);
        };
        Action<float,float,float,float> box=(x,y,ww,hh)=>Quad(vh,point,offset,new Vector2(u+x*w,v+y*h),new Vector2(u+(x+ww)*w,v+y*h),new Vector2(u+(x+ww)*w,v+(y+hh)*h),new Vector2(u+x*w,v+(y+hh)*h),tint);
        if(pattern==7) // Ferns.
        {
            line(.5f,.1f,.5f,.9f,.025f);
            for(int i=0;i<3;i++){float yy=.25f+i*.22f;line(.5f,yy,.17f,yy+.17f,.05f);line(.5f,yy,.83f,yy+.17f,.05f);}
        }
        else if(pattern==8) // Six-armed snow crystals.
        {
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3,dx=Mathf.Cos(a),dy=Mathf.Sin(a);line(.5f,.5f,.5f+dx*.39f,.5f+dy*.39f,.022f);for(int side=-1;side<=1;side+=2){float aa=a+side*.8f;line(.5f+dx*.24f,.5f+dy*.24f,.5f+dx*.24f-Mathf.Cos(aa)*.12f,.5f+dy*.24f-Mathf.Sin(aa)*.12f,.017f);}}
        }
        else if(pattern==9) // Staggered parquet joints and grain.
        {
            line(0,.02f,1,.02f,.012f);line((row%2)*.5f,0,(row%2)*.5f,1,.02f);
            for(int i=0;i<3;i++)line(.12f,.22f+i*.24f,.85f,.28f+i*.24f,.008f);
        }
        else if(pattern==10) // Rope chevrons.
        {
            for(int i=0;i<3;i++){float yy=i*.32f;line(.05f,yy+.05f,.5f,yy+.28f,.025f);line(.5f,yy+.28f,.95f,yy+.05f,.025f);}
        }
        else if(pattern==11) // Irregular painted tiles.
        {
            box(.13f,.2f,.63f,.15f);box(.25f,.4f,.64f,.18f);box(.1f,.67f,.62f,.12f);
        }
        else if(pattern==12) // Constellation tracks.
        {
            line(.18f,.27f,.52f,.73f,.012f);line(.52f,.73f,.87f,.43f,.012f);
            box(.13f,.22f,.1f,.1f);box(.47f,.68f,.1f,.1f);box(.82f,.38f,.1f,.1f);
        }
        else if(pattern==13) // Diamond tread, alternating direction.
        {
            line(.2f,.15f,.55f,.5f,.055f);line(.55f,.5f,.2f,.85f,.055f);line(.7f,.15f,.95f,.4f,.045f);
        }
        else if(pattern==14) // Printed circuits.
        {
            line(0,.25f,.4f,.25f,.02f);line(.4f,.25f,.7f,.55f,.02f);line(.7f,.55f,1,.55f,.02f);box(.12f,.16f,.18f,.18f);
            line(.1f,.85f,.45f,.85f,.02f);line(.45f,.85f,.6f,.7f,.02f);box(.56f,.65f,.12f,.12f);
        }
        else if(pattern==15) // Tessellated ceramic inlay.
        {
            Vector2 center=new Vector2(u+w*.5f,v+h*.5f);
            Vector2[] diamond={new Vector2(u+w*.5f,v+h*.08f),new Vector2(u+w*.92f,v+h*.5f),new Vector2(u+w*.5f,v+h*.92f),new Vector2(u+w*.08f,v+h*.5f)};
            for(int i=0;i<4;i++)Quad(vh,point,offset,diamond[i],diamond[(i+1)%4],Vector2.Lerp(diamond[(i+1)%4],center,.23f),Vector2.Lerp(diamond[i],center,.23f),tint);
            box(.44f,.44f,.12f,.12f);
        }
        else if(pattern==16) // Bamboo segments.
        {
            line(.2f,0,.2f,1,.045f);line(.7f,0,.7f,1,.045f);box(.10f,.17f,.20f,.07f);box(.60f,.66f,.20f,.07f);
        }
        else if(pattern==17) // Hexagonal honeycomb.
        {
            Vector2[] hex=new Vector2[6];for(int i=0;i<6;i++){float a=i*Mathf.PI/3;hex[i]=new Vector2(u+w*(.5f+Mathf.Cos(a)*.46f),v+h*(.5f+Mathf.Sin(a)*.46f));}
            Vector2 center=new Vector2(u+w*.5f,v+h*.5f);
            for(int i=0;i<6;i++)Quad(vh,point,offset,hex[i],hex[(i+1)%6],Vector2.Lerp(hex[(i+1)%6],center,.08f),Vector2.Lerp(hex[i],center,.08f),tint);
        }
        else // Drafting grid and dimension marks.
        {
            line(0,0,1,0,.012f);line(0,0,0,1,.012f);box(.18f,.2f,.4f,.012f);box(.18f,.2f,.012f,.55f);
            for(int i=0;i<4;i++)line(.2f+i*.14f,.16f,.2f+i*.14f,.27f,.014f);
            line(.75f,.4f,.75f,.85f,.015f);line(.65f,.5f,.85f,.5f,.015f);
        }
    }
    // Clip repeated material tiles before projecting them onto the deck; motifs never spill onto rails.
    private static readonly List<Vector2> clipA=new List<Vector2>(8), clipB=new List<Vector2>(8);
    private static void Quad(VertexHelper vh,Func<float,float,Vector2> point,float offset,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        for(int tile=offset==0?0:-1;tile<=0;tile++)
        {
            Vector2 shift=new Vector2(offset+tile,0);
            clipA.Clear();clipA.Add(a+shift);clipA.Add(b+shift);clipA.Add(c+shift);clipA.Add(d+shift);
            var input=clipA;var output=clipB;
            for(int edge=0;edge<4 && input.Count>0;edge++)
            {
                output.Clear();Vector2 previous=input[input.Count-1];bool previousInside=Inside(previous,edge);
                foreach(var current in input)
                {
                    bool currentInside=Inside(current,edge);
                    if(currentInside!=previousInside)
                    {
                        bool horizontal=edge<2;float boundary=edge==1||edge==3?1:0;
                        float from=horizontal?previous.x:previous.y,to=horizontal?current.x:current.y;
                        output.Add(Vector2.LerpUnclamped(previous,current,(boundary-from)/(to-from)));
                    }
                    if(currentInside)output.Add(current);
                    previous=current;previousInside=currentInside;
                }
                var swap=input;input=output;output=swap;
            }
            if(input.Count<3)continue;
            int start=vh.currentVertCount;
            foreach(var vertex in input)vh.AddVert(point(vertex.x,vertex.y),color,Vector2.zero);
            for(int i=1;i<input.Count-1;i++)vh.AddTriangle(start,start+i,start+i+1);
        }
    }
    private static bool Inside(Vector2 p,int edge) => edge==0?p.x>=0:edge==1?p.x<=1:edge==2?p.y>=0:p.y<=1;
}
