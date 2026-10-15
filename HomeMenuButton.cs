using Godot;

/// <summary>
/// A font-independent home icon, so the run menu remains readable on every
/// platform even when the active font has no house glyph.
/// </summary>
public partial class HomeMenuButton : Button
{
    public override void _Ready()
    {
        Text = "";
        UiIconLibrary.Apply(
            this,
            UiIconLibrary.Home,
            true,
            28);
        AddThemeColorOverride(
            "icon_normal_color",
            new Color(
                0.08f,
                0.2f,
                0.24f));
        AddThemeColorOverride(
            "icon_hover_color",
            new Color(
                0.02f,
                0.58f,
                0.7f));
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsInstanceValid(Icon))
        {
            return;
        }
        float side = Mathf.Min(Size.X, Size.Y);
        float unit = side * 0.24f;
        float line = Mathf.Max(2.0f, side * 0.052f);
        Vector2 centre = Size * 0.5f +
            new Vector2(0.0f, side * 0.025f);
        Color color = ButtonPressed
            ? new Color(1.0f, 0.56f, 0.16f)
            : IsHovered()
                ? new Color(0.02f, 0.58f, 0.7f)
                : new Color(0.08f, 0.2f, 0.24f);

        Vector2 roofLeft =
            centre + new Vector2(-unit, -unit * 0.08f);
        Vector2 roofTop =
            centre + new Vector2(0.0f, -unit * 0.92f);
        Vector2 roofRight =
            centre + new Vector2(unit, -unit * 0.08f);
        Vector2 wallLeft =
            centre + new Vector2(-unit * 0.72f, -unit * 0.02f);
        Vector2 wallRight =
            centre + new Vector2(unit * 0.72f, -unit * 0.02f);
        Vector2 floorLeft =
            centre + new Vector2(-unit * 0.72f, unit * 0.82f);
        Vector2 floorRight =
            centre + new Vector2(unit * 0.72f, unit * 0.82f);

        DrawLine(roofLeft, roofTop, color, line, true);
        DrawLine(roofTop, roofRight, color, line, true);
        DrawLine(wallLeft, floorLeft, color, line, true);
        DrawLine(wallRight, floorRight, color, line, true);
        DrawLine(floorLeft, floorRight, color, line, true);

        float doorHalf = unit * 0.2f;
        Vector2 doorTop =
            centre + new Vector2(0.0f, unit * 0.26f);
        DrawLine(
            doorTop + Vector2.Left * doorHalf,
            floorLeft + Vector2.Right * (unit * 0.52f),
            color,
            line * 0.8f,
            true);
        DrawLine(
            doorTop + Vector2.Right * doorHalf,
            floorRight + Vector2.Left * (unit * 0.52f),
            color,
            line * 0.8f,
            true);
        DrawLine(
            doorTop + Vector2.Left * doorHalf,
            doorTop + Vector2.Right * doorHalf,
            color,
            line * 0.8f,
            true);
    }
}
