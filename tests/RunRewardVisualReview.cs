using Godot;

/// <summary>
/// Render review for both post-arena stages and the compact phone layout.
/// This scene is never part of production; it keeps regression screenshots
/// reproducible without playing through several complete arenas.
/// </summary>
public partial class RunRewardVisualReview : Node
{
    [Export] public bool Portrait { get; set; }
    private ArenaRewardOverlay _overlay;
    private RunProgression _progression;

    public override async void _Ready()
    {
        if (Portrait)
        {
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = new Vector2I(390, 844);
            GetWindow().ContentScaleSize = new Vector2I(390, 844);
            await WaitFrames(3);
        }
        CreateBackdrop();
        _progression = new RunProgression(0xA11CEUL);
        _progression.BeginArenaDraft();
        _overlay = GD.Load<PackedScene>("res://ui/arena_reward_overlay.tscn")
            .Instantiate<ArenaRewardOverlay>();
        _overlay.Initialize(_progression);
        AddChild(_overlay);
        _overlay.ShowRewardStage();

        await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
        Vector2 viewport =
            GetViewport().GetVisibleRect().Size;
        string layout =
            viewport.Y > viewport.X
                ? "portrait"
                : "landscape";
        Error rewardResult = await Capture(
            $"/tmp/no-way-down-reward-{layout}.png");

        _overlay._UnhandledInput(
            new InputEventAction
            {
                Action = "ui_accept",
                Pressed = true
            });
        await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
        Error routeResult = await Capture(
            $"/tmp/no-way-down-route-{layout}.png");

        _overlay._UnhandledInput(
            new InputEventAction
            {
                Action = "ui_accept",
                Pressed = true
            });
        await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);

        GD.Print(
            "RUN_REWARD_VISUAL_CAPTURE: " +
            $"reward={rewardResult}, " +
            $"route={routeResult}, " +
            $"layout={layout}, size={viewport}");
        GetTree().Quit(
            rewardResult == Error.Ok &&
            routeResult == Error.Ok
                ? 0
                : 1);
    }

    private void CreateBackdrop()
    {
        ColorRect background = new()
        {
            Name = "MockArena",
            Color = new Color(
                0.22f,
                0.52f,
                0.62f)
        };
        background.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        AddChild(background);

        Label horizon = new()
        {
            Text = "ARENA CLEAR  //  ASCENT CONTINUES",
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center,
            Modulate = new Color(
                1.0f,
                1.0f,
                1.0f,
                0.32f)
        };
        horizon.AddThemeFontSizeOverride(
            "font_size",
            28);
        horizon.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        background.AddChild(horizon);
    }

    private async System.Threading.Tasks.Task
        WaitFrames(int count)
    {
        for (int index = 0; index < count; index++)
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

}
