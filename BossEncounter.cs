using Godot;
using System;
using System.Collections.Generic;

/// <summary>Guardian lifecycle and readable openings. Policy, attacks and arena geometry are separate collaborators.</summary>
public partial class BossEncounter : Node3D
{
    public event Action Defeated;
    public event Action<int> PhaseChanged;
    public BossDefinition Definition {get;private set;}
    public BossMob Core {get;private set;}
    public int Health {get;private set;}=8;
    public int Phase=>Health>4?1:2;
    public bool IsActive {get;private set;}
    public bool IsExposed=>IsActive&&_state==State.Exposed;
    public bool IsCharging=>IsActive&&_state==State.Charge;
    public int RequiredSeal=>_requiredSeal;
    public string Instruction {get;private set;}="";
    public float StateTimeRemaining=>_timer;
    public BossPattern CurrentPattern=>_plan.Pattern;
    public bool IsPerformingPattern => _state is State.Pattern or State.Aim or State.Charge or State.Seals;
    public bool IsMarked => _marked;
    public Vector3 CameraFocus => _state == State.Seals && _seals.Find(s => s.SealIndex == _requiredSeal) is BossMob seal
        ? Core.GlobalPosition.Lerp(seal.GlobalPosition, .5f) : Core.GlobalPosition;
    public ArenaLayout Layout {get;private set;}
    private enum State {Intro,Reposition,Pattern,Aim,Charge,Seals,Exposed,Recover,Transition,Defeated}
    private State _state;
    private Player _player;
    private BossHud _hud;
    private BossBrain _brain;
    private BossAttacks _attacks;
    private ArenaTactics _tactics;
    private BossPlan _plan;
    private Vector3 _center,_motionGoal,_chargeFrom,_chargeTo,_lastPlayerPoint;
    private float _radius,_timer,_elapsed,_stillTime;
    private int _charges,_cycle,_requiredSeal,_sealsBroken,_encounterId,_sealShift;
    private bool _extended;
    private bool _marked;
    private readonly List<BossMob> _seals=new();
    private readonly List<MeshInstance3D> _markers=new();
    private PackedScene _targetScene;
    private StandardMaterial3D _auraMaterial;
    private MeshInstance3D _aura;
    public void Begin(BossId id,Player player,Vector3 center,float radius,Control interfaceRoot,int encounterId,ArenaLayout layout=null,ArenaTactics tactics=null,ulong seed=17)
    {
        Definition=BossCatalog.Get(id);_player=player;_center=center;_radius=Mathf.Max(9,radius-3);Layout=layout??ArenaLayout.Create(ArenaShape.Round,radius);
        _tactics=tactics;_encounterId=encounterId;_brain=new BossBrain(id,seed+(ulong)id*997);
        _targetScene=GD.Load<PackedScene>("res://art/bosses/boss_target.tscn");Health=8;IsActive=true;
        Core=CreateTarget(Layout.Anchor(5,center,7),-1);_motionGoal=Core.GlobalPosition;_lastPlayerPoint=player.GlobalPosition;
        _attacks=new BossAttacks {Name="Attacks",Target=player,Tint=Definition.Color};AddChild(_attacks);
        _auraMaterial=new StandardMaterial3D {Transparency=BaseMaterial3D.TransparencyEnum.Alpha,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,AlbedoColor=new Color(Definition.Color,.2f)};
        _aura=new MeshInstance3D {Mesh=new TorusMesh {InnerRadius=2.25f,OuterRadius=2.42f,Rings=48},MaterialOverride=_auraMaterial,Scale=new Vector3(1,.12f,1),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(_aura);
        _hud=GD.Load<PackedScene>("res://ui/boss_hud.tscn").Instantiate<BossHud>();_hud.Encounter=this;interfaceRoot.AddChild(_hud);
        SetState(State.Intro,2.8f,Definition.Lesson);player.GrantDamageInvulnerability(2.8f);player.RewardDangerousSection(18);
    }
    private BossMob CreateTarget(Vector3 point,int seal)
    {
        var target=_targetScene.Instantiate<BossMob>();target.Name=seal<0?"Guardian":"Seal"+seal;target.Encounter=this;target.SealIndex=seal;
        target.ConfigureArchetype(EnemyArchetypeId.Relay);target.ConfigureEncounter(_encounterId);target.Initialize(point,Vector3.Back,0);AddChild(target);return target;
    }
    public bool CanStomp(BossMob target)=>IsActive&&(target.IsSeal?_state==State.Seals&&target.SealIndex==_requiredSeal&&_seals.Contains(target):IsExposed);
    public void ReceiveStomp(BossMob target,Player player)
    {
        if(!CanStomp(target))return;
        if(target.IsSeal){
            _seals.Remove(target);target.QueueFree();_sealsBroken++;player?.RewardDangerousSection(9);
            if(_sealsBroken>=Phase){ClearSeals();Expose();}
            else{_requiredSeal=(_requiredSeal+1)%3;_sealShift=_plan.Pattern==BossPattern.MirrorExchange?3:0;_motionGoal=Layout.Anchor(_cycle+4,_center,7);Instruction="FOLLOW THE NEXT SEAL · THE WEAVER CHANGES SIDES";}
            return;
        }
        int previousPhase=Phase;Health--;player?.RewardDangerousSection(12);GamepadInput.Rumble(.2f,.5f,.12f);_attacks.Clear();Core.SetMotion(Vector3.Zero);
        if(Health==0){IsActive=false;_state=State.Defeated;Instruction="GUARDIAN QUIETED";Core.CollisionLayer=0;Core.CollisionMask=0;_hud.Hide();ClearSeals();Core.Hide();_aura.Hide();Defeated?.Invoke();return;}
        if(previousPhase!=Phase){ClearSeals();_tactics?.ResetPosts();SetState(State.Transition,2.6f,"PHASE II · "+Definition.PhaseTwoLesson);_player.GrantDamageInvulnerability(2.6f);PhaseChanged?.Invoke(Phase);}
        else SetState(State.Recover,.8f,"CROWN CLOSED · WATCH THE NEXT MOVE");
    }
    public void MarkWeakPoint(){ if (IsExposed) ExtendOpening(1.5f); else _marked = true; }
    public void ExtendOpening(float seconds)
    {if(IsExposed&&!_extended){_timer+=Mathf.Clamp(seconds,0,1.5f);_extended=true;}}
    public override void _PhysicsProcess(double delta)
    {
        if(!IsActive||!IsInstanceValid(_player)||_player.Health<=0)return;
        float dt=Mathf.Max(0,(float)delta);_timer-=dt;_elapsed+=dt;
        Vector3 travel=_player.GlobalPosition-_lastPlayerPoint;travel.Y=0;_stillTime=travel.Length()<dt*.8f?_stillTime+dt:0;_lastPlayerPoint=_player.GlobalPosition;
        _attacks.Tick(dt);
        Vector3 before=Core.GlobalPosition;
        if(_state==State.Charge)Core.GlobalPosition=_chargeFrom.Lerp(_chargeTo,Mathf.Clamp(1-_timer/.65f,0,1));
        else if(_state==State.Reposition)Core.GlobalPosition=before.MoveToward(_motionGoal,dt*(Phase==2?11:9));
        else if(_state is State.Pattern or State.Seals)Core.GlobalPosition=before.MoveToward(_motionGoal,dt*(Definition.Id==BossId.Prismweaver?3.2f:2.6f));
        Core.SetMotion(dt>.001f?(Core.GlobalPosition-before)/dt:Vector3.Zero);
        _aura.GlobalPosition=Core.GlobalPosition+new Vector3(0,.08f,0);
        _auraMaterial.AlbedoColor=new Color(IsExposed?new Color("#a6ffe3"):Definition.Color,(IsExposed?.75f:.2f)+.07f*Mathf.Sin(_elapsed*5));
        Vector3 facing=_player.GlobalPosition-Core.GlobalPosition;facing.Y=0;
        if(facing.LengthSquared()>.1f)Core.GetNode<Node3D>("Pivot").Rotation=new Vector3(0,Mathf.Atan2(-facing.X,-facing.Z),0);
        UpdateSeals(dt);
        if(_state==State.Reposition&&Core.GlobalPosition.DistanceTo(_motionGoal)<.25f)_timer=0;
        if(_timer>0)return;
        switch(_state){
            case State.Intro:case State.Recover:case State.Transition:StartCycle();break;
            case State.Reposition:StartPattern();break;
            case State.Pattern:Expose();break;
            case State.Aim:SetState(State.Charge,.65f,"DODGE THE LOCKED STRIPE");break;
            case State.Charge:
                Core.GlobalPosition=_chargeTo;Core.SetMotion(Vector3.Zero);_charges++;
                if(_tactics?.TryBreakPost(_chargeTo)==true){Expose(6.4f);Instruction="RESONATOR BROKEN · EXTENDED OPENING";}
                else if(Phase==2&&_charges<2)AimCharge();else Expose();break;
            case State.Seals:FirePrismLanes();_timer=4.5f;break;
            case State.Exposed:_attacks.Clear();SetState(State.Recover,.8f,"MISSED OPENING · FOLLOW THE NEXT POSITION");break;
        }
    }
    private void StartCycle()
    {
        ClearSeals();_attacks.Clear();_cycle++;_charges=0;
        _plan=_brain.Choose(new BossObservation(Core.GlobalPosition,_player.GlobalPosition,_player.Velocity,_stillTime,Phase),Layout,_center);
        _motionGoal=_plan.Destination;
        SetState(State.Reposition,Mathf.Clamp(Core.GlobalPosition.DistanceTo(_motionGoal)/9+.3f,.7f,3.8f),"GUARDIAN REPOSITIONING · FOLLOW THE CROWN");
    }
    private void StartPattern()
    {
        _motionGoal=Layout.ClampWorld(Core.GlobalPosition+new Vector3(_plan.Handedness*6,0,3),_center,6);
        switch(_plan.Pattern){
            case BossPattern.ResonantMarch:
                _attacks.Ring(Core.GlobalPosition,_radius*2);
                if(Phase==2){Vector3 echo=Core.GlobalPosition;_attacks.After(1.25f,()=>_attacks.Ring(echo,_radius*2,.9f));QueueMarkedSlam(1.6f);}
                SetState(State.Pattern,Phase==2?5.5f:4.2f,"MOVING CHIMES · JUMP THE RIMS, LEAVE THE MARK");break;
            case BossPattern.ChimeFan:
                FireFan(Definition.Id==BossId.Bellwarden?3:2);if(Phase==2)_attacks.After(1.5f,()=>_attacks.Ring(Core.GlobalPosition,_radius*1.6f));
                SetState(State.Pattern,Phase==2?4.8f:3.4f,"CHIME FAN · CROSS A STRIPE BEFORE IT FIRES");break;
            case BossPattern.EchoChase:
                _motionGoal=Layout.ClampWorld(_player.GlobalPosition,_center,6);_motionGoal=new Vector3(_motionGoal.X,0,_motionGoal.Z);
                _attacks.Ring(Core.GlobalPosition,_radius*1.8f);_attacks.After(1.3f,()=>_attacks.Ring(Core.GlobalPosition,_radius*1.8f));if(Phase==2)QueueMarkedSlam(1.8f);
                SetState(State.Pattern,5,"ECHO CHASE · MOVE ACROSS THE WAVES");break;
            case BossPattern.HornRush:AimCharge();break;
            case BossPattern.ThornGarden:
                for(int i=0;i<(Phase==2?4:3);i++)QueueMarkedSlam(i*.55f,Phase==2?1.4f:.7f);
                SetState(State.Pattern,4.6f,"THORN GARDEN · KEEP MOVING, MARKS STOP TRACKING");break;
            case BossPattern.BrambleSweep:
                FireFan(3);if(Phase==2)QueueMarkedSlam(1.3f);
                SetState(State.Pattern,3.6f,"BRAMBLE SWEEP · JUMP OR CUT BETWEEN LANES");break;
            default:BeginSeals();break;
        }
    }
    private void QueueMarkedSlam(float delay,float linger=.55f)
    {_attacks.After(delay,()=>{Vector3 point=_player.GlobalPosition;point.Y=0;_attacks.Disc(point,3,1.2f,linger);});}
    private void FireFan(int count)
    {
        Vector3 from=Core.GlobalPosition,aim=_player.GlobalPosition-from;aim.Y=0;if(aim.IsZeroApprox())aim=Vector3.Back;aim=aim.Normalized();
        for(int i=0;i<count;i++) {Vector3 to=Layout.ClampWorld(from+aim.Rotated(Vector3.Up,(i-(count-1)/2f)*.52f)*_radius*2,_center,1);
            _attacks.Lane(from,to,1.15f+i*.22f,.55f,2.2f);}
    }
    private void AimCharge()
    {
        _chargeFrom=Core.GlobalPosition;Vector3 aim=_player.GlobalPosition;aim.Y=0;Vector3 direction=aim-_chargeFrom;
        if(direction.IsZeroApprox())direction=Vector3.Back;
        _chargeTo=Layout.ClampWorld(aim+direction.Normalized()*6,_center,3);
        _attacks.Lane(_chargeFrom,_chargeTo,1.15f,.65f,3.2f);SetState(State.Aim,1.15f,_charges==0?"BAIT THE RUSH INTO A RESONATOR · STEP ASIDE":"SECOND RUSH · WAIT FOR THE STAGGER");
    }
    private void BeginSeals()
    {
        _sealsBroken=0;_sealShift=0;_requiredSeal=_cycle%3;
        for(int i=0;i<3;i++) {
            Vector3 point=Layout.Anchor(i*2+_cycle,_center,8);_seals.Add(CreateTarget(point,i));
            var marker=new MeshInstance3D {Mesh=new TorusMesh {InnerRadius=1,OuterRadius=1.22f},MaterialOverride=new StandardMaterial3D {ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,AlbedoColor=new Color("#a6ffe3")}};
            AddChild(marker);_markers.Add(marker);
        }
        _motionGoal=Layout.Anchor(_cycle+5,_center,7);FirePrismLanes();
        SetState(State.Seals,4.5f,Phase==1?"ORBITING SEALS · STOMP THE MARKED CRYSTAL":"TWO SEALS IN ORDER · FOLLOW THE MOVING LIGHT");
    }
    private void UpdateSeals(float dt)
    {
        foreach(var seal in _seals) {
            int i=seal.SealIndex;
            Vector3 goal=Layout.Anchor(i*2+_cycle+_sealShift,_center,8)+new Vector3(Mathf.Cos(_elapsed*.65f+i),0,Mathf.Sin(_elapsed*.65f+i))*2;
            if(_plan.Pattern==BossPattern.MirrorExchange)goal+=new Vector3(0,0,Mathf.Sin(_elapsed*.8f)*2);
            goal=Layout.ClampWorld(goal,_center,3);Vector3 before=seal.GlobalPosition;
            seal.GlobalPosition=before.MoveToward(goal,dt*(_sealShift>0?4.5f:1.4f));seal.SetMotion(dt>.001f?(seal.GlobalPosition-before)/dt:Vector3.Zero);
            if(i<_markers.Count&&IsInstanceValid(_markers[i])){_markers[i].GlobalPosition=seal.GlobalPosition+Vector3.Up*.08f;_markers[i].Scale=new Vector3(1.2f+Mathf.Sin(_elapsed*5)*.1f,.12f,1.2f);_markers[i].Visible=i==_requiredSeal;}
        }
        for(int i=0;i<_markers.Count;i++)if(!_seals.Exists(s=>s.SealIndex==i))_markers[i].Hide();
    }
    private void FirePrismLanes()
    {
        float angle=_plan.Pattern==BossPattern.PrismLattice?Mathf.Pi/4:(_cycle%4)*Mathf.Pi/4;
        Vector3 direction=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
        Vector3 from=Layout.ClampWorld(_center-direction*_radius*2,_center,1),to=Layout.ClampWorld(_center+direction*_radius*2,_center,1);
        _attacks.Lane(from,to,1.5f,.7f,2);
        if (_plan.Pattern==BossPattern.PrismLattice && Phase==2) QueueMarkedSlam(1.4f);
        if(Phase==2)_attacks.After(.9f,()=>{var d=direction.Rotated(Vector3.Up,Mathf.Pi/2);_attacks.Lane(Layout.ClampWorld(_center-d*_radius*2,_center,1),Layout.ClampWorld(_center+d*_radius*2,_center,1),1.25f,.7f,2);});
    }
    private void Expose(float duration=5.2f)
    {_attacks.Clear();_extended=false;Core.SetMotion(Vector3.Zero);duration += _marked ? 1.5f : 0;_marked=false;SetState(State.Exposed,duration,"CROWN OPEN · JUMP, RIFT OR USE YOUR SKILL");_player.RewardDangerousSection(5);}
    private void SetState(State state,float duration,string instruction){_state=state;_timer=duration;Instruction=instruction;Core?.PresentCrown(state==State.Exposed,Phase);}
    private void ClearSeals(){foreach(var s in _seals)if(IsInstanceValid(s))s.QueueFree();foreach(var m in _markers)if(IsInstanceValid(m))m.QueueFree();_seals.Clear();_markers.Clear();}
    public void Stop(){IsActive=false;_attacks?.Clear();ClearSeals();if(IsInstanceValid(_hud))_hud.Hide();if(IsInstanceValid(Core)){Core.SetMotion(Vector3.Zero);Core.CollisionLayer=0;Core.CollisionMask=0;Core.RemoveFromGroup("mob");}}
    public override void _ExitTree(){if(IsInstanceValid(_hud))_hud.QueueFree();}
}
