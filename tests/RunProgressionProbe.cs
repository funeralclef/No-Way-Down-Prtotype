using Godot;
using System;
using System.Collections.Generic;

public partial class RunProgressionProbe : Node
{
    private bool _failed;

    public override void _Ready()
    {
        TestCatalogShape();
        TestCadenceAndEvolutionLegality();
        TestRerollContract();
        TestDeterministicSeed();
        TestSelectionGuards();

        GD.Print(
            _failed
                ? "RUN_PROGRESSION_PROBE: FAILED"
                : "RUN_PROGRESSION_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
    }

    private void TestCatalogShape()
    {
        int impulses = 0;
        int devices = 0;
        int evolutions = 0;
        HashSet<RunUpgradeId> ids = new();
        foreach (RunUpgradeDefinition definition in
            RunProgression.Catalog)
        {
            ids.Add(definition.Id);
            switch (definition.Tier)
            {
                case RunRewardTier.Impulse:
                    impulses++;
                    break;
                case RunRewardTier.Device:
                    devices++;
                    break;
                case RunRewardTier.Evolution:
                    evolutions++;
                    Check(
                        definition.ParentUpgrade.HasValue &&
                        RunProgression
                            .GetDefinition(
                                definition.ParentUpgrade.Value)
                            .Tier == RunRewardTier.Device,
                        $"{definition.Name} has a device parent");
                    break;
            }
        }

        Check(
            RunProgression.Catalog.Count == 28 &&
            ids.Count == 28 &&
            impulses == 12 &&
            devices == 8 &&
            evolutions == 8,
            "catalog contains 12 impulses, 8 devices and 8 evolutions");
    }

    private void TestCadenceAndEvolutionLegality()
    {
        RunProgression progression = new(0xC0FFEEUL);
        for (int arena = 0; arena < 40; arena++)
        {
            progression.BeginArenaDraft();
            CheckDraft(progression, RunRewardTier.Device, $"arena {arena + 1}");
            foreach (var offer in progression.CurrentRewardOffers)
                Check(offer.Tier == RunRewardTier.Device, "arena rewards never offer passive stat boosts or direct evolutions");
            var selected = progression.SelectReward(0);
            Check(progression.LastGrantedCatalyst.HasValue && progression.GetRank(progression.LastGrantedCatalyst.Value) > 0,
                "installing a device also grants its matching catalyst");
            Check(progression.GetRank(selected.Id) <= selected.MaxRank, "long runs remain rank capped");
            CheckRoutes(progression, "device reward");
            progression.SelectRoute(0);
            foreach (var recipe in RunBuffCatalog.Recipes)
                if (progression.CanMerge(recipe.Evolution))
                    Check(progression.TryMerge(recipe.Evolution), "evolutions are acquired through merge");
        }
    }

    private void TestRerollContract()
    {
        RunProgression progression =
            new(4815162342UL);
        progression.BeginArenaDraft();
        HashSet<RunUpgradeId> original =
            OfferIds(progression);
        Check(
            progression.CanReroll &&
            progression.RerollRewards(),
            "one reroll is available at the start of a chapter");
        HashSet<RunUpgradeId> rerolled =
            OfferIds(progression);
        original.IntersectWith(rerolled);
        Check(
            original.Count == 0,
            "reroll anti-repeat produces a fresh three-card draft");
        Check(
            !progression.CanReroll &&
            !progression.RerollRewards(),
            "reroll cannot be repeated in the same chapter");

        progression.SelectReward(0);
        Check(
            !progression.RerollRewards(),
            "reward selection permanently closes chapter rerolling");
    }

    private void TestDeterministicSeed()
    {
        RunProgression first = new(9127UL);
        RunProgression second = new(9127UL);
        for (int arena = 0; arena < 6; arena++)
        {
            first.BeginArenaDraft();
            second.BeginArenaDraft();
            Check(
                SameOffers(first, second),
                $"seeded reward draft {arena + 1} is deterministic");
            first.SelectReward(0);
            second.SelectReward(0);
            Check(
                SameRoutes(first, second),
                $"seeded route draft {arena + 1} is deterministic");
            first.SelectRoute(0);
            second.SelectRoute(0);
        }
    }

    private void TestSelectionGuards()
    {
        RunProgression progression = new(77UL);
        bool routeBeforeRewardRejected = false;
        progression.BeginArenaDraft();
        try
        {
            progression.SelectRoute(0);
        }
        catch (InvalidOperationException)
        {
            routeBeforeRewardRejected = true;
        }
        Check(
            routeBeforeRewardRejected,
            "route selection is rejected before reward selection");

        progression.SelectReward(0);
        progression.SelectRoute(0);
        bool secondRouteRejected = false;
        try
        {
            progression.SelectRoute(1);
        }
        catch (InvalidOperationException)
        {
            secondRouteRejected = true;
        }
        Check(
            secondRouteRejected,
            "a chapter route can only be selected once");
    }

    private void CheckDraft(
        RunProgression progression,
        RunRewardTier expectedTier,
        string context)
    {
        Check(
            progression.CurrentRewardTier == expectedTier,
            $"{context} uses {expectedTier} cadence");
        HashSet<RunUpgradeId> ids =
            OfferIds(progression);
        Check(
            progression.CurrentRewardOffers.Count == 3 &&
            ids.Count == 3,
            $"{context} offers three unique rewards");
        Check(
            progression.CurrentRewardOffers[0].Tier == RunRewardTier.Device,
            $"{context} starts with a gameplay device");
    }

    private void CheckRoutes(
        RunProgression progression,
        string context)
    {
        Check(
            progression.CurrentRouteOffers.Count == 2 &&
            progression.CurrentRouteOffers[0] !=
                progression.CurrentRouteOffers[1] &&
            progression.CurrentRouteOffers[0] !=
                RunRouteStyle.Neutral &&
            progression.CurrentRouteOffers[1] !=
                RunRouteStyle.Neutral,
            $"{context} offers two distinct authored routes after its reward");
    }

    private static int FindTierOffer(
        RunProgression progression,
        RunRewardTier tier)
    {
        for (int index = 0;
            index < progression.CurrentRewardOffers.Count;
            index++)
        {
            if (progression.CurrentRewardOffers[index].Tier == tier)
            {
                return index;
            }
        }
        return -1;
    }

    private static HashSet<RunUpgradeId> OfferIds(
        RunProgression progression)
    {
        HashSet<RunUpgradeId> ids = new();
        foreach (RunUpgradeDefinition offer in
            progression.CurrentRewardOffers)
        {
            ids.Add(offer.Id);
        }
        return ids;
    }

    private static bool SameOffers(
        RunProgression first,
        RunProgression second)
    {
        if (first.CurrentRewardOffers.Count !=
            second.CurrentRewardOffers.Count)
        {
            return false;
        }
        for (int index = 0;
            index < first.CurrentRewardOffers.Count;
            index++)
        {
            if (first.CurrentRewardOffers[index].Id !=
                second.CurrentRewardOffers[index].Id)
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameRoutes(
        RunProgression first,
        RunProgression second)
    {
        return first.CurrentRouteOffers.Count == 2 &&
            second.CurrentRouteOffers.Count == 2 &&
            first.CurrentRouteOffers[0] ==
                second.CurrentRouteOffers[0] &&
            first.CurrentRouteOffers[1] ==
                second.CurrentRouteOffers[1];
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            GD.Print($"PASS: {message}");
            return;
        }

        _failed = true;
        GD.PushError($"FAIL: {message}");
    }
}
