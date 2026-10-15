using Godot;

/// <summary>A single-use friendly stepping stone. Shares target selection, never deals damage or awards a kill.</summary>
public partial class RunSkillTarget : Mob
{
    private float _life=6;
    public override bool CanDamagePlayer=>false;
    public override bool CanBeStomped=>_life>0&&!IsQueuedForDeletion();
    public override bool CanBeRouteTarget=>CanBeStomped;
    public override bool CountsForEncounter=>false;
    public override bool ArenaTargetEligible=>true;
    public override bool WillBeDefeatedByNextStomp=>false;
    public override void _Ready()
    {
        base._Ready();GetNode<AnimationPlayer>("AnimationPlayer").Stop();
        var adapter=GetNode<ModelVisualRoot>("Pivot/Character");adapter.ReplaceModel(GD.Load<PackedScene>("res://art/bosses/seal.tscn"));
        var friendly=new StandardMaterial3D {AlbedoColor=new Color("#7fe6ca"),EmissionEnabled=true,Emission=new Color("#62ceb5"),EmissionEnergyMultiplier=.7f};
        adapter.ApplySurfacePalette(new System.Collections.Generic.Dictionary<string,Material> { ["mob_body"]=friendly,["accent"]=friendly },null);
        AddChild(new MeshInstance3D {Position=Vector3.Up*.09f,Mesh=new TorusMesh {InnerRadius=1.1f,OuterRadius=1.24f,Material=friendly}});
        GetNode<Node3D>("Pivot").Scale=new Vector3(1.2f,.7f,1.2f);
        GetNode<CollisionShape3D>("CollisionShape3D").Shape=new CylinderShape3D {Radius=.95f,Height=.65f};
        GetNode<CollisionShape3D>("CollisionShape3D").Position=Vector3.Up*.5f;
    }
    public override void _PhysicsProcess(double delta){Velocity=Vector3.Zero;_life-=(float)delta;if(_life<=0)QueueFree();}
    protected override void OnVisibleOnScreenNotifierScreenExited(){ }
    public override void Squash(int rewardMultiplier=1,Player stompingPlayer=null)
    {if(!CanBeStomped)return;_life=0;stompingPlayer?.GrantDamageInvulnerability(.15f);QueueFree();}
}
