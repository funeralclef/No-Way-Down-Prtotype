using Godot;

/// <summary>Secondary animation for the cyclops; the Character wrapper owns the float animation.</summary>
public partial class RunnerVisualMotion : Node3D
{
    private Player _player;
    private Node3D _leftFin, _rightFin, _leftFoot, _rightFoot;
    private float _time;
    public override void _Ready()
    {
        _player = GetParent()?.GetParent()?.GetParent() as Player;
        _leftFin = GetNode<Node3D>("LeftFin"); _rightFin = GetNode<Node3D>("RightFin");
        _leftFoot = GetNode<Node3D>("LeftFoot"); _rightFoot = GetNode<Node3D>("RightFoot");
    }
    public override void _Process(double delta)
    {
        _time += (float)delta;
        float speed = _player == null ? 0 : new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();
        float strength = Mathf.Clamp(speed / 18, 0, 1);
        float flutter = Mathf.Sin(_time * 4) * .04f;
        _leftFin.Rotation = new Vector3(-.35f - strength * .25f, 0, .45f + flutter);
        _rightFin.Rotation = new Vector3(-.35f - strength * .25f, 0, -.45f - flutter);
        float stride = Mathf.Sin(_time * 10) * strength * .09f;
        _leftFoot.Position = new Vector3(-.34f, -.53f + stride, -.08f);
        _rightFoot.Position = new Vector3(.34f, -.53f - stride, -.08f);
    }
}
