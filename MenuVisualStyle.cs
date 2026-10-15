using Godot;

/// <summary>Shared transparent actions and optical backdrop for pause, settings and field panels.</summary>
public static class MenuVisualStyle
{
    public static ShaderMaterial FocusMaterial() => new() { Shader = GD.Load<Shader>("res://shaders/menu_glass.gdshader") };

    public static void Action(Button button, int fontSize = 15, bool centered = false)
    {
        button.Alignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat {
                BgColor = Colors.Transparent,
                BorderColor = new Color("#efaa65"),
                BorderWidthBottom = state is "focus" or "pressed" ? 2 : state == "hover" ? 1 : 0,
                ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 });
        button.AddThemeColorOverride("font_color", BuffUi.Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(BuffUi.Muted, .45f));
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }
}
