using Godot;

public partial class RunEndOverlayProbe : Node
{
    private bool _failed;
    private int _retryRequests;
    private int _refugeRequests;

    public override async void _Ready()
    {
        PackedScene scene = GD.Load<PackedScene>(
            "res://ui/run_end_overlay.tscn");
        RunEndOverlay overlay =
            scene.Instantiate<RunEndOverlay>();
        AddChild(overlay);
        overlay.RetryRequested += () => _retryRequests++;
        overlay.RefugeRequested += () => _refugeRequests++;
        await ProcessFrames(2);

        overlay.ShowResults(123, 456, false);
        await ProcessFrames(2);
        Label subtitle = overlay.GetNode<Label>(
            "Card/Margin/Content/Subtitle");
        Label score = overlay.GetNode<Label>(
            "Card/Margin/Content/Results/Score/Value");
        Label best = overlay.GetNode<Label>(
            "Card/Margin/Content/Results/Best/Value");
        Control badge = overlay.GetNode<Control>(
            "Card/Margin/Content/RecordBadge");
        Button retry = overlay.GetNode<Button>(
            "Card/Margin/Content/Actions/RetryRun");
        Button refuge = overlay.GetNode<Button>(
            "Card/Margin/Content/Actions/ReturnToRefuge");

        Check(
            overlay.Visible &&
            subtitle.Text == "THE CREEPS GOT YOU" &&
            score.Text == "123" &&
            best.Text == "456" &&
            !badge.Visible,
            "ordinary result content is bound by the overlay");
        Check(
            GetViewport().GuiGetFocusOwner() == retry,
            "desktop result focus begins on Retry");

        InputEventAction accept = Action("ui_accept");
        Check(
            overlay.HandleInput(accept) &&
            _retryRequests == 1 &&
            _refugeRequests == 0,
            "accept on Retry emits exactly one retry intent");
        refuge.GrabFocus();
        await ProcessFrames(1);
        overlay.HandleInput(accept);
        Check(
            _retryRequests == 1 &&
            _refugeRequests == 1,
            "accept on Refuge emits exactly one refuge intent");
        overlay.HandleInput(Action("ui_cancel"));
        Check(
            _refugeRequests == 2,
            "cancel emits refuge intent");

        retry.EmitSignal(BaseButton.SignalName.Pressed);
        refuge.EmitSignal(BaseButton.SignalName.Pressed);
        Check(
            _retryRequests == 2 &&
            _refugeRequests == 3,
            "button presses emit their component signals");

        overlay.ShowResults(789, 789, true);
        await ProcessFrames(2);
        Check(
            subtitle.Text == "THAT WAS A GREAT RUN" &&
            score.Text == "789" &&
            best.Text == "789" &&
            badge.Visible,
            "record result content is bound by the overlay");

        Rect2 cardRect = overlay.GetNode<Control>("Card")
            .GetGlobalRect();
        Vector2 viewportSize =
            GetViewport().GetVisibleRect().Size;
        Check(
            cardRect.Position.X >= -1.0f &&
            cardRect.Position.Y >= -1.0f &&
            cardRect.End.X <= viewportSize.X + 1.0f &&
            cardRect.End.Y <= viewportSize.Y + 1.0f,
            "responsive card stays inside the viewport");

        overlay.QueueFree();
        await ProcessFrames(2);
        GD.Print(
            _failed
                ? "RUN_END_OVERLAY_PROBE: FAILED"
                : "RUN_END_OVERLAY_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }

    private static InputEventAction Action(string action)
    {
        return new InputEventAction
        {
            Action = action,
            Pressed = true,
            Strength = 1.0f
        };
    }

    private async System.Threading.Tasks.Task
        ProcessFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            GD.Print($"PASS: {message}");
            return;
        }
        _failed = true;
        GD.PushError($"FAIL: {message}");
    }
}
