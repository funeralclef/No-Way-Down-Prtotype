using Godot;
using System;
using System.Collections.Generic;

/// <summary>Shared recipes and player-facing buff language, independent of views.</summary>
public sealed record RunMergeRecipe(RunUpgradeId Evolution, RunUpgradeId Device, RunUpgradeId Catalyst)
{
    public const int CatalystCost = 2;
}

public static class RunBuffCatalog
{
    public static IReadOnlyList<RunMergeRecipe> Recipes { get; } = Array.AsReadOnly(new[]
    {
        new RunMergeRecipe(RunUpgradeId.BlueComet, RunUpgradeId.Afterburner, RunUpgradeId.CoolBurn),
        new RunMergeRecipe(RunUpgradeId.ChainReaction, RunUpgradeId.RedThread, RunUpgradeId.LongStep),
        new RunMergeRecipe(RunUpgradeId.EarthSplitter, RunUpgradeId.ShockSole, RunUpgradeId.SteadyPulse),
        new RunMergeRecipe(RunUpgradeId.LastLight, RunUpgradeId.GlassHeart, RunUpgradeId.SecondWind),
        new RunMergeRecipe(RunUpgradeId.TwoHorizons, RunUpgradeId.HunterLens, RunUpgradeId.Airfoil),
        new RunMergeRecipe(RunUpgradeId.PhaseRoute, RunUpgradeId.Overpass, RunUpgradeId.QuickSpark),
        new RunMergeRecipe(RunUpgradeId.PerfectCircuit, RunUpgradeId.KineticReserve, RunUpgradeId.CloseCall),
        new RunMergeRecipe(RunUpgradeId.InfiniteLine, RunUpgradeId.ClosedLoop, RunUpgradeId.FinisherCell)
    });

    public static RunMergeRecipe GetRecipe(RunUpgradeId evolution)
    {
        foreach (var recipe in Recipes) if (recipe.Evolution == evolution) return recipe;
        return null;
    }

    public static Color Accent(RunUpgradeId id) => RunProgression.GetDefinition(id).Tier switch
    {
        RunRewardTier.Device => new Color("#f5b65b"),
        RunRewardTier.Evolution => new Color("#bd9dff"),
        _ => new Color("#67dcca")
    };

    public static string Icon(RunUpgradeId id)
    {
        return id switch
        {
            RunUpgradeId.LightFrame or RunUpgradeId.Afterburner or RunUpgradeId.BlueComet => "res://no-padding icons/4-nature/wind.svg",
            RunUpgradeId.QuickSpark or RunUpgradeId.SteadyPulse or RunUpgradeId.KineticReserve or RunUpgradeId.PerfectCircuit => "res://no-padding icons/4-nature/lightning.svg",
            RunUpgradeId.HunterLens or RunUpgradeId.TwoHorizons or RunUpgradeId.LongStep => "res://no-padding icons/6-buildings/target-02.svg",
            RunUpgradeId.SecondWind or RunUpgradeId.RecoveryMesh or RunUpgradeId.GlassHeart or RunUpgradeId.LastLight => UiIconLibrary.Health,
            RunUpgradeId.CoolBurn => "res://no-padding icons/4-nature/fire.svg",
            RunUpgradeId.DeepTank or RunUpgradeId.ClosedLoop or RunUpgradeId.InfiniteLine => UiIconLibrary.Nitro,
            RunUpgradeId.Airfoil or RunUpgradeId.Overpass or RunUpgradeId.PhaseRoute => "res://no-padding icons/1-game/double.svg",
            RunUpgradeId.FinisherCell or RunUpgradeId.RedThread or RunUpgradeId.ChainReaction => UiIconLibrary.Trophy,
            _ => UiIconLibrary.Loadout
        };
    }

    public static string RankPips(RunUpgradeId id, int rank)
    {
        int max = RunProgression.GetDefinition(id).MaxRank;
        rank = Mathf.Clamp(rank, 0, max);
        return new string('●', rank) + new string('○', max - rank);
    }

    // Totals at the selected rank make repeat rewards understandable.
    public static string Effect(RunUpgradeId id, int rank)
    {
        rank = Mathf.Clamp(rank, 0, RunProgression.GetDefinition(id).MaxRank);
        return id switch
        {
            RunUpgradeId.LightFrame => $"+{rank} speed · +{rank * 6}% acceleration",
            RunUpgradeId.DeepTank => $"+{rank * 20} nitro capacity · new charge on pickup",
            RunUpgradeId.SoftLanding => $"{Mathf.RoundToInt((1 - Mathf.Pow(.75f, rank)) * 100)}% less ground momentum loss · longer combo grace",
            RunUpgradeId.LongStep => $"+{rank * 2.25f:0.##} combo reach · wider route adjustment",
            RunUpgradeId.SecondWind => $"At critical health: every {Mathf.Max(2, 5 - rank)} kills restore 5 health",
            RunUpgradeId.QuickSpark => $"-{rank * 1.5f:0.#} burst cost · -{rank * .035f:0.###}s cooldown",
            RunUpgradeId.Airfoil => $"+{rank * 8}% double-jump carry · +{rank * 4}% bounce",
            RunUpgradeId.CoolBurn => $"{Mathf.RoundToInt((1 - Mathf.Pow(.9f, rank)) * 100)}% less reactor drain",
            RunUpgradeId.SteadyPulse => $"+{rank * 12}% stomp momentum · slower momentum decay",
            RunUpgradeId.RecoveryMesh => $"+{rank * 15} maximum health · new capacity heals immediately",
            RunUpgradeId.CloseCall => $"+{rank * .08f:0.##}s dodge window · +{rank * 2} slipstream nitro",
            RunUpgradeId.FinisherCell => $"-{rank * 12} finisher threshold · +{rank * 6} finisher nitro",
            RunUpgradeId.Afterburner => "Release thrust to leave a wind lane. Crossing it restores pace and nitro. Blue Comet also erases nearby attack warnings.",
            RunUpgradeId.GlassHeart => "A visible glass guard absorbs one enemy hit. Three stomps rebuild it. Last Light breaks into a warning-clearing pulse.",
            RunUpgradeId.HunterLens => "Unlock HUNT SPARK: a homing drone strikes a creep or marks a guardian, extending its next opening. Equip in BUILD.",
            RunUpgradeId.KineticReserve => "Perfect dodges release a counterwave that separates creeps and interrupts nearby warnings; your next stomp releases stored energy.",
            RunUpgradeId.ClosedLoop => "Every fifth combo stomp creates a single-use Echo Step ahead. Jump or Rift onto it to bridge an empty route.",
            RunUpgradeId.ShockSole => "Unlock FAULT DIVE: cast airborne to plunge and interrupt nearby attack warnings on impact. Hard landings still push enemies apart.",
            RunUpgradeId.RedThread => "Unlock THREAD RECALL: plant a five-second anchor, then cast again to return. Rift creates an anchor automatically.",
            RunUpgradeId.Overpass => "Unlock AIR STEP: blink in your steering direction without needing an enemy. Walls and arena edges block unsafe casts.",
            _ => RunProgression.GetDefinition(id).Description
        };
    }

    public static string RecipeText(RunMergeRecipe recipe) =>
        $"{RunProgression.GetDefinition(recipe.Device).Name} + {RunProgression.GetDefinition(recipe.Catalyst).Name} II";
}
