using Godot;

/// <summary>
/// Keeps controller detection, stick shaping, prompts, and haptics
/// consistent across gameplay and menus.
/// </summary>
public static class GamepadInput
{
    public static int ActiveDevice { get; private set; } = -1;
    public static bool LastInputWasGamepad { get; private set; }
    public static bool LastInputWasPointer { get; private set; }
    public static bool HasObservedInputDevice { get; private set; }

    public static bool ShouldShowGamepadPrompts =>
        LastInputWasGamepad ||
        (!HasObservedInputDevice &&
            Input.GetConnectedJoypads().Count > 0);

    public static bool ShouldShowNavigationFocus =>
        ShouldShowGamepadPrompts ||
        (HasObservedInputDevice &&
            !LastInputWasPointer);

    public static void Observe(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventJoypadButton button
                when button.Pressed:
                ActiveDevice = button.Device;
                LastInputWasGamepad = true;
                LastInputWasPointer = false;
                HasObservedInputDevice = true;
                break;
            case InputEventJoypadMotion motion
                when Mathf.Abs(motion.AxisValue) >= 0.28f:
                ActiveDevice = motion.Device;
                LastInputWasGamepad = true;
                LastInputWasPointer = false;
                HasObservedInputDevice = true;
                break;
            case InputEventKey key
                when key.Pressed && !key.Echo:
                LastInputWasGamepad = false;
                LastInputWasPointer = false;
                HasObservedInputDevice = true;
                break;
            case InputEventMouseButton mouse
                when mouse.Pressed:
            case InputEventScreenTouch touch
                when touch.Pressed:
                LastInputWasGamepad = false;
                LastInputWasPointer = true;
                HasObservedInputDevice = true;
                break;
            case InputEventMouseMotion mouseMotion
                when mouseMotion.Relative.LengthSquared() >
                    0.01f:
            case InputEventScreenDrag screenDrag
                when screenDrag.Relative.LengthSquared() >
                    0.01f:
                LastInputWasGamepad = false;
                LastInputWasPointer = true;
                HasObservedInputDevice = true;
                break;
        }
    }

    public static void EnsureUiMappings()
    {
        EnsureKeyMapping(
            "ui_accept",
            Key.Enter);
        EnsureKeyMapping(
            "ui_cancel",
            Key.Escape);
        EnsureKeyMapping(
            "ui_up",
            Key.W);
        EnsureKeyMapping(
            "ui_down",
            Key.S);
        EnsureKeyMapping(
            "ui_left",
            Key.A);
        EnsureKeyMapping(
            "ui_right",
            Key.D);
        EnsureButtonMapping("ui_accept", JoyButton.A);
        EnsureButtonMapping("ui_cancel", JoyButton.B);
        EnsureButtonMapping("ui_up", JoyButton.DpadUp);
        EnsureButtonMapping("ui_down", JoyButton.DpadDown);
        EnsureButtonMapping("ui_left", JoyButton.DpadLeft);
        EnsureButtonMapping("ui_right", JoyButton.DpadRight);
        EnsureAxisMapping("ui_up", JoyAxis.LeftY, -1.0f);
        EnsureAxisMapping("ui_down", JoyAxis.LeftY, 1.0f);
        EnsureAxisMapping("ui_left", JoyAxis.LeftX, -1.0f);
        EnsureAxisMapping("ui_right", JoyAxis.LeftX, 1.0f);
        EnsureButtonMapping(
            "menu_tab_left",
            JoyButton.LeftShoulder);
        EnsureButtonMapping(
            "menu_tab_right",
            JoyButton.RightShoulder);
        EnsureButtonMapping(
            "pause",
            JoyButton.Start);
    }

    public static void EnsureGameplayMappings()
    {
        // RB is contextual: a short ground burst or an airborne target rift.
        RemoveConflictingDashMappings(
            "dash",
            JoyButton.RightShoulder);
        EnsureButtonMapping(
            "dash",
            JoyButton.RightShoulder);

        // RT remains analog all the way through the action system so partial
        // trigger travel produces partial thrust and fuel consumption.
        EnsureAxisMapping(
            "nitro_throttle",
            JoyAxis.TriggerRight,
            1.0f);
    }

    public static Vector2 ShapeLeftStick(
        Vector2 fallbackInput,
        float innerDeadZone,
        float outerDeadZone,
        float responseExponent)
    {
        int device = ResolveActiveDevice();
        if (device < 0 || !LastInputWasGamepad)
        {
            return fallbackInput;
        }

        Vector2 rawInput = new(
            Input.GetJoyAxis(device, JoyAxis.LeftX),
            Input.GetJoyAxis(device, JoyAxis.LeftY));
        float magnitude = rawInput.Length();
        float inner = Mathf.Clamp(innerDeadZone, 0.0f, 0.8f);
        float outer = Mathf.Clamp(
            outerDeadZone,
            inner + 0.05f,
            1.0f);
        if (magnitude <= inner)
        {
            // Keep D-pad and keyboard actions available even while a
            // controller is the active device.
            return fallbackInput;
        }

        float normalizedMagnitude = Mathf.Clamp(
            (magnitude - inner) /
                Mathf.Max(0.05f, outer - inner),
            0.0f,
            1.0f);
        float shapedMagnitude = Mathf.Pow(
            normalizedMagnitude,
            Mathf.Clamp(responseExponent, 0.5f, 2.0f));
        return rawInput.Normalized() * shapedMagnitude;
    }

    public static void Rumble(
        float weakMagnitude,
        float strongMagnitude,
        float duration)
    {
        int device = ResolveActiveDevice();
        if (device < 0)
        {
            return;
        }

        Input.StartJoyVibration(
            device,
            Mathf.Clamp(weakMagnitude, 0.0f, 1.0f),
            Mathf.Clamp(strongMagnitude, 0.0f, 1.0f),
            Mathf.Max(0.01f, duration));
    }

    private static int ResolveActiveDevice()
    {
        Godot.Collections.Array<int> connected =
            Input.GetConnectedJoypads();
        if (ActiveDevice >= 0 &&
            connected.Contains(ActiveDevice))
        {
            return ActiveDevice;
        }

        ActiveDevice = connected.Count > 0
            ? connected[0]
            : -1;
        return ActiveDevice;
    }

    private static void EnsureButtonMapping(
        string action,
        JoyButton buttonIndex)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action, 0.2f);
        }

        foreach (InputEvent inputEvent in
            InputMap.ActionGetEvents(action))
        {
            if (inputEvent is InputEventJoypadButton button &&
                button.ButtonIndex == buttonIndex)
            {
                return;
            }
        }

        InputMap.ActionAddEvent(
            action,
            new InputEventJoypadButton
            {
                Device = -1,
                ButtonIndex = buttonIndex
            });
    }

    private static void EnsureKeyMapping(
        string action,
        Key physicalKey)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action, 0.2f);
        }

        foreach (InputEvent inputEvent in
            InputMap.ActionGetEvents(action))
        {
            if (inputEvent is InputEventKey key &&
                key.PhysicalKeycode ==
                    physicalKey)
            {
                return;
            }
        }

        InputMap.ActionAddEvent(
            action,
            new InputEventKey
            {
                PhysicalKeycode =
                    physicalKey
            });
    }

    private static void EnsureAxisMapping(
        string action,
        JoyAxis axisIndex,
        float axisValue)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action, 0.2f);
        }

        foreach (InputEvent inputEvent in
            InputMap.ActionGetEvents(action))
        {
            if (inputEvent is InputEventJoypadMotion motion &&
                motion.Axis == axisIndex &&
                Mathf.Sign(motion.AxisValue) ==
                    Mathf.Sign(axisValue))
            {
                return;
            }
        }

        InputMap.ActionAddEvent(
            action,
            new InputEventJoypadMotion
            {
                Device = -1,
                Axis = axisIndex,
                AxisValue = axisValue
            });
    }

    private static void RemoveConflictingDashMappings(
        string action,
        JoyButton allowedButton)
    {
        if (!InputMap.HasAction(action))
        {
            return;
        }

        foreach (InputEvent inputEvent in
            InputMap.ActionGetEvents(action))
        {
            bool remove =
                inputEvent is
                    InputEventJoypadMotion ||
                inputEvent is
                    InputEventJoypadButton button &&
                button.ButtonIndex !=
                    allowedButton;
            if (remove)
            {
                InputMap.ActionEraseEvent(
                    action,
                    inputEvent);
            }
        }
    }
}
