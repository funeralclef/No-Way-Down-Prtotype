using Godot;

/// <summary>
/// Immutable authored metadata used to select and connect one location scene.
/// Per-run state belongs to ProceduralWorld, never to this Resource.
/// </summary>
[GlobalClass]
public partial class WorldChunkDefinition : Resource
{
    [Export]
    public string ContentId { get; set; } = "";

    [Export]
    public PackedScene Scene { get; set; }

    [Export]
    public WorldChunkKind Kind { get; set; }

    [Export]
    public WorldContentTheme Theme { get; set; } =
        WorldContentTheme.Universal;

    [Export]
    public WorldWidthClass WidthClass { get; set; } =
        WorldWidthClass.Standard;

    [Export]
    public WorldSocketProfile EntrySocket { get; set; } =
        WorldSocketProfile.Standard;

    [Export]
    public WorldSocketProfile ExitSocket { get; set; } =
        WorldSocketProfile.Standard;

    [Export]
    public WorldChunkKindMask AllowedPreviousKinds { get; set; } =
        WorldChunkKindMask.Any;

    [Export]
    public WorldChunkKindMask AllowedNextKinds { get; set; } =
        WorldChunkKindMask.Any;

    [Export]
    public WorldChunkTag Tags { get; set; } =
        WorldChunkTag.None;

    [Export(PropertyHint.Range, "0,100,1")]
    public int MinimumChapter { get; set; }

    [Export(PropertyHint.Range, "0.0,20.0,0.1")]
    public float BaseDifficulty { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.01,100.0,0.01")]
    public float SelectionWeight { get; set; } = 1.0f;

    public bool CanPrecede(
        WorldChunkDefinition next,
        int chapter)
    {
        return next != null &&
            chapter >= MinimumChapter &&
            chapter >= next.MinimumChapter &&
            Includes(
                AllowedNextKinds,
                next.Kind) &&
            Includes(
                next.AllowedPreviousKinds,
                Kind) &&
            AreSocketsCompatible(
                ExitSocket,
                next.EntrySocket);
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
        if (SelectionWeight <= 0.0f)
        {
            error = $"{ContentId} requires a positive selection weight.";
            return false;
        }
        if (MinimumChapter < 0 ||
            BaseDifficulty < 0.0f)
        {
            error = $"{ContentId} has invalid progression metadata.";
            return false;
        }
        if (AllowedPreviousKinds ==
                WorldChunkKindMask.None ||
            AllowedNextKinds ==
                WorldChunkKindMask.None)
        {
            error = $"{ContentId} must allow at least one neighbour kind.";
            return false;
        }

        error = "";
        return true;
    }

    public static bool AreSocketsCompatible(
        WorldSocketProfile from,
        WorldSocketProfile to)
    {
        return from == WorldSocketProfile.Universal ||
            to == WorldSocketProfile.Universal ||
            from == to;
    }

    private static bool Includes(
        WorldChunkKindMask mask,
        WorldChunkKind kind)
    {
        WorldChunkKindMask value =
            (WorldChunkKindMask)(1 << (int)kind);
        return (mask & value) != 0;
    }
}
