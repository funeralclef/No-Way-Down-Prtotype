using Godot;
using System;
using System.Collections.Generic;

public enum RunRewardTier
{
    Impulse,
    Device,
    Evolution
}

public enum RunRouteStyle
{
    Neutral,
    Hunt,
    Flow,
    Endure
}

public enum RunUpgradeId
{
    LightFrame,
    DeepTank,
    SoftLanding,
    LongStep,
    SecondWind,
    QuickSpark,
    Airfoil,
    CoolBurn,
    SteadyPulse,
    RecoveryMesh,
    CloseCall,
    FinisherCell,

    Afterburner,
    RedThread,
    ShockSole,
    GlassHeart,
    HunterLens,
    Overpass,
    KineticReserve,
    ClosedLoop,

    BlueComet,
    ChainReaction,
    EarthSplitter,
    LastLight,
    TwoHorizons,
    PhaseRoute,
    PerfectCircuit,
    InfiniteLine
}

/// <summary>
/// Immutable presentation and drafting data. Runtime effects deliberately
/// live outside this model so a run can apply them to Player and Main without
/// making the catalog depend on either scene.
/// </summary>
public sealed class RunUpgradeDefinition
{
    public RunUpgradeId Id { get; }
    public RunRewardTier Tier { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<string> Tags { get; }
    public RunUpgradeId? ParentUpgrade { get; }
    public bool IsSafe { get; }
    public int MaxRank { get; }

    public RunUpgradeDefinition(
        RunUpgradeId id,
        RunRewardTier tier,
        string name,
        string description,
        string[] tags,
        bool isSafe = false,
        int maxRank = 1,
        RunUpgradeId? parentUpgrade = null)
    {
        Id = id;
        Tier = tier;
        Name = name;
        Description = description;
        Tags = Array.AsReadOnly(
            tags ?? Array.Empty<string>());
        ParentUpgrade = parentUpgrade;
        IsSafe = isSafe;
        MaxRank = Mathf.Max(1, maxRank);
    }
}

/// <summary>
/// Scene-local run progression. Offers are ordered as safe, synergy and
/// wildcard whenever the pool permits it. A fixed seed makes the complete
/// draft and route sequence deterministic for probes, replays and seeded runs.
/// </summary>
public sealed class RunProgression
{
    private const int RewardOfferCount = 3;
    private const int RouteOfferCount = 2;
    private const int RecentDraftWindow = 4;

    private static readonly RunUpgradeDefinition[]
        Definitions =
        {
            // IMPULSES: immediate, legible improvements with modest ranks.
            Def(
                RunUpgradeId.LightFrame,
                RunRewardTier.Impulse,
                "LIGHT FRAME",
                "Build speed sooner and carry more pace into Flow.",
                new[] { "speed", "momentum" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.DeepTank,
                RunRewardTier.Impulse,
                "DEEP TANK",
                "Increase nitro capacity and the charge retained between fights.",
                new[] { "nitro", "reserve" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.SoftLanding,
                RunRewardTier.Impulse,
                "SOFT LANDING",
                "Ground contact preserves more speed and combo grace.",
                new[] { "landing", "speed", "combo" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.LongStep,
                RunRewardTier.Impulse,
                "LONG STEP",
                "Extend readable combo arcs without increasing auto-steering.",
                new[] { "combo", "air", "range" },
                maxRank: 3),
            Def(
                RunUpgradeId.SecondWind,
                RunRewardTier.Impulse,
                "SECOND WIND",
                "Kills at critical health restore a small amount of vitality.",
                new[] { "health", "kill", "risk" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.QuickSpark,
                RunRewardTier.Impulse,
                "QUICK SPARK",
                "Ground bursts cost less nitro and become ready sooner.",
                new[] { "nitro", "burst", "speed" },
                maxRank: 3),
            Def(
                RunUpgradeId.Airfoil,
                RunRewardTier.Impulse,
                "AIRFOIL",
                "Double jumps carry more horizontal intent through the arc.",
                new[] { "air", "jump", "combo" },
                maxRank: 3),
            Def(
                RunUpgradeId.CoolBurn,
                RunRewardTier.Impulse,
                "COOL BURN",
                "Sustained reactor thrust consumes nitro more efficiently.",
                new[] { "nitro", "reactor", "speed" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.SteadyPulse,
                RunRewardTier.Impulse,
                "STEADY PULSE",
                "Stomps and dangerous dodges restore more momentum.",
                new[] { "momentum", "stomp", "dodge" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.RecoveryMesh,
                RunRewardTier.Impulse,
                "RECOVERY MESH",
                "Increase maximum health by 15 and immediately fill the new capacity.",
                new[] { "health", "recovery", "capacity" },
                safe: true,
                maxRank: 3),
            Def(
                RunUpgradeId.CloseCall,
                RunRewardTier.Impulse,
                "CLOSE CALL",
                "Widen the dodge window and earn more nitro from slipstreams.",
                new[] { "dodge", "risk", "nitro" },
                maxRank: 3),
            Def(
                RunUpgradeId.FinisherCell,
                RunRewardTier.Impulse,
                "FINISHER CELL",
                "Combo finishers charge sooner and return additional nitro.",
                new[] { "combo", "finisher", "nitro" },
                maxRank: 3),

            // DEVICES: build-defining rules rather than passive percentages.
            Def(
                RunUpgradeId.Afterburner,
                RunRewardTier.Device,
                "AFTERBURNER",
                "Reactor release leaves a visible wind lane you can cross for a fresh burst.",
                new[] { "nitro", "reactor", "speed" },
                safe: true,
                maxRank: 2),
            Def(
                RunUpgradeId.RedThread,
                RunRewardTier.Device,
                "RED THREAD",
                "Unlock Thread Recall: return to a planted or Rift-created anchor.",
                new[] { "rift", "combo", "target" },
                maxRank: 2),
            Def(
                RunUpgradeId.ShockSole,
                RunRewardTier.Device,
                "SHOCK SOLE",
                "Unlock Fault Dive: plunge from the air and interrupt enemy attack warnings.",
                new[] { "landing", "control", "enemy" },
                safe: true,
                maxRank: 2),
            Def(
                RunUpgradeId.GlassHeart,
                RunRewardTier.Device,
                "GLASS HEART",
                "A glass guard absorbs a hit and rebuilds after three stomps.",
                new[] { "health", "risk", "finisher" },
                maxRank: 2),
            Def(
                RunUpgradeId.HunterLens,
                RunRewardTier.Device,
                "HUNTER LENS",
                "Unlock Hunt Spark: a homing drone strikes creeps or marks a guardian.",
                new[] { "combo", "target", "range" },
                safe: true,
                maxRank: 2),
            Def(
                RunUpgradeId.Overpass,
                RunRewardTier.Device,
                "OVERPASS",
                "Unlock Air Step: steer a short blink without an enemy target.",
                new[] { "rift", "air", "combo" },
                maxRank: 2),
            Def(
                RunUpgradeId.KineticReserve,
                RunRewardTier.Device,
                "KINETIC RESERVE",
                "Perfect dodges release a counterwave and store energy for a stomp.",
                new[] { "dodge", "nitro", "stomp" },
                safe: true,
                maxRank: 2),
            Def(
                RunUpgradeId.ClosedLoop,
                RunRewardTier.Device,
                "CLOSED LOOP",
                "Every fifth combo stomp creates a friendly Echo Step to extend your aerial route.",
                new[] { "combo", "kill", "nitro" },
                safe: true,
                maxRank: 2),

            // EVOLUTIONS: only legal after their matching device is owned.
            Evolution(
                RunUpgradeId.BlueComet,
                "BLUE COMET",
                "Wind wakes erase nearby attack warnings and carry power through three stomps.",
                new[] { "nitro", "reactor", "speed", "stomp" },
                RunUpgradeId.Afterburner,
                safe: true),
            Evolution(
                RunUpgradeId.ChainReaction,
                "CHAIN REACTION",
                "Recalling leaves a single-use Echo Step; Red Thread still forecasts two continuations.",
                new[] { "rift", "combo", "target" },
                RunUpgradeId.RedThread),
            Evolution(
                RunUpgradeId.EarthSplitter,
                "EARTH SPLITTER",
                "Fault Dive clears a wider field of warnings and pushes enemies apart.",
                new[] { "landing", "control", "enemy", "momentum" },
                RunUpgradeId.ShockSole,
                safe: true),
            Evolution(
                RunUpgradeId.LastLight,
                "LAST LIGHT",
                "Breaking your guard clears attack warnings. Combo defeats still restore health.",
                new[] { "health", "risk", "combo" },
                RunUpgradeId.GlassHeart),
            Evolution(
                RunUpgradeId.TwoHorizons,
                "TWO HORIZONS",
                "Hunt Spark launches two drones at separate targets. Guardian marks never stack.",
                new[] { "combo", "target", "range" },
                RunUpgradeId.HunterLens,
                safe: true),
            Evolution(
                RunUpgradeId.PhaseRoute,
                "PHASE ROUTE",
                "Air Step leaves an Echo Step behind. The first stomp after a Rift refunds nitro.",
                new[] { "rift", "nitro", "combo" },
                RunUpgradeId.Overpass),
            Evolution(
                RunUpgradeId.PerfectCircuit,
                "PERFECT CIRCUIT",
                "A perfect dodge creates a counterwave and an Echo Step, priming two stomps.",
                new[] { "dodge", "nitro", "stomp" },
                RunUpgradeId.KineticReserve,
                safe: true),
            Evolution(
                RunUpgradeId.InfiniteLine,
                "INFINITE LINE",
                "Every fourth combo stomp makes an Echo Step. Two may coexist to bridge a longer route.",
                new[] { "combo", "kill", "nitro" },
                RunUpgradeId.ClosedLoop,
                safe: true)
        };

    private static readonly IReadOnlyList<RunUpgradeDefinition>
        CatalogView = Array.AsReadOnly(Definitions);
    private static readonly Dictionary<RunUpgradeId, RunUpgradeDefinition>
        DefinitionsById = BuildDefinitionLookup();

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<RunUpgradeId, int> _ranks = new();
    private readonly List<RunUpgradeDefinition> _rewardOffers = new();
    private readonly List<RunRouteStyle> _routeOffers = new();
    private readonly Queue<HashSet<RunUpgradeId>> _recentDrafts = new();
    private bool _draftActive;
    private bool _rewardChosen;
    private bool _routeChosen;

    public static IReadOnlyList<RunUpgradeDefinition> Catalog =>
        CatalogView;
    public int ArenaNumber { get; private set; }
    public RunRewardTier CurrentRewardTier { get; private set; } =
        RunRewardTier.Device;
    public IReadOnlyList<RunUpgradeDefinition> CurrentRewardOffers =>
        _rewardOffers.AsReadOnly();
    public bool CanReroll { get; private set; }
    public IReadOnlyList<RunRouteStyle> CurrentRouteOffers =>
        _routeOffers.AsReadOnly();
    public RunRouteStyle SelectedRoute { get; private set; } =
        RunRouteStyle.Neutral;
    public RunUpgradeDefinition SelectedReward { get; private set; }
    public RunUpgradeId? LastGrantedCatalyst { get; private set; }

    public RunProgression()
    {
        _random.Randomize();
    }

    public RunProgression(ulong seed)
    {
        SetSeed(seed);
    }

    public void SetSeed(ulong seed)
    {
        _random.Seed = seed;
    }

    public static RunUpgradeDefinition GetDefinition(
        RunUpgradeId id)
    {
        if (!DefinitionsById.TryGetValue(
                id,
                out RunUpgradeDefinition definition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "Unknown run upgrade.");
        }

        return definition;
    }

    public void BeginArenaDraft()
    {
        if (_draftActive && !_routeChosen)
        {
            throw new InvalidOperationException(
                "Finish the current reward and route draft before starting another arena draft.");
        }

        ArenaNumber++;
        CurrentRewardTier = RunRewardTier.Device;
        SelectedReward = null;
        LastGrantedCatalyst = null;
        _rewardChosen = false;
        _routeChosen = false;
        _draftActive = true;
        CanReroll = true;
        _routeOffers.Clear();
        BuildRewardOffers();
    }

    public bool RerollRewards()
    {
        if (!_draftActive || _rewardChosen || !CanReroll)
        {
            return false;
        }

        RememberCurrentDraft();
        CanReroll = false;
        BuildRewardOffers();
        return true;
    }

    public RunUpgradeDefinition SelectReward(int index)
    {
        EnsureRewardSelectionAvailable();
        if (index < 0 || index >= _rewardOffers.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }

        RunUpgradeDefinition selected =
            _rewardOffers[index];
        if (selected.Tier == RunRewardTier.Evolution &&
            (!selected.ParentUpgrade.HasValue ||
                !HasUpgrade(selected.ParentUpgrade.Value)))
        {
            throw new InvalidOperationException(
                "An evolution cannot be selected before its parent device.");
        }

        _ranks.TryGetValue(
            selected.Id,
            out int currentRank);
        _ranks[selected.Id] = Mathf.Min(
            selected.MaxRank,
            currentRank + 1);
        // Arena rewards are active devices. Their matching catalyst is earned
        // automatically; evolutions are crafted, never a parallel stat draft.
        foreach (var recipe in RunBuffCatalog.Recipes)
        {
            if (recipe.Device != selected.Id) continue;
            _ranks[recipe.Catalyst] = Mathf.Min(GetDefinition(recipe.Catalyst).MaxRank, GetRank(recipe.Catalyst) + 1);
            LastGrantedCatalyst = recipe.Catalyst;
            break;
        }
        SelectedReward = selected;
        _rewardChosen = true;
        CanReroll = false;
        RememberCurrentDraft();
        BuildRouteOffers();
        return selected;
    }

    public RunRouteStyle SelectRoute(int index)
    {
        if (!_draftActive || !_rewardChosen || _routeChosen)
        {
            throw new InvalidOperationException(
                "A route can only be chosen once, after selecting an arena reward.");
        }
        if (index < 0 || index >= _routeOffers.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }

        SelectedRoute = _routeOffers[index];
        _routeChosen = true;
        return SelectedRoute;
    }

    public bool HasUpgrade(RunUpgradeId id)
    {
        return GetRank(id) > 0;
    }

    public int GetRank(RunUpgradeId id)
    {
        return _ranks.TryGetValue(
            id,
            out int rank)
                ? rank
                : 0;
    }

    // Merging is allowed between drafts. A device stays installed; two ranks
    // of its catalyst are traded for the evolution. All writes are atomic.
    public bool CanMerge(RunUpgradeId evolution)
    {
        RunMergeRecipe recipe = RunBuffCatalog.GetRecipe(evolution);
        return recipe != null && (!_draftActive || _routeChosen) &&
            !HasUpgrade(evolution) && HasUpgrade(recipe.Device) &&
            GetRank(recipe.Catalyst) >= RunMergeRecipe.CatalystCost;
    }

    public bool TryMerge(RunUpgradeId evolution)
    {
        if (!CanMerge(evolution)) return false;
        RunMergeRecipe recipe = RunBuffCatalog.GetRecipe(evolution);
        int remaining = GetRank(recipe.Catalyst) - RunMergeRecipe.CatalystCost;
        if (remaining == 0) _ranks.Remove(recipe.Catalyst);
        else _ranks[recipe.Catalyst] = remaining;
        _ranks[evolution] = 1;
        return true;
    }

    private void EnsureRewardSelectionAvailable()
    {
        if (!_draftActive || _rewardChosen)
        {
            throw new InvalidOperationException(
                "No reward selection is currently available.");
        }
    }

    private void BuildRewardOffers()
    {
        _rewardOffers.Clear();
        List<RunUpgradeDefinition> candidates =
            BuildCandidatePool();
        if (candidates.Count < RewardOfferCount)
        {
            throw new InvalidOperationException(
                "The run reward catalog cannot produce three distinct offers.");
        }

        HashSet<RunUpgradeId> recent =
            GetRecentUpgradeIds();

        // Slot zero is the dependable option. Avoid repeats whenever at least
        // one fresh safe choice remains, but never sacrifice a complete draft.
        List<RunUpgradeDefinition> safe =
            FilterCandidates(
                candidates,
                definition =>
                    definition.IsSafe &&
                    !recent.Contains(definition.Id));
        if (safe.Count == 0)
        {
            safe = FilterCandidates(
                candidates,
                definition => definition.IsSafe);
        }
        AddOffer(
            ChooseRandom(
                safe.Count > 0
                    ? safe
                    : candidates),
            candidates);

        // Slot one reinforces the current equipment family or offers a
        // compatible device. Evolutions are reserved for the merge system.
        RunUpgradeDefinition synergy =
            ChooseSynergyCandidate(
                candidates,
                recent);
        AddOffer(synergy, candidates);

        // Slot two is the wildcard: fresh first, then any remaining legal
        // choice. It keeps seeded drafts surprising without becoming noise.
        List<RunUpgradeDefinition> fresh =
            FilterCandidates(
                candidates,
                definition =>
                    !recent.Contains(definition.Id));
        AddOffer(
            ChooseRandom(
                fresh.Count > 0
                    ? fresh
                    : candidates),
            candidates);
    }

    private List<RunUpgradeDefinition> BuildCandidatePool()
    {
        List<RunUpgradeDefinition> primary = new();
        foreach (var definition in Definitions)
            if (definition.Tier == RunRewardTier.Device && IsSelectable(definition)) primary.Add(definition);
        // Endless runs can service a capped module to earn its catalyst again.
        if (primary.Count < RewardOfferCount)
            foreach (var definition in Definitions)
                if (definition.Tier == RunRewardTier.Device && !primary.Contains(definition)) primary.Add(definition);
        return primary;
    }

    private bool IsSelectable(
        RunUpgradeDefinition definition)
    {
        if (GetRank(definition.Id) >= definition.MaxRank)
        {
            return false;
        }
        return definition.Tier != RunRewardTier.Evolution ||
            definition.ParentUpgrade.HasValue &&
            HasUpgrade(definition.ParentUpgrade.Value);
    }

    private RunUpgradeDefinition ChooseSynergyCandidate(
        List<RunUpgradeDefinition> candidates,
        HashSet<RunUpgradeId> recent)
    {
        int bestScore = int.MinValue;
        List<RunUpgradeDefinition> best = new();
        HashSet<string> ownedTags = GetOwnedTags();
        foreach (RunUpgradeDefinition candidate in candidates)
        {
            int score = 0;
            foreach (string tag in candidate.Tags)
            {
                if (ownedTags.Contains(tag))
                {
                    score += 2;
                }
            }
            if (candidate.ParentUpgrade.HasValue &&
                HasUpgrade(candidate.ParentUpgrade.Value))
            {
                score += 8;
            }
            if (HasUpgrade(candidate.Id))
            {
                score += 1;
            }
            if (recent.Contains(candidate.Id))
            {
                score -= 3;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(candidate);
            }
            else if (score == bestScore)
            {
                best.Add(candidate);
            }
        }

        return ChooseRandom(
            best.Count > 0
                ? best
                : candidates);
    }

    private HashSet<string> GetOwnedTags()
    {
        HashSet<string> tags = new(
            StringComparer.Ordinal);
        foreach ((RunUpgradeId id, int rank) in _ranks)
        {
            if (rank <= 0)
            {
                continue;
            }
            foreach (string tag in
                GetDefinition(id).Tags)
            {
                tags.Add(tag);
            }
        }
        return tags;
    }

    private static List<RunUpgradeDefinition> FilterCandidates(
        List<RunUpgradeDefinition> candidates,
        Func<RunUpgradeDefinition, bool> predicate)
    {
        List<RunUpgradeDefinition> filtered = new();
        foreach (RunUpgradeDefinition candidate in candidates)
        {
            if (predicate(candidate))
            {
                filtered.Add(candidate);
            }
        }
        return filtered;
    }

    private RunUpgradeDefinition ChooseRandom(
        List<RunUpgradeDefinition> candidates)
    {
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot choose from an empty progression pool.");
        }
        return candidates[
            _random.RandiRange(
                0,
                candidates.Count - 1)];
    }

    private void AddOffer(
        RunUpgradeDefinition definition,
        List<RunUpgradeDefinition> candidates)
    {
        _rewardOffers.Add(definition);
        candidates.Remove(definition);
    }

    private void BuildRouteOffers()
    {
        _routeOffers.Clear();
        List<RunRouteStyle> routes = new()
        {
            RunRouteStyle.Hunt,
            RunRouteStyle.Flow,
            RunRouteStyle.Endure
        };

        int firstIndex =
            _random.RandiRange(
                0,
                routes.Count - 1);
        _routeOffers.Add(routes[firstIndex]);
        routes.RemoveAt(firstIndex);
        _routeOffers.Add(
            routes[
                _random.RandiRange(
                    0,
                    routes.Count - 1)]);
    }

    private void RememberCurrentDraft()
    {
        if (_rewardOffers.Count == 0)
        {
            return;
        }

        HashSet<RunUpgradeId> offerIds = new();
        foreach (RunUpgradeDefinition offer in _rewardOffers)
        {
            offerIds.Add(offer.Id);
        }
        _recentDrafts.Enqueue(offerIds);
        while (_recentDrafts.Count > RecentDraftWindow)
        {
            _recentDrafts.Dequeue();
        }
    }

    private HashSet<RunUpgradeId> GetRecentUpgradeIds()
    {
        HashSet<RunUpgradeId> recent = new();
        foreach (HashSet<RunUpgradeId> draft in _recentDrafts)
        {
            recent.UnionWith(draft);
        }
        return recent;
    }

    private static Dictionary<RunUpgradeId, RunUpgradeDefinition>
        BuildDefinitionLookup()
    {
        Dictionary<RunUpgradeId, RunUpgradeDefinition> lookup = new();
        foreach (RunUpgradeDefinition definition in Definitions)
        {
            if (!lookup.TryAdd(
                    definition.Id,
                    definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate run upgrade definition: {definition.Id}");
            }
        }
        return lookup;
    }

    private static RunUpgradeDefinition Def(
        RunUpgradeId id,
        RunRewardTier tier,
        string name,
        string description,
        string[] tags,
        bool safe = false,
        int maxRank = 1)
    {
        return new RunUpgradeDefinition(
            id,
            tier,
            name,
            description,
            tags,
            safe,
            maxRank);
    }

    private static RunUpgradeDefinition Evolution(
        RunUpgradeId id,
        string name,
        string description,
        string[] tags,
        RunUpgradeId parent,
        bool safe = false)
    {
        return new RunUpgradeDefinition(
            id,
            RunRewardTier.Evolution,
            name,
            description,
            tags,
            safe,
            maxRank: 1,
            parentUpgrade: parent);
    }
}
