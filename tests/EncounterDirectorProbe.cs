using Godot;
using System;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public partial class EncounterDirectorProbe : Node
{
    private const ulong DeterministicSeed = 240629;
    private const string ExpectedFingerprint =
        "2768989D3B1EEC164DA80F2D1153037CE412A7780BB43DC641010CA8CCB448AF";
    private int _failures;

    public override async void _Ready()
    {
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);

        Main game = GetParent() as Main;
        EncounterDirector director =
            game?.GetNodeOrNull<EncounterDirector>(
                "RunSystems/EncounterDirector");
        Check(
            IsInstanceValid(game) &&
            IsInstanceValid(director) &&
            director.IsInitialized,
            "production run owns an initialized EncounterDirector");
        if (!IsInstanceValid(game) ||
            !IsInstanceValid(director))
        {
            Finish();
            return;
        }

        Check(
            director.Settings?.ResourcePath ==
                "res://world/default_encounter_settings.tres" &&
            director.GetNodeOrNull<Timer>(
                "MobTimer") != null &&
            director.GetNodeOrNull<Timer>(
                "ArenaWaveTimer") != null,
            "settings and encounter timers are authored under the director");

        ulong seedBefore =
            director.PlanningSeed;
        director.Initialize(
            game.MobScene,
            game,
            game.GetNode<Player>("Player"),
            game.GetNode<Camera3D>(
                "CameraPivot/Camera3D"),
            game.GetNode<ProceduralWorld>(
                "RunSystems/ProceduralWorld"),
            game.GetNode<ScoreLabel>(
                "UserInterface/ScoreLabel"));
        Check(
            director.PlanningSeed == seedBefore,
            "repeated Initialize is idempotent");

        string[] removedMainMethods =
        {
            "ChooseMobWavePattern",
            "GetNextSpawnLane",
            "SpawnWavePattern",
            "SpawnArenaPulse",
            "OnArenaWaveTimerTimeout"
        };
        bool mainBoundaryClean = true;
        foreach (string methodName in
            removedMainMethods)
        {
            mainBoundaryClean &=
                typeof(Main).GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic) == null;
        }
        Check(
            mainBoundaryClean,
            "Main no longer owns encounter policy or physical spawn methods");

        EncounterSettings settings = new();
        string first = BuildFingerprint(
            new EncounterPlanner(
                settings,
                DeterministicSeed));
        string second = BuildFingerprint(
            new EncounterPlanner(
                settings,
                DeterministicSeed));
        string different = BuildFingerprint(
            new EncounterPlanner(
                settings,
                DeterministicSeed + 1));
        GD.Print(
            $"ENCOUNTER_FINGERPRINT: {first}");
        Check(
            first == second &&
            first != different,
            "equal seeds reproduce the same plan and different seeds diverge");
        Check(
            first == ExpectedFingerprint,
            "planning version and deterministic fingerprint are pinned");

        EncounterPlanner lanePlanner = new(
            settings,
            DeterministicSeed);
        int previousLane = -1;
        bool laneDeckValid = true;
        for (int deck = 0; deck < 3; deck++)
        {
            bool[] found = new bool[
                EncounterPlanner.SpawnLaneCount];
            for (int index = 0;
                index < found.Length;
                index++)
            {
                int lane =
                    lanePlanner.GetNextSpawnLane();
                laneDeckValid &=
                    lane >= 0 &&
                    lane < found.Length &&
                    !found[lane] &&
                    lane != previousLane;
                if (lane >= 0 &&
                    lane < found.Length)
                {
                    found[lane] = true;
                }
                previousLane = lane;
            }
        }
        Check(
            laneDeckValid,
            "spawn lane decks cover every lane without boundary repeats");

        ProceduralWorld world =
            game.GetNode<ProceduralWorld>(
                "RunSystems/ProceduralWorld");
        bool foundArena =
            world.TryGetArenaSpan(
                1,
                out int arenaSection,
                out _,
                out _);
        Player player =
            game.GetNode<Player>("Player");
        player.SetPhysicsProcess(false);
        game.GetNode<AbyssPressureFront>(
                "RunSystems/AbyssPressureFront")
            .Stop();
        bool waveStarted = false;
        director.ArenaWaveStarted +=
            _ => waveStarted = true;
        Invoke(
            game,
            "OnArenaStarted",
            arenaSection,
            1);
        Timer arenaTimer =
            director.GetNode<Timer>(
                "ArenaWaveTimer");
        Check(
            !arenaTimer.IsStopped() &&
            arenaTimer.TimeLeft > 0.0,
            "the arena entry arms the authored timer");
        await ToSignal(
            GetTree().CreateTimer(0.8f),
            SceneTreeTimer.SignalName.Timeout);
        int encounterMobCount = 0;
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is Mob mob &&
                IsInstanceValid(mob) &&
                !mob.IsQueuedForDeletion() &&
                mob.EncounterId ==
                    director.ActiveEncounterId)
            {
                encounterMobCount++;
            }
        }
        Check(
            foundArena,
            "the production world exposes an arena for the encounter probe");
        Check(
            director.ActiveArenaSection ==
                arenaSection,
            "the production arena entry activates the director");
        Check(
            waveStarted,
            "the authored arena timer starts the first wave");
        Check(
            encounterMobCount > 0,
            "the arena pulse creates encounter-owned mobs");
        director.StopAll();

        Finish();
    }

    private static object Invoke(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method =
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        return method?.Invoke(
            target,
            arguments);
    }

    private static string BuildFingerprint(
        EncounterPlanner planner)
    {
        StringBuilder value = new();
        value.Append(
            EncounterPlanner.PlanningVersion);
        int regularWaves = 0;
        for (int index = 0;
            index < 14;
            index++)
        {
            RunRouteStyle route =
                (RunRouteStyle)(index % 4);
            MobWavePattern pattern =
                planner.ChooseMobWavePattern(
                    40 + index * 17,
                    route);
            int count =
                planner.GetWavePatternCount(
                    pattern,
                    route,
                    index / 5);
            int lane =
                planner.GetNextSpawnLane();
            float delay =
                planner.GetNextWaveDelay(
                    ref regularWaves,
                    index,
                    3 + index,
                    route,
                    index / 5);
            regularWaves++;
            value.Append('|')
                .Append((int)pattern)
                .Append(':')
                .Append(count)
                .Append('@')
                .Append(lane)
                .Append('/')
                .Append(
                    delay.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
        }

        ArenaTacticalRead tactical = new(
            0.58f,
            0.72f,
            0.34f,
            -0.4f);
        for (int wave = 0;
            wave < 4;
            wave++)
        {
            ArenaWavePlan plan =
                planner.CreateArenaWavePlan(
                    wave,
                    4,
                    1,
                    new Vector3(2.0f, 0.0f, 3.0f),
                    new Vector3(7.0f, 0.0f, -11.0f),
                    Vector3.Zero,
                    tactical);
            value.Append("|A")
                .Append((int)plan.Style)
                .Append(':')
                .Append(plan.EnemyBudget)
                .Append('/')
                .Append(plan.PulseCount)
                .Append('@')
                .Append(
                    plan.TurnSign.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(
            value.ToString());
        return Convert.ToHexString(
            SHA256.HashData(bytes));
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

    private void Finish()
    {
        GD.Print(
            _failures == 0
                ? "ENCOUNTER_DIRECTOR_PROBE: PASSED"
                : $"ENCOUNTER_DIRECTOR_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }
}
