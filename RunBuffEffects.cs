using Godot;
using System;

/// <summary>Bounded visual feedback for installed buffs; never changes gameplay.</summary>
public partial class RunBuffEffects : Node3D
{
    public event Action<RunUpgradeId> Activated;
    private Player _player;
    private MeshInstance3D _halo;
    private StandardMaterial3D _material;
    private float _pulse;
    private float _time;
    private int _ranks;
    public override void _Ready()
    {
        _player = GetParent<Player>();
        _material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = BuffUi.Accent,
            NoDepthTest = false, EmissionEnabled = true, Emission = BuffUi.Accent,
            EmissionEnergyMultiplier = .6f };
        _halo = new MeshInstance3D { Name = "BuildHalo", Mesh = new TorusMesh { InnerRadius = .9f, OuterRadius = 1.02f, Rings = 24, RingSegments = 12 },
            MaterialOverride = _material, Position = new Vector3(0, .18f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_halo);
        _player.RunBuffTriggered += OnBuffTriggered;
        _player.DoubleJumped += OnJump;
        _player.DaringDodge += OnDodge;
    }

    public void Refresh(RunUpgradeId newest)
    {
        _ranks = 0;
        foreach (var definition in RunProgression.Catalog) _ranks += _player.GetRunUpgradeRank(definition.Id);
        Color color = RunBuffCatalog.Accent(newest);
        _material.AlbedoColor = color;
        _material.Emission = color;
        _pulse = 1;
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;
        _time += (float)delta;
        _pulse = Mathf.Max(0, _pulse - (float)delta * 1.8f);
        _halo.Visible = _ranks > 0 && _player.EffectsQuality > 0 && _player.Health > 0;
        float scale = 1 + Mathf.Min(_ranks, 12) * .025f + _pulse * 1.8f;
        _halo.Scale = Vector3.One * scale;
        Color color = _material.AlbedoColor;
        color.A = .18f + _pulse * .65f + _player.NitroSpool * .12f;
        _material.AlbedoColor = color;
        _halo.Position = new Vector3(0, .18f + Mathf.Sin(_time * 2) * .035f, 0);
    }

    private void Pulse(RunUpgradeId id)
    {
        _pulse = 1;
        _material.AlbedoColor = RunBuffCatalog.Accent(id);
        _material.Emission = RunBuffCatalog.Accent(id);
        Activated?.Invoke(id);
    }
    private void OnBuffTriggered(int id) => Pulse((RunUpgradeId)id);
    private void OnJump() { if (_player.HasRunUpgrade(RunUpgradeId.Airfoil)) Pulse(RunUpgradeId.Airfoil); }
    private void OnDodge(float reduced, float boost, bool perfect)
    {
        if (perfect && _player.HasRunUpgrade(RunUpgradeId.KineticReserve)) Pulse(_player.HasRunUpgrade(RunUpgradeId.PerfectCircuit) ? RunUpgradeId.PerfectCircuit : RunUpgradeId.KineticReserve);
        else if (_player.HasRunUpgrade(RunUpgradeId.CloseCall)) Pulse(RunUpgradeId.CloseCall);
    }

    public override void _ExitTree()
    {
        if (!IsInstanceValid(_player)) return;
        _player.RunBuffTriggered -= OnBuffTriggered; _player.DoubleJumped -= OnJump;
        _player.DaringDodge -= OnDodge;
    }
}
