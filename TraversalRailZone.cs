using Godot;

/// <summary>
/// A short authored slide: it protects forward momentum while leaving the
/// player enough lateral authority to choose the exit line.
/// </summary>
public partial class TraversalRailZone : Area3D
{
    [Export]
    public Vector3 LocalDirection { get; set; } =
        Vector3.Forward;

    [Export(PropertyHint.Range, "12.0,42.0,0.5")]
    public float MinimumSpeed { get; set; } =
        24.0f;

    [Export(PropertyHint.Range, "0.3,3.0,0.05")]
    public float RideDuration { get; set; } =
        1.15f;

    [Export(PropertyHint.Range, "0.0,0.72,0.02")]
    public float SteeringAuthority { get; set; } =
        0.34f;

    private bool _consumed;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_consumed ||
            body is not Player player)
        {
            return;
        }

        _consumed = true;
        Vector3 worldDirection =
            GlobalBasis *
                LocalDirection;
        player.BeginTraversalRail(
            worldDirection,
            MinimumSpeed,
            RideDuration,
            SteeringAuthority);
    }
}
