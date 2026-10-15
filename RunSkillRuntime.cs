using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Scene-local device rules and one equipped cast slot. Player owns all movement and resource changes.</summary>
public partial class RunSkillRuntime : Node3D
{
    public RunUpgradeId? Equipped {get;private set;}
    public RunSkillDefinition Skill=>Equipped.HasValue?RunSkillCatalog.Get(Equipped.Value):null;
    public float Cooldown {get;private set;}
    public bool ShieldCharged {get;private set;}
    public bool HasAnchor=>_anchorTime>0;
    public bool DivePending {get;private set;}
    private Player _player;
    private Vector3 _anchor;
    private float _anchorTime,_echoCooldown,_wakeCooldown;
    private bool _hadHeart;
    private int _shieldStomps;
    private MeshInstance3D _shield,_anchorMesh;
    private readonly List<RunSkillTarget> _echoes=new();
    public override void _Ready()
    {
        _player=GetParent<Player>();
        if(!InputMap.HasAction("module_skill")){
            InputMap.AddAction("module_skill");InputMap.ActionAddEvent("module_skill",new InputEventKey {PhysicalKeycode=Key.E});
            InputMap.ActionAddEvent("module_skill",new InputEventJoypadButton {ButtonIndex=JoyButton.X});
        }
        _player.ComboImpact+=OnComboImpact;_player.RiftStepStarted+=OnRift;_player.RunBuffTriggered+=OnDeviceEffect;_player.DaringDodge+=OnDodge;
        _shield=Orb("GlassGuard",1.15f,new Color(.6f,.9f,1,.28f));_shield.Position=Vector3.Up;
        _anchorMesh=new MeshInstance3D {Mesh=new TorusMesh {InnerRadius=.85f,OuterRadius=1.05f,Material=Material(new Color("#bd9dff"))}};
        _anchorMesh.TopLevel=true;AddChild(_anchorMesh);_anchorMesh.Hide();
        Refresh();
    }
    private static StandardMaterial3D Material(Color tint)=>new() {AlbedoColor=tint,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency=BaseMaterial3D.TransparencyEnum.Alpha,EmissionEnabled=true,Emission=tint,EmissionEnergyMultiplier=.5f};
    private MeshInstance3D Orb(string name,float radius,Color tint)
    {var mesh=new MeshInstance3D {Name=name,Mesh=new SphereMesh {Radius=radius,Height=radius*2,RadialSegments=20,Rings=10,Material=Material(tint)},CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(mesh);return mesh;}
    public void Refresh()
    {
        if(Equipped==null||!_player.HasRunUpgrade(Equipped.Value))Equipped=RunSkillCatalog.All.FirstOrDefault(s=>_player.HasRunUpgrade(s.Device))?.Device;
        bool heart=_player.HasRunUpgrade(RunUpgradeId.GlassHeart);
        if(heart&&!_hadHeart){ShieldCharged=true;_shieldStomps=0;}
        if(!heart)ShieldCharged=false;_hadHeart=heart;
        _shield.Visible=ShieldCharged;
    }
    public bool Equip(RunUpgradeId device)
    {if(RunSkillCatalog.Get(device)==null||!_player.HasRunUpgrade(device))return false;Equipped=device;return true;}
    public override void _UnhandledInput(InputEvent inputEvent)
    {if(inputEvent.IsActionPressed("module_skill")&&!inputEvent.IsEcho()&&TryCast())GetViewport().SetInputAsHandled();}
    public override void _PhysicsProcess(double delta)
    {
        float dt=(float)delta;Cooldown=Mathf.Max(0,Cooldown-dt);_anchorTime=Mathf.Max(0,_anchorTime-dt);_echoCooldown=Mathf.Max(0,_echoCooldown-dt);_wakeCooldown=Mathf.Max(0,_wakeCooldown-dt);
        _anchorMesh.Visible=HasAnchor;_anchorMesh.GlobalPosition=_anchor+Vector3.Up*.08f;_anchorMesh.RotateY(dt);
        _shield.Visible=ShieldCharged&&_player.Health>0;
        if(DivePending&&_player.IsSkillGrounded){DivePending=false;Pulse(_player.HasRunUpgrade(RunUpgradeId.EarthSplitter)?10:6,true);}
        _echoes.RemoveAll(e=>!IsInstanceValid(e)||e.IsQueuedForDeletion());
    }
    public bool TryCast()
    {
        if(GetTree().Paused||Skill==null||Cooldown>0||_player.Health<=0||_player.IsInsideTunnel||_player.NitroCharge<Skill.Cost)return false;
        bool success=false;Vector3 origin=_player.GlobalPosition;
        switch(Skill.Command){
            case RunSkillCommand.AirStep:
                success=_player.TrySkillAirStep();
                if(success){if(_player.HasRunUpgrade(RunUpgradeId.PhaseRoute))SpawnEcho(origin);PulseAt(origin,2,new Color("#bda1ff"));}break;
            case RunSkillCommand.FaultDive:success=_player.TrySkillDive();DivePending|=success;break;
            case RunSkillCommand.HuntSpark:success=LaunchDrones();break;
            case RunSkillCommand.ThreadRecall:
                if(!HasAnchor){SetAnchor(origin);_player.AnnounceSkill(RunUpgradeId.RedThread);return true;}
                success=_player.TrySkillTeleport(_anchor+Vector3.Up*.5f,28);
                if(success){_anchorTime=0;if(_player.HasRunUpgrade(RunUpgradeId.ChainReaction))SpawnEcho(origin);PulseAt(origin,2,new Color("#bd9dff"));}break;
        }
        if(!success)return false;
        _player.SpendSkillNitro(Skill.Cost);Cooldown=Skill.Cooldown;_player.AnnounceSkill(Skill.Device);return true;
    }
    private bool LaunchDrones()
    {
        var targets=new List<Mob>();
        foreach(Node node in GetTree().GetNodesInGroup("mob"))if(node is Mob mob&&IsInstanceValid(mob)&&!mob.IsQueuedForDeletion()&&mob is not RunSkillTarget&&
            mob is not BossMob {IsSeal:true}&&!mob.IsPostArenaRetreating&&mob.GlobalPosition.DistanceTo(_player.GlobalPosition)<24&&(mob.CanBeRouteTarget||mob is BossMob {IsSeal:false}))targets.Add(mob);
        targets.Sort((a,b)=>a.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition)));
        int count=Mathf.Min(targets.Count,_player.HasRunUpgrade(RunUpgradeId.TwoHorizons)?2:1);
        for(int i=0;i<count;i++)_player.GetParent().AddChild(new RunSkillDrone {Position=_player.GlobalPosition+Vector3.Up*1.4f,Target=targets[i],OwnerPlayer=_player});
        return count>0;
    }
    private void SetAnchor(Vector3 point){_anchor=point;_anchorTime=5;}
    private void OnRift(bool teleported,Vector3 origin,Vector3 destination)
    {if(teleported&&_player.HasRunUpgrade(RunUpgradeId.RedThread))SetAnchor(origin);}
    public bool TryAbsorbHit()
    {
        if(!ShieldCharged)return false;
        ShieldCharged=false;_shieldStomps=0;_player.GrantDamageInvulnerability(.3f);_player.AnnounceSkill(RunUpgradeId.GlassHeart);
        Pulse(_player.HasRunUpgrade(RunUpgradeId.LastLight)?8:4,_player.HasRunUpgrade(RunUpgradeId.LastLight));return true;
    }
    private void OnComboImpact(int chain,int style,int flow,int archetype,bool counter,bool milestone,bool finisher,Vector3 point)
    {
        if(_hadHeart&&!ShieldCharged&&++_shieldStomps>=3){ShieldCharged=true;_shieldStomps=0;_player.AnnounceSkill(RunUpgradeId.GlassHeart);}
        if(DivePending){DivePending=false;Pulse(_player.HasRunUpgrade(RunUpgradeId.EarthSplitter)?10:6,true);}
        if(_player.HasRunUpgrade(RunUpgradeId.ClosedLoop)&&chain>0&&chain%(_player.HasRunUpgrade(RunUpgradeId.InfiniteLine)?4:5)==0&&_echoCooldown<=0){SpawnEcho(_player.GlobalPosition+_player.SkillHeading*6);_echoCooldown=5;}
    }
    private void OnDodge(float reduced,float boost,bool perfect)
    {if(perfect&&_player.HasRunUpgrade(RunUpgradeId.KineticReserve)){Pulse(5,true);if(_player.HasRunUpgrade(RunUpgradeId.PerfectCircuit)&&_echoCooldown<=0){SpawnEcho(_player.GlobalPosition+_player.SkillHeading*5);_echoCooldown=5;}}}
    private void OnDeviceEffect(int id)
    {
        if((RunUpgradeId)id is RunUpgradeId.Afterburner or RunUpgradeId.BlueComet&&_wakeCooldown<=0){
            _wakeCooldown=1.2f;_player.GetParent().AddChild(new RunWindWake {Position=_player.GlobalPosition,Heading=_player.SkillHeading,Player=_player,Evolved=_player.HasRunUpgrade(RunUpgradeId.BlueComet)});
        }
    }
    private void SpawnEcho(Vector3 point)
    {
        int limit=_player.HasRunUpgrade(RunUpgradeId.InfiniteLine)?2:1;
        _echoes.RemoveAll(e=>!IsInstanceValid(e)||e.IsQueuedForDeletion());if(_echoes.Count>=limit)return;
        var world=GetTree().GetFirstNodeInGroup("procedural_world") as ProceduralWorld;
        if(world?.ArenaEncounterActive==true)point=world.ClampActiveArenaPoint(point,2);
        var floor=PhysicsRayQueryParameters3D.Create(point+Vector3.Up*5,point+Vector3.Down*20,4);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(floor);if(hit.Count==0)return;
        point=hit["position"].AsVector3()+Vector3.Up*.1f;
        var echo=GD.Load<PackedScene>("res://art/skills_echo.tscn").Instantiate<RunSkillTarget>();echo.Initialize(point,Vector3.Forward,0);
        _player.GetParent().AddChild(echo);_echoes.Add(echo);_player.AnnounceSkill(RunUpgradeId.ClosedLoop);PulseAt(point,2,new Color("#a6ffe3"));
    }
    private void Pulse(float radius,bool interrupt)
    {
        PulseAt(_player.GlobalPosition,radius,new Color("#a6ffe3"));
        foreach(Node node in GetTree().GetNodesInGroup("mob"))if(node is Mob mob&&mob is not BossMob&&mob is not RunSkillTarget&&!mob.IsQueuedForDeletion()&&mob.GlobalPosition.DistanceTo(_player.GlobalPosition)<radius)
            mob.ApplySpacingPulse(_player.GlobalPosition,10,.75f);
        if(interrupt)foreach(Node node in GetTree().GetNodesInGroup("hostile_telegraph"))if(node is BossHazard h&&h.OverlapsDisc(_player.GlobalPosition,radius))h.InterruptWarning();
    }
    public bool ResolveDiveImpact()
    {if(!DivePending)return false;DivePending=false;Pulse(_player.HasRunUpgrade(RunUpgradeId.EarthSplitter)?10:6,true);return true;}
    private void PulseAt(Vector3 point,float radius,Color tint){_player.GetParent().AddChild(new RunSkillPulse {Position=point+Vector3.Up*.1f,Radius=radius,Tint=tint});}
    public override void _ExitTree()
    {
        if(IsInstanceValid(_player)){_player.ComboImpact-=OnComboImpact;_player.RiftStepStarted-=OnRift;_player.RunBuffTriggered-=OnDeviceEffect;_player.DaringDodge-=OnDodge;}
        foreach(var echo in _echoes)if(IsInstanceValid(echo))echo.QueueFree();
    }
}
