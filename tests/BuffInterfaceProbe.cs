using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

public partial class BuffInterfaceProbe : Node
{
    private bool _failed;
    private readonly bool _capture = !DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase);
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Main game = GetNode<Main>("Game");
        await Frames(4);
        Player player = game.GetNode<Player>("Player");
        player.SetPhysicsProcess(false);
        game.GetNode<AbyssPressureFront>("RunSystems/AbyssPressureFront").Stop();
        var progression = Field<RunProgression>(game, "_runProgression");
        // Build ingredients through the public drafting path.
        progression.SetSeed(0xB0FFUL + (ulong)RunUpgradeId.LastLight);
        for (int arena = 0; arena < 180 && !progression.CanMerge(RunUpgradeId.LastLight); arena++)
        {
            progression.BeginArenaDraft();
            int selected = -1;
            for (int i = 0; i < progression.CurrentRewardOffers.Count; i++)
            {
                var offer = progression.CurrentRewardOffers[i];
                if (offer.Id is RunUpgradeId.SecondWind or RunUpgradeId.GlassHeart) { selected = i; break; }
                if (offer.Tier != RunRewardTier.Evolution) selected = i;
            }
            if (selected < 0) selected = 0;
            if (progression.CurrentRewardOffers[selected].Id == RunUpgradeId.LastLight) selected = (selected + 1) % progression.CurrentRewardOffers.Count;
            progression.SelectReward(selected); progression.SelectRoute(0);
        }
        game.Call("SynchronizeRunBuild");
        var hud = Field<RunBuffHud>(game, "_buffHud");
        var dock = hud.GetNode<HBoxContainer>("BuffDock");
        Check(dock.GetChildCount() == 2, "persistent dock only contains the two compact actions");
        foreach (Node node in dock.GetChildren())
            if (node is Button button)
                Check(button.Text.Length == 0 && button.Icon != null && button.GetThemeConstant("icon_max_width") == 18,
                    "dock buttons use constrained icon-only presentation");
        var panel = hud.GetNode<RunBuildPanel>("Buffs");
        panel.Open();
        Check(GetTree().Paused, "build pauses the active run");
        panel.Call("ShowTab", true);
        await Frames(4);
        await Capture("merge-ready");
        var merge = panel.FindChild("MergeLastLight", true, false) as Button;
        Check(merge != null && !merge.Disabled, "ready recipe is actionable");
        int previous = player.GetRunUpgradeRank(RunUpgradeId.SecondWind);
        merge?.EmitSignal(BaseButton.SignalName.Pressed);
        Check(player.HasRunUpgrade(RunUpgradeId.LastLight) && player.GetRunUpgradeRank(RunUpgradeId.SecondWind) == previous - 2,
            "merge UI synchronizes evolution and consumed catalyst to the live player");
        Check(GetViewport().GuiGetFocusOwner() is Button, "merge restores controller focus");
        panel.Call("ShowTab", false);
        await Frames(4);
        await Capture("active-buffs");
        foreach (Vector2I size in new[] { new Vector2I(390, 844), new Vector2I(640, 360) })
        {
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = size;
            GetWindow().ContentScaleSize = size;
            await Frames(5);
            Check(dock.GetGlobalRect().Position.X >= 0 && dock.GetGlobalRect().End.X <= size.X,
                "compact actions stay inside the top bar at " + size);
            var card = panel.GetChild<PanelContainer>(1);
            Rect2 rect = card.GetGlobalRect();
            Vector2 viewport = GetViewport().GetVisibleRect().Size;
            Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.X + 1 && rect.End.Y <= viewport.Y + 1,
                "build fits " + size);
            await Capture($"buffs-{size.X}x{size.Y}");
        }
        var accessibility = GetNode<UiAccessibility>("/root/UiAccessibility");
        accessibility.SetScales(1.4f, 1.35f);
        await Frames(5);
        Check(panel.GetChild<PanelContainer>(1).GetGlobalRect().End.X <= GetViewport().GetVisibleRect().Size.X + 1,
            "large text stays within the panel width");
        accessibility.SetScales(1, 1);
        panel.Close();
        Check(!GetTree().Paused, "closing build resumes its owned pause");
        GetTree().Paused = true;
        var info = hud.GetNode<GameInfoPanel>("Info");
        info.Open();
        await Frames(8);
        Rect2 infoRect = info.GetChild<PanelContainer>(1).GetGlobalRect();
        Check(infoRect.Position.Y >= 0 && infoRect.End.Y <= GetViewport().GetVisibleRect().Size.Y + 1,
            "control guide fits on its first open");
        info.Close();
        Check(GetTree().Paused, "guide preserves an existing pause");
        GetTree().Paused = false;
        var pause = game.GetNode<RunPauseMenu>("UserInterface/RunOverlays/RunPauseMenu");
        pause.Call("OpenPauseMenu"); await Frames(4);
        var resume = pause.FindChild("Resume", true, false) as Button;
        foreach (string name in new[] { "Build", "InfoAction" })
        {
            var action = pause.FindChild(name, true, false) as Button;
            Check(action != null && action.Alignment == resume.Alignment &&
                    action.GetThemeFontSize("font_size") == resume.GetThemeFontSize("font_size") &&
                    action.GetThemeStylebox("normal").GetContentMargin(Side.Left) == resume.GetThemeStylebox("normal").GetContentMargin(Side.Left),
                name + " follows the pause action style");
        }
        await Capture("pause-unified");
        pause.Call("ResumeRun");
        GD.Print(_failed ? "BUFF_INTERFACE_PROBE: FAILED" : "BUFF_INTERFACE_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }

    private async Task Capture(string name)
    {
        if (!_capture) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-{name}.png");
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    private void Check(bool condition, string message)
    {
        if (condition) GD.Print("PASS: " + message);
        else { _failed = true; GD.PushError("FAIL: " + message); }
    }
}
