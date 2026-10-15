using Godot;
using System.Reflection;

/// <summary>
/// Places the real player immediately before the first authored construction
/// in the production main scene, then captures it through the production
/// camera and atmosphere. This complements the neutral silhouette gallery.
/// </summary>
public partial class ConstructionRunVisualReview :
    Node
{
    public override async void _Ready()
    {
        for (int frame = 0;
            frame < 4;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName
                    .ProcessFrame);
        }

        Player player =
            GetParent()
                .GetNodeOrNull<Player>(
                    "Player");
        ProceduralWorld world =
            GetParent()
                .GetNodeOrNull
                    <ProceduralWorld>(
                        "RunSystems/ProceduralWorld");
        if (!IsInstanceValid(player) ||
            !IsInstanceValid(world))
        {
            GD.PushError(
                "CONSTRUCTION_RUN_VISUAL: missing production systems");
            GetTree().Quit(1);
            return;
        }

        typeof(ProceduralWorld)
            .GetMethod(
                "EnsureGeneratedThrough",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.Invoke(
                world,
                new object[] { -1400.0f });
        Node3D construction =
            world.FindChild(
                "TunnelBoostZone",
                true,
                false) as
                    Node3D;
        if (!IsInstanceValid(construction))
        {
            GD.PushError(
                "CONSTRUCTION_RUN_VISUAL: missing production construction");
            GetTree().Quit(1);
            return;
        }

        player.GrantDamageInvulnerability(
            20.0f);
        player.GlobalPosition =
            new Vector3(
                construction.GlobalPosition.X,
                0.7f,
                construction.GlobalPosition.Z +
                    14.0f);
        player.Velocity =
            Vector3.Zero;
        for (int frame = 0;
            frame < 5;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName
                    .PhysicsFrame);
        }
        player.SetPhysicsProcess(
            false);

        for (int frame = 0;
            frame < 10;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName
                    .PhysicsFrame);
        }
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName
                .FramePostDraw);
        Control briefHud =
            GetParent()
                .GetNodeOrNull<Control>(
                    "UserInterface/ConstructionBriefHud");
        GD.Print(
            "CONSTRUCTION_RUN_VISUAL: " +
            $"brief_hud_removed={briefHud == null}");
        for (int frame = 0;
            frame < 36;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName
                    .PhysicsFrame);
        }
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName
                .FramePostDraw);
        Error result =
            GetViewport()
                .GetTexture()
                .GetImage()
                .SavePng(
                    "/tmp/no-way-down-construction-run.png");
        GD.Print(
            "CONSTRUCTION_RUN_VISUAL: " +
            $"construction={result}");
        GetTree().Quit(
            result == Error.Ok &&
                briefHud == null
                ? 0
                : 1);
    }
}
