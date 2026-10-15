using Godot;

/// <summary>Small shared view primitives for the guide and build panels.</summary>
public static class BuffUi
{
    public static readonly Color Text = new("#edf2f6");
    public static readonly Color Muted = new("#a9b8c8");
    public static readonly Color Accent = new("#efaa65");

    public static StyleBoxFlat Surface(Color accent, bool highlighted = false) => new()
    {
        BgColor = highlighted ? new Color(.16f, .20f, .23f, .35f) : new Color(.045f, .065f, .085f, .12f),
        BorderColor = new Color(accent, highlighted ? .65f : .12f),
        BorderWidthLeft = 0, BorderWidthTop = 0, BorderWidthRight = 0, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
        ContentMarginLeft = 16, ContentMarginRight = 16,
        ContentMarginTop = 14, ContentMarginBottom = 14
    };

    public static Label Label(string text, int size = 14, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
        return label;
    }

    public static Button IconButton(string icon, string tooltip)
    {
        var button = new Button { Flat = true, Text = "", TooltipText = tooltip,
            CustomMinimumSize = new Vector2(32, 42), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        UiIconLibrary.Apply(button, icon, true, 18);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        foreach (string state in new[] { "hover", "focus", "pressed" })
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = new Color(1, 1, 1, .06f),
                BorderColor = Accent, BorderWidthBottom = state == "focus" ? 1 : 0 });
        button.AddThemeColorOverride("icon_normal_color", Muted);
        button.AddThemeColorOverride("icon_hover_color", Text);
        return button;
    }

    public static Button Button(string text)
    {
        var button = new Button { Text = text, ClipText = true, CustomMinimumSize = new Vector2(88, 44),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        MenuVisualStyle.Action(button, 14, true);
        return button;
    }
}
