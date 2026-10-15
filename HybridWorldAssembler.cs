using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AuthoredWorldChunkAssembly
{
    public WorldChunk Chunk { get; init; }
    public WorldChunkDefinition Definition { get; init; }
    public Transform3D EntryTransform { get; init; }
    public Transform3D ExitTransform { get; init; }
    public float SouthZ { get; init; }
    public float NorthZ { get; init; }
    public float Width { get; init; }
    public float ArenaRadius { get; init; }
    public int ObstacleCount { get; init; }
    public IReadOnlyList<string> ObstacleIds
        { get; init; }
    public string Fingerprint { get; init; }
}

/// <summary>
/// Instantiates authored chunks, aligns their public sockets and decorates
/// typed obstacle slots. It does not own section streaming or encounters.
/// </summary>
public sealed class HybridWorldAssembler
{
    private const float SocketTolerance = 0.01f;

    public bool TryValidatePlan(
        Node3D validationOwner,
        HybridWorldPlan plan,
        Godot.Collections.Array<WorldObstacleDefinition>
            obstacleDefinitions,
        float maximumDifficulty,
        out string error)
    {
        if (!GodotObject.IsInstanceValid(
                validationOwner) ||
            !validationOwner.IsInsideTree() ||
            plan == null)
        {
            error =
                "Hybrid validation requires an in-tree owner and a plan.";
            return false;
        }

        Node3D validationRoot = new()
        {
            Name = "HybridPlanValidation"
        };
        validationOwner.AddChild(
            validationRoot);
        Transform3D socket =
            Transform3D.Identity;
        for (int index = 0;
            index < plan.Entries.Count;
            index++)
        {
            if (!TryAssemble(
                    validationRoot,
                    plan.Entries[index],
                    socket,
                    obstacleDefinitions,
                    plan.Seed,
                    plan.Chapter,
                    maximumDifficulty,
                    index,
                    out AuthoredWorldChunkAssembly
                        assembly,
                    out error))
            {
                validationRoot.Free();
                return false;
            }
            socket = assembly.ExitTransform;
        }

        validationRoot.Free();
        error = "";
        return true;
    }

    public bool TryAssemble(
        Node3D owner,
        HybridWorldPlanEntry entry,
        Transform3D targetEntry,
        Godot.Collections.Array<WorldObstacleDefinition>
            obstacleDefinitions,
        ulong generationSeed,
        int chapter,
        float maximumDifficulty,
        int sectionIndex,
        out AuthoredWorldChunkAssembly assembly,
        out string error)
    {
        assembly = null;
        if (!GodotObject.IsInstanceValid(owner) ||
            entry?.Definition?.Scene == null)
        {
            error =
                "Assembler requires an owner and a valid chunk definition.";
            return false;
        }

        Node instance =
            entry.Definition.Scene.Instantiate();
        if (instance is not WorldChunk chunk)
        {
            instance?.Free();
            error =
                $"{entry.Definition.ContentId} does not instantiate a WorldChunk root.";
            return false;
        }
        if (!chunk.ValidateContract(out error) ||
            !chunk.Matches(entry.Definition))
        {
            chunk.Free();
            error = string.IsNullOrWhiteSpace(error)
                ? $"{entry.Definition.ContentId} scene and definition do not match."
                : error;
            return false;
        }

        chunk.Name = $"Section{sectionIndex}";
        chunk.AddToGroup("world_section");
        owner.AddChild(chunk);
        chunk.GlobalTransform =
            targetEntry *
            chunk.Entry.Transform.AffineInverse();
        if (chunk.Entry.GlobalPosition.DistanceTo(
                targetEntry.Origin) >
            SocketTolerance)
        {
            ReleaseFailedChunk(owner, chunk);
            error =
                $"{entry.Definition.ContentId} entry socket could not be aligned.";
            return false;
        }

        List<string> obstacleIds = new();
        if (!TryPopulateObstacleSlots(
                chunk,
                obstacleDefinitions,
                generationSeed,
                chapter,
                maximumDifficulty,
                sectionIndex,
                obstacleIds,
                out error))
        {
            ReleaseFailedChunk(owner, chunk);
            return false;
        }

        Transform3D entryTransform =
            chunk.Entry.GlobalTransform;
        Transform3D exitTransform =
            chunk.Exit.GlobalTransform;
        float southZ = entryTransform.Origin.Z;
        float northZ = exitTransform.Origin.Z;
        if (northZ >= southZ -
                SocketTolerance)
        {
            ReleaseFailedChunk(owner, chunk);
            error =
                $"{entry.Definition.ContentId} does not advance along world -Z.";
            return false;
        }

        float arenaRadius =
            chunk.Kind == WorldChunkKind.Arena
                ? Mathf.Max(
                    4.0f,
                    Mathf.Min(
                        chunk.PlayableSize.X,
                        chunk.PlayableSize.Z) *
                    0.5f - 3.0f)
                : 0.0f;
        string obstacleRecipe = string.Join(
            ",",
            obstacleIds);
        string fingerprint =
            WorldGenerationFingerprint.Compute(
                $"{entry.Order}|" +
                $"{entry.Definition.ContentId}|" +
                $"{southZ:F3}>{northZ:F3}|" +
                obstacleRecipe);
        assembly = new AuthoredWorldChunkAssembly
        {
            Chunk = chunk,
            Definition = entry.Definition,
            EntryTransform = entryTransform,
            ExitTransform = exitTransform,
            SouthZ = southZ,
            NorthZ = northZ,
            Width = chunk.PlayableSize.X,
            ArenaRadius = arenaRadius,
            ObstacleCount = obstacleIds.Count,
            ObstacleIds = obstacleIds,
            Fingerprint = fingerprint
        };
        error = "";
        return true;
    }

    private static bool TryPopulateObstacleSlots(
        WorldChunk chunk,
        Godot.Collections.Array<WorldObstacleDefinition>
            obstacleDefinitions,
        ulong generationSeed,
        int chapter,
        float maximumDifficulty,
        int sectionIndex,
        List<string> obstacleIds,
        out string error)
    {
        ulong slotSeed =
            generationSeed ^
            WorldGenerationFingerprint.ComputeValue(
                $"{chunk.ContentId}|{sectionIndex}");
        RandomNumberGenerator random = new()
        {
            Seed = slotSeed
        };

        foreach (Node child in
            chunk.ObstacleSlots.GetChildren())
        {
            if (child is not Marker3D slot)
            {
                error =
                    $"{chunk.ContentId} obstacle slot {child.Name} is not a Marker3D.";
                return false;
            }

            float spawnChance = slot.HasMeta(
                    "spawn_chance")
                ? (float)slot.GetMeta(
                    "spawn_chance").AsDouble()
                : 1.0f;
            if (spawnChance <= 0.0f ||
                random.Randf() > spawnChance)
            {
                continue;
            }

            WorldObstacleFootprint maxFootprint =
                slot.HasMeta("max_footprint")
                    ? (WorldObstacleFootprint)
                        slot.GetMeta(
                            "max_footprint")
                            .AsInt32()
                    : WorldObstacleFootprint
                        .LaneWide;
            WorldObstacleTag requiredTags =
                slot.HasMeta(
                    "required_obstacle_tags")
                    ? (WorldObstacleTag)
                        slot.GetMeta(
                            "required_obstacle_tags")
                            .AsInt32()
                    : WorldObstacleTag.None;
            List<WorldObstacleDefinition>
                candidates =
                obstacleDefinitions
                    .Where(
                        definition =>
                            definition != null &&
                            definition.Supports(
                                chunk.Kind,
                                chapter) &&
                            definition.BaseDifficulty <=
                                maximumDifficulty &&
                            definition.Footprint <=
                                maxFootprint &&
                            (requiredTags ==
                                    WorldObstacleTag.None ||
                                (definition.Tags &
                                    requiredTags) ==
                                    requiredTags) &&
                            (definition.Theme ==
                                    WorldContentTheme.Universal ||
                                chunk.Theme ==
                                    WorldContentTheme.Universal ||
                                definition.Theme ==
                                    chunk.Theme))
                    .OrderBy(
                        definition =>
                            definition.ContentId,
                        StringComparer.Ordinal)
                    .ToList();
            if (candidates.Count == 0)
            {
                if (requiredTags ==
                    WorldObstacleTag.None)
                {
                    continue;
                }

                error =
                    $"{chunk.ContentId}/{slot.Name} has no compatible obstacle.";
                return false;
            }

            WorldObstacleDefinition definition =
                ChooseWeighted(
                    candidates,
                    random);
            Node obstacleInstance =
                definition.Scene.Instantiate();
            if (obstacleInstance is not
                WorldObstacle obstacle)
            {
                obstacleInstance?.Free();
                error =
                    $"{definition.ContentId} does not instantiate a WorldObstacle root.";
                return false;
            }
            if (!obstacle.ValidateContract(
                    out string obstacleError) ||
                !obstacle.Matches(definition))
            {
                obstacle.Free();
                error = string.IsNullOrWhiteSpace(
                        obstacleError)
                    ? $"{definition.ContentId} obstacle contract mismatch."
                    : obstacleError;
                return false;
            }
            if (obstacle.FootprintSize.X >
                    chunk.PlayableSize.X - 1.0f ||
                obstacle.FootprintSize.Z >
                    chunk.PlayableSize.Z)
            {
                obstacle.Free();
                error =
                    $"{definition.ContentId} does not fit {chunk.ContentId}/{slot.Name}.";
                return false;
            }

            obstacle.Name =
                $"Obstacle_{slot.Name}_" +
                definition.ContentId;
            slot.AddChild(obstacle);
            obstacleIds.Add(
                $"{slot.Name}:{definition.ContentId}");
        }

        error = "";
        return true;
    }

    private static WorldObstacleDefinition
        ChooseWeighted(
            IReadOnlyList<WorldObstacleDefinition>
                candidates,
            RandomNumberGenerator random)
    {
        float total = candidates.Sum(
            candidate =>
                Mathf.Max(
                    0.001f,
                    candidate.SelectionWeight));
        float roll = random.RandfRange(
            0.0f,
            total);
        foreach (WorldObstacleDefinition candidate in
            candidates)
        {
            roll -= Mathf.Max(
                0.001f,
                candidate.SelectionWeight);
            if (roll <= 0.0f)
            {
                return candidate;
            }
        }
        return candidates[^1];
    }

    private static void ReleaseFailedChunk(
        Node3D owner,
        WorldChunk chunk)
    {
        owner.RemoveChild(chunk);
        chunk.Free();
    }
}
