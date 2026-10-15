using Godot;

public enum BossId { Bellwarden, Thornmaw, Prismweaver }

/// <summary>Final guardians share the runner's stomp vocabulary; each teaches a different opening.</summary>
public sealed record BossDefinition(BossId Id, string Name, string Title, Color Color, string Lesson, string PhaseTwoLesson);

public static class BossCatalog
{
    public static readonly BossDefinition[] All = {
        new(BossId.Bellwarden, "BELLWARDEN", "THE HOLLOW CHOIR", new Color("#f6bc55"),
            "FOLLOW THE CHIMES · JUMP THE RINGS", "ECHO WAVES AND MARKED SLAMS · KEEP MOVING"),
        new(BossId.Thornmaw, "THORNMAW", "THE LOST GARDENER", new Color("#e87597"),
            "BAIT RUSHES INTO RESONATORS · LEAVE THORN MARKS", "DOUBLE RUSHES AND LINGERING THORNS"),
        new(BossId.Prismweaver, "PRISMWEAVER", "THE DREAM KEEPER", new Color("#8cc8ff"),
            "FOLLOW THE ORBITING SEAL · THEN THE CROWN", "TWO SEALS IN ORDER · WATCH FOR THE EXCHANGE")
    };
    public static BossDefinition Get(BossId id) => All[(int)id];
    public static string ModelPath(BossId id) => "res://art/bosses/" + id.ToString().ToLowerInvariant() + ".tscn";
}
