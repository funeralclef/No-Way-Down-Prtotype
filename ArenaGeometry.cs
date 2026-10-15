using Godot;

/// <summary>Builds exactly the footprint used by AI. Entry/exit bridges are outside the sealed fight.</summary>
public static class ArenaGeometry
{
    public static void Build(Node3D root,ArenaLayout layout,float sectionLength,float connectorWidth,Material floor,Color color)
    {
        var ground=new StaticBody3D { Name="ArenaGround",CollisionLayer=4,CollisionMask=0 };root.AddChild(ground);
        var points=new Vector3[layout.Vertices.Length*2];
        for(int i=0;i<layout.Vertices.Length;i++) { var p=layout.Vertices[i];points[i]=new Vector3(p.X,0,p.Y);points[i+layout.Vertices.Length]=new Vector3(p.X,-2,p.Y); }
        ground.AddChild(new CollisionShape3D { Shape=new ConvexPolygonShape3D { Points=points } });
        var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);surface.SetMaterial(floor);
        for(int i=0;i<layout.Vertices.Length;i++) {
            Vector2 a=layout.Vertices[i],b=layout.Vertices[(i+1)%layout.Vertices.Length];
            // Godot uses clockwise front faces.
            foreach(var p in new[]{Vector2.Zero,a,b}) { surface.SetNormal(Vector3.Up);surface.SetUV(p*.05f);surface.AddVertex(new Vector3(p.X,0,p.Y)); }
            foreach(var p in new[]{new Vector3(a.X,0,a.Y),new Vector3(b.X,-2,b.Y),new Vector3(b.X,0,b.Y),new Vector3(a.X,0,a.Y),new Vector3(a.X,-2,a.Y),new Vector3(b.X,-2,b.Y)}) {surface.SetNormal(new Vector3(a.X,0,a.Y).Normalized());surface.SetUV(new Vector2(p.X,p.Z)*.05f);surface.AddVertex(p);}
        }
        ground.AddChild(new MeshInstance3D { Mesh=surface.Commit() });
        var trim=new StandardMaterial3D { AlbedoColor=color.Lightened(.5f),EmissionEnabled=true,Emission=color.Lightened(.3f),EmissionEnergyMultiplier=.24f };
        for(int i=0;i<layout.Vertices.Length;i++) {
            Vector2 a=layout.Vertices[i]*.98f,b=layout.Vertices[(i+1)%layout.Vertices.Length]*.98f;var d=b-a;
            root.AddChild(new MeshInstance3D {Name="ArenaEdge"+i,Position=new Vector3((a.X+b.X)/2,.035f,(a.Y+b.Y)/2),Rotation=new Vector3(0,Mathf.Atan2(d.X,d.Y),0),
                Mesh=new BoxMesh {Size=new Vector3(.1f,.035f,d.Length()*.86f),Material=trim},CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        }
        float inner=layout.Extents.Y-1,edge=sectionLength/2,depth=Mathf.Max(.5f,edge-inner);
        foreach(int side in new[]{-1,1}) {
            var bridge=new StaticBody3D {Name=side<0?"ArenaExitBridge":"ArenaEntryBridge",CollisionLayer=4,CollisionMask=0,Position=new Vector3(0,-1,side*(edge+inner)/2)};
            Vector3 size=new(Mathf.Min(connectorWidth,layout.Extents.X*.9f),2,depth+1);
            bridge.AddChild(new CollisionShape3D { Shape=new BoxShape3D {Size=size} });bridge.AddChild(new MeshInstance3D {Mesh=new BoxMesh {Size=size,Material=floor}});root.AddChild(bridge);
        }
        root.SetMeta("arena_shape",layout.Shape.ToString());
    }
}
