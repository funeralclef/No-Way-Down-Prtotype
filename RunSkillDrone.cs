using Godot;

/// <summary>A visible homing projectile. Guardians are marked, never damaged through their shield.</summary>
public partial class RunSkillDrone : Node3D
{
    public Mob Target {get;init;}
    public Player OwnerPlayer {get;init;}
    private float _age;
    public override void _Ready()
    {
        var mat=new StandardMaterial3D {AlbedoColor=new Color("#a5e5ff"),EmissionEnabled=true,Emission=new Color("#80d0ff"),EmissionEnergyMultiplier=1};
        AddChild(new MeshInstance3D {Mesh=new SphereMesh {Radius=.24f,Height=.48f,Material=mat}});
        AddChild(new MeshInstance3D {RotationDegrees=new Vector3(40,0,0),Mesh=new TorusMesh {InnerRadius=.35f,OuterRadius=.43f,Material=mat}});
    }
    public override void _PhysicsProcess(double delta)
    {
        _age+=(float)delta;if(_age>3||!IsInstanceValid(Target)||Target.IsQueuedForDeletion()){QueueFree();return;}
        Vector3 goal=Target.GlobalPosition+Vector3.Up*(Target is BossMob?2:1);
        Vector3 next=Position.MoveToward(goal,(float)delta*25);
        if(GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(GlobalPosition,next,12)).Count>0){QueueFree();return;}
        Position=next;RotateY((float)delta*5);
        if(Position.DistanceTo(goal)<.65f){
            if(Target is BossMob boss)boss.Encounter.MarkWeakPoint();else Target.Squash(1,OwnerPlayer);
            GetParent().AddChild(new RunSkillPulse {Position=goal,Radius=2,Tint=new Color("#a5e5ff")});QueueFree();
        }
    }
}
