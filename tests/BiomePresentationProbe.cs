using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Checks structural biome identity and renders each authored environment recipe.</summary>
public partial class BiomePresentationProbe : Node3D
{
    private bool _failed;
    private bool Rendered => DisplayServer.GetName() != "headless";
    public override async void _Ready()
    {
        var player = GD.Load<PackedScene>("res://player.tscn").Instantiate<Player>();
        AddChild(player); player.SetPhysicsProcess(false); player.Position = new Vector3(0, .3f, 0);
        player.ApplySkin(0, Colors.White, new Color("#293b49"), new Color("#ed994e"));
        var world = new ProceduralWorld(); AddChild(world); world.Initialize(player, 0);
        world.TryGetArenaSpan(6, out _, out _, out _);
        int chapter = 0;
        var generatedBiomes = new HashSet<int>();
        foreach (var section in world.GeneratedSections)
        {
            Check(section.BiomeId == chapter % RunBiomeCatalog.Count, "biome stays coherent until its arena is completed");
            Check(section.Root.GetNodeOrNull<Node3D>("BiomeEnvironment") != null, "generated world section owns its biome scenery");
            generatedBiomes.Add(section.BiomeId);
            if (section.IsArena) chapter++;
        }
        Check(generatedBiomes.Count == RunBiomeCatalog.Count, "extended progression reaches all six biomes");
        world.QueueFree(); await Frames(2);
        var camera = new Camera3D { Position = new Vector3(0, 16, 26), Fov = 58, Current = true };
        AddChild(camera); camera.LookAt(new Vector3(0, 1, -12));
        var light = new DirectionalLight3D { RotationDegrees = new Vector3(-55, -20, 0), LightEnergy = 1.0f, ShadowEnabled = true };
        AddChild(light);
        var floor = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(27, .5f, 100) }, Position = new Vector3(0, -.25f, -28) };
        var floorMaterial = new StandardMaterial3D { AlbedoColor = new Color("#34434a"), Roughness = .7f };
        floor.MaterialOverride = floorMaterial; AddChild(floor);
        var atmosphere = new RunVisualAtmosphere(); AddChild(atmosphere); atmosphere.Initialize(player);
        var signatures = new HashSet<string>();
        for (int biome = 0; biome < RunBiomeCatalog.Count; biome++)
        {
            var anchor = new Node3D { Position = new Vector3(0, 0, -22) }; AddChild(anchor);
            Node3D scenery = RunBiomeScenery.Build(anchor, biome, 27, 80, 1234);
            atmosphere.SetBiome(biome);
            floorMaterial.Roughness = RunBiomeCatalog.Get(biome).Rain ? .22f : .78f;
            Check(CountMeshes(scenery) >= 12, "biome " + biome + " has actual environment meshes");
            Check(CountPhysics(scenery) == 0, "biome " + biome + " scenery does not obstruct play");
            signatures.Add(scenery.GetChild(0).Name + ":" + CountMeshes(scenery) + ":" + atmosphere.CurrentWeather);
            if (Rendered)
            {
                await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-biome-{biome}.png");
            }
            anchor.QueueFree(); await Frames(2);
        }
        Check(signatures.Count == 6, "all six biomes have distinct geometry and weather identities");
        player.SetEffectsQuality(0); await Frames(3);
        var weather = atmosphere.GetNode<RunBiomeWeather>("BiomeWeather");
        Check(!weather.GetNode<GpuParticles3D>("BiomeWeatherParticles").Visible, "weather honors disabled effects");
        GD.Print(_failed ? "BIOME_PRESENTATION_PROBE: FAILED" : "BIOME_PRESENTATION_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }
    private static int CountMeshes(Node node)
    {
        int count = node is MeshInstance3D ? 1 : 0;
        foreach (Node child in node.GetChildren()) count += CountMeshes(child);
        return count;
    }
    private static int CountPhysics(Node node)
    {
        int count = node is CollisionObject3D ? 1 : 0;
        foreach (Node child in node.GetChildren()) count += CountPhysics(child);
        return count;
    }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool condition, string detail) { if (!condition) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
