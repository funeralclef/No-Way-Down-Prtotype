using Godot;

/// <summary>
/// Launches along the authored course without owning the player's movement
/// after take-off. Landing pads can require a descending approach.
/// </summary>
public partial class CourseLaunchPad : Area3D
{
    [Export]
    public Vector3 LocalDirection { get; set; } =
        Vector3.Forward;

    [Export(PropertyHint.Range, "8.0,44.0,0.5")]
    public float ForwardSpeed { get; set; } =
        22.0f;

    [Export(PropertyHint.Range, "4.0,38.0,0.5")]
    public float VerticalSpeed { get; set; } =
        26.0f;

    [Export(PropertyHint.Range, "0.2,2.5,0.05")]
    public float BoostDuration { get; set; } =
        0.85f;

    [Export]
    public bool RequiresDescendingPlayer { get; set; }

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float RearmTime { get; set; } =
        0.35f;

    private float _rearmTimer;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(
        double delta)
    {
        _rearmTimer =
            Mathf.Max(
                0.0f,
                _rearmTimer -
                    (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_rearmTimer > 0.0f ||
            body is not Player player ||
            (RequiresDescendingPlayer &&
                player.Velocity.Y >= -0.5f))
        {
            return;
        }

        _rearmTimer =
            Mathf.Max(
                0.1f,
                RearmTime);
        Vector3 worldDirection =
            GlobalBasis *
                LocalDirection;
        player.ApplyCourseLaunch(
            worldDirection,
            ForwardSpeed,
            VerticalSpeed,
            BoostDuration);
    }
}
