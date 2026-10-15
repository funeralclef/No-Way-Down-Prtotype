using Godot;

/// <summary>
/// Owns the run-only atmosphere. Keeping it separate from the refuge prevents
/// the darker road grade and travelling particles from leaking into the hub.
/// </summary>
public partial class RunVisualAtmosphere : Node3D
{
    private Player _player;
    private GpuParticles3D _neonDust;
    private GpuParticles3D _speedMotes;
    private OmniLight3D _playerReadabilityLight;
    private Godot.Environment _environment;
    private float _zoneLight = 0.65f;
    private RunBiomeWeather _weather;
    private int _biomeId;
    public int CurrentBiomeId => _biomeId;
    public string CurrentWeather => RunBiomeCatalog.Get(_biomeId).Weather;

    public void SetBiome(int id)
    {
        _biomeId = Mathf.PosMod(id, RunBiomeCatalog.Count);
        _weather?.SetBiome(_biomeId);
        if (_neonDust?.ProcessMaterial is ParticleProcessMaterial dust)
            dust.Color = new Color(RunBiomeCatalog.Get(_biomeId).Accent.Lerp(Colors.White, .6f), .25f);
    }
    private RunRouteStyle _routeStyle =
        RunRouteStyle.Neutral;
    private Color _routeTint = Colors.White;
    private Color _routeTintTarget = Colors.White;
    private float _routeTintStrength;
    private float _routeTintStrengthTarget;

    public RunRouteStyle CurrentRouteStyle =>
        _routeStyle;

    public void Initialize(Player player)
    {
        _player = player;
        CreateEnvironment();
        CreateParticleField();
        CreateReadabilityLight();
        _weather = new RunBiomeWeather { Name = "BiomeWeather" };
        AddChild(_weather);
        SetBiome(0);
        SnapToPlayer();
    }

    public void SetRouteStyle(
        RunRouteStyle routeStyle)
    {
        _routeStyle = routeStyle;
        (_routeTintTarget,
            _routeTintStrengthTarget) =
            routeStyle switch
            {
                RunRouteStyle.Hunt =>
                    (new Color(
                        0.96f,
                        0.34f,
                        0.16f),
                    0.105f),
                RunRouteStyle.Flow =>
                    (new Color(
                        0.2f,
                        0.82f,
                        0.94f),
                    0.09f),
                RunRouteStyle.Endure =>
                    (new Color(
                        0.36f,
                        0.43f,
                        0.7f),
                    0.085f),
                _ =>
                    (Colors.White, 0.0f)
            };
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        float weight =
            1.0f -
            Mathf.Exp(
                -5.0f *
                (float)delta);
        Vector3 target =
            _player.GlobalPosition +
            new Vector3(
                0.0f,
                3.1f,
                -8.0f);
        GlobalPosition =
            GlobalPosition.Lerp(
                target,
                weight);

        float routeWeight =
            1.0f -
            Mathf.Exp(
                -0.82f *
                (float)delta);
        _routeTint =
            _routeTint.Lerp(
                _routeTintTarget,
                routeWeight);
        _routeTintStrength =
            Mathf.Lerp(
                _routeTintStrength,
                _routeTintStrengthTarget,
                routeWeight);

        int quality =
            _player.EffectsQuality;
        _weather.SetQuality(quality);
        _neonDust.Visible =
            quality > 0;
        _speedMotes.Visible =
            quality >= 2;
        _neonDust.AmountRatio =
            quality switch
            {
                1 => 0.38f,
                2 => 0.7f,
                _ => 1.0f
            };
        _speedMotes.AmountRatio =
            quality >= 3
                ? 1.0f
                : 0.52f;

        float motion =
            Mathf.Clamp(
                new Vector2(
                    _player.Velocity.X,
                    _player.Velocity.Z)
                    .Length() /
                24.0f,
                0.0f,
                1.0f);
        _speedMotes.SpeedScale =
            Mathf.Lerp(
                0.78f,
                1.75f,
                motion);
        UpdateLightRhythm(
            (float)delta);
        _playerReadabilityLight
            .LightEnergy =
            Mathf.Lerp(
                0.22f,
                0.38f,
                motion) *
            Mathf.Lerp(
                0.88f,
                1.12f,
                _zoneLight);
    }

    private void SnapToPlayer()
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        GlobalPosition =
            _player.GlobalPosition +
            new Vector3(
                0.0f,
                3.1f,
                -8.0f);
    }

    private void CreateEnvironment()
    {
        _environment =
            new()
            {
                BackgroundMode =
                    Godot.Environment
                        .BGMode
                        .Color,
                BackgroundColor =
                    new Color(
                        0.36f,
                        0.62f,
                        0.76f),
                AmbientLightSource =
                    Godot.Environment
                        .AmbientSource
                        .Color,
                AmbientLightColor =
                    new Color(
                        0.76f,
                        0.82f,
                        0.84f),
                AmbientLightEnergy =
                    0.72f,
                FogEnabled = true,
                FogLightColor =
                    new Color(
                        0.52f,
                        0.72f,
                        0.8f),
                FogLightEnergy = 0.68f,
                FogDensity = 0.0032f,
                FogHeight = -1.0f,
                FogHeightDensity = 0.024f,
                TonemapMode =
                    Godot.Environment
                        .ToneMapper
                        .Filmic,
                GlowEnabled = true,
                GlowIntensity = 0.26f,
                GlowStrength = 0.38f,
                GlowBloom = 0.025f
            };
        AddChild(
            new WorldEnvironment
            {
                Name =
                    "RunWorldEnvironment",
                Environment =
                    _environment
            });
    }

    private void UpdateLightRhythm(
        float deltaSeconds)
    {
        if (_environment == null)
        {
            return;
        }

        float phase =
            Mathf.PosMod(
                -_player.GlobalPosition.Z,
                144.0f) /
            144.0f;
        float targetLight;
        if (phase < 0.26f)
        {
            targetLight = 1.0f;
        }
        else if (phase < 0.46f)
        {
            targetLight =
                1.0f -
                Mathf.SmoothStep(
                    0.26f,
                    0.46f,
                    phase);
        }
        else if (phase < 0.66f)
        {
            targetLight = 0.0f;
        }
        else
        {
            targetLight =
                Mathf.SmoothStep(
                    0.66f,
                    1.0f,
                    phase);
        }

        float weight =
            1.0f -
            Mathf.Exp(
                -1.7f *
                deltaSeconds);
        _zoneLight =
            Mathf.Lerp(
                _zoneLight,
                targetLight,
                weight);
        var biome = RunBiomeCatalog.Get(_biomeId);
        float blend = 1 - Mathf.Exp(-deltaSeconds * .8f);
        _environment.BackgroundColor = _environment.BackgroundColor.Lerp(biome.Sky.Lerp(_routeTint, _routeTintStrength), blend);
        _environment.AmbientLightColor = _environment.AmbientLightColor.Lerp(biome.Fog.Lerp(Colors.White, .55f), blend);
        _environment.AmbientLightEnergy = Mathf.Lerp(.60f, .82f, _zoneLight);
        _environment.FogLightColor = _environment.FogLightColor.Lerp(biome.Fog, blend);
        _environment.FogDensity = Mathf.Lerp(_environment.FogDensity, biome.FogDensity, blend);
        _environment.FogLightEnergy = .55f;
    }

    private void CreateParticleField()
    {
        StandardMaterial3D dustVisual =
            CreateParticleMaterial(
                new Color(
                    0.84f,
                    0.98f,
                    1.0f,
                    0.22f),
                0.42f);
        ParticleProcessMaterial dustProcess =
            new()
            {
                EmissionShape =
                    ParticleProcessMaterial
                        .EmissionShapeEnum
                        .Box,
                EmissionBoxExtents =
                    new Vector3(
                        20.0f,
                        5.2f,
                        20.0f),
                Direction =
                    new Vector3(
                        0.0f,
                        0.5f,
                        0.2f),
                Spread = 56.0f,
                InitialVelocityMin = 0.12f,
                InitialVelocityMax = 0.7f,
                Gravity =
                    new Vector3(
                        0.0f,
                        0.08f,
                        0.0f),
                ScaleMin = 0.45f,
                ScaleMax = 1.25f,
                Color =
                    new Color(
                        0.84f,
                        0.98f,
                        1.0f,
                        0.28f)
            };
        _neonDust =
            new GpuParticles3D
            {
                Name = "NeonDust",
                Amount = 48,
                Lifetime = 5.8f,
                Randomness = 0.78f,
                ProcessMaterial =
                    dustProcess,
                DrawPass1 =
                    new QuadMesh
                    {
                        Size =
                            new Vector2(
                                0.045f,
                                0.045f),
                        Material =
                            dustVisual
                    },
                VisibilityAabb =
                    new Aabb(
                        new Vector3(
                            -24.0f,
                            -7.0f,
                            -24.0f),
                        new Vector3(
                            48.0f,
                            14.0f,
                            48.0f)),
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off
            };
        AddChild(_neonDust);

        StandardMaterial3D moteVisual =
            CreateParticleMaterial(
                new Color(
                    0.24f,
                    0.78f,
                    0.9f,
                    0.16f),
                0.56f);
        ParticleProcessMaterial moteProcess =
            new()
            {
                EmissionShape =
                    ParticleProcessMaterial
                        .EmissionShapeEnum
                        .Box,
                EmissionBoxExtents =
                    new Vector3(
                        18.0f,
                        4.5f,
                        18.0f),
                Direction =
                    new Vector3(
                        0.0f,
                        0.08f,
                        1.0f),
                Spread = 18.0f,
                InitialVelocityMin = 2.8f,
                InitialVelocityMax = 6.2f,
                Gravity = Vector3.Zero,
                ScaleMin = 0.5f,
                ScaleMax = 1.35f,
                Color =
                    new Color(
                        0.68f,
                        0.94f,
                        0.98f,
                        0.22f)
            };
        _speedMotes =
            new GpuParticles3D
            {
                Name = "DepthMotes",
                Amount = 26,
                Lifetime = 3.4f,
                Randomness = 0.68f,
                ProcessMaterial =
                    moteProcess,
                DrawPass1 =
                    new QuadMesh
                    {
                        Size =
                            new Vector2(
                                0.025f,
                                0.34f),
                        Material =
                            moteVisual
                    },
                VisibilityAabb =
                    new Aabb(
                        new Vector3(
                            -24.0f,
                            -7.0f,
                            -24.0f),
                        new Vector3(
                            48.0f,
                            14.0f,
                            48.0f)),
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off
            };
        AddChild(_speedMotes);
    }

    private void CreateReadabilityLight()
    {
        _playerReadabilityLight =
            new OmniLight3D
            {
                Name =
                    "PlayerReadabilityLight",
                Position =
                    new Vector3(
                        0.0f,
                        4.0f,
                        7.0f),
                LightColor =
                    new Color(
                        0.82f,
                        0.94f,
                        1.0f),
                LightEnergy = 0.4f,
                OmniRange = 15.0f,
                ShadowEnabled = false
            };
        AddChild(
            _playerReadabilityLight);
    }

    private static StandardMaterial3D
        CreateParticleMaterial(
            Color color,
            float emission)
    {
        return new StandardMaterial3D
        {
            Transparency =
                BaseMaterial3D
                    .TransparencyEnum
                    .Alpha,
            ShadingMode =
                BaseMaterial3D
                    .ShadingModeEnum
                    .Unshaded,
            BillboardMode =
                BaseMaterial3D
                    .BillboardModeEnum
                    .Enabled,
            VertexColorUseAsAlbedo =
                true,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission =
                new Color(
                    color.R,
                    color.G,
                    color.B),
            EmissionEnergyMultiplier =
                emission
        };
    }
}
