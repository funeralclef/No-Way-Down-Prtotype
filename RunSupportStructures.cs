using Godot;

/// <summary>Optional momentum playgrounds. Center lane stays open; decorative arches never collide.</summary>
public static class RunSupportStructures
{
    public static Node3D Build(Node3D section, float width, int variant, Color color)
    {
        variant = Mathf.PosMod(variant, 3);
        var root = new Node3D { Name = variant switch { 0 => "SlipstreamGallery", 1 => "BloomLaunch", _ => "RibbonBridge" },
            Position = new Vector3((variant == 1 ? -1 : 1) * Mathf.Min(7, width * .21f), 0, 0) };
        root.SetMeta("reserve_obstacle_z", true);
        section.AddChild(root);
        var frame = new StandardMaterial3D { AlbedoColor = color.Lightened(.14f), Metallic = .3f, Roughness = .55f };
        var trim = new StandardMaterial3D { AlbedoColor = new Color("#7ce1cb"), EmissionEnabled = true,
            Emission = new Color("#2ea992"), EmissionEnergyMultiplier = .6f, Roughness = .38f };
        switch (variant)
        {
            case 0:
                FlowLane(root, new Vector3(0, .035f, 0), new Vector2(6.6f, 64));
                for (int i = 0; i < 4; i++)
                {
                    float z = 25 - i * 16;
                    // Broad open frames indicate a beneficial lane without making a tunnel collision trap.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(root, "WindPylon", new Vector3(side * 4.2f, 2.2f, z), new Vector3(.28f, 4.4f, .5f), frame);
                        Box(root, "WindInlay", new Vector3(side * 4.2f, 3.4f, z - .28f), new Vector3(.15f, 1.8f, .07f), trim);
                    }
                    Box(root, "WindArch", new Vector3(0, 4.4f, z), new Vector3(8.7f, .24f, .5f), frame);
                    Rail(root, "Tailwind", new Vector3(0, 1.1f, z), new Vector3(6.6f, 2.2f, 3), 26, .9f);
                }
                break;
            case 1:
                foreach (float z in new[] { 23f, -19f })
                {
                    var pad = new KineticJumpPad { Name = "BloomPad", Position = new Vector3(0, .55f, z), CollisionLayer = 0,
                        CollisionMask = 1, BaseForwardSpeed = 27, BaseVerticalSpeed = 13, BoostDuration = 1.1f };
                    pad.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.5f, Height = 1.2f } });
                    // Build the visual before adding the pad, so its ready hook can bind the pulse.
                    pad.AddChild(new MeshInstance3D { Name = "ImpactPulse", Position = new Vector3(0, -.48f, 0),
                        Mesh = new TorusMesh { InnerRadius = 2.2f, OuterRadius = 2.5f, Rings = 32, RingSegments = 8, Material = trim } });
                    root.AddChild(pad);
                    root.AddChild(new MeshInstance3D { Name = "BloomCore", Position = new Vector3(0, .06f, z),
                        Mesh = new CylinderMesh { TopRadius = 2.3f, BottomRadius = 2.6f, Height = .12f, RadialSegments = 24, Material = frame } });
                    for (int petal = 0; petal < 6; petal++)
                    {
                        float angle = petal * Mathf.Tau / 6;
                        root.AddChild(new MeshInstance3D { Name = "BloomPetal", Position = new Vector3(Mathf.Cos(angle) * 2.55f, .08f, z + Mathf.Sin(angle) * 2.55f),
                            Rotation = new Vector3(0, -angle, 0), Mesh = new PrismMesh { Size = new Vector3(.9f, .16f, 1.15f), Material = trim } });
                    }
                }
                FlowLane(root, new Vector3(0, .035f, 0), new Vector2(4.5f, 34));
                break;
            case 2:
                BridgeSlab(root, "EntryRamp", new Vector3(0, 1.2f, 20), new Vector3(7, .6f, 20.65f), Mathf.Atan2(2.4f, 20), frame);
                BridgeSlab(root, "SkyDeck", new Vector3(0, 2.4f, 0), new Vector3(7, .6f, 20.4f), 0, frame);
                BridgeSlab(root, "ExitRamp", new Vector3(0, 1.2f, -20), new Vector3(7, .6f, 20.65f), -Mathf.Atan2(2.4f, 20), frame);
                FlowLane(root, new Vector3(0, 2.435f, 0), new Vector2(6.3f, 19));
                Rail(root, "BridgeEntryFlow", new Vector3(0, 1, 31), new Vector3(6.5f, 2, 3), 24, .6f);
                var reward = new MomentumZone { Name = "BridgeExitCharge", Position = new Vector3(0, .9f, -31),
                    CollisionLayer = 0, CollisionMask = 1, MomentumReward = 12 };
                reward.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(7, 2, 3) } }); root.AddChild(reward);
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(root, "BridgeEdge", new Vector3(side * 3.35f, 2.44f, 0), new Vector3(.09f, .05f, 20), trim);
                    for (int i = 0; i < 4; i++)
                        Box(root, "BridgeSupport", new Vector3(side * 3.1f, 1.1f, 8 - i * 5), new Vector3(.28f, 2.2f, .35f), frame);
                }
                break;
        }
        return root;
    }

    private static void Rail(Node3D root, string name, Vector3 point, Vector3 size, float speed, float duration)
    {
        var zone = new TraversalRailZone { Name = name, Position = point, MinimumSpeed = speed, RideDuration = duration,
            SteeringAuthority = .68f, CollisionLayer = 0, CollisionMask = 1 };
        zone.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); root.AddChild(zone);
    }
    private static void FlowLane(Node3D root, Vector3 point, Vector2 size)
    {
        root.AddChild(new MeshInstance3D { Name = "FlowInlay", Position = point, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new PlaneMesh { Size = size, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/flow_lane.gdshader") } } });
    }
    private static void BridgeSlab(Node3D root, string name, Vector3 top, Vector3 size, float pitch, Material material)
    {
        var body = new StaticBody3D { Name = name, Position = top - Vector3.Up * .3f, Rotation = new Vector3(pitch, 0, 0),
            CollisionLayer = 4, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = material } }); root.AddChild(body);
    }
    private static void Box(Node3D root, string name, Vector3 point, Vector3 size, Material material) =>
        root.AddChild(new MeshInstance3D { Name = name, Position = point, Mesh = new BoxMesh { Size = size, Material = material } });
}
