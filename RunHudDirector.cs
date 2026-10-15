using Godot;

/// <summary>
/// Gives the run HUD one visual language and keeps its information rails
/// separated at every supported aspect ratio.
/// </summary>
public partial class RunHudDirector : Node
{
    private Control _interface;
    private PanelContainer _score;
    private PanelContainer _health;
    private PanelContainer _location;
    private PanelContainer _momentum;
    private PanelContainer _nitro;
    private RunProgressHud _runProgress;
    private Label _momentumMultiplier;
    private RunPauseMenu _pauseMenu;
    private ProgressBar _healthBar;
    private ProgressBar _nitroBar;
    private Label _healthPercent;
    private Label _nitroPercent;
    private Label _healthTitle;
    private Label _nitroTitle;
    private PanelContainer _healthChip;
    private PanelContainer _nitroChip;
    private Control _contractSlot;
    private PanelContainer _contractFrame;
    private RichTextLabel _contractText;
    private StyleBoxFlat _healthChipStyle;
    private StyleBoxFlat _nitroChipStyle;
    private UiAccessibility _uiAccessibility;
    private Player _player;
    private Camera3D _camera;
    private GroundVitalsIndicator _groundVitals;
    private Line2D _statusLink;
    private Line2D _contractLink;
    private Line2D _scoreLink;
    private Line2D _momentumLink;
    private float _projectionTime;
    private float _statusWidth;
    private float _healthHeight;
    private float _nitroHeight;
    private float _statusBottomLimit;
    private Vector2 _lastPlayerProjection;
    private Vector3 _lastCameraPosition;
    private bool _vitalsProjectionInitialized;
    private int _lastHealthReadout = -1;
    private int _lastNitroReadout = -1;
    private float _healthReadoutPulse;
    private float _nitroReadoutPulse;
    private float _contractReveal;
    private float _contractExpandedWidth = 220.0f;
    private float _momentumReveal;
    private Vector2 _momentumHiddenPosition;
    private Vector2 _momentumShownPosition;
    private Vector2 _locationRestPosition;
    private bool _runEnded;

    public Vector2 LocationBannerRestPosition =>
        _locationRestPosition.IsZeroApprox() &&
        IsInstanceValid(_location)
            ? _location.Position
            : _locationRestPosition;

    public void Initialize(Control userInterface)
    {
        _interface = userInterface;
        _score = _interface.GetNode<PanelContainer>(
            "ScoreLabel");
        _health = _interface.GetNode<PanelContainer>(
            "HealthDisplay");
        _location = _interface.GetNode<PanelContainer>(
            "LocationBanner");
        _momentum = _interface.GetNode<PanelContainer>(
            "MomentumDisplay");
        _nitro = _interface.GetNode<PanelContainer>(
            "NitroDisplay");
        _momentumMultiplier =
            _interface.GetNode<Label>(
                "MomentumDisplay/Margin/Content/Multiplier");
        _pauseMenu =
            _interface.GetNodeOrNull<RunPauseMenu>(
                "RunOverlays/RunPauseMenu");
        _healthBar = _interface.GetNode<ProgressBar>(
            "HealthDisplay/Margin/Content/HealthRow/Bar");
        _nitroBar = _interface.GetNode<ProgressBar>(
            "NitroDisplay/Margin/Content/Bar");
        _uiAccessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _player =
            GetTree().GetFirstNodeInGroup(
                "player") as Player;
        _groundVitals =
            GetParent().GetNode<GroundVitalsIndicator>(
                "GroundVitals");
        _groundVitals.SetPlayer(_player);
        BindDiegeticLinks();

        ApplyVisualLanguage();
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged +=
                OnInterfaceScaleChanged;
            _uiAccessibility.VitalsScaleChanged +=
                OnVitalsScaleChanged;
        }
        GetViewport().SizeChanged +=
            OnViewportSizeChanged;
        Callable.From(ApplyResponsiveLayout)
            .CallDeferred();
        SetProcess(true);
    }

    public void AttachRunProgressHud(
        RunProgressHud progressHud)
    {
        _runProgress = progressHud;
        Callable.From(ApplyResponsiveLayout)
            .CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_runEnded)
        {
            return;
        }

        float deltaSeconds = (float)delta;
        _projectionTime += deltaSeconds;
        _healthReadoutPulse =
            Mathf.MoveToward(
                _healthReadoutPulse,
                0.0f,
                deltaSeconds * 3.8f);
        _nitroReadoutPulse =
            Mathf.MoveToward(
                _nitroReadoutPulse,
                0.0f,
                deltaSeconds * 5.2f);
        UpdateDiegeticProjection(
            deltaSeconds);
        UpdateVitalsReadout();
        UpdateContractPresentation(
            deltaSeconds);
        UpdateMomentumDock(
            deltaSeconds);
    }

    public void SetRunEnded(bool ended)
    {
        _runEnded = ended;
        bool showGameplay = !ended;
        _score.Visible = showGameplay;
        _health.Visible = showGameplay;
        _location.Visible = showGameplay;
        _momentum.Visible = showGameplay;
        if (IsInstanceValid(_runProgress))
        {
            _runProgress.Visible = showGameplay;
        }
        _pauseMenu?.SetGameplayVisible(
            showGameplay);
        _groundVitals.Visible = showGameplay;
        _statusLink.Visible = showGameplay;
        _contractLink.Hide();
        _scoreLink.Hide();
        _momentumLink.Hide();
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -=
                OnViewportSizeChanged;
        }
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged -=
                OnInterfaceScaleChanged;
            _uiAccessibility.VitalsScaleChanged -=
                OnVitalsScaleChanged;
        }
    }

    private void ApplyVisualLanguage()
    {
        _score.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _health.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _momentum.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _nitro.AddThemeStyleboxOverride(
            "panel",
            CreateStatusStyle(
                new Color(0.35f, 0.9f, 1.0f),
                false));
        _interface.GetNode<Control>(
                "HealthDisplay/Margin/Content/HealthRow")
            .Hide();
        BindVitalsReadout();
        _health.ZIndex = 6;
        _health.Show();
        _nitro.Hide();
        _location.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxFlat
            {
                BgColor =
                    new Color(
                        0.012f,
                        0.024f,
                        0.044f,
                        0.09f),
                BorderColor =
                    new Color(
                        0.42f,
                        0.8f,
                        1.0f,
                        0.24f),
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                ContentMarginLeft = 14.0f,
                ContentMarginRight = 14.0f
            });
        Label locationLabel =
            _interface.GetNode<Label>(
                "LocationBanner/Margin/Label");
        locationLabel
            .AddThemeColorOverride(
                "font_color",
                new Color(
                    0.94f,
                    0.98f,
                    0.99f));
        locationLabel.ClipText = true;
        _interface.GetNode<Label>(
                "ScoreLabel/Margin/Values/Score/Title")
            .AddThemeColorOverride(
                "font_color",
                new Color(
                    0.08f,
                    0.24f,
                    0.29f,
                    0.72f));
        _interface.GetNode<Label>(
                "ScoreLabel/Margin/Values/Score/Value")
            .AddThemeColorOverride(
                "font_color",
                new Color(
                    0.04f,
                    0.1f,
                    0.12f));

        _healthBar.CustomMinimumSize =
            new Vector2(170.0f, 6.0f);
        _nitroBar.CustomMinimumSize =
            new Vector2(170.0f, 5.0f);
        _interface.GetNode<Control>(
                "HealthDisplay/Margin/Content/HealthRow/Health")
            .CustomMinimumSize =
                new Vector2(84.0f, 0.0f);
        _interface.GetNode<Control>(
                "NitroDisplay/Margin/Content/State")
            .CustomMinimumSize =
                new Vector2(84.0f, 0.0f);
        _interface.GetNode<HBoxContainer>(
                "HealthDisplay/Margin/Content/HealthRow")
            .AddThemeConstantOverride(
                "separation",
                8);
        _interface.GetNode<Control>(
                "ScoreLabel/Margin/Values/Score")
            .CustomMinimumSize =
                new Vector2(65.0f, 0.0f);
        _interface.GetNode<Control>(
                "ScoreLabel/Margin/Values/Best")
            .Hide();
        _interface.GetNode<Control>(
                "ScoreLabel/Margin/Values/Divider")
            .Hide();
        _interface.GetNode<Control>(
                "MomentumDisplay/Margin/Content/Hint")
            .Hide();
        ApplyProgressStyle(_healthBar);
        ApplyProgressStyle(_nitroBar);

        SetMargins(
            _score.GetNode<MarginContainer>("Margin"),
            8, 5, 5, 5);
        SetMargins(
            _health.GetNode<MarginContainer>("Margin"),
            14, 5, 14, 5);
        SetMargins(
            _momentum.GetNode<MarginContainer>("Margin"),
            5, 5, 8, 5);
        SetMargins(
            _nitro.GetNode<MarginContainer>("Margin"),
            12, 3, 12, 3);
        SetMargins(
            _location.GetNode<MarginContainer>("Margin"),
            12, 4, 12, 4);

        SetFontSize(
            "ScoreLabel/Margin/Values/Score/Title",
            8);
        SetFontSize(
            "ScoreLabel/Margin/Values/Best/Title",
            8);
        SetFontSize(
            "ScoreLabel/Margin/Values/Score/Value",
            19);
        SetFontSize(
            "ScoreLabel/Margin/Values/Best/Value",
            19);
        SetFontSize(
            "HealthDisplay/Margin/Content/HealthRow/Health",
            11);
        SetRichTextFontSize(
            "HealthDisplay/Margin/Content/RecoveryReadout/ContractSlot/ContractFrame/Contract",
            8);
        SetFontSize(
            "MomentumDisplay/Margin/Content/State",
            10);
        SetFontSize(
            "MomentumDisplay/Margin/Content/Multiplier",
            24);
        SetFontSize(
            "MomentumDisplay/Margin/Content/Hint",
            8);
        SetFontSize(
            "NitroDisplay/Margin/Content/State",
            9);
        SetFontSize(
            "LocationBanner/Margin/Label",
            11);

    }

    private void BindVitalsReadout()
    {
        VBoxContainer content =
            _health.GetNode<VBoxContainer>(
                "Margin/Content");
        content.AddThemeConstantOverride(
            "separation",
            0);
        HBoxContainer readout =
            content.GetNode<HBoxContainer>(
                "RecoveryReadout");

        _healthChip =
            readout.GetNode<PanelContainer>(
                "HealthChip");
        BindVitalsChip(
            _healthChip,
            new Color(1.0f, 0.3f, 0.24f),
            out _healthTitle,
            out _healthPercent,
            out _healthChipStyle);

        _nitroChip =
            readout.GetNode<PanelContainer>(
                "NitroChip");
        BindVitalsChip(
            _nitroChip,
            new Color(0.2f, 0.68f, 1.0f),
            out _nitroTitle,
            out _nitroPercent,
            out _nitroChipStyle);

        _contractSlot =
            readout.GetNode<Control>(
                "ContractSlot");
        _contractFrame =
            _contractSlot.GetNode<PanelContainer>(
                "ContractFrame");
        _contractFrame.AddThemeStyleboxOverride(
            "panel",
            CreateContractReadoutStyle());
        _contractText =
            _contractFrame.GetNode<RichTextLabel>(
                "Contract");
    }

    private static void BindVitalsChip(
        PanelContainer chip,
        Color accent,
        out Label titleLabel,
        out Label valueLabel,
        out StyleBoxFlat style)
    {
        style = CreateVitalsChipStyle(accent);
        chip.AddThemeStyleboxOverride(
            "panel",
            style);

        HBoxContainer row =
            chip.GetNode<HBoxContainer>(
                "ChipRow");
        titleLabel =
            row.GetNode<Label>(
                "Title");
        valueLabel =
            row.GetNode<Label>(
                "Value");

        StyleReadoutLabel(
            titleLabel,
            new Color(
                accent.R,
                accent.G,
                accent.B,
                0.82f));
        titleLabel.AddThemeFontSizeOverride(
            "font_size",
            8);
        StyleReadoutLabel(
            valueLabel,
            accent);
        valueLabel.AddThemeFontSizeOverride(
            "font_size",
            16);
    }

    private static void StyleReadoutLabel(
        Label label,
        Color color)
    {
        label.AddThemeColorOverride(
            "font_color",
            color);
        label.AddThemeColorOverride(
            "font_shadow_color",
            new Color(0.0f, 0.0f, 0.0f, 0.92f));
        label.AddThemeConstantOverride(
            "shadow_offset_x", 1);
        label.AddThemeConstantOverride(
            "shadow_offset_y", 1);
    }

    private void UpdateVitalsReadout()
    {
        if (!IsInstanceValid(_player) ||
            !IsInstanceValid(_healthPercent) ||
            !IsInstanceValid(_nitroPercent))
        {
            return;
        }
        int healthValue =
            Mathf.RoundToInt(
                _player.HealthRatio * 100.0f);
        int nitroValue =
            Mathf.RoundToInt(
                _player.NitroRatio * 100.0f);
        if (_lastHealthReadout >= 0 &&
            healthValue < _lastHealthReadout)
        {
            _healthReadoutPulse = 1.0f;
        }
        if (_lastNitroReadout >= 0 &&
            Mathf.Abs(
                nitroValue -
                _lastNitroReadout) >= 2)
        {
            _nitroReadoutPulse =
                Mathf.Max(
                    _nitroReadoutPulse,
                    _player.IsNitroBurning
                        ? 0.72f
                        : 0.42f);
        }
        _lastHealthReadout = healthValue;
        _lastNitroReadout = nitroValue;
        _healthPercent.Text =
            $"{healthValue}%";
        _nitroPercent.Text =
            $"{nitroValue}%";
        _nitroTitle.Text =
            _player.IsNitroOverdrive
                ? "THRUST"
                : _player.IsRiftTeleportActive
                    ? "RIFT"
                : _player.IsGroundBurstActive
                    ? "BURST"
                    : "NITRO";
        if (_player.IsNitroBurning)
        {
            _nitroReadoutPulse =
                Mathf.Max(
                    _nitroReadoutPulse,
                    0.32f +
                        Mathf.Sin(
                            _projectionTime * 12.0f) *
                        0.08f);
        }
        _healthPercent.AddThemeColorOverride(
            "font_color",
            _groundVitals.DisplayHealthColor);
        _nitroPercent.AddThemeColorOverride(
            "font_color",
            _groundVitals.DisplayNitroColor);
        Color healthColor =
            _groundVitals.DisplayHealthColor;
        Color nitroColor =
            _groundVitals.DisplayNitroColor;
        _healthTitle.AddThemeColorOverride(
            "font_color",
            new Color(
                healthColor.R,
                healthColor.G,
                healthColor.B,
                0.78f));
        _nitroTitle.AddThemeColorOverride(
            "font_color",
            new Color(
                nitroColor.R,
                nitroColor.G,
                nitroColor.B,
                0.78f));

        float criticalPulse =
            _player.HealthRatio <= 0.3f
                ? 0.28f +
                    Mathf.Sin(
                        _projectionTime * 7.0f) *
                    0.12f
                : 0.0f;
        float healthPulse =
            Mathf.Clamp(
                Mathf.Max(
                    _healthReadoutPulse,
                    criticalPulse),
                0.0f,
                1.0f);
        float nitroPulse =
            Mathf.Clamp(
                _nitroReadoutPulse,
                0.0f,
                1.0f);
        ApplyChipPulse(
            _healthChip,
            _healthPercent,
            _healthChipStyle,
            healthColor,
            healthPulse);
        ApplyChipPulse(
            _nitroChip,
            _nitroPercent,
            _nitroChipStyle,
            nitroColor,
            nitroPulse);
    }

    private void UpdateContractPresentation(
        float deltaSeconds)
    {
        if (!IsInstanceValid(_contractSlot) ||
            !IsInstanceValid(_contractFrame) ||
            !IsInstanceValid(_player))
        {
            return;
        }

        float target =
            _player.RecoveryTarget > 0
                ? 1.0f
                : 0.0f;
        float response =
            1.0f -
            Mathf.Exp(
                -deltaSeconds *
                (target > _contractReveal
                    ? 6.2f
                    : 7.4f));
        _contractReveal =
            Mathf.Lerp(
                _contractReveal,
                target,
                response);
        if (Mathf.Abs(
                _contractReveal - target) <
            0.002f)
        {
            _contractReveal = target;
        }

        float eased =
            _contractReveal *
            _contractReveal *
            (3.0f -
                2.0f * _contractReveal);
        _contractSlot.CustomMinimumSize =
            new Vector2(
                _contractExpandedWidth * eased,
                _contractSlot.CustomMinimumSize.Y);
        float fade =
            Mathf.Clamp(
                (_contractReveal - 0.12f) /
                    0.58f,
                0.0f,
                1.0f);
        fade =
            fade * fade *
            (3.0f - 2.0f * fade);
        _contractFrame.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                fade);
        _contractFrame.PivotOffset =
            _contractFrame.Size * 0.5f;
        _contractFrame.Scale =
            new Vector2(
                1.0f,
                0.94f + fade * 0.06f);
        _healthChip.Rotation = 0.0f;
        _nitroChip.Rotation = 0.0f;
        _contractLink.Hide();
        if (target <= 0.0f &&
            _contractReveal <= 0.01f &&
            IsInstanceValid(_contractText))
        {
            _contractText.Text = "";
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsInstanceValid(_interface))
        {
            return;
        }

        Vector2 viewportSize =
            _interface.GetRect().Size;
        if (viewportSize.X <= 1.0f ||
            viewportSize.Y <= 1.0f)
        {
            viewportSize =
                GetViewport().GetVisibleRect().Size;
        }

        bool compact = viewportSize.X < 960.0f;
        bool touch =
            _uiAccessibility?.IsTouchLayout ??
            (OS.HasFeature("mobile") ||
                DisplayServer.IsTouchscreenAvailable());
        Vector4 safeInsets =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layoutScale =
            _uiAccessibility?.LayoutScale ??
            1.0f;
        float textScale =
            _uiAccessibility?.EffectiveTextScale ??
            1.0f;
        float leftInset =
            safeInsets.X +
            (compact ? 12.0f : 20.0f) *
                layoutScale;
        float rightInset =
            safeInsets.Z +
            (compact ? 12.0f : 20.0f) *
                layoutScale;
        float topInset =
            safeInsets.Y +
            (compact ? 10.0f : 14.0f) *
                layoutScale;
        float scoreWidth =
            (touch
                ? 166.0f
                : compact ? 118.0f : 132.0f) *
            layoutScale;
        float scoreHeight =
            (touch
                ? 68.0f
                : compact ? 48.0f : 54.0f) *
            layoutScale;
        float momentumWidth =
            (touch
                ? 108.0f
                : compact ? 104.0f : 116.0f) *
            layoutScale;
        float momentumHeight =
            (touch
                ? 68.0f
                : compact ? 48.0f : 54.0f) *
            layoutScale;
        _contractExpandedWidth =
            (touch
                ? 190.0f
                : compact ? 140.0f : 155.0f) *
            layoutScale;

        ApplyScaledMetrics(
            touch,
            layoutScale,
            textScale);

        float safeWidth =
            viewportSize.X -
            safeInsets.X -
            safeInsets.Z;
        float safeCenterX =
            safeInsets.X +
            safeWidth * 0.5f;
        PlaceTopLeft(
            _score,
            leftInset,
            topInset,
            scoreWidth,
            scoreHeight);
        float dockGap =
            -5.0f * layoutScale;
        PlaceTopLeft(
            _momentum,
            leftInset +
                scoreWidth +
                dockGap,
            topInset,
            momentumWidth,
            momentumHeight);
        _momentumShownPosition =
            _momentum.Position;
        _momentumHiddenPosition =
            new Vector2(
                leftInset +
                    scoreWidth * 0.62f,
                _momentum.Position.Y);
        _score.PivotOffset =
            new Vector2(
                scoreWidth * 0.5f,
                scoreHeight * 0.5f);
        _momentum.PivotOffset =
            new Vector2(
                momentumWidth * 0.5f,
                momentumHeight * 0.5f);
        _score.Rotation = 0.0f;
        _momentum.Rotation = 0.0f;

        float statusWidth =
            Mathf.Min(
                (touch
                    ? 600.0f
                    : compact ? 430.0f : 480.0f) *
                    layoutScale,
                safeWidth *
                    (touch
                        ? 0.92f
                        : compact ? 0.82f : 0.62f));
        float healthHeight =
            (touch
                ? 62.0f
                : compact ? 40.0f : 42.0f) *
            layoutScale;
        float nitroHeight =
            (touch ? 100.0f : 82.0f) *
            layoutScale;
        float statusBottomInset =
            safeInsets.W +
            (touch
                ? 20.0f
                : 18.0f) *
            layoutScale;
        _statusWidth = statusWidth;
        _healthHeight = healthHeight;
        _nitroHeight = nitroHeight;
        _statusBottomLimit =
            statusBottomInset;
        PlaceBottomCenter(
            _health,
            safeCenterX,
            statusBottomInset,
            statusWidth,
            healthHeight);
        _groundVitals.Configure(
            _uiAccessibility?.VitalsScale ??
                1.15f);

        float locationTop =
            topInset +
            (IsInstanceValid(_runProgress)
                ? _runProgress.ReservedHeight +
                    3.0f * layoutScale
                : 2.0f * layoutScale);
        PlaceTopCenter(
            _location,
            safeCenterX,
            locationTop,
            Mathf.Min(
                (touch
                    ? 315.0f
                    : compact ? 305.0f : 330.0f) *
                    layoutScale,
                safeWidth -
                    24.0f * layoutScale),
            (touch ? 30.0f : 24.0f) *
                layoutScale);
        _locationRestPosition =
            _location.Position;

        UpdateDiegeticProjection();
    }

    private void BindDiegeticLinks()
    {
        _statusLink =
            _interface.GetNode<Line2D>(
                "VitalProjectionLink");
        _contractLink =
            _interface.GetNode<Line2D>(
                "RecoveryProjectionLink");
        _scoreLink =
            _interface.GetNode<Line2D>(
                "ScoreProjectionLink");
        _momentumLink =
            _interface.GetNode<Line2D>(
                "MomentumProjectionLink");
    }

    private void UpdateMomentumDock(
        float deltaSeconds)
    {
        if (!IsInstanceValid(_momentum) ||
            !IsInstanceValid(_player))
        {
            return;
        }

        bool active =
            _player.CurrentMomentumTier !=
                Player.MomentumTier.Low ||
            _player.RewardMultiplier > 1;
        bool fading =
            !active &&
            _momentumReveal > 0.02f;
        _momentumReveal =
            Mathf.MoveToward(
                _momentumReveal,
                active ? 1.0f : 0.0f,
                deltaSeconds *
                    (active ? 4.8f : 6.4f));
        float eased =
            1.0f -
            Mathf.Pow(
                1.0f - _momentumReveal,
                3.0f);
        _momentum.Position =
            _momentumHiddenPosition.Lerp(
                _momentumShownPosition,
                eased);
        Color modulate =
            _momentum.Modulate;
        modulate.A =
            eased;
        _momentum.Modulate =
            modulate;
        _momentum.Visible =
            _momentumReveal > 0.01f &&
            !_runEnded;
        if (IsInstanceValid(
                _momentumMultiplier))
        {
            if (fading)
            {
                float pulse =
                    0.78f +
                    Mathf.Sin(
                        _projectionTime *
                        12.0f) *
                    0.22f;
                _momentumMultiplier.Modulate =
                    new Color(
                        1.0f,
                        0.12f,
                        0.1f,
                        pulse);
                _momentumMultiplier.PivotOffset =
                    _momentumMultiplier.Size *
                    0.5f;
                _momentumMultiplier.Scale =
                    Vector2.One *
                    (1.0f +
                        (1.0f - pulse) *
                        0.08f);
            }
            else
            {
                _momentumMultiplier.Scale =
                    Vector2.One;
            }
        }
    }

    private void UpdateDiegeticProjection(
        float delta = 1.0f / 60.0f)
    {
        if (!IsInstanceValid(_interface) ||
            !IsInstanceValid(_player) ||
            _statusWidth <= 1.0f)
        {
            return;
        }

        _camera =
            GetViewport().GetCamera3D();
        if (!IsInstanceValid(_camera) ||
            _camera.IsPositionBehind(
                _player.GlobalPosition))
        {
            return;
        }

        Vector2 viewportSize =
            _interface.GetRect().Size;
        if (viewportSize.X <= 1.0f ||
            viewportSize.Y <= 1.0f)
        {
            viewportSize =
                GetViewport().GetVisibleRect().Size;
        }
        Vector4 safe =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layout =
            _uiAccessibility?.LayoutScale ??
            1.0f;
        Vector2 playerPoint =
            _camera.UnprojectPosition(
                _player.GlobalPosition +
                    Vector3.Up * 0.52f);
        if (!_vitalsProjectionInitialized)
        {
            _lastPlayerProjection =
                playerPoint;
            _lastCameraPosition =
                _camera.GlobalPosition;
            _vitalsProjectionInitialized = true;
        }
        float safeDelta =
            Mathf.Max(0.001f, delta);
        float screenMotion =
            (playerPoint -
                _lastPlayerProjection).Length() /
            safeDelta;
        float cameraMotion =
            (_camera.GlobalPosition -
                _lastCameraPosition).Length() /
            safeDelta;
        _groundVitals.SetMotionDynamics(
            screenMotion / 720.0f,
            Mathf.Abs(_player.Velocity.Y) / 18.0f,
            cameraMotion / 22.0f);
        ProceduralWorld world =
            GetTree().GetFirstNodeInGroup(
                "procedural_world")
                as ProceduralWorld;
        if (IsInstanceValid(world))
        {
            _groundVitals.SetBackdropColor(
                world.CurrentSurfaceColor);
        }
        _lastPlayerProjection =
            playerPoint;
        _lastCameraPosition =
            _camera.GlobalPosition;
        _statusLink.Hide();

        float contractCenterX =
            _health.Position.X +
            _health.Size.X * 0.5f;
        float contractBottomY =
            _health.Position.Y +
            _health.Size.Y;
        _contractLink.Points =
            new[]
            {
                new Vector2(
                    contractCenterX -
                        18.0f * layout,
                    contractBottomY),
                new Vector2(
                    contractCenterX +
                        18.0f * layout,
                    contractBottomY),
                new Vector2(
                    contractCenterX +
                        24.0f * layout,
                    contractBottomY +
                        5.0f * layout)
            };

        Vector2 scoreEdge = new(
            _score.Position.X,
            _score.Position.Y +
                _score.Size.Y * 0.55f);
        Vector2 momentumEdge = new(
            _momentum.Position.X +
                _momentum.Size.X,
            _momentum.Position.Y +
                _momentum.Size.Y * 0.55f);
        Vector2 leftWorldProjection =
            new(
                safe.X +
                    3.0f * layout,
                scoreEdge.Y +
                    13.0f * layout);
        Vector2 rightWorldProjection =
            new(
                viewportSize.X -
                    safe.Z -
                    3.0f * layout,
                momentumEdge.Y +
                    13.0f * layout);
        _scoreLink.Points =
            CreateElbowLink(
                scoreEdge,
                leftWorldProjection);
        _momentumLink.Points =
            CreateElbowLink(
                momentumEdge,
                rightWorldProjection);
        float pulse =
            0.8f +
            Mathf.Sin(
                _projectionTime * 2.2f) *
            0.12f;
        _statusLink.Modulate =
            new Color(1.0f, 1.0f, 1.0f, pulse);
    }

    private static Vector2[] CreateElbowLink(
        Vector2 panelPoint,
        Vector2 worldPoint)
    {
        float elbowX = Mathf.Lerp(
            panelPoint.X,
            worldPoint.X,
            0.62f);
        return new[]
        {
            panelPoint,
            new Vector2(
                elbowX,
                panelPoint.Y),
            new Vector2(
                elbowX,
                worldPoint.Y),
            worldPoint
        };
    }

    private void ApplyScaledMetrics(
        bool touch,
        float layoutScale,
        float textScale)
    {
        float mobileTextBoost =
            touch ? 1.12f : 1.0f;
        SetScaledFont(
            "ScoreLabel/Margin/Values/Score/Title",
            touch ? 11 : 9,
            textScale * mobileTextBoost);
        SetScaledFont(
            "ScoreLabel/Margin/Values/Best/Title",
            touch ? 14 : 8,
            textScale * mobileTextBoost);
        SetScaledFont(
            "ScoreLabel/Margin/Values/Score/Value",
            touch ? 34 : 26,
            textScale);
        SetScaledFont(
            "ScoreLabel/Margin/Values/Best/Value",
            touch ? 30 : 19,
            textScale);
        SetScaledFont(
            "HealthDisplay/Margin/Content/HealthRow/Health",
            touch ? 20 : 11,
            textScale);
        SetScaledRichTextFont(
            "HealthDisplay/Margin/Content/RecoveryReadout/ContractSlot/ContractFrame/Contract",
            touch ? 13 : 8,
            textScale);
        _healthTitle.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 13.0f : 8.0f) *
                textScale));
        _nitroTitle.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 13.0f : 8.0f) *
                textScale));
        _healthPercent.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 22.0f : 16.0f) *
                textScale));
        _nitroPercent.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 22.0f : 16.0f) *
                textScale));
        _healthChip.CustomMinimumSize =
            new Vector2(
                (touch ? 150.0f : 106.0f) *
                    layoutScale,
                (touch ? 44.0f : 30.0f) *
                    layoutScale);
        _nitroChip.CustomMinimumSize =
            _healthChip.CustomMinimumSize;
        _contractSlot.CustomMinimumSize =
            new Vector2(
                _contractExpandedWidth *
                    _contractReveal,
                (touch ? 44.0f : 30.0f) *
                    layoutScale);
        SetScaledFont(
            "MomentumDisplay/Margin/Content/State",
            touch ? 13 : 9,
            textScale);
        SetScaledFont(
            "MomentumDisplay/Margin/Content/Multiplier",
            touch ? 27 : 20,
            textScale);
        SetScaledFont(
            "MomentumDisplay/Margin/Content/Hint",
            touch ? 16 : 8,
            textScale);
        SetScaledFont(
            "NitroDisplay/Margin/Content/State",
            touch ? 18 : 9,
            textScale);
        SetScaledFont(
            "LocationBanner/Margin/Label",
            touch ? 14 : 10,
            textScale);
        _healthBar.CustomMinimumSize =
            new Vector2(
                170.0f * layoutScale,
                (touch ? 14.0f : 6.0f) *
                    layoutScale);
        _nitroBar.CustomMinimumSize =
            new Vector2(
                170.0f * layoutScale,
                (touch ? 14.0f : 5.0f) *
                    layoutScale);
    }

    private void OnInterfaceScaleChanged(
        float uiScale,
        float textScale)
    {
        Callable.From(ApplyResponsiveLayout)
            .CallDeferred();
    }

    private void OnVitalsScaleChanged(
        float vitalsScale)
    {
        Callable.From(
            ApplyResponsiveLayout)
            .CallDeferred();
    }

    private void OnViewportSizeChanged()
    {
        Callable.From(ApplyResponsiveLayout)
            .CallDeferred();
    }

    private void SetFontSize(
        NodePath path,
        int size)
    {
        _interface.GetNode<Control>(path)
            .AddThemeFontSizeOverride(
                "font_size",
                size);
    }

    private void SetScaledFont(
        NodePath path,
        int baseSize,
        float scale)
    {
        SetFontSize(
            path,
            Mathf.Max(
                8,
                Mathf.RoundToInt(
                    baseSize * scale)));
    }

    private void SetRichTextFontSize(
        NodePath path,
        int size)
    {
        RichTextLabel label =
            _interface.GetNode<RichTextLabel>(
                path);
        label.AddThemeFontSizeOverride(
            "normal_font_size",
            size);
        label.AddThemeFontSizeOverride(
            "bold_font_size",
            size);
        label.AddThemeFontSizeOverride(
            "italics_font_size",
            size);
        label.AddThemeFontSizeOverride(
            "bold_italics_font_size",
            size);
        label.AddThemeFontSizeOverride(
            "mono_font_size",
            size);
    }

    private void SetScaledRichTextFont(
        NodePath path,
        int baseSize,
        float scale)
    {
        SetRichTextFontSize(
            path,
            Mathf.Max(
                8,
                Mathf.RoundToInt(
                    baseSize * scale)));
    }

    private static void ApplyProgressStyle(
        ProgressBar bar)
    {
        bar.AddThemeStyleboxOverride(
            "background",
            new StyleBoxFlat
            {
                BgColor =
                    new Color(
                        0.0f,
                        0.0f,
                        0.0f,
                0.34f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2
            });
        bar.AddThemeStyleboxOverride(
            "fill",
            new StyleBoxFlat
            {
                BgColor = Colors.White,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2
            });
    }

    private static void ApplyChipPulse(
        PanelContainer chip,
        Label value,
        StyleBoxFlat style,
        Color accent,
        float pulse)
    {
        if (!IsInstanceValid(chip) ||
            !IsInstanceValid(value) ||
            style == null)
        {
            return;
        }

        float glow =
            Mathf.Clamp(pulse, 0.0f, 1.0f);
        style.BorderColor =
            new Color(
                accent.R,
                accent.G,
                accent.B,
                0.42f + glow * 0.4f);
        style.ShadowColor =
            new Color(
                accent.R,
                accent.G,
                accent.B,
                glow * 0.16f);
        style.ShadowSize =
            Mathf.RoundToInt(glow * 2.0f);
        style.BgColor =
            new Color(
                accent.R * 0.025f,
                accent.G * 0.025f,
                accent.B * 0.025f,
                0.05f + glow * 0.035f);

        value.PivotOffset =
            value.Size * 0.5f;
        value.Scale =
            Vector2.One *
            (1.0f + glow * 0.025f);
        value.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.94f + glow * 0.06f);
    }

    private static StyleBoxFlat CreateVitalsChipStyle(
        Color accent)
    {
        return new StyleBoxFlat
        {
            BgColor =
                new Color(
                    accent.R * 0.025f,
                    accent.G * 0.025f,
                    accent.B * 0.025f,
                    0.05f),
            BorderColor =
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.42f),
            BorderWidthBottom = 2,
            ContentMarginLeft = 8.0f,
            ContentMarginRight = 8.0f,
            ContentMarginTop = 3.0f,
            ContentMarginBottom = 3.0f,
            ShadowColor =
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.0f),
            ShadowSize = 0
        };
    }

    private static StyleBoxFlat CreateContractReadoutStyle()
    {
        return new StyleBoxFlat
        {
            BgColor =
                new Color(
                    0.008f,
                    0.018f,
                    0.034f,
                    0.045f),
            BorderColor =
                new Color(
                    0.48f,
                    0.72f,
                    0.9f,
                    0.34f),
            BorderWidthBottom = 1,
            ContentMarginLeft = 8.0f,
            ContentMarginRight = 8.0f,
            ContentMarginTop = 2.0f,
            ContentMarginBottom = 2.0f,
            ShadowColor =
                new Color(
                    0.16f,
                    0.7f,
                    1.0f,
                    0.0f),
            ShadowSize = 0
        };
    }

    private static StyleBoxFlat CreateStatusStyle(
        Color accent,
        bool primary)
    {
        StyleBoxFlat style = new()
        {
            BgColor =
                new Color(
                    0.006f,
                    0.014f,
                    0.027f,
                    primary ? 0.2f : 0.13f),
            BorderColor =
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    primary ? 0.68f : 0.5f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft =
                primary ? 2 : 1,
            CornerRadiusTopRight =
                primary ? 9 : 5,
            CornerRadiusBottomLeft =
                primary ? 2 : 1,
            CornerRadiusBottomRight =
                primary ? 5 : 9,
            ShadowColor =
                new Color(
                    0.0f,
                    0.0f,
                    0.0f,
                    primary ? 0.24f : 0.12f),
            ShadowSize = primary ? 4 : 2
        };
        if (primary)
        {
            style.BorderWidthTop = 1;
        }
        else
        {
            style.BorderWidthBottom = 1;
        }

        return style;
    }

    private static void SetMargins(
        MarginContainer margin,
        int left,
        int top,
        int right,
        int bottom)
    {
        margin.AddThemeConstantOverride(
            "margin_left",
            left);
        margin.AddThemeConstantOverride(
            "margin_top",
            top);
        margin.AddThemeConstantOverride(
            "margin_right",
            right);
        margin.AddThemeConstantOverride(
            "margin_bottom",
            bottom);
    }

    private static void PlaceTopLeft(
        Control control,
        float x,
        float y,
        float width,
        float height)
    {
        SetAnchors(control, 0.0f, 0.0f);
        SetOffsets(
            control,
            x,
            y,
            x + width,
            y + height);
    }

    private static void PlaceTopRight(
        Control control,
        float inset,
        float y,
        float width,
        float height)
    {
        SetAnchors(control, 1.0f, 0.0f);
        SetOffsets(
            control,
            -inset - width,
            y,
            -inset,
            y + height);
    }

    private static void PlaceTopCenter(
        Control control,
        float centerX,
        float y,
        float width,
        float height)
    {
        SetAnchors(control, 0.0f, 0.0f);
        SetOffsets(
            control,
            centerX - width * 0.5f,
            y,
            centerX + width * 0.5f,
            y + height);
    }

    private static void PlaceBottomCenter(
        Control control,
        float centerX,
        float inset,
        float width,
        float height)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorTop = 1.0f;
        control.AnchorBottom = 1.0f;
        SetOffsets(
            control,
            centerX - width * 0.5f,
            -inset - height,
            centerX + width * 0.5f,
            -inset);
    }

    private static void PlaceBottomRight(
        Control control,
        float rightInset,
        float bottomInset,
        float width,
        float height)
    {
        SetAnchors(control, 1.0f, 1.0f);
        SetOffsets(
            control,
            -rightInset - width,
            -bottomInset - height,
            -rightInset,
            -bottomInset);
    }

    private static void SetAnchors(
        Control control,
        float horizontal,
        float vertical)
    {
        control.AnchorLeft = horizontal;
        control.AnchorRight = horizontal;
        control.AnchorTop = vertical;
        control.AnchorBottom = vertical;
    }

    private static void SetOffsets(
        Control control,
        float left,
        float top,
        float right,
        float bottom)
    {
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }
}
