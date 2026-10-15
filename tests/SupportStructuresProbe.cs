using Godot;
using System.Threading.Tasks;

/// <summary>Helpful constructions activate real movement hooks and retain a clear ground route.</summary>
public partial class SupportStructuresProbe : Node3D
{
    private bool _failed;
    public override async void _Ready()
    {
        var camera = new Camera3D { Position = new Vector3(29, 29, 40), Current = true, Fov = 60 };
        AddChild(camera); camera.LookAt(new Vector3(4, 1, 0));
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-52, -20, 0), ShadowEnabled = true });
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("#152d38"), AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#b9d3d9"), AmbientLightEnergy = .7f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        var floor = new StaticBody3D { Position = Vector3.Down, CollisionLayer = 4, CollisionMask = 0 };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(36, 2, 100) } });
        floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(36, 2, 100),
            Material = new StandardMaterial3D { AlbedoColor = new Color("#314a53") } } }); AddChild(floor);
        var player = GD.Load<PackedScene>("res://player.tscn").Instantiate<Player>();
        player.GetNodeOrNull<VisibleOnScreenNotifier3D>("VisibleOnScreenNotifier3D")?.Free();
        player.Position = new Vector3(-10, 0, 10); AddChild(player);
        player.SetPhysicsProcess(false);
        int railTriggers = 0; player.TraversalRailEntered += _ => railTriggers++;
        float health = player.Health;
        for (int variant = 0; variant < 3; variant++)
        {
            var section = new Node3D(); AddChild(section);
            Node3D root = RunSupportStructures.Build(section, 36, variant, new Color("#62808b"));
            await PhysicsFrames(3);
            for (int i = 0; i <= 60; i++)
                Check(Mathf.Abs(HeightAt(new Vector3(0, 0, 30 - i))) < .03f, "center bypass stays flat and open");
            switch (variant)
            {
                case 0:
                    player.Velocity = new Vector3(2, -4, -12);
                    root.FindChild("Tailwind", true, false).EmitSignal(Area3D.SignalName.BodyEntered, player);
                    Check(railTriggers == 1 && player.Velocity.Y == -4, "wind gallery grants a rail boost while preserving the vertical arc");
                    break;
                case 1:
                    player.Velocity = new Vector3(0, -2, -12);
                    root.FindChild("BloomPad", true, false).EmitSignal(Area3D.SignalName.BodyEntered, player);
                    Check(player.Velocity.Y >= 13 && -player.Velocity.Z >= 27, "bloom pad supplies useful forward and upward momentum");
                    break;
                case 2:
                    for (int i = 0; i <= 60; i++)
                    {
                        float z = 30 - i;
                        float expected = Mathf.Abs(z) <= 10 ? 2.4f : (30 - Mathf.Abs(z)) * .12f;
                        Check(HeightAt(new Vector3(7, 0, z)) >= expected - .08f, "bridge ramps and deck join continuously");
                    }
                    break;
            }
            Check(player.Health == health, "helpful constructions never deal damage");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-support-{variant}.png");
            }
            section.QueueFree(); await PhysicsFrames(2);
        }
        GD.Print(_failed ? "SUPPORT_STRUCTURES_PROBE: FAILED" : "SUPPORT_STRUCTURES_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }
    private float HeightAt(Vector3 point)
    {
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 7, point + Vector3.Down * 2, 4));
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NegativeInfinity;
    }
    private async Task PhysicsFrames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool value, string detail) { if (!value) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
