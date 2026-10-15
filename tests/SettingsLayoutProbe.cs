using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Actual settings pages at desktop and narrow sizes, including the tab marker.</summary>
public partial class SettingsLayoutProbe : Node
{
    private bool _failed;
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var settings = GD.Load<PackedScene>("res://ui/full_settings.tscn").Instantiate<DisplaySettings>();
        AddChild(settings); settings.UseExternalLauncher(); settings.OpenFromExternal();
        var tabs = Field<TabContainer>(settings, "_tabs");
        foreach (var size in new[] { new Vector2I(1152, 648), new Vector2I(390, 844), new Vector2I(640, 360) })
        {
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            await Frames(6);
            var initial = tabs.GetTabBar().GetNode<SettingsTabAccent>("TabAccent");
            Rect2 selectedRect = tabs.GetTabBar().GetTabRect(tabs.CurrentTab);
            Check(Mathf.Abs(initial.AccentRect.Position.X - (selectedRect.Position.X + 7)) < 1,
                "first-open and resized selection snaps to its native tab");
            for (int tab = 0; tab < tabs.GetTabCount(); tab++)
            {
                tabs.CurrentTab = tab; await Frames(5);
                var marker = tabs.GetTabBar().GetNodeOrNull<SettingsTabAccent>("TabAccent");
                Check(marker != null && marker.AccentRect.Position.X >= 0 && marker.AccentRect.End.X <= tabs.GetTabBar().Size.X + 1,
                    $"gradient remains clipped to native tabs at {size}, tab {tab}");
                Check(Field<PanelContainer>(settings, "_settingsCard").GetThemeStylebox("panel") is StyleBoxFlat surface && surface.BgColor.A == 0,
                    "settings card has a fully transparent surface");
                foreach (Node node in Descendants(tabs.GetCurrentTabControl()))
                {
                    if (node is Label label && label.GetParent() is GridContainer)
                        Check(label.Size.X > 1 && label.Size.Y > 10, "setting label remains visible");
                    if (node is HSlider slider)
                    {
                        Rect2 viewport = GetViewport().GetVisibleRect();
                        Check(slider.GetGlobalRect().End.X <= viewport.End.X + 1, "slider stays inside screen");
                    }
                }
                if (DisplayServer.GetName() != "headless" && (tab == 0 || tab == 2 || tab == 4))
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-settings-{size.X}-{tab}.png");
                }
            }
        }
        GD.Print(_failed ? "SETTINGS_LAYOUT_PROBE: FAILED" : "SETTINGS_LAYOUT_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node root)
    { foreach (Node child in root.GetChildren()) { yield return child; foreach (Node nested in Descendants(child)) yield return nested; } }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool value, string detail) { if (!value) { _failed = true; GD.PushError("FAIL: " + detail); } }
}
