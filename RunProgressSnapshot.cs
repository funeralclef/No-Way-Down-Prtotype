/// <summary>
/// A physical reading of the player's position in the four-arena ascent.
/// CoreProgress spans the complete authored run, while SegmentProgress is
/// always local to the road between the previous and the next arena.
/// </summary>
public readonly struct RunProgressSnapshot
{
    public RunProgressSnapshot(
        int completedArenas,
        int targetArena,
        float segmentProgress,
        float coreProgress,
        bool coreCompleted,
        bool arenaReached,
        int overrunChapter)
    {
        CompletedArenas = completedArenas;
        TargetArena = targetArena;
        SegmentProgress = segmentProgress;
        CoreProgress = coreProgress;
        CoreCompleted = coreCompleted;
        ArenaReached = arenaReached;
        OverrunChapter = overrunChapter;
    }

    public int CompletedArenas { get; }
    public int TargetArena { get; }
    public float SegmentProgress { get; }
    public float CoreProgress { get; }
    public bool CoreCompleted { get; }
    public bool ArenaReached { get; }
    public int OverrunChapter { get; }
}
