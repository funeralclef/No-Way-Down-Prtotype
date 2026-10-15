using Godot;

/// <summary>
/// Forgiving, extended traversal beats. The shallow bed catches missed jumps;
/// broad banks and an uninterrupted bypass keep either route recoverable.
/// Decoration has no collision and never obscures the landing surfaces.
/// </summary>
public static class GentleTraversalCourse
{
    public const float Span = 60;
    public const float WalkWidth = 8;
    public static Vector3 WalkPoint(float t) => new(Mathf.Sin(t * Mathf.Tau) * 6, 0, Span * (.5f - t));

    public static void Build(Node3D section, float width, float length, Color color, bool islands,
        Material ground, Material water)
    {
        var root = new Node3D { Name = islands ? "IslandPromenade" : "MeanderWalk" };
        root.SetMeta("reserve_obstacle_z", true);
        root.SetMeta("course_span", Span);
        section.AddChild(root);
        float bankLength = (length - Span) * .5f;
        foreach (float sign in new[] { -1f, 1f })
            Slab(root, "Bank", new Vector3(0, 0, sign * (Span * .5f + bankLength * .5f)),
                new Vector3(width, 2, bankLength + .3f), ground);
        // A real, shallow solid bed: a miss costs momentum, never health or a restart.
        Slab(root, "ShallowBed", new Vector3(0, -1.1f, 0), new Vector3(width, 2, Span + .2f), ground);
        root.AddChild(new MeshInstance3D { Name = "ShallowWater", Position = new Vector3(0, -.42f, 0),
            Mesh = new PlaneMesh { Size = new Vector2(width - .3f, Span), SubdivideWidth = 20,
                SubdivideDepth = 40, Material = water } });
        var trim = new StandardMaterial3D { AlbedoColor = new Color(.43f, .83f, .8f),
            EmissionEnabled = true, Emission = new Color(.16f, .48f, .46f), EmissionEnergyMultiplier = .25f };
        if (islands) BuildIslands(root, width, ground, trim);
        else BuildMeander(root, ground, trim);
        BuildReeds(root, width, color);
        if (islands)
        {
            var pad = new KineticJumpPad { Name = "IslandBloom", Position = new Vector3(0, .65f, 26), CollisionLayer = 0, CollisionMask = 1,
                BaseForwardSpeed = 24, BaseVerticalSpeed = 12, BoostDuration = 1.0f };
            pad.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2, Height = 1.2f } });
            root.AddChild(pad);
            root.AddChild(new MeshInstance3D { Name = "IslandBloomRing", Position = new Vector3(0, .14f, 26),
                Mesh = new TorusMesh { InnerRadius = 1.7f, OuterRadius = 1.95f, Rings = 24, RingSegments = 8, Material = trim } });
        }
    }

    private static void BuildMeander(Node3D root, Material ground, Material trim)
    {
        const int segments = 40;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(ground);
        var left = new Vector3[segments + 1];
        var right = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = WalkPoint(t);
            Vector3 tangent = new Vector3(Mathf.Cos(t * Mathf.Tau) * 6 * Mathf.Tau, 0, -Span).Normalized();
            Vector3 side = new Vector3(-tangent.Z, 0, tangent.X) * (WalkWidth * .5f);
            left[i] = point - side; right[i] = point + side;
        }
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = WalkPoint(i / (float)segments), b = WalkPoint((i + 1f) / segments);
            Vector3 direction = (b - a).Normalized();
            Vector3 across = new Vector3(-direction.Z, 0, direction.X) * (WalkWidth * .5f);
            var slab = Slab(root, "WalkCollider", (a + b) * .5f, new Vector3(WalkWidth + .15f, 2, a.DistanceTo(b) + .5f), ground, false);
            slab.Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
            foreach (Vector3 point in new[] { left[i], left[i + 1], right[i], right[i], left[i + 1], right[i + 1] })
            { surface.SetNormal(Vector3.Up); surface.SetUV(new Vector2(point.X, point.Z) * .1f); surface.AddVertex(point); }
            if (i % 3 != 0) continue;
            foreach (float sign in new[] { -1f, 1f })
                Visual(root, "EdgeInlay", (a + b) * .5f + across * sign + Vector3.Up * .025f,
                    new Vector3(.09f, .035f, 1.1f), trim, slab.Rotation.Y);
        }
        root.AddChild(new MeshInstance3D { Name = "ContinuousWalk", Mesh = surface.Commit() });
    }

    private static void BuildIslands(Node3D root, float width, Material ground, Material trim)
    {
        // Wide optional stepping stones; an obvious continuous path runs alongside.
        float bypassX = width * .5f - 4.2f;
        Slab(root, "Bypass", new Vector3(bypassX, 0, 0), new Vector3(5.8f, 2, Span + .3f), ground);
        for (int i = 0; i < 7; i++)
        {
            float z = 27 - i * 9;
            float x = i == 0 || i == 6 ? 0 : (i % 2 == 0 ? -2.8f : 2.8f);
            var island = new StaticBody3D { Name = "LandingIsland", Position = new Vector3(x, -.9f, z), CollisionLayer = 4, CollisionMask = 0 };
            root.AddChild(island);
            island.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 4.4f, Height = 2 } });
            island.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 4.4f, BottomRadius = 4.6f,
                Height = 2, RadialSegments = 32, Material = ground } });
            root.AddChild(new MeshInstance3D { Name = "LandingRim", Position = new Vector3(x, .12f, z),
                Mesh = new TorusMesh { InnerRadius = 4.05f, OuterRadius = 4.12f, Rings = 32, RingSegments = 6, Material = trim } });
        }
        for (int i = 0; i < 12; i++)
            Visual(root, "BypassInlay", new Vector3(bypassX - 2.4f, .025f, 27 - i * 5), new Vector3(.1f, .035f, 1.4f), trim);
    }

    private static void BuildReeds(Node3D root, float width, Color color)
    {
        var reed = new StandardMaterial3D { AlbedoColor = color.Lightened(.22f), Roughness = .85f };
        for (int i = 0; i < 18; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * (width * .5f - .9f);
            float z = 27 - (i / 2) * 6.7f;
            float height = .65f + i % 3 * .22f;
            Visual(root, "Reed", new Vector3(x, -.35f + height * .5f, z), new Vector3(.12f, height, .16f), reed, .15f * (i % 3 - 1));
        }
    }

    private static StaticBody3D Slab(Node3D parent, string name, Vector3 top, Vector3 size, Material material, bool visible = true)
    {
        var slab = new StaticBody3D { Name = name, Position = top - Vector3.Up * (size.Y * .5f), CollisionLayer = 4, CollisionMask = 0 };
        parent.AddChild(slab);
        slab.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (visible) slab.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = material } });
        return slab;
    }

    private static void Visual(Node3D parent, string name, Vector3 position, Vector3 size, Material material, float yaw = 0)
    {
        parent.AddChild(new MeshInstance3D { Name = name, Position = position, Rotation = new Vector3(0, yaw, 0),
            Mesh = new BoxMesh { Size = size, Material = material }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
