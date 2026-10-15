using Godot;

/// <summary>Checks actual arena gaps for multiple seeds and both generation pipelines.</summary>
public partial class BiomePacingProbe : Node
{
    private bool _failed;
    public override void _Ready()
    {
        var production = GD.Load<WorldGenerationSettings>("res://world/default_world_generation_settings.tres");
        foreach (var pipeline in new[] { WorldGenerationPipeline.Legacy, WorldGenerationPipeline.HybridV1 })
        foreach (ulong seed in new[] { 1UL, 42UL, 240629UL, 9888UL })
        {
            GD.Seed(seed);
            var world = new ProceduralWorld { GenerationSettings = production, GenerationPipeline = pipeline,
                ContentCatalog = GD.Load<WorldContentCatalog>("res://world/content/reference_catalog.tres"), HybridSliceArenaOrdinal = 2 };
            AddChild(world); world.Initialize(null, 0, seed);
            float previousExit = 0;
            for (int ordinal = 1; ordinal <= 8; ordinal++)
            {
                bool found = world.TryGetArenaSpan(ordinal, out _, out float entry, out float exit);
                Check(found, $"arena {ordinal} exists");
                if (ordinal > 1)
                    Check(previousExit - entry >= production.MinimumBiomeTravelDistance - .1f,
                        $"{pipeline} seed {seed}, arena {ordinal}: gap {previousExit - entry:0.0} >= {production.MinimumBiomeTravelDistance}");
                previousExit = exit;
            }
            Check(pipeline != WorldGenerationPipeline.HybridV1 || world.HybridSliceComplete, "hybrid slice still completes after pacing prelude");
            world.Free();
        }
        GD.Print(_failed ? "BIOME_PACING_PROBE: FAILED" : "BIOME_PACING_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }
    private void Check(bool value, string detail) { if (!value) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
