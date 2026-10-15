using Godot;
using System;
using System.Collections.Generic;

public partial class ArenaTacticsProbe : Node3D
{
    private int _failures;
    public override async void _Ready()
    {
        foreach(ArenaShape shape in Enum.GetValues<ArenaShape>()) {
            var layout=ArenaLayout.Create(shape,30);
            Check(layout.Contains(Vector2.Zero,3),"every footprint has an open center");
            for(int i=0;i<360;i++) {
                float a=i*Mathf.Tau/360;Vector2 point=new Vector2(Mathf.Cos(a)*60,Mathf.Sin(a)*60);
                Check(layout.Contains(layout.Clamp(point,2.2f),2.18f),shape+" clamped targets remain inside the visible footprint");
            }
            foreach(BossId id in Enum.GetValues<BossId>()) {
                var brain=new BossBrain(id,631);var mirror=new BossBrain(id,631);var patterns=new HashSet<BossPattern>();
                Vector3 position=Vector3.Zero;BossPattern? last=null;
                for(int i=0;i<12;i++) {
                    var observation=new BossObservation(position,new Vector3(i%2==0?12:-12,0,4),Vector3.Right*5,i%3, i<6?1:2);
                    var plan=brain.Choose(observation,layout,Vector3.Zero);var replay=mirror.Choose(observation,layout,Vector3.Zero);
                    Check(plan==replay,"AI decisions replay from seed without using global RNG");
                    Check(last==null||last!=plan.Pattern,"guardian does not repeat its last attack");
                    Check(layout.Contains(new Vector2(plan.Destination.X,plan.Destination.Z),5.9f),"boss destination reserves body clearance");
                    Check(plan.Destination.DistanceTo(position)>1,"boss changes position between patterns");
                    patterns.Add(plan.Pattern);position=plan.Destination;last=plan.Pattern;
                }
                Check(patterns.Count==3,id+" uses all three mechanics across arena shapes");
            }
        }
        var player=GD.Load<PackedScene>("res://player.tscn").Instantiate<Player>();AddChild(player);player.SetPhysicsProcess(false);
        var world=new ProceduralWorld();AddChild(world);world.Initialize(player,0);world.TryGetArenaSpan(4,out int finalSection,out float south,out float north);
        var shapes=new HashSet<ArenaShape>();
        foreach(var section in world.GeneratedSections)if(section.IsArena){
            Check(section.ArenaLayout!=null&&section.Root.GetNodeOrNull<ArenaTactics>("ArenaTactics")!=null,"production arenas bind bounds and helpful features");
            shapes.Add(section.ArenaLayout.Shape);
        }
        Check(shapes.Count>=3,"ascent contains multiple real arena footprints");
        for(int i=1;i<=3;i++){world.TryGetArenaSpan(i,out int section,out _,out _);world.CompleteArena(section);}
        player.Position=new Vector3(0,0,south-1);Vector3 approach=player.Position;world.UpdateForPlayer(player);
        Check(!world.ArenaEncounterActive&&player.Position==approach,"entry bridge never snaps the runner into the arena");
        player.Position=new Vector3(0,0,(south+north)/2+6);world.UpdateForPlayer(player);
        Check(world.ArenaEncounterActive,"stepping onto the actual surface starts the encounter");
        player.Position+=Vector3.Right*80;player.Velocity=Vector3.Right*8;world.UpdateForPlayer(player);
        Check(world.ContainsActiveArenaPoint(player.GlobalPosition,1.2f),"sealed play remains inside the real polygon");
        world.CompleteArena(finalSection);Check(!world.ArenaEncounterActive,"victory releases the polygon boundary");
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        GD.Print(_failures==0?"ARENA_TACTICS_PROBE: PASSED":$"ARENA_TACTICS_PROBE: FAILED ({_failures})");GetTree().Quit(_failures==0?0:1);
    }
    private void Check(bool valid,string message){if(!valid){_failures++;GD.PushError("FAIL: "+message);}}
}
