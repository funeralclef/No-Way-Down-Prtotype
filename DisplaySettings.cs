using Godot;
using System.Collections.Generic;

public partial class DisplaySettings : Control
{
    public event System.Action MenuClosed;

    [ExportGroup("Platform preview")]
    [Export]
    public bool PreviewMobileLayout { get; set; }

    private const string SettingsPath = "user://display.cfg";
    private const string FullPanelScenePath =
        "res://ui/full_settings.tscn";
    private const string SettingsSection = "display";
    private const string AudioSection = "audio";
    private const string ControlsSection = "controls";
    private const string GameplaySection = "gameplay";
    private const string GraphicsSection = "graphics";
    private const string AccessibilitySection =
        "accessibility";
    private const int AssistPresetVersion = 2;
    private const int LegacyStrongAssistIndex = 3;
    private const int CustomGraphicsPreset = 4;

    private static readonly Vector2I[] CommonWindowSizes =
    {
        new(3840, 2160),
        new(3440, 1440),
        new(2560, 1440),
        new(2560, 1080),
        new(1920, 1200),
        new(1920, 1080),
        new(1600, 900),
        new(1440, 900),
        new(1366, 768),
        new(1280, 720),
        new(1024, 768),
        new(960, 540),
        new(720, 540)
    };

    private static readonly int[] FpsLimits = { 60, 120, 144, 165, 240, 0 };
    private static readonly string[] RemappableActions =
    {
        "move_forward",
        "move_back",
        "move_left",
        "move_right",
        "jump",
        "dash",
        "module_skill"
    };
    private static readonly string[] ControlButtonNames =
    {
        "Forward",
        "Back",
        "Left",
        "Right",
        "Jump",
        "Dash",
        "ModuleSkill"
    };
    private static readonly Key[] DefaultKeys =
    {
        Key.W,
        Key.S,
        Key.A,
        Key.D,
        Key.Space,
        Key.Shift,
        Key.E
    };
    private static readonly string[] AspectNames =
    {
        "AUTO / MONITOR",
        "16:9",
        "16:10",
        "21:9",
        "32:9",
        "4:3"
    };
    private static readonly float[] AspectRatios =
    {
        0.0f,
        16.0f / 9.0f,
        16.0f / 10.0f,
        21.0f / 9.0f,
        32.0f / 9.0f,
        4.0f / 3.0f
    };
    private static readonly float[] GraphicsRenderScales =
    {
        0.55f,
        0.68f,
        0.82f,
        1.0f
    };
    private static readonly string[] GraphicsPresetNames =
    {
        "PERFORMANCE",
        "LOW",
        "BALANCED",
        "HIGH",
        "CUSTOM"
    };
    private static readonly string[] GraphicsRenderScaleNames =
    {
        "55%  -  FASTEST",
        "68%  -  LOW",
        "82%  -  BALANCED",
        "100%  -  NATIVE"
    };
    private static readonly string[] GraphicsShadowNames =
    {
        "OFF",
        "LOW",
        "MEDIUM",
        "HIGH"
    };
    private static readonly string[] GraphicsAntiAliasingNames =
    {
        "OFF",
        "FXAA  -  FAST",
        "MSAA 2X",
        "MSAA 4X"
    };
    private static readonly string[] GraphicsEffectsNames =
    {
        "MINIMAL",
        "LOW",
        "MEDIUM",
        "HIGH"
    };
    private static readonly string[] AssistLevelNames =
    {
        "OFF",
        "BALANCED",
        "STRONG"
    };
    private static readonly string[] EnemyTrajectoryPreviewNames =
    {
        "OFF",
        "CLEAR"
    };

    private readonly List<Vector2I> _availableWindowSizes = new();
    private readonly Dictionary<string, Button> _bindingButtons = new();
    private readonly Dictionary<string, Key> _pendingBindings = new();

    private Button _openButton;
    private Button _quitButton;
    private Button _defaultsButton;
    private Button _closeButton;
    private Button _applyButton;
    private Control _overlay;
    private ColorRect _settingsShade;
    private PanelContainer _settingsCard;
    private TabContainer _tabs;
    private Label _settingsSubtitle;
    private Control _displayTab;
    private Control _graphicsTab;
    private Control _controlsTab;
    private OptionButton _modeOption;
    private OptionButton _aspectOption;
    private OptionButton _resolutionOption;
    private CheckButton _vsyncCheck;
    private OptionButton _fpsOption;
    private Label _monitorLabel;
    private Label _resolutionLabel;
    private Label _appliedLabel;
    private OptionButton _graphicsPresetOption;
    private OptionButton _graphicsRenderScaleOption;
    private OptionButton _graphicsShadowsOption;
    private OptionButton _graphicsAntiAliasingOption;
    private OptionButton _graphicsEffectsOption;
    private Label _graphicsAppliedLabel;
    private HSlider _masterVolumeSlider;
    private HSlider _musicVolumeSlider;
    private Label _masterVolumeValue;
    private Label _musicVolumeValue;
    private OptionButton _enemyTrajectoryOption;
    private OptionButton _survivalAssistOption;
    private OptionButton _stompAssistOption;
    private HSlider _uiScaleSlider;
    private HSlider _textScaleSlider;
    private HSlider _vitalsScaleSlider;
    private Label _uiScaleValue;
    private Label _textScaleValue;
    private Label _vitalsScaleValue;
    private Label _controlsHint;
    private AudioStreamPlayer _musicPlayer;
    private MobileControls _mobileControls;
    private UiAccessibility _uiAccessibility;
    private UiVisualStyle _uiVisualStyle;
    private readonly Dictionary<Control, ScrollContainer>
        _tabScrolls = new();
    private ColorRect _menuScanline;
    private ColorRect _menuRevealTop;
    private ColorRect _menuRevealBottom;
    private float _menuProjectionTime;
    private Tween _menuTween;
    private Tween _tabTween;
    private Tween _confirmationTween;
    private bool _closingMenu;
    private string _listeningAction;
    private int _appliedMode;
    private int _appliedAspect;
    private Vector2I _appliedSelectedSize = new(1280, 720);
    private bool _pausedByMenu;
    private bool _isMobileLayout;
    private bool _updatingGraphicsOptions;
    private bool _usesExternalLauncher;
    private bool _hideQuitForExternalLauncher;
    private PanelContainer _unappliedPanel;
    private Button _unappliedApplyButton;
    private Button _unappliedDiscardButton;
    private Button _unappliedKeepButton;
    private VBoxContainer _unappliedContent;
    private string _settingsOpenSignature = "";
    private bool _closeWithoutConfirmation;
    private float _graphicsRenderScale = 1.0f;
    private string _defaultSettingsSubtitle;

    public bool IsMenuOpen =>
        IsInstanceValid(_overlay) &&
        _overlay.Visible;

    public static DisplaySettings CreateFullPanel()
    {
        PackedScene scene =
            ResourceLoader.Load<PackedScene>(
                FullPanelScenePath);
        return scene.Instantiate<DisplaySettings>();
    }

    public void UseExternalLauncher(
        bool hideQuit = false)
    {
        _usesExternalLauncher = true;
        _hideQuitForExternalLauncher =
            hideQuit;
        if (IsInstanceValid(_openButton))
        {
            _openButton.Hide();
        }
        if (hideQuit &&
            IsInstanceValid(_quitButton))
        {
            _quitButton.Hide();
        }
    }

    public void OpenFromExternal()
    {
        OpenMenu();
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GamepadInput.EnsureUiMappings();

        _openButton = GetNode<Button>("OpenButton");
        _overlay = GetNode<Control>("Overlay");
        _settingsShade =
            GetNode<ColorRect>("Overlay/Shade");
        _settingsCard =
            GetNode<PanelContainer>("Overlay/Card");
        _overlay.ZIndex = 240;
        _overlay.MouseFilter =
            MouseFilterEnum.Stop;
        _settingsShade.MouseFilter =
            MouseFilterEnum.Stop;
        _settingsCard.ZIndex = 1;
        _settingsCard.ClipContents = true;
        _tabs = GetNode<TabContainer>("Overlay/Card/Margin/Content/Tabs");
        _tabs.Set(
            "tab_alignment",
            (int)HorizontalAlignment.Center);
        _settingsSubtitle =
            GetNode<Label>("Overlay/Card/Margin/Content/Subtitle");
        _defaultSettingsSubtitle = _settingsSubtitle.Text;
        const string displayPath = "Overlay/Card/Margin/Content/Tabs/Display";
        const string graphicsPath = "Overlay/Card/Margin/Content/Tabs/Graphics";
        const string audioPath = "Overlay/Card/Margin/Content/Tabs/Audio";
        const string gameplayPath = "Overlay/Card/Margin/Content/Tabs/Gameplay";
        const string interfacePath =
            "Overlay/Card/Margin/Content/Tabs/Interface";
        const string controlsPath = "Overlay/Card/Margin/Content/Tabs/Controls";
        _displayTab = GetNode<Control>(displayPath);
        _graphicsTab = GetNode<Control>(graphicsPath);
        _controlsTab = GetNode<Control>(controlsPath);
        _modeOption = GetNode<OptionButton>($"{displayPath}/Options/Mode");
        _aspectOption = GetNode<OptionButton>($"{displayPath}/Options/Aspect");
        _resolutionOption = GetNode<OptionButton>($"{displayPath}/Options/Resolution");
        _vsyncCheck = GetNode<CheckButton>($"{displayPath}/Options/VSync");
        _fpsOption = GetNode<OptionButton>($"{displayPath}/Options/MaxFps");
        _monitorLabel = GetNode<Label>($"{displayPath}/Monitor");
        _resolutionLabel =
            GetNode<Label>($"{displayPath}/Options/ResolutionLabel");
        _appliedLabel = GetNode<Label>($"{displayPath}/Applied");
        _graphicsPresetOption =
            GetNode<OptionButton>($"{graphicsPath}/Options/Preset");
        _graphicsRenderScaleOption =
            GetNode<OptionButton>($"{graphicsPath}/Options/RenderScale");
        _graphicsShadowsOption =
            GetNode<OptionButton>($"{graphicsPath}/Options/Shadows");
        _graphicsAntiAliasingOption =
            GetNode<OptionButton>($"{graphicsPath}/Options/AntiAliasing");
        _graphicsEffectsOption =
            GetNode<OptionButton>($"{graphicsPath}/Options/Effects");
        _graphicsAppliedLabel = GetNode<Label>($"{graphicsPath}/Applied");
        _masterVolumeSlider = GetNode<HSlider>($"{audioPath}/Levels/Master");
        _musicVolumeSlider = GetNode<HSlider>($"{audioPath}/Levels/Music");
        _masterVolumeValue = GetNode<Label>($"{audioPath}/Levels/MasterValue");
        _musicVolumeValue = GetNode<Label>($"{audioPath}/Levels/MusicValue");
        _enemyTrajectoryOption =
            GetNode<OptionButton>($"{gameplayPath}/Options/EnemyTrajectory");
        _survivalAssistOption =
            GetNode<OptionButton>($"{gameplayPath}/Options/SurvivalAssist");
        _stompAssistOption =
            GetNode<OptionButton>($"{gameplayPath}/Options/StompAssist");
        _uiScaleSlider =
            GetNode<HSlider>(
                $"{interfacePath}/Scale/UiScale");
        _textScaleSlider =
            GetNode<HSlider>(
                $"{interfacePath}/Scale/TextScale");
        _vitalsScaleSlider =
            GetNode<HSlider>(
                $"{interfacePath}/Scale/VitalsScale");
        _uiScaleValue =
            GetNode<Label>(
                $"{interfacePath}/Scale/UiValue");
        _textScaleValue =
            GetNode<Label>(
                $"{interfacePath}/Scale/TextValue");
        _vitalsScaleValue =
            GetNode<Label>(
                $"{interfacePath}/Scale/VitalsValue");
        _controlsHint = GetNode<Label>($"{controlsPath}/Hint");
        _musicPlayer = GetNodeOrNull<AudioStreamPlayer>("/root/MusicPlayer");
        _mobileControls = GetNodeOrNull<MobileControls>("../MobileControls");
        _uiAccessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _uiVisualStyle =
            GetNodeOrNull<UiVisualStyle>(
                "/root/UiVisualStyle");

        _isMobileLayout =
            PreviewMobileLayout ||
            (_uiAccessibility?.IsTouchLayout ??
                (OS.HasFeature("mobile") ||
                    DisplayServer.IsTouchscreenAvailable()));
        Visible = true;
        ConfigurePlatformLayout();

        BuildOptions();
        BuildGraphicsOptions();
        BuildGameplayOptions();
        BuildInterfaceOptions();
        BuildBindingButtons(controlsPath);
        EnsureAudioBuses();
        LoadSettings();
        GetViewport().SizeChanged += OnViewportSizeChanged;
        ApplySettings(false);
        _overlay.Hide();
        UpdateMenuLayout();
        UpdateVsyncText(_vsyncCheck.ButtonPressed);
        UpdateInputPresentation();

        _defaultsButton = GetNode<Button>(
            "Overlay/Card/Margin/Content/Buttons/Defaults");
        _quitButton = GetNodeOrNull<Button>(
            "Overlay/Card/Margin/Content/Buttons/Quit");
        _closeButton = GetNode<Button>(
            "Overlay/Card/Margin/Content/Buttons/Close");
        _applyButton = GetNode<Button>(
            "Overlay/Card/Margin/Content/Buttons/Apply");
        if (_isMobileLayout ||
            _hideQuitForExternalLauncher)
        {
            _quitButton?.Hide();
        }
        _openButton.Pressed += OpenMenu;
        _tabs.TabChanged += OnTabChanged;
        _defaultsButton.Pressed += RestoreDefaults;
        if (_quitButton != null)
        {
            _quitButton.Pressed += () =>
                GetTree().Quit();
        }
        _closeButton.Pressed += CloseMenu;
        _applyButton.Pressed += OnApplyPressed;
        BuildUnappliedChangesDialog();
        _modeOption.ItemSelected += OnModeSelected;
        _aspectOption.ItemSelected += OnAspectSelected;
        _vsyncCheck.Toggled += UpdateVsyncText;
        _masterVolumeSlider.ValueChanged += OnVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnVolumeChanged;
        _uiScaleSlider.ValueChanged +=
            OnInterfaceScaleChanged;
        _textScaleSlider.ValueChanged +=
            OnInterfaceScaleChanged;
        _vitalsScaleSlider.ValueChanged +=
            OnInterfaceScaleChanged;
        EnableSettingsScrolling();
        GetViewport().GuiFocusChanged +=
            OnSettingsFocusChanged;
        ApplyUnifiedMenuStyle(
            _settingsCard);
        _uiVisualStyle?.RefreshTree();
        ConfigureHolographicSurface();
        _tabs.GetTabBar().AddChild(new SettingsTabAccent { Name = "TabAccent" });
        BuildMenuRevealEdges();
        _uiAccessibility?.ApplyControlTree(
            _settingsCard);
        UpdateMenuLayout();
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_menuScanline) ||
            !IsInstanceValid(_settingsCard) ||
            !_overlay.Visible)
        {
            return;
        }

        _menuProjectionTime +=
            (float)delta;
        float travelHeight = Mathf.Max(
            16.0f,
            _settingsCard.Size.Y - 14.0f);
        float scanY =
            7.0f +
            Mathf.PosMod(
                _menuProjectionTime * 42.0f,
                travelHeight);
        _menuScanline.OffsetLeft =
            _settingsCard.Position.X +
            6.0f;
        _menuScanline.OffsetRight =
            _settingsCard.Position.X +
            _settingsCard.Size.X -
            6.0f;
        _menuScanline.OffsetTop =
            _settingsCard.Position.Y +
            scanY;
        _menuScanline.OffsetBottom =
            _menuScanline.OffsetTop +
            2.0f;
        _menuScanline.Color =
            new Color(
                0.36f,
                0.88f,
                1.0f,
                0.08f +
                    Mathf.Sin(
                        _menuProjectionTime * 2.8f) *
                    0.025f);
    }

    public override void _ExitTree()
    {
        Viewport viewport = GetViewport();
        if (viewport != null)
        {
            viewport.SizeChanged -=
                OnViewportSizeChanged;
            viewport.GuiFocusChanged -=
                OnSettingsFocusChanged;
        }
    }

    private void ConfigurePlatformLayout()
    {
        int displayTabIndex = _tabs.GetTabIdxFromControl(_displayTab);
        int controlsTabIndex = _tabs.GetTabIdxFromControl(_controlsTab);
        _tabs.SetTabHidden(displayTabIndex, _isMobileLayout);
        _tabs.SetTabHidden(controlsTabIndex, _isMobileLayout);
        if (!_isMobileLayout)
        {
            return;
        }

        // Display modes and render resolutions are controlled by the phone.
        // Keyboard bindings do not apply to the on-screen touch controls.
        int graphicsTabIndex =
            _tabs.GetTabIdxFromControl(
                _graphicsTab);
        int audioTabIndex =
            _tabs.GetTabIdxFromControl(
                GetNode<Control>(
                    "Overlay/Card/Margin/Content/Tabs/Audio"));
        int gameplayTabIndex =
            _tabs.GetTabIdxFromControl(
                GetNode<Control>(
                    "Overlay/Card/Margin/Content/Tabs/Gameplay"));
        int interfaceTabIndex =
            _tabs.GetTabIdxFromControl(
                GetNode<Control>(
                    "Overlay/Card/Margin/Content/Tabs/Interface"));
        _tabs.SetTabTitle(
            graphicsTabIndex,
            "VIDEO");
        _tabs.SetTabTitle(
            audioTabIndex,
            "AUDIO");
        _tabs.SetTabTitle(
            gameplayTabIndex,
            "PLAY");
        _tabs.SetTabTitle(
            interfaceTabIndex,
            "UI");
        _tabs.CurrentTab =
            graphicsTabIndex;
        _settingsSubtitle.Text =
            "VIDEO  •  AUDIO  •  PLAY  •  UI  •  ABOUT";

        // Keep the settings button clear of the virtual joystick and action
        // buttons. It sits directly below the momentum panel on mobile.
        _openButton.Text = "SETTINGS";
        _openButton.AnchorLeft = 1.0f;
        _openButton.AnchorTop = 0.0f;
        _openButton.AnchorRight = 1.0f;
        _openButton.AnchorBottom = 0.0f;
        _openButton.OffsetLeft = -154.0f;
        _openButton.OffsetTop = 110.0f;
        _openButton.OffsetRight = -18.0f;
        _openButton.OffsetBottom = 148.0f;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible)
        {
            return;
        }

        GamepadInput.Observe(inputEvent);
        UpdateInputPresentation();

        if (_listeningAction != null)
        {
            if (inputEvent.IsActionPressed("ui_cancel") ||
                inputEvent.IsActionPressed("pause"))
            {
                CancelBinding();
                GetViewport().SetInputAsHandled();
            }
            else if (inputEvent is InputEventKey keyEvent &&
                keyEvent.Pressed &&
                !keyEvent.Echo)
            {
                HandleBindingInput(keyEvent);
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (_overlay.Visible &&
            IsInstanceValid(
                _unappliedPanel) &&
            _unappliedPanel.Visible &&
            (inputEvent.IsActionPressed(
                    "ui_cancel") ||
                inputEvent.IsActionPressed(
                    "pause")))
        {
            KeepEditingFromConfirmation();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("pause"))
        {
            if (_usesExternalLauncher &&
                !_overlay.Visible)
            {
                return;
            }
            ToggleMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_overlay.Visible)
        {
            return;
        }

        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            CloseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("menu_tab_left"))
        {
            CycleVisibleTab(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("menu_tab_right"))
        {
            CycleVisibleTab(1);
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildOptions()
    {
        _modeOption.AddItem("WINDOWED");
        _modeOption.AddItem("BORDERLESS FULLSCREEN");
        _modeOption.AddItem("EXCLUSIVE FULLSCREEN");
        foreach (string aspectName in AspectNames)
        {
            _aspectOption.AddItem(aspectName);
        }

        Vector2I monitorSize = DisplayServer.ScreenGetSize();
        Rect2I usableRect = DisplayServer.ScreenGetUsableRect();
        if (monitorSize.X < 640 || monitorSize.Y < 480)
        {
            // Headless runs and a few window managers do not report a monitor.
            // Keep a safe list instead of leaving the selector empty.
            monitorSize = new Vector2I(1920, 1080);
            usableRect = new Rect2I(Vector2I.Zero, monitorSize);
        }

        _monitorLabel.Text =
            $"MONITOR  {monitorSize.X} × {monitorSize.Y}    USABLE  {usableRect.Size.X} × {usableRect.Size.Y}";

        foreach (Vector2I size in CommonWindowSizes)
        {
            if (size.X <= monitorSize.X && size.Y <= monitorSize.Y)
            {
                AddWindowSize(size);
            }
        }

        Vector2I currentSize = GetWindow().Size;
        if (currentSize.X < 640 || currentSize.Y < 480)
        {
            currentSize = new Vector2I(720, 540);
        }

        AddWindowSize(currentSize);
        SortWindowSizes();
        RebuildResolutionOptions();

        foreach (int fpsLimit in FpsLimits)
        {
            _fpsOption.AddItem(fpsLimit == 0 ? "UNLIMITED" : fpsLimit.ToString());
        }
    }

    private void BuildGraphicsOptions()
    {
        AddOptionItems(_graphicsPresetOption, GraphicsPresetNames);
        AddOptionItems(_graphicsRenderScaleOption, GraphicsRenderScaleNames);
        AddOptionItems(_graphicsShadowsOption, GraphicsShadowNames);
        AddOptionItems(
            _graphicsAntiAliasingOption,
            GraphicsAntiAliasingNames);
        AddOptionItems(_graphicsEffectsOption, GraphicsEffectsNames);

        _graphicsPresetOption.ItemSelected += OnGraphicsPresetSelected;
        _graphicsRenderScaleOption.ItemSelected += OnGraphicsOptionSelected;
        _graphicsShadowsOption.ItemSelected += OnGraphicsOptionSelected;
        _graphicsAntiAliasingOption.ItemSelected += OnGraphicsOptionSelected;
        _graphicsEffectsOption.ItemSelected += OnGraphicsOptionSelected;
    }

    private void BuildGameplayOptions()
    {
        AddOptionItems(
            _enemyTrajectoryOption,
            EnemyTrajectoryPreviewNames);
        AddOptionItems(_survivalAssistOption, AssistLevelNames);
        AddOptionItems(_stompAssistOption, AssistLevelNames);
    }

    private void BuildInterfaceOptions()
    {
        _uiScaleSlider.MinValue = 80.0;
        _uiScaleSlider.MaxValue = 140.0;
        _uiScaleSlider.Step = 5.0;
        _textScaleSlider.MinValue = 90.0;
        _textScaleSlider.MaxValue = 135.0;
        _textScaleSlider.Step = 5.0;
        _vitalsScaleSlider.MinValue = 75.0;
        _vitalsScaleSlider.MaxValue = 150.0;
        _vitalsScaleSlider.Step = 5.0;
    }

    private static void AddOptionItems(
        OptionButton optionButton,
        string[] itemNames)
    {
        optionButton.Clear();
        foreach (string itemName in itemNames)
        {
            optionButton.AddItem(itemName);
        }
    }

    private int GetDefaultGraphicsPreset()
    {
        return _isMobileLayout ? 2 : 3;
    }

    private void OnGraphicsPresetSelected(long selectedIndex)
    {
        int preset = Mathf.Clamp(
            (int)selectedIndex,
            0,
            GraphicsPresetNames.Length - 1);
        if (preset < CustomGraphicsPreset)
        {
            ApplyGraphicsPresetToOptions(preset);
        }
    }

    private void OnGraphicsOptionSelected(long selectedIndex)
    {
        if (_updatingGraphicsOptions)
        {
            return;
        }

        _graphicsPresetOption.Select(CustomGraphicsPreset);
    }

    private void ApplyGraphicsPresetToOptions(int preset)
    {
        int qualityIndex = Mathf.Clamp(preset, 0, CustomGraphicsPreset - 1);
        _updatingGraphicsOptions = true;
        _graphicsRenderScaleOption.Select(qualityIndex);
        _graphicsShadowsOption.Select(qualityIndex);
        _graphicsAntiAliasingOption.Select(qualityIndex);
        _graphicsEffectsOption.Select(qualityIndex);
        _updatingGraphicsOptions = false;
    }

    private void BuildBindingButtons(string controlsPath)
    {
        for (int index = 0; index < RemappableActions.Length; index++)
        {
            string action = RemappableActions[index];
            Button button = GetNode<Button>(
                $"{controlsPath}/Bindings/{ControlButtonNames[index]}");
            _bindingButtons[action] = button;
            button.Pressed += () => BeginBinding(action);
        }
    }

    private void EnsureAudioBuses()
    {
        MusicPlayer.EnsureMusicBus(
            _musicPlayer);
    }

    private void AddWindowSize(Vector2I size)
    {
        if (size.X < 640 || size.Y < 480 || _availableWindowSizes.Contains(size))
        {
            return;
        }

        _availableWindowSizes.Add(size);
    }

    private void SortWindowSizes()
    {
        _availableWindowSizes.Sort((left, right) =>
        {
            long leftPixels = (long)left.X * left.Y;
            long rightPixels = (long)right.X * right.Y;
            return rightPixels.CompareTo(leftPixels);
        });
    }

    private void RebuildResolutionOptions()
    {
        _resolutionOption.Clear();
        foreach (Vector2I size in _availableWindowSizes)
        {
            _resolutionOption.AddItem($"{size.X} × {size.Y}");
        }
    }

    private void LoadSettings()
    {
        ConfigFile settings = new();
        Error loadResult = settings.Load(SettingsPath);

        int mode = 0;
        Vector2I currentSize = GetWindow().Size;
        if (currentSize.X < 640 || currentSize.Y < 480)
        {
            currentSize = new Vector2I(720, 540);
        }

        Vector2I windowSize = currentSize;
        bool vsyncEnabled = true;
        int fpsLimit = 0;
        int aspect = 0;
        double masterVolume = 100.0;
        double musicVolume = 80.0;
        double uiScale =
            (_uiAccessibility?.UiScale ??
                (_isMobileLayout ? 1.2f : 1.0f)) *
            100.0;
        double textScale =
            (_uiAccessibility?.TextScale ??
                (_isMobileLayout ? 1.1f : 1.0f)) *
            100.0;
        double vitalsScale =
            (_uiAccessibility?.VitalsScale ??
                1.15f) *
            100.0;
        int enemyTrajectoryPreview = 1;
        int survivalAssistLevel = (int)Player.AssistLevel.Balanced;
        int stompAssistLevel = (int)Player.AssistLevel.Balanced;
        int graphicsPreset = GetDefaultGraphicsPreset();
        int defaultGraphicsQuality = Mathf.Min(
            graphicsPreset,
            CustomGraphicsPreset - 1);
        int renderQuality = defaultGraphicsQuality;
        int shadowQuality = defaultGraphicsQuality;
        int antiAliasingQuality = defaultGraphicsQuality;
        int effectsQuality = defaultGraphicsQuality;

        if (loadResult == Error.Ok)
        {
            mode = Mathf.Clamp(
                settings.GetValue(SettingsSection, "mode", 0).AsInt32(),
                0,
                _modeOption.ItemCount - 1);
            windowSize = new Vector2I(
                settings.GetValue(SettingsSection, "width", currentSize.X).AsInt32(),
                settings.GetValue(SettingsSection, "height", currentSize.Y).AsInt32());
            vsyncEnabled = settings.GetValue(SettingsSection, "vsync", true).AsBool();
            fpsLimit = settings.GetValue(SettingsSection, "max_fps", 0).AsInt32();
            aspect = Mathf.Clamp(
                settings.GetValue(SettingsSection, "aspect", 0).AsInt32(),
                0,
                AspectNames.Length - 1);
            masterVolume = System.Math.Clamp(
                settings.GetValue(AudioSection, "master_volume", 100.0).AsDouble(),
                0.0,
                100.0);
            musicVolume = System.Math.Clamp(
                settings.GetValue(AudioSection, "music_volume", 80.0).AsDouble(),
                0.0,
                100.0);
            uiScale = System.Math.Clamp(
                settings.GetValue(
                    AccessibilitySection,
                    "ui_scale",
                    uiScale / 100.0).AsDouble() *
                    100.0,
                80.0,
                140.0);
            textScale = System.Math.Clamp(
                settings.GetValue(
                    AccessibilitySection,
                    "text_scale",
                    textScale / 100.0).AsDouble() *
                    100.0,
                90.0,
                135.0);
            vitalsScale = System.Math.Clamp(
                settings.GetValue(
                    AccessibilitySection,
                    "vitals_scale",
                    vitalsScale / 100.0).AsDouble() *
                    100.0,
                75.0,
                150.0);
            int savedTrajectoryPreference =
                settings.GetValue(
                    GameplaySection,
                    "enemy_trajectory_preview",
                    2).AsInt32();
            enemyTrajectoryPreview =
                savedTrajectoryPreference <= 0
                    ? 0
                    : 1;
            survivalAssistLevel = LoadAssistPreset(
                settings,
                "survival_assist_level",
                (int)Player.AssistLevel.Balanced);
            stompAssistLevel = LoadAssistPreset(
                settings,
                "stomp_assist_level",
                (int)Player.AssistLevel.Balanced);
            graphicsPreset = Mathf.Clamp(
                settings.GetValue(
                    GraphicsSection,
                    "quality_preset",
                    graphicsPreset).AsInt32(),
                0,
                GraphicsPresetNames.Length - 1);
            int savedQualityFallback = graphicsPreset < CustomGraphicsPreset
                ? graphicsPreset
                : defaultGraphicsQuality;
            renderQuality = Mathf.Clamp(
                settings.GetValue(
                    GraphicsSection,
                    "render_quality",
                    savedQualityFallback).AsInt32(),
                0,
                GraphicsRenderScales.Length - 1);
            shadowQuality = Mathf.Clamp(
                settings.GetValue(
                    GraphicsSection,
                    "shadow_quality",
                    savedQualityFallback).AsInt32(),
                0,
                GraphicsShadowNames.Length - 1);
            antiAliasingQuality = Mathf.Clamp(
                settings.GetValue(
                    GraphicsSection,
                    "anti_aliasing",
                    savedQualityFallback).AsInt32(),
                0,
                GraphicsAntiAliasingNames.Length - 1);
            effectsQuality = Mathf.Clamp(
                settings.GetValue(
                    GraphicsSection,
                    "effects_quality",
                    savedQualityFallback).AsInt32(),
                0,
                GraphicsEffectsNames.Length - 1);
        }

        AddWindowSize(windowSize);
        SortWindowSizes();
        RebuildResolutionOptions();

        _modeOption.Select(mode);
        _aspectOption.Select(aspect);
        SelectResolution(windowSize);
        _vsyncCheck.ButtonPressed = vsyncEnabled;
        SelectFpsLimit(fpsLimit);
        _masterVolumeSlider.Value = masterVolume;
        _musicVolumeSlider.Value = musicVolume;
        _uiScaleSlider.Value = uiScale;
        _textScaleSlider.Value = textScale;
        _vitalsScaleSlider.Value = vitalsScale;
        _enemyTrajectoryOption.Select(enemyTrajectoryPreview);
        _survivalAssistOption.Select(survivalAssistLevel);
        _stompAssistOption.Select(stompAssistLevel);
        _updatingGraphicsOptions = true;
        _graphicsPresetOption.Select(graphicsPreset);
        _graphicsRenderScaleOption.Select(renderQuality);
        _graphicsShadowsOption.Select(shadowQuality);
        _graphicsAntiAliasingOption.Select(antiAliasingQuality);
        _graphicsEffectsOption.Select(effectsQuality);
        _updatingGraphicsOptions = false;
        LoadBindings(settings, loadResult == Error.Ok);
        ApplyBindings();
        UpdateBindingButtons();
        UpdateVolumeLabels();
        UpdateInterfaceScaleLabels();
        UpdateResolutionAvailability();
    }

    private static int LoadAssistPreset(
        ConfigFile settings,
        string key,
        int fallback)
    {
        if (!settings.HasSectionKey(GameplaySection, key))
        {
            return fallback;
        }

        int savedLevel =
            settings.GetValue(GameplaySection, key, fallback).AsInt32();
        int savedVersion =
            settings.GetValue(
                GameplaySection,
                "assist_preset_version",
                0).AsInt32();
        if (savedVersion < AssistPresetVersion)
        {
            // Legacy: OFF, LIGHT, BALANCED, STRONG.
            // Current: OFF (softer legacy BALANCED),
            // BALANCED (legacy STRONG), STRONG (new).
            return savedLevel >= LegacyStrongAssistIndex
                ? (int)Player.AssistLevel.Balanced
                : (int)Player.AssistLevel.Off;
        }

        return Mathf.Clamp(
            savedLevel,
            0,
            AssistLevelNames.Length - 1);
    }

    private void SelectResolution(Vector2I size)
    {
        int index = _availableWindowSizes.IndexOf(size);
        _resolutionOption.Select(index >= 0 ? index : _availableWindowSizes.Count - 1);
    }

    private void SelectFpsLimit(int fpsLimit)
    {
        int index = System.Array.IndexOf(FpsLimits, fpsLimit);
        _fpsOption.Select(index >= 0 ? index : FpsLimits.Length - 1);
    }

    private void OnApplyPressed()
    {
        ApplyCurrentSelection();
        _settingsOpenSignature =
            CaptureSettingsSignature();
        CloseMenuWithoutConfirmation();
    }

    public void ApplyCurrentSelection(bool save = true)
    {
        ApplySettings(save);
    }

    private void ApplySettings(bool save)
    {
        Window window = GetWindow();
        int mode = _modeOption.Selected;
        int aspect = Mathf.Clamp(_aspectOption.Selected, 0, AspectNames.Length - 1);
        Vector2I selectedSize = GetSelectedWindowSize();
        if (!_isMobileLayout)
        {
            Vector2I windowSize = FitAspectInside(selectedSize, aspect);
            _appliedMode = mode;
            _appliedAspect = aspect;
            _appliedSelectedSize = selectedSize;
            ApplyContentAspect(window, aspect);

            switch (mode)
            {
                case 1:
                    window.Borderless = true;
                    window.Mode = Window.ModeEnum.Fullscreen;
                    break;
                case 2:
                    window.Borderless = false;
                    window.Mode = Window.ModeEnum.ExclusiveFullscreen;
                    break;
                default:
                    window.Mode = Window.ModeEnum.Windowed;
                    window.Borderless = false;
                    window.Size = windowSize;
                    CenterWindow(window, windowSize);
                    ApplyWindowedSizeAgain(windowSize);
                    break;
            }

            DisplayServer.WindowSetVsyncMode(_vsyncCheck.ButtonPressed
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = FpsLimits[
                Mathf.Clamp(_fpsOption.Selected, 0, FpsLimits.Length - 1)];
        }

        ApplyGraphicsSettings();
        if (_isMobileLayout)
        {
            float finalRenderScale = ApplyGraphicsRenderScale(1.0f);
            UpdateGraphicsAppliedLabel(finalRenderScale);
        }
        else
        {
            ApplyRenderResolution(mode, selectedSize, aspect);
            ScheduleRenderResolutionReapply(mode, selectedSize, aspect);
        }

        ApplyAudioSettings();
        ApplyInterfaceSettings();
        ApplyGameplaySettings();
        ApplyBindings();

        if (save)
        {
            SaveSettings(mode, selectedSize, aspect);
        }
    }

    private void ApplyGraphicsSettings()
    {
        int renderQuality = Mathf.Clamp(
            _graphicsRenderScaleOption.Selected,
            0,
            GraphicsRenderScales.Length - 1);
        _graphicsRenderScale = GraphicsRenderScales[renderQuality];

        ApplyShadowQuality(_graphicsShadowsOption.Selected);
        ApplyAntiAliasingQuality(_graphicsAntiAliasingOption.Selected);
        ApplyEffectsQuality(_graphicsEffectsOption.Selected);
    }

    private void ApplyShadowQuality(int selectedQuality)
    {
        int quality = Mathf.Clamp(
            selectedQuality,
            0,
            GraphicsShadowNames.Length - 1);

        int atlasSize = quality switch
        {
            0 => 512,
            1 => 1024,
            2 => 2048,
            _ => 4096
        };
        RenderingServer.ShadowQuality filterQuality = quality switch
        {
            0 => RenderingServer.ShadowQuality.Hard,
            1 => RenderingServer.ShadowQuality.Hard,
            2 => RenderingServer.ShadowQuality.SoftVeryLow,
            _ => RenderingServer.ShadowQuality.SoftMedium
        };
        RenderingServer.DirectionalShadowAtlasSetSize(atlasSize, false);
        RenderingServer.DirectionalSoftShadowFilterSetQuality(filterQuality);

        foreach (Node node in GetTree().GetNodesInGroup(
            "quality_directional_light"))
        {
            if (node is not DirectionalLight3D light)
            {
                continue;
            }

            light.ShadowEnabled = quality > 0;
            light.DirectionalShadowBlendSplits = false;
            // A single shadow map is the stable high-quality option for this
            // orthographic camera. PSSM split updates could drop small moving
            // casters for a frame while the travelling camera was following.
            light.DirectionalShadowMode = quality == 2
                ? DirectionalLight3D.ShadowMode.Parallel2Splits
                : DirectionalLight3D.ShadowMode.Orthogonal;
            light.DirectionalShadowFadeStart = quality >= 3 ? 0.95f : 0.8f;
            light.DirectionalShadowMaxDistance = quality switch
            {
                1 => 30.0f,
                2 => 52.0f,
                _ => 72.0f
            };
        }

    }

    private void ApplyAntiAliasingQuality(int selectedQuality)
    {
        int quality = Mathf.Clamp(
            selectedQuality,
            0,
            GraphicsAntiAliasingNames.Length - 1);
        Viewport viewport = GetViewport();
        viewport.Msaa3D = quality switch
        {
            2 => Viewport.Msaa.Msaa2X,
            3 => Viewport.Msaa.Msaa4X,
            _ => Viewport.Msaa.Disabled
        };
        viewport.ScreenSpaceAA = quality == 1
            ? Viewport.ScreenSpaceAAEnum.Fxaa
            : Viewport.ScreenSpaceAAEnum.Disabled;
    }

    private void ApplyEffectsQuality(int selectedQuality)
    {
        int quality = Mathf.Clamp(
            selectedQuality,
            0,
            GraphicsEffectsNames.Length - 1);
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.SetEffectsQuality(quality);

        MomentumOverlay overlay =
            GetNodeOrNull<MomentumOverlay>("../MomentumOverlay");
        if (overlay != null)
        {
            overlay.DetailLevel = quality;
        }
    }

    private float ApplyGraphicsRenderScale(float displayRenderScale)
    {
        // Display render targets and graphics quality are both upper bounds.
        // Taking the lower one avoids accidentally stacking two downscales.
        float finalRenderScale = Mathf.Clamp(
            Mathf.Min(displayRenderScale, _graphicsRenderScale),
            0.25f,
            1.0f);
        Viewport viewport = GetViewport();
        viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = finalRenderScale;
        return finalRenderScale;
    }

    private void UpdateGraphicsAppliedLabel(float finalRenderScale)
    {
        _graphicsAppliedLabel.Text =
            $"ACTIVE  RENDER {Mathf.RoundToInt(finalRenderScale * 100.0f)}%  " +
            $"-  SHADOWS {GraphicsShadowNames[Mathf.Clamp(
                _graphicsShadowsOption.Selected,
                0,
                GraphicsShadowNames.Length - 1)]}  " +
            $"-  {GraphicsAntiAliasingNames[Mathf.Clamp(
                _graphicsAntiAliasingOption.Selected,
                0,
                GraphicsAntiAliasingNames.Length - 1)]}  " +
            $"-  EFFECTS {GraphicsEffectsNames[Mathf.Clamp(
                _graphicsEffectsOption.Selected,
                0,
                GraphicsEffectsNames.Length - 1)]}";
    }

    private void ScheduleRenderResolutionReapply(int mode, Vector2I selectedSize, int aspect)
    {
        if (mode == 0)
        {
            return;
        }

        // Fullscreen transitions are asynchronous on desktop platforms. The
        // render buffer can be recreated after this frame, resetting its scale.
        SceneTreeTimer renderTimer = GetTree().CreateTimer(0.2, true);
        renderTimer.Timeout += () =>
        {
            if (_appliedMode == mode &&
                _appliedAspect == aspect &&
                _appliedSelectedSize == selectedSize)
            {
                ApplyRenderResolution(mode, selectedSize, aspect);
            }
        };
    }

    private void OnViewportSizeChanged()
    {
        UpdateMenuLayout();
        if (_appliedMode != 0)
        {
            ApplyRenderResolution(_appliedMode, _appliedSelectedSize, _appliedAspect);
        }
    }

    private void UpdateMenuLayout()
    {
        if (!IsInstanceValid(_settingsCard))
        {
            return;
        }

        Vector2 viewportSize =
            GetViewportRect().Size;
        Vector4 safeInsets =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layoutScale =
            _uiAccessibility?.LayoutScale ??
            1.0f;
        float availableWidth = Mathf.Max(
            1.0f,
            viewportSize.X -
                safeInsets.X -
                safeInsets.Z);
        float availableHeight = Mathf.Max(
            1.0f,
            viewportSize.Y -
                safeInsets.Y -
                safeInsets.W);
        float margin =
            (_isMobileLayout ? 8.0f : 15.0f) *
            layoutScale;
        bool portrait =
            viewportSize.Y >
            viewportSize.X;
        float cardWidth =
            Mathf.Min(
                760.0f * layoutScale,
                availableWidth *
                    (portrait
                        ? 0.92f
                        : _isMobileLayout
                            ? 0.88f
                            : 0.72f));
        float cardHeight =
            Mathf.Min(
                720.0f * layoutScale,
                availableHeight -
                    margin * 2.0f);
        cardWidth =
            Mathf.Min(
                Mathf.Max(
                    280.0f,
                    cardWidth),
                Mathf.Max(
                    1.0f,
                    availableWidth -
                        margin * 2.0f));
        cardHeight =
            Mathf.Min(
                Mathf.Max(
                    220.0f,
                    cardHeight),
                Mathf.Max(
                    1.0f,
                    availableHeight -
                        margin * 2.0f));
        _settingsCard.SetAnchorsPreset(
            Control.LayoutPreset.TopLeft);
        _settingsCard.OffsetLeft =
            safeInsets.X +
            (availableWidth -
                cardWidth) *
                0.5f;
        _settingsCard.OffsetRight =
            _settingsCard.OffsetLeft +
            cardWidth;
        _settingsCard.OffsetTop =
            safeInsets.Y +
            (availableHeight -
                cardHeight) *
                0.5f;
        _settingsCard.OffsetBottom =
            _settingsCard.OffsetTop +
            cardHeight;
        _settingsCard.PivotOffset =
            new Vector2(
                cardWidth * 0.5f,
                cardHeight * 0.5f);
        _settingsCard.Rotation = 0.0f;
        if (IsInstanceValid(
                _unappliedPanel))
        {
            _unappliedPanel
                .SetAnchorsPreset(
                    Control.LayoutPreset.TopLeft);
            _unappliedPanel.OffsetLeft =
                _settingsCard.OffsetLeft;
            _unappliedPanel.OffsetRight =
                _settingsCard.OffsetRight;
            _unappliedPanel.OffsetTop =
                _settingsCard.OffsetTop;
            _unappliedPanel.OffsetBottom =
                _settingsCard.OffsetBottom;
            float confirmationWidth =
                Mathf.Min(
                    320.0f * layoutScale,
                    Mathf.Max(
                        150.0f,
                        cardWidth -
                            (_isMobileLayout
                                ? 38.0f
                                : 64.0f) *
                            layoutScale));
            if (IsInstanceValid(
                    _unappliedContent))
            {
                _unappliedContent
                    .CustomMinimumSize =
                    new Vector2(
                        confirmationWidth,
                        0.0f);
            }
            foreach (Button button in
                new[]
                {
                    _unappliedApplyButton,
                    _unappliedDiscardButton,
                    _unappliedKeepButton
                })
            {
                if (IsInstanceValid(button))
                {
                    button.CustomMinimumSize =
                        new Vector2(
                            Mathf.Min(
                                240.0f *
                                    layoutScale,
                                confirmationWidth),
                            44.0f *
                                layoutScale);
                }
            }
        }
        _settingsSubtitle.Visible = availableWidth >= 600 * layoutScale;
        ApplyMobileSettingsMetrics(
            cardWidth,
            layoutScale);
    }

    private void EnableSettingsScrolling()
    {
        if (_tabScrolls.Count > 0 ||
            !IsInstanceValid(_tabs))
        {
            return;
        }

        foreach (Node child in _tabs.GetChildren())
        {
            if (child is not Control tab)
            {
                continue;
            }

            Godot.Collections.Array<Node>
                originalChildren =
                    tab.GetChildren();
            ScrollContainer scroll = new()
            {
                Name = "ContentScroll",
                SizeFlagsHorizontal =
                    Control.SizeFlags.ExpandFill,
                SizeFlagsVertical =
                    Control.SizeFlags.ExpandFill,
                HorizontalScrollMode =
                    ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode =
                    ScrollContainer.ScrollMode.Auto,
                FollowFocus = true
            };
            VBoxContainer content = new()
            {
                Name = "ScrollContent",
                SizeFlagsHorizontal =
                    Control.SizeFlags.ExpandFill
            };
            content.AddThemeConstantOverride(
                "separation",
                7);
            tab.AddChild(scroll);
            scroll.AddChild(content);
            foreach (Node originalChild in
                originalChildren)
            {
                originalChild.Reparent(content);
            }
            ConfigureScrollChrome(scroll);
            _tabScrolls[tab] = scroll;
        }

        _tabs.SizeFlagsHorizontal =
            Control.SizeFlags.ExpandFill;
        _tabs.SizeFlagsVertical =
            Control.SizeFlags.ExpandFill;
        _tabs.CustomMinimumSize = Vector2.Zero;
        _settingsCard.ClipContents = true;
    }

    private void ConfigureScrollChrome(
        ScrollContainer scroll)
    {
        ScrollBar[] bars =
        {
            scroll.GetVScrollBar(),
            scroll.GetHScrollBar()
        };
        foreach (ScrollBar bar in bars)
        {
            bar.Modulate =
                new Color(
                    1.0f,
                    1.0f,
                    1.0f,
                    _isMobileLayout
                        ? 0.34f
                        : 0.0f);
            bar.MouseFilter =
                MouseFilterEnum.Ignore;
            bar.FocusMode =
                FocusModeEnum.None;
        }
    }

    private void ApplyMobileSettingsMetrics(
        float cardWidth,
        float layoutScale)
    {
        // Apply responsive widths on desktop as well as touch layouts.
        MarginContainer margin =
            _settingsCard.GetNodeOrNull<
                MarginContainer>("Margin");
        if (IsInstanceValid(margin))
        {
            margin.AddThemeConstantOverride(
                "margin_left",
                Mathf.RoundToInt(
                    18.0f * layoutScale));
            margin.AddThemeConstantOverride(
                "margin_top",
                Mathf.RoundToInt(
                    12.0f * layoutScale));
            margin.AddThemeConstantOverride(
                "margin_right",
                Mathf.RoundToInt(
                    18.0f * layoutScale));
            margin.AddThemeConstantOverride(
                "margin_bottom",
                Mathf.RoundToInt(
                    14.0f * layoutScale));
        }

        float usableWidth = Mathf.Max(1, cardWidth - 68 * layoutScale);
        foreach (Node descendant in
            GetDescendants(_tabs))
        {
            if (descendant is not
                GridContainer grid)
            {
                continue;
            }

            grid.AddThemeConstantOverride(
                "h_separation",
                Mathf.RoundToInt(
                    8.0f * layoutScale));
            Godot.Collections.Array<Node>
                cells = grid.GetChildren();
            if (grid.Columns == 2)
            {
                float labelWidth =
                    usableWidth * 0.34f;
                float valueWidth =
                    usableWidth -
                    labelWidth -
                    8.0f * layoutScale;
                for (int index = 0;
                    index < cells.Count;
                    index++)
                {
                    if (cells[index] is
                        Control control)
                    {
                        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                        control.SizeFlagsVertical = Control.SizeFlags.Fill;
                        if (control is Label label) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.ClipText = true; }
                        control.CustomMinimumSize =
                            new Vector2(
                                index % 2 == 0
                                    ? labelWidth
                                    : valueWidth,
                                Mathf.Max(control.CustomMinimumSize.Y, 38 * layoutScale));
                    }
                }
            }
            else if (grid.Columns == 3)
            {
                float first =
                    usableWidth * 0.27f;
                float third =
                    usableWidth * 0.16f;
                float second =
                    usableWidth -
                    first -
                    third -
                    16.0f * layoutScale;
                float[] widths =
                {
                    first,
                    second,
                    third
                };
                for (int index = 0;
                    index < cells.Count;
                    index++)
                {
                    if (cells[index] is
                        Control control)
                    {
                        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                        control.SizeFlagsVertical = Control.SizeFlags.Fill;
                        if (control is Label label) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.ClipText = true; }
                        control.CustomMinimumSize =
                            new Vector2(
                                widths[index % 3],
                                Mathf.Max(control.CustomMinimumSize.Y, 38 * layoutScale));
                    }
                }
            }
        }

        HBoxContainer buttons =
            _settingsCard.GetNodeOrNull<
                HBoxContainer>(
                "Margin/Content/Buttons");
        if (IsInstanceValid(buttons))
        {
            List<Button> visibleButtons = new();
            foreach (Node child in
                buttons.GetChildren())
            {
                if (child is Button button &&
                    button.Visible)
                {
                    visibleButtons.Add(button);
                }
            }
            float buttonWidth =
                Mathf.Max(
                    64.0f,
                    (usableWidth -
                        Mathf.Max(
                            0,
                            visibleButtons.Count - 1) *
                        8.0f * layoutScale) /
                    Mathf.Max(
                        1,
                        visibleButtons.Count));
            buttons.AddThemeConstantOverride(
                "separation",
                Mathf.RoundToInt(
                    8.0f * layoutScale));
            foreach (Button button in
                visibleButtons)
            {
                button.CustomMinimumSize =
                    new Vector2(
                        buttonWidth,
                        Mathf.Max(
                            42.0f * layoutScale,
                            button.CustomMinimumSize.Y));
            }
        }
    }

    private static IEnumerable<Node>
        GetDescendants(Node root)
    {
        foreach (Node child in
            root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in
                GetDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void ConfigureHolographicSurface()
    {
        _settingsShade.Color =
            Colors.White;
        _settingsShade.Material =
            CreateSettingsFocusMaterial();
        _menuScanline = new ColorRect
        {
            Name = "ProjectionScanline",
            Visible = false,
            Color =
                new Color(
                    0.36f,
                    0.88f,
                    1.0f,
                    0.1f),
            MouseFilter =
                Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(
            _menuScanline);
    }

    private static ShaderMaterial
        CreateSettingsFocusMaterial()
    {
        return MenuVisualStyle.FocusMaterial();
    }

    private void OnSettingsFocusChanged(
        Control focusedControl)
    {
        if (!IsInstanceValid(focusedControl) ||
            !IsDescendantOf(
                focusedControl,
                _tabs))
        {
            return;
        }

        Node current = focusedControl;
        while (current != null &&
            current != _tabs)
        {
            if (current is
                ScrollContainer scroll)
            {
                Callable.From(
                    () =>
                        scroll.EnsureControlVisible(
                            focusedControl))
                    .CallDeferred();
                return;
            }
            current = current.GetParent();
        }
    }

    private static bool IsDescendantOf(
        Node node,
        Node ancestor)
    {
        Node current = node;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }
            current = current.GetParent();
        }
        return false;
    }

    private void ApplyWindowedSizeAgain(Vector2I windowSize)
    {
        // Some desktop window managers finish the fullscreen-to-windowed
        // transition one frame later and otherwise overwrite the requested size.
        SceneTreeTimer resizeTimer = GetTree().CreateTimer(0.05, true);
        resizeTimer.Timeout += () =>
        {
            Window window = GetWindow();
            if (window.Mode != Window.ModeEnum.Windowed)
            {
                return;
            }

            window.Size = windowSize;
            CenterWindow(window, windowSize);
            float finalRenderScale = ApplyGraphicsRenderScale(1.0f);
            UpdateAppliedResolution(
                0,
                windowSize,
                windowSize,
                finalRenderScale,
                _appliedAspect);
            UpdateGraphicsAppliedLabel(finalRenderScale);
        };
    }

    private void ApplyRenderResolution(int mode, Vector2I selectedSize, int aspect)
    {
        Vector2I outputSize = mode == 0 ? selectedSize : DisplayServer.ScreenGetSize();
        if (outputSize.X < 640 || outputSize.Y < 480)
        {
            outputSize = selectedSize;
        }

        Vector2I viewportOutputSize = FitAspectInside(outputSize, aspect);
        Vector2I renderTargetSize = FitAspectInside(selectedSize, aspect);
        float renderScale = 1.0f;
        if (mode != 0)
        {
            renderScale = Mathf.Clamp(
                Mathf.Min(
                    renderTargetSize.X / (float)viewportOutputSize.X,
                    renderTargetSize.Y / (float)viewportOutputSize.Y),
                0.25f,
                1.0f);
        }

        float finalRenderScale = ApplyGraphicsRenderScale(renderScale);
        UpdateAppliedResolution(
            mode,
            outputSize,
            viewportOutputSize,
            finalRenderScale,
            aspect);
        UpdateGraphicsAppliedLabel(finalRenderScale);
    }

    private void UpdateAppliedResolution(
        int mode,
        Vector2I outputSize,
        Vector2I viewportOutputSize,
        float renderScale,
        int aspect)
    {
        Vector2I renderSize = new(
            Mathf.RoundToInt(viewportOutputSize.X * renderScale),
            Mathf.RoundToInt(viewportOutputSize.Y * renderScale));
        string aspectName = AspectNames[Mathf.Clamp(aspect, 0, AspectNames.Length - 1)];

        _appliedLabel.Text = mode == 0
            ? $"APPLIED  WINDOW {viewportOutputSize.X} × {viewportOutputSize.Y}  •  {aspectName}  •  3D NATIVE"
            : $"APPLIED  OUTPUT {outputSize.X} × {outputSize.Y}  •  VIEW {viewportOutputSize.X} × {viewportOutputSize.Y}  •  {aspectName}  •  3D {renderSize.X} × {renderSize.Y} ({Mathf.RoundToInt(renderScale * 100.0f)}%)";
    }

    private static void ApplyContentAspect(Window window, int aspect)
    {
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        if (aspect <= 0 || aspect >= AspectRatios.Length)
        {
            window.ContentScaleSize = new Vector2I(720, 540);
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        }
        else
        {
            const int baseHeight = 540;
            int baseWidth = MakeEven(Mathf.RoundToInt(baseHeight * AspectRatios[aspect]));
            window.ContentScaleSize = new Vector2I(baseWidth, baseHeight);
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        }

        RenderingServer.SetDefaultClearColor(new Color(0.008f, 0.012f, 0.02f));
    }

    private static Vector2I FitAspectInside(Vector2I bounds, int aspect)
    {
        if (aspect <= 0 || aspect >= AspectRatios.Length)
        {
            return bounds;
        }

        float targetAspect = AspectRatios[aspect];
        float boundsAspect = bounds.X / (float)bounds.Y;

        if (boundsAspect > targetAspect)
        {
            return new Vector2I(MakeEven(Mathf.RoundToInt(bounds.Y * targetAspect)), bounds.Y);
        }

        return new Vector2I(bounds.X, MakeEven(Mathf.RoundToInt(bounds.X / targetAspect)));
    }

    private static int MakeEven(int value)
    {
        return Mathf.Max(2, value - value % 2);
    }

    private static void CenterWindow(Window window, Vector2I windowSize)
    {
        Rect2I usableRect = DisplayServer.ScreenGetUsableRect();
        Vector2I centeredPosition = usableRect.Position + (usableRect.Size - windowSize) / 2;
        window.Position = new Vector2I(
            Mathf.Max(usableRect.Position.X, centeredPosition.X),
            Mathf.Max(usableRect.Position.Y, centeredPosition.Y));
    }

    private Vector2I GetSelectedWindowSize()
    {
        if (_availableWindowSizes.Count == 0)
        {
            return new Vector2I(1280, 720);
        }

        int index = Mathf.Clamp(_resolutionOption.Selected, 0, _availableWindowSizes.Count - 1);
        return _availableWindowSizes[index];
    }

    private void SaveSettings(int mode, Vector2I windowSize, int aspect)
    {
        ConfigFile settings = new();
        settings.SetValue(SettingsSection, "mode", mode);
        settings.SetValue(SettingsSection, "width", windowSize.X);
        settings.SetValue(SettingsSection, "height", windowSize.Y);
        settings.SetValue(SettingsSection, "aspect", aspect);
        settings.SetValue(SettingsSection, "vsync", _vsyncCheck.ButtonPressed);
        settings.SetValue(
            SettingsSection,
            "max_fps",
            FpsLimits[Mathf.Clamp(_fpsOption.Selected, 0, FpsLimits.Length - 1)]);
        settings.SetValue(AudioSection, "master_volume", _masterVolumeSlider.Value);
        settings.SetValue(AudioSection, "music_volume", _musicVolumeSlider.Value);
        settings.SetValue(
            AccessibilitySection,
            "ui_scale",
            _uiScaleSlider.Value / 100.0);
        settings.SetValue(
            AccessibilitySection,
            "text_scale",
            _textScaleSlider.Value / 100.0);
        settings.SetValue(
            AccessibilitySection,
            "vitals_scale",
            _vitalsScaleSlider.Value / 100.0);
        settings.SetValue(
            GameplaySection,
            "enemy_trajectory_preview",
            _enemyTrajectoryOption.Selected == 0
                ? 0
                : 2);
        settings.SetValue(
            GameplaySection,
            "survival_assist_level",
            _survivalAssistOption.Selected);
        settings.SetValue(
            GameplaySection,
            "stomp_assist_level",
            _stompAssistOption.Selected);
        settings.SetValue(
            GameplaySection,
            "assist_preset_version",
            AssistPresetVersion);
        settings.SetValue(
            GraphicsSection,
            "quality_preset",
            _graphicsPresetOption.Selected);
        settings.SetValue(
            GraphicsSection,
            "render_quality",
            _graphicsRenderScaleOption.Selected);
        settings.SetValue(
            GraphicsSection,
            "shadow_quality",
            _graphicsShadowsOption.Selected);
        settings.SetValue(
            GraphicsSection,
            "anti_aliasing",
            _graphicsAntiAliasingOption.Selected);
        settings.SetValue(
            GraphicsSection,
            "effects_quality",
            _graphicsEffectsOption.Selected);
        foreach (string action in RemappableActions)
        {
            settings.SetValue(ControlsSection, action, (long)_pendingBindings[action]);
        }

        Error saveResult = settings.Save(SettingsPath);
        if (saveResult != Error.Ok)
        {
            GD.PushWarning($"Could not save display settings: {saveResult}");
        }
    }

    private void RestoreDefaults()
    {
        _modeOption.Select(0);
        _aspectOption.Select(0);
        SelectResolution(new Vector2I(1280, 720));
        _vsyncCheck.ButtonPressed = true;
        SelectFpsLimit(0);
        _masterVolumeSlider.Value = 100.0;
        _musicVolumeSlider.Value = 80.0;
        _uiScaleSlider.Value =
            _isMobileLayout ? 120.0 : 100.0;
        _textScaleSlider.Value =
            _isMobileLayout ? 110.0 : 100.0;
        _vitalsScaleSlider.Value =
            115.0;
        _enemyTrajectoryOption.Select(1);
        _survivalAssistOption.Select((int)Player.AssistLevel.Balanced);
        _stompAssistOption.Select((int)Player.AssistLevel.Balanced);
        int defaultGraphicsPreset = GetDefaultGraphicsPreset();
        _graphicsPresetOption.Select(defaultGraphicsPreset);
        ApplyGraphicsPresetToOptions(defaultGraphicsPreset);
        for (int index = 0; index < RemappableActions.Length; index++)
        {
            _pendingBindings[RemappableActions[index]] = DefaultKeys[index];
        }

        UpdateBindingButtons();
        UpdateVolumeLabels();
        UpdateResolutionAvailability();
    }

    private void OnModeSelected(long index)
    {
        UpdateResolutionAvailability();
    }

    private void OnAspectSelected(long index)
    {
        UpdateResolutionAvailability();
    }

    private void UpdateResolutionAvailability()
    {
        bool isWindowed = _modeOption.Selected == 0;
        _resolutionOption.Disabled = false;
        _resolutionLabel.Text = isWindowed ? "WINDOW SIZE" : "3D RENDER TARGET";
        _resolutionOption.TooltipText = isWindowed
            ? "Changes the physical window size."
            : "Fullscreen output stays native; this changes the internal 3D render resolution.";
    }

    private void UpdateVsyncText(bool enabled)
    {
        SettingsToggleVisual visual =
            _vsyncCheck.GetNodeOrNull<SettingsToggleVisual>(
                "GradientToggleVisual");
        if (IsInstanceValid(visual))
        {
            _vsyncCheck.Text = string.Empty;
            visual.SetEnabledState(enabled);
            return;
        }

        _vsyncCheck.Text = enabled
            ? "ENABLED"
            : "DISABLED";
    }

    private void ApplyGameplaySettings()
    {
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null)
        {
            player.SetControlStyle(
                (int)Player.ControlStyle.Agile);
            player.SetContractRecoveryMode(
                (int)Player.ContractRecoveryMode.HuntOrEndure);
            player.SetSurvivalAssistLevel(_survivalAssistOption.Selected);
            player.SetStompAssistLevel(_stompAssistOption.Selected);
        }

        Mob.SetTrajectoryPreviewPreference(
            _enemyTrajectoryOption.Selected == 0
                ? 0
                : 2);
    }

    private void OnVolumeChanged(double value)
    {
        UpdateVolumeLabels();
        ApplyAudioSettings();
    }

    private void UpdateVolumeLabels()
    {
        _masterVolumeValue.Text = $"{Mathf.RoundToInt(_masterVolumeSlider.Value)}%";
        _musicVolumeValue.Text = $"{Mathf.RoundToInt(_musicVolumeSlider.Value)}%";
    }

    private void ApplyAudioSettings()
    {
        SetBusVolume("Master", (float)_masterVolumeSlider.Value);
        SetBusVolume("Music", (float)_musicVolumeSlider.Value);
    }

    private void OnInterfaceScaleChanged(
        double value)
    {
        UpdateInterfaceScaleLabels();
        ApplyInterfaceSettings();
        _uiAccessibility?.ApplyControlTree(
            _settingsCard);
        UpdateMenuLayout();
    }

    private void ApplyInterfaceSettings()
    {
        _uiAccessibility?.SetScales(
            (float)_uiScaleSlider.Value /
                100.0f,
            (float)_textScaleSlider.Value /
                100.0f);
        _uiAccessibility?.SetVitalsScale(
            (float)_vitalsScaleSlider.Value /
                100.0f);
    }

    private void UpdateInterfaceScaleLabels()
    {
        _uiScaleValue.Text =
            $"{Mathf.RoundToInt(
                _uiScaleSlider.Value)}%";
        _textScaleValue.Text =
            $"{Mathf.RoundToInt(
                _textScaleSlider.Value)}%";
        _vitalsScaleValue.Text =
            $"{Mathf.RoundToInt(
                _vitalsScaleSlider.Value)}%";
    }

    private static void SetBusVolume(string busName, float percentage)
    {
        MusicPlayer.SetBusVolume(
            busName,
            percentage);
    }

    private void LoadBindings(ConfigFile settings, bool settingsLoaded)
    {
        _pendingBindings.Clear();
        for (int index = 0; index < RemappableActions.Length; index++)
        {
            long defaultKey = (long)DefaultKeys[index];
            long savedKey = settingsLoaded
                ? settings.GetValue(ControlsSection, RemappableActions[index], defaultKey).AsInt64()
                : defaultKey;
            if (RemappableActions[index] == "dash" &&
                savedKey == (long)Key.E)
            {
                savedKey = (long)Key.Shift;
            }
            _pendingBindings[RemappableActions[index]] =
                savedKey == 0 ? DefaultKeys[index] : (Key)savedKey;
        }
    }

    private void ApplyBindings()
    {
        foreach (string action in RemappableActions)
        {
            if (!InputMap.HasAction(action))
            {
                continue;
            }

            foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action))
            {
                if (inputEvent is InputEventKey)
                {
                    InputMap.ActionEraseEvent(action, inputEvent);
                }
            }

            InputMap.ActionAddEvent(action, new InputEventKey
            {
                PhysicalKeycode = _pendingBindings[action]
            });
        }
    }

    private void BeginBinding(string action)
    {
        if (GamepadInput.ShouldShowGamepadPrompts)
        {
            _controlsHint.Text =
                "GAMEPAD: RB/R1 RIFT STEP  •  RT/R2 ANALOG THRUST  •  A/CROSS JUMP";
            return;
        }

        _listeningAction = action;
        UpdateBindingButtons();
        _bindingButtons[action].Text = "PRESS A KEY…";
        _controlsHint.Text = "PRESS A KEY  •  ESC TO CANCEL";
    }

    private void HandleBindingInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
        {
            CancelBinding();
            return;
        }

        Key newKey = keyEvent.PhysicalKeycode != Key.None
            ? keyEvent.PhysicalKeycode
            : keyEvent.Keycode;
        if (newKey == Key.None)
        {
            return;
        }
        if (newKey == Key.F10)
        {
            _controlsHint.Text = "F10 IS RESERVED FOR SETTINGS";
            return;
        }

        string currentAction = _listeningAction;
        Key previousKey = _pendingBindings[currentAction];
        foreach (string action in RemappableActions)
        {
            if (action != currentAction && _pendingBindings[action] == newKey)
            {
                _pendingBindings[action] = previousKey;
                break;
            }
        }

        _pendingBindings[currentAction] = newKey;
        _listeningAction = null;
        _controlsHint.Text = "CLICK A BINDING, THEN PRESS A KEY";
        UpdateBindingButtons();
    }

    private void CancelBinding()
    {
        _listeningAction = null;
        _controlsHint.Text = "CLICK A BINDING, THEN PRESS A KEY";
        UpdateBindingButtons();
    }

    private void UpdateBindingButtons()
    {
        foreach (string action in RemappableActions)
        {
            if (!_bindingButtons.TryGetValue(action, out Button button))
            {
                continue;
            }

            button.Text = GetPhysicalKeyName(_pendingBindings[action]);
        }
    }

    private static string GetPhysicalKeyName(Key physicalKey)
    {
        // OS.GetKeycodeString and DisplayServer.KeyboardGetLabelFromPhysical
        // both depend on an active desktop display server. Keeping this
        // conversion local also makes the menu safe in headless builds.
        return physicalKey.ToString().ToUpperInvariant();
    }

    private void BuildUnappliedChangesDialog()
    {
        _unappliedPanel =
            new PanelContainer
            {
                Name =
                    "UnappliedChanges",
                ProcessMode =
                    ProcessModeEnum.Always
            };
        _unappliedPanel.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxEmpty());
        _overlay.AddChild(
            _unappliedPanel);
        MarginContainer margin = new()
        {
            SizeFlagsHorizontal =
                Control.SizeFlags.ExpandFill,
            SizeFlagsVertical =
                Control.SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride(
            "margin_left",
            30);
        margin.AddThemeConstantOverride(
            "margin_top",
            30);
        margin.AddThemeConstantOverride(
            "margin_right",
            30);
        margin.AddThemeConstantOverride(
            "margin_bottom",
            30);
        _unappliedPanel.AddChild(margin);
        CenterContainer centre = new()
        {
            SizeFlagsHorizontal =
                Control.SizeFlags.ExpandFill,
            SizeFlagsVertical =
                Control.SizeFlags.ExpandFill
        };
        margin.AddChild(centre);
        _unappliedContent = new VBoxContainer
        {
            Alignment =
                BoxContainer.AlignmentMode.Center,
            CustomMinimumSize =
                new Vector2(320.0f, 0.0f),
            SizeFlagsHorizontal =
                Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical =
                Control.SizeFlags.ShrinkCenter
        };
        _unappliedContent.AddThemeConstantOverride(
            "separation",
            12);
        centre.AddChild(
            _unappliedContent);
        Label eyebrow = new()
        {
            Text = "SETTINGS",
            HorizontalAlignment =
                HorizontalAlignment.Center,
            SizeFlagsHorizontal =
                Control.SizeFlags.ExpandFill,
            Modulate =
                new Color(
                    0.46f,
                    0.82f,
                    0.92f)
        };
        eyebrow.AddThemeFontSizeOverride(
            "font_size",
            11);
        Label title = new()
        {
            Text = "UNAPPLIED CHANGES",
            HorizontalAlignment =
                HorizontalAlignment.Center,
            SizeFlagsHorizontal =
                Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride(
            "font_size",
            30);
        Label detail = new()
        {
            Text =
                "Apply your changes before leaving settings?",
            Modulate =
                new Color(
                    0.72f,
                    0.76f,
                    0.82f),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            SizeFlagsHorizontal =
                Control.SizeFlags.ExpandFill,
            AutowrapMode =
                TextServer.AutowrapMode.WordSmart
        };
        detail.AddThemeFontSizeOverride(
            "font_size",
            14);
        _unappliedContent.AddChild(eyebrow);
        _unappliedContent.AddChild(title);
        _unappliedContent.AddChild(detail);
        _unappliedContent.AddChild(
            new Control
            {
                CustomMinimumSize =
                    new Vector2(1.0f, 12.0f)
            });
        _unappliedApplyButton =
            CreateMinimalDialogButton(
                "APPLY");
        _unappliedDiscardButton =
            CreateMinimalDialogButton(
                "DISCARD");
        _unappliedKeepButton =
            CreateMinimalDialogButton(
                "KEEP EDITING");
        _unappliedApplyButton.Pressed +=
            ApplyAndCloseFromConfirmation;
        _unappliedDiscardButton.Pressed +=
            DiscardAndCloseFromConfirmation;
        _unappliedKeepButton.Pressed +=
            KeepEditingFromConfirmation;
        _unappliedContent.AddChild(
            _unappliedApplyButton);
        _unappliedContent.AddChild(
            _unappliedDiscardButton);
        _unappliedContent.AddChild(
            _unappliedKeepButton);
        ConfigureFocusLoop(
            new List<Control>
            {
                _unappliedApplyButton,
                _unappliedDiscardButton,
                _unappliedKeepButton
            });
        _unappliedPanel.Hide();
    }

    private void ShowUnappliedChangesDialog()
    {
        _settingsCard.Hide();
        _unappliedPanel.Show();
        UpdateMenuLayout();
        _confirmationTween?.Kill();
        _unappliedPanel.PivotOffset =
            _unappliedPanel.Size * 0.5f;
        _unappliedPanel.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        _unappliedPanel.Scale =
            new Vector2(0.975f, 0.96f);
        _confirmationTween =
            CreateTween()
                .SetParallel(true);
        _confirmationTween.SetTrans(
            Tween.TransitionType.Cubic);
        _confirmationTween.SetEase(
            Tween.EaseType.Out);
        _confirmationTween.TweenProperty(
            _unappliedPanel,
            "modulate",
            Colors.White,
            0.2f);
        _confirmationTween.TweenProperty(
            _unappliedPanel,
            "scale",
            Vector2.One,
            0.26f);
        Callable.From(
                _unappliedApplyButton
                    .GrabFocus)
            .CallDeferred();
    }

    private void ApplyAndCloseFromConfirmation()
    {
        _unappliedPanel.Hide();
        _settingsCard.Show();
        ApplyCurrentSelection();
        _settingsOpenSignature =
            CaptureSettingsSignature();
        CloseMenuWithoutConfirmation();
    }

    private void DiscardAndCloseFromConfirmation()
    {
        _unappliedPanel.Hide();
        _settingsCard.Show();
        LoadSettings();
        ApplySettings(false);
        _settingsOpenSignature =
            CaptureSettingsSignature();
        CloseMenuWithoutConfirmation();
    }

    private void KeepEditingFromConfirmation()
    {
        _unappliedPanel.Hide();
        _settingsCard.Show();
        Callable.From(
                FocusCurrentTabControl)
            .CallDeferred();
    }

    private static Button
        CreateMinimalDialogButton(
            string text)
    {
        Button button = new()
        {
            Text = text,
            Alignment =
                HorizontalAlignment.Center,
            FocusMode =
                FocusModeEnum.All,
            SizeFlagsHorizontal =
                Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize =
                new Vector2(240.0f, 44.0f)
        };
        button.AddThemeFontSizeOverride(
            "font_size",
            14);
        button.AddThemeStyleboxOverride(
            "normal",
            MenuButtonStyle(
                0.0f,
                0.0f));
        button.AddThemeStyleboxOverride(
            "hover",
            MenuButtonStyle(
                0.12f,
                0.72f));
        button.AddThemeStyleboxOverride(
            "pressed",
            MenuButtonStyle(
                0.18f,
                1.0f,
                true));
        button.AddThemeStyleboxOverride(
            "focus",
            MenuButtonStyle(
                0.12f,
                1.0f,
                true));
        return button;
    }

    private void CloseMenuWithoutConfirmation()
    {
        _closeWithoutConfirmation = true;
        CloseMenu();
        _closeWithoutConfirmation = false;
    }

    private bool HasUnappliedChanges()
    {
        return _settingsOpenSignature !=
            CaptureSettingsSignature();
    }

    private string CaptureSettingsSignature()
    {
        System.Text.StringBuilder signature =
            new();
        signature.Append(_modeOption.Selected)
            .Append('|')
            .Append(_aspectOption.Selected)
            .Append('|')
            .Append(_resolutionOption.Selected)
            .Append('|')
            .Append(_vsyncCheck.ButtonPressed)
            .Append('|')
            .Append(_fpsOption.Selected)
            .Append('|')
            .Append(
                Mathf.RoundToInt(
                    _masterVolumeSlider.Value *
                    10.0))
            .Append('|')
            .Append(
                Mathf.RoundToInt(
                    _musicVolumeSlider.Value *
                    10.0))
            .Append('|')
            .Append(_enemyTrajectoryOption.Selected)
            .Append('|')
            .Append(_survivalAssistOption.Selected)
            .Append('|')
            .Append(_stompAssistOption.Selected)
            .Append('|')
            .Append(_graphicsPresetOption.Selected)
            .Append('|')
            .Append(_graphicsRenderScaleOption.Selected)
            .Append('|')
            .Append(_graphicsShadowsOption.Selected)
            .Append('|')
            .Append(_graphicsAntiAliasingOption.Selected)
            .Append('|')
            .Append(_graphicsEffectsOption.Selected)
            .Append('|')
            .Append(
                Mathf.RoundToInt(
                    _uiScaleSlider.Value))
            .Append('|')
            .Append(
                Mathf.RoundToInt(
                    _textScaleSlider.Value))
            .Append('|')
            .Append(
                Mathf.RoundToInt(
                    _vitalsScaleSlider.Value));
        foreach (string action in
            RemappableActions)
        {
            signature.Append('|')
                .Append(action)
                .Append('=')
                .Append(
                    _pendingBindings
                        .GetValueOrDefault(
                            action,
                            Key.None));
        }
        return signature.ToString();
    }

    private void ToggleMenu()
    {
        if (_overlay.Visible)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    private void OpenMenu()
    {
        // GUI hit testing follows sibling order, independently of draw Z.
        // The run overlay used to cover this panel with an invisible input surface.
        Show();
        GetParent().MoveChild(this, -1);
        ZIndex = 600;
        _menuTween?.Kill();
        _closingMenu = false;
        _settingsOpenSignature =
            CaptureSettingsSignature();
        _mobileControls?.SetSettingsMenuOpen(true);
        _overlay.Show();
        _openButton.Hide();
        _settingsCard.PivotOffset =
            _settingsCard.Size * 0.5f;
        _settingsCard.Modulate =
            new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _settingsCard.Scale = Vector2.One;
        _settingsShade.Modulate =
            new Color(1.0f, 1.0f, 1.0f, 0.0f);

        PrepareMenuRevealEdges(false);
        _menuTween = CreateTween();
        _menuTween.SetParallel(true);
        _menuTween.SetTrans(
            Tween.TransitionType.Quart);
        _menuTween.SetEase(
            Tween.EaseType.Out);
        _menuTween.TweenProperty(
            _settingsCard,
            "modulate",
            Colors.White,
            0.42f);
        _menuTween.TweenProperty(
            _settingsShade,
            "modulate",
            Colors.White,
            0.42f);

        AnimateMenuRevealEdges(
            _menuTween,
            false,
            0.42f);
        _menuTween.Chain().TweenCallback(
            Callable.From(
                () =>
                {
                    HideMenuRevealEdges();
                }));
        UpdateInputPresentation();
        Callable.From(FocusCurrentTabControl).CallDeferred();

        if (!GetTree().Paused)
        {
            GetTree().Paused = true;
            _pausedByMenu = true;
        }
    }

    private void CloseMenu()
    {
        if (_closingMenu ||
            !_overlay.Visible)
        {
            return;
        }

        if (!_closeWithoutConfirmation &&
            HasUnappliedChanges())
        {
            ShowUnappliedChangesDialog();
            return;
        }

        if (_listeningAction != null)
        {
            CancelBinding();
        }

        _closingMenu = true;
        _menuTween?.Kill();
        PrepareMenuRevealEdges(true);
        _menuTween = CreateTween();
        _menuTween.SetParallel(true);
        _menuTween.SetTrans(
            Tween.TransitionType.Quad);
        _menuTween.SetEase(
            Tween.EaseType.In);
        _menuTween.TweenProperty(
            _settingsCard,
            "modulate",
            new Color(1.0f, 1.0f, 1.0f, 0.0f),
            0.3f);
        _menuTween.TweenProperty(
            _settingsShade,
            "modulate",
            new Color(1.0f, 1.0f, 1.0f, 0.0f),
            0.3f);

        AnimateMenuRevealEdges(
            _menuTween,
            true,
            0.3f);
        _menuTween.Chain().TweenCallback(
            Callable.From(FinishCloseMenu));
    }

    private void FinishCloseMenu()
    {
        _overlay.Hide();
        _settingsCard.Modulate = Colors.White;
        _settingsCard.Scale = Vector2.One;
        _settingsShade.Modulate = Colors.White;
        HideMenuRevealEdges();

        _closingMenu = false;
        if (_usesExternalLauncher)
        {
            _openButton.Hide();
        }
        else
        {
            _openButton.Show();
        }
        _mobileControls?.SetSettingsMenuOpen(false);
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();

        if (_pausedByMenu)
        {
            GetTree().Paused = false;
            _pausedByMenu = false;
        }
        MenuClosed?.Invoke();
    }

    private void UpdateInputPresentation()
    {
        if (_isMobileLayout)
        {
            _openButton.Text = "SETTINGS";
            return;
        }

        bool gamepad = GamepadInput.ShouldShowGamepadPrompts;
        _openButton.Text = gamepad
            ? "SETTINGS  START"
            : "SETTINGS  ESC";
        _settingsSubtitle.Text = gamepad
            ? "LB/RB TABS  ·  A CONFIRM  ·  B BACK"
            : "SELECT A CATEGORY  ·  CHANGES ARE PREVIEWED LIVE";
        if (_listeningAction == null &&
            _controlsTab.IsVisibleInTree())
        {
            _controlsHint.Text = gamepad
                ? "STICK / D-PAD MOVE  •  A/CROSS JUMP  •  RB/R1 RIFT STEP  •  RT/R2 ANALOG THRUST  •  X/SQUARE DEVICE SKILL"
                : "CLICK A BINDING, THEN PRESS A KEY";
        }
    }

    private void CycleVisibleTab(int direction)
    {
        int tabCount = _tabs.GetTabCount();
        if (tabCount <= 1)
        {
            return;
        }

        int nextTab = _tabs.CurrentTab;
        for (int attempt = 0; attempt < tabCount; attempt++)
        {
            nextTab =
                (nextTab + direction + tabCount) % tabCount;
            if (!_tabs.IsTabHidden(nextTab))
            {
                _tabs.CurrentTab = nextTab;
                Callable.From(FocusCurrentTabControl)
                    .CallDeferred();
                return;
            }
        }
    }

    private void OnTabChanged(long tabIndex)
    {
        UpdateInputPresentation();
        Control tab =
            _tabs.GetCurrentTabControl();
        if (IsInstanceValid(tab))
        {
            if (_tabScrolls.TryGetValue(
                tab,
                out ScrollContainer scroll))
            {
                scroll.ScrollVertical = 0;
            }
            _tabTween?.Kill();
            tab.Modulate =
                new Color(
                    1.0f,
                    1.0f,
                    1.0f,
                    0.34f);
            _tabTween = CreateTween();
            _tabTween.SetTrans(
                Tween.TransitionType.Cubic);
            _tabTween.SetEase(
                Tween.EaseType.Out);
            _tabTween.TweenProperty(
                tab,
                "modulate",
                Colors.White,
                0.24f);
        }
        Callable.From(FocusCurrentTabControl)
            .CallDeferred();
    }

    private void FocusCurrentTabControl()
    {
        if (!_overlay.Visible)
        {
            return;
        }

        Control tab = _tabs.GetCurrentTabControl();
        List<Control> focusOrder = new();
        CollectFocusableControls(
            tab,
            focusOrder);
        if (IsInstanceValid(_quitButton) &&
            _quitButton.Visible)
        {
            focusOrder.Add(_quitButton);
        }
        focusOrder.Add(_defaultsButton);
        focusOrder.Add(_closeButton);
        focusOrder.Add(_applyButton);
        ConfigureFocusLoop(focusOrder);
        if (focusOrder.Count > 0)
        {
            focusOrder[0].GrabFocus();
        }
    }

    private static void CollectFocusableControls(
        Node root,
        List<Control> controls)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Control control &&
                control.IsVisibleInTree() &&
                control.FocusMode != FocusModeEnum.None &&
                (control is not BaseButton button ||
                    !button.Disabled))
            {
                controls.Add(control);
            }

            CollectFocusableControls(
                child,
                controls);
        }
    }

    private static void ConfigureFocusLoop(
        List<Control> controls)
    {
        for (int index = 0;
            index < controls.Count;
            index++)
        {
            Control current = controls[index];
            Control previous = controls[
                (index - 1 + controls.Count) %
                controls.Count];
            Control next = controls[
                (index + 1) %
                controls.Count];
            current.FocusNeighborTop =
                current.GetPathTo(previous);
            current.FocusNeighborBottom =
                current.GetPathTo(next);
            current.FocusPrevious =
                current.GetPathTo(previous);
            current.FocusNext =
                current.GetPathTo(next);
            if (current is HSlider)
            {
                continue;
            }

            current.FocusNeighborLeft =
                current.GetPathTo(previous);
            current.FocusNeighborRight =
                current.GetPathTo(next);
        }
    }

    private static void ApplyUnifiedMenuStyle(
        Node root)
    {
        if (root is PanelContainer panel)
        {
            panel.AddThemeStyleboxOverride(
                "panel",
                MenuSurfaceStyle());
        }

        if (root is TabContainer tabs)
        {
            tabs.AddThemeStyleboxOverride(
                "panel",
                MenuInsetStyle());
            tabs.AddThemeStyleboxOverride(
                "tab_selected",
                MenuTabStyle(
                    0.0f,
                    0.9f));
            tabs.AddThemeStyleboxOverride(
                "tab_unselected",
                MenuTabStyle(
                    0.0f,
                    0.0f));
            tabs.AddThemeStyleboxOverride(
                "tab_hovered",
                MenuTabStyle(
                    0.025f,
                    0.0f));
            tabs.AddThemeStyleboxOverride(
                "tab_disabled",
                MenuTabStyle(
                    0.0f,
                    0.0f));
            tabs.AddThemeColorOverride(
                "font_selected_color",
                new Color(
                    0.96f,
                    0.98f,
                    1.0f));
            tabs.AddThemeColorOverride(
                "font_unselected_color",
                new Color(
                    0.68f,
                    0.72f,
                    0.76f,
                    0.82f));
            tabs.AddThemeColorOverride(
                "font_hovered_color",
                new Color(
                    0.78f,
                    0.9f,
                    0.94f));
        }

        if (root is OptionButton optionButton)
        {
            ApplyChoiceControlStyle(optionButton);
        }
        else if (root is CheckButton checkButton)
        {
            ApplyChoiceControlStyle(checkButton);
            ApplyGradientToggleStyle(checkButton);
        }
        else if (root is Button button)
        {
            button.AddThemeStyleboxOverride(
                "normal",
                MenuButtonStyle(
                    0.0f,
                    0.0f));
            button.AddThemeStyleboxOverride(
                "hover",
                MenuButtonStyle(
                    0.12f,
                    0.54f));
            button.AddThemeStyleboxOverride(
                "pressed",
                MenuButtonStyle(
                    0.18f,
                    0.9f));
            button.AddThemeStyleboxOverride(
                "focus",
                MenuButtonStyle(
                    0.12f,
                    1.0f,
                    true));
        }

        if (root is HSlider slider)
        {
            ApplyGradientSliderStyle(slider);
        }

        if (root is HSeparator separator)
        {
            separator.AddThemeStyleboxOverride(
                "separator",
                new StyleBoxFlat
                {
                    BgColor = Colors.Transparent,
                    BorderColor =
                        new Color(
                            0.5f,
                            0.76f,
                            0.84f,
                            0.14f),
                    BorderWidthBottom = 1
                });
        }

        foreach (Node child in root.GetChildren())
        {
            ApplyUnifiedMenuStyle(child);
        }
    }

    private static StyleBoxFlat MenuSurfaceStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(.7f, .8f, .86f, .18f),
            BorderWidthTop = 0,
            BorderWidthBottom = 0,
            ContentMarginLeft = 4.0f,
            ContentMarginRight = 4.0f
        };
    }

    private static StyleBoxFlat MenuInsetStyle()
    {
        return new StyleBoxFlat
        {
            BgColor =
                Colors.Transparent,
            ContentMarginLeft = 8.0f,
            ContentMarginTop = 6.0f,
            ContentMarginRight = 8.0f,
            ContentMarginBottom = 6.0f
        };
    }

    private static StyleBoxFlat MenuTabStyle(
        float backgroundAlpha,
        float borderAlpha)
    {
        return new StyleBoxFlat
        {
            BgColor =
                new Color(
                    0.72f,
                    0.86f,
                    0.9f,
                    backgroundAlpha),
            BorderColor =
                new Color(
                    0.94f,
                    0.67f,
                    0.40f,
                    borderAlpha),
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusTopRight =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusBottomLeft =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusBottomRight =
                UiVisualStyle
                    .UnifiedCornerRadius,
            ContentMarginLeft = 14.0f,
            ContentMarginRight = 14.0f,
            ContentMarginTop = 6.0f,
            ContentMarginBottom = 6.0f
        };
    }

    private static StyleBoxFlat MenuButtonStyle(
        float backgroundAlpha,
        float borderAlpha,
        bool focus = false)
    {
        return new StyleBoxFlat
        {
            BgColor =
                new Color(
                    0.12f,
                    0.2f,
                    0.24f,
                    backgroundAlpha *
                        0.34f),
            BorderColor =
                Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusTopRight =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusBottomLeft =
                UiVisualStyle
                    .UnifiedCornerRadius,
            CornerRadiusBottomRight =
                UiVisualStyle
                    .UnifiedCornerRadius,
            ContentMarginLeft = 9.0f,
            ContentMarginRight = 9.0f,
            ContentMarginTop = 5.0f,
            ContentMarginBottom = 5.0f
        };
    }

    private static void ApplyChoiceControlStyle(
        Button choice)
    {
        choice.Alignment =
            HorizontalAlignment.Left;
        choice.ClipText = true;
        choice.AddThemeConstantOverride(
            "h_separation",
            8);
        choice.AddThemeStyleboxOverride(
            "normal",
            MenuChoiceStyle(0.025f));
        choice.AddThemeStyleboxOverride(
            "hover",
            MenuChoiceStyle(0.085f));
        choice.AddThemeStyleboxOverride(
            "pressed",
            MenuChoiceStyle(0.12f));
        choice.AddThemeStyleboxOverride(
            "focus",
            MenuChoiceStyle(0.07f));
        choice.AddThemeStyleboxOverride(
            "disabled",
            MenuChoiceStyle(0.015f));
    }

    private static StyleBoxFlat MenuChoiceStyle(
        float backgroundAlpha)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(
                0.42f,
                0.72f,
                0.8f,
                backgroundAlpha),
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft =
                UiVisualStyle.UnifiedCornerRadius,
            CornerRadiusTopRight =
                UiVisualStyle.UnifiedCornerRadius,
            CornerRadiusBottomLeft =
                UiVisualStyle.UnifiedCornerRadius,
            CornerRadiusBottomRight =
                UiVisualStyle.UnifiedCornerRadius,
            ContentMarginLeft = 10.0f,
            ContentMarginRight = 10.0f,
            ContentMarginTop = 5.0f,
            ContentMarginBottom = 5.0f
        };
    }

    private static void ApplyGradientSliderStyle(
        HSlider slider)
    {
        SettingsSliderVisual visual =
            slider.GetNodeOrNull<SettingsSliderVisual>(
                "GradientSliderVisual");
        if (!IsInstanceValid(visual))
        {
            visual = new SettingsSliderVisual();
            slider.AddChild(visual);
        }
        visual.Configure(slider);
    }

    private static void ApplyGradientToggleStyle(
        CheckButton toggle)
    {
        SettingsToggleVisual visual =
            toggle.GetNodeOrNull<SettingsToggleVisual>(
                "GradientToggleVisual");
        if (!IsInstanceValid(visual))
        {
            visual = new SettingsToggleVisual();
            toggle.AddChild(visual);
        }
        visual.Configure(toggle);
    }



    private void BuildMenuRevealEdges()
    {
        _menuRevealTop = CreateMenuRevealEdge(
            "MenuRevealTop");
        _menuRevealBottom = CreateMenuRevealEdge(
            "MenuRevealBottom");
        _overlay.AddChild(_menuRevealTop);
        _overlay.AddChild(_menuRevealBottom);
    }

    private static ColorRect CreateMenuRevealEdge(
        string name)
    {
        return new ColorRect
        {
            Name = name,
            Visible = false,
            Color = new Color(
                1.0f,
                0.58f,
                0.18f,
                0.44f),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 4
        };
    }

    private void PrepareMenuRevealEdges(
        bool closing)
    {
        if (!IsInstanceValid(_menuRevealTop) ||
            !IsInstanceValid(_menuRevealBottom))
        {
            return;
        }

        float left = _settingsCard.Position.X + 8.0f;
        float width = Mathf.Max(
            24.0f,
            _settingsCard.Size.X - 16.0f);
        float top = _settingsCard.Position.Y + 1.0f;
        float bottom =
            _settingsCard.Position.Y +
            _settingsCard.Size.Y - 2.0f;
        float centre = (top + bottom) * 0.5f;
        _menuRevealTop.Position = new Vector2(
            left,
            closing ? top : centre);
        _menuRevealBottom.Position = new Vector2(
            left,
            closing ? bottom : centre);
        _menuRevealTop.Size = new Vector2(width, 1.0f);
        _menuRevealBottom.Size = new Vector2(width, 1.0f);
        float startingAlpha = closing
            ? 0.58f
            : 0.42f;
        _menuRevealTop.Modulate = new Color(
            1.0f,
            1.0f,
            1.0f,
            startingAlpha);
        _menuRevealBottom.Modulate = new Color(
            1.0f,
            1.0f,
            1.0f,
            startingAlpha);
        _menuRevealTop.Show();
        _menuRevealBottom.Show();
    }

    private void AnimateMenuRevealEdges(
        Tween tween,
        bool closing,
        float duration)
    {
        if (!IsInstanceValid(_menuRevealTop) ||
            !IsInstanceValid(_menuRevealBottom))
        {
            return;
        }

        float top = _settingsCard.Position.Y + 1.0f;
        float bottom =
            _settingsCard.Position.Y +
            _settingsCard.Size.Y - 2.0f;
        float centre = (top + bottom) * 0.5f;
        tween.TweenProperty(
            _menuRevealTop,
            "position:y",
            closing ? centre : top,
            duration);
        tween.TweenProperty(
            _menuRevealBottom,
            "position:y",
            closing ? centre : bottom,
            duration);
        tween.TweenProperty(
            _menuRevealTop,
            "modulate:a",
            0.0f,
            duration);
        tween.TweenProperty(
            _menuRevealBottom,
            "modulate:a",
            0.0f,
            duration);
    }

    private void HideMenuRevealEdges()
    {
        _menuRevealTop?.Hide();
        _menuRevealBottom?.Hide();
    }


}
