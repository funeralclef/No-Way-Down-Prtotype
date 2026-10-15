using Godot;
using System;
using System.Collections.Generic;

public enum ArenaShape { Round, Courtyard, LongHall, Hexagon }

/// <summary>Immutable convex play surface shared by geometry, AI, spawn placement and camera.</summary>
public sealed class ArenaLayout
{
    public ArenaShape Shape { get; }
    public string Name => Shape switch { ArenaShape.LongHall => "WIND GALLERY", ArenaShape.Hexagon => "PRISM COURT", ArenaShape.Courtyard => "GARDEN COURT", _ => "RESONANCE RING" };
    public Vector2[] Vertices { get; }
    public float Radius { get; }
    public Vector2 Extents { get; }
    private ArenaLayout(ArenaShape shape, Vector2[] vertices)
    {
        Shape = shape; Vertices = vertices;
        foreach (var v in vertices) { Radius = Mathf.Max(Radius,v.Length()); Extents = Extents.Max(v.Abs()); }
    }
    public static ArenaLayout Create(ArenaShape shape, float radius)
    {
        var points = new List<Vector2>();
        switch(shape) {
            case ArenaShape.Courtyard: return Rectangle(radius * .93f, radius * .92f, shape, radius * .23f);
            case ArenaShape.LongHall: return Rectangle(radius * .68f, radius, shape, radius * .23f);
            case ArenaShape.Hexagon:
                for(int i=0;i<6;i++){float a=Mathf.Tau*i/6;points.Add(new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}
                break;
            default:
                for(int i=0;i<48;i++){float a=Mathf.Tau*i/48;points.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}
                break;
        }
        return new ArenaLayout(shape,points.ToArray());
    }
    public static ArenaLayout ForOrdinal(int ordinal,float radius) => Create((ArenaShape)Mathf.PosMod(ordinal-1,4),radius);
    public static ArenaLayout Rectangle(float halfX,float halfZ,ArenaShape shape=ArenaShape.Courtyard,float bevel=0)
    {
        float b=Mathf.Clamp(bevel,.01f,Mathf.Min(halfX,halfZ)*.45f);
        return new ArenaLayout(shape,new[]{new Vector2(-halfX+b,-halfZ),new Vector2(halfX-b,-halfZ),new Vector2(halfX,-halfZ+b),new Vector2(halfX,halfZ-b),new Vector2(halfX-b,halfZ),new Vector2(-halfX+b,halfZ),new Vector2(-halfX,halfZ-b),new Vector2(-halfX,-halfZ+b)});
    }
    public bool Contains(Vector2 point,float margin=0)
    {
        for(int i=0;i<Vertices.Length;i++) {
            Vector2 a=Vertices[i],edge=Vertices[(i+1)%Vertices.Length]-a;
            Vector2 inward=new Vector2(-edge.Y,edge.X).Normalized();
            if((point-a).Dot(inward)<margin-.001f)return false;
        }
        return true;
    }
    public Vector2 Clamp(Vector2 point,float margin=0)
    {
        // Projection onto convex half-planes converges even near bevel corners.
        for(int pass=0;pass<12;pass++) {
            bool moved=false;
            for(int i=0;i<Vertices.Length;i++) {
                Vector2 a=Vertices[i],edge=Vertices[(i+1)%Vertices.Length]-a;
                Vector2 inward=new Vector2(-edge.Y,edge.X).Normalized();float d=(point-a).Dot(inward);
                if(d<margin){point+=inward*(margin-d+.001f);moved=true;}
            }
            if(!moved)break;
        }
        return point;
    }
    public Vector3 ClampWorld(Vector3 point,Vector3 center,float margin=0)
    {var p=Clamp(new Vector2(point.X-center.X,point.Z-center.Z),margin);return new Vector3(center.X+p.X,point.Y,center.Z+p.Y);}
    public Vector3 Anchor(int index,Vector3 center,float inset=5)
    {
        float a=index*Mathf.Tau/8;
        Vector2 p=Clamp(new Vector2(Mathf.Cos(a)*Extents.X*.7f,Mathf.Sin(a)*Extents.Y*.7f),inset);
        return center+new Vector3(p.X,0,p.Y);
    }
}
