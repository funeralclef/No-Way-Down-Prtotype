using Godot;

/// <summary>
/// Scene-free mutable state for Nitro and Rift Step. Player owns the state
/// and remains the only node allowed to apply movement or gameplay effects.
/// </summary>
public sealed class PlayerAbilityState
{
    public float NitroCharge { get; set; }
    public float NitroSpool { get; set; }
    public float NitroThrottle { get; set; }
    public bool NitroIgnitionKickPending { get; set; }
    public bool NitroBurning { get; set; }
    public bool NitroWasBurning { get; set; }
    public bool NitroInputWasPressed { get; set; }
    public bool NitroLatched { get; set; }
    public bool NitroActionUsesHold { get; set; }
    public bool NitroActionStartedReactor { get; set; }
    public float NitroActionHoldTimer { get; set; }
    public float NitroDashCooldownTimer { get; set; }
    public float NitroRumbleTimer { get; set; }
    public float NitroDashSpeedProtectionTimer { get; set; }
    public float GroundBurstTimer { get; set; }
    public float SlipstreamDodgeWindowTimer { get; set; }
    public float RiftTeleportFxTimer { get; set; }
    public float RiftStompWindowTimer { get; set; }
    public float RiftRequestTimer { get; set; }
    public bool RiftRequestPending { get; set; }
    public Vector2 RiftRequestedHeading { get; set; }
    public Vector2 GroundBurstHeading { get; set; }
    public float GroundBurstTargetSpeed { get; set; }
    public Vector3 NitroDirection { get; set; } = Vector3.Forward;

    public void InitializeNitro(
        float initialCharge,
        float capacity)
    {
        NitroCharge = Mathf.Clamp(
            initialCharge,
            0.0f,
            Mathf.Max(0.0f, capacity));
    }

    public bool SetNitroCharge(
        float value,
        float capacity)
    {
        float clamped = Mathf.Clamp(
            value,
            0.0f,
            Mathf.Max(0.0f, capacity));
        if (Mathf.IsEqualApprox(
            clamped,
            NitroCharge))
        {
            return false;
        }

        NitroCharge = clamped;
        if (NitroCharge <= 0.0f)
        {
            NitroBurning = false;
        }
        return true;
    }

    public void Tick(float deltaSeconds)
    {
        float delta = Mathf.Max(
            0.0f,
            deltaSeconds);
        GroundBurstTimer = Decay(
            GroundBurstTimer,
            delta);
        SlipstreamDodgeWindowTimer = Decay(
            SlipstreamDodgeWindowTimer,
            delta);
        RiftTeleportFxTimer = Decay(
            RiftTeleportFxTimer,
            delta);
        RiftStompWindowTimer = Decay(
            RiftStompWindowTimer,
            delta);
        RiftRequestTimer = Decay(
            RiftRequestTimer,
            delta);
        NitroDashCooldownTimer = Decay(
            NitroDashCooldownTimer,
            delta);
        NitroRumbleTimer = Decay(
            NitroRumbleTimer,
            delta);
        NitroDashSpeedProtectionTimer = Decay(
            NitroDashSpeedProtectionTimer,
            delta);
    }

    public void QueueRift(
        Vector2 heading,
        float bufferTime)
    {
        RiftRequestPending = true;
        RiftRequestTimer = Mathf.Max(
            0.01f,
            bufferTime);
        RiftRequestedHeading = heading;
    }

    public void ClearRiftRequest()
    {
        RiftRequestPending = false;
        RiftRequestTimer = 0.0f;
        RiftRequestedHeading = Vector2.Zero;
    }

    public bool TryStartOverdrive(
        float minimumCharge)
    {
        if (NitroCharge <
            Mathf.Max(
                0.0f,
                minimumCharge))
        {
            return false;
        }

        NitroLatched = true;
        return true;
    }

    public void StopOverdrive()
    {
        NitroLatched = false;
        NitroThrottle = 0.0f;
    }

    public void ResetRift()
    {
        GroundBurstTimer = 0.0f;
        SlipstreamDodgeWindowTimer = 0.0f;
        RiftTeleportFxTimer = 0.0f;
        RiftStompWindowTimer = 0.0f;
        ClearRiftRequest();
        GroundBurstHeading = Vector2.Zero;
        GroundBurstTargetSpeed = 0.0f;
    }

    public void ResetNitroTransient()
    {
        StopOverdrive();
        NitroSpool = 0.0f;
        NitroBurning = false;
        NitroIgnitionKickPending = false;
    }

    private static float Decay(
        float value,
        float deltaSeconds)
    {
        return Mathf.Max(
            0.0f,
            value - deltaSeconds);
    }
}
