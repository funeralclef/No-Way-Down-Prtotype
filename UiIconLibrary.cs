using Godot;
using System.Collections.Generic;

/// <summary>
/// Central semantic map for the imported no-padding SVG pack. Keeping paths
/// here prevents individual screens from drifting back to font glyphs.
/// </summary>
public static class UiIconLibrary
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public const string Info = "res://no-padding icons/8-ui/info-02.svg";
    public const string Home =
        "res://no-padding icons/6-buildings/house-03.svg";
    public const string Settings =
        "res://no-padding icons/8-ui/settings.svg";
    public const string Visible =
        "res://no-padding icons/8-ui/visible.svg";
    public const string Invisible =
        "res://no-padding icons/8-ui/invisible.svg";
    public const string Previous =
        "res://no-padding icons/8-ui/arrow-left-03.svg";
    public const string Next =
        "res://no-padding icons/8-ui/arrow-right-03.svg";
    public const string Up =
        "res://no-padding icons/8-ui/arrow-up-03.svg";
    public const string Down =
        "res://no-padding icons/8-ui/arrow-down-03.svg";
    public const string Confirm =
        "res://no-padding icons/8-ui/tick.svg";
    public const string Close =
        "res://no-padding icons/8-ui/cross.svg";
    public const string Restore =
        "res://no-padding icons/8-ui/restore-03.svg";
    public const string Play =
        "res://no-padding icons/9-media/play.svg";
    public const string Pause =
        "res://no-padding icons/9-media/pause.svg";
    public const string Volume =
        "res://no-padding icons/9-media/volume.svg";
    public const string Display =
        "res://no-padding icons/9-media/display.svg";
    public const string Controller =
        "res://no-padding icons/1-game/controller.svg";
    public const string Interface =
        "res://no-padding icons/8-ui/ui.svg";
    public const string Palette =
        "res://no-padding icons/10-editing/palette.svg";
    public const string Wardrobe =
        "res://no-padding icons/6-buildings/wardrobe.svg";
    public const string Exit =
        "res://no-padding icons/6-buildings/open-the-door.svg";
    public const string Retry =
        "res://no-padding icons/8-ui/refresh.svg";
    public const string Health =
        "res://no-padding icons/1-game/health.svg";
    public const string Nitro =
        "res://no-padding icons/7-vehicles/rocket.svg";
    public const string Trophy =
        "res://no-padding icons/1-game/trophy.svg";
    public const string Adjustment =
        "res://no-padding icons/8-ui/adjustment.svg";
    public const string Loadout =
        "res://no-padding icons/3-gear/shield-03.svg";

    public static Texture2D Get(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        if (Cache.TryGetValue(path, out Texture2D cached) && GodotObject.IsInstanceValid(cached)) return cached;
        Texture2D texture = null;
        string svg =
            FileAccess.GetFileAsString(
                path);
        if (!string.IsNullOrEmpty(svg))
        {
            svg = svg.Replace(
                "currentColor",
                "#ffffff");
            Image image = new();
            if (image.LoadSvgFromString(
                    svg,
                    2.0f) ==
                Error.Ok)
            {
                texture =
                    ImageTexture
                        .CreateFromImage(
                            image);
            }
        }
        if (texture == null)
        {
            Texture2D imported =
                GD.Load<Texture2D>(path);
            Image raster =
                imported?.GetImage();
            if (raster != null &&
                !raster.IsEmpty())
            {
                raster.Convert(
                    Image.Format.Rgba8);
                for (int y = 0;
                    y < raster.GetHeight();
                    y++)
                {
                    for (int x = 0;
                        x < raster.GetWidth();
                        x++)
                    {
                        Color pixel =
                            raster.GetPixel(
                                x,
                                y);
                        if (pixel.A > 0.0f)
                        {
                            raster.SetPixel(
                                    x,
                                    y,
                                    new Color(
                                        1.0f,
                                        1.0f,
                                        1.0f,
                                        pixel.A));
                        }
                    }
                }
                texture =
                    ImageTexture
                        .CreateFromImage(
                            raster);
            }
            texture ??= imported;
        }
        if (texture != null) Cache[path] = texture;
        return texture;
    }

    public static void Apply(
        Button button,
        string path,
        bool iconOnly = false,
        int maximumWidth = 22)
    {
        if (!GodotObject.IsInstanceValid(
                button))
        {
            return;
        }
        button.Icon = Get(path);
        button.ExpandIcon = true;
        // Godot exposes icon_max_width as a theme constant, including when ExpandIcon is enabled.
        button.AddThemeConstantOverride("icon_max_width", maximumWidth);
        button.IconAlignment =
            HorizontalAlignment.Center;
        button.AddThemeColorOverride(
            "icon_normal_color",
            new Color(
                0.82f,
                0.92f,
                0.96f));
        button.AddThemeColorOverride(
            "icon_hover_color",
            new Color(
                0.4f,
                0.9f,
                1.0f));
        button.AddThemeColorOverride(
            "icon_pressed_color",
            new Color(
                1.0f,
                0.58f,
                0.16f));
        button.AddThemeColorOverride(
            "icon_focus_color",
            new Color(
                1.0f,
                0.62f,
                0.18f));
        if (iconOnly)
        {
            button.Text = "";
        }
    }

    public static TextureRect Create(
        string path,
        float size,
        Color color)
    {
        return new TextureRect
        {
            Texture = Get(path),
            ExpandMode =
                TextureRect.ExpandModeEnum
                    .IgnoreSize,
            StretchMode =
                TextureRect.StretchModeEnum
                    .KeepAspectCentered,
            CustomMinimumSize =
                Vector2.One * size,
            MouseFilter =
                Control.MouseFilterEnum.Ignore,
            SelfModulate = color
        };
    }
}
