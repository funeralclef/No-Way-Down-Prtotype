using Godot;

/// <summary>
/// Mutable state for one generated section. Authored definitions stay
/// immutable; encounter and streaming state lives here for one run only.
/// </summary>
public sealed class WorldSectionRuntime
{
    public int Index;
    public int BiomeId;
    public int ArenaOrdinal;
    public string Title = "";
    public string BaseTitle = "";
    public float SouthZ;
    public float NorthZ;
    public float Width;
    public float ArenaRadius;
    public ArenaLayout ArenaLayout;
    public Color ThemeColor;
    public bool IsArena;
    public bool ArenaStarted;
    public bool ArenaCompleted;
    public bool IsAuthored;
    public string ContentId = "";
    public RunRouteStyle RouteStyle =
        RunRouteStyle.Neutral;
    public Node3D Root;
}
