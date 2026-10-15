using Godot;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Actual GUI hit testing in the production run, including pause ownership.</summary>
public partial class RunSettingsInputProbe : Node
{
    private int _failures;
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        await Frames(5);
        var game = GetParent();
        var pause = game.GetNode<RunPauseMenu>("UserInterface/RunOverlays/RunPauseMenu");
        var settings = game.GetNode<DisplaySettings>("UserInterface/DesktopDisplay");
        Invoke(pause,"OpenPauseMenu"); await Seconds(.4f);
        Check(GetTree().Paused,"pause stops the run");
        var button = pause.FindChild("Settings",true,false) as Button;
        await Click(button.GetGlobalRect().GetCenter()); await Seconds(.55f);
        Check(settings.IsMenuOpen,"mouse opens settings from the real pause menu");
        var tabs = Field<TabContainer>(settings,"_tabs");
        var slider = Field<HSlider>(settings,"_masterVolumeSlider");
        for(int i=0;i<tabs.GetTabCount();i++) if(tabs.GetTabControl(i).IsAncestorOf(slider)) tabs.CurrentTab=i;
        await Frames(5);
        double before = slider.Value;
        await Click(slider.GetGlobalRect().Position + new Vector2(slider.Size.X * .31f,slider.Size.Y/2));
        Check(System.Math.Abs(slider.Value-before)>5,"in-run slider receives real pointer clicks above all HUD surfaces");
        settings.ApplyCurrentSelection(false);
        Check(System.Math.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"))-Mathf.LinearToDb((float)slider.Value/100))<.2,"audio change reaches the active audio bus");
        Invoke(settings,"CloseMenuWithoutConfirmation"); await Seconds(.45f);
        Check(!settings.IsMenuOpen && GetTree().Paused && Field<Control>(pause,"_overlay").Visible,"closing settings returns to pause without resuming gameplay");
        Invoke(pause,"ResumeRun"); await Frames(2);
        Check(!GetTree().Paused,"resume releases pause after settings");
        GD.Print(_failures==0?"RUN_SETTINGS_INPUT_PROBE: PASSED":$"RUN_SETTINGS_INPUT_PROBE: FAILED ({_failures})");
        GetTree().Quit(_failures==0?0:1);
    }
    private async Task Click(Vector2 point) {
        GetViewport().PushInput(new InputEventMouseMotion { Position=point,GlobalPosition=point });
        GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true });
        await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false });
        await Frames(2);
    }
    private async Task Frames(int count) { for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Seconds(float count) { await ToSignal(GetTree().CreateTimer(count,true),SceneTreeTimer.SignalName.Timeout); }
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private static void Invoke(object value,string name)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,null);
    private void Check(bool good,string message) { if(!good){_failures++;GD.PushError("FAIL: "+message);}else GD.Print("PASS: "+message); }
}
