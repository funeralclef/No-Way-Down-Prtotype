using Godot;
using System.Collections.Generic;

public partial class Refuge : Node3D
{
    private enum HubStation
    {
        Wardrobe,
        Shelter,
        Loadout,
        Descent
    }

    private sealed class StationView
    {
        public string Title;
        public string Eyebrow;
        public string Description;
        public Vector3 CameraPosition;
        public Vector3 LookTarget;
        public float Fov;
    }

    private const string RunScenePath = "res://main.tscn";

    private readonly StationView[] _stations =
    {
        new()
        {
            Title = "WARDROBE",
            Eyebrow = "IDENTITY",
            Description =
                "Choose the silhouette and light signature carried into every descent.",
            CameraPosition = new Vector3(-6.9f, 3.55f, 8.2f),
            LookTarget = new Vector3(-8.4f, 2.35f, -2.2f),
            Fov = 48.0f
        },
        new()
        {
            Title = "THE REFUGE",
            Eyebrow = "HOME",
            Description =
                "Shape the light and atmosphere waiting between runs.",
            CameraPosition = new Vector3(0.0f, 4.75f, 11.4f),
            LookTarget = new Vector3(0.0f, 1.55f, -2.15f),
            Fov = 54.0f
        },
        new()
        {
            Title = "LOADOUT",
            Eyebrow = "PREPARATION",
            Description =
                "Explore the buffs and plan an evolution for your next run.",
            CameraPosition = new Vector3(6.9f, 3.55f, 8.2f),
            LookTarget = new Vector3(8.35f, 1.4f, -2.2f),
            Fov = 48.0f
        },
        new()
        {
            Title = "THE DESCENT",
            Eyebrow = "NO WAY DOWN",
            Description =
                "Cross the threshold. Return stronger.",
            CameraPosition = new Vector3(0.0f, 3.35f, 3.2f),
            LookTarget = new Vector3(0.0f, 2.0f, -11.85f),
            Fov = 50.0f
        }
    };

    private readonly List<MeshInstance3D> _accentMeshes = new();
    private readonly List<OmniLight3D> _accentLights = new();
    private readonly List<Node3D> _dustMotes = new();
    private readonly List<Vector3> _dustOrigins = new();
    private readonly List<Node3D> _portalRings = new();
    private readonly List<Button> _stationButtons = new();
    private readonly List<Node3D> _refugeStyleModules = new();
    private static readonly Vector3[] StationWorldAnchors =
    {
        new(-8.4f, 2.65f, -2.2f),
        new(0.0f, 2.4f, -2.15f),
        new(8.35f, 2.45f, -2.2f),
        new(0.0f, 3.15f, -11.85f)
    };

    private CustomizationProfile _profile;
    private Camera3D _camera;
    private Environment _environment;
    private StandardMaterial3D _floorMaterial;
    private StandardMaterial3D _wallMaterial;
    private StandardMaterial3D _accentMaterial;
    private StandardMaterial3D _routeMaterial;
    private StandardMaterial3D _portalMaterial;
    private Player _mannequin;
    private Node3D _mannequinTurntable;
    private Node3D _mannequinSpinPivot;
    private MusicPlayer _musicPlayer;
    private UiAccessibility _uiAccessibility;
    private RefugeInterface _refugeInterface;
    private PanelContainer _stationPanel;
    private ScrollContainer _stationScroll;
    private MarginContainer _stationPanelMargin;
    private VBoxContainer _stationContent;
    private Label _stationEyebrow;
    private Label _stationTitle;
    private Label _stationDescription;
    private Label _selectionName;
    private Label _selectionDetail;
    private Button _settingsButton;
    private Button _uiToggleButton;
    private Button _infoButton;
    private GameInfoPanel _infoPanel;
    private RunBuildPanel _buffArchive;
    private StandardMaterial3D _windowMaterial;
    private StandardMaterial3D _softLightMaterial;
    private StandardMaterial3D _glassMaterial;
    private StandardMaterial3D _dustMaterial;
    private Button _previousButton;
    private Button _primaryButton;
    private Button _nextButton;
    private Button _mobilePreviousStationButton;
    private Button _mobileNextStationButton;
    private ColorRect _fade;
    private DisplaySettings _fullSettings;
    private ColorRect _leftFocusVeil;
    private Label3D _worldBrandLabel;
    private Label3D _worldStatsLabel;
    private HubStation _currentStation = HubStation.Shelter;
    private int _previewSkin;
    private int _previewRefugeStyle;
    private Vector3 _cameraLookTarget;
    private float _cinematicTime;
    private float _cameraTransitionPhase;
    private bool _transitioning;
    private bool _hubUiCollapsed;
    private bool _hubUiFading;
    private bool _hubProjectionReady;
    private bool _pointerDragging;
    private bool _rotatingMannequin;
    private int _touchDragIndex = -1;
    private int _panelTouchDragIndex = -1;
    private Vector2 _dragStart;
    private Vector2 _dragTravel;
    private Vector2 _panelDragTravel;
    private Vector2 _lastViewportSize;
    private float _mannequinTargetYaw;
    private int _stickAxisLock;
    private int _stickDirection;
    private float _stickRepeatTimer;
    private float _hubUiOpacity = 1;
    private readonly List<Label3D> _hubWorldLabels = new();

    public override void _Ready()
    {
        GamepadInput.EnsureUiMappings();
        _profile =
            GetNodeOrNull<CustomizationProfile>(
                "/root/CustomizationProfile");
        _uiAccessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _musicPlayer =
            GetNodeOrNull<MusicPlayer>(
                "/root/MusicPlayer");
        _musicPlayer?.EnterRefuge();

        _previewSkin = _profile?.SelectedSkin ?? 0;
        _previewRefugeStyle =
            _profile?.SelectedRefugeStyle ?? 0;
        BuildWorld();
        _camera.GlobalPosition =
            _stations[(int)_currentStation].CameraPosition;
        _cameraLookTarget =
            GetComposedLookTarget(
                _stations[(int)_currentStation]);
        _camera.LookAt(_cameraLookTarget, Vector3.Up);
        BindInterface();
        foreach (Node node in FindChildren("*", "Label3D", true, false))
            if (node is Label3D label) _hubWorldLabels.Add(label);
        AttachFullSettings();
        _hubProjectionReady = true;
        UpdateResponsiveLayout();
        ApplyRefugeStyle(_previewRefugeStyle);
        RefreshStation(true);
        if (_profile != null)
        {
            _profile.ProfileChanged += OnProfileChanged;
        }
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged +=
                OnInterfaceScaleChanged;
        }

        _fade.Color =
            new Color(0.005f, 0.009f, 0.018f, 1.0f);
        Tween reveal = CreateTween();
        reveal.TweenProperty(
            _fade,
            "color:a",
            0.0f,
            0.85f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    public override void _ExitTree()
    {
        if (_profile != null)
        {
            _profile.ProfileChanged -= OnProfileChanged;
        }
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged -=
                OnInterfaceScaleChanged;
        }

    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        _cinematicTime += deltaSeconds;
        _hubUiOpacity = Mathf.MoveToward(_hubUiOpacity, _hubUiCollapsed ? 0 : 1, deltaSeconds / .24f);
        _hubUiFading = !Mathf.IsEqualApprox(_hubUiOpacity, _hubUiCollapsed ? 0 : 1);
        if (_infoPanel?.IsOpen != true && _buffArchive?.IsOpen != true) UpdateHubStickNavigation(deltaSeconds);
        UpdateCamera(deltaSeconds);
        UpdateProjectedHubInterface();
        UpdateAmbientMotion(deltaSeconds);
        ApplyHubVisibility();
        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        if (!_lastViewportSize.IsEqualApprox(
            viewportSize))
        {
            UpdateResponsiveLayout();
        }
    }

    public override void _Input(
        InputEvent inputEvent)
    {
        if (!touchLayout() ||
            _transitioning ||
            _fullSettings?.IsMenuOpen == true ||
            (_infoPanel?.IsOpen == true || _buffArchive?.IsOpen == true))
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventScreenTouch touch:
                HandleScreenTouch(touch);
                break;
            case InputEventScreenDrag drag
                when drag.Index == _touchDragIndex:
                if (_rotatingMannequin)
                {
                    RotateMannequin(
                        drag.Relative.X);
                }
                else
                {
                    _dragTravel += drag.Relative;
                }
                break;
        }
    }

    public override void _UnhandledInput(
        InputEvent inputEvent)
    {
        GamepadInput.Observe(inputEvent);
        if (_transitioning)
        {
            return;
        }

        if (_fullSettings.IsMenuOpen || (_infoPanel?.IsOpen == true || _buffArchive?.IsOpen == true))
        {
            return;
        }

        if (inputEvent is InputEventKey
            {
                Pressed: true,
                Echo: false,
                Keycode: Key.H
            })
        {
            ToggleHubUi();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent is InputEventJoypadButton
            {
                Pressed: true
            } joypadButton &&
            HandleHubJoypadButton(joypadButton))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
        {
            OpenHubInfo(); GetViewport().SetInputAsHandled(); return;
        }
        if (_hubUiCollapsed)
        {
            if (inputEvent.IsActionPressed("ui_cancel")) { ToggleHubUi(); GetViewport().SetInputAsHandled(); }
            return;
        }

        if (inputEvent is InputEventJoypadMotion)
        {
            return;
        }

        if (inputEvent.IsActionPressed("ui_left"))
        {
            MoveStation(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("ui_right"))
        {
            MoveStation(1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("ui_up"))
        {
            ChangeSelection(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("ui_down"))
        {
            ChangeSelection(1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("ui_accept"))
        {
            ActivateStation();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("pause"))
        {
            OpenSettings();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            ChangeStation(HubStation.Shelter);
            GetViewport().SetInputAsHandled();
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseButton mouse:
                HandleMouseButton(mouse);
                break;
            case InputEventMouseMotion motion
                when _rotatingMannequin:
                RotateMannequin(
                    motion.Relative.X);
                break;
            case InputEventMouseMotion motion
                when _pointerDragging:
                _dragTravel += motion.Relative;
                break;
        }
    }

    private void BuildWorld()
    {
        _floorMaterial = CreateMaterial(
            new Color(0.58f, 0.64f, 0.66f),
            0.82f,
            0.0f);
        _wallMaterial = CreateMaterial(
            new Color(0.72f, 0.76f, 0.77f),
            0.78f,
            0.0f);
        _accentMaterial = CreateMaterial(
            new Color(0.94f, 0.24f, 0.08f),
            0.56f,
            0.22f);
        _routeMaterial = CreateMaterial(
            new Color(
                0.06f,
                0.58f,
                0.7f),
            0.64f,
            0.14f);
        _portalMaterial = CreateMaterial(
            new Color(0.94f, 0.28f, 0.08f),
            0.42f,
            0.58f);

        _environment =
            GetNode<WorldEnvironment>(
                "WorldEnvironment")
            .Environment;

        AddBox(
            "Floor",
            new Vector3(0.0f, -0.28f, 0.0f),
            new Vector3(32.0f, 0.55f, 30.0f),
            _floorMaterial);
        AddBox(
            "BackWall",
            new Vector3(0.0f, 4.0f, -13.2f),
            new Vector3(32.0f, 8.0f, 0.6f),
            _wallMaterial);
        AddBox(
            "LeftWall",
            new Vector3(-15.7f, 3.75f, 0.0f),
            new Vector3(0.6f, 7.5f, 30.0f),
            _wallMaterial);
        AddBox(
            "RightWall",
            new Vector3(15.7f, 3.75f, 0.0f),
            new Vector3(0.6f, 7.5f, 30.0f),
            _wallMaterial);
        AddBox(
            "Ceiling",
            new Vector3(0.0f, 7.55f, 0.0f),
            new Vector3(32.0f, 0.35f, 30.0f),
            _wallMaterial);

        for (int rib = 0; rib < 9; rib++)
        {
            float z = Mathf.Lerp(-11.8f, 12.8f, rib / 8.0f);
            MeshInstance3D beam = AddBox(
                $"CeilingRib{rib + 1}",
                new Vector3(0.0f, 7.12f, z),
                new Vector3(31.2f, 0.18f, 0.22f),
                _accentMaterial);
            _accentMeshes.Add(beam);
        }

        CreateLocationArchitecture();
        CreateWindow();
        CreateRefugeSignage();
        CreateWardrobe();
        CreateHearth();
        CreateLoadout();
        CreateDescentGate();
        CreateRefugeModules();
        CreateDust();

        _camera =
            GetNode<Camera3D>(
                "CinematicCamera");
    }

    private void CreateLocationArchitecture()
    {
        // A calm continuation of the run route crosses the refuge and points
        // directly at the descent gate. It ties the hub to the procedural
        // locations without turning the safe room into another arena.
        foreach (float laneX in
            new[] { -5.4f, 5.4f })
        {
            AddBox(
                laneX < 0.0f
                    ? "RefugeRouteLeft"
                    : "RefugeRouteRight",
                new Vector3(
                    laneX,
                    0.025f,
                    0.0f),
                new Vector3(
                    0.055f,
                    0.035f,
                    27.4f),
                _routeMaterial);
        }

        for (int beat = 0;
            beat < 7;
            beat++)
        {
            float z =
                Mathf.Lerp(
                    -11.2f,
                    11.2f,
                    beat / 6.0f);
            AddBox(
                $"RefugeBeat{beat + 1}",
                new Vector3(
                    0.0f,
                    0.03f,
                    z),
                new Vector3(
                    beat % 3 == 0
                        ? 10.8f
                        : 5.2f,
                    0.04f,
                    0.065f),
                _routeMaterial);
        }

        for (int sideIndex = 0;
            sideIndex < 2;
            sideIndex++)
        {
            float side =
                sideIndex == 0
                    ? -1.0f
                    : 1.0f;
            AddBox(
                $"RefugeEdgeRail{sideIndex + 1}",
                new Vector3(
                    side * 15.22f,
                    0.18f,
                    0.0f),
                new Vector3(
                    0.1f,
                    0.22f,
                    27.6f),
                _accentMaterial);
            for (int pylon = 0;
                pylon < 4;
                pylon++)
            {
                float z =
                    -10.0f +
                    pylon * 6.65f;
                AddBox(
                    $"RoutePylon{sideIndex + 1}_{pylon + 1}",
                    new Vector3(
                        side * 14.95f,
                        2.05f,
                        z),
                    new Vector3(
                        0.48f,
                        4.1f,
                        0.7f),
                    _wallMaterial);
                AddBox(
                    $"PylonSignal{sideIndex + 1}_{pylon + 1}",
                    new Vector3(
                        side * 14.64f,
                        2.3f,
                        z),
                    new Vector3(
                        0.055f,
                        2.25f,
                        0.18f),
                    _routeMaterial);
            }
        }

        StandardMaterial3D paleLight =
            CreateMaterial(
                new Color(
                    0.82f,
                    0.95f,
                    1.0f),
                0.5f,
                0.26f);
        _softLightMaterial = paleLight;
        for (int lightIndex = 0;
            lightIndex < 3;
            lightIndex++)
        {
            AddBox(
                $"RefugeSoftLight{lightIndex + 1}",
                new Vector3(
                    -8.2f +
                        lightIndex *
                        8.2f,
                    7.32f,
                    -0.8f),
                new Vector3(
                    3.8f,
                    0.045f,
                    0.7f),
                paleLight);
        }
    }

    private void CreateRefugeSignage()
    {
        _worldBrandLabel = CreateWorldLabel(
            "NO WAY DOWN  //  REFUGE 07",
            new Vector3(0.0f, 6.7f, -12.78f),
            34);
        _worldBrandLabel.PixelSize = 0.009f;
        _worldBrandLabel.Modulate =
            new Color(0.62f, 0.92f, 1.0f);
        AddChild(_worldBrandLabel);

        _worldStatsLabel = CreateWorldLabel(
            "",
            new Vector3(0.0f, 6.05f, -12.76f),
            18);
        _worldStatsLabel.PixelSize = 0.008f;
        _worldStatsLabel.Modulate =
            new Color(0.48f, 0.7f, 0.82f);
        AddChild(_worldStatsLabel);

        AddBox(
            "RefugeSignRail",
            new Vector3(0.0f, 5.7f, -12.84f),
            new Vector3(10.5f, 0.045f, 0.08f),
            _accentMaterial);
    }

    private void CreateWindow()
    {
        StandardMaterial3D skyMaterial =
            CreateMaterial(
                new Color(0.24f, 0.62f, 0.82f),
                0.5f,
                0.18f);
        _windowMaterial = skyMaterial;
        AddBox(
            "SkyWindow",
            new Vector3(0.0f, 4.5f, -13.52f),
            new Vector3(11.8f, 4.1f, 0.08f),
            skyMaterial);
        for (int column = -2; column <= 2; column++)
        {
            AddBox(
                $"WindowFrame{column + 3}",
                new Vector3(
                    column * 2.92f,
                    4.5f,
                    -13.4f),
                new Vector3(0.11f, 4.25f, 0.18f),
                _wallMaterial);
        }
        AddBox(
            "WindowHorizon",
            new Vector3(0.0f, 4.0f, -13.38f),
            new Vector3(12.0f, 0.12f, 0.18f),
            _accentMaterial);
    }

    private void CreateWardrobe()
    {
        Node3D station = new()
        {
            Name = "WardrobeStation"
        };
        AddChild(station);
        AddStationPlatform(
            station,
            new Vector3(-8.4f, 0.12f, -2.2f),
            "WARDROBE");
        AddBoxTo(
            station,
            "WardrobeBack",
            new Vector3(-8.4f, 2.45f, -3.3f),
            new Vector3(5.1f, 4.9f, 0.35f),
            _wallMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            MeshInstance3D strip = AddBoxTo(
                station,
                $"WardrobeLight{side}",
                new Vector3(
                    -8.4f + side * 2.05f,
                    2.4f,
                    -3.03f),
                new Vector3(0.12f, 4.1f, 0.12f),
                _accentMaterial);
            _accentMeshes.Add(strip);
        }

        PackedScene playerScene =
            GD.Load<PackedScene>("res://player.tscn");
        _mannequinTurntable = new Node3D
        {
            Name = "PreviewTurntable",
            Position =
                new Vector3(
                    -8.4f,
                    0.65f,
                    -2.37f)
        };
        station.AddChild(_mannequinTurntable);
        _mannequinSpinPivot = new Node3D
        {
            Name = "PreviewSpinPivot"
        };
        _mannequinTurntable.AddChild(
            _mannequinSpinPivot);
        _mannequin =
            playerScene.Instantiate<Player>();
        _mannequin.Name = "PlayerPreview";
        _mannequin.Position = Vector3.Zero;
        _mannequin.RotationDegrees = new Vector3(0, 180, 0);
        _mannequin.Scale =
            Vector3.One * 1.34f;
        _mannequin.CollisionLayer = 0;
        _mannequin.CollisionMask = 0;
        _mannequinSpinPivot.AddChild(
            _mannequin);
        _mannequin.SetPhysicsProcess(false);
        _mannequin.SetProcess(false);
        AnimationPlayer previewAnimation =
            _mannequin.GetNodeOrNull<AnimationPlayer>(
                "AnimationPlayer");
        if (previewAnimation != null)
        {
            previewAnimation.Play("RESET");
            previewAnimation.Seek(0.0, true);
            previewAnimation.Pause();
        }
        Node3D previewArt =
            _mannequin.GetNodeOrNull<Node3D>(
                "Pivot/Character");
        if (previewArt != null)
        {
            // Recenter from the actual imported art transform. This
            // remains correct when the model or its rest pose changes.
            Vector3 artOffset =
                _mannequinSpinPivot.ToLocal(
                    previewArt.GlobalPosition);
            _mannequin.Position -=
                new Vector3(
                    artOffset.X,
                    0.0f,
                    artOffset.Z);
        }
        GpuParticles3D trail =
            _mannequin.GetNodeOrNull<GpuParticles3D>(
                "MomentumTrail");
        if (trail != null)
        {
            trail.Emitting = false;
        }
        _profile?.ApplySelectedSkin(_mannequin);
    }

    private void CreateHearth()
    {
        Node3D station = new()
        {
            Name = "HearthStation"
        };
        AddChild(station);
        AddStationPlatform(
            station,
            new Vector3(0.0f, 0.12f, -2.15f),
            "REFUGE CORE");
        for (int ring = 0; ring < 3; ring++)
        {
            TorusMesh torus = new()
            {
                InnerRadius = 0.64f + ring * 0.31f,
                OuterRadius = 0.76f + ring * 0.31f,
                Rings = 32,
                RingSegments = 8
            };
            torus.SurfaceSetMaterial(0, _accentMaterial);
            MeshInstance3D mesh = new()
            {
                Mesh = torus,
                Position =
                    new Vector3(0.0f, 0.5f + ring * 0.18f, -2.15f),
                RotationDegrees =
                    new Vector3(90.0f, 0.0f, 0.0f)
            };
            station.AddChild(mesh);
            _accentMeshes.Add(mesh);
        }
        OmniLight3D hearthLight = new()
        {
            Position = new Vector3(0.0f, 1.6f, -2.15f),
            OmniRange = 8.0f,
            LightEnergy = 4.2f,
            ShadowEnabled = true
        };
        station.AddChild(hearthLight);
        _accentLights.Add(hearthLight);
    }

    private void CreateLoadout()
    {
        Node3D station = new()
        {
            Name = "LoadoutStation"
        };
        AddChild(station);
        AddStationPlatform(
            station,
            new Vector3(8.35f, 0.12f, -2.2f),
            "LOADOUT");
        string[] slotNames =
        {
            "CORE",
            "TRAIL",
            "RELIC"
        };
        for (int slot = 0; slot < slotNames.Length; slot++)
        {
            float x = 6.6f + slot * 1.75f;
            AddBoxTo(
                station,
                $"Pedestal{slot + 1}",
                new Vector3(x, 0.55f, -2.4f),
                new Vector3(1.25f, 1.1f, 1.25f),
                _wallMaterial);
            Mesh hologramMesh = slot switch
            {
                0 => new SphereMesh
                {
                    Radius = 0.38f,
                    Height = 0.76f,
                    RadialSegments = 16,
                    Rings = 8
                },
                1 => new CylinderMesh
                {
                    TopRadius = 0.18f,
                    BottomRadius = 0.42f,
                    Height = 0.85f,
                    RadialSegments = 14
                },
                _ => new BoxMesh
                {
                    Size = Vector3.One * 0.65f
                }
            };
            hologramMesh.SurfaceSetMaterial(
                0,
                _accentMaterial);
            MeshInstance3D hologram = new()
            {
                Name = $"Hologram{slot + 1}",
                Mesh = hologramMesh,
                Position = new Vector3(x, 1.65f, -2.4f)
            };
            station.AddChild(hologram);
            _accentMeshes.Add(hologram);
            Label3D label = CreateWorldLabel(
                slotNames[slot],
                new Vector3(x, 0.22f, -1.67f),
                19);
            station.AddChild(label);
        }
    }

    private void CreateDescentGate()
    {
        Node3D gate = new()
        {
            Name = "DescentGate",
            Position = new Vector3(0.0f, 0.0f, -12.45f)
        };
        AddChild(gate);
        AddBoxTo(
            gate,
            "GateLeft",
            new Vector3(-2.55f, 2.8f, 0.0f),
            new Vector3(1.1f, 5.6f, 0.8f),
            _wallMaterial);
        AddBoxTo(
            gate,
            "GateRight",
            new Vector3(2.55f, 2.8f, 0.0f),
            new Vector3(1.1f, 5.6f, 0.8f),
            _wallMaterial);
        AddBoxTo(
            gate,
            "GateTop",
            new Vector3(0.0f, 5.25f, 0.0f),
            new Vector3(6.2f, 0.7f, 0.8f),
            _wallMaterial);
        for (int ring = 0; ring < 3; ring++)
        {
            TorusMesh torus = new()
            {
                InnerRadius = 1.7f + ring * 0.22f,
                OuterRadius = 1.83f + ring * 0.22f,
                Rings = 32,
                RingSegments = 8
            };
            torus.SurfaceSetMaterial(0, _portalMaterial);
            MeshInstance3D portalRing = new()
            {
                Mesh = torus,
                Position =
                    new Vector3(0.0f, 2.65f, 0.46f + ring * 0.05f),
                RotationDegrees =
                    new Vector3(90.0f, 0.0f, 0.0f)
            };
            gate.AddChild(portalRing);
            _portalRings.Add(portalRing);
        }
        Label3D warning = CreateWorldLabel(
            "DESCENT READY",
            new Vector3(0.0f, 5.85f, 0.2f),
            25);
        gate.AddChild(warning);
        OmniLight3D portalLight = new()
        {
            Position = new Vector3(0.0f, 2.6f, 1.2f),
            OmniRange = 10.0f,
            LightEnergy = 5.0f
        };
        gate.AddChild(portalLight);
        _accentLights.Add(portalLight);
    }

    private void CreateRefugeModules()
    {
        StandardMaterial3D glassMaterial =
            CreateMaterial(
                new Color(0.08f, 0.54f, 0.8f, 0.34f),
                0.18f,
                1.25f);
        _glassMaterial = glassMaterial;
        glassMaterial.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;
        StandardMaterial3D plantMaterial =
            CreateMaterial(
                new Color(0.08f, 0.48f, 0.2f),
                0.82f,
                0.08f);

        Node3D ember = CreateStyleModule(
            "EmberWorkshop");
        for (int panel = 0; panel < 5; panel++)
        {
            float x = -13.2f + panel * 1.25f;
            AddBoxTo(
                ember,
                $"WorkshopWallRib{panel}",
                new Vector3(x, 3.25f, -12.82f),
                new Vector3(0.16f, 5.5f, 0.22f),
                _accentMaterial);
        }
        CreateBench(
            ember,
            "WorkshopBench",
            new Vector3(-12.2f, 0.72f, 3.6f),
            4.2f);
        AddBoxTo(
            ember,
            "FieldLockerA",
            new Vector3(-13.6f, 0.75f, 0.5f),
            new Vector3(1.3f, 1.5f, 1.25f),
            _wallMaterial);
        AddBoxTo(
            ember,
            "FieldLockerB",
            new Vector3(-12.15f, 0.5f, 0.65f),
            new Vector3(1.1f, 1.0f, 1.0f),
            _floorMaterial);

        Node3D tide = CreateStyleModule(
            "TideglassLounge");
        for (int fin = 0; fin < 6; fin++)
        {
            float x = 7.3f + fin * 1.25f;
            MeshInstance3D wallFin = AddBoxTo(
                tide,
                $"GlassWallFin{fin}",
                new Vector3(x, 3.45f, -12.78f),
                new Vector3(0.82f, 5.1f, 0.16f),
                glassMaterial);
            wallFin.RotationDegrees =
                new Vector3(0.0f, 0.0f, fin % 2 == 0 ? 5.0f : -5.0f);
        }
        CreateBench(
            tide,
            "ConsoleLounge",
            new Vector3(12.0f, 0.62f, 3.6f),
            4.8f);
        AddBoxTo(
            tide,
            "FloatingConsole",
            new Vector3(11.8f, 1.65f, 2.95f),
            new Vector3(3.1f, 0.12f, 0.95f),
            glassMaterial);

        Node3D verdant = CreateStyleModule(
            "VerdantRecovery");
        for (int planter = 0; planter < 4; planter++)
        {
            float z = -6.8f + planter * 2.65f;
            AddBoxTo(
                verdant,
                $"Planter{planter}",
                new Vector3(-14.65f, 0.55f, z),
                new Vector3(1.45f, 1.1f, 1.75f),
                _floorMaterial);
            AddCylinderTo(
                verdant,
                $"PlantStem{planter}",
                new Vector3(-14.65f, 1.65f, z),
                0.24f,
                2.1f,
                plantMaterial);
            AddCylinderTo(
                verdant,
                $"PlantCrown{planter}",
                new Vector3(-14.65f, 2.75f, z),
                0.72f,
                0.35f,
                plantMaterial);
        }
        for (int frame = 0; frame < 4; frame++)
        {
            AddBoxTo(
                verdant,
                $"LivingWallFrame{frame}",
                new Vector3(
                    -12.8f + frame * 1.65f,
                    4.0f,
                    -12.82f),
                new Vector3(1.3f, 4.6f, 0.2f),
                plantMaterial);
        }
        CreateBench(
            verdant,
            "RecoverySeat",
            new Vector3(-10.8f, 0.58f, 5.2f),
            5.0f);

        Node3D neon = CreateStyleModule(
            "NeonProjectionSuite");
        for (int segment = 0; segment < 7; segment++)
        {
            AddBoxTo(
                neon,
                $"SegmentedWall{segment}",
                new Vector3(
                    7.0f + segment * 1.15f,
                    2.0f + segment % 2 * 1.65f,
                    -12.8f),
                new Vector3(0.86f, 1.35f, 0.24f),
                _accentMaterial);
        }
        CreateBench(
            neon,
            "ModularSeatLeft",
            new Vector3(10.0f, 0.58f, 5.0f),
            3.2f);
        CreateBench(
            neon,
            "ModularSeatRight",
            new Vector3(13.1f, 0.58f, 3.4f),
            2.5f);
        AddCylinderTo(
            neon,
            "ProjectionTable",
            new Vector3(11.7f, 0.72f, 1.8f),
            1.15f,
            0.18f,
            _accentMaterial);
        AddCylinderTo(
            neon,
            "ProjectionColumn",
            new Vector3(11.7f, 0.36f, 1.8f),
            0.24f,
            0.7f,
            _wallMaterial);
    }

    private Node3D CreateStyleModule(
        string moduleName)
    {
        Node3D module = new()
        {
            Name = moduleName,
            Visible = false
        };
        AddChild(module);
        _refugeStyleModules.Add(module);
        return module;
    }

    private void CreateBench(
        Node parent,
        string name,
        Vector3 position,
        float width)
    {
        AddBoxTo(
            parent,
            name,
            position,
            new Vector3(width, 0.32f, 1.2f),
            _floorMaterial);
        AddBoxTo(
            parent,
            $"{name}Back",
            position +
                new Vector3(0.0f, 0.65f, 0.52f),
            new Vector3(width, 1.05f, 0.22f),
            _wallMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            AddBoxTo(
                parent,
                $"{name}Leg{side}",
                position +
                    new Vector3(
                        side * width * 0.38f,
                        -0.35f,
                        0.0f),
                new Vector3(0.24f, 0.7f, 0.82f),
                _wallMaterial);
        }
    }

    private void CreateDust()
    {
        StandardMaterial3D dustMaterial =
            CreateMaterial(
                new Color(0.6f, 0.82f, 1.0f),
                0.25f,
                2.5f);
        _dustMaterial = dustMaterial;
        for (int mote = 0; mote < 24; mote++)
        {
            SphereMesh sphere = new()
            {
                Radius = 0.025f,
                Height = 0.05f,
                RadialSegments = 6,
                Rings = 3
            };
            sphere.SurfaceSetMaterial(
                0,
                dustMaterial);
            Vector3 origin = new(
                (float)GD.RandRange(-10.0f, 10.0f),
                (float)GD.RandRange(0.4f, 6.8f),
                (float)GD.RandRange(-7.4f, 7.5f));
            MeshInstance3D dust = new()
            {
                Mesh = sphere,
                Position = origin,
                CastShadow =
                    GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(dust);
            _dustMotes.Add(dust);
            _dustOrigins.Add(origin);
        }
    }

    private void BindInterface()
    {
        _refugeInterface =
            GetNode<RefugeInterface>(
                "Interface/Root");
        if (!_refugeInterface.HasValidContract())
        {
            throw new System.InvalidOperationException(
                "Refuge interface scene contract is incomplete.");
        }

        _leftFocusVeil =
            _refugeInterface.FocusVeil;
        _stationPanel =
            _refugeInterface.StationPanel;
        _stationScroll =
            _refugeInterface.StationScroll;
        _stationPanelMargin =
            _refugeInterface.StationMargin;
        _stationContent =
            _refugeInterface.StationContent;
        _stationEyebrow =
            _refugeInterface.StationEyebrow;
        _stationTitle =
            _refugeInterface.StationTitle;
        _stationDescription =
            _refugeInterface.StationDescription;
        _selectionName =
            _refugeInterface.SelectionName;
        _selectionDetail =
            _refugeInterface.SelectionDetail;
        _previousButton =
            _refugeInterface.PreviousButton;
        _primaryButton =
            _refugeInterface.PrimaryButton;
        _nextButton =
            _refugeInterface.NextButton;
        _settingsButton =
            _refugeInterface.SettingsButton;
        _uiToggleButton =
            _refugeInterface.ToggleInterfaceButton;
        _mobilePreviousStationButton =
            _refugeInterface.MobilePreviousButton;
        _mobileNextStationButton =
            _refugeInterface.MobileNextButton;
        _fade = _refugeInterface.Fade;

        _stationButtons.Clear();
        _stationButtons.AddRange(
            _refugeInterface.StationButtons);

        _stationScroll.GuiInput +=
            HandleStationScrollGuiInput;
        _previousButton.Pressed +=
            SelectPrevious;
        _primaryButton.Pressed +=
            ActivateStation;
        _nextButton.Pressed +=
            SelectNext;
        _stationButtons[0].Pressed += () =>
            ChangeStation(HubStation.Wardrobe);
        _stationButtons[1].Pressed += () =>
            ChangeStation(HubStation.Shelter);
        _stationButtons[2].Pressed += () =>
            ChangeStation(HubStation.Loadout);
        _stationButtons[3].Pressed += () =>
            ChangeStation(HubStation.Descent);
        _infoPanel = new GameInfoPanel { Name = "Info", RefugeContext = true };
        _refugeInterface.AddChild(_infoPanel);
        _infoButton = _refugeInterface.InfoButton;
        _infoButton.Pressed += OpenHubInfo;
        _buffArchive = new RunBuildPanel { Name = "BuffArchive" };
        _refugeInterface.AddChild(_buffArchive);
        _buffArchive.Initialize(new RunProgression(), null, archive: true);
        _settingsButton.Pressed +=
            OpenSettings;
        _uiToggleButton.Pressed +=
            ToggleHubUi;
        _mobilePreviousStationButton.Pressed +=
            () => NavigateFromMobileArrow(-1);
        _mobileNextStationButton.Pressed +=
            () => NavigateFromMobileArrow(1);
    }

    private void AttachFullSettings()
    {
        _fullSettings =
            DisplaySettings.CreateFullPanel();
        _refugeInterface.SettingsHost.AddChild(
            _fullSettings);
        _fullSettings.UseExternalLauncher();
    }

    private void RefreshStation(bool instant = false)
    {
        StationView station =
            _stations[(int)_currentStation];
        _selectionName.Modulate = Colors.White;
        _stationEyebrow.Text = station.Eyebrow;
        _stationTitle.Text = station.Title;
        _stationDescription.Text =
            station.Description;
        SetStationPanelLayout();
        for (int index = 0;
            index < _stationButtons.Count;
            index++)
        {
            bool selected =
                index == (int)_currentStation;
            _stationButtons[index].Disabled = false;
            _stationButtons[index].Modulate =
                selected
                    ? new Color(0.52f, 0.94f, 1.0f)
                    : new Color(0.78f, 0.82f, 0.88f);
        }

        switch (_currentStation)
        {
            case HubStation.Wardrobe:
                RefreshWardrobe();
                break;
            case HubStation.Shelter:
                RefreshShelterStyle();
                break;
            case HubStation.Loadout:
                _selectionName.Text =
                    "IMPULSE  •  DEVICE  •  EVOLUTION";
                _selectionDetail.Text =
                    "8 DEVICES  •  8 EVOLUTIONS  •  8 CATALYSTS";
                SetActionButtons(
                    false,
                    "EXPLORE BUFFS",
                    false);
                _primaryButton.Disabled = false;
                break;
            default:
                _selectionName.Text =
                    "RUN SYSTEMS READY";
                _selectionDetail.Text =
                    "APPEARANCE SAVED  •  ROUTE READY";
                SetActionButtons(
                    false,
                    "START RUN",
                    false);
                _primaryButton.Disabled = false;
                break;
        }

        RefreshStats();
    }

    private void RefreshWardrobe()
    {
        string name =
            CustomizationProfile.GetSkinName(
                _previewSkin);
        bool unlocked =
            _profile?.IsSkinUnlocked(
                _previewSkin) ?? _previewSkin == 0;
        bool equipped =
            _profile?.SelectedSkin ==
            _previewSkin;
        _selectionName.Text =
            unlocked ? name : $"{name}  •  LOCKED";
        _selectionName.Modulate =
            CustomizationProfile.GetSkinColor(
                _previewSkin);
        if (unlocked && touchLayout())
        {
            _selectionDetail.Text = equipped
                ? "EQUIPPED  •  BROWSE  ‹  ›  •  DRAG TO ROTATE"
                : "AVAILABLE  •  BROWSE  ‹  ›  •  DRAG TO ROTATE";
        }
        else
        {
            _selectionDetail.Text = unlocked
                ? equipped
                    ? "EQUIPPED  •  BROWSE APPEARANCES  •  ROTATE MODEL"
                    : "AVAILABLE  •  BROWSE APPEARANCES  •  ROTATE MODEL"
                : _profile?.GetUnlockProgressText(
                    _previewSkin) ?? "LOCKED";
        }
        SetActionButtons(
            true,
            equipped
                ? "EQUIPPED"
                : unlocked
                    ? "EQUIP"
                    : "LOCKED",
            true);
        _primaryButton.Disabled =
            !unlocked || equipped;
        _mannequin?.ApplySkin(
            _previewSkin,
            CustomizationProfile.GetSkinColor(
                _previewSkin),
            CustomizationProfile
                .GetSkinSecondaryColor(
                    _previewSkin),
            CustomizationProfile
                .GetSkinAccentColor(
                    _previewSkin));
    }

    private void RefreshShelterStyle()
    {
        bool active =
            _profile?.SelectedRefugeStyle ==
            _previewRefugeStyle;
        Color styleColor =
            CustomizationProfile.GetRefugeStyleColor(
                _previewRefugeStyle);
        _selectionName.Text =
            CustomizationProfile.GetRefugeStyleName(
                _previewRefugeStyle);
        _selectionName.Modulate = styleColor;
        _selectionDetail.Text = active
            ? "ACTIVE HOME  •  " +
                CustomizationProfile.GetRefugeStyleDetail(
                    _previewRefugeStyle)
            : "PREVIEWING  •  " +
                CustomizationProfile.GetRefugeStyleDetail(
                    _previewRefugeStyle);
        SetActionButtons(
            true,
            active ? "ACTIVE" : "MAKE HOME",
            true);
        _primaryButton.Disabled = active;
        ApplyRefugeStyle(_previewRefugeStyle);
    }

    private void SetActionButtons(
        bool showPrevious,
        string primaryText,
        bool showNext)
    {
        _previousButton.Visible = showPrevious;
        _nextButton.Visible = showNext;
        _primaryButton.Text = primaryText;
        _primaryButton.Disabled =
            primaryText.Contains("OFFLINE");
    }

    private void SetStationPanelLayout()
    {
        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        bool touch =
            _uiAccessibility?.IsTouchLayout ??
            (OS.HasFeature("mobile") ||
                DisplayServer.IsTouchscreenAvailable());
        Vector4 safe =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layout =
            GetHubLayoutScale(
                viewportSize,
                safe);
        float safeWidth = Mathf.Max(
            280.0f,
            viewportSize.X -
                safe.X -
                safe.Z);
        float safeHeight = Mathf.Max(
            220.0f,
            viewportSize.Y -
            safe.Y -
            safe.W);
        bool portrait =
            viewportSize.Y >
            viewportSize.X;
        float leadingReserve =
            touch
                ? 74.0f * layout
                : 24.0f * layout;
        float panelRegion =
            safeWidth *
                (portrait ? 0.62f : 0.49f) -
            leadingReserve;
        float desiredWidth =
            (touch ? 620.0f : 560.0f) *
            layout;
        float panelWidth = Mathf.Max(
            250.0f,
            Mathf.Min(
                desiredWidth,
                panelRegion));
        float systemReserve =
            (touch ? 68.0f : 58.0f) *
            layout;
        float desiredHeight =
            (touch ? 390.0f : 340.0f) *
            layout;
        float panelHeight = Mathf.Max(
            220.0f,
            Mathf.Min(
                desiredHeight,
                safeHeight -
                    systemReserve -
                    24.0f * layout));

        _stationPanel.AnchorLeft = 0.0f;
        _stationPanel.AnchorRight = 0.0f;
        _stationPanel.AnchorTop = 0.0f;
        _stationPanel.AnchorBottom = 0.0f;
        _stationPanel.OffsetLeft = 0.0f;
        _stationPanel.OffsetTop = 0.0f;
        _stationPanel.OffsetRight = panelWidth;
        _stationPanel.OffsetBottom = panelHeight;
    }

    private void UpdateResponsiveLayout()
    {
        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        _lastViewportSize = viewportSize;
        bool touch =
            _uiAccessibility?.IsTouchLayout ??
            (OS.HasFeature("mobile") ||
                DisplayServer.IsTouchscreenAvailable());
        Vector4 safe =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layout =
            GetHubLayoutScale(
                viewportSize,
                safe);
        float textScale =
            GetHubTextScale(
                layout);
        float safeWidth =
            viewportSize.X -
            safe.X -
            safe.Z;
        bool compact = viewportSize.X < 760.0f;
        SetMargins(
            _stationPanelMargin,
            touch ? 26 : 34,
            touch ? 24 : 28,
            touch ? 26 : 38,
            touch ? 26 : 30);
        _stationContent.AddThemeConstantOverride(
            "separation",
            touch ? 10 : 11);
        foreach (Button stationButton in
            _stationButtons)
        {
            stationButton.CustomMinimumSize =
                new Vector2(
                    (touch ? 132.0f : 112.0f) *
                        layout,
                    (touch ? 48.0f : 42.0f) *
                        layout);
            stationButton.AddThemeFontSizeOverride(
                "font_size",
                Mathf.Max(
                    9,
                    Mathf.RoundToInt(
                        (touch
                            ? 16.0f
                            : compact ? 9.0f : 10.0f) *
                        textScale)));
        }

        _settingsButton.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 24.0f : 19.0f) *
                textScale));
        _infoButton.CustomMinimumSize = new Vector2((touch ? 52 : 42) * layout, (touch ? 44 : 38) * layout);
        _settingsButton.CustomMinimumSize =
            new Vector2(
                (touch ? 52.0f : 42.0f) *
                    layout,
                (touch ? 44.0f : 38.0f) *
                    layout);
        _uiToggleButton.CustomMinimumSize =
            new Vector2(
                (touch ? 58.0f : 46.0f) *
                    layout,
                (touch ? 44.0f : 38.0f) *
                    layout);
        _uiToggleButton.AddThemeFontSizeOverride(
            "font_size",
            Mathf.RoundToInt(
                (touch ? 22.0f : 18.0f) *
                textScale));
        _mobilePreviousStationButton
            .AddThemeFontSizeOverride(
                "font_size",
                Mathf.RoundToInt(
                    30.0f * textScale));
        _mobileNextStationButton
            .AddThemeFontSizeOverride(
                "font_size",
                Mathf.RoundToInt(
                    30.0f * textScale));
        ApplyHubTypography(
            textScale,
            layout,
            touch);
        SetStationPanelLayout();
        UpdateProjectedHubInterface();
    }

    private void ApplyHubTypography(
        float textScale,
        float layoutScale,
        bool touch)
    {
        SetHubFont(
            _stationEyebrow,
            touch ? 16 : 12,
            textScale);
        SetHubFont(
            _stationTitle,
            touch ? 38 : 36,
            textScale);
        SetHubFont(
            _stationDescription,
            touch ? 18 : 14,
            textScale);
        SetHubFont(
            _selectionName,
            touch ? 23 : 20,
            textScale);
        SetHubFont(
            _selectionDetail,
            touch ? 17 : 13,
            textScale);
        SetHubFont(
            _previousButton,
            touch ? 24 : 19,
            textScale);
        SetHubFont(
            _primaryButton,
            touch ? 17 : 13,
            textScale);
        SetHubFont(
            _nextButton,
            touch ? 24 : 19,
            textScale);
        float actionHeight =
            (touch ? 48.0f : 40.0f) *
            layoutScale;
        _previousButton.CustomMinimumSize =
            new Vector2(
                48.0f * layoutScale,
                actionHeight);
        _primaryButton.CustomMinimumSize =
            new Vector2(
                130.0f * layoutScale,
                actionHeight);
        _nextButton.CustomMinimumSize =
            new Vector2(
                48.0f * layoutScale,
                actionHeight);
    }

    private void UpdateProjectedHubInterface()
    {
        if (!IsInstanceValid(_camera) ||
            !IsInstanceValid(_stationPanel) ||
            !IsInstanceValid(_refugeInterface) ||
            !_hubProjectionReady)
        {
            return;
        }

        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        Vector4 safe =
            _uiAccessibility?.GetSafeInsets(
                viewportSize) ??
            Vector4.Zero;
        float layout =
            GetHubLayoutScale(
                viewportSize,
                safe);
        float margin = 12.0f * layout;
        bool touch = touchLayout();

        for (int index = 0;
            index < _stationButtons.Count;
            index++)
        {
            Button button = _stationButtons[index];
            bool behind =
                _camera.IsPositionBehind(
                    StationWorldAnchors[index]);
            Vector2 projected = behind
                ? new Vector2(-1000.0f, -1000.0f)
                : _camera.UnprojectPosition(
                    StationWorldAnchors[index]);
            float width =
                (index == (int)_currentStation
                    ? 124.0f
                    : 104.0f) *
                layout;
            float height =
                (index == (int)_currentStation
                    ? 42.0f
                    : 34.0f) *
                layout;
            bool onScreen =
                !behind &&
                projected.X >=
                    safe.X - width &&
                projected.X <=
                    viewportSize.X -
                    safe.Z + width &&
                projected.Y >=
                    safe.Y - height &&
                projected.Y <=
                    viewportSize.Y -
                    safe.W + height;
            button.Visible =
                index != (int)_currentStation &&
                !touch &&
                (_hubUiOpacity > .002f) &&
                onScreen;
            PlaceProjectedControl(
                button,
                projected.X -
                    width * 0.5f,
                projected.Y +
                    34.0f * layout,
                width,
                height);
            if (index != (int)_currentStation)
            {
                button.Modulate =
                    new Color(
                        0.55f,
                        0.72f,
                        0.8f,
                        0.66f);
            }
        }

        float panelWidth =
            Mathf.Max(
                220.0f,
                _stationPanel.OffsetRight -
                    _stationPanel.OffsetLeft);
        float panelHeight =
            Mathf.Max(
                150.0f,
                _stationPanel.OffsetBottom -
                    _stationPanel.OffsetTop);
        float panelLeading =
            (touch ? 72.0f : 24.0f) *
            layout;
        float panelX =
            safe.X +
            panelLeading;
        float systemReserve =
            (touch ? 58.0f : 50.0f) *
            layout;
        float panelAreaTop =
            safe.Y +
            systemReserve +
            margin;
        float panelAreaBottom =
            viewportSize.Y -
            safe.W -
            margin;
        float panelAreaHeight =
            Mathf.Max(
                panelHeight,
                panelAreaBottom -
                    panelAreaTop);
        float panelY =
            panelAreaTop +
            Mathf.Max(
                0.0f,
                (panelAreaHeight -
                    panelHeight) *
                    0.48f);
        panelX = Mathf.Clamp(
            panelX,
            safe.X + margin,
            viewportSize.X -
                safe.Z -
                panelWidth -
                margin);
        panelY = Mathf.Clamp(
            panelY,
            panelAreaTop,
            viewportSize.Y -
                safe.W -
                panelHeight -
                margin);
        PlaceProjectedControl(
            _stationPanel,
            panelX,
            panelY,
            panelWidth,
            panelHeight);
        _stationPanel.PivotOffset =
            new Vector2(
                panelWidth * 0.5f,
                panelHeight * 0.5f);
        _stationPanel.Rotation =
            0.0f;
        _stationPanel.Visible =
            _hubUiOpacity > .002f && !_transitioning;

        bool showMobileNavigation =
            touch &&
            (_hubUiOpacity > .002f) &&
            !_transitioning &&
            !_fullSettings.IsMenuOpen;
        float arrowWidth =
            58.0f * layout;
        float arrowHeight =
            76.0f * layout;
        float arrowY =
            Mathf.Clamp(
                safe.Y +
                    (viewportSize.Y -
                        safe.Y -
                        safe.W) *
                    0.5f -
                    arrowHeight * 0.5f,
                safe.Y +
                    6.0f * layout,
                viewportSize.Y -
                    safe.W -
                    arrowHeight -
                    6.0f * layout);
        float leftArrowX =
            safe.X +
            8.0f * layout;
        float rightArrowX =
            viewportSize.X -
            safe.Z -
            arrowWidth -
            8.0f * layout;
        PlaceProjectedControl(
            _mobilePreviousStationButton,
            leftArrowX,
            arrowY,
            arrowWidth,
            arrowHeight);
        PlaceProjectedControl(
            _mobileNextStationButton,
            rightArrowX,
            arrowY,
            arrowWidth,
            arrowHeight);
        SetMobileNavigationVisible(
            showMobileNavigation);

        _settingsButton.Visible =
            _hubUiOpacity > .002f && !_transitioning;
        _uiToggleButton.Show();

        float settingsWidth =
            (touchLayout()
                ? 52.0f
                : 42.0f) *
            layout;
        float toggleWidth =
            (touchLayout()
                ? 58.0f
                : 46.0f) *
            layout;
        float systemHeight =
            (touchLayout()
                ? 44.0f
                : 38.0f) *
            layout;
        float systemY = Mathf.Max(
            safe.Y + margin,
            safe.Y + 8.0f * layout);
        PlaceProjectedControl(
            _settingsButton,
            viewportSize.X -
                safe.Z -
                margin -
                settingsWidth,
            systemY,
            settingsWidth,
            systemHeight);
        PlaceProjectedControl(
            _uiToggleButton,
            viewportSize.X -
                safe.Z -
                margin -
                settingsWidth -
                toggleWidth -
                7.0f * layout,
            systemY,
            toggleWidth,
            systemHeight);
        _infoButton.Visible = _hubUiOpacity > .002f && !_transitioning;
        PlaceProjectedControl(_infoButton,
            viewportSize.X - safe.Z - margin - settingsWidth * 2 - toggleWidth - 14 * layout,
            systemY, settingsWidth, systemHeight);
        Rect2 panelRect = new(
            panelX,
            panelY,
            panelWidth,
            panelHeight);
        for (int index = 0;
            index < _stationButtons.Count;
            index++)
        {
            if (index != (int)_currentStation &&
                panelRect.Intersects(
                    _stationButtons[index].GetRect()))
            {
                _stationButtons[index].Hide();
            }
        }
    }

    private bool touchLayout()
    {
        return _uiAccessibility?.IsTouchLayout ??
            (OS.HasFeature("mobile") ||
                DisplayServer.IsTouchscreenAvailable());
    }

    private float GetHubLayoutScale(
        Vector2 viewportSize,
        Vector4 safeInsets)
    {
        float requested =
            _uiAccessibility?.LayoutScale ??
            1.0f;
        float safeHeight =
            Mathf.Max(
                1.0f,
                viewportSize.Y -
                    safeInsets.Y -
                    safeInsets.W);
        float screenCapacity =
            Mathf.Clamp(
                safeHeight / 620.0f,
                0.82f,
                1.75f);
        return Mathf.Min(
            requested,
            screenCapacity);
    }

    private float GetHubTextScale(
        float layoutScale)
    {
        float requested =
            _uiAccessibility
                ?.EffectiveTextScale ??
            1.0f;
        return Mathf.Min(
            requested,
            Mathf.Max(
                0.9f,
                layoutScale * 1.06f));
    }

    private static void PlaceProjectedControl(
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

    private static void SetHubFont(
        Control control,
        int baseSize,
        float scale)
    {
        control.AddThemeFontSizeOverride(
            "font_size",
            Mathf.Max(
                9,
                Mathf.RoundToInt(
                    baseSize * scale)));
    }

    private void OnInterfaceScaleChanged(
        float uiScale,
        float textScale)
    {
        UpdateResponsiveLayout();
    }

    private void UpdateHubStickNavigation(
        float deltaSeconds)
    {
        if (_transitioning || _hubUiCollapsed ||
            _fullSettings.IsMenuOpen)
        {
            ResetStickNavigation();
            return;
        }

        Godot.Collections.Array<int> devices =
            Input.GetConnectedJoypads();
        if (devices.Count == 0)
        {
            ResetStickNavigation();
            return;
        }

        int device =
            GamepadInput.ActiveDevice >= 0 &&
            devices.Contains(
                GamepadInput.ActiveDevice)
                ? GamepadInput.ActiveDevice
                : devices[0];
        Vector2 stick = new(
            Input.GetJoyAxis(
                device,
                JoyAxis.LeftX),
            Input.GetJoyAxis(
                device,
                JoyAxis.LeftY));
        float rightX = Input.GetJoyAxis(
            device,
            JoyAxis.RightX);
        if (_currentStation ==
                HubStation.Wardrobe &&
            Mathf.Abs(rightX) > 0.12f)
        {
            _mannequinTargetYaw -=
                rightX *
                Mathf.DegToRad(115.0f) *
                deltaSeconds;
        }

        float horizontal =
            Mathf.Abs(stick.X);
        const float engage = 0.58f;
        const float release = 0.28f;
        if (_stickAxisLock == 0)
        {
            if (horizontal < engage)
            {
                return;
            }

            _stickAxisLock = 1;
            _stickDirection =
                Mathf.Sign(stick.X) >= 0.0f
                    ? 1
                    : -1;
            TriggerStickAction();
            _stickRepeatTimer = 0.48f;
            return;
        }

        if (horizontal < release)
        {
            ResetStickNavigation();
            return;
        }

        _stickRepeatTimer -= deltaSeconds;
        if (_stickRepeatTimer <= 0.0f)
        {
            TriggerStickAction();
            _stickRepeatTimer = 0.24f;
        }
    }

    private void TriggerStickAction()
    {
        MoveStation(_stickDirection);
    }

    private void ResetStickNavigation()
    {
        _stickAxisLock = 0;
        _stickDirection = 0;
        _stickRepeatTimer = 0.0f;
    }

    private bool HandleHubJoypadButton(
        InputEventJoypadButton button)
    {
        if (_hubUiCollapsed && button.ButtonIndex is not JoyButton.X and not JoyButton.Y) return false;
        switch (button.ButtonIndex)
        {
            case JoyButton.DpadLeft:
            case JoyButton.DpadUp:
                MoveStation(-1);
                return true;
            case JoyButton.DpadRight:
            case JoyButton.DpadDown:
                MoveStation(1);
                return true;
            case JoyButton.LeftShoulder:
                ChangeSelection(-1);
                return true;
            case JoyButton.RightShoulder:
                ChangeSelection(1);
                return true;
            case JoyButton.X:
                OpenHubInfo();
                return true;
            case JoyButton.Y:
                ToggleHubUi();
                return true;
            default:
                return false;
        }
    }

    private void MoveStation(int direction)
    {
        int count = _stations.Length;
        int next =
            ((int)_currentStation + direction + count) %
            count;
        ChangeStation((HubStation)next);
    }

    private void NavigateFromMobileArrow(
        int direction)
    {
        MoveStation(direction);
        RestoreMobileNavigationAppearance();
    }

    private void SetMobileNavigationVisible(
        bool visible)
    {
        _mobilePreviousStationButton.Visible =
            visible;
        _mobileNextStationButton.Visible =
            visible;
        if (visible)
        {
            RestoreMobileNavigationAppearance();
        }
    }

    private void RestoreMobileNavigationAppearance()
    {
        Button[] arrows =
        {
            _mobilePreviousStationButton,
            _mobileNextStationButton
        };
        foreach (Button arrow in arrows)
        {
            if (!IsInstanceValid(arrow))
            {
                continue;
            }

            arrow.Modulate = Colors.White;
            arrow.SelfModulate = Colors.White;
            arrow.Scale = Vector2.One;
            arrow.QueueRedraw();
        }
    }

    private void ChangeStation(HubStation station)
    {
        if (_currentStation == station ||
            _transitioning)
        {
            return;
        }

        _currentStation = station;
        if (IsInstanceValid(_stationScroll))
        {
            _stationScroll.ScrollVertical = 0;
        }
        _cameraTransitionPhase = 1.0f;
        RefreshStation();
        GamepadInput.Rumble(0.025f, 0.08f, 0.055f);
    }

    private void SelectPrevious()
    {
        ChangeSelection(-1);
    }

    private void SelectNext()
    {
        ChangeSelection(1);
    }

    private void ChangeSelection(int direction)
    {
        if (_currentStation == HubStation.Wardrobe)
        {
            _previewSkin =
                (_previewSkin +
                    direction +
                    CustomizationProfile.SkinCount) %
                CustomizationProfile.SkinCount;
            RefreshWardrobe();
            GamepadInput.Rumble(
                0.012f,
                0.035f,
                0.035f);
            return;
        }

        if (_currentStation == HubStation.Shelter)
        {
            _previewRefugeStyle =
                (_previewRefugeStyle +
                    direction +
                    CustomizationProfile.RefugeStyleCount) %
                CustomizationProfile.RefugeStyleCount;
            RefreshShelterStyle();
            GamepadInput.Rumble(
                0.012f,
                0.035f,
                0.035f);
        }
    }

    private void ActivateStation()
    {
        if (_hubUiCollapsed || _transitioning) return;
        switch (_currentStation)
        {
            case HubStation.Wardrobe:
                if (_profile?.SelectSkin(
                    _previewSkin) == true)
                {
                    RefreshWardrobe();
                }
                break;
            case HubStation.Shelter:
                _profile?.SelectRefugeStyle(
                    _previewRefugeStyle);
                RefreshShelterStyle();
                break;
            case HubStation.Loadout:
                _buffArchive.Open();
                break;
            case HubStation.Descent:
                BeginDescent();
                break;
        }
    }

    private void BeginDescent()
    {
        if (_transitioning)
        {
            return;
        }

        ChangeStation(HubStation.Descent);
        _transitioning = true;
        GamepadInput.Rumble(0.08f, 0.25f, 0.18f);
        _stationPanel.Hide();
        Tween transition = CreateTween();
        transition.SetParallel(true);
        transition.TweenProperty(
            _camera,
            "fov",
            37.0f,
            0.26f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        transition.TweenProperty(
            _fade,
            "color:a",
            0.16f,
            0.2f);
        transition.SetParallel(false);
        transition.TweenCallback(
            Callable.From(EnterRun));
    }

    private void EnterRun()
    {
        GetTree().Paused = false;
        Error result =
            GetTree().ChangeSceneToFile(
                RunScenePath);
        if (result == Error.Ok)
        {
            return;
        }

        _transitioning = false;
        _stationPanel.Show();
        _fade.Color = new Color(
            _fade.Color,
            0.0f);
        _selectionDetail.Text =
            "RUN SCENE FAILED  •  CHECK OUTPUT";
        GD.PushError(
            $"REFUGE: failed to load {RunScenePath}: {result}");
    }

    private void UpdateCamera(float deltaSeconds)
    {
        StationView station =
            _stations[(int)_currentStation];
        float travelWeight =
            1.0f -
            Mathf.Exp(-3.4f * deltaSeconds);
        float lookWeight =
            1.0f -
            Mathf.Exp(-4.2f * deltaSeconds);
        Vector3 idleOffset = new(
            Mathf.Sin(_cinematicTime * 0.37f) * 0.055f,
            Mathf.Sin(_cinematicTime * 0.53f) * 0.035f,
            Mathf.Cos(_cinematicTime * 0.29f) * 0.035f);
        _cameraTransitionPhase = Mathf.Max(
            0.0f,
            _cameraTransitionPhase -
                deltaSeconds / 0.92f);
        float travelArc = Mathf.Sin(
            (1.0f - _cameraTransitionPhase) *
            Mathf.Pi) *
            _cameraTransitionPhase;
        Vector3 cinematicArc = new(
            Mathf.Sin(
                ((int)_currentStation + 1) *
                1.7f) *
                travelArc *
                0.22f,
            travelArc * 0.42f,
            0.0f);
        _camera.GlobalPosition =
            _camera.GlobalPosition.Lerp(
                station.CameraPosition +
                    idleOffset +
                    cinematicArc,
                travelWeight);
        _cameraLookTarget =
            _cameraLookTarget.Lerp(
                GetComposedLookTarget(
                    station),
                lookWeight);
        _camera.Fov = Mathf.Lerp(
            _camera.Fov,
            station.Fov +
                travelArc * 1.8f,
            travelWeight);
        _camera.LookAt(
            _cameraLookTarget,
            Vector3.Up);
        _camera.RotateObjectLocal(
            Vector3.Back,
            Mathf.Sin(
                _cinematicTime * 0.31f) *
                0.0025f);
    }

    private Vector3 GetComposedLookTarget(
        StationView station)
    {
        Vector3 toSubject =
            station.LookTarget -
            station.CameraPosition;
        float distance =
            Mathf.Max(
                0.1f,
                toSubject.Length());
        Vector3 viewRight =
            toSubject.Normalized()
                .Cross(Vector3.Up)
                .Normalized();
        if (viewRight.LengthSquared() <
            0.001f)
        {
            viewRight = Vector3.Right;
        }

        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        float aspect =
            viewportSize.Y > 1.0f
                ? Mathf.Clamp(
                    viewportSize.X /
                        viewportSize.Y,
                    0.65f,
                    2.45f)
                : 16.0f / 9.0f;
        float halfWidthAtSubject =
            distance *
            Mathf.Tan(
                Mathf.DegToRad(
                    station.Fov * 0.5f)) *
            aspect;
        float compositionShift =
            Mathf.Clamp(
                halfWidthAtSubject * 0.44f,
                1.0f,
                6.2f);

        return station.LookTarget -
            viewRight *
            compositionShift;
    }

    private void UpdateAmbientMotion(float deltaSeconds)
    {
        for (int index = 0;
            index < _dustMotes.Count;
            index++)
        {
            Vector3 origin =
                _dustOrigins[index];
            float phase =
                _cinematicTime *
                    (0.22f + index % 5 * 0.035f) +
                index * 0.73f;
            _dustMotes[index].Position =
                origin +
                new Vector3(
                    Mathf.Sin(phase) * 0.18f,
                    Mathf.Sin(phase * 0.71f) * 0.22f,
                    Mathf.Cos(phase * 0.83f) * 0.12f);
        }

        for (int index = 0;
            index < _stationButtons.Count;
            index++)
        {
            if (index != (int)_currentStation)
            {
                continue;
            }
            float pulse =
                0.92f +
                Mathf.Sin(
                    _cinematicTime * 2.6f) *
                0.08f;
            _stationButtons[index].Modulate =
                new Color(
                    0.52f,
                    0.94f,
                    1.0f,
                    pulse);
        }

        for (int index = 0;
            index < _portalRings.Count;
            index++)
        {
            _portalRings[index].RotateY(
                deltaSeconds *
                (0.18f + index * 0.11f) *
                (index % 2 == 0 ? 1.0f : -1.0f));
        }

        if (IsInstanceValid(_worldBrandLabel))
        {
            Color brandColor =
                _worldBrandLabel.Modulate;
            brandColor.A =
                0.8f +
                Mathf.Sin(
                    _cinematicTime * 1.35f) *
                0.12f;
            _worldBrandLabel.Modulate =
                brandColor;
        }

        if (_mannequinSpinPivot != null)
        {
            Vector3 rotation =
                _mannequinSpinPivot.Rotation;
            rotation.Y = Mathf.LerpAngle(
                rotation.Y,
                _mannequinTargetYaw,
                1.0f -
                    Mathf.Exp(
                        -12.0f *
                        deltaSeconds));
            _mannequinSpinPivot.Rotation =
                rotation;
            _mannequinSpinPivot.Position =
                new Vector3(
                    0.0f,
                    Mathf.Sin(
                        _cinematicTime * 2.15f) *
                        0.065f,
                    0.0f);
        }
    }

    private void ApplyRefugeStyle(int styleIndex)
    {
        Color accent =
            CustomizationProfile.GetRefugeStyleColor(
                styleIndex);
        // Every architectural surface and light belongs to the selected palette.
        Color neutral = new Color(0.38f, 0.43f, 0.49f);
        _floorMaterial.AlbedoColor = neutral.Lerp(accent, .32f).Darkened(.26f);
        _floorMaterial.Metallic = .08f;
        _floorMaterial.Roughness = .82f;
        _wallMaterial.AlbedoColor = new Color(.63f, .67f, .7f).Lerp(accent, .28f);
        _wallMaterial.Metallic = .04f;
        _wallMaterial.Roughness = .76f;
        _accentMaterial.AlbedoColor = accent;
        _accentMaterial.Emission = accent;
        _routeMaterial.AlbedoColor = accent.Darkened(.12f);
        _routeMaterial.Emission = accent;
        _portalMaterial.AlbedoColor = accent.Lightened(.12f);
        _portalMaterial.Emission = accent;
        if (_windowMaterial != null)
        {
            _windowMaterial.AlbedoColor = accent.Darkened(.38f);
            _windowMaterial.Emission = accent.Darkened(.25f);
        }
        if (_softLightMaterial != null)
        {
            _softLightMaterial.AlbedoColor = accent.Lerp(Colors.White, .45f);
            _softLightMaterial.Emission = accent;
        }
        if (_glassMaterial != null)
        {
            _glassMaterial.AlbedoColor = new Color(accent, .24f);
            _glassMaterial.Emission = accent;
        }
        if (_dustMaterial != null)
        {
            _dustMaterial.AlbedoColor = accent.Lightened(.4f);
            _dustMaterial.Emission = accent;
        }
        _environment.BackgroundColor = accent.Darkened(.78f);
        _environment.AmbientLightColor = new Color(.7f, .73f, .78f).Lerp(accent, .24f);
        _environment.AmbientLightEnergy = .62f;
        _environment.FogLightColor = accent.Lerp(new Color(.16f, .2f, .26f), .72f);
        _environment.FogLightEnergy = .4f;
        DirectionalLight3D key = GetNode<DirectionalLight3D>("KeyLight");
        key.LightColor = Colors.White.Lerp(accent, .18f);
        key.LightEnergy = .85f;
        foreach (OmniLight3D light in _accentLights)
        {
            light.LightColor = accent;
            light.LightEnergy = 2.2f;
        }
        _worldBrandLabel.Modulate = accent.Lightened(.5f);
        _worldStatsLabel.Modulate = accent.Lightened(.35f);
        for (int moduleIndex = 0;
            moduleIndex < _refugeStyleModules.Count;
            moduleIndex++)
        {
            _refugeStyleModules[moduleIndex].Visible =
                moduleIndex == Mathf.Clamp(
                    styleIndex,
                    0,
                    _refugeStyleModules.Count - 1);
        }
    }

    private void OnProfileChanged()
    {
        _previewSkin =
            _profile.SelectedSkin;
        _previewRefugeStyle =
            _profile.SelectedRefugeStyle;
        _profile.ApplySelectedSkin(
            _mannequin);
        ApplyRefugeStyle(
            _previewRefugeStyle);
        RefreshStation(true);
    }

    private void RefreshStats()
    {
        if (_profile == null ||
            !IsInstanceValid(_worldStatsLabel))
        {
            return;
        }

        _worldStatsLabel.Text =
            $"BEST {_profile.BestScore}  //  " +
            $"KILLS {_profile.TotalKills}  //  " +
            $"ARENAS {_profile.ArenasCleared}";
    }

    private void HandleMouseButton(
        InputEventMouseButton mouse)
    {
        if (mouse.ButtonIndex ==
            MouseButton.WheelUp &&
            mouse.Pressed)
        {
            MoveStation(-1);
            return;
        }
        if (mouse.ButtonIndex ==
            MouseButton.WheelDown &&
            mouse.Pressed)
        {
            MoveStation(1);
            return;
        }
        if (mouse.ButtonIndex !=
            MouseButton.Left)
        {
            return;
        }

        if (mouse.Pressed)
        {
            _rotatingMannequin =
                _currentStation ==
                HubStation.Wardrobe;
            _pointerDragging =
                !_rotatingMannequin;
            _dragStart = mouse.Position;
            _dragTravel = Vector2.Zero;
        }
        else if (_rotatingMannequin)
        {
            _rotatingMannequin = false;
        }
        else if (_pointerDragging)
        {
            _pointerDragging = false;
            ResolveSwipe();
        }
    }

    private void HandleScreenTouch(
        InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            bool overHubInterface = IsTouchOverHubInterface(touch.Position);
            if (overHubInterface) { CancelHubGesture(); return; }
            _touchDragIndex = touch.Index;
            _rotatingMannequin =
                _currentStation ==
                HubStation.Wardrobe &&
                !overHubInterface;
            _dragStart = touch.Position;
            _dragTravel = Vector2.Zero;
        }
        else if (touch.Index == _touchDragIndex)
        {
            _touchDragIndex = -1;
            if (_rotatingMannequin)
            {
                _rotatingMannequin = false;
            }
            else
            {
                ResolveSwipe();
            }
        }
    }

    private bool IsTouchOverHubInterface(
        Vector2 position)
    {
        Control[] interactive =
        {
            _stationPanel,
            _mobilePreviousStationButton,
            _mobileNextStationButton,
            _settingsButton,
            _uiToggleButton,
            _infoButton
        };
        foreach (Control control in interactive)
        {
            if (IsInstanceValid(control) &&
                control.Visible &&
                control.GetGlobalRect()
                    .HasPoint(position))
            {
                return true;
            }
        }

        return false;
    }

    private void RotateMannequin(float horizontalDelta)
    {
        _mannequinTargetYaw -=
            Mathf.DegToRad(
                horizontalDelta * 0.34f);
    }

    private void HandleStationScrollGuiInput(
        InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventScreenTouch touch
                when touch.Pressed:
                _panelTouchDragIndex =
                    touch.Index;
                _panelDragTravel =
                    Vector2.Zero;
                break;
            case InputEventScreenDrag drag
                when drag.Index ==
                    _panelTouchDragIndex:
                _panelDragTravel +=
                    drag.Relative;
                if (Mathf.Abs(
                        _panelDragTravel.X) >
                        18.0f &&
                    Mathf.Abs(
                        _panelDragTravel.X) >
                        Mathf.Abs(
                            _panelDragTravel.Y) *
                        1.15f)
                {
                    _stationScroll.AcceptEvent();
                }
                break;
            case InputEventScreenTouch touch
                when !touch.Pressed &&
                    touch.Index ==
                        _panelTouchDragIndex:
                _panelTouchDragIndex = -1;
                if (TryResolveSwipe(
                        _panelDragTravel))
                {
                    _stationScroll.AcceptEvent();
                }
                break;
        }
    }

    private void ResolveSwipe()
    {
        TryResolveSwipe(_dragTravel);
    }

    private bool TryResolveSwipe(
        Vector2 travel)
    {
        float horizontal =
            Mathf.Abs(travel.X);
        float vertical =
            Mathf.Abs(travel.Y);
        if (horizontal <
            Mathf.Max(
                touchLayout()
                    ? 42.0f
                    : 52.0f,
                GetViewport().GetVisibleRect().Size.X *
                    0.05f) ||
            horizontal <
                vertical * 1.15f)
        {
            return false;
        }

        MoveStation(
            travel.X < 0.0f ? 1 : -1);
        return true;
    }

    private void OpenSettings()
    {
        if (_transitioning)
        {
            return;
        }

        _fullSettings.OpenFromExternal();
    }

    private void OpenHubInfo()
    {
        if (_transitioning || _fullSettings?.IsMenuOpen == true || _buffArchive?.IsOpen == true) return;
        CancelHubGesture();
        _infoPanel.Open();
    }

    private void ToggleHubUi()
    {
        if (_transitioning || _fullSettings?.IsMenuOpen == true || _infoPanel?.IsOpen == true || _buffArchive?.IsOpen == true) return;
        _hubUiCollapsed = !_hubUiCollapsed;
        _hubUiFading = true;
        CancelHubGesture();
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        UiIconLibrary.Apply(_uiToggleButton, _hubUiCollapsed ? UiIconLibrary.Invisible : UiIconLibrary.Visible, true, 24);
        _uiToggleButton.TooltipText = _hubUiCollapsed ? "SHOW INTERFACE · H / Y / Esc" : "HIDE INTERFACE · H / Y";
        UpdateProjectedHubInterface();
        ApplyHubVisibility();
        GamepadInput.Rumble(.012f, .025f, .04f);
    }

    private void CancelHubGesture()
    {
        _pointerDragging = _rotatingMannequin = false;
        _touchDragIndex = _panelTouchDragIndex = -1;
        _dragTravel = _panelDragTravel = Vector2.Zero;
        ResetStickNavigation();
    }

    private void ApplyHubVisibility()
    {
        if (!IsInstanceValid(_stationPanel)) return;
        bool draw = _hubUiOpacity > .002f && !_transitioning;
        bool interactive = !_hubUiCollapsed && !_transitioning;
        foreach (Control control in new Control[] { _stationPanel, _leftFocusVeil, _settingsButton, _infoButton })
        {
            control.Visible = draw;
            control.Modulate = new Color(control.Modulate, _hubUiOpacity);
        }
        foreach (Button button in _stationButtons)
            button.Modulate = new Color(button.Modulate, .66f * _hubUiOpacity);
        foreach (Button button in new[] { _mobilePreviousStationButton, _mobileNextStationButton })
            button.Modulate = new Color(button.Modulate, _hubUiOpacity);
        foreach (Control control in new Control[] { _previousButton, _primaryButton, _nextButton, _settingsButton, _infoButton,
            _mobilePreviousStationButton, _mobileNextStationButton })
            control.MouseFilter = interactive ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        foreach (Button button in _stationButtons)
        {
            button.MouseFilter = interactive ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
            if (!draw) button.Hide();
        }
        if (!draw) SetMobileNavigationVisible(false);
        foreach (Label3D label in _hubWorldLabels)
        {
            label.Visible = draw;
            label.Transparency = 1 - _hubUiOpacity;
        }
        _uiToggleButton.Visible = !_transitioning;
        _uiToggleButton.Modulate = Colors.White;
    }

    private void AddStationPlatform(
        Node3D parent,
        Vector3 position,
        string labelText)
    {
        CylinderMesh platformMesh = new()
        {
            TopRadius = 2.25f,
            BottomRadius = 2.45f,
            Height = 0.28f,
            RadialSegments = 32
        };
        platformMesh.SurfaceSetMaterial(
            0,
            _floorMaterial);
        parent.AddChild(new MeshInstance3D
        {
            Mesh = platformMesh,
            Position = position
        });
        TorusMesh ringMesh = new()
        {
            InnerRadius = 1.92f,
            OuterRadius = 2.12f,
            Rings = 32,
            RingSegments = 8
        };
        ringMesh.SurfaceSetMaterial(
            0,
            _accentMaterial);
        MeshInstance3D ring = new()
        {
            Mesh = ringMesh,
            Position =
                position + Vector3.Up * 0.17f
        };
        parent.AddChild(ring);
        _accentMeshes.Add(ring);
        Label3D label = CreateWorldLabel(
            labelText,
            position +
                new Vector3(0.0f, 0.16f, 2.38f),
            18);
        parent.AddChild(label);
    }

    private MeshInstance3D AddBox(
        string name,
        Vector3 position,
        Vector3 size,
        Material material)
    {
        return AddBoxTo(
            this,
            name,
            position,
            size,
            material);
    }

    private static MeshInstance3D AddBoxTo(
        Node parent,
        string name,
        Vector3 position,
        Vector3 size,
        Material material)
    {
        BoxMesh box = new()
        {
            Size = size
        };
        box.SurfaceSetMaterial(0, material);
        MeshInstance3D mesh = new()
        {
            Name = name,
            Mesh = box,
            Position = position
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static MeshInstance3D AddCylinderTo(
        Node parent,
        string name,
        Vector3 position,
        float radius,
        float height,
        Material material)
    {
        CylinderMesh cylinder = new()
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 18
        };
        cylinder.SurfaceSetMaterial(
            0,
            material);
        MeshInstance3D mesh = new()
        {
            Name = name,
            Mesh = cylinder,
            Position = position
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static Label3D CreateWorldLabel(
        string text,
        Vector3 position,
        int fontSize)
    {
        return new Label3D
        {
            Text = text,
            Position = position,
            FontSize = fontSize,
            PixelSize = 0.007f,
            Modulate =
                new Color(0.72f, 0.9f, 1.0f),
            OutlineSize = 4,
            Billboard =
                BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
    }

    private static StandardMaterial3D CreateMaterial(
        Color color,
        float roughness,
        float emissionEnergy)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0.07f,
            EmissionEnabled =
                emissionEnergy > 0.0f,
            Emission = color,
            EmissionEnergyMultiplier =
                Mathf.Min(
                    emissionEnergy,
                    0.68f)
        };
    }

    private static void SetMargins(
        MarginContainer margin,
        int left,
        int top,
        int right,
        int bottom)
    {
        margin.AddThemeConstantOverride(
            "margin_left", left);
        margin.AddThemeConstantOverride(
            "margin_top", top);
        margin.AddThemeConstantOverride(
            "margin_right", right);
        margin.AddThemeConstantOverride(
            "margin_bottom", bottom);
    }
}
