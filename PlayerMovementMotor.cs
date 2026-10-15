using Godot;

/// <summary>
/// Pure movement calculations. Player remains the sole owner of Velocity and
/// the only class that calls CharacterBody3D.MoveAndSlide().
/// </summary>
public static class PlayerMovementMotor
{
    public static Vector2 MovePlanarVelocity(
        Vector2 currentVelocity,
        Vector2 desiredVelocity,
        bool hasDirection,
        bool onFloor,
        float groundAcceleration,
        float groundTurnAcceleration,
        float groundDeceleration,
        float airAcceleration,
        float airTurnAcceleration,
        float airDeceleration,
        float accelerationMultiplier,
        float deltaSeconds)
    {
        float acceleration;
        if (!hasDirection)
        {
            acceleration = onFloor
                ? groundDeceleration
                : airDeceleration;
        }
        else
        {
            float alignment =
                currentVelocity.IsZeroApprox() ||
                desiredVelocity.IsZeroApprox()
                    ? 1.0f
                    : currentVelocity
                        .Normalized()
                        .Dot(
                            desiredVelocity
                                .Normalized());
            acceleration = onFloor
                ? alignment < 0.6f
                    ? groundTurnAcceleration
                    : groundAcceleration
                : alignment < 0.35f
                    ? airTurnAcceleration
                    : airAcceleration;
        }

        return currentVelocity.MoveToward(
            desiredVelocity,
            Mathf.Max(
                0.0f,
                acceleration) *
            Mathf.Max(
                0.0f,
                accelerationMultiplier) *
            Mathf.Max(
                0.0f,
                deltaSeconds));
    }

    public static float GetGravityMultiplier(
        bool playerJumpActive,
        bool jumpHeld,
        float verticalVelocity,
        float releaseMultiplier,
        float apexVelocityRange,
        float apexMultiplier)
    {
        if (playerJumpActive &&
            verticalVelocity > 0.0f &&
            !jumpHeld)
        {
            return releaseMultiplier;
        }
        if (playerJumpActive &&
            Mathf.Abs(verticalVelocity) <=
                apexVelocityRange)
        {
            return apexMultiplier;
        }
        return 1.0f;
    }
}
