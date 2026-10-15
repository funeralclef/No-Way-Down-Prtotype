using Godot;
using System.Collections.Generic;

public enum BossPattern { ResonantMarch, ChimeFan, EchoChase, HornRush, ThornGarden, BrambleSweep, SealOrbit, MirrorExchange, PrismLattice }
public readonly record struct BossObservation(Vector3 Boss,Vector3 Player,Vector3 Velocity,float StillTime,int Phase);
public readonly record struct BossPlan(BossPattern Pattern,Vector3 Destination,int Handedness);

/// <summary>Scene-free, seeded tactical policy. Observes position and recent choices, never reads future input.</summary>
public sealed class BossBrain
{
    private readonly RandomNumberGenerator _random=new();
    private readonly Queue<BossPattern> _recent=new();
    private readonly Dictionary<BossPattern,int> _lastUsed=new();
    private readonly BossId _id;
    private int _turn;
    public BossBrain(BossId id,ulong seed){_id=id;_random.Seed=seed;}
    public BossPlan Choose(BossObservation observation,ArenaLayout layout,Vector3 center)
    {
        BossPattern[] pool=_id switch {
            BossId.Bellwarden=>new[]{BossPattern.ResonantMarch,BossPattern.ChimeFan,BossPattern.EchoChase},
            BossId.Thornmaw=>new[]{BossPattern.HornRush,BossPattern.ThornGarden,BossPattern.BrambleSweep},
            _=>new[]{BossPattern.SealOrbit,BossPattern.MirrorExchange,BossPattern.PrismLattice}};
        float distance=(observation.Player-observation.Boss).Length();
        Vector3 toBoss=observation.Boss-observation.Player;toBoss.Y=0;
        float lateralSpeed=Mathf.Abs(Vector3.Up.Cross(toBoss.Normalized()).Dot(observation.Velocity));
        BossPattern best=pool[0];float bestScore=float.MinValue;
        foreach(var pattern in pool) {
            float score=_random.Randf()*1.5f;
            score+=Mathf.Min(1.4f,(_turn-_lastUsed.GetValueOrDefault(pattern,-3))*.18f);
            if(_recent.Contains(pattern))score-=3.5f;
            if(pattern==BossPattern.EchoChase&&distance>15)score+=1;
            if(pattern==BossPattern.HornRush&&distance>10)score+=.8f;
            if(pattern is BossPattern.ThornGarden or BossPattern.PrismLattice&&observation.StillTime>1.4f)score+=1;
            if(pattern is BossPattern.ChimeFan or BossPattern.BrambleSweep&&lateralSpeed>4)score+=.8f;
            if(observation.Phase==1&&pattern==pool[2])score-=.6f;
            if(score>bestScore){bestScore=score;best=pattern;}
        }
        _recent.Enqueue(best);if(_recent.Count>1)_recent.Dequeue();
        _lastUsed[best]=_turn;
        int sign=_turn++%2==0?1:-1;
        Vector3 destination=observation.Boss;float targetScore=float.MinValue;
        for(int i=0;i<8;i++) {
            Vector3 candidate=layout.Anchor((i+_turn)%8,center,6);
            float fromBoss=candidate.DistanceTo(observation.Boss),fromPlayer=candidate.DistanceTo(observation.Player);
            float score=Mathf.Min(fromBoss,18)*.4f-Mathf.Abs(fromPlayer-12)*.22f;
            if((candidate-center).X*sign>0)score+=1;
            if(score>targetScore){targetScore=score;destination=candidate;}
        }
        return new BossPlan(best,destination,sign);
    }
}
