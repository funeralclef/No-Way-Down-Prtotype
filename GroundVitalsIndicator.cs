using Godot;

/// <summary>
/// A pair of true world-space resource rings projected onto the ground below
/// the player. Camera movement changes their perspective naturally.
/// </summary>
public partial class GroundVitalsIndicator : Node3D
{
    private static readonly Color HealthBase =
        new(1.0f, 0.14f, 0.1f);
    private static readonly Color NitroBase =
        new(0.08f, 0.5f, 1.0f);

    private Player _player;
    private MeshInstance3D _healthTrack;
    private MeshInstance3D _healthFill;
    private MeshInstance3D _nitroTrack;
    private MeshInstance3D _nitroFill;
    private ShaderMaterial _healthFillMaterial;
    private ShaderMaterial _nitroFillMaterial;
    private StandardMaterial3D _healthTrackMaterial;
    private StandardMaterial3D _nitroTrackMaterial;
    private float _vitalsScale = 1.0f;
    private float _displayedHealth = 1.0f;
    private float _displayedNitro;
    private float _previousHealth = 1.0f;
    private float _damageFlash;
    private float _nitroGlow;
    private float _motionEnergy;
    private float _jumpEnergy;
    private float _cameraEnergy;
    private float _targetMotion;
    private float _targetJump;
    private float _targetCamera;
    private float _time;
    private bool _hasGroundPosition;
    private Color _backdropColor =
        new(0.12f, 0.16f, 0.24f);

    public Color DisplayHealthColor =>
        AdaptForBackdrop(
            HealthBase,
            true);
    public Color DisplayNitroColor =>
        AdaptForBackdrop(
            NitroBase,
            false);

    public override void _Ready()
    {
        BindVisuals();
        SetProcess(true);
        SetPhysicsProcess(true);
    }

    public override void _Process(double delta)
    {
        float seconds = (float)delta;
        _time += seconds;
        if (!IsInstanceValid(_player))
        {
            _player =
                GetTree().GetFirstNodeInGroup(
                    "player") as Player;
        }
        if (!IsInstanceValid(_player))
        {
            Visible = false;
            return;
        }

        Visible = _hasGroundPosition;
        float health =
            Mathf.Clamp(
                _player.HealthRatio,
                0.0f,
                1.0f);
        float nitro =
            Mathf.Clamp(
                _player.NitroRatio,
                0.0f,
                1.0f);
        if (health <
            _previousHealth - 0.002f)
        {
            _damageFlash = 1.0f;
        }
        _previousHealth = health;
        float smoothing =
            1.0f -
            Mathf.Exp(-9.0f * seconds);
        _displayedHealth =
            Mathf.Lerp(
                _displayedHealth,
                health,
                smoothing);
        _displayedNitro =
            Mathf.Lerp(
                _displayedNitro,
                nitro,
                smoothing);
        _damageFlash = Mathf.MoveToward(
            _damageFlash,
            0.0f,
            seconds * 1.5f);
        float nitroTarget =
            _player.IsNitroBurning
                ? Mathf.Clamp(
                    0.22f +
                        _player
                            .NitroThrottle *
                            0.78f,
                    0.0f,
                    1.0f)
                : 0.0f;
        _nitroGlow = Mathf.MoveToward(
            _nitroGlow,
            nitroTarget,
            seconds *
                (nitroTarget > _nitroGlow
                    ? 7.0f
                    : 2.0f));
        _motionEnergy = Mathf.Lerp(
            _motionEnergy,
            _targetMotion,
            smoothing);
        _jumpEnergy = Mathf.Lerp(
            _jumpEnergy,
            _targetJump,
            smoothing);
        _cameraEnergy = Mathf.Lerp(
            _cameraEnergy,
            _targetCamera,
            smoothing * 0.7f);
        UpdateVisuals();
    }

    public override void _PhysicsProcess(
        double delta)
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }
        Vector3 origin =
            _player.GlobalPosition +
            Vector3.Up * 1.5f;
        PhysicsRayQueryParameters3D query =
            PhysicsRayQueryParameters3D.Create(
                origin,
                origin +
                    Vector3.Down * 16.0f);
        query.Exclude =
            new Godot.Collections.Array<Rid>
            {
                _player.GetRid()
            };
        Godot.Collections.Dictionary hit =
            GetWorld3D()
                .DirectSpaceState
                .IntersectRay(query);
        if (hit.Count == 0)
        {
            return;
        }
        Vector3 target =
            hit["position"].AsVector3() +
            Vector3.Up * 0.055f;
        float follow =
            1.0f -
            Mathf.Exp(
                -22.0f * (float)delta);
        GlobalPosition = _hasGroundPosition
            ? GlobalPosition.Lerp(
                target,
                follow)
            : target;
        _hasGroundPosition = true;
    }

    public void SetPlayer(Player player)
    {
        _player = player;
    }

    public void Configure(float scale)
    {
        _vitalsScale =
            Mathf.Clamp(scale, 0.75f, 1.5f);
    }

    public void SetBackdropColor(Color color)
    {
        _backdropColor = color;
    }

    public void SetMotionDynamics(
        float screenMotion,
        float jumpMotion,
        float cameraMotion)
    {
        _targetMotion =
            Mathf.Clamp(
                screenMotion,
                0.0f,
                1.0f);
        _targetJump =
            Mathf.Clamp(
                jumpMotion,
                0.0f,
                1.0f);
        _targetCamera =
            Mathf.Clamp(
                cameraMotion,
                0.0f,
                1.0f);
    }

    private void BindVisuals()
    {
        _healthTrack =
            GetNode<MeshInstance3D>(
                "HealthTrack");
        _healthFill =
            GetNode<MeshInstance3D>(
                "HealthRing");
        _nitroTrack =
            GetNode<MeshInstance3D>(
                "NitroTrack");
        _nitroFill =
            GetNode<MeshInstance3D>(
                "NitroRing");
        _healthTrackMaterial =
            (StandardMaterial3D)
                _healthTrack.MaterialOverride;
        _nitroTrackMaterial =
            (StandardMaterial3D)
                _nitroTrack.MaterialOverride;
        _healthFillMaterial =
            (ShaderMaterial)
                _healthFill.MaterialOverride;
        _nitroFillMaterial =
            (ShaderMaterial)
                _nitroFill.MaterialOverride;
    }

    private void UpdateVisuals()
    {
        Color healthColor =
            DisplayHealthColor;
        Color nitroColor =
            DisplayNitroColor;
        float airborneExpansion =
            _jumpEnergy * 0.1f;
        float motionPulse =
            1.0f +
            Mathf.Sin(
                _time *
                Mathf.Lerp(
                    2.4f,
                    5.2f,
                    _motionEnergy)) *
            (0.012f +
                _motionEnergy * 0.018f);
        float healthPulse =
            1.0f +
            _damageFlash * 0.16f +
            (_player.HealthRatio <= 0.3f
                ? Mathf.Sin(_time * 7.0f) * 0.045f
                : 0.0f);
        float nitroPulse =
            1.0f +
            _nitroGlow *
                (0.08f +
                    Mathf.Sin(_time * 11.0f) *
                    0.035f);
        Scale =
            Vector3.One *
            _vitalsScale *
            (1.0f +
                airborneExpansion +
                _cameraEnergy * 0.035f);
        _healthFill.Scale =
            Vector3.One *
            healthPulse *
            motionPulse;
        _healthTrack.Scale =
            _healthFill.Scale;
        _nitroFill.Scale =
            Vector3.One *
            nitroPulse *
            motionPulse;
        _nitroTrack.Scale =
            _nitroFill.Scale;

        float healthAlpha =
            0.56f +
            _damageFlash * 0.38f;
        float nitroAlpha =
            0.5f +
            _nitroGlow * 0.4f;
        _healthFillMaterial.SetShaderParameter(
            "fill_ratio",
            _displayedHealth);
        _healthFillMaterial.SetShaderParameter(
            "tint",
            new Color(
                healthColor.R,
                healthColor.G,
                healthColor.B,
                healthAlpha));
        _healthFillMaterial.SetShaderParameter(
            "glow_energy",
            1.8f +
                _damageFlash * 4.2f);
        _nitroFillMaterial.SetShaderParameter(
            "fill_ratio",
            _displayedNitro);
        _nitroFillMaterial.SetShaderParameter(
            "tint",
            new Color(
                nitroColor.R,
                nitroColor.G,
                nitroColor.B,
                nitroAlpha));
        _nitroFillMaterial.SetShaderParameter(
            "glow_energy",
            1.7f +
                _nitroGlow * 4.8f);
        _healthTrackMaterial.AlbedoColor =
            new Color(
                healthColor.R,
                healthColor.G,
                healthColor.B,
                0.085f);
        _healthTrackMaterial.Emission =
            healthColor;
        _nitroTrackMaterial.AlbedoColor =
            new Color(
                nitroColor.R,
                nitroColor.G,
                nitroColor.B,
                0.08f);
        _nitroTrackMaterial.Emission =
            nitroColor;
    }

    private Color AdaptForBackdrop(
        Color source,
        bool health)
    {
        Vector3 sourceRgb =
            new(source.R, source.G, source.B);
        Vector3 backdropRgb =
            new(
                _backdropColor.R,
                _backdropColor.G,
                _backdropColor.B);
        float distance =
            sourceRgb.DistanceTo(backdropRgb);
        float sourceLuma =
            source.R * 0.2126f +
            source.G * 0.7152f +
            source.B * 0.0722f;
        float backdropLuma =
            _backdropColor.R * 0.2126f +
            _backdropColor.G * 0.7152f +
            _backdropColor.B * 0.0722f;
        float adaptation =
            Mathf.Clamp(
                Mathf.Max(
                    1.0f -
                        distance / 0.82f,
                    1.0f -
                        Mathf.Abs(
                            sourceLuma -
                            backdropLuma) /
                        0.42f) -
                    0.24f,
                0.0f,
                0.72f);
        Color target = health
            ? (_backdropColor.R >
                _backdropColor.B
                ? new Color(1.0f, 0.06f, 0.58f)
                : new Color(1.0f, 0.46f, 0.2f))
            : (_backdropColor.B >
                _backdropColor.R
                ? new Color(0.2f, 1.0f, 0.9f)
                : new Color(0.18f, 0.72f, 1.0f));
        return source.Lerp(
            target,
            adaptation);
    }
}
