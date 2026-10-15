using Godot;
using System.Reflection;

/// <summary>
/// Exercises the production refuge-to-run handoff and records the last hub
/// frame plus the first rendered run frame.
/// </summary>
public partial class RefugeTransitionProbe : Node
{
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Refuge refuge = GetParent() as Refuge;
        if (!IsInstanceValid(refuge))
        {
            GD.PushError("REFUGE_TRANSITION: missing refuge");
            GetTree().Quit(1);
            return;
        }

        SceneTree tree = GetTree();
        Callable.From(
            () => Reparent(tree.Root))
            .CallDeferred();
        await ToSignal(
            tree,
            SceneTree.SignalName.ProcessFrame);
        await WaitFrames(18);
        ulong startedAt = Time.GetTicksMsec();
        Invoke(refuge, "BeginDescent");

        await ToSignal(
            tree.CreateTimer(0.12f),
            SceneTreeTimer.SignalName.Timeout);
        Error transitionCapture = await Capture(
            "/tmp/no-way-down-transition-mid.png");

        int safety = 0;
        while (tree.CurrentScene is not Main &&
            safety < 240)
        {
            await ToSignal(
                tree,
                SceneTree.SignalName.ProcessFrame);
            safety++;
        }

        await WaitFrames(2);
        ulong elapsed =
            Time.GetTicksMsec() - startedAt;
        Error runCapture = await Capture(
            "/tmp/no-way-down-run-first-frame.png");
        bool passed =
            tree.CurrentScene is Main &&
            transitionCapture == Error.Ok &&
            runCapture == Error.Ok &&
            elapsed < 12000u;
        GD.Print(
            $"REFUGE_TRANSITION: elapsed={elapsed}ms " +
            $"mid={transitionCapture} run={runCapture}");
        tree.Quit(passed ? 0 : 1);
    }

    private async System.Threading.Tasks.Task<Error>
        Capture(string path)
    {
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw);
        return GetViewport()
            .GetTexture()
            .GetImage()
            .SavePng(path);
    }

    private async System.Threading.Tasks.Task
        WaitFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.ProcessFrame);
        }
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
