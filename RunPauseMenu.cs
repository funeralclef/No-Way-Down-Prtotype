using Godot;

public partial class RunPauseMenu : Control
{
    private DisplaySettings _settings;
    private MobileControls _mobileControls;
    private UiAccessibility _uiAccessibility;
    private HomeMenuButton _menuButton;
    private Button _resumeButton;
    private Button _confirmExitButton;
    private Control _overlay;
    private ColorRect _focusVeil;
    private PanelContainer _menuCard;
    private PanelContainer _confirmCard;
    private bool _gameplayVisible = true;
    private bool _pauseOwned;
    private bool _waitingForSettings;
    private Tween _cardTween;
    private GameInfoPanel _info;
    private Button _buildButton;

    public void Initialize(
        DisplaySettings settings)
    {
        _settings = settings;
        _mobileControls =
            GetParent()
                .GetNodeOrNull<MobileControls>(
                    "MobileControls");
        _uiAccessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _settings.UseExternalLauncher(true);
        _settings.MenuClosed +=
            OnSettingsClosed;
        BuildInterface();
        GetViewport().SizeChanged +=
            UpdateResponsiveLayout;
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged +=
                OnInterfaceScaleChanged;
        }
        UpdateResponsiveLayout();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_settings))
        {
            _settings.MenuClosed -=
                OnSettingsClosed;
        }
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -=
                UpdateResponsiveLayout;
        }
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged -=
                OnInterfaceScaleChanged;
        }
    }

    public override void _UnhandledInput(
        InputEvent inputEvent)
    {
        if (!_gameplayVisible ||
            _waitingForSettings)
        {
            return;
        }

        if (inputEvent.IsActionPressed(
                "pause"))
        {
            if (_overlay.Visible)
            {
                ResumeRun();
            }
            else
            {
                OpenPauseMenu();
            }
            GetViewport()
                .SetInputAsHandled();
            return;
        }

        if (!_overlay.Visible ||
            !inputEvent.IsActionPressed(
                "ui_cancel"))
        {
            return;
        }

        if (_confirmCard.Visible)
        {
            ShowPauseActions();
        }
        else
        {
            ResumeRun();
        }
        GetViewport().SetInputAsHandled();
    }

    public void SetGameplayVisible(
        bool visible)
    {
        _gameplayVisible = visible;
        _menuButton.Visible =
            visible &&
            !_overlay.Visible &&
            !_waitingForSettings;
        if (!visible)
        {
            _overlay.Hide();
            _waitingForSettings = false;
            _pauseOwned = false;
        }
    }

    public void BindBuildMenu(System.Action openBuild)
    {
        _buildButton = new Button { Name = "Build", Text = "BUILD / MERGE", CustomMinimumSize = _resumeButton.CustomMinimumSize };
        MenuVisualStyle.Action(_buildButton);
        _resumeButton.GetParent().AddChild(_buildButton);
        _resumeButton.GetParent().MoveChild(_buildButton, _resumeButton.GetIndex() + 1);
        _buildButton.Pressed += () => openBuild();
        var margin = _menuCard.GetNode<MarginContainer>("Margin");
        _menuCard.RemoveChild(margin);
        var scroll = new ScrollContainer { FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _menuCard.AddChild(scroll);
        scroll.AddChild(margin);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void BuildInterface()
    {
        _menuButton =
            GetNode<HomeMenuButton>(
                "MenuButton");
        StyleButton(_menuButton, 24, true);
        _menuButton.Pressed +=
            OpenPauseMenu;

        _overlay =
            GetNode<Control>(
                "Overlay");
        _focusVeil =
            _overlay.GetNode<ColorRect>(
                "FocusVeil");
        ConfigureFocusVeil(
            _focusVeil);

        _menuCard =
            _overlay.GetNode<PanelContainer>(
                "PauseCard");
        _menuCard.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _resumeButton =
            _menuCard.GetNode<Button>(
                "Margin/Content/Resume");
        Button settings =
            _menuCard.GetNode<Button>(
                "Margin/Content/Settings");
        Button refuge =
            _menuCard.GetNode<Button>(
                "Margin/Content/Refuge");
        StyleButton(
            _resumeButton,
            15,
            false);
        StyleButton(
            settings,
            15,
            false);
        StyleButton(
            refuge,
            15,
            false);
        _info = new GameInfoPanel { Name = "Info" };
        AddChild(_info);
        Button info = new() { Name = "InfoAction", Text = "INFO / CONTROLS", CustomMinimumSize = _resumeButton.CustomMinimumSize };
        MenuVisualStyle.Action(info);
        _resumeButton.GetParent().AddChild(info);
        _resumeButton.GetParent().MoveChild(info, _resumeButton.GetIndex() + 1);
        info.Pressed += _info.Open;
        _resumeButton.Pressed += ResumeRun;
        settings.Pressed += OpenSettings;
        refuge.Pressed +=
            ShowExitConfirmation;

        _confirmCard =
            _overlay.GetNode<PanelContainer>(
                "ConfirmCard");
        _confirmCard.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _confirmExitButton =
            _confirmCard.GetNode<Button>(
                "Margin/Content/ConfirmExit");
        Button cancel =
            _confirmCard.GetNode<Button>(
                "Margin/Content/Cancel");
        StyleButton(
            _confirmExitButton,
            15,
            false);
        StyleButton(
            cancel,
            15,
            false);
        _confirmExitButton.Pressed +=
            ReturnToRefuge;
        cancel.Pressed +=
            ShowPauseActions;
    }

    private void OpenPauseMenu()
    {
        if (!_gameplayVisible ||
            _waitingForSettings)
        {
            return;
        }

        _confirmCard.Hide();
        _menuCard.Show();
        _overlay.Show();
        _menuButton.Hide();
        _mobileControls
            ?.SetSettingsMenuOpen(true);
        if (!GetTree().Paused)
        {
            GetTree().Paused = true;
            _pauseOwned = true;
        }
        _menuCard.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        _menuCard.Position +=
            new Vector2(0.0f, 12.0f);
        Tween reveal =
            CreateTween()
                .SetParallel(true);
        reveal.SetTrans(
            Tween.TransitionType.Cubic);
        reveal.SetEase(
            Tween.EaseType.Out);
        reveal.TweenProperty(
            _menuCard,
            "modulate",
            Colors.White,
            0.2f);
        reveal.TweenProperty(
            _menuCard,
            "position:y",
            _menuCard.Position.Y -
                12.0f,
            0.24f);
        _focusVeil.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        reveal.TweenProperty(
            _focusVeil,
            "modulate",
            Colors.White,
            0.18f);
        Callable.From(
                _resumeButton.GrabFocus)
            .CallDeferred();
    }

    private void ResumeRun()
    {
        _overlay.Hide();
        _menuButton.Visible =
            _gameplayVisible;
        _mobileControls
            ?.SetSettingsMenuOpen(false);
        if (_pauseOwned)
        {
            GetTree().Paused = false;
            _pauseOwned = false;
        }
    }

    private void OpenSettings()
    {
        _overlay.Hide();
        _waitingForSettings = true;
        _settings.OpenFromExternal();
    }

    private void OnSettingsClosed()
    {
        if (!_waitingForSettings)
        {
            return;
        }

        _waitingForSettings = false;
        _mobileControls?.SetSettingsMenuOpen(true);
        _overlay.Show();
        ShowPauseActions();
    }

    private void ShowExitConfirmation()
    {
        _menuCard.Hide();
        RevealCard(
            _confirmCard,
            _confirmExitButton);
    }

    private void ShowPauseActions()
    {
        _confirmCard.Hide();
        RevealCard(
            _menuCard,
            _resumeButton);
    }

    private void RevealCard(
        Control card,
        Control focusTarget)
    {
        _cardTween?.Kill();
        card.Show();
        card.PivotOffset =
            card.Size * 0.5f;
        card.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        card.Scale =
            new Vector2(0.975f, 0.96f);
        _cardTween =
            CreateTween()
                .SetParallel(true);
        _cardTween.SetTrans(
            Tween.TransitionType.Cubic);
        _cardTween.SetEase(
            Tween.EaseType.Out);
        _cardTween.TweenProperty(
            card,
            "modulate",
            Colors.White,
            0.18f);
        _cardTween.TweenProperty(
            card,
            "scale",
            Vector2.One,
            0.24f);
        Callable.From(
                focusTarget.GrabFocus)
            .CallDeferred();
    }

    private void ReturnToRefuge()
    {
        _pauseOwned = false;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(
            "res://refuge.tscn");
    }

    private void UpdateResponsiveLayout()
    {
        if (!IsInstanceValid(_menuButton))
        {
            return;
        }

        Vector2 viewport =
            GetViewport()
                .GetVisibleRect()
                .Size;
        Vector4 safe =
            _uiAccessibility
                ?.GetSafeInsets(viewport) ??
            Vector4.Zero;
        float scale =
            _uiAccessibility?.LayoutScale ??
            1.0f;
        float buttonWidth =
            (_uiAccessibility?.IsTouchLayout ==
                true
                    ? 56.0f
                    : 46.0f) *
            scale;
        float buttonHeight =
            (_uiAccessibility?.IsTouchLayout ==
                true
                    ? 52.0f
                    : 42.0f) *
            scale;
        Place(
            _menuButton,
            viewport.X -
                safe.Z -
                buttonWidth -
                14.0f * scale,
            safe.Y +
                12.0f * scale,
            buttonWidth,
            buttonHeight);

        float safeWidth =
            viewport.X -
            safe.X -
            safe.Z;
        float safeHeight =
            viewport.Y -
            safe.Y -
            safe.W;
        float cardWidth =
            Mathf.Min(
                430.0f * scale,
                safeWidth *
                    (viewport.Y > viewport.X
                        ? 0.9f
                        : 0.64f));
        float cardHeight =
            Mathf.Min(
                470.0f * scale,
                safeHeight -
                    28.0f * scale);
        float cardX =
            safe.X +
            (safeWidth - cardWidth) *
                0.5f;
        float cardY =
            safe.Y +
            (safeHeight - cardHeight) *
                0.5f;
        Place(
            _menuCard,
            cardX,
            cardY,
            cardWidth,
            cardHeight);
        Place(
            _confirmCard,
            cardX,
            cardY,
            cardWidth,
            cardHeight);
    }

    private void OnInterfaceScaleChanged(
        float uiScale,
        float textScale)
    {
        UpdateResponsiveLayout();
    }

    private static void StyleButton(
        Button button,
        int fontSize,
        bool minimal)
    {
        MenuVisualStyle.Action(button, fontSize, minimal);
    }

    private static void ConfigureFocusVeil(
        ColorRect veil)
    {
        veil.Color = Colors.White;
        veil.Material = MenuVisualStyle.FocusMaterial();
    }

    private static void Place(
        Control control,
        float x,
        float y,
        float width,
        float height)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = x;
        control.OffsetTop = y;
        control.OffsetRight = x + width;
        control.OffsetBottom = y + height;
    }
}
