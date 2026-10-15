using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class HybridWorldPlanEntry
{
    public HybridWorldPlanEntry(
        int order,
        WorldChunkDefinition definition)
    {
        Order = order;
        Definition = definition;
    }

    public int Order { get; }
    public WorldChunkDefinition Definition { get; }
}

public sealed class HybridWorldPlan
{
    public const int CurrentVersion = 1;

    public HybridWorldPlan(
        ulong seed,
        int chapter,
        IReadOnlyList<HybridWorldPlanEntry>
            entries)
    {
        Seed = seed;
        Chapter = chapter;
        Entries = entries;
        string recipe = string.Join(
            ">",
            entries.Select(
                entry =>
                    entry.Definition.ContentId));
        Fingerprint =
            WorldGenerationFingerprint.Compute(
                $"hybrid-v{CurrentVersion}|" +
                $"seed:{seed}|chapter:{chapter}|" +
                recipe);
    }

    public int Version => CurrentVersion;
    public ulong Seed { get; }
    public int Chapter { get; }
    public IReadOnlyList<HybridWorldPlanEntry>
        Entries { get; }
    public string Fingerprint { get; }
}

/// <summary>
/// Selects a compatible authored recipe without creating scene nodes.
/// </summary>
public sealed class HybridWorldPlanner
{
    private static readonly WorldChunkKind[]
        VerticalSliceKinds =
        {
            WorldChunkKind.Travel,
            WorldChunkKind.Connector,
            WorldChunkKind.Arena,
            WorldChunkKind.Connector
        };

    public bool TryPlanVerticalSlice(
        WorldContentCatalog catalog,
        ulong seed,
        int chapter,
        float maximumDifficulty,
        out HybridWorldPlan plan,
        out string error)
    {
        plan = null;
        error = "";
        if (catalog == null ||
            !catalog.Validate(out error))
        {
            error = string.IsNullOrWhiteSpace(error)
                ? "A valid world content catalog is required."
                : error;
            return false;
        }

        RandomNumberGenerator random = new()
        {
            Seed = seed
        };
        List<WorldChunkDefinition> selected =
            new();
        if (!TrySelect(
                catalog,
                random,
                chapter,
                maximumDifficulty,
                0,
                null,
                selected))
        {
            error =
                "Catalog cannot build a compatible travel > connector > arena > exit recipe.";
            return false;
        }

        List<HybridWorldPlanEntry> entries =
            new();
        for (int index = 0;
            index < selected.Count;
            index++)
        {
            entries.Add(
                new HybridWorldPlanEntry(
                    index,
                    selected[index]));
        }

        plan = new HybridWorldPlan(
            seed,
            chapter,
            entries);
        error = "";
        return true;
    }

    private static bool TrySelect(
        WorldContentCatalog catalog,
        RandomNumberGenerator random,
        int chapter,
        float maximumDifficulty,
        int order,
        WorldChunkDefinition previous,
        List<WorldChunkDefinition> selected)
    {
        if (order >= VerticalSliceKinds.Length)
        {
            return true;
        }

        WorldChunkKind requiredKind =
            VerticalSliceKinds[order];
        List<WorldChunkDefinition> candidates =
            catalog.Chunks
                .Where(
                    definition =>
                        definition != null &&
                        definition.Kind ==
                            requiredKind &&
                        chapter >=
                            definition.MinimumChapter &&
                        definition.BaseDifficulty <=
                            maximumDifficulty &&
                        (previous == null ||
                            previous.CanPrecede(
                                definition,
                                chapter)))
                .OrderBy(
                    definition =>
                        definition.ContentId,
                    StringComparer.Ordinal)
                .ToList();

        while (candidates.Count > 0)
        {
            WorldChunkDefinition choice =
                ChooseWeighted(
                    candidates,
                    random);
            candidates.Remove(choice);
            selected.Add(choice);
            if (TrySelect(
                    catalog,
                    random,
                    chapter,
                    maximumDifficulty,
                    order + 1,
                    choice,
                    selected))
            {
                return true;
            }
            selected.RemoveAt(
                selected.Count - 1);
        }

        return false;
    }

    private static WorldChunkDefinition
        ChooseWeighted(
            IReadOnlyList<WorldChunkDefinition>
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
        foreach (WorldChunkDefinition candidate in
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
}

public static class WorldGenerationFingerprint
{
    private const ulong Offset =
        14695981039346656037UL;
    private const ulong Prime =
        1099511628211UL;

    public static string Compute(string value)
    {
        return ComputeValue(value).ToString("x16");
    }

    public static ulong ComputeValue(string value)
    {
        ulong hash = Offset;
        foreach (char character in value ?? "")
        {
            hash ^= (byte)(character & 0xff);
            hash *= Prime;
            hash ^= (byte)(character >> 8);
            hash *= Prime;
        }
        return hash;
    }
}
