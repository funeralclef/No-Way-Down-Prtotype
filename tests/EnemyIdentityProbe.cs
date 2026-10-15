using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Production enemy silhouettes, independent palettes, and a render at the gameplay viewing angle.</summary>
public partial class EnemyIdentityProbe : Node3D
{
    private bool _failed;
    public override async void _Ready()
    {
        var camera = new Camera3D { Position = new Vector3(0, 20, 23), Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 23, Current = true };
        AddChild(camera); camera.LookAt(new Vector3(0, .6f, 0));
        AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("#172c35"), AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#bbcdd6"), AmbientLightEnergy = .7f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -25, 0), ShadowEnabled = true });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.12f, 0), Mesh = new BoxMesh { Size = new Vector3(25, .2f, 23),
            Material = new StandardMaterial3D { AlbedoColor = new Color("#31434a"), Roughness = .8f } } });
        var signatures = new HashSet<string>();
        int index = 0;
        foreach (EnemyArchetypeId id in Enum.GetValues<EnemyArchetypeId>())
        {
            var mob = GD.Load<PackedScene>("res://mob.tscn").Instantiate<Mob>();
            mob.GetNode<VisibleOnScreenNotifier3D>("VisibleOnScreenNotifier3D").Free();
            mob.SetPhysicsProcess(false); mob.ConfigureArchetype(id);
            Vector3 point = new((index % 3 - 1) * 7, .36f, (index / 3 - 1) * 6.5f);
            mob.Position = point; AddChild(mob); mob.SetPhysicsProcess(false);
            mob.GetNode<Node3D>("Pivot").RotationDegrees = new Vector3(0, 180, 0);
            var identity = mob.GetNode<ModelVisualRoot>("Pivot/Character").ModelRoot;
            string signature = identity.SceneFilePath;
            Check(signature == EnemyVisualIdentity.ModelPath(id), "role uses its own authored model scene");
            Check(identity.GetNodeOrNull<Node3D>("Eyes/Blink/Sclera") != null, "shared cyclops face concept is retained");
            signatures.Add(signature);
            Check(CountPhysics(identity) == 0, "identity geometry does not add collision");
            var bodyMaterials = new HashSet<ulong>();
            foreach (var mesh in ModelMeshes(mob.GetNode<Node3D>("Pivot/Character/Model")))
                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                    bodyMaterials.Add(mesh.GetActiveMaterial(surface).GetInstanceId());
            Check(bodyMaterials.Count >= 3, "body, eye and pupil remain separate on " + id);
            AddChild(new Label3D { Text = EnemyArchetypeLibrary.Get(id).DisplayName, Position = point + new Vector3(0, .15f, 1.8f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, PixelSize = .009f, Modulate = new Color("#dce9ee") });
            index++;
        }
        Check(signatures.Count == 9, "all nine roles have unique silhouette grammar");
        var sample = GetNode<Mob>("Mob");
        var collision = sample.GetNode<CollisionShape3D>("CollisionShape3D");
        Vector3 collisionPosition = collision.Position;
        foreach (EnemyArchetypeId role in new[] { EnemyArchetypeId.Shell, EnemyArchetypeId.Trickster, EnemyArchetypeId.Springboard })
        {
            sample.ConfigureArchetype(role);
            var adapter = sample.GetNode<ModelVisualRoot>("Pivot/Character");
            Check(adapter.ModelRoot.SceneFilePath == EnemyVisualIdentity.ModelPath(role) && adapter.GetChildCount() == 1,
                "reconfiguration replaces one model without leaving duplicate geometry");
            Check(collision.Position == collisionPosition, "model reconfiguration preserves gameplay collision");
        }
        for (int frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (DisplayServer.GetName() != "headless")
        {
            await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/nwd-enemy-identities.png");
        }
        GD.Print(_failed ? "ENEMY_IDENTITY_PROBE: FAILED" : "ENEMY_IDENTITY_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }
    private static IEnumerable<MeshInstance3D> ModelMeshes(Node root)
    { if (root is MeshInstance3D mesh) yield return mesh; foreach (Node child in root.GetChildren()) foreach (var nested in ModelMeshes(child)) yield return nested; }
    private static int CountPhysics(Node root)
    { int count = root is CollisionObject3D ? 1 : 0; foreach (Node child in root.GetChildren()) count += CountPhysics(child); return count; }
    private void Check(bool value, string detail) { if (!value) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
