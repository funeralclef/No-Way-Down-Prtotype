using Godot;
using System;
using System.Reflection;

/// <summary>Exercise every guardian through real openings, phase change, missed opportunities and death.</summary>
public partial class BossEncounterProbe : Node3D
{
    private int _failures;
    public override async void _Ready()
    {
        var floor = new StaticBody3D(); floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80,1,80) }, Position = new Vector3(0,-.5f,0) }); AddChild(floor);
        var player = GD.Load<PackedScene>("res://player.tscn").Instantiate<Player>(); AddChild(player); player.SetPhysicsProcess(false);
        var ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; AddChild(ui);
        foreach (BossId id in Enum.GetValues<BossId>()) {
            player.Position = new Vector3(0,0,10);
            var boss = new BossEncounter(); AddChild(boss); boss.Begin(id,player,Vector3.Zero,19,ui,100);
            boss.SetPhysicsProcess(false);
            int victories = 0; boss.Defeated += () => victories++;
            int phaseChanges = 0; boss.PhaseChanged += _ => phaseChanges++;
            Check(!boss.Core.CanBeStomped && boss.Core is Mob, id + " starts shielded and supports existing targeting");
            boss.Core.Squash(1,player); Check(boss.Health == 8,"closed crown ignores damage");
            Check(boss.Core.GetNode<ModelVisualRoot>("Pivot/Character").ModelRoot.SceneFilePath == BossCatalog.ModelPath(id),"guardian owns a unique model");
            for (int hit = 0; hit < 8; hit++) {
                int guard = 0;
                while (!boss.IsExposed && guard++ < 50) {
                    boss._PhysicsProcess(.65);
                    if (id == BossId.Prismweaver) {
                        foreach (Node child in boss.GetChildren())
                            if (child is BossMob seal && seal.IsSeal && !seal.IsQueuedForDeletion() && seal.CanBeStomped) { seal.Squash(1,player); break; }
                    }
                }
                Check(boss.IsExposed, id + " has a reachable opening");
                Check((bool)typeof(Player).GetMethod("IsValidComboRouteTarget", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{boss.Core}),"open crown remains eligible for Rift and assist");
                if (id == BossId.Bellwarden && hit == 0)
                {
                    player.SetArenaCombatActive(true);
                    player.GlobalPosition = boss.Core.GlobalPosition + new Vector3(7, 4, 0);
                    player.Velocity = new Vector3(0, -2, 0);
                    player.SetPhysicsProcess(true);
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    bool rift = (bool)typeof(Player).GetMethod("TryPerformRiftTeleport", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player, null);
                    Check(rift, "real airborne Rift selects an exposed guardian");
                    for (int frame = 0; frame < 120 && boss.Health == 8; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    player.SetPhysicsProcess(false);
                    Check(boss.Health == 7, "Rift arrival physically lands as a damaging boss stomp");
                    continue;
                }
                int hp = boss.Health; boss.Core.Squash(1,player); boss.Core.Squash(1,player);
                Check(boss.Health == hp-1,"one hit per opening prevents contact spam");
                Check(boss.Phase == (boss.Health > 4 ? 1 : 2),"phase changes at exactly half health");
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            Check(!boss.IsActive && victories == 1 && boss.Health == 0 && phaseChanges == 1,"victory and phase transition are emitted once after both phases");
            boss.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        var ring = new BossHazard { Kind = BossHazard.ShapeKind.Ring, Radius = 6, Width = 1, Target = player };
        AddChild(ring); ring.SetPhysicsProcess(false);
        Check(ring.Hits(new Vector3(6,0,0)) && !ring.Hits(new Vector3(6,2,0)) && !ring.Hits(Vector3.Zero),"ring hit test matches rim and allows jumping");
        var lane = new BossHazard { Kind = BossHazard.ShapeKind.Lane, Width = 3, Length = 14, Target = player };
        AddChild(lane); lane.SetPhysicsProcess(false);
        Check(lane.Hits(new Vector3(0,0,5)) && !lane.Hits(new Vector3(4,0,5)),"charge warning matches safe side space");
        GD.Print(_failures == 0 ? "BOSS_ENCOUNTER_PROBE: PASSED" : $"BOSS_ENCOUNTER_PROBE: FAILED ({_failures})");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
    private void Check(bool good,string message) { if (!good) { _failures++; GD.PushError("FAIL: "+message); } else GD.Print("PASS: "+message); }
}
