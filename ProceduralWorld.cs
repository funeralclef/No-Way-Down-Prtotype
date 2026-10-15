using Godot;
using System.Collections.Generic;

public partial class ProceduralWorld : Node3D
{
    private const uint ObstacleCollisionLayer = 1u << 3;
    private const float WaterRelayGapDepth = 27.0f;
    private const float WaterRelayPrewarmLead = 25.0f;

    private enum ObstacleType
    {
        JumpHurdle,
        FlowRail,
        Pillar,
        RocketRamp,
        VectorJumpPad,
        KineticJumpPad
    }

    private enum ConstructionEpisode
    {
        RhythmHurdles,
        SpiralChannel,
        ReactorShaft,
        SwitchbackCanyon,
        KineticSluice,
        PulseCascade,
        SkySurf,
        Crosswind,
        CrawlingConvoy,
        DemolitionCorridor,
        PendulumSupports,
        TunnelJunction,
        LivingRift
    }

    [Signal]
    public delegate void SectionEnteredEventHandler(string title, bool isArena, int sectionIndex);

    [Signal]
    public delegate void ArenaStartedEventHandler(int sectionIndex, int waveCount);

    [Signal]
    public delegate void TraversalEnemyRequestedEventHandler(
        Vector3 worldPosition,
        Vector3 courseDirection,
        int relayIndex);

    [Signal]
    public delegate void ConstructionEnemyRequestedEventHandler(
        Vector3 worldPosition,
        Vector3 courseDirection,
        int relayIndex,
        int archetype);

    private static readonly string[] ThemeNames =
    {
        "DUSTBOUND",
        "VERDANT",
        "FROSTFALL",
        "RAINLINE",
        "CINDER",
        "SKYGLASS"
    };

    private static readonly string[] PlaceNames =
    {
        "RUN",
        "TRAIL",
        "RIDGE",
        "PASSAGE",
        "REACH",
        "FIELDS"
    };

    private static readonly Color[] ThemeColors =
    {
        new(0.94f, 0.28f, 0.12f),
        new(0.08f, 0.64f, 0.5f),
        new(0.08f, 0.48f, 0.82f),
        new(0.28f, 0.38f, 0.68f),
        new(0.9f, 0.18f, 0.1f),
        new(0.04f, 0.62f, 0.76f)
    };
    private const string RunSurfaceShaderPath =
        "res://shaders/run_surface.gdshader";
    private static Shader _runSurfaceShader;
    private static readonly Dictionary<ulong, ShaderMaterial>
        RunSurfaceMaterialCache = new();
    private static readonly Dictionary<ulong, StandardMaterial3D>
        StructureMaterialCache = new();
    private static readonly Dictionary<ulong, StandardMaterial3D>
        ObstacleMaterialCache = new();
    private static ShaderMaterial _relayWaterMaterial;

    [Export]
    public WorldGenerationSettings GenerationSettings { get; set; } =
        new();

    [ExportGroup("Hybrid world")]
    [Export]
    public WorldGenerationPipeline GenerationPipeline { get; set; } =
        WorldGenerationPipeline.Legacy;

    [Export]
    public WorldContentCatalog ContentCatalog { get; set; }

    [Export(PropertyHint.Range, "2,20,1")]
    public int HybridSliceArenaOrdinal { get; set; } = 2;

    public Player ConstructionPlayer { get; set; }
    public float RunStartZ { get; private set; }
    public ulong GenerationSeed { get; private set; }
    public int GeneratorVersion =>
        GenerationPipeline ==
            WorldGenerationPipeline.HybridV1
            ? HybridWorldPlan.CurrentVersion
            : 0;
    public string HybridPlanFingerprint { get; private set; } = "";
    public string HybridRuntimeFingerprint { get; private set; } = "";
    public bool HybridSliceComplete { get; private set; }
    public IReadOnlyList<WorldSectionRuntime>
        GeneratedSections =>
        _sectionStream.Sections;

    public int CurrentSectionIndex { get; private set; } = -1;
    public int CurrentBiomeId { get; private set; } = -1;
    public int CompletedArenaCount { get; private set; }
    public int OverrunTier { get; private set; }
    public bool ArenaEncounterActive => _activeArena != null;
    public Color CurrentSurfaceColor { get; private set; } =
        new(0.12f, 0.16f, 0.24f);

    private readonly WorldSectionStream _sectionStream =
        new();
    private WorldSectionRuntime _activeArena;
    private readonly HybridWorldPlanner _hybridPlanner =
        new();
    private readonly HybridWorldAssembler _hybridAssembler =
        new();
    private readonly List<string>
        _hybridAssemblyFingerprints = new();
    private HybridWorldPlan _hybridPlan;
    private int _hybridPlanIndex;
    private bool _hybridPipelineReady;
    private bool _hybridSliceStarted;
    private Transform3D _hybridNextSocket;
    private float _travelDistanceSinceArena;
    private float _minimumBiomeTravelDistance = 440;
    private float _hybridApproachDistance;
    private float _nextSectionSouthZ = 48.0f;
    private int _nextSectionIndex;
    private int _regularSectionsSinceArena;
    private int _regularSectionsUntilArena;
    private int _regularSectionsSinceSetPiece;
    private int _regularSectionsUntilSetPiece;
    private bool _nextSetPieceIsWater = true;
    private int _gentleCourseCount;
    private int _supportStructureCount;
    private int _arenaSequence;
    private int _generatedArenaCount;
    private float _lastCompletedArenaNorthZ;
    private readonly List<ConstructionEpisode>
        _constructionDeck = new();
    private ConstructionEpisode? _lastConstructionEpisode;
    private RunRouteStyle _chapterRouteStyle =
        RunRouteStyle.Neutral;
    private bool _chapterRouteActive;
    private float _minTravelLength = 92.0f;
    private float _maxTravelLength = 118.0f;
    private float _arenaLength = 78.0f;
    // At the normal running camera size this is exactly the horizontal span
    // of a 4:3 frame. Wider displays reveal only the sky beyond these edges.
    private float _travelSectionWidth = 36.0f;
    private float _generationAheadDistance = 620.0f;
    private float _retainBehindDistance = 280.0f;
    private bool _generationSettingsApplied;
    private bool _initialized;

    public RunRouteStyle ActiveChapterRouteStyle =>
        _chapterRouteActive
            ? _chapterRouteStyle
            : RunRouteStyle.Neutral;

    public override void _Ready()
    {
        AddToGroup("procedural_world");
    }

    /// <summary>
    /// Supplies the run-specific dependencies before any procedural content is
    /// generated. The scene owns this node's lifetime; Main only configures it.
    /// </summary>
    public void Initialize(
        Player constructionPlayer,
        float runStartZ,
        ulong generationSeed = 0)
    {
        if (_initialized)
        {
            return;
        }

        ApplyGenerationSettings();
        ConstructionPlayer = constructionPlayer;
        RunStartZ = runStartZ;
        PrepareHybridPipeline(
            generationSeed);
        _lastCompletedArenaNorthZ = RunStartZ;
        _regularSectionsUntilArena = GD.RandRange(4, 6);
        _regularSectionsUntilSetPiece = GD.RandRange(2, 3);
        EnsureGeneratedThrough(-_generationAheadDistance);
        EnsureArenaOrdinalGenerated(1);
        _initialized = true;
    }

    private void PrepareHybridPipeline(
        ulong requestedSeed)
    {
        if (GenerationPipeline !=
            WorldGenerationPipeline.HybridV1)
        {
            return;
        }

        if (requestedSeed == 0)
        {
            RandomNumberGenerator seedSource =
                new();
            seedSource.Randomize();
            GenerationSeed =
                seedSource.Randi();
        }
        else
        {
            GenerationSeed = requestedSeed;
        }
        int chapter = Mathf.Max(
            1,
            HybridSliceArenaOrdinal);
        float maximumDifficulty =
            1.5f + chapter * 0.5f;
        _hybridPipelineReady =
            _hybridPlanner.TryPlanVerticalSlice(
                ContentCatalog,
                GenerationSeed,
                chapter,
                maximumDifficulty,
                out _hybridPlan,
                out string error);
        if (_hybridPipelineReady)
        {
            _hybridPipelineReady =
                _hybridAssembler.TryValidatePlan(
                    this,
                    _hybridPlan,
                    ContentCatalog.Obstacles,
                    maximumDifficulty,
                    out error);
        }
        if (!_hybridPipelineReady)
        {
            GD.PushWarning(
                "Hybrid world pipeline disabled: " +
                error);
            return;
        }

        // Measure the authored approach from its public sockets instead of assuming
        // that a travel scene is as long as a procedural travel section.
        foreach (var entry in _hybridPlan.Entries)
        {
            if (entry.Definition.Kind == WorldChunkKind.Arena) break;
            var chunk = entry.Definition.Scene.Instantiate<WorldChunk>();
            _hybridApproachDistance += chunk.Entry.Position.Z - chunk.Exit.Position.Z;
            chunk.Free();
        }
        HybridPlanFingerprint =
            _hybridPlan.Fingerprint;
        HybridRuntimeFingerprint =
            WorldGenerationFingerprint.Compute(
                HybridPlanFingerprint);
    }

    private void ApplyGenerationSettings()
    {
        if (_generationSettingsApplied)
        {
            return;
        }

        GenerationSettings ??=
            new WorldGenerationSettings();
        _minimumBiomeTravelDistance = Mathf.Max(300, GenerationSettings.MinimumBiomeTravelDistance);
        _minTravelLength =
            GenerationSettings.MinTravelLength;
        _maxTravelLength =
            GenerationSettings.MaxTravelLength;
        _arenaLength =
            GenerationSettings.ArenaLength;
        _travelSectionWidth =
            GenerationSettings.TravelSectionWidth;
        _generationAheadDistance =
            GenerationSettings.GenerationAheadDistance;
        _retainBehindDistance =
            GenerationSettings.RetainBehindDistance;
        _generationSettingsApplied = true;
    }

    public void UpdateForPlayer(Player player)
    {
        if (!IsInstanceValid(player))
        {
            return;
        }

        ConstrainPlayerToActiveArena(player);
        EnsureGeneratedThrough(
            player.Position.Z - _generationAheadDistance);
        WorldSectionRuntime currentSection =
            FindSection(player.Position.Z);
        if (currentSection != null && currentSection.Index != CurrentSectionIndex)
        {
            CurrentSectionIndex = currentSection.Index;
            CurrentBiomeId = currentSection.BiomeId;
            CurrentSurfaceColor =
                currentSection.ThemeColor;
            EmitSignal(
                SignalName.SectionEntered,
                currentSection.Title,
                currentSection.IsArena,
                currentSection.Index);
        }

        if (currentSection is { IsArena: true, ArenaStarted: false, ArenaCompleted: false } &&
            (currentSection.ArenaLayout == null || currentSection.ArenaLayout.Contains(
                new Vector2(player.GlobalPosition.X - currentSection.Root.GlobalPosition.X,
                    player.GlobalPosition.Z - currentSection.Root.GlobalPosition.Z), 1.5f)))
        {
            currentSection.ArenaStarted = true;
            _activeArena = currentSection;
            int maximumWaves = OverrunTier > 0 ? 8 : 6;
            int waveCount = Mathf.Min(
                maximumWaves,
                GD.RandRange(4, 5) +
                    _arenaSequence / 2 +
                    Mathf.Min(2, OverrunTier / 2));
            _arenaSequence++;
            EmitSignal(SignalName.ArenaStarted, currentSection.Index, waveCount);
        }

        RemoveSectionsFarBehind(player.Position.Z);
    }

    private void ConstrainPlayerToActiveArena(Player player)
    {
        if (_activeArena == null || _activeArena.ArenaCompleted)
        {
            return;
        }

        Vector3 position = player.GlobalPosition;
        Vector3 velocity = player.Velocity;
        if (_activeArena.ArenaLayout != null)
        {
            Vector3 clamped = _activeArena.ArenaLayout.ClampWorld(position, _activeArena.Root.GlobalPosition, 1.3f);
            Vector3 outward = position - clamped; outward.Y = 0;
            if (outward.LengthSquared() > .001f)
            {
                Vector3 normal = outward.Normalized();
                velocity -= normal * Mathf.Max(0, velocity.Dot(normal));
                player.GlobalPosition = clamped; player.Velocity = velocity;
            }
            return;
        }
        if (position.Z < _activeArena.NorthZ)
        {
            position.Z = _activeArena.NorthZ;
            velocity.Z = Mathf.Max(0.0f, velocity.Z);
            player.GlobalPosition = position;
            player.Velocity = velocity;
        }
    }

    public void CompleteArena(int sectionIndex)
    {
        WorldSectionRuntime arena =
            FindSectionByIndex(sectionIndex);
        if (arena == null || !arena.IsArena || arena.ArenaCompleted)
        {
            return;
        }

        arena.ArenaCompleted = true;
        CompletedArenaCount = Mathf.Max(
            CompletedArenaCount,
            arena.ArenaOrdinal);
        _lastCompletedArenaNorthZ = arena.NorthZ;
        if (_activeArena == arena)
        {
            _activeArena = null;
        }
        EnsureArenaOrdinalGenerated(
            CompletedArenaCount + 1);
    }

    public void SetOverrunTier(int tier)
    {
        OverrunTier = Mathf.Max(0, tier);
    }

    public int GetArenaOrdinal(int sectionIndex)
    {
        WorldSectionRuntime section =
            FindSectionByIndex(sectionIndex);
        return section is { IsArena: true }
            ? section.ArenaOrdinal
            : 0;
    }

    public bool TryGetArenaSpan(
        int arenaOrdinal,
        out int sectionIndex,
        out float entryZ,
        out float exitZ)
    {
        EnsureArenaOrdinalGenerated(
            arenaOrdinal);
        WorldSectionRuntime arena =
            FindArenaByOrdinal(
                arenaOrdinal);
        if (arena == null)
        {
            sectionIndex = -1;
            entryZ = 0.0f;
            exitZ = 0.0f;
            return false;
        }

        sectionIndex = arena.Index;
        entryZ = arena.SouthZ;
        exitZ = arena.NorthZ;
        return true;
    }

    /// <summary>
    /// Maps the player's real Z coordinate to the current arena-to-arena
    /// chapter. Only the next arena is pre-generated, keeping this exact
    /// without constructing the whole run at startup.
    /// </summary>
    public RunProgressSnapshot GetRunProgress(
        float playerZ,
        int finalArenaCount = 4,
        bool ensureTargetGenerated = true)
    {
        int coreArenas = Mathf.Max(1, finalArenaCount);
        int targetOrdinal =
            CompletedArenaCount + 1;
        if (ensureTargetGenerated)
        {
            EnsureArenaOrdinalGenerated(targetOrdinal);
        }
        WorldSectionRuntime target =
            FindArenaByOrdinal(targetOrdinal);
        float segmentStart =
            CompletedArenaCount <= 0
                ? RunStartZ
                : _lastCompletedArenaNorthZ;
        float segmentEnd =
            target?.SouthZ ?? segmentStart - 1.0f;
        float denominator =
            segmentEnd - segmentStart;
        float segmentProgress =
            target == null
                ? 0.0f
                : Mathf.Abs(denominator) <= 0.001f
                ? 0.0f
                : Mathf.Clamp(
                    (playerZ - segmentStart) /
                        denominator,
                    0.0f,
                    1.0f);
        bool arenaReached =
            target != null &&
            playerZ <= target.SouthZ;
        if (arenaReached)
        {
            segmentProgress = 1.0f;
        }

        bool coreCompleted =
            CompletedArenaCount >= coreArenas;
        float coreProgress = coreCompleted
            ? 1.0f
            : Mathf.Clamp(
                (CompletedArenaCount +
                    segmentProgress) /
                    coreArenas,
                0.0f,
                1.0f);
        int overrunChapter = coreCompleted
            ? Mathf.Max(
                1,
                CompletedArenaCount -
                    coreArenas + 1)
            : 0;

        return new RunProgressSnapshot(
            CompletedArenaCount,
            targetOrdinal,
            segmentProgress,
            coreProgress,
            coreCompleted,
            arenaReached,
            overrunChapter);
    }

    private void EnsureArenaOrdinalGenerated(
        int arenaOrdinal)
    {
        ApplyGenerationSettings();
        int target = Mathf.Max(1, arenaOrdinal);
        int safety = 0;
        while (_generatedArenaCount < target &&
            safety < 64)
        {
            GenerateNextSection();
            safety++;
        }
    }

    private WorldSectionRuntime FindArenaByOrdinal(
        int arenaOrdinal)
    {
        return _sectionStream.FindArenaByOrdinal(
            arenaOrdinal);
    }

    /// <summary>
    /// Gives the next arena-to-arena chapter a visual identity without
    /// changing the road collision or creating a physical fork. Generation is
    /// intentionally far ahead of the player, so existing sections are
    /// retrofitted first and the same identity is then used only until the
    /// next arena is generated.
    /// </summary>
    public void ApplyChapterRoute(
        RunRouteStyle routeStyle,
        int completedArenaSection)
    {
        _chapterRouteStyle = routeStyle;
        bool reachedNextArena = false;

        foreach (WorldSectionRuntime section in
            _sectionStream.Sections)
        {
            if (section.Index <=
                completedArenaSection)
            {
                continue;
            }
            if (section.IsArena)
            {
                reachedNextArena = true;
                break;
            }

            ApplyRouteIdentity(
                section,
                routeStyle);
        }

        _chapterRouteActive =
            routeStyle !=
                RunRouteStyle.Neutral &&
            !reachedNextArena;

        WorldSectionRuntime completedArena =
            FindSectionByIndex(
                completedArenaSection);
        if (completedArena is
                { IsArena: true } &&
            IsInstanceValid(
                completedArena.Root))
        {
            CreateArenaExitRouteIdentity(
                completedArena,
                routeStyle);
        }
    }

    private void EnsureGeneratedThrough(
        float targetNorthZ)
    {
        ApplyGenerationSettings();
        while (_nextSectionSouthZ > targetNorthZ)
        {
            GenerateNextSection();
        }
    }

    private void GenerateNextSection()
    {
        if (TryGenerateNextHybridSection())
        {
            return;
        }

        GenerateNextLegacySection();
    }

    private bool TryGenerateNextHybridSection()
    {
        if (!_hybridPipelineReady ||
            HybridSliceComplete ||
            _hybridPlan == null)
        {
            return false;
        }

        int targetArena = Mathf.Max(
            2,
            HybridSliceArenaOrdinal);
        if (!_hybridSliceStarted)
        {
            if (_generatedArenaCount != targetArena - 1 ||
                _travelDistanceSinceArena + _hybridApproachDistance < _minimumBiomeTravelDistance)
            {
                return false;
            }

            _hybridSliceStarted = true;
            _hybridNextSocket =
                new Transform3D(
                    Basis.Identity,
                    new Vector3(
                        0.0f,
                        0.0f,
                        _nextSectionSouthZ));
        }

        if (_hybridPlanIndex >=
            _hybridPlan.Entries.Count)
        {
            HybridSliceComplete = true;
            return false;
        }

        HybridWorldPlanEntry entry =
            _hybridPlan.Entries[
                _hybridPlanIndex];
        float maximumDifficulty =
            1.5f +
            _hybridPlan.Chapter * 0.5f;
        if (!_hybridAssembler.TryAssemble(
                this,
                entry,
                _hybridNextSocket,
                ContentCatalog.Obstacles,
                GenerationSeed,
                _hybridPlan.Chapter,
                maximumDifficulty,
                _nextSectionIndex,
                out AuthoredWorldChunkAssembly
                    assembly,
                out string error))
        {
            _hybridPipelineReady = false;
            GD.PushError(
                "Hybrid section assembly failed: " +
                error);
            return false;
        }

        WorldChunk chunk = assembly.Chunk;
        bool isArena =
            chunk.Kind == WorldChunkKind.Arena;
        int arenaOrdinal = isArena
            ? ++_generatedArenaCount
            : 0;
        int themeIndex = Mathf.PosMod(_generatedArenaCount - (isArena ? 1 : 0), RunBiomeCatalog.Count);
        string baseTitle =
            GetHybridSectionTitle(
                chunk,
                themeIndex);
        string title =
            !isArena &&
            _chapterRouteActive
                ? GetRouteStyleName(
                    _chapterRouteStyle) +
                    "  /  " +
                    baseTitle
                : baseTitle;
        if (isArena)
        {
            CreateCameraArena(
                chunk,
                assembly.ArenaRadius,
                chunk.CameraViewSize);
            chunk.GetNode<CameraArena>("CameraArena").Layout = ArenaLayout.Rectangle(chunk.PlayableSize.X / 2, chunk.PlayableSize.Z / 2);
            var tactics = new ArenaTactics { Name = "ArenaTactics" }; chunk.AddChild(tactics);
            tactics.Build(chunk.GetNode<CameraArena>("CameraArena").Layout, ThemeColors[themeIndex]);
        }
        else if (_chapterRouteActive)
        {
            CreateRouteIdentityVisuals(
                chunk,
                assembly.Width,
                assembly.SouthZ -
                    assembly.NorthZ,
                _chapterRouteStyle);
        }

        RunBiomeScenery.Build(chunk, themeIndex, assembly.Width, assembly.SouthZ - assembly.NorthZ, GenerationSeed + (ulong)_nextSectionIndex * 7919);
        chunk.SetMeta(
            "generation_pipeline",
            "hybrid_v1");
        chunk.SetMeta(
            "assembly_fingerprint",
            assembly.Fingerprint);
        _sectionStream.Add(
            new WorldSectionRuntime
            {
                Index = _nextSectionIndex,
                BiomeId = themeIndex,
                ArenaOrdinal = arenaOrdinal,
                Title = title,
                BaseTitle = baseTitle,
                SouthZ = assembly.SouthZ,
                NorthZ = assembly.NorthZ,
                Width = assembly.Width,
                ArenaRadius =
                    assembly.ArenaRadius,
                ArenaLayout = isArena ? chunk.GetNode<CameraArena>("CameraArena").Layout : null,
                ThemeColor =
                    ThemeColors[themeIndex],
                IsArena = isArena,
                IsAuthored = true,
                ContentId = chunk.ContentId,
                RouteStyle =
                    !isArena &&
                    _chapterRouteActive
                        ? _chapterRouteStyle
                        : RunRouteStyle.Neutral,
                Root = chunk
            });

        _hybridAssemblyFingerprints.Add(
            assembly.Fingerprint);
        HybridRuntimeFingerprint =
            WorldGenerationFingerprint.Compute(
                HybridPlanFingerprint + "|" +
                string.Join(
                    "|",
                    _hybridAssemblyFingerprints));
        _hybridNextSocket =
            assembly.ExitTransform;
        _nextSectionSouthZ =
            assembly.NorthZ;
        _nextSectionIndex++;
        _hybridPlanIndex++;

        if (isArena)
        {
            _chapterRouteActive = false;
            _regularSectionsSinceArena = 0;
            _travelDistanceSinceArena = 0;
            ulong timing =
                WorldGenerationFingerprint
                    .ComputeValue(
                        $"{GenerationSeed}|" +
                        $"arena:{arenaOrdinal}");
            _regularSectionsUntilArena =
                4 + (int)(timing % 3UL);
        }
        else
        {
            _regularSectionsSinceArena++;
            _travelDistanceSinceArena += assembly.SouthZ - assembly.NorthZ;
            _regularSectionsSinceSetPiece++;
        }

        HybridSliceComplete =
            _hybridPlanIndex >=
                _hybridPlan.Entries.Count;
        return true;
    }

    private static string GetHybridSectionTitle(
        WorldChunk chunk,
        int themeIndex)
    {
        return chunk.Kind switch
        {
            WorldChunkKind.Arena =>
                $"{ThemeNames[themeIndex]} AUTHORED HOLD",
            WorldChunkKind.Connector =>
                $"{ThemeNames[themeIndex]} LINK",
            _ =>
                $"{ThemeNames[themeIndex]} AUTHORED RUN"
        };
    }

    private void GenerateNextLegacySection()
    {
        bool isArena = _regularSectionsSinceArena >= _regularSectionsUntilArena &&
            _travelDistanceSinceArena >= _minimumBiomeTravelDistance;
        int arenaOrdinal = isArena
            ? ++_generatedArenaCount
            : 0;
        RunRouteStyle sectionRouteStyle =
            !isArena &&
            _chapterRouteActive
                ? _chapterRouteStyle
                : RunRouteStyle.Neutral;
        float length = isArena
            ? _arenaLength
            : (float)GD.RandRange(
                _minTravelLength,
                _maxTravelLength);
        float width = isArena
            ? (float)GD.RandRange(94.0f, 102.0f)
            : _travelSectionWidth;
        float southZ = _nextSectionSouthZ;
        float northZ = southZ - length;
        float centerZ = (southZ + northZ) * 0.5f;
        int themeIndex = Mathf.PosMod(_generatedArenaCount - (isArena ? 1 : 0), RunBiomeCatalog.Count);
        string title = isArena
            ? $"{ThemeNames[themeIndex]} HOLD"
            : $"{ThemeNames[themeIndex]} {PlaceNames[GD.RandRange(0, PlaceNames.Length - 1)]}";
        bool createSetPiece =
            !isArena &&
            _nextSectionIndex > 2 &&
            _regularSectionsSinceSetPiece >=
                _regularSectionsUntilSetPiece;
        bool createWaterRelay =
            createSetPiece &&
            _nextSetPieceIsWater;
        bool createGentleCourse =
            createSetPiece &&
            !_nextSetPieceIsWater;
        if (createWaterRelay)
        {
            title =
                $"{ThemeNames[themeIndex]} BROKEN CAUSEWAY";
        }
        else if (createGentleCourse)
        {
            title = $"{ThemeNames[themeIndex]} " + (_gentleCourseCount % 2 == 0 ? "MEANDER WALK" : "ISLAND PROMENADE");
        }
        string baseTitle = title;
        if (sectionRouteStyle !=
            RunRouteStyle.Neutral)
        {
            title =
                GetRouteStyleName(
                    sectionRouteStyle) +
                "  /  " +
                baseTitle;
        }
        float arenaRadius = isArena
            ? Mathf.Min(width, length) * 0.5f - 3.0f
            : 0.0f;

        Node3D sectionRoot = new()
        {
            Name = $"Section{_nextSectionIndex}",
            Position = new Vector3(0.0f, 0.0f, centerZ)
        };
        sectionRoot.AddToGroup("world_section");
        AddChild(sectionRoot);

        if (isArena)
        {
            ArenaLayout footprint = ArenaLayout.ForOrdinal(arenaOrdinal, arenaRadius);
            ArenaGeometry.Build(sectionRoot, footprint, length, _travelSectionWidth, CreateRunSurfaceMaterial(ThemeColors[themeIndex], true), ThemeColors[themeIndex]);
            CreateCameraArena(sectionRoot, arenaRadius);
            sectionRoot.GetNode<CameraArena>("CameraArena").Layout = footprint;
            var tactics = new ArenaTactics { Name = "ArenaTactics" }; sectionRoot.AddChild(tactics); tactics.Build(footprint, ThemeColors[themeIndex]);
        }
        else
        {
            if (createWaterRelay)
            {
                CreateWaterRelayGround(
                    sectionRoot,
                    width,
                    length,
                    ThemeColors[themeIndex]);
            }
            else if (createGentleCourse)
            {
                GentleTraversalCourse.Build(sectionRoot, width, length, ThemeColors[themeIndex],
                    _gentleCourseCount++ % 2 == 1, CreateRunSurfaceMaterial(ThemeColors[themeIndex]), CreateRelayWaterMaterial());
            }
            else
            {
                CreateGround(sectionRoot, width, length, ThemeColors[themeIndex], false);
            }
            CreateEdgeAccents(
                sectionRoot,
                width,
                length,
                ThemeColors[themeIndex]);

            if (sectionRouteStyle !=
                RunRouteStyle.Neutral)
            {
                CreateRouteIdentityVisuals(
                    sectionRoot,
                    width,
                    length,
                    sectionRouteStyle);
            }

            if (createWaterRelay)
            {
                CreateWaterRelaySetPiece(
                    sectionRoot,
                    width,
                    length,
                    ThemeColors[themeIndex]);
            }
        }

        if (!isArena && !createWaterRelay && !createGentleCourse && _nextSectionIndex > 0 && _nextSectionIndex % 3 == 1)
            RunSupportStructures.Build(sectionRoot, width, _supportStructureCount++, ThemeColors[themeIndex]);

        RunBiomeScenery.Build(sectionRoot, themeIndex, width, length, GenerationSeed + (ulong)_nextSectionIndex * 7919);
        _sectionStream.Add(new WorldSectionRuntime
        {
            Index = _nextSectionIndex,
            BiomeId = themeIndex,
            ArenaOrdinal = arenaOrdinal,
            Title = title,
            BaseTitle = baseTitle,
            SouthZ = southZ,
            NorthZ = northZ,
            Width = width,
            ArenaRadius = arenaRadius,
            ArenaLayout = isArena ? ArenaLayout.ForOrdinal(arenaOrdinal, arenaRadius) : null,
            ThemeColor = ThemeColors[themeIndex],
            IsArena = isArena,
            RouteStyle = sectionRouteStyle,
            Root = sectionRoot
        });

        _nextSectionIndex++;
        _nextSectionSouthZ = northZ;
        if (isArena)
        {
            _chapterRouteActive = false;
            _regularSectionsSinceArena = 0;
            _travelDistanceSinceArena = 0;
            _regularSectionsUntilArena = GD.RandRange(4, 6);
        }
        else
        {
            _regularSectionsSinceArena++;
            _travelDistanceSinceArena += length;
            if (createSetPiece)
            {
                _regularSectionsSinceSetPiece = 0;
                _regularSectionsUntilSetPiece =
                    GD.RandRange(
                        2,
                        3);
                _nextSetPieceIsWater =
                    !_nextSetPieceIsWater;
            }
            else
            {
                _regularSectionsSinceSetPiece++;
            }
        }
    }

    private void ApplyRouteIdentity(
        WorldSectionRuntime section,
        RunRouteStyle routeStyle)
    {
        if (!IsInstanceValid(section.Root) ||
            section.IsArena)
        {
            return;
        }

        section.RouteStyle = routeStyle;
        string baseTitle =
            string.IsNullOrEmpty(
                section.BaseTitle)
                ? section.Title
                : section.BaseTitle;
        section.BaseTitle = baseTitle;
        section.Title =
            routeStyle ==
                RunRouteStyle.Neutral
                ? baseTitle
                : GetRouteStyleName(
                    routeStyle) +
                    "  /  " +
                    baseTitle;

        RemoveRouteIdentityVisuals(
            section.Root);
        if (routeStyle ==
            RunRouteStyle.Neutral)
        {
            return;
        }

        CreateRouteIdentityVisuals(
            section.Root,
            section.Width,
            Mathf.Max(
                1.0f,
                section.SouthZ -
                    section.NorthZ),
            routeStyle);
    }

    private static void RemoveRouteIdentityVisuals(
        Node3D sectionRoot)
    {
        Node existing =
            sectionRoot.GetNodeOrNull<Node>(
                "ChapterRouteIdentity");
        if (!IsInstanceValid(existing))
        {
            return;
        }

        sectionRoot.RemoveChild(existing);
        existing.QueueFree();
    }

    private static void CreateRouteIdentityVisuals(
        Node3D sectionRoot,
        float sectionWidth,
        float sectionLength,
        RunRouteStyle routeStyle)
    {
        Node3D identity = new()
        {
            Name = "ChapterRouteIdentity"
        };
        sectionRoot.AddChild(identity);

        Color routeColor =
            GetRouteStyleColor(
                routeStyle);
        StandardMaterial3D material =
            CreateRouteIdentityMaterial(
                routeColor,
                0.74f);
        StandardMaterial3D quietMaterial =
            CreateRouteIdentityMaterial(
                routeColor.Lerp(
                    Colors.White,
                    0.22f),
                0.42f);

        float segmentDepth =
            Mathf.Clamp(
                sectionLength * 0.18f,
                12.0f,
                20.0f);
        float segmentOffset =
            Mathf.Max(
                segmentDepth * 0.58f,
                sectionLength * 0.5f -
                    segmentDepth * 0.62f);
        float ribbonOffset =
            routeStyle switch
            {
                RunRouteStyle.Flow =>
                    sectionWidth * 0.22f,
                RunRouteStyle.Endure =>
                    1.6f,
                _ => 0.0f
            };
        int ribbonCount =
            routeStyle ==
                RunRouteStyle.Hunt
                ? 1
                : 2;
        for (int ribbon = 0;
            ribbon < ribbonCount;
            ribbon++)
        {
            float x = ribbonCount == 1
                ? 0.0f
                : (ribbon == 0
                    ? -ribbonOffset
                    : ribbonOffset);
            foreach (float z in new[]
                { -segmentOffset, segmentOffset })
            {
                AddVisualBox(
                    identity,
                    new Vector3(
                        x,
                        0.072f,
                        z),
                    new Vector3(
                        routeStyle ==
                            RunRouteStyle.Endure
                            ? 0.12f
                            : 0.16f,
                        0.024f,
                        segmentDepth),
                    quietMaterial);
            }
        }

        float edgeX =
            Mathf.Max(
                2.4f,
                sectionWidth * 0.5f -
                    1.25f);
        int chevronCount =
            routeStyle ==
                RunRouteStyle.Hunt
                ? 6
                : 4;
        for (int index = 0;
            index < chevronCount;
            index++)
        {
            float ratio =
                (index + 1.0f) /
                (chevronCount + 1.0f);
            float z =
                Mathf.Lerp(
                    sectionLength * 0.42f,
                    -sectionLength * 0.42f,
                    ratio);
            float side =
                index % 2 == 0
                    ? -1.0f
                    : 1.0f;
            AddVisualBoxRotated(
                identity,
                new Vector3(
                    side * edgeX,
                    0.082f,
                    z),
                new Vector3(
                    0.11f,
                    0.026f,
                    routeStyle ==
                        RunRouteStyle.Hunt
                        ? 2.2f
                        : 1.7f),
                new Vector3(
                    0.0f,
                    side * 0.5f,
                    0.0f),
                material,
                $"RouteChevron{index + 1}");
        }
    }

    private static void CreateArenaExitRouteIdentity(
        WorldSectionRuntime arena,
        RunRouteStyle routeStyle)
    {
        Node existing =
            arena.Root.GetNodeOrNull<Node>(
                "RouteExitIdentity");
        if (IsInstanceValid(existing))
        {
            arena.Root.RemoveChild(existing);
            existing.QueueFree();
        }
        if (routeStyle ==
            RunRouteStyle.Neutral)
        {
            return;
        }

        Node3D identity = new()
        {
            Name = "RouteExitIdentity"
        };
        arena.Root.AddChild(identity);
        Material material =
            CreateRouteIdentityMaterial(
                GetRouteStyleColor(
                    routeStyle),
                0.88f);
        float radius =
            Mathf.Max(
                8.0f,
                arena.ArenaRadius);
        for (int marker = 0;
            marker < 3;
            marker++)
        {
            float z =
                -radius *
                Mathf.Lerp(
                    0.62f,
                    0.9f,
                    marker / 2.0f);
            for (int side = -1;
                side <= 1;
                side += 2)
            {
                AddVisualBoxRotated(
                    identity,
                    new Vector3(
                        side * 0.62f,
                        0.085f,
                        z),
                    new Vector3(
                        0.13f,
                        0.028f,
                        2.15f),
                    new Vector3(
                        0.0f,
                        side * 0.5f,
                        0.0f),
                    material,
                    $"ExitChevron{marker + 1}_{side}");
            }
        }
    }

    private static StandardMaterial3D
        CreateRouteIdentityMaterial(
            Color routeColor,
            float alpha)
    {
        Color visibleColor =
            new(
                routeColor.R,
                routeColor.G,
                routeColor.B,
                Mathf.Clamp(alpha, 0.0f, 1.0f));
        return new StandardMaterial3D
        {
            AlbedoColor = visibleColor,
            Transparency =
                BaseMaterial3D
                    .TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = routeColor,
            EmissionEnergyMultiplier = 0.09f,
            Metallic = 0.02f,
            Roughness = 0.68f
        };
    }

    private static string GetRouteStyleName(
        RunRouteStyle routeStyle)
    {
        return routeStyle switch
        {
            RunRouteStyle.Hunt => "HUNT",
            RunRouteStyle.Flow => "FLOW",
            RunRouteStyle.Endure => "ENDURE",
            _ => "ROUTE"
        };
    }

    private static Color GetRouteStyleColor(
        RunRouteStyle routeStyle)
    {
        return routeStyle switch
        {
            RunRouteStyle.Hunt =>
                new Color(
                    0.96f,
                    0.22f,
                    0.08f),
            RunRouteStyle.Flow =>
                new Color(
                    0.08f,
                    0.7f,
                    0.88f),
            RunRouteStyle.Endure =>
                new Color(
                    0.92f,
                    0.62f,
                    0.12f),
            _ => Colors.White
        };
    }

    private ConstructionEpisode
        ChooseConstructionEpisode(
            int sectionIndex)
    {
        if (_constructionDeck.Count == 0)
        {
            RefillConstructionDeck();
        }

        bool gentleRoute =
            IsInstanceValid(
                ConstructionPlayer) &&
            ConstructionPlayer.HealthRatio <=
                0.38f;
        bool comboRoute =
            IsInstanceValid(
                ConstructionPlayer) &&
            ConstructionPlayer.AirStompChain >=
                5;
        int chosenIndex =
            _constructionDeck.Count - 1;
        for (int index =
                _constructionDeck.Count - 1;
            index >= 0;
            index--)
        {
            ConstructionEpisode candidate =
                _constructionDeck[index];
            bool beginnerReadable =
                candidate is
                    ConstructionEpisode
                        .RhythmHurdles or
                    ConstructionEpisode
                        .SpiralChannel or
                    ConstructionEpisode
                        .SkySurf or
                    ConstructionEpisode
                        .Crosswind;
            bool recoveryFriendly =
                candidate is
                    ConstructionEpisode
                        .SpiralChannel or
                    ConstructionEpisode
                        .ReactorShaft or
                    ConstructionEpisode
                        .PulseCascade or
                    ConstructionEpisode
                        .SkySurf;
            bool earlyReadable =
                candidate is
                    ConstructionEpisode
                        .RhythmHurdles or
                    ConstructionEpisode
                        .SpiralChannel or
                    ConstructionEpisode
                        .SwitchbackCanyon or
                    ConstructionEpisode
                        .KineticSluice or
                    ConstructionEpisode
                        .PulseCascade or
                    ConstructionEpisode
                        .SkySurf or
                    ConstructionEpisode
                        .CrawlingConvoy or
                    ConstructionEpisode
                        .TunnelJunction;
            bool comboFriendly =
                candidate is
                    ConstructionEpisode
                        .ReactorShaft or
                    ConstructionEpisode
                        .PulseCascade or
                    ConstructionEpisode
                        .CrawlingConvoy or
                    ConstructionEpisode
                        .LivingRift;
            if ((sectionIndex <= 2 &&
                    !beginnerReadable) ||
                (sectionIndex <= 6 &&
                    !earlyReadable) ||
                (gentleRoute &&
                    !recoveryFriendly) ||
                (comboRoute &&
                    !comboFriendly))
            {
                continue;
            }

            chosenIndex = index;
            break;
        }

        ConstructionEpisode selected =
            _constructionDeck[
                chosenIndex];
        _constructionDeck.RemoveAt(
            chosenIndex);
        _lastConstructionEpisode =
            selected;
        return selected;
    }

    private void RefillConstructionDeck()
    {
        _constructionDeck.Clear();
        _constructionDeck.AddRange(
            new[]
            {
                ConstructionEpisode
                    .RhythmHurdles,
                ConstructionEpisode
                    .SpiralChannel,
                ConstructionEpisode
                    .ReactorShaft,
                ConstructionEpisode
                    .PulseCascade,
                ConstructionEpisode
                    .SkySurf,
                ConstructionEpisode
                    .CrawlingConvoy
            });
        for (int index =
                _constructionDeck.Count - 1;
            index > 0;
            index--)
        {
            int swapIndex =
                GD.RandRange(
                    0,
                    index);
            (_constructionDeck[index],
                _constructionDeck[
                    swapIndex]) =
                (_constructionDeck[
                    swapIndex],
                    _constructionDeck[index]);
        }

        if (_lastConstructionEpisode
                .HasValue &&
            _constructionDeck[^1] ==
                _lastConstructionEpisode
                    .Value)
        {
            (_constructionDeck[0],
                _constructionDeck[^1]) =
                (_constructionDeck[^1],
                    _constructionDeck[0]);
        }
    }

    private static string
        GetConstructionTitle(
            ConstructionEpisode episode)
    {
        return episode switch
        {
            ConstructionEpisode
                .RhythmHurdles =>
                "RHYTHM LOCK",
            ConstructionEpisode
                .SpiralChannel =>
                "SPIRAL CHANNEL",
            ConstructionEpisode
                .ReactorShaft =>
                "REACTOR SPINE",
            ConstructionEpisode
                .SwitchbackCanyon =>
                "SWITCHBACK",
            ConstructionEpisode
                .KineticSluice =>
                "KINETIC SLUICE",
            ConstructionEpisode
                .PulseCascade =>
                "PULSE CASCADE",
            ConstructionEpisode
                .SkySurf =>
                "SKY SURF",
            ConstructionEpisode
                .Crosswind =>
                "CROSSWIND",
            ConstructionEpisode
                .CrawlingConvoy =>
                "CRAWLER CONVOY",
            ConstructionEpisode
                .DemolitionCorridor =>
                "DEMOLITION LINE",
            ConstructionEpisode
                .PendulumSupports =>
                "SWING ARRAY",
            ConstructionEpisode
                .TunnelJunction =>
                "TUNNEL JUNCTION",
            _ =>
                "LIVING RIFT"
        };
    }

    private void CreateDirectedConstruction(
        Node3D sectionRoot,
        float width,
        float length,
        Color color,
        ConstructionEpisode episode)
    {
        float localZ =
            FindOpenFeatureZ(
                sectionRoot,
                (float)GD.RandRange(
                    -length * 0.1f,
                    length * 0.12f),
                length,
                25.0f);
        string verb;
        Color signal;
        switch (episode)
        {
            case ConstructionEpisode
                .RhythmHurdles:
                CreateRhythmHurdles(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "JUMP OR WEAVE THE LOW BARS";
                signal =
                    new Color(
                        1.0f,
                        0.72f,
                        0.08f);
                break;
            case ConstructionEpisode
                .SpiralChannel:
                CreateFlowRailFan(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "PICK A LANE  •  HOLD THE RAIL";
                signal =
                    new Color(
                        0.16f,
                        0.88f,
                        1.0f);
                break;
            case ConstructionEpisode
                .ReactorShaft:
                CreateWideAirLift(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                CreateConstructionRelay(
                    sectionRoot,
                    localZ + 20.0f,
                    width,
                    new[]
                    {
                        new Vector3(
                            -4.2f,
                            5.2f,
                            localZ - 1.0f),
                        new Vector3(
                            3.5f,
                            5.2f,
                            localZ - 5.4f),
                        new Vector3(
                            -1.2f,
                            5.2f,
                            localZ - 9.4f)
                    },
                    EnemyArchetypeId
                        .Springboard);
                verb =
                    "HIT A PAD  •  LAND ON THE RELAY";
                signal =
                    new Color(
                        0.46f,
                        1.0f,
                        0.28f);
                break;
            case ConstructionEpisode
                .SwitchbackCanyon:
                CreateSwitchbackCanyon(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "FOLLOW THE OPEN GAP";
                signal =
                    new Color(
                        1.0f,
                        0.42f,
                        0.08f);
                break;
            case ConstructionEpisode
                .KineticSluice:
                CreateKineticGate(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "WAIT FOR THE GATE  •  PASS THROUGH";
                signal =
                    new Color(
                        1.0f,
                        0.34f,
                        0.08f);
                break;
            case ConstructionEpisode
                .PulseCascade:
                CreatePulseRunway(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                CreateConstructionRelay(
                    sectionRoot,
                    localZ + 18.0f,
                    width,
                    new[]
                    {
                        new Vector3(
                            5.0f,
                            0.0f,
                            localZ + 3.5f),
                        new Vector3(
                            -3.8f,
                            0.0f,
                            localZ - 2.2f),
                        new Vector3(
                            2.4f,
                            0.0f,
                            localZ - 7.8f),
                        new Vector3(
                            -1.0f,
                            0.0f,
                            localZ - 12.0f)
                    },
                    EnemyArchetypeId
                        .Relay);
                verb =
                    "LAND ON EACH PAD  •  KEEP THE CHAIN";
                signal =
                    new Color(
                        0.78f,
                        0.22f,
                        1.0f);
                break;
            case ConstructionEpisode
                .SkySurf:
                CreateSkySurf(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "TAKE THE RAMP  •  STAY ON A LANE";
                signal =
                    new Color(
                        0.12f,
                        0.84f,
                        1.0f);
                break;
            case ConstructionEpisode
                .Crosswind:
                CreateCrosswindSluice(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "STEER AGAINST THE CROSSWIND";
                signal =
                    new Color(
                        0.38f,
                        0.92f,
                        1.0f);
                break;
            case ConstructionEpisode
                .CrawlingConvoy:
                CreateCrawlingConvoy(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "BOARD THE CRAWLERS  •  TRANSFER FORWARD";
                signal =
                    new Color(
                        0.28f,
                        0.9f,
                        1.0f);
                break;
            case ConstructionEpisode
                .DemolitionCorridor:
                CreateDemolitionCorridor(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                CreateConstructionRelay(
                    sectionRoot,
                    localZ + 18.0f,
                    width,
                    new[]
                    {
                        new Vector3(
                            -5.4f,
                            0.0f,
                            localZ + 5.0f),
                        new Vector3(
                            4.8f,
                            0.0f,
                            localZ - 3.8f)
                    },
                    EnemyArchetypeId
                        .Shell);
                verb =
                    "FOLLOW THE SHELLS THROUGH THE PANELS";
                signal =
                    new Color(
                        1.0f,
                        0.24f,
                        0.08f);
                break;
            case ConstructionEpisode
                .PendulumSupports:
                CreatePendulumSupports(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "WAIT FOR A GAP  •  SLIP THROUGH";
                signal =
                    new Color(
                        1.0f,
                        0.58f,
                        0.12f);
                break;
            default:
                CreatePulseRunway(
                    sectionRoot,
                    width,
                    localZ,
                    color);
                verb =
                    "KEEP MOVING  •  THE FLOOR FADES";
                signal =
                    new Color(
                        1.0f,
                        0.22f,
                        0.72f);
                break;
        }

        _ = verb;
        _ = signal;
    }

    private static void CreateGround(
        Node3D sectionRoot,
        float width,
        float length,
        Color color,
        bool createVanishingFloor)
    {
        Material material =
            CreateRunSurfaceMaterial(
                color);
        if (!createVanishingFloor)
        {
            AddGroundSlab(
                sectionRoot,
                "Ground",
                Vector3.Zero,
                new Vector3(width, 2.0f, length),
                material);
            return;
        }

        float tileWidth =
            (float)GD.RandRange(
                8.0f,
                10.5f);
        float tileDepth =
            (float)GD.RandRange(
                5.0f,
                6.8f);
        float tileHalfTravel =
            Mathf.Max(
                0.0f,
                width * 0.5f -
                    tileWidth * 0.5f -
                    4.0f);
        float tileX =
            (float)GD.RandRange(
                -tileHalfTravel,
                tileHalfTravel);
        float tileZ =
            (float)GD.RandRange(
                -length * 0.24f,
                length * 0.24f);
        float halfWidth = width * 0.5f;
        float halfLength = length * 0.5f;
        float gapLeft =
            tileX -
            tileWidth * 0.5f;
        float gapRight =
            tileX +
            tileWidth * 0.5f;
        float gapNorth =
            tileZ -
            tileDepth * 0.5f;
        float gapSouth =
            tileZ +
            tileDepth * 0.5f;

        float southDepth = halfLength - gapSouth;
        float northDepth = gapNorth + halfLength;
        AddGroundSlab(
            sectionRoot,
            "GroundSouth",
            new Vector3(0.0f, 0.0f, gapSouth + southDepth * 0.5f),
            new Vector3(width, 2.0f, southDepth),
            material);
        AddGroundSlab(
            sectionRoot,
            "GroundNorth",
            new Vector3(0.0f, 0.0f, -halfLength + northDepth * 0.5f),
            new Vector3(width, 2.0f, northDepth),
            material);

        float leftWidth =
            gapLeft +
            halfWidth;
        float rightWidth =
            halfWidth -
            gapRight;
        AddGroundSlab(
            sectionRoot,
            "GroundGapLeft",
            new Vector3(
                -halfWidth +
                    leftWidth * 0.5f,
                0.0f,
                tileZ),
            new Vector3(
                leftWidth,
                2.0f,
                tileDepth),
            material);
        AddGroundSlab(
            sectionRoot,
            "GroundGapRight",
            new Vector3(
                gapRight +
                    rightWidth * 0.5f,
                0.0f,
                tileZ),
            new Vector3(
                rightWidth,
                2.0f,
                tileDepth),
            material);
        CreateVanishingTile(
            sectionRoot,
            new Vector3(
                tileX,
                -0.18f,
                tileZ),
            new Vector3(
                tileWidth,
                0.36f,
                tileDepth),
            0,
            0);
    }

#if false
    private static void CreateCurvedRouteGround(
        Node3D sectionRoot,
        float width,
        float length,
        Color color,
        float amplitude)
    {
        const int segmentCount = 28;
        float segmentDepth =
            length / segmentCount;
        StandardMaterial3D edge =
            CreateObstacleMaterial(
                color.Lightened(0.48f),
                0.12f);
        StandardMaterial3D laneFill =
            CreateStructureMaterial(
                color.Lerp(
                    Colors.White,
                    0.72f),
                0.04f);

        Node3D route = new()
        {
            Name = "CurvedRoute"
        };
        sectionRoot.AddChild(route);
        for (int index = 0;
            index < segmentCount;
            index++)
        {
            float progress =
                (index + 0.5f) /
                segmentCount;
            float centerX =
                EvaluateCurveOffset(
                    progress,
                    amplitude);
            float localZ =
                length * 0.5f -
                progress * length;
            float tangentStep =
                0.5f / segmentCount;
            float previousProgress =
                Mathf.Max(
                    0.0f,
                    progress -
                        tangentStep);
            float nextProgress =
                Mathf.Min(
                    1.0f,
                    progress +
                        tangentStep);
            Vector3 direction =
                new Vector3(
                    EvaluateCurveOffset(
                        nextProgress,
                        amplitude) -
                    EvaluateCurveOffset(
                        previousProgress,
                        amplitude),
                    0.0f,
                    -(nextProgress -
                        previousProgress) *
                        length).Normalized();
            float yaw =
                Mathf.Atan2(
                    direction.X,
                    direction.Z);
            Vector3 lateral =
                new Vector3(
                    -direction.Z,
                    0.0f,
                    direction.X);
            AddGroundSlabRotated(
                route,
                $"CurveRoadSegment_{index}",
                new Vector3(
                    centerX,
                    0.0f,
                    localZ),
                new Vector3(
                    width,
                    2.0f,
                    segmentDepth + 1.35f),
                yaw);
            foreach (float side in
                new[] { -1.0f, 1.0f })
            {
                AddVisualBoxRotated(
                    route,
                    new Vector3(
                        centerX +
                            lateral.X *
                            side *
                            (width * 0.5f -
                                0.48f),
                        0.04f,
                        localZ +
                            lateral.Z *
                            side *
                            (width * 0.5f -
                                0.48f)),
                    new Vector3(
                        0.24f,
                        0.045f,
                        segmentDepth + 0.72f),
                    new Vector3(
                        0.0f,
                        yaw,
                        0.0f),
                    edge,
                    "CurveEdgeRibbon");
            }
        }
        AddContinuousRouteStrip(
            route,
            "CurvedRoadSurface",
            length,
            width - 0.72f,
            42,
            laneFill,
            progress =>
                EvaluateCurveOffset(
                    progress,
                    amplitude));
    }

    private static void CreateForkRouteGround(
        Node3D sectionRoot,
        float length,
        Color color,
        float branchOffset,
        float branchLaneWidth,
        bool huntOnRight)
    {
        const int segmentCount = 20;
        float segmentDepth =
            length / segmentCount;
        StandardMaterial3D flowSignal =
            CreateObstacleMaterial(
                new Color(
                    0.04f,
                    0.72f,
                    0.88f),
                0.2f);
        StandardMaterial3D huntSignal =
            CreateObstacleMaterial(
                new Color(
                    0.94f,
                    0.16f,
                    0.08f),
                0.2f);
        StandardMaterial3D flowFill =
            CreateStructureMaterial(
                new Color(
                    0.48f,
                    0.82f,
                    0.9f),
                0.7f);
        StandardMaterial3D huntFill =
            CreateStructureMaterial(
                new Color(
                    0.96f,
                    0.64f,
                    0.56f),
                0.7f);
        StandardMaterial3D commonFill =
            CreateStructureMaterial(
                new Color(
                    0.86f,
                    0.89f,
                    0.88f),
                0.42f);

        Node3D fork = new()
        {
            Name = "RouteFork"
        };
        sectionRoot.AddChild(fork);
        for (int index = 0;
            index < segmentCount;
            index++)
        {
            float progress =
                (index + 0.5f) /
                segmentCount;
            float separation =
                EvaluateForkSeparation(
                    progress,
                    branchOffset);
            float localZ =
                length * 0.5f -
                progress * length;
            foreach (float side in
                new[] { -1.0f, 1.0f })
            {
                bool huntBranch =
                    (side > 0.0f) ==
                    huntOnRight;
                Material signal =
                    huntBranch
                        ? huntSignal
                        : flowSignal;
                float centerX =
                    side * separation;
                float tangentStep =
                    0.5f / segmentCount;
                float previousProgress =
                    Mathf.Max(
                        0.0f,
                        progress -
                            tangentStep);
                float nextProgress =
                    Mathf.Min(
                        1.0f,
                        progress +
                            tangentStep);
                Vector3 direction =
                    new Vector3(
                        side *
                            (EvaluateForkSeparation(
                                nextProgress,
                                branchOffset) -
                            EvaluateForkSeparation(
                                previousProgress,
                                branchOffset)),
                        0.0f,
                        -(nextProgress -
                            previousProgress) *
                            length).Normalized();
                float yaw =
                    Mathf.Atan2(
                        direction.X,
                        direction.Z);
                string branchName =
                    huntBranch
                        ? "Hunt"
                        : "Flow";
                AddGroundSlabRotated(
                    fork,
                    $"{branchName}BranchSegment_{index}",
                    new Vector3(
                        centerX,
                        0.0f,
                        localZ),
                    new Vector3(
                        branchLaneWidth,
                        2.0f,
                        segmentDepth + 1.2f),
                    yaw);
                AddVisualBoxRotated(
                    fork,
                    new Vector3(
                        centerX,
                        0.04f,
                        localZ),
                    new Vector3(
                        0.18f,
                        0.045f,
                        segmentDepth + 0.5f),
                    new Vector3(
                        0.0f,
                        yaw,
                        0.0f),
                    signal,
                    $"{branchName}RouteRibbon");
                if (index == 4)
                {
                    if (huntBranch)
                    {
                        for (int bar = -1;
                            bar <= 1;
                            bar++)
                        {
                            AddVisualBoxRotated(
                                fork,
                                new Vector3(
                                    centerX,
                                    0.055f,
                                    localZ +
                                        bar * 0.72f),
                                new Vector3(
                                    branchLaneWidth *
                                        0.54f,
                                    0.05f,
                                    0.16f),
                                new Vector3(
                                    0.0f,
                                    yaw,
                                    0.0f),
                                signal,
                                "HuntEntryMark");
                        }
                    }
                    else
                    {
                        foreach (float turn in
                            new[] { -1.0f, 1.0f })
                        {
                            AddVisualBoxRotated(
                                fork,
                                new Vector3(
                                    centerX +
                                        turn * 0.58f,
                                    0.055f,
                                    localZ),
                                new Vector3(
                                    0.16f,
                                    0.05f,
                                    2.4f),
                                new Vector3(
                                    0.0f,
                                    yaw +
                                        Mathf.DegToRad(
                                            turn *
                                            28.0f),
                                    0.0f),
                                signal,
                                "FlowEntryChevron");
                        }
                    }
                }
            }
        }
        float huntSide =
            huntOnRight ? 1.0f : -1.0f;
        float flowSide = -huntSide;
        AddContinuousRouteStrip(
            fork,
            "ForkEntryLeftSurface",
            length,
            branchLaneWidth - 0.48f,
            12,
            commonFill,
            progress =>
                -EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.0f,
            0.25f,
            0.046f);
        AddContinuousRouteStrip(
            fork,
            "ForkEntryRightSurface",
            length,
            branchLaneWidth - 0.48f,
            12,
            commonFill,
            progress =>
                EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.0f,
            0.25f,
            0.047f);
        AddContinuousRouteStrip(
            fork,
            "FlowBranchSurface",
            length,
            branchLaneWidth - 0.48f,
            36,
            flowFill,
            progress =>
                flowSide *
                EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.25f,
            0.75f,
            0.052f);
        AddContinuousRouteStrip(
            fork,
            "HuntBranchSurface",
            length,
            branchLaneWidth - 0.48f,
            36,
            huntFill,
            progress =>
                huntSide *
                EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.25f,
            0.75f,
            0.052f);
        AddContinuousRouteStrip(
            fork,
            "ForkMergeLeftSurface",
            length,
            branchLaneWidth - 0.48f,
            12,
            commonFill,
            progress =>
                -EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.75f,
            1.0f,
            0.046f);
        AddContinuousRouteStrip(
            fork,
            "ForkMergeRightSurface",
            length,
            branchLaneWidth - 0.48f,
            12,
            commonFill,
            progress =>
                EvaluateForkSeparation(
                    progress,
                    branchOffset),
            0.75f,
            1.0f,
            0.047f);
    }

    private static void AddContinuousRouteStrip(
        Node3D parent,
        string name,
        float length,
        float width,
        int sampleCount,
        Material material,
        Func<float, float> centerAtProgress,
        float startProgress = 0.0f,
        float endProgress = 1.0f,
        float surfaceHeight = 0.045f)
    {
        SurfaceTool surface = new();
        surface.Begin(
            Mesh.PrimitiveType.Triangles);
        for (int index = 0;
            index < sampleCount;
            index++)
        {
            float progressA =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    index /
                        (float)sampleCount);
            float progressB =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    (index + 1.0f) /
                        sampleCount);
            Vector3 leftA;
            Vector3 rightA;
            GetRouteStripEdges(
                length,
                width,
                progressA,
                centerAtProgress,
                surfaceHeight,
                out leftA,
                out rightA);
            Vector3 leftB;
            Vector3 rightB;
            GetRouteStripEdges(
                length,
                width,
                progressB,
                centerAtProgress,
                surfaceHeight,
                out leftB,
                out rightB);

            AddRouteSurfaceVertex(
                surface,
                leftA);
            AddRouteSurfaceVertex(
                surface,
                leftB);
            AddRouteSurfaceVertex(
                surface,
                rightB);
            AddRouteSurfaceVertex(
                surface,
                leftA);
            AddRouteSurfaceVertex(
                surface,
                rightB);
            AddRouteSurfaceVertex(
                surface,
                rightA);
        }

        ArrayMesh mesh =
            surface.Commit();
        if (mesh == null)
        {
            return;
        }
        mesh.SurfaceSetMaterial(
            0,
            material);
        parent.AddChild(
            new MeshInstance3D
            {
                Name = name,
                Mesh = mesh,
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off
            });
    }

    private static void GetRouteStripEdges(
        float length,
        float width,
        float progress,
        Func<float, float> centerAtProgress,
        float surfaceHeight,
        out Vector3 left,
        out Vector3 right)
    {
        const float tangentStep = 0.005f;
        float previous =
            Mathf.Max(
                0.0f,
                progress -
                    tangentStep);
        float next =
            Mathf.Min(
                1.0f,
                progress +
                    tangentStep);
        Vector3 direction =
            new Vector3(
                centerAtProgress(next) -
                    centerAtProgress(previous),
                0.0f,
                -(next - previous) *
                    length).Normalized();
        Vector3 lateral =
            new Vector3(
                -direction.Z,
                0.0f,
                direction.X);
        Vector3 center =
            new Vector3(
                centerAtProgress(
                    progress),
                surfaceHeight,
                length * 0.5f -
                    progress * length);
        left =
            center -
            lateral * width * 0.5f;
        right =
            center +
            lateral * width * 0.5f;
    }

    private static void AddRouteSurfaceVertex(
        SurfaceTool surface,
        Vector3 vertex)
    {
        surface.SetNormal(Vector3.Up);
        surface.AddVertex(vertex);
    }

    private void CreateRouteForkActivity(
        Node3D sectionRoot,
        float sectionWidth,
        float length,
        float branchOffset,
        float branchLaneWidth,
        bool huntOnRight)
    {
        float huntSide =
            huntOnRight ? 1.0f : -1.0f;
        float flowSide = -huntSide;
        List<Vector3> relayPositions =
            new();
        float[] relayProgress =
        {
            0.3f,
            0.42f,
            0.54f,
            0.66f,
            0.78f
        };
        for (int index = 0;
            index < relayProgress.Length;
            index++)
        {
            float progress =
                relayProgress[index];
            float separation =
                EvaluateForkSeparation(
                    progress,
                    branchOffset);
            relayPositions.Add(
                new Vector3(
                    huntSide * separation +
                        (index % 2 == 0
                            ? -0.72f
                            : 0.72f),
                    0.0f,
                    length * 0.5f -
                        progress * length));
        }

        TraversalRelayTrigger trigger =
            new()
            {
                Name = "RouteForkHuntTrigger",
                CollisionLayer = 0,
                CollisionMask = 1
            };
        trigger.Configure(
            relayPositions);
        trigger.EnemyRequested +=
            (
                Vector3 worldPosition,
                Vector3 direction,
                int relayIndex) =>
            {
                EmitSignal(
                    SignalName
                        .TraversalEnemyRequested,
                    worldPosition,
                    direction,
                    relayIndex);
            };
        trigger.AddChild(
            new CollisionShape3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        1.5f,
                        length * 0.5f -
                            12.0f),
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                sectionWidth,
                                3.0f,
                                5.0f)
                    }
            });
        sectionRoot.AddChild(trigger);

        foreach (float progress in
            new[] { 0.34f, 0.53f, 0.72f })
        {
            float separation =
                EvaluateForkSeparation(
                    progress,
                    branchOffset);
            AddTraversalBoostZone(
                sectionRoot,
                "FlowBranchPulse",
                new Vector3(
                    flowSide * separation,
                    1.0f,
                    length * 0.5f -
                        progress * length),
                new Vector3(
                    branchLaneWidth * 0.72f,
                    2.2f,
                    7.5f),
                false,
                1.15f,
                0.7f);
        }

    }

    private static float EvaluateCurveOffset(
        float progress,
        float amplitude)
    {
        float eased =
            Mathf.SmoothStep(
                0.0f,
                1.0f,
                Mathf.Clamp(
                    progress,
                    0.0f,
                    1.0f));
        return Mathf.Sin(
                eased * Mathf.Tau) *
            amplitude;
    }

    private static float EvaluateForkSeparation(
        float progress,
        float branchOffset)
    {
        float clamped =
            Mathf.Clamp(
                progress,
                0.0f,
                1.0f);
        return Mathf.Sin(
                clamped * Mathf.Pi) *
            branchOffset;
    }

#endif

    private static void CreateVanishingTile(
        Node3D runway,
        Vector3 position,
        Vector3 size,
        int row,
        int lane)
    {
        VanishingFloor tile = new()
        {
            Name = $"VanishingFloor_{row}_{lane}",
            Position = position,
            CollisionLayer = 4,
            CollisionMask = 0,
            WarningTime =
                0.34f +
                row * 0.045f
        };
        tile.SetMeta(
            "reserve_obstacle_z",
            true);
        tile.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new BoxShape3D { Size = size }
        });

        bool brightTile =
            (row + lane) % 2 == 0;
        StandardMaterial3D tileBody =
            CreateStructureMaterial(
                new Color(
                    0.38f,
                    0.17f,
                    0.08f),
                brightTile
                    ? 0.48f
                    : 0.38f);
        StandardMaterial3D tileInset =
            CreateStructureMaterial(
                new Color(
                    0.64f,
                    0.24f,
                    0.055f),
                brightTile
                    ? 0.42f
                    : 0.31f);
        StandardMaterial3D warningMaterial =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.48f,
                    0.07f),
                brightTile
                    ? 1.18f
                    : 0.88f);
        AddVisualBox(
            tile,
            Vector3.Zero,
            size,
            tileBody);
        AddVisualBox(
            tile,
            new Vector3(
                0.0f,
                size.Y * 0.5f + 0.035f,
                0.0f),
            new Vector3(
                size.X - 0.34f,
                0.055f,
                size.Z - 0.34f),
            tileInset);

        float edgeY =
            size.Y * 0.5f + 0.075f;
        foreach (float side in
            new[] { -1.0f, 1.0f })
        {
            AddVisualBox(
                tile,
                new Vector3(
                    0.0f,
                    edgeY,
                    side *
                        (size.Z * 0.5f -
                            0.2f)),
                new Vector3(
                    size.X * 0.76f,
                    0.045f,
                    0.105f),
                warningMaterial);
            AddVisualBox(
                tile,
                new Vector3(
                    side *
                        (size.X * 0.5f -
                            0.2f),
                    edgeY,
                    0.0f),
                new Vector3(
                    0.105f,
                    0.045f,
                    size.Z * 0.66f),
                warningMaterial);
        }
        for (int seam = -1;
            seam <= 1;
            seam++)
        {
            AddVisualBoxRotated(
                tile,
                new Vector3(
                    seam * size.X * 0.22f,
                    edgeY + 0.005f,
                    0.0f),
                new Vector3(
                    0.085f,
                    0.052f,
                    size.Z * 0.48f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        seam * 8.0f),
                    0.0f),
                warningMaterial,
                "WarningSeam");
        }

        Area3D trigger = new()
        {
            Name = "Trigger",
            Position = new Vector3(0.0f, 1.5f, 0.0f),
            CollisionLayer = 0,
            CollisionMask = 1
        };
        tile.AddChild(trigger);
        trigger.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(
                    size.X * 0.94f,
                    3.0f,
                    size.Z * 0.94f)
            }
        });
        runway.AddChild(tile);
    }

    private static void CreateWaterRelayGround(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        float halfLength =
            length * 0.5f;
        const float gapDepth =
            WaterRelayGapDepth;
        const float gapCenterZ = 0.0f;
        float gapSouth =
            gapCenterZ +
                gapDepth * 0.5f;
        float gapNorth =
            gapCenterZ -
                gapDepth * 0.5f;
        Material groundMaterial =
            CreateRunSurfaceMaterial(
                color);
        AddGroundSlab(
            sectionRoot,
            "RelaySouthBank",
            new Vector3(
                0.0f,
                0.0f,
                gapSouth +
                    (halfLength - gapSouth) *
                        0.5f),
            new Vector3(
                width,
                2.0f,
                halfLength -
                    gapSouth),
            groundMaterial);
        AddGroundSlab(
            sectionRoot,
            "RelayNorthBank",
            new Vector3(
                0.0f,
                0.0f,
                -halfLength +
                    (gapNorth +
                        halfLength) *
                        0.5f),
            new Vector3(
                width,
                2.0f,
                gapNorth +
                    halfLength),
            groundMaterial);

        ShaderMaterial waterMaterial =
            CreateRelayWaterMaterial();
        sectionRoot.AddChild(
            new MeshInstance3D
            {
                Name =
                    "RelayWater",
                Position =
                    new Vector3(
                        0.0f,
                        -2.4f,
                        gapCenterZ),
                Mesh =
                    new PlaneMesh
                    {
                        Size =
                            new Vector2(
                                width -
                                    1.2f,
                                gapDepth +
                                    1.2f),
                        SubdivideWidth = 20,
                        SubdivideDepth = 16,
                        Material =
                            waterMaterial
                    }
            });

        Material causewayMaterial =
            CreateRunSurfaceMaterial(
                color.Darkened(
                    0.08f));
        StandardMaterial3D fractureSignal =
            CreateObstacleMaterial(
                new Color(
                    0.24f,
                    0.82f,
                    1.0f),
                1.2f);
        Vector3[] fragmentPositions =
        {
            new(
                -4.8f,
                -0.18f,
                8.2f),
            new(
                4.1f,
                -0.34f,
                0.2f),
            new(
                -3.6f,
                -0.22f,
                -8.0f)
        };
        for (int fragmentIndex = 0;
            fragmentIndex <
                fragmentPositions.Length;
            fragmentIndex++)
        {
            Vector3 fragment =
                fragmentPositions[
                    fragmentIndex];
            AddGroundSlab(
                sectionRoot,
                $"CausewayFragment{fragmentIndex + 1}",
                fragment,
                new Vector3(
                    6.4f,
                    2.0f,
                    4.2f),
                causewayMaterial);
            AddVisualBoxRotated(
                sectionRoot,
                fragment +
                    new Vector3(
                        0.0f,
                        0.045f,
                        0.0f),
                new Vector3(
                    4.7f,
                    0.055f,
                    0.12f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        -14.0f +
                        fragmentIndex *
                        13.0f),
                    0.0f),
                fractureSignal,
                "CausewayFracture");
        }

        Area3D waterRescue = new()
        {
            Name = "WaterFallRescue",
            Position =
                new Vector3(
                    0.0f,
                    -2.2f,
                    gapCenterZ),
            CollisionLayer = 0,
            CollisionMask = 1
        };
        waterRescue.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                width - 1.4f,
                                2.4f,
                                gapDepth + 0.8f)
                    }
            });
        waterRescue.BodyEntered +=
            body =>
            {
                if (body is not Player player)
                {
                    return;
                }

                Vector3 playerLocal =
                    sectionRoot.ToLocal(
                        player.GlobalPosition);
                Vector3 safeLocal =
                    new(
                        Mathf.Clamp(
                            playerLocal.X,
                            -width * 0.5f + 2.4f,
                            width * 0.5f - 2.4f),
                        0.26f,
                        gapSouth + 2.8f);
                player.RescueFromWater(
                    sectionRoot.ToGlobal(
                        safeLocal),
                    18.0f);
            };
        sectionRoot.AddChild(
            waterRescue);

        StandardMaterial3D bankMarker =
            CreateObstacleMaterial(
                new Color(
                    0.16f,
                    0.78f,
                    1.0f),
                1.05f);
        StandardMaterial3D bankStructure =
            CreateStructureMaterial(
                color,
                0.22f);
        foreach (float edgeZ in
            new[]
            {
                gapSouth,
                gapNorth
            })
        {
            for (int segment = -2;
                segment <= 2;
                segment++)
            {
                float segmentX =
                    segment *
                        width *
                        0.155f;
                AddVisualBox(
                    sectionRoot,
                    new Vector3(
                        segmentX,
                        0.08f,
                        edgeZ),
                    new Vector3(
                        width * 0.115f,
                        0.16f,
                        0.38f),
                    bankStructure);
                AddVisualBox(
                    sectionRoot,
                    new Vector3(
                        segmentX,
                        0.175f,
                        edgeZ),
                    new Vector3(
                        width * 0.08f,
                        0.035f,
                        0.11f),
                    bankMarker);
            }
        }
    }

    private void CreateWaterRelaySetPiece(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        float lane =
            Mathf.Min(
                width * 0.12f,
                4.2f);
        Vector3[] relayPositions =
        {
            new(
                -lane * 0.82f,
                0.0f,
                10.8f),
            new(
                lane * 0.72f,
                0.0f,
                6.55f),
            new(
                -lane * 0.58f,
                0.0f,
                2.25f),
            new(
                lane * 0.88f,
                0.0f,
                -2.2f),
            new(
                -lane * 0.68f,
                0.0f,
                -6.55f),
            new(
                lane * 0.12f,
                0.0f,
                -10.85f)
        };

        TraversalRelayTrigger trigger =
            new()
            {
                Name =
                    "WaterRelayTrigger",
                CollisionLayer = 0,
                CollisionMask = 1
            };
        trigger.Configure(
            relayPositions);
        trigger.EnemyRequested +=
            (
                Vector3 worldPosition,
                Vector3 direction,
                int relayIndex) =>
            {
                EmitSignal(
                    SignalName
                        .TraversalEnemyRequested,
                    worldPosition,
                    direction,
                    relayIndex);
            };
        trigger.AddChild(
            new CollisionShape3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        1.5f,
                        WaterRelayGapDepth *
                            0.5f +
                            WaterRelayPrewarmLead),
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                width * 0.88f,
                                3.0f,
                                4.0f)
                    }
            });
        sectionRoot.AddChild(trigger);

        StandardMaterial3D routeMaterial =
            CreateObstacleMaterial(
                new Color(
                    0.38f,
                    1.0f,
                    0.52f),
                1.12f);
        StandardMaterial3D wakeShadow =
            CreateStructureMaterial(
                new Color(
                    0.02f,
                    0.3f,
                    0.36f),
                0.48f);
        foreach (Vector3 position in
            relayPositions)
        {
            Vector3 wakePosition =
                position +
                Vector3.Down *
                    2.25f;
            AddVisualCylinder(
                sectionRoot,
                wakePosition,
                1.45f,
                1.62f,
                0.055f,
                wakeShadow,
                "RelayWake");
            AddVisualTorus(
                sectionRoot,
                wakePosition +
                    Vector3.Up * 0.045f,
                0.86f,
                1.08f,
                routeMaterial,
                "RelayWakeSignal");
            AddVisualTorus(
                sectionRoot,
                wakePosition +
                    Vector3.Up * 0.052f,
                1.27f,
                1.36f,
                routeMaterial,
                "RelayWakeRipple");
        }

        AddTraversalBoostZone(
            sectionRoot,
            "RelayExitBoost",
            new Vector3(
                0.0f,
                1.4f,
                -14.0f),
            new Vector3(
                width * 0.58f,
                3.0f,
                2.6f),
            true,
            3.0f,
            1.05f);
    }

    private static void AddGroundSlab(
        Node3D sectionRoot,
        string name,
        Vector3 surfacePosition,
        Vector3 size,
        Material material)
    {
        if (size.X <= 0.05f || size.Z <= 0.05f)
        {
            return;
        }

        StaticBody3D ground = new()
        {
            Name = name,
            Position = surfacePosition + Vector3.Down,
            CollisionLayer = 4,
            CollisionMask = 0
        };
        sectionRoot.AddChild(ground);
        ground.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new BoxShape3D { Size = size }
        });
        ground.AddChild(new MeshInstance3D
        {
            Name = "MeshInstance3D",
            Mesh = new BoxMesh
            {
                Size = size,
                Material = material
            }
        });
    }

    private static void AddGroundSlabRotated(
        Node3D sectionRoot,
        string name,
        Vector3 surfacePosition,
        Vector3 size,
        float yaw)
    {
        if (size.X <= 0.05f ||
            size.Z <= 0.05f)
        {
            return;
        }

        StaticBody3D ground = new()
        {
            Name = name,
            Position =
                surfacePosition +
                Vector3.Down,
            Rotation =
                new Vector3(
                    0.0f,
                    yaw,
                    0.0f),
            CollisionLayer = 4,
            CollisionMask = 0
        };
        sectionRoot.AddChild(ground);
        ground.AddChild(
            new CollisionShape3D
            {
                Name =
                    "CollisionShape3D",
                Shape =
                    new BoxShape3D
                    {
                        Size = size
                    }
            });
        // Curved and forked routes use one continuous render mesh. Keeping
        // these collision boxes invisible prevents their rotated corners from
        // poking through the clean road silhouette.
    }

    private static void CreateEdgeAccents(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        Color accentColor =
            color.Lerp(
                new Color(
                    0.82f,
                    0.9f,
                    0.9f),
                0.62f);
        StandardMaterial3D accentMaterial = new()
        {
            AlbedoColor = accentColor,
            EmissionEnabled = true,
            Emission = accentColor,
            EmissionEnergyMultiplier = 0.06f,
            Metallic = 0.02f,
            Roughness = 0.72f
        };

        for (int side = -1; side <= 1; side += 2)
        {
            BoxMesh edgeMesh = new()
            {
                Size = new Vector3(0.32f, 0.06f, length),
                Material = accentMaterial
            };
            sectionRoot.AddChild(new MeshInstance3D
            {
                Position = new Vector3(side * (width * 0.5f - 0.45f), 0.035f, 0.0f),
                Mesh = edgeMesh
            });
        }

        int markerCount = GD.RandRange(2, 4);
        for (int index = 0; index < markerCount; index++)
        {
            float laneRatio = (index + 1.0f) / (markerCount + 1.0f);
            BoxMesh markerMesh = new()
            {
                Size = new Vector3(0.08f, 0.025f, length * 0.82f),
                Material = accentMaterial
            };
            sectionRoot.AddChild(new MeshInstance3D
            {
                Position = new Vector3(
                    Mathf.Lerp(-width * 0.38f, width * 0.38f, laneRatio),
                    0.025f,
                    0.0f),
                Mesh = markerMesh
            });
        }
    }

    private static void CreateCameraArena(Node3D sectionRoot, float radius)
    {
        CreateCameraArena(
            sectionRoot,
            radius,
            56.0f);
    }

    private static void CreateCameraArena(
        Node3D sectionRoot,
        float radius,
        float cameraViewSize)
    {
        CameraArena arena = new()
        {
            Name = "CameraArena",
            Position = new Vector3(0.0f, 5.0f, 0.0f),
            CollisionLayer = 0,
            CollisionMask = 1,
            CameraViewSize = cameraViewSize
        };
        arena.AddToGroup("camera_arena");
        sectionRoot.AddChild(arena);
        arena.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new CylinderShape3D
            {
                Radius = Mathf.Max(4.0f, radius - 1.5f),
                Height = 16.0f
            }
        });
    }

    private static void CreateMomentumGate(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        float localZ = (float)GD.RandRange(-length * 0.24f, length * 0.24f);
        localZ = FindOpenFeatureZ(sectionRoot, localZ, length, 10.0f);
        float gateWidth = width * (float)GD.RandRange(0.58f, 0.78f);
        MomentumZone zone = new()
        {
            Name = "MomentumGate",
            Position = new Vector3(0.0f, 1.2f, localZ),
            CollisionLayer = 0,
            CollisionMask = 1,
            MomentumReward = (float)GD.RandRange(10.0f, 16.0f)
        };
        sectionRoot.AddChild(zone);
        zone.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(gateWidth, 2.4f, 1.6f)
            }
        });

        StandardMaterial3D gateBase =
            CreateStructureMaterial(
                color,
                0.26f);
        StandardMaterial3D gateSignal =
            CreateObstacleMaterial(
                new Color(
                    0.32f,
                    1.0f,
                    0.7f),
                0.92f);
        AddVisualBox(
            zone,
            new Vector3(
                0.0f,
                -1.12f,
                0.0f),
            new Vector3(
                gateWidth,
                0.075f,
                1.12f),
            gateBase);

        const int pulseCount = 7;
        for (int pulse = 0;
            pulse < pulseCount;
            pulse++)
        {
            float ratio =
                pulse /
                    (float)(
                        pulseCount - 1);
            float pulseX =
                Mathf.Lerp(
                    -gateWidth * 0.42f,
                    gateWidth * 0.42f,
                    ratio);
            AddVisualBoxRotated(
                zone,
                new Vector3(
                    pulseX,
                    -1.065f,
                    0.0f),
                new Vector3(
                    gateWidth * 0.075f,
                    0.035f,
                    0.18f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        -20.0f),
                    0.0f),
                gateSignal,
                "MomentumPulse");
        }
        foreach (float side in
            new[] { -1.0f, 1.0f })
        {
            AddVisualTorus(
                zone,
                new Vector3(
                    side *
                        gateWidth *
                        0.5f,
                    -1.07f,
                    0.0f),
                0.36f,
                0.48f,
                gateSignal,
                "MomentumAnchor");
        }
    }

    private static void CreateRushBreakablePair(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        float localZ = (float)GD.RandRange(-length * 0.28f, length * 0.1f);
        localZ = FindOpenFeatureZ(sectionRoot, localZ, length, 10.0f);
        Node3D wallRoot = new()
        {
            Name = "RushBreakableWall",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        wallRoot.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(wallRoot);

        const int panelCount = 2;
        float blockWidth =
            (float)GD.RandRange(
                5.2f,
                6.6f);
        float laneOffset =
            Mathf.Min(
                width * 0.22f,
                7.8f);
        float side =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        StandardMaterial3D material =
            CreateStructureMaterial(
                color,
                0.46f);
        StandardMaterial3D insetMaterial =
            CreateStructureMaterial(
                color.Darkened(0.24f),
                0.27f);

        StandardMaterial3D crackMaterial =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.34f,
                    0.08f),
                1.55f);
        for (int panel = 0;
            panel < panelCount;
            panel++)
        {
            float x =
                panel == 0
                    ? side * laneOffset
                    : -side *
                        laneOffset *
                        0.72f;
            float blockHeight =
                panel % 2 == 0
                    ? 2.15f
                    : 2.4f;
            Vector3 size =
                new(
                    blockWidth,
                    blockHeight,
                    1.8f);
            RushBreakable block = new()
            {
                Name =
                    panel == 0
                        ? "RushShortcut"
                        : $"RushPanel{panel + 1}",
                Position =
                    new Vector3(
                        x,
                        blockHeight * 0.5f,
                        panel == 0
                            ? 1.45f
                            : -1.45f),
                CollisionLayer =
                    ObstacleCollisionLayer,
                CollisionMask = 0
            };
            block.AddToGroup("mob_obstacle");
            block.SetMeta(
                "avoidance_half_extents",
                new Vector2(
                    blockWidth * 0.5f,
                    size.Z * 0.5f));
            wallRoot.AddChild(block);
            block.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size = size
                        }
                });
            AddVisualBox(
                block,
                Vector3.Zero,
                size,
                material);
            AddVisualBox(
                block,
                new Vector3(
                    0.0f,
                    0.0f,
                    size.Z * 0.515f),
                new Vector3(
                    size.X - 0.34f,
                    size.Y - 0.3f,
                    0.055f),
                insetMaterial);
            AddVisualBox(
                block,
                new Vector3(
                    0.0f,
                    size.Y * 0.5f +
                        0.045f,
                    0.0f),
                new Vector3(
                    size.X * 0.82f,
                    0.07f,
                    size.Z * 0.72f),
                insetMaterial);

            float frontZ =
                size.Z * 0.548f;
            float backZ =
                -frontZ;
            Vector2[] crackLayout =
            {
                new(-0.18f, -24.0f),
                new(0.0f, 17.0f),
                new(0.19f, -32.0f),
                new(-0.07f, 48.0f)
            };
            for (int crack = 0;
                crack < crackLayout.Length;
                crack++)
            {
                float crackX =
                    crackLayout[crack].X *
                        blockWidth;
                float crackY =
                    (crack % 2 == 0
                        ? 0.14f
                        : -0.15f) *
                        size.Y;
                Vector3 crackSize =
                    new(
                        0.105f,
                        size.Y *
                            (crack == 3
                                ? 0.34f
                                : 0.56f),
                        0.055f);
                Vector3 crackRotation =
                    new(
                        0.0f,
                        0.0f,
                        Mathf.DegToRad(
                            crackLayout[crack].Y));
                AddVisualBoxRotated(
                    block,
                    new Vector3(
                        crackX,
                        crackY,
                        frontZ),
                    crackSize,
                    crackRotation,
                    crackMaterial,
                    "RushCrack");
                AddVisualBoxRotated(
                    block,
                    new Vector3(
                        -crackX,
                        crackY,
                        backZ),
                    crackSize,
                    crackRotation,
                    crackMaterial,
                    "RushCrack");
            }
            for (int slash = -1;
                slash <= 1;
                slash++)
            {
                AddVisualBoxRotated(
                    block,
                    new Vector3(
                        slash *
                            blockWidth *
                            0.22f,
                        size.Y * 0.5f +
                            0.095f,
                        0.0f),
                    new Vector3(
                        0.11f,
                        0.045f,
                        size.Z * 0.56f),
                    new Vector3(
                        0.0f,
                        Mathf.DegToRad(
                            28.0f),
                        0.0f),
                    crackMaterial,
                    "RushTopFracture");
            }
        }
    }

    private static float CreateRouteChoiceWall(
        Node3D sectionRoot,
        float width,
        float length,
        Color color)
    {
        float localZ = FindOpenFeatureZ(
            sectionRoot,
            (float)GD.RandRange(-length * 0.18f, length * 0.18f),
            length,
            13.0f);
        Node3D wallRoot = new()
        {
            Name = "RouteChoiceWall",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        wallRoot.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(wallRoot);

        float halfWidth = width * 0.5f;
        float tunnelWidth = 8.4f;
        float jumpWidth = 7.6f;
        float laneOffset = width * 0.27f;
        bool tunnelOnLeft = GD.Randf() < 0.5f;
        float tunnelCenter = tunnelOnLeft ? -laneOffset : laneOffset;
        float jumpCenter = -tunnelCenter;
        float tunnelStart = tunnelCenter - tunnelWidth * 0.5f;
        float tunnelEnd = tunnelCenter + tunnelWidth * 0.5f;
        float jumpStart = jumpCenter - jumpWidth * 0.5f;
        float jumpEnd = jumpCenter + jumpWidth * 0.5f;
        float firstOpeningStart = Mathf.Min(tunnelStart, jumpStart);
        float firstOpeningEnd = tunnelStart < jumpStart
            ? tunnelEnd
            : jumpEnd;
        float secondOpeningStart = Mathf.Max(tunnelStart, jumpStart);
        float secondOpeningEnd = tunnelStart > jumpStart
            ? tunnelEnd
            : jumpEnd;
        float wallHeight = 6.6f;
        float wallDepth = 1.55f;
        StandardMaterial3D wallMaterial =
            CreateStructureMaterial(
                color,
                0.42f);
        StandardMaterial3D wallTrim =
            CreateStructureMaterial(
                color.Lightened(0.18f),
                0.52f);

        AddRouteWallSolid(
            wallRoot,
            "WallLeft",
            -halfWidth,
            firstOpeningStart,
            wallHeight,
            wallDepth,
            wallMaterial);
        AddRouteWallSolid(
            wallRoot,
            "WallMiddle",
            firstOpeningEnd,
            secondOpeningStart,
            wallHeight,
            wallDepth,
            wallMaterial);
        AddRouteWallSolid(
            wallRoot,
            "WallRight",
            secondOpeningEnd,
            halfWidth,
            wallHeight,
            wallDepth,
            wallMaterial);
        AddRouteWallVisualTrim(
            wallRoot,
            -halfWidth,
            firstOpeningStart,
            wallHeight,
            wallDepth,
            wallTrim);
        AddRouteWallVisualTrim(
            wallRoot,
            firstOpeningEnd,
            secondOpeningStart,
            wallHeight,
            wallDepth,
            wallTrim);
        AddRouteWallVisualTrim(
            wallRoot,
            secondOpeningEnd,
            halfWidth,
            wallHeight,
            wallDepth,
            wallTrim);

        float tunnelClearance = 4.15f;
        StaticBody3D tunnelBeam = new()
        {
            Name = "TunnelBeam",
            Position = new Vector3(tunnelCenter, 0.0f, 0.0f),
            CollisionLayer = ObstacleCollisionLayer,
            CollisionMask = 0
        };
        wallRoot.AddChild(tunnelBeam);
        AddObstacleBox(
            tunnelBeam,
            new Vector3(
                0.0f,
                tunnelClearance +
                    (wallHeight - tunnelClearance) * 0.5f,
                0.0f),
            new Vector3(
                tunnelWidth,
                wallHeight - tunnelClearance,
                wallDepth),
            wallMaterial);

        StandardMaterial3D tunnelMarker = CreateObstacleMaterial(
            new Color(0.16f, 0.82f, 1.0f),
            1.18f);
        AddVisualBox(
            tunnelBeam,
            new Vector3(0.0f, tunnelClearance + 0.04f, -wallDepth * 0.52f),
            new Vector3(tunnelWidth * 0.84f, 0.12f, 0.08f),
            tunnelMarker);
        foreach (float side in
            new[] { -1.0f, 1.0f })
        {
            AddVisualBox(
                wallRoot,
                new Vector3(
                    tunnelCenter +
                        side *
                            tunnelWidth *
                            0.48f,
                    tunnelClearance *
                        0.47f,
                    wallDepth * 0.54f),
                new Vector3(
                    0.13f,
                    tunnelClearance *
                        0.72f,
                    0.09f),
                tunnelMarker);
        }

        TunnelBoostZone tunnelBoostZone = new()
        {
            Name = "TunnelBoostZone",
            Position = new Vector3(tunnelCenter, 1.9f, 0.0f),
            CollisionLayer = 0,
            CollisionMask = 3,
            SpeedBoost = 8.0f,
            BoostDuration = 3.0f,
            EntryInvulnerabilityDuration = 1.35f,
            ExitInvulnerabilityDuration = 0.55f
        };
        tunnelBoostZone.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(
                    tunnelWidth * 0.82f,
                    3.8f,
                    wallDepth * 3.4f)
            }
        });
        wallRoot.AddChild(tunnelBoostZone);
        for (int dash = -1;
            dash <= 1;
            dash++)
        {
            AddVisualBox(
                wallRoot,
                new Vector3(
                    tunnelCenter,
                    0.045f,
                    dash * wallDepth * 0.88f),
                new Vector3(
                    tunnelWidth * 0.32f,
                    0.055f,
                    wallDepth * 0.34f),
                tunnelMarker);
        }

        float hurdleHeight = 1.15f;
        StaticBody3D jumpHurdle = CreateObstacleBody(
            wallRoot,
            "WallJumpRoute",
            new Vector3(jumpCenter, 0.0f, 0.0f),
            new Vector2(jumpWidth * 0.5f, wallDepth * 0.72f));
        AddObstacleBox(
            jumpHurdle,
            new Vector3(0.0f, hurdleHeight * 0.5f, 0.0f),
            new Vector3(jumpWidth, hurdleHeight, wallDepth * 1.4f),
            CreateStructureMaterial(
                color,
                0.34f));
        StandardMaterial3D jumpMarker = CreateObstacleMaterial(
            new Color(1.0f, 0.78f, 0.16f),
            1.3f);

        const float rampLength = 4.8f;
        StaticBody3D springboard =
            CreateObstacleBody(
                wallRoot,
                "WallSpringboard",
                new Vector3(
                    jumpCenter,
                    0.52f,
                    3.2f),
                new Vector2(
                    jumpWidth * 0.42f,
                    rampLength * 0.5f));
        springboard.Rotation =
            new Vector3(
                Mathf.DegToRad(11.0f),
                0.0f,
                0.0f);
        AddObstacleBox(
            springboard,
            Vector3.Zero,
            new Vector3(
                jumpWidth * 0.84f,
                0.34f,
                rampLength),
            CreateStructureMaterial(
                color,
                0.48f));
        for (int chevron = -1;
            chevron <= 1;
            chevron++)
        {
            AddVisualBoxRotated(
                springboard,
                new Vector3(
                    -jumpWidth * 0.14f,
                    0.23f,
                    chevron * 1.25f),
                new Vector3(
                    0.13f,
                    0.08f,
                    jumpWidth * 0.34f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        -50.0f),
                    0.0f),
                jumpMarker,
                "LaunchChevron");
            AddVisualBoxRotated(
                springboard,
                new Vector3(
                    jumpWidth * 0.14f,
                    0.23f,
                    chevron * 1.25f),
                new Vector3(
                    0.13f,
                    0.08f,
                    jumpWidth * 0.34f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        50.0f),
                    0.0f),
                jumpMarker,
                "LaunchChevron");
        }
        CourseLaunchPad springboardZone = new()
        {
            Name = "WallSpringboardZone",
            Position =
                new Vector3(
                    jumpCenter,
                    1.35f,
                    1.1f),
            CollisionLayer = 0,
            CollisionMask = 1,
            LocalDirection = Vector3.Forward,
            ForwardSpeed = 26.0f,
            VerticalSpeed = 14.5f,
            BoostDuration = 0.78f,
            RequiresDescendingPlayer = false,
            RearmTime = 0.6f
        };
        springboardZone.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                jumpWidth * 0.76f,
                                2.5f,
                                1.35f)
                    }
            });
        wallRoot.AddChild(
            springboardZone);

        for (int marker = -1;
            marker <= 1;
            marker++)
        {
            AddVisualBox(
                jumpHurdle,
                new Vector3(
                    marker * jumpWidth * 0.28f,
                    hurdleHeight + 0.04f,
                    0.0f),
                new Vector3(
                    jumpWidth * 0.15f,
                    0.08f,
                    wallDepth * 0.82f),
                jumpMarker);
        }
        AddTraversalBoostZone(
            jumpHurdle,
            "WallJumpBoost",
            new Vector3(0.0f, 1.45f, -wallDepth * 0.72f),
            new Vector3(
                jumpWidth * 0.82f,
                2.9f,
                wallDepth * 1.9f),
            true,
            2.25f,
            0.9f);
        return localZ;
    }

    private static void AddRouteWallSolid(
        Node3D wallRoot,
        string name,
        float startX,
        float endX,
        float height,
        float depth,
        Material material)
    {
        float segmentWidth = endX - startX;
        if (segmentWidth <= 0.1f)
        {
            return;
        }

        StaticBody3D segment = CreateObstacleBody(
            wallRoot,
            name,
            new Vector3((startX + endX) * 0.5f, 0.0f, 0.0f),
            new Vector2(segmentWidth * 0.5f, depth * 0.5f));
        AddObstacleBox(
            segment,
            new Vector3(0.0f, height * 0.5f, 0.0f),
            new Vector3(segmentWidth, height, depth),
            material);
    }

    private static void AddRouteWallVisualTrim(
        Node3D wallRoot,
        float startX,
        float endX,
        float height,
        float depth,
        Material material)
    {
        float segmentWidth =
            endX - startX;
        if (segmentWidth <= 0.25f)
        {
            return;
        }

        float centerX =
            (startX + endX) *
                0.5f;
        AddVisualBox(
            wallRoot,
            new Vector3(
                centerX,
                height + 0.08f,
                0.0f),
            new Vector3(
                segmentWidth + 0.08f,
                0.16f,
                depth + 0.18f),
            material);

        int panelCount =
            Mathf.Max(
                1,
                Mathf.FloorToInt(
                    segmentWidth / 4.5f));
        for (int panel = 1;
            panel < panelCount;
            panel++)
        {
            float ratio =
                panel /
                    (float)panelCount;
            float panelX =
                Mathf.Lerp(
                    startX,
                    endX,
                    ratio);
            AddVisualBox(
                wallRoot,
                new Vector3(
                    panelX,
                    height * 0.5f,
                    depth * 0.515f),
                new Vector3(
                    0.065f,
                    height * 0.72f,
                    0.055f),
                material);
        }
    }

    private static float FindOpenFeatureZ(
        Node3D sectionRoot,
        float preferredZ,
        float sectionLength,
        float minimumSpacing)
    {
        float usableHalfLength = Mathf.Max(
            10.0f,
            sectionLength * 0.5f - 15.0f);
        float candidate = Mathf.Clamp(
            preferredZ,
            -usableHalfLength,
            usableHalfLength);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            bool conflict = false;
            foreach (Node child in sectionRoot.GetChildren())
            {
                if (child is not Node3D node ||
                    (child is not MomentumZone &&
                        child is not RushBreakable &&
                        !child.HasMeta("reserve_obstacle_z")))
                {
                    continue;
                }

                if (Mathf.Abs(node.Position.Z - candidate) < minimumSpacing)
                {
                    conflict = true;
                    break;
                }
            }

            if (!conflict)
            {
                return candidate;
            }

            float direction = attempt % 2 == 0 ? 1.0f : -1.0f;
            candidate = Mathf.Clamp(
                preferredZ + direction * (minimumSpacing + attempt * 4.0f),
                -usableHalfLength,
                usableHalfLength);
        }

        return candidate;
    }

    private static void CreateTravelObstacles(
        Node3D sectionRoot,
        float width,
        float length,
        Color color,
        int sectionIndex,
        bool hasConstruction = false)
    {
        float countRoll = GD.Randf();
        int obstacleCount =
            hasConstruction
                ? 0
                : countRoll < 0.58f
                    ? 1
                    : 2;
        float usableHalfLength = Mathf.Max(12.0f, length * 0.5f - 17.0f);
        int previousType = -1;
        List<float> reservedZPositions = new();
        foreach (Node child in sectionRoot.GetChildren())
        {
            if (child is Node3D node &&
                (child is MomentumZone ||
                    child is RushBreakable ||
                    child.HasMeta("reserve_obstacle_z")))
            {
                reservedZPositions.Add(node.Position.Z);
            }
        }

        for (int index = 0; index < obstacleCount; index++)
        {
            float pathRatio = (index + 1.0f) / (obstacleCount + 1.0f);
            float localZ = Mathf.Lerp(
                usableHalfLength,
                -usableHalfLength,
                pathRatio);
            localZ += (float)GD.RandRange(-4.5f, 4.5f);

            // Keep the opening stretch of the first generated section clear.
            if (sectionIndex == 0)
            {
                localZ = Mathf.Min(localZ, -length * 0.14f);
            }

            localZ = FindClearObstacleZ(
                localZ,
                usableHalfLength,
                reservedZPositions,
                index);
            ObstacleType type =
                ChooseTravelObstacleType(
                    sectionIndex);
            int typeIndex =
                (int)type;
            if (typeIndex ==
                previousType)
            {
                type =
                    ChooseTravelObstacleType(
                        sectionIndex);
                typeIndex =
                    (int)type;
                if (typeIndex ==
                    previousType)
                {
                    type = type ==
                            ObstacleType.FlowRail
                        ? ObstacleType
                            .VectorJumpPad
                        : ObstacleType
                            .FlowRail;
                    typeIndex =
                        (int)type;
                }
            }

            previousType = typeIndex;
            if (type ==
                ObstacleType.RocketRamp)
            {
                reservedZPositions.Add(
                    localZ - 7.0f);
                reservedZPositions.Add(
                    localZ + 7.0f);
            }
            else
            {
                reservedZPositions.Add(
                    localZ);
            }
            switch (type)
            {
                case ObstacleType.JumpHurdle:
                    CreateJumpHurdle(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
                case ObstacleType.FlowRail:
                    CreateFlowRail(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
                case ObstacleType.Pillar:
                    CreatePillar(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
                case ObstacleType.RocketRamp:
                    CreateRocketRamp(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
                case ObstacleType.VectorJumpPad:
                    CreateVectorJumpPad(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
                case ObstacleType.KineticJumpPad:
                    CreateKineticJumpPad(
                        sectionRoot,
                        width,
                        localZ,
                        color);
                    break;
            }
        }
    }

    private static ObstacleType
        ChooseTravelObstacleType(
            int sectionIndex)
    {
        float roll =
            GD.Randf();
        if (sectionIndex <= 0)
        {
            return roll < 0.46f
                    ? ObstacleType.FlowRail
                : roll < 0.78f
                    ? ObstacleType.VectorJumpPad
                    : ObstacleType.RocketRamp;
        }

        if (roll < 0.1f)
        {
            return ObstacleType.JumpHurdle;
        }
        if (roll < 0.36f)
        {
            return ObstacleType.FlowRail;
        }
        if (roll < 0.6f)
        {
            return ObstacleType.VectorJumpPad;
        }
        if (roll < 0.79f)
        {
            return ObstacleType.KineticJumpPad;
        }
        return ObstacleType.RocketRamp;
    }

    private static float FindClearObstacleZ(
        float preferredZ,
        float usableHalfLength,
        List<float> reservedZPositions,
        int obstacleIndex)
    {
        float candidateZ = Mathf.Clamp(
            preferredZ,
            -usableHalfLength,
            usableHalfLength);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            bool hasConflict = false;
            foreach (float reservedZ in reservedZPositions)
            {
                if (Mathf.Abs(candidateZ - reservedZ) < 10.0f)
                {
                    hasConflict = true;
                    break;
                }
            }

            if (!hasConflict)
            {
                return candidateZ;
            }

            float direction = (attempt + obstacleIndex) % 2 == 0
                ? 1.0f
                : -1.0f;
            candidateZ = Mathf.Clamp(
                preferredZ + direction * (10.5f + attempt * 4.0f),
                -usableHalfLength,
                usableHalfLength);
        }

        return candidateZ;
    }

    private static void CreateJumpHurdle(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float hurdleWidth =
            (float)GD.RandRange(
                6.8f,
                9.4f);
        float halfTravel =
            Mathf.Max(
                0.0f,
                sectionWidth * 0.5f -
                    hurdleWidth * 0.5f -
                    3.0f);
        float x =
            (float)GD.RandRange(
                -halfTravel,
                halfTravel);
        Vector3 size =
            new(
                hurdleWidth,
                1.0f,
                1.35f);
        StaticBody3D body =
            CreateObstacleBody(
                sectionRoot,
                "JumpHurdle",
                new Vector3(
                    x,
                    0.0f,
                    localZ),
                new Vector2(
                    size.X * 0.5f,
                    size.Z * 0.5f));
        body.SetMeta(
            "reserve_obstacle_z",
            true);
        AddObstacleBox(
            body,
            new Vector3(
                0.0f,
                size.Y * 0.5f,
                0.0f),
            size,
            CreateStructureMaterial(
                color,
                0.44f));
        StandardMaterial3D capMaterial =
            CreateStructureMaterial(
                color.Lightened(0.16f),
                0.54f);
        AddVisualBox(
            body,
            new Vector3(
                0.0f,
                size.Y + 0.035f,
                0.0f),
            new Vector3(
                size.X - 0.28f,
                0.07f,
                size.Z - 0.18f),
            capMaterial);
        foreach (float side in
            new[] { -1.0f, 1.0f })
        {
            AddVisualBox(
                body,
                new Vector3(
                    side *
                        (size.X * 0.5f +
                            0.06f),
                    size.Y * 0.48f,
                    0.0f),
                new Vector3(
                    0.16f,
                    size.Y * 1.08f,
                    size.Z * 1.08f),
                capMaterial);
        }

        StandardMaterial3D jumpMarker =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.74f,
                    0.08f),
                1.55f);
        for (int marker = -1;
            marker <= 1;
            marker++)
        {
            float markerX =
                marker *
                    hurdleWidth *
                    0.28f;
            AddVisualBoxRotated(
                body,
                new Vector3(
                    markerX -
                        hurdleWidth *
                            0.035f,
                    size.Y + 0.095f,
                    0.08f),
                new Vector3(
                    0.09f,
                    0.055f,
                    size.Z * 0.52f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        -38.0f),
                    0.0f),
                jumpMarker,
                "JumpChevron");
            AddVisualBoxRotated(
                body,
                new Vector3(
                    markerX +
                        hurdleWidth *
                            0.035f,
                    size.Y + 0.095f,
                    0.08f),
                new Vector3(
                    0.09f,
                    0.055f,
                    size.Z * 0.52f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        38.0f),
                    0.0f),
                jumpMarker,
                "JumpChevron");
        }
        for (int cue = 0;
            cue < 2;
            cue++)
        {
            float cueZ =
                2.15f +
                cue * 1.45f;
            float cueScale =
                cue == 0
                    ? 1.0f
                    : 0.74f;
            foreach (float side in
                new[] { -1.0f, 1.0f })
            {
                AddVisualBoxRotated(
                    body,
                    new Vector3(
                        side *
                            hurdleWidth *
                            0.07f,
                        0.045f,
                        cueZ),
                    new Vector3(
                        0.105f,
                        0.045f,
                        hurdleWidth *
                            0.26f *
                            cueScale),
                    new Vector3(
                        0.0f,
                        Mathf.DegToRad(
                            side * 48.0f),
                        0.0f),
                    jumpMarker,
                    "JumpApproachCue");
            }
        }
        AddTraversalBoostZone(
            body,
            "JumpBoost",
            new Vector3(
                0.0f,
                1.45f,
                -size.Z * 0.72f),
            new Vector3(
                size.X * 0.84f,
                2.9f,
                size.Z * 1.8f),
            true,
            1.9f,
            0.78f);
    }

    private static void CreateFlowRail(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        const float railWidth = 5.2f;
        const float railLength = 14.0f;
        float halfTravel =
            Mathf.Max(
                0.0f,
                sectionWidth * 0.5f -
                    railWidth * 0.5f -
                    4.0f);
        float x =
            (float)GD.RandRange(
                -halfTravel,
                halfTravel);
        float turnSign =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        Node3D railRoot = new()
        {
            Name = "FlowRail",
            Position =
                new Vector3(
                    x,
                    0.0f,
                    localZ),
            Rotation =
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        turnSign *
                            10.0f),
                    0.0f)
        };
        railRoot.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(railRoot);

        StandardMaterial3D railMaterial =
            CreateStructureMaterial(
                color,
                0.28f);
        StandardMaterial3D flowMaterial =
            CreateObstacleMaterial(
                new Color(
                    0.08f,
                    0.66f,
                    1.0f),
                0.54f);
        StandardMaterial3D edgeMaterial =
            CreateObstacleMaterial(
                new Color(
                    0.34f,
                    0.9f,
                    1.0f),
                1.08f);
        const int segmentCount = 6;
        for (int index = 0;
            index < segmentCount;
            index++)
        {
            float ratio =
                index /
                    (float)(
                        segmentCount - 1);
            float segmentZ =
                Mathf.Lerp(
                    railLength * 0.42f,
                    -railLength * 0.42f,
                    ratio);
            float segmentX =
                Mathf.Sin(
                    (ratio - 0.5f) *
                        Mathf.Pi) *
                    turnSign *
                    0.72f;
            AddVisualBox(
                railRoot,
                new Vector3(
                    segmentX,
                    0.055f,
                    segmentZ),
                new Vector3(
                    railWidth * 0.92f,
                    0.12f,
                    railLength /
                        segmentCount *
                        1.06f),
                railMaterial);
            AddVisualBox(
                railRoot,
                new Vector3(
                    segmentX,
                    0.135f,
                    segmentZ),
                new Vector3(
                    railWidth * 0.32f,
                    0.045f,
                    railLength /
                        segmentCount *
                        0.78f),
                flowMaterial);
            for (int edge = -1;
                edge <= 1;
                edge += 2)
            {
                AddVisualBox(
                    railRoot,
                    new Vector3(
                        segmentX +
                            edge *
                                railWidth *
                                0.43f,
                        0.16f,
                        segmentZ),
                    new Vector3(
                        0.08f,
                        0.13f,
                        railLength /
                            segmentCount),
                    edgeMaterial);
            }
        }
        foreach (float entryZ in
            new[]
            {
                railLength * 0.48f,
                -railLength * 0.48f
            })
        {
            AddVisualBox(
                railRoot,
                new Vector3(
                    0.0f,
                    0.14f,
                    entryZ),
                new Vector3(
                    railWidth * 0.66f,
                    0.07f,
                    0.16f),
                edgeMaterial);
        }

        TraversalRailZone zone = new()
        {
            Name = "FlowRailZone",
            Position =
                new Vector3(
                    0.0f,
                    1.4f,
                    0.0f),
            CollisionLayer = 0,
            CollisionMask = 1,
            LocalDirection = Vector3.Forward,
            MinimumSpeed = 25.5f,
            RideDuration = 1.08f,
            SteeringAuthority = 0.38f
        };
        zone.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                railWidth * 0.88f,
                                2.8f,
                                railLength * 0.94f)
                    }
            });
        railRoot.AddChild(zone);
    }

    private static void CreatePillar(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float radius =
            (float)GD.RandRange(
                2.8f,
                3.7f);
        float height =
            (float)GD.RandRange(
                7.5f,
                10.0f);
        float halfTravel =
            Mathf.Max(
                0.0f,
                sectionWidth * 0.5f -
                    radius -
                    3.5f);
        float x =
            (float)GD.RandRange(
                -halfTravel,
                halfTravel);
        StaticBody3D pillar =
            CreateObstacleBody(
                sectionRoot,
                "CoursePillar",
                new Vector3(
                    x,
                    0.0f,
                    localZ),
                new Vector2(
                    radius,
                    radius));
        pillar.SetMeta(
            "reserve_obstacle_z",
            true);

        pillar.AddChild(
            new CollisionShape3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        height * 0.5f,
                        0.0f),
                Shape =
                    new CylinderShape3D
                    {
                        Radius = radius,
                        Height = height
                    }
            });
        StandardMaterial3D pillarBody =
            CreateStructureMaterial(
                color,
                0.43f);
        StandardMaterial3D pillarTrim =
            CreateStructureMaterial(
                color.Lightened(0.18f),
                0.52f);
        AddVisualCylinder(
            pillar,
            new Vector3(
                0.0f,
                height * 0.5f,
                0.0f),
            radius * 0.78f,
            radius,
            height,
            pillarBody,
            "PillarBody");
        AddVisualCylinder(
            pillar,
            new Vector3(
                0.0f,
                0.22f,
                0.0f),
            radius * 1.02f,
            radius * 1.16f,
            0.44f,
            pillarTrim,
            "PillarFoot");
        AddVisualCylinder(
            pillar,
            new Vector3(
                0.0f,
                height - 0.24f,
                0.0f),
            radius * 0.72f,
            radius * 0.84f,
            0.48f,
            pillarTrim,
            "PillarCrown");

        StandardMaterial3D ringMaterial =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.2f,
                    0.15f),
                1.12f);
        AddVisualTorus(
            pillar,
            new Vector3(
                0.0f,
                0.16f,
                0.0f),
            radius * 1.05f,
            radius * 1.14f,
            ringMaterial,
            "PillarWarningRing");
        AddVisualTorus(
            pillar,
            new Vector3(
                0.0f,
                height * 0.64f,
                0.0f),
            radius * 0.72f,
            radius * 0.79f,
            pillarTrim,
            "PillarBrace");
        for (int stripe = -1;
            stripe <= 1;
            stripe += 2)
        {
            AddVisualBox(
                pillar,
                new Vector3(
                    stripe *
                        radius *
                        0.64f,
                    height * 0.46f,
                    radius * 0.79f),
                new Vector3(
                    0.11f,
                    height * 0.48f,
                    0.075f),
                ringMaterial);
        }
    }

    private static void CreateRocketRamp(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float rampWidth =
            (float)GD.RandRange(
                8.6f,
                10.2f);
        const float rampLength =
            16.5f;
        const float rise =
            5.15f;
        float halfTravel =
            Mathf.Max(
                0.0f,
                sectionWidth * 0.5f -
                    rampWidth * 0.5f -
                    3.5f);
        float x =
            (float)GD.RandRange(
                -halfTravel,
                halfTravel);
        float incline =
            Mathf.Asin(
                rise /
                    rampLength);
        StaticBody3D ramp =
            CreateObstacleBody(
                sectionRoot,
                "RocketRamp",
                new Vector3(
                    x,
                    rise * 0.5f +
                        0.08f,
                    localZ),
                new Vector2(
                    rampWidth * 0.5f,
                    rampLength * 0.5f));
        ramp.Rotation =
            new Vector3(
                incline,
                0.0f,
                0.0f);
        ramp.SetMeta(
            "reserve_obstacle_z",
            true);

        Vector3 rampSize =
            new(
                rampWidth,
                0.46f,
                rampLength);
        ramp.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size = rampSize
                    }
            });
        StandardMaterial3D rampBody =
            CreateStructureMaterial(
                color,
                0.42f);
        StandardMaterial3D rampTrim =
            CreateStructureMaterial(
                color.Lightened(0.16f),
                0.54f);
        StandardMaterial3D launchSignal =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.47f,
                    0.055f),
                1.22f);
        AddVisualBox(
            ramp,
            Vector3.Zero,
            rampSize,
            rampBody);
        foreach (float side in
            new[] { -1.0f, 1.0f })
        {
            AddVisualBox(
                ramp,
                new Vector3(
                    side *
                        rampWidth *
                        0.47f,
                    0.34f,
                    0.0f),
                new Vector3(
                    0.14f,
                    0.22f,
                    rampLength *
                        0.94f),
                rampTrim);
        }

        for (int marker = 0;
            marker < 4;
            marker++)
        {
            float markerZ =
                5.5f -
                    marker * 3.45f;
            foreach (float side in
                new[] { -1.0f, 1.0f })
            {
                AddVisualBoxRotated(
                    ramp,
                    new Vector3(
                        side *
                            rampWidth *
                            0.12f,
                        0.29f,
                        markerZ),
                    new Vector3(
                        0.13f,
                        0.075f,
                        rampWidth *
                            0.34f),
                    new Vector3(
                        0.0f,
                        Mathf.DegToRad(
                            side * 50.0f),
                        0.0f),
                    launchSignal,
                    "RocketChevron");
            }
        }

        TraversalRailZone climb =
            new()
            {
                Name =
                    "RocketRampClimb",
                Position =
                    new Vector3(
                        0.0f,
                        1.0f,
                        0.45f),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    Vector3.Forward,
                MinimumSpeed = 31.5f,
                RideDuration = 1.12f,
                SteeringAuthority = 0.44f
            };
        climb.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                rampWidth *
                                    0.88f,
                                2.2f,
                                rampLength *
                                    0.88f)
                    }
            });
        ramp.AddChild(
            climb);

        CourseLaunchPad launch =
            new()
            {
                Name =
                    "RocketRampLaunch",
                Position =
                    new Vector3(
                        0.0f,
                        1.05f,
                        -6.55f),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    Vector3.Forward,
                ForwardSpeed = 37.0f,
                VerticalSpeed = 18.5f,
                BoostDuration = 1.25f,
                RequiresDescendingPlayer =
                    false,
                RearmTime = 0.7f
            };
        launch.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                rampWidth *
                                    0.82f,
                                2.2f,
                                2.35f)
                    }
            });
        ramp.AddChild(
            launch);
        AddVisualBox(
            ramp,
            new Vector3(
                0.0f,
                0.31f,
                -7.18f),
            new Vector3(
                rampWidth * 0.72f,
                0.08f,
                0.24f),
            launchSignal);
    }

    private static void CreateVectorJumpPad(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float side =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        float x =
            side *
            Mathf.Min(
                sectionWidth * 0.2f,
                7.2f);
        Vector3 launchDirection =
            new(
                -side * 0.27f,
                0.0f,
                -1.0f);
        launchDirection =
            launchDirection.Normalized();
        Node3D padRoot =
            new()
            {
                Name =
                    "VectorJumpPad",
                Position =
                    new Vector3(
                        x,
                        0.0f,
                        localZ),
                Rotation =
                    new Vector3(
                        0.0f,
                        Mathf.Atan2(
                            -launchDirection.X,
                            -launchDirection.Z),
                        0.0f)
            };
        padRoot.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(
            padRoot);

        StandardMaterial3D padBody =
            CreateStructureMaterial(
                color,
                0.34f);
        StandardMaterial3D padSignal =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.65f,
                    0.08f),
                1.12f);
        AddVisualCylinder(
            padRoot,
            new Vector3(
                0.0f,
                0.1f,
                0.0f),
            2.3f,
            2.55f,
            0.2f,
            padBody,
            "VectorPadBody");
        AddVisualTorus(
            padRoot,
            new Vector3(
                0.0f,
                0.22f,
                0.0f),
            1.92f,
            2.22f,
            padSignal,
            "VectorPadRing");
        for (int marker = 0;
            marker < 2;
            marker++)
        {
            float markerZ =
                0.55f -
                    marker * 0.95f;
            foreach (float markerSide in
                new[] { -1.0f, 1.0f })
            {
                AddVisualBoxRotated(
                    padRoot,
                    new Vector3(
                        markerSide * 0.28f,
                        0.235f,
                        markerZ),
                    new Vector3(
                        0.11f,
                        0.055f,
                        1.15f),
                    new Vector3(
                        0.0f,
                        Mathf.DegToRad(
                            markerSide *
                                48.0f),
                        0.0f),
                    padSignal,
                    "VectorPadChevron");
            }
        }

        CourseLaunchPad pad =
            new()
            {
                Name =
                    "VectorJumpZone",
                Position =
                    new Vector3(
                        0.0f,
                        1.0f,
                        0.0f),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    Vector3.Forward,
                ForwardSpeed = 30.5f,
                VerticalSpeed = 13.5f,
                BoostDuration = 0.92f,
                RequiresDescendingPlayer =
                    false,
                RearmTime = 0.55f
            };
        pad.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new CylinderShape3D
                    {
                        Radius = 2.28f,
                        Height = 2.0f
                    }
            });
        padRoot.AddChild(
            pad);
    }

    private static void CreateKineticJumpPad(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float halfTravel =
            Mathf.Max(
                0.0f,
                sectionWidth * 0.5f -
                    6.5f);
        float x =
            (float)GD.RandRange(
                -halfTravel,
                halfTravel);
        KineticJumpPad pad =
            new()
            {
                Name =
                    "KineticJumpPad",
                Position =
                    new Vector3(
                        x,
                        0.0f,
                        localZ),
                CollisionLayer = 0,
                CollisionMask = 1,
                BaseForwardSpeed = 27.0f,
                BaseVerticalSpeed = 11.5f,
                ImpactToForward = 0.42f,
                ImpactToVertical = 0.28f,
                BoostDuration = 0.88f,
                RearmTime = 0.5f
            };
        pad.SetMeta(
            "reserve_obstacle_z",
            true);
        pad.AddChild(
            new CollisionShape3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        1.0f,
                        0.0f),
                Shape =
                    new CylinderShape3D
                    {
                        Radius = 2.55f,
                        Height = 2.0f
                    }
            });
        sectionRoot.AddChild(
            pad);

        StandardMaterial3D padBody =
            CreateStructureMaterial(
                color,
                0.3f);
        StandardMaterial3D jumpSignal =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.56f,
                    0.08f),
                1.02f);
        StandardMaterial3D kineticSignal =
            CreateObstacleMaterial(
                new Color(
                    0.82f,
                    0.22f,
                    1.0f),
                1.18f);
        AddVisualCylinder(
            pad,
            new Vector3(
                0.0f,
                0.1f,
                0.0f),
            2.35f,
            2.72f,
            0.2f,
            padBody,
            "KineticPadBody");
        AddVisualTorus(
            pad,
            new Vector3(
                0.0f,
                0.215f,
                0.0f),
            1.72f,
            2.12f,
            jumpSignal,
            "KineticPadRing");
        AddVisualCylinder(
            pad,
            new Vector3(
                0.0f,
                0.218f,
                0.0f),
            0.86f,
            1.22f,
            0.055f,
            kineticSignal,
            "KineticCore");
        for (int spoke = 0;
            spoke < 4;
            spoke++)
        {
            AddVisualBoxRotated(
                pad,
                new Vector3(
                    0.0f,
                    0.232f,
                    0.0f),
                new Vector3(
                    0.13f,
                    0.045f,
                    2.95f),
                new Vector3(
                    0.0f,
                    Mathf.DegToRad(
                        spoke * 45.0f),
                    0.0f),
                jumpSignal,
                "KineticSpoke");
        }
        AddVisualTorus(
            pad,
            new Vector3(
                0.0f,
                0.255f,
                0.0f),
            2.18f,
            2.34f,
            kineticSignal,
            "ImpactPulse");
    }

    private static void CreateAirLift(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float side =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        float x =
            side *
            Mathf.Min(
                sectionWidth * 0.17f,
                5.5f);
        float entryZ =
            localZ + 5.2f;
        float platformZ =
            localZ - 5.5f;
        StandardMaterial3D padMaterial =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.42f,
                    0.08f),
                1.75f);

        MeshInstance3D padVisual = new()
        {
            Name = "AirLiftLaunch",
            Position =
                new Vector3(
                    x,
                    0.08f,
                    entryZ),
            Mesh =
                new CylinderMesh
                {
                    TopRadius = 2.25f,
                    BottomRadius = 2.55f,
                    Height = 0.16f,
                    RadialSegments = 32,
                    Material = padMaterial
                }
        };
        padVisual.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(padVisual);

        CourseLaunchPad lift = new()
        {
            Name = "AirLiftZone",
            Position =
                new Vector3(
                    x,
                    1.0f,
                    entryZ),
            CollisionLayer = 0,
            CollisionMask = 1,
            ForwardSpeed = 20.0f,
            VerticalSpeed = 28.0f,
            BoostDuration = 0.92f,
            RequiresDescendingPlayer = false
        };
        lift.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new CylinderShape3D
                    {
                        Radius = 2.2f,
                        Height = 2.0f
                    }
            });
        sectionRoot.AddChild(lift);

        Vector3 platformSize =
            new(
                9.2f,
                0.48f,
                8.4f);
        StaticBody3D platform = new()
        {
            Name = "SkySlidePlatform",
            Position =
                new Vector3(
                    x,
                    4.5f,
                    platformZ),
            CollisionLayer = 4,
            CollisionMask = 0
        };
        platform.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size = platformSize
                    }
            });
        AddVisualBox(
            platform,
            Vector3.Zero,
            platformSize,
            CreateObstacleMaterial(
                color.Lightened(0.48f),
                0.74f));

        TraversalRailZone skySlide = new()
        {
            Name = "SkySlideZone",
            Position =
                new Vector3(
                    0.0f,
                    1.2f,
                    0.0f),
            CollisionLayer = 0,
            CollisionMask = 1,
            LocalDirection =
                new Vector3(
                    side * 0.12f,
                    0.0f,
                    -1.0f)
                    .Normalized(),
            MinimumSpeed = 28.0f,
            RideDuration = 1.05f,
            SteeringAuthority = 0.34f
        };
        skySlide.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                platformSize.X * 0.86f,
                                2.4f,
                                platformSize.Z * 0.88f)
                    }
            });
        platform.AddChild(skySlide);
        sectionRoot.AddChild(platform);
    }

    private static void CreateRhythmHurdles(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D hurdles = new()
        {
            Name = "RhythmHurdles",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        hurdles.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(hurdles);

        int rowCount =
            GD.Randf() < 0.7f
                ? 2
                : 3;
        float openingWidth = 6.8f;
        float wallEdge = sectionWidth * 0.5f - 0.65f;
        float openingSide =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        StandardMaterial3D material = CreateObstacleMaterial(
            color.Lightened(0.42f),
            0.42f);
        StandardMaterial3D markerMaterial = CreateObstacleMaterial(
            new Color(1.0f, 0.78f, 0.18f),
            1.15f);

        for (int row = 0; row < rowCount; row++)
        {
            float rowZ =
                (row -
                    (rowCount - 1) *
                        0.5f) *
                4.6f;
            float openingCenter =
                row % 2 == 0
                    ? openingSide *
                        sectionWidth *
                        0.22f
                    : -openingSide *
                        sectionWidth *
                        0.18f;
            float openingLeft =
                openingCenter -
                openingWidth * 0.5f;
            float openingRight =
                openingCenter +
                openingWidth * 0.5f;
            float leftWidth =
                openingLeft +
                wallEdge;
            float rightWidth =
                wallEdge -
                openingRight;
            float height =
                0.92f +
                row * 0.13f;

            float[] segmentWidths =
            {
                leftWidth,
                rightWidth
            };
            float[] segmentCenters =
            {
                -wallEdge + leftWidth * 0.5f,
                openingRight + rightWidth * 0.5f
            };
            for (int segment = 0;
                segment < 2;
                segment++)
            {
                Vector3 size =
                    new(
                        segmentWidths[segment],
                        height,
                        1.42f);
                StaticBody3D body =
                    CreateObstacleBody(
                        hurdles,
                        $"Hurdle_{row}_{segment}",
                        new Vector3(
                            segmentCenters[segment],
                            0.0f,
                            rowZ),
                        new Vector2(
                            size.X * 0.5f,
                            size.Z * 0.5f));
                AddObstacleBox(
                    body,
                    new Vector3(
                        0.0f,
                        size.Y * 0.5f,
                        0.0f),
                    size,
                    material);
                AddVisualBox(
                    body,
                    new Vector3(
                        0.0f,
                        size.Y + 0.035f,
                        0.0f),
                    new Vector3(
                        size.X * 0.86f,
                        0.07f,
                        size.Z * 0.72f),
                    markerMaterial);
            }
        }

        AddTraversalBoostZone(
            hurdles,
            "RhythmJumpBoost",
            new Vector3(
                0.0f,
                1.5f,
                -rowCount * 2.6f),
            new Vector3(
                sectionWidth - 2.0f,
                3.0f,
                2.2f),
            true,
            2.35f,
            0.92f);
    }

    private static void CreateFlowRailFan(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D fanRoot = new()
        {
            Name = "FlowRailFan",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        fanRoot.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(fanRoot);

        const int railCount = 3;
        const float railGap = 2.5f;
        float railWidth =
            Mathf.Min(
                7.4f,
                (sectionWidth -
                    railGap *
                    (railCount - 1) -
                    2.0f) /
                railCount);
        float fanWidth =
            railWidth *
                railCount +
            railGap *
                (railCount - 1);
        float railLength = 16.0f;
        float turnSign =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        StandardMaterial3D railMaterial =
            CreateObstacleMaterial(
                new Color(
                    0.2f,
                    0.78f,
                    1.0f),
                1.1f);
        StandardMaterial3D edgeMaterial =
            CreateObstacleMaterial(
                color.Lightened(
                    0.58f),
                1.55f);
        for (int rail = 0;
            rail < railCount;
            rail++)
        {
            float x =
                -fanWidth * 0.5f +
                railWidth * 0.5f +
                rail *
                    (railWidth +
                        railGap);
            float laneTurn =
                rail == 1
                    ? -turnSign * 0.08f
                    : turnSign *
                        (rail == 0
                            ? 0.16f
                            : -0.16f);
            Node3D railRoot = new()
            {
                Name = $"FlowRail{rail + 1}",
                Position = new Vector3(x, 0.0f, 0.0f)
            };
            fanRoot.AddChild(railRoot);

            const int segmentCount = 6;
            for (int index = 0;
                index < segmentCount;
                index++)
            {
                float ratio =
                    index /
                        (float)(
                            segmentCount - 1);
                float segmentZ =
                    Mathf.Lerp(
                        railLength * 0.42f,
                        -railLength * 0.42f,
                        ratio);
                float segmentX =
                    Mathf.Sin(
                        (ratio - 0.5f) *
                            Mathf.Pi) *
                        laneTurn *
                        5.0f;
                AddVisualBox(
                    railRoot,
                    new Vector3(
                        segmentX,
                        0.055f,
                        segmentZ),
                    new Vector3(
                        railWidth * 0.98f,
                        0.1f,
                        railLength /
                            segmentCount *
                            1.12f),
                    railMaterial);
                for (int edge = -1;
                    edge <= 1;
                    edge += 2)
                {
                    AddVisualBox(
                        railRoot,
                        new Vector3(
                            segmentX +
                                edge *
                                    railWidth *
                                    0.43f,
                            0.105f,
                            segmentZ),
                        new Vector3(
                            0.1f,
                            0.18f,
                            railLength /
                                segmentCount),
                        edgeMaterial);
                }
            }

            TraversalRailZone zone = new()
            {
                Name =
                    rail == 1
                        ? "FlowRailZone"
                        : $"FlowRailZone{rail + 1}",
                Position =
                    new Vector3(
                        0.0f,
                        1.4f,
                        0.0f),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    new Vector3(
                        laneTurn,
                        0.0f,
                        -1.0f)
                        .Normalized(),
                MinimumSpeed =
                    24.5f +
                    rail * 1.4f,
                RideDuration = 1.18f,
                SteeringAuthority =
                    rail == 1
                        ? 0.4f
                        : 0.34f
            };
            zone.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    railWidth *
                                        0.95f,
                                    2.8f,
                                    railLength *
                                        0.96f)
                        }
                });
            railRoot.AddChild(zone);
        }
    }

    private static void CreateWideAirLift(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D liftRoot = new()
        {
            Name = "AirLift",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        liftRoot.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(liftRoot);

        float sideSign =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        float entryZ = 6.2f;
        float platformZ = -6.0f;
        float platformY = 4.8f;
        bool bounceExit =
            GD.Randf() < 0.52f;

        StandardMaterial3D padMaterial =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.45f,
                    0.1f),
                1.7f);
        const int laneCount = 3;
        const float padWidth = 7.4f;
        float laneSpacing =
            Mathf.Min(
                10.2f,
                (sectionWidth -
                    padWidth -
                    2.0f) *
                0.5f);
        for (int lane = 0;
            lane < laneCount;
            lane++)
        {
            float x =
                (lane - 1) *
                laneSpacing;
            AddVisualBox(
                liftRoot,
                new Vector3(
                    x,
                    0.08f,
                    entryZ),
                new Vector3(
                    padWidth,
                    0.16f,
                    3.2f),
                padMaterial);

            CourseLaunchPad lift = new()
            {
                Name =
                    lane == 1
                        ? "AirLiftZone"
                        : $"AirLiftZone{lane + 1}",
                Position =
                    new Vector3(
                        x,
                        1.0f,
                        entryZ),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    new Vector3(
                        sideSign *
                            (lane - 1) *
                            0.055f,
                        0.0f,
                        -1.0f)
                        .Normalized(),
                ForwardSpeed = 20.0f,
                VerticalSpeed = 30.0f,
                BoostDuration = 1.0f,
                RequiresDescendingPlayer =
                    false
            };
            lift.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                padWidth,
                                2.0f,
                                3.0f)
                    }
                });
            liftRoot.AddChild(lift);
        }

        Vector3 platformSize =
            new(
                Mathf.Min(
                    sectionWidth -
                        2.0f,
                    25.5f),
                0.48f,
                bounceExit
                    ? 7.2f
                    : 10.5f);
        StaticBody3D platform =
            new()
            {
                Name =
                    bounceExit
                        ? "CourseBouncePlatform"
                        : "SkySlidePlatform",
                Position =
                    new Vector3(
                        0.0f,
                        platformY,
                        platformZ),
                CollisionLayer = 4,
                CollisionMask = 0
            };
        liftRoot.AddChild(
            platform);
        platform.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            platformSize
                    }
            });
        AddVisualBox(
            platform,
            Vector3.Zero,
            platformSize,
            CreateObstacleMaterial(
                color.Lightened(
                    0.5f),
                0.78f));

        if (bounceExit)
        {
            CourseLaunchPad landingPad =
                new()
                {
                    Name =
                        "CourseBounceZone",
                    Position =
                        new Vector3(
                            0.0f,
                            1.15f,
                            0.0f),
                    CollisionLayer = 0,
                    CollisionMask = 1,
                    ForwardSpeed = 28.0f,
                    VerticalSpeed = 15.5f,
                    BoostDuration = 1.05f,
                    RequiresDescendingPlayer =
                        true
                };
            landingPad.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    platformSize.X *
                                        0.88f,
                                    2.3f,
                                    platformSize.Z *
                                        0.82f)
                        }
                });
            platform.AddChild(
                landingPad);
            AddVisualBox(
                platform,
                new Vector3(
                    0.0f,
                    platformSize.Y *
                        0.5f +
                        0.06f,
                    0.0f),
                new Vector3(
                    platformSize.X *
                        0.72f,
                    0.1f,
                    platformSize.Z *
                        0.62f),
                padMaterial);
        }
        else
        {
            TraversalRailZone skyRail =
                new()
                {
                    Name =
                        "SkySlideZone",
                    Position =
                        new Vector3(
                            0.0f,
                            1.2f,
                            0.0f),
                    CollisionLayer = 0,
                    CollisionMask = 1,
                    LocalDirection =
                        new Vector3(
                            sideSign *
                                0.12f,
                            0.0f,
                            -1.0f)
                            .Normalized(),
                    MinimumSpeed = 29.0f,
                    RideDuration = 1.25f,
                    SteeringAuthority = 0.3f
                };
            skyRail.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    platformSize.X *
                                        0.88f,
                                    2.4f,
                                    platformSize.Z *
                                        0.9f)
                        }
                });
            platform.AddChild(
                skyRail);
        }
    }

    private static void CreateSwitchbackCanyon(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D canyon = new()
        {
            Name = "SwitchbackCanyon",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        canyon.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(canyon);

        float edge = sectionWidth * 0.5f - 0.65f;
        float openingWidth = 7.4f;
        float side =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        float[] openingCenters =
        {
            side * sectionWidth * 0.24f,
            -side * sectionWidth * 0.18f,
            side * sectionWidth * 0.08f
        };
        StandardMaterial3D wallMaterial =
            CreateObstacleMaterial(
                color.Lightened(0.26f),
                0.42f);
        StandardMaterial3D lipMaterial =
            CreateObstacleMaterial(
                new Color(0.24f, 0.88f, 1.0f),
                1.35f);

        for (int row = 0;
            row < openingCenters.Length;
            row++)
        {
            float rowZ =
                5.8f -
                row * 5.8f;
            float openingLeft =
                openingCenters[row] -
                openingWidth * 0.5f;
            float openingRight =
                openingCenters[row] +
                openingWidth * 0.5f;
            float[] widths =
            {
                openingLeft + edge,
                edge - openingRight
            };
            float[] centers =
            {
                -edge + widths[0] * 0.5f,
                openingRight + widths[1] * 0.5f
            };
            for (int segment = 0;
                segment < 2;
                segment++)
            {
                Vector3 size =
                    new(
                        widths[segment],
                        3.8f,
                        1.5f);
                StaticBody3D body =
                    CreateObstacleBody(
                        canyon,
                        $"CanyonWall_{row}_{segment}",
                        new Vector3(
                            centers[segment],
                            0.0f,
                            rowZ),
                        new Vector2(
                            size.X * 0.5f,
                            size.Z * 0.5f));
                AddObstacleBox(
                    body,
                    new Vector3(
                        0.0f,
                        size.Y * 0.5f,
                        0.0f),
                    size,
                    wallMaterial);
                AddVisualBox(
                    body,
                    new Vector3(
                        0.0f,
                        size.Y + 0.055f,
                        0.0f),
                    new Vector3(
                        size.X * 0.9f,
                        0.11f,
                        size.Z * 0.78f),
                    lipMaterial);
            }

            AddVisualBox(
                canyon,
                new Vector3(
                    openingCenters[row],
                    0.055f,
                    rowZ),
                new Vector3(
                    openingWidth * 0.68f,
                    0.1f,
                    1.9f),
                lipMaterial);
        }

        AddTraversalBoostZone(
            canyon,
            "CanyonExitBoost",
            new Vector3(0.0f, 1.35f, -7.2f),
            new Vector3(
                sectionWidth - 2.0f,
                2.7f,
                2.2f),
            false,
            2.2f,
            0.88f);
    }

    private static void CreateKineticGate(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D gate = new()
        {
            Name = "KineticGate",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        gate.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(gate);

        const float openingWidth = 7.2f;
        const float travel = 2.35f;
        float blockWidth =
            (sectionWidth -
                1.4f -
                openingWidth) *
                0.5f +
            travel +
            0.7f;
        float centerOffset =
            openingWidth * 0.5f +
            blockWidth * 0.5f;
        float phase =
            (float)GD.RandRange(
                0.0f,
                Mathf.Tau);
        Vector3 blockSize =
            new(
                blockWidth,
                4.7f,
                1.55f);
        StandardMaterial3D bodyMaterial =
            CreateObstacleMaterial(
                color.Lightened(0.34f),
                0.62f);
        StandardMaterial3D signalMaterial =
            CreateObstacleMaterial(
                new Color(1.0f, 0.35f, 0.08f),
                1.7f);

        for (int side = -1;
            side <= 1;
            side += 2)
        {
            OscillatingObstacle block = new()
            {
                Name =
                    side < 0
                        ? "KineticGateLeft"
                        : "KineticGateRight",
                Position =
                    new Vector3(
                        side * centerOffset,
                        0.0f,
                        0.0f),
                CollisionLayer =
                    ObstacleCollisionLayer,
                CollisionMask = 0,
                LocalAxis = Vector3.Right,
                Amplitude = travel,
                Frequency = 0.52f,
                Phase = phase
            };
            block.AddToGroup("mob_obstacle");
            block.SetMeta(
                "avoidance_half_extents",
                new Vector2(
                    blockWidth * 0.5f,
                    blockSize.Z * 0.5f));
            gate.AddChild(block);
            block.AddChild(
                new CollisionShape3D
                {
                    Position =
                        new Vector3(
                            0.0f,
                            blockSize.Y *
                                0.5f,
                            0.0f),
                    Shape =
                        new BoxShape3D
                        {
                            Size = blockSize
                        }
                });
            AddVisualBox(
                block,
                new Vector3(
                    0.0f,
                    blockSize.Y * 0.5f,
                    0.0f),
                blockSize,
                bodyMaterial);
            AddVisualBox(
                block,
                new Vector3(
                    -side *
                        blockSize.X *
                        0.48f,
                    blockSize.Y * 0.48f,
                    -blockSize.Z * 0.52f),
                new Vector3(
                    0.18f,
                    blockSize.Y * 0.72f,
                    0.08f),
                signalMaterial);
        }

        AddVisualBox(
            gate,
            new Vector3(
                0.0f,
                0.035f,
                0.0f),
            new Vector3(
                sectionWidth - 1.0f,
                0.06f,
                3.8f),
            CreateObstacleMaterial(
                new Color(0.14f, 0.42f, 0.56f),
                0.5f));
        AddTraversalBoostZone(
            gate,
            "KineticGateBoost",
            new Vector3(
                0.0f,
                1.4f,
                -2.0f),
            new Vector3(
                openingWidth * 0.86f,
                2.8f,
                2.8f),
            false,
            2.65f,
            0.9f);
    }

    private static void CreatePulseRunway(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D runway = new()
        {
            Name = "PulseRunway",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        runway.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(runway);

        StandardMaterial3D pulseMaterial =
            CreateObstacleMaterial(
                new Color(
                    0.78f,
                    0.2f,
                    0.9f),
                1.18f);
        StandardMaterial3D routeMaterial =
            CreateStructureMaterial(
                new Color(
                    0.26f,
                    0.58f,
                    0.72f),
                0.46f);
        float[] padX =
        {
            4.6f,
            -3.6f,
            2.2f
        };
        const float padWidth = 9.2f;
        Vector3 previousPad =
            Vector3.Zero;
        for (int pulse = 0;
            pulse < 3;
            pulse++)
        {
            float z =
                5.7f -
                pulse * 5.7f;
            Vector3 padPosition =
                new(
                    padX[pulse],
                    0.075f,
                    z);
            if (pulse > 0)
            {
                Vector3 delta =
                    padPosition -
                    previousPad;
                Vector3 midpoint =
                    (padPosition +
                        previousPad) *
                    0.5f;
                AddVisualBoxRotated(
                    runway,
                    new Vector3(
                        midpoint.X,
                        0.035f,
                        midpoint.Z),
                    new Vector3(
                        1.05f,
                        0.045f,
                        delta.Length()),
                    new Vector3(
                        0.0f,
                        Mathf.Atan2(
                            delta.X,
                            delta.Z),
                        0.0f),
                    routeMaterial,
                    "PulseRoute");
            }
            AddVisualBox(
                runway,
                padPosition,
                new Vector3(
                    padWidth,
                    0.14f,
                    3.0f),
                pulseMaterial);
            AddVisualBox(
                runway,
                new Vector3(
                    padX[pulse],
                    0.16f,
                    z),
                new Vector3(
                    padWidth * 0.58f,
                    0.08f,
                    1.35f),
                routeMaterial);

            CourseLaunchPad pad = new()
            {
                Name =
                    pulse == 0
                        ? "PulseLaunchZone"
                        : $"PulseLaunchZone{pulse + 1}",
                Position =
                    new Vector3(
                        padX[pulse],
                        1.05f,
                        z),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    new Vector3(
                        pulse < 2
                            ? (padX[pulse + 1] -
                                padX[pulse]) *
                                0.018f
                            : 0.0f,
                        0.0f,
                        -1.0f)
                        .Normalized(),
                ForwardSpeed =
                    25.5f +
                    pulse * 1.25f,
                VerticalSpeed =
                    10.5f +
                    pulse * 1.4f,
                BoostDuration =
                    0.72f +
                    pulse * 0.08f,
                RequiresDescendingPlayer =
                    pulse > 0,
                RearmTime = 0.52f
            };
            pad.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    padWidth,
                                    2.1f,
                                    2.7f)
                        }
                });
            runway.AddChild(pad);
            previousPad =
                padPosition;
        }
    }

    private static void CreateSkySurf(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D surf = new()
        {
            Name = "SkySurf",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        surf.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(surf);

        const int laneCount = 3;
        const float laneGap = 2.05f;
        const float rampLength = 12.5f;
        float usableWidth = sectionWidth - 1.4f;
        float laneWidth =
            (usableWidth -
                laneGap *
                    (laneCount - 1)) /
            laneCount;
        StandardMaterial3D rampMaterial =
            CreateObstacleMaterial(
                color.Lightened(0.48f),
                0.82f);
        StandardMaterial3D edgeMaterial =
            CreateObstacleMaterial(
                new Color(0.18f, 0.88f, 1.0f),
                0.92f);

        CourseLaunchPad entry = new()
        {
            Name = "SkySurfEntry",
            Position = new Vector3(0.0f, 1.05f, 7.25f),
            CollisionLayer = 0,
            CollisionMask = 1,
            ForwardSpeed = 24.0f,
            VerticalSpeed = 17.0f,
            BoostDuration = 0.84f,
            RequiresDescendingPlayer = false
        };
        entry.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                sectionWidth - 1.8f,
                                2.1f,
                                2.2f)
                    }
            });
        surf.AddChild(entry);
        for (int entryLane = 0;
            entryLane < laneCount;
            entryLane++)
        {
            float entryX =
                -usableWidth * 0.5f +
                laneWidth * 0.5f +
                entryLane *
                    (laneWidth +
                        laneGap);
            AddVisualBox(
                surf,
                new Vector3(
                    entryX,
                    0.08f,
                    7.25f),
                new Vector3(
                    laneWidth * 0.82f,
                    0.14f,
                    2.35f),
                edgeMaterial);
        }

        for (int lane = 0;
            lane < laneCount;
            lane++)
        {
            float x =
                -usableWidth * 0.5f +
                laneWidth * 0.5f +
                lane *
                    (laneWidth + laneGap);
            StaticBody3D bank =
                CreateObstacleBody(
                    surf,
                    $"SurfBank{lane + 1}",
                    new Vector3(
                        x,
                        2.18f,
                        0.15f),
                    new Vector2(
                        laneWidth * 0.5f,
                        rampLength * 0.5f));
            bank.Rotation =
                new Vector3(
                    Mathf.DegToRad(-10.0f),
                    0.0f,
                    Mathf.DegToRad(
                        (lane - 1) * 3.5f));
            AddObstacleBox(
                bank,
                Vector3.Zero,
                new Vector3(
                    laneWidth,
                    0.46f,
                    rampLength),
                rampMaterial);
            for (int edge = -1;
                edge <= 1;
                edge += 2)
            {
                AddVisualBox(
                    bank,
                    new Vector3(
                        edge * laneWidth * 0.45f,
                        0.3f,
                        0.0f),
                    new Vector3(
                        0.12f,
                        0.14f,
                        rampLength * 0.9f),
                    edgeMaterial);
            }

            TraversalRailZone slide = new()
            {
                Name =
                    lane == 1
                        ? "SkySurfZone"
                        : $"SkySurfZone{lane + 1}",
                Position = new Vector3(0.0f, 0.82f, 0.0f),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    new Vector3(
                        (lane - 1) * 0.055f,
                        0.0f,
                        -1.0f)
                        .Normalized(),
                MinimumSpeed =
                    28.0f +
                    lane * 1.2f,
                RideDuration = 1.05f,
                SteeringAuthority = 0.38f
            };
            slide.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    laneWidth * 0.9f,
                                    1.8f,
                                    rampLength * 0.9f)
                        }
                });
            bank.AddChild(slide);
        }

        CourseLaunchPad exit = new()
        {
            Name = "SkySurfExit",
            Position = new Vector3(0.0f, 1.05f, -7.1f),
            CollisionLayer = 0,
            CollisionMask = 1,
            ForwardSpeed = 29.0f,
            VerticalSpeed = 9.5f,
            BoostDuration = 0.88f,
            RequiresDescendingPlayer = true
        };
        exit.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                sectionWidth - 1.8f,
                                2.1f,
                                2.0f)
                    }
            });
        surf.AddChild(exit);
    }

    private static void CreateCrosswindSluice(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        Node3D sluice = new()
        {
            Name = "CrosswindSluice",
            Position = new Vector3(0.0f, 0.0f, localZ)
        };
        sluice.SetMeta("reserve_obstacle_z", true);
        sectionRoot.AddChild(sluice);

        float initialSide =
            GD.Randf() < 0.5f
                ? -1.0f
                : 1.0f;
        StandardMaterial3D fieldMaterial =
            CreateObstacleMaterial(
                new Color(0.4f, 0.94f, 1.0f),
                1.15f);
        StandardMaterial3D finMaterial =
            CreateObstacleMaterial(
                color.Lightened(0.5f),
                0.62f);

        for (int row = 0;
            row < 3;
            row++)
        {
            float z =
                5.5f -
                row * 5.5f;
            float side =
                row % 2 == 0
                    ? initialSide
                    : -initialSide;
            for (int stripe = -2;
                stripe <= 2;
                stripe++)
            {
                AddVisualBox(
                    sluice,
                    new Vector3(
                        stripe *
                            sectionWidth *
                            0.18f +
                            side * 0.7f,
                        0.055f,
                        z),
                    new Vector3(
                        sectionWidth * 0.12f,
                        0.08f,
                        1.35f),
                    fieldMaterial);
            }

            TraversalRailZone wind = new()
            {
                Name =
                    row == 0
                        ? "CrosswindZone"
                        : $"CrosswindZone{row + 1}",
                Position = new Vector3(0.0f, 1.35f, z),
                CollisionLayer = 0,
                CollisionMask = 1,
                LocalDirection =
                    new Vector3(
                        side * 0.22f,
                        0.0f,
                        -1.0f)
                        .Normalized(),
                MinimumSpeed =
                    23.5f +
                    row,
                RideDuration = 0.46f,
                SteeringAuthority = 0.58f
            };
            wind.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    sectionWidth - 1.2f,
                                    2.7f,
                                    3.7f)
                        }
                });
            sluice.AddChild(wind);

            float[] finPositions =
            {
                side * sectionWidth * 0.12f,
                -side * sectionWidth * 0.34f
            };
            foreach (float x in finPositions)
            {
                StaticBody3D fin =
                    CreateObstacleBody(
                        sluice,
                        $"WindFin_{row}_{x > 0.0f}",
                        new Vector3(x, 0.0f, z),
                        new Vector2(0.62f, 0.88f));
                AddObstacleBox(
                    fin,
                    new Vector3(0.0f, 1.35f, 0.0f),
                    new Vector3(
                        1.24f,
                        2.7f,
                        1.76f),
                    finMaterial);
            }
        }
    }

    private static void CreateDetourBlock(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float blockWidth = (float)GD.RandRange(4.2f, 6.2f);
        float blockDepth = (float)GD.RandRange(3.2f, 5.0f);
        float blockHeight = (float)GD.RandRange(3.0f, 4.7f);
        float halfTravel = Mathf.Max(
            0.0f,
            sectionWidth * 0.5f - blockWidth * 0.5f - 3.5f);
        float x = (float)GD.RandRange(-halfTravel, halfTravel);
        Vector3 size = new(blockWidth, blockHeight, blockDepth);
        StaticBody3D body = CreateObstacleBody(
            sectionRoot,
            "DetourBlock",
            new Vector3(x, 0.0f, localZ),
            new Vector2(size.X * 0.5f, size.Z * 0.5f));
        StandardMaterial3D material = CreateObstacleMaterial(
            color.Lightened(0.3f),
            0.28f);
        AddObstacleBox(
            body,
            new Vector3(0.0f, blockHeight * 0.5f, 0.0f),
            size,
            material);

        StandardMaterial3D capMaterial = CreateObstacleMaterial(
            color.Lightened(0.62f),
            0.85f);
        AddVisualBox(
            body,
            new Vector3(0.0f, blockHeight + 0.06f, 0.0f),
            new Vector3(blockWidth * 0.86f, 0.12f, blockDepth * 0.86f),
            capMaterial);
    }

    private static void CreateOverheadFrame(
        Node3D sectionRoot,
        float sectionWidth,
        float localZ,
        Color color)
    {
        float openingWidth = (float)GD.RandRange(7.0f, 10.0f);
        float pillarWidth = 1.15f;
        float totalWidth = openingWidth + pillarWidth * 2.0f;
        float halfTravel = Mathf.Max(
            0.0f,
            sectionWidth * 0.5f - totalWidth * 0.5f - 3.0f);
        float x = (float)GD.RandRange(-halfTravel, halfTravel);
        float pillarHeight = 4.35f;
        float beamHeight = 0.9f;
        float depth = 1.65f;
        StaticBody3D body = CreateObstacleBody(
            sectionRoot,
            "OverheadFrame",
            new Vector3(x, 0.0f, localZ),
            new Vector2(totalWidth * 0.5f, depth * 0.5f));
        StandardMaterial3D material = CreateObstacleMaterial(
            color.Lightened(0.38f),
            0.5f);

        float pillarOffset = openingWidth * 0.5f + pillarWidth * 0.5f;
        Vector3 pillarSize = new(pillarWidth, pillarHeight, depth);
        AddObstacleBox(
            body,
            new Vector3(-pillarOffset, pillarHeight * 0.5f, 0.0f),
            pillarSize,
            material);
        AddObstacleBox(
            body,
            new Vector3(pillarOffset, pillarHeight * 0.5f, 0.0f),
            pillarSize,
            material);
        AddObstacleBox(
            body,
            new Vector3(
                0.0f,
                pillarHeight - beamHeight * 0.5f,
                0.0f),
            new Vector3(totalWidth, beamHeight, depth),
            material);

        StandardMaterial3D markerMaterial = CreateObstacleMaterial(
            new Color(0.55f, 0.9f, 1.0f),
            1.0f);
        AddVisualBox(
            body,
            new Vector3(0.0f, pillarHeight - beamHeight - 0.04f, 0.0f),
            new Vector3(openingWidth * 0.82f, 0.08f, depth * 1.03f),
            markerMaterial);
        AddTraversalBoostZone(
            body,
            "UnderpassBoost",
            new Vector3(0.0f, 1.4f, 0.0f),
            new Vector3(openingWidth * 0.8f, 2.8f, depth * 2.35f),
            false,
            1.75f,
            0.8f);
    }

    private static void
        CreateCrawlingConvoy(
            Node3D sectionRoot,
            float sectionWidth,
            float localZ,
            Color color)
    {
        Node3D convoy =
            new()
            {
                Name =
                    "CrawlerConvoy",
                Position =
                    new Vector3(
                        0.0f,
                        0.0f,
                        localZ)
            };
        convoy.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(
            convoy);

        StandardMaterial3D hull =
            CreateStructureMaterial(
                color,
                0.48f);
        StandardMaterial3D drive =
            CreateObstacleMaterial(
                new Color(
                    0.16f,
                    0.84f,
                    1.0f),
                1.45f);
        const float carWidth = 10.4f;
        const float carDepth = 5.1f;
        for (int carIndex = 0;
            carIndex < 3;
            carIndex++)
        {
            float carZ =
                6.0f -
                carIndex * 6.0f;
            float side =
                carIndex % 2 == 0
                    ? -1.0f
                    : 1.0f;
            OscillatingObstacle car =
                new()
                {
                    Name =
                        $"CrawlerCar{carIndex + 1}",
                    Position =
                        new Vector3(
                            side * 1.5f,
                            0.62f,
                            carZ),
                    CollisionLayer = 4,
                    CollisionMask = 0,
                    LocalAxis =
                        Vector3.Right,
                    Amplitude =
                        1.05f +
                        carIndex * 0.18f,
                    Frequency =
                        0.16f +
                        carIndex * 0.025f,
                    Phase =
                        carIndex *
                        1.72f
                };
            convoy.AddChild(car);
            car.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    carWidth,
                                    1.0f,
                                    carDepth)
                        }
                });
            AddVisualBox(
                car,
                Vector3.Zero,
                new Vector3(
                    carWidth,
                    1.0f,
                    carDepth),
                hull);
            AddVisualBox(
                car,
                new Vector3(
                    0.0f,
                    0.56f,
                    0.0f),
                new Vector3(
                    carWidth * 0.72f,
                    0.12f,
                    carDepth * 0.68f),
                drive);
            for (int sideIndex = -1;
                sideIndex <= 1;
                sideIndex += 2)
            {
                for (int wheel = -1;
                    wheel <= 1;
                    wheel += 2)
                {
                    MeshInstance3D tread =
                        new()
                        {
                            Name =
                                "CrawlerDrive",
                            Position =
                                new Vector3(
                                    sideIndex *
                                        carWidth *
                                        0.49f,
                                    -0.28f,
                                    wheel *
                                        carDepth *
                                        0.28f),
                            RotationDegrees =
                                new Vector3(
                                    0.0f,
                                    0.0f,
                                    90.0f),
                            Mesh =
                                new TorusMesh
                                {
                                    InnerRadius =
                                        0.42f,
                                    OuterRadius =
                                        0.56f,
                                    Rings = 10,
                                    RingSegments = 24
                                },
                            MaterialOverride =
                                drive,
                            CastShadow =
                                GeometryInstance3D
                                    .ShadowCastingSetting
                                    .Off
                        };
                    car.AddChild(
                        tread);
                }
            }

            TraversalRailZone deck =
                new()
                {
                    Name =
                        $"ConvoyDeckZone{carIndex + 1}",
                    Position =
                        new Vector3(
                            0.0f,
                            1.05f,
                            0.0f),
                    CollisionLayer = 0,
                    CollisionMask = 1,
                    LocalDirection =
                        new Vector3(
                            side * 0.08f,
                            0.0f,
                            -1.0f)
                            .Normalized(),
                    MinimumSpeed =
                        25.0f +
                        carIndex,
                    RideDuration = 0.72f,
                    SteeringAuthority = 0.46f
                };
            deck.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                new Vector3(
                                    carWidth * 0.9f,
                                    2.1f,
                                    carDepth * 0.84f)
                        }
                });
            car.AddChild(deck);
        }

        CourseLaunchPad entry =
            new()
            {
                Name =
                    "ConvoyBoardingPad",
                Position =
                    new Vector3(
                        0.0f,
                        1.0f,
                        10.0f),
                CollisionLayer = 0,
                CollisionMask = 1,
                ForwardSpeed = 24.5f,
                VerticalSpeed = 10.5f,
                BoostDuration = 0.72f
            };
        entry.AddChild(
            new CollisionShape3D
            {
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                sectionWidth * 0.62f,
                                2.0f,
                                2.2f)
                    }
            });
        convoy.AddChild(entry);
        AddVisualBox(
            convoy,
            new Vector3(
                0.0f,
                0.08f,
                10.0f),
            new Vector3(
                sectionWidth * 0.62f,
                0.15f,
                2.4f),
            drive);
        AddTraversalBoostZone(
            convoy,
            "ConvoyExitBoost",
            new Vector3(
                0.0f,
                1.3f,
                -10.0f),
            new Vector3(
                sectionWidth * 0.68f,
                2.6f,
                2.4f),
            true,
            3.2f,
            1.0f);
    }

    private static void
        CreateDemolitionCorridor(
            Node3D sectionRoot,
            float sectionWidth,
            float localZ,
            Color color)
    {
        Node3D corridor =
            new()
            {
                Name =
                    "DemolitionCorridor",
                Position =
                    new Vector3(
                        0.0f,
                        0.0f,
                        localZ)
            };
        corridor.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(
            corridor);
        StandardMaterial3D bodyMaterial =
            CreateStructureMaterial(
                color,
                0.5f);
        StandardMaterial3D fracture =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.28f,
                    0.06f),
                1.8f);
        for (int panelIndex = 0;
            panelIndex < 3;
            panelIndex++)
        {
            float side =
                panelIndex % 2 == 0
                    ? -1.0f
                    : 1.0f;
            float x =
                side *
                Mathf.Min(
                    sectionWidth *
                        (panelIndex == 2
                            ? 0.18f
                            : 0.15f),
                    6.2f);
            float z =
                5.6f -
                panelIndex * 5.6f;
            Vector3 size =
                new(
                    11.4f,
                    2.45f,
                    1.7f);
            RushBreakable block =
                new()
                {
                    Name =
                        $"DemolitionPanel{panelIndex + 1}",
                    Position =
                        new Vector3(
                            x,
                            size.Y * 0.5f,
                            z),
                    CollisionLayer =
                        ObstacleCollisionLayer,
                    CollisionMask = 0
                };
            block.AddToGroup(
                "mob_obstacle");
            block.SetMeta(
                "avoidance_half_extents",
                new Vector2(
                    size.X * 0.5f,
                    size.Z * 0.5f));
            corridor.AddChild(
                block);
            block.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size = size
                        }
                });
            AddVisualBox(
                block,
                Vector3.Zero,
                size,
                bodyMaterial);
            for (int crack = -1;
                crack <= 1;
                crack++)
            {
                AddVisualBoxRotated(
                    block,
                    new Vector3(
                        crack *
                            size.X *
                            0.21f,
                        0.0f,
                        size.Z *
                            0.52f),
                    new Vector3(
                        0.11f,
                        size.Y * 0.7f,
                        0.06f),
                    new Vector3(
                        0.0f,
                        0.0f,
                        Mathf.DegToRad(
                            crack * 28.0f)),
                    fracture,
                    "DemolitionFracture");
            }

            Area3D enemyBreaker =
                new()
                {
                    Name =
                        "EnemyDemolitionSensor",
                    CollisionLayer = 0,
                    CollisionMask = 2
                };
            enemyBreaker.AddChild(
                new CollisionShape3D
                {
                    Shape =
                        new BoxShape3D
                        {
                            Size =
                                size +
                                new Vector3(
                                    0.5f,
                                    0.5f,
                                    0.65f)
                        }
                });
            enemyBreaker.BodyEntered +=
                body =>
                {
                    if (body is Mob)
                    {
                        block.Break(true);
                    }
                };
            block.AddChild(
                enemyBreaker);
        }

        AddTraversalBoostZone(
            corridor,
            "DemolitionExitBoost",
            new Vector3(
                0.0f,
                1.4f,
                -8.4f),
            new Vector3(
                sectionWidth - 2.0f,
                2.8f,
                2.3f),
            false,
            3.0f,
            0.95f);
    }

    private static void
        CreatePendulumSupports(
            Node3D sectionRoot,
            float sectionWidth,
            float localZ,
            Color color)
    {
        Node3D array =
            new()
            {
                Name =
                    "PendulumSupports",
                Position =
                    new Vector3(
                        0.0f,
                        0.0f,
                        localZ)
            };
        array.SetMeta(
            "reserve_obstacle_z",
            true);
        sectionRoot.AddChild(
            array);
        StandardMaterial3D bodyMaterial =
            CreateStructureMaterial(
                color,
                0.5f);
        StandardMaterial3D warning =
            CreateObstacleMaterial(
                new Color(
                    1.0f,
                    0.54f,
                    0.08f),
                1.6f);
        for (int supportIndex = 0;
            supportIndex < 3;
            supportIndex++)
        {
            float z =
                5.8f -
                supportIndex *
                5.8f;
            float phase =
                supportIndex *
                Mathf.Tau /
                3.0f;
            OscillatingObstacle support =
                new()
                {
                    Name =
                        $"SwingSupport{supportIndex + 1}",
                    Position =
                        new Vector3(
                            0.0f,
                            0.0f,
                            z),
                    CollisionLayer =
                        ObstacleCollisionLayer,
                    CollisionMask = 0,
                    LocalAxis =
                        Vector3.Right,
                    Amplitude =
                        sectionWidth *
                        0.27f,
                    Frequency =
                        0.24f +
                        supportIndex *
                        0.025f,
                    Phase = phase
                };
            support.AddToGroup(
                "mob_obstacle");
            support.SetMeta(
                "avoidance_half_extents",
                new Vector2(
                    1.1f,
                    1.0f));
            array.AddChild(
                support);
            Vector3 size =
                new(
                    2.0f,
                    6.2f,
                    1.8f);
            support.AddChild(
                new CollisionShape3D
                {
                    Position =
                        Vector3.Up *
                        size.Y *
                        0.5f,
                    Shape =
                        new BoxShape3D
                        {
                            Size = size
                        }
                });
            AddVisualBox(
                support,
                Vector3.Up *
                    size.Y *
                    0.5f,
                size,
                bodyMaterial);
            AddVisualBox(
                support,
                new Vector3(
                    0.0f,
                    1.1f,
                    size.Z *
                        0.52f),
                new Vector3(
                    size.X * 0.72f,
                    1.4f,
                    0.07f),
                warning);

            AddVisualBox(
                array,
                new Vector3(
                    0.0f,
                    0.045f,
                    z),
                new Vector3(
                    sectionWidth * 0.68f,
                    0.055f,
                    0.12f),
                warning);
        }
        AddTraversalBoostZone(
            array,
            "SwingArrayExitBoost",
            new Vector3(
                0.0f,
                1.4f,
                -8.2f),
            new Vector3(
                sectionWidth - 2.0f,
                2.8f,
                2.2f),
            false,
            2.65f,
            0.9f);
    }

    private void CreateConstructionRelay(
        Node3D sectionRoot,
        float triggerZ,
        float width,
        Vector3[] relayPositions,
        EnemyArchetypeId archetype)
    {
        TraversalRelayTrigger trigger =
            new()
            {
                Name =
                    "ConstructionRelayTrigger",
                CollisionLayer = 0,
                CollisionMask = 1
            };
        trigger.Configure(
            relayPositions);
        trigger.EnemyRequested +=
            (
                Vector3 worldPosition,
                Vector3 direction,
                int relayIndex) =>
            {
                EmitSignal(
                    SignalName
                        .ConstructionEnemyRequested,
                    worldPosition,
                    direction,
                    relayIndex,
                    (int)archetype);
            };
        trigger.AddChild(
            new CollisionShape3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        1.5f,
                        triggerZ),
                Shape =
                    new BoxShape3D
                    {
                        Size =
                            new Vector3(
                                width * 0.88f,
                                3.0f,
                                4.0f)
                    }
            });
        sectionRoot.AddChild(
            trigger);
    }

    private static void AddTraversalBoostZone(
        Node3D parent,
        string name,
        Vector3 position,
        Vector3 size,
        bool requiresAirbornePlayer,
        float speedBoost,
        float duration)
    {
        TraversalBoostZone zone = new()
        {
            Name = name,
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 1,
            RequiresAirbornePlayer = requiresAirbornePlayer,
            SpeedBoost = speedBoost,
            BoostDuration = duration
        };
        zone.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size }
        });
        parent.AddChild(zone);
    }

    private static StaticBody3D CreateObstacleBody(
        Node3D sectionRoot,
        string name,
        Vector3 position,
        Vector2 horizontalHalfExtents)
    {
        StaticBody3D body = new()
        {
            Name = name,
            Position = position,
            CollisionLayer = ObstacleCollisionLayer,
            CollisionMask = 0
        };
        body.AddToGroup("mob_obstacle");
        body.SetMeta("avoidance_half_extents", horizontalHalfExtents);
        sectionRoot.AddChild(body);
        return body;
    }

    private static void AddObstacleBox(
        StaticBody3D body,
        Vector3 localPosition,
        Vector3 size,
        Material material)
    {
        body.AddChild(new CollisionShape3D
        {
            Position = localPosition,
            Shape = new BoxShape3D { Size = size }
        });
        AddVisualBox(body, localPosition, size, material);
    }

    private static void AddVisualBox(
        Node3D parent,
        Vector3 localPosition,
        Vector3 size,
        Material material)
    {
        parent.AddChild(new MeshInstance3D
        {
            Position = localPosition,
            Mesh = new BoxMesh
            {
                Size = size,
                Material = material
            }
        });
    }

    private static void AddVisualBoxRotated(
        Node3D parent,
        Vector3 localPosition,
        Vector3 size,
        Vector3 rotation,
        Material material,
        string name = "ObstacleDetail")
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = localPosition,
            Rotation = rotation,
            Mesh = new BoxMesh
            {
                Size = size,
                Material = material
            }
        });
    }

    private static void AddVisualCylinder(
        Node3D parent,
        Vector3 localPosition,
        float topRadius,
        float bottomRadius,
        float height,
        Material material,
        string name = "ObstacleDetail")
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = localPosition,
            Mesh = new CylinderMesh
            {
                TopRadius = topRadius,
                BottomRadius = bottomRadius,
                Height = height,
                RadialSegments = 32,
                Material = material
            }
        });
    }

    private static void AddVisualTorus(
        Node3D parent,
        Vector3 localPosition,
        float innerRadius,
        float outerRadius,
        Material material,
        string name = "ObstacleDetail")
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = localPosition,
            Mesh = new TorusMesh
            {
                InnerRadius = innerRadius,
                OuterRadius = outerRadius,
                Rings = 36,
                RingSegments = 8,
                Material = material
            }
        });
    }

    private static ShaderMaterial CreateRelayWaterMaterial()
    {
        if (_relayWaterMaterial != null)
        {
            return _relayWaterMaterial;
        }

        Shader shader = GD.Load<Shader>("res://shaders/traversal_water.gdshader");
        _relayWaterMaterial =
            new ShaderMaterial
            {
                Shader = shader
            };
        return _relayWaterMaterial;
    }

    private static ShaderMaterial
        CreateRunSurfaceMaterial(
            Color themeColor,
            bool arena = false)
    {
        _runSurfaceShader ??=
            GD.Load<Shader>(
                RunSurfaceShaderPath);

        ulong cacheKey =
            MaterialCacheKey(
                themeColor,
                arena ? 1.0f : 0.0f);
        if (RunSurfaceMaterialCache.TryGetValue(
                cacheKey,
                out ShaderMaterial cachedMaterial))
        {
            return cachedMaterial;
        }

        Color neutral =
            new(
                0.52f,
                0.58f,
                0.6f,
                1.0f);
        Color opaqueTheme =
            new(
                themeColor.R,
                themeColor.G,
                themeColor.B,
                1.0f);
        Color baseColor =
            neutral.Lerp(
                opaqueTheme,
                arena
                    ? 0.13f
                    : 0.075f);
        Color accent =
            opaqueTheme.Lightened(
                arena
                    ? 0.48f
                    : 0.58f);
        ShaderMaterial material =
            new()
            {
                Shader =
                    _runSurfaceShader
            };
        material.SetShaderParameter(
            "base_color",
            baseColor);
        material.SetShaderParameter(
            "accent_color",
            accent);
        material.SetShaderParameter(
            "arena_mix",
            arena
                ? 1.0f
                : 0.0f);
        RunSurfaceMaterialCache[cacheKey] =
            material;
        return material;
    }

    private static StandardMaterial3D CreateStructureMaterial(
        Color themeColor,
        float themeWeight = 0.36f)
    {
        ulong cacheKey =
            MaterialCacheKey(
                themeColor,
                themeWeight);
        if (StructureMaterialCache.TryGetValue(
                cacheKey,
                out StandardMaterial3D cachedMaterial))
        {
            return cachedMaterial;
        }

        Color neutral =
            new(
                0.68f,
                0.72f,
                0.72f,
                1.0f);
        Color bodyColor =
            neutral.Lerp(
                new Color(
                    themeColor.R,
                    themeColor.G,
                    themeColor.B,
                    1.0f),
                Mathf.Clamp(
                    themeWeight *
                        0.34f,
                    0.0f,
                    0.24f));
        StandardMaterial3D material =
            new()
            {
                AlbedoColor = bodyColor,
                Metallic = 0.06f,
                Roughness = 0.68f,
                EmissionEnabled = false
            };
        StructureMaterialCache[cacheKey] =
            material;
        return material;
    }

    private static StandardMaterial3D CreateObstacleMaterial(
        Color color,
        float emissionEnergy)
    {
        ulong cacheKey =
            MaterialCacheKey(
                color,
                emissionEnergy);
        if (ObstacleMaterialCache.TryGetValue(
                cacheKey,
                out StandardMaterial3D cachedMaterial))
        {
            return cachedMaterial;
        }

        Color cleanColor =
            color.Lerp(
                new Color(
                    0.92f,
                    0.94f,
                    0.92f,
                    color.A),
                0.24f);
        StandardMaterial3D material =
            new()
            {
                AlbedoColor =
                    cleanColor,
                Metallic = 0.04f,
                Roughness = 0.62f,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier =
                    emissionEnergy * 0.16f +
                    0.012f
            };
        ObstacleMaterialCache[cacheKey] =
            material;
        return material;
    }

    private static ulong MaterialCacheKey(
        Color color,
        float variant)
    {
        return
            ((ulong)color.ToRgba32() << 32) |
            (uint)Mathf.RoundToInt(
                variant * 1000.0f);
    }

    private WorldSectionRuntime FindSection(
        float playerZ)
    {
        return _sectionStream.FindAtZ(playerZ);
    }

#if false
    public float GetPlayableHalfWidth(float playerZ)
    {
        return GetPlayableHalfWidth(
            playerZ,
            0.0f);
    }

    public float GetPlayableHalfWidth(
        float playerZ,
        float preferredX)
    {
        WorldSectionRuntime section =
            FindSection(playerZ);
        if (section == null)
        {
            return _travelSectionWidth *
                0.5f;
        }

        if (section.Shape !=
            RouteShape.Fork)
        {
            return section.Width *
                0.5f;
        }

        float progress =
            GetSectionProgress(
                section,
                playerZ);
        float separation =
            EvaluateForkSeparation(
                progress,
                section.BranchOffset);
        return Mathf.Abs(preferredX) >
                1.0f &&
            separation > 2.2f
                ? section.BranchLaneWidth *
                    0.5f
                : separation +
                    section.BranchLaneWidth *
                    0.5f;
    }

    public float GetRouteCenterX(
        float playerZ,
        float preferredX = 0.0f)
    {
        WorldSectionRuntime section =
            FindSection(playerZ);
        if (section == null ||
            section.IsArena)
        {
            return 0.0f;
        }

        float progress =
            GetSectionProgress(
                section,
                playerZ);
        if (section.Shape ==
            RouteShape.Curve)
        {
            return EvaluateCurveOffset(
                progress,
                section.CurveAmplitude);
        }

        if (section.Shape ==
            RouteShape.Fork &&
            Mathf.Abs(preferredX) > 1.0f)
        {
            return Mathf.Sign(preferredX) *
                EvaluateForkSeparation(
                    progress,
                    section.BranchOffset);
        }

        return 0.0f;
    }

    public bool IsRouteFork(float playerZ)
    {
        return FindSection(playerZ)?.Shape ==
            RouteShape.Fork;
    }

    private static float GetSectionProgress(
        WorldSectionRuntime section,
        float playerZ)
    {
        float length =
            Mathf.Max(
                0.001f,
                section.SouthZ -
                    section.NorthZ);
        return Mathf.Clamp(
            (section.SouthZ - playerZ) /
                length,
            0.0f,
            1.0f);
    }

#endif

    public float GetPlayableHalfWidth(float playerZ)
    {
        ApplyGenerationSettings();
        WorldSectionRuntime section =
            FindSection(playerZ);
        return section == null
            ? _travelSectionWidth * 0.5f
            : section.Width * 0.5f;
    }

    public bool ContainsActiveArenaPoint(Vector3 point, float margin = 0) =>
        _activeArena?.ArenaLayout == null || _activeArena.ArenaLayout.Contains(new Vector2(point.X - _activeArena.Root.GlobalPosition.X, point.Z - _activeArena.Root.GlobalPosition.Z), margin);
    public Vector3 ClampActiveArenaPoint(Vector3 point, float margin = 0) =>
        _activeArena?.ArenaLayout == null ? point : _activeArena.ArenaLayout.ClampWorld(point, _activeArena.Root.GlobalPosition, margin);

    public bool TryGetArenaLayout(int sectionIndex, out ArenaLayout layout, out Vector3 center)
    {
        var section = FindSectionByIndex(sectionIndex);
        layout = section?.ArenaLayout;
        center = section != null && IsInstanceValid(section.Root) ? section.Root.GlobalPosition : Vector3.Zero;
        center.Y = 0;
        return layout != null;
    }

    public bool TryGetArenaCircle(
        int sectionIndex,
        out Vector3 center,
        out float radius)
    {
        WorldSectionRuntime section =
            FindSectionByIndex(sectionIndex);
        if (section == null ||
            !section.IsArena ||
            section.ArenaRadius <= 0.0f ||
            !IsInstanceValid(section.Root))
        {
            center = Vector3.Zero;
            radius = 0.0f;
            return false;
        }

        center = section.Root.GlobalPosition;
        center.Y = 0.0f;
        radius = section.ArenaRadius;
        return true;
    }

    private WorldSectionRuntime FindSectionByIndex(
        int sectionIndex)
    {
        return _sectionStream.FindByIndex(
            sectionIndex);
    }

    private void RemoveSectionsFarBehind(float playerZ)
    {
        _sectionStream.PruneBehind(
            playerZ,
            _retainBehindDistance,
            _activeArena,
            CurrentSectionIndex);
    }
}
