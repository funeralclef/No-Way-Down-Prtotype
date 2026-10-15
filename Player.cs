using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    private sealed class NearMissAttempt
    {
        public float ClosestDistance = float.MaxValue;
        public float ClosestVerticalDistance =
            float.MaxValue;
        public float PeakRelativeSpeed;
        public bool SawCommittedAttack;
        public bool SawGroundBurst;
        public bool SawThreatAhead;
        public bool SawThreatBehind;
        public bool SawClosingApproach;
        public bool SawOpeningSeparation;
        public bool SawSideBySide;
        public Vector3 ClosestWorldPosition;
        public Vector3 PassDirection =
            Vector3.Forward;
    }

    private sealed class MobCollisionException
    {
        public Mob Mob;
        public float TimeRemaining;
    }

    private sealed class ComboRouteProjection
    {
        public Mob Origin;
        public Vector3 Position;
        public Vector3 Heading;
        public float ElapsedTime;
        public float BounceVelocity;
        public float ReferenceSpeed;
        public float RouteCost;
        public int ChainCount;
        public HashSet<ulong> Visited = new();
    }

    public enum AssistLevel
    {
        Off,
        Balanced,
        Strong
    }

    public enum ControlStyle
    {
        Agile
    }

    public enum MomentumTier
    {
        Low,
        Medium,
        Rush
    }

    public enum ComboFlowTier
    {
        Warmup,
        Rhythm,
        Dominant,
        Legendary
    }

    public enum SlipstreamCueLevel
    {
        None,
        Approach,
        Ready,
        Perfect
    }

    public enum ContractRecoveryMode
    {
        HuntOrEndure
    }

    [Export]
    public int Speed { get; set; } = 15;

    [Export]
    public int FallAcceleration { get; set; } = 75;

    [Export]
    public int JumpImpulse { get; set; } = 20;

    [Export]
    public int BounceImpulse { get; set; } = 16;

    [Export(PropertyHint.Range, "-2.0,4.0,0.1")]
    public float StompMaximumUpwardContactSpeed { get; set; } =
        0.8f;

    [ExportGroup("Movement feel")]
    [Export(PropertyHint.Range, "10.0,250.0,1.0")]
    public float GroundAcceleration { get; set; } = 116.0f;

    [Export(PropertyHint.Range, "10.0,300.0,1.0")]
    public float GroundTurnAcceleration { get; set; } = 220.0f;

    [Export(PropertyHint.Range, "10.0,250.0,1.0")]
    public float GroundDeceleration { get; set; } = 150.0f;

    [Export(PropertyHint.Range, "5.0,150.0,1.0")]
    public float AirAcceleration { get; set; } = 68.0f;

    [Export(PropertyHint.Range, "5.0,200.0,1.0")]
    public float AirTurnAcceleration { get; set; } = 118.0f;

    [Export(PropertyHint.Range, "0.0,100.0,1.0")]
    public float AirDeceleration { get; set; } = 28.0f;

    [Export(PropertyHint.Range, "0.0,12.0,0.25")]
    public float NitroSteeringResponsiveness { get; set; } = 6.5f;

    [Export(PropertyHint.Range, "1.0,4.0,0.05")]
    public float JumpReleaseGravityMultiplier { get; set; } = 2.45f;

    [Export(PropertyHint.Range, "0.3,1.0,0.05")]
    public float JumpApexGravityMultiplier { get; set; } = 0.58f;

    [Export(PropertyHint.Range, "0.5,6.0,0.25")]
    public float JumpApexVelocityRange { get; set; } = 3.0f;

    [ExportGroup("Double jump")]
    [Export(PropertyHint.Range, "0.5,1.0,0.01")]
    public float DoubleJumpImpulseScale { get; set; } = 0.78f;

    [Export(PropertyHint.Range, "0.0,0.8,0.05")]
    public float DoubleJumpCarryScale { get; set; } = 0.2f;

    [ExportGroup("Clutch vault")]
    [Export(PropertyHint.Range, "2.0,5.0,0.1")]
    public float ClutchVaultRadius { get; set; } = 4.4f;

    [Export(PropertyHint.Range, "0.05,0.35,0.01")]
    public float ClutchVaultInputGrace { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "1.5,3.2,0.05")]
    public float ClutchVaultArrivalHeight { get; set; } = 2.3f;

    [Export(PropertyHint.Range, "4.0,16.0,0.25")]
    public float ClutchVaultArrivalDiveSpeed { get; set; } = 9.0f;

    [Export(PropertyHint.Range, "0.1,0.5,0.01")]
    public float ClutchVaultContactGrace { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float OffScreenMarginRatio { get; set; } = 0.18f;

    [ExportGroup("Gamepad")]
    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float GamepadInnerDeadZone { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "0.6,1.0,0.01")]
    public float GamepadOuterDeadZone { get; set; } = 0.94f;

    [Export(PropertyHint.Range, "0.5,2.0,0.05")]
    public float GamepadResponseExponent { get; set; } = 1.05f;

    [ExportGroup("Fair enemy contact")]
    [Export(PropertyHint.Range, "0.015,0.2,0.005")]
    public float EnemyContactConfirmationTime { get; set; } =
        0.025f;

    [Export(PropertyHint.Range, "1.2,2.4,0.025")]
    public float EnemyContactRadius { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.1,1.5,0.05")]
    public float AirborneContactSafeHeight { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.05,0.6,0.025")]
    public float GrazeReactionCooldown { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.0,5.0,0.1")]
    public float GrazeSlipSpeed { get; set; } = 1.8f;

    [ExportGroup("Health and recovery")]
    [Export(PropertyHint.Range, "50.0,250.0,5.0")]
    public float MaxHealth { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "5.0,50.0,1.0")]
    public float EnemyContactDamage { get; set; } = 20.0f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int RecoveryContractKills { get; set; } = 2;

    [Export(PropertyHint.Range, "2,12,1")]
    public int CriticalRecoveryContractKills { get; set; } = 4;

    [Export(PropertyHint.Range, "5.0,60.0,1.0")]
    public float KillContractHealing { get; set; } = 26.0f;

    [Export(PropertyHint.Range, "5.0,50.0,1.0")]
    public float EndureContractHealing { get; set; } = 16.0f;

    [Export(PropertyHint.Range, "0.0,100.0,1.0")]
    public float ArenaClearHealing { get; set; } = 32.0f;

    [Export(PropertyHint.Range, "0.25,2.0,0.05")]
    public float DamageInvulnerabilityTime { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "2.0,20.0,0.5")]
    public float DamageKnockbackSpeed { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.0,12.0,0.5")]
    public float DamageKnockbackLift { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.0,25.0,1.0")]
    public float RecoveryRushBuffer { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "10.0,60.0,1.0")]
    public float FirstRecoveryWaitTime { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "15.0,90.0,1.0")]
    public float CriticalRecoveryWaitTime { get; set; } = 36.0f;

    [ExportGroup("Daring dodges")]
    [Export(PropertyHint.Range, "2.0,5.0,0.1")]
    public float DaringDodgeEntryRadius { get; set; } = 4.8f;

    [Export(PropertyHint.Range, "1.8,4.0,0.1")]
    public float DaringDodgeDangerRadius { get; set; } = 3.6f;

    [Export(PropertyHint.Range, "3.0,7.0,0.1")]
    public float DaringDodgeExitRadius { get; set; } = 5.8f;

    [Export(PropertyHint.Range, "2.0,16.0,0.5")]
    public float DaringDodgeMinRelativeSpeed { get; set; } = 6.5f;

    [Export(PropertyHint.Range, "0.5,5.0,0.25")]
    public float DaringDodgeContractReduction { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.25,4.0,0.25")]
    public float DaringDodgeSpeedBoost { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float DaringDodgeBoostDuration { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "1.0,3.0,0.1")]
    public float PerfectDodgeReductionMultiplier { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.0,3.0,0.25")]
    public float PerfectDodgeExtraSpeedBoost { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "1.4,4.5,0.05")]
    public float SlipstreamDodgeRadius { get; set; } = 3.35f;

    [Export(PropertyHint.Range, "1.0,2.6,0.05")]
    public float PerfectSlipstreamRadius { get; set; } = 2.05f;

    [Export(PropertyHint.Range, "0.5,2.5,0.05")]
    public float SlipstreamVerticalTolerance { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "6.0,24.0,0.5")]
    public float SlipstreamMinimumRelativeSpeed { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "1,20,1")]
    public int SlipstreamDodgeScore { get; set; } = 5;

    [Export(PropertyHint.Range, "0,12,1")]
    public int PerfectSlipstreamScoreBonus { get; set; } = 3;

    [Export(PropertyHint.Range, "0.0,16.0,0.5")]
    public float SlipstreamNitroReward { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.0,16.0,0.5")]
    public float SlipstreamMomentumReward { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.5,8.0,0.25")]
    public float SlipstreamSpeedImpulse { get; set; } = 3.25f;

    [Export(PropertyHint.Range, "0.25,5.0,0.25")]
    public float SlipstreamSpeedBoost { get; set; } = 2.25f;

    [Export(PropertyHint.Range, "0.2,1.2,0.05")]
    public float SlipstreamBoostDuration { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.25,0.8,0.05")]
    public float SlipstreamDodgeInputWindow { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0.35,1.4,0.05")]
    public float SlipstreamCuePredictionTime { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0.15,0.8,0.05")]
    public float SlipstreamCueReadyTime { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "6.0,20.0,0.5")]
    public float SlipstreamCueMaximumDistance { get; set; } = 15.0f;

    [Export(PropertyHint.Range, "0.0,1.5,0.05")]
    public float SlipstreamCueMissPadding { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0.0,20.0,1.0")]
    public float CounterStompMomentumBonus { get; set; } = 8.0f;

    [ExportGroup("Momentum")]
    [Export(PropertyHint.Range, "10.0,200.0,1.0")]
    public float MaxMomentum { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "1.0,99.0,1.0")]
    public float MediumMomentumThreshold { get; set; } = 35.0f;

    [Export(PropertyHint.Range, "1.0,99.0,1.0")]
    public float RushMomentumThreshold { get; set; } = 75.0f;

    [Export(PropertyHint.Range, "0.0,40.0,0.5")]
    public float ForwardMomentumGain { get; set; } = 16.0f;

    [Export(PropertyHint.Range, "0.0,40.0,0.5")]
    public float IdleMomentumDecay { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.0,50.0,0.5")]
    public float BackwardMomentumDecay { get; set; } = 26.0f;

    [Export(PropertyHint.Range, "0.0,20.0,0.5")]
    public float FallingMomentumDecay { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float GroundContactMomentumDecay { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.0,50.0,1.0")]
    public float StompMomentumGain { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.0,25.0,1.0")]
    public float AirStompChainBonus { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "0.0,20.0,0.5")]
    public float NitroMomentumGainPerSecond { get; set; } = 5.0f;

    [ExportGroup("Arena momentum")]
    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float ArenaIdleDecayMultiplier { get; set; } = 0.25f;

    [ExportGroup("Momentum bonuses")]
    [Export(PropertyHint.Range, "1.0,2.0,0.01")]
    public float MediumSpeedMultiplier { get; set; } = 1.18f;

    [Export(PropertyHint.Range, "1.0,2.5,0.01")]
    public float RushSpeedMultiplier { get; set; } = 1.42f;

    [Export(PropertyHint.Range, "1.0,2.0,0.01")]
    public float MediumJumpMultiplier { get; set; } = 1.08f;

    [Export(PropertyHint.Range, "1.0,2.0,0.01")]
    public float RushJumpMultiplier { get; set; } = 1.18f;

    [Export(PropertyHint.Range, "1.0,2.5,0.01")]
    public float MediumBounceMultiplier { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "1.0,3.0,0.01")]
    public float RushBounceMultiplier { get; set; } = 1.45f;

    [ExportGroup("Combo flow")]
    [Export(PropertyHint.Range, "0.15,0.75,0.01")]
    public float ComboGroundGraceTime { get; set; } = 0.36f;

    [Export(PropertyHint.Range, "2,10,1")]
    public int ComboRescueMinimumChain { get; set; } = 4;

    [Export(PropertyHint.Range, "25.0,200.0,5.0")]
    public float ComboFinisherThreshold { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0.1,2.0,0.05")]
    public float ComboFinisherChargeScale { get; set; } = 0.72f;

    [Export(PropertyHint.Range, "0.0,60.0,1.0")]
    public float ComboFinisherNitroReward { get; set; } = 28.0f;

    [Export(PropertyHint.Range, "0.1,1.5,0.05")]
    public float ComboFinisherInvulnerability { get; set; } = 0.55f;

    [ExportGroup("Survival assist")]
    [Export]
    public bool SurvivalAssistEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "2.0,10.0,0.1")]
    public float SurvivalAssistRadius { get; set; } = 7.2f;

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float SurvivalAssistLookAheadTime { get; set; } = 0.78f;

    [Export(PropertyHint.Range, "1.0,8.0,0.25")]
    public float SurvivalAssistMaxCorrectionSpeed { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "1.0,20.0,0.5")]
    public float SurvivalAssistResponsiveness { get; set; } = 10.5f;

    [ExportGroup("Stomp assist")]
    [Export]
    public bool StompAssistEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "1.0,6.0,0.1")]
    public float StompAssistRadius { get; set; } = 2.9f;

    [Export(PropertyHint.Range, "1.0,8.0,0.1")]
    public float StompAssistMaxHeight { get; set; } = 5.2f;

    [Export(PropertyHint.Range, "1.0,30.0,0.5")]
    public float StompAssistStrength { get; set; } = 9.5f;

    [Export(PropertyHint.Range, "1.0,30.0,0.5")]
    public float StompAssistMaxCorrectionSpeed { get; set; } = 4.2f;

    [ExportGroup("Green target jump commitment")]
    [Export(PropertyHint.Range, "0.05,0.25,0.01")]
    public float FirstStompGuideCandidateMemory { get; set; } =
        0.12f;

    [Export(PropertyHint.Range, "2.0,35.0,0.5")]
    public float FirstStompGuideMaxTurnAngle { get; set; } =
        22.0f;

    [Export(PropertyHint.Range, "0.0,5.0,0.05")]
    public float FirstStompGuideMaxSpeedAdjustment { get; set; } =
        1.8f;

    [Export(PropertyHint.Range, "2.0,45.0,0.5")]
    public float FirstStompGuideAcceleration { get; set; } =
        34.0f;

    [Export(PropertyHint.Range, "1.0,9.0,0.25")]
    public float FirstStompGuideDescentHandoffSpeed { get; set; } =
        7.2f;

    [Export(PropertyHint.Range, "0.0,0.25,0.01")]
    public float FirstStompGuideInputGraceTime { get; set; } =
        0.17f;

    [Export(PropertyHint.Range, "-1.0,1.0,0.05")]
    public float FirstStompGuideCancelDotThreshold { get; set; } =
        0.25f;

    [Export(PropertyHint.Range, "0.0,0.75,0.05")]
    public float FirstStompGuideLandingPocket { get; set; } =
        0.52f;

    [Export(PropertyHint.Range, "1.0,4.0,0.1")]
    public float FirstStompGuideMinimumDistance { get; set; } =
        2.0f;

    [ExportGroup("Stomp intent corridor")]
    [Export(PropertyHint.Range, "5.0,40.0,0.5")]
    public float FirstStompIntentAcquisitionAngle { get; set; } =
        28.0f;

    [Export(PropertyHint.Range, "5.0,24.0,0.5")]
    public float FirstStompIntentMaximumDistance { get; set; } =
        16.5f;

    [Export(PropertyHint.Range, "0.1,0.8,0.05")]
    public float FirstStompIntentMinimumSpeedRatio { get; set; } =
        0.25f;

    [Export(PropertyHint.Range, "0.1,0.8,0.05")]
    public float FirstStompGuideMinimumInputStrength { get; set; } =
        0.14f;

    [Export(PropertyHint.Range, "0.0,0.8,0.02")]
    public float FirstStompGuideCorrectionShare { get; set; } =
        0.68f;

    [ExportGroup("Combo route director")]
    [Export(PropertyHint.Range, "0.05,0.4,0.01")]
    public float ComboRouteReplanInterval { get; set; } = 0.11f;

    [Export(PropertyHint.Range, "10.0,75.0,1.0")]
    public float ComboRouteSearchAngle { get; set; } = 52.0f;

    [Export(PropertyHint.Range, "2.0,35.0,1.0")]
    public float ComboRouteMaxTurnAngle { get; set; } = 21.0f;

    [Export(PropertyHint.Range, "0.0,0.4,0.01")]
    public float ComboRouteInputGraceTime { get; set; } = 0.17f;

    [Export(PropertyHint.Range, "-1.0,1.0,0.05")]
    public float ComboRouteCancelDotThreshold { get; set; } = 0.48f;

    [Export(PropertyHint.Range, "8.0,35.0,0.5")]
    public float ComboRouteMaxDistance { get; set; } = 27.0f;

    [Export(PropertyHint.Range, "1.0,2.5,0.05")]
    public float ComboRouteMinimumAssistDistance { get; set; } =
        1.6f;

    [Export(PropertyHint.Range, "0.8,1.5,0.01")]
    public float ComboStompContactHeight { get; set; } =
        1.16f;

    [Export(PropertyHint.Range, "2.5,7.0,0.1")]
    public float ComboRouteCloseArcDistance { get; set; } =
        4.6f;

    [Export(PropertyHint.Range, "0.4,1.0,0.02")]
    public float ComboRouteCloseSpeedFloorScale { get; set; } =
        0.68f;

    [Export(PropertyHint.Range, "0.2,0.9,0.02")]
    public float ComboRouteMinSpeedScale { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1.0,2.0,0.02")]
    public float ComboRouteMaxSpeedScale { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "12.0,36.0,0.5")]
    public float ComboRouteAbsoluteMaxSpeed { get; set; } = 26.0f;

    [Export(PropertyHint.Range, "2.0,18.0,0.5")]
    public float ComboRouteResponsiveness { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "0.0,0.8,0.05")]
    public float ComboRouteAssistInfluence { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.1,0.8,0.05")]
    public float ComboRouteMinimumInputStrength { get; set; } =
        0.14f;

    [Export(PropertyHint.Range, "0.0,5.0,0.05")]
    public float ComboRouteSpeedAssistAllowance { get; set; } =
        1.45f;

    [Export(PropertyHint.Range, "0.0,7.0,0.05")]
    public float ComboRouteMaximumRangeAdjustment { get; set; } =
        3.6f;

    [Export(PropertyHint.Range, "0.6,1.0,0.01")]
    public float ComboRouteMinBounceScale { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "1.0,1.35,0.01")]
    public float ComboRouteMaxBounceScale { get; set; } = 1.3f;

    [ExportGroup("Air combo growth")]
    [Export(PropertyHint.Range, "3,10,1")]
    public int AirComboLinksToMaximum { get; set; } = 6;

    [Export(PropertyHint.Range, "0.0,0.3,0.01")]
    public float AirComboMaximumHorizontalBonus { get; set; } =
        0.18f;

    [Export(PropertyHint.Range, "0.0,6.0,0.1")]
    public float AirComboMaximumSpeedBonus { get; set; } =
        3.0f;

    [Export(PropertyHint.Range, "0.0,0.2,0.01")]
    public float AirComboMaximumBounceBonus { get; set; } =
        0.08f;

    [Export(PropertyHint.Range, "0.0,10.0,0.25")]
    public float AirComboMaximumDistanceBonus { get; set; } =
        5.0f;

    [Export(PropertyHint.Range, "20.0,36.0,0.5")]
    public float AirComboAbsoluteSpeedCap { get; set; } =
        29.0f;

    [ExportGroup("Nitro")]
    [Export(PropertyHint.Range, "20.0,200.0,1.0")]
    public float NitroCapacity { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0.0,200.0,1.0")]
    public float NitroInitialCharge { get; set; } = 42.0f;

    [Export(PropertyHint.Range, "5.0,80.0,1.0")]
    public float NitroDrainPerSecond { get; set; } = 32.0f;

    [Export(PropertyHint.Range, "0.0,30.0,0.5")]
    public float NitroStompGain { get; set; } = 11.0f;

    [Export(PropertyHint.Range, "0.0,8.0,0.25")]
    public float NitroComboLinkBonus { get; set; } = 1.75f;

    [Export(PropertyHint.Range, "0.0,30.0,0.5")]
    public float NitroDaringDodgeGain { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.0,80.0,1.0")]
    public float NitroDamageLoss { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "0.0,32.0,0.5")]
    public float NitroMaximumSpeedBonus { get; set; } = 26.0f;

    [Export(PropertyHint.Range, "10.0,180.0,1.0")]
    public float NitroAcceleration { get; set; } = 142.0f;

    [Export(PropertyHint.Range, "20.0,56.0,0.5")]
    public float NitroAbsoluteSpeedCap { get; set; } = 46.0f;

    [Export(PropertyHint.Range, "0.04,1.5,0.01")]
    public float NitroSpoolUpTime { get; set; } = 0.07f;

    [Export(PropertyHint.Range, "0.05,1.0,0.05")]
    public float NitroSpoolDownTime { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "0.0,20.0,0.5")]
    public float NitroIgnitionSpeedKick { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.0,20.0,0.5")]
    public float NitroMinimumIgnitionCharge { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0.05,0.25,0.01")]
    public float NitroAnalogDeadZone { get; set; } = 0.07f;

    [ExportGroup("Rift Step")]
    [Export(PropertyHint.Range, "2.0,20.0,1.0")]
    public float GroundBurstCost { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "4.0,30.0,1.0")]
    public float RiftTeleportCost { get; set; } = 14.0f;

    [Export(PropertyHint.Range, "0.2,1.2,0.01")]
    public float RiftStepCooldown { get; set; } = 0.48f;

    [Export(PropertyHint.Range, "0.12,0.5,0.01")]
    public float GroundBurstCooldown { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0.04,0.25,0.01")]
    public float RiftStepInputBufferTime { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0.12,0.4,0.01")]
    public float KeyboardReactorHoldTime { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0.08,0.32,0.01")]
    public float GroundBurstDuration { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "2.0,28.0,0.5")]
    public float GroundBurstSpeedGain { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "18.0,48.0,0.5")]
    public float GroundBurstSpeedCap { get; set; } = 40.0f;

    [Export(PropertyHint.Range, "60.0,420.0,5.0")]
    public float GroundBurstAcceleration { get; set; } = 320.0f;

    [Export(PropertyHint.Range, "0.1,0.45,0.01")]
    public float RiftStompWindow { get; set; } = 0.24f;

    [Export(PropertyHint.Range, "0.0,0.3,0.01")]
    public float MobileRiftInputGrace { get; set; } = 0.14f;

    [Export(PropertyHint.Range, "1.0,8.0,0.25")]
    public float RiftTargetMinimumDistance { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "8.0,36.0,0.5")]
    public float RiftTargetMaximumDistance { get; set; } = 22.0f;

    [Export(PropertyHint.Range, "12.0,44.0,0.5")]
    public float RiftArenaTargetMaximumDistance { get; set; } = 30.0f;

    [Export(PropertyHint.Range, "-0.2,0.8,0.05")]
    public float RiftTargetForwardDot { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "1.2,4.0,0.1")]
    public float RiftArrivalHeight { get; set; } = 2.4f;

    [Export(PropertyHint.Range, "3.0,14.0,0.1")]
    public float RiftArrivalDiveSpeed { get; set; } = 7.5f;

    [Export(PropertyHint.Range, "0.05,0.3,0.01")]
    public float RiftArrivalInvulnerability { get; set; } = 0.13f;

    [Export(PropertyHint.Range, "0.06,0.25,0.01")]
    public float MobBounceFloorIgnoreTime { get; set; } = 0.14f;

    [Export(PropertyHint.Range, "0.03,0.2,0.01")]
    public float AirborneStallGuardTime { get; set; } = 0.03f;

    [Signal]
    public delegate void HitEventHandler();

    [Signal]
    public delegate void MomentumChangedEventHandler(float momentum, int tier);

    [Signal]
    public delegate void RunBuffTriggeredEventHandler(int upgradeId);

    [Signal]
    public delegate void DashStartedEventHandler();

    [Signal]
    public delegate void RiftStepStartedEventHandler(
        bool teleported,
        Vector3 origin,
        Vector3 destination);

    [Signal]
    public delegate void ClutchVaultStartedEventHandler(
        Vector3 origin,
        Vector3 targetPosition);

    [Signal]
    public delegate void NitroChangedEventHandler(
        float charge,
        float capacity,
        bool active);

    [Signal]
    public delegate void DoubleJumpedEventHandler();

    [Signal]
    public delegate void TunnelEnteredEventHandler();

    [Signal]
    public delegate void CourseLaunchedEventHandler(
        float launchSpeed);

    [Signal]
    public delegate void TraversalRailEnteredEventHandler(
        float minimumSpeed);

    [Signal]
    public delegate void HealthChangedEventHandler(
        float health,
        float maxHealth);

    [Signal]
    public delegate void RecoveryContractChangedEventHandler(
        int recoveryKills,
        int recoveryTarget,
        int secondsRemaining);

    [Signal]
    public delegate void DamageTakenEventHandler(
        float health,
        float damage);

    [Signal]
    public delegate void HealthRecoveredEventHandler(
        float amount,
        bool earnedByKills);

    [Signal]
    public delegate void ArenaHealthRecoveredEventHandler(float amount);

    [Signal]
    public delegate void DaringDodgeEventHandler(
        float contractTimeReduced,
        float speedBoost,
        bool perfectDodge);

    [Signal]
    public delegate void SlipstreamDodgeEventHandler(
        int scoreReward,
        float nitroReward,
        float speedImpulse,
        bool perfectDodge,
        Vector3 worldPosition,
        Vector3 passDirection);

    [Signal]
    public delegate void CounterStompedEventHandler(
        int scoreMultiplier);

    [Signal]
    public delegate void StompChainAdvancedEventHandler(
        int chainCount,
        int scoreMultiplier);

    [Signal]
    public delegate void ComboRouteLockedEventHandler(
        int plannedTargets,
        bool arcAdjusted);

    [Signal]
    public delegate void ComboRouteForecastChangedEventHandler(
        int plannedTargets);

    [Signal]
    public delegate void ComboImpactEventHandler(
        int chainCount,
        int stylePoints,
        int flowTier,
        int archetype,
        bool counterStomp,
        bool milestone,
        bool finisher,
        Vector3 worldPosition);

    [Signal]
    public delegate void ComboRescuedEventHandler(
        int chainCount,
        bool fromGroundGrace);

    [Signal]
    public delegate void ComboEndedEventHandler(
        int chainCount,
        int stylePoints);

    public float Momentum { get; private set; }
    public MomentumTier CurrentMomentumTier { get; private set; } = MomentumTier.Low;
    public float MomentumRatio => Mathf.IsZeroApprox(MaxMomentum) ? 0.0f : Momentum / MaxMomentum;
    public int RewardMultiplier => CurrentMomentumTier switch
    {
        MomentumTier.Medium => 2,
        MomentumTier.Rush => 3,
        _ => 1
    };
    public bool IsRushing => CurrentMomentumTier == MomentumTier.Rush;
    public bool IsArenaCombatActive { get; private set; }
    public float RiftStepCooldownRemaining =>
        _nitroDashCooldownTimer;
    // Kept as a read-only compatibility alias for older HUD/test scenes.
    public float DashCooldownRemaining =>
        RiftStepCooldownRemaining;
    public float NitroCharge =>
        _abilityState.NitroCharge;
    public float NitroRatio =>
        NitroCapacity <= 0.0f
            ? 0.0f
            : NitroCharge / NitroCapacity;
    public float NitroSpool => _nitroSpool;
    public float NitroThrottle => _nitroThrottle;
    public bool IsNitroActive =>
        _nitroSpool > 0.01f;
    public bool IsNitroBurning => _nitroBurning;
    public bool IsNitroOverdrive => _nitroLatched;
    public bool IsGroundBurstActive =>
        _groundBurstTimer > 0.0f;
    public bool IsRiftTeleportActive =>
        _riftTeleportFxTimer > 0.0f;
    public bool IsRiftStompWindowActive =>
        _riftStompWindowTimer > 0.0f;
    public Mob SlipstreamCueTarget
    {
        get;
        private set;
    }
    public SlipstreamCueLevel CurrentSlipstreamCue
    {
        get;
        private set;
    }
    public float SlipstreamCueUrgency
    {
        get;
        private set;
    }
    public float SlipstreamCuePredictedDistance
    {
        get;
        private set;
    }
    public Vector3 SlipstreamCueDirection
    {
        get;
        private set;
    } = Vector3.Forward;
    public Mob ClutchVaultCueTarget
    {
        get;
        private set;
    }
    public float ClutchVaultCueUrgency
    {
        get;
        private set;
    }
    public bool IsClutchVaultActive =>
        _clutchVaultActiveTimer > 0.0f &&
        IsValidComboRouteTarget(
            _clutchVaultActiveTarget);
    // Vector Shift no longer participates in live gameplay. These aliases
    // deliberately remain false while older debug scenes migrate.
    public bool IsNitroDashActive => false;
    public bool IsNitroComboDash => false;
    public bool LastNitroBurstComboGuided => false;
    public bool CanDoubleJump => _airJumpsRemaining > 0;
    public float CurrentRunSpeed =>
        Mathf.Max(
            1.0f,
            (Speed + _runBaseSpeedBonus) *
                GetMovementMultiplier() -
                ActiveObstacleSlowAmount +
                ActiveSpeedBoost);
    public float ProjectedJumpLaunchSpeed =>
        JumpImpulse * GetJumpMultiplier();
    public float ProjectedBounceLaunchSpeed =>
        BounceImpulse * GetBounceMultiplier();
    public float CurrentAirComboReachBonus =>
        AirComboMaximumHorizontalBonus *
        GetAirComboReachProgress(_airStompChain);
    public float CurrentAirComboBounceMultiplier =>
        GetAirComboBounceMultiplier(_airStompChain);
    public float CurrentAirComboSpeedBonus =>
        _airComboAppliedSpeedBonus;
    public int AirStompChain => _airStompChain;
    public int ComboStylePoints => _comboStylePoints;
    public float ComboFinisherCharge => _comboFinisherCharge;
    public float ComboFinisherRatio =>
        GetEffectiveFinisherThreshold() <= 0.0f
            ? 0.0f
            : Mathf.Clamp(
                _comboFinisherCharge /
                    GetEffectiveFinisherThreshold(),
                0.0f,
                1.0f);
    public bool ComboRescueAvailable =>
        _comboRescueAvailable &&
        _airStompChain >= ComboRescueMinimumChain;
    public ComboFlowTier CurrentComboFlowTier =>
        _comboState.GetFlowTier();
    public float ActiveObstacleSlowAmount { get; private set; }
    public float ActiveSpeedBoost { get; private set; }
    public float ActiveTunnelSpeedBoost => ActiveSpeedBoost;
    public float SpeedBoostVisualIntensity { get; private set; }
    public float Health { get; private set; }
    public float HealthRatio =>
        MaxHealth <= 0.0f
            ? 0.0f
            : Health / MaxHealth;
    public int RecoveryKills { get; private set; }
    public int RecoveryTarget { get; private set; }
    public float RecoveryTimeRemaining { get; private set; }
    public bool IsDamageInvulnerable => _damageInvulnerabilityTimer > 0.0f;
    public bool IsInsideTunnel { get; private set; }
    public bool IsEnduranceRecoveryEnabled =>
        SelectedContractRecoveryMode == ContractRecoveryMode.HuntOrEndure;
    public AssistLevel SurvivalAssistLevel { get; private set; } = AssistLevel.Balanced;
    public AssistLevel StompAssistLevel { get; private set; } = AssistLevel.Balanced;
    public ControlStyle SelectedControlStyle { get; private set; } = ControlStyle.Agile;
    public ContractRecoveryMode SelectedContractRecoveryMode { get; private set; } =
        ContractRecoveryMode.HuntOrEndure;
    public Mob ComboRouteTarget =>
        IsValidComboRouteTarget(_comboRouteTarget)
            ? _comboRouteTarget
            : null;
    public int ComboRoutePlannedTargets { get; private set; }
    public float LastComboArcScale { get; private set; } =
        1.0f;
    public bool HasActiveFirstStompGuide =>
        IsValidComboRouteTarget(_guidedFirstStompTarget);
    public Mob FirstStompGuideTarget =>
        HasActiveFirstStompGuide
            ? _guidedFirstStompTarget
            : null;

    private const float CoyoteTime = 0.12f;
    private const float JumpBufferTime = 0.14f;

    private MobileControls _mobileControls;
    private Area3D _mobDetector;
    private CollisionShape3D _playerCollisionShape;
    private PlayerPresentation _presentation;
    private readonly PlayerAbilityState
        _abilityState = new();
    private readonly PlayerComboState
        _comboState = new();
    private readonly EnemyContactTracker
        _contactTracker = new();
    public int EffectsQuality { get; private set; } = 3;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _nitroSpool
    {
        get => _abilityState.NitroSpool;
        set => _abilityState.NitroSpool = value;
    }
    private float _nitroThrottle
    {
        get => _abilityState.NitroThrottle;
        set => _abilityState.NitroThrottle = value;
    }
    private bool _nitroIgnitionKickPending
    {
        get => _abilityState.NitroIgnitionKickPending;
        set => _abilityState.NitroIgnitionKickPending = value;
    }
    private bool _nitroBurning
    {
        get => _abilityState.NitroBurning;
        set => _abilityState.NitroBurning = value;
    }
    private bool _nitroWasBurning
    {
        get => _abilityState.NitroWasBurning;
        set => _abilityState.NitroWasBurning = value;
    }
    private bool _nitroInputWasPressed
    {
        get => _abilityState.NitroInputWasPressed;
        set => _abilityState.NitroInputWasPressed = value;
    }
    private bool _nitroLatched
    {
        get => _abilityState.NitroLatched;
        set => _abilityState.NitroLatched = value;
    }
    private bool _nitroActionUsesHold
    {
        get => _abilityState.NitroActionUsesHold;
        set => _abilityState.NitroActionUsesHold = value;
    }
    private bool _nitroActionStartedReactor
    {
        get => _abilityState.NitroActionStartedReactor;
        set => _abilityState.NitroActionStartedReactor = value;
    }
    private float _nitroActionHoldTimer
    {
        get => _abilityState.NitroActionHoldTimer;
        set => _abilityState.NitroActionHoldTimer = value;
    }
    private float _nitroDashCooldownTimer
    {
        get => _abilityState.NitroDashCooldownTimer;
        set => _abilityState.NitroDashCooldownTimer = value;
    }
    private float _nitroRumbleTimer
    {
        get => _abilityState.NitroRumbleTimer;
        set => _abilityState.NitroRumbleTimer = value;
    }
    private float _nitroDashSpeedProtectionTimer
    {
        get => _abilityState.NitroDashSpeedProtectionTimer;
        set => _abilityState.NitroDashSpeedProtectionTimer = value;
    }
    private float _groundBurstTimer
    {
        get => _abilityState.GroundBurstTimer;
        set => _abilityState.GroundBurstTimer = value;
    }
    private float _slipstreamDodgeWindowTimer
    {
        get => _abilityState.SlipstreamDodgeWindowTimer;
        set => _abilityState.SlipstreamDodgeWindowTimer = value;
    }
    private float _riftTeleportFxTimer
    {
        get => _abilityState.RiftTeleportFxTimer;
        set => _abilityState.RiftTeleportFxTimer = value;
    }
    private float _riftStompWindowTimer
    {
        get => _abilityState.RiftStompWindowTimer;
        set => _abilityState.RiftStompWindowTimer = value;
    }
    private float _riftRequestTimer
    {
        get => _abilityState.RiftRequestTimer;
        set => _abilityState.RiftRequestTimer = value;
    }
    private bool _riftRequestPending
    {
        get => _abilityState.RiftRequestPending;
        set => _abilityState.RiftRequestPending = value;
    }
    private Vector2 _riftRequestedHeading
    {
        get => _abilityState.RiftRequestedHeading;
        set => _abilityState.RiftRequestedHeading = value;
    }
    private Vector2 _groundBurstHeading
    {
        get => _abilityState.GroundBurstHeading;
        set => _abilityState.GroundBurstHeading = value;
    }
    private float _groundBurstTargetSpeed
    {
        get => _abilityState.GroundBurstTargetSpeed;
        set => _abilityState.GroundBurstTargetSpeed = value;
    }
    private Vector3 _nitroDirection
    {
        get => _abilityState.NitroDirection;
        set => _abilityState.NitroDirection = value;
    }
    private Mob _lastRiftStompMob;
    private Mob _lastRiftTeleportTarget;
    private readonly RandomNumberGenerator
        _riftRandom = new();
    private float NitroDashSpeedProtectionTime => 0.14f;
    private float NitroDashCooldown =>
        RiftStepCooldown;
    private float _mobBounceFloorIgnoreTimer;
    private float _airborneStallTimer;
    private float _airborneBlockedMotionTimer;
    private float _airborneEscapeDistance;
    private bool _mobBounceVelocityRecoveryAvailable;
    private readonly Dictionary<
        ulong,
        MobCollisionException>
        _airborneMobCollisionExceptions =
            new();
    private readonly List<ulong>
        _airborneMobCollisionExceptionKeys =
            new();
    private int _airJumpsRemaining = 1;
    private float _damageInvulnerabilityTimer;
    private float _obstacleSlowTimer;
    private float _speedBoostTimer;
    private float _traversalRailTimer;
    private Vector3 _traversalRailDirection =
        Vector3.Forward;
    private float _traversalRailMinimumSpeed;
    private float _traversalRailSteering;
    private int _lastEmittedRecoverySecond = -1;
    private float _nearMissProbeTimer;
    private float _nearMissDamageBlockTimer;
    private readonly Dictionary<ulong, NearMissAttempt> _nearMissAttempts =
        new();
    private readonly HashSet<ulong> _rewardedNearMisses = new();
    private int _airStompChain
    {
        get => _comboState.AirStompChain;
        set => _comboState.AirStompChain = value;
    }
    private float _airComboAppliedSpeedBonus
    {
        get => _comboState.AirComboAppliedSpeedBonus;
        set => _comboState.AirComboAppliedSpeedBonus = value;
    }
    private float _comboGroundGraceTimer
    {
        get => _comboState.GroundGraceTimer;
        set => _comboState.GroundGraceTimer = value;
    }
    private int _comboStylePoints
    {
        get => _comboState.StylePoints;
        set => _comboState.StylePoints = value;
    }
    private float _comboFinisherCharge
    {
        get => _comboState.FinisherCharge;
        set => _comboState.FinisherCharge = value;
    }
    private bool _comboRescueAvailable
    {
        get => _comboState.RescueAvailable;
        set => _comboState.RescueAvailable = value;
    }
    private bool _comboRescueRoutePending
    {
        get => _comboState.RescueRoutePending;
        set => _comboState.RescueRoutePending = value;
    }
    private EnemyArchetypeId? _lastComboArchetype
    {
        get => _comboState.LastArchetype;
        set => _comboState.LastArchetype = value;
    }
    private Vector3 _lastMoveDirection = Vector3.Forward;
    private Mob _stompAssistTarget;
    private Mob _firstStompGuideCandidate;
    private Mob _guidedFirstStompTarget;
    private Vector3 _guidedFirstStompHeading;
    private Vector3 _guidedFirstStompLandingOffset;
    private float _firstStompGuideCandidateTimer;
    private float _firstStompGuideGraceTimer;
    private float _firstStompGuideLaunchSpeed;
    private float _firstStompGuideLaunchVerticalSpeed;
    private float _clutchVaultCueGraceTimer;
    private float _clutchVaultActiveTimer;
    private float _clutchVaultContactGraceTimer;
    private Mob _clutchVaultActiveTarget;
    private Mob _comboRouteTarget;
    private float _comboRouteCruiseSpeed;
    private float _comboRouteGraceTimer;
    private float _comboRouteReplanTimer;
    private Vector2 _survivalAssistCorrection;
    private bool _stompAssistSuppressedUntilLanding;
    private bool _bouncedFromMobLastFrame;
    private bool _playerJumpActive;
    private bool _isDead;
    private uint _normalCollisionMask;
    private readonly Dictionary<ulong, Mob> _activeEnemyContacts =
        new();

    // Run build state is scene-local and never touches saved accessibility
    // presets. The settings menu can therefore rebuild Agile/assist values
    // without erasing an arena reward.
    private readonly PlayerRunBuild _runBuild = new();
    private float _runBaseSpeedBonus =>
        _runBuild.BaseSpeedBonus;
    private float _runAccelerationMultiplier =>
        _runBuild.AccelerationMultiplier;
    private float _runNitroDrainMultiplier =>
        _runBuild.NitroDrainMultiplier;
    private float _runMomentumDecayMultiplier =>
        _runBuild.MomentumDecayMultiplier;
    private float _runGroundMomentumDecayMultiplier =>
        _runBuild.GroundMomentumDecayMultiplier;
    private float _runComboGroundGraceBonus =>
        _runBuild.ComboGroundGraceBonus;
    private float _runComboDistanceBonus =>
        _runBuild.ComboDistanceBonus;
    private float _runComboRangeAdjustmentBonus =>
        _runBuild.ComboRangeAdjustmentBonus;
    private float _runDoubleJumpScaleBonus =>
        _runBuild.DoubleJumpScaleBonus;
    private float _runBounceMultiplierBonus =>
        _runBuild.BounceMultiplierBonus;
    private float _runGroundBurstCostReduction =>
        _runBuild.GroundBurstCostReduction;
    private float _runGroundBurstCooldownReduction =>
        _runBuild.GroundBurstCooldownReduction;
    private float _runDodgeWindowBonus =>
        _runBuild.DodgeWindowBonus;
    private float _runSlipstreamNitroBonus =>
        _runBuild.SlipstreamNitroBonus;
    private float _runStompMomentumMultiplier =>
        _runBuild.StompMomentumMultiplier;
    private float _runFinisherThresholdReduction =>
        _runBuild.FinisherThresholdReduction;
    private float _runFinisherNitroBonus =>
        _runBuild.FinisherNitroBonus;
    private float _runNitroBurnTime;
    private int _criticalDefeatCounter;
    private int _lastLightDefeatCounter;
    private int _afterburnerLinksRemaining;
    private int _kineticReserveCharges;
    private bool _redThreadPending;
    private bool _phaseRouteRefundPending;

    public override void _Ready()
    {
        GamepadInput.EnsureGameplayMappings();
        _riftRandom.Randomize();
        _mobileControls = GetTree().GetFirstNodeInGroup("mobile_controls") as MobileControls;
        _mobDetector = GetNode<Area3D>("MobDetector");
        _playerCollisionShape =
            GetNode<CollisionShape3D>(
                "CollisionShape3D");
        _normalCollisionMask = CollisionMask;
        _runBuild.ConfigureBaseStats(
            MaxHealth,
            NitroCapacity);
        // Enemies are stomp targets, not moving platforms. A shell survives
        // the first hit and must not donate its lateral platform velocity.
        PlatformFloorLayers &= ~(1u << 1);
        // Dense combo groups can create more contacts than the engine's
        // default slide budget. Processing the whole group prevents an
        // otherwise valid tangent from being discarded after a few hits.
        MaxSlides = Mathf.Max(MaxSlides, 12);
        _presentation =
            GetNode<PlayerPresentation>("Pivot");
        SetControlStyle(
            (int)ControlStyle.Agile);
        SetSurvivalAssistLevel(
            (int)SurvivalAssistLevel);
        SetStompAssistLevel(
            (int)StompAssistLevel);
        SetEffectsQuality(EffectsQuality);
        GetNodeOrNull<CustomizationProfile>("/root/CustomizationProfile")
            ?.ApplySelectedSkin(this);
        Health = Mathf.Max(1.0f, MaxHealth);
        _abilityState.InitializeNitro(
            NitroInitialCharge,
            NitroCapacity);
        EmitHealthChanged();
        EmitRecoveryContractChanged(true);
        EmitNitroChanged();
        UpdatePresentation(Vector3.Zero, 0.0f);
        InitializeRunSkills();
    }

    private void Die()
    {
        if (_isDead)
        {
            return;
        }

        EndComboFlow(false);
        ResetRiftStepState();
        _isDead = true;
        SetHealth(0.0f);
        RecoveryKills = 0;
        RecoveryTarget = 0;
        ClearRecoveryTimer();
        GetPresentation()?.SetDamageVisible(true);

        LoseMomentum(MaxMomentum);
        GamepadInput.Rumble(0.7f, 1.0f, 0.34f);
        EmitSignal(SignalName.Hit);
        QueueFree();
    }

    public void TakeDamage(Node3D source)
    {
        if (_isDead ||
            IsInsideTunnel ||
            _damageInvulnerabilityTimer > 0.0f)
        {
            return;
        }

        if (RunSkills?.TryAbsorbHit() == true) return;

        float previousHealth = Health;
        SetHealth(
            Health -
            Mathf.Max(1.0f, EnemyContactDamage));
        float damageTaken = previousHealth - Health;
        if (Health <= 0.0f)
        {
            Die();
            return;
        }

        DowngradeMomentumFromDamage();

        RecoveryKills = 0;
        RecoveryTarget = HealthRatio <= 0.4f
            ? Mathf.Max(1, CriticalRecoveryContractKills)
            : Mathf.Max(1, RecoveryContractKills);
        StartRecoveryTimer();
        _damageInvulnerabilityTimer = DamageInvulnerabilityTime;
        _nearMissDamageBlockTimer = 0.8f;
        _nearMissAttempts.Clear();
        EndComboFlow(false);
        ResetRiftStepState();
        _comboState.ScaleFinisherCharge(0.75f);
        AddNitro(-Mathf.Max(0.0f, NitroDamageLoss));
        _stompAssistTarget = null;
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        _survivalAssistCorrection = Vector2.Zero;
        _contactTracker.ClearDanger();
        ClearComboRoute();

        Vector3 knockbackDirection = IsInstanceValid(source)
            ? GlobalPosition - source.GlobalPosition
            : -_lastMoveDirection;
        knockbackDirection.Y = 0.0f;
        if (knockbackDirection.IsZeroApprox())
        {
            knockbackDirection = -_lastMoveDirection;
        }

        knockbackDirection = knockbackDirection.Normalized();
        Velocity = new Vector3(
            knockbackDirection.X * DamageKnockbackSpeed,
            Mathf.Max(Velocity.Y, DamageKnockbackLift),
            knockbackDirection.Z * DamageKnockbackSpeed);
        GamepadInput.Rumble(0.52f, 0.82f, 0.2f);
        EmitRecoveryContractChanged(true);
        EmitSignal(
            SignalName.DamageTaken,
            Health,
            damageTaken);
    }

    public void RescueFromWater(
        Vector3 safeGlobalPosition,
        float damage)
    {
        if (_isDead)
        {
            return;
        }

        float previousHealth = Health;
        SetHealth(
            Health -
            Mathf.Max(
                1.0f,
                damage));
        float damageTaken =
            previousHealth -
            Health;
        if (Health <= 0.0f)
        {
            Die();
            return;
        }

        if (IsInsideTunnel)
        {
            ExitTunnelPhase();
        }
        _abilityState.ResetNitroTransient();
        ResetRiftStepState();
        _traversalRailTimer = 0.0f;
        _traversalRailMinimumSpeed = 0.0f;
        _traversalRailSteering = 0.0f;

        DowngradeMomentumFromDamage();
        RecoveryKills = 0;
        RecoveryTarget =
            HealthRatio <= 0.4f
                ? Mathf.Max(
                    1,
                    CriticalRecoveryContractKills)
                : Mathf.Max(
                    1,
                    RecoveryContractKills);
        StartRecoveryTimer();
        EndComboFlow(false);
        _comboState.ScaleFinisherCharge(0.75f);
        AddNitro(
            -Mathf.Max(
                0.0f,
                NitroDamageLoss * 0.65f));
        _stompAssistTarget = null;
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        ClearComboRoute();
        _survivalAssistCorrection =
            Vector2.Zero;
        _contactTracker.ClearDanger();
        _nearMissAttempts.Clear();
        _nearMissDamageBlockTimer = 0.9f;

        GlobalPosition =
            safeGlobalPosition;
        Vector3 rescueDirection =
            _lastMoveDirection;
        rescueDirection.Y = 0.0f;
        if (rescueDirection.IsZeroApprox() ||
            rescueDirection.Z > -0.2f)
        {
            rescueDirection =
                Vector3.Forward;
        }
        rescueDirection =
            rescueDirection.Normalized();
        float rescueSpeed =
            Mathf.Max(
                8.0f,
                CurrentRunSpeed * 0.58f);
        Velocity =
            new Vector3(
                rescueDirection.X *
                    rescueSpeed,
                1.5f,
                rescueDirection.Z *
                    rescueSpeed);
        _playerJumpActive = false;
        _airJumpsRemaining =
            Mathf.Max(
                _airJumpsRemaining,
                1);
        _damageInvulnerabilityTimer =
            Mathf.Max(
                1.15f,
                DamageInvulnerabilityTime);

        GamepadInput.Rumble(
            0.58f,
            0.76f,
            0.24f);
        EmitRecoveryContractChanged(true);
        EmitSignal(
            SignalName.DamageTaken,
            Health,
            damageTaken);
    }

    public bool ReceiveAbyssPressure(
        float damage,
        float nitroLoss)
    {
        if (_isDead ||
            _damageInvulnerabilityTimer > 0.0f)
        {
            return false;
        }

        float previousHealth = Health;
        SetHealth(
            Health -
            Mathf.Max(
                1.0f,
                damage));
        float damageTaken =
            previousHealth -
            Health;
        if (Health <= 0.0f)
        {
            Die();
            return true;
        }

        if (IsInsideTunnel)
        {
            ExitTunnelPhase();
        }
        _abilityState.ResetNitroTransient();
        ResetRiftStepState();
        _traversalRailTimer = 0.0f;
        _traversalRailMinimumSpeed = 0.0f;
        _traversalRailSteering = 0.0f;

        DowngradeMomentumFromDamage();
        RecoveryKills = 0;
        RecoveryTarget =
            HealthRatio <= 0.4f
                ? Mathf.Max(
                    1,
                    CriticalRecoveryContractKills)
                : Mathf.Max(
                    1,
                    RecoveryContractKills);
        StartRecoveryTimer();
        EndComboFlow(false);
        _comboState.ScaleFinisherCharge(0.75f);
        AddNitro(
            -Mathf.Max(
                0.0f,
                nitroLoss));
        _stompAssistTarget = null;
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        ClearComboRoute();
        _survivalAssistCorrection =
            Vector2.Zero;
        _contactTracker.ClearDanger();
        _nearMissAttempts.Clear();
        _nearMissDamageBlockTimer = 0.95f;

        // Always throw the player back into the readable direction of play.
        // A small amount of existing lateral intent is preserved, so the
        // rescue feels like their line rather than an authored teleport.
        float currentLateral =
            Mathf.Clamp(
                Velocity.X /
                    Mathf.Max(
                        8.0f,
                        CurrentRunSpeed),
                -0.34f,
                0.34f);
        Vector3 rescueDirection =
            new Vector3(
                currentLateral,
                0.0f,
                -1.0f)
            .Normalized();
        float rescueSpeed =
            Mathf.Max(
                13.5f,
                CurrentRunSpeed * 0.92f);
        Velocity =
            new Vector3(
                rescueDirection.X *
                    rescueSpeed,
                4.8f,
                rescueDirection.Z *
                    rescueSpeed);
        _lastMoveDirection =
            rescueDirection;
        _playerJumpActive = false;
        _airJumpsRemaining =
            Mathf.Max(
                _airJumpsRemaining,
                1);
        _damageInvulnerabilityTimer =
            Mathf.Max(
                1.25f,
                DamageInvulnerabilityTime);

        GamepadInput.Rumble(
            0.72f,
            0.92f,
            0.28f);
        EmitRecoveryContractChanged(true);
        EmitSignal(
            SignalName.DamageTaken,
            Health,
            damageTaken);
        return true;
    }

    public void RegisterRecoveryKill()
    {
        if (_isDead || RecoveryTarget <= 0)
        {
            return;
        }

        RecoveryKills = Mathf.Min(RecoveryKills + 1, RecoveryTarget);
        if (RecoveryKills < RecoveryTarget)
        {
            EmitRecoveryContractChanged(true);
            return;
        }

        CompleteRecoveryContract(true);
    }

    public void OnArenaCleared()
    {
        if (_isDead ||
            Health >= Mathf.Max(1.0f, MaxHealth))
        {
            return;
        }

        float previousHealth = Health;
        SetHealth(
            Health +
            Mathf.Max(0.0f, ArenaClearHealing));
        float restoredHealth = Health - previousHealth;
        RecoveryKills = 0;
        RecoveryTarget = 0;
        ClearRecoveryTimer();
        EmitSignal(
            SignalName.ArenaHealthRecovered,
            restoredHealth);
    }

    private void DowngradeMomentumFromDamage()
    {
        if (HealthRatio <= 0.4f)
        {
            SetMomentum(0.0f);
            return;
        }

        if (CurrentMomentumTier == MomentumTier.Rush)
        {
            SetMomentum(Mathf.Min(
                RushMomentumThreshold - 0.5f,
                MediumMomentumThreshold + 8.0f));
        }
    }

    private void SetHealth(float value)
    {
        float clampedHealth = Mathf.Clamp(
            value,
            0.0f,
            Mathf.Max(1.0f, MaxHealth));
        if (Mathf.IsEqualApprox(clampedHealth, Health))
        {
            return;
        }

        Health = clampedHealth;
        EmitHealthChanged();
    }

    private void EmitHealthChanged()
    {
        EmitSignal(
            SignalName.HealthChanged,
            Health,
            Mathf.Max(1.0f, MaxHealth));
    }

    private void CompleteRecoveryContract(bool earnedByKills)
    {
        float previousHealth = Health;
        float healing = earnedByKills
            ? KillContractHealing
            : EndureContractHealing;
        SetHealth(Health + Mathf.Max(0.0f, healing));
        float restoredHealth = Health - previousHealth;
        RecoveryKills = 0;
        RecoveryTarget = 0;
        ClearRecoveryTimer();

        float restoredMomentum = earnedByKills
            ? RushMomentumThreshold + RecoveryRushBuffer
            : MediumMomentumThreshold + 10.0f;
        if (!earnedByKills)
        {
            restoredMomentum = Mathf.Min(
                restoredMomentum,
                RushMomentumThreshold - 0.5f);
        }

        SetMomentum(Mathf.Min(MaxMomentum, restoredMomentum));
        EmitSignal(
            SignalName.HealthRecovered,
            restoredHealth,
            earnedByKills);
    }

    private void StartRecoveryTimer()
    {
        RecoveryTimeRemaining = IsEnduranceRecoveryEnabled
            ? (HealthRatio <= 0.4f
                ? Mathf.Max(1.0f, CriticalRecoveryWaitTime)
                : Mathf.Max(1.0f, FirstRecoveryWaitTime))
            : 0.0f;
        EmitRecoveryContractChanged(true);
    }

    private void ClearRecoveryTimer()
    {
        RecoveryTimeRemaining = 0.0f;
        EmitRecoveryContractChanged(true);
    }

    private void UpdateRecoveryContract(float deltaSeconds)
    {
        if (_isDead ||
            RecoveryTarget <= 0 ||
            !IsEnduranceRecoveryEnabled ||
            RecoveryTimeRemaining <= 0.0f)
        {
            return;
        }

        RecoveryTimeRemaining = Mathf.Max(
            0.0f,
            RecoveryTimeRemaining - deltaSeconds);
        EmitRecoveryContractChanged();
        if (RecoveryTimeRemaining <= 0.0f)
        {
            CompleteRecoveryContract(false);
        }
    }

    private void EmitRecoveryContractChanged(bool force = false)
    {
        int secondsRemaining = Mathf.CeilToInt(RecoveryTimeRemaining);
        if (!force && secondsRemaining == _lastEmittedRecoverySecond)
        {
            return;
        }

        _lastEmittedRecoverySecond = secondsRemaining;
        EmitSignal(
            SignalName.RecoveryContractChanged,
            RecoveryKills,
            RecoveryTarget,
            secondsRemaining);
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaSeconds = (float)delta;
        UpdateAirborneMobCollisionExceptions(
            deltaSeconds);
        UpdateDamageInvulnerability(deltaSeconds);
        UpdateObstacleSlow(deltaSeconds);
        UpdateSpeedBoost(deltaSeconds);
        UpdateTraversalRail(deltaSeconds);
        UpdateRecoveryContract(deltaSeconds);
        UpdateDaringDodges(deltaSeconds);
        UpdateFirstStompGuideCandidateTimer(deltaSeconds);
        Vector2 movementInput = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        movementInput = GamepadInput.ShapeLeftStick(
            movementInput,
            GamepadInnerDeadZone,
            GamepadOuterDeadZone,
            GamepadResponseExponent);

        if (_mobileControls == null)
        {
            _mobileControls = GetTree().GetFirstNodeInGroup("mobile_controls") as MobileControls;
        }

        if (_mobileControls != null && _mobileControls.MovementVector.LengthSquared() > movementInput.LengthSquared())
        {
            movementInput = _mobileControls.MovementVector;
        }

        float inputStrength = Mathf.Min(movementInput.Length(), 1.0f);
        Vector3 direction = new(movementInput.X, 0.0f, movementInput.Y);
        _mobBounceFloorIgnoreTimer =
            Mathf.Max(
                0.0f,
                _mobBounceFloorIgnoreTimer -
                    deltaSeconds);
        bool continuingMobBounce =
            _bouncedFromMobLastFrame ||
            _mobBounceFloorIgnoreTimer >
                0.0f;
        bool wasOnFloor =
            IsOnWorldFloor() &&
            !continuingMobBounce;

        if (!direction.IsZeroApprox())
        {
            direction = direction.Normalized();
            _lastMoveDirection = direction;
        }
        Vector3 movementIntent = direction * inputStrength;

        UpdateMomentumFromMovement(movementIntent, wasOnFloor, deltaSeconds);
        UpdateSlipstreamDodgeCue(
            wasOnFloor);
        UpdateRiftStepAndNitro(
            direction,
            inputStrength,
            deltaSeconds);

        float movementMultiplier = GetMovementMultiplier();
        Vector3 velocity = Velocity;
        ApplyHorizontalMovement(
            ref velocity,
            ref direction,
            inputStrength,
            wasOnFloor,
            movementMultiplier,
            deltaSeconds);
        ApplyTraversalRailMotion(
            ref velocity,
            ref direction,
            inputStrength,
            deltaSeconds);

        _presentation?.SetAnimationSpeed(
            direction,
            movementInput.Length(),
            movementMultiplier);
        ApplySurvivalAssist(ref velocity, wasOnFloor, deltaSeconds);

        bool jumpPressed = Input.IsActionJustPressed("jump") ||
            (_mobileControls?.ConsumeJumpPressed() ?? false);
        bool jumpHeld = Input.IsActionPressed("jump") ||
            (_mobileControls?.IsJumpHeld ?? false);
        UpdateClutchVaultState(
            wasOnFloor,
            movementIntent,
            deltaSeconds);

        if (wasOnFloor)
        {
            _coyoteTimer = CoyoteTime;
            _airJumpsRemaining = 1;
            UpdateComboGroundGrace(
                continuingMobBounce,
                jumpPressed,
                deltaSeconds);

            if (!continuingMobBounce)
            {
                _playerJumpActive = false;
            }

            if (velocity.Y < 0.0f)
            {
                velocity.Y = 0.0f;
            }
        }
        else
        {
            if (_airStompChain > 0)
            {
                _comboGroundGraceTimer =
                    Mathf.Max(
                        _comboGroundGraceTimer,
                        GetEffectiveComboGroundGraceTime());
            }
            _coyoteTimer = Mathf.Max(0.0f, _coyoteTimer - deltaSeconds);
            float gravityMultiplier =
                PlayerMovementMotor
                    .GetGravityMultiplier(
                        _playerJumpActive,
                        jumpHeld,
                        velocity.Y,
                        JumpReleaseGravityMultiplier,
                        JumpApexVelocityRange,
                        JumpApexGravityMultiplier);

            velocity.Y -= FallAcceleration * gravityMultiplier * deltaSeconds;
        }
        _bouncedFromMobLastFrame = false;

        if (wasOnFloor && !continuingMobBounce)
        {
            _stompAssistSuppressedUntilLanding = false;
            ClearFirstStompGuide();
        }

        _jumpBufferTimer = Mathf.Max(
            0.0f,
            _jumpBufferTimer -
                deltaSeconds);
        bool clutchVaultStarted =
            jumpPressed &&
            TryStartClutchVault(
                wasOnFloor,
                ref velocity);
        if (jumpPressed &&
            !clutchVaultStarted)
        {
            _jumpBufferTimer =
                JumpBufferTime;
        }
        if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f)
        {
            velocity.Y = JumpImpulse * GetJumpMultiplier();
            _jumpBufferTimer = 0.0f;
            _coyoteTimer = 0.0f;
            _playerJumpActive = true;
            BeginFirstStompGuide(
                new Vector3(
                    velocity.X,
                    0.0f,
                    velocity.Z),
                velocity.Y,
                movementIntent);
        }
        else if (_jumpBufferTimer > 0.0f &&
            !wasOnFloor &&
            _airJumpsRemaining > 0)
        {
            float carriedLift = Mathf.Max(0.0f, velocity.Y) *
                Mathf.Clamp(DoubleJumpCarryScale, 0.0f, 0.8f);
            velocity.Y =
                JumpImpulse *
                    GetJumpMultiplier() *
                    Mathf.Clamp(
                        DoubleJumpImpulseScale +
                            _runDoubleJumpScaleBonus,
                        0.5f,
                        1.15f) +
                carriedLift;
            _jumpBufferTimer = 0.0f;
            _airJumpsRemaining--;
            _playerJumpActive = true;
            if (ComboRescueAvailable)
            {
                ActivateComboRescue(false);
            }
            ClearFirstStompGuide();
            GamepadInput.Rumble(0.08f, 0.23f, 0.07f);
            EmitSignal(SignalName.DoubleJumped);
        }

        if (_comboRescueRoutePending)
        {
            _comboRescueRoutePending = false;
            TryAcquireComboRescueRoute(
                movementIntent,
                velocity);
        }

        ApplyFirstStompGuide(
            ref velocity,
            movementIntent,
            deltaSeconds);
        bool followingFirstStompGuide =
            HasActiveFirstStompGuide;
        bool followingComboRoute = ApplyComboRouteDirector(
            ref velocity,
            movementIntent,
            wasOnFloor && !continuingMobBounce,
            deltaSeconds);
        if (followingComboRoute)
        {
            _stompAssistTarget = null;
        }
        else if (!followingFirstStompGuide)
        {
            ApplyStompAssist(ref velocity, wasOnFloor, deltaSeconds);
        }
        Vector3 intendedMoveVelocity = velocity;
        Velocity = intendedMoveVelocity;
        Vector3 frameStartPosition =
            GlobalPosition;
        MoveAndSlide();

        bool stompResolvedThisFrame = false;
        bool touchedMobThisFrame = false;
        bool hasMobFloorContact = false;
        bool hasMobCeilingContact = false;
        Vector3 strongestMobBlockingNormal =
            Vector3.Zero;
        float strongestMobBlockingScore =
            float.NegativeInfinity;
        Vector3 intendedHorizontalDirection =
            new(
                intendedMoveVelocity.X,
                0.0f,
                intendedMoveVelocity.Z);
        if (!intendedHorizontalDirection
            .IsZeroApprox())
        {
            intendedHorizontalDirection =
                intendedHorizontalDirection
                    .Normalized();
        }
        for (int index = 0; index < GetSlideCollisionCount(); index++)
        {
            KinematicCollision3D collision = GetSlideCollision(index);

            if  (collision.GetCollider() is Mob mob)
            {
                touchedMobThisFrame = true;
                Vector3 collisionNormal =
                    collision.GetNormal();
                hasMobFloorContact |=
                    collisionNormal.Y >= 0.55f;
                hasMobCeilingContact |=
                    collisionNormal.Y <= -0.45f;
                Vector3 horizontalNormal =
                    new(
                        collisionNormal.X,
                        0.0f,
                        collisionNormal.Z);
                float blockingScore =
                    horizontalNormal
                        .IsZeroApprox()
                        ? -1.0f
                        : intendedHorizontalDirection
                            .IsZeroApprox()
                            ? 0.0f
                            : -intendedHorizontalDirection
                                .Dot(
                                    horizontalNormal
                                        .Normalized());
                if (strongestMobBlockingNormal
                        .IsZeroApprox() ||
                    blockingScore >
                        strongestMobBlockingScore)
                {
                    strongestMobBlockingNormal =
                        collisionNormal;
                    strongestMobBlockingScore =
                        blockingScore;
                }
                if (mob.CanBeStomped &&
                    intendedMoveVelocity.Y <=
                        StompMaximumUpwardContactSpeed &&
                    Vector3.Up.Dot(collisionNormal) > 0.1f)
                {
                    stompResolvedThisFrame = true;
                    // Preserve the player's earned horizontal momentum. The
                    // collision result may include velocity from a moving
                    // body, which must never become a launch impulse.
                    Velocity = new Vector3(
                        intendedMoveVelocity.X,
                        Velocity.Y,
                        intendedMoveVelocity.Z);
                    bool counterStomp = mob.IsAttackCommitted;
                    bool defeatsMob =
                        mob.WillBeDefeatedByNextStomp;
                    float momentumReward =
                        (StompMomentumGain +
                            AirStompChainBonus * _airStompChain +
                            (counterStomp
                                ? CounterStompMomentumBonus
                                : 0.0f)) *
                        _runStompMomentumMultiplier;
                    AddMomentum(momentumReward);
                    _comboState.AdvanceStomp(
                        GetEffectiveComboGroundGraceTime());
                    _airJumpsRemaining = 1;
                    AddNitro(
                        NitroStompGain +
                        Mathf.Min(4, _airStompChain - 1) *
                            NitroComboLinkBonus);
                    // A stomp rewards rhythm without making Rift Step
                    // infinitely spammable: half of the remaining recovery
                    // is refunded instead of resetting it outright.
                    _nitroDashCooldownTimer =
                        Mathf.Max(
                            0.0f,
                            _nitroDashCooldownTimer *
                                0.5f);
                    ApplyAirComboPaceReward();
                    float bounceVelocity =
                        GetAirComboBounceVelocityForChain(
                            _airStompChain);
                    bounceVelocity = BeginComboRoute(
                        mob,
                        bounceVelocity,
                        movementIntent);
                    ApplyRunStompEffects();
                    ClearFirstStompGuide();
                    GamepadInput.Rumble(
                        0.16f,
                        Mathf.Clamp(
                            0.34f + _airStompChain * 0.08f,
                            0.34f,
                            0.68f),
                        0.1f);
                    int scoreMultiplier =
                        (RewardMultiplier +
                            Mathf.Clamp(
                                _airStompChain - 1,
                                0,
                                3) +
                            (CurrentComboFlowTier >=
                                    ComboFlowTier.Dominant
                                ? 1
                                : 0)) *
                        mob.StompScoreMultiplier;
                    RegisterComboImpact(
                        mob,
                        counterStomp,
                        ref scoreMultiplier);
                    if (defeatsMob)
                    {
                        RewardEnemyDefeat(
                            mob.BonusNitroReward,
                            mob.BonusHealingReward);
                    }
                    mob.Squash(scoreMultiplier, this);
                    float mobileInputGrace =
                        IsInstanceValid(
                            _mobileControls) &&
                        _mobileControls
                            .IsVisibleInTree()
                            ? Mathf.Max(
                                0.0f,
                                MobileRiftInputGrace)
                            : 0.0f;
                    _riftStompWindowTimer =
                        Mathf.Max(
                            0.08f,
                            RiftStompWindow +
                                mobileInputGrace);
                    _lastRiftStompMob = mob;
                    // The stomp is the deliberate hand-off into a possible
                    // chain teleport. A tiny debounce remains, but an input
                    // pressed on contact stays inside the request buffer.
                    _nitroDashCooldownTimer =
                        Mathf.Min(
                            _nitroDashCooldownTimer,
                            Mathf.Min(
                                0.06f,
                                RiftStepInputBufferTime));
                    if (counterStomp)
                    {
                        EmitSignal(
                            SignalName.CounterStomped,
                            scoreMultiplier);
                    }
                    if (_airStompChain >= 2)
                    {
                        EmitSignal(
                            SignalName.StompChainAdvanced,
                            _airStompChain,
                            scoreMultiplier);
                    }
                    _stompAssistTarget = null;
                    bounceVelocity =
                        SanitizeStompBounceVelocity(
                            bounceVelocity);
                    Velocity = new Vector3(
                        Velocity.X,
                        bounceVelocity,
                        Velocity.Z);
                    _mobBounceFloorIgnoreTimer =
                        Mathf.Max(
                            0.06f,
                            MobBounceFloorIgnoreTime);
                    _mobBounceVelocityRecoveryAvailable =
                        true;
                    _airborneEscapeDistance = 0.0f;
                    _bouncedFromMobLastFrame = true;
                    _playerJumpActive = false;
                    break;
                }
            }
            else if (collision.GetCollider() is RushBreakable breakable)
            {
                bool poweredImpact =
                    IsRushing ||
                    IsGroundBurstActive ||
                    NitroSpool >= 0.48f;
                if (breakable.Break(poweredImpact))
                {
                    if (poweredImpact)
                    {
                        AddMomentum(4.0f);
                        AddNitro(2.0f);
                        ApplyTraversalSpeedBoost(
                            2.8f,
                            0.82f);
                    }
                    else
                    {
                        ApplyObstacleSlow(
                            breakable.SlowAmount,
                            breakable.SlowDuration);
                    }

                    break;
                }
            }
        }

        if (!wasOnFloor &&
            !stompResolvedThisFrame &&
            !hasMobFloorContact &&
            IsOnWorldFloor() &&
            intendedMoveVelocity.Y <= -8.0f)
        {
            TriggerRunLandingPulse(
                -intendedMoveVelocity.Y);
        }

        ApplyAirborneMotionInvariant(
            deltaSeconds,
            intendedMoveVelocity,
            GlobalPosition -
                frameStartPosition,
            touchedMobThisFrame,
            stompResolvedThisFrame,
            hasMobFloorContact,
            hasMobCeilingContact,
            strongestMobBlockingNormal);

        UpdateEnemyContactSafety(deltaSeconds);
        if (_isDead)
        {
            return;
        }

        if (IsFarOutsideViewport())
        {
            Die();
            return;
        }

        UpdatePresentation(direction, deltaSeconds);
    }

    private void TriggerRunLandingPulse(
        float impactSpeed)
    {
        if (RunSkills?.ResolveDiveImpact() == true) return;
        if (!HasRunUpgrade(RunUpgradeId.ShockSole))
        {
            return;
        }

        bool evolved = HasRunUpgrade(
            RunUpgradeId.EarthSplitter);
        EmitSignal(SignalName.RunBuffTriggered, (int)(evolved ? RunUpgradeId.EarthSplitter : RunUpgradeId.ShockSole));
        float radius = (evolved ? 11.0f : 8.0f) + GetRunUpgradeRank(RunUpgradeId.ShockSole) - 1;
        float strength = Mathf.Clamp(
            impactSpeed * (evolved ? 0.72f : 0.52f),
            4.0f,
            evolved ? 14.0f : 9.0f);
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion())
            {
                continue;
            }

            Vector3 offset =
                mob.GlobalPosition -
                GlobalPosition;
            offset.Y = 0.0f;
            if (offset.LengthSquared() >
                radius * radius)
            {
                continue;
            }
            mob.ApplySpacingPulse(
                GlobalPosition,
                strength,
                evolved ? 0.62f : 0.42f);
        }

        AddMomentum(evolved ? 5.0f : 3.0f);
        GamepadInput.Rumble(
            evolved ? 0.34f : 0.2f,
            evolved ? 0.72f : 0.46f,
            0.12f);
    }

    private float SanitizeStompBounceVelocity(
        float bounceVelocity)
    {
        float fallbackVelocity =
            Mathf.Max(1.0f, ProjectedBounceLaunchSpeed);
        if (!float.IsFinite(bounceVelocity) ||
            bounceVelocity <= 0.0f)
        {
            return fallbackVelocity;
        }

        float maximumExpectedVelocity =
            fallbackVelocity *
            Mathf.Max(
                1.0f,
                GetAirComboBounceMultiplier(
                    Mathf.Max(
                        1,
                        AirComboLinksToMaximum))) *
            Mathf.Max(
                1.0f,
                ComboRouteMaxBounceScale);
        return Mathf.Min(
            bounceVelocity,
            maximumExpectedVelocity + 0.5f);
    }

    private void ApplyAirborneMotionInvariant(
        float deltaSeconds,
        Vector3 intendedVelocity,
        Vector3 actualDisplacement,
        bool touchedMob,
        bool stompResolved,
        bool hasMobFloorContact,
        bool hasMobCeilingContact,
        Vector3 mobBlockingNormal)
    {
        if (_isDead ||
            IsInsideTunnel)
        {
            _airborneStallTimer = 0.0f;
            _airborneBlockedMotionTimer = 0.0f;
            _airborneEscapeDistance = 0.0f;
            _mobBounceVelocityRecoveryAvailable =
                false;
            return;
        }
        if (stompResolved)
        {
            _airborneStallTimer = 0.0f;
            _airborneBlockedMotionTimer = 0.0f;
            return;
        }

        bool bounceProtected =
            _mobBounceFloorIgnoreTimer >
                0.0f;
        float expectedVerticalDisplacement =
            intendedVelocity.Y *
            deltaSeconds;
        bool upwardMotionWasBlocked =
            intendedVelocity.Y > 1.0f &&
            actualDisplacement.Y <
                expectedVerticalDisplacement *
                    0.2f;
        if (bounceProtected &&
            touchedMob &&
            upwardMotionWasBlocked &&
            hasMobFloorContact &&
            !hasMobCeilingContact &&
            _mobBounceVelocityRecoveryAvailable)
        {
            // The body may retain one stale floor contact immediately after
            // a stomp. Restore the awarded lift once, but never keep
            // re-applying positive velocity against a real blocker.
            Velocity = new Vector3(
                Velocity.X,
                intendedVelocity.Y,
                Velocity.Z);
            _mobBounceVelocityRecoveryAvailable =
                false;
        }
        else if (bounceProtected &&
            touchedMob &&
            upwardMotionWasBlocked &&
            hasMobCeilingContact)
        {
            // Restoring lift into a mob above the player creates a genuine
            // hover loop. A ceiling contact instead becomes a short,
            // controlled fall while the horizontal escape remains active.
            Velocity = new Vector3(
                Velocity.X,
                -Mathf.Clamp(
                    Mathf.Abs(
                        intendedVelocity.Y) *
                        0.25f,
                    2.5f,
                    5.5f),
                Velocity.Z);
            _playerJumpActive = false;
            _mobBounceVelocityRecoveryAvailable =
                false;
        }

        bool effectivelyAirborne =
            bounceProtected ||
            !IsOnWorldFloor();
        if (!effectivelyAirborne)
        {
            _airborneStallTimer = 0.0f;
            _airborneBlockedMotionTimer = 0.0f;
            _airborneEscapeDistance = 0.0f;
            _mobBounceVelocityRecoveryAvailable =
                false;
            return;
        }

        Vector2 intendedHorizontal =
            new(
                intendedVelocity.X,
                intendedVelocity.Z);
        Vector2 actualHorizontalDisplacement =
            new(
                actualDisplacement.X,
                actualDisplacement.Z);
        float expectedHorizontalDistance =
            intendedHorizontal.Length() *
            deltaSeconds;
        bool dashMotionBlocked =
            touchedMob &&
            _nitroDashSpeedProtectionTimer >
                0.0f &&
            intendedHorizontal.Length() >=
                10.0f &&
            actualHorizontalDisplacement
                .Length() <
                expectedHorizontalDistance *
                    0.25f;
        if (dashMotionBlocked)
        {
            _airborneBlockedMotionTimer +=
                deltaSeconds;
        }
        else
        {
            _airborneBlockedMotionTimer =
                Mathf.Max(
                    0.0f,
                    _airborneBlockedMotionTimer -
                        deltaSeconds * 3.0f);
        }

        bool expectedVerticalMotion =
            Mathf.Abs(intendedVelocity.Y) >
                0.75f;
        bool verticalMotionBlocked =
            expectedVerticalMotion &&
            Mathf.Abs(
                actualDisplacement.Y) <
                Mathf.Max(
                    0.006f,
                    Mathf.Abs(
                        expectedVerticalDisplacement) *
                        0.18f);
        float expectedDistance =
            intendedVelocity.Length() *
            deltaSeconds;
        bool wholeBodyBlocked =
            expectedDistance > 0.035f &&
            actualDisplacement.Length() <
                Mathf.Max(
                    0.006f,
                    expectedDistance *
                        0.12f);
        bool invalidAirborneStall =
            verticalMotionBlocked ||
            (touchedMob &&
                wholeBodyBlocked);
        if (invalidAirborneStall)
        {
            _airborneStallTimer +=
                deltaSeconds;
        }
        else
        {
            _airborneStallTimer =
                Mathf.Max(
                    0.0f,
                    _airborneStallTimer -
                        deltaSeconds * 4.0f);
            if (!touchedMob)
            {
                _airborneEscapeDistance =
                    Mathf.Max(
                        0.0f,
                        _airborneEscapeDistance -
                            deltaSeconds * 0.4f);
            }
        }

        float guardTime =
            Mathf.Max(
                0.03f,
                AirborneStallGuardTime);
        bool shouldRecover =
            _airborneStallTimer >=
                guardTime ||
            _airborneBlockedMotionTimer >=
                Mathf.Min(
                    guardTime,
                    0.05f);
        if (!shouldRecover)
        {
            return;
        }

        if (touchedMob)
        {
            TryApplyAirborneMobEscape(
                intendedVelocity,
                mobBlockingNormal,
                deltaSeconds);
        }

        // A normal apex changes sign under gravity and therefore does not
        // trip this displacement-based guard. If the body has failed to
        // move for several samples, explicitly resume a controlled descent.
        Velocity = new Vector3(
            Velocity.X,
            -Mathf.Max(
                2.5f,
                Mathf.Min(
                    5.5f,
                    Mathf.Abs(
                        intendedVelocity.Y) *
                        0.25f)),
            Velocity.Z);
        _playerJumpActive = false;
        _airborneStallTimer = 0.0f;
        _airborneBlockedMotionTimer = 0.0f;
        _mobBounceVelocityRecoveryAvailable =
            false;
    }

    private bool TryApplyAirborneMobEscape(
        Vector3 intendedVelocity,
        Vector3 blockingNormal,
        float deltaSeconds)
    {
        float remainingEscapeDistance =
            Mathf.Max(
                0.0f,
                0.18f -
                    _airborneEscapeDistance);
        if (remainingEscapeDistance <=
            0.001f)
        {
            return false;
        }

        Vector2 intendedDirection =
            new(
                intendedVelocity.X,
                intendedVelocity.Z);
        float intendedSpeed =
            intendedDirection.Length();
        if (intendedDirection.IsZeroApprox())
        {
            intendedDirection =
                new(
                    _lastMoveDirection.X,
                    _lastMoveDirection.Z);
        }
        if (intendedDirection.IsZeroApprox())
        {
            intendedDirection =
                Vector2.Up;
        }
        intendedDirection =
            intendedDirection.Normalized();

        Vector2 outward = new(
            blockingNormal.X,
            blockingNormal.Z);
        outward = outward.IsZeroApprox()
            ? -intendedDirection
            : outward.Normalized();
        Vector2 tangent =
            new(
                -outward.Y,
                outward.X);
        if (tangent.Dot(
                intendedDirection) <
            0.0f)
        {
            tangent = -tangent;
        }

        Vector2[] candidates =
        {
            (tangent * 0.72f +
                intendedDirection * 0.28f)
                .Normalized(),
            (outward * 0.55f +
                tangent * 0.45f)
                .Normalized(),
            outward,
            intendedDirection,
            (outward * 0.55f -
                tangent * 0.45f)
                .Normalized()
        };
        float requestedDistance =
            Mathf.Min(
                remainingEscapeDistance,
                Mathf.Clamp(
                    Mathf.Max(
                        6.0f,
                        intendedSpeed) *
                        deltaSeconds *
                        0.22f,
                    0.04f,
                    0.065f));
        float safeMargin =
            Mathf.Max(
                0.001f,
                SafeMargin);
        Vector3 startPosition =
            GlobalPosition;
        Vector2 selectedDirection =
            candidates[0];
        foreach (Vector2 candidate in
            candidates)
        {
            if (candidate.IsZeroApprox())
            {
                continue;
            }
            Vector3 motion =
                new Vector3(
                    candidate.X,
                    0.0f,
                    candidate.Y) *
                requestedDistance;
            if (TestMove(
                GlobalTransform,
                motion,
                null,
                safeMargin,
                false,
                4))
            {
                continue;
            }

            MoveAndCollide(
                motion,
                false,
                safeMargin,
                true,
                4);
            selectedDirection =
                candidate;
            break;
        }

        float movedDistance =
            new Vector2(
                GlobalPosition.X -
                    startPosition.X,
                GlobalPosition.Z -
                    startPosition.Z)
                .Length();
        if (movedDistance <= 0.005f)
        {
            // The player has already registered a physical touch this tick,
            // so contact damage remains valid. Only the specific bodies
            // already intersecting the player are ignored briefly, allowing
            // the next normal MoveAndSlide to leave the wedge without a
            // manual depenetration pop. Future targets and world geometry
            // remain solid.
            GrantHardWedgeCollisionExceptions();
        }

        _airborneEscapeDistance +=
            Mathf.Max(
                movedDistance,
                requestedDistance * 0.35f);
        float recoveredSpeed =
            Mathf.Max(
                new Vector2(
                    Velocity.X,
                    Velocity.Z)
                    .Length(),
                intendedSpeed * 0.7f);
        Velocity = new Vector3(
            selectedDirection.X *
                recoveredSpeed,
            Velocity.Y,
            selectedDirection.Y *
                recoveredSpeed);
        return movedDistance >
            0.005f;
    }

    private void UpdateAirborneMobCollisionExceptions(
        float deltaSeconds)
    {
        if (_airborneMobCollisionExceptions
            .Count == 0)
        {
            return;
        }

        _airborneMobCollisionExceptionKeys
            .Clear();
        foreach ((ulong instanceId,
            MobCollisionException exception) in
            _airborneMobCollisionExceptions)
        {
            exception.TimeRemaining -=
                deltaSeconds;
            if (exception.TimeRemaining >
                    0.0f &&
                IsInstanceValid(
                    exception.Mob) &&
                !exception.Mob
                    .IsQueuedForDeletion())
            {
                continue;
            }
            _airborneMobCollisionExceptionKeys
                .Add(
                    instanceId);
        }

        foreach (ulong instanceId in
            _airborneMobCollisionExceptionKeys)
        {
            MobCollisionException exception =
                _airborneMobCollisionExceptions[
                    instanceId];
            if (IsInstanceValid(
                exception.Mob))
            {
                RemoveCollisionExceptionWith(
                    exception.Mob);
            }
            _airborneMobCollisionExceptions
                .Remove(
                    instanceId);
        }
        _airborneMobCollisionExceptionKeys
            .Clear();
    }

    private bool GrantHardWedgeCollisionExceptions()
    {
        bool grantedException = false;
        for (int index = 0;
            index < GetSlideCollisionCount();
            index++)
        {
            if (GetSlideCollision(index)
                    .GetCollider() is
                Mob mob)
            {
                grantedException |=
                    GrantHardWedgeCollisionException(
                        mob);
            }
        }
        if (_mobDetector != null)
        {
            foreach (Node3D body in
                _mobDetector
                    .GetOverlappingBodies())
            {
                if (body is Mob mob)
                {
                    grantedException |=
                        GrantHardWedgeCollisionException(
                            mob);
                }
            }
        }
        if (_playerCollisionShape?.Shape !=
                null &&
            GetWorld3D() != null)
        {
            PhysicsShapeQueryParameters3D query =
                new()
                {
                    Shape =
                        _playerCollisionShape
                            .Shape,
                    Transform =
                        _playerCollisionShape
                            .GlobalTransform,
                    CollisionMask =
                        1u << 1,
                    Margin =
                        Mathf.Max(
                            0.001f,
                            SafeMargin),
                    CollideWithBodies =
                        true,
                    CollideWithAreas =
                        false,
                    Exclude =
                        new Godot
                            .Collections
                            .Array<Rid>
                        {
                            GetRid()
                        }
                };
            foreach (Godot.Collections
                .Dictionary hit in
                GetWorld3D()
                    .DirectSpaceState
                    .IntersectShape(
                        query,
                        32))
            {
                if ((GodotObject)hit[
                        "collider"] is
                    Mob mob)
                {
                    grantedException |=
                        GrantHardWedgeCollisionException(
                            mob);
                }
            }
        }
        return grantedException;
    }

    private bool GrantHardWedgeCollisionException(
        Mob mob)
    {
        if (!IsInstanceValid(mob) ||
            mob.IsQueuedForDeletion())
        {
            return false;
        }

        ulong instanceId =
            mob.GetInstanceId();
        float exceptionTime =
            Mathf.Max(
                0.06f,
                AirborneStallGuardTime +
                    0.02f);
        if (_airborneMobCollisionExceptions
            .TryGetValue(
                instanceId,
                out MobCollisionException existing))
        {
            existing.TimeRemaining =
                Mathf.Max(
                    existing.TimeRemaining,
                    exceptionTime);
            return true;
        }

        AddCollisionExceptionWith(
            mob);
        _airborneMobCollisionExceptions[
            instanceId] =
            new MobCollisionException
            {
                Mob = mob,
                TimeRemaining =
                    exceptionTime
            };
        return true;
    }

    private bool IsOnWorldFloor()
    {
        if (!IsOnFloor())
        {
            return false;
        }

        Vector3 upDirection =
            UpDirection.IsZeroApprox()
                ? Vector3.Up
                : UpDirection.Normalized();
        float floorDotThreshold =
            Mathf.Cos(
                Mathf.Clamp(
                    FloorMaxAngle,
                    0.0f,
                    Mathf.Pi * 0.5f));
        for (int index = 0;
            index < GetSlideCollisionCount();
            index++)
        {
            KinematicCollision3D collision =
                GetSlideCollision(index);
            if (collision.GetNormal().Dot(
                    upDirection) <
                floorDotThreshold)
            {
                continue;
            }

            // Mobs can be stomped, touched or escaped around, but they must
            // never become a platform that suppresses player gravity.
            if (collision.GetCollider() is
                not Mob)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateComboGroundGrace(
        bool continuingMobBounce,
        bool jumpPressed,
        float deltaSeconds)
    {
        if (_airStompChain <= 0 ||
            continuingMobBounce)
        {
            return;
        }

        _comboGroundGraceTimer = Mathf.Max(
            0.0f,
            _comboGroundGraceTimer - deltaSeconds);
        if (jumpPressed &&
            _comboGroundGraceTimer > 0.0f)
        {
            if (ComboRescueAvailable)
            {
                ActivateComboRescue(true);
            }

            _comboGroundGraceTimer =
                GetEffectiveComboGroundGraceTime();
            return;
        }

        if (_comboGroundGraceTimer <= 0.0f)
        {
            EndComboFlow();
        }
    }

    private void ActivateComboRescue(
        bool fromGroundGrace)
    {
        if (!ComboRescueAvailable ||
            !_comboState.ActivateRescue(
                fromGroundGrace,
                GetEffectiveFinisherThreshold()))
        {
            return;
        }

        AddNitro(fromGroundGrace ? 6.0f : 4.0f);
        ApplyTraversalSpeedBoost(
            fromGroundGrace ? 1.4f : 0.9f,
            fromGroundGrace ? 0.65f : 0.45f);
        EmitSignal(
            SignalName.ComboRescued,
            _airStompChain,
            fromGroundGrace);
    }

    private bool TryAcquireComboRescueRoute(
        Vector3 movementIntent,
        Vector3 launchVelocity)
    {
        if (!StompAssistEnabled ||
            IsInsideTunnel)
        {
            return false;
        }

        Vector3 intendedHeading = new(
            movementIntent.X,
            0.0f,
            movementIntent.Z);
        if (intendedHeading.IsZeroApprox())
        {
            intendedHeading = _lastMoveDirection;
        }

        if (intendedHeading.IsZeroApprox())
        {
            return false;
        }

        intendedHeading = intendedHeading.Normalized();
        float maximumDistance =
            GetAirComboRouteMaxDistance(
                Mathf.Max(1, _airStompChain)) *
            1.08f;
        float minimumAlignment =
            Mathf.Cos(
                Mathf.DegToRad(
                    Mathf.Min(
                        58.0f,
                        ComboRouteSearchAngle + 6.0f)));
        Mob bestTarget = null;
        float bestScore = float.MaxValue;
        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob candidate ||
                !IsValidComboRouteTarget(candidate))
            {
                continue;
            }

            Vector3 offset =
                candidate.GlobalPosition -
                GlobalPosition;
            offset.Y = 0.0f;
            float distance = offset.Length();
            if (distance <= 1.0f ||
                distance > maximumDistance)
            {
                continue;
            }

            Vector3 targetHeading =
                offset / distance;
            float alignment =
                intendedHeading.Dot(targetHeading);
            if (alignment < minimumAlignment)
            {
                continue;
            }

            float score =
                (1.0f - alignment) * 6.0f +
                distance * 0.055f -
                candidate.JumpOpportunityPriority * 1.2f -
                (candidate.IsComboRouteMember
                    ? 0.7f
                    : 0.0f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        if (!IsValidComboRouteTarget(bestTarget))
        {
            return false;
        }

        _comboRouteTarget = bestTarget;
        _comboRouteCruiseSpeed = Mathf.Clamp(
            new Vector2(
                launchVelocity.X,
                launchVelocity.Z).Length(),
            4.0f,
            ComboRouteAbsoluteMaxSpeed);
        _comboRouteGraceTimer =
            ComboRouteInputGraceTime + 0.08f;
        _comboRouteReplanTimer = 0.0f;
        ComboRoutePlannedTargets =
            Mathf.Max(1, ComboRoutePlannedTargets);
        return true;
    }

    private void RegisterComboImpact(
        Mob mob,
        bool counterStomp,
        ref int scoreMultiplier)
    {
        EnemyArchetypeId archetype =
            mob.ArchetypeId;
        bool changedArchetype =
            !_lastComboArchetype.HasValue ||
            _lastComboArchetype.Value != archetype;
        float horizontalSpeed =
            new Vector2(Velocity.X, Velocity.Z).Length();
        int speedStyle = Mathf.RoundToInt(
            Mathf.Clamp(
                horizontalSpeed /
                    Mathf.Max(1.0f, CurrentRunSpeed) -
                    0.75f,
                0.0f,
                1.25f) *
            5.0f);
        int styleGain =
            9 +
            Mathf.Min(6, _airStompChain) +
            speedStyle +
            (changedArchetype ? 5 : 0) +
            (counterStomp ? 9 : 0) +
            (ComboRoutePlannedTargets >= 2 ? 3 : 0) +
            (mob.WillBeDefeatedByNextStomp &&
                    archetype == EnemyArchetypeId.Shell
                ? 8
                : 0);
        float finisherThreshold =
            GetEffectiveFinisherThreshold();
        if (HasRunUpgrade(RunUpgradeId.GlassHeart) &&
            HealthRatio <= 0.4f)
        {
            styleGain = Mathf.CeilToInt(
                styleGain * (1.0f + .25f * GetRunUpgradeRank(RunUpgradeId.GlassHeart)));
        }
        PlayerComboImpact impact =
            _comboState.RegisterImpact(
                styleGain,
                ComboFinisherChargeScale,
                finisherThreshold);
        _lastComboArchetype = archetype;
        if (impact.Finisher)
        {
            scoreMultiplier +=
                Mathf.Max(1, mob.StompScoreMultiplier) * 2;
            AddNitro(
                ComboFinisherNitroReward +
                _runFinisherNitroBonus);
            AddMomentum(12.0f);
            GrantDamageInvulnerability(
                ComboFinisherInvulnerability);
            ApplyTraversalSpeedBoost(2.6f, 0.85f);
        }

        EmitSignal(
            SignalName.ComboImpact,
            _airStompChain,
            _comboStylePoints,
            (int)CurrentComboFlowTier,
            (int)archetype,
            counterStomp,
            impact.Milestone,
            impact.Finisher,
            mob.GlobalPosition);
    }

    private void ApplyRunStompEffects()
    {
        if (_afterburnerLinksRemaining > 0)
        {
            _afterburnerLinksRemaining--;
            AddNitro(
                HasRunUpgrade(RunUpgradeId.BlueComet)
                    ? 2.4f
                    : 1.5f);
            ApplyTraversalSpeedBoost(
                HasRunUpgrade(RunUpgradeId.BlueComet)
                    ? 2.2f
                    : 1.4f,
                0.42f);
        }

        if (_kineticReserveCharges > 0)
        {
            _kineticReserveCharges--;
            EmitSignal(SignalName.RunBuffTriggered, (int)(HasRunUpgrade(RunUpgradeId.PerfectCircuit) ? RunUpgradeId.PerfectCircuit : RunUpgradeId.KineticReserve));
            AddNitro(6.0f * GetRunUpgradeRank(RunUpgradeId.KineticReserve));
            _comboState.AddStyle(8 * GetRunUpgradeRank(RunUpgradeId.KineticReserve));
            ApplyTraversalSpeedBoost(2.0f, 0.58f);
        }

        if (HasRunUpgrade(RunUpgradeId.ClosedLoop))
        {
            int loopLength =
                HasRunUpgrade(RunUpgradeId.InfiniteLine)
                    ? 4
                    : 5;
            if (_airStompChain > 0 &&
                _airStompChain % loopLength == 0)
            {
                EmitSignal(SignalName.RunBuffTriggered, (int)(HasRunUpgrade(RunUpgradeId.InfiniteLine) ? RunUpgradeId.InfiniteLine : RunUpgradeId.ClosedLoop));
                AddNitro(
                    (HasRunUpgrade(RunUpgradeId.InfiniteLine) ? 9.0f : 6.0f) *
                    (1 + .3f * (GetRunUpgradeRank(RunUpgradeId.ClosedLoop) - 1)));
                if (HasRunUpgrade(RunUpgradeId.InfiniteLine))
                {
                    _comboGroundGraceTimer = Mathf.Max(
                        _comboGroundGraceTimer,
                        GetEffectiveComboGroundGraceTime() +
                            0.12f);
                }
            }
        }

        if (_phaseRouteRefundPending)
        {
            _phaseRouteRefundPending = false;
            EmitSignal(SignalName.RunBuffTriggered, (int)RunUpgradeId.PhaseRoute);
            AddNitro(
                Mathf.Max(0.0f, RiftTeleportCost) *
                0.5f);
        }

        if (_redThreadPending)
        {
            _redThreadPending = false;
            EmitSignal(SignalName.RunBuffTriggered, (int)(HasRunUpgrade(RunUpgradeId.ChainReaction) ? RunUpgradeId.ChainReaction : RunUpgradeId.RedThread));
            _comboRouteGraceTimer = Mathf.Max(
                _comboRouteGraceTimer,
                ComboRouteInputGraceTime +
                    .08f * (GetRunUpgradeRank(RunUpgradeId.RedThread) - 1) +
                    (HasRunUpgrade(
                        RunUpgradeId.ChainReaction)
                        ? 0.24f
                        : 0.14f));
            ComboRoutePlannedTargets = Mathf.Max(
                ComboRoutePlannedTargets,
                HasRunUpgrade(RunUpgradeId.ChainReaction)
                    ? 2
                    : 1);
        }
    }

    private void EndComboFlow(
        bool bankRewards = true)
    {
        PlayerComboEnd ended =
            _comboState.EndFlow();
        int endedChain = ended.ChainCount;
        int endedStyle = ended.StylePoints;
        if (bankRewards &&
            endedChain >= 3)
        {
            AddNitro(
                Mathf.Min(
                    14.0f,
                    endedStyle * 0.04f));
            AddMomentum(
                Mathf.Min(
                    8.0f,
                    endedChain * 0.45f));
        }
        if (endedChain >= 2)
        {
            EmitSignal(
                SignalName.ComboEnded,
                endedChain,
                endedStyle);
        }
    }

    private void UpdateEnemyContactSafety(float deltaSeconds)
    {
        _contactTracker.BeginFrame(
            deltaSeconds);
        _activeEnemyContacts.Clear();
        if (_isDead || IsInsideTunnel)
        {
            _contactTracker.ClearDanger();
            return;
        }

        foreach (Node3D body in _mobDetector.GetOverlappingBodies())
        {
            if (body is Mob mob)
            {
                RegisterEnemyContact(mob, false);
            }
        }

        for (int index = 0;
            index < GetSlideCollisionCount();
            index++)
        {
            if (GetSlideCollision(index).GetCollider() is Mob mob)
            {
                RegisterEnemyContact(mob, true);
            }
        }

        foreach ((ulong instanceId, Mob mob) in
            _activeEnemyContacts)
        {
            if (!IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion())
            {
                _contactTracker.RemoveContact(
                    instanceId);
                continue;
            }

            Vector3 horizontalSeparation =
                GlobalPosition - mob.GlobalPosition;
            horizontalSeparation.Y = 0.0f;
            bool hasStompPriority =
                !IsOnWorldFloor() &&
                Velocity.Y < -0.25f &&
                GlobalPosition.Y - mob.GlobalPosition.Y >=
                    AirborneContactSafeHeight &&
                horizontalSeparation.Length() <= 1.55f;
            bool hasClutchVaultPriority =
                _clutchVaultContactGraceTimer >
                    0.0f &&
                mob ==
                    _clutchVaultActiveTarget;
            bool protectedContact =
                IsDamageInvulnerable ||
                hasStompPriority ||
                hasClutchVaultPriority ||
                !mob.CanDamagePlayer;
            if (protectedContact)
            {
                _contactTracker.ResetExposure(
                    instanceId);
                if (IsDamageInvulnerable)
                {
                    ResolvePostHitSeparation(mob, instanceId);
                }

                continue;
            }

            if (!TryGetDangerousContactWindow(
                mob,
                _contactTracker.IsPhysical(
                    instanceId),
                deltaSeconds,
                out float requiredContactTime))
            {
                _contactTracker.ResetExposure(
                    instanceId);
                continue;
            }

            if (!_contactTracker.AccumulateDanger(
                    instanceId,
                    deltaSeconds,
                    requiredContactTime))
            {
                continue;
            }

            _contactTracker.ClearDanger();
            TakeDamage(mob);
            return;
        }

        CleanupEnemyContactMemory();
    }

    private void RegisterEnemyContact(
        Mob mob,
        bool physicalContact)
    {
        if (!IsInstanceValid(mob) ||
            mob.IsQueuedForDeletion())
        {
            return;
        }

        ulong instanceId = mob.GetInstanceId();
        _activeEnemyContacts[instanceId] = mob;
        _contactTracker.Register(
            instanceId,
            physicalContact);
    }

    private bool TryGetDangerousContactWindow(
        Mob mob,
        bool physicalContact,
        float deltaSeconds,
        out float requiredContactTime)
    {
        requiredContactTime = 0.0f;
        Vector3 separation =
            GlobalPosition - mob.GlobalPosition;
        separation.Y = 0.0f;
        float distance = separation.Length();
        if (!physicalContact &&
            distance >
            Mathf.Max(1.2f, EnemyContactRadius))
        {
            return false;
        }

        if (physicalContact)
        {
            // A real body collision is decisive on the same physics tick.
            // Only the slightly larger Area3D sensor receives debouncing.
            requiredContactTime = 0.0f;
            return true;
        }

        // Sensor confirmation is capped to two physics samples. This makes
        // contact feel equally immediate at 60/120 Hz while still discarding
        // a one-frame broad-phase overlap.
        requiredContactTime = Mathf.Min(
            Mathf.Max(0.005f, EnemyContactConfirmationTime),
            Mathf.Max(0.005f, deltaSeconds * 1.25f));
        return true;
    }

    private void ResolvePostHitSeparation(
        Mob mob,
        ulong instanceId)
    {
        if (!_contactTracker.TryBeginReaction(
                instanceId,
                GrazeReactionCooldown))
        {
            return;
        }
        mob.ResolvePostHitPlayerContact(
            GlobalPosition,
            Velocity);

        Vector3 away = GlobalPosition - mob.GlobalPosition;
        away.Y = 0.0f;
        if (away.IsZeroApprox())
        {
            away = -_lastMoveDirection;
        }

        away = away.Normalized();
        Vector3 horizontalVelocity =
            new(Velocity.X, 0.0f, Velocity.Z);
        Vector3 tangent =
            Vector3.Up.Cross(away).Normalized();
        if (!horizontalVelocity.IsZeroApprox() &&
            tangent.Dot(horizontalVelocity) < 0.0f)
        {
            tangent = -tangent;
        }

        Vector3 slipDirection =
            (away * 0.46f + tangent * 0.54f).Normalized();
        Vector3 slippedVelocity =
            horizontalVelocity +
            slipDirection * Mathf.Max(0.0f, GrazeSlipSpeed);
        float maximumSpeed = Mathf.Max(
            2.0f,
            horizontalVelocity.Length() +
                GrazeSlipSpeed * 0.35f);
        slippedVelocity =
            slippedVelocity.LimitLength(maximumSpeed);
        Velocity = new Vector3(
            slippedVelocity.X,
            Velocity.Y,
            slippedVelocity.Z);
    }

    private void CleanupEnemyContactMemory()
    {
        _contactTracker.EndFrame();
    }

    public void AddMomentum(float amount)
    {
        SetMomentum(Momentum + Mathf.Max(0.0f, amount));
    }

    public int GetRunUpgradeRank(RunUpgradeId id)
    {
        return _runBuild.GetRank(id);
    }

    public bool HasRunUpgrade(RunUpgradeId id)
    {
        return _runBuild.HasUpgrade(id);
    }

    /// <summary>
    /// Applies an already validated draft result. Values are rebuilt from
    /// ranks instead of accumulated in-place, so reapplying a UI event can
    /// never double a modifier or corrupt the build.
    /// </summary>
    public void ApplyRunUpgrade(
        RunUpgradeId id,
        int rank)
    {
        int safeRank = Mathf.Clamp(rank, 0, RunProgression.GetDefinition(id).MaxRank);
        if (_runBuild.GetRank(id) == safeRank)
        {
            return;
        }

        float previousMaxHealth = MaxHealth;
        float previousNitroCapacity = NitroCapacity;
        _runBuild.SetRank(id, safeRank);
        RebuildRunModifiers();
        RunSkills?.Refresh();

        float healthCapacityGain = Mathf.Max(
            0.0f,
            MaxHealth - previousMaxHealth);
        float nitroCapacityGain = Mathf.Max(
            0.0f,
            NitroCapacity - previousNitroCapacity);
        if (healthCapacityGain > 0.0f)
        {
            SetHealth(Health + healthCapacityGain);
        }
        else
        {
            SetHealth(Health);
        }
        if (nitroCapacityGain > 0.0f)
        {
            SetNitroCharge(
                NitroCharge +
                nitroCapacityGain * 0.75f);
        }
        else
        {
            SetNitroCharge(NitroCharge);
            EmitNitroChanged();
        }

        // A device should announce itself in the hands, not only in text.
        if (id == RunUpgradeId.QuickSpark)
        {
            AddNitro(8.0f);
        }
        else if (id is RunUpgradeId.Afterburner or
            RunUpgradeId.KineticReserve or
            RunUpgradeId.ClosedLoop)
        {
            AddNitro(5.0f);
        }
    }

    public void OnRunEnemyDefeated()
    {
        if (HasRunUpgrade(RunUpgradeId.SecondWind) &&
            HealthRatio <= 0.4f)
        {
            _criticalDefeatCounter++;
            int threshold = Mathf.Max(
                2,
                4 -
                    Mathf.Max(
                        0,
                        GetRunUpgradeRank(
                            RunUpgradeId.SecondWind) - 1));
            if (_criticalDefeatCounter >= threshold)
            {
                _criticalDefeatCounter = 0;
                SetHealth(Health + 5.0f);
                EmitSignal(SignalName.RunBuffTriggered, (int)RunUpgradeId.SecondWind);
            }
        }

        if (HasRunUpgrade(RunUpgradeId.LastLight) &&
            _airStompChain > 0 &&
            Health < MaxHealth)
        {
            _lastLightDefeatCounter++;
            if (_lastLightDefeatCounter >= 3)
            {
                _lastLightDefeatCounter = 0;
                SetHealth(
                    Health +
                    Mathf.Min(
                        4.0f,
                        MaxHealth * 0.035f));
                EmitSignal(SignalName.RunBuffTriggered, (int)RunUpgradeId.LastLight);
            }
        }
    }

    private void RebuildRunModifiers()
    {
        MaxHealth = _runBuild.MaxHealth;
        NitroCapacity = _runBuild.NitroCapacity;
    }

    public void AddNitro(float amount)
    {
        SetNitroCharge(NitroCharge + amount);
    }

    public void RewardEnemyDefeat(
        float bonusNitro,
        float bonusHealing)
    {
        AddNitro(Mathf.Max(0.0f, bonusNitro));
        if (bonusHealing > 0.0f)
        {
            SetHealth(
                Health +
                Mathf.Max(0.0f, bonusHealing));
        }
    }

    public void LoseMomentum(float amount)
    {
        SetMomentum(Momentum - Mathf.Max(0.0f, amount));
    }

    public void RewardDangerousSection(float amount = 14.0f)
    {
        AddMomentum(amount);
        AddNitro(Mathf.Max(2.0f, amount * 0.32f));
    }

    public void RewardCollectible(bool difficultTrajectory)
    {
        AddMomentum(difficultTrajectory ? 14.0f : 6.0f);
        AddNitro(difficultTrajectory ? 6.0f : 2.5f);
    }

    public void SetArenaCombatActive(bool active)
    {
        IsArenaCombatActive = active;
    }

    public void ApplyObstacleSlow(float amount, float duration)
    {
        ActiveObstacleSlowAmount = Mathf.Max(
            ActiveObstacleSlowAmount,
            Mathf.Max(0.0f, amount));
        _obstacleSlowTimer = Mathf.Max(
            _obstacleSlowTimer,
            Mathf.Max(0.0f, duration));
    }

    public void ApplyTunnelSpeedBoost(float amount, float duration)
    {
        ApplySpeedBoost(amount, duration, 1.0f);
    }

    public void EnterTunnelPhase()
    {
        if (IsInsideTunnel)
        {
            return;
        }

        IsInsideTunnel = true;
        EmitSignal(
            SignalName.TunnelEntered);
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        ClearComboRoute();
        // Only the enemy bit is removed. The player still collides with the
        // tunnel, floor and every traversal obstacle.
        CollisionMask &= ~(1u << 1);
    }

    public void ExitTunnelPhase()
    {
        if (!IsInsideTunnel)
        {
            return;
        }

        IsInsideTunnel = false;
        CollisionMask = _normalCollisionMask;
    }

    public void ApplyTraversalSpeedBoost(float amount, float duration)
    {
        ApplySpeedBoost(amount, duration, 0.36f);
    }

    public void BeginTraversalRail(
        Vector3 worldDirection,
        float minimumSpeed,
        float duration,
        float steeringAuthority)
    {
        Vector3 direction = new(
            worldDirection.X,
            0.0f,
            worldDirection.Z);
        if (direction.IsZeroApprox())
        {
            direction = _lastMoveDirection;
        }
        if (direction.IsZeroApprox())
        {
            direction = Vector3.Forward;
        }

        _traversalRailDirection =
            direction.Normalized();
        _traversalRailMinimumSpeed =
            Mathf.Max(
                CurrentRunSpeed,
                minimumSpeed);
        _traversalRailTimer =
            Mathf.Max(
                _traversalRailTimer,
                Mathf.Max(0.15f, duration));
        _traversalRailSteering =
            Mathf.Clamp(
                steeringAuthority,
                0.0f,
                0.72f);
        EmitSignal(
            SignalName.TraversalRailEntered,
            _traversalRailMinimumSpeed);
        ApplyTraversalSpeedBoost(
            Mathf.Max(
                1.0f,
                _traversalRailMinimumSpeed -
                    CurrentRunSpeed),
            _traversalRailTimer);
    }

    public void ApplyCourseLaunch(
        Vector3 worldDirection,
        float forwardSpeed,
        float verticalSpeed,
        float boostDuration)
    {
        if (_isDead)
        {
            return;
        }

        Vector3 direction = new(
            worldDirection.X,
            0.0f,
            worldDirection.Z);
        if (direction.IsZeroApprox())
        {
            direction = _lastMoveDirection;
        }
        if (direction.IsZeroApprox())
        {
            direction = Vector3.Forward;
        }
        direction = direction.Normalized();

        _traversalRailTimer = 0.0f;
        _traversalRailMinimumSpeed =
            0.0f;
        _traversalRailSteering =
            0.0f;
        float currentHorizontalSpeed =
            new Vector2(
                Velocity.X,
                Velocity.Z).Length();
        float launchSpeed =
            Mathf.Max(
                Mathf.Max(
                    CurrentRunSpeed,
                    forwardSpeed),
                currentHorizontalSpeed *
                    0.88f);
        Velocity = new Vector3(
            direction.X * launchSpeed,
            Mathf.Max(
                Velocity.Y,
                verticalSpeed),
            direction.Z * launchSpeed);
        _lastMoveDirection = direction;
        _playerJumpActive = false;
        _airJumpsRemaining =
            Mathf.Max(
                _airJumpsRemaining,
                1);
        EmitSignal(
            SignalName.CourseLaunched,
            launchSpeed);
        ApplyTraversalSpeedBoost(
            Mathf.Max(
                1.0f,
                launchSpeed -
                    CurrentRunSpeed),
            Mathf.Max(
                0.25f,
                boostDuration));
        GrantDamageInvulnerability(0.16f);
    }

    public void GrantDamageInvulnerability(float duration)
    {
        _damageInvulnerabilityTimer = Mathf.Max(
            _damageInvulnerabilityTimer,
            Mathf.Max(0.0f, duration));
    }

    private void ApplySpeedBoost(
        float amount,
        float duration,
        float visualIntensity)
    {
        float safeAmount = Mathf.Max(0.0f, amount);
        float safeDuration = Mathf.Max(0.0f, duration);
        if (safeAmount <= 0.0f || safeDuration <= 0.0f)
        {
            return;
        }

        // Weaker impulses cannot lengthen a stronger launch. This keeps a
        // close-call microboost from accidentally extending a tunnel boost.
        if (_speedBoostTimer > 0.0f &&
            safeAmount + 0.05f < ActiveSpeedBoost)
        {
            return;
        }

        ActiveSpeedBoost = Mathf.Max(ActiveSpeedBoost, safeAmount);
        _speedBoostTimer = Mathf.Max(_speedBoostTimer, safeDuration);
        SpeedBoostVisualIntensity = Mathf.Max(
            SpeedBoostVisualIntensity,
            Mathf.Clamp(visualIntensity, 0.0f, 1.0f));
    }

    private void UpdateTraversalRail(
        float deltaSeconds)
    {
        _traversalRailTimer =
            Mathf.Max(
                0.0f,
                _traversalRailTimer -
                    deltaSeconds);
        if (_traversalRailTimer <= 0.0f)
        {
            _traversalRailMinimumSpeed =
                0.0f;
            _traversalRailSteering =
                0.0f;
        }
    }

    private void ApplyTraversalRailMotion(
        ref Vector3 velocity,
        ref Vector3 movementDirection,
        float inputStrength,
        float deltaSeconds)
    {
        if (_traversalRailTimer <= 0.0f ||
            _traversalRailDirection
                .IsZeroApprox())
        {
            return;
        }

        Vector3 desiredDirection =
            _traversalRailDirection;
        Vector3 inputDirection = new(
            movementDirection.X,
            0.0f,
            movementDirection.Z);
        if (!inputDirection.IsZeroApprox() &&
            inputStrength > 0.01f)
        {
            desiredDirection =
                desiredDirection.Lerp(
                    inputDirection.Normalized(),
                    _traversalRailSteering *
                        inputStrength)
                .Normalized();
        }

        Vector2 horizontal = new(
            velocity.X,
            velocity.Z);
        float targetSpeed =
            Mathf.Max(
                horizontal.Length(),
                _traversalRailMinimumSpeed);
        float speed =
            Mathf.MoveToward(
                horizontal.Length(),
                targetSpeed,
                220.0f * deltaSeconds);
        velocity.X =
            desiredDirection.X * speed;
        velocity.Z =
            desiredDirection.Z * speed;
        movementDirection =
            desiredDirection;
        _lastMoveDirection =
            desiredDirection;
    }

    public void SetContractRecoveryMode(int mode)
    {
        ContractRecoveryMode selectedMode =
            ContractRecoveryMode.HuntOrEndure;
        if (SelectedContractRecoveryMode == selectedMode)
        {
            return;
        }

        SelectedContractRecoveryMode = selectedMode;
        if (RecoveryTarget > 0)
        {
            StartRecoveryTimer();
        }
        else
        {
            ClearRecoveryTimer();
        }

        EmitRecoveryContractChanged(true);
    }

    public void SetControlStyle(int style)
    {
        SelectedControlStyle =
            ControlStyle.Agile;
        (GroundAcceleration,
            GroundTurnAcceleration,
            GroundDeceleration,
            AirAcceleration,
            AirTurnAcceleration,
            AirDeceleration,
            NitroSteeringResponsiveness,
            JumpReleaseGravityMultiplier,
            JumpApexGravityMultiplier) =
            (116.0f, 220.0f, 150.0f,
                68.0f, 118.0f, 28.0f,
                7.5f, 2.45f, 0.58f);
    }

    public void SetSurvivalAssistLevel(int level)
    {
        SurvivalAssistLevel = (AssistLevel)Mathf.Clamp(
            level,
            0,
            (int)AssistLevel.Strong);
        // OFF is the baseline fairness layer: it never chooses a route or
        // reverses input, but softens precision tax at convergences.
        SurvivalAssistEnabled = true;

        (SurvivalAssistRadius,
            SurvivalAssistLookAheadTime,
            SurvivalAssistMaxCorrectionSpeed,
            SurvivalAssistResponsiveness) = SurvivalAssistLevel switch
        {
            AssistLevel.Off => (6.15f, 0.66f, 2.4f, 8.8f),
            AssistLevel.Strong => (9.0f, 0.96f, 4.5f, 13.2f),
            _ => (8.2f, 0.9f, 3.8f, 11.7f)
        };
    }

    public void SetStompAssistLevel(int level)
    {
        StompAssistLevel = (AssistLevel)Mathf.Clamp(
            level,
            0,
            (int)AssistLevel.Strong);
        StompAssistEnabled = true;

        (StompAssistRadius,
            StompAssistMaxHeight,
            StompAssistStrength,
            StompAssistMaxCorrectionSpeed) = StompAssistLevel switch
        {
            AssistLevel.Off => (2.45f, 4.6f, 7.8f, 3.2f),
            AssistLevel.Strong => (4.05f, 6.5f, 13.0f, 5.8f),
            _ => (3.35f, 5.8f, 10.8f, 4.75f)
        };
        (FirstStompGuideMaxTurnAngle,
            FirstStompGuideMaxSpeedAdjustment,
            FirstStompGuideAcceleration,
            FirstStompGuideDescentHandoffSpeed,
            FirstStompGuideInputGraceTime,
            FirstStompGuideLandingPocket) =
            StompAssistLevel switch
            {
                AssistLevel.Off =>
                    (17.5f, 1.3f, 27.0f, 6.1f, 0.135f, 0.44f),
                AssistLevel.Strong =>
                    (31.0f, 4.2f, 44.0f, 9.0f, 0.28f, 0.74f),
                _ =>
                    (25.5f, 2.5f, 39.0f, 8.3f, 0.22f, 0.63f)
            };
        FirstStompGuideCandidateMemory =
            StompAssistLevel switch
            {
                AssistLevel.Off => 0.1f,
                AssistLevel.Strong => 0.26f,
                _ => 0.18f
            };
        FirstStompGuideMinimumDistance =
            StompAssistLevel switch
            {
                AssistLevel.Off => 2.0f,
                AssistLevel.Strong => 1.35f,
                _ => 1.65f
            };
        FirstStompIntentAcquisitionAngle =
            StompAssistLevel switch
            {
                AssistLevel.Off => 22.0f,
                AssistLevel.Strong => 38.0f,
                _ => 34.0f
            };
        (FirstStompIntentMaximumDistance,
            FirstStompIntentMinimumSpeedRatio,
            FirstStompGuideMinimumInputStrength,
            FirstStompGuideCorrectionShare) =
            StompAssistLevel switch
            {
                AssistLevel.Off =>
                    (14.3f, 0.34f, 0.2f, 0.55f),
                AssistLevel.Strong =>
                    (21.5f, 0.12f, 0.1f, 0.8f),
                _ =>
                    (19.0f, 0.18f, 0.1f, 0.74f)
            };
        (ComboRouteSearchAngle,
            ComboRouteMaxTurnAngle,
            ComboRouteMinSpeedScale,
            ComboRouteMaxSpeedScale,
            ComboRouteResponsiveness,
            ComboRouteInputGraceTime,
            ComboRouteMinBounceScale,
            ComboRouteMaxBounceScale) = StompAssistLevel switch
        {
            AssistLevel.Off =>
                (42.0f, 16.5f, 0.58f, 1.14f, 9.7f, 0.135f, 0.76f, 1.24f),
            AssistLevel.Strong =>
                (72.0f, 31.0f, 0.36f, 1.36f, 17.5f, 0.32f, 0.61f, 1.35f),
            _ =>
                (62.0f, 25.5f, 0.43f, 1.27f, 14.5f, 0.24f, 0.66f, 1.34f)
        };
        (ComboRouteAssistInfluence,
            ComboRouteMinimumInputStrength,
            ComboRouteSpeedAssistAllowance,
            ComboRouteMaximumRangeAdjustment,
            ComboRouteCancelDotThreshold) =
            StompAssistLevel switch
            {
                AssistLevel.Off =>
                    (0.54f, 0.2f, 1.1f, 2.9f, 0.57f),
                AssistLevel.Strong =>
                    (0.78f, 0.1f, 3.8f, 6.5f, 0.28f),
                _ =>
                    (0.68f, 0.1f, 2.45f, 4.9f, 0.41f)
            };
        ComboRouteMinimumAssistDistance =
            StompAssistLevel switch
            {
                AssistLevel.Off => 1.6f,
                AssistLevel.Strong => 1.3f,
                _ => 1.45f
            };
        ComboRouteAbsoluteMaxSpeed =
            StompAssistLevel switch
            {
                AssistLevel.Off => 26.0f,
                AssistLevel.Strong => 32.0f,
                _ => 29.0f
            };
        (AirComboLinksToMaximum,
            AirComboMaximumSpeedBonus,
            AirComboMaximumBounceBonus,
            AirComboMaximumDistanceBonus) =
            StompAssistLevel switch
            {
                AssistLevel.Off =>
                    (6, 3.0f, 0.08f, 5.0f),
                AssistLevel.Strong =>
                    (4, 4.8f, 0.13f, 8.0f),
                _ =>
                    (5, 3.8f, 0.1f, 6.5f)
            };
    }

    public void ApplyBodyColor(Color color)
    {
        GetPresentation()?.ApplyBodyColor(color);
    }

    public void ApplySkin(
        int skinIndex,
        Color primary,
        Color secondary,
        Color accent)
    {
        GetPresentation()?.ApplySkin(
            skinIndex,
            primary,
            secondary,
            accent);
    }

    public void SetEffectsQuality(int quality)
    {
        EffectsQuality = Mathf.Clamp(quality, 0, 3);
        GetPresentation()?.SetEffectsQuality(EffectsQuality);
    }

    private PlayerPresentation GetPresentation()
    {
        if (!IsInstanceValid(_presentation))
        {
            _presentation =
                GetNodeOrNull<PlayerPresentation>(
                    "Pivot");
        }

        return _presentation;
    }

    private void UpdatePresentation(
        Vector3 inputDirection,
        float deltaSeconds)
    {
        _presentation?.UpdateMotion(
            Velocity,
            inputDirection,
            JumpImpulse,
            CurrentMomentumTier,
            SpeedBoostVisualIntensity,
            _nitroSpool,
            _airStompChain,
            CurrentComboFlowTier,
            deltaSeconds);
    }

    private void UpdateMomentumFromMovement(Vector3 direction, bool onFloor, float deltaSeconds)
    {
        float momentumChange;
        if (IsArenaCombatActive)
        {
            float movementStrength = Mathf.Clamp(direction.Length(), 0.0f, 1.0f);
            momentumChange = movementStrength > 0.1f
                ? 0.0f
                : -IdleMomentumDecay *
                    ArenaIdleDecayMultiplier *
                    _runMomentumDecayMultiplier;
            SetMomentum(Momentum + momentumChange * deltaSeconds);
            return;
        }

        float forwardIntent = Mathf.Max(0.0f, -direction.Z);
        if (forwardIntent > 0.25f)
        {
            momentumChange = ForwardMomentumGain * forwardIntent;
        }
        else if (direction.IsZeroApprox())
        {
            momentumChange =
                -IdleMomentumDecay *
                _runMomentumDecayMultiplier;
        }
        else if (direction.Z > 0.1f)
        {
            momentumChange =
                -BackwardMomentumDecay *
                direction.Z *
                _runMomentumDecayMultiplier;
        }
        else
        {
            momentumChange =
                -IdleMomentumDecay * 0.45f *
                _runMomentumDecayMultiplier;
        }

        if (onFloor)
        {
            momentumChange -=
                GroundContactMomentumDecay *
                _runGroundMomentumDecayMultiplier *
                _runMomentumDecayMultiplier;
        }

        if (Velocity.Y < -4.0f)
        {
            float fallSeverity = Mathf.Clamp((-Velocity.Y - 4.0f) / 18.0f, 0.25f, 1.5f);
            momentumChange -=
                FallingMomentumDecay *
                fallSeverity *
                _runMomentumDecayMultiplier;
        }

        SetMomentum(Momentum + momentumChange * deltaSeconds);
    }

    private void ApplyHorizontalMovement(
        ref Vector3 velocity,
        ref Vector3 direction,
        float inputStrength,
        bool onFloor,
        float movementMultiplier,
        float deltaSeconds)
    {
        Vector2 currentVelocity = new(velocity.X, velocity.Z);

        if (!onFloor &&
            HasActiveFirstStompGuide)
        {
            // The intent corridor resolves air steering below. Applying raw
            // steering here as well made forward input erase the subtle
            // lateral correction on every frame. The guide still blends in
            // aligned input, and a deliberate turn away cancels it.
        }
        else if (!onFloor &&
            IsValidComboRouteTarget(_comboRouteTarget))
        {
            // Manual direction and its small aim correction are combined by
            // the route director below. Applying raw steering here as a
            // second pass would erase that correction every frame.
        }
        else
        {
            float desiredMovementSpeed =
                Mathf.Max(
                    1.0f,
                    (Speed + _runBaseSpeedBonus) *
                        movementMultiplier -
                        ActiveObstacleSlowAmount +
                        ActiveSpeedBoost);
            if (_nitroBurning)
            {
                float nitroThrust = Mathf.Pow(
                    Mathf.Clamp(
                        _nitroSpool,
                        0.0f,
                        1.0f),
                    0.72f);
                desiredMovementSpeed = Mathf.Min(
                    Mathf.Max(
                        desiredMovementSpeed,
                        NitroAbsoluteSpeedCap),
                    desiredMovementSpeed +
                        NitroMaximumSpeedBonus *
                        nitroThrust);
            }

            Vector2 desiredVelocity =
                new Vector2(direction.X, direction.Z) *
                desiredMovementSpeed *
                inputStrength;
            currentVelocity =
                PlayerMovementMotor
                    .MovePlanarVelocity(
                currentVelocity,
                desiredVelocity,
                !direction.IsZeroApprox(),
                onFloor,
                GroundAcceleration,
                GroundTurnAcceleration,
                GroundDeceleration,
                AirAcceleration,
                AirTurnAcceleration,
                AirDeceleration,
                _runAccelerationMultiplier,
                deltaSeconds);
        }

        ApplyNitroAcceleration(
            ref currentVelocity,
            ref direction,
            inputStrength,
            movementMultiplier,
            deltaSeconds);
        ApplyGroundBurst(
            ref currentVelocity,
            ref direction,
            deltaSeconds);
        velocity.X = currentVelocity.X;
        velocity.Z = currentVelocity.Y;
    }

    private void ApplyNitroAcceleration(
        ref Vector2 currentVelocity,
        ref Vector3 direction,
        float inputStrength,
        float movementMultiplier,
        float deltaSeconds)
    {
        if (_nitroSpool <= 0.001f)
        {
            return;
        }

        if (!direction.IsZeroApprox() &&
            NitroSteeringResponsiveness > 0.0f)
        {
            float steerWeight = 1.0f -
                Mathf.Exp(
                    -NitroSteeringResponsiveness *
                    deltaSeconds);
            _nitroDirection = _nitroDirection
                .Lerp(direction, steerWeight)
                .Normalized();
        }

        direction = _nitroDirection;
        float thrust = Mathf.Pow(
            Mathf.Clamp(_nitroSpool, 0.0f, 1.0f),
            0.72f);
        float baseSpeed =
            Mathf.Max(
                1.0f,
                (Speed + _runBaseSpeedBonus) *
                    movementMultiplier -
                    ActiveObstacleSlowAmount +
                    ActiveSpeedBoost);
        float poweredSpeed = Mathf.Min(
            baseSpeed +
                NitroMaximumSpeedBonus * thrust,
            Mathf.Max(baseSpeed, NitroAbsoluteSpeedCap));
        if (_nitroIgnitionKickPending)
        {
            float ignitionSpeed = Mathf.Min(
                Mathf.Max(baseSpeed, NitroAbsoluteSpeedCap),
                Mathf.Max(
                    currentVelocity.Length(),
                    baseSpeed) +
                    Mathf.Max(0.0f, NitroIgnitionSpeedKick) *
                    Mathf.Max(0.25f, _nitroThrottle));
            currentVelocity =
                new Vector2(direction.X, direction.Z) *
                ignitionSpeed;
            _nitroIgnitionKickPending = false;
        }

        float targetSpeed = Mathf.Max(
            currentVelocity.Length(),
            poweredSpeed);
        Vector2 targetVelocity =
            new Vector2(direction.X, direction.Z) *
            targetSpeed;
        currentVelocity = currentVelocity.MoveToward(
            targetVelocity,
            Mathf.Max(1.0f, NitroAcceleration) *
                thrust *
                deltaSeconds);
    }

    private void ApplyGroundBurst(
        ref Vector2 currentVelocity,
        ref Vector3 direction,
        float deltaSeconds)
    {
        if (_groundBurstTimer <= 0.0f ||
            _groundBurstHeading.IsZeroApprox())
        {
            return;
        }
        if (!IsOnWorldFloor())
        {
            // The impulse may be carried into a jump as momentum, but Rift
            // Step never becomes a second air dash.
            _groundBurstTimer = 0.0f;
            return;
        }

        float speed = Mathf.MoveToward(
            currentVelocity.Length(),
            Mathf.Max(
                currentVelocity.Length(),
                _groundBurstTargetSpeed),
            Mathf.Max(
                40.0f,
                GroundBurstAcceleration) *
                deltaSeconds);
        currentVelocity =
            _groundBurstHeading * speed;
        direction = new Vector3(
            _groundBurstHeading.X,
            0.0f,
            _groundBurstHeading.Y);
    }

   private void UpdateRiftStepAndNitro(
        Vector3 direction,
        float inputStrength,
        float deltaSeconds)
    {
        _abilityState.Tick(deltaSeconds);
        if (_riftStompWindowTimer <= 0.0f)
        {
            _lastRiftStompMob = null;
        }

        TryConsumeQueuedRiftStep(direction);

        float dashActionStrength = Mathf.Clamp(
            Input.GetActionStrength("dash"),
            0.0f,
            1.0f);
        bool actionPressed =
            dashActionStrength >= 0.42f;
        bool actionStarted =
            actionPressed &&
            !_nitroInputWasPressed;
        bool actionReleased =
            !actionPressed &&
            _nitroInputWasPressed;
        if (actionStarted)
        {
            _nitroActionUsesHold =
                !GamepadInput
                    .LastInputWasGamepad;
            _nitroActionStartedReactor =
                false;
            _nitroActionHoldTimer =
                0.0f;
            if (!_nitroActionUsesHold)
            {
                QueueRiftStep(direction);
            }
        }
        if (actionPressed &&
            _nitroActionUsesHold)
        {
            _nitroActionHoldTimer +=
                deltaSeconds;
            if (!_nitroActionStartedReactor &&
                _nitroActionHoldTimer >=
                    Mathf.Max(
                        0.12f,
                        KeyboardReactorHoldTime))
            {
                StartNitroOverdrive();
                _nitroActionStartedReactor =
                    _nitroLatched;
            }
        }
        if (actionReleased &&
            _nitroActionUsesHold)
        {
            if (_nitroActionStartedReactor)
            {
                StopNitroOverdrive();
            }
            else
            {
                QueueRiftStep(direction);
            }
            _nitroActionUsesHold = false;
            _nitroActionStartedReactor =
                false;
            _nitroActionHoldTimer =
                0.0f;
        }
        _nitroInputWasPressed =
            actionPressed;
        bool mobileRiftStep =
            _mobileControls
                ?.ConsumeRiftStep() ??
            false;
        bool mobileMaximum =
            _mobileControls
                ?.ConsumeNitroMaximum() ??
            false;
        bool mobileStop =
            _mobileControls
                ?.ConsumeNitroStop() ??
            false;

        if (mobileStop)
        {
            StopNitroOverdrive();
        }
        if (mobileMaximum)
        {
            StartNitroOverdrive();
        }
        if (mobileRiftStep)
        {
            QueueRiftStep(direction);
        }

        float analogThrottle = Mathf.Clamp(
            Input.GetActionStrength(
                "nitro_throttle"),
            0.0f,
            1.0f);
        float analogDeadZone = Mathf.Clamp(
            NitroAnalogDeadZone,
            0.0f,
            0.5f);
        analogThrottle =
            analogThrottle <= analogDeadZone
                ? 0.0f
                : Mathf.Clamp(
                    (analogThrottle -
                        analogDeadZone) /
                        Mathf.Max(
                            0.01f,
                            1.0f -
                                analogDeadZone),
                    0.0f,
                    1.0f);
        _nitroThrottle =
            _nitroLatched
                ? 1.0f
                : analogThrottle;
        bool hasFuel =
            NitroCharge > 0.001f &&
            (_nitroBurning ||
                NitroCharge >=
                    Mathf.Max(
                        0.0f,
                        NitroMinimumIgnitionCharge));
        _nitroBurning =
            _nitroThrottle >= 0.06f &&
            hasFuel;

        if (_nitroBurning)
        {
            _runNitroBurnTime += deltaSeconds;
            if (!_nitroWasBurning)
            {
                _nitroDirection =
                    direction.IsZeroApprox()
                        ? _lastMoveDirection
                        : direction;
                _nitroDirection =
                    _nitroDirection.Normalized();
                _nitroIgnitionKickPending =
                    true;
            }

            float drainWeight =
                _nitroThrottle *
                Mathf.Lerp(
                    0.72f,
                    1.0f,
                    _nitroThrottle);
            AddNitro(
                -Mathf.Max(
                    0.0f,
                    NitroDrainPerSecond) *
                _runNitroDrainMultiplier *
                drainWeight *
                deltaSeconds);
            float forwardQuality = Mathf.Clamp(
                -_nitroDirection.Z,
                0.2f,
                1.0f);
            AddMomentum(
                Mathf.Max(
                    0.0f,
                    NitroMomentumGainPerSecond) *
                forwardQuality *
                _nitroThrottle *
                deltaSeconds);
            if (GamepadInput
                    .LastInputWasGamepad &&
                _nitroRumbleTimer <= 0.0f)
            {
                float rumbleStrength =
                    Mathf.Pow(
                        _nitroThrottle,
                        0.82f);
                GamepadInput.Rumble(
                    Mathf.Lerp(
                        0.035f,
                        0.16f,
                        rumbleStrength),
                    Mathf.Lerp(
                        0.08f,
                        0.34f,
                        rumbleStrength),
                    0.13f);
                _nitroRumbleTimer =
                    Mathf.Lerp(
                        0.16f,
                        0.085f,
                        rumbleStrength);
            }
        }
        else
        {
            if (_nitroWasBurning &&
                _runNitroBurnTime >= 0.18f &&
                HasRunUpgrade(
                    RunUpgradeId.Afterburner))
            {
                bool evolved = HasRunUpgrade(
                    RunUpgradeId.BlueComet);
                ApplyTraversalSpeedBoost(
                    (evolved ? 4.4f : 3.0f) * (1 + .2f * (GetRunUpgradeRank(RunUpgradeId.Afterburner) - 1)),
                    (evolved ? 1.15f : 0.9f) + .15f * (GetRunUpgradeRank(RunUpgradeId.Afterburner) - 1));
                _afterburnerLinksRemaining =
                    evolved ? 3 : 0;
                EmitSignal(SignalName.RunBuffTriggered, (int)(evolved ? RunUpgradeId.BlueComet : RunUpgradeId.Afterburner));
            }
            _runNitroBurnTime = 0.0f;
            _nitroIgnitionKickPending =
                false;
            if (NitroCharge <= 0.001f)
            {
                _nitroLatched = false;
            }
        }

        float targetSpool =
            _nitroBurning
                ? _nitroThrottle
                : 0.0f;
        float spoolDuration =
            _nitroBurning
                ? Mathf.Max(
                    0.05f,
                    NitroSpoolUpTime)
                : Mathf.Max(
                    0.05f,
                    NitroSpoolDownTime);
        _nitroSpool = Mathf.MoveToward(
            _nitroSpool,
            targetSpool,
            deltaSeconds /
                spoolDuration);
        if (!Mathf.IsEqualApprox(
            _nitroSpool,
            targetSpool))
        {
            EmitNitroChanged();
        }
        _nitroWasBurning =
            _nitroBurning;
    }

    private void QueueRiftStep(
        Vector3 requestedDirection)
    {
        float inputBuffer = Mathf.Max(
            0.04f,
            RiftStepInputBufferTime);
        float minimumCost = Mathf.Min(
            GetEffectiveGroundBurstCost(),
            Mathf.Max(
                0.0f,
                RiftTeleportCost));
        if (_isDead ||
            IsInsideTunnel ||
            _nitroDashCooldownTimer >
                inputBuffer ||
            NitroCharge < minimumCost)
        {
            return;
        }

        Vector2 heading = new(
            requestedDirection.X,
            requestedDirection.Z);
        if (heading.IsZeroApprox())
        {
            heading = new Vector2(
                _lastMoveDirection.X,
                _lastMoveDirection.Z);
        }
        if (heading.IsZeroApprox())
        {
            heading = new Vector2(
                Velocity.X,
                Velocity.Z);
        }
        if (heading.IsZeroApprox())
        {
            heading = Vector2.Up;
        }

        _abilityState.QueueRift(
            heading.Normalized(),
            inputBuffer);
        TryConsumeQueuedRiftStep(
            requestedDirection);
    }

    private void TryConsumeQueuedRiftStep(
        Vector3 currentDirection)
    {
        if (!_riftRequestPending)
        {
            return;
        }

        if (_nitroDashCooldownTimer > 0.0f)
        {
            if (_riftRequestTimer <= 0.0f)
            {
                _abilityState.ClearRiftRequest();
            }
            return;
        }

        bool consumed = false;
        bool canTeleport =
            _riftStompWindowTimer > 0.0f ||
            !IsOnWorldFloor();
        if (canTeleport &&
            NitroCharge >=
                Mathf.Max(
                    0.0f,
                    RiftTeleportCost))
        {
            consumed =
                TryPerformRiftTeleport();
        }
        else if (IsOnWorldFloor() &&
            NitroCharge >=
                GetEffectiveGroundBurstCost())
        {
            Vector2 heading =
                _riftRequestedHeading;
            if (heading.IsZeroApprox())
            {
                heading = new Vector2(
                    currentDirection.X,
                    currentDirection.Z);
            }
            consumed =
                TryPerformGroundBurst(
                    heading);
        }

        if (consumed)
        {
            _abilityState.ClearRiftRequest();
            return;
        }

        if (_riftRequestTimer <= 0.0f)
        {
            _abilityState.ClearRiftRequest();
        }
    }

    private bool TryPerformGroundBurst(
        Vector2 heading)
    {
        if (_isDead ||
            IsInsideTunnel ||
            !IsOnWorldFloor() ||
            _nitroDashCooldownTimer > 0.0f ||
            NitroCharge <
                GetEffectiveGroundBurstCost())
        {
            return false;
        }

        if (heading.IsZeroApprox())
        {
            heading = Vector2.Up;
        }
        heading = heading.Normalized();
        ClearSlipstreamDodgeCue();

        Vector3 origin = GlobalPosition;
        Vector2 horizontalVelocity = new(
            Velocity.X,
            Velocity.Z);
        float currentSpeed =
            horizontalVelocity.Length();
        float desiredSpeed =
            Mathf.Max(
                currentSpeed,
                CurrentRunSpeed) +
            Mathf.Max(
                0.0f,
                GroundBurstSpeedGain);
        _groundBurstTargetSpeed =
            Mathf.Min(
                desiredSpeed,
                Mathf.Max(
                    currentSpeed,
                    GroundBurstSpeedCap));
        float ignitionSpeed =
            Mathf.Lerp(
                Mathf.Max(
                    currentSpeed,
                    CurrentRunSpeed),
                _groundBurstTargetSpeed,
                0.88f);
        Velocity = new Vector3(
            heading.X * ignitionSpeed,
            Velocity.Y,
            heading.Y * ignitionSpeed);
        if (HasRunUpgrade(RunUpgradeId.QuickSpark))
            EmitSignal(SignalName.RunBuffTriggered, (int)RunUpgradeId.QuickSpark);
        _groundBurstHeading = heading;
        _groundBurstTimer =
            Mathf.Max(
                0.06f,
                GroundBurstDuration);
        _slipstreamDodgeWindowTimer =
            Mathf.Max(
                _groundBurstTimer,
                GetEffectiveDodgeWindow());
        _nitroDashCooldownTimer =
            Mathf.Max(
                0.12f,
                GetEffectiveGroundBurstCooldown());
        AddNitro(
            -GetEffectiveGroundBurstCost());
        Vector3 destination =
            origin +
            new Vector3(
                heading.X,
                0.0f,
                heading.Y) *
            _groundBurstTargetSpeed *
            _groundBurstTimer;
        GamepadInput.Rumble(
            0.2f,
            0.7f,
            0.12f);
        Input.VibrateHandheld(
            58,
            0.48f);
        EmitSignal(
            SignalName.RiftStepStarted,
            false,
            origin,
            destination);
        return true;
    }

    private bool TryPerformRiftTeleport()
    {
        if (_isDead ||
            IsInsideTunnel ||
            (_riftStompWindowTimer <= 0.0f &&
                IsOnWorldFloor()) ||
            _nitroDashCooldownTimer > 0.0f ||
            NitroCharge <
                Mathf.Max(
                    0.0f,
                    RiftTeleportCost) ||
            !TrySelectRiftTarget(
                out Mob target,
                out Vector3 destination))
        {
            return false;
        }

        Vector3 origin = GlobalPosition;
        Vector3 incomingVelocity = Velocity;
        GlobalPosition = destination;
        Vector3 targetVelocity =
            target.Velocity;
        Vector2 arrivalVelocity = new(
            targetVelocity.X,
            targetVelocity.Z);
        if (HasRunUpgrade(RunUpgradeId.Overpass))
        {
            Vector2 incomingPlanar = new(
                incomingVelocity.X,
                incomingVelocity.Z);
            Vector2 arrivalHeading =
                incomingPlanar.IsZeroApprox()
                    ? arrivalVelocity.Normalized()
                    : incomingPlanar.Normalized();
            if (arrivalHeading.IsZeroApprox())
            {
                arrivalHeading = Vector2.Up;
            }
            float preservedSpeed = Mathf.Clamp(
                Mathf.Max(
                    incomingPlanar.Length(),
                    arrivalVelocity.Length()),
                CurrentRunSpeed * (.78f + .1f * (GetRunUpgradeRank(RunUpgradeId.Overpass) - 1)),
                NitroAbsoluteSpeedCap);
            arrivalVelocity =
                arrivalHeading * preservedSpeed;
        }
        Velocity = new Vector3(
            arrivalVelocity.X,
            -Mathf.Max(
                1.0f,
                RiftArrivalDiveSpeed),
            arrivalVelocity.Y);
        _playerJumpActive = false;
        _mobBounceVelocityRecoveryAvailable = false;
        _airborneStallTimer = 0.0f;
        _airborneBlockedMotionTimer = 0.0f;
        _airborneEscapeDistance = 0.0f;
        _mobBounceFloorIgnoreTimer =
            Mathf.Max(
                _mobBounceFloorIgnoreTimer,
                0.1f);
        _airJumpsRemaining =
            Mathf.Max(
                _airJumpsRemaining,
                1);
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        ClearComboRoute();
        _stompAssistTarget = target;
        _lastRiftTeleportTarget = target;
        _redThreadPending =
            HasRunUpgrade(RunUpgradeId.RedThread);
        _phaseRouteRefundPending =
            HasRunUpgrade(RunUpgradeId.PhaseRoute);
        _riftStompWindowTimer = 0.0f;
        _lastRiftStompMob = null;
        _riftTeleportFxTimer = 0.24f;
        _nitroDashCooldownTimer =
            Mathf.Max(
                0.12f,
                RiftStepCooldown);
        GrantDamageInvulnerability(
            RiftArrivalInvulnerability);
        AddNitro(
            -Mathf.Max(
                0.0f,
                RiftTeleportCost));
        target.SetJumpReachCue(
            Mob.JumpReachCue.Reachable,
            1.0f);
        GamepadInput.Rumble(
            0.26f,
            0.88f,
            0.16f);
        Input.VibrateHandheld(
            90,
            0.78f);
        EmitSignal(
            SignalName.RiftStepStarted,
            true,
            origin,
            destination);
        return true;
    }

    private bool TrySelectRiftTarget(
        out Mob selectedTarget,
        out Vector3 selectedDestination)
    {
        selectedTarget = null;
        selectedDestination =
            Vector3.Zero;
        Vector2 worldForward =
            Vector2.Up;
        float minimumDistance =
            Mathf.Max(
                0.5f,
                RiftTargetMinimumDistance);
        bool arenaRift =
            IsArenaCombatActive;
        float maximumDistance =
            Mathf.Max(
                minimumDistance + 0.5f,
                arenaRift
                    ? RiftArenaTargetMaximumDistance
                    : RiftTargetMaximumDistance);
        float forwardDot =
            Mathf.Clamp(
                RiftTargetForwardDot,
                -0.2f,
                0.8f);
        List<Mob> candidates = new();
        List<Vector3> destinations =
            new();

        foreach (Node node in
            GetTree().GetNodesInGroup(
                "mob"))
        {
            if (node is not Mob mob ||
                mob == _lastRiftStompMob ||
                !IsValidComboRouteTarget(
                    mob) ||
                (arenaRift &&
                    !mob.ArenaTargetEligible) ||
                mob.IsArenaAttackResolved ||
                mob.IsPostArenaRetreating ||
                mob.IsYieldingToTunnel)
            {
                continue;
            }

            Vector3 offset =
                mob.GlobalPosition -
                GlobalPosition;
            Vector2 horizontalOffset =
                new(
                    offset.X,
                    offset.Z);
            float distance =
                horizontalOffset.Length();
            if (distance <
                    minimumDistance ||
                distance >
                    maximumDistance ||
                Mathf.Abs(offset.Y) >
                    9.0f)
            {
                continue;
            }
            if (!arenaRift &&
                horizontalOffset
                    .Normalized()
                    .Dot(worldForward) <
                    forwardDot)
            {
                continue;
            }

            Vector3 predictedTarget =
                mob.GlobalPosition +
                mob.Velocity * 0.11f;
            Vector3 destination =
                predictedTarget +
                Vector3.Up *
                    Mathf.Max(
                        1.0f,
                        Mathf.Max(RiftArrivalHeight, mob.MinimumRiftArrivalHeight));
            if (!IsRiftPathSafe(
                destination))
            {
                continue;
            }

            candidates.Add(mob);
            destinations.Add(
                destination);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int selectedIndex =
            _riftRandom.RandiRange(
                0,
                candidates.Count - 1);
        selectedTarget =
            candidates[selectedIndex];
        selectedDestination =
            destinations[selectedIndex];
        return true;
    }

    private bool IsRiftPathSafe(
        Vector3 destination)
    {
        if (!IsInsideTree())
        {
            return true;
        }

        const uint worldAndObstacleMask =
            (1u << 2) |
            (1u << 3);
        PhysicsRayQueryParameters3D query =
            PhysicsRayQueryParameters3D.Create(
                GlobalPosition +
                    Vector3.Up * 0.45f,
                destination,
                worldAndObstacleMask);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude =
            new Godot.Collections.Array<Rid>
            {
                GetRid()
            };
        return GetWorld3D()
            .DirectSpaceState
            .IntersectRay(query)
            .Count == 0;
    }

    private void ResetRiftStepState()
    {
        _abilityState.ResetRift();
        _lastRiftStompMob = null;
        _lastRiftTeleportTarget = null;
        _redThreadPending = false;
        _phaseRouteRefundPending = false;
        ClearSlipstreamDodgeCue();
        ClearClutchVaultState();
    }

    private void StartNitroOverdrive()
    {
        _abilityState.TryStartOverdrive(
            NitroMinimumIgnitionCharge);
    }

    private void StopNitroOverdrive()
    {
        _abilityState.StopOverdrive();
    }

    private void ApplySurvivalAssist(
        ref Vector3 velocity,
        bool wasOnFloor,
        float deltaSeconds)
    {
        if (!SurvivalAssistEnabled ||
            !wasOnFloor ||
            IsNitroActive ||
            SurvivalAssistRadius <= 0.0f ||
            SurvivalAssistMaxCorrectionSpeed <= 0.0f)
        {
            _survivalAssistCorrection = Vector2.Zero;
            return;
        }

        Vector2 playerPosition = new(GlobalPosition.X, GlobalPosition.Z);
        Vector2 playerVelocity = new(velocity.X, velocity.Z);
        float playerSpeed = playerVelocity.Length();
        if (playerSpeed < 0.75f)
        {
            _survivalAssistCorrection = _survivalAssistCorrection.MoveToward(
                Vector2.Zero,
                SurvivalAssistResponsiveness * deltaSeconds);
            return;
        }

        Vector2 suggestedCorrection = Vector2.Zero;
        float totalUrgency = 0.0f;
        const float collisionClearance = 2.15f;
        const float emergencyClearance = 1.35f;

        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion() ||
                Mathf.Abs(mob.GlobalPosition.Y - GlobalPosition.Y) > 2.25f)
            {
                continue;
            }

            Vector2 mobPosition = new(mob.GlobalPosition.X, mob.GlobalPosition.Z);
            Vector2 separation = mobPosition - playerPosition;
            float currentDistance = separation.Length();
            if (currentDistance > SurvivalAssistRadius || currentDistance <= 0.001f)
            {
                continue;
            }

            Vector2 relativeVelocity =
                new Vector2(mob.Velocity.X, mob.Velocity.Z) - playerVelocity;
            float relativeSpeedSquared = relativeVelocity.LengthSquared();
            float closingSpeed =
                -separation.Normalized().Dot(relativeVelocity);
            float closestTime = 0.0f;
            if (relativeSpeedSquared > 0.01f)
            {
                closestTime = Mathf.Clamp(
                    -separation.Dot(relativeVelocity) / relativeSpeedSquared,
                    0.0f,
                    SurvivalAssistLookAheadTime);
            }

            bool emergency = currentDistance < emergencyClearance;
            if (!emergency &&
                (closingSpeed <= 0.1f || closestTime <= 0.015f))
            {
                continue;
            }

            Vector2 predictedSeparation =
                separation + relativeVelocity * closestTime;
            float predictedDistance = predictedSeparation.Length();
            float collisionUrgency =
                1.0f - Mathf.Clamp(
                    predictedDistance / collisionClearance,
                    0.0f,
                    1.0f);
            float timeUrgency = 1.0f - Mathf.Clamp(
                closestTime / Mathf.Max(0.05f, SurvivalAssistLookAheadTime),
                0.0f,
                1.0f);
            float urgency = collisionUrgency *
                Mathf.Lerp(0.35f, 1.0f, timeUrgency);
            if (emergency)
            {
                urgency = Mathf.Max(
                    urgency,
                    1.0f - currentDistance / emergencyClearance);
            }

            if (urgency <= 0.0f)
            {
                continue;
            }

            Vector2 threatDirection = predictedDistance > 0.05f
                ? predictedSeparation.Normalized()
                : separation.Normalized();
            Vector2 side = new(-threatDirection.Y, threatDirection.X);
            float sidePreference = playerVelocity.Dot(side);
            if (Mathf.Abs(sidePreference) <= 0.2f)
            {
                sidePreference = mob.GetInstanceId() % 2UL == 0UL
                    ? 1.0f
                    : -1.0f;
            }

            Vector2 escapeHeading = side * Mathf.Sign(sidePreference);
            if (emergency)
            {
                escapeHeading = escapeHeading
                    .Lerp(-threatDirection, 0.35f)
                    .Normalized();
            }

            Vector2 desiredHeading = playerVelocity.Normalized()
                .Lerp(
                    escapeHeading,
                    Mathf.Lerp(0.22f, 0.42f, urgency))
                .Normalized();
            Vector2 correction =
                desiredHeading * playerSpeed -
                playerVelocity;
            correction = correction.LimitLength(
                SurvivalAssistMaxCorrectionSpeed * urgency);
            suggestedCorrection += correction * urgency;
            totalUrgency += urgency;
        }

        Vector2 targetCorrection = Vector2.Zero;
        if (!suggestedCorrection.IsZeroApprox() && totalUrgency > 0.0f)
        {
            targetCorrection =
                (suggestedCorrection / totalUrgency).LimitLength(
                    SurvivalAssistMaxCorrectionSpeed *
                    Mathf.Clamp(totalUrgency, 0.0f, 1.0f));

            // Never pull against the player's chosen pace. The assist may
            // bend a route, but it cannot brake or reverse it.
            Vector2 forward = playerVelocity / playerSpeed;
            float backwardComponent = targetCorrection.Dot(forward);
            if (backwardComponent < 0.0f)
            {
                targetCorrection -= forward * backwardComponent;
            }
        }

        float steerWeight =
            1.0f - Mathf.Exp(-SurvivalAssistResponsiveness * deltaSeconds);
        _survivalAssistCorrection =
            _survivalAssistCorrection.Lerp(targetCorrection, steerWeight);
        if (_survivalAssistCorrection.LengthSquared() < 0.0001f)
        {
            _survivalAssistCorrection = Vector2.Zero;
        }

        Vector2 assistedVelocity = playerVelocity + _survivalAssistCorrection;
        if (!assistedVelocity.IsZeroApprox())
        {
            assistedVelocity = assistedVelocity.Normalized() * playerSpeed;
        }

        velocity.X = assistedVelocity.X;
        velocity.Z = assistedVelocity.Y;
    }

    public void SetFirstStompGuideCandidate(
        Mob candidate,
        bool reachable)
    {
        if (HasActiveFirstStompGuide)
        {
            return;
        }

        bool canStillLaunch =
            IsOnWorldFloor() ||
            _coyoteTimer > 0.0f;
        if (!StompAssistEnabled ||
            !reachable ||
            !canStillLaunch ||
            !IsValidComboRouteTarget(candidate) ||
            IsInsideTunnel)
        {
            ClearFirstStompGuideCandidate();
            return;
        }

        _firstStompGuideCandidate = candidate;
        _firstStompGuideCandidateTimer =
            Mathf.Max(
                0.05f,
                FirstStompGuideCandidateMemory);
    }

    public bool IsFirstStompGuideActive()
    {
        return HasActiveFirstStompGuide;
    }

    private void UpdateFirstStompGuideCandidateTimer(
        float deltaSeconds)
    {
        _firstStompGuideCandidateTimer = Mathf.Max(
            0.0f,
            _firstStompGuideCandidateTimer - deltaSeconds);
        if (_firstStompGuideCandidateTimer <= 0.0f ||
            !IsValidComboRouteTarget(
                _firstStompGuideCandidate))
        {
            ClearFirstStompGuideCandidate();
        }
    }

    private void BeginFirstStompGuide(
        Vector3 horizontalVelocity,
        float launchVerticalSpeed,
        Vector3 movementIntent)
    {
        Vector3 intendedHeading = new(
            movementIntent.X,
            0.0f,
            movementIntent.Z);
        float inputStrength = intendedHeading.Length();
        if (!StompAssistEnabled ||
            IsInsideTunnel ||
            IsNitroActive)
        {
            ClearFirstStompGuideCandidate();
            return;
        }
        float minimumInputStrength =
            Mathf.Clamp(
                FirstStompGuideMinimumInputStrength,
                0.1f,
                0.8f);
        if (inputStrength <
            minimumInputStrength)
        {
            if (StompAssistLevel !=
                    AssistLevel.Strong)
            {
                ClearFirstStompGuideCandidate();
                return;
            }

            intendedHeading =
                horizontalVelocity;
            intendedHeading.Y = 0.0f;
            inputStrength =
                intendedHeading.Length();
            if (inputStrength <
                CurrentRunSpeed * 0.28f)
            {
                ClearFirstStompGuideCandidate();
                return;
            }
        }
        intendedHeading /=
            inputStrength;

        Mob guideTarget =
            _firstStompGuideCandidateTimer > 0.0f &&
            IsValidComboRouteTarget(_firstStompGuideCandidate)
                ? _firstStompGuideCandidate
                : FindFirstStompIntentTarget(
                    horizontalVelocity,
                    launchVerticalSpeed,
                    intendedHeading);
        if (!IsValidComboRouteTarget(guideTarget))
        {
            ClearFirstStompGuideCandidate();
            return;
        }

        Vector3 targetOffset =
            guideTarget.GlobalPosition -
            GlobalPosition;
        targetOffset.Y = 0.0f;
        float targetDistance = targetOffset.Length();
        if (targetDistance <
                FirstStompGuideMinimumDistance ||
            targetDistance >
                Mathf.Max(
                    FirstStompGuideMinimumDistance,
                    FirstStompIntentMaximumDistance) ||
            targetDistance <= 0.05f)
        {
            ClearFirstStompGuideCandidate();
            return;
        }

        Vector3 targetHeading =
            targetOffset / targetDistance;
        float acquisitionAngle = Mathf.DegToRad(
            Mathf.Clamp(
                FirstStompIntentAcquisitionAngle,
                5.0f,
                40.0f));
        if (intendedHeading.Dot(targetHeading) <
            Mathf.Cos(acquisitionAngle))
        {
            ClearFirstStompGuideCandidate();
            return;
        }

        Vector3 side =
            Vector3.Up.Cross(targetHeading).Normalized();
        float lateralSpeed =
            horizontalVelocity.Dot(side);
        _guidedFirstStompLandingOffset =
            side *
            Mathf.Clamp(
                lateralSpeed * 0.04f,
                -FirstStompGuideLandingPocket,
                FirstStompGuideLandingPocket);
        _guidedFirstStompTarget = guideTarget;
        _guidedFirstStompHeading = intendedHeading;
        _firstStompGuideGraceTimer =
            FirstStompGuideInputGraceTime;
        _firstStompGuideLaunchSpeed = Mathf.Max(
            1.0f,
            horizontalVelocity.Length());
        _firstStompGuideLaunchVerticalSpeed =
            Mathf.Max(1.0f, launchVerticalSpeed);
        ClearFirstStompGuideCandidate();
        GamepadInput.Rumble(0.035f, 0.075f, 0.04f);
    }

    private Mob FindFirstStompIntentTarget(
        Vector3 horizontalVelocity,
        float launchVerticalSpeed,
        Vector3 launchHeading)
    {
        float horizontalSpeed =
            horizontalVelocity.Length();
        if (horizontalSpeed <
            CurrentRunSpeed *
                Mathf.Clamp(
                    FirstStompIntentMinimumSpeedRatio,
                    0.1f,
                    0.8f))
        {
            return null;
        }

        launchHeading.Y = 0.0f;
        if (launchHeading.IsZeroApprox())
        {
            return null;
        }

        launchHeading = launchHeading.Normalized();
        float acquisitionAngle = Mathf.DegToRad(
            Mathf.Clamp(
                FirstStompIntentAcquisitionAngle,
                5.0f,
                40.0f));
        float minimumAlignment =
            Mathf.Cos(acquisitionAngle);
        float maximumDistance = Mathf.Max(
            FirstStompGuideMinimumDistance,
            FirstStompIntentMaximumDistance);
        float referenceSpeed = horizontalSpeed;
        float speedAdjustment = Mathf.Max(
            0.0f,
            FirstStompGuideMaxSpeedAdjustment);
        float minimumSpeed = Mathf.Max(
            2.0f,
            referenceSpeed -
                speedAdjustment * 0.72f);
        float maximumSpeed =
            referenceSpeed + speedAdjustment;
        Mob bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Node node in
            GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob candidate ||
                !IsValidComboRouteTarget(candidate) ||
                !TryGetComboContactTime(
                    GlobalPosition.Y,
                    candidate.GlobalPosition.Y,
                    launchVerticalSpeed,
                    out float contactTime))
            {
                continue;
            }

            Vector3 predictedTarget =
                candidate.GlobalPosition +
                candidate.Velocity * contactTime;
            Vector3 targetOffset =
                predictedTarget - GlobalPosition;
            targetOffset.Y = 0.0f;
            float targetDistance =
                targetOffset.Length();
            if (targetDistance <
                    FirstStompGuideMinimumDistance ||
                targetDistance > maximumDistance ||
                targetDistance <= 0.05f)
            {
                continue;
            }

            Vector3 targetHeading =
                targetOffset / targetDistance;
            float alignment =
                launchHeading.Dot(targetHeading);
            if (alignment < minimumAlignment)
            {
                continue;
            }

            float requiredSpeed =
                targetDistance /
                Mathf.Max(0.08f, contactTime);
            float landingWindowSpeed =
                Mathf.Max(
                    0.65f,
                    StompAssistRadius /
                        Mathf.Max(0.08f, contactTime) *
                        0.32f);
            if (requiredSpeed <
                    minimumSpeed - landingWindowSpeed ||
                requiredSpeed >
                    maximumSpeed + landingWindowSpeed)
            {
                continue;
            }

            Vector3 targetContact =
                predictedTarget +
                Vector3.Up * 0.75f;
            if (IsComboArcBlocked(
                GlobalPosition,
                targetContact,
                launchVerticalSpeed,
                contactTime))
            {
                continue;
            }

            float angleCost =
                1.0f - alignment;
            float speedCost =
                Mathf.Abs(
                    requiredSpeed - referenceSpeed) /
                Mathf.Max(1.0f, referenceSpeed);
            float score =
                angleCost * 7.0f +
                speedCost * 0.7f +
                targetDistance * 0.006f -
                candidate.JumpOpportunityPriority *
                    0.7f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private void ApplyFirstStompGuide(
        ref Vector3 velocity,
        Vector3 movementIntent,
        float deltaSeconds)
    {
        if (!HasActiveFirstStompGuide)
        {
            ClearFirstStompGuide();
            return;
        }

        if (!_playerJumpActive ||
            IsInsideTunnel ||
            IsNitroActive)
        {
            CancelFirstStompGuide();
            return;
        }

        float handoffSpeed = Mathf.Max(
            1.0f,
            FirstStompGuideDescentHandoffSpeed);

        _firstStompGuideGraceTimer = Mathf.Max(
            0.0f,
            _firstStompGuideGraceTimer - deltaSeconds);
        if (!TryGetComboContactTime(
            GlobalPosition.Y,
            _guidedFirstStompTarget.GlobalPosition.Y,
            velocity.Y,
            out float contactTime))
        {
            CancelFirstStompGuide();
            return;
        }

        Vector3 predictedTarget =
            _guidedFirstStompTarget.GlobalPosition +
            _guidedFirstStompTarget.Velocity *
                contactTime +
            _guidedFirstStompLandingOffset;
        Vector3 targetOffset =
            predictedTarget - GlobalPosition;
        targetOffset.Y = 0.0f;
        float targetDistance = targetOffset.Length();
        if (targetDistance <= 0.05f)
        {
            return;
        }

        float landingCaptureDistance = Mathf.Max(
            2.4f,
            StompAssistRadius * 1.65f);
        if (velocity.Y <= -handoffSpeed &&
            targetDistance <= landingCaptureDistance)
        {
            Mob landingTarget = _guidedFirstStompTarget;
            ClearFirstStompGuide();
            if (IsValidComboRouteTarget(landingTarget))
            {
                _stompAssistTarget = landingTarget;
            }

            return;
        }

        Vector3 targetHeading =
            targetOffset / targetDistance;
        Vector3 horizontalIntent =
            new(
                movementIntent.X,
                0.0f,
                movementIntent.Z);
        float inputStrength = horizontalIntent.Length();
        float minimumInputStrength = Mathf.Clamp(
            FirstStompGuideMinimumInputStrength,
            0.1f,
            0.8f);
        bool hasActiveIntent =
            inputStrength >= minimumInputStrength;
        if (!hasActiveIntent &&
            _firstStompGuideGraceTimer <= 0.0f)
        {
            CancelFirstStompGuide();
            return;
        }

        Vector3 inputHeading = hasActiveIntent
            ? horizontalIntent / inputStrength
            : new Vector3(
                velocity.X,
                0.0f,
                velocity.Z).Normalized();
        if (inputHeading.IsZeroApprox())
        {
            inputHeading = _guidedFirstStompHeading;
        }

        if (hasActiveIntent &&
            inputHeading.Dot(targetHeading) <
                FirstStompGuideCancelDotThreshold)
        {
            CancelFirstStompGuide();
            return;
        }

        Vector3 targetContact =
            predictedTarget +
            Vector3.Up *
                Mathf.Clamp(
                    ComboStompContactHeight,
                    0.8f,
                    1.5f);
        if (IsComboArcBlocked(
            GlobalPosition,
            targetContact,
            velocity.Y,
            contactTime))
        {
            CancelFirstStompGuide();
            return;
        }

        Vector2 inputDirection = new(
            inputHeading.X,
            inputHeading.Z);
        Vector2 limitedTargetDirection = ClampDirectionToCone(
            new Vector2(targetHeading.X, targetHeading.Z),
            inputDirection,
            Mathf.DegToRad(
                FirstStompGuideMaxTurnAngle));
        float correctionShare = hasActiveIntent
            ? Mathf.Clamp(
                FirstStompGuideCorrectionShare,
                0.0f,
                0.8f)
            : 0.0f;
        Vector2 guidedDirection = inputDirection
            .Lerp(
                limitedTargetDirection,
                correctionShare)
            .Normalized();

        float requestedSpeed =
            targetDistance /
            Mathf.Max(0.08f, contactTime);
        float speedAdjustment = Mathf.Max(
            0.0f,
            FirstStompGuideMaxSpeedAdjustment);
        float minimumSpeed = Mathf.Max(
            0.0f,
            _firstStompGuideLaunchSpeed -
                speedAdjustment * 0.72f);
        float maximumSpeed =
            _firstStompGuideLaunchSpeed +
            speedAdjustment;
        float guidedSpeed = Mathf.Clamp(
            requestedSpeed,
            minimumSpeed,
            maximumSpeed);
        Vector2 horizontalVelocity =
            new(velocity.X, velocity.Z);
        Vector2 desiredVelocity =
            guidedDirection * guidedSpeed;

        float correctionStrength;
        if (velocity.Y >= 0.0f)
        {
            float ascentProgress =
                1.0f -
                Mathf.Clamp(
                    velocity.Y /
                        _firstStompGuideLaunchVerticalSpeed,
                    0.0f,
                    1.0f);
            float naturalEnvelope =
                ascentProgress *
                ascentProgress *
                (3.0f - 2.0f * ascentProgress);
            correctionStrength =
                Mathf.Lerp(0.48f, 1.0f, naturalEnvelope);
        }
        else
        {
            float descentProgress = Mathf.Clamp(
                -velocity.Y / handoffSpeed,
                0.0f,
                1.0f);
            correctionStrength = Mathf.Lerp(
                1.0f,
                0.62f,
                descentProgress);
        }

        horizontalVelocity = horizontalVelocity.MoveToward(
            desiredVelocity,
            FirstStompGuideAcceleration *
                correctionStrength *
                deltaSeconds);
        velocity.X = horizontalVelocity.X;
        velocity.Z = horizontalVelocity.Y;
    }

    private void CancelFirstStompGuide()
    {
        _stompAssistSuppressedUntilLanding = true;
        ClearFirstStompGuide();
    }

    private void ClearFirstStompGuide()
    {
        _guidedFirstStompTarget = null;
        _guidedFirstStompHeading = Vector3.Zero;
        _guidedFirstStompLandingOffset = Vector3.Zero;
        _firstStompGuideGraceTimer = 0.0f;
        _firstStompGuideLaunchSpeed = 0.0f;
        _firstStompGuideLaunchVerticalSpeed = 0.0f;
    }

    private void ClearFirstStompGuideCandidate()
    {
        _firstStompGuideCandidate = null;
        _firstStompGuideCandidateTimer = 0.0f;
    }

    private void UpdateClutchVaultState(
        bool wasOnFloor,
        Vector3 movementIntent,
        float deltaSeconds)
    {
        _clutchVaultCueGraceTimer =
            Mathf.Max(
                0.0f,
                _clutchVaultCueGraceTimer -
                    deltaSeconds);
        _clutchVaultActiveTimer =
            Mathf.Max(
                0.0f,
                _clutchVaultActiveTimer -
                    deltaSeconds);
        _clutchVaultContactGraceTimer =
            Mathf.Max(
                0.0f,
                _clutchVaultContactGraceTimer -
                    deltaSeconds);
        if (!IsValidComboRouteTarget(
                _clutchVaultActiveTarget))
        {
            _clutchVaultActiveTarget =
                null;
            _clutchVaultActiveTimer =
                0.0f;
            _clutchVaultContactGraceTimer =
                0.0f;
        }

        if (_isDead ||
            IsInsideTunnel ||
            IsRiftTeleportActive ||
            IsGroundBurstActive ||
            IsNitroActive ||
            IsClutchVaultActive ||
            (!wasOnFloor &&
                _airJumpsRemaining <= 0))
        {
            ClearClutchVaultCue();
            return;
        }

        Vector3 heading =
            movementIntent;
        heading.Y = 0.0f;
        if (heading.IsZeroApprox())
        {
            heading =
                new Vector3(
                    Velocity.X,
                    0.0f,
                    Velocity.Z);
        }
        if (heading.IsZeroApprox())
        {
            heading =
                _lastMoveDirection;
            heading.Y = 0.0f;
        }
        if (heading.IsZeroApprox())
        {
            heading =
                Vector3.Forward;
        }
        heading = heading.Normalized();

        Mob bestTarget = null;
        float bestScore =
            float.MaxValue;
        float bestDistance = 0.0f;
        float radius =
            Mathf.Max(
                1.5f,
                ClutchVaultRadius);
        foreach (Node node in
            GetTree().GetNodesInGroup(
                "mob"))
        {
            if (node is not Mob mob ||
                !IsValidComboRouteTarget(mob) ||
                mob.IsArenaAttackResolved ||
                mob.IsPostArenaRetreating ||
                mob.IsYieldingToTunnel)
            {
                continue;
            }

            Vector3 separation =
                mob.GlobalPosition -
                GlobalPosition;
            float height =
                GlobalPosition.Y -
                mob.GlobalPosition.Y;
            separation.Y = 0.0f;
            float distance =
                separation.Length();
            if (distance < 0.45f ||
                distance > radius ||
                height < -0.5f ||
                height > 1.45f ||
                (!wasOnFloor &&
                    Velocity.Y < -2.0f &&
                    height > 0.55f))
            {
                continue;
            }

            Vector3 targetDirection =
                separation /
                Mathf.Max(
                    0.001f,
                    distance);
            float facing =
                targetDirection.Dot(
                    heading);
            if (facing < -0.24f)
            {
                continue;
            }
            if (!TryGetClutchVaultDestination(
                mob,
                out _))
            {
                continue;
            }

            float score =
                distance +
                (1.0f - facing) *
                    0.72f +
                Mathf.Abs(height) *
                    0.28f;
            if (score >= bestScore)
            {
                continue;
            }
            bestScore = score;
            bestTarget = mob;
            bestDistance = distance;
        }

        if (IsValidComboRouteTarget(
                bestTarget))
        {
            ClutchVaultCueTarget =
                bestTarget;
            _clutchVaultCueGraceTimer =
                Mathf.Max(
                    0.05f,
                    ClutchVaultInputGrace);
            ClutchVaultCueUrgency =
                1.0f -
                Mathf.Clamp(
                    (bestDistance -
                        0.8f) /
                        Mathf.Max(
                            0.5f,
                            radius -
                                0.8f),
                    0.0f,
                    1.0f);
            return;
        }

        if (_clutchVaultCueGraceTimer >
                0.0f &&
            IsValidComboRouteTarget(
                ClutchVaultCueTarget))
        {
            Vector3 separation =
                ClutchVaultCueTarget
                    .GlobalPosition -
                GlobalPosition;
            float height =
                Mathf.Abs(
                    separation.Y);
            separation.Y = 0.0f;
            if (separation.Length() <=
                    radius + 0.7f &&
                height <= 1.8f &&
                TryGetClutchVaultDestination(
                    ClutchVaultCueTarget,
                    out _))
            {
                ClutchVaultCueUrgency =
                    Mathf.Max(
                        0.35f,
                        ClutchVaultCueUrgency -
                            deltaSeconds * 1.8f);
                return;
            }
        }

        ClearClutchVaultCue();
    }

    private bool TryGetClutchVaultDestination(
        Mob target,
        out Vector3 destination)
    {
        destination =
            Vector3.Zero;
        if (!IsValidComboRouteTarget(
            target))
        {
            return false;
        }

        Vector3 predictedTarget =
            target.GlobalPosition +
            target.Velocity * 0.08f;
        destination =
            predictedTarget +
            Vector3.Up *
                Mathf.Max(
                    1.5f,
                    ClutchVaultArrivalHeight);
        return IsRiftPathSafe(
            destination);
    }

    private bool TryStartClutchVault(
        bool wasOnFloor,
        ref Vector3 velocity)
    {
        Mob target =
            ClutchVaultCueTarget;
        if (_isDead ||
            IsInsideTunnel ||
            IsNitroActive ||
            !IsValidComboRouteTarget(
                target) ||
            _clutchVaultCueGraceTimer <=
                0.0f ||
            (!wasOnFloor &&
                _airJumpsRemaining <= 0))
        {
            return false;
        }

        Vector3 separation =
            target.GlobalPosition -
            GlobalPosition;
        float height =
            GlobalPosition.Y -
            target.GlobalPosition.Y;
        separation.Y = 0.0f;
        float distance =
            separation.Length();
        if (distance < 0.35f ||
            distance >
                Mathf.Max(
                    1.5f,
                    ClutchVaultRadius) +
                    0.75f ||
            height < -0.65f ||
            height > 1.8f ||
            !TryGetClutchVaultDestination(
                target,
                out Vector3 destination))
        {
            return false;
        }

        Vector3 origin =
            GlobalPosition;
        GlobalPosition =
            destination;
        velocity =
            new Vector3(
                target.Velocity.X,
                -Mathf.Max(
                    1.0f,
                    ClutchVaultArrivalDiveSpeed),
                target.Velocity.Z);
        if (!wasOnFloor)
        {
            _airJumpsRemaining =
                Mathf.Max(
                    0,
                    _airJumpsRemaining - 1);
        }
        _playerJumpActive = false;
        _jumpBufferTimer = 0.0f;
        _coyoteTimer = 0.0f;
        _airborneStallTimer = 0.0f;
        _airborneBlockedMotionTimer = 0.0f;
        _airborneEscapeDistance = 0.0f;
        _mobBounceVelocityRecoveryAvailable =
            false;
        _mobBounceFloorIgnoreTimer =
            Mathf.Max(
                _mobBounceFloorIgnoreTimer,
                0.1f);
        _clutchVaultActiveTarget =
            target;
        _clutchVaultActiveTimer = 0.34f;
        _clutchVaultContactGraceTimer =
            Mathf.Max(
                0.12f,
                ClutchVaultContactGrace);
        _stompAssistSuppressedUntilLanding =
            false;
        _stompAssistTarget = target;
        ClearFirstStompGuide();
        ClearFirstStompGuideCandidate();
        ClearComboRoute();
        ClearClutchVaultCue();
        GamepadInput.Rumble(
            0.18f,
            0.62f,
            0.12f);
        Input.VibrateHandheld(
            68,
            0.58f);
        EmitSignal(
            SignalName.ClutchVaultStarted,
            origin,
            destination);
        return true;
    }

    private void ClearClutchVaultCue()
    {
        ClutchVaultCueTarget = null;
        ClutchVaultCueUrgency = 0.0f;
        _clutchVaultCueGraceTimer = 0.0f;
    }

    private void ClearClutchVaultState()
    {
        ClearClutchVaultCue();
        _clutchVaultActiveTarget = null;
        _clutchVaultActiveTimer = 0.0f;
        _clutchVaultContactGraceTimer = 0.0f;
    }

    private void ApplyStompAssist(ref Vector3 velocity, bool wasOnFloor, float deltaSeconds)
    {
        if (!StompAssistEnabled ||
            _stompAssistSuppressedUntilLanding ||
            wasOnFloor ||
            velocity.Y >= -0.5f ||
            IsNitroActive)
        {
            _stompAssistTarget = null;
            return;
        }

        if (!IsValidStompAssistTarget(_stompAssistTarget))
        {
            _stompAssistTarget = FindStompAssistTarget();
        }

        if (_stompAssistTarget == null)
        {
            return;
        }

        Vector2 horizontalVelocity = new(velocity.X, velocity.Z);
        if (!TryGetStompLandingError(
            _stompAssistTarget,
            horizontalVelocity,
            out Vector2 landingError,
            out float landingTime))
        {
            _stompAssistTarget = null;
            return;
        }

        Vector2 correctionVelocity =
            (landingError / Mathf.Max(0.06f, landingTime))
            .LimitLength(StompAssistMaxCorrectionSpeed);
        float currentSpeed = horizontalVelocity.Length();
        if (currentSpeed > 1.0f)
        {
            Vector2 forward = horizontalVelocity / currentSpeed;
            float backwardComponent = correctionVelocity.Dot(forward);
            if (backwardComponent < 0.0f)
            {
                // The landing funnel is lateral only: it cannot cancel the
                // route the player committed to.
                correctionVelocity -= forward * backwardComponent;
            }
        }

        float heightAboveTarget =
            GlobalPosition.Y - _stompAssistTarget.GlobalPosition.Y;
        float lateWeight = 1.0f - Mathf.Clamp(
            heightAboveTarget / StompAssistMaxHeight,
            0.0f,
            1.0f);
        float nearMissWeight = 1.0f - Mathf.Clamp(
            landingError.Length() / StompAssistRadius,
            0.0f,
            1.0f);
        correctionVelocity *=
            Mathf.Lerp(0.35f, 1.0f, lateWeight) *
            Mathf.Lerp(0.45f, 1.0f, nearMissWeight);
        horizontalVelocity = horizontalVelocity.MoveToward(
            horizontalVelocity + correctionVelocity,
            StompAssistStrength * deltaSeconds);
        velocity.X = horizontalVelocity.X;
        velocity.Z = horizontalVelocity.Y;
    }

    private bool ApplyComboRouteDirector(
        ref Vector3 velocity,
        Vector3 movementIntent,
        bool landedOnGround,
        float deltaSeconds)
    {
        if (!StompAssistEnabled ||
            IsInsideTunnel ||
            !IsValidComboRouteTarget(_comboRouteTarget) ||
            landedOnGround)
        {
            ClearComboRoute();
            return false;
        }

        _comboRouteGraceTimer = Mathf.Max(
            0.0f,
            _comboRouteGraceTimer - deltaSeconds);
        Vector2 horizontalIntent = new(
            movementIntent.X,
            movementIntent.Z);
        float inputStrength = horizontalIntent.Length();
        float minimumInputStrength = Mathf.Clamp(
            ComboRouteMinimumInputStrength,
            0.1f,
            0.8f);
        bool hasActiveIntent =
            inputStrength >= minimumInputStrength;

        if (IsNitroActive)
        {
            if (!hasActiveIntent &&
                _comboRouteGraceTimer <= 0.0f)
            {
                CancelComboRouteFromInput();
                return false;
            }

            Vector2 dashVelocity = new(
                velocity.X,
                velocity.Z);
            _comboRouteCruiseSpeed = Mathf.Max(
                _comboRouteCruiseSpeed,
                dashVelocity.Length());
            // Nitro remains player-steered. The director keeps only its
            // limited range correction so the added pace can still land on
            // the target the player already chose.
        }

        if (!hasActiveIntent)
        {
            if (_comboRouteGraceTimer <= 0.0f)
            {
                CancelComboRouteFromInput();
                return false;
            }

            // A tiny release buffer prevents controller noise from dropping
            // a route, but never steers or accelerates without player input.
            return true;
        }

        if (!TryGetComboContactTime(
            GlobalPosition.Y,
            _comboRouteTarget.GlobalPosition.Y,
            velocity.Y,
            out float interceptTime))
        {
            ClearComboRoute();
            return false;
        }

        Vector3 predictedTarget =
            _comboRouteTarget.GlobalPosition +
            _comboRouteTarget.Velocity * interceptTime;
        Vector2 targetOffset = new(
            predictedTarget.X - GlobalPosition.X,
            predictedTarget.Z - GlobalPosition.Z);
        float targetDistance = targetOffset.Length();
        if (targetDistance <= 0.05f ||
            targetDistance >
                GetAirComboRouteMaxDistance(_airStompChain))
        {
            ClearComboRoute();
            return false;
        }

        Vector2 targetDirection = targetOffset / targetDistance;
        Vector2 inputDirection =
            horizontalIntent / inputStrength;
        if (inputDirection.Dot(targetDirection) <
                ComboRouteCancelDotThreshold)
        {
            CancelComboRouteFromInput();
            return false;
        }

        Vector2 limitedTargetDirection = ClampDirectionToCone(
            targetDirection,
            inputDirection,
            Mathf.DegToRad(ComboRouteMaxTurnAngle));
        float analogAssistWeight =
            StompAssistLevel switch
            {
                AssistLevel.Strong =>
                    Mathf.Lerp(
                        0.78f,
                        1.0f,
                        inputStrength),
                AssistLevel.Balanced =>
                    Mathf.Lerp(
                        0.58f,
                        1.0f,
                        inputStrength),
                _ =>
                    inputStrength
            };
        float assistanceWeight =
            Mathf.Clamp(
                ComboRouteAssistInfluence,
                0.0f,
                0.8f) *
            Mathf.Clamp(
                analogAssistWeight,
                0.0f,
                1.0f);
        Vector2 directedDirection = inputDirection
            .Lerp(
                limitedTargetDirection,
                assistanceWeight)
            .Normalized();

        GetComboSpeedEnvelope(
            _comboRouteCruiseSpeed,
            targetDistance,
            _airStompChain,
            out float minimumSpeed,
            out float maximumSpeed);
        float requiredSpeed =
            targetDistance / Mathf.Max(0.06f, interceptTime);
        float landingWindowSpeed =
            Mathf.Max(
                0.65f,
                StompAssistRadius /
                    interceptTime *
                    0.4f);
        if (requiredSpeed < minimumSpeed - landingWindowSpeed ||
            requiredSpeed > maximumSpeed + landingWindowSpeed)
        {
            ClearComboRoute();
            return false;
        }

        float speedAllowance =
            GetComboRangeAdjustmentAllowance(
                _airStompChain);
        float directedMinimum =
            Mathf.Max(
                minimumSpeed,
                _comboRouteCruiseSpeed -
                    speedAllowance * 1.5f);
        float directedMaximum =
            Mathf.Min(
                maximumSpeed,
                _comboRouteCruiseSpeed +
                    speedAllowance);
        // Nitro thrust can temporarily sit above the normal route envelope.
        // Never feed an inverted range to Clamp; the speed-protection pass
        // below preserves earned pace while the route envelope catches up.
        directedMinimum =
            Mathf.Min(
                directedMinimum,
                directedMaximum);
        float directedSpeed = Mathf.Clamp(
            requiredSpeed,
            directedMinimum,
            directedMaximum);
        UpdateComboRouteForecast(
            predictedTarget,
            new Vector3(
                directedDirection.X,
                0.0f,
                directedDirection.Y),
            interceptTime,
            directedSpeed,
            deltaSeconds);
        Vector2 horizontalVelocity = new(
            velocity.X,
            velocity.Z);
        if (horizontalVelocity.IsZeroApprox())
        {
            ClearComboRoute();
            return false;
        }

        Vector2 directedVelocity =
            directedDirection * directedSpeed;
        float protectedSpeed =
            horizontalVelocity.Length();
        float responseWeight =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(1.0f, ComboRouteResponsiveness) *
                deltaSeconds);
        horizontalVelocity = horizontalVelocity.Lerp(
            directedVelocity,
            responseWeight);
        if (_nitroDashSpeedProtectionTimer >
                0.0f &&
            horizontalVelocity.Length() <
                protectedSpeed)
        {
            horizontalVelocity =
                horizontalVelocity
                    .IsZeroApprox()
                    ? new Vector2(
                        velocity.X,
                        velocity.Z)
                    : horizontalVelocity
                        .Normalized() *
                        protectedSpeed;
        }
        velocity.X = horizontalVelocity.X;
        velocity.Z = horizontalVelocity.Y;
        return true;
    }

    private float BeginComboRoute(
        Mob stompedMob,
        float baseBounceVelocity,
        Vector3 movementIntent)
    {
        _stompAssistSuppressedUntilLanding = false;
        ClearComboRoute();
        Vector3 intendedHeading = new(
            movementIntent.X,
            0.0f,
            movementIntent.Z);
        float inputStrength = intendedHeading.Length();
        if (!StompAssistEnabled ||
            IsInsideTunnel ||
            !IsInstanceValid(stompedMob))
        {
            return baseBounceVelocity;
        }

        Vector3 horizontalVelocity =
            new(Velocity.X, 0.0f, Velocity.Z);
        float minimumInputStrength =
            Mathf.Clamp(
                ComboRouteMinimumInputStrength,
                0.1f,
                0.8f);
        if (inputStrength <
            minimumInputStrength)
        {
            if (StompAssistLevel !=
                    AssistLevel.Strong ||
                horizontalVelocity.Length() <
                    CurrentRunSpeed * 0.28f)
            {
                return baseBounceVelocity;
            }

            intendedHeading =
                horizontalVelocity;
            inputStrength =
                intendedHeading.Length();
        }
        intendedHeading /=
            inputStrength;

        float referenceSpeed = Mathf.Clamp(
            horizontalVelocity.IsZeroApprox()
                ? 4.0f
                : horizontalVelocity.Length(),
            4.0f,
            ComboRouteAbsoluteMaxSpeed);

        if (!TryChooseComboRoute(
            stompedMob,
            intendedHeading,
            referenceSpeed,
            baseBounceVelocity,
            out Mob target,
            out Vector3 routeHeading,
            out float directedBounceVelocity,
            out float requiredSpeed,
            out int plannedTargets))
        {
            return baseBounceVelocity;
        }

        _comboRouteTarget = target;
        _comboRouteCruiseSpeed = referenceSpeed;
        _comboRouteGraceTimer = ComboRouteInputGraceTime;
        _comboRouteReplanTimer =
            Mathf.Max(0.05f, ComboRouteReplanInterval);
        ComboRoutePlannedTargets = plannedTargets;

        bool arcAdjusted =
            Mathf.Abs(
                directedBounceVelocity /
                    Mathf.Max(0.1f, baseBounceVelocity) -
                1.0f) > 0.025f ||
            Mathf.Abs(
                requiredSpeed /
                    Mathf.Max(0.1f, referenceSpeed) -
                1.0f) > 0.1f;
        LastComboArcScale =
            directedBounceVelocity /
            Mathf.Max(0.1f, baseBounceVelocity);
        EmitSignal(
            SignalName.ComboRouteLocked,
            plannedTargets,
            arcAdjusted);
        GamepadInput.Rumble(0.055f, 0.12f, 0.045f);
        return directedBounceVelocity;
    }

    private bool TryChooseComboRoute(
        Mob stompedMob,
        Vector3 launchHeading,
        float referenceSpeed,
        float baseBounceVelocity,
        out Mob selectedTarget,
        out Vector3 selectedHeading,
        out float selectedBounceVelocity,
        out float selectedSpeed,
        out int plannedTargets)
    {
        selectedTarget = null;
        selectedHeading = Vector3.Zero;
        selectedBounceVelocity = baseBounceVelocity;
        selectedSpeed = referenceSpeed;
        plannedTargets = 0;
        float bestScore = float.MaxValue;
        float arcAuthority = Mathf.Lerp(
            0.68f,
            1.0f,
            GetAirComboReachProgress(
                _airStompChain));
        float minimumBounceScale = Mathf.Lerp(
            1.0f,
            Mathf.Min(
                ComboRouteMinBounceScale,
                ComboRouteMaxBounceScale),
            arcAuthority);
        float maximumBounceScale = Mathf.Lerp(
            1.0f,
            Mathf.Max(
                ComboRouteMinBounceScale,
                ComboRouteMaxBounceScale),
            arcAuthority);

        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob candidate ||
                candidate == stompedMob ||
                !IsValidComboRouteTarget(candidate))
            {
                continue;
            }

            float preferredBounceScale =
                GetTargetMatchedBounceScale(
                    candidate,
                    referenceSpeed,
                    baseBounceVelocity,
                    minimumBounceScale,
                    maximumBounceScale);
            float[] bounceScales =
            {
                preferredBounceScale,
                1.0f,
                minimumBounceScale,
                Mathf.Lerp(
                    minimumBounceScale,
                    preferredBounceScale,
                    0.5f),
                Mathf.Lerp(
                    preferredBounceScale,
                    maximumBounceScale,
                    0.5f),
                maximumBounceScale
            };
            foreach (float bounceScale in bounceScales)
            {
                float bounceVelocity =
                    baseBounceVelocity * bounceScale;
                if (!TryEvaluateComboCandidate(
                    candidate,
                    GlobalPosition,
                    launchHeading,
                    0.0f,
                    bounceVelocity,
                    referenceSpeed,
                    _airStompChain,
                    true,
                    out Vector3 predictedTarget,
                    out Vector3 routeHeading,
                    out float requiredSpeed,
                    out float interceptTime))
                {
                    continue;
                }

                float alignment = Mathf.Clamp(
                    launchHeading.Dot(routeHeading),
                    -1.0f,
                    1.0f);
                Vector3 predictedOffset =
                    predictedTarget - GlobalPosition;
                predictedOffset.Y = 0.0f;
                float predictedDistance =
                    predictedOffset.Length();
                float bounceScaleCost =
                    Mathf.Abs(
                        bounceScale -
                        preferredBounceScale) *
                    (predictedDistance <
                        ComboRouteCloseArcDistance
                            ? 2.6f
                            : 1.4f);
                float score =
                    (1.0f - alignment) * 3.4f +
                    Mathf.Abs(
                        requiredSpeed - referenceSpeed) /
                        Mathf.Max(1.0f, referenceSpeed) *
                        0.72f +
                    bounceScaleCost +
                    predictedDistance * 0.004f -
                    candidate.JumpOpportunityPriority * 1.15f;

                if (candidate.IsComboRouteMember)
                {
                    score -= 0.58f;
                }

                if (stompedMob.IsColumnMember &&
                    candidate.ColumnId == stompedMob.ColumnId &&
                    candidate.ColumnIndex > stompedMob.ColumnIndex)
                {
                    int indexGap =
                        candidate.ColumnIndex -
                        stompedMob.ColumnIndex;
                    score -= indexGap == 1
                        ? 1.35f
                        : Mathf.Max(0.25f, 0.92f - indexGap * 0.14f);
                }

                if (stompedMob.EncounterId >= 0 &&
                    candidate.EncounterId == stompedMob.EncounterId)
                {
                    score -= 0.32f;
                }

                HashSet<ulong> visited = new()
                {
                    stompedMob.GetInstanceId(),
                    candidate.GetInstanceId()
                };
                int continuationCount =
                    EstimateComboContinuations(
                        candidate,
                        predictedTarget,
                        routeHeading,
                        interceptTime,
                        GetAirComboBounceVelocityForChain(
                            _airStompChain + 1),
                        requiredSpeed,
                        _airStompChain + 1,
                        visited);
                score -= continuationCount * 0.45f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                selectedTarget = candidate;
                selectedHeading = routeHeading;
                selectedBounceVelocity = bounceVelocity;
                selectedSpeed = requiredSpeed;
                plannedTargets = 1 + continuationCount;
            }
        }

        return IsValidComboRouteTarget(selectedTarget);
    }

    private int EstimateComboContinuations(
        Mob routeOrigin,
        Vector3 predictedOrigin,
        Vector3 routeHeading,
        float elapsedTime,
        float bounceVelocity,
        float referenceSpeed,
        int chainCount,
        HashSet<ulong> visited)
    {
        const int beamWidth = 6;
        Godot.Collections.Array<Node> activeMobs =
            GetTree().GetNodesInGroup("mob");
        List<ComboRouteProjection> frontier = new()
        {
            new ComboRouteProjection
            {
                Origin = routeOrigin,
                Position = predictedOrigin,
                Heading = routeHeading,
                ElapsedTime = elapsedTime,
                BounceVelocity = bounceVelocity,
                ReferenceSpeed = referenceSpeed,
                RouteCost = 0.0f,
                ChainCount = chainCount,
                Visited = new HashSet<ulong>(visited)
            }
        };
        int longestContinuation = 0;

        // Depth is deliberately bounded only by the enemies that currently
        // exist. The beam keeps the search finite without imposing a fixed
        // number of kills, and visited IDs prevent cycles.
        for (int step = 0;
            step < activeMobs.Count;
            step++)
        {
            List<ComboRouteProjection> nextFrontier = new();
            foreach (ComboRouteProjection projection in frontier)
            {
                Vector3 playerOrigin =
                    projection.Position + Vector3.Up * 0.75f;
                foreach (Node node in activeMobs)
                {
                    if (node is not Mob candidate ||
                        !IsValidComboRouteTarget(candidate) ||
                        projection.Visited.Contains(
                            candidate.GetInstanceId()) ||
                        !TryEvaluateComboCandidate(
                            candidate,
                            playerOrigin,
                            projection.Heading,
                            projection.ElapsedTime,
                            projection.BounceVelocity,
                            projection.ReferenceSpeed,
                            projection.ChainCount,
                            false,
                            out Vector3 predictedTarget,
                            out Vector3 nextHeading,
                            out float requiredSpeed,
                            out float interceptTime))
                    {
                        continue;
                    }

                    bool followsAuthoredRoute =
                        projection.Origin.IsColumnMember &&
                        candidate.ColumnId ==
                            projection.Origin.ColumnId &&
                        candidate.ColumnIndex >
                            projection.Origin.ColumnIndex;
                    float alignment =
                        Mathf.Clamp(
                            projection.Heading.Dot(nextHeading),
                            -1.0f,
                            1.0f);
                    if (!followsAuthoredRoute &&
                        alignment <
                            Mathf.Cos(
                                Mathf.DegToRad(
                                    ComboRouteSearchAngle * 0.72f)))
                    {
                        continue;
                    }

                    float edgeCost =
                        (1.0f - alignment) * 2.2f +
                        Mathf.Abs(
                            requiredSpeed -
                            projection.ReferenceSpeed) /
                            Mathf.Max(
                                1.0f,
                                projection.ReferenceSpeed) *
                            0.55f -
                        candidate.JumpOpportunityPriority * 0.45f;
                    if (candidate.IsComboRouteMember)
                    {
                        edgeCost -= 0.28f;
                    }

                    if (followsAuthoredRoute)
                    {
                        int indexGap =
                            candidate.ColumnIndex -
                            projection.Origin.ColumnIndex;
                        edgeCost -= indexGap == 1
                            ? 0.9f
                            : 0.42f;
                    }

                    HashSet<ulong> nextVisited =
                        new(projection.Visited)
                        {
                            candidate.GetInstanceId()
                        };
                    int nextChainCount =
                        projection.ChainCount + 1;
                    nextFrontier.Add(
                        new ComboRouteProjection
                        {
                            Origin = candidate,
                            Position = predictedTarget,
                            Heading = nextHeading,
                            ElapsedTime =
                                projection.ElapsedTime +
                                interceptTime,
                            BounceVelocity =
                                GetAirComboBounceVelocityForChain(
                                    nextChainCount),
                            ReferenceSpeed = requiredSpeed,
                            RouteCost =
                                projection.RouteCost +
                                edgeCost,
                            ChainCount = nextChainCount,
                            Visited = nextVisited
                        });
                }
            }

            if (nextFrontier.Count == 0)
            {
                break;
            }

            nextFrontier.Sort(
                (left, right) =>
                    left.RouteCost.CompareTo(right.RouteCost));
            if (nextFrontier.Count > beamWidth)
            {
                nextFrontier.RemoveRange(
                    beamWidth,
                    nextFrontier.Count - beamWidth);
            }

            frontier = nextFrontier;
            longestContinuation = step + 1;
        }

        return longestContinuation;
    }

    private void UpdateComboRouteForecast(
        Vector3 predictedTarget,
        Vector3 routeHeading,
        float interceptTime,
        float referenceSpeed,
        float deltaSeconds)
    {
        _comboRouteReplanTimer -= deltaSeconds;
        if (_comboRouteReplanTimer > 0.0f ||
            !IsValidComboRouteTarget(_comboRouteTarget))
        {
            return;
        }

        _comboRouteReplanTimer =
            Mathf.Max(0.05f, ComboRouteReplanInterval);
        HashSet<ulong> visited = new()
        {
            _comboRouteTarget.GetInstanceId()
        };
        int continuationCount =
            EstimateComboContinuations(
                _comboRouteTarget,
                predictedTarget,
                routeHeading,
                interceptTime,
                GetAirComboBounceVelocityForChain(
                    _airStompChain + 1),
                referenceSpeed,
                _airStompChain + 1,
                visited);
        int updatedPlan = 1 + continuationCount;
        if (updatedPlan == ComboRoutePlannedTargets)
        {
            return;
        }

        ComboRoutePlannedTargets = updatedPlan;
        EmitSignal(
            SignalName.ComboRouteForecastChanged,
            ComboRoutePlannedTargets);
    }

    private bool TryEvaluateComboCandidate(
        Mob candidate,
        Vector3 playerOrigin,
        Vector3 routeHeading,
        float elapsedTime,
        float bounceVelocity,
        float referenceSpeed,
        int chainCount,
        bool checkObstacles,
        out Vector3 predictedTarget,
        out Vector3 candidateHeading,
        out float requiredSpeed,
        out float interceptTime)
    {
        predictedTarget = Vector3.Zero;
        candidateHeading = Vector3.Zero;
        requiredSpeed = 0.0f;
        interceptTime = 0.0f;
        if (!IsValidComboRouteTarget(candidate) ||
            !TryGetComboContactTime(
                playerOrigin.Y,
                candidate.GlobalPosition.Y,
                bounceVelocity,
                out interceptTime))
        {
            return false;
        }

        predictedTarget =
            candidate.GlobalPosition +
            candidate.Velocity *
                (elapsedTime + interceptTime);
        Vector3 targetOffset =
            predictedTarget - playerOrigin;
        targetOffset.Y = 0.0f;
        float targetDistance = targetOffset.Length();
        if (targetDistance <
                Mathf.Max(
                    1.0f,
                    ComboRouteMinimumAssistDistance) ||
            targetDistance >
                GetAirComboRouteMaxDistance(chainCount))
        {
            return false;
        }

        candidateHeading = targetOffset / targetDistance;
        Vector3 safeRouteHeading = routeHeading;
        safeRouteHeading.Y = 0.0f;
        if (!safeRouteHeading.IsZeroApprox() &&
            safeRouteHeading.Normalized().Dot(candidateHeading) <
                Mathf.Cos(
                    Mathf.DegToRad(ComboRouteSearchAngle)))
        {
            return false;
        }

        GetComboSpeedEnvelope(
            referenceSpeed,
            targetDistance,
            chainCount,
            out float minimumSpeed,
            out float maximumSpeed);
        requiredSpeed =
            targetDistance / Mathf.Max(0.06f, interceptTime);
        float landingWindowSpeed =
            Mathf.Max(
                0.65f,
                StompAssistRadius /
                    interceptTime *
                    0.4f);
        if (requiredSpeed < minimumSpeed - landingWindowSpeed ||
            requiredSpeed > maximumSpeed + landingWindowSpeed)
        {
            return false;
        }

        Vector3 targetContact =
            predictedTarget + Vector3.Up * 0.75f;
        return !checkObstacles ||
            !IsComboArcBlocked(
                playerOrigin,
                targetContact,
                bounceVelocity,
                interceptTime);
    }

    private bool TryGetComboContactTime(
        float playerHeight,
        float targetHeight,
        float verticalVelocity,
        out float contactTime)
    {
        float stompContactHeight =
            Mathf.Clamp(
                ComboStompContactHeight,
                0.8f,
                1.5f);
        float gravity = Mathf.Max(1.0f, FallAcceleration);
        float heightOffset =
            playerHeight -
            targetHeight -
            stompContactHeight;
        float discriminant =
            verticalVelocity * verticalVelocity +
            2.0f * gravity * heightOffset;
        if (discriminant < 0.0f)
        {
            contactTime = 0.0f;
            return false;
        }

        contactTime =
            (verticalVelocity +
                Mathf.Sqrt(discriminant)) /
            gravity;
        return contactTime >= 0.07f &&
            contactTime <= 1.15f;
    }

    private void GetComboSpeedEnvelope(
        float referenceSpeed,
        float targetDistance,
        int chainCount,
        out float minimumSpeed,
        out float maximumSpeed)
    {
        float reachProgress =
            GetAirComboReachProgress(chainCount);
        float effectiveAbsoluteSpeedCap =
            Mathf.Lerp(
                ComboRouteAbsoluteMaxSpeed,
                Mathf.Max(
                    ComboRouteAbsoluteMaxSpeed,
                    AirComboAbsoluteSpeedCap),
                reachProgress);
        float safeReference = Mathf.Clamp(
            referenceSpeed,
            4.0f,
            effectiveAbsoluteSpeedCap);
        minimumSpeed = Mathf.Max(
            2.8f,
            safeReference *
                Mathf.Clamp(
                    ComboRouteMinSpeedScale,
                    0.2f,
                    0.9f) *
                GetCloseTargetSpeedFloor(targetDistance));
        float configuredScaleCeiling =
            safeReference *
            Mathf.Max(1.0f, ComboRouteMaxSpeedScale);
        float assistanceCeiling =
            safeReference +
            GetComboRangeAdjustmentAllowance(
                chainCount);
        maximumSpeed = Mathf.Min(
            effectiveAbsoluteSpeedCap,
            Mathf.Min(
                configuredScaleCeiling,
                assistanceCeiling));
    }

    private float GetComboRangeAdjustmentAllowance(
        int chainCount)
    {
        float baseAllowance = Mathf.Max(
            0.0f,
            ComboRouteSpeedAssistAllowance);
        float earnedAllowance = Mathf.Max(
            baseAllowance,
            ComboRouteMaximumRangeAdjustment +
                _runComboRangeAdjustmentBonus +
                (HasRunUpgrade(
                    RunUpgradeId.HunterLens)
                    ? GetRunUpgradeRank(RunUpgradeId.HunterLens) * 1.0f
                    : 0.0f) +
                (HasRunUpgrade(
                    RunUpgradeId.TwoHorizons)
                    ? 1.5f
                    : 0.0f));
        return Mathf.Lerp(
            baseAllowance,
            earnedAllowance,
            GetAirComboReachProgress(chainCount));
    }

    private float GetAirComboReachProgress(int chainCount)
    {
        int linksToMaximum =
            Mathf.Max(1, AirComboLinksToMaximum - 1);
        float linearProgress =
            Mathf.Clamp(
                (chainCount - 1) /
                    (float)linksToMaximum,
                0.0f,
                1.0f);
        // The first extension should be readable, while later links settle
        // gently into the cap instead of causing a sudden late-series spike.
        float chainProgress = 1.0f -
            (1.0f - linearProgress) *
            (1.0f - linearProgress);
        float accessibilityFactor = Mathf.Lerp(
            0.68f,
            1.0f,
            GetComboEffortFactor());
        return chainProgress * accessibilityFactor;
    }

    private void ApplyAirComboPaceReward()
    {
        float targetBonus =
            Mathf.Max(
                0.0f,
                AirComboMaximumSpeedBonus) *
            GetAirComboReachProgress(_airStompChain);
        float bonusDelta = Mathf.Max(
            0.0f,
            targetBonus -
                _airComboAppliedSpeedBonus);
        _airComboAppliedSpeedBonus = Mathf.Max(
            _airComboAppliedSpeedBonus,
            targetBonus);
        if (bonusDelta <= 0.001f)
        {
            return;
        }

        Vector2 horizontalVelocity =
            new(Velocity.X, Velocity.Z);
        float horizontalSpeed =
            horizontalVelocity.Length();
        if (horizontalSpeed <= 0.5f)
        {
            return;
        }

        float rewardedSpeed = Mathf.Max(
            horizontalSpeed,
            Mathf.Min(
                Mathf.Max(
                    CurrentRunSpeed,
                    AirComboAbsoluteSpeedCap),
                horizontalSpeed + bonusDelta));
        horizontalVelocity =
            horizontalVelocity / horizontalSpeed *
            rewardedSpeed;
        Velocity = new Vector3(
            horizontalVelocity.X,
            Velocity.Y,
            horizontalVelocity.Y);
    }

    private float GetComboEffortFactor()
    {
        float momentumFactor = CurrentMomentumTier switch
        {
            MomentumTier.Medium => 0.72f,
            MomentumTier.Rush => 1.0f,
            _ => 0.38f
        };
        float horizontalSpeed =
            new Vector2(Velocity.X, Velocity.Z).Length();
        float paceStart = CurrentRunSpeed * 1.05f;
        float nitroPace = Mathf.Max(
            paceStart + 0.1f,
            NitroAbsoluteSpeedCap);
        float earnedPaceFactor = Mathf.Clamp(
            (horizontalSpeed - paceStart) /
                (nitroPace - paceStart),
            0.0f,
            1.0f);
        return Mathf.Max(
            momentumFactor,
            earnedPaceFactor);
    }

    private float GetAirComboBounceMultiplier(int chainCount)
    {
        return 1.0f +
            Mathf.Max(0.0f, AirComboMaximumBounceBonus) *
                GetAirComboReachProgress(chainCount);
    }

    private float GetAirComboBounceVelocityForChain(int chainCount)
    {
        return ProjectedBounceLaunchSpeed *
            GetAirComboBounceMultiplier(chainCount);
    }

    private float GetAirComboRouteMaxDistance(int chainCount)
    {
        return ComboRouteMaxDistance +
            _runComboDistanceBonus +
            (HasRunUpgrade(
                RunUpgradeId.HunterLens)
                ? GetRunUpgradeRank(RunUpgradeId.HunterLens) * 2.5f
                : 0.0f) +
            (HasRunUpgrade(
                RunUpgradeId.TwoHorizons)
                ? 2.5f
                : 0.0f) +
            Mathf.Max(0.0f, AirComboMaximumDistanceBonus) *
                GetAirComboReachProgress(chainCount);
    }

    private float GetTargetMatchedBounceScale(
        Mob target,
        float referenceSpeed,
        float baseBounceVelocity,
        float minimumBounceScale,
        float maximumBounceScale)
    {
        if (!IsValidComboRouteTarget(target))
        {
            return 1.0f;
        }

        float safeSpeed = Mathf.Max(
            4.0f,
            referenceSpeed);
        Vector3 targetOffset =
            target.GlobalPosition - GlobalPosition;
        targetOffset.Y = 0.0f;
        float contactTime = Mathf.Clamp(
            targetOffset.Length() / safeSpeed,
            0.12f,
            1.15f);
        for (int iteration = 0; iteration < 2; iteration++)
        {
            Vector3 predictedTarget =
                target.GlobalPosition +
                target.Velocity * contactTime;
            Vector3 predictedOffset =
                predictedTarget - GlobalPosition;
            predictedOffset.Y = 0.0f;
            contactTime = Mathf.Clamp(
                predictedOffset.Length() / safeSpeed,
                0.12f,
                1.15f);
        }

        float heightOffset =
            GlobalPosition.Y -
            target.GlobalPosition.Y -
            0.75f;
        float requiredVerticalVelocity =
            (0.5f *
                Mathf.Max(1.0f, FallAcceleration) *
                contactTime *
                contactTime -
                heightOffset) /
            contactTime;
        float requiredScale =
            requiredVerticalVelocity /
            Mathf.Max(1.0f, baseBounceVelocity);
        return Mathf.Clamp(
            requiredScale,
            Mathf.Min(
                minimumBounceScale,
                maximumBounceScale),
            Mathf.Max(
                minimumBounceScale,
                maximumBounceScale));
    }

    private float GetCloseTargetSpeedFloor(
        float targetDistance)
    {
        return Mathf.Lerp(
            Mathf.Clamp(
                ComboRouteCloseSpeedFloorScale,
                0.4f,
                1.0f),
            1.0f,
            GetCloseTargetProgress(targetDistance));
    }

    private float GetCloseTargetProgress(
        float targetDistance)
    {
        float minimumDistance = Mathf.Max(
            1.0f,
            ComboRouteMinimumAssistDistance);
        float closeDistance = Mathf.Max(
            minimumDistance + 0.1f,
            ComboRouteCloseArcDistance);
        float progress = Mathf.Clamp(
            (targetDistance - minimumDistance) /
                (closeDistance - minimumDistance),
            0.0f,
            1.0f);
        // Smoothstep prevents a visible change in bounce height when a moving
        // target crosses either edge of the close-combo window.
        return progress * progress * (3.0f - 2.0f * progress);
    }

    private bool IsComboArcBlocked(
        Vector3 origin,
        Vector3 target,
        float verticalVelocity,
        float flightTime)
    {
        if (!IsInsideTree() || flightTime <= 0.0f)
        {
            return false;
        }

        const uint obstacleMask = 1u << 3;
        const int sampleCount = 7;
        float gravity = Mathf.Max(1.0f, FallAcceleration);
        Vector3 previousPoint = origin + Vector3.Up * 0.06f;
        for (int sample = 1; sample <= sampleCount; sample++)
        {
            float ratio = sample / (float)sampleCount;
            float sampleTime = flightTime * ratio;
            Vector3 point = origin.Lerp(target, ratio);
            point.Y =
                origin.Y +
                verticalVelocity * sampleTime -
                0.5f * gravity * sampleTime * sampleTime;
            PhysicsRayQueryParameters3D query =
                PhysicsRayQueryParameters3D.Create(
                    previousPoint,
                    point,
                    obstacleMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid>
            {
                GetRid()
            };
            if (GetWorld3D()
                .DirectSpaceState
                .IntersectRay(query)
                .Count > 0)
            {
                return true;
            }

            previousPoint = point;
        }

        return false;
    }

    private static Vector2 ClampDirectionToCone(
        Vector2 direction,
        Vector2 coneCenter,
        float maximumAngle)
    {
        if (direction.IsZeroApprox() ||
            coneCenter.IsZeroApprox())
        {
            return direction.Normalized();
        }

        direction = direction.Normalized();
        coneCenter = coneCenter.Normalized();
        float angle = coneCenter.AngleTo(direction);
        if (Mathf.Abs(angle) <= maximumAngle)
        {
            return direction;
        }

        return coneCenter
            .Rotated(
                Mathf.Clamp(
                    angle,
                    -maximumAngle,
                    maximumAngle))
            .Normalized();
    }

    private static bool IsValidComboRouteTarget(Mob mob)
    {
        return mob != null &&
            IsInstanceValid(mob) &&
            !mob.IsQueuedForDeletion() &&
            mob.CanBeRouteTarget;
    }

    private void CancelComboRouteFromInput()
    {
        _stompAssistSuppressedUntilLanding = true;
        ClearComboRoute();
    }

    private void ClearComboRoute()
    {
        _comboRouteTarget = null;
        _comboRouteCruiseSpeed = 0.0f;
        _comboRouteGraceTimer = 0.0f;
        _comboRouteReplanTimer = 0.0f;
        ComboRoutePlannedTargets = 0;
    }

    private Mob FindStompAssistTarget()
    {
        Mob bestTarget = null;
        float bestLandingError = StompAssistRadius;
        Vector2 horizontalVelocity = new(Velocity.X, Velocity.Z);

        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !TryGetStompLandingError(
                    mob,
                    horizontalVelocity,
                    out Vector2 landingError,
                    out _))
            {
                continue;
            }

            float missDistance = landingError.Length();
            if (missDistance < bestLandingError)
            {
                bestLandingError = missDistance;
                bestTarget = mob;
            }
        }

        return bestTarget;
    }

    private bool IsValidStompAssistTarget(Mob mob)
    {
        return TryGetStompLandingError(
            mob,
            new Vector2(Velocity.X, Velocity.Z),
            out _,
            out _);
    }

    private bool TryGetStompLandingError(
        Mob mob,
        Vector2 horizontalVelocity,
        out Vector2 landingError,
        out float landingTime)
    {
        landingError = Vector2.Zero;
        landingTime = 0.0f;
        if (mob == null || !IsInstanceValid(mob) || mob.IsQueuedForDeletion())
        {
            return false;
        }

        float heightAboveTarget = GlobalPosition.Y - mob.GlobalPosition.Y;
        if (heightAboveTarget < 0.25f ||
            heightAboveTarget > StompAssistMaxHeight)
        {
            return false;
        }

        float downwardSpeed = Mathf.Max(-Velocity.Y, 1.0f);
        landingTime = Mathf.Clamp(
            heightAboveTarget / downwardSpeed,
            0.06f,
            0.38f);
        Vector2 targetPosition = new(
            mob.GlobalPosition.X + mob.Velocity.X * landingTime,
            mob.GlobalPosition.Z + mob.Velocity.Z * landingTime);
        Vector2 predictedPlayerPosition =
            new Vector2(GlobalPosition.X, GlobalPosition.Z) +
            horizontalVelocity * landingTime;
        landingError = targetPosition - predictedPlayerPosition;
        return landingError.LengthSquared() <=
            StompAssistRadius * StompAssistRadius;
    }

    private void SetMomentum(float value)
    {
        float clampedMomentum = Mathf.Clamp(value, 0.0f, MaxMomentum);
        MomentumTier newTier = clampedMomentum >= RushMomentumThreshold
            ? MomentumTier.Rush
            : clampedMomentum >= MediumMomentumThreshold
                ? MomentumTier.Medium
                : MomentumTier.Low;

        if (Mathf.IsEqualApprox(clampedMomentum, Momentum) && newTier == CurrentMomentumTier)
        {
            return;
        }

        Momentum = clampedMomentum;
        CurrentMomentumTier = newTier;
        EmitSignal(SignalName.MomentumChanged, Momentum, (int)CurrentMomentumTier);
    }

    private void SetNitroCharge(float value)
    {
        if (!_abilityState.SetNitroCharge(
                value,
                NitroCapacity))
        {
            return;
        }

        EmitNitroChanged();
    }

    private void EmitNitroChanged()
    {
        EmitSignal(
            SignalName.NitroChanged,
            NitroCharge,
            Mathf.Max(0.0f, NitroCapacity),
            IsNitroActive);
    }

    private float GetEffectiveComboGroundGraceTime()
    {
        return Mathf.Clamp(
            ComboGroundGraceTime +
                _runComboGroundGraceBonus,
            0.12f,
            0.72f);
    }

    private float GetEffectiveFinisherThreshold()
    {
        return Mathf.Max(
            54.0f,
            ComboFinisherThreshold -
                _runFinisherThresholdReduction);
    }

    private float GetEffectiveGroundBurstCost()
    {
        return Mathf.Max(
            3.5f,
            GroundBurstCost -
                _runGroundBurstCostReduction);
    }

    private float GetEffectiveGroundBurstCooldown()
    {
        return Mathf.Max(
            0.14f,
            GroundBurstCooldown -
                _runGroundBurstCooldownReduction);
    }

    private float GetEffectiveDodgeWindow()
    {
        return Mathf.Clamp(
            SlipstreamDodgeInputWindow +
                _runDodgeWindowBonus,
            0.25f,
            0.85f);
    }

    private float GetMovementMultiplier()
    {
        return CurrentMomentumTier switch
        {
            MomentumTier.Medium => MediumSpeedMultiplier,
            MomentumTier.Rush => RushSpeedMultiplier,
            _ => 1.0f
        };
    }

    private float GetJumpMultiplier()
    {
        return CurrentMomentumTier switch
        {
            MomentumTier.Medium => MediumJumpMultiplier,
            MomentumTier.Rush => RushJumpMultiplier,
            _ => 1.0f
        };
    }

    private float GetBounceMultiplier()
    {
        float momentumMultiplier =
            CurrentMomentumTier switch
        {
            MomentumTier.Medium => MediumBounceMultiplier,
            MomentumTier.Rush => RushBounceMultiplier,
            _ => 1.0f
        };
        return momentumMultiplier *
            (1.0f + _runBounceMultiplierBonus);
    }

    private void UpdateDamageInvulnerability(float deltaSeconds)
    {
        if (_damageInvulnerabilityTimer <= 0.0f)
        {
            _presentation?.SetDamageVisible(true);
            return;
        }

        _damageInvulnerabilityTimer = Mathf.Max(
            0.0f,
            _damageInvulnerabilityTimer - deltaSeconds);
        _presentation?.SetDamageVisible(
            _damageInvulnerabilityTimer <= 0.0f ||
            Mathf.FloorToInt(
                _damageInvulnerabilityTimer * 12.0f) % 2 == 0);
    }

    private void UpdateObstacleSlow(float deltaSeconds)
    {
        if (_obstacleSlowTimer <= 0.0f)
        {
            ActiveObstacleSlowAmount = 0.0f;
            return;
        }

        _obstacleSlowTimer = Mathf.Max(
            0.0f,
            _obstacleSlowTimer - deltaSeconds);
        if (_obstacleSlowTimer <= 0.0f)
        {
            ActiveObstacleSlowAmount = 0.0f;
        }
    }

    private void UpdateSpeedBoost(float deltaSeconds)
    {
        if (_speedBoostTimer <= 0.0f)
        {
            ActiveSpeedBoost = 0.0f;
            SpeedBoostVisualIntensity = 0.0f;
            return;
        }

        _speedBoostTimer = Mathf.Max(
            0.0f,
            _speedBoostTimer - deltaSeconds);
        if (_speedBoostTimer <= 0.0f)
        {
            ActiveSpeedBoost = 0.0f;
            SpeedBoostVisualIntensity = 0.0f;
        }
    }

    private void UpdateDaringDodges(float deltaSeconds)
    {
        _nearMissDamageBlockTimer = Mathf.Max(
            0.0f,
            _nearMissDamageBlockTimer - deltaSeconds);
        _nearMissProbeTimer -= deltaSeconds;
        if (_nearMissProbeTimer > 0.0f)
        {
            return;
        }

        _nearMissProbeTimer = 0.04f;
        if (_isDead || IsDamageInvulnerable)
        {
            _nearMissAttempts.Clear();
            return;
        }

        HashSet<ulong> visibleMobIds = new();
        List<ulong> completedAttempts = new();
        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !IsInstanceValid(mob) ||
                mob.IsQueuedForDeletion() ||
                mob.IsPostArenaRetreating)
            {
                continue;
            }

            ulong mobId = mob.GetInstanceId();
            visibleMobIds.Add(mobId);
            if (_rewardedNearMisses.Contains(mobId))
            {
                continue;
            }

            Vector3 separation = mob.GlobalPosition - GlobalPosition;
            float verticalDistance =
                Mathf.Abs(separation.Y);
            separation.Y = 0.0f;
            float distance = separation.Length();
            bool hasAttempt =
                _nearMissAttempts.TryGetValue(
                    mobId,
                    out NearMissAttempt attempt);
            if (distance <= DaringDodgeEntryRadius)
            {
                if (_nearMissDamageBlockTimer > 0.0f)
                {
                    continue;
                }

                if (!hasAttempt)
                {
                    attempt = new NearMissAttempt();
                    _nearMissAttempts[mobId] = attempt;
                }

                UpdateNearMissAttempt(
                    attempt,
                    mob,
                    separation,
                    distance,
                    verticalDistance);
                continue;
            }

            if (distance < DaringDodgeExitRadius ||
                !hasAttempt)
            {
                continue;
            }

            NearMissAttempt finishedAttempt =
                attempt;
            UpdateNearMissAttempt(
                finishedAttempt,
                mob,
                separation,
                distance,
                verticalDistance);
            completedAttempts.Add(mobId);
            bool wasDangerous =
                finishedAttempt.ClosestDistance <= DaringDodgeDangerRadius &&
                finishedAttempt.PeakRelativeSpeed >=
                    DaringDodgeMinRelativeSpeed;
            if (wasDangerous)
            {
                _rewardedNearMisses.Add(mobId);
                bool crossedThreat =
                    (finishedAttempt
                        .SawThreatAhead &&
                        finishedAttempt
                            .SawThreatBehind) ||
                    (finishedAttempt
                        .SawClosingApproach &&
                        finishedAttempt
                            .SawOpeningSeparation &&
                        finishedAttempt
                            .SawSideBySide);
                bool slipstreamDodge =
                    finishedAttempt.SawGroundBurst &&
                    crossedThreat &&
                    finishedAttempt
                        .ClosestVerticalDistance <=
                        Mathf.Max(
                            0.25f,
                            SlipstreamVerticalTolerance) &&
                    finishedAttempt
                        .ClosestDistance <=
                        Mathf.Min(
                            DaringDodgeDangerRadius,
                            SlipstreamDodgeRadius) &&
                    finishedAttempt
                        .PeakRelativeSpeed >=
                        Mathf.Max(
                            DaringDodgeMinRelativeSpeed,
                            SlipstreamMinimumRelativeSpeed);
                if (slipstreamDodge)
                {
                    bool perfectSlipstream =
                        finishedAttempt
                            .ClosestDistance <=
                            Mathf.Min(
                                SlipstreamDodgeRadius,
                                PerfectSlipstreamRadius) &&
                        (finishedAttempt
                            .SawCommittedAttack ||
                            finishedAttempt
                                .PeakRelativeSpeed >=
                                Mathf.Max(
                                    DaringDodgeMinRelativeSpeed,
                                    SlipstreamMinimumRelativeSpeed) *
                                1.35f);
                    RegisterSlipstreamDodge(
                        finishedAttempt,
                        perfectSlipstream);
                }
                else
                {
                    RegisterDaringDodge(
                        finishedAttempt
                            .SawCommittedAttack);
                }
            }
        }

        foreach (ulong mobId in completedAttempts)
        {
            _nearMissAttempts.Remove(mobId);
        }

        List<ulong> vanishedAttempts = new();
        foreach (ulong mobId in _nearMissAttempts.Keys)
        {
            if (!visibleMobIds.Contains(mobId))
            {
                vanishedAttempts.Add(mobId);
            }
        }

        foreach (ulong mobId in vanishedAttempts)
        {
            _nearMissAttempts.Remove(mobId);
        }

        _rewardedNearMisses.RemoveWhere(
            mobId => !visibleMobIds.Contains(mobId));
    }

    private void UpdateSlipstreamDodgeCue(
        bool wasOnFloor)
    {
        ClearSlipstreamDodgeCue();
        if (_isDead ||
            !wasOnFloor ||
            IsInsideTunnel ||
            IsDamageInvulnerable ||
            IsGroundBurstActive ||
            NitroCharge <
                GetEffectiveGroundBurstCost())
        {
            return;
        }

        Vector3 heading =
            new(
                Velocity.X,
                0.0f,
                Velocity.Z);
        float currentSpeed =
            heading.Length();
        if (heading.IsZeroApprox())
        {
            heading =
                _lastMoveDirection;
            heading.Y = 0.0f;
        }
        if (heading.IsZeroApprox())
        {
            heading =
                Vector3.Forward;
        }
        heading =
            heading.Normalized();
        float projectedSpeed =
            Mathf.Min(
                Mathf.Max(
                    currentSpeed,
                    CurrentRunSpeed) +
                Mathf.Max(
                    0.0f,
                    GroundBurstSpeedGain),
                Mathf.Max(
                    CurrentRunSpeed,
                    GroundBurstSpeedCap));
        Vector3 projectedPlayerVelocity =
            heading * projectedSpeed;
        float predictionTime =
            Mathf.Max(
                0.2f,
                SlipstreamCuePredictionTime);
        float maximumDistance =
            Mathf.Max(
                DaringDodgeEntryRadius,
                SlipstreamCueMaximumDistance);
        float maximumMissDistance =
            Mathf.Max(
                0.5f,
                SlipstreamDodgeRadius +
                    Mathf.Max(
                        0.0f,
                        SlipstreamCueMissPadding));
        float actionableDelay =
            Mathf.Max(
                0.0f,
                _nitroDashCooldownTimer -
                    RiftStepInputBufferTime);
        float bestScore =
            float.MaxValue;
        Mob bestTarget = null;
        float bestTime = 0.0f;
        float bestMissDistance = 0.0f;

        foreach (Node node in
            GetTree().GetNodesInGroup(
                "mob"))
        {
            if (node is not Mob mob ||
                !IsValidComboRouteTarget(
                    mob) ||
                _rewardedNearMisses.Contains(
                    mob.GetInstanceId()) ||
                mob.IsArenaAttackResolved ||
                mob.IsPostArenaRetreating ||
                mob.IsYieldingToTunnel)
            {
                continue;
            }

            Vector3 separation =
                mob.GlobalPosition -
                GlobalPosition;
            float verticalDistance =
                Mathf.Abs(separation.Y);
            separation.Y = 0.0f;
            float distance =
                separation.Length();
            if (distance >
                    maximumDistance ||
                verticalDistance >
                    Mathf.Max(
                        0.25f,
                        SlipstreamVerticalTolerance))
            {
                continue;
            }

            Vector3 relativeVelocity =
                mob.Velocity -
                projectedPlayerVelocity;
            relativeVelocity.Y = 0.0f;
            float relativeSpeedSquared =
                relativeVelocity
                    .LengthSquared();
            if (relativeSpeedSquared <
                Mathf.Pow(
                    Mathf.Max(
                        3.0f,
                        SlipstreamMinimumRelativeSpeed *
                            0.7f),
                    2.0f))
            {
                continue;
            }

            float timeToClosest =
                -separation.Dot(
                    relativeVelocity) /
                relativeSpeedSquared;
            if (timeToClosest < -0.03f ||
                timeToClosest >
                    predictionTime ||
                timeToClosest +
                    0.04f <
                    actionableDelay)
            {
                continue;
            }
            timeToClosest =
                Mathf.Max(
                    0.0f,
                    timeToClosest);
            Vector3 closestOffset =
                separation +
                relativeVelocity *
                    timeToClosest;
            float missDistance =
                closestOffset.Length();
            if (missDistance >
                maximumMissDistance)
            {
                continue;
            }

            float candidateScore =
                missDistance +
                timeToClosest * 1.35f +
                distance * 0.015f;
            if (candidateScore >=
                bestScore)
            {
                continue;
            }
            bestScore =
                candidateScore;
            bestTarget =
                mob;
            bestTime =
                timeToClosest;
            bestMissDistance =
                missDistance;
        }

        if (!IsInstanceValid(bestTarget))
        {
            return;
        }

        SlipstreamCueTarget =
            bestTarget;
        SlipstreamCueDirection =
            heading;
        SlipstreamCuePredictedDistance =
            bestMissDistance;
        SlipstreamCueUrgency =
            1.0f -
            Mathf.Clamp(
                bestTime /
                    predictionTime,
                0.0f,
                1.0f);
        float readyTime =
            Mathf.Clamp(
                Mathf.Min(
                    SlipstreamCueReadyTime,
                    GetEffectiveDodgeWindow()),
                0.08f,
                predictionTime);
        CurrentSlipstreamCue =
            bestTime <= readyTime &&
            actionableDelay <= 0.04f
                ? bestMissDistance <=
                    PerfectSlipstreamRadius
                    ? SlipstreamCueLevel
                        .Perfect
                    : SlipstreamCueLevel
                        .Ready
                : SlipstreamCueLevel
                    .Approach;
    }

    private void ClearSlipstreamDodgeCue()
    {
        SlipstreamCueTarget = null;
        CurrentSlipstreamCue =
            SlipstreamCueLevel.None;
        SlipstreamCueUrgency = 0.0f;
        SlipstreamCuePredictedDistance =
            0.0f;
    }

    private void UpdateNearMissAttempt(
        NearMissAttempt attempt,
        Mob mob,
        Vector3 separation,
        float distance,
        float verticalDistance)
    {
        Vector3 relativeVelocity =
            mob.Velocity - Velocity;
        relativeVelocity.Y = 0.0f;
        float closingAmount =
            separation.Dot(
                relativeVelocity);
        attempt.SawClosingApproach |=
            closingAmount <= -1.0f;
        attempt.SawOpeningSeparation |=
            closingAmount >= 1.0f;
        attempt.PeakRelativeSpeed = Mathf.Max(
            attempt.PeakRelativeSpeed,
            relativeVelocity.Length());
        attempt.SawCommittedAttack |=
            mob.IsAttackCommitted;

        Vector3 planarDirection =
            new(
                Velocity.X,
                0.0f,
                Velocity.Z);
        if (planarDirection.IsZeroApprox())
        {
            planarDirection =
                _lastMoveDirection;
            planarDirection.Y = 0.0f;
        }
        if (planarDirection.IsZeroApprox())
        {
            planarDirection =
                Vector3.Forward;
        }
        planarDirection =
            planarDirection.Normalized();
        float alongPass =
            separation.Dot(
                planarDirection);
        Vector3 sideDirection =
            Vector3.Up.Cross(
                planarDirection)
                .Normalized();
        float lateralPass =
            Mathf.Abs(
                separation.Dot(
                    sideDirection));
        attempt.SawThreatAhead |=
            alongPass >= 0.35f;
        attempt.SawThreatBehind |=
            alongPass <= -0.35f;
        attempt.SawSideBySide |=
            Mathf.Abs(alongPass) <=
                Mathf.Max(
                    1.0f,
                    distance * 0.78f) &&
            lateralPass >= 0.45f;
        attempt.SawGroundBurst |=
            _slipstreamDodgeWindowTimer >
                0.0f &&
            IsOnWorldFloor();

        if (distance <
            attempt.ClosestDistance)
        {
            attempt.ClosestDistance =
                distance;
            attempt.ClosestVerticalDistance =
                verticalDistance;
            attempt.ClosestWorldPosition =
                GlobalPosition.Lerp(
                    mob.GlobalPosition,
                    0.5f);
            attempt.PassDirection =
                planarDirection;
        }
    }

    private void RegisterSlipstreamDodge(
        NearMissAttempt attempt,
        bool perfectDodge)
    {
        RegisterDaringDodge(
            perfectDodge);

        float nitroReward =
            (Mathf.Max(
                0.0f,
                SlipstreamNitroReward) +
                _runSlipstreamNitroBonus) *
            (perfectDodge ? 1.35f : 1.0f);
        AddNitro(nitroReward);
        AddMomentum(
            Mathf.Max(
                0.0f,
                SlipstreamMomentumReward) *
            _runStompMomentumMultiplier *
            (perfectDodge ? 1.35f : 1.0f));

        if (perfectDodge &&
            HasRunUpgrade(
                RunUpgradeId.KineticReserve))
        {
            _kineticReserveCharges = Mathf.Max(
                _kineticReserveCharges,
                HasRunUpgrade(
                    RunUpgradeId.PerfectCircuit)
                    ? 2
                    : 1);
        }

        float speedImpulse =
            Mathf.Max(
                0.0f,
                SlipstreamSpeedImpulse) *
            (perfectDodge ? 1.22f : 1.0f);
        Vector2 horizontalVelocity =
            new(
                Velocity.X,
                Velocity.Z);
        Vector2 heading =
            horizontalVelocity
                .IsZeroApprox()
                ? new Vector2(
                    attempt.PassDirection.X,
                    attempt.PassDirection.Z)
                : horizontalVelocity
                    .Normalized();
        if (heading.IsZeroApprox())
        {
            heading = Vector2.Up;
        }
        float currentSpeed =
            horizontalVelocity.Length();
        float boostedSpeed =
            Mathf.Min(
                currentSpeed +
                    speedImpulse,
                Mathf.Max(
                    currentSpeed,
                    GroundBurstSpeedCap +
                        4.0f));
        Velocity = new Vector3(
            heading.X * boostedSpeed,
            Velocity.Y,
            heading.Y * boostedSpeed);
        ApplyTraversalSpeedBoost(
            Mathf.Max(
                0.0f,
                SlipstreamSpeedBoost) +
            (perfectDodge ? 0.75f : 0.0f),
            Mathf.Max(
                0.2f,
                SlipstreamBoostDuration) +
            (perfectDodge ? 0.12f : 0.0f));

        int scoreReward =
            Mathf.Max(
                1,
                SlipstreamDodgeScore +
                (perfectDodge
                    ? PerfectSlipstreamScoreBonus
                    : 0) +
                Mathf.Max(
                    0,
                    RewardMultiplier - 1));
        GamepadInput.Rumble(
            perfectDodge ? 0.32f : 0.22f,
            perfectDodge ? 0.9f : 0.68f,
            perfectDodge ? 0.15f : 0.11f);
        Input.VibrateHandheld(
            perfectDodge ? 82 : 58,
            perfectDodge ? 0.78f : 0.52f);
        EmitSignal(
            SignalName.SlipstreamDodge,
            scoreReward,
            nitroReward,
            speedImpulse,
            perfectDodge,
            attempt.ClosestWorldPosition,
            attempt.PassDirection);
    }

    private void RegisterDaringDodge(bool perfectDodge)
    {
        AddNitro(
            Mathf.Max(0.0f, NitroDaringDodgeGain) *
            (perfectDodge ? 1.5f : 1.0f));
        float speedBoost =
            DaringDodgeSpeedBoost +
            (perfectDodge
                ? PerfectDodgeExtraSpeedBoost
                : 0.0f);
        ApplyTraversalSpeedBoost(
            speedBoost,
            DaringDodgeBoostDuration +
                (perfectDodge ? 0.15f : 0.0f));
        if (_airStompChain > 0)
        {
            int dodgeStyle =
                perfectDodge ? 9 : 5;
            _comboState.AddStyle(dodgeStyle);
            float finisherThreshold =
                GetEffectiveFinisherThreshold();
            _comboFinisherCharge = Mathf.Min(
                Mathf.Max(
                    finisherThreshold * 1.35f,
                    finisherThreshold + 1.0f),
                _comboFinisherCharge +
                    dodgeStyle *
                    ComboFinisherChargeScale);
        }

        float timeReduced = 0.0f;
        bool completesContract = false;
        if (RecoveryTarget > 0 &&
            IsEnduranceRecoveryEnabled &&
            RecoveryTimeRemaining > 0.0f)
        {
            float previousTime = RecoveryTimeRemaining;
            float reduction =
                Mathf.Max(0.0f, DaringDodgeContractReduction) *
                (perfectDodge
                    ? Mathf.Max(
                        1.0f,
                        PerfectDodgeReductionMultiplier)
                    : 1.0f);
            RecoveryTimeRemaining = Mathf.Max(
                0.0f,
                RecoveryTimeRemaining - reduction);
            timeReduced = previousTime - RecoveryTimeRemaining;
            completesContract = RecoveryTimeRemaining <= 0.0f;
            EmitRecoveryContractChanged();
        }

        EmitSignal(
            SignalName.DaringDodge,
            timeReduced,
            speedBoost,
            perfectDodge);
        if (completesContract)
        {
            CompleteRecoveryContract(false);
        }
    }

    private bool IsFarOutsideViewport()
    {
        Camera3D camera = GetViewport().GetCamera3D();

        if (camera == null)
        {
            return false;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 screenPosition = camera.UnprojectPosition(GlobalPosition);
        float margin = viewportSize.Y * OffScreenMarginRatio;

        // The top edge is intentionally open: moving through it advances the
        // scrolling camera and lets the player reach the next location.
        return screenPosition.X < -margin ||
               screenPosition.X > viewportSize.X + margin ||
               screenPosition.Y > viewportSize.Y + margin ||
               GlobalPosition.Y < -8.0f;
    }
}
