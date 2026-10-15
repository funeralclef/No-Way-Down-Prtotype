using Godot;

/// <summary>
/// Stable VSync presentation that does not inherit CheckButton's changing
/// icon margins between normal and hover states.
/// </summary>
public partial class SettingsToggleVisual : Control
{
    private static readonly Color Paper =
        new(1.0f, 0.985f, 0.95f, 1.0f);
    private static readonly Color Ember =
        new(1.0f, 0.43f, 0.055f, 1.0f);

    private CheckButton _toggle;
    private Texture2D _transparentIcon;
    private bool _signalsConnected;

    public void Configure(CheckButton toggle)
    {
        if (_signalsConnected &&
            IsInstanceValid(_toggle) &&
            _toggle != toggle)
        {
            _toggle.Toggled -= OnToggled;
            _toggle.Resized -= QueueRedraw;
            _signalsConnected = false;
        }

        _toggle = toggle;
        Name = "GradientToggleVisual";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(
            LayoutPreset.FullRect);
        _toggle.Text = string.Empty;
        _toggle.CustomMinimumSize = new Vector2(
            _toggle.CustomMinimumSize.X,
            Mathf.Max(
                36.0f,
                _toggle.CustomMinimumSize.Y));

        _transparentIcon ??=
            CreateTransparentIcon();
        foreach (string iconName in new[]
        {
            "checked",
            "checked_disabled",
            "checked_hover",
            "checked_hover_pressed",
            "checked_pressed",
            "unchecked",
            "unchecked_disabled",
            "unchecked_hover",
            "unchecked_hover_pressed",
            "unchecked_pressed"
        })
        {
            _toggle.AddThemeIconOverride(
                iconName,
                _transparentIcon);
        }

        if (!_signalsConnected)
        {
            _toggle.Toggled += OnToggled;
            _toggle.Resized += QueueRedraw;
            _signalsConnected = true;
        }
        QueueRedraw();
    }

    public void SetEnabledState(bool enabled)
    {
        if (IsInstanceValid(_toggle))
        {
            _toggle.TooltipText = enabled
                ? "V-Sync enabled"
                : "V-Sync disabled";
        }
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_signalsConnected &&
            IsInstanceValid(_toggle))
        {
            _toggle.Toggled -= OnToggled;
            _toggle.Resized -= QueueRedraw;
        }
        _signalsConnected = false;
    }

    public override void _Draw()
    {
        if (!IsInstanceValid(_toggle) ||
            Size.X < 80.0f)
        {
            return;
        }

        bool enabled = _toggle.ButtonPressed;
        string value = enabled
            ? "ENABLED"
            : "DISABLED";
        Font font = _toggle.GetThemeFont("font");
        int fontSize = Mathf.Max(
            10,
            _toggle.GetThemeFontSize("font_size"));
        Vector2 textSize = font.GetStringSize(
            value,
            HorizontalAlignment.Left,
            -1.0f,
            fontSize);
        float x = 10.0f;
        float baseline =
            (Size.Y - textSize.Y) * 0.5f +
            font.GetAscent(fontSize);
        DrawString(
            font,
            new Vector2(x, baseline),
            value,
            HorizontalAlignment.Left,
            -1.0f,
            fontSize,
            enabled
                ? new Color(
                    0.94f,
                    0.96f,
                    0.97f,
                    0.96f)
                : new Color(
                    0.7f,
                    0.75f,
                    0.78f,
                    0.84f));

        float railRight = Size.X - 10.0f;
        float railLeft = railRight - 42.0f;
        float railY = Mathf.Round(
            Size.Y * 0.5f);
        float radius = 7.5f;
        Color trackColour = enabled
            ? new Color(
                Ember.R,
                Ember.G,
                Ember.B,
                0.82f)
            : new Color(
                0.25f,
                0.31f,
                0.34f,
                0.72f);
        DrawRoundedPill(
            railLeft,
            railRight,
            railY,
            radius,
            trackColour,
            trackColour);
        DrawLine(
            new Vector2(
                railLeft + radius,
                railY - radius + 2.0f),
            new Vector2(
                railRight - radius,
                railY - radius + 2.0f),
            new Color(
                1.0f,
                1.0f,
                1.0f,
                enabled ? 0.17f : 0.08f),
            1.0f,
            true);

        float knobX = enabled
            ? railRight - radius
            : railLeft + radius;
        DrawCircle(
            new Vector2(knobX, railY),
            6.7f,
            new Color(
                0.0f,
                0.0f,
                0.0f,
                0.24f));
        DrawCircle(
            new Vector2(knobX, railY),
            5.4f,
            new Color(
                Paper.R,
                Paper.G,
                Paper.B,
                0.98f));
    }

    private void DrawRoundedPill(
        float left,
        float right,
        float y,
        float radius,
        Color leftColour,
        Color rightColour)
    {
        float bodyLeft = left + radius;
        float bodyRight = right - radius;
        DrawPolygon(
            new[]
            {
                new Vector2(bodyLeft, y - radius),
                new Vector2(bodyRight, y - radius),
                new Vector2(bodyRight, y + radius),
                new Vector2(bodyLeft, y + radius)
            },
            new[]
            {
                leftColour,
                rightColour,
                rightColour,
                leftColour
            });
        DrawCircle(
            new Vector2(bodyLeft, y),
            radius,
            leftColour);
        DrawCircle(
            new Vector2(bodyRight, y),
            radius,
            rightColour);
    }

    private void OnToggled(bool enabled)
    {
        SetEnabledState(enabled);
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
