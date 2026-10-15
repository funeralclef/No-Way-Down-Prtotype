using Godot;
using System.Collections.Generic;

public partial class Mob : CharacterBody3D
{
    public static int TrajectoryPreviewPreference { get; private set; } = 2;

    public static void SetTrajectoryPreviewPreference(int level)
    {
        TrajectoryPreviewPreference = Mathf.Clamp(level, 0, 2);
    }

    public enum RegularBehavior
    {
        Pursuer,
        Feinter,
        Wanderer,
        Drifter
    }

    public enum TravelCombatRole
    {
        FreeAgent,
        LaneRunner,
        Sweeper,
        Challenger
    }

    public enum AttackPhase
    {
        Hunting,
        Telegraphing,
        Lunging,
        Recovering
    }

    public enum ArenaMotionStyle
    {
        StraightPass,
        ArcSweep,
        OrbitThenStrike
    }

    public enum JumpReachCue
    {
        None,
        Unreachable,
        OnCourse,
        Reachable
    }

    [Export]
    public int MinSpeed { get; set; } = 10;

    [Export]
    public int MaxSpeed { get; set; } = 18;

    [ExportGroup("Attack steering")]
    [Export(PropertyHint.Range, "0.5,8.0,0.1")]
    public float TurnResponsiveness { get; set; } = 2.8f;

    [Export(PropertyHint.Range, "0.0,0.8,0.05")]
    public float TargetPredictionTime { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "0.0,0.6,0.01")]
    public float WeaveStrength { get; set; } = 0.24f;

    [Export(PropertyHint.Range, "0.2,2.5,0.05")]
    public float WeaveFrequency { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "1.0,8.0,0.1")]
    public float SeparationRadius { get; set; } = 4.2f;

    [Export(PropertyHint.Range, "0.0,2.0,0.05")]
    public float SeparationStrength { get; set; } = 1.1f;

    [ExportGroup("Pursuit lifetime")]
    [Export(PropertyHint.Range, "1.0,12.0,0.25")]
    public float MinPursuitTime { get; set; } = 3.25f;

    [Export(PropertyHint.Range, "2.0,16.0,0.25")]
    public float MaxPursuitTime { get; set; } = 6.75f;

    [Export(PropertyHint.Range, "1.0,2.0,0.05")]
    public float ExitSpeedMultiplier { get; set; } = 1.12f;

    [Export(PropertyHint.Range, "0.5,10.0,0.25")]
    public float ExitTurnResponsiveness { get; set; } = 4.5f;

    [ExportGroup("Behavior mix")]
    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float PursuerWeight { get; set; } = 4.5f;

    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float FeinterWeight { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float WandererWeight { get; set; } = 2.25f;

    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float DrifterWeight { get; set; } = 1.25f;

    [ExportGroup("Wanderer")]
    [Export(PropertyHint.Range, "1.0,12.0,0.25")]
    public float MinWanderTime { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "1.0,16.0,0.25")]
    public float MaxWanderTime { get; set; } = 6.5f;

    [Export(PropertyHint.Range, "0.15,2.0,0.05")]
    public float MinRouteChangeTime { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.2,3.0,0.05")]
    public float MaxRouteChangeTime { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "20.0,175.0,5.0")]
    public float MaxRandomTurnDegrees { get; set; } = 145.0f;

    [Export(PropertyHint.Range, "0.5,12.0,0.25")]
    public float WanderTurnResponsiveness { get; set; } = 5.0f;

    [ExportGroup("Attack coordination")]
    [Export(PropertyHint.Range, "1,8,1")]
    public int MaxCommittedAttackers { get; set; } = 3;

    [Export(PropertyHint.Range, "2.0,12.0,0.25")]
    public float TargetCrowdRadius { get; set; } = 5.5f;

    [Export(PropertyHint.Range, "0.0,3.0,0.1")]
    public float AttackSlotRadius { get; set; } = 1.35f;

    [Export(PropertyHint.Range, "3.0,16.0,0.25")]
    public float HoldingDistance { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.25,1.0,0.05")]
    public float MinHoldingSpeedMultiplier { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.25,1.0,0.05")]
    public float MaxHoldingSpeedMultiplier { get; set; } = 0.78f;

    [Export(PropertyHint.Range, "0.5,5.0,0.25")]
    public float MinHoldingTime { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "0.5,6.0,0.25")]
    public float MaxHoldingTime { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float CrowdedExitChance { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int MaxCrowdDeferrals { get; set; } = 3;

    [Export(PropertyHint.Range, "0.1,1.5,0.05")]
    public float MinCrowdDecisionTime { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.1,2.0,0.05")]
    public float MaxCrowdDecisionTime { get; set; } = 0.6f;

    [ExportGroup("Collision recovery")]
    [Export(PropertyHint.Range, "0.15,1.5,0.05")]
    public float MinAvoidanceTime { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float MaxAvoidanceTime { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0.1,0.8,0.05")]
    public float StuckRecoveryTime { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.4,1.2,0.05")]
    public float AvoidanceSpeedMultiplier { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "2.0,20.0,0.5")]
    public float AvoidanceTurnResponsiveness { get; set; } = 11.0f;

    [ExportGroup("Traffic anticipation")]
    [Export(PropertyHint.Range, "4.0,16.0,0.5")]
    public float TrafficPredictionRadius { get; set; } = 9.5f;

    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float TrafficPredictionHorizon { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0.8,3.0,0.05")]
    public float StrongCollisionDistance { get; set; } = 1.65f;

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float YieldSpeedMultiplier { get; set; } = 0.68f;

    [Export(PropertyHint.Range, "1.0,20.0,0.5")]
    public float TrafficTurnResponsiveness { get; set; } = 8.5f;

    [Export(PropertyHint.Range, "0.05,0.8,0.05")]
    public float TrafficDecisionHoldTime { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "2.0,20.0,0.5")]
    public float StrongImpactSpeed { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float CollisionBehaviorChangeChance { get; set; } = 0.42f;

    [ExportGroup("Obstacle avoidance")]
    [Export(PropertyHint.Layers3DPhysics)]
    public uint ObstacleCollisionMask { get; set; } = 1u << 3;

    [Export(PropertyHint.Range, "4.0,18.0,0.5")]
    public float ObstacleLookAheadDistance { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.25,2.0,0.05")]
    public float ObstacleProbeHeight { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.2,1.5,0.05")]
    public float ObstacleProbeHalfWidth { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "25.0,75.0,1.0")]
    public float ObstacleDetourAngleDegrees { get; set; } = 52.0f;

    [Export(PropertyHint.Range, "0.25,1.5,0.05")]
    public float ObstacleDetourTime { get; set; } = 0.68f;

    [Export(PropertyHint.Range, "0.02,0.3,0.01")]
    public float ObstacleProbeInterval { get; set; } = 0.08f;

    [ExportGroup("Readable attacks")]
    [Export(PropertyHint.Range, "5.0,16.0,0.25")]
    public float AttackTriggerDistance { get; set; } = 10.5f;

    [Export(PropertyHint.Range, "2.0,8.0,0.25")]
    public float AttackMinimumDistance { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0.2,1.2,0.05")]
    public float MinTelegraphTime { get; set; } = 0.42f;

    [Export(PropertyHint.Range, "0.2,1.5,0.05")]
    public float MaxTelegraphTime { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.3,1.5,0.05")]
    public float MinLungeTime { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.3,1.8,0.05")]
    public float MaxLungeTime { get; set; } = 0.86f;

    [Export(PropertyHint.Range, "1.0,2.0,0.05")]
    public float MinLungeSpeedMultiplier { get; set; } = 1.22f;

    [Export(PropertyHint.Range, "1.0,2.2,0.05")]
    public float MaxLungeSpeedMultiplier { get; set; } = 1.38f;

    [Export(PropertyHint.Range, "0.0,4.0,0.1")]
    public float LungeTurnResponsiveness { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0.2,1.5,0.05")]
    public float MinRecoveryTime { get; set; } = 0.48f;

    [Export(PropertyHint.Range, "0.2,1.8,0.05")]
    public float MaxRecoveryTime { get; set; } = 0.72f;

    [Export(PropertyHint.Range, "0.2,1.0,0.05")]
    public float RecoverySpeedMultiplier { get; set; } = 0.58f;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float FeintChance { get; set; } = 0.38f;

    [ExportGroup("Trajectory preview")]
    [Export]
    public bool TrajectoryPreviewEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "3,10,1")]
    public int TrajectoryMarkerCount { get; set; } = 7;

    [Export(PropertyHint.Range, "0.25,0.8,0.05")]
    public float TrajectoryVisibleFraction { get; set; } = 0.58f;

    [Export(PropertyHint.Range, "3.0,16.0,0.5")]
    public float TrajectoryMaxVisibleDistance { get; set; } = 9.0f;

    [Export(PropertyHint.Range, "0.02,0.2,0.01")]
    public float TrajectoryUpdateInterval { get; set; } = 0.06f;

    [ExportGroup("Shell")]
    [Export(PropertyHint.Range, "0.15,0.6,0.01")]
    public float ShellCrackContactGraceTime { get; set; } = 0.34f;

    [Signal]
    public delegate void SquashedEventHandler(int rewardMultiplier);

    public int ColumnId { get; private set; } = -1;
    public int ColumnIndex { get; private set; } = -1;
    public int EncounterId { get; private set; } = -1;
    public bool IsColumnMember => ColumnId >= 0 && ColumnIndex >= 0;
    public bool IsArenaAttackResolved { get; private set; }
    public bool IsPostArenaRetreating { get; private set; }
    public virtual bool CanDamagePlayer =>
        !IsPostArenaRetreating &&
        !IsYieldingToTunnel &&
        !IsArenaAttackResolved &&
        !_shellCrackContactGraceActive;
    public virtual bool CanBeRouteTarget => CanDamagePlayer;
    public virtual bool CountsForEncounter => true;
    public virtual bool ArenaTargetEligible => EncounterId >= 0;
    public virtual float MinimumRiftArrivalHeight => 0;
    public virtual bool CanBeStomped =>
        !_shellCrackContactGraceActive;
    public bool IsYieldingToTunnel => _tunnelYieldTimer > 0.0f;
    public bool IsComboRouteMember => _comboRouteTimer > 0.0f;
    public AttackPhase CurrentAttackPhase { get; private set; } =
        AttackPhase.Hunting;
    public bool IsAttackCommitted =>
        EncounterId < 0 &&
        CurrentAttackPhase == AttackPhase.Lunging;
    public bool IsAttackTelegraphing =>
        EncounterId < 0 &&
        CurrentAttackPhase == AttackPhase.Telegraphing;
    public EnemyArchetypeId ArchetypeId { get; private set; } =
        EnemyArchetypeId.Spear;
    public string ArchetypeName =>
        _archetypeProfile?.DisplayName ?? "SPEAR";
    public float BonusNitroReward =>
        _archetypeProfile?.BonusNitro ?? 0.0f;
    public float BonusHealingReward =>
        _archetypeProfile?.BonusHealing ?? 0.0f;
    public int ArmorLayersRemaining { get; private set; }
    public virtual bool WillBeDefeatedByNextStomp =>
        ArmorLayersRemaining <= 0;
    public int StompScoreMultiplier =>
        Mathf.Max(
            IsAttackCommitted ? 2 : 1,
            _archetypeProfile?.ScoreMultiplier ?? 1);
    public int VisibleTrajectoryMarkerCount { get; private set; }
    public float VisibleTrajectoryLength { get; private set; }
    public float EncounterAge { get; private set; }
    public float SpeedVariationMultiplier { get; private set; } = 1.0f;
    public Vector3 OriginalTravelDirection { get; private set; } = Vector3.Forward;
    public RegularBehavior CurrentRegularBehavior { get; private set; } =
        RegularBehavior.Pursuer;
    public TravelCombatRole CurrentTravelCombatRole { get; private set; } =
        TravelCombatRole.FreeAgent;
    public ArenaMotionStyle CurrentArenaMotionStyle { get; private set; } =
        ArenaMotionStyle.StraightPass;
    public bool IsArenaStrikePending =>
        EncounterId >= 0 &&
        CurrentArenaMotionStyle ==
            ArenaMotionStyle.OrbitThenStrike &&
        !_arenaStrikeLocked;
    public float JumpOpportunityPriority { get; private set; }
    public JumpReachCue CurrentJumpReachCue { get; private set; }

    private Player _target;
    private AnimationPlayer _animationPlayer;
    private ModelVisualRoot _modelVisualRoot;
    private float _cruiseSpeed;
    private float _exitSpeed;
    private float _movementTime;
    private float _weavePhase;
    private float _weaveBias;
    private float _pursuitTimeRemaining;
    private float _wanderTimeRemaining;
    private float _routeChangeTimer;
    private bool _isLeaving;
    private Vector3 _exitDirection;
    private Vector3 _wanderDirection;
    private bool _isHolding;
    private float _holdingTimeRemaining;
    private float _holdingSpeedMultiplier;
    private float _crowdDecisionTimer;
    private int _crowdDeferrals;
    private float _holdingOrbitSign;
    private Vector3 _collisionAvoidanceDirection;
    private float _collisionAvoidanceTimer;
    private float _stuckTimer;
    private int _failedAvoidanceAttempts;
    private float _obstacleProbeTimer;
    private Node3D _activeObstacle;
    private Vector3 _obstacleNearWaypoint;
    private Vector3 _obstacleFarWaypoint;
    private Vector3 _obstacleRouteDirection;
    private Vector3 _obstacleSteeredDirection;
    private bool _obstacleNearWaypointReached;
    private bool _followingObstacleRouteThisFrame;
    private Mob _anticipatedMob;
    private Vector3 _trafficSteeredDirection;
    private float _trafficSpeedMultiplier = 1.0f;
    private float _trafficDecisionTimer;
    private float _courtesy;
    private float _contactReactionCooldown;
    private float _contactMemoryTimer;
    private int _recentContactCount;
    private Mob _contactPartner;
    private float _postContactSeparationTimer;
    private float _groundHeight;
    private float _attackPhaseTimer;
    private float _attackPhaseDuration;
    private float _attackCooldown;
    private Vector3 _lungeDirection;
    private Vector3 _recoveryDirection;
    private float _lungeSpeed;
    private bool _recoveryFromFeint;
    private bool _feintUsed;
    private MeshInstance3D _intentDisc;
    private StandardMaterial3D _intentMaterial;
    private OmniLight3D _intentLight;
    private Node3D _trajectoryPreviewRoot;
    private readonly List<MeshInstance3D> _trajectoryMarkers = new();
    private readonly List<BoxMesh> _trajectoryMarkerMeshes = new();
    private readonly List<StandardMaterial3D> _trajectoryMaterials = new();
    private float _trajectoryUpdateTimer;
    private float _tunnelProbeTimer;
    private float _tunnelYieldTimer;
    private Vector3 _tunnelYieldDirection;
    private float _comboRouteTimer;
    private float _comboRouteWeaveSign;
    private float _travelRoleTimer;
    private float _travelRoleWeaveSign;
    private bool _isTraversalRelayAnchor;
    private Vector3 _traversalRelayAnchor;
    private float _traversalRelayPhase;
    private Vector3 _arenaCenter;
    private float _arenaTurnSign = 1.0f;
    private float _arenaMotionTimer;
    private float _arenaOrbitRadius;
    private bool _arenaStrikeLocked;
    private Vector3 _arenaStrikeOffset;
    private MeshInstance3D _guidanceDisc;
    private StandardMaterial3D _guidanceMaterial;
    private float _guidanceUrgency;
    private float _guidancePulseTime;
    private EnemyArchetypeProfile _archetypeProfile;
    private bool _archetypeTuningApplied;
    private float _archetypeMotionTimer;
    private float _archetypeMotionSign = 1.0f;
    private float _twinPhaseOffset;
    private StandardMaterial3D _archetypeAccentMaterial;
    private bool _isCrackedShell;
    private bool _shellCrackContactGraceActive;
    private float _shellCrackContactGraceTimer;
    private int _shellCrackSeparatedPhysicsFrames;
    private Player _shellCrackStompingPlayer;
    private AudioStreamPlayer3D _archetypeCuePlayer;

    public override void _Ready()
    {
        _animationPlayer =
            GetNodeOrNull<AnimationPlayer>(
                "AnimationPlayer");
        _modelVisualRoot =
            GetNodeOrNull<ModelVisualRoot>(
                "Pivot/Character");
        ApplyArchetypeVisual();
        CreateArchetypeAudio();
        CreateIntentVisual();
        CreateJumpReachVisual();
        CallDeferred(nameof(PlaySpawnCue));
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaSeconds = (float)delta;
        UpdateShellCrackContactGrace(deltaSeconds);
        if (EncounterId >= 0)
        {
            EncounterAge += deltaSeconds;
        }

        // Arena attacks retain their original committed trajectory. Regular
        // enemies hunt only for a limited, individually randomized time.
        bool yieldingToTunnel =
            UpdateTunnelClearance(deltaSeconds);
        if (yieldingToTunnel)
        {
            UpdateTunnelYieldVelocity(deltaSeconds);
        }
        else if (IsPostArenaRetreating)
        {
            UpdateLeavingVelocity(deltaSeconds);
        }
        else if (_isTraversalRelayAnchor)
        {
            UpdateTraversalRelayAnchor(
                deltaSeconds);
        }
        else if (EncounterId < 0 && !IsArenaAttackResolved)
        {
            UpdateRegularBehavior(deltaSeconds);
        }
        else if (EncounterId >= 0 && _cruiseSpeed > 0.01f)
        {
            UpdateArenaMotion(deltaSeconds);
        }

        _collisionAvoidanceTimer = Mathf.Max(
            0.0f,
            _collisionAvoidanceTimer - deltaSeconds);
        _contactReactionCooldown = Mathf.Max(
            0.0f,
            _contactReactionCooldown - deltaSeconds);
        _contactMemoryTimer = Mathf.Max(
            0.0f,
            _contactMemoryTimer - deltaSeconds);
        _postContactSeparationTimer = Mathf.Max(
            0.0f,
            _postContactSeparationTimer - deltaSeconds);
        if (_contactMemoryTimer <= 0.0f)
        {
            _recentContactCount = 0;
        }

        UpdateObstacleAvoidance(deltaSeconds);
        if (_followingObstacleRouteThisFrame &&
            (CurrentAttackPhase == AttackPhase.Telegraphing ||
                CurrentAttackPhase == AttackPhase.Lunging))
        {
            BeginAttackRecovery(false);
        }

        if (!_followingObstacleRouteThisFrame &&
            !yieldingToTunnel &&
            !_isTraversalRelayAnchor)
        {
            bool ownsAuthoredRoute =
                _isCrackedShell ||
                _archetypeProfile?.UsesCourierMotion == true ||
                _archetypeProfile?.UsesShepherdMotion == true ||
                _archetypeProfile?.UsesRelayMotion == true ||
                _archetypeProfile?.UsesTwinMotion == true;
            if (!ownsAuthoredRoute)
            {
                ApplyTrafficAnticipation(deltaSeconds);
            }
            ApplyCollisionAvoidance(deltaSeconds);
        }

        Vector3 frameStartPosition = GlobalPosition;
        Vector3 intendedVelocity = Velocity;
        MoveAndSlide();
        KeepOnGroundPlane();
        UpdateCollisionRecovery(
            frameStartPosition,
            intendedVelocity,
            deltaSeconds);
        UpdateIntentVisual(deltaSeconds);
        UpdateJumpReachVisual(deltaSeconds);
        UpdateTrajectoryPreview(deltaSeconds);
    }

    public override void _ExitTree()
    {
        EndShellCrackContactGrace();
    }

    public void Initialize(Vector3 startPosition, Vector3 playerPosition)
    {
        int randomSpeed = GD.RandRange(MinSpeed, MaxSpeed);
        Vector3 direction = (playerPosition - startPosition).Normalized();
        Initialize(startPosition, direction, randomSpeed);
    }

    public void Initialize(Vector3 startPosition, Vector3 travelDirection, float speed)
    {
        Vector3 direction = travelDirection.IsZeroApprox()
            ? Vector3.Forward
            : travelDirection.Normalized();
        Position = startPosition;
        _groundHeight = startPosition.Y;
        OriginalTravelDirection = new Vector3(direction.X, 0.0f, direction.Z).Normalized();
        _weavePhase = (float)GD.RandRange(0.0f, Mathf.Tau);
        _weaveBias = GD.Randf() < 0.5f ? -1.0f : 1.0f;
        _pursuitTimeRemaining = (float)GD.RandRange(
            Mathf.Min(MinPursuitTime, MaxPursuitTime),
            Mathf.Max(MinPursuitTime, MaxPursuitTime));
        ChooseRegularBehavior();
        if (CurrentRegularBehavior == RegularBehavior.Feinter)
        {
            _pursuitTimeRemaining *= (float)GD.RandRange(0.35f, 0.62f);
        }

        _wanderTimeRemaining = (float)GD.RandRange(
            Mathf.Min(MinWanderTime, MaxWanderTime),
            Mathf.Max(MinWanderTime, MaxWanderTime));
        _routeChangeTimer = 0.0f;
        _wanderDirection = OriginalTravelDirection;
        _isLeaving = false;
        IsPostArenaRetreating = false;
        _exitDirection = Vector3.Zero;
        _isHolding = false;
        _holdingTimeRemaining = 0.0f;
        _holdingSpeedMultiplier = 1.0f;
        _crowdDecisionTimer = (float)GD.RandRange(
            Mathf.Min(MinCrowdDecisionTime, MaxCrowdDecisionTime),
            Mathf.Max(MinCrowdDecisionTime, MaxCrowdDecisionTime));
        _crowdDeferrals = 0;
        _holdingOrbitSign = GD.Randf() < 0.5f ? -1.0f : 1.0f;
        _collisionAvoidanceDirection = Vector3.Zero;
        _collisionAvoidanceTimer = 0.0f;
        _stuckTimer = 0.0f;
        _failedAvoidanceAttempts = 0;
        _anticipatedMob = null;
        _trafficSteeredDirection = Vector3.Zero;
        _trafficSpeedMultiplier = 1.0f;
        _trafficDecisionTimer = 0.0f;
        _courtesy = (float)GD.RandRange(0.0f, 1.0f);
        _contactReactionCooldown = 0.0f;
        _contactMemoryTimer = 0.0f;
        _recentContactCount = 0;
        _contactPartner = null;
        _postContactSeparationTimer = 0.0f;
        CurrentAttackPhase = AttackPhase.Hunting;
        _attackPhaseTimer = 0.0f;
        _attackPhaseDuration = 0.0f;
        _attackCooldown = (float)GD.RandRange(0.25f, 0.75f);
        _lungeDirection = Vector3.Zero;
        _recoveryDirection = Vector3.Zero;
        _lungeSpeed = 0.0f;
        _recoveryFromFeint = false;
        _feintUsed = false;
        _tunnelProbeTimer = (float)GD.RandRange(0.0f, 0.12f);
        _tunnelYieldTimer = 0.0f;
        _tunnelYieldDirection = Vector3.Zero;
        _comboRouteTimer = 0.0f;
        _comboRouteWeaveSign = 1.0f;
        CurrentTravelCombatRole = TravelCombatRole.FreeAgent;
        _travelRoleTimer = 0.0f;
        _travelRoleWeaveSign = 1.0f;
        _isTraversalRelayAnchor = false;
        _traversalRelayAnchor =
            Vector3.Zero;
        _traversalRelayPhase = 0.0f;
        CurrentArenaMotionStyle = ArenaMotionStyle.StraightPass;
        _arenaCenter = Vector3.Zero;
        _arenaTurnSign = 1.0f;
        _arenaMotionTimer = 0.0f;
        _arenaOrbitRadius = 0.0f;
        _arenaStrikeLocked = false;
        _arenaStrikeOffset = Vector3.Zero;
        JumpOpportunityPriority = 0.0f;
        CurrentJumpReachCue = JumpReachCue.None;
        _guidanceUrgency = 0.0f;
        _guidancePulseTime = 0.0f;
        _obstacleProbeTimer = (float)GD.RandRange(
            0.0f,
            Mathf.Max(0.02f, ObstacleProbeInterval));
        ClearObstacleRoute();
        _cruiseSpeed = Mathf.Max(0.0f, speed);
        _exitSpeed = _cruiseSpeed;
        SetTravelVelocity(direction * _cruiseSpeed);
    }

    public void ConfigureArchetype(EnemyArchetypeId archetypeId)
    {
        ArchetypeId = archetypeId;
        _archetypeProfile =
            EnemyArchetypeLibrary.Get(archetypeId);
        ArmorLayersRemaining = Mathf.Max(
            0,
            _archetypeProfile.ArmorLayers);
        _archetypeMotionTimer = (float)GD.RandRange(
            3.5f,
            6.5f);
        _archetypeMotionSign =
            GD.Randf() < 0.5f ? -1.0f : 1.0f;
        ApplyArchetypeTuning();
        if (IsInsideTree())
        {
            ApplyArchetypeVisual();
        }
    }

    private void ApplyArchetypeTuning()
    {
        if (_archetypeProfile == null ||
            _archetypeTuningApplied)
        {
            return;
        }

        _archetypeTuningApplied = true;
        CurrentRegularBehavior =
            _archetypeProfile.BaseBehavior;
        _cruiseSpeed *= Mathf.Max(
            0.1f,
            _archetypeProfile.SpeedMultiplier);
        _exitSpeed = _cruiseSpeed;
        TurnResponsiveness *= Mathf.Max(
            0.1f,
            _archetypeProfile.TurnMultiplier);
        WeaveStrength *= Mathf.Max(
            0.0f,
            _archetypeProfile.WeaveMultiplier);
        MinTelegraphTime *= Mathf.Max(
            0.2f,
            _archetypeProfile.TelegraphTimeMultiplier);
        MaxTelegraphTime *= Mathf.Max(
            0.2f,
            _archetypeProfile.TelegraphTimeMultiplier);
        MinLungeSpeedMultiplier *= Mathf.Max(
            0.2f,
            _archetypeProfile.LungeSpeedMultiplier);
        MaxLungeSpeedMultiplier *= Mathf.Max(
            0.2f,
            _archetypeProfile.LungeSpeedMultiplier);
        FeintChance =
            _archetypeProfile.FeintChance;
        JumpOpportunityPriority = Mathf.Max(
            JumpOpportunityPriority,
            _archetypeProfile.JumpOpportunityPriority);
        SetTravelVelocity(
            OriginalTravelDirection *
            _cruiseSpeed);
    }

    public void ConfigureSpeedVariation(float multiplier)
    {
        SpeedVariationMultiplier = Mathf.Clamp(multiplier, 0.8f, 1.2f);
    }

    public void BeginTunnelYield(
        Vector3 yieldDirection,
        float duration = 1.0f)
    {
        Vector3 flatDirection =
            new(yieldDirection.X, 0.0f, yieldDirection.Z);
        if (flatDirection.IsZeroApprox())
        {
            return;
        }

        _tunnelYieldDirection = flatDirection.Normalized();
        _tunnelYieldTimer = Mathf.Max(
            _tunnelYieldTimer,
            Mathf.Max(0.25f, duration));
        CurrentAttackPhase = AttackPhase.Hunting;
        _attackPhaseTimer = 0.0f;
        _isHolding = false;
    }

    public void ConfigureComboRoute(
        float readableDuration,
        float weaveSign)
    {
        _comboRouteTimer = Mathf.Max(0.5f, readableDuration);
        _comboRouteWeaveSign = Mathf.Sign(weaveSign);
        if (Mathf.IsZeroApprox(_comboRouteWeaveSign))
        {
            _comboRouteWeaveSign = 1.0f;
        }

        CurrentAttackPhase = AttackPhase.Hunting;
        _attackCooldown = _comboRouteTimer;
    }

    public void ConfigureTwinMotion(
        float weaveSign,
        float phaseOffset)
    {
        _archetypeMotionSign =
            Mathf.Sign(weaveSign);
        if (Mathf.IsZeroApprox(_archetypeMotionSign))
        {
            _archetypeMotionSign = 1.0f;
        }

        _twinPhaseOffset = phaseOffset;
        _archetypeMotionTimer =
            Mathf.Max(_archetypeMotionTimer, 5.5f);
    }

    public void ConfigureTravelCombatRole(
        TravelCombatRole role,
        float readableDuration,
        float activationDelay = 0.0f,
        float weaveSign = 1.0f)
    {
        CurrentTravelCombatRole = role;
        _travelRoleTimer = Mathf.Max(0.6f, readableDuration);
        _travelRoleWeaveSign = Mathf.Sign(weaveSign);
        if (Mathf.IsZeroApprox(_travelRoleWeaveSign))
        {
            _travelRoleWeaveSign = 1.0f;
        }

        _isHolding = false;
        CurrentAttackPhase = AttackPhase.Hunting;
        if (role == TravelCombatRole.Challenger)
        {
            // A challenger is the one deliberate attacker in a travel beat:
            // it announces one committed pass, then yields the screen.
            CurrentRegularBehavior =
                ArchetypeId ==
                    EnemyArchetypeId.Trickster
                    ? RegularBehavior.Feinter
                    : RegularBehavior.Pursuer;
            _pursuitTimeRemaining = _travelRoleTimer;
            _attackCooldown = Mathf.Max(0.2f, activationDelay);
            _feintUsed =
                ArchetypeId !=
                    EnemyArchetypeId.Trickster;
            MinTelegraphTime = Mathf.Max(MinTelegraphTime, 0.58f);
            MaxTelegraphTime = Mathf.Max(MaxTelegraphTime, 0.76f);
            AttackTriggerDistance =
                Mathf.Max(AttackTriggerDistance, 12.0f);
            MaxCommittedAttackers =
                Mathf.Min(MaxCommittedAttackers, 2);
            JumpOpportunityPriority =
                Mathf.Max(JumpOpportunityPriority, 0.18f);
        }
        else
        {
            // Route actors remain hazards and stomp targets, but do not
            // abandon the readable formation to home independently.
            _attackCooldown = _travelRoleTimer + 1.0f;
        }
    }

    public void ConfigureArenaMotion(
        ArenaMotionStyle motionStyle,
        Vector3 arenaCenter,
        float turnSign,
        float activationDelay,
        Vector3 strikeOffset = default)
    {
        CurrentArenaMotionStyle = motionStyle;
        _arenaCenter = arenaCenter;
        _arenaCenter.Y = _groundHeight;
        _arenaTurnSign = Mathf.Sign(turnSign);
        if (Mathf.IsZeroApprox(_arenaTurnSign))
        {
            _arenaTurnSign = 1.0f;
        }

        Vector3 radial = GlobalPosition - _arenaCenter;
        radial.Y = 0.0f;
        _arenaOrbitRadius = Mathf.Max(5.0f, radial.Length());
        _arenaMotionTimer = motionStyle switch
        {
            ArenaMotionStyle.ArcSweep => 1.45f,
            ArenaMotionStyle.OrbitThenStrike =>
                Mathf.Max(0.2f, activationDelay),
            _ => 0.0f
        };
        _arenaStrikeLocked = false;
        _arenaStrikeOffset =
            new Vector3(strikeOffset.X, 0.0f, strikeOffset.Z);
    }

    public void ConfigureStompOpportunity(bool enabled)
    {
        JumpOpportunityPriority = enabled
            ? Mathf.Max(
                JumpOpportunityPriority,
                0.38f)
            : 0.0f;
    }

    public void ConfigureTraversalRelayAnchor(
        Vector3 anchorPosition,
        float phaseOffset)
    {
        _isTraversalRelayAnchor = true;
        _traversalRelayAnchor =
            anchorPosition;
        _traversalRelayAnchor.Y =
            _groundHeight;
        _traversalRelayPhase =
            phaseOffset;
        CurrentRegularBehavior =
            RegularBehavior.Wanderer;
        CurrentAttackPhase =
            AttackPhase.Recovering;
        _attackCooldown = 999.0f;
        _pursuitTimeRemaining = 999.0f;
        JumpOpportunityPriority =
            Mathf.Max(
                JumpOpportunityPriority,
                0.92f);
        ClearObstacleRoute();
    }

    private void UpdateTraversalRelayAnchor(
        float deltaSeconds)
    {
        _traversalRelayPhase +=
            deltaSeconds * 1.65f;
        Vector3 target =
            _traversalRelayAnchor +
            new Vector3(
                Mathf.Sin(
                    _traversalRelayPhase) *
                    0.48f,
                0.0f,
                Mathf.Cos(
                    _traversalRelayPhase *
                        0.72f) *
                    0.2f);
        Vector3 offset =
            target -
                GlobalPosition;
        offset.Y = 0.0f;
        Vector3 desiredVelocity =
            offset *
                3.8f;
        desiredVelocity =
            desiredVelocity.LimitLength(
                2.6f);
        Vector3 current =
            Velocity;
        current.X =
            Mathf.MoveToward(
                current.X,
                desiredVelocity.X,
                15.0f *
                    deltaSeconds);
        current.Z =
            Mathf.MoveToward(
                current.Z,
                desiredVelocity.Z,
                15.0f *
                    deltaSeconds);
        current.Y = 0.0f;
        Velocity = current;
        CurrentAttackPhase =
            AttackPhase.Recovering;
    }

    public void SetJumpReachCue(
        JumpReachCue cueType,
        float urgency)
    {
        CurrentJumpReachCue = cueType;
        _guidanceUrgency = Mathf.Clamp(urgency, 0.0f, 1.0f);
    }

    public void ResolvePostHitPlayerContact(
        Vector3 playerPosition,
        Vector3 playerVelocity)
    {
        if (IsPostArenaRetreating ||
            IsArenaAttackResolved ||
            IsYieldingToTunnel)
        {
            return;
        }

        Vector3 away = GlobalPosition - playerPosition;
        away.Y = 0.0f;
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : currentDirection.Normalized();
        if (away.IsZeroApprox())
        {
            away = Vector3.Up.Cross(currentDirection);
        }

        away = away.Normalized();
        Vector3 tangent =
            Vector3.Up.Cross(away).Normalized();
        Vector3 playerHeading =
            new(playerVelocity.X, 0.0f, playerVelocity.Z);
        if (!playerHeading.IsZeroApprox() &&
            tangent.Dot(playerHeading) < 0.0f)
        {
            tangent = -tangent;
        }

        _collisionAvoidanceDirection =
            (away * 0.82f +
                tangent * 0.58f +
                currentDirection * 0.24f)
            .Normalized();
        _collisionAvoidanceTimer = Mathf.Max(
            _collisionAvoidanceTimer,
            0.38f);
        _contactReactionCooldown = Mathf.Max(
            _contactReactionCooldown,
            0.24f);

        if (EncounterId >= 0)
        {
            // Contact has already damaged the player. Spend this pass
            // so it cannot bend back into an immediate repeat hit.
            IsArenaAttackResolved = true;
        }
        else if (CurrentAttackPhase ==
            AttackPhase.Telegraphing)
        {
            BeginAttackRecovery(true);
        }
        else if (CurrentAttackPhase ==
            AttackPhase.Lunging)
        {
            BeginAttackRecovery(false);
        }
    }

    /// <summary>
    /// Requests a brief, AI-owned spacing manoeuvre.  It does not teleport or
    /// directly launch the enemy, so obstacle avoidance and authored arena
    /// motion remain authoritative after a Shock Sole landing.
    /// </summary>
    public void ApplySpacingPulse(
        Vector3 origin,
        float strength,
        float duration)
    {
        if (IsPostArenaRetreating ||
            IsYieldingToTunnel ||
            _isTraversalRelayAnchor)
        {
            return;
        }

        Vector3 away =
            GlobalPosition - origin;
        away.Y = 0.0f;
        Vector3 current = new(
            Velocity.X,
            0.0f,
            Velocity.Z);
        if (away.IsZeroApprox())
        {
            away = Vector3.Up.Cross(
                current.IsZeroApprox()
                    ? OriginalTravelDirection
                    : current);
        }
        away = away.Normalized();
        Vector3 currentHeading =
            current.IsZeroApprox()
                ? away
                : current.Normalized();
        float response = Mathf.Clamp(
            strength / 10.0f,
            0.25f,
            1.0f);
        _collisionAvoidanceDirection =
            (away * Mathf.Lerp(0.72f, 1.0f, response) +
                currentHeading * 0.38f)
            .Normalized();
        _collisionAvoidanceTimer = Mathf.Max(
            _collisionAvoidanceTimer,
            Mathf.Clamp(duration, 0.18f, 0.8f));
        _postContactSeparationTimer = Mathf.Max(
            _postContactSeparationTimer,
            Mathf.Clamp(duration, 0.18f, 0.8f));
        if (CurrentAttackPhase == AttackPhase.Lunging ||
            CurrentAttackPhase == AttackPhase.Telegraphing)
        {
            BeginAttackRecovery(false);
        }
    }

    private void UpdateArenaMotion(float deltaSeconds)
    {
        switch (CurrentArenaMotionStyle)
        {
            case ArenaMotionStyle.ArcSweep:
                UpdateArenaArcSweep(deltaSeconds);
                break;
            case ArenaMotionStyle.OrbitThenStrike:
                UpdateArenaOrbitStrike(deltaSeconds);
                break;
            default:
                SetTravelVelocity(
                    OriginalTravelDirection * _cruiseSpeed);
                break;
        }
    }

    private void UpdateArenaArcSweep(float deltaSeconds)
    {
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : currentDirection.Normalized();
        if (_arenaMotionTimer <= 0.0f)
        {
            OriginalTravelDirection = currentDirection;
            CurrentArenaMotionStyle =
                ArenaMotionStyle.StraightPass;
            SetTravelVelocity(
                OriginalTravelDirection * _cruiseSpeed);
            return;
        }

        _arenaMotionTimer = Mathf.Max(
            0.0f,
            _arenaMotionTimer - deltaSeconds);
        float turnRate =
            Mathf.DegToRad(
                Mathf.Lerp(19.0f, 27.0f, _courtesy));
        Vector3 curvedDirection = currentDirection
            .Rotated(
                Vector3.Up,
                turnRate * _arenaTurnSign * deltaSeconds)
            .Normalized();
        SetTravelVelocity(curvedDirection * _cruiseSpeed);
    }

    private void UpdateArenaOrbitStrike(float deltaSeconds)
    {
        if (_arenaMotionTimer > 0.0f)
        {
            _arenaMotionTimer = Mathf.Max(
                0.0f,
                _arenaMotionTimer - deltaSeconds);
            Vector3 radial = GlobalPosition - _arenaCenter;
            radial.Y = 0.0f;
            if (radial.IsZeroApprox())
            {
                radial = Vector3.Right;
            }

            float distance = radial.Length();
            Vector3 radialDirection = radial / distance;
            Vector3 tangent =
                Vector3.Up.Cross(radialDirection) *
                _arenaTurnSign;
            float radiusCorrection = Mathf.Clamp(
                (_arenaOrbitRadius - distance) * 0.12f,
                -0.32f,
                0.32f);
            Vector3 orbitDirection =
                (tangent +
                    radialDirection * radiusCorrection)
                .Normalized();
            SetTravelVelocity(
                orbitDirection * _cruiseSpeed * 0.74f);
            return;
        }

        if (!_arenaStrikeLocked)
        {
            if (!IsInstanceValid(_target))
            {
                _target =
                    GetTree().GetFirstNodeInGroup("player") as Player;
            }

            Vector3 strikeTarget = IsInstanceValid(_target)
                ? _target.GlobalPosition +
                    new Vector3(
                        _target.Velocity.X,
                        0.0f,
                        _target.Velocity.Z) *
                        0.22f
                : _arenaCenter;
            strikeTarget += _arenaStrikeOffset;
            Vector3 strikeDirection =
                strikeTarget - GlobalPosition;
            strikeDirection.Y = 0.0f;
            OriginalTravelDirection =
                strikeDirection.IsZeroApprox()
                    ? -Vector3.Forward
                    : strikeDirection.Normalized();
            _arenaStrikeLocked = true;
        }

        SetTravelVelocity(
            OriginalTravelDirection * _cruiseSpeed * 1.08f);
    }

    private bool UpdateTunnelClearance(float deltaSeconds)
    {
        _tunnelYieldTimer = Mathf.Max(
            0.0f,
            _tunnelYieldTimer - deltaSeconds);
        _tunnelProbeTimer -= deltaSeconds;
        if (_tunnelProbeTimer <= 0.0f)
        {
            _tunnelProbeTimer = 0.12f;
            foreach (Node node in GetTree().GetNodesInGroup("tunnel_clearance"))
            {
                if (node is TunnelBoostZone tunnel &&
                    IsInstanceValid(tunnel) &&
                    tunnel.TryGetMobYieldDirection(
                        this,
                        out Vector3 yieldDirection))
                {
                    BeginTunnelYield(yieldDirection, 0.42f);
                    break;
                }
            }
        }

        return IsYieldingToTunnel;
    }

    private void UpdateTunnelYieldVelocity(float deltaSeconds)
    {
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? _tunnelYieldDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f - Mathf.Exp(-9.0f * deltaSeconds);
        Vector3 direction = currentDirection.Lerp(
            _tunnelYieldDirection,
            turnWeight).Normalized();
        SetTravelVelocity(
            direction *
            _cruiseSpeed *
            0.88f);
    }

    private void SetTravelVelocity(Vector3 travelVelocity)
    {
        Velocity = new Vector3(travelVelocity.X, 0.0f, travelVelocity.Z);
        if (!Velocity.IsZeroApprox())
        {
            Vector3 direction = Velocity.Normalized();
            Vector3 origin = IsInsideTree() ? GlobalPosition : Position;
            LookAtFromPosition(origin, origin + direction, Vector3.Up);
        }

        _animationPlayer ??=
            GetNodeOrNull<AnimationPlayer>(
                "AnimationPlayer");
        if (IsInstanceValid(_animationPlayer))
        {
            _animationPlayer.SpeedScale =
                Mathf.Max(
                    0.1f,
                    Velocity.Length() /
                        Mathf.Max(1, MinSpeed));
        }
    }

    private void ApplyArchetypeVisual()
    {
        if (_archetypeProfile == null)
        {
            _archetypeProfile =
                EnemyArchetypeLibrary.Get(ArchetypeId);
        }

        Node3D pivot = GetNodeOrNull<Node3D>("Pivot");
        if (pivot == null)
        {
            return;
        }

        _modelVisualRoot ??=
            pivot.GetNodeOrNull<ModelVisualRoot>(
                "Character");
        if (!IsInstanceValid(_modelVisualRoot) ||
            !_modelVisualRoot.ValidateContract())
        {
            return;
        }

        EnemyVisualIdentity.ApplyModel(_modelVisualRoot, ArchetypeId);
        pivot.Scale = _archetypeProfile.SilhouetteScale * 1.45f;
        StandardMaterial3D bodyMaterial = new()
        {
            AlbedoColor =
                _archetypeProfile.BodyColor,
            Metallic = 0.08f,
            Roughness = 0.58f,
            EmissionEnabled = true,
            Emission =
                _archetypeProfile.BodyColor *
                0.14f,
            EmissionEnergyMultiplier = 0.35f
        };
        var eye = new StandardMaterial3D { AlbedoColor = new Color("#edf2e5"), Roughness = .36f };
        var pupil = new StandardMaterial3D { AlbedoColor = new Color("#10212a"), Roughness = .28f };
        var armor = new StandardMaterial3D { AlbedoColor = _archetypeProfile.BodyColor.Darkened(.42f), Roughness = .6f };
        var tooth = new StandardMaterial3D { AlbedoColor = new Color("#f9ebca"), Roughness = .5f };
        var glint = new StandardMaterial3D { AlbedoColor = Colors.White, EmissionEnabled = true, Emission = Colors.White, EmissionEnergyMultiplier = .3f };
        _archetypeAccentMaterial = new StandardMaterial3D
        {
            AlbedoColor =
                _archetypeProfile.AccentColor,
            Metallic = 0.16f,
            Roughness = 0.32f,
            EmissionEnabled = true,
            Emission =
                _archetypeProfile.AccentColor,
            EmissionEnergyMultiplier = .8f
        };
        _modelVisualRoot.ApplySurfacePalette(new Dictionary<string, Material> {
            ["mob_body"] = bodyMaterial, ["mob_eye"] = eye, ["pupil"] = pupil,
            ["armor"] = armor, ["tooth"] = tooth, ["glint"] = glint, ["accent"] = _archetypeAccentMaterial }, bodyMaterial);
    }

    private void CreateArchetypeAudio()
    {
        AudioStreamGenerator generator = new()
        {
            MixRate = 22050.0f,
            BufferLength = 0.18f
        };
        _archetypeCuePlayer = new AudioStreamPlayer3D
        {
            Name = "ArchetypeCue",
            Stream = generator,
            VolumeDb = -22.0f,
            MaxDistance = 26.0f,
            UnitSize = 7.0f
        };
        AddChild(_archetypeCuePlayer);
        _archetypeCuePlayer.Play();
    }

    private void PlaySpawnCue()
    {
        PlayArchetypeCue(0.42f);
    }

    private void PlayArchetypeCue(float intensity)
    {
        if (_archetypeCuePlayer == null ||
            !_archetypeCuePlayer.Playing)
        {
            return;
        }

        AudioStreamGeneratorPlayback playback =
            _archetypeCuePlayer.GetStreamPlayback()
                as AudioStreamGeneratorPlayback;
        if (playback == null)
        {
            return;
        }

        const float mixRate = 22050.0f;
        int frameCount = Mathf.RoundToInt(
            mixRate * 0.065f);
        float baseFrequency =
            245.0f *
            (_archetypeProfile?.CuePitch ?? 1.0f);
        float safeIntensity = Mathf.Clamp(
            intensity,
            0.1f,
            1.0f);
        for (int frame = 0;
            frame < frameCount;
            frame++)
        {
            float progress =
                frame / (float)frameCount;
            float envelope =
                Mathf.Sin(progress * Mathf.Pi) *
                safeIntensity *
                0.16f;
            float sample =
                Mathf.Sin(
                    Mathf.Tau *
                    baseFrequency *
                    frame /
                    mixRate) *
                envelope;
            playback.PushFrame(
                new Vector2(sample, sample));
        }
    }

    private void CreateIntentVisual()
    {
        _intentMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(1.0f, 0.72f, 0.12f, 0.0f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.55f, 0.08f),
            EmissionEnergyMultiplier = 1.5f
        };
        _intentDisc = new MeshInstance3D
        {
            Name = "AttackIntent",
            Position = new Vector3(0.0f, 0.055f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = 1.38f,
                BottomRadius = 1.38f,
                Height = 0.035f,
                RadialSegments = 28,
                Rings = 1,
                Material = _intentMaterial
            },
            Visible = false
        };
        AddChild(_intentDisc);

        _intentLight = new OmniLight3D
        {
            Name = "AttackIntentLight",
            Position = new Vector3(0.0f, 1.0f, 0.0f),
            LightColor = new Color(1.0f, 0.48f, 0.08f),
            LightEnergy = 0.0f,
            OmniRange = 3.6f,
            ShadowEnabled = false,
            Visible = false
        };
        AddChild(_intentLight);
    }

    private void CreateJumpReachVisual()
    {
        _guidanceMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(0.2f, 0.92f, 1.0f, 0.0f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.92f, 1.0f),
            EmissionEnergyMultiplier = 1.8f
        };
        _guidanceDisc = new MeshInstance3D
        {
            Name = "JumpReachRing",
            Position = new Vector3(0.0f, 0.09f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = 1.42f,
                OuterRadius = 1.72f,
                Rings = 32,
                RingSegments = 8,
                Material = _guidanceMaterial
            },
            Visible = false
        };
        AddChild(_guidanceDisc);
    }

    private void UpdateJumpReachVisual(float deltaSeconds)
    {
        if (_guidanceDisc == null || _guidanceMaterial == null)
        {
            return;
        }

        bool visible =
            CurrentJumpReachCue != JumpReachCue.None &&
            !IsPostArenaRetreating &&
            !IsArenaAttackResolved;
        _guidanceDisc.Visible = visible;
        if (!visible)
        {
            return;
        }

        _guidancePulseTime += deltaSeconds;
        Color cueColor = CurrentJumpReachCue switch
        {
            JumpReachCue.Reachable =>
                new Color(0.12f, 1.0f, 0.32f),
            JumpReachCue.OnCourse =>
                new Color(1.0f, 0.78f, 0.08f),
            _ => new Color(1.0f, 0.08f, 0.045f)
        };
        float pulseSpeed = Mathf.Lerp(
            4.5f,
            10.0f,
            _guidanceUrgency);
        float pulse =
            1.0f +
            Mathf.Sin(_guidancePulseTime * pulseSpeed) *
                Mathf.Lerp(0.05f, 0.14f, _guidanceUrgency);
        _guidanceDisc.Scale = Vector3.One * pulse;
        _guidanceMaterial.AlbedoColor = new Color(
            cueColor.R,
            cueColor.G,
            cueColor.B,
            Mathf.Lerp(0.12f, 0.32f, _guidanceUrgency));
        _guidanceMaterial.Emission = cueColor;
        _guidanceMaterial.EmissionEnergyMultiplier =
            Mathf.Lerp(1.25f, 2.5f, _guidanceUrgency);
    }

    private void UpdateIntentVisual(float deltaSeconds)
    {
        if (_intentDisc == null ||
            _intentMaterial == null ||
            _intentLight == null)
        {
            return;
        }

        bool showIntent =
            EncounterId < 0 &&
            CurrentJumpReachCue == JumpReachCue.None &&
            !IsPostArenaRetreating &&
            CurrentAttackPhase != AttackPhase.Hunting;
        _intentDisc.Visible = showIntent;
        _intentLight.Visible = showIntent;
        if (!showIntent)
        {
            _intentLight.LightEnergy = 0.0f;
            return;
        }

        float progress = _attackPhaseDuration <= 0.0f
            ? 1.0f
            : 1.0f -
                Mathf.Clamp(
                    _attackPhaseTimer / _attackPhaseDuration,
                    0.0f,
                    1.0f);
        float pulse;
        Color color;
        float alpha;
        float energy;
        switch (CurrentAttackPhase)
        {
            case AttackPhase.Telegraphing:
                pulse =
                    0.88f +
                    Mathf.Sin(progress * Mathf.Pi * 5.0f) * 0.12f;
                color = CurrentRegularBehavior ==
                    RegularBehavior.Feinter
                        ? new Color(0.92f, 0.3f, 1.0f)
                        : new Color(1.0f, 0.72f, 0.1f);
                alpha = Mathf.Lerp(0.16f, 0.58f, progress);
                energy = Mathf.Lerp(0.45f, 1.75f, progress);
                break;
            case AttackPhase.Lunging:
                pulse =
                    1.08f +
                    Mathf.Sin(_movementTime * 18.0f) * 0.08f;
                color = new Color(1.0f, 0.18f, 0.055f);
                alpha = 0.62f;
                energy = 2.2f;
                break;
            default:
                pulse = Mathf.Lerp(1.0f, 0.76f, progress);
                color = new Color(0.22f, 0.82f, 1.0f);
                alpha = Mathf.Lerp(0.38f, 0.08f, progress);
                energy = Mathf.Lerp(0.75f, 0.15f, progress);
                break;
        }

        _movementTime += deltaSeconds * 0.25f;
        _intentDisc.Scale = Vector3.One * pulse;
        _intentMaterial.AlbedoColor =
            new Color(color.R, color.G, color.B, alpha);
        _intentMaterial.Emission = color;
        _intentMaterial.EmissionEnergyMultiplier =
            Mathf.Lerp(1.0f, 2.2f, energy / 2.2f);
        _intentLight.LightColor = color;
        _intentLight.LightEnergy = energy;
    }

    private void CreateTrajectoryPreview()
    {
        _trajectoryPreviewRoot = new Node3D
        {
            Name = "TrajectoryPreview",
            TopLevel = true,
            Visible = false
        };
        AddChild(_trajectoryPreviewRoot);

        const int maximumMarkers = 10;
        for (int index = 0; index < maximumMarkers; index++)
        {
            StandardMaterial3D material = new()
            {
                Transparency =
                    BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode =
                    BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode =
                    BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor =
                    new Color(1.0f, 0.72f, 0.12f, 0.0f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.58f, 0.08f),
                EmissionEnergyMultiplier = 1.6f
            };
            BoxMesh mesh = new()
            {
                Size = new Vector3(0.12f, 0.045f, 0.7f),
                Material = material
            };
            MeshInstance3D marker = new()
            {
                Name = $"TrajectoryDash{index + 1}",
                Mesh = mesh,
                Visible = false
            };
            _trajectoryPreviewRoot.AddChild(marker);
            _trajectoryMarkers.Add(marker);
            _trajectoryMarkerMeshes.Add(mesh);
            _trajectoryMaterials.Add(material);
        }
    }

    private void UpdateTrajectoryPreview(float deltaSeconds)
    {
        bool shouldShow =
            TrajectoryPreviewEnabled &&
            TrajectoryPreviewPreference > 0 &&
            CurrentJumpReachCue == JumpReachCue.None &&
            EncounterId < 0 &&
            !IsPostArenaRetreating &&
            IsInstanceValid(_target) &&
            (CurrentAttackPhase == AttackPhase.Telegraphing ||
                CurrentAttackPhase == AttackPhase.Lunging);
        if (!shouldShow)
        {
            HideTrajectoryPreview();
            return;
        }

        if (_trajectoryPreviewRoot == null)
        {
            CreateTrajectoryPreview();
        }

        _trajectoryUpdateTimer -= deltaSeconds;
        if (_trajectoryUpdateTimer > 0.0f)
        {
            return;
        }

        _trajectoryUpdateTimer = Mathf.Max(
            0.02f,
            TrajectoryUpdateInterval);
        CalculateAndDisplayTrajectory();
    }

    private void CalculateAndDisplayTrajectory()
    {
        int requestedMarkerCount = Mathf.Clamp(
            TrajectoryMarkerCount,
            3,
            _trajectoryMarkers.Count);
        float visibleFraction = Mathf.Clamp(
            TrajectoryVisibleFraction,
            0.25f,
            0.8f);
        if (TrajectoryPreviewPreference >= 2)
        {
            visibleFraction = Mathf.Min(
                0.76f,
                visibleFraction * 1.18f);
            requestedMarkerCount = Mathf.Min(
                _trajectoryMarkers.Count,
                requestedMarkerCount + 1);
        }
        if (CurrentAttackPhase == AttackPhase.Telegraphing &&
            CurrentRegularBehavior == RegularBehavior.Feinter)
        {
            // A feinter reveals intent, not certainty.
            visibleFraction *= 0.72f;
            requestedMarkerCount = Mathf.Max(
                3,
                requestedMarkerCount - 2);
        }

        float expectedLungeDuration =
            CurrentAttackPhase == AttackPhase.Lunging
                ? Mathf.Max(0.08f, _attackPhaseTimer)
                : (MinLungeTime + MaxLungeTime) * 0.5f;
        float previewHorizon =
            expectedLungeDuration * visibleFraction;
        float simulatedSpeed =
            CurrentAttackPhase == AttackPhase.Lunging &&
            _lungeSpeed > 0.01f
                ? _lungeSpeed
                : _cruiseSpeed *
                    (MinLungeSpeedMultiplier +
                        MaxLungeSpeedMultiplier) *
                    0.5f;
        float timeStep =
            previewHorizon / requestedMarkerCount;
        Vector3 simulatedPosition = GlobalPosition;
        simulatedPosition.Y = _groundHeight + 0.1f;
        Vector3 simulatedDirection = _lungeDirection.IsZeroApprox()
            ? GetPredictedAttackDirection(0.12f)
            : _lungeDirection.Normalized();
        float travelledDistance = 0.0f;
        int shownMarkers = 0;
        Color pathColor =
            CurrentAttackPhase == AttackPhase.Lunging
                ? new Color(1.0f, 0.16f, 0.045f)
                : CurrentRegularBehavior == RegularBehavior.Feinter
                    ? new Color(0.9f, 0.28f, 1.0f)
                    : new Color(1.0f, 0.72f, 0.1f);
        float phasePulse =
            0.82f +
            Mathf.Sin(_movementTime * 14.0f) * 0.18f;

        _trajectoryPreviewRoot.Visible = true;
        for (int index = 0;
            index < requestedMarkerCount;
            index++)
        {
            float simulatedTime = (index + 1) * timeStep;
            Vector3 predictedTarget =
                _target.GlobalPosition +
                new Vector3(
                    _target.Velocity.X,
                    0.0f,
                    _target.Velocity.Z) *
                simulatedTime;
            Vector3 desiredDirection =
                predictedTarget - simulatedPosition;
            desiredDirection.Y = 0.0f;
            if (!desiredDirection.IsZeroApprox())
            {
                desiredDirection = desiredDirection.Normalized();
                float turnWeight =
                    1.0f -
                    Mathf.Exp(
                        -Mathf.Max(
                            0.0f,
                            LungeTurnResponsiveness) *
                        timeStep);
                simulatedDirection = simulatedDirection
                    .Lerp(desiredDirection, turnWeight)
                    .Normalized();
            }

            float remainingVisibleDistance =
                Mathf.Max(
                    0.0f,
                    TrajectoryMaxVisibleDistance -
                        travelledDistance);
            float segmentLength = Mathf.Min(
                simulatedSpeed * timeStep,
                remainingVisibleDistance);
            if (segmentLength <= 0.08f)
            {
                break;
            }

            Vector3 nextPosition =
                simulatedPosition +
                simulatedDirection * segmentLength;
            bool hitsObstacle = TryClipTrajectoryToObstacle(
                simulatedPosition,
                ref nextPosition,
                simulatedDirection);
            segmentLength =
                simulatedPosition.DistanceTo(nextPosition);
            if (segmentLength <= 0.08f)
            {
                break;
            }

            MeshInstance3D marker =
                _trajectoryMarkers[index];
            BoxMesh markerMesh =
                _trajectoryMarkerMeshes[index];
            StandardMaterial3D markerMaterial =
                _trajectoryMaterials[index];
            marker.Visible = true;
            marker.GlobalPosition =
                (simulatedPosition + nextPosition) * 0.5f;
            marker.GlobalBasis = Basis.LookingAt(
                simulatedDirection,
                Vector3.Up);
            markerMesh.Size = new Vector3(
                Mathf.Lerp(0.14f, 0.08f, index /
                    (float)requestedMarkerCount),
                0.045f,
                Mathf.Min(0.92f, segmentLength * 0.62f));

            float fade =
                1.0f -
                index / (float)requestedMarkerCount;
            float alpha =
                Mathf.Pow(fade, 1.35f) *
                (CurrentAttackPhase == AttackPhase.Lunging
                    ? 0.7f
                    : 0.5f) *
                phasePulse *
                (TrajectoryPreviewPreference >= 2
                    ? 1.18f
                    : 1.0f);
            markerMaterial.AlbedoColor = new Color(
                pathColor.R,
                pathColor.G,
                pathColor.B,
                alpha);
            markerMaterial.Emission = pathColor;
            markerMaterial.EmissionEnergyMultiplier =
                Mathf.Lerp(1.1f, 2.2f, fade);

            shownMarkers++;
            travelledDistance += segmentLength;
            simulatedPosition = nextPosition;
            if (hitsObstacle)
            {
                break;
            }
        }

        for (int index = shownMarkers;
            index < _trajectoryMarkers.Count;
            index++)
        {
            _trajectoryMarkers[index].Visible = false;
        }

        VisibleTrajectoryMarkerCount = shownMarkers;
        VisibleTrajectoryLength = travelledDistance;
    }

    private bool TryClipTrajectoryToObstacle(
        Vector3 start,
        ref Vector3 end,
        Vector3 direction)
    {
        World3D world = GetWorld3D();
        if (world == null)
        {
            return false;
        }

        Vector3 rayLift = Vector3.Up * 0.65f;
        PhysicsRayQueryParameters3D query =
            PhysicsRayQueryParameters3D.Create(
                start + rayLift,
                end + rayLift,
                ObstacleCollisionMask);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        Godot.Collections.Dictionary hit =
            world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return false;
        }

        Vector3 hitPosition = (Vector3)hit["position"] - rayLift;
        float safeDistance = Mathf.Max(
            0.0f,
            start.DistanceTo(hitPosition) - 0.28f);
        end = start + direction * safeDistance;
        return true;
    }

    private void HideTrajectoryPreview()
    {
        if (_trajectoryPreviewRoot == null)
        {
            return;
        }

        _trajectoryPreviewRoot.Visible = false;
        foreach (MeshInstance3D marker in _trajectoryMarkers)
        {
            marker.Visible = false;
        }

        VisibleTrajectoryMarkerCount = 0;
        VisibleTrajectoryLength = 0.0f;
        _trajectoryUpdateTimer = 0.0f;
    }

    private void KeepOnGroundPlane()
    {
        Vector3 groundedPosition = GlobalPosition;
        if (!Mathf.IsEqualApprox(groundedPosition.Y, _groundHeight))
        {
            groundedPosition.Y = _groundHeight;
            GlobalPosition = groundedPosition;
        }

        if (!Mathf.IsZeroApprox(Velocity.Y))
        {
            Velocity = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        }
    }

    private void UpdateAttackVelocity(float deltaSeconds)
    {
        if (!IsInstanceValid(_target))
        {
            _target = GetTree().GetFirstNodeInGroup("player") as Player;
        }

        if (!IsInstanceValid(_target) || _cruiseSpeed <= 0.01f)
        {
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 targetVelocity =
            new(_target.Velocity.X, 0.0f, _target.Velocity.Z);
        Vector3 separationToTarget = _target.GlobalPosition - GlobalPosition;
        separationToTarget.Y = 0.0f;
        float targetDistance = separationToTarget.Length();
        if (targetDistance <= 0.01f)
        {
            return;
        }

        float predictionWeight = Mathf.Clamp(targetDistance / 16.0f, 0.15f, 1.0f);
        Vector3 predictedTarget =
            _target.GlobalPosition +
            targetVelocity * TargetPredictionTime * predictionWeight;
        int slotCount = Mathf.Max(1, MaxCommittedAttackers);
        int slotIndex = (int)(GetInstanceId() % (ulong)slotCount);
        float slotAngle = Mathf.Tau * slotIndex / slotCount;
        predictedTarget += new Vector3(
            Mathf.Cos(slotAngle) * AttackSlotRadius,
            0.0f,
            Mathf.Sin(slotAngle) * AttackSlotRadius);
        Vector3 attackDirection = predictedTarget - GlobalPosition;
        attackDirection.Y = 0.0f;
        attackDirection = attackDirection.IsZeroApprox()
            ? separationToTarget.Normalized()
            : attackDirection.Normalized();

        Vector3 side = Vector3.Up.Cross(attackDirection).Normalized();
        float weaveAmount =
            Mathf.Sin(_movementTime * WeaveFrequency * Mathf.Tau + _weavePhase) *
            WeaveStrength *
            _weaveBias *
            Mathf.Clamp(targetDistance / 7.0f, 0.15f, 1.0f);
        attackDirection += side * weaveAmount;

        Vector3 separationSteer = GetSeparationSteer();
        attackDirection += separationSteer * SeparationStrength;
        attackDirection = attackDirection.Normalized();

        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? attackDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f - Mathf.Exp(-TurnResponsiveness * deltaSeconds);
        Vector3 steeredDirection =
            currentDirection.Lerp(attackDirection, turnWeight).Normalized();
        SetTravelVelocity(steeredDirection * _cruiseSpeed);
    }

    private void TryBeginAttackTelegraph()
    {
        if (_attackCooldown > 0.0f ||
            _archetypeProfile?.CanInitiateAttack == false ||
            !IsInstanceValid(_target) ||
            _target.IsDamageInvulnerable ||
            _activeObstacle != null ||
            _collisionAvoidanceTimer > 0.1f)
        {
            return;
        }

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        toTarget.Y = 0.0f;
        float targetDistance = toTarget.Length();
        if (targetDistance < AttackMinimumDistance ||
            targetDistance > AttackTriggerDistance ||
            CountActiveReadableAttackers() >=
                Mathf.Max(1, MaxCommittedAttackers))
        {
            return;
        }

        CurrentAttackPhase = AttackPhase.Telegraphing;
        _attackPhaseDuration = (float)GD.RandRange(
            Mathf.Min(MinTelegraphTime, MaxTelegraphTime),
            Mathf.Max(MinTelegraphTime, MaxTelegraphTime));
        _attackPhaseTimer = _attackPhaseDuration;
        _lungeDirection = GetPredictedAttackDirection(0.18f);
        _isHolding = false;
        PlayArchetypeCue(
            ArchetypeId ==
                EnemyArchetypeId.Trickster
                ? 0.72f
                : 0.9f);
    }

    private void UpdateReadableAttack(float deltaSeconds)
    {
        switch (CurrentAttackPhase)
        {
            case AttackPhase.Telegraphing:
                UpdateAttackTelegraph(deltaSeconds);
                break;
            case AttackPhase.Lunging:
                UpdateAttackLunge(deltaSeconds);
                break;
            case AttackPhase.Recovering:
                UpdateAttackRecovery(deltaSeconds);
                break;
        }
    }

    private void UpdateAttackTelegraph(float deltaSeconds)
    {
        if (!IsInstanceValid(_target))
        {
            BeginAttackRecovery(false);
            return;
        }

        _attackPhaseTimer -= deltaSeconds;
        Vector3 desiredDirection =
            GetPredictedAttackDirection(0.16f);
        if (!_lungeDirection.IsZeroApprox())
        {
            float aimWeight =
                1.0f - Mathf.Exp(-5.5f * deltaSeconds);
            desiredDirection = _lungeDirection
                .Lerp(desiredDirection, aimWeight)
                .Normalized();
        }

        _lungeDirection = desiredDirection;
        SetTravelVelocity(
            _lungeDirection *
            _cruiseSpeed *
            0.46f);
        if (_attackPhaseTimer > 0.0f)
        {
            return;
        }

        bool performsFeint =
            CurrentRegularBehavior == RegularBehavior.Feinter &&
            !_feintUsed &&
            GD.Randf() < Mathf.Clamp(FeintChance, 0.0f, 1.0f);
        if (performsFeint)
        {
            _feintUsed = true;
            BeginAttackRecovery(true);
        }
        else
        {
            BeginAttackLunge();
        }
    }

    private void BeginAttackLunge()
    {
        CurrentAttackPhase = AttackPhase.Lunging;
        _attackPhaseDuration = (float)GD.RandRange(
            Mathf.Min(MinLungeTime, MaxLungeTime),
            Mathf.Max(MinLungeTime, MaxLungeTime));
        _attackPhaseTimer = _attackPhaseDuration;
        _lungeDirection = GetPredictedAttackDirection(0.12f);
        _lungeSpeed =
            _cruiseSpeed *
            (float)GD.RandRange(
                Mathf.Min(
                    MinLungeSpeedMultiplier,
                    MaxLungeSpeedMultiplier),
                Mathf.Max(
                    MinLungeSpeedMultiplier,
                    MaxLungeSpeedMultiplier));
        ClearObstacleRoute();
    }

    private void UpdateAttackLunge(float deltaSeconds)
    {
        _attackPhaseTimer -= deltaSeconds;
        if (IsInstanceValid(_target) &&
            LungeTurnResponsiveness > 0.0f)
        {
            Vector3 desiredDirection =
                GetPredictedAttackDirection(0.06f);
            float turnWeight =
                1.0f -
                Mathf.Exp(
                    -LungeTurnResponsiveness * deltaSeconds);
            _lungeDirection = _lungeDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized();
        }

        SetTravelVelocity(_lungeDirection * _lungeSpeed);
        bool passedPlayer = false;
        if (IsInstanceValid(_target))
        {
            Vector3 toPlayer =
                _target.GlobalPosition - GlobalPosition;
            toPlayer.Y = 0.0f;
            passedPlayer =
                toPlayer.Dot(_lungeDirection) < -0.8f;
        }

        if (_attackPhaseTimer <= 0.0f || passedPlayer)
        {
            BeginAttackRecovery(false);
        }
    }

    private void BeginAttackRecovery(bool fromFeint)
    {
        CurrentAttackPhase = AttackPhase.Recovering;
        _recoveryFromFeint = fromFeint;
        _attackPhaseDuration = (float)GD.RandRange(
            Mathf.Min(MinRecoveryTime, MaxRecoveryTime),
            Mathf.Max(MinRecoveryTime, MaxRecoveryTime));
        _attackPhaseTimer = _attackPhaseDuration;

        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? (_lungeDirection.IsZeroApprox()
                ? OriginalTravelDirection
                : _lungeDirection)
            : currentDirection.Normalized();
        if (fromFeint)
        {
            float sideSign = _weaveBias < 0.0f ? -1.0f : 1.0f;
            Vector3 side =
                Vector3.Up.Cross(currentDirection).Normalized() *
                sideSign;
            _recoveryDirection =
                (side + currentDirection * 0.18f).Normalized();
        }
        else
        {
            _recoveryDirection = currentDirection;
        }
    }

    private void UpdateAttackRecovery(float deltaSeconds)
    {
        _attackPhaseTimer -= deltaSeconds;
        Vector3 recoverySteer =
            (_recoveryDirection +
                GetSeparationSteer() * 0.75f).Normalized();
        float speedMultiplier = _recoveryFromFeint
            ? Mathf.Max(0.72f, RecoverySpeedMultiplier)
            : RecoverySpeedMultiplier;
        SetTravelVelocity(
            recoverySteer *
            _cruiseSpeed *
            Mathf.Clamp(speedMultiplier, 0.2f, 1.0f));
        if (_attackPhaseTimer > 0.0f)
        {
            return;
        }

        ResolveAttackRecovery();
    }

    private void ResolveAttackRecovery()
    {
        bool wasFeint = _recoveryFromFeint;
        CurrentAttackPhase = AttackPhase.Hunting;
        _recoveryFromFeint = false;
        _attackCooldown = wasFeint
            ? (float)GD.RandRange(0.35f, 0.65f)
            : (float)GD.RandRange(0.75f, 1.3f);
        if (wasFeint)
        {
            return;
        }

        if (CurrentTravelCombatRole ==
            TravelCombatRole.Challenger)
        {
            BeginLeaving();
            return;
        }

        float decision = GD.Randf();
        if (CurrentRegularBehavior == RegularBehavior.Feinter)
        {
            if (decision < 0.24f)
            {
                BeginLeaving();
            }
            else if (decision < 0.68f)
            {
                SwitchToWandererAfterAttack();
            }

            return;
        }

        if (decision < 0.38f)
        {
            BeginLeaving();
        }
        else if (decision < 0.68f)
        {
            BeginHoldingPattern();
        }
        else
        {
            SwitchToWandererAfterAttack();
        }
    }

    private void SwitchToWandererAfterAttack()
    {
        CurrentRegularBehavior = RegularBehavior.Wanderer;
        _isHolding = false;
        _wanderTimeRemaining =
            (float)GD.RandRange(1.6f, 3.4f);
        _routeChangeTimer = 0.0f;
        _wanderDirection = _recoveryDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : _recoveryDirection
                .Rotated(
                    Vector3.Up,
                    Mathf.DegToRad(
                        (float)GD.RandRange(-68.0f, 68.0f)))
                .Normalized();
    }

    private Vector3 GetPredictedAttackDirection(
        float predictionTime)
    {
        if (!IsInstanceValid(_target))
        {
            return _lungeDirection.IsZeroApprox()
                ? OriginalTravelDirection
                : _lungeDirection;
        }

        Vector3 targetVelocity =
            new(_target.Velocity.X, 0.0f, _target.Velocity.Z);
        Vector3 predictedPosition =
            _target.GlobalPosition +
            targetVelocity * Mathf.Max(0.0f, predictionTime);
        Vector3 direction = predictedPosition - GlobalPosition;
        direction.Y = 0.0f;
        return direction.IsZeroApprox()
            ? OriginalTravelDirection
            : direction.Normalized();
    }

    private int CountActiveReadableAttackers()
    {
        int count = 0;
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob other &&
                other != this &&
                IsInstanceValid(other) &&
                !other.IsQueuedForDeletion() &&
                (other.IsAttackTelegraphing ||
                    other.IsAttackCommitted))
            {
                if (IsInstanceValid(_target) &&
                    other.HorizontalDistanceTo(
                        _target.GlobalPosition) >
                        TargetCrowdRadius * 1.7f)
                {
                    continue;
                }

                count++;
            }
        }

        return count;
    }

    private void UpdateRegularBehavior(float deltaSeconds)
    {
        if (_isLeaving)
        {
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        if (_comboRouteTimer > 0.0f)
        {
            UpdateComboRouteVelocity(deltaSeconds);
            return;
        }

        if (CurrentTravelCombatRole ==
                TravelCombatRole.LaneRunner ||
            CurrentTravelCombatRole ==
                TravelCombatRole.Sweeper)
        {
            UpdateScriptedTravelVelocity(deltaSeconds);
            return;
        }

        if (CurrentTravelCombatRole ==
            TravelCombatRole.Challenger)
        {
            _travelRoleTimer = Mathf.Max(
                0.0f,
                _travelRoleTimer - deltaSeconds);
            if (_travelRoleTimer <= 0.0f &&
                CurrentAttackPhase == AttackPhase.Hunting)
            {
                BeginLeaving();
                UpdateLeavingVelocity(deltaSeconds);
                return;
            }
        }

        if (_isCrackedShell)
        {
            UpdateCrackedShellVelocity(deltaSeconds);
            return;
        }

        if (_archetypeProfile?.UsesCourierMotion == true)
        {
            UpdateCourierVelocity(deltaSeconds);
            return;
        }

        if (_archetypeProfile?.UsesShepherdMotion == true)
        {
            UpdateShepherdVelocity(deltaSeconds);
            return;
        }

        if (_archetypeProfile?.UsesRelayMotion == true)
        {
            UpdateRelayVelocity(deltaSeconds);
            return;
        }

        if (_archetypeProfile?.UsesTwinMotion == true)
        {
            UpdateTwinVelocity(deltaSeconds);
            return;
        }

        _attackCooldown = Mathf.Max(
            0.0f,
            _attackCooldown - deltaSeconds);
        if (CurrentAttackPhase != AttackPhase.Hunting)
        {
            UpdateReadableAttack(deltaSeconds);
            return;
        }

        bool coordinatesAttack =
            CurrentRegularBehavior == RegularBehavior.Pursuer ||
            CurrentRegularBehavior == RegularBehavior.Feinter;
        if (coordinatesAttack)
        {
            UpdateCrowdCoordination(deltaSeconds);
            if (_isLeaving)
            {
                UpdateLeavingVelocity(deltaSeconds);
                return;
            }

            if (_isHolding)
            {
                UpdateHoldingVelocity(deltaSeconds);
                return;
            }
        }

        switch (CurrentRegularBehavior)
        {
            case RegularBehavior.Drifter:
                BeginLeaving();
                UpdateLeavingVelocity(deltaSeconds);
                break;
            case RegularBehavior.Wanderer:
                _wanderTimeRemaining -= deltaSeconds;
                if (_wanderTimeRemaining <= 0.0f)
                {
                    BeginLeaving();
                    UpdateLeavingVelocity(deltaSeconds);
                }
                else
                {
                    UpdateWanderVelocity(deltaSeconds);
                }
                break;
            default:
                _pursuitTimeRemaining -= deltaSeconds;
                if (_pursuitTimeRemaining <= 0.0f)
                {
                    BeginLeaving();
                    UpdateLeavingVelocity(deltaSeconds);
                }
                else
                {
                    UpdateAttackVelocity(deltaSeconds);
                    TryBeginAttackTelegraph();
                }
                break;
        }
    }

    private void UpdateCourierVelocity(float deltaSeconds)
    {
        _archetypeMotionTimer = Mathf.Max(
            0.0f,
            _archetypeMotionTimer - deltaSeconds);
        if (_archetypeMotionTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 escapeDirection =
            -OriginalTravelDirection;
        if (escapeDirection.IsZeroApprox())
        {
            escapeDirection = Vector3.Forward;
        }
        escapeDirection = escapeDirection
            .Rotated(
                Vector3.Up,
                Mathf.DegToRad(24.0f) *
                    _archetypeMotionSign)
            .Normalized();

        Vector3 side =
            Vector3.Up.Cross(escapeDirection).Normalized();
        float weave =
            Mathf.Sin(
                _movementTime * 2.7f +
                _weavePhase) *
            0.34f *
            _archetypeMotionSign;
        Vector3 desiredDirection =
            (escapeDirection + side * weave)
            .Normalized();
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? desiredDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f -
            Mathf.Exp(-11.5f * deltaSeconds);
        SetTravelVelocity(
            currentDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized() *
            _cruiseSpeed);
    }

    private void UpdateCrackedShellVelocity(
        float deltaSeconds)
    {
        _archetypeMotionTimer = Mathf.Max(
            0.0f,
            _archetypeMotionTimer - deltaSeconds);
        if (_archetypeMotionTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        Vector3 desiredDirection =
            OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? desiredDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f -
            Mathf.Exp(-7.5f * deltaSeconds);
        SetTravelVelocity(
            currentDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized() *
            _cruiseSpeed);
    }

    private void UpdateShepherdVelocity(float deltaSeconds)
    {
        _archetypeMotionTimer = Mathf.Max(
            0.0f,
            _archetypeMotionTimer - deltaSeconds);
        if (_archetypeMotionTimer <= 0.0f ||
            !IsInstanceValid(_target))
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 playerHeading =
            new(
                _target.Velocity.X,
                0.0f,
                _target.Velocity.Z);
        if (playerHeading.IsZeroApprox())
        {
            playerHeading =
                OriginalTravelDirection;
        }
        playerHeading = playerHeading.Normalized();
        Vector3 side =
            Vector3.Up.Cross(playerHeading)
                .Normalized() *
            _archetypeMotionSign;
        float pressurePulse =
            5.8f +
            Mathf.Sin(_movementTime * 1.9f) * 1.4f;
        Vector3 pressurePoint =
            _target.GlobalPosition +
            side * pressurePulse -
            playerHeading * 2.4f;
        pressurePoint.Y = GlobalPosition.Y;
        Vector3 desiredDirection =
            pressurePoint - GlobalPosition;
        desiredDirection.Y = 0.0f;
        if (desiredDirection.IsZeroApprox())
        {
            desiredDirection =
                OriginalTravelDirection;
        }
        desiredDirection = desiredDirection.Normalized();
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? desiredDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f -
            Mathf.Exp(-TurnResponsiveness * deltaSeconds);
        SetTravelVelocity(
            currentDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized() *
            _cruiseSpeed *
            0.9f);
    }

    private void UpdateRelayVelocity(float deltaSeconds)
    {
        _archetypeMotionTimer = Mathf.Max(
            0.0f,
            _archetypeMotionTimer - deltaSeconds);
        if (_archetypeMotionTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 baseDirection =
            OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        Vector3 side =
            Vector3.Up.Cross(baseDirection)
                .Normalized();
        float beat =
            Mathf.PosMod(
                _movementTime + _weavePhase * 0.08f,
                1.9f);
        float speedMultiplier;
        if (beat < 0.24f)
        {
            speedMultiplier =
                Mathf.Lerp(
                    0.34f,
                    0.52f,
                    beat / 0.24f);
        }
        else
        {
            float release =
                Mathf.Clamp(
                    (beat - 0.24f) / 0.34f,
                    0.0f,
                    1.0f);
            speedMultiplier =
                Mathf.Lerp(0.72f, 1.05f, release);
        }

        float routePulse =
            Mathf.Sin(
                _movementTime * 1.55f +
                _weavePhase) *
            0.075f *
            _archetypeMotionSign;
        Vector3 desiredDirection =
            (baseDirection + side * routePulse)
            .Normalized();
        SetTravelVelocity(
            desiredDirection *
            _cruiseSpeed *
            speedMultiplier);
    }

    private void UpdateTwinVelocity(float deltaSeconds)
    {
        _archetypeMotionTimer = Mathf.Max(
            0.0f,
            _archetypeMotionTimer - deltaSeconds);
        if (_archetypeMotionTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 baseDirection =
            OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        Vector3 side =
            Vector3.Up.Cross(baseDirection)
                .Normalized();
        float pairedWeave =
            Mathf.Sin(
                _movementTime * 1.72f +
                _twinPhaseOffset) *
            0.17f *
            _archetypeMotionSign;
        float breathingSpeed =
            0.94f +
            Mathf.Cos(
                _movementTime * 1.72f +
                _twinPhaseOffset) *
            0.06f;
        Vector3 desiredDirection =
            (baseDirection + side * pairedWeave)
            .Normalized();
        Vector3 currentDirection =
            new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? desiredDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f -
            Mathf.Exp(-8.5f * deltaSeconds);
        SetTravelVelocity(
            currentDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized() *
            _cruiseSpeed *
            breathingSpeed);
    }

    private void UpdateComboRouteVelocity(float deltaSeconds)
    {
        _comboRouteTimer = Mathf.Max(
            0.0f,
            _comboRouteTimer - deltaSeconds);
        if (_comboRouteTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 baseDirection =
            OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        Vector3 side =
            Vector3.Up.Cross(baseDirection).Normalized();
        float weave =
            Mathf.Sin(
                _movementTime * 2.2f +
                _weavePhase) *
            0.11f *
            _comboRouteWeaveSign;
        Vector3 direction =
            (baseDirection + side * weave).Normalized();
        SetTravelVelocity(direction * _cruiseSpeed);
    }

    private void UpdateScriptedTravelVelocity(float deltaSeconds)
    {
        _travelRoleTimer = Mathf.Max(
            0.0f,
            _travelRoleTimer - deltaSeconds);
        if (_travelRoleTimer <= 0.0f)
        {
            BeginLeaving();
            UpdateLeavingVelocity(deltaSeconds);
            return;
        }

        _movementTime += deltaSeconds;
        Vector3 baseDirection =
            OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        Vector3 side =
            Vector3.Up.Cross(baseDirection).Normalized();
        bool sweeper =
            CurrentTravelCombatRole ==
            TravelCombatRole.Sweeper;
        float weaveStrength = sweeper ? 0.14f : 0.055f;
        float weaveFrequency = sweeper ? 1.35f : 1.85f;
        float weave =
            Mathf.Sin(
                _movementTime * weaveFrequency +
                _weavePhase) *
            weaveStrength *
            _travelRoleWeaveSign;
        Vector3 direction =
            (baseDirection + side * weave).Normalized();
        float speedMultiplier = sweeper ? 1.0f : 0.94f;
        SetTravelVelocity(
            direction * _cruiseSpeed * speedMultiplier);
    }

    private void UpdateCrowdCoordination(float deltaSeconds)
    {
        if (!IsInstanceValid(_target))
        {
            _target = GetTree().GetFirstNodeInGroup("player") as Player;
        }

        if (!IsInstanceValid(_target))
        {
            return;
        }

        if (_isHolding)
        {
            _holdingTimeRemaining -= deltaSeconds;
        }

        _crowdDecisionTimer -= deltaSeconds;
        if (_crowdDecisionTimer > 0.0f)
        {
            return;
        }

        _crowdDecisionTimer = (float)GD.RandRange(
            Mathf.Min(MinCrowdDecisionTime, MaxCrowdDecisionTime),
            Mathf.Max(MinCrowdDecisionTime, MaxCrowdDecisionTime));
        int closerAttackers = CountCloserCommittedAttackers();
        int attackLimit = Mathf.Max(1, MaxCommittedAttackers);

        if (!_isHolding)
        {
            if (closerAttackers >= attackLimit)
            {
                BeginHoldingPattern();
            }

            return;
        }

        if (closerAttackers < attackLimit)
        {
            _isHolding = false;
            _holdingTimeRemaining = 0.0f;
            _crowdDeferrals = 0;
            return;
        }

        if (_holdingTimeRemaining > 0.0f)
        {
            return;
        }

        _crowdDeferrals++;
        bool givesUp =
            _crowdDeferrals >= Mathf.Max(1, MaxCrowdDeferrals) ||
            GD.Randf() < Mathf.Clamp(CrowdedExitChance, 0.0f, 1.0f);
        if (givesUp)
        {
            _isHolding = false;
            BeginLeaving();
        }
        else
        {
            ResetHoldingTimerAndSpeed();
            _holdingOrbitSign *= GD.Randf() < 0.35f ? -1.0f : 1.0f;
        }
    }

    private int CountCloserCommittedAttackers()
    {
        if (!IsInstanceValid(_target))
        {
            return 0;
        }

        float ownDistance = HorizontalDistanceTo(_target.GlobalPosition);
        int closerAttackers = 0;
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob other ||
                other == this ||
                !IsInstanceValid(other) ||
                other.IsQueuedForDeletion() ||
                !other.IsCommittedAttacker())
            {
                continue;
            }

            float otherDistance =
                other.HorizontalDistanceTo(_target.GlobalPosition);
            if (otherDistance > TargetCrowdRadius)
            {
                continue;
            }

            bool clearlyCloser = otherDistance < ownDistance - 0.35f;
            bool winsTie =
                Mathf.Abs(otherDistance - ownDistance) <= 0.35f &&
                other.GetInstanceId() < GetInstanceId();
            if (clearlyCloser || winsTie)
            {
                closerAttackers++;
            }
        }

        return closerAttackers;
    }

    private bool IsCommittedAttacker()
    {
        return EncounterId < 0 &&
            !_isLeaving &&
            !_isHolding &&
            (CurrentRegularBehavior == RegularBehavior.Pursuer ||
                CurrentRegularBehavior == RegularBehavior.Feinter) &&
            (CurrentAttackPhase == AttackPhase.Telegraphing ||
                CurrentAttackPhase == AttackPhase.Lunging);
    }

    private float HorizontalDistanceTo(Vector3 position)
    {
        Vector3 offset = GlobalPosition - position;
        offset.Y = 0.0f;
        return offset.Length();
    }

    private void BeginHoldingPattern()
    {
        CurrentAttackPhase = AttackPhase.Hunting;
        _isHolding = true;
        _holdingOrbitSign = GD.Randf() < 0.5f ? -1.0f : 1.0f;
        ResetHoldingTimerAndSpeed();
    }

    private void ResetHoldingTimerAndSpeed()
    {
        _holdingTimeRemaining = (float)GD.RandRange(
            Mathf.Min(MinHoldingTime, MaxHoldingTime),
            Mathf.Max(MinHoldingTime, MaxHoldingTime));
        _holdingSpeedMultiplier = (float)GD.RandRange(
            Mathf.Min(MinHoldingSpeedMultiplier, MaxHoldingSpeedMultiplier),
            Mathf.Max(MinHoldingSpeedMultiplier, MaxHoldingSpeedMultiplier));
    }

    private void UpdateHoldingVelocity(float deltaSeconds)
    {
        if (!IsInstanceValid(_target))
        {
            _isHolding = false;
            return;
        }

        Vector3 outward = GlobalPosition - _target.GlobalPosition;
        outward.Y = 0.0f;
        float distance = outward.Length();
        if (outward.IsZeroApprox())
        {
            outward = Vector3.Right * _holdingOrbitSign;
            distance = 0.0f;
        }
        else
        {
            outward = outward.Normalized();
        }

        Vector3 tangent =
            Vector3.Up.Cross(outward).Normalized() * _holdingOrbitSign;
        float radialError = Mathf.Clamp(
            (distance - HoldingDistance) /
                Mathf.Max(1.0f, HoldingDistance * 0.35f),
            -1.0f,
            1.0f);
        Vector3 desiredDirection =
            tangent -
            outward * radialError * 0.9f +
            GetSeparationSteer() * 1.25f;
        desiredDirection = desiredDirection.Normalized();

        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? desiredDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f - Mathf.Exp(-TurnResponsiveness * 1.35f * deltaSeconds);
        Vector3 holdingDirection =
            currentDirection.Lerp(desiredDirection, turnWeight).Normalized();
        SetTravelVelocity(
            holdingDirection *
            _cruiseSpeed *
            _holdingSpeedMultiplier);
    }

    private void UpdateWanderVelocity(float deltaSeconds)
    {
        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : currentDirection.Normalized();

        _routeChangeTimer -= deltaSeconds;
        if (_routeChangeTimer <= 0.0f || _wanderDirection.IsZeroApprox())
        {
            float turnDegrees = (float)GD.RandRange(
                -MaxRandomTurnDegrees,
                MaxRandomTurnDegrees);
            if (Mathf.Abs(turnDegrees) < 25.0f)
            {
                turnDegrees = 25.0f * (GD.Randf() < 0.5f ? -1.0f : 1.0f);
            }

            _wanderDirection =
                currentDirection.Rotated(
                    Vector3.Up,
                    Mathf.DegToRad(turnDegrees)).Normalized();
            _routeChangeTimer = (float)GD.RandRange(
                Mathf.Min(MinRouteChangeTime, MaxRouteChangeTime),
                Mathf.Max(MinRouteChangeTime, MaxRouteChangeTime));
        }

        Vector3 desiredDirection =
            (_wanderDirection + GetSeparationSteer() * 0.45f).Normalized();
        float turnWeight =
            1.0f - Mathf.Exp(-WanderTurnResponsiveness * deltaSeconds);
        Vector3 wanderingDirection =
            currentDirection.Lerp(desiredDirection, turnWeight).Normalized();
        SetTravelVelocity(wanderingDirection * _cruiseSpeed);
    }

    private void ChooseRegularBehavior()
    {
        float pursuerWeight = Mathf.Max(0.0f, PursuerWeight);
        float feinterWeight = Mathf.Max(0.0f, FeinterWeight);
        float wandererWeight = Mathf.Max(0.0f, WandererWeight);
        float drifterWeight = Mathf.Max(0.0f, DrifterWeight);
        float totalWeight =
            pursuerWeight +
            feinterWeight +
            wandererWeight +
            drifterWeight;
        if (totalWeight <= 0.0f)
        {
            CurrentRegularBehavior = RegularBehavior.Pursuer;
            return;
        }

        float roll = GD.Randf() * totalWeight;
        if (roll < pursuerWeight)
        {
            CurrentRegularBehavior = RegularBehavior.Pursuer;
        }
        else if ((roll -= pursuerWeight) < feinterWeight)
        {
            CurrentRegularBehavior = RegularBehavior.Feinter;
        }
        else if ((roll -= feinterWeight) < wandererWeight)
        {
            CurrentRegularBehavior = RegularBehavior.Wanderer;
        }
        else
        {
            CurrentRegularBehavior = RegularBehavior.Drifter;
        }
    }

    private Vector3 GetSeparationSteer()
    {
        if (SeparationRadius <= 0.0f)
        {
            return Vector3.Zero;
        }

        Vector3 steer = Vector3.Zero;
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob other ||
                other == this ||
                !IsInstanceValid(other) ||
                other.IsQueuedForDeletion())
            {
                continue;
            }

            Vector3 away = GlobalPosition - other.GlobalPosition;
            away.Y = 0.0f;
            float distance = away.Length();
            if (distance <= 0.01f || distance >= SeparationRadius)
            {
                continue;
            }

            steer +=
                away.Normalized() *
                (1.0f - distance / SeparationRadius);
        }

        return steer.LimitLength(1.0f);
    }

    private void BeginLeaving()
    {
        CurrentAttackPhase = AttackPhase.Hunting;
        if (!IsInstanceValid(_target))
        {
            _target = GetTree().GetFirstNodeInGroup("player") as Player;
        }

        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : currentDirection.Normalized();
        Vector3 awayFromPlayer = IsInstanceValid(_target)
            ? GlobalPosition - _target.GlobalPosition
            : currentDirection;
        awayFromPlayer.Y = 0.0f;
        awayFromPlayer = awayFromPlayer.IsZeroApprox()
            ? currentDirection
            : awayFromPlayer.Normalized();
        Vector3 side = _weaveBias < 0.0f ? Vector3.Left : Vector3.Right;
        float awayWeight = 0.8f;
        float currentWeight = 0.35f;
        float sideWeight = 0.28f;
        if (CurrentRegularBehavior == RegularBehavior.Feinter)
        {
            awayWeight = 0.45f;
            currentWeight = 0.12f;
            sideWeight = 1.0f;
        }
        else if (CurrentRegularBehavior == RegularBehavior.Drifter)
        {
            awayWeight = 1.0f;
            currentWeight = 0.08f;
            sideWeight = 0.42f;
        }

        _exitDirection =
            (awayFromPlayer * awayWeight +
                currentDirection * currentWeight +
                side * sideWeight).Normalized();
        _exitSpeed = _cruiseSpeed * Mathf.Max(1.0f, ExitSpeedMultiplier);
        _isLeaving = true;
    }

    private void UpdateLeavingVelocity(float deltaSeconds)
    {
        if (_exitDirection.IsZeroApprox())
        {
            _exitDirection = OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        }

        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? _exitDirection
            : currentDirection.Normalized();
        float turnWeight =
            1.0f - Mathf.Exp(-ExitTurnResponsiveness * deltaSeconds);
        Vector3 departureDirection =
            currentDirection.Lerp(_exitDirection, turnWeight).Normalized();
        SetTravelVelocity(departureDirection * _exitSpeed);
    }

    private void ApplyTrafficAnticipation(float deltaSeconds)
    {
        if (ApplyPostContactSpacing(deltaSeconds))
        {
            return;
        }

        if (!TryFindPredictedCollision(
            out Mob threat,
            out float timeToClosest,
            out Vector3 closestOffset,
            out float headingAlignment,
            out float closingSpeed))
        {
            _trafficDecisionTimer = Mathf.Max(
                0.0f,
                _trafficDecisionTimer - deltaSeconds);
            if (_trafficDecisionTimer <= 0.0f ||
                _trafficSteeredDirection.IsZeroApprox())
            {
                _anticipatedMob = null;
                _trafficSteeredDirection = Vector3.Zero;
                _trafficSpeedMultiplier = 1.0f;
                return;
            }

            Vector3 currentDirection =
                new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
            Vector3 heldDirection =
                currentDirection
                    .Lerp(_trafficSteeredDirection, 0.42f)
                    .Normalized();
            _trafficSpeedMultiplier = Mathf.Lerp(
                _trafficSpeedMultiplier,
                1.0f,
                1.0f -
                    Mathf.Exp(-5.0f * deltaSeconds));
            SetTravelVelocity(
                heldDirection *
                Velocity.Length() *
                _trafficSpeedMultiplier);
            return;
        }

        Vector3 travelDirection =
            new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        }

        bool headOn = headingAlignment < -0.35f;
        Vector3 toThreat = threat.GlobalPosition - GlobalPosition;
        toThreat.Y = 0.0f;
        bool threatIsAhead = toThreat.Dot(travelDirection) > 0.0f;
        bool shouldYield = headOn ||
            (headingAlignment > 0.55f && threatIsAhead) ||
            (!headOn && _courtesy > threat._courtesy);
        Vector3 right = travelDirection.Cross(Vector3.Up).Normalized();
        float sideSign;
        if (headOn)
        {
            // Each enemy takes its own right, so opposite headings produce
            // opposite world-space lanes instead of mirroring into each other.
            sideSign = 1.0f;
        }
        else
        {
            float rightCongestion =
                GetSideCongestionScore(right, travelDirection);
            float leftCongestion =
                GetSideCongestionScore(-right, travelDirection);
            if (Mathf.Abs(rightCongestion - leftCongestion) < 0.12f)
            {
                sideSign = _courtesy >= 0.5f ? 1.0f : -1.0f;
            }
            else
            {
                sideSign = rightCongestion < leftCongestion ? 1.0f : -1.0f;
            }
        }

        float horizon = Mathf.Max(0.1f, TrafficPredictionHorizon);
        float timeUrgency =
            1.0f - Mathf.Clamp(timeToClosest / horizon, 0.0f, 1.0f);
        float distanceUrgency =
            1.0f -
            Mathf.Clamp(
                closestOffset.Length() /
                    Mathf.Max(0.1f, StrongCollisionDistance),
                0.0f,
                1.0f);
        float urgency = Mathf.Clamp(
            timeUrgency * 0.62f +
                distanceUrgency * 0.28f +
                Mathf.Clamp(closingSpeed / 20.0f, 0.0f, 0.1f),
            0.2f,
            1.0f);
        float sideStrength = headOn
            ? 1.05f
            : shouldYield ? 0.72f : 0.05f;
        Vector3 predictedAway = -closestOffset;
        predictedAway.Y = 0.0f;
        predictedAway = predictedAway.IsZeroApprox()
            ? right * sideSign
            : predictedAway.Normalized();
        Vector3 crossingYield = Vector3.Zero;
        if (!headOn &&
            shouldYield &&
            headingAlignment <= 0.55f)
        {
            Vector3 otherDirection =
                new Vector3(
                    threat.Velocity.X,
                    0.0f,
                    threat.Velocity.Z).Normalized();
            crossingYield = -otherDirection * 0.42f * urgency;
        }

        Vector3 desiredDirection =
            (travelDirection +
                right * sideSign * sideStrength * urgency +
                predictedAway * (shouldYield ? 0.24f : 0.03f) +
                crossingYield)
            .Normalized();

        if (_anticipatedMob != threat ||
            _trafficSteeredDirection.IsZeroApprox())
        {
            _trafficSteeredDirection = travelDirection;
        }

        _anticipatedMob = threat;
        _trafficDecisionTimer = Mathf.Max(
            0.05f,
            TrafficDecisionHoldTime);
        float turnWeight =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(1.0f, TrafficTurnResponsiveness) *
                deltaSeconds);
        _trafficSteeredDirection =
            _trafficSteeredDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized();

        float targetSpeedMultiplier = shouldYield
            ? Mathf.Clamp(YieldSpeedMultiplier, 0.1f, 1.0f)
            : 1.0f;
        if (headOn)
        {
            targetSpeedMultiplier = shouldYield &&
                _courtesy >= threat._courtesy
                ? Mathf.Min(targetSpeedMultiplier, 0.78f)
                : 0.92f;
        }

        _trafficSpeedMultiplier = Mathf.Lerp(
            _trafficSpeedMultiplier,
            Mathf.Lerp(1.0f, targetSpeedMultiplier, urgency),
            1.0f - Mathf.Exp(-7.0f * deltaSeconds));
        SetTravelVelocity(
            _trafficSteeredDirection *
            Velocity.Length() *
            _trafficSpeedMultiplier);
    }

    private bool ApplyPostContactSpacing(float deltaSeconds)
    {
        if (_postContactSeparationTimer <= 0.0f ||
            !IsInstanceValid(_contactPartner))
        {
            _contactPartner = null;
            return false;
        }

        Vector3 away =
            GlobalPosition - _contactPartner.GlobalPosition;
        away.Y = 0.0f;
        float distance = away.Length();
        if (distance >= 2.8f)
        {
            _contactPartner = null;
            _postContactSeparationTimer = 0.0f;
            return false;
        }

        Vector3 travelDirection =
            new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = OriginalTravelDirection.IsZeroApprox()
                ? Vector3.Forward
                : OriginalTravelDirection;
        }

        away = away.IsZeroApprox()
            ? travelDirection.Cross(Vector3.Up) *
                (GetInstanceId() % 2UL == 0UL ? 1.0f : -1.0f)
            : away.Normalized();
        float spacingStrength = Mathf.Lerp(
            0.72f,
            0.24f,
            Mathf.Clamp((distance - 1.5f) / 1.3f, 0.0f, 1.0f));
        Vector3 desiredDirection =
            (travelDirection + away * spacingStrength).Normalized();
        if (_trafficSteeredDirection.IsZeroApprox())
        {
            _trafficSteeredDirection = travelDirection;
        }

        float turnWeight =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(10.0f, TrafficTurnResponsiveness) *
                deltaSeconds);
        _trafficSteeredDirection =
            _trafficSteeredDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized();
        bool yields = _courtesy >= _contactPartner._courtesy;
        float speedMultiplier = yields ? 0.82f : 0.98f;
        SetTravelVelocity(
            _trafficSteeredDirection *
            Velocity.Length() *
            speedMultiplier);
        return true;
    }

    private bool TryFindPredictedCollision(
        out Mob threat,
        out float timeToClosest,
        out Vector3 closestOffset,
        out float headingAlignment,
        out float closingSpeed)
    {
        threat = null;
        timeToClosest = 0.0f;
        closestOffset = Vector3.Zero;
        headingAlignment = 0.0f;
        closingSpeed = 0.0f;
        Vector3 ownVelocity = new(Velocity.X, 0.0f, Velocity.Z);
        if (ownVelocity.LengthSquared() <= 0.1f)
        {
            return false;
        }

        Vector3 ownDirection = ownVelocity.Normalized();
        float bestScore = float.NegativeInfinity;
        float horizon = Mathf.Max(0.1f, TrafficPredictionHorizon);
        float radius = Mathf.Max(
            StrongCollisionDistance,
            TrafficPredictionRadius);
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob other ||
                other == this ||
                !IsInstanceValid(other) ||
                other.IsQueuedForDeletion() ||
                other.IsArenaAttackResolved)
            {
                continue;
            }

            Vector3 offset = other.GlobalPosition - GlobalPosition;
            offset.Y = 0.0f;
            float distance = offset.Length();
            if (distance <= 0.05f || distance > radius)
            {
                continue;
            }

            Vector3 otherVelocity =
                new(other.Velocity.X, 0.0f, other.Velocity.Z);
            Vector3 relativeVelocity = otherVelocity - ownVelocity;
            float relativeSpeedSquared = relativeVelocity.LengthSquared();
            if (relativeSpeedSquared <= 0.25f)
            {
                continue;
            }

            float candidateTime = Mathf.Clamp(
                -offset.Dot(relativeVelocity) / relativeSpeedSquared,
                0.0f,
                horizon);
            if (candidateTime <= 0.015f ||
                candidateTime >= horizon)
            {
                continue;
            }

            Vector3 candidateClosest =
                offset + relativeVelocity * candidateTime;
            float candidateClosestDistance = candidateClosest.Length();
            if (candidateClosestDistance >= StrongCollisionDistance)
            {
                // Near misses and grazing contacts are deliberately left
                // alone; only a likely hard impact changes the route.
                continue;
            }

            float candidateClosingSpeed =
                -offset.Normalized().Dot(relativeVelocity);
            if (candidateClosingSpeed <= 1.5f)
            {
                continue;
            }

            Vector3 otherDirection = otherVelocity.IsZeroApprox()
                ? Vector3.Zero
                : otherVelocity.Normalized();
            float candidateHeading = otherDirection.IsZeroApprox()
                ? 0.0f
                : ownDirection.Dot(otherDirection);
            float score =
                (1.0f - candidateTime / horizon) * 1.4f +
                (1.0f -
                    candidateClosestDistance /
                    Mathf.Max(0.1f, StrongCollisionDistance)) +
                candidateClosingSpeed / 30.0f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            threat = other;
            timeToClosest = candidateTime;
            closestOffset = candidateClosest;
            headingAlignment = candidateHeading;
            closingSpeed = candidateClosingSpeed;
        }

        return threat != null;
    }

    private void ApplyCollisionAvoidance(float deltaSeconds)
    {
        if (_collisionAvoidanceTimer <= 0.0f ||
            _collisionAvoidanceDirection.IsZeroApprox())
        {
            return;
        }

        Vector3 currentDirection = new(Velocity.X, 0.0f, Velocity.Z);
        currentDirection = currentDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : currentDirection.Normalized();
        Vector3 desiredDirection =
            (_collisionAvoidanceDirection +
                GetSeparationSteer() * 0.55f).Normalized();
        float turnWeight =
            1.0f -
            Mathf.Exp(-AvoidanceTurnResponsiveness * deltaSeconds);
        if (_contactReactionCooldown > 0.0f)
        {
            turnWeight = Mathf.Max(turnWeight, 0.46f);
        }

        Vector3 avoidanceDirection =
            currentDirection.Lerp(desiredDirection, turnWeight).Normalized();
        float referenceSpeed = _isLeaving ? _exitSpeed : _cruiseSpeed;
        float avoidanceSpeed = Mathf.Max(
            Velocity.Length(),
            referenceSpeed * AvoidanceSpeedMultiplier);
        SetTravelVelocity(avoidanceDirection * avoidanceSpeed);
    }

    private void UpdateObstacleAvoidance(float deltaSeconds)
    {
        _followingObstacleRouteThisFrame = false;
        if (FollowActiveObstacleRoute(deltaSeconds))
        {
            return;
        }

        _obstacleProbeTimer -= deltaSeconds;
        if (_obstacleProbeTimer > 0.0f ||
            ObstacleCollisionMask == 0 ||
            Velocity.LengthSquared() <= 0.01f)
        {
            return;
        }

        _obstacleProbeTimer = Mathf.Max(0.02f, ObstacleProbeInterval);
        Vector3 travelDirection =
            new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
        float lookAhead = Mathf.Clamp(
            Velocity.Length() * 0.72f,
            4.0f,
            Mathf.Max(4.0f, ObstacleLookAheadDistance));
        if (!TryGetObstacleAhead(
            travelDirection,
            lookAhead,
            out Node3D obstacle,
            out _))
        {
            return;
        }

        BeginObstacleRoute(obstacle, travelDirection, false);
        FollowActiveObstacleRoute(deltaSeconds);
    }

    private void BeginObstacleRoute(
        Node3D obstacle,
        Vector3 travelDirection,
        bool collisionRecovery)
    {
        if (!IsInstanceValid(obstacle))
        {
            StartObstacleDetour(travelDirection, collisionRecovery);
            return;
        }

        travelDirection.Y = 0.0f;
        travelDirection = travelDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : travelDirection.Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = Vector3.Forward;
        }

        float probeDistance = Mathf.Max(5.0f, ObstacleLookAheadDistance);
        float detourAngle = Mathf.DegToRad(
            Mathf.Clamp(ObstacleDetourAngleDegrees, 25.0f, 75.0f));
        Vector3 firstDirection =
            travelDirection.Rotated(Vector3.Up, detourAngle).Normalized();
        Vector3 secondDirection =
            travelDirection.Rotated(Vector3.Up, -detourAngle).Normalized();
        float firstClearance =
            GetObstacleClearance(firstDirection, probeDistance);
        float secondClearance =
            GetObstacleClearance(secondDirection, probeDistance);
        bool chooseFirst;
        if (Mathf.Abs(firstClearance - secondClearance) < 0.35f)
        {
            chooseFirst =
                (GetInstanceId() + (ulong)_failedAvoidanceAttempts) % 2UL ==
                0UL;
        }
        else
        {
            chooseFirst = firstClearance > secondClearance;
        }

        Vector3 lateral =
            Vector3.Up.Cross(travelDirection).Normalized();
        Vector3 chosenDirection =
            chooseFirst ? firstDirection : secondDirection;
        float sideSign = chosenDirection.Dot(lateral) >= 0.0f
            ? 1.0f
            : -1.0f;
        Vector2 halfExtents = obstacle.HasMeta("avoidance_half_extents")
            ? (Vector2)obstacle.GetMeta("avoidance_half_extents")
            : new Vector2(3.0f, 2.0f);
        float sideExtent =
            Mathf.Abs(lateral.X) * halfExtents.X +
            Mathf.Abs(lateral.Z) * halfExtents.Y;
        float forwardExtent =
            Mathf.Abs(travelDirection.X) * halfExtents.X +
            Mathf.Abs(travelDirection.Z) * halfExtents.Y;
        float clearanceMargin =
            1.8f + (collisionRecovery ? 0.8f : 0.0f);
        Vector3 obstacleCenter = obstacle.GlobalPosition;
        Vector3 sideOffset =
            lateral * sideSign * (sideExtent + clearanceMargin);
        _activeObstacle = obstacle;
        _obstacleRouteDirection = travelDirection;
        _obstacleNearWaypoint =
            obstacleCenter +
            sideOffset -
            travelDirection * (forwardExtent + clearanceMargin * 0.7f);
        _obstacleFarWaypoint =
            obstacleCenter +
            sideOffset +
            travelDirection * (forwardExtent + clearanceMargin);
        _obstacleNearWaypoint.Y = GlobalPosition.Y;
        _obstacleFarWaypoint.Y = GlobalPosition.Y;
        _obstacleNearWaypointReached = false;
        _obstacleSteeredDirection =
            new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
        _collisionAvoidanceTimer = 0.0f;
        if (collisionRecovery)
        {
            _failedAvoidanceAttempts++;
        }
    }

    private bool FollowActiveObstacleRoute(float deltaSeconds)
    {
        if (!IsInstanceValid(_activeObstacle))
        {
            ClearObstacleRoute();
            return false;
        }

        Vector3 obstacleOffset =
            GlobalPosition - _activeObstacle.GlobalPosition;
        obstacleOffset.Y = 0.0f;
        if (obstacleOffset.Dot(_obstacleRouteDirection) >
            _activeObstacle.GlobalPosition
                .DistanceTo(_obstacleFarWaypoint) + 1.0f)
        {
            ClearObstacleRoute();
            return false;
        }

        Vector3 waypoint = _obstacleNearWaypointReached
            ? _obstacleFarWaypoint
            : _obstacleNearWaypoint;
        Vector3 toWaypoint = waypoint - GlobalPosition;
        toWaypoint.Y = 0.0f;
        if (toWaypoint.Length() <= 1.35f)
        {
            if (_obstacleNearWaypointReached)
            {
                ClearObstacleRoute();
                return false;
            }

            _obstacleNearWaypointReached = true;
            waypoint = _obstacleFarWaypoint;
            toWaypoint = waypoint - GlobalPosition;
            toWaypoint.Y = 0.0f;
        }

        Vector3 desiredDirection = toWaypoint.IsZeroApprox()
            ? _obstacleRouteDirection
            : toWaypoint.Normalized();
        desiredDirection =
            (desiredDirection +
                GetSeparationSteer() * SeparationStrength * 0.48f)
            .Normalized();
        if (_obstacleSteeredDirection.IsZeroApprox())
        {
            _obstacleSteeredDirection = desiredDirection;
        }

        float turnWeight =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(8.0f, AvoidanceTurnResponsiveness) *
                deltaSeconds);
        _obstacleSteeredDirection =
            _obstacleSteeredDirection
                .Lerp(desiredDirection, turnWeight)
                .Normalized();
        float referenceSpeed = _isLeaving ? _exitSpeed : _cruiseSpeed;
        SetTravelVelocity(
            _obstacleSteeredDirection *
            referenceSpeed *
            Mathf.Max(0.72f, AvoidanceSpeedMultiplier));
        _followingObstacleRouteThisFrame = true;
        return true;
    }

    private void ClearObstacleRoute()
    {
        _activeObstacle = null;
        _obstacleNearWaypoint = Vector3.Zero;
        _obstacleFarWaypoint = Vector3.Zero;
        _obstacleRouteDirection = Vector3.Zero;
        _obstacleSteeredDirection = Vector3.Zero;
        _obstacleNearWaypointReached = false;
        _followingObstacleRouteThisFrame = false;
    }

    private void StartObstacleDetour(
        Vector3 travelDirection,
        bool collisionRecovery)
    {
        travelDirection.Y = 0.0f;
        travelDirection = travelDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : travelDirection.Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = Vector3.Forward;
        }

        float probeDistance = Mathf.Max(5.0f, ObstacleLookAheadDistance);
        float detourAngle = Mathf.DegToRad(
            Mathf.Clamp(ObstacleDetourAngleDegrees, 25.0f, 75.0f));
        Vector3 firstDirection =
            travelDirection.Rotated(Vector3.Up, detourAngle).Normalized();
        Vector3 secondDirection =
            travelDirection.Rotated(Vector3.Up, -detourAngle).Normalized();
        float firstClearance =
            GetObstacleClearance(firstDirection, probeDistance);
        float secondClearance =
            GetObstacleClearance(secondDirection, probeDistance);

        bool chooseFirst;
        if (Mathf.Abs(firstClearance - secondClearance) < 0.35f)
        {
            chooseFirst =
                (GetInstanceId() + (ulong)_failedAvoidanceAttempts) % 2UL ==
                0UL;
        }
        else
        {
            chooseFirst = firstClearance > secondClearance;
        }

        Vector3 chosenDirection =
            chooseFirst ? firstDirection : secondDirection;
        float chosenClearance = Mathf.Max(firstClearance, secondClearance);
        if (collisionRecovery && chosenClearance < 2.4f)
        {
            Vector3 lateral =
                Vector3.Up.Cross(travelDirection).Normalized() *
                (chooseFirst ? 1.0f : -1.0f);
            chosenDirection =
                (lateral - travelDirection * 0.38f).Normalized();
        }

        _collisionAvoidanceDirection = chosenDirection;
        float duration =
            Mathf.Max(0.25f, ObstacleDetourTime) *
            (collisionRecovery ? 1.45f : 1.0f);
        _collisionAvoidanceTimer = Mathf.Max(
            _collisionAvoidanceTimer,
            duration);
        if (collisionRecovery)
        {
            _failedAvoidanceAttempts++;
        }
    }

    private float GetObstacleClearance(
        Vector3 direction,
        float maxDistance)
    {
        TryGetObstacleAhead(
            direction,
            maxDistance,
            out _,
            out float closestDistance);
        return closestDistance;
    }

    private bool TryGetObstacleAhead(
        Vector3 direction,
        float maxDistance,
        out Node3D closestObstacle,
        out float closestDistance)
    {
        closestObstacle = null;
        closestDistance = maxDistance;
        World3D world = GetWorld3D();
        if (world == null ||
            direction.IsZeroApprox() ||
            maxDistance <= 0.01f)
        {
            return false;
        }

        direction.Y = 0.0f;
        direction = direction.Normalized();
        Vector3 lateral = Vector3.Up.Cross(direction).Normalized();
        Vector3 rayOrigin =
            GlobalPosition + Vector3.Up * ObstacleProbeHeight;
        for (int rayIndex = -1; rayIndex <= 1; rayIndex++)
        {
            Vector3 origin =
                rayOrigin +
                lateral * ObstacleProbeHalfWidth * rayIndex;
            PhysicsRayQueryParameters3D query =
                PhysicsRayQueryParameters3D.Create(
                    origin,
                    origin + direction * maxDistance,
                    ObstacleCollisionMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            Godot.Collections.Dictionary hit =
                world.DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0)
            {
                continue;
            }

            Vector3 hitPosition = (Vector3)hit["position"];
            float hitDistance = origin.DistanceTo(hitPosition);
            if (hitDistance < closestDistance)
            {
                closestDistance = hitDistance;
                closestObstacle =
                    (GodotObject)hit["collider"] as Node3D;
            }
        }

        return closestObstacle != null;
    }

    private void UpdateCollisionRecovery(
        Vector3 frameStartPosition,
        Vector3 intendedVelocity,
        float deltaSeconds)
    {
        Mob blockingMob = null;
        Node3D blockingObstacle = null;
        Vector3 mobCollisionNormal = Vector3.Zero;
        float strongestMobImpact = 0.0f;
        Vector3 horizontalIntent =
            new(intendedVelocity.X, 0.0f, intendedVelocity.Z);
        for (int index = 0; index < GetSlideCollisionCount(); index++)
        {
            KinematicCollision3D collision = GetSlideCollision(index);
            GodotObject collider = collision.GetCollider();
            if (collider is Mob mob)
            {
                Vector3 collisionNormal = collision.GetNormal();
                collisionNormal.Y = 0.0f;
                Vector3 relativeVelocity =
                    horizontalIntent -
                    new Vector3(
                        mob.Velocity.X,
                        0.0f,
                        mob.Velocity.Z);
                float impactSpeed = collisionNormal.IsZeroApprox()
                    ? relativeVelocity.Length()
                    : Mathf.Max(
                        0.0f,
                        -relativeVelocity.Dot(
                            collisionNormal.Normalized()));
                if (blockingMob == null ||
                    impactSpeed > strongestMobImpact)
                {
                    blockingMob = mob;
                    mobCollisionNormal = collisionNormal;
                    strongestMobImpact = impactSpeed;
                }
            }
            else if (collider is Node node &&
                node.IsInGroup("mob_obstacle"))
            {
                blockingObstacle = node as Node3D;
            }
        }

        Vector3 displacement = GlobalPosition - frameStartPosition;
        displacement.Y = 0.0f;
        float actualSpeed = deltaSeconds <= 0.0f
            ? 0.0f
            : displacement.Length() / deltaSeconds;
        float intendedSpeed = horizontalIntent.Length();
        bool barelyMoved =
            intendedSpeed > 1.0f &&
            actualSpeed < Mathf.Max(0.65f, intendedSpeed * 0.18f);

        if (blockingMob != null)
        {
            ReactToMobContact(
                blockingMob,
                horizontalIntent,
                mobCollisionNormal,
                strongestMobImpact);
        }

        if (blockingObstacle != null &&
            (_collisionAvoidanceTimer <= 0.12f || barelyMoved))
        {
            if (CurrentAttackPhase == AttackPhase.Lunging)
            {
                BeginAttackRecovery(false);
            }

            BeginObstacleRoute(blockingObstacle, horizontalIntent, true);
        }

        if (blockingMob != null && barelyMoved)
        {
            TryStartCollisionAvoidance(
                blockingMob,
                horizontalIntent,
                false);
        }

        if (barelyMoved)
        {
            _stuckTimer += deltaSeconds;
        }
        else
        {
            _stuckTimer = Mathf.Max(0.0f, _stuckTimer - deltaSeconds * 2.0f);
        }

        if (_stuckTimer >= StuckRecoveryTime)
        {
            if (blockingObstacle != null)
            {
                BeginObstacleRoute(blockingObstacle, horizontalIntent, true);
            }
            else
            {
                TryStartCollisionAvoidance(
                    blockingMob,
                    horizontalIntent,
                    true);
            }

            _stuckTimer = 0.0f;
        }

        if (blockingMob == null &&
            blockingObstacle == null &&
            actualSpeed >= intendedSpeed * 0.55f &&
            _collisionAvoidanceTimer <= 0.0f)
        {
            _failedAvoidanceAttempts = 0;
        }
    }

    private void ReactToMobContact(
        Mob other,
        Vector3 intendedVelocity,
        Vector3 collisionNormal,
        float impactSpeed)
    {
        if (_contactReactionCooldown > 0.0f ||
            !IsInstanceValid(other))
        {
            return;
        }

        _recentContactCount++;
        _contactMemoryTimer = 1.15f;
        bool strongImpact =
            impactSpeed >= StrongImpactSpeed ||
            _recentContactCount >= 3;
        if (strongImpact &&
            (CurrentAttackPhase == AttackPhase.Telegraphing ||
                CurrentAttackPhase == AttackPhase.Lunging))
        {
            BeginAttackRecovery(false);
        }
        _contactPartner = other;
        _postContactSeparationTimer = Mathf.Max(
            _postContactSeparationTimer,
            strongImpact ? 0.82f : 0.48f);
        _contactReactionCooldown = strongImpact ? 0.34f : 0.16f;
        Vector3 travelDirection =
            new Vector3(
                intendedVelocity.X,
                0.0f,
                intendedVelocity.Z);
        travelDirection = travelDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : travelDirection.Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = Vector3.Forward;
        }

        collisionNormal.Y = 0.0f;
        if (collisionNormal.IsZeroApprox())
        {
            collisionNormal = GlobalPosition - other.GlobalPosition;
            collisionNormal.Y = 0.0f;
        }

        collisionNormal = collisionNormal.IsZeroApprox()
            ? travelDirection.Cross(Vector3.Up)
            : collisionNormal.Normalized();
        if (!strongImpact)
        {
            // A grazing touch only causes a brief shoulder correction.
            Vector3 tangent =
                collisionNormal.Cross(Vector3.Up).Normalized();
            if (tangent.Dot(travelDirection) < 0.0f)
            {
                tangent = -tangent;
            }

            _collisionAvoidanceDirection =
                (travelDirection +
                    collisionNormal * 0.52f +
                    tangent * 0.05f).Normalized();
            _collisionAvoidanceTimer = Mathf.Max(
                _collisionAvoidanceTimer,
                0.18f);
            return;
        }

        TryStartCollisionAvoidance(
            other,
            intendedVelocity,
            _recentContactCount >= 3);
        AdaptBehaviorAfterCollision(collisionNormal);
    }

    private void AdaptBehaviorAfterCollision(Vector3 collisionNormal)
    {
        if (EncounterId >= 0 || IsArenaAttackResolved)
        {
            return;
        }

        if (_isLeaving)
        {
            float turn = Mathf.DegToRad(
                (float)GD.RandRange(14.0f, 34.0f) *
                (GD.Randf() < 0.5f ? -1.0f : 1.0f));
            _exitDirection =
                _exitDirection
                    .Rotated(Vector3.Up, turn)
                    .Normalized();
            return;
        }

        int nearbyMobs = CountNearbyMobs(5.0f);
        float changeChance = Mathf.Clamp(
            CollisionBehaviorChangeChance +
                (_recentContactCount - 1) * 0.12f +
                Mathf.Max(0, nearbyMobs - 3) * 0.06f,
            0.0f,
            0.9f);
        if (GD.Randf() > changeChance)
        {
            _routeChangeTimer = 0.0f;
            return;
        }

        float decision = GD.Randf();
        switch (CurrentRegularBehavior)
        {
            case RegularBehavior.Pursuer:
                if (nearbyMobs >= 4 && decision < 0.32f)
                {
                    BeginHoldingPattern();
                }
                else if (decision < 0.62f)
                {
                    CurrentRegularBehavior = RegularBehavior.Feinter;
                    _pursuitTimeRemaining = (float)GD.RandRange(
                        0.7f,
                        1.45f);
                    _isHolding = false;
                }
                else
                {
                    SwitchToWandererAfterContact(collisionNormal);
                }
                break;
            case RegularBehavior.Feinter:
                if (nearbyMobs >= 5 && decision < 0.28f)
                {
                    BeginLeaving();
                }
                else
                {
                    SwitchToWandererAfterContact(collisionNormal);
                }
                break;
            case RegularBehavior.Wanderer:
                if (_recentContactCount >= 3 && decision < 0.3f)
                {
                    CurrentRegularBehavior = RegularBehavior.Drifter;
                    BeginLeaving();
                }
                else
                {
                    SwitchToWandererAfterContact(collisionNormal);
                }
                break;
            case RegularBehavior.Drifter:
                float turn = Mathf.DegToRad(
                    (float)GD.RandRange(18.0f, 42.0f) *
                    (decision < 0.5f ? -1.0f : 1.0f));
                _exitDirection =
                    (_exitDirection.IsZeroApprox()
                        ? collisionNormal
                        : _exitDirection)
                    .Rotated(Vector3.Up, turn)
                    .Normalized();
                break;
        }
    }

    private void SwitchToWandererAfterContact(Vector3 collisionNormal)
    {
        CurrentAttackPhase = AttackPhase.Hunting;
        CurrentRegularBehavior = RegularBehavior.Wanderer;
        _isHolding = false;
        _holdingTimeRemaining = 0.0f;
        _wanderTimeRemaining = (float)GD.RandRange(1.8f, 4.2f);
        _routeChangeTimer = (float)GD.RandRange(0.12f, 0.32f);
        Vector3 travelDirection =
            new Vector3(Velocity.X, 0.0f, Velocity.Z).Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = OriginalTravelDirection;
        }

        float turn = Mathf.DegToRad(
            (float)GD.RandRange(32.0f, 78.0f) *
            (collisionNormal.Dot(
                travelDirection.Cross(Vector3.Up)) >= 0.0f
                ? -1.0f
                : 1.0f));
        _wanderDirection =
            travelDirection
                .Rotated(Vector3.Up, turn)
                .Normalized();
    }

    private int CountNearbyMobs(float radius)
    {
        int count = 0;
        float radiusSquared = radius * radius;
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob other ||
                other == this ||
                !IsInstanceValid(other) ||
                other.IsQueuedForDeletion())
            {
                continue;
            }

            Vector3 offset = other.GlobalPosition - GlobalPosition;
            offset.Y = 0.0f;
            if (offset.LengthSquared() <= radiusSquared)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryStartCollisionAvoidance(
        Mob blockingMob,
        Vector3 intendedVelocity,
        bool forcedRecovery)
    {
        Vector3 travelDirection =
            new(intendedVelocity.X, 0.0f, intendedVelocity.Z);
        travelDirection = travelDirection.IsZeroApprox()
            ? OriginalTravelDirection
            : travelDirection.Normalized();
        if (travelDirection.IsZeroApprox())
        {
            travelDirection = Vector3.Forward;
        }

        Vector3 toBlocker = Vector3.Zero;
        Vector3 blockerDirection = Vector3.Zero;
        if (IsInstanceValid(blockingMob))
        {
            toBlocker = blockingMob.GlobalPosition - GlobalPosition;
            toBlocker.Y = 0.0f;
            blockerDirection = new(
                blockingMob.Velocity.X,
                0.0f,
                blockingMob.Velocity.Z);
            if (!blockerDirection.IsZeroApprox())
            {
                blockerDirection = blockerDirection.Normalized();
            }
        }

        float headingAlignment = blockerDirection.IsZeroApprox()
            ? 0.0f
            : travelDirection.Dot(blockerDirection);
        bool blockerIsBehind =
            !toBlocker.IsZeroApprox() &&
            toBlocker.Dot(travelDirection) < 0.0f;
        if (!forcedRecovery &&
            headingAlignment > 0.55f &&
            blockerIsBehind)
        {
            // The leading enemy keeps its lane; the one approaching from
            // behind is responsible for finding a way around.
            return false;
        }

        Vector3 right = travelDirection.Cross(Vector3.Up).Normalized();
        float sideSign;
        if (headingAlignment < -0.25f)
        {
            // In a head-on encounter both enemies move to their own right,
            // producing opposite world-space paths.
            sideSign = 1.0f;
        }
        else
        {
            float rightCongestion =
                GetSideCongestionScore(right, travelDirection);
            float leftCongestion =
                GetSideCongestionScore(-right, travelDirection);
            if (Mathf.Abs(rightCongestion - leftCongestion) <= 0.1f)
            {
                sideSign = GetInstanceId() % 2UL == 0UL ? 1.0f : -1.0f;
            }
            else
            {
                sideSign = rightCongestion < leftCongestion ? 1.0f : -1.0f;
            }
        }

        if (forcedRecovery)
        {
            _failedAvoidanceAttempts++;
        }
        else
        {
            _failedAvoidanceAttempts =
                Mathf.Max(1, _failedAvoidanceAttempts);
        }

        float forwardWeight = _failedAvoidanceAttempts switch
        {
            >= 3 => -0.55f,
            2 => -0.18f,
            _ => 0.55f
        };
        Vector3 sideDirection = right * sideSign;
        Vector3 escapeFromCrowd = GetSeparationSteer();
        _collisionAvoidanceDirection =
            (sideDirection +
                travelDirection * forwardWeight +
                escapeFromCrowd * 0.85f).Normalized();
        if (_collisionAvoidanceDirection.IsZeroApprox())
        {
            _collisionAvoidanceDirection = sideDirection;
        }

        float baseDuration = (float)GD.RandRange(
            Mathf.Min(MinAvoidanceTime, MaxAvoidanceTime),
            Mathf.Max(MinAvoidanceTime, MaxAvoidanceTime));
        _collisionAvoidanceTimer =
            baseDuration *
            (1.0f + 0.22f * (_failedAvoidanceAttempts - 1));
        return true;
    }

    private float GetSideCongestionScore(
        Vector3 sideDirection,
        Vector3 travelDirection)
    {
        Vector3 probePosition =
            GlobalPosition +
            sideDirection * 3.0f +
            travelDirection * 1.8f;
        float congestion = 0.0f;
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob other ||
                other == this ||
                !IsInstanceValid(other) ||
                other.IsQueuedForDeletion())
            {
                continue;
            }

            Vector3 offset = other.GlobalPosition - probePosition;
            offset.Y = 0.0f;
            float distance = offset.Length();
            if (distance < 5.0f)
            {
                congestion += 1.0f - distance / 5.0f;
            }
        }

        return congestion;
    }

    public void ConfigureColumn(int columnId, int columnIndex)
    {
        ColumnId = columnId;
        ColumnIndex = columnIndex;
    }

    public void ConfigureEncounter(int encounterId)
    {
        EncounterId = encounterId;
        EncounterAge = 0.0f;
        IsArenaAttackResolved = false;
        CurrentAttackPhase = AttackPhase.Hunting;
    }

    public void ResolveArenaAttack()
    {
        IsArenaAttackResolved = true;
    }

    public void BeginPostArenaRetreat()
    {
        if (IsPostArenaRetreating)
        {
            return;
        }

        IsPostArenaRetreating = true;
        IsArenaAttackResolved = true;
        BeginLeaving();
        _exitSpeed = Mathf.Max(
            _exitSpeed,
            _cruiseSpeed * 1.2f);
    }

    protected virtual void OnVisibleOnScreenNotifierScreenExited()
    {
        QueueFree();
    }

    public virtual void Squash(
        int rewardMultiplier = 1,
        Player stompingPlayer = null)
    {
        if (ArmorLayersRemaining > 0)
        {
            ArmorLayersRemaining--;
            _isCrackedShell = true;
            BeginShellCrackContactGrace(stompingPlayer);
            CurrentAttackPhase = AttackPhase.Recovering;
            _attackPhaseDuration = 0.42f;
            _attackPhaseTimer = _attackPhaseDuration;
            _attackCooldown = 0.75f;
            _isHolding = false;
            _comboRouteTimer = 0.0f;
            Vector3 currentDirection =
                new(Velocity.X, 0.0f, Velocity.Z);
            currentDirection = currentDirection.IsZeroApprox()
                ? OriginalTravelDirection
                : currentDirection.Normalized();
            Vector3 escapeDirection =
                GetShellCrackEscapeDirection(stompingPlayer);
            if (escapeDirection.IsZeroApprox())
            {
                escapeDirection = Vector3.Forward;
            }
            OriginalTravelDirection =
                escapeDirection.Normalized();
            _cruiseSpeed = Mathf.Clamp(
                _cruiseSpeed * 0.82f,
                5.5f,
                12.0f);
            _archetypeMotionTimer = 3.2f;
            SetTravelVelocity(
                currentDirection *
                _cruiseSpeed);
            if (_archetypeAccentMaterial != null)
            {
                Color crackedColor =
                    new(1.0f, 0.2f, 0.06f);
                _archetypeAccentMaterial.AlbedoColor =
                    crackedColor;
                _archetypeAccentMaterial.Emission =
                    crackedColor;
                _archetypeAccentMaterial
                    .EmissionEnergyMultiplier = 2.2f;
            }
            GamepadInput.Rumble(
                0.12f,
                0.42f,
                0.09f);
            PlayArchetypeCue(1.0f);
            return;
        }

        EmitSignal(SignalName.Squashed, Mathf.Max(1, rewardMultiplier));
        QueueFree();
    }

    private Vector3 GetShellCrackEscapeDirection(
        Player stompingPlayer)
    {
        Vector3 escapeDirection =
            -OriginalTravelDirection;
        if (IsInstanceValid(stompingPlayer))
        {
            Vector3 playerHeading = new(
                stompingPlayer.Velocity.X,
                0.0f,
                stompingPlayer.Velocity.Z);
            if (playerHeading.LengthSquared() > 4.0f)
            {
                escapeDirection =
                    playerHeading.Normalized();
            }

            Vector3 side =
                Vector3.Up.Cross(escapeDirection);
            Vector3 separation =
                GlobalPosition -
                stompingPlayer.GlobalPosition;
            separation.Y = 0.0f;
            float peelSign = Mathf.Sign(
                side.Dot(separation));
            if (Mathf.IsZeroApprox(peelSign))
            {
                peelSign = _archetypeMotionSign;
            }

            escapeDirection = escapeDirection.Rotated(
                Vector3.Up,
                Mathf.DegToRad(12.0f) * peelSign);
        }

        return escapeDirection.Normalized();
    }

    private void BeginShellCrackContactGrace(
        Player stompingPlayer)
    {
        EndShellCrackContactGrace();
        _shellCrackContactGraceActive = true;
        _shellCrackContactGraceTimer = Mathf.Max(
            0.15f,
            ShellCrackContactGraceTime);
        _shellCrackSeparatedPhysicsFrames = 0;
        if (!IsInstanceValid(stompingPlayer))
        {
            return;
        }

        _shellCrackStompingPlayer = stompingPlayer;
        AddCollisionExceptionWith(stompingPlayer);
        stompingPlayer.AddCollisionExceptionWith(this);
    }

    private void UpdateShellCrackContactGrace(
        float deltaSeconds)
    {
        if (!_shellCrackContactGraceActive)
        {
            return;
        }

        _shellCrackContactGraceTimer = Mathf.Max(
            0.0f,
            _shellCrackContactGraceTimer - deltaSeconds);
        if (_shellCrackContactGraceTimer > 0.0f)
        {
            return;
        }

        if (!IsInstanceValid(_shellCrackStompingPlayer))
        {
            EndShellCrackContactGrace();
            return;
        }

        if (HasSafeShellCrackSeparation(
            _shellCrackStompingPlayer))
        {
            _shellCrackSeparatedPhysicsFrames++;
        }
        else
        {
            _shellCrackSeparatedPhysicsFrames = 0;
        }

        // Two consecutive clear samples avoid restoring collision while the
        // shapes are only grazing the separation boundary.
        if (_shellCrackSeparatedPhysicsFrames >= 2)
        {
            EndShellCrackContactGrace();
        }
    }

    private bool HasSafeShellCrackSeparation(
        Player stompingPlayer)
    {
        Vector3 separation =
            stompingPlayer.GlobalPosition -
            GlobalPosition;
        float verticalSeparation =
            Mathf.Abs(separation.Y);
        separation.Y = 0.0f;

        // These clearances conservatively enclose the player's sphere and
        // the shell's box. The elapsed timer alone is not enough.
        return verticalSeparation >= 1.8f ||
            separation.LengthSquared() >= 2.25f * 2.25f;
    }

    private void EndShellCrackContactGrace()
    {
        _shellCrackContactGraceActive = false;
        _shellCrackContactGraceTimer = 0.0f;
        _shellCrackSeparatedPhysicsFrames = 0;
        if (!IsInstanceValid(_shellCrackStompingPlayer))
        {
            _shellCrackStompingPlayer = null;
            return;
        }

        RemoveCollisionExceptionWith(
            _shellCrackStompingPlayer);
        _shellCrackStompingPlayer
            .RemoveCollisionExceptionWith(this);
        _shellCrackStompingPlayer = null;
    }
}
