using Godot;
using System.Collections.Generic;

public partial class WorldGenerationSettingsProbe : Node
{
    private const string SettingsPath =
        "res://world/default_world_generation_settings.tres";
    private const ulong DeterministicSeed = 240629;

    private sealed class GenerationFingerprint
    {
        public readonly List<bool> Found = new();
        public readonly List<int> SectionIndices = new();
        public readonly List<float> EntryPositions = new();
        public readonly List<float> ExitPositions = new();
        public uint NextRandom;
    }

    private bool _failed;

    public override void _Ready()
    {
        WorldGenerationSettings settings =
            GD.Load<WorldGenerationSettings>(
                SettingsPath);
        Check(
            IsInstanceValid(settings),
            "production world settings load as their typed resource");
        if (!IsInstanceValid(settings))
        {
            Finish();
            return;
        }

        Check(
            Mathf.IsEqualApprox(
                settings.MinTravelLength,
                132.0f) &&
            Mathf.IsEqualApprox(
                settings.MaxTravelLength,
                168.0f) &&
            Mathf.IsEqualApprox(
                settings.ArenaLength,
                78.0f) &&
            Mathf.IsEqualApprox(settings.MinimumBiomeTravelDistance, 600.0f) &&
            Mathf.IsEqualApprox(
                settings.TravelSectionWidth,
                36.0f) &&
            Mathf.IsEqualApprox(
                settings.GenerationAheadDistance,
                620.0f) &&
            Mathf.IsEqualApprox(
                settings.RetainBehindDistance,
                280.0f),
            "production settings preserve the previous Main/world values");

        PackedScene mainScene =
            GD.Load<PackedScene>(
                "res://main.tscn");
        Node mainInstance =
            mainScene?.Instantiate();
        ProceduralWorld sceneWorld =
            mainInstance?.GetNodeOrNull<ProceduralWorld>(
                "RunSystems/ProceduralWorld");
        Check(
            sceneWorld?.GenerationSettings?.ResourcePath ==
                SettingsPath,
            "main scene assigns the production settings to its world");
        mainInstance?.Free();

        GenerationFingerprint first =
            CaptureFingerprint(
                settings,
                "SettingsWorldA");
        GenerationFingerprint second =
            CaptureFingerprint(
                settings,
                "SettingsWorldB");
        Check(
            FingerprintsMatch(
                first,
                second),
            "equal settings and seed preserve the generation sequence");

        Finish();
    }

    private GenerationFingerprint CaptureFingerprint(
        WorldGenerationSettings settings,
        string worldName)
    {
        ProceduralWorld world = new()
        {
            Name = worldName,
            GenerationSettings = settings
        };
        AddChild(world);

        GD.Seed(DeterministicSeed);
        world.Initialize(
            null,
            0.0f);

        GenerationFingerprint fingerprint =
            new();
        for (int ordinal = 1;
            ordinal <= 4;
            ordinal++)
        {
            bool found =
                world.TryGetArenaSpan(
                    ordinal,
                    out int sectionIndex,
                    out float entryZ,
                    out float exitZ);
            fingerprint.Found.Add(found);
            fingerprint.SectionIndices.Add(
                sectionIndex);
            fingerprint.EntryPositions.Add(
                entryZ);
            fingerprint.ExitPositions.Add(
                exitZ);
        }
        fingerprint.NextRandom =
            GD.Randi();
        return fingerprint;
    }

    private static bool FingerprintsMatch(
        GenerationFingerprint first,
        GenerationFingerprint second)
    {
        if (first.NextRandom !=
                second.NextRandom ||
            first.Found.Count !=
                second.Found.Count)
        {
            return false;
        }

        for (int index = 0;
            index < first.Found.Count;
            index++)
        {
            if (first.Found[index] !=
                    second.Found[index] ||
                first.SectionIndices[index] !=
                    second.SectionIndices[index] ||
                !Mathf.IsEqualApprox(
                    first.EntryPositions[index],
                    second.EntryPositions[index]) ||
                !Mathf.IsEqualApprox(
                    first.ExitPositions[index],
                    second.ExitPositions[index]))
            {
                return false;
            }
        }

        return true;
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

    private void Finish()
    {
        GD.Print(
            _failed
                ? "WORLD_GENERATION_SETTINGS_PROBE: FAILED"
                : "WORLD_GENERATION_SETTINGS_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }
}
