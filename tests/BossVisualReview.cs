using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Render every guardian and phase inside the actual final biome, camera and HUD.</summary>
public partial class BossVisualReview : Node
{
    public override async void _Ready()
    {
        await Frames(4);
        var game = GetParent<Main>();
        var player = game.GetNode<Player>("Player");
        var world = game.GetNode<ProceduralWorld>("RunSystems/ProceduralWorld");
        player.SetPhysicsProcess(false); player.GrantDamageInvulnerability(100);
        for(int i=1;i<=3;i++){world.TryGetArenaSpan(i,out int s,out _,out _);world.CompleteArena(s);}
        world.TryGetArenaSpan(4,out int section,out float south,out float north);
        player.Position = new Vector3(0,0,(south+north)/2+8); world.UpdateForPlayer(player);
        world.TryGetArenaCircle(section,out Vector3 center,out float radius);
        world.TryGetArenaLayout(section,out var layout,out _);
        var tactics=world.GetNode<Node3D>($"Section{section}").GetNodeOrNull<ArenaTactics>("ArenaTactics");
        player.ApplyRunUpgrade(RunUpgradeId.Overpass,1);player.ApplyRunUpgrade(RunUpgradeId.GlassHeart,1);
        var field = typeof(Main).GetField("_bossEncounter",BindingFlags.Instance|BindingFlags.NonPublic);
        var initial = field.GetValue(game) as BossEncounter; initial?.Stop(); initial?.QueueFree(); await Frames(2);
        foreach(BossId id in Enum.GetValues<BossId>()) {
            var boss = new BossEncounter { Name = "ReviewGuardian" }; game.AddChild(boss);
            field.SetValue(game,boss); boss.Begin(id,player,center,radius,game.GetNode<Control>("UserInterface"),101,layout,tactics,(ulong)id+73);
            boss.SetPhysicsProcess(false); boss._PhysicsProcess(2.9);
            for(int i=0;i<60&&!boss.IsPerformingPattern;i++)boss._PhysicsProcess(.1);
            boss._PhysicsProcess(.15);
            await Seconds(1.2);
            await Capture("/tmp/nwd-boss-"+id+"-I.png");
            for(int hit=0;hit<4;hit++) {
                for(int step=0;!boss.IsExposed&&step<60;step++) {
                    boss._PhysicsProcess(.7);
                    foreach(Node child in boss.GetChildren())
                        if(child is BossMob seal&&seal.IsSeal&&!seal.IsQueuedForDeletion()&&seal.CanBeStomped){seal.Squash(1,player);break;}
                }
                boss.Core.Squash(1,player); await Frames(1);
            }
            boss._PhysicsProcess(2.8);for(int i=0;i<60&&!boss.IsPerformingPattern;i++)boss._PhysicsProcess(.1);boss._PhysicsProcess(.15);await Seconds(.25);
            await Capture("/tmp/nwd-boss-"+id+"-II.png");
            boss.Stop(); boss.QueueFree(); await Frames(3);
        }
        GD.Print("BOSS_VISUAL_REVIEW: PASSED"); GetTree().Quit();
    }
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Seconds(double count){await ToSignal(GetTree().CreateTimer(count),SceneTreeTimer.SignalName.Timeout);}
    private async Task Capture(string path){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(path);}
}
