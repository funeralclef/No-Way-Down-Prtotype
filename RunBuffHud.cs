using Godot;
using System;
using System.Linq;

/// <summary>Compact persistent build dock. The modal handles details and merge.</summary>
public partial class RunBuffHud : Control
{
    private RunProgression _progression;
    private Player _player;
    private RunBuildPanel _panel;
    private GameInfoPanel _info;
    private HBoxContainer _dock;
    private Button _buildButton;
    private ColorRect _mergeDot;
    private Label _toast;
    private float _toastTime;
    private bool _available = true;
    private UiAccessibility _accessibility;
    public void Initialize(RunProgression progression, Player player, Action synchronize)
    {
        _progression = progression; _player = player;
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new RunBuildPanel { Name = "Buffs" };
        AddChild(_panel); _panel.Initialize(progression, synchronize);
        _info = new GameInfoPanel { Name = "Info" };
        AddChild(_info);
        _dock = new HBoxContainer { Name = "BuffDock" };
        AddChild(_dock);
        var build = BuffUi.IconButton(UiIconLibrary.Loadout, "BUILD · modules & fusion"); build.Name = "OpenBuffs";
        // Movement also drives GUI navigation; Space/A must never activate the gameplay dock.
        // Keyboard and controller users reach these panels through the pause menu.
        build.FocusMode = FocusModeEnum.None;
        _buildButton = build;
        _dock.AddChild(build); build.Pressed += _panel.Open;
        var info = BuffUi.IconButton(UiIconLibrary.Info, "INFO · controls & field guide"); info.Name = "OpenInfo";
        info.FocusMode = FocusModeEnum.None;
        _dock.AddChild(info); info.Pressed += _info.Open;
        _dock.AddThemeConstantOverride("separation", 4);
        _mergeDot = new ColorRect { Name = "MergeReady", Color = BuffUi.Accent, MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(24, 9), Size = new Vector2(4, 4) };
        build.AddChild(_mergeDot);
        _toast = BuffUi.Label("", 16, BuffUi.Accent); _toast.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_toast);
        GetViewport().SizeChanged += Layout;
        _accessibility = GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");
        if (_accessibility != null) _accessibility.ScaleChanged += OnScaleChanged;
        Refresh(); Layout();
    }

    public void OpenBuild()
    {
        if (_available) _panel.Open();
    }

    public void SetAvailable(bool available)
    {
        _available = available;
        if (available) Refresh();
        if (!available) { _panel.Close(); _info.Close(); }
    }

    public override void _Process(double delta)
    {
        if (_dock == null) return;
        _dock.Visible = _available && !GetTree().Paused && IsInstanceValid(_player) && _player.Health > 0;
        if (_toastTime > 0 && !GetTree().Paused) _toastTime -= (float)delta;
        _toast.Visible = _dock.Visible && _toastTime > 0;
    }

    public void AnnounceActivation(RunUpgradeId id)
    {
        if (_toastTime > 1.2f) return;
        _toast.Text = RunProgression.GetDefinition(id).Name + " · ACTIVE";
        _toast.AddThemeColorOverride("font_color", RunBuffCatalog.Accent(id));
        _toastTime = 2;
    }

    public void Announce(RunUpgradeDefinition definition)
    {
        Refresh();
        _toast.Text = $"{definition.Name}  {RunBuffCatalog.RankPips(definition.Id, _progression.GetRank(definition.Id))}";
        _toast.AddThemeColorOverride("font_color", RunBuffCatalog.Accent(definition.Id));
        _toastTime = 4;
    }

    public void Refresh()
    {
        bool ready = RunBuffCatalog.Recipes.Any(recipe => _progression.CanMerge(recipe.Evolution));
        _mergeDot.Visible = ready;
        _buildButton.TooltipText = ready ? "BUILD · fusion available" : "BUILD · modules & fusion";
        Callable.From(PositionDock).CallDeferred();
    }

    private void Layout()
    {
        Refresh();
        PositionDock();
    }

    private void PositionDock()
    {
        if (!IsInsideTree() || _dock == null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        var accessibility = GetNodeOrNull<UiAccessibility>("/root/UiAccessibility");
        Vector4 safe = accessibility?.GetSafeInsets(viewport) ?? Vector4.Zero;
        float scale = accessibility?.LayoutScale ?? 1;
        bool touch = accessibility?.IsTouchLayout == true;
        foreach (Node child in _dock.GetChildren())
            if (child is Button button) button.CustomMinimumSize = new Vector2(touch ? 44 : 32, touch ? 52 : 42) * scale;
        _dock.ResetSize();
        _dock.Position = new Vector2(Mathf.Max(safe.X + 12, viewport.X - safe.Z - _dock.GetCombinedMinimumSize().X - (touch ? 78 : 68) * scale), safe.Y + 12 * scale);
        _toast.Position = new Vector2(safe.X + 16, safe.Y + 64);
        _toast.Size = new Vector2(viewport.X - safe.X - safe.Z - 32, 36);
    }

    private void OnScaleChanged(float ui, float text) => Layout();

    public override void _ExitTree()
    {
        if (GetViewport() != null) GetViewport().SizeChanged -= Layout;
        if (IsInstanceValid(_accessibility)) _accessibility.ScaleChanged -= OnScaleChanged;
    }
}
