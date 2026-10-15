using Godot;
using System.Reflection;

/// <summary>
/// Captures the production refuge and every procedural skin silhouette from
/// the wardrobe camera. This is intentionally kept out of production scenes.
/// </summary>
public partial class RefugeVisualReview : Node
{
    public override async void _Ready()
    {
        Refuge refuge =
            GetParent() as Refuge;
        if (!IsInstanceValid(refuge))
        {
            GD.PushError(
                "REFUGE_VISUAL: missing refuge");
            GetTree().Quit(1);
            return;
        }

        await WaitFrames(36);
        Error hubResult =
            await Capture(
                "/tmp/no-way-down-refuge-visual.png");

        for (int style = 0; style < CustomizationProfile.RefugeStyleCount; style++)
        {
            Invoke(refuge, "ApplyRefugeStyle", style);
            await WaitFrames(6);
            await Capture($"/tmp/nwd-refuge-style-{style}.png");
        }

        Invoke(
            refuge,
            "MoveStation",
            -1);
        await ToSignal(
            GetTree().CreateTimer(1.15f),
            SceneTreeTimer
                .SignalName
                .Timeout);

        Error skinResult =
            Error.Ok;
        FieldInfo previewField =
            typeof(Refuge)
                .GetField(
                    "_previewSkin",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        for (int skinIndex = 0;
            skinIndex <
                CustomizationProfile
                    .SkinCount;
            skinIndex++)
        {
            previewField?.SetValue(
                refuge,
                skinIndex);
            Invoke(
                refuge,
                "RefreshWardrobe");
            await WaitFrames(4);
            Error result =
                await Capture(
                    $"/tmp/no-way-down-skin-{skinIndex + 1}.png");
            if (result != Error.Ok)
            {
                skinResult = result;
            }
        }

        DisplaySettings settings =
            typeof(Refuge)
                .GetField(
                    "_fullSettings",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                ?.GetValue(refuge) as
                DisplaySettings;
        Error settingsResult =
            Error.Failed;
        Error settingsOpeningResult =
            Error.Failed;
        Error settingsDisplayResult =
            Error.Failed;
        Error settingsVsyncHoverResult =
            Error.Failed;
        Error settingsVsyncRestResult =
            Error.Failed;
        if (IsInstanceValid(settings))
        {
            settings.OpenFromExternal();
            settingsOpeningResult =
                await Capture(
                    "/tmp/no-way-down-settings-opening.png");
            await WaitFrames(12);
            settingsDisplayResult =
                await Capture(
                    "/tmp/no-way-down-settings-display-rest.png");
            CheckButton vsync =
                typeof(DisplaySettings)
                    .GetField(
                        "_vsyncCheck",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    ?.GetValue(settings) as
                    CheckButton;
            if (IsInstanceValid(vsync))
            {
                Input.WarpMouse(
                    vsync.GlobalPosition +
                    vsync.Size * 0.5f);
                await WaitFrames(5);
                settingsVsyncHoverResult =
                    await Capture(
                        "/tmp/no-way-down-settings-vsync-hover.png");
                Input.WarpMouse(
                    new Vector2(8.0f, 8.0f));
                await WaitFrames(5);
                settingsVsyncRestResult =
                    await Capture(
                        "/tmp/no-way-down-settings-vsync-rest.png");
            }
            TabContainer tabs =
                typeof(DisplaySettings)
                    .GetField(
                        "_tabs",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    ?.GetValue(settings) as
                    TabContainer;
            if (IsInstanceValid(tabs))
            {
                tabs.CurrentTab = 4;
                await WaitFrames(4);
            }
            settingsResult =
                await Capture(
                    "/tmp/no-way-down-settings-clean.png");
        }

        GD.Print(
            $"REFUGE_VISUAL_CAPTURE: {hubResult}");
        GD.Print(
            $"SKIN_VISUAL_CAPTURE: {skinResult}");
        GD.Print(
            "SETTINGS_VISUAL_CAPTURE: " +
            $"opening={settingsOpeningResult} " +
            $"display={settingsDisplayResult} " +
            $"hover={settingsVsyncHoverResult} " +
            $"rest={settingsVsyncRestResult} " +
            $"clean={settingsResult}");
        GetTree().Quit(
            hubResult == Error.Ok &&
                skinResult == Error.Ok &&
                settingsOpeningResult == Error.Ok &&
                settingsDisplayResult == Error.Ok &&
                settingsVsyncHoverResult == Error.Ok &&
                settingsVsyncRestResult == Error.Ok &&
                settingsResult == Error.Ok
                ? 0
                : 1);
    }

    private async System.Threading.Tasks
        .Task<Error> Capture(
            string path)
    {
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
        return GetViewport()
            .GetTexture()
            .GetImage()
            .SavePng(path);
    }

    private async System.Threading.Tasks
        .Task WaitFrames(
            int frameCount)
    {
        for (int frame = 0;
            frame < frameCount;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree
                    .SignalName
                    .ProcessFrame);
        }
    }

    private static void Invoke(
        object target,
        string methodName,
        params object[] arguments)
    {
        typeof(Refuge)
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.Invoke(
                target,
                arguments);
    }
}
