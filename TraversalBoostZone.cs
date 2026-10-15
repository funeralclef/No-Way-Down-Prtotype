using Godot;

public partial class TraversalBoostZone : Area3D
{
    [Export(PropertyHint.Range, "0.25,4.0,0.25")]
    public float SpeedBoost { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float BoostDuration { get; set; } = 0.85f;

    [Export]
    public bool RequiresAirbornePlayer { get; set; }

    private bool _consumed;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_consumed || body is not Player player)
        {
            return;
        }

        if (RequiresAirbornePlayer &&
            player.IsOnFloor() &&
            player.Velocity.Y <= 1.0f)
        {
            return;
        }

        _consumed = true;
        player.ApplyTraversalSpeedBoost(SpeedBoost, BoostDuration);
    }
}
