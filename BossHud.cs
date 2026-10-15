using Godot;

/// <summary>A compact, non-interactive guardian rail using the existing glass menu palette.</summary>
public partial class BossHud : PanelContainer
{
    public BossEncounter Encounter { get; set; }
    private Label _name, _hint;
    private ProgressBar _health;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 18;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.02f,.04f,.07f,.32f),
            BorderColor = new Color(Encounter.Definition.Color,.5f), BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 5, ContentMarginBottom = 5 });
        _name = GetNode<Label>("Content/Identity");
        _health = GetNode<ProgressBar>("Content/Health");
        _hint = GetNode<Label>("Content/Hint");
        _name.AddThemeColorOverride("font_color", BuffUi.Text);
        _hint.AddThemeColorOverride("font_color", BuffUi.Text);
        _health.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(1,1,1,.1f) });
        _health.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Encounter.Definition.Color });
        GetViewport().SizeChanged += Layout; Layout();
    }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Encounter)) return;
        _name.Text = Encounter.Definition.Name + "  ·  " + (Encounter.Phase == 1 ? "I" : "II");
        if(Encounter.IsMarked)_name.Text += "  ·  MARKED";
        _hint.Text = Encounter.Instruction; _health.Value = Encounter.Health;
        Layout();
    }
    private void Layout()
    {
        Vector2 view = GetViewport().GetVisibleRect().Size;
        var access = GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");
        Vector4 safe = access?.GetSafeInsets(view) ?? Vector4.Zero;
        float width = Mathf.Min(460, view.X - safe.X - safe.Z - 28);
        Size = new Vector2(width, 0); Position = new Vector2(safe.X + (view.X - safe.X - safe.Z - width)/2, safe.Y + 44);
    }
    public override void _ExitTree() { if (GetViewport() != null) GetViewport().SizeChanged -= Layout; }
}
