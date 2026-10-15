using Godot;

public partial class RunUpgradeProbe : Node3D
{
    private bool _failed;

    public override async void _Ready()
    {
        PackedScene playerScene =
            GD.Load<PackedScene>(
                "res://player.tscn");
        Player player =
            playerScene.Instantiate<Player>();
        player.Position =
            new Vector3(0.0f, 20.0f, 0.0f);
        AddChild(player);
        await PhysicsFrames(3);

        float baseSpeed = player.CurrentRunSpeed;
        float baseHealth = player.MaxHealth;
        float baseNitro = player.NitroCapacity;
        float baseBounce =
            player.ProjectedBounceLaunchSpeed;

        foreach (RunUpgradeDefinition definition in
            RunProgression.Catalog)
        {
            player.ApplyRunUpgrade(
                definition.Id,
                1);
        }

        Check(
            player.CurrentRunSpeed > baseSpeed,
            "Light Frame raises the authored run pace");
        Check(
            player.MaxHealth >= baseHealth + 14.9f &&
            player.Health > baseHealth,
            "Recovery Mesh expands and fills its health capacity");
        Check(
            player.NitroCapacity >= baseNitro + 19.9f,
            "Deep Tank expands nitro capacity");
        Check(
            player.ProjectedBounceLaunchSpeed > baseBounce,
            "Airfoil changes the live bounce envelope");
        Check(
            player.GetRunUpgradeRank(
                RunUpgradeId.Afterburner) == 1 &&
            player.HasRunUpgrade(
                RunUpgradeId.BlueComet),
            "devices and evolutions are visible to runtime hooks");

        float routeRankOne = player.Call("GetAirComboRouteMaxDistance", 1).AsSingle();
        player.ApplyRunUpgrade(RunUpgradeId.HunterLens, 2);
        float routeRankTwo = player.Call("GetAirComboRouteMaxDistance", 1).AsSingle();
        Check(routeRankTwo > routeRankOne,
            "second device rank extends the live route envelope");

        float stableHealth = player.MaxHealth;
        float stableNitro = player.NitroCapacity;
        player.ApplyRunUpgrade(
            RunUpgradeId.RecoveryMesh,
            1);
        player.ApplyRunUpgrade(
            RunUpgradeId.DeepTank,
            1);
        Check(
            Mathf.IsEqualApprox(
                player.MaxHealth,
                stableHealth) &&
            Mathf.IsEqualApprox(
                player.NitroCapacity,
                stableNitro),
            "duplicate UI delivery cannot double an upgrade");
        Check(
            float.IsFinite(player.CurrentRunSpeed) &&
            float.IsFinite(player.Health) &&
            float.IsFinite(player.NitroCharge),
            "the complete build leaves player resources finite");

        player.QueueFree();
        await PhysicsFrames(3);
        GD.Print(
            _failed
                ? "RUN_UPGRADE_PROBE: FAILED"
                : "RUN_UPGRADE_PROBE: PASSED");
        GetTree().Quit(_failed ? 1 : 0);
    }

    private async System.Threading.Tasks.Task
        PhysicsFrames(int count)
    {
        for (int frame = 0;
            frame < count;
            frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.PhysicsFrame);
        }
    }

    private void Check(
        bool condition,
        string message)
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
