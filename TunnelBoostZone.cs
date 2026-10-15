using Godot;

public partial class TunnelBoostZone : Area3D
{
    [Export(PropertyHint.Range, "0.0,8.0,0.25")]
    public float SpeedBoost { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.2,5.0,0.05")]
    public float BoostDuration { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float EntryInvulnerabilityDuration { get; set; } = 1.35f;

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float ExitInvulnerabilityDuration { get; set; } = 0.55f;

    private Player _activePlayer;
    private bool _consumed;

    public override void _Ready()
    {
        AddToGroup("tunnel_clearance");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Mob mob)
        {
            if (TryGetMobYieldDirection(mob, out Vector3 yieldDirection))
            {
                mob.BeginTunnelYield(yieldDirection, 1.15f);
            }

            return;
        }

        if (body is not Player player)
        {
            return;
        }

        _activePlayer = player;
        player.EnterTunnelPhase();
        if (!_consumed)
        {
            _consumed = true;
            player.ApplyTunnelSpeedBoost(SpeedBoost, BoostDuration);
            player.GrantDamageInvulnerability(
                EntryInvulnerabilityDuration);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body != _activePlayer || !IsInstanceValid(_activePlayer))
        {
            return;
        }

        _activePlayer.GrantDamageInvulnerability(
            ExitInvulnerabilityDuration);
        _activePlayer.ExitTunnelPhase();
        _activePlayer = null;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_activePlayer))
        {
            _activePlayer.ExitTunnelPhase();
        }

        _activePlayer = null;
    }

    public bool TryGetMobYieldDirection(
        Mob mob,
        out Vector3 yieldDirection)
    {
        yieldDirection = Vector3.Zero;
        if (!IsInstanceValid(mob))
        {
            return false;
        }

        CollisionShape3D collision =
            GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (collision?.Shape is not BoxShape3D boxShape)
        {
            return false;
        }

        Vector3 localPosition = ToLocal(mob.GlobalPosition);
        Vector3 localVelocity = GlobalBasis.Inverse() * mob.Velocity;
        Vector3 futurePosition =
            localPosition +
            localVelocity * 0.72f;
        Vector2 start =
            new(localPosition.X, localPosition.Z);
        Vector2 end =
            new(futurePosition.X, futurePosition.Z);
        Vector2 segment = end - start;
        float closestRatio = segment.LengthSquared() <= 0.001f
            ? 0.0f
            : Mathf.Clamp(
                -start.Dot(segment) / segment.LengthSquared(),
                0.0f,
                1.0f);
        Vector2 closest = start + segment * closestRatio;
        Vector3 halfSize = boxShape.Size * 0.5f;
        bool inside =
            Mathf.Abs(localPosition.X) <= halfSize.X + 0.35f &&
            Mathf.Abs(localPosition.Z) <= halfSize.Z + 0.45f;
        bool pathThreatensTunnel =
            Mathf.Abs(closest.X) <= halfSize.X + 1.75f &&
            Mathf.Abs(closest.Y) <= halfSize.Z + 4.5f &&
            Mathf.Abs(localPosition.Z) <= halfSize.Z + 13.0f;
        if (!inside && !pathThreatensTunnel)
        {
            return false;
        }

        float sideSign;
        if (Mathf.Abs(localPosition.X) > 0.2f)
        {
            sideSign = Mathf.Sign(localPosition.X);
        }
        else
        {
            sideSign = mob.GetInstanceId() % 2 == 0 ? -1.0f : 1.0f;
        }

        Vector3 localYield;
        if (inside)
        {
            float exitSign = Mathf.Abs(localPosition.Z) > 0.2f
                ? Mathf.Sign(localPosition.Z)
                : (Mathf.Abs(localVelocity.Z) > 0.1f
                    ? -Mathf.Sign(localVelocity.Z)
                    : 1.0f);
            localYield = new Vector3(sideSign * 0.35f, 0.0f, exitSign);
        }
        else
        {
            float retreatSign = Mathf.Abs(localPosition.Z) > 0.2f
                ? Mathf.Sign(localPosition.Z)
                : 1.0f;
            localYield = new Vector3(
                sideSign,
                0.0f,
                retreatSign * 0.32f);
        }

        yieldDirection = GlobalBasis * localYield.Normalized();
        yieldDirection.Y = 0.0f;
        yieldDirection = yieldDirection.Normalized();
        return !yieldDirection.IsZeroApprox();
    }
}
