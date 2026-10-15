using Godot;
using System.Reflection;
using System.Threading.Tasks;

public partial class RunSkillsProbe : Node3D
{
    private int _failures;
    public override async void _Ready()
    {
        var floor=new StaticBody3D {CollisionLayer=4,CollisionMask=0,Position=Vector3.Down*.5f};floor.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(100,1,100)}});AddChild(floor);
        var player=GD.Load<PackedScene>("res://player.tscn").Instantiate<Player>();AddChild(player);player.SetPhysicsProcess(false);
        var skills=player.RunSkills;skills.SetPhysicsProcess(false);
        await PhysicsFrames(2);
        player.GlobalPosition=new Vector3(0,4,0);player.AddNitro(100);
        player.ApplyRunUpgrade(RunUpgradeId.Overpass,1);player.ApplyRunUpgrade(RunUpgradeId.PhaseRoute,1);
        float before=player.NitroCharge;Vector3 origin=player.GlobalPosition;
        Check(skills.TryCast()&&player.GlobalPosition.DistanceTo(origin)>6,"Air Step creates a real free-direction blink");
        Check(player.NitroCharge<before&&skills.Cooldown>0&&!skills.TryCast(),"successful cast pays once and cooldown blocks repeat input");
        Check(GetTree().GetNodesInGroup("mob").Count>0,"Phase Route leaves a friendly Echo Step");
        var echo=GetTree().GetFirstNodeInGroup("mob") as RunSkillTarget;
        Check(echo!=null&&!echo.CanDamagePlayer&&echo.CanBeRouteTarget&&echo.ArenaTargetEligible&&!echo.CountsForEncounter&&!echo.WillBeDefeatedByNextStomp,"echo is a movement target without contact damage, enemy-slot use or kill farming");
        echo?.Squash(1,player);await PhysicsFrames(1);
        skills._PhysicsProcess(6);
        var wall=new StaticBody3D {CollisionLayer=8,Position=player.GlobalPosition+Vector3.Forward*3};wall.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(5,10,1)}});AddChild(wall);await PhysicsFrames(2);
        before=player.NitroCharge;Check(!skills.TryCast()&&Mathf.IsEqualApprox(player.NitroCharge,before),"blocked blink neither crosses a wall nor consumes nitro");wall.QueueFree();
        player.ApplyRunUpgrade(RunUpgradeId.GlassHeart,1);float health=player.Health;
        typeof(Player).GetMethod("UpdateDamageInvulnerability",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{1f});
        var source=new Node3D();AddChild(source);player.TakeDamage(source);
        Check(player.Health==health&&!skills.ShieldCharged,"glass guard absorbs one accepted hit");
        typeof(Player).GetMethod("UpdateDamageInvulnerability",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{1f});player.TakeDamage(source);
        Check(player.Health<health,"spent guard does not make the player permanently invulnerable");
        for(int i=1;i<=3;i++)player.EmitSignal(Player.SignalName.ComboImpact,i,0,0,0,false,false,false,player.GlobalPosition);
        Check(skills.ShieldCharged,"three stomps visibly rebuild the glass guard");
        player.ApplyRunUpgrade(RunUpgradeId.RedThread,1);skills.Equip(RunUpgradeId.RedThread);skills._PhysicsProcess(6);
        origin=player.GlobalPosition;Check(skills.TryCast()&&skills.HasAnchor,"Recall first press plants an anchor");player.GlobalPosition+=Vector3.Right*4;
        Check(skills.TryCast()&&!skills.HasAnchor&&player.GlobalPosition.DistanceTo(origin)<1,"Recall second press returns to the planted position");
        skills._PhysicsProcess(6);player.ApplyRunUpgrade(RunUpgradeId.HunterLens,1);skills.Equip(RunUpgradeId.HunterLens);player.AddNitro(100);
        var target=GD.Load<PackedScene>("res://mob.tscn").Instantiate<Mob>();target.Initialize(player.GlobalPosition+Vector3.Right*8,Vector3.Forward,0);AddChild(target);target.SetPhysicsProcess(false);
        int hits=0;target.Squashed+=_=>hits++;Check(skills.TryCast(),"Hunt Spark creates a projectile with a valid enemy");await PhysicsFrames(70);
        Check(hits==1,"homing drone impacts once and triggers the ordinary enemy reward path");
        skills._PhysicsProcess(6);player.ApplyRunUpgrade(RunUpgradeId.ShockSole,1);skills.Equip(RunUpgradeId.ShockSole);player.AddNitro(100);player.GlobalPosition=new Vector3(0,5,0);player.Velocity=Vector3.Zero;
        var warning=new BossHazard {Kind=BossHazard.ShapeKind.Disc,Position=Vector3.Zero,Target=player,WarningTime=10,Radius=3};AddChild(warning);
        Check(skills.TryCast()&&player.Velocity.Y<-20,"Fault Dive commits an airborne plunge");
        player.SetPhysicsProcess(true);skills.SetPhysicsProcess(true);await PhysicsFrames(120);player.SetPhysicsProcess(false);
        Check(!skills.DivePending&&!IsInstanceValid(warning),"dive impact clears nearby pending attack warnings");
        GetTree().Paused=true;before=player.NitroCharge;Check(!skills.TryCast()&&player.NitroCharge==before,"paused casts cannot spend resources");GetTree().Paused=false;
        GD.Print(_failures==0?"RUN_SKILLS_PROBE: PASSED":$"RUN_SKILLS_PROBE: FAILED ({_failures})");GetTree().Quit(_failures==0?0:1);
    }
    private async Task PhysicsFrames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Check(bool valid,string message){if(!valid){_failures++;GD.PushError("FAIL: "+message);}else GD.Print("PASS: "+message);}
}
