using Godot;
using System.Collections.Generic;

/// <summary>
/// Scene-local run upgrade state and its deterministic derived modifiers.
/// Gameplay side effects deliberately remain on Player: this resource only
/// stores ranks and rebuilds values from the configured base capacities.
/// </summary>
[GlobalClass]
public partial class PlayerRunBuild : Resource
{
    private readonly Dictionary<RunUpgradeId, int> _ranks = new();
    private float _baseMaxHealth = 1.0f;
    private float _baseNitroCapacity;

    public float BaseSpeedBonus { get; private set; }
    public float AccelerationMultiplier { get; private set; } = 1.0f;
    public float NitroDrainMultiplier { get; private set; } = 1.0f;
    public float MomentumDecayMultiplier { get; private set; } = 1.0f;
    public float GroundMomentumDecayMultiplier { get; private set; } = 1.0f;
    public float ComboGroundGraceBonus { get; private set; }
    public float ComboDistanceBonus { get; private set; }
    public float ComboRangeAdjustmentBonus { get; private set; }
    public float DoubleJumpScaleBonus { get; private set; }
    public float BounceMultiplierBonus { get; private set; }
    public float GroundBurstCostReduction { get; private set; }
    public float GroundBurstCooldownReduction { get; private set; }
    public float DodgeWindowBonus { get; private set; }
    public float SlipstreamNitroBonus { get; private set; }
    public float StompMomentumMultiplier { get; private set; } = 1.0f;
    public float FinisherThresholdReduction { get; private set; }
    public float FinisherNitroBonus { get; private set; }
    public float MaxHealth { get; private set; } = 1.0f;
    public float NitroCapacity { get; private set; }

    public void ConfigureBaseStats(
        float maxHealth,
        float nitroCapacity)
    {
        _baseMaxHealth = Mathf.Max(1.0f, maxHealth);
        _baseNitroCapacity = Mathf.Max(0.0f, nitroCapacity);
        RebuildModifiers();
    }

    public int GetRank(RunUpgradeId id)
    {
        return _ranks.GetValueOrDefault(id);
    }

    public bool HasUpgrade(RunUpgradeId id)
    {
        return GetRank(id) > 0;
    }

    public bool SetRank(
        RunUpgradeId id,
        int rank)
    {
        int safeRank = Mathf.Clamp(rank, 0, RunProgression.GetDefinition(id).MaxRank);
        if (GetRank(id) == safeRank)
        {
            return false;
        }

        if (safeRank <= 0)
        {
            _ranks.Remove(id);
        }
        else
        {
            _ranks[id] = safeRank;
        }

        RebuildModifiers();
        return true;
    }

    private void RebuildModifiers()
    {
        int lightFrame = GetRank(RunUpgradeId.LightFrame);
        int deepTank = GetRank(RunUpgradeId.DeepTank);
        int softLanding = GetRank(RunUpgradeId.SoftLanding);
        int longStep = GetRank(RunUpgradeId.LongStep);
        int quickSpark = GetRank(RunUpgradeId.QuickSpark);
        int airfoil = GetRank(RunUpgradeId.Airfoil);
        int coolBurn = GetRank(RunUpgradeId.CoolBurn);
        int steadyPulse = GetRank(RunUpgradeId.SteadyPulse);
        int recoveryMesh = GetRank(RunUpgradeId.RecoveryMesh);
        int closeCall = GetRank(RunUpgradeId.CloseCall);
        int finisherCell = GetRank(RunUpgradeId.FinisherCell);

        BaseSpeedBonus = lightFrame;
        AccelerationMultiplier =
            1.0f + lightFrame * 0.06f;
        NitroDrainMultiplier =
            Mathf.Pow(0.9f, coolBurn);
        MomentumDecayMultiplier =
            Mathf.Pow(0.92f, steadyPulse);
        GroundMomentumDecayMultiplier =
            Mathf.Pow(0.75f, softLanding);
        ComboGroundGraceBonus =
            Mathf.Min(0.28f, softLanding * 0.1f);
        ComboDistanceBonus =
            longStep * 2.25f;
        ComboRangeAdjustmentBonus =
            longStep * 0.75f;
        DoubleJumpScaleBonus =
            airfoil * 0.08f;
        BounceMultiplierBonus =
            airfoil * 0.04f;
        GroundBurstCostReduction =
            quickSpark * 1.5f;
        GroundBurstCooldownReduction =
            quickSpark * 0.035f;
        DodgeWindowBonus =
            closeCall * 0.08f;
        SlipstreamNitroBonus =
            closeCall * 2.0f;
        StompMomentumMultiplier =
            1.0f + steadyPulse * 0.12f;
        FinisherThresholdReduction =
            finisherCell * 12.0f;
        FinisherNitroBonus =
            finisherCell * 6.0f;
        MaxHealth = Mathf.Max(
            1.0f,
            _baseMaxHealth +
                recoveryMesh * 15.0f);
        NitroCapacity = Mathf.Max(
            0.0f,
            _baseNitroCapacity +
                deepTank * 20.0f);
    }
}
