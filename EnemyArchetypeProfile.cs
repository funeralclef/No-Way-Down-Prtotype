using Godot;
using System.Collections.Generic;

public enum EnemyArchetypeId
{
    Springboard,
    Spear,
    Courier,
    Shepherd,
    Trickster,
    Shell,
    Relay,
    Diver,
    Twin
}

/// <summary>
/// The readable promise made by an enemy before steering and collision are
/// applied. Physics remain shared by Mob; identity, rewards and authored
/// behaviour live here.
/// </summary>
[GlobalClass]
public partial class EnemyArchetypeProfile : Resource
{
    [Export]
    public EnemyArchetypeId Id { get; set; }

    [Export]
    public string DisplayName { get; set; } = "SPRINGBOARD";

    [Export]
    public Color BodyColor { get; set; } = Colors.White;

    [Export]
    public Color AccentColor { get; set; } = Colors.White;

    [Export]
    public Vector3 SilhouetteScale { get; set; } = Vector3.One;

    [Export]
    public Mob.RegularBehavior BaseBehavior { get; set; } =
        Mob.RegularBehavior.Pursuer;

    [Export]
    public bool CanInitiateAttack { get; set; } = true;

    [Export]
    public float SpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float TurnMultiplier { get; set; } = 1.0f;

    [Export]
    public float WeaveMultiplier { get; set; } = 1.0f;

    [Export]
    public float TelegraphTimeMultiplier { get; set; } = 1.0f;

    [Export]
    public float LungeSpeedMultiplier { get; set; } = 1.0f;

    [Export]
    public float FeintChance { get; set; }

    [Export]
    public float JumpOpportunityPriority { get; set; }

    [Export]
    public int ScoreMultiplier { get; set; } = 1;

    [Export]
    public float BonusNitro { get; set; }

    [Export]
    public float BonusHealing { get; set; }

    [Export]
    public int ArmorLayers { get; set; }

    [Export]
    public bool UsesCourierMotion { get; set; }

    [Export]
    public bool UsesShepherdMotion { get; set; }

    [Export]
    public bool UsesRelayMotion { get; set; }

    [Export]
    public bool UsesTwinMotion { get; set; }

    [Export]
    public float CuePitch { get; set; } = 1.0f;
}

public static class EnemyArchetypeLibrary
{
    private static readonly Dictionary<
        EnemyArchetypeId,
        EnemyArchetypeProfile> Profiles = BuildProfiles();

    public static EnemyArchetypeProfile Get(
        EnemyArchetypeId id)
    {
        return Profiles[id];
    }

    private static Dictionary<
        EnemyArchetypeId,
        EnemyArchetypeProfile> BuildProfiles()
    {
        return new()
        {
            [EnemyArchetypeId.Springboard] = new()
            {
                Id = EnemyArchetypeId.Springboard,
                DisplayName = "SPRINGBOARD",
                BodyColor = new Color(0.48f, 1.0f, 0.25f),
                AccentColor = new Color(0.9f, 1.0f, 0.28f),
                SilhouetteScale = new Vector3(0.86f, 0.92f, 0.86f),
                BaseBehavior = Mob.RegularBehavior.Wanderer,
                CanInitiateAttack = false,
                SpeedMultiplier = 0.88f,
                TurnMultiplier = 0.72f,
                WeaveMultiplier = 0.55f,
                JumpOpportunityPriority = 0.7f,
                CuePitch = 1.25f
            },
            [EnemyArchetypeId.Spear] = new()
            {
                Id = EnemyArchetypeId.Spear,
                DisplayName = "SPEAR",
                BodyColor = new Color(1.0f, 0.18f, 0.08f),
                AccentColor = new Color(1.0f, 0.68f, 0.08f),
                SilhouetteScale = new Vector3(0.72f, 0.92f, 1.28f),
                BaseBehavior = Mob.RegularBehavior.Pursuer,
                CanInitiateAttack = true,
                SpeedMultiplier = 1.06f,
                TurnMultiplier = 1.08f,
                WeaveMultiplier = 0.35f,
                TelegraphTimeMultiplier = 1.12f,
                LungeSpeedMultiplier = 1.14f,
                ScoreMultiplier = 2,
                BonusNitro = 4.0f,
                CuePitch = 0.78f
            },
            [EnemyArchetypeId.Courier] = new()
            {
                Id = EnemyArchetypeId.Courier,
                DisplayName = "COURIER",
                BodyColor = new Color(0.08f, 0.94f, 1.0f),
                AccentColor = new Color(0.65f, 1.0f, 1.0f),
                SilhouetteScale = new Vector3(0.68f, 0.72f, 0.88f),
                BaseBehavior = Mob.RegularBehavior.Drifter,
                CanInitiateAttack = false,
                SpeedMultiplier = 1.04f,
                TurnMultiplier = 1.2f,
                WeaveMultiplier = 1.35f,
                JumpOpportunityPriority = 0.48f,
                ScoreMultiplier = 2,
                BonusNitro = 22.0f,
                BonusHealing = 5.0f,
                UsesCourierMotion = true,
                CuePitch = 1.55f
            },
            [EnemyArchetypeId.Shepherd] = new()
            {
                Id = EnemyArchetypeId.Shepherd,
                DisplayName = "SHEPHERD",
                BodyColor = new Color(0.2f, 0.52f, 1.0f),
                AccentColor = new Color(0.42f, 0.9f, 1.0f),
                SilhouetteScale = new Vector3(1.28f, 0.82f, 0.84f),
                BaseBehavior = Mob.RegularBehavior.Pursuer,
                CanInitiateAttack = false,
                SpeedMultiplier = 0.94f,
                TurnMultiplier = 1.35f,
                WeaveMultiplier = 0.72f,
                ScoreMultiplier = 2,
                BonusNitro = 7.0f,
                UsesShepherdMotion = true,
                CuePitch = 0.92f
            },
            [EnemyArchetypeId.Trickster] = new()
            {
                Id = EnemyArchetypeId.Trickster,
                DisplayName = "TRICKSTER",
                BodyColor = new Color(0.84f, 0.2f, 1.0f),
                AccentColor = new Color(1.0f, 0.52f, 1.0f),
                SilhouetteScale = new Vector3(0.9f, 1.05f, 1.02f),
                BaseBehavior = Mob.RegularBehavior.Feinter,
                CanInitiateAttack = true,
                SpeedMultiplier = 1.0f,
                TurnMultiplier = 1.15f,
                WeaveMultiplier = 1.15f,
                TelegraphTimeMultiplier = 1.22f,
                LungeSpeedMultiplier = 1.04f,
                FeintChance = 1.0f,
                ScoreMultiplier = 2,
                BonusNitro = 9.0f,
                CuePitch = 1.38f
            },
            [EnemyArchetypeId.Shell] = new()
            {
                Id = EnemyArchetypeId.Shell,
                DisplayName = "SHELL",
                BodyColor = new Color(1.0f, 0.58f, 0.12f),
                AccentColor = new Color(1.0f, 0.9f, 0.42f),
                SilhouetteScale = new Vector3(1.25f, 1.12f, 1.18f),
                BaseBehavior = Mob.RegularBehavior.Wanderer,
                CanInitiateAttack = false,
                SpeedMultiplier = 0.8f,
                TurnMultiplier = 0.65f,
                WeaveMultiplier = 0.25f,
                JumpOpportunityPriority = 0.32f,
                ScoreMultiplier = 3,
                BonusNitro = 12.0f,
                BonusHealing = 8.0f,
                ArmorLayers = 1,
                CuePitch = 0.62f
            },
            [EnemyArchetypeId.Relay] = new()
            {
                Id = EnemyArchetypeId.Relay,
                DisplayName = "RELAY",
                BodyColor = new Color(0.72f, 1.0f, 0.16f),
                AccentColor = new Color(1.0f, 1.0f, 0.72f),
                SilhouetteScale = new Vector3(0.78f, 0.94f, 0.82f),
                BaseBehavior = Mob.RegularBehavior.Wanderer,
                CanInitiateAttack = false,
                SpeedMultiplier = 0.96f,
                TurnMultiplier = 0.82f,
                WeaveMultiplier = 0.42f,
                JumpOpportunityPriority = 0.76f,
                ScoreMultiplier = 2,
                BonusNitro = 8.0f,
                UsesRelayMotion = true,
                CuePitch = 1.72f
            },
            [EnemyArchetypeId.Diver] = new()
            {
                Id = EnemyArchetypeId.Diver,
                DisplayName = "DIVER",
                BodyColor = new Color(1.0f, 0.08f, 0.38f),
                AccentColor = new Color(1.0f, 0.78f, 0.22f),
                SilhouetteScale = new Vector3(0.66f, 1.16f, 1.38f),
                BaseBehavior = Mob.RegularBehavior.Pursuer,
                CanInitiateAttack = true,
                SpeedMultiplier = 1.02f,
                TurnMultiplier = 0.92f,
                WeaveMultiplier = 0.18f,
                TelegraphTimeMultiplier = 1.48f,
                LungeSpeedMultiplier = 1.26f,
                JumpOpportunityPriority = 0.22f,
                ScoreMultiplier = 3,
                BonusNitro = 11.0f,
                CuePitch = 0.56f
            },
            [EnemyArchetypeId.Twin] = new()
            {
                Id = EnemyArchetypeId.Twin,
                DisplayName = "TWIN",
                BodyColor = new Color(0.24f, 0.48f, 1.0f),
                AccentColor = new Color(0.45f, 1.0f, 0.92f),
                SilhouetteScale = new Vector3(0.74f, 0.86f, 0.92f),
                BaseBehavior = Mob.RegularBehavior.Wanderer,
                CanInitiateAttack = false,
                SpeedMultiplier = 0.93f,
                TurnMultiplier = 1.06f,
                WeaveMultiplier = 1.18f,
                JumpOpportunityPriority = 0.62f,
                ScoreMultiplier = 2,
                BonusNitro = 6.0f,
                UsesTwinMotion = true,
                CuePitch = 1.12f
            }
        };
    }
}
