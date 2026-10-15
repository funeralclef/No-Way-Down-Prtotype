using Godot;

public partial class MomentumZone : Area3D
{
    [Export(PropertyHint.Range, "0.0,50.0,1.0")]
    public float MomentumReward { get; set; } = 14.0f;

    [Export]
    public bool OneShot { get; set; } = true;

    private bool _claimed;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_claimed && OneShot)
        {
            return;
        }

        if (body is not Player player)
        {
            return;
        }

        player.RewardDangerousSection(MomentumReward);
        _claimed = true;
    }
}
