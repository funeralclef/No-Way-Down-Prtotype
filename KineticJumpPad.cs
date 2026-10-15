using Godot;

/// <summary>
/// Converts a landing into forward momentum. A normal drive-over produces a
/// useful hop, while an intentional fall or stomp earns a stronger launch.
/// The pad never owns the player's movement after take-off.
/// </summary>
public partial class KineticJumpPad : Area3D
{
    [Export]
    public Vector3 LocalDirection { get; set; } =
        Vector3.Forward;

    [Export(PropertyHint.Range, "12.0,40.0,0.5")]
    public float BaseForwardSpeed { get; set; } =
        27.0f;

    [Export(PropertyHint.Range, "6.0,24.0,0.5")]
    public float BaseVerticalSpeed { get; set; } =
        11.5f;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float ImpactToForward { get; set; } =
        0.42f;

    [Export(PropertyHint.Range, "0.0,0.8,0.05")]
    public float ImpactToVertical { get; set; } =
        0.28f;

    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float BoostDuration { get; set; } =
        0.88f;

    [Export(PropertyHint.Range, "0.15,1.0,0.05")]
    public float RearmTime { get; set; } =
        0.48f;

    private MeshInstance3D _impactPulse;
    private float _rearmTimer;
    private float _pulse;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        _impactPulse =
            GetNodeOrNull<MeshInstance3D>(
                "ImpactPulse");
        if (IsInstanceValid(_impactPulse))
        {
            _impactPulse.Visible = false;
        }
    }

    public override void _PhysicsProcess(
        double delta)
    {
        float deltaSeconds =
            (float)delta;
        _rearmTimer =
            Mathf.Max(
                0.0f,
                _rearmTimer -
                    deltaSeconds);
        _pulse =
            Mathf.Max(
                0.0f,
                _pulse -
                    deltaSeconds *
                        2.8f);
        if (!IsInstanceValid(_impactPulse))
        {
            return;
        }

        _impactPulse.Visible =
            _pulse > 0.001f;
        float expansion =
            Mathf.Lerp(
                0.72f,
                1.48f,
                1.0f - _pulse);
        _impactPulse.Scale =
            new Vector3(
                expansion,
                1.0f,
                expansion);
    }

    private void OnBodyEntered(
        Node3D body)
    {
        if (_rearmTimer > 0.0f ||
            body is not Player player)
        {
            return;
        }

        _rearmTimer =
            Mathf.Max(
                0.15f,
                RearmTime);
        float impactSpeed =
            Mathf.Max(
                0.0f,
                -player.Velocity.Y);
        float forwardBonus =
            Mathf.Min(
                8.5f,
                impactSpeed *
                    ImpactToForward);
        float verticalBonus =
            Mathf.Min(
                5.5f,
                impactSpeed *
                    ImpactToVertical);
        Vector3 worldDirection =
            GlobalBasis *
                LocalDirection;
        player.ApplyCourseLaunch(
            worldDirection,
            BaseForwardSpeed +
                forwardBonus,
            BaseVerticalSpeed +
                verticalBonus,
            BoostDuration +
                Mathf.Min(
                    0.24f,
                    impactSpeed *
                        0.012f));
        _pulse = 1.0f;
    }
}
