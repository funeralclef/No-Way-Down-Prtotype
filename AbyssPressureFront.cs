using Godot;
using System.Collections.Generic;

/// <summary>
/// A readable rear boundary for the vertical run. It pressures hesitation,
/// but successful play buys real space. The front never changes enemy speed
/// or steering, so combat remains authored by the enemy director.
/// </summary>
public partial class AbyssPressureFront : Node3D
{
    [Signal]
    public delegate void TierChangedEventHandler(
        int tier,
        string title);

    [Signal]
    public delegate void BreachedEventHandler();

    [ExportGroup("Spacing")]
    [Export(PropertyHint.Range, "20.0,52.0,0.5")]
    public float InitialDistance { get; set; } = 30.0f;

    [Export(PropertyHint.Range, "18.0,44.0,0.5")]
    public float ComfortDistance { get; set; } = 22.0f;

    [Export(PropertyHint.Range, "28.0,70.0,0.5")]
    public float MaximumBuffer { get; set; } = 32.0f;

    [Export(PropertyHint.Range, "12.0,30.0,0.5")]
    public float RiskDistance { get; set; } = 25.0f;

    [Export(PropertyHint.Range, "3.0,12.0,0.25")]
    public float CriticalDistance { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "1.0,5.0,0.25")]
    public float BreachDistance { get; set; } = 2.75f;

    [ExportGroup("Pursuit")]
    [Export(PropertyHint.Range, "1.0,8.0,0.1")]
    public float BaseAdvanceSpeed { get; set; } = 7.8f;

    [Export(PropertyHint.Range, "0.0,1.5,0.05")]
    public float SpeedPerTier { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float ElasticCatchupSpeed { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.0,6.0,0.1")]
    public float StallPressureSpeed { get; set; } = 4.5f;

    [Export(PropertyHint.Range, "0.5,4.0,0.1")]
    public float StallGraceTime { get; set; } = 0.95f;

    [ExportGroup("Breach")]
    [Export(PropertyHint.Range, "5.0,40.0,1.0")]
    public float BreachDamage { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.0,50.0,1.0")]
    public float BreachNitroLoss { get; set; } = 20.0f;

    [Export(PropertyHint.Range, "8.0,30.0,0.5")]
    public float BreachRetreatDistance { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.5,3.0,0.05")]
    public float BreachRecoveryTime { get; set; } = 1.25f;

    [ExportGroup("Action relief")]
    [Export(PropertyHint.Range, "0.0,2.0,0.05")]
    public float KillRelief { get; set; } = 0.42f;

    [Export(PropertyHint.Range, "0.0,2.0,0.05")]
    public float ComboLinkRelief { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "0.0,4.0,0.1")]
    public float DodgeRelief { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.0,3.0,0.1")]
    public float NitroBurstRelief { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0.0,8.0,0.1")]
    public float TunnelRelief { get; set; } = 4.2f;

    [Export(PropertyHint.Range, "0.0,6.0,0.1")]
    public float CourseLaunchRelief { get; set; } = 2.4f;

    private static readonly string[] TierTitles =
    {
        "WHISPER",
        "PURSUIT",
        "SURGE",
        "RIFT",
        "NO WAY DOWN"
    };

    private Player _player;
    private ProceduralWorld _world;
    private MeshInstance3D _wake;
    private MeshInstance3D _rim;
    private MeshInstance3D _curtain;
    private readonly List<MeshInstance3D>
        _tendrils = new();
    private readonly List<float>
        _tendrilRatios = new();
    private readonly List<OmniLight3D>
        _frontLights = new();
    private GpuParticles3D _ash;
    private ParticleProcessMaterial
        _ashProcessMaterial;
    private ShaderMaterial _wakeMaterial;
    private ShaderMaterial _rimMaterial;
    private ShaderMaterial _tendrilMaterial;
    private ShaderMaterial _curtainMaterial;
    private ColorRect _screenVeil;
    private ShaderMaterial _screenVeilMaterial;
    private float _frontZ;
    private float _stallTimer;
    private float _cycleTimer;
    private float _cycleLength = 18.0f;
    private float _breachRecoveryTimer;
    private float _postArenaBreather;
    private float _mobConsumeTimer;
    private float _visualIntensity;
    private float _distance = 40.0f;
    private float _risk;
    private float _arenaWidth = 36.0f;
    private int _tierIndex;
    private bool _initialized;
    private bool _runActive;
    private bool _arenaPaused;
    private bool _warning;
    private bool _surging;
    private RunRouteStyle _routeStyle =
        RunRouteStyle.Neutral;
    private float _routeAdvanceScale = 1.0f;
    private float _routeStallScale = 1.0f;
    private float _routeCycleScale = 1.0f;
    private float _routeActionReliefScale = 1.0f;
    private float _routeKillReliefScale = 1.0f;
    private float _routeDodgeReliefScale = 1.0f;
    private float _routeTraversalReliefScale = 1.0f;
    private int _overrunTier;
    private float _overrunAdvanceScale = 1.0f;

    public int Tier => _tierIndex + 1;
    public string TierTitle => TierTitles[_tierIndex];
    public float DistanceToPlayer => _distance;
    public float RiskRatio => _risk;
    public float Intensity =>
        _arenaPaused || !_runActive
            ? 0.0f
            : Mathf.Max(
                _risk,
                _surging
                    ? 0.72f
                    : _warning
                        ? 0.36f
                        : 0.0f);
    public bool IsArenaPaused => _arenaPaused;
    public bool IsSurging => _surging;
    public RunRouteStyle CurrentRouteStyle =>
        _routeStyle;
    public int OverrunTier => _overrunTier;
    public bool IsRiskRewardActive =>
        _runActive &&
        !_arenaPaused &&
        _postArenaBreather <= 0.0f &&
            _risk > 0.04f;

    /// <summary>
    /// Replaces the active pressure profile. Every value is assigned from a
    /// fixed lookup so selecting routes repeatedly can never compound their
    /// multipliers.
    /// </summary>
    public void SetRouteStyle(
        RunRouteStyle routeStyle)
    {
        if (_routeStyle == routeStyle)
        {
            return;
        }

        _routeStyle = routeStyle;
        _routeAdvanceScale = 1.0f;
        _routeStallScale = 1.0f;
        _routeCycleScale = 1.0f;
        _routeActionReliefScale = 1.0f;
        _routeKillReliefScale = 1.0f;
        _routeDodgeReliefScale = 1.0f;
        _routeTraversalReliefScale = 1.0f;

        switch (routeStyle)
        {
            case RunRouteStyle.Hunt:
                _routeAdvanceScale = 1.06f;
                _routeStallScale = 1.06f;
                _routeCycleScale = 0.94f;
                _routeActionReliefScale = 1.15f;
                break;
            case RunRouteStyle.Flow:
                _routeAdvanceScale = 0.92f;
                _routeStallScale = 1.18f;
                _routeCycleScale = 1.08f;
                _routeTraversalReliefScale = 1.2f;
                break;
            case RunRouteStyle.Endure:
                _routeAdvanceScale = 0.98f;
                _routeStallScale = 0.92f;
                _routeCycleScale = 0.92f;
                _routeKillReliefScale = 0.85f;
                _routeDodgeReliefScale = 1.35f;
                break;
        }

        ResetPressureCycle();
    }

    /// <summary>
    /// Endless continuation tightens the rear pressure gradually. The scale
    /// is bounded so deep runs become demanding without turning one chapter
    /// into an unavoidable speed check.
    /// </summary>
    public void SetOverrunTier(int tier)
    {
        _overrunTier = Mathf.Max(0, tier);
        _overrunAdvanceScale =
            1.0f +
            Mathf.Min(
                0.32f,
                _overrunTier * 0.04f);
        ResetPressureCycle();
    }

    public void Initialize(
        Player player,
        ProceduralWorld world,
        Control interfaceRoot)
    {
        _player = player;
        _world = world;
        CreateWorldVisuals();
        CreateScreenVeil(interfaceRoot);
        ConnectPlayerSignals();

        if (IsInstanceValid(_player))
        {
            _frontZ =
                _player.GlobalPosition.Z +
                Mathf.Max(
                    InitialDistance,
                    ComfortDistance);
            _distance =
                _frontZ -
                _player.GlobalPosition.Z;
            _initialized = true;
        }

        Visible = false;
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        DisconnectPlayerSignals();
        if (IsInstanceValid(_screenVeil))
        {
            _screenVeil.QueueFree();
        }
    }

    public void Advance(
        float deltaSeconds,
        bool runActive,
        int sectionIndex)
    {
        if (!IsInstanceValid(_player) ||
            !IsInstanceValid(_world))
        {
            HideFeedback();
            return;
        }

        float safeDelta =
            Mathf.Clamp(
                deltaSeconds,
                0.0f,
                0.1f);
        _runActive = runActive;
        UpdateTier(sectionIndex);
        _breachRecoveryTimer =
            Mathf.Max(
                0.0f,
                _breachRecoveryTimer -
                    safeDelta);
        _postArenaBreather =
            Mathf.Max(
                0.0f,
                _postArenaBreather -
                    safeDelta);

        if (!_initialized)
        {
            _frontZ =
                _player.GlobalPosition.Z +
                Mathf.Max(
                    InitialDistance,
                    GetComfortDistance());
            _initialized = true;
        }

        if (!runActive)
        {
            _frontZ =
                _player.GlobalPosition.Z +
                Mathf.Max(
                    InitialDistance,
                    GetComfortDistance());
            _distance =
                _frontZ -
                _player.GlobalPosition.Z;
            HideFeedback();
            return;
        }

        if (_arenaPaused)
        {
            _distance =
                _frontZ -
                _player.GlobalPosition.Z;
            _warning = false;
            _surging = false;
            UpdateFeedback(
                safeDelta,
                arenaSeal: true);
            return;
        }

        if (_postArenaBreather > 0.0f)
        {
            float safeDistance =
                Mathf.Max(
                    InitialDistance,
                    GetComfortDistance() + 5.0f);
            _frontZ =
                Mathf.Max(
                    _frontZ,
                    _player.GlobalPosition.Z +
                        safeDistance);
            _distance =
                _frontZ -
                _player.GlobalPosition.Z;
            _warning = false;
            _surging = false;
            UpdateRisk();
            UpdateFeedback(
                safeDelta,
                arenaSeal: false);
            return;
        }

        UpdatePressureRhythm(safeDelta);
        UpdateStallPressure(safeDelta);

        float comfort =
            GetComfortDistance();
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        float elastic =
            Mathf.Clamp(
                (_distance - comfort) / 13.0f,
                -1.0f,
                1.0f);
        float advanceSpeed =
            (BaseAdvanceSpeed +
            _tierIndex * SpeedPerTier) *
            _routeAdvanceScale *
            _overrunAdvanceScale;
        if (elastic >= 0.0f)
        {
            advanceSpeed +=
                elastic *
                ElasticCatchupSpeed;
        }
        else
        {
            // The closer it is, the more it breathes instead of snapping
            // into the player. Hesitation is punished, but readable.
            advanceSpeed *=
                Mathf.Lerp(
                    0.38f,
                    1.0f,
                    elastic + 1.0f);
        }

        if (_stallTimer > StallGraceTime)
        {
            float stallWeight =
                Mathf.Clamp(
                    (_stallTimer -
                        StallGraceTime) /
                        2.4f,
                    0.0f,
                    1.0f);
            advanceSpeed +=
                StallPressureSpeed *
                _routeStallScale *
                stallWeight;
        }
        if (_surging)
        {
            advanceSpeed +=
                6.0f +
                _tierIndex * 0.55f;
        }

        _frontZ -=
            Mathf.Max(
                0.0f,
                advanceSpeed) *
            safeDelta;
        _frontZ =
            Mathf.Min(
                _frontZ,
                _player.GlobalPosition.Z +
                    MaximumBuffer);
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        UpdateRisk();

        if (_distance <= BreachDistance &&
            _breachRecoveryTimer <= 0.0f)
        {
            ResolveBreach();
        }
        ConsumeOvertakenMobs(
            safeDelta);

        UpdateFeedback(
            safeDelta,
            arenaSeal: false);
    }

    public void EnterArena(
        Vector3 arenaCenter,
        float arenaRadius)
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        _arenaPaused = true;
        _warning = false;
        _surging = false;
        _stallTimer = 0.0f;
        _arenaWidth =
            Mathf.Max(
                36.0f,
                arenaRadius * 2.0f);
        // The front becomes the sealed southern threshold. It remains
        // visible in the world, but cannot damage or reward arena play.
        _frontZ =
            Mathf.Max(
                arenaCenter.Z +
                    arenaRadius +
                    1.4f,
                _player.GlobalPosition.Z +
                    4.0f);
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        UpdateRisk();
    }

    public void ExitArena(float breatherTime)
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        _arenaPaused = false;
        _arenaWidth = 36.0f;
        _postArenaBreather =
            Mathf.Max(
                1.0f,
                breatherTime);
        _frontZ =
            _player.GlobalPosition.Z +
            Mathf.Max(
                InitialDistance,
                GetComfortDistance() + 5.0f);
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        UpdateRisk();
        ResetPressureCycle();
    }

    public void Stop()
    {
        _runActive = false;
        HideFeedback();
    }

    public int ApplyScoreRiskBonus(
        int baseReward)
    {
        int safeReward =
            Mathf.Max(
                1,
                baseReward);
        if (!IsRiskRewardActive)
        {
            return safeReward;
        }

        float multiplier =
            Mathf.Lerp(
                1.05f,
                1.55f,
                _risk);
        return Mathf.Max(
            safeReward,
            Mathf.RoundToInt(
                safeReward *
                multiplier));
    }

    public void RewardEnemyDefeat(int comboChain)
    {
        if (!CanReceiveActionRelief())
        {
            return;
        }

        float chainRelief =
            Mathf.Clamp(
                Mathf.Max(
                    0,
                    comboChain - 1) *
                    0.035f,
                0.0f,
                0.36f);
        Retreat(
            KillRelief +
            chainRelief,
            _routeKillReliefScale);
        if (IsRiskRewardActive &&
            IsInstanceValid(_player))
        {
            _player.AddNitro(
                Mathf.Lerp(
                    0.6f,
                    2.8f,
                    _risk));
        }
    }

    private void OnStompChainAdvanced(
        int chainCount,
        int scoreMultiplier)
    {
        if (!CanReceiveActionRelief())
        {
            return;
        }

        Retreat(
            Mathf.Clamp(
                ComboLinkRelief *
                    Mathf.Sqrt(
                        Mathf.Max(
                            1,
                            chainCount)),
                0.18f,
                1.05f),
            _routeKillReliefScale);
    }

    private void OnDaringDodge(
        float contractTimeReduced,
        float speedBoost,
        bool perfectDodge)
    {
        if (!CanReceiveActionRelief())
        {
            return;
        }

        Retreat(
            DodgeRelief *
            (perfectDodge
                ? 1.65f
                : 1.0f),
            _routeDodgeReliefScale);
        if (IsRiskRewardActive &&
            IsInstanceValid(_player))
        {
            _player.AddNitro(
                perfectDodge
                    ? 2.2f
                    : 1.1f);
        }
    }

    private void OnRiftStepStarted(
        bool teleported,
        Vector3 origin,
        Vector3 destination)
    {
        if (teleported &&
            CanReceiveActionRelief())
        {
            Retreat(
                NitroBurstRelief *
                Mathf.Lerp(
                    1.0f,
                    1.25f,
                    Mathf.Clamp(
                        origin.DistanceTo(
                            destination) /
                            18.0f,
                        0.0f,
                        1.0f)),
                _routeTraversalReliefScale);
        }
    }

    private void OnTunnelEntered()
    {
        if (CanReceiveActionRelief())
        {
            Retreat(
                TunnelRelief,
                _routeTraversalReliefScale);
        }
    }

    private void OnCourseLaunched(float launchSpeed)
    {
        if (!CanReceiveActionRelief())
        {
            return;
        }

        float speedWeight =
            Mathf.Clamp(
                (launchSpeed - 14.0f) /
                    18.0f,
                0.35f,
                1.0f);
        Retreat(
            CourseLaunchRelief *
            speedWeight,
            _routeTraversalReliefScale);
    }

    private void OnTraversalRailEntered(float minimumSpeed)
    {
        if (!CanReceiveActionRelief())
        {
            return;
        }

        Retreat(
            CourseLaunchRelief *
            0.55f *
            Mathf.Clamp(
                minimumSpeed / 24.0f,
                0.6f,
                1.25f),
            _routeTraversalReliefScale);
    }

    private bool CanReceiveActionRelief()
    {
        return
            _runActive &&
            !_arenaPaused &&
            _postArenaBreather <= 0.0f &&
            IsInstanceValid(_player);
    }

    private void Retreat(
        float amount,
        float routeSpecificScale = 1.0f)
    {
        if (amount <= 0.0f ||
            !IsInstanceValid(_player))
        {
            return;
        }

        float tierActionScale =
            1.0f +
            _tierIndex * 0.035f;
        _frontZ =
            Mathf.Min(
                _frontZ +
                    amount *
                    _routeActionReliefScale *
                    Mathf.Max(
                        0.0f,
                        routeSpecificScale) *
                    tierActionScale,
                _player.GlobalPosition.Z +
                    MaximumBuffer);
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        UpdateRisk();
    }

    private void ResolveBreach()
    {
        if (!IsInstanceValid(_player) ||
            !_player.ReceiveAbyssPressure(
                BreachDamage,
                BreachNitroLoss))
        {
            // Invulnerability cannot be used to sit inside the boundary.
            // Give it a small physical breath and try again later.
            _frontZ =
                _player.GlobalPosition.Z +
                Mathf.Max(
                    BreachDistance + 3.0f,
                    CriticalDistance);
            _breachRecoveryTimer = 0.35f;
            return;
        }

        _frontZ =
            _player.GlobalPosition.Z +
            Mathf.Max(
                BreachRetreatDistance,
                GetComfortDistance() * 0.78f);
        _distance =
            _frontZ -
            _player.GlobalPosition.Z;
        _breachRecoveryTimer =
            Mathf.Max(
                0.5f,
                BreachRecoveryTime);
        _stallTimer = 0.0f;
        ResetPressureCycle();
        UpdateRisk();
        EmitSignal(SignalName.Breached);
    }

    private void UpdateTier(int sectionIndex)
    {
        int nextTier =
            Mathf.Clamp(
                Mathf.Max(
                    0,
                    sectionIndex) /
                    3,
                0,
                TierTitles.Length - 1);
        if (nextTier == _tierIndex)
        {
            return;
        }

        _tierIndex = nextTier;
        ResetPressureCycle();
        if (_runActive)
        {
            EmitSignal(
                SignalName.TierChanged,
                Tier,
                TierTitle);
        }
    }

    private float GetComfortDistance()
    {
        return Mathf.Max(
            CriticalDistance + 5.0f,
            ComfortDistance -
                _tierIndex * 1.8f);
    }

    private void UpdateStallPressure(float deltaSeconds)
    {
        float forwardSpeed =
            Mathf.Max(
                0.0f,
                -_player.Velocity.Z);
        bool activelyAdvancing =
            forwardSpeed >= 2.4f ||
            _player.IsNitroBurning ||
            _player.AirStompChain > 0;
        _stallTimer =
            activelyAdvancing
                ? Mathf.MoveToward(
                    _stallTimer,
                    0.0f,
                    deltaSeconds * 2.5f)
                : _stallTimer +
                    deltaSeconds;
    }

    private void UpdatePressureRhythm(
        float deltaSeconds)
    {
        _cycleTimer += deltaSeconds;
        if (_cycleTimer >= _cycleLength)
        {
            ResetPressureCycle();
        }

        float surgeDuration =
            2.6f +
            _tierIndex * 0.18f;
        float warningDuration =
            1.8f;
        float surgeStart =
            _cycleLength -
            surgeDuration;
        _surging =
            _cycleTimer >= surgeStart;
        _warning =
            !_surging &&
            _cycleTimer >=
                surgeStart -
                warningDuration;
    }

    private void ResetPressureCycle()
    {
        _cycleTimer = 0.0f;
        _cycleLength =
            Mathf.Max(
                9.5f,
                (14.5f -
                    _tierIndex * 0.75f +
                    (float)GD.RandRange(
                        -1.2,
                        1.6)) *
                _routeCycleScale);
        _warning = false;
        _surging = false;
    }

    private void UpdateRisk()
    {
        float range =
            Mathf.Max(
                0.1f,
                RiskDistance -
                    CriticalDistance);
        _risk =
            Mathf.Clamp(
                (RiskDistance -
                    _distance) /
                    range,
                0.0f,
                1.0f);
    }

    private void UpdateFeedback(
        float deltaSeconds,
        bool arenaSeal)
    {
        Visible = true;
        float targetIntensity =
            arenaSeal
                ? 0.34f
                : Mathf.Clamp(
                    0.18f +
                        _risk * 0.82f +
                        (_warning
                            ? 0.2f
                            : 0.0f) +
                        (_surging
                            ? 0.34f
                            : 0.0f),
                    0.0f,
                    1.0f);
        float response =
            targetIntensity >
                _visualIntensity
                ? 5.2f
                : 2.2f;
        _visualIntensity =
            Mathf.Lerp(
                _visualIntensity,
                targetIntensity,
                1.0f -
                    Mathf.Exp(
                        -response *
                        deltaSeconds));

        GlobalPosition =
            new Vector3(
                0.0f,
                0.0f,
                _frontZ);
        float width =
            arenaSeal
                ? _arenaWidth
                : Mathf.Clamp(
                    _world.GetPlayableHalfWidth(
                        _frontZ) *
                        2.0f +
                        1.5f,
                    36.0f,
                    104.0f);
        float widthScale =
            width / 36.0f;
        _wake.Scale =
            new Vector3(
                widthScale,
                1.0f,
                1.0f);
        _rim.Scale =
            new Vector3(
                widthScale,
                1.0f,
                1.0f);
        _curtain.Scale =
            new Vector3(
                widthScale,
                1.0f,
                1.0f);
        for (int index = 0;
            index < _tendrils.Count;
            index++)
        {
            MeshInstance3D tendril =
                _tendrils[index];
            Vector3 position =
                tendril.Position;
            position.X =
                _tendrilRatios[index] *
                width;
            tendril.Position =
                position;
        }
        for (int index = 0;
            index < _frontLights.Count;
            index++)
        {
            Vector3 position =
                _frontLights[index]
                    .Position;
            position.X =
                (index - 1) *
                width *
                0.29f;
            _frontLights[index]
                .Position =
                position;
        }

        Color color =
            GetPressureColor();
        int effectsQuality =
            IsInstanceValid(_player)
                ? _player.EffectsQuality
                : 3;
        float effectsScale =
            effectsQuality switch
            {
                <= 0 => 0.0f,
                1 => 0.38f,
                2 => 0.72f,
                _ => 1.0f
            };
        _curtain.Visible =
            effectsQuality >= 2 ||
            arenaSeal;
        float pulse =
            0.88f +
            Mathf.Sin(
                Time.GetTicksMsec() *
                    0.001f *
                    (_surging
                        ? 9.0f
                        : 3.6f)) *
                (_surging
                    ? 0.12f
                    : 0.05f);
        _wakeMaterial.SetShaderParameter(
            "tint",
            color);
        _wakeMaterial.SetShaderParameter(
            "intensity",
            _visualIntensity *
                pulse);
        _wakeMaterial.SetShaderParameter(
            "surge",
            _surging
                ? 1.0f
                : _warning
                    ? 0.45f
                    : 0.0f);
        _rimMaterial.SetShaderParameter(
            "tint",
            color);
        _rimMaterial.SetShaderParameter(
            "intensity",
            _visualIntensity *
                pulse);
        _rimMaterial.SetShaderParameter(
            "surge",
            _surging
                ? 1.0f
                : _warning
                    ? 0.45f
                    : 0.0f);
        _tendrilMaterial.SetShaderParameter(
            "tint",
            color);
        _tendrilMaterial.SetShaderParameter(
            "intensity",
            _visualIntensity);
        _tendrilMaterial.SetShaderParameter(
            "surge",
            _surging
                ? 1.0f
                : 0.0f);
        _curtainMaterial.SetShaderParameter(
            "tint",
            color);
        _curtainMaterial.SetShaderParameter(
            "intensity",
            _visualIntensity *
                (arenaSeal
                    ? 0.68f
                    : 1.0f));
        _curtainMaterial.SetShaderParameter(
            "surge",
            _surging
                ? 1.0f
                : _warning
                    ? 0.4f
                    : 0.0f);

        if (IsInstanceValid(_ash) &&
            _ashProcessMaterial != null)
        {
            _ash.Emitting =
                effectsScale > 0.01f;
            _ash.AmountRatio =
                Mathf.Clamp(
                    (0.22f +
                        _visualIntensity *
                        0.78f) *
                        effectsScale,
                    0.0f,
                    1.0f);
            _ashProcessMaterial
                .EmissionBoxExtents =
                new Vector3(
                    width * 0.48f,
                    0.1f,
                    0.55f);
            _ashProcessMaterial.Color =
                new Color(
                    color.R,
                    color.G,
                    color.B,
                    0.72f);
        }
        for (int index = 0;
            index < _frontLights.Count;
            index++)
        {
            OmniLight3D light =
                _frontLights[index];
            light.Visible =
                effectsQuality >= 3 ||
                (effectsQuality == 2 &&
                    index == 1);
            light.LightColor =
                color;
            light.LightEnergy =
                (0.24f +
                    _visualIntensity *
                    (_surging
                        ? 1.75f
                        : 1.05f)) *
                pulse;
            light.OmniRange =
                7.0f +
                _visualIntensity *
                    4.0f;
        }

        if (IsInstanceValid(_screenVeilMaterial))
        {
            _screenVeil.Show();
            _screenVeilMaterial.SetShaderParameter(
                "tint",
                color);
            _screenVeilMaterial.SetShaderParameter(
                "intensity",
                arenaSeal
                    ? 0.0f
                    : Mathf.Clamp(
                        (_risk - 0.12f) /
                            0.88f,
                        0.0f,
                        1.0f));
            _screenVeilMaterial.SetShaderParameter(
                "surge",
                _surging
                    ? 1.0f
                    : _warning
                        ? 0.35f
                        : 0.0f);
        }
    }

    private void HideFeedback()
    {
        Visible = false;
        _visualIntensity = 0.0f;
        if (IsInstanceValid(_ash))
        {
            _ash.Emitting = false;
        }
        if (IsInstanceValid(_screenVeil))
        {
            _screenVeil.Hide();
        }
    }

    private Color GetPressureColor()
    {
        Color tierColor =
            new Color(
                0.64f,
                0.1f,
                1.0f)
            .Lerp(
                new Color(
                    1.0f,
                    0.055f,
                    0.12f),
                _tierIndex /
                    (float)(TierTitles.Length - 1));
        Color backdrop =
            _world?.CurrentSurfaceColor ??
            new Color(
                0.12f,
                0.16f,
                0.24f);
        Vector3 delta =
            new Vector3(
                tierColor.R,
                tierColor.G,
                tierColor.B) -
            new Vector3(
                backdrop.R,
                backdrop.G,
                backdrop.B);
        if (delta.Length() < 0.42f)
        {
            tierColor =
                tierColor.Lerp(
                    new Color(
                        1.0f,
                        0.42f,
                        0.06f),
                    0.62f);
        }
        return tierColor;
    }

    private void ConsumeOvertakenMobs(
        float deltaSeconds)
    {
        _mobConsumeTimer -=
            deltaSeconds;
        if (_mobConsumeTimer > 0.0f ||
            _arenaPaused)
        {
            return;
        }

        _mobConsumeTimer = 0.16f;
        foreach (Node node in
            GetTree().GetNodesInGroup(
                "mob"))
        {
            if (node is not Mob mob ||
                !IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion() ||
                mob.EncounterId >= 0 ||
                mob.GlobalPosition.Z <=
                    _frontZ + 1.6f)
            {
                continue;
            }

            // Anything fully swallowed by the front leaves without score.
            // This prevents enemies from visibly walking inside the void.
            mob.QueueFree();
        }
    }

    private void ConnectPlayerSignals()
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        _player.StompChainAdvanced +=
            OnStompChainAdvanced;
        _player.DaringDodge +=
            OnDaringDodge;
        _player.RiftStepStarted +=
            OnRiftStepStarted;
        _player.TunnelEntered +=
            OnTunnelEntered;
        _player.CourseLaunched +=
            OnCourseLaunched;
        _player.TraversalRailEntered +=
            OnTraversalRailEntered;
    }

    private void DisconnectPlayerSignals()
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        _player.StompChainAdvanced -=
            OnStompChainAdvanced;
        _player.DaringDodge -=
            OnDaringDodge;
        _player.RiftStepStarted -=
            OnRiftStepStarted;
        _player.TunnelEntered -=
            OnTunnelEntered;
        _player.CourseLaunched -=
            OnCourseLaunched;
        _player.TraversalRailEntered -=
            OnTraversalRailEntered;
    }

    private void CreateWorldVisuals()
    {
        _wakeMaterial =
            new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.3, 0.6, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 1.5) = 0.2;
uniform float surge : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    float coarse =
        sin(UV.x * 18.0 + TIME * 0.82) * 0.038 +
        sin(UV.x * 43.0 - TIME * 1.27) * 0.018 +
        sin(UV.x * 91.0 + TIME * 1.91) * 0.009;
    float boundary =
        0.075 +
        coarse +
        sin(TIME * 0.7) * 0.012 +
        surge * 0.025;
    float body =
        smoothstep(
            boundary,
            boundary + 0.13,
            UV.y);
    float edge =
        1.0 -
        smoothstep(
            0.0,
            0.055,
            abs(UV.y - boundary));
    float side_fade =
        smoothstep(0.0, 0.035, UV.x) *
        (1.0 - smoothstep(0.965, 1.0, UV.x));
    float veins =
        pow(
            max(
                0.0,
                sin(
                    UV.x * 74.0 -
                    UV.y * 18.0 +
                    TIME * 2.1)),
            7.0) *
        body;
    float flicker =
        0.9 +
        sin(TIME * (3.2 + surge * 5.0) + UV.x * 23.0) *
        (0.05 + surge * 0.07);
    vec3 void_color =
        mix(
            vec3(0.002, 0.004, 0.009),
            tint.rgb * 0.07,
            0.35 + veins * 0.4);
    ALBEDO = void_color;
    EMISSION =
        tint.rgb *
        (edge * (1.25 + intensity * 3.4) +
            veins * 0.24) *
        flicker;
    ALPHA =
        side_fade *
        intensity *
        (body * (0.5 + intensity * 0.32) +
            edge * 0.5);
}"
                }
            };

        _wake = new MeshInstance3D
        {
            Name = "AbyssWake",
            Position =
                new Vector3(
                    0.0f,
                    0.065f,
                    11.0f),
            Mesh = new PlaneMesh
            {
                Size =
                    new Vector2(
                        36.0f,
                        24.0f),
                SubdivideWidth = 32,
                SubdivideDepth = 12
            },
            MaterialOverride =
                _wakeMaterial,
            CastShadow =
                GeometryInstance3D
                    .ShadowCastingSetting.Off
        };
        AddChild(_wake);

        _rimMaterial =
            new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.3, 0.6, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 1.5) = 0.2;
uniform float surge : hint_range(0.0, 1.0) = 0.0;

void vertex() {
    float wave =
        sin(VERTEX.x * 0.72 + TIME * 2.2) * 0.24 +
        sin(VERTEX.x * 1.83 - TIME * 3.1) * 0.11 +
        sin(VERTEX.x * 4.2 + TIME * 4.7) * 0.035;
    VERTEX.z +=
        wave *
        (0.8 + surge * 0.75);
}

void fragment() {
    float band =
        1.0 -
        smoothstep(
            0.08,
            0.5,
            abs(UV.y - 0.5));
    float side_fade =
        smoothstep(0.0, 0.025, UV.x) *
        (1.0 - smoothstep(0.975, 1.0, UV.x));
    float tear =
        0.72 +
        sin(UV.x * 87.0 + TIME * 5.0) * 0.18 +
        sin(UV.x * 31.0 - TIME * 2.7) * 0.1;
    float pulse =
        0.82 +
        sin(TIME * (4.8 + surge * 5.0) + UV.x * 16.0) *
        (0.08 + surge * 0.1);
    ALBEDO = tint.rgb * 0.16;
    EMISSION =
        tint.rgb *
        (1.8 + intensity * 4.4) *
        pulse;
    ALPHA =
        band *
        side_fade *
        tear *
        intensity *
        0.82;
}"
                }
            };
        _rim = new MeshInstance3D
        {
            Name = "AbyssLivingRim",
            Position =
                new Vector3(
                    0.0f,
                    0.105f,
                    0.0f),
            Mesh = new PlaneMesh
            {
                Size =
                    new Vector2(
                        36.0f,
                        1.55f),
                SubdivideWidth = 56,
                SubdivideDepth = 2
            },
            MaterialOverride =
                _rimMaterial,
            CastShadow =
                GeometryInstance3D
                    .ShadowCastingSetting.Off
        };
        AddChild(_rim);

        _curtainMaterial =
            new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.3, 0.6, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 1.5) = 0.2;
uniform float surge : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    float horizontal =
        smoothstep(0.0, 0.045, UV.x) *
        (1.0 - smoothstep(0.955, 1.0, UV.x));
    float vertical =
        sin(UV.y * 3.14159265);
    vertical =
        pow(
            max(0.0, vertical),
            0.72);
    float holes =
        sin(UV.x * 29.0 + TIME * 0.9 + UV.y * 8.0) *
        sin(UV.x * 71.0 - TIME * 1.4 - UV.y * 13.0);
    holes =
        smoothstep(
            -0.6,
            0.65,
            holes);
    float pulse =
        0.9 +
        sin(TIME * (2.6 + surge * 4.0)) *
        (0.04 + surge * 0.07);
    ALBEDO =
        mix(
            vec3(0.001, 0.003, 0.008),
            tint.rgb * 0.05,
            holes * 0.35);
    EMISSION =
        tint.rgb *
        holes *
        vertical *
        intensity *
        (0.12 + surge * 0.25);
    ALPHA =
        horizontal *
        vertical *
        intensity *
        mix(0.025, 0.16, holes) *
        pulse;
}"
                }
            };
        _curtain = new MeshInstance3D
        {
            Name = "AbyssBreath",
            Position =
                new Vector3(
                    0.0f,
                    2.8f,
                    1.15f),
            Mesh = new QuadMesh
            {
                Size =
                    new Vector2(
                        36.0f,
                        5.6f),
                SubdivideWidth = 36,
                SubdivideDepth = 4
            },
            MaterialOverride =
                _curtainMaterial,
            CastShadow =
                GeometryInstance3D
                    .ShadowCastingSetting.Off
        };
        AddChild(_curtain);

        _tendrilMaterial =
            new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.3, 0.6, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 1.5) = 0.2;
uniform float surge : hint_range(0.0, 1.0) = 0.0;

void vertex() {
    float seed =
        MODEL_MATRIX[3].x * 0.37;
    VERTEX.x +=
        sin(
            VERTEX.z * 1.1 +
            TIME * (2.0 + surge * 2.4) +
            seed) *
        (0.08 + surge * 0.12);
}

void fragment() {
    vec2 p =
        UV * 2.0 -
        vec2(1.0);
    float body =
        1.0 -
        smoothstep(
            0.16,
            1.0,
            length(
                vec2(
                    p.x * 1.5,
                    p.y * 0.82)));
    float broken =
        0.72 +
        sin(
            UV.y * 34.0 +
            UV.x * 9.0 -
            TIME * 3.4) *
        0.18;
    float edge =
        body *
        (1.0 -
            smoothstep(
                0.0,
                0.36,
                body));
    ALBEDO =
        vec3(0.002, 0.004, 0.009);
    EMISSION =
        tint.rgb *
        edge *
        intensity *
        (1.2 + surge * 2.0);
    ALPHA =
        body *
        broken *
        intensity *
        (0.16 + surge * 0.11);
}"
                }
            };
        float[] ratios =
        {
            -0.39f,
            -0.23f,
            -0.08f,
            0.11f,
            0.27f,
            0.41f
        };
        for (int index = 0;
            index < ratios.Length;
            index++)
        {
            float length =
                4.8f +
                (index % 3) * 1.1f;
            MeshInstance3D tendril =
                new()
                {
                    Name =
                        $"AbyssTendril{index + 1}",
                    Position =
                        new Vector3(
                            ratios[index] *
                                36.0f,
                            0.082f +
                                index % 2 *
                                    0.006f,
                            -length * 0.44f),
                    Rotation =
                        new Vector3(
                            0.0f,
                            Mathf.DegToRad(
                                -9.0f +
                                index * 3.4f),
                            0.0f),
                    Mesh = new PlaneMesh
                    {
                        Size =
                            new Vector2(
                                1.35f +
                                    index % 2 *
                                        0.45f,
                                length),
                        SubdivideWidth = 4,
                        SubdivideDepth = 18
                    },
                    MaterialOverride =
                        _tendrilMaterial,
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            _tendrils.Add(
                tendril);
            _tendrilRatios.Add(
                ratios[index]);
            AddChild(
                tendril);
        }

        _ashProcessMaterial =
            new ParticleProcessMaterial
            {
                EmissionShape =
                    ParticleProcessMaterial
                        .EmissionShapeEnum
                        .Box,
                EmissionBoxExtents =
                    new Vector3(
                        17.0f,
                        0.1f,
                        0.55f),
                Direction =
                    new Vector3(
                        0.0f,
                        1.0f,
                        -0.18f),
                Spread = 46.0f,
                InitialVelocityMin = 1.1f,
                InitialVelocityMax = 3.8f,
                Gravity =
                    new Vector3(
                        0.0f,
                        0.7f,
                        0.0f),
                ScaleMin = 0.45f,
                ScaleMax = 1.25f,
                Color =
                    new Color(
                        0.42f,
                        0.65f,
                        1.0f,
                        0.7f)
            };
        StandardMaterial3D ashMaterial =
            new()
            {
                Transparency =
                    BaseMaterial3D
                        .TransparencyEnum
                        .Alpha,
                ShadingMode =
                    BaseMaterial3D
                        .ShadingModeEnum
                        .Unshaded,
                BillboardMode =
                    BaseMaterial3D
                        .BillboardModeEnum
                        .Enabled,
                VertexColorUseAsAlbedo =
                    true,
                AlbedoColor = Colors.White,
                EmissionEnabled = true,
                Emission =
                    new Color(
                        0.3f,
                        0.55f,
                        1.0f),
                EmissionEnergyMultiplier =
                    1.7f
            };
        _ash = new GpuParticles3D
        {
            Name = "AbyssAsh",
            Position =
                new Vector3(
                    0.0f,
                    0.18f,
                    0.4f),
            Amount = 180,
            Lifetime = 1.7f,
            Randomness = 0.72f,
            Explosiveness = 0.06f,
            ProcessMaterial =
                _ashProcessMaterial,
            DrawPass1 = new QuadMesh
            {
                Size =
                    new Vector2(
                        0.085f,
                        0.22f),
                Material = ashMaterial
            },
            VisibilityAabb =
                new Aabb(
                    new Vector3(
                        -52.0f,
                        -1.0f,
                        -5.0f),
                    new Vector3(
                        104.0f,
                        9.0f,
                        12.0f)),
            CastShadow =
                GeometryInstance3D
                    .ShadowCastingSetting.Off
        };
        AddChild(_ash);

        for (int index = 0;
            index < 3;
            index++)
        {
            OmniLight3D light =
                new()
                {
                    Name =
                        $"AbyssLight{index + 1}",
                    Position =
                        new Vector3(
                            (index - 1) *
                                10.5f,
                            1.2f,
                            0.15f),
                    LightColor =
                        new Color(
                            0.28f,
                            0.54f,
                            1.0f),
                    LightEnergy = 0.3f,
                    OmniRange = 8.0f,
                    ShadowEnabled = false
                };
            _frontLights.Add(
                light);
            AddChild(
                light);
        }
    }

    private void CreateScreenVeil(
        Control interfaceRoot)
    {
        if (!IsInstanceValid(interfaceRoot))
        {
            return;
        }

        _screenVeilMaterial =
            new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform vec4 tint : source_color = vec4(0.3, 0.6, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float surge : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    float bottom =
        smoothstep(0.56, 1.0, UV.y);
    float corner =
        smoothstep(
            0.34,
            0.54,
            abs(UV.x - 0.5));
    float pulse =
        0.88 +
        sin(TIME * mix(4.0, 10.0, surge)) *
        (0.04 + surge * 0.08);
    float alpha =
        intensity *
        pulse *
        (bottom * bottom * 0.2 +
            corner * bottom * 0.035);
    vec3 veil_color =
        mix(
            vec3(0.001, 0.003, 0.008),
            tint.rgb * 0.22,
            bottom * 0.24 + surge * 0.08);
    COLOR =
        vec4(
            veil_color,
            alpha);
}"
                }
            };
        _screenVeil = new ColorRect
        {
            Name = "AbyssPressureVeil",
            Color = Colors.White,
            Material =
                _screenVeilMaterial,
            MouseFilter =
                Control.MouseFilterEnum.Ignore,
            ZIndex = -50
        };
        _screenVeil.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        interfaceRoot.AddChild(_screenVeil);
        interfaceRoot.MoveChild(
            _screenVeil,
            0);
        _screenVeil.Hide();
    }
}
