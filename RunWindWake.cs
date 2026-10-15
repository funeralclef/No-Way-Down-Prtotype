using Godot;

/// <summary>Reactor release leaves an actual reusable lane; its evolved form also clears nearby warnings.</summary>
public partial class RunWindWake : Node3D
{
    public Player Player {get;init;}
    public Vector3 Heading {get;init;}
    public bool Evolved {get;init;}
    private float _age;
    private bool _used;
    public override void _Ready()
    {
        Rotation=new Vector3(0,Mathf.Atan2(Heading.X,Heading.Z),0);
        var mat=new StandardMaterial3D {ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,AlbedoColor=new Color(.35f,.85f,1,.28f)};
        for(int i=0;i<3;i++)AddChild(new MeshInstance3D {Position=new Vector3(0,.15f,-i*2),RotationDegrees=new Vector3(90,0,0),Mesh=new TorusMesh {InnerRadius=1.1f,OuterRadius=1.23f,Material=mat}});
    }
    public override void _PhysicsProcess(double delta)
    {
        _age+=(float)delta;if(_age>2.8f||!IsInstanceValid(Player)){QueueFree();return;}
        Vector3 local=ToLocal(Player.GlobalPosition);
        if(!_used&&_age>.35f&&Mathf.Abs(local.X)<1.7f&&local.Z<1&&local.Z>-6){_used=true;Player.ApplyTraversalSpeedBoost(4,.7f);Player.AddNitro(3);}
        if(Evolved)foreach(Node n in GetTree().GetNodesInGroup("hostile_telegraph"))if(n is BossHazard h&&h.OverlapsDisc(GlobalPosition,4))h.InterruptWarning();
    }
}
