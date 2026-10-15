using Godot;
using System;
using System.Reflection;

/// <summary>
/// Headless gameplay regression for Rift Step and analog Nitro thrust.
/// </summary>
public partial class NitroRegressionProbe : Node3D
{
    private Player _player;
    private StaticBody3D _floor;
    private int _riftEvents;
    private bool _lastRiftTeleported;
    private Vector3 _lastRiftOrigin;
    private Vector3 _lastRiftDestination;
    private int _slipstreamEvents;
    private int _lastSlipstreamScore;
    private bool _lastSlipstreamPerfect;
    private int _clutchVaultEvents;
    private Vector3 _lastClutchVaultOrigin;
    private Vector3 _lastClutchVaultTarget;
    private bool _failed;

    public override async void _Ready()
    {
        CreateFloor();
        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        _player =
            playerScene.Instantiate<Player>();
        AddChild(_player);
        _player.RiftStepStarted +=
            OnRiftStepStarted;
        _player.SlipstreamDodge +=
            OnSlipstreamDodge;
        _player.ClutchVaultStarted +=
            OnClutchVaultStarted;
        await PhysicsFrames(8);

        await TestGroundBurst();
        await TestSlipstreamCue();
        await TestForgivingSlipstreamInputWindow();
        await TestSlipstreamDodge();
        await TestLateralSlipstreamDodge();
        await TestClutchVault();
        await TestClutchVaultPhysicalStomp();
        await TestRiftTeleportTargetRules();
        await TestArenaRiftTargetRules();
        await TestAirborneRiftTeleport();
        await TestBufferedAirborneRiftTeleport();
        await TestBlockedRiftPath();
        await TestKeyboardTapHoldSplit();
        await TestAnalogThrust();
        await TestMobileGestureSequence();

        Input.ActionRelease("dash");
        Input.ActionRelease(
            "nitro_throttle");
        if (IsInstanceValid(_player))
        {
            _player.QueueFree();
        }
        if (IsInstanceValid(_floor))
        {
            _floor.QueueFree();
        }
        await PhysicsFrames(3);
        GD.Print(
            _failed
                ? "RIFT_STEP_PROBE: FAILED"
                : "RIFT_STEP_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }

    private void CreateFloor()
    {
        _floor = new StaticBody3D
        {
            Name = "RegressionFloor",
            CollisionLayer = 1u << 2,
            CollisionMask = 0
        };
        CollisionShape3D collision =
            new()
            {
                Shape = new BoxShape3D
                {
                    Size = new Vector3(
                        80.0f,
                        1.0f,
                        80.0f)
                }
            };
        _floor.AddChild(collision);
        _floor.Position =
            new Vector3(
                0.0f,
                -0.5f,
                0.0f);
        AddChild(_floor);
    }

    private async System.Threading.Tasks.Task
        TestGroundBurst()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -10.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);

        float nitroBefore =
            _player.NitroCharge;
        int eventsBefore =
            _riftEvents;
        float verticalBefore =
            _player.Velocity.Y;
        bool activated =
            InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up);
        float speedAfter =
            HorizontalSpeed();
        Check(
            activated &&
                _player.IsGroundBurstActive &&
                !_lastRiftTeleported &&
                _riftEvents ==
                    eventsBefore + 1,
            "ground input starts one non-teleporting burst");
        Check(
            speedAfter >= 24.0f &&
                speedAfter <=
                    _player
                        .GroundBurstSpeedCap +
                        0.01f &&
                Mathf.IsEqualApprox(
                    _player.Velocity.Y,
                    verticalBefore),
            $"ground burst is sharp, short, and planar ({speedAfter:0.00})");
        Check(
            Mathf.Abs(
                nitroBefore -
                    _player.NitroCharge -
                    _player.GroundBurstCost) <=
                0.05f &&
                !_player.IsDamageInvulnerable,
            "ground burst pays its cost and grants no invulnerability");
        Check(
            !InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up),
            "ground burst cooldown blocks immediate spam");

        _player.SetPhysicsProcess(true);
        await PhysicsFrames(16);
        _player.SetPhysicsProcess(false);
        Check(
            _player.GroundBurstCooldown <=
                    0.26f &&
                InvokePrivateBool(
                    "TryPerformGroundBurst",
                    Vector2.Up),
            "ground burst recovers quickly enough for reactive ground play");
        _player.SetPhysicsProcess(true);
    }

    private async System.Threading.Tasks.Task
        TestSlipstreamCue()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -10.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);
        Mob cueTarget =
            await CreateTarget(
                new Vector3(
                    3.0f,
                    -0.05f,
                    -8.0f));
        cueTarget.Velocity =
            Vector3.Zero;

        InvokePrivateVoid(
            "UpdateSlipstreamDodgeCue",
            true);
        Check(
            _player.SlipstreamCueTarget ==
                    cueTarget &&
                _player
                    .CurrentSlipstreamCue ==
                    Player
                        .SlipstreamCueLevel
                        .Ready &&
                _player
                    .SlipstreamCueUrgency >
                    0.45f,
            "predictive cue marks the best Dodge window before the close pass");
        Check(
            _player
                    .SlipstreamCuePredictedDistance >
                    _player
                        .PerfectSlipstreamRadius &&
                _player
                    .SlipstreamCuePredictedDistance <=
                    _player
                        .SlipstreamDodgeRadius,
            "cue distinguishes a safe Slipstream line from a Perfect Line");

        bool burstStarted =
            InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up);
        Check(
            burstStarted &&
                _player.SlipstreamCueTarget ==
                    null &&
                _player
                    .CurrentSlipstreamCue ==
                    Player
                        .SlipstreamCueLevel
                        .None,
            "pressing Dodge immediately clears the predictive cue");

        cueTarget.QueueFree();
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestForgivingSlipstreamInputWindow()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                2.4f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -12.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                2.4f);
        Mob threat =
            await CreateTarget(
                new Vector3(
                    1.65f,
                    -0.05f,
                    -2.4f));
        int eventsBefore =
            _slipstreamEvents;
        bool burstStarted =
            InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up);
        SetPrivate(
            "_groundBurstTimer",
            0.0f);

        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                0.0f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -6.2f));
        Check(
            burstStarted &&
                !_player.IsGroundBurstActive &&
                _slipstreamEvents ==
                    eventsBefore + 1,
            "an early Dodge remains eligible after its short physical impulse ends");

        threat.QueueFree();
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestSlipstreamDodge()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                2.4f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -12.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);
        Mob threat =
            await CreateTarget(
                Vector3.Zero);
        threat.GlobalPosition =
            new Vector3(
                1.65f,
                0.0f,
                0.0f);

        bool burstStarted =
            InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up);
        float nitroAfterBurst =
            _player.NitroCharge;
        float speedBeforeReward =
            HorizontalSpeed();
        int eventsBefore =
            _slipstreamEvents;
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                0.0f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -6.2f));
        Check(
            burstStarted &&
                _slipstreamEvents ==
                    eventsBefore + 1 &&
                _lastSlipstreamPerfect,
            "a grounded burst that crosses a close enemy triggers one perfect slipstream");
        Check(
            _lastSlipstreamScore >=
                    _player
                        .SlipstreamDodgeScore +
                    _player
                        .PerfectSlipstreamScoreBonus &&
                _player.NitroCharge >
                    nitroAfterBurst &&
                HorizontalSpeed() >
                    speedBeforeReward +
                        2.5f,
            "slipstream grants score, refunds Nitro, and adds a sharp speed impulse");

        int eventsAfterReward =
            _slipstreamEvents;
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -6.2f));
        Check(
            _slipstreamEvents ==
                eventsAfterReward,
            "the same enemy cannot be farmed for repeated slipstream rewards");

        threat.QueueFree();
        await PhysicsFrames(2);
        Mob ordinaryNearMiss =
            await CreateTarget(
                Vector3.Zero);
        ordinaryNearMiss.GlobalPosition =
            new Vector3(
                -1.65f,
                0.0f,
                0.0f);
        SetPrivate(
            "_groundBurstTimer",
            0.0f);
        SetPrivate(
            "_slipstreamDodgeWindowTimer",
            0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -20.0f);
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                0.0f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -6.2f));
        Check(
            _slipstreamEvents ==
                eventsAfterReward,
            "an ordinary near miss never impersonates a ground-burst slipstream");

        ordinaryNearMiss.QueueFree();
        await PhysicsFrames(2);
        Mob elevatedThreat =
            await CreateTarget(
                Vector3.Zero);
        elevatedThreat.GlobalPosition =
            new Vector3(
                1.65f,
                3.0f,
                0.0f);
        SetPrivate(
            "_groundBurstTimer",
            _player
                .GroundBurstDuration);
        SetPrivate(
            "_slipstreamDodgeWindowTimer",
            _player
                .SlipstreamDodgeInputWindow);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -30.0f);
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                0.0f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -2.4f));
        ProbeNearMissAt(
            new Vector3(
                0.0f,
                0.05f,
                -6.2f));
        Check(
            _slipstreamEvents ==
                eventsAfterReward,
            "a vertically separated enemy cannot award a fake ground slipstream");

        elevatedThreat.QueueFree();
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestLateralSlipstreamDodge()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -30.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        Mob crossingThreat =
            await CreateTarget(
                Vector3.Zero);
        crossingThreat.Velocity =
            new Vector3(
                18.0f,
                0.0f,
                0.0f);
        bool burstStarted =
            InvokePrivateBool(
                "TryPerformGroundBurst",
                Vector2.Up);
        int eventsBefore =
            _slipstreamEvents;

        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                -4.5f,
                0.0f,
                0.8f));
        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                -2.0f,
                0.0f,
                0.8f));
        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                -0.8f,
                0.0f,
                0.8f));
        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                0.8f,
                0.0f,
                0.8f));
        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                2.0f,
                0.0f,
                0.8f));
        ProbeNearMissWithMobAt(
            crossingThreat,
            new Vector3(
                6.2f,
                0.0f,
                0.8f));
        Check(
            burstStarted &&
                _slipstreamEvents ==
                    eventsBefore + 1 &&
                _lastSlipstreamPerfect,
            "a fast enemy crossing along the player's side awards a lateral Perfect Line " +
            $"(burst={burstStarted}, events={_slipstreamEvents - eventsBefore}, perfect={_lastSlipstreamPerfect})");

        crossingThreat.QueueFree();
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
    }

    private void ProbeNearMissAt(
        Vector3 position)
    {
        _player.GlobalPosition =
            position;
        SetPrivate(
            "_nearMissProbeTimer",
            0.0f);
        InvokePrivateVoid(
            "UpdateDaringDodges",
            0.05f);
    }

    private void ProbeNearMissWithMobAt(
        Mob mob,
        Vector3 position)
    {
        mob.GlobalPosition =
            position;
        SetPrivate(
            "_nearMissProbeTimer",
            0.0f);
        InvokePrivateVoid(
            "UpdateDaringDodges",
            0.05f);
    }

    private async System.Threading.Tasks.Task
        TestClutchVault()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -10.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(4);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        Mob vaultTarget =
            await CreateTarget(
                new Vector3(
                    2.55f,
                    -0.05f,
                    -0.55f));

        InvokePrivateVoid(
            "UpdateClutchVaultState",
            true,
            Vector3.Forward,
            0.016f);
        Check(
            _player.ClutchVaultCueTarget ==
                    vaultTarget &&
                _player.ClutchVaultCueUrgency >
                    0.15f,
            "a close enemy in front or alongside opens the Clutch Vault window");
        vaultTarget.GlobalPosition =
            new Vector3(
                4.55f,
                0.0f,
                -0.55f);
        InvokePrivateVoid(
            "UpdateClutchVaultState",
            true,
            Vector3.Forward,
            0.08f);
        Check(
            _player.ClutchVaultCueTarget ==
                    vaultTarget,
            "the expanded Clutch Vault window preserves a readable late input beat");
        vaultTarget.GlobalPosition =
            new Vector3(
                2.55f,
                0.0f,
                -0.55f);

        Vector3 origin =
            _player.GlobalPosition;
        int eventsBefore =
            _clutchVaultEvents;
        bool started =
            InvokeClutchVault(
                true,
                _player.Velocity,
                out Vector3 arrivalVelocity);
        Check(
            started &&
                _player.IsClutchVaultActive &&
                IsOverTarget(
                    vaultTarget) &&
                arrivalVelocity.Y <=
                    -_player
                        .ClutchVaultArrivalDiveSpeed +
                        0.01f,
            "Jump converts the close window into an immediate stomp-ready Blink");
        Check(
            !_player.GlobalPosition
                    .IsEqualApprox(origin) &&
                _player.ClutchVaultCueTarget ==
                    null &&
                _clutchVaultEvents ==
                    eventsBefore + 1 &&
                _lastClutchVaultOrigin
                    .IsEqualApprox(origin) &&
                _lastClutchVaultTarget
                    .IsEqualApprox(
                        _player
                            .GlobalPosition),
            "Clutch Blink places the player at its marked arrival point and clears the prompt");

        ResetRiftState();
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.8f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                2.0f,
                -8.0f);
        SetPrivate(
            "_airJumpsRemaining",
            1);
        vaultTarget.GlobalPosition =
            new Vector3(
                2.4f,
                0.0f,
                -0.45f);
        InvokePrivateVoid(
            "UpdateClutchVaultState",
            false,
            Vector3.Forward,
            0.016f);
        bool airStarted =
            InvokeClutchVault(
                false,
                _player.Velocity,
                out _);
        Check(
            airStarted &&
                GetPrivateInt(
                    "_airJumpsRemaining") ==
                    0,
            "an airborne Clutch Blink consumes the available double jump");

        vaultTarget.QueueFree();
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestClutchVaultPhysicalStomp()
    {
        ResetRiftState();
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -6.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(5);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        Mob target =
            await CreateTarget(
                new Vector3(
                    2.55f,
                    -0.05f,
                    -0.5f));
        target.CollisionLayer =
            1u << 1;
        target.CollisionMask = 0;
        int chainBefore =
            _player.AirStompChain;
        float healthBefore =
            _player.Health;

        Input.ActionPress("jump");
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
        Input.ActionRelease("jump");
        await PhysicsFrames(42);
        Check(
            _player.AirStompChain >
                    chainBefore &&
                Mathf.IsEqualApprox(
                    _player.Health,
                    healthBefore),
            "the live Clutch Blink arrival lands as a stomp without taking contact damage");

        Input.ActionRelease("jump");
        if (IsInstanceValid(target))
        {
            target.QueueFree();
        }
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestRiftTeleportTargetRules()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                4.0f,
                0.0f);
        Mob source = await CreateTarget(
            new Vector3(
                0.0f,
                -4.0f,
                -0.5f));
        Mob frontLeft = await CreateTarget(
            new Vector3(
                -4.5f,
                -4.0f,
                -8.0f));
        Mob frontRight = await CreateTarget(
            new Vector3(
                5.0f,
                -4.0f,
                -12.0f));
        Mob behind = await CreateTarget(
            new Vector3(
                0.0f,
                -4.0f,
                7.0f));
        SetPrivate(
            "_lastRiftStompMob",
            source);
        SetPrivate(
            "_riftStompWindowTimer",
            0.24f);
        float nitroBefore =
            _player.NitroCharge;
        int eventsBefore =
            _riftEvents;
        bool teleported =
            InvokePrivateBool(
                "TryPerformRiftTeleport");
        bool landedOverFrontTarget =
            IsOverTarget(frontLeft) ||
            IsOverTarget(frontRight);
        Check(
            teleported &&
                landedOverFrontTarget &&
                _lastRiftTeleported &&
                _riftEvents ==
                    eventsBefore + 1,
            "stomp input teleports above one random valid enemy ahead");
        Check(
            !IsOverTarget(source) &&
                !IsOverTarget(behind) &&
                _player.Velocity.Y <=
                    -_player
                        .RiftArrivalDiveSpeed +
                        0.01f,
            "rift excludes the stomped enemy and every enemy behind the player");
        Check(
            Mathf.Abs(
                nitroBefore -
                    _player.NitroCharge -
                    _player.RiftTeleportCost) <=
                0.05f &&
                _player.IsDamageInvulnerable,
            "teleport pays its larger cost and protects only the arrival beat");

        source.QueueFree();
        frontLeft.QueueFree();
        frontRight.QueueFree();
        behind.QueueFree();
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestArenaRiftTargetRules()
    {
        ResetRiftState();
        _player.SetArenaCombatActive(true);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                4.0f,
                0.0f);
        Mob source = await CreateTarget(
            new Vector3(
                0.0f,
                -4.0f,
                -0.5f));
        source.ConfigureEncounter(71);
        Mob arenaTargetBehind =
            await CreateTarget(
                new Vector3(
                    0.0f,
                    -4.0f,
                    26.0f));
        arenaTargetBehind
            .ConfigureEncounter(71);
        Mob nonArenaTargetAhead =
            await CreateTarget(
                new Vector3(
                    0.0f,
                    -4.0f,
                    -8.0f));
        SetPrivate(
            "_lastRiftStompMob",
            source);
        SetPrivate(
            "_riftStompWindowTimer",
            0.24f);

        bool teleported =
            InvokePrivateBool(
                "TryPerformRiftTeleport");
        Check(
            teleported &&
                IsOverTarget(
                    arenaTargetBehind) &&
                !IsOverTarget(
                    nonArenaTargetAhead),
            "arena Rift Step reaches a valid enemy across the full circle");
        Check(
            _lastRiftDestination.Z >
                _lastRiftOrigin.Z,
            "arena target selection accepts enemies behind the scrolling direction");

        source.QueueFree();
        arenaTargetBehind.QueueFree();
        nonArenaTargetAhead.QueueFree();
        _player.SetArenaCombatActive(false);
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestAirborneRiftTeleport()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                4.0f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                -12.0f,
                -8.0f);
        // Let CharacterBody clear the previous floor contact. This mirrors
        // the real frame between leaving the ground and touching a mob.
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
        _player.SetPhysicsProcess(false);
        Mob target = await CreateTarget(
            new Vector3(
                3.0f,
                -4.0f,
                -9.0f));
        float nitroBefore =
            _player.NitroCharge;
        int eventsBefore =
            _riftEvents;
        InvokePrivateVoid(
            "QueueRiftStep",
            Vector3.Forward);
        Check(
            !GetPrivateBool(
                "_riftRequestPending") &&
                IsOverTarget(target) &&
                _lastRiftTeleported &&
                _riftEvents ==
                    eventsBefore + 1,
            "an ordinary airborne press teleports to a valid enemy ahead");
        Check(
            Mathf.Abs(
                nitroBefore -
                    _player.NitroCharge -
                    _player.RiftTeleportCost) <=
                0.05f &&
                _player.Velocity.Y <=
                    -_player
                        .RiftArrivalDiveSpeed +
                        0.01f,
            "airborne Rift keeps its Nitro cost and arrives in a stomp-ready dive");

        target.QueueFree();
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestBufferedAirborneRiftTeleport()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                4.0f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                -8.0f,
                -6.0f);
        _player.SetPhysicsProcess(true);
        await PhysicsFrames(2);
        _player.SetPhysicsProcess(false);
        float nitroBefore =
            _player.NitroCharge;
        InvokePrivateVoid(
            "QueueRiftStep",
            Vector3.Forward);
        Check(
            GetPrivateBool(
                "_riftRequestPending") &&
                Mathf.IsEqualApprox(
                    nitroBefore,
                    _player.NitroCharge),
            "an airborne Rift press with no target remains briefly buffered");

        Mob lateTarget =
            await CreateTarget(
                new Vector3(
                    -2.5f,
                    -3.5f,
                    -8.0f));
        InvokePrivateVoid(
            "TryConsumeQueuedRiftStep",
            Vector3.Forward);
        Check(
            !GetPrivateBool(
                "_riftRequestPending") &&
                IsOverTarget(lateTarget) &&
                _lastRiftTeleported,
            "the buffered airborne Rift resolves when a target enters the route");

        lateTarget.QueueFree();
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestBlockedRiftPath()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                3.0f,
                0.0f);
        Mob blockedTarget =
            await CreateTarget(
                new Vector3(
                    0.0f,
                    -3.0f,
                    -10.0f));
        StaticBody3D wall =
            new()
            {
                Name = "RiftSafetyWall",
                CollisionLayer =
                    1u << 3,
                CollisionMask = 0
            };
        wall.AddChild(
            new CollisionShape3D
            {
                Shape = new BoxShape3D
                {
                    Size = new Vector3(
                        8.0f,
                        8.0f,
                        0.8f)
                }
            });
        AddChild(wall);
        wall.GlobalPosition =
            new Vector3(
                0.0f,
                3.0f,
                -5.0f);
        await PhysicsFrames(2);
        SetPrivate(
            "_riftStompWindowTimer",
            0.24f);
        float nitroBefore =
            _player.NitroCharge;
        bool teleported =
            InvokePrivateBool(
                "TryPerformRiftTeleport");
        Check(
            !teleported &&
                Mathf.IsEqualApprox(
                    nitroBefore,
                    _player.NitroCharge),
            "rift never crosses an obstacle and never charges a failed cast");

        wall.QueueFree();
        blockedTarget.QueueFree();
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestKeyboardTapHoldSplit()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(true);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.05f,
                0.0f);
        _player.Velocity =
            Vector3.Zero;
        await PhysicsFrames(5);
        GamepadInput.Observe(
            new InputEventKey
            {
                PhysicalKeycode =
                    Key.Shift,
                Pressed = true
            });

        int eventsBeforeTap =
            _riftEvents;
        Input.ActionPress(
            "dash",
            1.0f);
        await PhysicsFrames(3);
        Input.ActionRelease("dash");
        await PhysicsFrames(3);
        Check(
            _riftEvents ==
                eventsBeforeTap + 1 &&
                !_player.IsNitroOverdrive,
            "short keyboard Shift triggers Rift Step once");

        await PhysicsFrames(32);
        int eventsBeforeHold =
            _riftEvents;
        Input.ActionPress(
            "dash",
            1.0f);
        await PhysicsFrames(18);
        Check(
            _player.IsNitroOverdrive &&
                _riftEvents ==
                    eventsBeforeHold,
            "held keyboard Shift starts thrust without an extra Rift Step");
        Input.ActionRelease("dash");
        await PhysicsFrames(3);
        Check(
            !_player.IsNitroOverdrive,
            "releasing held Shift stops thrust");
    }

    private async System.Threading.Tasks.Task
        TestAnalogThrust()
    {
        ResetRiftState();
        _player.SetPhysicsProcess(true);
        float nitroBefore =
            _player.NitroCharge;
        Input.ActionPress(
            "nitro_throttle",
            0.45f);
        await PhysicsFrames(10);
        float partial =
            _player.NitroThrottle;
        Input.ActionPress(
            "nitro_throttle",
            1.0f);
        await PhysicsFrames(10);
        float full =
            _player.NitroThrottle;
        Input.ActionRelease(
            "nitro_throttle");
        Check(
            partial > 0.25f &&
                partial < 0.7f &&
                full > 0.95f &&
                _player.NitroCharge <
                    nitroBefore,
            $"RT keeps analog thrust authority ({partial:0.00} -> {full:0.00})");
    }

    private async System.Threading.Tasks.Task
        TestMobileGestureSequence()
    {
        MobileControls controls =
            new()
            {
                Name = "MobileGestureProbe",
                ShowOnDesktop = true
            };
        AddChild(controls);
        await PhysicsFrames(1);
        controls.SetProcess(false);

        QueueMobileSwipe(
            controls,
            Vector2.Zero);
        Check(
            !controls.ConsumeRiftStep() &&
                !controls
                    .ConsumeNitroMaximum(),
            "one upward swipe waits briefly for a possible second swipe");
        SetObjectPrivate(
            controls,
            "_nitroUpSwipePendingSinceMs",
            0UL);
        InvokeObjectPrivate(
            controls,
            "UpdateNitroSwipeSequence");
        Check(
            controls.ConsumeRiftStep(),
            "one sharp upward swipe resolves to Rift Step");

        InvokeObjectPrivate(
            controls,
            "ResetInputState");
        QueueMobileSwipe(
            controls,
            Vector2.Zero);
        SetObjectPrivate(
            controls,
            "_nitroSecondSwipeArmed",
            true);
        SetObjectPrivate(
            controls,
            "_nitroGestureStartedMs",
            Time.GetTicksMsec());
        InvokeObjectPrivate(
            controls,
            "CompleteNitroGesture",
            Vector2.Zero);
        Check(
            controls
                .ConsumeNitroMaximum() &&
                !controls
                    .ConsumeRiftStep(),
            "double upward swipe starts thrust without an extra Rift Step");

        controls.QueueFree();
        await PhysicsFrames(2);
    }

    private void QueueMobileSwipe(
        MobileControls controls,
        Vector2 endPosition)
    {
        SetObjectPrivate(
            controls,
            "_nitroGestureStart",
            new Vector2(
                0.0f,
                100.0f));
        SetObjectPrivate(
            controls,
            "_nitroGestureStartedMs",
            Time.GetTicksMsec());
        InvokeObjectPrivate(
            controls,
            "CompleteNitroGesture",
            endPosition);
    }

    private void OnRiftStepStarted(
        bool teleported,
        Vector3 origin,
        Vector3 destination)
    {
        _riftEvents++;
        _lastRiftTeleported =
            teleported;
        _lastRiftOrigin = origin;
        _lastRiftDestination =
            destination;
    }

    private void OnSlipstreamDodge(
        int scoreReward,
        float nitroReward,
        float speedImpulse,
        bool perfectDodge,
        Vector3 worldPosition,
        Vector3 passDirection)
    {
        _slipstreamEvents++;
        _lastSlipstreamScore =
            scoreReward;
        _lastSlipstreamPerfect =
            perfectDodge;
    }

    private void OnClutchVaultStarted(
        Vector3 origin,
        Vector3 targetPosition)
    {
        _clutchVaultEvents++;
        _lastClutchVaultOrigin =
            origin;
        _lastClutchVaultTarget =
            targetPosition;
    }

    private bool IsOverTarget(Mob target)
    {
        if (!IsInstanceValid(target))
        {
            return false;
        }
        Vector2 horizontalOffset =
            new(
                _player.GlobalPosition.X -
                    target.GlobalPosition.X,
                _player.GlobalPosition.Z -
                    target.GlobalPosition.Z);
        float verticalOffset =
            _player.GlobalPosition.Y -
            target.GlobalPosition.Y;
        return horizontalOffset.Length() <=
                1.8f &&
            verticalOffset >= 1.0f &&
            verticalOffset <= 4.5f;
    }

    private async System.Threading.Tasks.Task<Mob>
        CreateTarget(
            Vector3 localOffset)
    {
        PackedScene mobScene =
            GD.Load<PackedScene>(
                "res://mob.tscn");
        Mob mob =
            mobScene.Instantiate<Mob>();
        AddChild(mob);
        VisibleOnScreenNotifier3D notifier =
            mob.GetNodeOrNull<
                VisibleOnScreenNotifier3D>(
                "VisibleOnScreenNotifier3D");
        if (notifier != null)
        {
            notifier.ProcessMode =
                ProcessModeEnum.Disabled;
        }
        mob.GlobalPosition =
            _player.GlobalPosition +
            localOffset;
        mob.CollisionLayer = 0;
        mob.CollisionMask = 0;
        mob.SetPhysicsProcess(false);
        await PhysicsFrames(1);
        return mob;
    }

    private void ResetRiftState()
    {
        _player.SetArenaCombatActive(false);
        InvokePrivateVoid(
            "ResetRiftStepState");
        InvokePrivateVoid(
            "StopNitroOverdrive");
        SetPrivate(
            "_nitroDashCooldownTimer",
            0.0f);
        SetPrivate(
            "_nitroSpool",
            0.0f);
        SetPrivate(
            "_nitroBurning",
            false);
        SetPrivate(
            "_nitroWasBurning",
            false);
        SetPrivate(
            "_nitroInputWasPressed",
            false);
        SetPrivate(
            "_nitroActionUsesHold",
            false);
        SetPrivate(
            "_nitroActionStartedReactor",
            false);
        SetPrivate(
            "_nitroActionHoldTimer",
            0.0f);
        SetPrivate(
            "_damageInvulnerabilityTimer",
            0.0f);
        _player.AddNitro(100.0f);
        _lastRiftTeleported = false;
        _lastRiftOrigin =
            Vector3.Zero;
        _lastRiftDestination =
            Vector3.Zero;
    }

    private void SetPrivate(
        string name,
        object value)
    {
        FieldInfo field =
            typeof(Player).GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(
                _player,
                value);
            return;
        }
        if (TryGetAbilityProperty(
                name,
                out object abilityState,
                out PropertyInfo property))
        {
            property.SetValue(
                abilityState,
                value);
            return;
        }

        Check(
            false,
            $"test field or ability property {name} exists");
    }

    private bool TryGetAbilityProperty(
        string legacyName,
        out object abilityState,
        out PropertyInfo property)
    {
        FieldInfo stateField =
            typeof(Player).GetField(
                "_abilityState",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        abilityState = stateField?.GetValue(
            _player);
        string propertyName =
            legacyName.Length > 1
                ? char.ToUpperInvariant(
                    legacyName[1]) +
                    legacyName.Substring(2)
                : string.Empty;
        property = abilityState?.GetType()
            .GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public);
        return abilityState != null &&
            property != null;
    }

    private void SetObjectPrivate(
        object instance,
        string name,
        object value)
    {
        FieldInfo field =
            instance.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (field == null)
        {
            Check(
                false,
                $"test field {name} exists");
            return;
        }
        field.SetValue(
            instance,
            value);
    }

    private bool GetPrivateBool(
        string name)
    {
        FieldInfo field =
            typeof(Player).GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (field != null)
        {
            return (bool)field.GetValue(
                _player);
        }
        if (TryGetAbilityProperty(
                name,
                out object abilityState,
                out PropertyInfo property))
        {
            return (bool)property.GetValue(
                abilityState);
        }

        Check(
            false,
            $"test field or ability property {name} exists");
        return false;
    }

    private int GetPrivateInt(
        string name)
    {
        FieldInfo field =
            typeof(Player).GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (field == null)
        {
            Check(
                false,
                $"test field {name} exists");
            return 0;
        }
        return (int)field.GetValue(
            _player);
    }

    private bool InvokeClutchVault(
        bool wasOnFloor,
        Vector3 initialVelocity,
        out Vector3 launchVelocity)
    {
        MethodInfo method =
            typeof(Player).GetMethod(
                "TryStartClutchVault",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (method == null)
        {
            Check(
                false,
                "test method TryStartClutchVault exists");
            launchVelocity =
                initialVelocity;
            return false;
        }
        object[] arguments =
        {
            wasOnFloor,
            initialVelocity
        };
        bool started =
            (bool)method.Invoke(
                _player,
                arguments);
        launchVelocity =
            (Vector3)arguments[1];
        return started;
    }

    private bool InvokePrivateBool(
        string name,
        params object[] arguments)
    {
        MethodInfo method =
            typeof(Player).GetMethod(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (method == null)
        {
            Check(
                false,
                $"test method {name} exists");
            return false;
        }
        return (bool)method.Invoke(
            _player,
            arguments);
    }

    private void InvokePrivateVoid(
        string name,
        params object[] arguments)
    {
        MethodInfo method =
            typeof(Player).GetMethod(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (method == null)
        {
            Check(
                false,
                $"test method {name} exists");
            return;
        }
        method.Invoke(
            _player,
            arguments);
    }

    private void InvokeObjectPrivate(
        object instance,
        string name,
        params object[] arguments)
    {
        MethodInfo method =
            instance.GetType().GetMethod(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        if (method == null)
        {
            Check(
                false,
                $"test method {name} exists");
            return;
        }
        method.Invoke(
            instance,
            arguments);
    }

    private float HorizontalSpeed()
    {
        return new Vector2(
            _player.Velocity.X,
            _player.Velocity.Z)
            .Length();
    }

    private async System.Threading.Tasks.Task
        PhysicsFrames(int count)
    {
        for (int index = 0;
            index < count;
            index++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName
                    .PhysicsFrame);
        }
    }

    private void Check(
        bool condition,
        string message)
    {
        if (condition)
        {
            GD.Print(
                $"PASS: {message}");
            return;
        }
        _failed = true;
        GD.PushError(
            $"FAIL: {message}");
    }
}
