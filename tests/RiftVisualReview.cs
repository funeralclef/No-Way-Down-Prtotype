using Godot;
using System.Reflection;

/// <summary>
/// Rendered review scene for the production Rift Step VFX.
/// </summary>
public partial class RiftVisualReview : Node3D
{
    private Player _player;
    private Camera3D _camera;

    public override async void _Ready()
    {
        CreateEnvironment();
        CreateStage();
        CreateCamera();

        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        _player =
            playerScene.Instantiate<Player>();
        AddChild(_player);
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                2.0f,
                1.0f);
        _player.SetPhysicsProcess(false);

        Mob source = CreateMob(
            new Vector3(
                -2.3f,
                0.0f,
                0.5f));
        Mob destinationTarget =
            CreateMob(
                new Vector3(
                    1.8f,
                    0.0f,
                    -8.0f));

        Control interfaceRoot =
            new()
            {
                Name = "VisualReviewUi",
                MouseFilter =
                    Control.MouseFilterEnum
                        .Ignore
            };
        interfaceRoot
            .SetAnchorsAndOffsetsPreset(
                Control.LayoutPreset.FullRect);
        AddChild(interfaceRoot);
        CombatFeedbackDirector feedback =
            new()
            {
                Name =
                    "CombatFeedbackDirector"
            };
        AddChild(feedback);
        feedback.Initialize(
            _player,
            _camera,
            this,
            interfaceRoot);

        await PhysicsFrames(3);
        SetPlayerPrivate(
            "_lastRiftStompMob",
            source);
        SetPlayerPrivate(
            "_riftStompWindowTimer",
            0.24f);
        SetPlayerPrivate(
            "_nitroDashCooldownTimer",
            0.0f);
        _player.AddNitro(100.0f);
        InvokePlayerPrivate(
            "TryPerformRiftTeleport");
        destinationTarget
            .SetJumpReachCue(
                Mob.JumpReachCue.Reachable,
                1.0f);

        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error result =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-rift-visual.png");
        GD.Print(
            $"RIFT_VISUAL_CAPTURE: {result}");

        await ToSignal(
            GetTree().CreateTimer(0.58f),
            SceneTreeTimer.SignalName.Timeout);
        _player.EmitSignal(
            Player.SignalName.RiftStepStarted,
            false,
            new Vector3(
                -5.2f,
                0.1f,
                3.2f),
            new Vector3(
                -1.8f,
                0.1f,
                -4.8f));
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error groundResult =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-ground-burst-visual.png");
        GD.Print(
            $"GROUND_BURST_VISUAL_CAPTURE: {groundResult}");
        await ToSignal(
            GetTree().CreateTimer(0.52f),
            SceneTreeTimer.SignalName.Timeout);

        _player.EmitSignal(
            Player.SignalName.SlipstreamDodge,
            8,
            6.75f,
            3.96f,
            true,
            new Vector3(
                -1.1f,
                0.1f,
                0.5f),
            Vector3.Forward);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error slipstreamResult =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-slipstream-visual.png");
        GD.Print(
            $"SLIPSTREAM_VISUAL_CAPTURE: {slipstreamResult}");
        await ToSignal(
            GetTree().CreateTimer(1.08f),
            SceneTreeTimer.SignalName.Timeout);
        destinationTarget.SetJumpReachCue(
            Mob.JumpReachCue.None,
            0.0f);

        SetPlayerProperty(
            nameof(Player.SlipstreamCueTarget),
            destinationTarget);
        SetPlayerProperty(
            nameof(Player.CurrentSlipstreamCue),
            Player.SlipstreamCueLevel.Perfect);
        SetPlayerProperty(
            nameof(Player.SlipstreamCueUrgency),
            0.92f);
        SetPlayerProperty(
            nameof(Player.SlipstreamCuePredictedDistance),
            1.6f);
        SetPlayerProperty(
            nameof(Player.SlipstreamCueDirection),
            Vector3.Forward);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error cueResult =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-slipstream-cue-visual.png");
        GD.Print(
            $"SLIPSTREAM_CUE_VISUAL_CAPTURE: {cueResult}");
        SetPlayerProperty(
            nameof(Player.SlipstreamCueTarget),
            null);
        SetPlayerProperty(
            nameof(Player.CurrentSlipstreamCue),
            Player.SlipstreamCueLevel.None);

        SetPlayerProperty(
            nameof(Player.ClutchVaultCueTarget),
            source);
        SetPlayerProperty(
            nameof(Player.ClutchVaultCueUrgency),
            0.86f);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error vaultCueResult =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-clutch-vault-cue-visual.png");
        GD.Print(
            $"CLUTCH_VAULT_CUE_VISUAL_CAPTURE: {vaultCueResult}");
        SetPlayerProperty(
            nameof(Player.ClutchVaultCueTarget),
            null);
        _player.EmitSignal(
            Player.SignalName
                .ClutchVaultStarted,
            _player.GlobalPosition,
            source.GlobalPosition +
                Vector3.Up *
                    _player
                        .ClutchVaultArrivalHeight);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer
                .SignalName
                .FramePostDraw);
        Error vaultLaunchResult =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-clutch-vault-launch-visual.png");
        GD.Print(
            $"CLUTCH_VAULT_LAUNCH_VISUAL_CAPTURE: {vaultLaunchResult}");
        GetTree().Quit(
            result == Error.Ok &&
                groundResult == Error.Ok &&
                slipstreamResult == Error.Ok &&
                cueResult == Error.Ok &&
                vaultCueResult == Error.Ok &&
                vaultLaunchResult == Error.Ok
                ? 0
                : 1);
    }

    private void CreateEnvironment()
    {
        WorldEnvironment world =
            new()
            {
                Name =
                    "ReviewEnvironment",
                Environment =
                    new Environment
                    {
                        BackgroundMode =
                            Environment
                                .BGMode
                                .Color,
                        BackgroundColor =
                            new Color(
                                0.012f,
                                0.018f,
                                0.045f),
                        AmbientLightSource =
                            Environment
                                .AmbientSource
                                .Color,
                        AmbientLightColor =
                            new Color(
                                0.18f,
                                0.25f,
                                0.48f),
                        AmbientLightEnergy =
                            0.72f,
                        GlowEnabled = true,
                        GlowIntensity = 1.25f,
                        GlowBloom = 0.34f
                    }
            };
        AddChild(world);
        DirectionalLight3D light =
            new()
            {
                Name = "KeyLight",
                RotationDegrees =
                    new Vector3(
                        -52.0f,
                        -28.0f,
                        0.0f),
                LightColor =
                    new Color(
                        0.58f,
                        0.78f,
                        1.0f),
                LightEnergy = 1.35f,
                ShadowEnabled = true
            };
        AddChild(light);
    }

    private void CreateStage()
    {
        StandardMaterial3D floorMaterial =
            new()
            {
                AlbedoColor =
                    new Color(
                        0.025f,
                        0.05f,
                        0.1f),
                Metallic = 0.42f,
                Roughness = 0.38f
            };
        MeshInstance3D floor =
            new()
            {
                Name = "ReviewFloor",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(
                        24.0f,
                        0.25f,
                        18.0f),
                    Material =
                        floorMaterial
                },
                Position =
                    new Vector3(
                        0.0f,
                        -0.15f,
                        -3.5f)
            };
        AddChild(floor);

        for (int index = 0;
            index < 9;
            index++)
        {
            float z =
                2.0f - index * 1.5f;
            MeshInstance3D lane =
                new()
                {
                    Name =
                        $"LanePulse{index + 1}",
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(
                            0.04f,
                            0.02f,
                            0.68f),
                        Material =
                            new StandardMaterial3D
                            {
                                ShadingMode =
                                    BaseMaterial3D
                                        .ShadingModeEnum
                                        .Unshaded,
                                AlbedoColor =
                                    new Color(
                                        0.08f,
                                        0.5f,
                                        0.82f),
                                EmissionEnabled =
                                    true,
                                Emission =
                                    new Color(
                                        0.08f,
                                        0.5f,
                                        0.82f),
                                EmissionEnergyMultiplier =
                                    1.8f
                            }
                    },
                    Position =
                        new Vector3(
                            0.0f,
                            0.01f,
                            z)
                };
            AddChild(lane);
        }
    }

    private void CreateCamera()
    {
        _camera =
            new Camera3D
            {
                Name = "ReviewCamera",
                Projection =
                    Camera3D
                        .ProjectionType
                        .Perspective,
                Fov = 51.0f,
                Position =
                    new Vector3(
                        8.7f,
                        9.8f,
                        12.0f),
                Current = true
            };
        AddChild(_camera);
        _camera.LookAt(
            new Vector3(
                0.0f,
                1.15f,
                -4.0f),
            Vector3.Up);
    }

    private Mob CreateMob(
        Vector3 position)
    {
        PackedScene mobScene =
            GD.Load<PackedScene>(
                "res://mob.tscn");
        Mob mob =
            mobScene.Instantiate<Mob>();
        AddChild(mob);
        mob.GlobalPosition = position;
        mob.SetPhysicsProcess(false);
        VisibleOnScreenNotifier3D notifier =
            mob.GetNodeOrNull<
                VisibleOnScreenNotifier3D>(
                "VisibleOnScreenNotifier3D");
        if (notifier != null)
        {
            notifier.ProcessMode =
                ProcessModeEnum.Disabled;
        }
        return mob;
    }

    private void SetPlayerPrivate(
        string fieldName,
        object value)
    {
        typeof(Player)
            .GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.SetValue(
                _player,
                value);
    }

    private void SetPlayerProperty(
        string propertyName,
        object value)
    {
        typeof(Player)
            .GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            ?.SetValue(
                _player,
                value);
    }

    private void InvokePlayerPrivate(
        string methodName)
    {
        typeof(Player)
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.Invoke(
                _player,
                System.Array.Empty<object>());
    }

    private async System.Threading.Tasks.Task
        PhysicsFrames(int count)
    {
        for (int index = 0;
            index < count;
            index++)
        {
            await ToSignal(
                GetTree(),
                SceneTree
                    .SignalName
                    .PhysicsFrame);
        }
    }
}
