using Godot;

/// <summary>Quiet creature expression; secondary motion never changes the gameplay wrapper or collision.</summary>
public partial class EnemyCreatureMotion : Node3D
{
    private Node3D _eye, _secondEye, _left, _right, _tail;
    private Vector3 _leftRest, _rightRest, _tailRest;
    private float _time;
    private float _blinkAt;
    public override void _Ready()
    {
        _eye = GetNodeOrNull<Node3D>("Eyes/Blink");
        _secondEye = GetNodeOrNull<Node3D>("SecondEyes/Blink");
        _left = GetNodeOrNull<Node3D>("LeftAppendage"); _right = GetNodeOrNull<Node3D>("RightAppendage");
        _tail = GetNodeOrNull<Node3D>("TailGroup");
        _leftRest = _left?.Rotation ?? Vector3.Zero; _rightRest = _right?.Rotation ?? Vector3.Zero; _tailRest = _tail?.Rotation ?? Vector3.Zero;
        // Instance variation without advancing encounter/world generation RNG.
        _time = (GetInstanceId() % 997) * .013f;
        _blinkAt = _time + 2.4f;
    }
    public override void _Process(double delta)
    {
        _time += (float)delta;
        float sinceBlink = _time - _blinkAt;
        if (sinceBlink > .32f) _blinkAt = _time + 2.8f + Mathf.Abs(Mathf.Sin(_time)) * 2.1f;
        Blink(_eye, sinceBlink); Blink(_secondEye, sinceBlink - .085f);
        float sway = Mathf.Sin(_time * 2.6f) * .055f;
        if (_left != null) _left.Rotation = _leftRest + new Vector3(sway * .5f, 0, sway);
        if (_right != null) _right.Rotation = _rightRest + new Vector3(-sway * .5f, 0, -sway);
        if (_tail != null) _tail.Rotation = _tailRest + new Vector3(0, sway * 2, 0);
    }
    private static void Blink(Node3D eye, float phase)
    {
        if (eye == null) return;
        float close = phase is >= 0 and < .18f ? Mathf.Sin(phase / .18f * Mathf.Pi) : 0;
        eye.Scale = new Vector3(1, Mathf.Max(.08f, 1 - close), 1);
    }
}
