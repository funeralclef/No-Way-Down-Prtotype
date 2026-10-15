using Godot;

public readonly struct PlayerComboImpact
{
    public readonly bool Milestone;
    public readonly bool Finisher;

    public PlayerComboImpact(
        bool milestone,
        bool finisher)
    {
        Milestone = milestone;
        Finisher = finisher;
    }
}

public readonly struct PlayerComboEnd
{
    public readonly int ChainCount;
    public readonly int StylePoints;

    public PlayerComboEnd(
        int chainCount,
        int stylePoints)
    {
        ChainCount = chainCount;
        StylePoints = stylePoints;
    }
}

/// <summary>
/// Scene-free combo progression. It owns counters and one-shot state, while
/// Player remains responsible for signals, rewards and physical bounce.
/// </summary>
public sealed class PlayerComboState
{
    public int AirStompChain { get; set; }
    public float AirComboAppliedSpeedBonus { get; set; }
    public float GroundGraceTimer { get; set; }
    public int StylePoints { get; set; }
    public float FinisherCharge { get; set; }
    public bool RescueAvailable { get; set; }
    public bool RescueRoutePending { get; set; }
    public EnemyArchetypeId? LastArchetype { get; set; }

    public int AdvanceStomp(float groundGraceTime)
    {
        AirStompChain++;
        if (AirStompChain == 1)
        {
            RescueAvailable = true;
            StylePoints = 0;
            LastArchetype = null;
        }
        GroundGraceTimer = Mathf.Max(
            0.0f,
            groundGraceTime);
        return AirStompChain;
    }

    public bool ActivateRescue(
        bool fromGroundGrace,
        float finisherThreshold)
    {
        if (!RescueAvailable)
        {
            return false;
        }

        RescueAvailable = false;
        RescueRoutePending = true;
        StylePoints += fromGroundGrace
            ? 8
            : 6;
        FinisherCharge = Mathf.Min(
            GetChargeCap(finisherThreshold),
            FinisherCharge +
                (fromGroundGrace
                    ? 7.0f
                    : 5.0f));
        return true;
    }

    public PlayerComboImpact RegisterImpact(
        int styleGain,
        float chargeScale,
        float finisherThreshold)
    {
        StylePoints += Mathf.Max(
            0,
            styleGain);
        FinisherCharge = Mathf.Min(
            GetChargeCap(finisherThreshold),
            FinisherCharge +
                Mathf.Max(0, styleGain) *
                Mathf.Max(
                    0.1f,
                    chargeScale));

        bool milestone =
            AirStompChain == 3 ||
            AirStompChain == 6 ||
            AirStompChain == 10 ||
            (AirStompChain > 10 &&
                AirStompChain % 5 == 0);
        bool finisher =
            AirStompChain >= 8 &&
            FinisherCharge >=
                finisherThreshold;
        if (finisher)
        {
            FinisherCharge -=
                finisherThreshold;
        }
        return new PlayerComboImpact(
            milestone,
            finisher);
    }

    public PlayerComboEnd EndFlow()
    {
        PlayerComboEnd ended = new(
            AirStompChain,
            StylePoints);
        AirStompChain = 0;
        AirComboAppliedSpeedBonus = 0.0f;
        GroundGraceTimer = 0.0f;
        StylePoints = 0;
        RescueAvailable = false;
        RescueRoutePending = false;
        LastArchetype = null;
        return ended;
    }

    public void AddStyle(int amount)
    {
        StylePoints = Mathf.Max(
            0,
            StylePoints + amount);
    }

    public void ScaleFinisherCharge(float scale)
    {
        FinisherCharge = Mathf.Max(
            0.0f,
            FinisherCharge *
                Mathf.Max(0.0f, scale));
    }

    public Player.ComboFlowTier GetFlowTier()
    {
        if (AirStompChain >= 10 ||
            StylePoints >= 210)
        {
            return Player.ComboFlowTier.Legendary;
        }
        if (AirStompChain >= 6 ||
            StylePoints >= 105)
        {
            return Player.ComboFlowTier.Dominant;
        }
        if (AirStompChain >= 3 ||
            StylePoints >= 42)
        {
            return Player.ComboFlowTier.Rhythm;
        }
        return Player.ComboFlowTier.Warmup;
    }

    private static float GetChargeCap(
        float finisherThreshold)
    {
        return Mathf.Max(
            finisherThreshold * 1.35f,
            finisherThreshold + 1.0f);
    }
}
