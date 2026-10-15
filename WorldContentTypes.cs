using System;

public enum WorldChunkKind
{
    Travel,
    Connector,
    Arena
}

public enum WorldGenerationPipeline
{
    Legacy,
    HybridV1
}

public enum WorldContentTheme
{
    Universal,
    DescentCore,
    Industrial,
    Ruins,
    Abyssal
}

public enum WorldWidthClass
{
    Narrow,
    Standard,
    Wide,
    Arena
}

public enum WorldSocketProfile
{
    Universal,
    Narrow,
    Standard,
    Wide
}

[Flags]
public enum WorldChunkKindMask
{
    None = 0,
    Travel = 1 << (int)WorldChunkKind.Travel,
    Connector = 1 << (int)WorldChunkKind.Connector,
    Arena = 1 << (int)WorldChunkKind.Arena,
    Any = Travel | Connector | Arena
}

[Flags]
public enum WorldChunkTag
{
    None = 0,
    Straight = 1 << 0,
    Traversal = 1 << 1,
    Combat = 1 << 2,
    SafeEntry = 1 << 3,
    SupportsObstacles = 1 << 4,
    SupportsEnemies = 1 << 5,
    ChapterTransition = 1 << 6
}

public enum WorldObstacleFootprint
{
    Small,
    Medium,
    Large,
    LaneWide
}

[Flags]
public enum WorldObstacleTag
{
    None = 0,
    Barrier = 1 << 0,
    Jumpable = 1 << 1,
    Breakable = 1 << 2,
    Moving = 1 << 3,
    Hazard = 1 << 4,
    TraversalAid = 1 << 5,
    Structure = 1 << 6
}
