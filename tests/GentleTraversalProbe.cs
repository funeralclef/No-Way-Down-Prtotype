using Godot;
using System.Threading.Tasks;

/// <summary>Collision continuity, recovery floor and rendered review of both long courses.</summary>
public partial class GentleTraversalProbe : Node3D
{
    [Export] public bool HoldForReview { get; set; }
    private bool _failed;
    public override async void _Ready()
    {
        var ground = new StandardMaterial3D { AlbedoColor = new Color("#687b80"), Roughness = .7f };
        var water = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/traversal_water.gdshader") };
        var camera = new Camera3D { Position = new Vector3(39, 48, 42), Current = true, Fov = 56 };
        AddChild(camera); camera.LookAt(new Vector3(0, 0, -1));
        var light = new DirectionalLight3D { RotationDegrees = new Vector3(-52, -24, 0), ShadowEnabled = true };
        AddChild(light);
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("#162b36"), AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#b6d7e0"), AmbientLightEnergy = .6f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        for (int variant = 0; variant < 2; variant++)
        {
            var section = new Node3D(); AddChild(section);
            GentleTraversalCourse.Build(section, 36, 104, new Color("#347a77"), variant == 1, ground, water);
            await PhysicsFrames(3);
            if (variant == 0)
                for (int step = 0; step <= 120; step++)
                    foreach (float side in new[] { -3.4f, 0f, 3.4f })
                    {
                        float t = step / 120f;
                        Vector3 tangent = new Vector3(Mathf.Cos(t * Mathf.Tau) * 6 * Mathf.Tau, 0, -GentleTraversalCourse.Span).Normalized();
                        Vector3 offset = new Vector3(-tangent.Z, 0, tangent.X) * side;
                        Check(HitHeight(GentleTraversalCourse.WalkPoint(t) + offset) > -.04f,
                            "meander has an uninterrupted broad collision floor");
                    }
            else
                for (int step = 0; step <= 120; step++)
                    foreach (float side in new[] { -2.3f, 0f, 2.3f })
                        Check(HitHeight(new Vector3(13.8f + side, 0, 30 - step * .5f)) > -.04f,
                            "islands have an uninterrupted wide bypass");
            for (float z = -28; z <= 28; z += 4)
                for (float x = -16; x <= 16; x += 4)
                    Check(HitHeight(new Vector3(x, 0, z)) >= -1.15f, "missed jumps land on a shallow recovery bed");
            Check(CountBodies(section) < 50, "course uses a bounded number of static colliders");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-gentle-course-{variant}.png");
            }
            if (HoldForReview && variant == 1) break;
            section.QueueFree(); await PhysicsFrames(2);
        }
        GD.Print(_failed ? "GENTLE_TRAVERSAL_PROBE: FAILED" : "GENTLE_TRAVERSAL_PROBE: PASSED");
        if (!HoldForReview) GetTree().Quit(_failed ? 1 : 0);
    }
    private float HitHeight(Vector3 point)
    {
        var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 3, point + Vector3.Down * 3, 4);
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count > 0 ? result["position"].AsVector3().Y : float.NegativeInfinity;
    }
    private static int CountBodies(Node root)
    { int count = root is StaticBody3D ? 1 : 0; foreach (Node child in root.GetChildren()) count += CountBodies(child); return count; }
    private async Task PhysicsFrames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool value, string detail) { if (!value) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
