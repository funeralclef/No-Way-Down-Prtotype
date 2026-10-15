using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HybridWorldAssemblerProbe : Node
{
    private const ulong HybridSeed = 240629UL;
    private const ulong LegacySeed = 73013UL;
    private const string CatalogPath =
        "res://world/content/reference_catalog.tres";

    private sealed class WorldCapture
    {
        public ProceduralWorld World;
        public List<WorldSectionRuntime> Authored;
        public int ArenaThreeIndex;
        public float ArenaThreeEntry;
        public float ArenaThreeExit;
    }

    private int _failures;

    public override async void _Ready()
    {
        WorldContentCatalog catalog =
            GD.Load<WorldContentCatalog>(
                CatalogPath);
        Check(
            catalog != null &&
            catalog.Validate(out _),
            "production hybrid catalog is valid");
        if (catalog == null)
        {
            Finish();
            return;
        }

        TestPlanner(catalog);
        WorldCapture first = CaptureHybrid(
            catalog,
            "HybridWorldA");
        WorldCapture second = CaptureHybrid(
            catalog,
            "HybridWorldB");
        TestHybridAssembly(first);
        TestDeterminism(first, second);
        TestLegacyComparison(catalog, first);
        TestStreamingLifetime();

        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        Finish();
    }

    private void TestPlanner(
        WorldContentCatalog catalog)
    {
        HybridWorldPlanner planner = new();
        bool firstOk =
            planner.TryPlanVerticalSlice(
                catalog,
                HybridSeed,
                2,
                3.0f,
                out HybridWorldPlan first,
                out _);
        bool secondOk =
            planner.TryPlanVerticalSlice(
                catalog,
                HybridSeed,
                2,
                3.0f,
                out HybridWorldPlan second,
                out _);
        bool differentOk =
            planner.TryPlanVerticalSlice(
                catalog,
                HybridSeed + 1,
                2,
                3.0f,
                out HybridWorldPlan different,
                out _);
        string recipe = firstOk
            ? string.Join(
                ">",
                first.Entries.Select(
                    entry =>
                        entry.Definition.ContentId))
            : "";
        Check(
            firstOk &&
            secondOk &&
            differentOk &&
            first.Version == 1 &&
            first.Fingerprint ==
                second.Fingerprint &&
            first.Fingerprint !=
                different.Fingerprint,
            "planner exposes a versioned deterministic fingerprint");
        Check(
            recipe ==
                "reference_travel>" +
                "reference_connector>" +
                "reference_arena>" +
                "reference_exit_connector" &&
            ChainIsCompatible(
                first.Entries,
                2),
            "planner selects a socket-compatible travel-to-exit recipe");
    }

    private WorldCapture CaptureHybrid(
        WorldContentCatalog catalog,
        string name)
    {
        ProceduralWorld world = new()
        {
            Name = name,
            GenerationSettings =
                CreateProbeSettings(),
            GenerationPipeline =
                WorldGenerationPipeline.HybridV1,
            ContentCatalog = catalog,
            HybridSliceArenaOrdinal = 2
        };
        AddChild(world);
        GD.Seed(LegacySeed);
        world.Initialize(
            null,
            0.0f,
            HybridSeed);
        bool arenaFound =
            world.TryGetArenaSpan(
                3,
                out int sectionIndex,
                out float entryZ,
                out float exitZ);
        Check(
            arenaFound,
            $"{name} reaches legacy content after the authored slice");
        return new WorldCapture
        {
            World = world,
            Authored = world.GeneratedSections
                .Where(
                    section =>
                        section.IsAuthored)
                .ToList(),
            ArenaThreeIndex = sectionIndex,
            ArenaThreeEntry = entryZ,
            ArenaThreeExit = exitZ
        };
    }

    private void TestHybridAssembly(
        WorldCapture capture)
    {
        string recipe = string.Join(
            ">",
            capture.Authored.Select(
                section =>
                    section.ContentId));
        Check(
            capture.World.GeneratorVersion == 1 &&
            capture.World.HybridSliceComplete &&
            capture.Authored.Count == 4 &&
            recipe ==
                "reference_travel>" +
                "reference_connector>" +
                "reference_arena>" +
                "reference_exit_connector",
            "production world completes one mixed authored vertical slice");

        bool seamsAlign = true;
        for (int index = 1;
            index < capture.Authored.Count;
            index++)
        {
            WorldChunk previous =
                capture.Authored[index - 1]
                    .Root as WorldChunk;
            WorldChunk current =
                capture.Authored[index]
                    .Root as WorldChunk;
            seamsAlign &=
                previous != null &&
                current != null &&
                previous.Exit.GlobalPosition
                    .DistanceTo(
                        current.Entry
                            .GlobalPosition) <=
                    0.01f &&
                capture.Authored[index - 1]
                    .NorthZ <
                    capture.Authored[index - 1]
                        .SouthZ &&
                Mathf.IsEqualApprox(
                    capture.Authored[index - 1]
                        .NorthZ,
                    capture.Authored[index]
                        .SouthZ);
        }
        Check(
            seamsAlign,
            "authored sockets align with no gap or section overlap");

        int obstacleCount = 0;
        bool spawnAreasReachable = true;
        bool routeWidthsValid = true;
        foreach (WorldSectionRuntime section in
            capture.Authored)
        {
            WorldChunk chunk =
                section.Root as WorldChunk;
            routeWidthsValid &=
                chunk != null &&
                section.Width >= 18.0f &&
                Mathf.IsEqualApprox(
                    section.Width,
                    chunk.PlayableSize.X);
            if (chunk == null)
            {
                continue;
            }
            obstacleCount +=
                CountDescendants<WorldObstacle>(
                    chunk);
            foreach (Node node in
                chunk.EnemySpawnAreas
                    .GetChildren())
            {
                if (node is not Marker3D marker)
                {
                    spawnAreasReachable = false;
                    continue;
                }
                Vector3 local =
                    chunk.ToLocal(
                        marker.GlobalPosition);
                spawnAreasReachable &=
                    Mathf.Abs(local.X) <=
                        chunk.PlayableSize.X *
                            0.5f &&
                    Mathf.Abs(local.Z) <=
                        chunk.PlayableSize.Z *
                            0.5f;
            }
        }
        Check(
            obstacleCount >= 1 &&
            routeWidthsValid &&
            spawnAreasReachable,
            "seeded obstacle slots fit valid routes and authored spawn areas stay reachable");

        bool mixed =
            capture.World.GeneratedSections.Any(
                section =>
                    section.IsAuthored) &&
            capture.World.GeneratedSections.Any(
                section =>
                    !section.IsAuthored);
        Check(
            mixed &&
            capture.World.GeneratedSections
                .All(
                    section =>
                        section.SouthZ >
                        section.NorthZ),
            "authored and legacy sections coexist under one runtime contract");
    }

    private void TestDeterminism(
        WorldCapture first,
        WorldCapture second)
    {
        bool spansMatch =
            first.ArenaThreeIndex ==
                second.ArenaThreeIndex &&
            Mathf.IsEqualApprox(
                first.ArenaThreeEntry,
                second.ArenaThreeEntry) &&
            Mathf.IsEqualApprox(
                first.ArenaThreeExit,
                second.ArenaThreeExit);
        Check(
            !string.IsNullOrWhiteSpace(
                first.World
                    .HybridRuntimeFingerprint) &&
            first.World.HybridPlanFingerprint ==
                second.World
                    .HybridPlanFingerprint &&
            first.World.HybridRuntimeFingerprint ==
                second.World
                    .HybridRuntimeFingerprint &&
            spansMatch,
            "equal legacy and hybrid seeds reproduce the same runtime result");
    }

    private void TestLegacyComparison(
        WorldContentCatalog catalog,
        WorldCapture hybrid)
    {
        ProceduralWorld legacy = new()
        {
            Name = "LegacyWorld",
            GenerationSettings =
                CreateProbeSettings(),
            GenerationPipeline =
                WorldGenerationPipeline.Legacy,
            ContentCatalog = catalog
        };
        AddChild(legacy);
        GD.Seed(LegacySeed);
        legacy.Initialize(
            null,
            0.0f);
        bool legacyArena =
            legacy.TryGetArenaSpan(
                3,
                out int legacyIndex,
                out float legacyEntry,
                out float legacyExit);
        Check(
            legacyArena &&
            legacyIndex >= 0 &&
            legacyEntry > legacyExit &&
            hybrid.ArenaThreeIndex >= 0 &&
            hybrid.ArenaThreeEntry >
                hybrid.ArenaThreeExit &&
            legacy.GeneratedSections.All(
                section =>
                    !section.IsAuthored),
            "one probe compares valid legacy and hybrid pipelines through the same arena API");
    }

    private void TestStreamingLifetime()
    {
        WorldSectionStream stream = new();
        Node3D behind = new()
        {
            Name = "BehindSection"
        };
        Node3D ahead = new()
        {
            Name = "AheadSection"
        };
        AddChild(behind);
        AddChild(ahead);
        stream.Add(
            new WorldSectionRuntime
            {
                Index = 1,
                SouthZ = 20.0f,
                NorthZ = 0.0f,
                Root = behind
            });
        stream.Add(
            new WorldSectionRuntime
            {
                Index = 2,
                SouthZ = -220.0f,
                NorthZ = -260.0f,
                Root = ahead
            });
        int removed = stream.PruneBehind(
            -200.0f,
            50.0f,
            null,
            -1);
        Check(
            removed == 1 &&
            stream.Count == 1 &&
            behind.IsQueuedForDeletion() &&
            !ahead.IsQueuedForDeletion(),
            "streaming lifetime releases only sections beyond retain-behind");
    }

    private static WorldGenerationSettings
        CreateProbeSettings()
    {
        return new WorldGenerationSettings
        {
            MinTravelLength = 80.0f,
            MaxTravelLength = 88.0f,
            ArenaLength = 78.0f,
            TravelSectionWidth = 36.0f,
            GenerationAheadDistance = 180.0f,
            RetainBehindDistance = 120.0f
        };
    }

    private static bool ChainIsCompatible(
        IReadOnlyList<HybridWorldPlanEntry>
            entries,
        int chapter)
    {
        for (int index = 1;
            index < entries.Count;
            index++)
        {
            if (!entries[index - 1]
                .Definition.CanPrecede(
                    entries[index]
                        .Definition,
                    chapter))
            {
                return false;
            }
        }
        return true;
    }

    private static int CountDescendants<T>(
        Node root)
        where T : Node
    {
        int count = root is T ? 1 : 0;
        foreach (Node child in root.GetChildren())
        {
            count += CountDescendants<T>(child);
        }
        return count;
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
                ? "HYBRID_WORLD_ASSEMBLER_PROBE: PASSED"
                : $"HYBRID_WORLD_ASSEMBLER_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }
}
