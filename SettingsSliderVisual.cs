using Godot;

/// <summary>
/// A restrained, theme-independent slider rail. The native HSlider keeps all
/// input and focus behaviour while this child owns the stable visual layer.
/// </summary>
public partial class SettingsSliderVisual : Control
{
    private static readonly Color Paper =
        new(1.0f, 0.985f, 0.95f, 1.0f);
    private static readonly Color Ember =
        new(1.0f, 0.43f, 0.055f, 1.0f);
    private static readonly Color Quiet =
        new(0.72f, 0.8f, 0.84f, 0.2f);

    private HSlider _slider;
    private Texture2D _transparentIcon;
    private bool _signalsConnected;

    public void Configure(HSlider slider)
    {
        if (_signalsConnected &&
            IsInstanceValid(_slider) &&
            _slider != slider)
        {
            _slider.ValueChanged -= OnValueChanged;
            _slider.Resized -= QueueRedraw;
            _signalsConnected = false;
        }

        _slider = slider;
        Name = "GradientSliderVisual";
        MouseFilter = MouseFilterEnum.Ignore;
        ShowBehindParent = true;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(
            LayoutPreset.FullRect);

        StyleBoxFlat transparent = new()
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            ContentMarginTop = 1.0f,
            ContentMarginBottom = 1.0f
        };
        _slider.AddThemeStyleboxOverride(
            "slider",
            transparent);
        _slider.AddThemeStyleboxOverride(
            "grabber_area",
            transparent.Duplicate() as StyleBoxFlat);
        _slider.AddThemeStyleboxOverride(
            "grabber_area_highlight",
            transparent.Duplicate() as StyleBoxFlat);

        _transparentIcon ??=
            CreateTransparentIcon();
        foreach (string iconName in new[]
        {
            "grabber",
            "grabber_highlight",
            "grabber_disabled"
        })
        {
            _slider.AddThemeIconOverride(
                iconName,
                _transparentIcon);
        }

        if (!_signalsConnected)
        {
            _slider.ValueChanged += OnValueChanged;
            _slider.Resized += QueueRedraw;
            _signalsConnected = true;
        }
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_signalsConnected &&
            IsInstanceValid(_slider))
        {
            _slider.ValueChanged -= OnValueChanged;
            _slider.Resized -= QueueRedraw;
        }
        _signalsConnected = false;
    }

    public override void _Draw()
    {
        if (!IsInstanceValid(_slider) ||
            Size.X < 20.0f)
        {
            return;
        }

        float left = 7.0f;
        float right = Mathf.Max(
            left + 1.0f,
            Size.X - 7.0f);
        float y = Mathf.Round(Size.Y * 0.5f);
        float span = right - left;
        float range = Mathf.Max(
            0.0001f,
            (float)(_slider.MaxValue -
                _slider.MinValue));
        float ratio = Mathf.Clamp(
            (float)(_slider.Value -
                _slider.MinValue) / range,
            0.0f,
            1.0f);

        DrawGradientRail(
            left,
            right,
            y,
            1.25f,
            0.18f,
            0.0f,
            1.0f);

        float fillRight = left + span * ratio;
        if (fillRight > left + 0.05f)
        {
            DrawGradientRail(
                left,
                fillRight,
                y,
                1.85f,
                0.96f,
                0.0f,
                ratio);
        }

        Vector2 knob = new(
            left + span * ratio,
            Mathf.Round(y));
        Color knobColour = Paper.Lerp(
            Ember,
            Mathf.SmoothStep(
                0.14f,
                0.92f,
                ratio));
        DrawCircle(
            knob,
            6.5f,
            new Color(
                0.0f,
                0.0f,
                0.0f,
                0.22f));
        DrawCircle(
            knob,
            5.8f,
            knobColour);
        DrawCircle(
            knob,
            4.7f,
            new Color(
                Paper.R,
                Paper.G,
                Paper.B,
                0.98f));
    }

    private void DrawGradientRail(
        float left,
        float right,
        float y,
        float thickness,
        float alpha,
        float leftRatio,
        float rightRatio)
    {
        float half = thickness * 0.5f;
        Color leftColour = Paper.Lerp(
            Ember,
            Mathf.SmoothStep(
                0.14f,
                0.92f,
                leftRatio));
        Color rightColour = Paper.Lerp(
            Ember,
            Mathf.SmoothStep(
                0.14f,
                0.92f,
                rightRatio));
        leftColour.A = alpha;
        rightColour.A = alpha;

        DrawPolygon(
            new[]
            {
                new Vector2(left, y - half),
                new Vector2(right, y - half),
                new Vector2(right, y + half),
                new Vector2(left, y + half)
            },
            new[]
            {
                leftColour,
                rightColour,
                rightColour,
                leftColour
            });
        DrawCircle(
            new Vector2(left, y),
            half,
            leftColour);
        DrawCircle(
            new Vector2(right, y),
            half,
            rightColour);
    }

    private void OnValueChanged(double value)
    {
        QueueRedraw();
    }

    private static Texture2D CreateTransparentIcon()
    {
        Image image = Image.CreateEmpty(
            1,
            1,
            false,
            Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        return ImageTexture.CreateFromImage(image);
    }
}
