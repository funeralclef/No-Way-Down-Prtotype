using Godot;

/// <summary>
/// A small focus cue shared by text buttons. It only marks navigation state
/// and leaves the control's authored background untouched.
/// </summary>
public partial class UiSelectionLight : Control
{
    private Control _target;
    private ColorRect _marker;
    private float _reveal;
    private bool _inputConnected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ShowBehindParent = true;
        SetProcess(true);
    }

    public void Configure(
        Control target,
        Material markerMaterial)
    {
        _target = target;
        MouseFilter = MouseFilterEnum.Ignore;
        ShowBehindParent = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        if (!IsInstanceValid(_marker))
        {
            _marker = new ColorRect
            {
                Name = "SelectionLight",
                Color = Colors.White,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_marker);
        }
        _marker.Material = markerMaterial;
        if (!_inputConnected)
        {
            _target.GuiInput += OnTargetGuiInput;
            _inputConnected = true;
        }
        UpdateMarker();
    }

    public override void _ExitTree()
    {
        if (_inputConnected &&
            IsInstanceValid(_target))
        {
            _target.GuiInput -= OnTargetGuiInput;
        }
        _inputConnected = false;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_target))
        {
            return;
        }

        bool active =
            _target.IsVisibleInTree() &&
            (IsPointerInsideTarget() ||
                (_target.HasFocus() &&
                    GamepadInput
                        .ShouldShowNavigationFocus) ||
                (_target is BaseButton button &&
                    button.ButtonPressed &&
                    GamepadInput
                        .ShouldShowNavigationFocus));
        _reveal = Mathf.MoveToward(
            _reveal,
            active ? 1.0f : 0.0f,
            (float)delta * 7.5f);
        UpdateMarker();
    }

    private static void OnTargetGuiInput(
        InputEvent inputEvent)
    {
        GamepadInput.Observe(inputEvent);
    }

    private bool IsPointerInsideTarget()
    {
        if (!IsInstanceValid(_target) ||
            _target.GetViewport() == null)
        {
            return false;
        }
        return new Rect2(
            Vector2.Zero,
            _target.Size).HasPoint(
                _target.GetLocalMousePosition());
    }

    private void UpdateMarker()
    {
        if (!IsInstanceValid(_marker) ||
            _reveal <= 0.01f ||
            Size.X < 12.0f)
        {
            _marker?.Hide();
            return;
        }

        float eased = Mathf.SmoothStep(
            0.0f,
            1.0f,
            _reveal);
        float restingInset = Mathf.Min(
            Size.X * 0.33f,
            36.0f);
        float inset = Mathf.Lerp(
            restingInset,
            5.0f,
            eased);
        _marker.Position = new Vector2(
            inset,
            Mathf.Max(0.0f, Size.Y - 2.0f));
        _marker.Size = new Vector2(
            Mathf.Max(1.0f, Size.X - inset * 2.0f),
            2.0f);
        _marker.Modulate = new Color(
            1.0f,
            1.0f,
            1.0f,
            eased);
        _marker.Show();
    }
}
