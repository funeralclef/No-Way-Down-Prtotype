using Godot;
using System.Reflection;

/// <summary>
/// Guards the authored refuge UI contract and its production controller
/// bindings for navigation, settings, and collapse/restore.
/// </summary>
public partial class RefugeInterfaceProbe : Node
{
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Refuge refuge = GetParent() as Refuge;
        if (!IsInstanceValid(refuge))
        {
            Fail("missing production refuge");
            return;
        }

        await WaitFrames(3);
        RefugeInterface view =
            refuge.GetNodeOrNull<RefugeInterface>(
                "Interface/Root");
        RefugeInterface authored =
            refuge.GetNodeOrNull<RefugeInterface>(
                "AuthoredContract");
        bool valid =
            IsInstanceValid(view) &&
            view.HasValidContract() &&
            IsInstanceValid(authored) &&
            authored.HasValidContract();
        if (!valid)
        {
            Fail("typed scene contract is incomplete");
            return;
        }

        Button[] stations = authored.StationButtons;
        bool stationOrder =
            stations.Length == 4 &&
            stations[0] == authored.WardrobeButton &&
            stations[1] == authored.RefugeButton &&
            stations[2] == authored.LoadoutButton &&
            stations[3] == authored.DescentButton;
        bool initialVisibility =
            !authored.StationPanel.Visible &&
            !authored.WardrobeButton.Visible &&
            !authored.RefugeButton.Visible &&
            !authored.LoadoutButton.Visible &&
            !authored.DescentButton.Visible &&
            !authored.SettingsButton.Visible && !authored.InfoButton.Visible &&
            !authored.ToggleInterfaceButton.Visible &&
            !authored.MobilePreviousButton.Visible &&
            !authored.MobileNextButton.Visible;
        Shader shader =
            (authored.FocusVeil.Material as ShaderMaterial)
                ?.Shader;
        bool authoredVeil =
            shader?.ResourcePath ==
            "res://shaders/refuge_focus_veil.gdshader";
        bool settingsLayering =
            authored.SettingsHost.GetIndex() >
            authored.Fade.GetIndex();
        bool deadNodesRemoved =
            authored.FindChild(
                "FilteredWorldBackdrop",
                true,
                false) == null &&
            authored.FindChild(
                "ProjectionScanline",
                true,
                false) == null &&
            authored.FindChild(
                "*ProjectionLink",
                true,
                false) == null;

        string stationBefore =
            ReadPrivate(refuge, "_currentStation")
                ?.ToString();
        Invoke(refuge, "MoveStation", -1);
        await WaitFrames(2);
        string stationAfter =
            ReadPrivate(refuge, "_currentStation")
                ?.ToString();
        bool navigation =
            stationBefore == "Shelter" &&
            stationAfter == "Wardrobe";

        DisplaySettings settings =
            ReadPrivate(refuge, "_fullSettings") as
            DisplaySettings;
        bool settingsAttached =
            IsInstanceValid(settings) &&
            settings.GetParent() == view.SettingsHost;
        Invoke(refuge, "OpenSettings");
        await WaitFrames(2);
        bool settingsOpen =
            settingsAttached &&
            settings.IsMenuOpen;
        Invoke(
            settings,
            "CloseMenuWithoutConfirmation");
        await ToSignal(
            GetTree().CreateTimer(0.4f),
            SceneTreeTimer.SignalName.Timeout);

        Invoke(refuge, "ToggleHubUi");
        await ToSignal(
            GetTree().CreateTimer(0.45f),
            SceneTreeTimer.SignalName.Timeout);
        bool collapsed =
            ReadPrivateBool(
                refuge,
                "_hubUiCollapsed") &&
            !view.FocusVeil.Visible &&
            !view.StationPanel.Visible &&
            !view.SettingsButton.Visible &&
            view.ToggleInterfaceButton.Visible;

        Invoke(refuge, "ToggleHubUi");
        await ToSignal(
            GetTree().CreateTimer(0.45f),
            SceneTreeTimer.SignalName.Timeout);
        bool restored =
            !ReadPrivateBool(
                refuge,
                "_hubUiCollapsed") &&
            view.FocusVeil.Visible &&
            view.StationPanel.Visible &&
            view.SettingsButton.Visible &&
            view.ToggleInterfaceButton.Visible;

        // Quick reversals, station refresh, and a resize must never resurrect a faded control.
        bool stressValid = true;
        for (int press = 0; press < 5; press++)
        {
            Invoke(refuge, "ToggleHubUi");
            Invoke(refuge, "RefreshStation", false);
            Invoke(refuge, "UpdateResponsiveLayout");
            await ToSignal(GetTree().CreateTimer(.035), SceneTreeTimer.SignalName.Timeout);
        }
        await ToSignal(GetTree().CreateTimer(.35), SceneTreeTimer.SignalName.Timeout);
        stressValid &= ReadPrivateBool(refuge, "_hubUiCollapsed") && !ReadPrivateBool(refuge, "_hubUiFading") &&
            !view.InfoButton.Visible && view.ToggleInterfaceButton.Visible &&
            view.PrimaryButton.MouseFilter == Control.MouseFilterEnum.Ignore;
        foreach (Node node in refuge.FindChildren("*", "Label3D", true, false))
            stressValid &= !(node as Label3D).Visible;
        string hiddenStation = ReadPrivate(refuge, "_currentStation").ToString();
        refuge._UnhandledInput(new InputEventAction { Action = "ui_right", Pressed = true });
        stressValid &= hiddenStation == ReadPrivate(refuge, "_currentStation").ToString();
        refuge._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await ToSignal(GetTree().CreateTimer(.35), SceneTreeTimer.SignalName.Timeout);
        stressValid &= !ReadPrivateBool(refuge, "_hubUiCollapsed") && view.InfoButton.Visible &&
            view.StationPanel.Modulate.A > .99f && view.SettingsButton.Modulate.A > .99f;
        Rect2 infoRect = view.InfoButton.GetGlobalRect();
        stressValid &= !infoRect.Intersects(view.SettingsButton.GetGlobalRect()) &&
            !infoRect.Intersects(view.ToggleInterfaceButton.GetGlobalRect()) &&
            Mathf.Abs(infoRect.Size.Y - view.SettingsButton.Size.Y) < 1;
        view.InfoButton.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFrames(2);
        var guide = view.GetNode<GameInfoPanel>("Info");
        stressValid &= guide.IsOpen && GetTree().Paused;
        guide.Close();
        stressValid &= !GetTree().Paused && view.InfoButton.Visible && guide.RefugeContext;
        var accessibility = GetNode<UiAccessibility>("/root/UiAccessibility");
        accessibility.SetTouchPreview(true);
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Size = new Vector2I(390, 844); GetWindow().ContentScaleSize = new Vector2I(390, 844);
        await WaitFrames(5);
        Vector2 touchPoint = view.InfoButton.GetGlobalRect().GetCenter();
        stressValid &= view.InfoButton.Size.X >= 44 && view.InfoButton.Size.Y >= 44;
        refuge._Input(new InputEventScreenTouch { Index = 0, Position = touchPoint, Pressed = true });
        refuge._Input(new InputEventScreenDrag { Index = 0, Position = touchPoint + new Vector2(180, 0), Relative = new Vector2(180, 0) });
        refuge._Input(new InputEventScreenTouch { Index = 0, Position = touchPoint + new Vector2(180, 0), Pressed = false });
        stressValid &= (int)ReadPrivate(refuge, "_touchDragIndex") == -1 &&
            hiddenStation == ReadPrivate(refuge, "_currentStation").ToString();
        view.InfoButton.EmitSignal(BaseButton.SignalName.Pressed); await WaitFrames(5);
        Rect2 guideRect = guide.GetChild<PanelContainer>(1).GetGlobalRect();
        stressValid &= guideRect.Position.X >= 0 && guideRect.End.X <= 391 && guideRect.End.Y <= 845;
        if (DisplayServer.GetName() != "headless")
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/nwd-refuge-info-portrait.png");
        }
        guide.Close(); accessibility.SetTouchPreview(false);

        if (!stationOrder ||
            !initialVisibility ||
            !authoredVeil ||
            !settingsLayering ||
            !deadNodesRemoved ||
            !navigation ||
            !settingsOpen ||
            !collapsed ||
            !restored || !stressValid)
        {
            Fail(
                $"order={stationOrder} " +
                $"visibility={initialVisibility} " +
                $"veil={authoredVeil} " +
                $"layering={settingsLayering} " +
                $"dead_nodes={deadNodesRemoved} " +
                $"navigation={navigation} " +
                $"settings={settingsOpen} " +
                $"collapsed={collapsed} " +
                $"restored={restored} stress={stressValid}");
            return;
        }

        GD.Print(
            "REFUGE_INTERFACE_PROBE: PASS " +
            "contract navigation settings collapse restore");
        GetTree().Quit(0);
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

    private static object ReadPrivate(
        object target,
        string fieldName)
    {
        return target?.GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.GetValue(target);
    }

    private static bool ReadPrivateBool(
        object target,
        string fieldName)
    {
        return ReadPrivate(target, fieldName) is
            bool value &&
            value;
    }

    private static void Invoke(
        object target,
        string methodName,
        params object[] arguments)
    {
        target?.GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.Invoke(
                target,
                arguments);
    }

    private void Fail(string reason)
    {
        GD.PushError(
            $"REFUGE_INTERFACE_PROBE: FAIL {reason}");
        GetTree().Quit(1);
    }
}
