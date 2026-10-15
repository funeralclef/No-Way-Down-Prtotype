using Godot;
using System.Linq;

public partial class HybridWorldVisualReview : Node3D
{
    private const ulong HybridSeed = 240629UL;
    private const ulong LegacySeed = 73013UL;

    public override void _Ready()
    {
        WorldContentCatalog catalog =
            GD.Load<WorldContentCatalog>(
                "res://world/content/reference_catalog.tres");
        ProceduralWorld world = new()
        {
            Name = "HybridWorld",
            GenerationSettings =
                new WorldGenerationSettings
                {
                    MinTravelLength = 80.0f,
                    MaxTravelLength = 88.0f,
                    ArenaLength = 78.0f,
                    TravelSectionWidth = 36.0f,
                    GenerationAheadDistance =
                        180.0f,
                    RetainBehindDistance =
                        120.0f
                },
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
        world.TryGetArenaSpan(
            3,
            out _,
            out _,
            out _);

        var authored =
            world.GeneratedSections
                .Where(
                    section =>
                        section.IsAuthored)
                .ToList();
        foreach (WorldSectionRuntime section in
            world.GeneratedSections)
        {
            if (!section.IsAuthored &&
                IsInstanceValid(section.Root))
            {
                section.Root.Visible = false;
            }
        }
        if (authored.Count == 0)
        {
            return;
        }

        float south = authored[0].SouthZ;
        float north = authored[^1].NorthZ;
        float centerZ =
            (south + north) * 0.5f;
        GetNode<Camera3D>("Camera3D")
            .LookAtFromPosition(
                new Vector3(
                    82.0f,
                    72.0f,
                    centerZ + 55.0f),
                new Vector3(
                    0.0f,
                    0.0f,
                    centerZ - 8.0f),
                Vector3.Up);
        GD.Print(
            "HYBRID_WORLD_VISUAL_REVIEW: READY " +
            world.HybridRuntimeFingerprint);
    }
}
