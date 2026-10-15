using Godot;

/// <summary>
/// Authored top-level dimensions and streaming distances for a procedural run.
/// ProceduralWorld snapshots these values before generation starts so a shared
/// resource cannot change an active run halfway through it.
/// </summary>
[GlobalClass]
public partial class WorldGenerationSettings : Resource
{
    [ExportGroup("Section geometry")]
    [Export(PropertyHint.Range, "40.0,240.0,0.5")]
    public float MinTravelLength { get; set; } = 92.0f;

    [Export(PropertyHint.Range, "40.0,240.0,0.5")]
    public float MaxTravelLength { get; set; } = 118.0f;

    [Export(PropertyHint.Range, "40.0,140.0,0.5")]
    public float ArenaLength { get; set; } = 78.0f;

    [Export(PropertyHint.Range, "20.0,72.0,0.5")]
    public float TravelSectionWidth { get; set; } = 36.0f;

    [ExportGroup("Biome pacing")]
    [Export(PropertyHint.Range, "300.0,1200.0,10.0")]
    public float MinimumBiomeTravelDistance { get; set; } = 440.0f;

    [ExportGroup("Streaming")]
    [Export(PropertyHint.Range, "80.0,1600.0,10.0")]
    public float GenerationAheadDistance { get; set; } = 620.0f;

    [Export(PropertyHint.Range, "40.0,800.0,10.0")]
    public float RetainBehindDistance { get; set; } = 280.0f;
}
