using Godot;

public partial class HealthHud : PanelContainer
{
    private static readonly Color SafeColor =
        new(0.45f, 0.9f, 1.0f);
    private static readonly Color WarningColor =
        new(1.0f, 0.67f, 0.2f);
    private static readonly Color CriticalColor =
        new(1.0f, 0.3f, 0.22f);

    private Label _healthLabel;
    private ProgressBar _healthBar;
    private RichTextLabel _contractLabel;
    private Player _player;
    private float _health;
    private float _maxHealth = 100.0f;
    private int _recoveryKills;
    private int _recoveryTarget;
    private int _recoverySeconds;

    public override void _Ready()
    {
        _healthLabel = GetNode<Label>(
            "Margin/Content/HealthRow/Health");
        _healthBar = GetNode<ProgressBar>(
            "Margin/Content/HealthRow/Bar");
        _contractLabel = GetNode<RichTextLabel>(
            "Margin/Content/RecoveryReadout/ContractSlot/ContractFrame/Contract");
        _contractLabel.BbcodeEnabled = true;
        _contractLabel.FitContent = false;
        _contractLabel.ScrollActive = false;
        _contractLabel.AutowrapMode =
            TextServer.AutowrapMode.WordSmart;
        BindPlayer();
    }

    public override void _ExitTree()
    {
        if (!IsInstanceValid(_player))
        {
            return;
        }

        _player.HealthChanged -= OnHealthChanged;
        _player.RecoveryContractChanged -=
            OnRecoveryContractChanged;
    }

    private void BindPlayer()
    {
        _player =
            GetTree().GetFirstNodeInGroup("player") as Player;
        if (!IsInstanceValid(_player))
        {
            CallDeferred(MethodName.BindPlayer);
            return;
        }

        _player.HealthChanged += OnHealthChanged;
        _player.RecoveryContractChanged +=
            OnRecoveryContractChanged;
        _health = _player.Health;
        _maxHealth = Mathf.Max(1.0f, _player.MaxHealth);
        _recoveryKills = _player.RecoveryKills;
        _recoveryTarget = _player.RecoveryTarget;
        _recoverySeconds = Mathf.CeilToInt(
            _player.RecoveryTimeRemaining);
        RefreshLabels();
    }

    private void OnHealthChanged(
        float health,
        float maxHealth)
    {
        _health = health;
        _maxHealth = Mathf.Max(1.0f, maxHealth);
        RefreshLabels();
    }

    private void OnRecoveryContractChanged(
        int recoveryKills,
        int recoveryTarget,
        int secondsRemaining)
    {
        _recoveryKills = recoveryKills;
        _recoveryTarget = recoveryTarget;
        _recoverySeconds = Mathf.Max(
            0,
            secondsRemaining);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        float healthRatio = Mathf.Clamp(
            _health / Mathf.Max(1.0f, _maxHealth),
            0.0f,
            1.0f);
        Color stateColor = healthRatio <= 0.3f
            ? CriticalColor
            : healthRatio <= 0.6f
                ? WarningColor
                : SafeColor;
        _healthLabel.Text =
            $"HP   {Mathf.CeilToInt(_health)} / " +
            $"{Mathf.CeilToInt(_maxHealth)}";
        _healthLabel.Modulate = stateColor;
        _healthBar.Value = healthRatio * 100.0f;
        _healthBar.Modulate = stateColor;

        if (_recoveryTarget <= 0)
        {
            _contractLabel.Modulate = Colors.White;
            return;
        }

        string stateHex = Hex(stateColor);
        int displayedSeconds =
            _recoverySeconds > 10
                ? Mathf.CeilToInt(
                    _recoverySeconds / 5.0f) * 5
                : _recoverySeconds;
        _contractLabel.Modulate = Colors.White;
        _contractLabel.Text =
            _player.IsEnduranceRecoveryEnabled
                ? $"[color=#{stateHex}][b]HUNT[/b][/color] " +
                    $"[color=#FFFFFF][b]{_recoveryKills}/{_recoveryTarget}[/b][/color] " +
                    "\n" +
                    "[color=#55D9FF][b]ENDURE[/b][/color] " +
                    $"[color=#FFFFFF][b]{displayedSeconds}s[/b][/color]"
                : $"[color=#{stateHex}][b]HUNT[/b][/color] " +
                    $"[color=#FFFFFF][b]{_recoveryKills}/{_recoveryTarget}[/b][/color]";
    }

    private static string Hex(Color color)
    {
        return color.ToHtml(false);
    }

}
