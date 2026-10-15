using Godot;

/// <summary>
/// Stable authored-scene boundary used by the future hybrid world assembler.
/// Geometry may change freely below Geometry; socket and metadata paths may not.
/// </summary>
[GlobalClass]
public partial class WorldChunk : Node3D
{
    public const int CurrentContractVersion = 1;

    [Export]
    public int ContractVersion { get; set; } =
        CurrentContractVersion;

    [Export]
    public string ContentId { get; set; } = "";

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
    public WorldChunkTag Tags { get; set; }

    [Export]
    public Vector3 PlayableSize { get; set; } =
        new(18.0f, 6.0f, 40.0f);

    [Export(PropertyHint.Range, "8.0,80.0,0.5")]
    public float CameraViewSize { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "0.0,4.0,0.05")]
    public float PressureSpeedMultiplier { get; set; } = 1.0f;

    public Marker3D Entry =>
        GetNodeOrNull<Marker3D>("Entry");

    public Marker3D Exit =>
        GetNodeOrNull<Marker3D>("Exit");

    public Marker3D PlayableBounds =>
        GetNodeOrNull<Marker3D>(
            "PlayableBounds");

    public Node3D ObstacleSlots =>
        GetNodeOrNull<Node3D>(
            "ObstacleSlots");

    public Node3D EnemySpawnAreas =>
        GetNodeOrNull<Node3D>(
            "EnemySpawnAreas");

    public bool ValidateContract(
        out string error)
    {
        if (ContractVersion !=
                CurrentContractVersion ||
            string.IsNullOrWhiteSpace(
                ContentId))
        {
            error = "Chunk id or contract version is invalid.";
            return false;
        }
        if (!IsInstanceValid(Entry) ||
            !IsInstanceValid(Exit) ||
            !IsInstanceValid(PlayableBounds) ||
            !IsInstanceValid(ObstacleSlots) ||
            !IsInstanceValid(EnemySpawnAreas))
        {
            error = $"{ContentId} is missing a required socket or bounds root.";
            return false;
        }
        if (Entry.Position.Z <=
                Exit.Position.Z ||
            Entry.Position.IsEqualApprox(
                Exit.Position))
        {
            error = $"{ContentId} sockets must follow local forward -Z.";
            return false;
        }
        if (PlayableSize.X <= 1.0f ||
            PlayableSize.Y <= 0.0f ||
            PlayableSize.Z <= 1.0f)
        {
            error = $"{ContentId} has invalid playable bounds.";
            return false;
        }
        if (ObstacleSlots.GetChildCount() == 0 ||
            EnemySpawnAreas.GetChildCount() == 0)
        {
            error = $"{ContentId} requires obstacle and enemy markers.";
            return false;
        }
        if (GetNodeOrNull<Node>(
                "Metadata/Camera") == null ||
            GetNodeOrNull<Node>(
                "Metadata/Pressure") == null)
        {
            error = $"{ContentId} requires camera and pressure metadata nodes.";
            return false;
        }
        if (!HasCollisionShape(this))
        {
            error = $"{ContentId} requires authored collision.";
            return false;
        }

        error = "";
        return true;
    }

    public bool Matches(
        WorldChunkDefinition definition)
    {
        return definition != null &&
            definition.ContentId == ContentId &&
            definition.Kind == Kind &&
            definition.Theme == Theme &&
            definition.WidthClass == WidthClass &&
            definition.EntrySocket == EntrySocket &&
            definition.ExitSocket == ExitSocket;
    }

    private static bool HasCollisionShape(
        Node root)
    {
        if (root is CollisionShape3D shape &&
            shape.Shape != null)
        {
            return true;
        }
        foreach (Node child in root.GetChildren())
        {
            if (HasCollisionShape(child))
            {
                return true;
            }
        }
        return false;
    }
}
