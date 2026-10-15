using Godot;

public partial class RunArcProbe : Node
{
    private int _failures;

    public override async void _Ready()
    {
        ProceduralWorld world = new()
        {
            Name = "ProgressWorld",
            GenerationSettings =
                new WorldGenerationSettings
                {
                    GenerationAheadDistance =
                        180.0f,
                    RetainBehindDistance =
                        10000.0f
                }
        };
        AddChild(world);
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        world.Initialize(
            null,
            0.0f);

        RunProgressSnapshot start =
            world.GetRunProgress(0.0f, 4);
        Check(
            start.TargetArena == 1 &&
                Mathf.IsZeroApprox(start.CoreProgress),
            "the authored run begins before arena one");

        for (int ordinal = 1;
            ordinal <= 4;
            ordinal++)
        {
            bool found = world.TryGetArenaSpan(
                ordinal,
                out int sectionIndex,
                out float entryZ,
                out float exitZ);
            Check(
                found,
                $"arena {ordinal} has an exact progress anchor");
            if (!found)
            {
                continue;
            }

            RunProgressSnapshot reached =
                world.GetRunProgress(entryZ, 4);
            Check(
                reached.TargetArena == ordinal &&
                    reached.ArenaReached &&
                    Mathf.IsEqualApprox(
                        reached.CoreProgress,
                        ordinal / 4.0f),
                $"arena {ordinal} fills its physical segment exactly");
            Check(
                reached.CompletedArenas ==
                    ordinal - 1 &&
                    !reached.CoreCompleted,
                ordinal < 4
                    ? $"reaching arena {ordinal} does not count as clearing it"
                    : "reaching the fourth arena does not finish it before the clear");

            world.CompleteArena(sectionIndex);
            RunProgressSnapshot after =
                world.GetRunProgress(exitZ, 4);
            Check(
                after.CompletedArenas == ordinal,
                $"clearing arena {ordinal} advances the run milestone");
        }

        Check(
            world.CompletedArenaCount == 4,
            "the fourth clear completes the finite ascent");
        RunProgressSnapshot completed =
            world.GetRunProgress(-100000.0f, 4);
        Check(
            completed.CoreCompleted &&
                Mathf.IsEqualApprox(
                    completed.CoreProgress,
                    1.0f) &&
                completed.OverrunChapter == 1,
            "continuation begins as the first beyond chapter");

        world.SetOverrunTier(40);
        Check(
            world.OverrunTier == 40,
            "the world accepts deep-run difficulty tiers without altering milestones");

        world.QueueFree();
        GD.Print(
            _failures == 0
                ? "RUN_ARC_PROBE: PASSED"
                : $"RUN_ARC_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
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
