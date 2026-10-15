using Godot;
using System;
using System.Collections.Generic;

/// <summary>Attack scheduling and bounded hazard lifetime. The encounter decides when an opening occurs.</summary>
public partial class BossAttacks : Node3D
{
    private readonly List<(float At,Action Fire)> _pending=new();
    private readonly List<BossHazard> _live=new();
    private float _time;
    public Player Target {get;set;}
    public Color Tint {get;set;}
    public void Tick(float dt)
    {
        _time+=dt;
        for(int i=_pending.Count-1;i>=0;i--)if(_pending[i].At<=_time){var action=_pending[i].Fire;_pending.RemoveAt(i);action();}
        _live.RemoveAll(h=>!IsInstanceValid(h));
    }
    public void After(float delay,Action action){_pending.Add((_time+delay,action));}
    public BossHazard Ring(Vector3 point,float reach,float warning=1.05f)
        =>Spawn(new BossHazard {Kind=BossHazard.ShapeKind.Ring,Position=point,Radius=2.5f,Width=1.05f,WarningTime=warning,ActiveTime=reach/9,ExpansionSpeed=9});
    public BossHazard Lane(Vector3 from,Vector3 to,float warning=1.15f,float active=.6f,float width=2.7f)
    {
        Vector3 delta=to-from;
        var h=Spawn(new BossHazard {Kind=BossHazard.ShapeKind.Lane,Position=(from+to)/2,Width=width,Length=Mathf.Max(1,delta.Length()),WarningTime=warning,ActiveTime=active});
        h.Rotation=new Vector3(0,Mathf.Atan2(delta.X,delta.Z),0);return h;
    }
    public BossHazard Disc(Vector3 point,float radius=3,float warning=1.25f,float active=.5f)
        =>Spawn(new BossHazard {Kind=BossHazard.ShapeKind.Disc,Position=point,Radius=radius,WarningTime=warning,ActiveTime=active});
    private BossHazard Spawn(BossHazard hazard)
    {hazard.Target=Target;hazard.Tint=Tint;AddChild(hazard);_live.Add(hazard);return hazard;}
    public void Clear()
    { _pending.Clear();foreach(var h in _live)if(IsInstanceValid(h))h.QueueFree();_live.Clear(); }
}
