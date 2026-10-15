using Godot;

/// <summary>Player remains the authority for movement, resources and damage. Modules request these narrow actions.</summary>
public partial class Player
{
    public RunSkillRuntime RunSkills {get;private set;}
    public bool IsSkillGrounded => IsOnWorldFloor();
    private void InitializeRunSkills(){RunSkills=new RunSkillRuntime {Name="RunSkills"};AddChild(RunSkills);}
    public Vector3 SkillHeading
    {
        get {
            Vector2 input=Input.GetVector("move_left","move_right","move_forward","move_back");
            if(_mobileControls?.MovementVector.LengthSquared()>input.LengthSquared())input=_mobileControls.MovementVector;
            Vector3 heading=new(input.X,0,input.Y);
            if(heading.LengthSquared()<.05f)heading=new Vector3(Velocity.X,0,Velocity.Z);
            if(heading.IsZeroApprox())heading=_lastMoveDirection;
            return heading.IsZeroApprox()?Vector3.Forward:heading.Normalized();
        }
    }
    public bool TrySkillTeleport(Vector3 destination,float range=24)
    {
        if(_isDead||IsInsideTunnel||GlobalPosition.DistanceTo(destination)>range)return false;
        var world=GetTree().GetFirstNodeInGroup("procedural_world") as ProceduralWorld;
        if(world?.ArenaEncounterActive==true&&!world.ContainsActiveArenaPoint(destination,1.5f))return false;
        if(!IsRiftPathSafe(destination))return false;
        var shape=GetNode<CollisionShape3D>("CollisionShape3D");
        var query=new PhysicsShapeQueryParameters3D {Shape=shape.Shape,Transform=new Transform3D(GlobalBasis,destination+shape.Position),CollisionMask=12,
            Exclude=new Godot.Collections.Array<Rid>{GetRid()}};
        if(GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count>0)return false;
        var floor=PhysicsRayQueryParameters3D.Create(destination+Vector3.Up*2,destination+Vector3.Down*30,4);
        if(GetWorld3D().DirectSpaceState.IntersectRay(floor).Count==0)return false;
        GlobalPosition=destination;GrantDamageInvulnerability(.22f);_mobBounceFloorIgnoreTimer=.15f;return true;
    }
    public bool TrySkillAirStep()
    {
        Vector3 destination=GlobalPosition+SkillHeading*7+Vector3.Up*.8f;
        if(!TrySkillTeleport(destination,9))return false;
        Velocity=new Vector3(SkillHeading.X*CurrentRunSpeed,Mathf.Max(Velocity.Y,3.5f),SkillHeading.Z*CurrentRunSpeed);
        _playerJumpActive=true;return true;
    }
    public bool TrySkillDive()
    {
        if(_isDead||IsInsideTunnel||IsOnWorldFloor()||GlobalPosition.Y<.8f)return false;
        Velocity=new Vector3(Velocity.X*.35f,-24,Velocity.Z*.35f);_playerJumpActive=false;return true;
    }
    public bool SpendSkillNitro(float cost)
    {if(_isDead||NitroCharge<cost)return false;AddNitro(-cost);return true;}
    public void AnnounceSkill(RunUpgradeId id){EmitSignal(SignalName.RunBuffTriggered,(int)id);}
}
