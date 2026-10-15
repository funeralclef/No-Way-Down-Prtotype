using Godot;

/// <summary>
/// A quiet four-point rail for the authored ascent. The marker is driven by
/// world distance, not elapsed time, so stopping, falling back, and entering
/// an arena are all reflected honestly.
/// </summary>
public partial class RunProgressHud : Control
{
    private static readonly Color Ink =
        new(0.012f, 0.024f, 0.044f, 0.16f);
    private static readonly Color Muted =
        new(0.8f, 0.88f, 0.9f, 0.7f);
    private static readonly Color Cyan =
        new(0.42f, 0.8f, 1.0f, 0.96f);
    private static readonly Color Checkpoint =
        new(1.0f, 0.72f, 0.79f, 0.92f);
    private static readonly Color Paper =
        new(0.94f, 0.98f, 0.99f, 0.96f);
    private static readonly Color Final =
        new(1.0f, 0.14f, 0.22f, 0.98f);

    private const int CoreArenaCount = 4;

    private Player _player;
    private ProceduralWorld _world;
    private UiAccessibility _accessibility;
    private Label _percentage;
    private RunProgressSnapshot _snapshot;
    private float _displayedCoreProgress;
    private float _displayedSegmentProgress;
    private StyleBoxFlat _panelStyle;
    private Rect2 _panelRect;
    private Vector2 _trackStart;
    private Vector2 _trackEnd;
    private float _trackY;
    private float _percentageDividerX;
    private float _layoutScale = 1.0f;
    private bool _initialized;

    public float ReservedHeight =>
        36.0f * _layoutScale;

    public void Initialize(
        Player player,
        ProceduralWorld world)
    {
        _player = player;
        _world = world;
        _initialized = true;
        if (IsInsideTree())
        {
            RefreshSnapshot(true);
        }
    }

    public override void _Ready()
    {
        _accessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _panelStyle = new StyleBoxFlat
        {
            BgColor = Ink,
            BorderColor =
                new Color(
                    Cyan.R,
                    Cyan.G,
                    Cyan.B,
                    0.26f),
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2
        };
        _percentage =
            GetNode<Label>(
                "ProgressPercent");
        GetViewport().SizeChanged +=
            ApplyResponsiveLayout;
        if (_accessibility != null)
        {
            _accessibility.ScaleChanged +=
                OnAccessibilityScaleChanged;
        }
        ApplyResponsiveLayout();
        RefreshSnapshot(true);
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -=
                ApplyResponsiveLayout;
        }
        if (_accessibility != null)
        {
            _accessibility.ScaleChanged -=
                OnAccessibilityScaleChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (!_initialized ||
            !IsInstanceValid(_player) ||
            !IsInstanceValid(_world))
        {
            return;
        }

        float deltaSeconds =
            Mathf.Clamp((float)delta, 0.0f, 0.1f);
        RefreshSnapshot(false);
        float response = 1.0f -
            Mathf.Exp(-deltaSeconds * 10.5f);
        _displayedCoreProgress = Mathf.Lerp(
            _displayedCoreProgress,
            _snapshot.CoreProgress,
            response);
        _displayedSegmentProgress = Mathf.Lerp(
            _displayedSegmentProgress,
            _snapshot.SegmentProgress,
            response);
        UpdatePercentageReadout();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_panelStyle != null)
        {
            DrawStyleBox(
                _panelStyle,
                _panelRect);
        }

        float width = Mathf.Max(
            1.0f,
            _trackEnd.X - _trackStart.X);
        Vector2 start =
            new(_trackStart.X, _trackY);
        Vector2 end =
            new(_trackEnd.X, _trackY);

        DrawLine(
            new Vector2(
                _percentageDividerX,
                _panelRect.Position.Y +
                    5.0f * _layoutScale),
            new Vector2(
                _percentageDividerX,
                _panelRect.End.Y -
                    5.0f * _layoutScale),
            new Color(
                Checkpoint.R,
                Checkpoint.G,
                Checkpoint.B,
                0.3f),
            1.0f,
            true);

        DrawLine(
            start,
            end,
            new Color(
                0.01f,
                0.025f,
                0.04f,
                0.38f),
            3.0f * _layoutScale,
            true);
        DrawLine(
            start,
            end,
            Muted,
            1.25f * _layoutScale,
            true);

        if (!_snapshot.CoreCompleted)
        {
            Vector2 fillEnd = new(
                start.X + width *
                    Mathf.Clamp(
                        _displayedCoreProgress,
                        0.0f,
                        1.0f),
                _trackY);
            DrawLine(
                start,
                fillEnd,
                Cyan,
                2.2f * _layoutScale,
                true);
            DrawPlayerMarker(
                fillEnd,
                Cyan);
        }
        else
        {
            DrawLine(
                start,
                end,
                Cyan,
                1.8f * _layoutScale,
                true);
            Vector2 beyondMarker = new(
                start.X + width *
                    Mathf.Clamp(
                        _displayedSegmentProgress,
                        0.0f,
                        1.0f),
                _trackY);
            DrawLine(
                start,
                beyondMarker,
                new Color(
                    Final.R,
                    Final.G,
                    Final.B,
                    0.72f),
                2.0f * _layoutScale,
                true);
            DrawPlayerMarker(
                beyondMarker,
                Final);
        }

        for (int index = 1;
            index <= CoreArenaCount;
            index++)
        {
            float ratio =
                index /
                (float)CoreArenaCount;
            Vector2 point = new(
                start.X + width * ratio,
                _trackY);
            bool completed =
                _snapshot.CompletedArenas >= index;
            bool reached =
                !_snapshot.CoreCompleted &&
                _snapshot.TargetArena == index &&
                _snapshot.ArenaReached;
            Color accent = index == CoreArenaCount
                ? Final
                : Checkpoint;
            float radius =
                (index == CoreArenaCount
                    ? 3.75f
                    : 3.3f) *
                _layoutScale;
            DrawCircle(
                point,
                radius + 1.8f *
                    _layoutScale,
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    completed || reached
                        ? 0.18f
                        : 0.08f));
            DrawCircle(
                point,
                radius,
                completed || reached
                    ? accent
                        : index == CoreArenaCount
                        ? new Color(
                            Final.R,
                            Final.G,
                            Final.B,
                            0.74f)
                        : new Color(
                            Checkpoint.R,
                            Checkpoint.G,
                            Checkpoint.B,
                            0.76f));

            if (reached)
            {
                DrawArc(
                    point,
                    radius + 3.0f *
                        _layoutScale,
                    0.0f,
                    Mathf.Tau,
                    24,
                    new Color(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.56f),
                    1.0f * _layoutScale,
                    true);
            }
        }
    }

    private void DrawPlayerMarker(
        Vector2 point,
        Color accent)
    {
        DrawCircle(
            point,
            7.0f * _layoutScale,
            new Color(
                0.01f,
                0.025f,
                0.04f,
                0.28f));
        DrawCircle(
            point,
            5.8f * _layoutScale,
            new Color(
                accent.R,
                accent.G,
                accent.B,
                0.28f));
        DrawArc(
            point,
            4.2f * _layoutScale,
            0.0f,
            Mathf.Tau,
            20,
            new Color(
                accent.R,
                accent.G,
                accent.B,
                0.66f),
            1.0f * _layoutScale,
            true);
        DrawCircle(
            point,
            2.65f * _layoutScale,
            Paper);
    }

    private void UpdatePercentageReadout()
    {
        if (!IsInstanceValid(_percentage))
        {
            return;
        }

        int percent = Mathf.Clamp(
            Mathf.RoundToInt(
                _displayedCoreProgress *
                    100.0f),
            0,
            100);
        _percentage.Text = $"{percent}%";
        _percentage.AddThemeColorOverride(
            "font_color",
            percent >= 100
                ? Final
                : new Color(
                    Checkpoint.R,
                    Checkpoint.G,
                    Checkpoint.B,
                    0.98f));
    }

    private void RefreshSnapshot(bool snap)
    {
        if (!_initialized ||
            !IsInstanceValid(_player) ||
            !IsInstanceValid(_world))
        {
            return;
        }
        _snapshot = _world.GetRunProgress(
            _player.GlobalPosition.Z,
            CoreArenaCount);
        if (snap)
        {
            _displayedCoreProgress =
                _snapshot.CoreProgress;
            _displayedSegmentProgress =
                _snapshot.SegmentProgress;
        }
        UpdatePercentageReadout();
        QueueRedraw();
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsInsideTree() ||
            !IsInstanceValid(_percentage))
        {
            return;
        }

        Vector2 viewport =
            GetViewport()
                .GetVisibleRect()
                .Size;
        Vector4 safe =
            _accessibility
                ?.GetSafeInsets(viewport) ??
            Vector4.Zero;
        _layoutScale =
            _accessibility?.LayoutScale ??
            1.0f;
        bool compact =
            viewport.X < 760.0f ||
            viewport.Y > viewport.X * 1.12f ||
            _accessibility?.IsTouchLayout == true;
        float safeWidth = Mathf.Max(
            240.0f,
            viewport.X - safe.X - safe.Z);
        float width = Mathf.Min(
            (compact ? 340.0f : 376.0f) *
                _layoutScale,
            safeWidth *
                (compact ? 0.78f : 0.4f));
        width = Mathf.Clamp(
            width,
            Mathf.Min(
                250.0f * _layoutScale,
                safeWidth - 16.0f),
            safeWidth - 16.0f);
        float centerX =
            safe.X + safeWidth * 0.5f;
        float top =
            safe.Y +
            (compact ? 7.0f : 10.0f) *
                _layoutScale;
        _panelRect = new Rect2(
            centerX - width * 0.5f,
            top,
            width,
            (compact ? 26.0f : 22.0f) *
                _layoutScale);
        _trackStart = new Vector2(
            _panelRect.Position.X +
                11.0f * _layoutScale,
            0.0f);
        _trackEnd = new Vector2(
            _panelRect.End.X -
                51.0f * _layoutScale,
            0.0f);
        _percentageDividerX =
            _panelRect.End.X -
                45.0f * _layoutScale;
        _trackY =
            _panelRect.Position.Y +
                _panelRect.Size.Y * 0.5f;
        _percentage.Position =
            new Vector2(
                _percentageDividerX +
                    5.0f * _layoutScale,
                _panelRect.Position.Y);
        _percentage.Size =
            new Vector2(
                38.0f * _layoutScale,
                _panelRect.Size.Y);
        _percentage.HorizontalAlignment =
            HorizontalAlignment.Center;
        _percentage.AddThemeFontSizeOverride(
            "font_size",
            Mathf.Max(
                9,
                Mathf.RoundToInt(
                    10.0f *
                    (_accessibility
                        ?.EffectiveTextScale ??
                        1.0f))));
        QueueRedraw();
    }

    private void OnAccessibilityScaleChanged(
        float uiScale,
        float textScale)
    {
        ApplyResponsiveLayout();
    }
}
