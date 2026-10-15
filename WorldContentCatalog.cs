using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WorldContentCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<WorldChunkDefinition>
        Chunks { get; set; } = new();

    [Export]
    public Godot.Collections.Array<WorldObstacleDefinition>
        Obstacles { get; set; } = new();

    public bool Validate(out string error)
    {
        HashSet<string> ids = new();
        foreach (WorldChunkDefinition chunk in
            Chunks)
        {
            if (chunk == null)
            {
                error = "Catalog contains a null chunk definition.";
                return false;
            }
            if (!chunk.Validate(out error))
            {
                return false;
            }
            if (!ids.Add(chunk.ContentId))
            {
                error = $"Duplicate content id: {chunk.ContentId}.";
                return false;
            }
        }
        foreach (WorldObstacleDefinition obstacle in
            Obstacles)
        {
            if (obstacle == null)
            {
                error = "Catalog contains a null obstacle definition.";
                return false;
            }
            if (!obstacle.Validate(out error))
            {
                return false;
            }
            if (!ids.Add(obstacle.ContentId))
            {
                error = $"Duplicate content id: {obstacle.ContentId}.";
                return false;
            }
        }
        if (Chunks.Count == 0 ||
            Obstacles.Count == 0)
        {
            error = "Catalog requires chunks and obstacles.";
            return false;
        }

        error = "";
        return true;
    }
}
