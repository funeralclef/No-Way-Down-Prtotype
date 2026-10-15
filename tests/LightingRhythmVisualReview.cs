using Godot;

/// <summary>
/// Captures the light, dark, and returning-light beats of the production run
/// atmosphere. The player is moved through one lighting cycle while the real
/// camera, ground shader, fog, and particles remain active.
/// </summary>
public partial class LightingRhythmVisualReview :
    Node
{
    private readonly float[] _reviewZ =
    {
        -14.0f,
        -78.0f,
        -140.0f
    };

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
        if (!IsInstanceValid(player))
        {
            GetTree().Quit(1);
            return;
        }
        player.GrantDamageInvulnerability(
            30.0f);
        player.SetPhysicsProcess(
            false);
        CanvasItem interfaceRoot =
            GetParent()
                .GetNodeOrNull
                    <CanvasItem>(
                        "UserInterface");
        if (interfaceRoot != null)
        {
            interfaceRoot.Hide();
        }

        Error result =
            Error.Ok;
        for (int zone = 0;
            zone < _reviewZ.Length;
            zone++)
        {
            player.GlobalPosition =
                new Vector3(
                    0.0f,
                    0.7f,
                    _reviewZ[zone]);
            for (int frame = 0;
                frame < 40;
                frame++)
            {
                await ToSignal(
                    GetTree(),
                    SceneTree.SignalName
                        .PhysicsFrame);
            }
            await ToSignal(
                RenderingServer.Singleton,
                RenderingServer
                    .SignalName
                    .FramePostDraw);
            Error frameResult =
                GetViewport()
                    .GetTexture()
                    .GetImage()
                    .SavePng(
                        $"/tmp/no-way-down-light-zone-{zone + 1}.png");
            GD.Print(
                $"LIGHT_ZONE_{zone + 1}: {frameResult}");
            if (frameResult !=
                Error.Ok)
            {
                result =
                    frameResult;
            }
        }

        GetTree().Quit(
            result == Error.Ok
                ? 0
                : 1);
    }
}
