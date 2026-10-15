using Godot;
using System.Collections.Generic;

/// <summary>Optional helpful routes and breakable charge stops, independent of encounter state.</summary>
public partial class ArenaTactics : Node3D
{
    private readonly List<Node3D> _posts=new();
    public void Build(ArenaLayout layout,Color color)
    {
        var trim=new StandardMaterial3D {AlbedoColor=new Color("#8ae9d2"),EmissionEnabled=true,Emission=new Color("#3caa9b"),EmissionEnergyMultiplier=.65f};
        foreach(int index in new[]{0,3,5}) {
            Vector3 point=layout.Anchor(index,Vector3.Zero,4);
            var post=new Node3D {Name="Resonator"+index,Position=point};AddChild(post);_posts.Add(post);
            post.AddChild(new MeshInstance3D {Position=new Vector3(0,1.3f,0),Mesh=new CylinderMesh {TopRadius=.3f,BottomRadius=.7f,Height=2.6f,Material=new StandardMaterial3D {AlbedoColor=color.Lightened(.2f)}}});
            post.AddChild(new MeshInstance3D {Position=new Vector3(0,2.8f,0),Mesh=new TorusMesh {InnerRadius=.7f,OuterRadius=.86f,Material=trim}});
        }
        foreach(int index in new[]{1,6}) {
            Vector3 point=layout.Anchor(index,Vector3.Zero,6);
            var pad=new KineticJumpPad {Name="ArenaBloom"+index,Position=point+Vector3.Up*.5f,LocalDirection=-point.Normalized(),CollisionLayer=0,CollisionMask=1,BaseForwardSpeed=12,BaseVerticalSpeed=13,BoostDuration=.6f};
            pad.AddChild(new CollisionShape3D {Shape=new CylinderShape3D {Radius=1.8f,Height=1.1f}});
            pad.AddChild(new MeshInstance3D {Name="ImpactPulse",Position=Vector3.Down*.43f,Mesh=new TorusMesh {InnerRadius=1.5f,OuterRadius=1.85f,Material=trim}});AddChild(pad);
            AddChild(new MeshInstance3D {Name="BloomBase"+index,Position=point+Vector3.Up*.06f,Mesh=new CylinderMesh {TopRadius=1.5f,BottomRadius=1.7f,Height=.12f,RadialSegments=24,Material=trim}});
            for(int petal=0;petal<6;petal++) {
                float angle=petal*Mathf.Tau/6;
                AddChild(new MeshInstance3D {Name="BloomPetal"+index+"_"+petal,Position=point+new Vector3(Mathf.Cos(angle)*1.85f,.08f,Mathf.Sin(angle)*1.85f),Rotation=new Vector3(0,-angle,0),Mesh=new PrismMesh {Size=new Vector3(.55f,.12f,.7f),Material=trim}});
            }
        }
    }
    public bool TryBreakPost(Vector3 point)
    {
        foreach(var post in _posts) if(IsInstanceValid(post)&&post.Visible&&post.GlobalPosition.DistanceTo(point)<4.8f) {
            post.Hide();return true;
        }
        return false;
    }
    public void ResetPosts(){foreach(var p in _posts)if(IsInstanceValid(p))p.Show();}
}
