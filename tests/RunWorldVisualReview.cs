using Godot;

/// <summary>
/// Short rendered pass through the real main scene. It exists so changes to
/// the run grade, procedural road shader, and atmosphere are reviewed together.
/// </summary>
public partial class RunWorldVisualReview : Node
{
    public override async void _Ready()
    {
        Player player =
            GetParent()
                .GetNodeOrNull<Player>(
                    "Player");
        if (!IsInstanceValid(player))
        {
            GD.PushError(
                "RUN_WORLD_VISUAL: missing player");
            GetTree().Quit(1);
            return;
        }

        player.GrantDamageInvulnerability(
            12.0f);
        Input.ActionPress(
            "move_forward",
            0.82f);
        for (int frame = 0;
            frame < 110;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree
                    .SignalName
                    .PhysicsFrame);
        }
        Input.ActionRelease(
            "move_forward");
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
                    "/tmp/no-way-down-run-world-visual.png");
        GD.Print(
            $"RUN_WORLD_VISUAL_CAPTURE: {result}");
        GetTree().Quit(
            result == Error.Ok
                ? 0
                : 1);
    }

    public override void _ExitTree()
    {
        Input.ActionRelease(
            "move_forward");
    }
}
