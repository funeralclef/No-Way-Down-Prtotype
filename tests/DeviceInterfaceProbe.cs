using Godot;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Cast affordance, real GUI input and physical-key input at the supported touch sizes.</summary>
public partial class DeviceInterfaceProbe : Node
{
    private int _failures;
    public override async void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        var access=GetNode<UiAccessibility>("/root/UiAccessibility");access.SetTouchPreview(true);
        var game=GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();AddChild(game);await Frames(5);
        var player=game.GetNode<Player>("Player");player.SetPhysicsProcess(false);player.ApplyRunUpgrade(RunUpgradeId.Overpass,1);
        game.GetNode<AbyssPressureFront>("RunSystems/AbyssPressureFront").Stop();game.GetNode<EncounterDirector>("RunSystems/EncounterDirector").StopAll();
        var mobile=game.GetNode<MobileControls>("UserInterface/MobileControls");
        var hud=game.GetNode<RunSkillHud>("UserInterface/RunOverlays/RunSkillHud");var cast=hud.GetNode<Button>("Cast");
        foreach(var size in new[]{new Vector2I(390,844),new Vector2I(640,360),new Vector2I(1152,648)}) {
            GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=size;GetWindow().ContentScaleSize=size;await Frames(8);
            Rect2 rect=cast.GetGlobalRect();Vector2 center=Field<Vector2>(mobile,"_joystickCenter");float radius=Field<float>(mobile,"_joystickRadius");
            Vector2 closest=new(Mathf.Clamp(center.X,rect.Position.X,rect.End.X),Mathf.Clamp(center.Y,rect.Position.Y,rect.End.Y));
            Check(rect.Position.X>=0&&rect.Position.Y>=0&&rect.End.X<=size.X&&rect.End.Y<=size.Y,"cast button stays on screen at "+size);
            Check(rect.Size.X>=44&&rect.Size.Y>=44&&closest.DistanceTo(center)>radius,"cast button has a touch target outside the joystick");
            Check(!rect.Intersects(Field<Rect2>(mobile,"_nitroGestureRect")),"skill touch does not share the reactor gesture area");
            mobile._Input(new InputEventScreenTouch {Index=7,Pressed=true,Position=rect.GetCenter()});
            Check(Field<int>(mobile,"_movementTouchId")<0&&Field<int>(mobile,"_jumpTouchId")<0&&Field<int>(mobile,"_nitroGestureTouchId")<0,"skill touch never starts movement or jump");
            mobile._Input(new InputEventScreenTouch {Index=7,Pressed=false,Position=rect.GetCenter()});
            player.GlobalPosition=new Vector3(0,3,0);player.AddNitro(100);player.RunSkills._PhysicsProcess(8);
            await Frames(2);
            await Click(rect.GetCenter());Check(player.RunSkills.Cooldown>0,"real pointer click casts the equipped device");
            if(DisplayServer.GetName()!="headless") {await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng($"/tmp/nwd-device-ui-{size.X}.png");}
        }
        player.RunSkills._PhysicsProcess(8);player.GlobalPosition=new Vector3(0,3,0);player.AddNitro(100);
        GetViewport().PushInput(new InputEventKey {PhysicalKeycode=Key.E,Pressed=true});await Frames(2);
        GetViewport().PushInput(new InputEventKey {PhysicalKeycode=Key.E,Pressed=false});
        Check(player.RunSkills.Cooldown>0,"default physical E key reaches the skill through production input routing");
        access.SetTouchPreview(false);
        GD.Print(_failures==0?"DEVICE_INTERFACE_PROBE: PASSED":$"DEVICE_INTERFACE_PROBE: FAILED ({_failures})");GetTree().Quit(_failures==0?0:1);
    }
    private async Task Click(Vector2 p){GetViewport().PushInput(new InputEventMouseMotion {Position=p,GlobalPosition=p});GetViewport().PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,Pressed=true,ButtonIndex=MouseButton.Left});await Frames(1);GetViewport().PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,Pressed=false,ButtonIndex=MouseButton.Left});await Frames(2);}
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static T Field<T>(object node,string name)=>(T)node.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(node);
    private void Check(bool valid,string message){if(!valid){_failures++;GD.PushError("FAIL: "+message);}else GD.Print("PASS: "+message);}
}
