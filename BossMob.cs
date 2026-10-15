using Godot;

/// <summary>Adapter lets existing jump, assist, collision and Rift targeting work on guardian weak points.</summary>
public partial class BossMob : Mob
{
    public BossEncounter Encounter { get; set; }
    public int SealIndex { get; set; } = -1;
    public bool IsSeal => SealIndex >= 0;
    public override float MinimumRiftArrivalHeight => IsSeal ? 2.4f : 3.8f;
    public override bool CanBeRouteTarget => CanBeStomped || CanDamagePlayer;
    private int _presentedPhase;
    private bool _presentedOpen;
    public void PresentCrown(bool open, int phase)
    {
        if (IsSeal || (_presentedPhase == phase && _presentedOpen == open)) return;
        _presentedPhase = phase; _presentedOpen = open;
        Color tint = open ? new Color("#a6ffe3") : phase == 2 ? new Color("#ff7c9e") : Encounter.Definition.Color;
        var material = new StandardMaterial3D { AlbedoColor = tint, EmissionEnabled = true,
            Emission = tint, EmissionEnergyMultiplier = open ? 1.4f : phase == 2 ? .85f : .4f, Roughness = .36f };
        GetNode<ModelVisualRoot>("Pivot/Character").ApplySurfacePalette(
            new System.Collections.Generic.Dictionary<string,Material> { ["accent"] = material }, null);
    }
    public override bool CanBeStomped => IsInstanceValid(Encounter) && Encounter.CanStomp(this);
    public override bool CanDamagePlayer => !IsSeal && IsInstanceValid(Encounter) && Encounter.IsCharging;
    public override bool WillBeDefeatedByNextStomp => CanBeStomped && (IsSeal || Encounter.Health == 1);

    public override void _Ready()
    {
        base._Ready();
        GetNode<AnimationPlayer>("AnimationPlayer").Stop();
        var pivot = GetNode<Node3D>("Pivot");
        pivot.Scale = IsSeal ? Vector3.One * .95f : new Vector3(2.65f, 1.85f, 2.65f);
        pivot.Position = new Vector3(0, .12f, 0);
        var adapter = GetNode<ModelVisualRoot>("Pivot/Character");
        adapter.ReplaceModel(GD.Load<PackedScene>(IsSeal ? "res://art/bosses/seal.tscn" : BossCatalog.ModelPath(Encounter.Definition.Id)));
        GetNode<CollisionShape3D>("CollisionShape3D").Shape = new CylinderShape3D {
            Radius = IsSeal ? .72f : 2.05f, Height = IsSeal ? 1 : 3 };
        GetNode<CollisionShape3D>("CollisionShape3D").Position = new Vector3(0, IsSeal ? .65f : 1.5f, 0);
        GetNode<VisibleOnScreenNotifier3D>("VisibleOnScreenNotifier3D").Hide();
    }
    public void SetMotion(Vector3 velocity) { Velocity = velocity; }
    public override void _PhysicsProcess(double delta) { }
    protected override void OnVisibleOnScreenNotifierScreenExited() { }
    public override void Squash(int rewardMultiplier = 1, Player stompingPlayer = null)
    {
        if (CanBeStomped) Encounter.ReceiveStomp(this, stompingPlayer);
    }
}
