using Godot;
using System;

/// <summary>
/// Verifies that imported art can be replaced inside visual wrapper scenes
/// without changing gameplay paths, collisions or authored animations.
/// </summary>
public partial class ModelReplacementContractProbe : Node3D
{
    private bool _failed;

    public override async void _Ready()
    {
        Player player = GD.Load<PackedScene>(
                "res://player.tscn")
            .Instantiate<Player>();
        Mob mob = GD.Load<PackedScene>(
                "res://mob.tscn")
            .Instantiate<Mob>();
        player.SetPhysicsProcess(false);
        mob.SetPhysicsProcess(false);
        VisibleOnScreenNotifier3D mobNotifier =
            mob.GetNodeOrNull<VisibleOnScreenNotifier3D>(
                "VisibleOnScreenNotifier3D");
        if (mobNotifier != null)
        {
            mobNotifier.ProcessMode =
                ProcessModeEnum.Disabled;
        }
        mob.ConfigureArchetype(
            EnemyArchetypeId.Springboard);
        AddChild(player);
        AddChild(mob);

        try
        {

        ModelVisualRoot playerVisual =
            player.GetNodeOrNull<ModelVisualRoot>(
                "Pivot/Character");
        ModelVisualRoot mobVisual =
            mob.GetNodeOrNull<ModelVisualRoot>(
                "Pivot/Character");
        Check(
            IsInstanceValid(playerVisual) &&
                playerVisual.SceneFilePath ==
                    "res://art/player_visual.tscn" &&
                playerVisual.ModelRoot?.SceneFilePath ==
                    "res://art/runner_model.tscn" &&
                playerVisual.ValidateContract() &&
                playerVisual.TintTargetCount > 0,
            "player gameplay scene uses a valid replaceable visual wrapper");
        Check(
            IsInstanceValid(mobVisual) &&
                mobVisual.SceneFilePath ==
                    "res://art/mob_visual.tscn" &&
                mobVisual.ModelRoot?.SceneFilePath ==
                    "res://art/enemies/springboard.tscn" &&
                mobVisual.ValidateContract() &&
                mobVisual.CountModelMeshes() > 0,
            "mob gameplay scene uses a valid replaceable visual wrapper");

        CollisionShape3D playerCollision =
            player.GetNode<CollisionShape3D>(
                "CollisionShape3D");
        CollisionShape3D mobCollision =
            mob.GetNode<CollisionShape3D>(
                "CollisionShape3D");
        Vector3 playerCollisionPosition =
            playerCollision.Position;
        Vector3 mobCollisionPosition =
            mobCollision.Position;
        if (IsInstanceValid(playerVisual?.ModelRoot))
        {
            playerVisual.ModelRoot.Position +=
                new Vector3(1.0f, 2.0f, 3.0f);
        }
        if (IsInstanceValid(mobVisual?.ModelRoot))
        {
            mobVisual.ModelRoot.Scale *= 1.25f;
        }
        Check(
            playerCollision.GetParent() == player &&
                mobCollision.GetParent() == mob &&
                playerCollision.Position ==
                    playerCollisionPosition &&
                mobCollision.Position ==
                    mobCollisionPosition,
            "model-local transform changes cannot mutate gameplay collisions");

        AnimationPlayer playerAnimation =
            player.GetNode<AnimationPlayer>(
                "AnimationPlayer");
        AnimationPlayer mobAnimation =
            mob.GetNode<AnimationPlayer>(
                "AnimationPlayer");
        Check(
            AnimationTargetsWrapper(
                playerAnimation,
                "float") &&
                AnimationTargetsWrapper(
                    mobAnimation,
                    "float"),
            "authored animations target the stable Character wrapper");

        Check(
            CountMeshesWithOverride(
                mobVisual?.ModelRoot) ==
                mobVisual?.CountModelMeshes(),
            "mob archetype tint is applied only through the model adapter");

        player.QueueFree();
        mob.QueueFree();
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        GD.Print(
            _failed
                ? "MODEL_REPLACEMENT_CONTRACT_PROBE: FAILED"
                : "MODEL_REPLACEMENT_CONTRACT_PROBE: PASSED");
        GetTree().Quit(
            _failed ? 1 : 0);
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"MODEL_CONTRACT_EXCEPTION: {exception}");
            GetTree().Quit(1);
        }
    }

    private static bool AnimationTargetsWrapper(
        AnimationPlayer player,
        StringName animationName)
    {
        Animation animation =
            player?.GetAnimation(animationName);
        if (animation == null ||
            animation.GetTrackCount() == 0)
        {
            return false;
        }

        for (int track = 0;
            track < animation.GetTrackCount();
            track++)
        {
            string path =
                animation.TrackGetPath(track).ToString();
            if (!path.StartsWith("Pivot/Character:"))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMeshesWithOverride(Node root)
    {
        if (!IsInstanceValid(root))
        {
            return 0;
        }

        int count = 0;
        if (root is MeshInstance3D mesh && mesh.Mesh != null)
        {
            bool adapted = true;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                adapted &= mesh.MaterialOverride != null || mesh.GetSurfaceOverrideMaterial(i) != null;
            count = adapted ? 1 : 0;
        }
        foreach (Node child in root.GetChildren())
        {
            count += CountMeshesWithOverride(child);
        }

        return count;
    }

    private void Check(
        bool condition,
        string description)
    {
        if (condition)
        {
            GD.Print(
                $"MODEL_CONTRACT_ASSERT: PASS - {description}");
            return;
        }

        _failed = true;
        GD.PushError(
            $"MODEL_CONTRACT_ASSERT: FAIL - {description}");
    }
}
