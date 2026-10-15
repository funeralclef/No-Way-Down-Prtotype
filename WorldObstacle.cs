using Godot;

[GlobalClass]
public partial class WorldObstacle : Node3D
{
    [Export]
    public string ContentId { get; set; } = "";

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
    public Vector3 FootprintSize { get; set; } =
        new(4.0f, 2.0f, 1.0f);

    [Export]
    public NodePath MutableVisualPath { get; set; } =
        new("Visual");

    public StandardMaterial3D MutableSurfaceMaterial
    {
        get
        {
            MeshInstance3D visual =
                GetNodeOrNull<MeshInstance3D>(
                    MutableVisualPath);
            return visual?.MaterialOverride as
                StandardMaterial3D;
        }
    }

    public bool ValidateContract(out string error)
    {
        if (string.IsNullOrWhiteSpace(
                ContentId) ||
            FootprintSize.X <= 0.0f ||
            FootprintSize.Y <= 0.0f ||
            FootprintSize.Z <= 0.0f)
        {
            error = "Obstacle id or footprint is invalid.";
            return false;
        }
        if (MutableSurfaceMaterial == null ||
            !MutableSurfaceMaterial.ResourceLocalToScene)
        {
            error = $"{ContentId} requires a scene-local mutable material.";
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
        WorldObstacleDefinition definition)
    {
        return definition != null &&
            definition.ContentId == ContentId &&
            definition.Theme == Theme &&
            definition.Footprint == Footprint &&
            definition.Tags == Tags;
    }

    private static bool HasCollisionShape(Node root)
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
