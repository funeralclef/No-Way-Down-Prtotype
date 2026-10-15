using Godot;
using System;
using System.Reflection;

public partial class PlayerComponentsProbe : Node
{
    private int _failures;

    public override void _Ready()
    {
        CheckPlayerBoundary();
        CheckAbilityOrder();
        CheckComboOneShots();
        CheckContactDebouncing();
        CheckMovementCalculations();

        GD.Print(
            _failures == 0
                ? "PLAYER_COMPONENTS_PROBE: PASSED"
                : $"PLAYER_COMPONENTS_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }

    private void CheckPlayerBoundary()
    {
        bool ownsComponents =
            typeof(Player).GetField(
                "_abilityState",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
                ?.FieldType ==
                    typeof(PlayerAbilityState) &&
            typeof(Player).GetField(
                "_comboState",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
                ?.FieldType ==
                    typeof(PlayerComboState) &&
            typeof(Player).GetField(
                "_contactTracker",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
                ?.FieldType ==
                    typeof(EnemyContactTracker);
        Check(
            typeof(CharacterBody3D)
                .IsAssignableFrom(
                    typeof(Player)) &&
            ownsComponents &&
            !typeof(Node).IsAssignableFrom(
                typeof(PlayerAbilityState)),
            "Player keeps CharacterBody physics and owns scene-free state components");
    }

    private void CheckAbilityOrder()
    {
        PlayerAbilityState state = new();
        state.InitializeNitro(42.0f, 100.0f);
        bool started =
            state.TryStartOverdrive(4.0f);
        state.QueueRift(
            Vector2.Up,
            0.15f);
        state.Tick(0.05f);
        state.ResetRift();
        Check(
            started &&
            state.NitroLatched &&
            !state.RiftRequestPending &&
            Mathf.IsEqualApprox(
                state.NitroCharge,
                42.0f),
            "Rift reset does not consume or stop an already-started Nitro reactor");

        bool changed =
            state.SetNitroCharge(
                0.0f,
                100.0f);
        bool repeated =
            state.SetNitroCharge(
                0.0f,
                100.0f);
        Check(
            changed &&
            !repeated &&
            !state.NitroBurning &&
            !new PlayerAbilityState()
                .TryStartOverdrive(4.0f),
            "Nitro charge updates are idempotent and an empty reactor cannot start");
    }

    private void CheckComboOneShots()
    {
        PlayerComboState combo = new();
        for (int index = 0;
            index < 3;
            index++)
        {
            combo.AdvanceStomp(0.36f);
        }
        bool firstRescue =
            combo.ActivateRescue(
                true,
                100.0f);
        int styleAfterFirst =
            combo.StylePoints;
        bool duplicateRescue =
            combo.ActivateRescue(
                true,
                100.0f);
        Check(
            firstRescue &&
            !duplicateRescue &&
            combo.StylePoints ==
                styleAfterFirst &&
            combo.GetFlowTier() ==
                Player.ComboFlowTier.Rhythm,
            "combo rescue applies its rewards once and preserves tier order");

        PlayerComboEnd ended =
            combo.EndFlow();
        PlayerComboEnd repeatedEnd =
            combo.EndFlow();
        Check(
            ended.ChainCount == 3 &&
            repeatedEnd.ChainCount == 0 &&
            combo.AirStompChain == 0 &&
            combo.StylePoints == 0,
            "ending a combo returns one snapshot without duplicate banking state");
    }

    private void CheckContactDebouncing()
    {
        const ulong enemyId = 71;
        EnemyContactTracker tracker =
            new();
        tracker.BeginFrame(0.016f);
        tracker.Register(
            enemyId,
            false);
        bool firstSensorSample =
            tracker.AccumulateDanger(
                enemyId,
                0.016f,
                0.02f);
        tracker.EndFrame();
        tracker.BeginFrame(0.016f);
        tracker.Register(
            enemyId,
            false);
        bool confirmedSensorContact =
            tracker.AccumulateDanger(
                enemyId,
                0.016f,
                0.02f);
        Check(
            !firstSensorSample &&
            confirmedSensorContact,
            "sensor contact requires confirmation across ordered physics samples");

        bool firstReaction =
            tracker.TryBeginReaction(
                enemyId,
                0.25f);
        bool duplicateReaction =
            tracker.TryBeginReaction(
                enemyId,
                0.25f);
        Check(
            firstReaction &&
            !duplicateReaction,
            "repeat contact cannot apply the same graze side effect twice");

        tracker.ClearDanger();
        tracker.EndFrame();
        Check(
            tracker.ExposureCount == 0,
            "resolved contact clears pending damage exposure");
    }

    private void CheckMovementCalculations()
    {
        Vector2 accelerated =
            PlayerMovementMotor
                .MovePlanarVelocity(
                    Vector2.Zero,
                    new Vector2(0.0f, -20.0f),
                    true,
                    true,
                    60.0f,
                    80.0f,
                    40.0f,
                    20.0f,
                    30.0f,
                    10.0f,
                    1.0f,
                    0.1f);
        float releasedGravity =
            PlayerMovementMotor
                .GetGravityMultiplier(
                    true,
                    false,
                    5.0f,
                    2.0f,
                    3.0f,
                    0.55f);
        float apexGravity =
            PlayerMovementMotor
                .GetGravityMultiplier(
                    true,
                    true,
                    1.0f,
                    2.0f,
                    3.0f,
                    0.55f);
        Check(
            Mathf.IsEqualApprox(
                accelerated.Length(),
                6.0f) &&
            Mathf.IsEqualApprox(
                releasedGravity,
                2.0f) &&
            Mathf.IsEqualApprox(
                apexGravity,
                0.55f),
            "movement motor preserves acceleration and jump gravity ordering");
    }

    private void Check(
        bool condition,
        string description)
    {
        if (condition)
        {
            GD.Print($"PASS: {description}");
            return;
        }
        _failures++;
        GD.PushError($"FAIL: {description}");
    }
}
