using Godot;

/// <summary>One compact cast affordance for keyboard, controller and touch. Build owns equipment selection.</summary>
public partial class RunSkillHud : Control
{
    public RunSkillRuntime Runtime {get;set;}
    private Button _cast;
    private ProgressBar _cooldown;
    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;MouseFilter=MouseFilterEnum.Ignore;
        _cast=GetNode<Button>("Cast");_cooldown=GetNode<ProgressBar>("Cast/Cooldown");MenuVisualStyle.Action(_cast,10,true);
        foreach(string state in new[]{"normal","hover","pressed","disabled"})
            _cast.AddThemeStyleboxOverride(state,new StyleBoxFlat {
                BgColor=new Color(.025f,.07f,.09f,state=="hover"?.52f:state=="disabled"?.3f:.42f),
                CornerRadiusTopLeft=4,CornerRadiusTopRight=4,CornerRadiusBottomLeft=4,CornerRadiusBottomRight=4,
                ContentMarginLeft=12,ContentMarginRight=12,ContentMarginTop=8,ContentMarginBottom=8 });
        _cast.AddThemeColorOverride("font_disabled_color",new Color(.86f,.95f,.96f,.85f));
        (GetTree().GetFirstNodeInGroup("mobile_controls") as MobileControls)?.RegisterTouchExclusion(_cast);
        _cast.Pressed+=()=>Runtime?.TryCast();
    }
    public override void _Process(double delta)
    {
        Visible=IsInstanceValid(Runtime)&&Runtime.Skill!=null&&!GetTree().Paused&&Runtime.GetParent<Player>().Health>0;
        if(!Visible)return;
        var skill=Runtime.Skill;var access=GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");bool touch=access?.IsTouchLayout==true;
        Vector2 view=GetViewport().GetVisibleRect().Size;Vector4 safe=access?.GetSafeInsets(view)??Vector4.Zero;
        float scale=access?.LayoutScale??1;
        Vector2 size=new Vector2(touch?68:108,touch?48:42)*scale;
        _cast.Size=size;_cast.Position=new Vector2(view.X-safe.Z-size.X-(touch?152:24)*scale,view.Y-safe.W-size.Y-30*scale);
        var mobile=GetTree().GetFirstNodeInGroup("mobile_controls") as MobileControls;
        if(touch&&IsInstanceValid(mobile))_cast.Position=mobile.SkillButtonPosition(size);
        string binding=touch?"CAST":GamepadInput.ShouldShowGamepadPrompts?"X":BindingName();
        string title=skill.Command==RunSkillCommand.ThreadRecall&&Runtime.HasAnchor?"RECALL":skill.Name;
        if(touch)title=skill.Command switch {RunSkillCommand.AirStep=>"STEP",RunSkillCommand.FaultDive=>"DIVE",RunSkillCommand.HuntSpark=>"SPARK",_=>"RECALL"};
        _cast.Text=(Runtime.Cooldown>0?$"{Runtime.Cooldown:0.0}s":binding)+"\n"+title;
        _cast.TooltipText=skill.Instruction+"\n"+skill.Cost+" NITRO · "+skill.Cooldown+"s cooldown";
        _cooldown.Value=1-Runtime.Cooldown/skill.Cooldown;_cast.Disabled=Runtime.Cooldown>0;
    }
    private static string BindingName()
    {foreach(var e in InputMap.ActionGetEvents("module_skill"))if(e is InputEventKey key)return OS.GetKeycodeString(key.PhysicalKeycode);return "E";}
}
