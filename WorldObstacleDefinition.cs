using Godot;

/// <summary>
/// Immutable catalog entry for an authored obstacle scene.
/// </summary>
[GlobalClass]
public partial class WorldObstacleDefinition : Resource
{
    [Export]
    public string ContentId { get; set; } = "";

    [Export]
    public PackedScene Scene { get; set; }

    [Export]
    public WorldContentTheme Theme { get; set; } =
        WorldContentTheme.Universal;

    [Export]
    public WorldObstacleFootprint Footprint { get; set; } =
        WorldObstacleFootprint.Medium;

    [Export]
    public WorldObstacleTag Tags { get; set; } =
        WorldObstacleTag.None;

    [Export]
    public WorldChunkKindMask AllowedChunkKinds { get; set; } =
        WorldChunkKindMask.Travel |
        WorldChunkKindMask.Connector;

    [Export(PropertyHint.Range, "0,100,1")]
    public int MinimumChapter { get; set; }

    [Export(PropertyHint.Range, "0.0,20.0,0.1")]
    public float BaseDifficulty { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.01,100.0,0.01")]
    public float SelectionWeight { get; set; } = 1.0f;

    public bool Supports(
        WorldChunkKind chunkKind,
        int chapter)
    {
        WorldChunkKindMask kind =
            (WorldChunkKindMask)(
                1 << (int)chunkKind);
        return chapter >= MinimumChapter &&
            (AllowedChunkKinds & kind) != 0;
    }

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(
                ContentId))
        {
            error = "ContentId is required.";
            return false;
        }
        if (Scene == null)
        {
            error = $"{ContentId} requires a PackedScene.";
            return false;
        }
        if (AllowedChunkKinds ==
                WorldChunkKindMask.None ||
            SelectionWeight <= 0.0f ||
            MinimumChapter < 0 ||
            BaseDifficulty < 0.0f)
        {
            error = $"{ContentId} has invalid selection metadata.";
            return false;
        }

        error = "";
        return true;
    }
}
