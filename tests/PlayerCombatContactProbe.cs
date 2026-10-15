using Godot;
using System.Reflection;

public partial class PlayerCombatContactProbe : Node3D
{
    private int _failures;
    private int _damageEvents;
    private int _comboEndEvents;

    public override async void _Ready()
    {
        Player player =
            GD.Load<PackedScene>(
                    "res://player.tscn")
                .Instantiate<Player>();
        AddChild(player);
        player.SetPhysicsProcess(false);
        player.DamageTaken +=
            (_, _) => _damageEvents++;
        player.ComboEnded +=
            (_, _) => _comboEndEvents++;

        PlayerComboState combo =
            (PlayerComboState)typeof(Player)
                .GetField(
                    "_comboState",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                ?.GetValue(player);
        combo?.AdvanceStomp(0.36f);
        combo?.AdvanceStomp(0.36f);

        Node3D enemyContact = new()
        {
            Position = Vector3.Right
        };
        AddChild(enemyContact);
        float healthBefore = player.Health;
        player.TakeDamage(enemyContact);
        float healthAfterFirst = player.Health;
        player.TakeDamage(enemyContact);

        Check(
            Mathf.IsEqualApprox(
                healthBefore -
                    healthAfterFirst,
                player.EnemyContactDamage) &&
            Mathf.IsEqualApprox(
                player.Health,
                healthAfterFirst),
            "repeat enemy contact removes health only once during invulnerability");
        Check(
            _damageEvents == 1 &&
            _comboEndEvents == 1 &&
            player.AirStompChain == 0,
            "repeat contact emits damage and combo side effects only once");
        Check(
            player.IsDamageInvulnerable &&
            player.Velocity.X < 0.0f &&
            float.IsFinite(
                player.Velocity.Length()),
            "the accepted contact applies one finite separation impulse");

        player.QueueFree();
        enemyContact.QueueFree();
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame);
        GD.Print(
            _failures == 0
                ? "PLAYER_COMBAT_CONTACT_PROBE: PASSED"
                : $"PLAYER_COMBAT_CONTACT_PROBE: FAILED ({_failures})");
        GetTree().Quit(
            _failures == 0 ? 0 : 1);
    }

    private void Check(
        bool condition,
        string description)
    {
        if (condition)
        {
            GD.Print($"PASS: {description}");
            return;
        }
        _failures++;
        GD.PushError($"FAIL: {description}");
    }
}
