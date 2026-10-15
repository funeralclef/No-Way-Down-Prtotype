using Godot;
using System.Reflection;

/// <summary>
/// Captures the physical four-arena rail and the optional continuation choice
/// from the real run scene in both desktop and portrait review runs.
/// </summary>
public partial class RunArcVisualReview : Node
{
    public override async void _Ready()
    {
        ProcessMode =
            ProcessModeEnum.Always;
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        bool forcePortrait =
            System.Array.Exists(
                OS.GetCmdlineUserArgs(),
                argument =>
                    argument.Equals(
                        "portrait",
                        System.StringComparison
                            .OrdinalIgnoreCase));
        if (forcePortrait)
        {
            GetWindow().Mode =
                Window.ModeEnum.Windowed;
            GetWindow().Size =
                new Vector2I(720, 1280);
            GetWindow().ContentScaleMode =
                Window.ContentScaleModeEnum.CanvasItems;
            GetWindow().ContentScaleSize =
                new Vector2I(720, 540);
            GetWindow().ContentScaleAspect =
                Window.ContentScaleAspectEnum.Expand;
            await WaitFrames(4);
        }
        Main game = GetParent() as Main;
        Player player =
            game?.GetNodeOrNull<Player>(
                "Player");
        ProceduralWorld world =
            game?.GetNodeOrNull<ProceduralWorld>(
                "RunSystems/ProceduralWorld");
        if (!IsInstanceValid(game) ||
            !IsInstanceValid(player) ||
            !IsInstanceValid(world))
        {
            GD.PushError(
                "RUN_ARC_VISUAL: missing live run nodes");
            GetTree().Quit(1);
            return;
        }

        player.GrantDamageInvulnerability(
            20.0f);
        if (world.TryGetArenaSpan(
                1,
                out _,
                out float firstEntry,
                out _))
        {
            Vector3 position =
                player.GlobalPosition;
            position.Z = Mathf.Lerp(
                world.RunStartZ,
                firstEntry,
                0.46f);
            player.GlobalPosition = position;
        }
        await WaitSeconds(1.1f);
        string layout = forcePortrait ||
            GetViewport()
                .GetVisibleRect()
                .Size.Y >
            GetViewport()
                .GetVisibleRect()
                .Size.X
                    ? "portrait"
                    : "landscape";
        Error hudCapture =
            await Capture(
                $"/tmp/no-way-down-run-progress-{layout}.png");

        game.GetNode<Label>(
                "UserInterface/LocationBanner/Margin/Label")
            .Text = "WAVE 2 / 4";
        Invoke(
            game,
            "ShowArenaBanner");
        await WaitSeconds(0.72f);
        Error arenaCapture =
            await Capture(
                $"/tmp/no-way-down-arena-banner-{layout}.png");

        for (int ordinal = 1;
            ordinal <= 3;
            ordinal++)
        {
            if (world.TryGetArenaSpan(
                    ordinal,
                    out int section,
                    out _,
                    out _))
            {
                world.CompleteArena(section);
            }
        }
        world.TryGetArenaSpan(
            4,
            out int finalSection,
            out _,
            out _);
        WriteField(
            game,
            "_activeArenaSection",
            finalSection);
        Invoke(
            game,
            "CompleteArenaEncounter");
        await WaitSeconds(0.72f);
        Error finaleCapture =
            await Capture(
                $"/tmp/no-way-down-run-finale-{layout}.png");

        GD.Print(
            $"RUN_ARC_VISUAL_CAPTURE: HUD={hudCapture} " +
            $"ARENA={arenaCapture} FINALE={finaleCapture}");
        GetTree().Quit(
            hudCapture == Error.Ok &&
                arenaCapture == Error.Ok &&
                finaleCapture == Error.Ok
                ? 0
                : 1);
    }

    private async System.Threading.Tasks.Task
        WaitFrames(int count)
    {
        for (int frame = 0;
            frame < count;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.ProcessFrame);
        }
    }

    private async System.Threading.Tasks.Task<Error>
        Capture(string path)
    {
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw);
        return GetViewport()
            .GetTexture()
            .GetImage()
            .SavePng(path);
    }

    private async System.Threading.Tasks.Task
        WaitSeconds(float seconds)
    {
        await ToSignal(
            GetTree().CreateTimer(
                Mathf.Max(0.01f, seconds),
                true),
            SceneTreeTimer.SignalName.Timeout);
    }

    private static void WriteField<T>(
        object target,
        string fieldName,
        T value)
    {
        target.GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance |
                    BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static void Invoke(
        object target,
        string methodName)
    {
        target.GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                    BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }
}
