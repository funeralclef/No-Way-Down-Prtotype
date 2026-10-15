using Godot;
using System;
using System.Reflection;

/// <summary>
/// Headless checks for the pressure contract: elastic pursuit, action relief,
/// arena suspension and a safe, non-repeating breach recovery.
/// </summary>
public partial class AbyssPressureProbe : Node3D
{
    private bool _failed;
    private Player _player;
    private ProceduralWorld _world;
    private AbyssPressureFront _pressure;

    public override async void _Ready()
    {
        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        _player =
            playerScene.Instantiate<Player>();
        AddChild(_player);

        _world = new ProceduralWorld
        {
            Name = "ProbeWorld",
            GenerationSettings =
                new WorldGenerationSettings
                {
                    GenerationAheadDistance =
                        180.0f,
                    RetainBehindDistance =
                        100.0f
                }
        };
        AddChild(_world);

        Control interfaceRoot = new()
        {
            Name = "ProbeInterface"
        };
        interfaceRoot.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        AddChild(interfaceRoot);

        _pressure = new AbyssPressureFront
        {
            Name = "ProbePressure"
        };
        AddChild(_pressure);
        _pressure.Initialize(
            _player,
            _world,
            interfaceRoot);

        await PhysicsFrames(3);
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.8f,
                0.0f);
        _player.Velocity =
            Vector3.Zero;

        TestElasticPursuit();
        TestSurgeReachesActiveRunner();
        TestRiskAndActionRelief();
        TestArenaSuspension();
        TestBreachRecovery();

        _pressure.QueueFree();
        _world.QueueFree();
        _player.QueueFree();
        await PhysicsFrames(3);
        GD.Print(
            _failed
                ? "ABYSS_PRESSURE_PROBE: FAILED"
                : "ABYSS_PRESSURE_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }

    private void TestElasticPursuit()
    {
        _pressure.Advance(
            1.0f / 60.0f,
            true,
            0);
        float initialDistance =
            _pressure.DistanceToPlayer;
        for (int index = 0;
            index < 120;
            index++)
        {
            _pressure.Advance(
                1.0f / 60.0f,
                true,
                0);
        }

        Check(
            _pressure.DistanceToPlayer <
                initialDistance - 10.0f,
            "stopping lets the front close its elastic buffer");
        Check(
            _pressure.DistanceToPlayer >
                _pressure.BreachDistance,
            "the opening pressure remains readable before first breach");
    }

    private void TestRiskAndActionRelief()
    {
        SetPrivateField(
            _pressure,
            "_frontZ",
            _player.GlobalPosition.Z +
                13.0f);
        _pressure.Advance(
            1.0f / 60.0f,
            true,
            2);
        Check(
            _pressure.IsRiskRewardActive &&
                _pressure.ApplyScoreRiskBonus(4) >
                    4,
            "near-front kills receive a bounded score bonus");

        float beforeKill =
            _pressure.DistanceToPlayer;
        _pressure.RewardEnemyDefeat(6);
        Check(
            _pressure.DistanceToPlayer >
                beforeKill + 0.45f,
            "a combo kill buys visible breathing room");

        float beforeTunnel =
            _pressure.DistanceToPlayer;
        _player.EnterTunnelPhase();
        Check(
            _pressure.DistanceToPlayer >
                beforeTunnel + 3.5f,
            "entering a tunnel decisively pushes the front back");
        _player.ExitTunnelPhase();
    }

    private void TestSurgeReachesActiveRunner()
    {
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                0.8f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                0.0f,
                -13.6f);
        SetPrivateField(
            _pressure,
            "_tierIndex",
            0);
        SetPrivateField(
            _pressure,
            "_postArenaBreather",
            0.0f);
        SetPrivateField(
            _pressure,
            "_frontZ",
            _player.GlobalPosition.Z +
                _pressure.MaximumBuffer);
        float cycleLength =
            (float)GetPrivateField(
                _pressure,
                "_cycleLength");
        SetPrivateField(
            _pressure,
            "_cycleTimer",
            Mathf.Max(
                0.0f,
                cycleLength - 2.55f));

        float closestDistance =
            _pressure.MaximumBuffer;
        for (int index = 0;
            index < 150;
            index++)
        {
            Vector3 position =
                _player.GlobalPosition;
            position.Z -=
                13.6f / 60.0f;
            _player.GlobalPosition =
                position;
            _pressure.Advance(
                1.0f / 60.0f,
                true,
                0);
            closestDistance =
                Mathf.Min(
                    closestDistance,
                    _pressure
                        .DistanceToPlayer);
        }

        Check(
            closestDistance <
                _pressure.RiskDistance,
            $"a telegraphed surge reaches even a steady forward runner ({closestDistance:0.00}m)");
        Check(
            closestDistance >
                _pressure.BreachDistance +
                    4.0f,
            "the opening surge pressures without auto-hitting a steady runner");
        _player.Velocity =
            Vector3.Zero;
    }

    private void TestArenaSuspension()
    {
        _pressure.EnterArena(
            new Vector3(
                0.0f,
                0.0f,
                -25.0f),
            28.0f);
        float arenaDistance =
            _pressure.DistanceToPlayer;
        for (int index = 0;
            index < 180;
            index++)
        {
            _pressure.Advance(
                1.0f / 60.0f,
                true,
                3);
        }
        _pressure.RewardEnemyDefeat(12);
        Check(
            Mathf.Abs(
                _pressure.DistanceToPlayer -
                    arenaDistance) <
                0.05f,
            "the front is sealed and cannot reward or attack arena play");
        Check(
            _pressure.ApplyScoreRiskBonus(5) ==
                5,
            "arena kills never receive pressure rewards");

        _pressure.ExitArena(2.0f);
        Check(
            !_pressure.IsArenaPaused &&
                _pressure.DistanceToPlayer >=
                    _pressure.InitialDistance -
                    0.1f,
            "leaving the arena restores a safe post-arena buffer");
    }

    private void TestBreachRecovery()
    {
        SetPrivateField(
            _pressure,
            "_postArenaBreather",
            0.0f);
        SetPrivateField(
            _pressure,
            "_frontZ",
            _player.GlobalPosition.Z +
                1.5f);
        float healthBefore =
            _player.Health;
        _pressure.Advance(
            1.0f / 60.0f,
            true,
            4);
        float healthAfter =
            _player.Health;
        Check(
            Mathf.IsEqualApprox(
                healthBefore - healthAfter,
                _pressure.BreachDamage),
            "a breach removes fixed HP instead of causing an instant kill");
        Check(
            _player.Velocity.Z < -10.0f &&
                _player.Velocity.Y >= 4.7f &&
                _pressure.DistanceToPlayer >=
                    _pressure.BreachRetreatDistance,
            "a breach restores forward motion and retreats the boundary");

        SetPrivateField(
            _pressure,
            "_frontZ",
            _player.GlobalPosition.Z +
                1.5f);
        _pressure.Advance(
            1.0f / 60.0f,
            true,
            4);
        Check(
            Mathf.IsEqualApprox(
                _player.Health,
                healthAfter),
            "breach recovery prevents repeated damage in the same moment");
    }

    private void Check(
        bool condition,
        string description)
    {
        if (condition)
        {
            GD.Print(
                $"PASS: {description}");
            return;
        }

        _failed = true;
        GD.PushError(
            $"FAIL: {description}");
    }

    private static void SetPrivateField(
        object instance,
        string fieldName,
        object value)
    {
        FieldInfo field =
            instance
                .GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Missing field {fieldName}");
        }

        field.SetValue(
            instance,
            value);
    }

    private static object GetPrivateField(
        object instance,
        string fieldName)
    {
        FieldInfo field =
            instance
                .GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Missing field {fieldName}");
        }

        return field.GetValue(
            instance);
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
}
