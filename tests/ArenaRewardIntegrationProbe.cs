using Godot;
using System;
using System.Reflection;

public partial class ArenaRewardIntegrationProbe : Node
{
    private bool _failed;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Main game = GetNode<Main>("Game");
        await ProcessFrames(5);

        ProceduralWorld world = ReadField<ProceduralWorld>(
            game,
            "_world");
        Player player = ReadField<Player>(
            game,
            "_player");
        ArenaRewardOverlay overlay =
            ReadField<ArenaRewardOverlay>(
                game,
                "_arenaRewardOverlay");
        RunProgression progression =
            ReadField<RunProgression>(
                game,
                "_runProgression");
        typeof(ProceduralWorld)
            .GetMethod(
                "EnsureGeneratedThrough",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            .Invoke(
                world,
                new object[] { -1600.0f });
        int arenaIndex = FindFirstArenaIndex(world);
        Check(
            arenaIndex >= 0,
            "a pre-generated arena is available for the lifecycle probe");
        if (arenaIndex < 0)
        {
            GetTree().Quit(1);
            return;
        }

        WriteField(
            game,
            "_activeArenaSection",
            arenaIndex);
        Invoke(game, "CompleteArenaEncounter");
        await WaitSeconds(0.55f);

        Check(
            overlay.Visible &&
            GetTree().Paused &&
            ReadField<bool>(
                game,
                "_arenaRewardSequenceActive"),
            "arena clear opens a mandatory paused reward draft");
        Check(
            !ReadArenaCompleted(
                world,
                arenaIndex),
            "the arena exit remains sealed during reward selection");

        overlay._UnhandledInput(Accept());
        await WaitSeconds(0.55f);
        Check(
            progression.SelectedReward != null &&
            player.HasRunUpgrade(
                progression.SelectedReward.Id) &&
            progression.CurrentRouteOffers.Count == 2,
            "reward selection reaches Player before route selection");

        overlay._UnhandledInput(Accept());
        await ProcessFrames(3);
        Check(
            progression.SelectedRoute !=
                RunRouteStyle.Neutral &&
            ReadArenaCompleted(
                world,
                arenaIndex) &&
            ReadField<int>(
                game,
                "_activeArenaSection") == -1,
            "route choice applies the chapter and unlocks the sole exit");
        Check(
            world.ActiveChapterRouteStyle ==
                RunRouteStyle.Neutral ||
            world.FindChild(
                "ChapterRouteIdentity",
                true,
                false) != null,
            "pre-generated next chapter receives route identity immediately");

        await WaitSeconds(0.45f);
        Check(
            !GetTree().Paused &&
            !overlay.Visible,
            "the run resumes only after the route transition completes");

        game.QueueFree();
        await ProcessFrames(10);
        GD.Print(
            _failed
                ? "ARENA_REWARD_INTEGRATION: FAILED"
                : "ARENA_REWARD_INTEGRATION: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }

    private static InputEventAction Accept()
    {
        return new InputEventAction
        {
            Action = "ui_accept",
            Pressed = true
        };
    }

    private async System.Threading.Tasks.Task
        ProcessFrames(int count)
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

    private static int FindFirstArenaIndex(
        ProceduralWorld world)
    {
        foreach (WorldSectionRuntime section in
            world.GeneratedSections)
        {
            if (section.IsArena)
            {
                return section.Index;
            }
        }
        return -1;
    }

    private static bool ReadArenaCompleted(
        ProceduralWorld world,
        int arenaIndex)
    {
        foreach (WorldSectionRuntime section in
            world.GeneratedSections)
        {
            if (section.Index == arenaIndex)
            {
                return section.ArenaCompleted;
            }
        }
        return false;
    }

    private static T ReadField<T>(
        object target,
        string name)
    {
        return (T)target.GetType()
            .GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void WriteField(
        object target,
        string name,
        object value)
    {
        target.GetType()
            .GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void Invoke(
        object target,
        string name)
    {
        target.GetType()
            .GetMethod(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    private void Check(
        bool condition,
        string message)
    {
        if (condition)
        {
            GD.Print($"PASS: {message}");
            return;
        }
        _failed = true;
        GD.PushError($"FAIL: {message}");
    }
}
