using Godot;

/// <summary>
/// Keeps the interface on one restrained visual grammar without changing the
/// authored colours, opacity or backgrounds of individual screens.
/// </summary>
public partial class UiVisualStyle : Node
{
    private static readonly StringName UsesSelectionLightMeta =
        "_nwd_uses_selection_light";

    public const int UnifiedCornerRadius = 4;

    private static readonly string[] SurfaceStyles =
    {
        "panel",
        "normal",
        "hover",
        "pressed",
        "focus",
        "disabled",
        "background",
        "fill",
        "slider",
        "grabber_area",
        "grabber_area_highlight",
        "tab_selected",
        "tab_unselected",
        "tab_hovered",
        "popup"
    };

    private ShaderMaterial _selectionMaterial;

    public override void _EnterTree()
    {
        _selectionMaterial =
            CreateSelectionMaterial();
    }

    public override void _Ready()
    {
        GetTree().NodeAdded += OnNodeAdded;
        RefreshTree();
    }

    public override void _ExitTree()
    {
        if (GetTree() != null)
        {
            GetTree().NodeAdded -= OnNodeAdded;
        }
    }

    public void RefreshTree()
    {
        if (GetTree()?.Root == null)
        {
            return;
        }
        ApplyBranch(GetTree().Root);
    }

    private void OnNodeAdded(Node node)
    {
        if (node is not Control control)
        {
            return;
        }

        Callable.From(() =>
        {
            if (IsInstanceValid(control))
            {
                ApplyControl(control);
            }
        }).CallDeferred();
    }

    private void ApplyBranch(Node root)
    {
        if (root is Control control)
        {
            ApplyControl(control);
        }
        foreach (Node child in root.GetChildren())
        {
            ApplyBranch(child);
        }
    }

    private void ApplyControl(Control control)
    {
        bool readableButton =
            control is Button button &&
            control is not OptionButton &&
            control is not CheckButton &&
            !string.IsNullOrWhiteSpace(button.Text);
        bool usesSelectionLight =
            control is BaseButton &&
            control is not OptionButton &&
            control is not CheckButton &&
            (readableButton ||
                (control.HasMeta(UsesSelectionLightMeta) &&
                    control.GetMeta(UsesSelectionLightMeta).AsBool()) ||
                HasAuthoredUnderline(control));

        if (usesSelectionLight)
        {
            control.SetMeta(UsesSelectionLightMeta, true);
        }

        foreach (string styleName in SurfaceStyles)
        {
            if (!control.HasThemeStylebox(styleName) ||
                control.GetThemeStylebox(styleName) is not
                    StyleBoxFlat source)
            {
                continue;
            }

            StyleBoxFlat flat = source;
            if (!control.HasThemeStyleboxOverride(styleName))
            {
                flat = source.Duplicate() as StyleBoxFlat;
                if (flat == null)
                {
                    continue;
                }
                control.AddThemeStyleboxOverride(styleName, flat);
            }

            bool hasSurface =
                flat.BgColor.A > 0.01f ||
                flat.BorderWidthLeft > 0 ||
                flat.BorderWidthTop > 0 ||
                flat.BorderWidthRight > 0 ||
                flat.BorderWidthBottom > 0;
            if (!hasSurface &&
                control is not BaseButton &&
                control is not Slider &&
                control is not ProgressBar)
            {
                continue;
            }

            flat.CornerRadiusTopLeft = UnifiedCornerRadius;
            flat.CornerRadiusTopRight = UnifiedCornerRadius;
            flat.CornerRadiusBottomLeft = UnifiedCornerRadius;
            flat.CornerRadiusBottomRight = UnifiedCornerRadius;

            if (usesSelectionLight && IsUnderlineStyle(flat))
            {
                SuppressLegacyUnderline(flat);
            }
        }

        if (usesSelectionLight)
        {
            EnsureSelectionLight(control);
        }
    }

    private void EnsureSelectionLight(Control control)
    {
        UiSelectionLight light =
            control.GetNodeOrNull<UiSelectionLight>(
                "SelectionLightLayer");
        if (!IsInstanceValid(light))
        {
            light = new UiSelectionLight
            {
                Name = "SelectionLightLayer"
            };
            control.AddChild(light);
        }
        light.Configure(control, _selectionMaterial);
    }

    private static bool HasAuthoredUnderline(Control control)
    {
        string[] activeStyles =
        {
            "hover",
            "pressed",
            "focus",
            "hover_pressed"
        };
        foreach (string styleName in activeStyles)
        {
            if (control.HasThemeStylebox(styleName) &&
                control.GetThemeStylebox(styleName) is
                    StyleBoxFlat flat &&
                IsUnderlineStyle(flat))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsUnderlineStyle(StyleBoxFlat style)
    {
        return style.BorderWidthBottom > 0 &&
            style.BorderWidthBottom > style.BorderWidthTop &&
            style.BorderWidthBottom > style.BorderWidthLeft &&
            style.BorderWidthBottom > style.BorderWidthRight;
    }

    private static void SuppressLegacyUnderline(StyleBoxFlat style)
    {
        style.BorderWidthLeft = 0;
        style.BorderWidthTop = 0;
        style.BorderWidthRight = 0;
        style.BorderWidthBottom = 0;
        style.BorderColor = Colors.Transparent;
    }

    private static ShaderMaterial CreateSelectionMaterial()
    {
        Shader shader = new()
        {
            Code = @"
shader_type canvas_item;
render_mode unshaded;

void fragment() {
    float taper =
        smoothstep(0.0, 0.12, UV.x) *
        smoothstep(1.0, 0.88, UV.x);
    float centre =
        1.0 - abs(UV.x * 2.0 - 1.0);
    vec3 colour = mix(
        vec3(1.0, 0.98, 0.93),
        vec3(1.0, 0.42, 0.055),
        smoothstep(0.26, 0.9, UV.x));
    COLOR = vec4(
        colour,
        taper * (0.58 + centre * 0.42));
}"
        };
        return new ShaderMaterial
        {
            Shader = shader
        };
    }
}
