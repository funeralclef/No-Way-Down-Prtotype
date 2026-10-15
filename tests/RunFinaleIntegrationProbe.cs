using Godot;
using System.Reflection;

public partial class RunFinaleIntegrationProbe : Node
{
    private int _failures;

    public override async void _Ready()
    {
        ProcessMode =
            ProcessModeEnum.Always;
        await WaitFrames(2);

        Main game = GetParent() as Main;
        ProceduralWorld world =
            game?.GetNodeOrNull<ProceduralWorld>(
                "RunSystems/ProceduralWorld");
        ArenaRewardOverlay overlay =
            ReadField<ArenaRewardOverlay>(
                game,
                "_arenaRewardOverlay");
        RunProgression progression =
            ReadField<RunProgression>(
                game,
                "_runProgression");
        AbyssPressureFront abyss =
            game?.GetNodeOrNull<AbyssPressureFront>(
                "RunSystems/AbyssPressureFront");
        RunProgressHud progressHud =
            ReadField<RunProgressHud>(
                game,
                "_runProgressHud");
        if (!IsInstanceValid(game) ||
            !IsInstanceValid(world) ||
            !IsInstanceValid(overlay) ||
            progression == null)
        {
            GD.PushError(
                "RUN_FINALE_INTEGRATION: missing live systems");
            GetTree().Quit(1);
            return;
        }

        for (int ordinal = 1;
            ordinal <= 3;
            ordinal++)
        {
            world.TryGetArenaSpan(
                ordinal,
                out int section,
                out _,
                out _);
            world.CompleteArena(section);
        }
        world.TryGetArenaSpan(
            4,
            out int finalSection,
            out float entryZ,
            out float exitZ);
        int draftBefore =
            progression.ArenaNumber;
        var player = game.GetNode<Player>("Player");
        player.SetPhysicsProcess(false);
        player.Position = new Vector3(0, 0, (entryZ + exitZ) / 2 + 8);
        world.UpdateForPlayer(player);
        var boss = ReadField<BossEncounter>(game, "_bossEncounter");
        Check(IsInstanceValid(boss) && boss.IsActive,
            "the fourth arena replaces ordinary waves with a final guardian");
        Invoke(game, "CompleteArenaEncounter");
        Check(!overlay.IsFinaleStageVisible,
            "the finale stays locked while the guardian is alive");
        boss.SetPhysicsProcess(false);
        for (int hit = 0; hit < 8; hit++)
        {
            for (int step = 0; !boss.IsExposed && step < 60; step++)
            {
                boss._PhysicsProcess(.65);
                foreach (Node child in boss.GetChildren())
                    if (child is BossMob seal && seal.IsSeal && !seal.IsQueuedForDeletion() && seal.CanBeStomped)
                    { seal.Squash(1, player); break; }
            }
            boss.Core.Squash(1, player);
            await WaitFrames(1);
        }
        await WaitSeconds(0.7f);

        Check(
            GetTree().Paused &&
                overlay.IsFinaleStageVisible,
            "the fourth clear pauses on a dedicated final decision");
        Check(
            progression.ArenaNumber == draftBefore,
            "the final decision does not create an unused reward draft");
        Check(
            IsInstanceValid(progressHud) &&
                !progressHud.Visible,
            "the compact progress rail yields focus to the final decision");

        Invoke(overlay, "ActivateCard", 1);
        await WaitSeconds(0.58f);
        Check(
            progression.ArenaNumber ==
                draftBefore + 1 &&
                ReadField<bool>(
                    game,
                    "_continuedBeyondFinal"),
            "choosing beyond preserves the run and opens its reward");
        Check(
            world.CompletedArenaCount == 3,
            "the exit remains sealed until the reward and route are committed");

        Invoke(overlay, "ActivateCard", 0);
        await WaitSeconds(0.54f);
        Invoke(overlay, "ActivateCard", 0);
        await WaitSeconds(0.62f);

        Check(
            world.CompletedArenaCount == 4 &&
                world.OverrunTier == 1 &&
                abyss?.OverrunTier == 1,
            "the first beyond chapter starts at bounded difficulty tier one");
        Check(
            !GetTree().Paused &&
                !overlay.Visible &&
                progressHud.Visible,
            "the run resumes only after the complete continuation flow");

        GD.Print(
            _failures == 0
                ? "RUN_FINALE_INTEGRATION: PASSED"
                : $"RUN_FINALE_INTEGRATION: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }

    private async System.Threading.Tasks.Task
        WaitFrames(int count)
    {
        for (int frame = 0;
            frame < count;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.ProcessFrame);
        }
    }

    private async System.Threading.Tasks.Task
        WaitSeconds(float seconds)
    {
        await ToSignal(
            GetTree().CreateTimer(
                Mathf.Max(0.01f, seconds),
                true),
            SceneTreeTimer.SignalName.Timeout);
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

    private static T ReadField<T>(
        object target,
        string fieldName)
    {
        object value = target?.GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance |
                    BindingFlags.NonPublic)
            ?.GetValue(target);
        return value is T typed
            ? typed
            : default;
    }

    private static void WriteField<T>(
        object target,
        string fieldName,
        T value)
    {
        target.GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance |
                    BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static void Invoke(
        object target,
        string methodName,
        params object[] arguments)
    {
        target.GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                    BindingFlags.NonPublic)
            ?.Invoke(target, arguments);
    }
}
