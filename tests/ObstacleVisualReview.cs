using Godot;
using System;
using System.Reflection;

/// <summary>
/// Render-only obstacle gallery. It uses the production construction methods
/// and the gameplay camera angle so visual reviews see the same silhouettes
/// as a real run.
/// </summary>
public partial class ObstacleVisualReview : Node3D
{
    private readonly Vector3[] _stations =
    {
        new(0.0f, 0.0f, 0.0f),
        new(52.0f, 0.0f, 0.0f),
        new(104.0f, 0.0f, 0.0f),
        new(156.0f, 0.0f, 0.0f),
        new(208.0f, 0.0f, 0.0f),
        new(260.0f, 0.0f, 0.0f),
        new(312.0f, 0.0f, 0.0f),
        new(364.0f, 0.0f, 0.0f),
        new(416.0f, 0.0f, 0.0f),
        new(468.0f, 0.0f, 0.0f),
        new(520.0f, 0.0f, 0.0f),
        new(572.0f, 0.0f, 0.0f),
        new(624.0f, 0.0f, 0.0f),
        new(676.0f, 0.0f, 0.0f)
    };

    private Node3D _cameraPivot;
    private int _frame;

    public override void _Ready()
    {
        GD.Seed(240629);
        CreateLighting();
        CreateGallery();
        CreateCamera();
    }

    public override void _PhysicsProcess(
        double delta)
    {
        _frame++;
        int station =
            Mathf.Clamp(
                _frame / 105,
                0,
                _stations.Length - 1);
        Vector3 target =
            _stations[station];
        float weight =
            1.0f -
            Mathf.Exp(
                -7.0f *
                (float)delta);
        Vector3 position =
            _cameraPivot.Position;
        position.X =
            Mathf.Lerp(
                position.X,
                target.X,
                weight);
        position.Z =
            Mathf.Lerp(
                position.Z,
                target.Z,
                weight);
        _cameraPivot.Position =
            position;
        if (_frame % 105 == 94)
        {
            CaptureStation(
                station);
        }
    }

    private void CreateGallery()
    {
        Color theme =
            new(
                0.12f,
                0.24f,
                0.56f);
        for (int index = 0;
            index < _stations.Length;
            index++)
        {
            Node3D station =
                new()
                {
                    Name =
                        $"ReviewStation{index + 1}",
                    Position =
                        _stations[index]
                };
            AddChild(
                station);
            if (index != 12)
            {
                AddGround(
                    station,
                    theme);
            }

            switch (index)
            {
                case 0:
                    InvokeStatic(
                        "CreateRhythmHurdles",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 1:
                    InvokeStatic(
                        "CreateFlowRailFan",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 2:
                    InvokeStatic(
                        "CreateWideAirLift",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 3:
                    InvokeStatic(
                        "CreateSwitchbackCanyon",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 4:
                    InvokeStatic(
                        "CreateKineticGate",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 5:
                    InvokeStatic(
                        "CreatePulseRunway",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 6:
                    InvokeStatic(
                        "CreateSkySurf",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 7:
                    InvokeStatic(
                        "CreateCrosswindSluice",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 8:
                    InvokeStatic(
                        "CreateCrawlingConvoy",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 9:
                    InvokeStatic(
                        "CreateDemolitionCorridor",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 10:
                    InvokeStatic(
                        "CreatePendulumSupports",
                        station,
                        36.0f,
                        0.0f,
                        theme);
                    break;
                case 11:
                    InvokeStatic(
                        "CreateRouteChoiceWall",
                        station,
                        36.0f,
                        70.0f,
                        theme);
                    break;
                case 12:
                    InvokeStatic(
                        "CreateWaterRelayGround",
                        station,
                        36.0f,
                        70.0f,
                        theme);
                    ProceduralWorld builder =
                        new();
                    InvokeInstance(
                        builder,
                        "CreateWaterRelaySetPiece",
                        station,
                        36.0f,
                        70.0f,
                        theme);
                    builder.Free();
                    break;
                default:
                    InvokeStatic(
                        "CreateRocketRamp",
                        station,
                        36.0f,
                        6.0f,
                        theme);
                    InvokeStatic(
                        "CreateKineticJumpPad",
                        station,
                        36.0f,
                        -9.0f,
                        theme);
                    break;
            }
        }
    }

    private async void CaptureStation(
        int station)
    {
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
                    $"/tmp/no-way-down-construction-{station + 1}.png");
        GD.Print(
            $"CONSTRUCTION_VISUAL_{station + 1}: {result}");
        if (station ==
            _stations.Length - 1)
        {
            GetTree().Quit(
                result == Error.Ok
                    ? 0
                    : 1);
        }
    }

    private static void AddGround(
        Node3D parent,
        Color color)
    {
        StandardMaterial3D material =
            new()
            {
                AlbedoColor = color,
                Roughness = 0.9f
            };
        parent.AddChild(
            new MeshInstance3D
            {
                Position =
                    new Vector3(
                        0.0f,
                        -1.0f,
                        0.0f),
                Mesh =
                    new BoxMesh
                    {
                        Size =
                            new Vector3(
                                36.0f,
                                2.0f,
                                70.0f),
                        Material =
                            material
                    }
            });
    }

    private void CreateCamera()
    {
        _cameraPivot =
            new Node3D
            {
                Name =
                    "ReviewCameraPivot",
                Rotation =
                    new Vector3(
                        Mathf.DegToRad(
                            -32.0f),
                        0.0f,
                        0.0f)
            };
        AddChild(
            _cameraPivot);
        Camera3D camera =
            new()
            {
                Name =
                    "ReviewCamera",
                Position =
                    new Vector3(
                        0.0f,
                        0.0f,
                        31.0f),
                Projection =
                    Camera3D
                        .ProjectionType
                        .Orthogonal,
                Size = 44.0f,
                Far = 240.0f,
                Current = true
            };
        _cameraPivot.AddChild(
            camera);
    }

    private void CreateLighting()
    {
        DirectionalLight3D light =
            new()
            {
                Rotation =
                    new Vector3(
                        Mathf.DegToRad(
                            -52.0f),
                        Mathf.DegToRad(
                            -28.0f),
                        0.0f),
                LightEnergy = 1.2f,
                ShadowEnabled = true
            };
        AddChild(
            light);
        WorldEnvironment world =
            new()
            {
                Environment =
                    new Godot.Environment
                    {
                        BackgroundMode =
                            Godot.Environment
                                .BGMode
                                .Color,
                        BackgroundColor =
                            new Color(
                                0.003f,
                                0.007f,
                                0.015f),
                        AmbientLightSource =
                            Godot.Environment
                                .AmbientSource
                                .Color,
                        AmbientLightColor =
                            new Color(
                                0.3f,
                                0.38f,
                                0.5f),
                        AmbientLightEnergy =
                            0.62f
                    }
            };
        AddChild(
            world);
    }

    private static void InvokeStatic(
        string methodName,
        params object[] arguments)
    {
        MethodInfo method =
            typeof(ProceduralWorld)
                .GetMethod(
                    methodName,
                    BindingFlags.Static |
                    BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"Missing {methodName}");
        }
        method.Invoke(
            null,
            arguments);
    }

    private static void InvokeInstance(
        object instance,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method =
            typeof(ProceduralWorld)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"Missing {methodName}");
        }
        method.Invoke(
            instance,
            arguments);
    }
}
