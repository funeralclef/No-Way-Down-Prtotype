using Godot;
using System.Threading.Tasks;

/// <summary>Exercises the engine's GUI focus routing with real gameplay and menu events.</summary>
public partial class GameplayMenuInputProbe : Node
{
    private int _failures;
    private RunPauseMenu _pause;
    private RunBuildPanel _build;
    private GameInfoPanel _info;
    private Player _player;
    private Button _cast;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        await Frames(5);
        var game = GetParent();
        _player = game.GetNode<Player>("Player");
        _player.SetPhysicsProcess(false);
        _player.ApplyRunUpgrade(RunUpgradeId.RedThread, 1);
        game.GetNode<AbyssPressureFront>("RunSystems/AbyssPressureFront").Stop();
        game.GetNode<EncounterDirector>("RunSystems/EncounterDirector").StopAll();
        _pause = game.GetNode<RunPauseMenu>("UserInterface/RunOverlays/RunPauseMenu");
        var hud = game.FindChild("RunBuffHud", true, false) as RunBuffHud;
        _build = hud.GetNode<RunBuildPanel>("Buffs");
        _info = hud.GetNode<GameInfoPanel>("Info");
        _cast = game.GetNode<Button>("UserInterface/RunOverlays/RunSkillHud/Cast");
        await Frames(2);

        await ClickSkill();
        Check(_player.RunSkills.HasAnchor, "pointer still casts the equipped skill");
        Check(GetViewport().GuiGetFocusOwner() == null, "casting leaves gameplay without GUI focus");
        await KeyPress(Key.Tab);
        await KeyPress(Key.Space);
        CheckPlaying("Tab followed by jump cannot open a HUD menu");
        await ResetMenus();

        foreach (Key key in new[] { Key.W, Key.A, Key.S, Key.D, Key.Up, Key.Left, Key.Down, Key.Right })
        {
            await ClickSkill();
            await KeyPress(key);
            await KeyPress(Key.Space);
            CheckPlaying(key + " followed by jump stays in gameplay");
            await ResetMenus();
        }
        foreach (JoyButton direction in new[] { JoyButton.DpadDown, JoyButton.DpadRight, JoyButton.DpadUp, JoyButton.DpadLeft })
        {
            await ClickSkill();
            await PadPress(direction);
            await PadPress(JoyButton.A);
            CheckPlaying(direction + " followed by A stays in gameplay");
            await ResetMenus();
        }
        await ClickSkill();
        GetViewport().PushInput(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1 });
        await PadPress(JoyButton.A);
        GetViewport().PushInput(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 0 });
        CheckPlaying("stick movement followed by A stays in gameplay");
        await ResetMenus();

        foreach (string name in new[] { "OpenInfo", "OpenBuffs" })
        {
            await Click(hud.GetNode<Button>("BuffDock/" + name));
            Check(GetTree().Paused && (name == "OpenInfo" ? _info.IsOpen : _build.IsOpen), name + " still opens by pointer");
            await KeyPress(Key.Escape);
            CheckPlaying("Escape closes " + name);
            await KeyPress(Key.Tab);
            await KeyPress(Key.Space);
            await PadPress(JoyButton.A);
            CheckPlaying("jump cannot reopen " + name + " after closing it");
            await ResetMenus();
        }

        await KeyPress(Key.Escape);
        Check(GetTree().Paused && (GetViewport().GuiGetFocusOwner() as Button)?.Name == "Resume", "Escape deliberately opens pause with controller focus");
        var infoAction = _pause.FindChild("InfoAction", true, false) as Button;
        infoAction.GrabFocus();
        await PadPress(JoyButton.A);
        Check(_pause.GetNode<GameInfoPanel>("Info").IsOpen, "controller accept still opens info inside pause");
        await PadPress(JoyButton.B);
        Check(GetTree().Paused && GetViewport().GuiGetFocusOwner() == infoAction, "closing nested info preserves pause and restores menu focus");
        var buildAction = _pause.FindChild("Build", true, false) as Button;
        buildAction.GrabFocus();
        await PadPress(JoyButton.A);
        Check(_build.IsOpen, "controller accept still opens build inside pause");
        await PadPress(JoyButton.B);
        Check(GetTree().Paused && GetViewport().GuiGetFocusOwner() == buildAction, "closing nested build restores pause focus");
        (_pause.FindChild("Resume", true, false) as Button).GrabFocus();
        await PadPress(JoyButton.A);
        CheckPlaying("controller accept resumes the run");
        await KeyPress(Key.Tab);
        await KeyPress(Key.Space);
        await PadPress(JoyButton.A);
        CheckPlaying("jump cannot reopen pause after resume");
        await ResetMenus();
        await Click(_pause.GetNode<Button>("MenuButton"));
        Check(GetTree().Paused, "pause launcher still opens by pointer");
        await KeyPress(Key.Escape);
        CheckPlaying("Escape resumes pointer-opened pause");
        await PadPress(JoyButton.Start);
        Check(GetTree().Paused, "Start deliberately opens pause");
        await PadPress(JoyButton.Start);
        CheckPlaying("Start resumes the run");

        GD.Print(_failures == 0 ? "GAMEPLAY_MENU_INPUT_PROBE: PASSED" : $"GAMEPLAY_MENU_INPUT_PROBE: FAILED ({_failures})");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void CheckPlaying(string message) => Check(!GetTree().Paused && !_info.IsOpen && !_build.IsOpen, message);
    private async Task ClickSkill()
    {
        _player.RunSkills._PhysicsProcess(10);
        _player.AddNitro(100);
        await Frames(2);
        await Click(_cast);
    }
    private async Task ResetMenus()
    {
        _info.Close(); _build.Close(); _pause.Call("ResumeRun");
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        await Frames(2);
    }
    private async Task KeyPress(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Frames(1);
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frames(2);
    }
    private async Task PadPress(JoyButton button)
    {
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        await Frames(1);
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        await Frames(2);
    }
    private async Task Click(Button button)
    {
        Vector2 point = button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(3);
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private void Check(bool condition, string message)
    {
        if (condition) GD.Print("PASS: " + message);
        else { _failures++; GD.PushError("FAIL: " + message); }
    }
}
