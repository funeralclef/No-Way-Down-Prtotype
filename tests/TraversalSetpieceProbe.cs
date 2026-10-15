using Godot;
using System;
using System.Reflection;

public partial class TraversalSetpieceProbe :
    Node3D
{
    private bool _failed;
    private Player _player;

    public override async void _Ready()
    {
        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        _player =
            playerScene.Instantiate<Player>();
        AddChild(_player);
        await PhysicsFrames(3);

        await TestRailMotion();
        await TestCourseLaunch();
        await TestRelayAnchor();
        await TestSetPieceConstruction();

        _player.QueueFree();
        await PhysicsFrames(3);
        GD.Print(
            _failed
                ? "TRAVERSAL_PROBE: FAILED"
                : "TRAVERSAL_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }

    private async System.Threading.Tasks.Task
        TestRailMotion()
    {
        _player.GlobalPosition =
            new Vector3(
                0.0f,
                40.0f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                9.0f,
                -12.0f);
        float verticalBefore =
            _player.Velocity.Y;
        _player.BeginTraversalRail(
            Vector3.Right,
            24.0f,
            0.8f,
            0.34f);
        await PhysicsFrames(8);
        Vector2 horizontal =
            new(
                _player.Velocity.X,
                _player.Velocity.Z);
        Check(
            horizontal.X > 12.0f &&
                horizontal.Length() >=
                    22.0f,
            "flow rail redirects and protects planar momentum");
        Check(
            _player.Velocity.Y <
                verticalBefore,
            "flow rail leaves the vertical arc to gravity");
    }

    private async System.Threading.Tasks.Task
        TestCourseLaunch()
    {
        _player.SetPhysicsProcess(false);
        _player.Velocity =
            new Vector3(
                4.0f,
                -8.0f,
                -10.0f);
        _player.ApplyCourseLaunch(
            Vector3.Forward,
            20.0f,
            30.0f,
            0.9f);
        Check(
            _player.Velocity.Y >=
                29.9f &&
                new Vector2(
                    _player.Velocity.X,
                    _player.Velocity.Z)
                    .Length() >=
                    19.9f,
            "course pad creates a deliberate upward and forward launch");
        _player.SetPhysicsProcess(true);
        float verticalBefore =
            _player.Velocity.Y;
        float zBefore =
            _player.GlobalPosition.Z;
        await PhysicsFrames(5);
        Check(
            _player.Velocity.Y <
                verticalBefore &&
                _player.GlobalPosition.Z <
                    zBefore - 0.8f,
            "course launch returns ownership to normal physics immediately");
    }

    private async System.Threading.Tasks.Task
        TestRelayAnchor()
    {
        PackedScene mobScene =
            GD.Load<PackedScene>(
                "res://mob.tscn");
        Mob relay =
            mobScene.Instantiate<Mob>();
        Vector3 anchor =
            new(
                2.0f,
                0.0f,
                -8.0f);
        relay.Initialize(
            anchor,
            Vector3.Forward,
            6.0f);
        relay.ConfigureArchetype(
            EnemyArchetypeId
                .Springboard);
        AddChild(relay);
        relay.ConfigureTraversalRelayAnchor(
            anchor,
            0.7f);
        await PhysicsFrames(20);
        Vector3 offset =
            relay.GlobalPosition -
                anchor;
        offset.Y = 0.0f;
        Check(
            offset.Length() <= 1.1f &&
                float.IsFinite(
                    relay.Velocity.X) &&
                relay
                    .JumpOpportunityPriority >=
                    0.9f,
            "relay enemy stays readable and remains a high-priority stomp target");
        relay.QueueFree();
        await PhysicsFrames(2);
    }

    private async System.Threading.Tasks.Task
        TestSetPieceConstruction()
    {
        ProceduralWorld world =
            new()
            {
                GenerationSettings =
                    new WorldGenerationSettings
                    {
                        GenerationAheadDistance =
                            0.0f
                    }
            };
        AddChild(world);
        InvokeWorld(
            world,
            "EnsureGeneratedThrough",
            -1600.0f);
        Check(world.FindChild("RelayWater", true, false) != null &&
                world.FindChild("MeanderWalk", true, false) != null,
            "live course alternates the water relay and forgiving long walks");
        StaticBody3D road =
            world.FindChild(
                "Ground",
                true,
                false) as StaticBody3D;
        MeshInstance3D roadMesh =
            road?.GetNodeOrNull<MeshInstance3D>(
                "MeshInstance3D");
        Shader roadShader =
            ((roadMesh?.Mesh as BoxMesh)
                ?.Material as ShaderMaterial)
                ?.Shader;
        Check(
            roadShader?.ResourcePath ==
                "res://shaders/run_surface.gdshader",
            "generated road uses the external run-surface shader");
        Node3D firstArenaGround =
            world.FindChild(
                "ArenaGround",
                true,
                false) as Node3D;
        Node3D firstArenaSection =
            firstArenaGround?.GetParent<Node3D>();
        int firstArenaIndex =
            firstArenaSection != null &&
            firstArenaSection.Name.ToString()
                .StartsWith("Section")
                ? int.Parse(
                    firstArenaSection.Name.ToString()
                        .Replace("Section", ""))
                : -1;
        world.ApplyChapterRoute(
            RunRouteStyle.Hunt,
            firstArenaIndex);
        Node3D routedSection = null;
        foreach (Node child in world.GetChildren())
        {
            if (child is not Node3D candidate ||
                !candidate.Name.ToString()
                    .StartsWith("Section"))
            {
                continue;
            }
            int candidateIndex =
                int.Parse(
                    candidate.Name.ToString()
                        .Replace("Section", ""));
            if (candidateIndex <= firstArenaIndex)
            {
                continue;
            }
            if (candidate.FindChild(
                    "ArenaGround",
                    true,
                    false) != null)
            {
                break;
            }
            routedSection ??= candidate;
        }
        Node routeIdentity =
            routedSection?.GetNodeOrNull<Node>(
                "ChapterRouteIdentity");
        Check(
            firstArenaIndex >= 0 &&
                firstArenaSection?.GetNodeOrNull<Node>(
                    "RouteExitIdentity") != null &&
                routeIdentity != null &&
                CountDescendantsOfType
                    <CollisionObject3D>(
                        routeIdentity) == 0,
            "route choice retrofits the existing straight chapter with non-colliding identity marks");
#if false
        StaticBody3D curveQuarter =
            world.FindChild(
                "CurveRoadSegment_4",
                true,
                false) as
                    StaticBody3D;
        StaticBody3D curveReturn =
            world.FindChild(
                "CurveRoadSegment_20",
                true,
                false) as
                    StaticBody3D;
        StaticBody3D flowStart =
            world.FindChild(
                "FlowBranchSegment_0",
                true,
                false) as
                    StaticBody3D;
        StaticBody3D flowMiddle =
            world.FindChild(
                "FlowBranchSegment_9",
                true,
                false) as
                    StaticBody3D;
        StaticBody3D huntMiddle =
            world.FindChild(
                "HuntBranchSegment_9",
                true,
                false) as
                    StaticBody3D;
        StaticBody3D flowMerge =
            world.FindChild(
                "FlowBranchSegment_19",
                true,
                false) as
                    StaticBody3D;
        Check(
            IsInstanceValid(curveQuarter) &&
                IsInstanceValid(curveReturn) &&
                Mathf.Sign(
                    curveQuarter.Position.X) !=
                    Mathf.Sign(
                        curveReturn.Position.X) &&
                IsInstanceValid(flowStart) &&
                IsInstanceValid(flowMiddle) &&
                IsInstanceValid(huntMiddle) &&
                IsInstanceValid(flowMerge) &&
                Mathf.Abs(
                    flowMiddle.Position.X) >
                    6.0f &&
                Mathf.Abs(
                    flowStart.Position.X) <
                    2.0f &&
                Mathf.Abs(
                    flowMerge.Position.X) <
                    2.0f &&
                Mathf.Abs(
                    flowMiddle.Position.X -
                    huntMiddle.Position.X) >
                    13.0f &&
                world.FindChild(
                    "CurvedRoadSurface",
                    true,
                    false) is
                    MeshInstance3D &&
                world.FindChild(
                    "FlowBranchSurface",
                    true,
                    false) is
                    MeshInstance3D &&
                Mathf.Abs(
                    world.GetRouteCenterX(
                        curveQuarter
                            .GlobalPosition.Z) -
                    curveQuarter
                        .GlobalPosition.X) <
                    0.75f &&
                Mathf.Abs(
                    world.GetRouteCenterX(
                        flowMiddle
                            .GlobalPosition.Z,
                        flowMiddle
                            .GlobalPosition.X) -
                    flowMiddle
                        .GlobalPosition.X) <
                    0.9f,
            "curves sweep across the axis and fork branches merge before the arena");
        Check(
            world.FindChild(
                    "RouteForkHuntTrigger",
                    true,
                    false) != null &&
                world.FindChild(
                    "FlowBranchPulse",
                    true,
                    false) != null &&
                world.FindChild(
                    "ConstructionBrief_ROUTESPLIT",
                    true,
                    false) == null,
            "route fork exposes distinct HUNT and FLOW verbs without a pre-obstacle briefing");
        int forkRelayRequests = 0;
        world.TraversalEnemyRequested +=
            (
                Vector3 position,
                Vector3 direction,
                int index) =>
            {
                forkRelayRequests++;
            };
        TraversalRelayTrigger forkTrigger =
            world.FindChild(
                "RouteForkHuntTrigger",
                true,
                false) as
                    TraversalRelayTrigger;
        typeof(TraversalRelayTrigger)
            .GetMethod(
                "OnBodyEntered",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.Invoke(
                forkTrigger,
                new object[] { _player });
        Check(
            forkRelayRequests == 5,
            "HUNT branch prewarms a five-target combo route before the choice");
#endif
        Check(
            world.FindChild(
                    "CoursePillar",
                    true,
                    false) == null &&
                world.FindChild(
                    "KineticGate",
                    true,
                    false) == null &&
                world.FindChild(
                    "SwitchbackCanyon",
                    true,
                    false) == null &&
                world.FindChild(
                    "DemolitionCorridor",
                    true,
                    false) == null &&
                world.FindChild(
                    "PendulumSupports",
                    true,
                    false) == null &&
                world.FindChild(
                    "JumpHurdle",
                    true,
                    false) == null &&
                world.FindChild(
                    "FlowRail",
                    true,
                    false) == null &&
                world.FindChild(
                    "RocketRamp",
                    true,
                    false) == null &&
                world.FindChild(
                    "VectorJumpPad",
                    true,
                    false) == null &&
                world.FindChild(
                    "KineticJumpPad",
                    true,
                    false) == null,
            "live course excludes every unrelated obstacle family");
        Node3D waterRoot =
            new()
            {
                Name =
                    "WaterProbe"
            };
        world.AddChild(waterRoot);
        InvokeWorld(
            world,
            "CreateWaterRelayGround",
            waterRoot,
            36.0f,
            100.0f,
            new Color(
                0.1f,
                0.3f,
                0.5f));
        InvokeWorld(
            world,
            "CreateWaterRelaySetPiece",
            waterRoot,
            36.0f,
            100.0f,
            new Color(
                0.1f,
                0.3f,
                0.5f));
        int relayRequests = 0;
        world.TraversalEnemyRequested +=
            (
                Vector3 position,
                Vector3 direction,
                int index) =>
            {
                relayRequests++;
            };
        TraversalRelayTrigger relayTrigger =
            waterRoot.FindChild(
                "WaterRelayTrigger",
                true,
                false) as
                    TraversalRelayTrigger;
        MethodInfo triggerMethod =
            typeof(TraversalRelayTrigger)
                .GetMethod(
                    "OnBodyEntered",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        triggerMethod?.Invoke(
            relayTrigger,
            new object[]
            {
                _player
            });
        MeshInstance3D relayWater =
            waterRoot.FindChild(
                "RelayWater",
                true,
                false) as
                    MeshInstance3D;
        CollisionShape3D relayTriggerShape =
            relayTrigger != null &&
                relayTrigger.GetChildCount() >
                    0
                ? relayTrigger.GetChild(0) as
                    CollisionShape3D
                : null;
        Check(
            relayWater?.Mesh is
                PlaneMesh waterPlane &&
                waterPlane.Size.Y >=
                    28.0f &&
                waterRoot.FindChild(
                "WaterRelayTrigger",
                    true,
                    false) is
                    TraversalRelayTrigger &&
                waterRoot.FindChild(
                    "WaterFallRescue",
                    true,
                    false) is
                    Area3D &&
                relayTriggerShape?.Position.Z >=
                    38.0f &&
                relayRequests == 6,
            "wider water relay builds a real gap, a rescue volume, and a prewarmed enemy chain");

        float healthBeforeWater =
            _player.Health;
        _player.GlobalPosition =
            new Vector3(
                3.0f,
                -2.2f,
                0.0f);
        _player.Velocity =
            new Vector3(
                0.0f,
                -8.0f,
                0.0f);
        await PhysicsFrames(3);
        Check(
            Mathf.IsEqualApprox(
                _player.Health,
                healthBeforeWater -
                    18.0f) &&
                _player.GlobalPosition.Y >
                    -0.4f &&
                _player.GlobalPosition.Z >
                    11.0f,
            "water fall removes health and returns the same player to the bank");

        Node3D railRoot =
            new()
            {
                Name =
                    "RailProbe"
            };
        world.AddChild(railRoot);
        InvokeWorld(
            world,
            "CreateFlowRail",
            railRoot,
            36.0f,
            0.0f,
            new Color(
                0.2f,
                0.4f,
                0.6f));
        Check(
            railRoot.FindChild(
                "FlowRailZone",
                true,
                false) is
                    TraversalRailZone,
            "local flow rail contains one readable guided slide zone");

        Node3D pillarRoot =
            new()
            {
                Name =
                    "PillarProbe"
            };
        world.AddChild(pillarRoot);
        InvokeWorld(
            world,
            "CreatePillar",
            pillarRoot,
            36.0f,
            0.0f,
            new Color(
                0.3f,
                0.45f,
                0.65f));
        Check(
            pillarRoot.FindChild(
                "CoursePillar",
                true,
                false) is
                    StaticBody3D &&
                pillarRoot.FindChild(
                    "AirLiftZone",
                    true,
                    false) == null,
            "course pillar replaces the suspended air platform");

        Node3D obstacleRoot = new()
        {
            Name = "ObstacleProbe"
        };
        world.AddChild(obstacleRoot);
        InvokeWorld(
            world,
            "CreateJumpHurdle",
            obstacleRoot,
            36.0f,
            24.0f,
            new Color(0.3f, 0.5f, 0.7f));
        InvokeWorld(
            world,
            "CreateRouteChoiceWall",
            obstacleRoot,
            36.0f,
            100.0f,
            new Color(0.4f, 0.3f, 0.7f));
        InvokeWorld(
            world,
            "CreateRocketRamp",
            obstacleRoot,
            36.0f,
            -4.0f,
            new Color(0.3f, 0.4f, 0.7f));
        InvokeWorld(
            world,
            "CreateVectorJumpPad",
            obstacleRoot,
            36.0f,
            -20.0f,
            new Color(0.3f, 0.4f, 0.7f));
        InvokeWorld(
            world,
            "CreateKineticJumpPad",
            obstacleRoot,
            36.0f,
            -34.0f,
            new Color(0.3f, 0.4f, 0.7f));
        CourseLaunchPad rocketLaunch =
            obstacleRoot.FindChild(
                "RocketRampLaunch",
                true,
                false) as
                    CourseLaunchPad;
        KineticJumpPad kineticPad =
            obstacleRoot.FindChild(
                "KineticJumpPad",
                true,
                false) as
                    KineticJumpPad;
        Check(
            obstacleRoot.FindChild(
                "JumpHurdle",
                true,
                false) != null &&
                obstacleRoot.FindChild(
                    "TunnelBeam",
                    true,
                    false) != null &&
                obstacleRoot.FindChild(
                "WallSpringboardZone",
                true,
                false) is
                    CourseLaunchPad &&
                rocketLaunch != null &&
                rocketLaunch.ForwardSpeed >=
                    36.0f &&
                rocketLaunch.VerticalSpeed >=
                    18.0f &&
                obstacleRoot.FindChild(
                    "VectorJumpZone",
                    true,
                    false) is
                    CourseLaunchPad &&
                kineticPad != null,
            "course keeps readable local obstacles, a rocket ramp, and distinct jump pads");

        _player.Velocity =
            new Vector3(
                0.0f,
                -14.0f,
                -12.0f);
        MethodInfo kineticTrigger =
            typeof(KineticJumpPad)
                .GetMethod(
                    "OnBodyEntered",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        kineticTrigger?.Invoke(
            kineticPad,
            new object[]
            {
                _player
            });
        Check(
            new Vector2(
                _player.Velocity.X,
                _player.Velocity.Z)
                    .Length() >= 32.0f &&
                _player.Velocity.Y >=
                    15.0f,
            "kinetic pad converts a hard landing into extra forward and vertical energy");

        Node3D legacyRoot = new()
        {
            Name = "LegacyProbe"
        };
        world.AddChild(legacyRoot);
        InvokeWorld(
            world,
            "CreateRushBreakablePair",
            legacyRoot,
            36.0f,
            100.0f,
            new Color(0.5f, 0.3f, 0.2f));
        InvokeWorld(
            world,
            "CreateGround",
            legacyRoot,
            36.0f,
            100.0f,
            new Color(0.2f, 0.3f, 0.4f),
            true);
        Check(
            CountDescendantsOfType
                <RushBreakable>(
                    legacyRoot) == 2 &&
                CountDescendantsOfType
                    <VanishingFloor>(
                        legacyRoot) == 1,
            "breakable and vanishing obstacles stay local and leave readable escape space");

        Node3D constructionRoot =
            new()
            {
                Name =
                    "ConstructionLibraryProbe"
            };
        world.AddChild(
            constructionRoot);
        string[] constructionMethods =
        {
            "CreateRhythmHurdles",
            "CreateFlowRailFan",
            "CreateWideAirLift",
            "CreateSwitchbackCanyon",
            "CreateKineticGate",
            "CreatePulseRunway",
            "CreateSkySurf",
            "CreateCrosswindSluice",
            "CreateCrawlingConvoy",
            "CreateDemolitionCorridor",
            "CreatePendulumSupports"
        };
        for (int index = 0;
            index < constructionMethods.Length;
            index++)
        {
            InvokeWorld(
                world,
                constructionMethods[index],
                constructionRoot,
                36.0f,
                index * -26.0f,
                new Color(
                    0.12f,
                    0.34f,
                    0.68f));
        }
        Check(
            constructionRoot.FindChild(
                "RhythmHurdles",
                true,
                false) != null &&
                constructionRoot.FindChild(
                    "FlowRailFan",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "AirLift",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "SwitchbackCanyon",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "KineticGate",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "PulseRunway",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "SkySurf",
                    true,
                    false) != null &&
                constructionRoot.FindChild(
                    "CrosswindSluice",
                    true,
                    false) != null,
            "construction library exposes eight readable traversal grammars");
        Check(
            CountDescendantsOfType
                <OscillatingObstacle>(
                    constructionRoot) >= 8 &&
                CountDescendantsOfType
                    <RushBreakable>(
                        constructionRoot) >= 3 &&
                CountDescendantsOfType
                    <TraversalRailZone>(
                        constructionRoot) >= 9 &&
                constructionRoot.FindChild(
                    "ConvoyBoardingPad",
                    true,
                    false) is
                    CourseLaunchPad,
            "living constructions combine movement, demolition, rails, and deliberate entry pads");

        Area3D demolitionSensor =
            constructionRoot.FindChild(
                "EnemyDemolitionSensor",
                true,
                false) as
                    Area3D;
        RushBreakable demolitionPanel =
            demolitionSensor?.GetParent()
                as RushBreakable;
        PackedScene demolitionMobScene =
            GD.Load<PackedScene>(
                "res://mob.tscn");
        Mob demolitionMob =
            demolitionMobScene
                .Instantiate<Mob>();
        AddChild(
            demolitionMob);
        demolitionSensor?.EmitSignal(
            Area3D.SignalName.BodyEntered,
            demolitionMob);
        Check(
            demolitionPanel != null &&
                demolitionPanel
                    .CollisionLayer == 0,
            "demolition enemies open the route instead of bunching against the panels");
        demolitionMob.QueueFree();

        int constructionRequests = 0;
        EnemyArchetypeId requestedArchetype =
            EnemyArchetypeId.Spear;
        world.ConstructionEnemyRequested +=
            (
                Vector3 position,
                Vector3 direction,
                int index,
                int archetype) =>
            {
                constructionRequests++;
                requestedArchetype =
                    (EnemyArchetypeId)
                        archetype;
            };
        Node3D constructionRelayRoot =
            new()
            {
                Name =
                    "ConstructionRelayProbe"
            };
        world.AddChild(
            constructionRelayRoot);
        InvokeWorld(
            world,
            "CreateConstructionRelay",
            constructionRelayRoot,
            12.0f,
            36.0f,
            new[]
            {
                new Vector3(
                    -3.0f,
                    0.0f,
                    2.0f),
                new Vector3(
                    2.0f,
                    0.0f,
                    -3.0f)
            },
            EnemyArchetypeId
                .Springboard);
        TraversalRelayTrigger
            constructionTrigger =
                constructionRelayRoot
                    .FindChild(
                        "ConstructionRelayTrigger",
                        true,
                        false) as
                    TraversalRelayTrigger;
        triggerMethod?.Invoke(
            constructionTrigger,
            new object[]
            {
                _player
            });
        Check(
            constructionRequests == 2 &&
                requestedArchetype ==
                    EnemyArchetypeId
                        .Springboard,
            "construction relay prewarms the authored enemy route with its intended archetype");

        Check(
            world.FindChild(
                    "ConstructionBrief_*",
                    true,
                    false) == null &&
                constructionRelayRoot
                    .FindChild(
                        "ConstructionRead",
                        true,
                        false) == null,
            "construction routes contain no pre-obstacle guidance UI or world-space text");

        world.QueueFree();
        await PhysicsFrames(3);
    }

    private static int CountDescendantsOfType<T>(
        Node root)
        where T : Node
    {
        int count = 0;
        foreach (Node child in root.GetChildren())
        {
            if (child is T)
            {
                count++;
            }
            count +=
                CountDescendantsOfType<T>(
                    child);
        }
        return count;
    }

    private void InvokeWorld(
        ProceduralWorld world,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method =
            typeof(ProceduralWorld)
                .GetMethod(
                    methodName,
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        if (method == null)
        {
            Check(
                false,
                $"set-piece method {methodName} exists");
            return;
        }
        method.Invoke(
            method.IsStatic
                ? null
                : world,
            arguments);
    }

    private void Check(
        bool condition,
        string message)
    {
        if (condition)
        {
            GD.Print(
                $"PASS: {message}");
            return;
        }
        _failed = true;
        GD.PushError(
            $"FAIL: {message}");
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
                SceneTree.SignalName
                    .PhysicsFrame);
        }
    }
}
