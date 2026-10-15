using Godot;
using System.Collections.Generic;

/// <summary>
/// Owns generated-section lookup and release policy independently from how
/// those sections were planned or assembled.
/// </summary>
public sealed class WorldSectionStream
{
    private readonly List<WorldSectionRuntime>
        _sections = new();

    public IReadOnlyList<WorldSectionRuntime> Sections =>
        _sections;

    public int Count => _sections.Count;

    public void Add(WorldSectionRuntime section)
    {
        if (section != null)
        {
            _sections.Add(section);
        }
    }

    public WorldSectionRuntime FindAtZ(float playerZ)
    {
        foreach (WorldSectionRuntime section in
            _sections)
        {
            if (playerZ <= section.SouthZ &&
                playerZ >= section.NorthZ)
            {
                return section;
            }
        }

        return null;
    }

    public WorldSectionRuntime FindByIndex(
        int sectionIndex)
    {
        foreach (WorldSectionRuntime section in
            _sections)
        {
            if (section.Index == sectionIndex)
            {
                return section;
            }
        }

        return null;
    }

    public WorldSectionRuntime FindArenaByOrdinal(
        int arenaOrdinal)
    {
        foreach (WorldSectionRuntime section in
            _sections)
        {
            if (section.IsArena &&
                section.ArenaOrdinal ==
                    arenaOrdinal)
            {
                return section;
            }
        }

        return null;
    }

    public int PruneBehind(
        float playerZ,
        float retainBehindDistance,
        WorldSectionRuntime activeArena,
        int currentSectionIndex)
    {
        int removed = 0;
        for (int index = _sections.Count - 1;
            index >= 0;
            index--)
        {
            WorldSectionRuntime section =
                _sections[index];
            if (section == activeArena ||
                section.Index ==
                    currentSectionIndex ||
                section.NorthZ <=
                    playerZ +
                        retainBehindDistance)
            {
                continue;
            }

            if (GodotObject.IsInstanceValid(
                    section.Root))
            {
                section.Root.QueueFree();
            }

            _sections.RemoveAt(index);
            removed++;
        }

        return removed;
    }
}
