using Godot;
using System.Collections.Generic;

/// <summary>
/// Prewarms a readable chain of stomp targets while the river is still ahead
/// of the camera. This keeps relay enemies synchronized with the gap without
/// letting them visibly pop into existence beside the player.
/// </summary>
public partial class TraversalRelayTrigger : Area3D
{
    [Signal]
    public delegate void EnemyRequestedEventHandler(
        Vector3 worldPosition,
        Vector3 courseDirection,
        int relayIndex);

    private readonly List<Vector3>
        _localRelayPositions = new();
    private bool _triggered;

    public void Configure(
        IEnumerable<Vector3> relayPositions)
    {
        _localRelayPositions.Clear();
        _localRelayPositions.AddRange(
            relayPositions);
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_triggered ||
            body is not Player)
        {
            return;
        }

        _triggered = true;
        SetDeferred(
            Area3D.PropertyName.Monitoring,
            false);
        Vector3 courseDirection =
            GlobalBasis *
                Vector3.Forward;
        courseDirection.Y = 0.0f;
        courseDirection =
            courseDirection.IsZeroApprox()
                ? Vector3.Forward
                : courseDirection.Normalized();
        for (int index = 0;
            index <
                _localRelayPositions.Count;
            index++)
        {
            EmitSignal(
                SignalName.EnemyRequested,
                ToGlobal(
                    _localRelayPositions[
                        index]),
                courseDirection,
                index);
        }
    }
}
