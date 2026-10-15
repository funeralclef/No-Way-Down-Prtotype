using Godot;

/// <summary>
/// Owns coordinated interface and text scaling for every scene.
/// UI scale grows controls; text scale grows typography while layout
/// automatically reserves enough room for the larger of the two.
/// </summary>
public partial class UiAccessibility : Node
{
    [Signal]
    public delegate void ScaleChangedEventHandler(
        float uiScale,
        float textScale);
    [Signal]
    public delegate void VitalsScaleChangedEventHandler(
        float vitalsScale);

    private const string SettingsPath =
        "user://display.cfg";
    private const string Section = "accessibility";
    private static readonly StringName BaseFontMeta =
        "_nwd_base_font";
    private static readonly StringName BaseMinimumMeta =
        "_nwd_base_minimum";

    public bool ForceTouchPreview { get; set; }
    public float UiScale { get; private set; } = 1.0f;
    public float TextScale { get; private set; } = 1.0f;
    public float VitalsScale { get; private set; } = 1.15f;
    public float EffectiveTextScale =>
        Mathf.Clamp(
            UiScale * TextScale,
            0.75f,
            1.75f);
    public float LayoutScale =>
        Mathf.Max(
            UiScale,
            EffectiveTextScale);
    public bool IsTouchLayout =>
        ForceTouchPreview ||
        OS.HasFeature("mobile") ||
        DisplayServer.IsTouchscreenAvailable();

    public void SetTouchPreview(bool enabled)
    {
        ForceTouchPreview = enabled;
    }

    public override void _EnterTree()
    {
        LoadSettings();
    }

    public void SetScales(
        float uiScale,
        float textScale)
    {
        float nextUi =
            Mathf.Clamp(uiScale, 0.8f, 1.4f);
        float nextText =
            Mathf.Clamp(textScale, 0.9f, 1.35f);
        if (Mathf.IsEqualApprox(nextUi, UiScale) &&
            Mathf.IsEqualApprox(
                nextText,
                TextScale))
        {
            return;
        }

        UiScale = nextUi;
        TextScale = nextText;
        EmitSignal(
            SignalName.ScaleChanged,
            UiScale,
            TextScale);
    }

    public void SetVitalsScale(float vitalsScale)
    {
        float next =
            Mathf.Clamp(vitalsScale, 0.75f, 1.5f);
        if (Mathf.IsEqualApprox(
            next,
            VitalsScale))
        {
            return;
        }
        VitalsScale = next;
        EmitSignal(
            SignalName.VitalsScaleChanged,
            VitalsScale);
    }

    public Vector4 GetSafeInsets(
        Vector2 viewportSize)
    {
        if (!IsTouchLayout)
        {
            return Vector4.Zero;
        }

        Rect2I safeArea =
            DisplayServer.GetDisplaySafeArea();
        Vector2I screenSize =
            DisplayServer.ScreenGetSize();
        if (screenSize.X <= 0 ||
            screenSize.Y <= 0 ||
            safeArea.Size.X <= 0 ||
            safeArea.Size.Y <= 0)
        {
            return Vector4.Zero;
        }

        return new Vector4(
            safeArea.Position.X /
                (float)screenSize.X *
                viewportSize.X,
            safeArea.Position.Y /
                (float)screenSize.Y *
                viewportSize.Y,
            (screenSize.X - safeArea.End.X) /
                (float)screenSize.X *
                viewportSize.X,
            (screenSize.Y - safeArea.End.Y) /
                (float)screenSize.Y *
                viewportSize.Y);
    }

    public void ApplyControlTree(
        Control root)
    {
        ApplyControl(root);
        foreach (Node child in root.GetChildren())
        {
            if (child is Control control)
            {
                ApplyControlTree(control);
            }
        }
    }

    private void LoadSettings()
    {
        bool touch = IsTouchLayout;
        float defaultUi =
            touch ? 1.2f : 1.0f;
        float defaultText =
            touch ? 1.1f : 1.0f;
        ConfigFile settings = new();
        if (settings.Load(SettingsPath) !=
            Error.Ok)
        {
            UiScale = defaultUi;
            TextScale = defaultText;
            VitalsScale = 1.15f;
            return;
        }

        UiScale = Mathf.Clamp(
            (float)settings.GetValue(
                Section,
                "ui_scale",
                defaultUi).AsDouble(),
            0.8f,
            1.4f);
        TextScale = Mathf.Clamp(
            (float)settings.GetValue(
                Section,
                "text_scale",
                defaultText).AsDouble(),
            0.9f,
            1.35f);
        VitalsScale = Mathf.Clamp(
            (float)settings.GetValue(
                Section,
                "vitals_scale",
                1.15f)
                .AsDouble(),
            0.75f,
            1.5f);
    }

    private void ApplyControl(
        Control control)
    {
        if (!control.HasMeta(BaseFontMeta))
        {
            control.SetMeta(
                BaseFontMeta,
                control.GetThemeFontSize(
                    "font_size"));
        }
        int baseFont =
            control.GetMeta(
                BaseFontMeta).AsInt32();
        if (IsTouchLayout)
        {
            baseFont = Mathf.Max(
                baseFont,
                control is BaseButton
                    ? 16
                    : 14);
        }
        control.AddThemeFontSizeOverride(
            "font_size",
            Mathf.Max(
                8,
                Mathf.RoundToInt(
                    baseFont *
                    EffectiveTextScale)));

        if (!control.HasMeta(BaseMinimumMeta))
        {
            control.SetMeta(
                BaseMinimumMeta,
                control.CustomMinimumSize);
        }
        Vector2 baseMinimum =
            control.GetMeta(
                BaseMinimumMeta).AsVector2();
        Vector2 scaled =
            baseMinimum * LayoutScale;
        if (IsTouchLayout &&
            (control is BaseButton ||
                control is Slider))
        {
            scaled.Y = Mathf.Max(
                scaled.Y,
                44.0f * LayoutScale);
        }
        control.CustomMinimumSize = scaled;
    }
}
