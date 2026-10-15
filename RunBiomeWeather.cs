using Godot;

/// <summary>Bounded weather field following the runner; scales with the effects budget.</summary>
public partial class RunBiomeWeather : Node3D
{
    private GpuParticles3D _particles;
    private ParticleProcessMaterial _process;
    private StandardMaterial3D _visual;
    public int BiomeId { get; private set; } = -1;
    public string WeatherName => RunBiomeCatalog.Get(Mathf.Max(0, BiomeId)).Weather;
    public override void _Ready()
    {
        _process = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(23, 10, 24), Gravity = Vector3.Zero, Spread = 12 };
        _visual = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true };
        _particles = new GpuParticles3D { Name = "BiomeWeatherParticles", Amount = 320, Lifetime = 4, Preprocess = 2,
            ProcessMaterial = _process, VisibilityAabb = new Aabb(new Vector3(-35, -20, -35), new Vector3(70, 40, 70)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_particles);
    }
    public void SetBiome(int biome)
    {
        if (biome == BiomeId) return;
        BiomeId = Mathf.PosMod(biome, RunBiomeCatalog.Count);
        var profile = RunBiomeCatalog.Get(BiomeId);
        _process.Direction = profile.Wind.Normalized();
        _process.InitialVelocityMin = profile.Wind.Length() * .7f;
        _process.InitialVelocityMax = profile.Wind.Length() * 1.25f;
        _process.Spread = profile.Snow ? 35 : profile.Rain ? 5 : 48;
        _process.ScaleMin = .65f; _process.ScaleMax = profile.Snow ? 1.8f : 1.2f;
        _process.Color = new Color(profile.Accent.Lerp(Colors.White, .65f), profile.Rain ? .25f : .45f);
        _visual.AlbedoColor = Colors.White;
        _particles.DrawPass1 = new QuadMesh { Size = profile.Rain ? new Vector2(.025f, .7f) : profile.Snow ? new Vector2(.1f, .1f) : new Vector2(.055f, .055f), Material = _visual };
        _particles.Lifetime = profile.Rain ? 2.5f : 7;
        _particles.Restart();
    }
    public void SetQuality(int quality)
    {
        _particles.Visible = quality > 0;
        _particles.AmountRatio = quality switch { 0 => 0, 1 => .22f, 2 => .55f, _ => 1 };
        _particles.Emitting = quality > 0;
    }
}
