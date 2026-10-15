using Godot;

/// <summary>Scrollable modal with explicit pause ownership and focus restoration.</summary>
public partial class GameMenuPanel : Control
{
    protected VBoxContainer Content;
    private PanelContainer _card;
    private Button _close;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Control _previousFocus;
    private bool _ownsPause;
    private bool _layoutQueued;
    private UiAccessibility _accessibility;
    public bool IsOpen => Visible;
    protected virtual string PanelTitle => "FIELD TERMINAL";
    protected virtual string PanelSubtitle => "NO WAY DOWN  /  RUNNER SYSTEMS";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 250;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = Colors.White, Material = MenuVisualStyle.FocusMaterial(), MouseFilter = MouseFilterEnum.Stop };
        AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new PanelContainer { ClipContents = true };
        _card.MinimumSizeChanged += QueueLayout;
        _card.AddThemeStyleboxOverride("panel", BuffUi.Surface(BuffUi.Accent));
        AddChild(_card);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        _card.AddChild(body);
        var header = new HBoxContainer();
        body.AddChild(header);
        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _subtitleLabel = BuffUi.Label(PanelSubtitle, 10, BuffUi.Accent);
        _titleLabel = BuffUi.Label(PanelTitle, 26);
        identity.AddChild(_subtitleLabel);
        identity.AddChild(_titleLabel);
        header.AddChild(identity);
        _close = BuffUi.Button("BACK");
        _close.Name = "Close";
        header.AddChild(_close);
        _close.Pressed += Close;
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        Content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(Content);
        _accessibility = GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");
        if (_accessibility != null) _accessibility.ScaleChanged += OnScaleChanged;
        _close.CustomMinimumSize = new Vector2(96, 44);
        GetViewport().SizeChanged += HandleResize;
        Layout();
        Hide();
    }

    public void Open()
    {
        if (Visible) return;
        _previousFocus = GetViewport().GuiGetFocusOwner();
        _ownsPause = !GetTree().Paused;
        if (_ownsPause) GetTree().Paused = true;
        RefreshContent();
        Show();
        Layout();
        _close.GrabFocus();
        QueueLayout();
    }

    public void Close()
    {
        if (!Visible) return;
        Hide();
        if (_ownsPause) GetTree().Paused = false;
        _ownsPause = false;
        if (IsInstanceValid(_previousFocus) && _previousFocus.IsVisibleInTree()) _previousFocus.GrabFocus();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!Visible) return;
        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("pause"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    protected virtual void Rebuild() { }

    protected void RefreshContent()
    {
        _titleLabel.Text = PanelTitle;
        _subtitleLabel.Text = PanelSubtitle;
        Rebuild();
        _accessibility?.ApplyControlTree(_card);
        Layout();
    }

    private void OnScaleChanged(float uiScale, float textScale)
    {
        if (Visible) RefreshContent();
    }

    protected void ClearContent()
    {
        foreach (Node child in Content.GetChildren()) { Content.RemoveChild(child); child.QueueFree(); }
    }

    protected virtual void OnPanelResized() { }
    private void HandleResize() { Layout(); OnPanelResized(); }

    private void QueueLayout()
    {
        if (_layoutQueued || !IsInsideTree()) return;
        _layoutQueued = true;
        Callable.From(() => { _layoutQueued = false; if (IsInsideTree() && Visible) Layout(); }).CallDeferred();
    }

    private void Layout()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        var accessibility = GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");
        Vector4 safe = accessibility?.GetSafeInsets(viewport) ?? Vector4.Zero;
        Vector2 available = viewport - new Vector2(safe.X + safe.Z, safe.Y + safe.W) - Vector2.One * 24;
        Vector2 requested = new(Mathf.Min(980, available.X), Mathf.Min(720, available.Y));
        _card.Size = requested;
        _card.Position = new Vector2(safe.X + 12, safe.Y + 12) + (available - requested) * .5f;
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null) GetViewport().SizeChanged -= HandleResize;
        if (IsInstanceValid(_accessibility)) _accessibility.ScaleChanged -= OnScaleChanged;
        if (_ownsPause && GetTree() != null) GetTree().Paused = false;
    }
}
