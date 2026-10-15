using Godot;
using System.Reflection;

/// <summary>
/// Deterministic camera run used to inspect the pressure front in an actual
/// rendered build. It is never loaded by production scenes.
/// </summary>
public partial class PressureVisualCaptureDriver : Node
{
    private int _frame;
    private Player _player;
    private AbyssPressureFront _pressure;

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        _player ??=
            GetParent()
                .GetNodeOrNull<Player>(
                    "Player");
        _pressure ??=
            GetParent()
                .GetNodeOrNull<AbyssPressureFront>(
                    "RunSystems/AbyssPressureFront");
        if (!IsInstanceValid(_player) ||
            !IsInstanceValid(_pressure))
        {
            return;
        }

        if (_frame == 5)
        {
            _player.GrantDamageInvulnerability(
                30.0f);
        }
        if (_frame == 8)
        {
            Input.ActionPress(
                "move_forward",
                0.82f);
        }
        if (_frame == 100)
        {
            Input.ActionRelease(
                "move_forward");
        }
        if (_frame == 135)
        {
            PlaceFrontAtDistance(15.0f);
        }
        if (_frame == 205)
        {
            PlaceFrontAtDistance(9.5f);
            BeginWarning();
        }
        if (_frame == 285)
        {
            Input.ActionPress(
                "move_forward",
                1.0f);
            Input.ActionPress(
                "nitro_throttle",
                1.0f);
        }
        if (_frame == 345)
        {
            Input.ActionRelease(
                "nitro_throttle");
        }
        if (_frame == 390)
        {
            Input.ActionRelease(
                "move_forward");
        }
    }

    public override void _ExitTree()
    {
        Input.ActionRelease(
            "move_forward");
        Input.ActionRelease(
            "nitro_throttle");
    }

    private void PlaceFrontAtDistance(
        float distance)
    {
        SetPrivateField(
            "_frontZ",
            _player.GlobalPosition.Z +
                distance);
    }

    private void BeginWarning()
    {
        FieldInfo lengthField =
            typeof(AbyssPressureFront)
                .GetField(
                    "_cycleLength",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
        float cycleLength =
            lengthField?.GetValue(
                _pressure) is float value
                ? value
                : 14.0f;
        SetPrivateField(
            "_tierIndex",
            3);
        SetPrivateField(
            "_cycleTimer",
            Mathf.Max(
                0.0f,
                cycleLength - 3.0f));
    }

    private void SetPrivateField(
        string fieldName,
        object value)
    {
        typeof(AbyssPressureFront)
            .GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?.SetValue(
                _pressure,
                value);
    }
}
