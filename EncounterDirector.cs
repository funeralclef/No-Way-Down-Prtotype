using Godot;
using System;

/// <summary>
/// Owns encounter lifetime, wave execution and temporary enemy spawning.
/// Main receives only coarse arena/UI events; EncounterPlanner owns the
/// scene-free selection policy.
/// </summary>
[GlobalClass]
public partial class EncounterDirector : Node
{
    private enum CombatPhrase
    {
        Relay,
        SpearLesson,
        NitroChase,
        ShepherdFork,
        TwinRelay,
        ShellPayoff
    }

    [Export]
    public EncounterSettings Settings { get; set; }

    public event Action<int> MobSquashed;
    public event Action<ArenaWavePlan> ArenaWaveStarted;
    public event Action<int> ArenaCompleted;

    public bool IsInitialized { get; private set; }
    public int ActiveArenaSection { get; private set; } = -1;
    public int ActiveEncounterId { get; private set; } = -1;
    public ulong PlanningSeed { get; private set; }

    private const int SpawnLaneCount =
        EncounterPlanner.SpawnLaneCount;

    private Timer _mobTimer;
    private Timer _arenaWaveTimer;
    private PackedScene _mobScene;
    private Node _spawnParent;
    private Player _player;
    private Camera3D _camera;
    private ProceduralWorld _world;
    private ScoreLabel _scoreHud;
    private EncounterPlanner _planner;
    private MobWavePattern? _pendingWavePattern;
    private int _pendingWaveCount;
    private int _lastDirectedPayoffChain;
    private int _nextColumnId = 1;
    private int _nextEncounterId = 1;
    private int _regularWavesSinceBreather;
    private int _currentLocation = -1;
    private int _overrunTier;
    private RunRouteStyle _chapterRouteStyle =
        RunRouteStyle.Neutral;
    private int _arenaWaveCount;
    private int _arenaWavesSpawned;
    private int _arenaEnemiesPending;
    private int _arenaPulsesPending;
    private int _arenaPulseIndex;
    private int _arenaWaveTotalPulses;
    private bool _arenaWaveAlive;
    private ArenaWaveStyle _currentArenaWaveStyle =
        ArenaWaveStyle.Crossfire;
    private float _arenaWaveBaseAngle;
    private float _arenaWaveSafeAngle;
    private float _arenaWaveTurnSign = 1.0f;
    private float _arenaObservedRadialRatio = 0.45f;
    private float _arenaObservedSpeedRatio = 0.65f;
    private float _arenaObservedTurnAmount = 0.35f;
    private float _arenaObservedTurnSign;
    private Vector3 _arenaLastObservedHeading;
    private float _arenaObservationTimer;

    public override void _Ready()
    {
        _mobTimer = GetNode<Timer>("MobTimer");
        _arenaWaveTimer =
            GetNode<Timer>("ArenaWaveTimer");
        _mobTimer.Timeout +=
            OnMobTimerTimeout;
        _arenaWaveTimer.Timeout +=
            OnArenaWaveTimerTimeout;
    }

    public void Initialize(
        PackedScene mobScene,
        Node spawnParent,
        Player player,
        Camera3D camera,
        ProceduralWorld world,
        ScoreLabel scoreHud,
        ulong planningSeed = 0)
    {
        if (IsInitialized &&
            _mobScene == mobScene &&
            _spawnParent == spawnParent &&
            _player == player &&
            _camera == camera &&
            _world == world &&
            _scoreHud == scoreHud)
        {
            return;
        }
        if (Settings == null)
        {
            throw new InvalidOperationException(
                "EncounterDirector requires EncounterSettings.");
        }

        _mobScene = mobScene ??
            throw new ArgumentNullException(
                nameof(mobScene));
        _spawnParent = spawnParent ??
            throw new ArgumentNullException(
                nameof(spawnParent));
        _player = player ??
            throw new ArgumentNullException(
                nameof(player));
        _camera = camera ??
            throw new ArgumentNullException(
                nameof(camera));
        _world = world ??
            throw new ArgumentNullException(
                nameof(world));
        _scoreHud = scoreHud ??
            throw new ArgumentNullException(
                nameof(scoreHud));
        PlanningSeed = planningSeed == 0
            ? GD.Randi()
            : planningSeed;
        _planner = new EncounterPlanner(
            Settings,
            PlanningSeed);
        IsInitialized = true;
    }

    public void SetCurrentLocation(int sectionIndex)
    {
        _currentLocation = sectionIndex;
    }

    public void SetRunContext(
        RunRouteStyle routeStyle,
        int overrunTier)
    {
        _chapterRouteStyle = routeStyle;
        _overrunTier = Mathf.Max(
            0,
            overrunTier);
    }

    public void OnComboImpact(
        int chainCount,
        int stylePoints,
        int flowTier,
        int archetype,
        bool counterStomp,
        bool milestone,
        bool finisher,
        Vector3 worldPosition)
    {
        if (!IsInitialized ||
            ActiveArenaSection >= 0 ||
            _pendingWavePattern != null ||
            chainCount <= _lastDirectedPayoffChain)
        {
            return;
        }

        MobWavePattern? payoffPattern =
            chainCount >= 10
                ? MobWavePattern.ShellPayoff
                : chainCount >= 6
                    ? MobWavePattern.TwinRelay
                    : null;
        if (!payoffPattern.HasValue)
        {
            return;
        }

        _lastDirectedPayoffChain = chainCount;
        _pendingWavePattern =
            payoffPattern.Value;
        _pendingWaveCount =
            _planner.GetWavePatternCount(
                payoffPattern.Value,
                _chapterRouteStyle,
                _overrunTier);
        if (_mobTimer.TimeLeft > 0.5f)
        {
            _mobTimer.Start(0.5f);
        }
    }

    public void OnComboEnded(
        int chainCount,
        int stylePoints)
    {
        _lastDirectedPayoffChain = 0;
    }

    public void SpawnTraversalEnemy(
        Vector3 worldPosition,
        Vector3 courseDirection,
        int relayIndex)
    {
        if (!IsInitialized ||
            !IsInstanceValid(_player) ||
            ActiveArenaSection >= 0)
        {
            return;
        }

        PrepareTraversalRelayPhrase(
            relayIndex);
        Mob relay = SpawnMob(
            worldPosition,
            courseDirection,
            6.0f,
            archetype:
                EnemyArchetypeId.Springboard);
        relay.ConfigureComboRoute(
            8.0f,
            relayIndex % 2 == 0
                ? 1.0f
                : -1.0f);
        relay.ConfigureTraversalRelayAnchor(
            worldPosition,
            relayIndex * 1.17f);
        relay.ConfigureStompOpportunity(true);
    }

    public void SpawnConstructionEnemy(
        Vector3 worldPosition,
        Vector3 courseDirection,
        int relayIndex,
        int archetype)
    {
        if (!IsInitialized ||
            !IsInstanceValid(_player) ||
            ActiveArenaSection >= 0)
        {
            return;
        }

        PrepareTraversalRelayPhrase(
            relayIndex);
        EnemyArchetypeId requested =
            Enum.IsDefined(
                typeof(EnemyArchetypeId),
                archetype)
                ? (EnemyArchetypeId)archetype
                : EnemyArchetypeId.Springboard;
        Mob relay = SpawnMob(
            worldPosition,
            courseDirection,
            requested == EnemyArchetypeId.Shell
                ? 4.8f
                : 6.0f,
            archetype: requested);
        relay.ConfigureComboRoute(
            8.4f,
            relayIndex % 2 == 0
                ? 1.0f
                : -1.0f);
        relay.ConfigureTraversalRelayAnchor(
            worldPosition,
            relayIndex * 1.17f);
        relay.ConfigureStompOpportunity(true);
    }

    public bool BeginArena(
        int sectionIndex,
        int waveCount)
    {
        if (!IsInitialized ||
            ActiveArenaSection >= 0)
        {
            return false;
        }

        _mobTimer.Stop();
        _pendingWavePattern = null;
        _pendingWaveCount = 0;
        ActiveArenaSection = sectionIndex;
        ActiveEncounterId =
            _nextEncounterId++;
        _arenaWaveCount = Mathf.Max(
            1,
            waveCount);
        _arenaWavesSpawned = 0;
        _arenaEnemiesPending = 0;
        _arenaPulsesPending = 0;
        _arenaPulseIndex = 0;
        _arenaWaveTotalPulses = 0;
        _arenaWaveAlive = false;
        ResetArenaTacticalRead();
        _arenaWaveTimer.Start(0.6f);
        return true;
    }

    public void PauseForReward()
    {
        _arenaWaveTimer.Stop();
        _mobTimer.Stop();
    }

    public void FinishArena(float breatherTime)
    {
        _planner?.ResetTravelDeck();
        _pendingWavePattern = null;
        _pendingWaveCount = 0;
        ActiveArenaSection = -1;
        ActiveEncounterId = -1;
        _arenaWaveCount = 0;
        _arenaWavesSpawned = 0;
        _arenaEnemiesPending = 0;
        _arenaPulsesPending = 0;
        _arenaPulseIndex = 0;
        _arenaWaveTotalPulses = 0;
        _arenaWaveAlive = false;
        _regularWavesSinceBreather = 0;
        _mobTimer.Start(
            Mathf.Max(
                1.0f,
                breatherTime));
    }

    public void RetreatActiveMobs()
    {
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob mob &&
                IsInstanceValid(mob) &&
                !mob.IsQueuedForDeletion())
            {
                mob.BeginPostArenaRetreat();
            }
        }
    }

    public void StopAll()
    {
        _mobTimer?.Stop();
        _arenaWaveTimer?.Stop();
    }

    public void UpdateArenaTacticalRead(
        float deltaSeconds)
    {
        if (ActiveArenaSection < 0 ||
            !IsInstanceValid(_player))
        {
            return;
        }

        _arenaObservationTimer +=
            deltaSeconds;
        if (_arenaObservationTimer < 0.18f)
        {
            return;
        }
        _arenaObservationTimer = 0.0f;

        GetActiveArenaCircle(
            out Vector3 center,
            out float radius);
        Vector3 offset =
            _player.GlobalPosition - center;
        offset.Y = 0.0f;
        float radialRatio = Mathf.Clamp(
            offset.Length() /
                Mathf.Max(1.0f, radius),
            0.0f,
            1.0f);
        Vector3 heading = new(
            _player.Velocity.X,
            0.0f,
            _player.Velocity.Z);
        float speedRatio =
            heading.Length() /
            Mathf.Max(
                1.0f,
                _player.CurrentRunSpeed);
        float turnAmount = 0.0f;
        float turnSign = 0.0f;
        if (heading.LengthSquared() > 0.25f &&
            _arenaLastObservedHeading
                .LengthSquared() > 0.25f)
        {
            Vector3 previous =
                _arenaLastObservedHeading.Normalized();
            Vector3 current = heading.Normalized();
            float signedTurn = previous
                .SignedAngleTo(
                    current,
                    Vector3.Up);
            turnAmount = Mathf.Clamp(
                Mathf.Abs(signedTurn) /
                    Mathf.DegToRad(45.0f),
                0.0f,
                1.0f);
            turnSign = Mathf.Sign(signedTurn);
        }
        if (heading.LengthSquared() > 0.25f)
        {
            _arenaLastObservedHeading =
                heading;
        }

        const float observationWeight = 0.18f;
        _arenaObservedRadialRatio = Mathf.Lerp(
            _arenaObservedRadialRatio,
            radialRatio,
            observationWeight);
        _arenaObservedSpeedRatio = Mathf.Lerp(
            _arenaObservedSpeedRatio,
            Mathf.Min(1.0f, speedRatio),
            observationWeight);
        _arenaObservedTurnAmount = Mathf.Lerp(
            _arenaObservedTurnAmount,
            turnAmount,
            observationWeight);
        _arenaObservedTurnSign = Mathf.Lerp(
            _arenaObservedTurnSign,
            turnSign,
            observationWeight * 0.7f);
    }

    private void OnMobTimerTimeout()
    {
        if (!IsInitialized ||
            !IsInstanceValid(_player) ||
            ActiveArenaSection >= 0)
        {
            return;
        }
        if (_pendingWavePattern == null)
        {
            _pendingWavePattern =
                _planner.ChooseMobWavePattern(
                    _scoreHud.Score,
                    _chapterRouteStyle);
            _pendingWaveCount =
                _planner.GetWavePatternCount(
                    _pendingWavePattern.Value,
                    _chapterRouteStyle,
                    _overrunTier);
        }

        int availableSlots =
            _planner.GetEffectiveActiveMobLimit(
                _chapterRouteStyle,
                _overrunTier) -
            GetActiveMobCount();
        if (availableSlots < _pendingWaveCount)
        {
            _mobTimer.Start(0.35f);
            return;
        }

        SpawnWavePattern(
            _pendingWavePattern.Value,
            _pendingWaveCount);
        _pendingWavePattern = null;
        _pendingWaveCount = 0;
        _regularWavesSinceBreather++;
        float delay =
            _planner.GetNextWaveDelay(
                ref _regularWavesSinceBreather,
                _currentLocation,
                GetActiveMobCount(),
                _chapterRouteStyle,
                _overrunTier);
        _mobTimer.Start(delay);
    }

    private void OnArenaWaveTimerTimeout()
    {
        if (!IsInitialized ||
            !IsInstanceValid(_player) ||
            ActiveArenaSection < 0)
        {
            return;
        }

        ResolveSpentArenaAttacks();
        if (_arenaPulsesPending > 0)
        {
            SpawnNextArenaPulse();
            _arenaWaveTimer.Start(
                EncounterPlanner
                    .GetArenaPulseInterval(
                        _currentArenaWaveStyle,
                        _overrunTier));
            return;
        }
        if (_arenaWaveAlive)
        {
            if (GetEncounterMobCount(
                    ActiveEncounterId) > 0)
            {
                _arenaWaveTimer.Start(0.25f);
                return;
            }

            _arenaWaveAlive = false;
            if (_arenaWavesSpawned >=
                _arenaWaveCount)
            {
                ArenaCompleted?.Invoke(
                    ActiveArenaSection);
                return;
            }
            _arenaWaveTimer.Start(0.4f);
            return;
        }

        GetActiveArenaCircle(
            out Vector3 arenaCenter,
            out _);
        ArenaWavePlan plan =
            _planner.CreateArenaWavePlan(
                _arenaWavesSpawned,
                _arenaWaveCount,
                _overrunTier,
                _player.GlobalPosition,
                _player.Velocity,
                arenaCenter,
                GetTacticalRead());
        _currentArenaWaveStyle =
            plan.Style;
        _arenaWaveSafeAngle =
            plan.SafeAngle;
        _arenaWaveBaseAngle =
            plan.BaseAngle;
        _arenaWaveTurnSign =
            plan.TurnSign;
        _arenaEnemiesPending =
            plan.EnemyBudget;
        _arenaPulsesPending =
            plan.PulseCount;
        _arenaWaveTotalPulses =
            plan.PulseCount;
        _arenaPulseIndex = 0;
        _arenaWavesSpawned++;
        _arenaWaveAlive = true;
        ArenaWaveStarted?.Invoke(plan);
        SpawnNextArenaPulse();
        _arenaWaveTimer.Start(
            EncounterPlanner.GetArenaPulseInterval(
                _currentArenaWaveStyle,
                _overrunTier));
    }

    private void PrepareTraversalRelayPhrase(
        int relayIndex)
    {
        if (relayIndex != 0)
        {
            return;
        }
        _pendingWavePattern = null;
        _pendingWaveCount = 0;
        _mobTimer.Start(3.8f);
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob mob &&
                mob.EncounterId < 0)
            {
                mob.BeginPostArenaRetreat();
            }
        }
    }

    private void SpawnNextArenaPulse()
    {
        if (_arenaPulsesPending <= 0 ||
            _arenaEnemiesPending <= 0)
        {
            _arenaPulsesPending = 0;
            _arenaEnemiesPending = 0;
            return;
        }

        int pulseEnemyCount = Mathf.CeilToInt(
            _arenaEnemiesPending /
            (float)_arenaPulsesPending);
        _arenaEnemiesPending -=
            pulseEnemyCount;
        _arenaPulsesPending--;
        SpawnArenaPulse(pulseEnemyCount);
        _arenaPulseIndex++;
    }

    private ArenaTacticalRead GetTacticalRead()
    {
        return new ArenaTacticalRead(
            _arenaObservedRadialRatio,
            _arenaObservedSpeedRatio,
            _arenaObservedTurnAmount,
            _arenaObservedTurnSign);
    }

    private void ResetArenaTacticalRead()
    {
        _arenaObservedRadialRatio = 0.45f;
        _arenaObservedSpeedRatio = 0.65f;
        _arenaObservedTurnAmount = 0.35f;
        _arenaObservedTurnSign = 0.0f;
        _arenaLastObservedHeading =
            Vector3.Zero;
        _arenaObservationTimer = 0.0f;
    }

    private void GetActiveArenaCircle(
        out Vector3 center,
        out float radius)
    {
        if (_world != null &&
            _world.TryGetArenaCircle(
                ActiveArenaSection,
                out center,
                out radius))
        {
            return;
        }
        center = _player.GlobalPosition;
        center.Y = 0.0f;
        radius = 34.0f;
    }

    private int GetActiveMobCount()
    {
        int activeMobCount = 0;
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob mob && mob.CountsForEncounter &&
                IsInstanceValid(mob) &&
                !mob.IsQueuedForDeletion() &&
                !mob.IsArenaAttackResolved)
            {
                activeMobCount++;
            }
        }
        return activeMobCount;
    }

    private int GetEncounterMobCount(
        int encounterId)
    {
        int count = 0;
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob mob && mob.CountsForEncounter &&
                IsInstanceValid(mob) &&
                !mob.IsQueuedForDeletion() &&
                !mob.IsArenaAttackResolved &&
                mob.EncounterId == encounterId)
            {
                count++;
            }
        }
        return count;
    }

    private void ResolveSpentArenaAttacks()
    {
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion() ||
                mob.IsArenaAttackResolved ||
                mob.IsArenaStrikePending ||
                mob.EncounterId !=
                    ActiveEncounterId)
            {
                continue;
            }

            Vector3 separation =
                mob.GlobalPosition -
                _player.GlobalPosition;
            separation.Y = 0.0f;
            Vector3 relativeVelocity =
                mob.Velocity -
                _player.Velocity;
            relativeVelocity.Y = 0.0f;
            float relativeSpeedSquared =
                relativeVelocity.LengthSquared();
            float timeSinceClosestApproach =
                relativeSpeedSquared > 0.01f
                    ? separation.Dot(
                        relativeVelocity) /
                        relativeSpeedSquared
                    : 0.0f;
            bool movingSafelyAway =
                timeSinceClosestApproach >=
                Settings.ArenaMobRetireGraceTime;
            bool spentAttack =
                mob.EncounterAge >=
                    Settings
                        .ArenaMobMinimumThreatTime &&
                movingSafelyAway &&
                separation.Length() >=
                    Settings.ArenaMobRetireDistance;
            bool expiredFarAway =
                mob.EncounterAge >=
                    Settings.ArenaMobMaxLifetime &&
                separation.Length() >=
                    Mathf.Max(
                        14.0f,
                        Settings
                            .ArenaMobRetireDistance) &&
                separation.Dot(
                    relativeVelocity) >= 0.0f;
            if (spentAttack || expiredFarAway)
            {
                mob.ResolveArenaAttack();
            }
        }
    }

	private void SpawnWavePattern(MobWavePattern pattern, int count)
	{
		switch (pattern)
		{
			case MobWavePattern.Relay:
				SpawnCombatPhrase(
					CombatPhrase.Relay,
					count);
				break;
			case MobWavePattern.SpearLesson:
				SpawnCombatPhrase(
					CombatPhrase.SpearLesson,
					count);
				break;
			case MobWavePattern.NitroChase:
				SpawnCombatPhrase(
					CombatPhrase.NitroChase,
					count);
				break;
			case MobWavePattern.ShepherdFork:
				SpawnCombatPhrase(
					CombatPhrase.ShepherdFork,
					count);
				break;
			case MobWavePattern.TwinRelay:
				SpawnCombatPhrase(
					CombatPhrase.TwinRelay,
					count);
				break;
			case MobWavePattern.ShellPayoff:
				SpawnCombatPhrase(
					CombatPhrase.ShellPayoff,
					count);
				break;
			case MobWavePattern.ComboRoute:
				SpawnComboRoute(count);
				break;
			case MobWavePattern.Column:
				SpawnColumn(count);
				break;
			case MobWavePattern.Row:
				SpawnRow(count);
				break;
			case MobWavePattern.Echelon:
				SpawnEchelon(count);
				break;
			case MobWavePattern.Crossing:
				SpawnCrossing(count);
				break;
			case MobWavePattern.Scatter:
				SpawnScatter(count);
				break;
			case MobWavePattern.SplitColumns:
				SpawnSplitColumns(count);
				break;
			case MobWavePattern.Chevron:
				SpawnChevron(count);
				break;
			case MobWavePattern.BaitAndPounce:
				SpawnBaitAndPounce(count);
				break;
			default:
				SpawnSoloGroup(count);
				break;
		}
	}

	private void SpawnCombatPhrase(
		CombatPhrase phrase,
		int count)
	{
		bool hasSeparateThreat =
			phrase == CombatPhrase.SpearLesson ||
			phrase == CombatPhrase.ShepherdFork ||
			phrase == CombatPhrase.TwinRelay;
		int routeCount = Mathf.Max(
			4,
			count - (hasSeparateThreat ? 1 : 0));
		int routeId = _nextColumnId++;
		int lane = GetNextSpawnLane();
		Vector3 leaderPosition =
			GetTopSpawnPosition(lane);
		leaderPosition.X = Mathf.MoveToward(
			_player.GlobalPosition.X,
			leaderPosition.X,
			5.5f);
		Vector3 streamDirection =
			GetLongitudinalDirectionToPlayer(
				leaderPosition);
		Vector3 routeDirection =
			GetReadableLongitudinalAttackDirection(
				leaderPosition,
				streamDirection,
				0.28f);
		float speed = Mathf.Clamp(
			GetRandomMobSpeed() * 0.76f,
			7.8f,
			10.4f);
		float spacing =
			GetDirectedComboSpacing(speed);
		float depth = 0.0f;
		float phraseSign =
			GD.Randf() < 0.5f ? -1.0f : 1.0f;
		float previousX = leaderPosition.X;
		float previousDepth = depth;

		for (int index = 0;
			index < routeCount;
			index++)
		{
			EnemyArchetypeId archetype =
				GetPhraseArchetype(
					phrase,
					index,
					routeCount);
			float lateralOffset =
				GetPhraseLateralOffset(
					phrase,
					index,
					routeCount,
					phraseSign);
			float desiredX =
				leaderPosition.X +
				lateralOffset;
			if (index > 0)
			{
				float maximumStep =
					GetMaximumComboLateralStep(
						Mathf.Max(
							0.1f,
							depth - previousDepth),
						speed);
				desiredX = Mathf.Clamp(
					desiredX,
					previousX - maximumStep,
					previousX + maximumStep);
			}

			Vector3 spawnPosition =
				leaderPosition -
				streamDirection * depth;
			spawnPosition.X = desiredX;
			float archetypeSpeed =
				archetype ==
					EnemyArchetypeId.Courier
					? speed * 0.92f
					: speed;
			Mob mob = SpawnMob(
				spawnPosition,
				routeDirection,
				archetypeSpeed,
				routeId,
				index,
				archetype);
			if (archetype ==
				EnemyArchetypeId.Twin)
			{
				mob.ConfigureTwinMotion(
					index % 2 == 0
						? phraseSign
						: -phraseSign,
					index % 2 == 0
						? 0.0f
						: Mathf.Pi);
				mob.ConfigureStompOpportunity(true);
			}
			else if (archetype ==
					EnemyArchetypeId.Springboard ||
				archetype ==
					EnemyArchetypeId.Shell)
			{
				mob.ConfigureComboRoute(
					5.2f +
						depth /
							Mathf.Max(1.0f, speed),
					Mathf.Sign(
						Mathf.IsZeroApprox(lateralOffset)
							? phraseSign
							: lateralOffset));
			}
			else
			{
				mob.ConfigureStompOpportunity(true);
			}

			previousX = desiredX;
			previousDepth = depth;
			depth +=
				spacing *
				GetComboGrowthSpacingScale(index + 1) *
				(index % 3 == 1 ? 1.08f : 0.96f);
		}

		if (!hasSeparateThreat)
		{
			return;
		}

		bool fromRight =
			phraseSign > 0.0f;
		Vector3 threatPosition =
			phrase == CombatPhrase.SpearLesson
				? GetSideSpawnPosition(
					fromRight,
					0.22f)
				: GetTopSpawnPosition(
					fromRight ? 0 : SpawnLaneCount - 1);
		EnemyArchetypeId threatType =
			phrase == CombatPhrase.SpearLesson
				? (_scoreHud.Score >= 70
					? EnemyArchetypeId.Diver
					: EnemyArchetypeId.Spear)
				: phrase == CombatPhrase.TwinRelay
					? EnemyArchetypeId.Diver
					: EnemyArchetypeId.Shepherd;
		Vector3 threatDirection =
			GetAttackDirection(
				threatPosition,
				phrase ==
					CombatPhrase.SpearLesson
					? 3.6f
					: 6.0f);
		Mob threat = SpawnMob(
			threatPosition,
			threatDirection,
			GetRandomMobSpeed() *
				(phrase ==
					CombatPhrase.SpearLesson
					? 1.04f
					: phrase ==
						CombatPhrase.TwinRelay
						? 0.98f
						: 0.9f),
			archetype: threatType);
		if (phrase == CombatPhrase.SpearLesson ||
			phrase == CombatPhrase.TwinRelay)
		{
			threat.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.Challenger,
				5.4f,
				activationDelay: 0.68f,
				weaveSign:
					fromRight ? -1.0f : 1.0f);
		}
	}

	private static EnemyArchetypeId GetPhraseArchetype(
		CombatPhrase phrase,
		int index,
		int count)
	{
		return phrase switch
		{
			CombatPhrase.Relay
				when index == count - 2 =>
				EnemyArchetypeId.Courier,
			CombatPhrase.Relay
				when index > 0 &&
					index % 2 == 1 =>
				EnemyArchetypeId.Relay,
			CombatPhrase.NitroChase
				when index == 2 ||
					index == count - 2 =>
				EnemyArchetypeId.Courier,
			CombatPhrase.ShepherdFork
				when index == count - 2 =>
				EnemyArchetypeId.Trickster,
			CombatPhrase.ShepherdFork
				when index == count / 2 ||
					index == count / 2 + 1 =>
				EnemyArchetypeId.Twin,
			CombatPhrase.TwinRelay
				when index == count - 2 =>
				EnemyArchetypeId.Courier,
			CombatPhrase.TwinRelay
				when index == count / 2 ||
					index == count / 2 + 1 =>
				EnemyArchetypeId.Twin,
			CombatPhrase.TwinRelay
				when index > 0 =>
				EnemyArchetypeId.Relay,
			CombatPhrase.ShellPayoff
				when index == count / 2 =>
				EnemyArchetypeId.Shell,
			CombatPhrase.ShellPayoff
				when index == count - 2 =>
				EnemyArchetypeId.Courier,
			_ => EnemyArchetypeId.Springboard
		};
	}

	private static float GetPhraseLateralOffset(
		CombatPhrase phrase,
		int index,
		int count,
		float sign)
	{
		float progress = count <= 1
			? 1.0f
			: index / (float)(count - 1);
		return phrase switch
		{
			CombatPhrase.Relay =>
				Mathf.Sin(index * 0.92f) *
					2.5f * sign,
			CombatPhrase.SpearLesson =>
				Mathf.Sin(progress * Mathf.Pi) *
					2.8f * sign,
			CombatPhrase.NitroChase =>
				Mathf.Lerp(0.0f, 5.2f, progress) *
					sign +
				Mathf.Sin(index * 1.4f) * 0.8f,
			CombatPhrase.ShepherdFork =>
				(index % 2 == 0 ? -1.0f : 1.0f) *
					3.6f * sign,
			CombatPhrase.TwinRelay =>
				(index < count / 2
					? Mathf.Sin(index * 0.86f) * 2.2f
					: (index % 2 == 0 ? -3.8f : 3.8f)) *
				sign,
			_ =>
				Mathf.Sin(index * 1.18f) *
					3.1f * sign
		};
	}

	private void SpawnComboRoute(int count)
	{
		int sidecarCount = count >= 10 ? 2 : 1;
		int routeCount = Mathf.Max(7, count - sidecarCount);
		int routeId = _nextColumnId++;
		int lane = GetNextSpawnLane();
		Vector3 lanePosition = GetTopSpawnPosition(lane);
		Vector3 leaderPosition = lanePosition;
		leaderPosition.X = Mathf.MoveToward(
			_player.GlobalPosition.X,
			lanePosition.X,
			6.5f);
		Vector3 streamDirection =
			GetLongitudinalDirectionToPlayer(leaderPosition);
		Vector3 sharedRouteDirection =
			GetReadableLongitudinalAttackDirection(
				leaderPosition,
				streamDirection,
				0.32f);
		float routeSpeed = Mathf.Clamp(
			GetRandomMobSpeed() * 0.78f,
			7.5f,
			10.5f);
		float directedSpacing =
			GetDirectedComboSpacing(routeSpeed);
		float depth = (float)GD.RandRange(0.0f, 1.8f);
		float weavePhase = (float)GD.RandRange(0.0f, Mathf.Tau);
		float weaveSign = GD.Randf() < 0.5f ? -1.0f : 1.0f;
		float phraseAmplitude =
			(float)GD.RandRange(2.7f, 4.0f);
		int phraseIndex = GD.RandRange(0, 3);
		int rhythmIndex = GD.RandRange(0, 3);
		float previousRouteX = leaderPosition.X;
		float previousDepth = depth;

		for (int index = 0; index < routeCount; index++)
		{
			float progress = routeCount <= 1
				? 1.0f
				: index / (float)(routeCount - 1);
			float routeProgress =
				progress * progress * (3.0f - 2.0f * progress);
			float routeX = Mathf.Lerp(
				leaderPosition.X,
				lanePosition.X,
				routeProgress);
			float routeWeave = GetComboPhraseOffset(
				phraseIndex,
				index,
				routeCount,
				phraseAmplitude,
				weavePhase,
				weaveSign);
			float desiredRouteX = routeX + routeWeave;
			if (index > 0)
			{
				float longitudinalGap =
					Mathf.Max(0.1f, depth - previousDepth);
				float maximumLateralStep =
					GetMaximumComboLateralStep(
						longitudinalGap,
						routeSpeed);
				desiredRouteX = Mathf.Clamp(
					desiredRouteX,
					previousRouteX - maximumLateralStep,
					previousRouteX + maximumLateralStep);
			}

			Vector3 spawnPosition =
				leaderPosition -
				streamDirection * depth;
			spawnPosition.X = desiredRouteX;
			bool newRouteRolesUnlocked =
				(_scoreHud?.Score ?? 0) >= 55;
			EnemyArchetypeId routeArchetype =
				index == 0
					? EnemyArchetypeId.Springboard
					: index == routeCount - 2
						? EnemyArchetypeId.Courier
						: newRouteRolesUnlocked &&
							(index == routeCount / 2 ||
								index == routeCount / 2 + 1)
							? EnemyArchetypeId.Twin
							: newRouteRolesUnlocked &&
								index % 4 == 1
								? EnemyArchetypeId.Relay
								: EnemyArchetypeId.Springboard;
			Mob mob = SpawnMob(
				spawnPosition,
				sharedRouteDirection,
				routeSpeed *
					(float)GD.RandRange(0.97f, 1.03f),
				routeId,
				index,
				routeArchetype);
			if (routeArchetype ==
				EnemyArchetypeId.Twin)
			{
				mob.ConfigureTwinMotion(
					index % 2 == 0
						? weaveSign
						: -weaveSign,
					index % 2 == 0
						? 0.0f
						: Mathf.Pi);
				mob.ConfigureStompOpportunity(true);
			}
			else if (routeArchetype ==
				EnemyArchetypeId.Relay)
			{
				mob.ConfigureStompOpportunity(true);
			}
			else
			{
				mob.ConfigureComboRoute(
					5.0f +
						depth / Mathf.Max(1.0f, routeSpeed),
					Mathf.Sign(
						Mathf.IsZeroApprox(routeWeave)
							? weaveSign
							: routeWeave));
			}

			previousRouteX = desiredRouteX;
			previousDepth = depth;
			depth +=
				directedSpacing *
				GetComboGrowthSpacingScale(index + 1) *
				GetComboRhythmSpacingScale(
					rhythmIndex,
					index + 1) *
				(float)GD.RandRange(0.94f, 1.04f);
		}

		SpawnComboRouteSidecars(
			sidecarCount,
			lane,
			streamDirection,
			routeSpeed,
			directedSpacing,
			depth);
	}

	private void SpawnComboRouteSidecars(
		int count,
		int primaryLane,
		Vector3 streamDirection,
		float routeSpeed,
		float directedSpacing,
		float routeDepth)
	{
		int outerLane = primaryLane <= 1
			? SpawnLaneCount - 1
			: primaryLane >= SpawnLaneCount - 2
				? 0
				: (GD.Randf() < 0.5f
					? 0
					: SpawnLaneCount - 1);
		int innerLane = outerLane == 0
			? 1
			: SpawnLaneCount - 2;
		if (primaryLane == SpawnLaneCount / 2)
		{
			innerLane = outerLane == 0
				? SpawnLaneCount - 1
				: 0;
		}
		for (int index = 0; index < count; index++)
		{
			int lane = index == 0 ? outerLane : innerLane;

			Vector3 spawnPosition = GetTopSpawnPosition(lane);
			float depth = Mathf.Clamp(
				directedSpacing * (2.0f + index * 2.75f),
				directedSpacing,
				Mathf.Max(directedSpacing, routeDepth - directedSpacing));
			spawnPosition -= streamDirection * depth;
			bool twinSidecar =
				(_scoreHud?.Score ?? 0) >= 80;
			Mob sidecar = SpawnMob(
				spawnPosition,
				streamDirection,
				routeSpeed *
					(float)GD.RandRange(0.88f, 0.94f),
				archetype: twinSidecar
					? EnemyArchetypeId.Twin
					: EnemyArchetypeId.Springboard);
			if (twinSidecar)
			{
				sidecar.ConfigureTwinMotion(
					index % 2 == 0 ? -1.0f : 1.0f,
					index * Mathf.Pi);
			}
			else
			{
				sidecar.ConfigureComboRoute(
					6.0f +
						depth / Mathf.Max(1.0f, routeSpeed),
					index % 2 == 0 ? -1.0f : 1.0f);
			}
			sidecar.ConfigureStompOpportunity(true);
		}
	}

	private float GetDirectedComboSpacing(float enemySpeed)
	{
		if (!IsInstanceValid(_player))
		{
			return 8.5f;
		}

		float gravity = Mathf.Max(
			1.0f,
			_player.FallAcceleration);
		float bounceFlightTime =
			2.0f *
			_player.ProjectedBounceLaunchSpeed /
			gravity;
		float closingSpeed =
			Mathf.Max(
				6.0f,
				_player.CurrentRunSpeed +
					Mathf.Max(0.0f, enemySpeed));
		// The stomp happens before the mathematical end of the arc. Keeping
		// authored spacing inside this window lets the director offer a line
		// without guaranteeing that the player accepts it.
		return Mathf.Clamp(
			closingSpeed * bounceFlightTime * 0.86f,
			7.2f,
			12.8f);
	}

	private static float GetComboGrowthSpacingScale(int linkIndex)
	{
		// Later links open up just enough to let the growing air-combo reach
		// matter, without turning the route into disconnected islands.
		return 1.0f +
			Mathf.Min(5, Mathf.Max(0, linkIndex - 1)) *
				0.018f;
	}

	private static float GetComboRhythmSpacingScale(
		int rhythmIndex,
		int linkIndex)
	{
		int beat = Mathf.Max(0, linkIndex - 1) % 4;
		return rhythmIndex switch
		{
			0 => beat switch
			{
				0 => 0.92f,
				1 => 1.08f,
				2 => 0.96f,
				_ => 1.16f
			},
			1 => beat switch
			{
				0 => 1.12f,
				1 => 0.93f,
				2 => 1.04f,
				_ => 0.96f
			},
			2 => beat switch
			{
				0 => 0.96f,
				1 => 1.16f,
				2 => 0.93f,
				_ => 1.06f
			},
			_ => beat switch
			{
				0 => 0.94f,
				1 => 0.99f,
				2 => 1.14f,
				_ => 0.96f
			}
		};
	}

	private float GetMaximumComboLateralStep(
		float authoredLongitudinalGap,
		float enemySpeed)
	{
		float gravity = IsInstanceValid(_player)
			? Mathf.Max(1.0f, _player.FallAcceleration)
			: 75.0f;
		float bounceSpeed = IsInstanceValid(_player)
			? _player.ProjectedBounceLaunchSpeed
			: 16.0f;
		float bounceFlightTime =
			2.0f * bounceSpeed / gravity;
		float contactGap = Mathf.Max(
			1.9f,
			authoredLongitudinalGap -
				Mathf.Max(0.0f, enemySpeed) *
					bounceFlightTime);
		// About 25 degrees after accounting for the target's own movement.
		// This stays below the director's turn cone and leaves a little room
		// for the player's input to shape the landing.
		return Mathf.Max(0.9f, contactGap * 0.46f);
	}

	private static float GetComboPhraseOffset(
		int phraseIndex,
		int memberIndex,
		int memberCount,
		float amplitude,
		float phase,
		float directionSign)
	{
		float progress = memberCount <= 1
			? 1.0f
			: memberIndex / (float)(memberCount - 1);
		float entranceWeight = Mathf.Lerp(
			0.28f,
			1.0f,
			Mathf.Clamp(memberIndex / 2.0f, 0.0f, 1.0f));
		float offset = phraseIndex switch
		{
			0 => Mathf.Sin(memberIndex * 1.16f + phase),
			1 =>
				Mathf.Sin(progress * Mathf.Tau * 1.2f + phase) *
					0.84f +
				Mathf.Sin(memberIndex * 2.05f + phase) *
					0.18f,
			2 =>
				Mathf.Asin(
					Mathf.Sin(memberIndex * 0.88f + phase)) *
				(2.0f / Mathf.Pi),
			_ =>
				Mathf.Sin(progress * Mathf.Tau + phase) *
					0.68f +
				Mathf.Sin(memberIndex * 1.52f - phase) *
					0.32f
		};
		return offset *
			Mathf.Max(0.0f, amplitude) *
			entranceWeight *
			Mathf.Sign(directionSign);
	}

	private void SpawnArenaPulse(int enemyCount)
	{
		GetActiveArenaCircle(out Vector3 arenaCenter, out float arenaRadius);
		float wavePressure = Mathf.Clamp(
			(_arenaWavesSpawned - 1) /
				(float)Mathf.Max(1, _arenaWaveCount - 1),
			0.0f,
			1.0f);
		float minimumSpeedMultiplier = Mathf.Min(
			Settings.ArenaMinSpeedMultiplier,
			Settings.ArenaMaxSpeedMultiplier);
		float maximumSpeedMultiplier = Mathf.Max(
			Settings.ArenaMinSpeedMultiplier,
			Settings.ArenaMaxSpeedMultiplier);
		float speedMultiplier = Mathf.Lerp(
			minimumSpeedMultiplier,
			maximumSpeedMultiplier,
			wavePressure);
		speedMultiplier *=
			GetOverrunSpeedScale();
		float pulseRotation = _arenaWaveTurnSign *
			_arenaPulseIndex *
			(_currentArenaWaveStyle switch
			{
				ArenaWaveStyle.SpiralRelay => 0.31f,
				ArenaWaveStyle.Pinwheel => 0.16f,
				ArenaWaveStyle.AmbushRing => 0.1f,
				_ => 0.075f
			});
		Vector3 safeDirection = new(
			Mathf.Cos(_arenaWaveSafeAngle),
			0.0f,
			Mathf.Sin(_arenaWaveSafeAngle));
		Vector3 safeLaneAxis =
			Vector3.Up.Cross(safeDirection).Normalized();

		for (int index = 0; index < enemyCount; index++)
		{
			float angle;
			float centeredSlot =
				index - (enemyCount - 1) * 0.5f;
			float laneOffset = Mathf.Clamp(
				centeredSlot * 2.35f,
				-7.2f,
				7.2f);
			bool openingWave =
				_arenaWavesSpawned <= 1;
			bool climaxWave =
				_arenaWavesSpawned >=
					_arenaWaveCount;
			int strikerCadence = openingWave
				? 6
				: climaxWave
					? 3
					: 4;
			bool designatedStriker =
				(index + _arenaPulseIndex * 2) %
					strikerCadence == 0;
			bool payoffTarget =
				_arenaPulseIndex ==
					Mathf.Max(0, _arenaWaveTotalPulses - 1) &&
				index == enemyCount - 1;
			if (payoffTarget)
			{
				designatedStriker = true;
			}

			Mob.ArenaMotionStyle motionStyle =
				Mob.ArenaMotionStyle.StraightPass;
			float activationDelay = 0.0f;
			switch (_currentArenaWaveStyle)
			{
				case ArenaWaveStyle.Pincer:
					int pincerSide = index % 2;
					int pincerRank = index / 2;
					float fan =
						(pincerRank -
							(enemyCount - 1) * 0.24f) *
						0.085f;
					angle =
						_arenaWaveBaseAngle +
						pincerSide * Mathf.Pi +
						fan +
						pulseRotation;
					break;
				case ArenaWaveStyle.Pinwheel:
					angle = GetArenaAngleOutsideSafeSector(
						index,
						enemyCount,
						pulseRotation);
					motionStyle = Mob.ArenaMotionStyle.ArcSweep;
					break;
				case ArenaWaveStyle.SpiralRelay:
					angle = GetArenaAngleOutsideSafeSector(
						index,
						enemyCount,
						pulseRotation +
							_arenaWaveTurnSign * index * 0.08f);
					motionStyle = Mob.ArenaMotionStyle.ArcSweep;
					break;
				case ArenaWaveStyle.AmbushRing:
					angle = GetArenaAngleOutsideSafeSector(
						index,
						enemyCount,
						pulseRotation);
					motionStyle =
						Mob.ArenaMotionStyle.OrbitThenStrike;
					activationDelay =
						0.58f +
						index * 0.14f +
						_arenaPulseIndex * 0.1f;
					break;
				default:
					int crossSide = index % 2;
					int crossRank = index / 2;
					angle =
						_arenaWaveBaseAngle +
						crossSide * Mathf.Pi +
						crossRank * 0.13f *
							(crossSide == 0 ? 1.0f : -1.0f) +
						pulseRotation;
					break;
			}

			Vector3 radial =
				new(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
			Vector3 spawnPosition =
				arenaCenter +
				radial * Mathf.Max(8.0f, arenaRadius - 1.35f);
			float predictionTime =
				designatedStriker ? 0.3f : 0.12f;
			Vector3 targetPosition =
				designatedStriker
					? _player.GlobalPosition +
						new Vector3(
							_player.Velocity.X,
							0.0f,
							_player.Velocity.Z) *
							predictionTime
					: arenaCenter +
						safeLaneAxis * laneOffset;
			Vector3 tangent =
				Vector3.Up.Cross(radial).Normalized();
			targetPosition.Y = spawnPosition.Y;
			Vector3 direction =
				(targetPosition - spawnPosition).Normalized();

			if (_currentArenaWaveStyle ==
				ArenaWaveStyle.Pinwheel)
			{
				direction = (-radial)
					.Rotated(
						Vector3.Up,
						Mathf.DegToRad(20.0f) *
							_arenaWaveTurnSign)
					.Normalized();
			}
			else if (_currentArenaWaveStyle ==
				ArenaWaveStyle.SpiralRelay)
			{
				direction = direction
					.Rotated(
						Vector3.Up,
						Mathf.DegToRad(11.0f) *
							_arenaWaveTurnSign)
					.Normalized();
			}
			else if (_currentArenaWaveStyle ==
				ArenaWaveStyle.AmbushRing)
			{
				direction = tangent * _arenaWaveTurnSign;
			}

			if (payoffTarget)
			{
				motionStyle = Mob.ArenaMotionStyle.StraightPass;
				activationDelay = 0.0f;
				speedMultiplier *= 0.9f;
			}

			EnemyArchetypeId arenaArchetype;
			int currentScore =
				_scoreHud?.Score ?? 0;
			bool advancedRolesUnlocked =
				currentScore >= 100;
			bool routeRolesUnlocked =
				currentScore >= 55;
			bool shellUnlocked =
				currentScore >= 180;
			if (payoffTarget)
			{
				arenaArchetype =
					climaxWave &&
					shellUnlocked
						? EnemyArchetypeId.Shell
						: EnemyArchetypeId.Courier;
			}
			else if (designatedStriker)
			{
				arenaArchetype =
					routeRolesUnlocked &&
					wavePressure >= 0.34f &&
					(index + _arenaPulseIndex) % 3 == 0
						? EnemyArchetypeId.Diver
						: _currentArenaWaveStyle ==
							ArenaWaveStyle.AmbushRing &&
						advancedRolesUnlocked &&
						(index + _arenaPulseIndex) % 2 == 1
						? EnemyArchetypeId.Trickster
						: EnemyArchetypeId.Spear;
			}
			else if (shellUnlocked &&
				wavePressure >= 0.52f &&
				index == 1)
			{
				arenaArchetype =
					EnemyArchetypeId.Shell;
			}
			else if (_currentArenaWaveStyle ==
					ArenaWaveStyle.Pinwheel ||
				_currentArenaWaveStyle ==
					ArenaWaveStyle.SpiralRelay ||
				_currentArenaWaveStyle ==
					ArenaWaveStyle.Pincer)
			{
				arenaArchetype =
					routeRolesUnlocked &&
					(index + _arenaPulseIndex) % 3 == 1
						? EnemyArchetypeId.Twin
						: advancedRolesUnlocked
							? EnemyArchetypeId.Shepherd
							: routeRolesUnlocked
								? EnemyArchetypeId.Relay
								: EnemyArchetypeId.Springboard;
			}
			else
			{
				arenaArchetype =
					EnemyArchetypeId.Springboard;
			}

			Mob mob = SpawnMob(
				spawnPosition,
				direction,
				Mathf.Max(
					Settings.ArenaMinimumMobSpeed,
					Mathf.Lerp(
						Settings.MinMobSpeed,
						Settings.MaxMobSpeed,
						0.58f) *
						speedMultiplier *
						(0.97f + index % 3 * 0.025f)),
				archetype: arenaArchetype);
			float missSide = Mathf.IsZeroApprox(laneOffset)
				? (index % 2 == 0 ? -1.0f : 1.0f)
				: Mathf.Sign(laneOffset);
			Vector3 strikeOffset = designatedStriker
				? Vector3.Zero
				: safeLaneAxis * missSide *
					Mathf.Max(3.8f, Mathf.Abs(laneOffset));
			mob.ConfigureArenaMotion(
				motionStyle,
				arenaCenter,
				_arenaWaveTurnSign,
				activationDelay,
				strikeOffset);
			if (arenaArchetype ==
				EnemyArchetypeId.Twin)
			{
				mob.ConfigureTwinMotion(
					index % 2 == 0
						? _arenaWaveTurnSign
						: -_arenaWaveTurnSign,
					index % 2 == 0
						? 0.0f
						: Mathf.Pi);
			}
			mob.ConfigureStompOpportunity(payoffTarget);
		}
	}

	private float GetArenaAngleOutsideSafeSector(
		int index,
		int count,
		float rotation)
	{
		const float safeSectorSize = 0.92f;
		float usableArc = Mathf.Tau - safeSectorSize;
		float ratio =
			(index + 0.5f) / Mathf.Max(1, count);
		return
			_arenaWaveSafeAngle +
			safeSectorSize * 0.5f +
			usableArc * ratio +
			rotation;
	}

		private void SpawnColumn(int count, int forcedLane = -1)
		{
			int columnId = _nextColumnId++;
			int lane = forcedLane >= 0 ? forcedLane : GetNextSpawnLane();
			Vector3 leaderPosition = GetTopSpawnPosition(lane);
			Vector3 streamDirection =
				GetLongitudinalDirectionToPlayer(leaderPosition);
			Vector3 sharedColumnDirection =
				GetReadableLongitudinalAttackDirection(
					leaderPosition,
					streamDirection,
					0.38f);
			float speed = GetRandomMobSpeed();
			float distanceBehindLeader = (float)GD.RandRange(0.0f, 3.0f);
			float lateralDrift = (float)GD.RandRange(
				-Settings.MaxColumnLateralOffset * 0.28f,
				Settings.MaxColumnLateralOffset * 0.28f);
			float weavePhase = (float)GD.RandRange(0.0f, Mathf.Tau);
			float weaveSign = GD.Randf() < 0.5f ? -1.0f : 1.0f;
			float phraseAmplitude = (float)GD.RandRange(
				Settings.MaxColumnLateralOffset * 0.58f,
				Settings.MaxColumnLateralOffset);
			int phraseIndex = GD.RandRange(0, 3);
			int rhythmIndex = GD.RandRange(0, 3);
			float previousSpawnX = leaderPosition.X;
			float previousDepth = distanceBehindLeader;

			for (int index = 0; index < count; index++)
			{
				if (index > 0)
				{
					distanceBehindLeader +=
						GetNextColumnSpacing(
							index,
							speed,
							rhythmIndex);
				}

				float lateralOffset =
					lateralDrift +
					GetComboPhraseOffset(
						phraseIndex,
						index,
						count,
						phraseAmplitude,
						weavePhase,
						weaveSign);
				float desiredSpawnX =
					leaderPosition.X + lateralOffset;
				if (index > 0)
				{
					float longitudinalGap = Mathf.Max(
						0.1f,
						distanceBehindLeader - previousDepth);
					float maximumLateralStep =
						GetMaximumComboLateralStep(
							longitudinalGap,
							speed);
					desiredSpawnX = Mathf.Clamp(
						desiredSpawnX,
						previousSpawnX - maximumLateralStep,
						previousSpawnX + maximumLateralStep);
				}

				Vector3 spawnPosition =
					leaderPosition -
					streamDirection * distanceBehindLeader;
				spawnPosition.X = desiredSpawnX;
				Mob mob = SpawnMob(
					spawnPosition,
					sharedColumnDirection,
					speed * (float)GD.RandRange(0.96f, 1.04f),
					columnId,
					index);
				mob.ConfigureTravelCombatRole(
					Mob.TravelCombatRole.LaneRunner,
					4.6f +
						distanceBehindLeader /
							Mathf.Max(1.0f, speed),
					weaveSign:
						Mathf.Sign(
							Mathf.IsZeroApprox(lateralOffset)
								? weaveSign
								: lateralOffset));
				previousSpawnX = desiredSpawnX;
				previousDepth = distanceBehindLeader;
			}
		}

		private float GetNextColumnSpacing(
			int memberIndex,
			float enemySpeed,
			int rhythmIndex)
		{
			float authoredMinimum =
				Mathf.Min(Settings.MinColumnSpacing, Settings.MaxColumnSpacing);
			float authoredMaximum =
				Mathf.Max(Settings.MinColumnSpacing, Settings.MaxColumnSpacing);
			float directedSpacing =
				GetDirectedComboSpacing(enemySpeed);
			float minimum = Mathf.Max(
				7.2f,
				Mathf.Min(
					authoredMinimum,
					directedSpacing * 0.96f));
			float maximum = Mathf.Max(
				minimum,
				Mathf.Min(
					authoredMaximum,
					directedSpacing * 1.1f));
			float spacing = (float)GD.RandRange(minimum, maximum);
			float rhythmicSpacing =
				spacing *
				GetComboGrowthSpacingScale(memberIndex) *
				GetComboRhythmSpacingScale(
					rhythmIndex,
					memberIndex);
			return Mathf.Clamp(
				rhythmicSpacing,
				5.8f,
				authoredMaximum);
		}

	private void SpawnSoloGroup(int count)
	{
		for (int index = 0; index < count; index++)
		{
			Vector3 spawnPosition = GD.Randf() < Settings.SoloSideChance
				? GetSideSpawnPosition()
				: GetTopSpawnPosition(GetNextSpawnLane());
			Vector3 direction = GetAttackDirection(spawnPosition, 4.5f);
			Mob mob = SpawnMob(
				spawnPosition - direction * index * (float)GD.RandRange(1.5f, 3.5f),
				direction,
				GetRandomMobSpeed() * (float)GD.RandRange(1.02f, 1.12f));
			mob.ConfigureTravelCombatRole(
				index == 0
					? Mob.TravelCombatRole.Challenger
					: Mob.TravelCombatRole.LaneRunner,
				index == 0 ? 5.2f : 3.6f,
				activationDelay: index == 0 ? 0.45f : 0.0f,
				weaveSign: index % 2 == 0 ? -1.0f : 1.0f);
		}
	}

	private void SpawnRow(int count)
	{
		Vector3 centerPosition = GetTopSpawnPosition(GetNextSpawnLane());
		Vector3 direction = GetAttackDirection(centerPosition, 1.5f);
		Vector3 lateralDirection = Vector3.Up.Cross(direction).Normalized();
		float speed = GetRandomMobSpeed() * 0.94f;
		float spacing = (float)GD.RandRange(
			Mathf.Min(Settings.MinRowSpacing, Settings.MaxRowSpacing),
			Mathf.Max(Settings.MinRowSpacing, Settings.MaxRowSpacing));

		int slotCount = count + 1;
		int gapIndex = GD.RandRange(1, Mathf.Max(1, slotCount - 2));
		int mobIndex = 0;
		for (int slot = 0; slot < slotCount; slot++)
		{
			if (slot == gapIndex)
			{
				continue;
			}

			float centeredIndex = slot - (slotCount - 1) * 0.5f;
			Vector3 spawnPosition =
				centerPosition + lateralDirection * centeredIndex * spacing;
			Mob mob = SpawnMob(spawnPosition, direction, speed);
			mob.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.LaneRunner,
				3.3f,
				weaveSign: slot % 2 == 0 ? -1.0f : 1.0f);
			mobIndex++;
			if (mobIndex >= count)
			{
				break;
			}
		}
	}

	private void SpawnEchelon(int count)
	{
		Vector3 leaderPosition = GetTopSpawnPosition(GetNextSpawnLane());
		Vector3 direction = GetAttackDirection(leaderPosition, 2.8f);
		Vector3 lateralDirection = Vector3.Up.Cross(direction).Normalized();
		float speed = GetRandomMobSpeed();
		float lateralStep = (float)GD.RandRange(2.2f, 3.6f);
		float depthStep = (float)GD.RandRange(2.0f, 3.8f);
		float mirror = GD.Randf() < 0.5f ? -1.0f : 1.0f;

		for (int index = 0; index < count; index++)
		{
			Vector3 spawnPosition =
				leaderPosition -
				direction * depthStep * index +
				lateralDirection * lateralStep * index * mirror;
			Mob mob = SpawnMob(spawnPosition, direction, speed);
			mob.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.LaneRunner,
				3.35f +
					index * depthStep /
						Mathf.Max(1.0f, speed),
				weaveSign: mirror);
		}
	}

	private void SpawnCrossing(int count)
	{
		for (int index = 0; index < count; index++)
		{
			bool fromRight = index % 2 == 0;
			float heightRatio = 0.18f + (index / 2) * 0.13f;
			Vector3 spawnPosition = GetSideSpawnPosition(fromRight, heightRatio);
			Vector3 targetPosition = _player.GlobalPosition + new Vector3(
				fromRight ? -9.0f : 9.0f,
				0.0f,
				-5.0f - index * 1.5f);
			Vector3 direction = (targetPosition - spawnPosition).Normalized();
			Mob mob = SpawnMob(
				spawnPosition,
				direction,
				GetRandomMobSpeed() * (float)GD.RandRange(0.92f, 1.05f));
			mob.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.Sweeper,
				3.0f + index * 0.16f,
				weaveSign: fromRight ? -1.0f : 1.0f);
		}
	}

	private void SpawnScatter(int count)
	{
		int startLane = GetNextSpawnLane();
		for (int index = 0; index < count; index++)
		{
			int lane = (startLane + index * 2) % SpawnLaneCount;
			Vector3 spawnPosition = GetTopSpawnPosition(lane);
			Vector3 direction = GetAttackDirection(
				spawnPosition,
				(float)GD.RandRange(2.5f, 6.0f));
			float stagger = (float)GD.RandRange(0.0f, 5.5f);
			Mob mob = SpawnMob(
				spawnPosition - direction * stagger,
				direction,
				GetRandomMobSpeed());
			mob.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.LaneRunner,
				3.4f +
					stagger /
						Mathf.Max(1.0f, mob.Velocity.Length()),
				weaveSign: index % 2 == 0 ? -1.0f : 1.0f);
		}
	}

	private void SpawnSplitColumns(int count)
	{
		int leftCount = Mathf.Max(1, count / 2);
		int rightCount = Mathf.Max(1, count - leftCount);
		SpawnColumn(leftCount, GD.RandRange(0, 1));
		SpawnColumn(rightCount, GD.RandRange(3, 4));
	}

	private void SpawnChevron(int count)
	{
		Vector3 leaderPosition = GetTopSpawnPosition(SpawnLaneCount / 2);
		Vector3 direction = GetAttackDirection(leaderPosition, 1.2f);
		Vector3 lateralDirection = Vector3.Up.Cross(direction).Normalized();
		float speed = GetRandomMobSpeed() * 0.96f;
		float lateralStep = (float)GD.RandRange(3.0f, 4.3f);
		float depthStep = (float)GD.RandRange(2.3f, 3.8f);
		bool mirror = GD.Randf() < 0.5f;

		for (int index = 0; index < count; index++)
		{
			int rank = (index + 1) / 2;
			float side = index == 0
				? 0.0f
				: (index % 2 == 0 ? -1.0f : 1.0f);
			if (mirror)
			{
				side *= -1.0f;
			}

			Vector3 spawnPosition =
				leaderPosition -
				direction * rank * depthStep +
				lateralDirection * side * rank * lateralStep;
			Mob mob = SpawnMob(spawnPosition, direction, speed);
			mob.ConfigureTravelCombatRole(
				Mob.TravelCombatRole.LaneRunner,
				3.35f +
					rank * depthStep /
						Mathf.Max(1.0f, speed),
				weaveSign: side < 0.0f ? -1.0f : 1.0f);
		}
	}

	private void SpawnBaitAndPounce(int count)
	{
		int runnerCount = Mathf.Max(3, count - 1);
		int routeId = _nextColumnId++;
		int lane = GetNextSpawnLane();
		Vector3 lanePosition = GetTopSpawnPosition(lane);
		Vector3 leaderPosition = lanePosition;
		leaderPosition.X = Mathf.MoveToward(
			_player.GlobalPosition.X,
			lanePosition.X,
			6.0f);
		Vector3 routeDirection =
			GetLongitudinalDirectionToPlayer(leaderPosition);
		Vector3 sharedRouteDirection =
			GetReadableLongitudinalAttackDirection(
				leaderPosition,
				routeDirection,
				0.34f);
		float routeSpeed = Mathf.Clamp(
			GetRandomMobSpeed() * 0.78f,
			7.8f,
			10.2f);
		float directedSpacing =
			GetDirectedComboSpacing(routeSpeed);
		float depth = 0.0f;
		float weavePhase = (float)GD.RandRange(0.0f, Mathf.Tau);
		float weaveSign = GD.Randf() < 0.5f ? -1.0f : 1.0f;
		float phraseAmplitude =
			(float)GD.RandRange(2.4f, 3.6f);
		int phraseIndex = GD.RandRange(0, 3);
		int rhythmIndex = GD.RandRange(0, 3);
		float previousRouteX = leaderPosition.X;
		float previousDepth = depth;

		for (int index = 0; index < runnerCount; index++)
		{
			float progress = runnerCount <= 1
				? 1.0f
				: index / (float)(runnerCount - 1);
			float routeProgress =
				progress * progress * (3.0f - 2.0f * progress);
			float sideOffset = GetComboPhraseOffset(
				phraseIndex,
				index,
				runnerCount,
				phraseAmplitude,
				weavePhase,
				weaveSign);
			float desiredRouteX =
				Mathf.Lerp(
					leaderPosition.X,
					lanePosition.X,
					routeProgress) +
				sideOffset;
			if (index > 0)
			{
				float longitudinalGap =
					Mathf.Max(0.1f, depth - previousDepth);
				float maximumLateralStep =
					GetMaximumComboLateralStep(
						longitudinalGap,
						routeSpeed);
				desiredRouteX = Mathf.Clamp(
					desiredRouteX,
					previousRouteX - maximumLateralStep,
					previousRouteX + maximumLateralStep);
			}

			Vector3 position =
				leaderPosition -
				routeDirection * depth;
			position.X = desiredRouteX;
			Mob runner = SpawnMob(
				position,
				sharedRouteDirection,
				routeSpeed *
					(float)GD.RandRange(0.98f, 1.03f),
				routeId,
				index);
			runner.ConfigureComboRoute(
				5.0f +
					depth / Mathf.Max(1.0f, routeSpeed),
				Mathf.Sign(
					Mathf.IsZeroApprox(sideOffset)
						? weaveSign
						: sideOffset));
			previousRouteX = desiredRouteX;
			previousDepth = depth;
			depth +=
				directedSpacing *
				GetComboGrowthSpacingScale(index + 1) *
				GetComboRhythmSpacingScale(
					rhythmIndex,
					index + 1) *
				(float)GD.RandRange(0.95f, 1.035f);
		}

		bool fromRight = GD.Randf() < 0.5f;
		Vector3 challengerPosition =
			GetSideSpawnPosition(fromRight, 0.2f);
		Vector3 predictedPlayer =
			_player.GlobalPosition +
			new Vector3(
				_player.Velocity.X,
				0.0f,
				_player.Velocity.Z) * 0.42f;
		predictedPlayer.Y = challengerPosition.Y;
		Mob challenger = SpawnMob(
			challengerPosition,
			(predictedPlayer - challengerPosition).Normalized(),
			GetRandomMobSpeed() * 1.02f);
		challenger.ConfigureTravelCombatRole(
			Mob.TravelCombatRole.Challenger,
			5.4f,
			activationDelay: 0.72f,
			weaveSign: fromRight ? -1.0f : 1.0f);
	}

		private Mob SpawnMob(
			Vector3 spawnPosition,
			Vector3 direction,
			float speed,
			int columnId = -1,
			int columnIndex = -1,
			EnemyArchetypeId archetype =
				EnemyArchetypeId.Spear)
		{
			spawnPosition = ClampSpawnToPlayableWidth(spawnPosition);
            if (ActiveArenaSection >= 0 && _world.TryGetArenaLayout(ActiveArenaSection, out var footprint, out var center))
                spawnPosition = footprint.ClampWorld(spawnPosition, center, 2);
			Mob mob = _mobScene.Instantiate<Mob>();
		float speedVariation = GetMobSpeedVariationMultiplier();
			mob.ConfigureSpeedVariation(speedVariation);
			mob.Initialize(spawnPosition, direction, speed * speedVariation);
			mob.ConfigureArchetype(archetype);
			mob.ConfigureColumn(columnId, columnIndex);
		mob.ConfigureEncounter(ActiveEncounterId);
			_spawnParent.AddChild(mob);
			mob.Squashed += OnMobSquashed;
			return mob;
		}

		private Vector3 ClampSpawnToPlayableWidth(Vector3 spawnPosition)
		{
			if (_world == null)
			{
				return spawnPosition;
			}

			float halfWidth = Mathf.Max(
				2.0f,
				_world.GetPlayableHalfWidth(_player.GlobalPosition.Z) - 2.0f);
			spawnPosition.X = Mathf.Clamp(
				spawnPosition.X,
				-halfWidth,
				halfWidth);
			return spawnPosition;
		}

		private Vector3 GetTopSpawnPosition(int lane)
		{
			Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
			float screenMargin = viewportSize.Y * Settings.SpawnMarginRatio;
			float laneRatio = SpawnLaneCount <= 1
				? 0.5f
				: lane / (float)(SpawnLaneCount - 1);
			float playerScreenX = IsInstanceValid(_player)
				? _camera.UnprojectPosition(_player.GlobalPosition).X
				: viewportSize.X * 0.5f;
			float screenX = Mathf.Clamp(
				playerScreenX +
					Mathf.Lerp(-0.32f, 0.32f, laneRatio) * viewportSize.X,
				viewportSize.X * 0.08f,
				viewportSize.X * 0.92f);
			Vector3 spawnPosition =
				ProjectScreenPositionToGround(
					new Vector2(screenX, -screenMargin));
			if (_world != null && IsInstanceValid(_player))
			{
				float halfWidth = Mathf.Max(
					2.0f,
					_world.GetPlayableHalfWidth(
						_player.GlobalPosition.Z) -
						2.0f);
				spawnPosition.X = Mathf.Lerp(
					-halfWidth,
					halfWidth,
					laneRatio);
			}

			return spawnPosition;
		}

	private Vector3 GetSideSpawnPosition()
	{
		bool fromRight = GD.Randf() < 0.5f;
		float heightRatio = (float)GD.RandRange(0.16f, 0.62f);
		return GetSideSpawnPosition(fromRight, heightRatio);
	}

	private Vector3 GetSideSpawnPosition(bool fromRight, float heightRatio)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float screenMargin = viewportSize.Y * Settings.SpawnMarginRatio;
		float screenY = viewportSize.Y * Mathf.Clamp(heightRatio, 0.08f, 0.9f);
		float screenX = fromRight
			? viewportSize.X + screenMargin
			: -screenMargin;
		return ProjectScreenPositionToGround(new Vector2(screenX, screenY));
	}

	private Vector3 GetArenaEdgeSpawnPosition(int edge, float alongRatio)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float screenMargin = viewportSize.Y * Settings.SpawnMarginRatio;
		float horizontalPosition = Mathf.Lerp(
			viewportSize.X * 0.08f,
			viewportSize.X * 0.92f,
			alongRatio);
		float verticalPosition = Mathf.Lerp(
			viewportSize.Y * 0.08f,
			viewportSize.Y * 0.92f,
			alongRatio);
		Vector2 screenPosition = edge switch
		{
			0 => new Vector2(horizontalPosition, -screenMargin),
			1 => new Vector2(
				viewportSize.X + screenMargin,
				verticalPosition),
			2 => new Vector2(
				horizontalPosition,
				viewportSize.Y + screenMargin),
			_ => new Vector2(-screenMargin, verticalPosition)
		};
		return ProjectScreenPositionToGround(screenPosition);
	}

	private Vector3 ProjectScreenPositionToGround(Vector2 screenPosition)
	{
		Vector3 rayOrigin = _camera.ProjectRayOrigin(screenPosition);
		Vector3 rayDirection = _camera.ProjectRayNormal(screenPosition);

		if (Mathf.IsZeroApprox(rayDirection.Y))
		{
			GD.PushWarning("Could not project the mob spawn point onto the ground.");
			return Vector3.Zero;
		}

		float distanceToGround = -rayOrigin.Y / rayDirection.Y;
		Vector3 spawnPosition = rayOrigin + rayDirection * distanceToGround;
		spawnPosition.Y = 0.0f;
		return spawnPosition;
	}

	private Vector3 GetAttackDirection(Vector3 spawnPosition, float aimVariation)
	{
		Vector3 targetPosition = _player.GlobalPosition +
			new Vector3(_player.Velocity.X, 0.0f, _player.Velocity.Z) * 0.18f;
		targetPosition.X += (float)GD.RandRange(-aimVariation, aimVariation);
		targetPosition.Y = spawnPosition.Y;
		Vector3 direction = targetPosition - spawnPosition;
		return direction.IsZeroApprox() ? Vector3.Back : direction.Normalized();
	}

	private Vector3 GetLongitudinalDirectionToPlayer(
		Vector3 spawnPosition)
	{
		float zDirection = IsInstanceValid(_player)
			? Mathf.Sign(
				_player.GlobalPosition.Z -
				spawnPosition.Z)
			: 1.0f;
		if (Mathf.IsZeroApprox(zDirection))
		{
			zDirection = 1.0f;
		}

		return new Vector3(0.0f, 0.0f, zDirection);
	}

	private Vector3 GetReadableLongitudinalAttackDirection(
		Vector3 spawnPosition,
		Vector3 longitudinalDirection,
		float lateralInfluence)
	{
		if (!IsInstanceValid(_player))
		{
			return longitudinalDirection;
		}

		float longitudinalDistance = Mathf.Max(
			8.0f,
			Mathf.Abs(
				_player.GlobalPosition.Z -
				spawnPosition.Z));
		float lateralSlope = Mathf.Clamp(
			(_player.GlobalPosition.X - spawnPosition.X) /
				longitudinalDistance,
			-0.34f,
			0.34f);
		Vector3 direction =
			longitudinalDirection +
			Vector3.Right *
				lateralSlope *
				Mathf.Clamp(lateralInfluence, 0.0f, 1.0f);
		return direction.IsZeroApprox()
			? longitudinalDirection
			: direction.Normalized();
	}

    private int GetNextSpawnLane()
    {
        return _planner.GetNextSpawnLane();
    }

	private float GetRandomMobSpeed()
	{
		float minimum = Mathf.Min(Settings.MinMobSpeed, Settings.MaxMobSpeed);
		float maximum = Mathf.Max(Settings.MinMobSpeed, Settings.MaxMobSpeed);
		return (float)GD.RandRange(
			minimum,
			maximum) *
			GetOverrunSpeedScale();
	}

	private float GetOverrunSpeedScale()
	{
		return 1.0f +
			Mathf.Min(
				0.3f,
				_overrunTier * 0.035f);
	}

	private float GetMobSpeedVariationMultiplier()
	{
		float variation = Mathf.Clamp(Settings.MobSpeedVariation, 0.0f, 0.2f);
		return (float)GD.RandRange(1.0f - variation, 1.0f + variation);
	}

    private void OnMobSquashed(
        int rewardMultiplier)
    {
        MobSquashed?.Invoke(
            rewardMultiplier);
    }
}
