using Godot;

/// <summary>
/// A deterministic moving obstacle. Both halves of a kinetic gate share the
/// same phase so the opening moves without changing its readable width.
/// </summary>
public partial class OscillatingObstacle :
    AnimatableBody3D
{
    [Export]
    public Vector3 LocalAxis { get; set; } =
        Vector3.Right;

    [Export(PropertyHint.Range, "0.0,8.0,0.1")]
    public float Amplitude { get; set; } =
        3.0f;

    [Export(PropertyHint.Range, "0.2,4.0,0.05")]
    public float Frequency { get; set; } =
        0.72f;

    [Export(PropertyHint.Range, "0.0,6.283,0.01")]
    public float Phase { get; set; }

    private Vector3 _origin;
    private float _elapsed;

    public override void _Ready()
    {
        _origin = Position;
        SyncToPhysics = true;
    }

    public override void _PhysicsProcess(
        double delta)
    {
        _elapsed +=
            (float)delta;
        Vector3 axis =
            LocalAxis.IsZeroApprox()
                ? Vector3.Right
                : LocalAxis.Normalized();
        Position =
            _origin +
            axis *
                Mathf.Sin(
                    _elapsed *
                        Frequency *
                        Mathf.Tau +
                    Phase) *
                Amplitude;
    }
}
