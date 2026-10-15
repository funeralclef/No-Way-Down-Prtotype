using Godot;

/// <summary>Paints in the native TabBar coordinate space, so first-open and scroll positions cannot drift.</summary>
public partial class SettingsTabAccent : Control
{
    private TabBar _bar;
    private GradientTexture2D _gradient;
    private Rect2 _drawRect;
    private bool _positioned;
    private int _lastIndex = -1;
    private Vector2 _lastBarSize;
    private Rect2 _lastTarget;
    public Rect2 AccentRect => _drawRect.Intersection(new Rect2(Vector2.Zero, Size));

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 3;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _bar = GetParent<TabBar>();
        _bar.ClipContents = true;
        _gradient = new GradientTexture2D { Width = 256, Height = 8,
            FillFrom = Vector2.Zero, FillTo = Vector2.Right,
            Gradient = new Gradient { Offsets = new[] { 0f, .16f, .48f, .83f, 1f },
                Colors = new[] { new Color(1, .75f, .4f, 0), new Color("#ffedc7"),
                    new Color("#f5bf7b"), new Color("#eb7d40"), new Color(1, .4f, .2f, 0) } } };
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _bar.CurrentTab < 0 || _bar.Size.X < 1)
        { _positioned = false; return; }
        Rect2 tab = _bar.GetTabRect(_bar.CurrentTab);
        if (tab.Size.X < 1 || tab.Size.Y < 1) return;
        Rect2 target = new(new Vector2(tab.Position.X + 7, Mathf.Min(_bar.Size.Y, tab.End.Y) - 3),
            new Vector2(Mathf.Max(1, tab.Size.X - 14), 2));
        // Every opening starts at its actual selection. Only subsequent tab changes interpolate.
        bool layoutChanged = _bar.Size != _lastBarSize || (_lastIndex == _bar.CurrentTab && target != _lastTarget);
        if (!_positioned || layoutChanged) _drawRect = target;
        else
        {
            float weight = 1 - Mathf.Exp(-(float)delta * 24);
            _drawRect = new Rect2(_drawRect.Position.Lerp(target.Position, weight), _drawRect.Size.Lerp(target.Size, weight));
        }
        _positioned = true;
        _lastIndex = _bar.CurrentTab; _lastTarget = target; _lastBarSize = _bar.Size;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_positioned || _gradient == null) return;
        Rect2 bounds = new(Vector2.Zero, Size);
        Rect2 line = _drawRect.Intersection(bounds);
        if (!line.HasArea()) return;
        DrawTextureRect(_gradient, new Rect2(line.Position - Vector2.Down * 2, line.Size + Vector2.Down * 3).Intersection(bounds), false, new Color(1, 1, 1, .13f));
        DrawTextureRect(_gradient, line, false);
    }
}
