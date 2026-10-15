using Godot;
using System;

/// <summary>Exercises real seeded drafts, merge transactions and modifier reversibility.</summary>
public partial class RunMergeProbe : Node
{
    private bool _failed;
    public override void _Ready()
    {
        foreach (var recipe in RunBuffCatalog.Recipes) TestRecipe(recipe);
        var build = new PlayerRunBuild();
        build.ConfigureBaseStats(100, 80);
        build.SetRank(RunUpgradeId.RecoveryMesh, 99);
        Check(build.GetRank(RunUpgradeId.RecoveryMesh) == 3 && build.MaxHealth == 145, "rank clamps to catalog cap");
        build.SetRank(RunUpgradeId.RecoveryMesh, 1);
        Check(build.MaxHealth == 115, "consuming ranks rebuilds modifiers");
        build.SetRank(RunUpgradeId.RecoveryMesh, 0);
        Check(build.MaxHealth == 100, "removing a catalyst restores base capacity");
        build.SetRank(RunUpgradeId.DeepTank, 2);
        build.SetRank(RunUpgradeId.DeepTank, 0);
        Check(build.NitroCapacity == 80, "nitro modifiers also reverse");
        GD.Print(_failed ? "RUN_MERGE_PROBE: FAILED" : "RUN_MERGE_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }

    private void TestRecipe(RunMergeRecipe recipe)
    {
        var progression = new RunProgression(0xB0FFUL + (ulong)recipe.Evolution);
        Check(!progression.TryMerge(recipe.Evolution), recipe.Evolution + " rejects missing ingredients");
        for (int arena = 0; arena < 180 && !progression.CanMerge(recipe.Evolution); arena++)
        {
            progression.BeginArenaDraft();
            int selected = -1;
            for (int i = 0; i < progression.CurrentRewardOffers.Count; i++)
            {
                var offer = progression.CurrentRewardOffers[i];
                if (offer.Id == recipe.Catalyst || offer.Id == recipe.Device) { selected = i; break; }
                if (offer.Tier != RunRewardTier.Evolution) selected = i;
            }
            if (selected < 0) selected = 0;
            // An evolution reward is an alternative to merging. Never take
            // the target recipe from a draft while testing the crafting path.
            if (progression.CurrentRewardOffers[selected].Id == recipe.Evolution)
                selected = (selected + 1) % progression.CurrentRewardOffers.Count;
            progression.SelectReward(selected);
            Check(!progression.TryMerge(recipe.Evolution), "merge is blocked during route selection");
            progression.SelectRoute(0);
        }
        int before = progression.GetRank(recipe.Catalyst);
        bool merged = progression.TryMerge(recipe.Evolution);
        if (!merged) GD.Print($"MERGE STATE {recipe.Evolution}: catalyst={before}, device={progression.GetRank(recipe.Device)}, evolution={progression.GetRank(recipe.Evolution)}");
        Check(merged, recipe.Evolution + " becomes craftable through normal drafts");
        Check(progression.HasUpgrade(recipe.Device) && progression.GetRank(recipe.Evolution) == 1 &&
            progression.GetRank(recipe.Catalyst) == before - 2, recipe.Evolution + " consumes exactly two catalyst ranks and keeps device");
        int after = progression.GetRank(recipe.Catalyst);
        Check(!progression.TryMerge(recipe.Evolution) && progression.GetRank(recipe.Catalyst) == after,
            recipe.Evolution + " duplicate merge is a no-op");
    }

    private void Check(bool value, string message)
    {
        if (!value) { _failed = true; GD.PushError("FAIL: " + message); }
    }
}
