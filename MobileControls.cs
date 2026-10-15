using Godot;
using System.Collections.Generic;

/// <summary>
/// A responsive, multi-touch virtual joystick and jump button.
/// The controls stay hidden on desktop unless ShowOnDesktop is enabled.
/// </summary>
public partial class MobileControls : Control
{
    private readonly List<Control> _touchExclusions = new();
    public void RegisterTouchExclusion(Control control)
    { if (!_touchExclusions.Contains(control)) _touchExclusions.Add(control); }
    private bool IsOverTouchExclusion(Vector2 point)
    {
        _touchExclusions.RemoveAll(control => !IsInstanceValid(control));
        foreach (var control in _touchExclusions)
            if (control.IsVisibleInTree() && control.GetGlobalRect().HasPoint(point)) return true;
        return false;
    }
    [Export(PropertyHint.Range, "0.0,0.9,0.01")]
    public float DeadZone { get; set; } = 0.18f;

    [Export]
    public bool ShowOnDesktop { get; set; }

    [Export(PropertyHint.Range, "0.2,0.5,0.01")]
    public float NitroDoubleSwipeWindow { get; set; } =
        0.28f;

    [Export(PropertyHint.Range, "0.16,0.5,0.01")]
    public float NitroSharpSwipeMaximumTime { get; set; } =
        0.32f;

    public Vector2 MovementVector { get; private set; } = Vector2.Zero;
    public bool IsTouchMode { get; private set; }
    public bool IsJumpHeld => _jumpHeld;
    public bool IsDashHeld =>
        _nitroGestureTouchId >= 0;
    public float NitroThrottle => 0.0f;

    private int _movementTouchId = -1;
    private int _jumpTouchId = -1;
    private int _nitroGestureTouchId = -1;
    private bool _jumpQueued;
    private bool _jumpHeld;
    private bool _nitroPulseQueued;
    private bool _nitroMaximumQueued;
    private bool _nitroStopQueued;
    private bool _nitroUpSwipePending;
    private bool _nitroSecondSwipeArmed;
    private ulong _nitroUpSwipePendingSinceMs;
    private Vector2 _nitroGestureStart;
    private Vector2 _nitroGestureCurrent;
    private ulong _nitroGestureStartedMs;
    private bool _settingsMenuOpen;
    private bool _inputDisabled;
    private bool _shouldBeVisible;
    private float _visibilityAmount;
    private float _visualOffsetY;

    private Vector2 _joystickCenter;
    private Vector2 _jumpCenter;
    private Rect2 _nitroGestureRect;
    private float _joystickRadius;
    private float _knobRadius;
    private float _jumpRadius;
    private Vector2 _lastControlSize;
    private UiAccessibility _uiAccessibility;
    private Texture2D _jumpIcon;

    private static readonly Color PanelColor =
        new(0.006f, 0.015f, 0.026f, 0.3f);
    private static readonly Color OutlineColor =
        new(0.66f, 0.82f, 0.87f, 0.42f);
    private static readonly Color AccentColor =
        new(0.34f, 0.86f, 1.0f, 0.88f);
    private static readonly Color ActiveColor =
        new(1.0f, 0.55f, 0.16f, 0.96f);
    public override void _EnterTree()
    {
        // Hide before the first render. Touch layouts opt in from _Ready().
        Hide();
        Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _uiAccessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        IsTouchMode =
            _uiAccessibility?.IsTouchLayout ??
            (OS.HasFeature("mobile") ||
                DisplayServer.IsTouchscreenAvailable());
        _jumpIcon =
            UiIconLibrary.Get(
                UiIconLibrary.Up);
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged +=
                OnInterfaceScaleChanged;
        }
        UpdateInputVisibility();
        AddToGroup("mobile_controls");
        UpdateLayout();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_uiAccessibility != null)
        {
            _uiAccessibility.ScaleChanged -=
                OnInterfaceScaleChanged;
        }
    }

    public override void _Process(double delta)
    {
        UpdateNitroSwipeSequence();
        float target =
            _shouldBeVisible ? 1.0f : 0.0f;
        float speed =
            _shouldBeVisible ? 4.2f : 5.6f;
        float previous =
            _visibilityAmount;
        _visibilityAmount =
            Mathf.MoveToward(
                _visibilityAmount,
                target,
                (float)delta * speed);
        float eased =
            _visibilityAmount *
            _visibilityAmount *
            (3.0f -
                2.0f * _visibilityAmount);
        _visualOffsetY =
            (1.0f - eased) *
            Mathf.Clamp(
                Size.Y * 0.09f,
                38.0f,
                78.0f);
        Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                eased);
        if (!_shouldBeVisible &&
            _visibilityAmount <= 0.001f)
        {
            Hide();
        }
        if (!Mathf.IsEqualApprox(
                previous,
                _visibilityAmount))
        {
            QueueRedraw();
        }

        if (Size != _lastControlSize)
        {
            UpdateLayout();
            QueueRedraw();
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!Visible || _settingsMenuOpen || _inputDisabled)
        {
            return;
        }

        if (inputEvent is InputEventScreenTouch touch)
        {
            HandleTouch(touch.Index, touch.Position, touch.Pressed);
        }
        else if (inputEvent is InputEventScreenDrag drag)
        {
            HandleDrag(drag.Index, drag.Position);
        }
        else if (ShowOnDesktop && inputEvent is InputEventMouseButton mouseButton &&
                 mouseButton.ButtonIndex == MouseButton.Left)
        {
            HandleTouch(0, mouseButton.Position, mouseButton.Pressed);
        }
        else if (ShowOnDesktop && inputEvent is InputEventMouseMotion mouseMotion &&
                 Input.IsMouseButtonPressed(MouseButton.Left))
        {
            HandleDrag(0, mouseMotion.Position);
        }
    }

    public override void _Draw()
    {
        if (!Visible)
        {
            return;
        }

        DrawSetTransform(
            Vector2.Down *
                _visualOffsetY);
        float lineWidth = Mathf.Max(
            1.8f,
            _joystickRadius * 0.034f);

        DrawCircle(_joystickCenter, _joystickRadius, PanelColor);
        DrawArc(
            _joystickCenter,
            _joystickRadius,
            0.0f,
            Mathf.Tau,
            64,
            OutlineColor,
            lineWidth,
            true);
        DrawArc(
            _joystickCenter,
            _joystickRadius,
            -2.3f,
            -0.84f,
            24,
            new Color(
                AccentColor.R,
                AccentColor.G,
                AccentColor.B,
                0.62f),
            lineWidth * 1.7f,
            true);

        Vector2 knobPosition = _joystickCenter + MovementVector * (_joystickRadius - _knobRadius * 0.65f);
        Color movementColor =
            MovementVector.IsZeroApprox()
                ? AccentColor
                : ActiveColor;
        DrawCircle(
            knobPosition,
            _knobRadius,
            new Color(
                movementColor.R,
                movementColor.G,
                movementColor.B,
                0.22f));
        DrawArc(
            knobPosition,
            _knobRadius,
            0.0f,
            Mathf.Tau,
            48,
            movementColor,
            lineWidth * 1.2f,
            true);
        DrawCircle(
            knobPosition,
            Mathf.Max(2.4f, _knobRadius * 0.11f),
            movementColor);

        float jumpScale =
            _jumpHeld ? 1.045f : 1.0f;
        DrawCircle(
            _jumpCenter,
            _jumpRadius * jumpScale,
            _jumpHeld
                ? new Color(
                    ActiveColor.R,
                    ActiveColor.G,
                    ActiveColor.B,
                    0.2f)
                : PanelColor);
        DrawArc(
            _jumpCenter,
            _jumpRadius * jumpScale,
            -2.45f,
            0.72f,
            42,
            _jumpHeld
                ? ActiveColor
                : OutlineColor,
            lineWidth * 1.25f,
            true);

        DrawJumpArrow(lineWidth);
    }

    public bool ConsumeJumpPressed()
    {
        if (!_jumpQueued)
        {
            return false;
        }

        _jumpQueued = false;
        return true;
    }

    public bool ConsumeDashPressed()
    {
        return ConsumeRiftStep();
    }

    public bool ConsumeRiftStep()
    {
        return ConsumeNitroPulse();
    }

    public bool ConsumeNitroPulse()
    {
        if (!_nitroPulseQueued)
        {
            return false;
        }

        _nitroPulseQueued = false;
        return true;
    }

    public bool ConsumeNitroMaximum()
    {
        if (!_nitroMaximumQueued)
        {
            return false;
        }

        _nitroMaximumQueued = false;
        return true;
    }

    public bool ConsumeNitroStop()
    {
        if (!_nitroStopQueued)
        {
            return false;
        }

        _nitroStopQueued = false;
        return true;
    }

    public void DisableInput()
    {
        _inputDisabled = true;
        ResetInputState();
        UpdateInputVisibility();
    }

    public void SetSettingsMenuOpen(bool isOpen)
    {
        _settingsMenuOpen = isOpen;
        ResetInputState();
        UpdateInputVisibility();
    }

    private void ResetInputState()
    {
        MovementVector = Vector2.Zero;
        _movementTouchId = -1;
        _jumpTouchId = -1;
        _nitroGestureTouchId = -1;
        _jumpQueued = false;
        _jumpHeld = false;
        _nitroPulseQueued = false;
        _nitroMaximumQueued = false;
        _nitroStopQueued = false;
        _nitroUpSwipePending = false;
        _nitroSecondSwipeArmed = false;
        _nitroUpSwipePendingSinceMs =
            0;
        _nitroGestureStart = Vector2.Zero;
        _nitroGestureCurrent = Vector2.Zero;
        _nitroGestureStartedMs = 0;
    }

    private void UpdateInputVisibility()
    {
        _shouldBeVisible =
            !_inputDisabled &&
            !_settingsMenuOpen &&
            (IsTouchMode || ShowOnDesktop);
        if (_shouldBeVisible)
        {
            Show();
        }
        QueueRedraw();
    }

    private void HandleTouch(int touchId, Vector2 position, bool pressed)
    {
        if (pressed && IsOverTouchExclusion(position)) return;
        if (pressed)
        {
            if (_nitroGestureTouchId == -1 &&
                _nitroGestureRect
                    .Grow(18.0f)
                    .HasPoint(position))
            {
                _nitroGestureTouchId =
                    touchId;
                _nitroGestureStart =
                    position;
                _nitroGestureCurrent =
                    position;
                _nitroGestureStartedMs =
                    Time.GetTicksMsec();
                _nitroSecondSwipeArmed =
                    _nitroUpSwipePending &&
                    _nitroGestureStartedMs -
                        _nitroUpSwipePendingSinceMs <=
                        GetDoubleSwipeWindowMs();
                QueueRedraw();
                return;
            }

            // Broad lower-screen hit zones make the controls forgiving on small phones.
            if (_jumpTouchId == -1 && position.X > Size.X * 0.55f && position.Y > Size.Y * 0.42f)
            {
                _jumpTouchId = touchId;
                _jumpHeld = true;
                _jumpQueued = true;
                QueueRedraw();
                return;
            }

            if (_movementTouchId == -1 && position.X <= Size.X * 0.55f && position.Y > Size.Y * 0.35f)
            {
                _movementTouchId = touchId;
                UpdateMovement(position);
            }

            return;
        }

        if (touchId == _movementTouchId)
        {
            _movementTouchId = -1;
            MovementVector = Vector2.Zero;
            QueueRedraw();
        }

        if (touchId == _jumpTouchId)
        {
            _jumpTouchId = -1;
            _jumpHeld = false;
            QueueRedraw();
        }

        if (touchId ==
            _nitroGestureTouchId)
        {
            CompleteNitroGesture(
                position);
            _nitroGestureTouchId = -1;
            QueueRedraw();
        }
    }

    private void HandleDrag(int touchId, Vector2 position)
    {
        if (touchId == _movementTouchId)
        {
            UpdateMovement(position);
        }
        else if (touchId ==
            _nitroGestureTouchId)
        {
            _nitroGestureCurrent =
                position;
            QueueRedraw();
        }
    }

    private void UpdateMovement(Vector2 touchPosition)
    {
        Vector2 rawVector = (touchPosition - _joystickCenter) / _joystickRadius;
        float rawLength = Mathf.Min(rawVector.Length(), 1.0f);

        if (rawLength <= DeadZone)
        {
            MovementVector = Vector2.Zero;
        }
        else
        {
            float adjustedLength = (rawLength - DeadZone) / (1.0f - DeadZone);
            MovementVector = rawVector.Normalized() * adjustedLength;
        }

        QueueRedraw();
    }

    private void CompleteNitroGesture(
        Vector2 endPosition)
    {
        _nitroGestureCurrent =
            endPosition;
        Vector2 swipe =
            endPosition -
            _nitroGestureStart;
        float threshold =
            Mathf.Clamp(
                _nitroGestureRect.Size.Y *
                    0.22f,
                38.0f,
                72.0f);
        if (Mathf.Abs(swipe.Y) <
                threshold ||
            Mathf.Abs(swipe.Y) <
                Mathf.Abs(swipe.X) *
                    1.15f)
        {
            _nitroGestureStartedMs = 0;
            return;
        }

        if (swipe.Y < 0.0f)
        {
            ulong now =
                Time.GetTicksMsec();
            ulong heldMs =
                _nitroGestureStartedMs > 0
                    ? now -
                        _nitroGestureStartedMs
                    : 0;
            if (heldMs >
                GetSharpSwipeMaximumMs())
            {
                _nitroGestureStartedMs = 0;
                _nitroSecondSwipeArmed =
                    false;
                return;
            }

            bool completesDoubleSwipe =
                _nitroUpSwipePending &&
                (_nitroSecondSwipeArmed ||
                    now -
                        _nitroUpSwipePendingSinceMs <=
                        GetDoubleSwipeWindowMs());
            if (completesDoubleSwipe)
            {
                _nitroUpSwipePending = false;
                _nitroUpSwipePendingSinceMs =
                    0;
                _nitroPulseQueued = false;
                _nitroMaximumQueued = true;
            }
            else
            {
                if (_nitroUpSwipePending)
                {
                    _nitroPulseQueued = true;
                }
                _nitroUpSwipePending = true;
                _nitroUpSwipePendingSinceMs =
                    now;
            }
        }
        else
        {
            _nitroUpSwipePending = false;
            _nitroUpSwipePendingSinceMs =
                0;
            _nitroPulseQueued = false;
            _nitroStopQueued = true;
        }
        _nitroGestureStartedMs = 0;
        _nitroSecondSwipeArmed = false;
    }

    private void UpdateNitroSwipeSequence()
    {
        if (!_nitroUpSwipePending ||
            _nitroGestureTouchId >= 0)
        {
            return;
        }

        ulong now =
            Time.GetTicksMsec();
        if (now -
                _nitroUpSwipePendingSinceMs <
            GetDoubleSwipeWindowMs())
        {
            return;
        }

        _nitroUpSwipePending = false;
        _nitroUpSwipePendingSinceMs =
            0;
        _nitroPulseQueued = true;
    }

    private ulong GetDoubleSwipeWindowMs()
    {
        return (ulong)Mathf.RoundToInt(
            Mathf.Clamp(
                NitroDoubleSwipeWindow,
                0.2f,
                0.5f) *
            1000.0f);
    }

    private ulong GetSharpSwipeMaximumMs()
    {
        return (ulong)Mathf.RoundToInt(
            Mathf.Clamp(
                NitroSharpSwipeMaximumTime,
                0.16f,
                0.5f) *
            1000.0f);
    }

    public Vector2 SkillButtonPosition(Vector2 buttonSize) => new(
        Mathf.Max(14, Mathf.Min(_joystickCenter.X - buttonSize.X * .5f, _nitroGestureRect.Position.X - buttonSize.X - 14)),
        _joystickCenter.Y - _joystickRadius - buttonSize.Y - 14);

    private void UpdateLayout()
    {
        _lastControlSize = Size;
        float scale =
            Mathf.Clamp(
                Size.Y / 540.0f,
                0.75f,
                1.45f) *
            (_uiAccessibility?.UiScale ??
                1.0f);
        Vector4 safeInsets =
            _uiAccessibility?.GetSafeInsets(
                Size) ??
            Vector4.Zero;
        float leftSafeInset =
            safeInsets.X;
        float rightSafeInset =
            safeInsets.Z;
        float bottomSafeInset =
            safeInsets.W;

        float sideInset = 34.0f * scale;
        float bottomInset = 30.0f * scale + bottomSafeInset;

        _joystickRadius = 70.0f * scale;
        _knobRadius = 29.0f * scale;
        _jumpRadius = 55.0f * scale;

        _joystickCenter = new Vector2(
            leftSafeInset + sideInset + _joystickRadius,
            Size.Y - bottomInset - _joystickRadius);
        _jumpCenter = new Vector2(
            Size.X - rightSafeInset - sideInset - _jumpRadius,
            Size.Y - bottomInset - _jumpRadius);
        Vector2 nitroSize =
            new(
                112.0f * scale,
                210.0f * scale);
        _nitroGestureRect =
            new Rect2(
                new Vector2(
                    Size.X -
                        rightSafeInset -
                        sideInset -
                        nitroSize.X,
                    Mathf.Max(
                        safeInsets.Y +
                            96.0f * scale,
                        _jumpCenter.Y -
                            _jumpRadius -
                            nitroSize.Y -
                            18.0f * scale)),
                nitroSize);
    }

    private void OnInterfaceScaleChanged(
        float uiScale,
        float textScale)
    {
        UpdateLayout();
        QueueRedraw();
    }

    private void DrawJumpArrow(float lineWidth)
    {
        if (IsInstanceValid(_jumpIcon))
        {
            float side =
                _jumpRadius * 0.92f;
            DrawTextureRect(
                _jumpIcon,
                new Rect2(
                    _jumpCenter -
                        Vector2.One *
                            side * 0.5f,
                    Vector2.One * side),
                false,
                _jumpHeld
                    ? Colors.White
                    : AccentColor);
            return;
        }

        float arrowWidth = _jumpRadius * 0.42f;
        float arrowHeight = _jumpRadius * 0.48f;
        Vector2 tip = _jumpCenter + Vector2.Up * arrowHeight;
        Vector2 left = _jumpCenter + new Vector2(-arrowWidth, 0.0f);
        Vector2 right = _jumpCenter + new Vector2(arrowWidth, 0.0f);
        Vector2 stemBottom = _jumpCenter + Vector2.Down * arrowHeight * 0.72f;
        Color arrowColor = _jumpHeld ? Colors.White : OutlineColor;

        DrawLine(left, tip, arrowColor, lineWidth * 1.5f, true);
        DrawLine(tip, right, arrowColor, lineWidth * 1.5f, true);
        DrawLine(_jumpCenter + Vector2.Up * arrowHeight * 0.1f, stemBottom,
            arrowColor, lineWidth * 1.5f, true);
    }

}
