using Godot;
using System;
using System.Collections.Generic;

public partial class WorldContentContractProbe : Node
{
    private int _failures;

    public override async void _Ready()
    {
        WorldContentCatalog catalog =
            GD.Load<WorldContentCatalog>(
                "res://world/content/reference_catalog.tres");
        Check(
            catalog != null &&
            catalog.Validate(out _) &&
            catalog.Chunks.Count == 4 &&
            catalog.Obstacles.Count == 1,
            "typed catalog loads the vertical-slice chunks and one obstacle");
        if (catalog == null)
        {
            Finish();
            return;
        }

        Dictionary<string, WorldChunkDefinition>
            definitions = new();
        List<WorldChunk> chunks = new();
        foreach (WorldChunkDefinition definition in
            catalog.Chunks)
        {
            WorldChunk chunk =
                definition.Scene
                    .Instantiate<WorldChunk>();
            AddChild(chunk);
            definitions[definition.ContentId] =
                definition;
            chunks.Add(chunk);
            Check(
                definition.Validate(out _) &&
                chunk.ValidateContract(out _) &&
                chunk.Matches(definition),
                $"{definition.ContentId} scene matches its typed definition");
            Check(
                HasOnlyContractScripts(chunk) &&
                HasValidCollisionOwnership(chunk),
                $"{definition.ContentId} has authored collision and no forbidden gameplay scripts");
        }

        WorldChunkDefinition travel =
            definitions["reference_travel"];
        WorldChunkDefinition connector =
            definitions["reference_connector"];
        WorldChunkDefinition arena =
            definitions["reference_arena"];
        WorldChunkDefinition exit =
            definitions[
                "reference_exit_connector"];
        Check(
            travel.CanPrecede(connector, 0) &&
            connector.CanPrecede(arena, 0) &&
            arena.CanPrecede(exit, 0) &&
            !travel.CanPrecede(arena, 0),
            "compatibility tags admit the complete travel-to-connector-to-arena-to-exit chain");

        WorldChunk travelChunk =
            chunks.Find(
                chunk => chunk.ContentId ==
                    "reference_travel");
        WorldChunk connectorChunk =
            chunks.Find(
                chunk => chunk.ContentId ==
                    "reference_connector");
        connectorChunk.Position =
            travelChunk.Exit.GlobalPosition -
            connectorChunk.Entry.Position;
        Check(
            connectorChunk.Entry.GlobalPosition
                .IsEqualApprox(
                    travelChunk.Exit.GlobalPosition),
            "authored entry and exit sockets stitch without internal model paths");

        WorldObstacleDefinition obstacleDefinition =
            catalog.Obstacles[0];
        WorldObstacle firstObstacle =
            obstacleDefinition.Scene
                .Instantiate<WorldObstacle>();
        WorldObstacle secondObstacle =
            obstacleDefinition.Scene
                .Instantiate<WorldObstacle>();
        AddChild(firstObstacle);
        AddChild(secondObstacle);
        StandardMaterial3D firstMaterial =
            firstObstacle.MutableSurfaceMaterial;
        StandardMaterial3D secondMaterial =
            secondObstacle.MutableSurfaceMaterial;
        Color secondColor =
            secondMaterial?.AlbedoColor ??
            Colors.Black;
        if (firstMaterial != null)
        {
            firstMaterial.AlbedoColor =
                Colors.Magenta;
        }
        Check(
            obstacleDefinition.Validate(out _) &&
            firstObstacle.ValidateContract(out _) &&
            firstObstacle.Matches(
                obstacleDefinition) &&
            obstacleDefinition.Supports(
                WorldChunkKind.Travel,
                0) &&
            !obstacleDefinition.Supports(
                WorldChunkKind.Arena,
                0),
            "obstacle definition owns footprint, tags and placement restrictions");
        Check(
            firstMaterial != null &&
            secondMaterial != null &&
            !ReferenceEquals(
                firstMaterial,
                secondMaterial) &&
            secondMaterial.AlbedoColor
                .IsEqualApprox(secondColor) &&
            HasOnlyContractScripts(
                firstObstacle) &&
            HasValidCollisionOwnership(
                firstObstacle),
            "mutable obstacle materials are instance-local and collision is authored");

        foreach (WorldChunk chunk in chunks)
        {
            chunk.QueueFree();
        }
        firstObstacle.QueueFree();
        secondObstacle.QueueFree();
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        Finish();
    }

    private static bool HasOnlyContractScripts(
        Node root)
    {
        if (root is Main ||
            root is Player ||
            root is Mob ||
            root is ProceduralWorld ||
            root is EncounterDirector)
        {
            return false;
        }
        foreach (Node child in root.GetChildren())
        {
            if (child.GetScript()
                    .VariantType !=
                Variant.Type.Nil ||
                !HasOnlyContractScripts(child))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasValidCollisionOwnership(
        Node root)
    {
        bool found = false;
        return Visit(root);

        bool Visit(Node node)
        {
            if (node is CollisionShape3D shape)
            {
                if (shape.Shape == null ||
                    shape.GetParent() is not
                        CollisionObject3D)
                {
                    return false;
                }
                found = true;
            }
            foreach (Node child in
                node.GetChildren())
            {
                if (!Visit(child))
                {
                    return false;
                }
            }
            return node != root || found;
        }
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
                ? "WORLD_CONTENT_CONTRACT_PROBE: PASSED"
                : $"WORLD_CONTENT_CONTRACT_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }
}
