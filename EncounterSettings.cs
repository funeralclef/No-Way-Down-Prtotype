using Godot;

/// <summary>
/// Authored tuning shared by encounter planning and physical spawning.
/// Runtime encounter state never belongs in this resource.
/// </summary>
[GlobalClass]
public partial class EncounterSettings : Resource
{
    [ExportGroup("Travel waves")]
    [Export(PropertyHint.Range, "0.02,0.25,0.01")]
    public float SpawnMarginRatio { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float SoloSideChance { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "6.0,28.0,0.1")]
    public float MinColumnSpacing { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "10.0,36.0,0.1")]
    public float MaxColumnSpacing { get; set; } = 12.5f;

    [Export(PropertyHint.Range, "0.0,8.0,0.1")]
    public float MaxColumnLateralOffset { get; set; } = 4.2f;

    [Export(PropertyHint.Range, "1.5,6.0,0.1")]
    public float MinRowSpacing { get; set; } = 2.8f;

    [Export(PropertyHint.Range, "1.5,8.0,0.1")]
    public float MaxRowSpacing { get; set; } = 4.2f;

    [Export(PropertyHint.Range, "0.5,5.0,0.1")]
    public float MinWaveInterval { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "0.5,5.0,0.1")]
    public float MaxWaveInterval { get; set; } = 1.7f;

    [Export(PropertyHint.Range, "4.0,30.0,0.5")]
    public float MinMobSpeed { get; set; } = 9.0f;

    [Export(PropertyHint.Range, "4.0,30.0,0.5")]
    public float MaxMobSpeed { get; set; } = 14.0f;

    [Export(PropertyHint.Range, "0.0,0.2,0.01")]
    public float MobSpeedVariation { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "2,30,1")]
    public int MaxActiveMobs { get; set; } = 16;

    [ExportGroup("Arena waves")]
    [Export(PropertyHint.Range, "4,30,1")]
    public int ArenaBaseEnemies { get; set; } = 10;

    [Export(PropertyHint.Range, "1,10,1")]
    public int ArenaEnemiesPerWave { get; set; } = 4;

    [Export(PropertyHint.Range, "8,40,1")]
    public int ArenaMaxEnemies { get; set; } = 26;

    [Export(PropertyHint.Range, "0.8,1.8,0.05")]
    public float ArenaMinSpeedMultiplier { get; set; } = 1.12f;

    [Export(PropertyHint.Range, "0.8,2.0,0.05")]
    public float ArenaMaxSpeedMultiplier { get; set; } = 1.35f;

    [Export(PropertyHint.Range, "8.0,24.0,0.5")]
    public float ArenaMinimumMobSpeed { get; set; } = 13.5f;

    [Export(PropertyHint.Range, "0.5,4.0,0.1")]
    public float ArenaMobMinimumThreatTime { get; set; } = 1.7f;

    [Export(PropertyHint.Range, "4.0,30.0,0.5")]
    public float ArenaMobRetireDistance { get; set; } = 10.5f;

    [Export(PropertyHint.Range, "0.0,1.5,0.05")]
    public float ArenaMobRetireGraceTime { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "3.0,12.0,0.25")]
    public float ArenaMobMaxLifetime { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "1.0,8.0,0.25")]
    public float PostArenaBreatherTime { get; set; } = 4.0f;
}
