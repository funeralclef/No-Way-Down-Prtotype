using Godot;

/// <summary>
/// Two quiet, tapered projections that sit beneath the player's feet.
/// They read as part of the world rather than as a conventional HUD card.
/// </summary>
public partial class RadialVitalsHud : Control
{
    private static readonly Color HealthColor =
        new(1.0f, 0.16f, 0.12f);
    private static readonly Color NitroColor =
        new(0.08f, 0.52f, 1.0f);

    private Player _player;
    private float _healthRatio = 1.0f;
    private float _nitroRatio;
    private float _displayedHealth = 1.0f;
    private float _displayedNitro;
    private float _layoutScale = 1.0f;
    private float _pulseTime;
    private float _damageFlash;
    private float _nitroGlow;
    private float _motionEnergy;
    private float _jumpEnergy;
    private float _cameraEnergy;
    private float _targetMotionEnergy;
    private float _targetJumpEnergy;
    private float _targetCameraEnergy;
    private float _previousHealthRatio = 1.0f;
    private Color _backdropColor =
        new(0.12f, 0.16f, 0.24f);

    public Color DisplayHealthColor =>
        AdaptForBackdrop(
            HealthColor,
            true);
    public Color DisplayNitroColor =>
        AdaptForBackdrop(
            NitroColor,
            false);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _pulseTime += (float)delta;
        if (!IsInstanceValid(_player))
        {
            _player =
                GetTree().GetFirstNodeInGroup("player")
                    as Player;
        }

        if (IsInstanceValid(_player))
        {
            _healthRatio = Mathf.Clamp(
                _player.Health /
                    Mathf.Max(1.0f, _player.MaxHealth),
                0.0f,
                1.0f);
            _nitroRatio = Mathf.Clamp(
                _player.NitroRatio,
                0.0f,
                1.0f);
            if (_healthRatio <
                _previousHealthRatio - 0.002f)
            {
                _damageFlash = 1.0f;
            }
            _previousHealthRatio =
                _healthRatio;
        }

        float smoothing =
            1.0f - Mathf.Exp(-9.0f * (float)delta);
        _displayedHealth = Mathf.Lerp(
            _displayedHealth,
            _healthRatio,
            smoothing);
        _displayedNitro = Mathf.Lerp(
            _displayedNitro,
            _nitroRatio,
            smoothing);
        _damageFlash = Mathf.MoveToward(
            _damageFlash,
            0.0f,
            (float)delta * 1.65f);
        float nitroTarget =
            IsInstanceValid(_player) &&
            _player.IsNitroBurning
                ? 1.0f
                : 0.0f;
        _nitroGlow = Mathf.MoveToward(
            _nitroGlow,
            nitroTarget,
            (float)delta *
                (nitroTarget > _nitroGlow
                    ? 6.0f
                    : 2.2f));
        _motionEnergy = Mathf.Lerp(
            _motionEnergy,
            _targetMotionEnergy,
            smoothing);
        _jumpEnergy = Mathf.Lerp(
            _jumpEnergy,
            _targetJumpEnergy,
            smoothing);
        _cameraEnergy = Mathf.Lerp(
            _cameraEnergy,
            _targetCameraEnergy,
            smoothing * 0.72f);
        QueueRedraw();
    }

    public void SetPlayer(Player player)
    {
        _player = player;
    }

    public void Configure(
        float vitalsScale,
        bool touch)
    {
        _layoutScale = vitalsScale;
        CustomMinimumSize =
            new Vector2(
                (touch ? 154.0f : 118.0f) * vitalsScale,
                (touch ? 104.0f : 80.0f) * vitalsScale);
        QueueRedraw();
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
        _targetMotionEnergy =
            Mathf.Clamp(screenMotion, 0.0f, 1.0f);
        _targetJumpEnergy =
            Mathf.Clamp(jumpMotion, 0.0f, 1.0f);
        _targetCameraEnergy =
            Mathf.Clamp(cameraMotion, 0.0f, 1.0f);
    }

    public override void _Draw()
    {
        float baseWidth =
            Mathf.Max(
                4.0f,
                5.25f * _layoutScale) *
            (1.0f +
                _motionEnergy * 0.12f);
        bool critical =
            _healthRatio <= 0.3f;
        bool burning =
            IsInstanceValid(_player) &&
            _player.IsNitroBurning;
        Color healthColor =
            DisplayHealthColor;
        Color nitroColor =
            DisplayNitroColor;
        float spacing =
            Size.X *
            (0.31f +
                _cameraEnergy * 0.025f);
        float halfHeight =
            Size.Y * 0.36f *
            (1.0f +
                _jumpEnergy * 0.13f +
                _cameraEnergy * 0.06f);
        float curve =
            Size.X *
            (0.045f +
                _motionEnergy * 0.025f);
        Vector2 center =
            Size * 0.5f;

        DrawRoundedSideGauge(
            new Vector2(
                center.X - spacing,
                center.Y),
            -1.0f,
            halfHeight,
            curve +
                _damageFlash *
                    2.5f * _layoutScale,
            _displayedHealth,
            healthColor,
            baseWidth *
                (1.0f +
                    _damageFlash * 0.34f),
            critical
                ? 0.74f +
                    Mathf.Sin(_pulseTime * 7.5f) * 0.16f
                : 0.56f +
                    _damageFlash * 0.42f);
        DrawRoundedSideGauge(
            new Vector2(
                center.X + spacing,
                center.Y),
            1.0f,
            halfHeight,
            curve +
                _nitroGlow *
                    2.0f * _layoutScale,
            _displayedNitro,
            nitroColor,
            baseWidth *
                (0.9f +
                    _nitroGlow * 0.26f),
            burning
                ? 0.76f +
                    Mathf.Sin(_pulseTime * 10.0f) * 0.12f
                : 0.5f +
                    _nitroGlow * 0.32f);
    }

    private void DrawRoundedSideGauge(
        Vector2 center,
        float side,
        float halfHeight,
        float curve,
        float ratio,
        Color color,
        float baseWidth,
        float activeAlpha)
    {
        Vector2[] track =
            CreateSidePath(
                center,
                side,
                halfHeight,
                curve,
                0.0f,
                1.0f);
        DrawRoundedPath(
            track,
            new Color(
                0.0f,
                0.0f,
                0.0f,
                0.48f),
            baseWidth +
                3.4f * _layoutScale);
        DrawRoundedPath(
            track,
            new Color(
                color.R,
                color.G,
                color.B,
                0.13f),
            baseWidth +
                0.8f * _layoutScale);
        if (ratio <= 0.002f)
        {
            return;
        }

        Vector2[] fill =
            CreateSidePath(
                center,
                side,
                halfHeight,
                curve,
                1.0f - ratio,
                1.0f);
        DrawRoundedPath(
            fill,
            new Color(
                0.0f,
                0.0f,
                0.0f,
                0.44f),
            baseWidth +
                4.8f * _layoutScale);
        DrawRoundedPath(
            fill,
            new Color(
                color.R,
                color.G,
                color.B,
                activeAlpha * 0.24f),
            baseWidth +
                4.0f * _layoutScale);
        DrawRoundedPath(
            fill,
            new Color(
                color.R,
                color.G,
                color.B,
                activeAlpha),
            baseWidth);
    }

    private static Vector2[] CreateSidePath(
        Vector2 center,
        float side,
        float halfHeight,
        float curve,
        float startRatio,
        float endRatio)
    {
        int pointCount =
            Mathf.Max(
                4,
                Mathf.CeilToInt(
                    (endRatio - startRatio) *
                    30.0f) + 1);
        Vector2[] points =
            new Vector2[pointCount];
        for (int index = 0;
            index < pointCount;
            index++)
        {
            float t = Mathf.Lerp(
                startRatio,
                endRatio,
                index /
                    (float)(pointCount - 1));
            points[index] =
                new Vector2(
                    center.X +
                        side *
                        Mathf.Sin(t * Mathf.Pi) *
                        curve,
                    center.Y -
                        halfHeight +
                        halfHeight * 2.0f * t);
        }
        return points;
    }

    private void DrawRoundedPath(
        Vector2[] points,
        Color color,
        float width)
    {
        if (points.Length < 2)
        {
            return;
        }
        DrawPolyline(
            points,
            color,
            width,
            true);
        float radius =
            width * 0.5f;
        DrawCircle(
            points[0],
            radius,
            color);
        DrawCircle(
            points[points.Length - 1],
            radius,
            color);
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
        float similarity =
            1.0f -
            Mathf.Clamp(distance / 0.82f, 0.0f, 1.0f);
        float lumaRisk =
            1.0f -
            Mathf.Clamp(
                Mathf.Abs(
                    sourceLuma -
                    backdropLuma) /
                    0.42f,
                0.0f,
                1.0f);
        float adaptation =
            Mathf.Clamp(
                Mathf.Max(
                    similarity,
                    lumaRisk) -
                    0.24f,
                0.0f,
                0.72f);
        Color contrastTarget = health
            ? (_backdropColor.R >
                _backdropColor.B
                ? new Color(1.0f, 0.08f, 0.58f)
                : new Color(1.0f, 0.46f, 0.24f))
            : (_backdropColor.B >
                _backdropColor.R
                ? new Color(0.22f, 1.0f, 0.92f)
                : new Color(0.22f, 0.72f, 1.0f));
        return source.Lerp(
            contrastTarget,
            adaptation);
    }
}
