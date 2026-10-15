using Godot;
using System;

public enum RunSkillCommand { AirStep, FaultDive, HuntSpark, ThreadRecall }
public sealed record RunSkillDefinition(RunUpgradeId Device,RunSkillCommand Command,string Name,string Instruction,float Cooldown,float Cost);
public static class RunSkillCatalog
{
    public static readonly RunSkillDefinition[] All={
        new(RunUpgradeId.Overpass,RunSkillCommand.AirStep,"AIR STEP","Blink in your steering direction. Cross gaps; walls stop the cast.",4,10),
        new(RunUpgradeId.ShockSole,RunSkillCommand.FaultDive,"FAULT DIVE","Cast in the air to plunge. The landing clears space and interrupts nearby attack warnings.",5,12),
        new(RunUpgradeId.HunterLens,RunSkillCommand.HuntSpark,"HUNT SPARK","Launch a homing spark. It strikes a creep or marks a guardian for a longer opening.",6,12),
        new(RunUpgradeId.RedThread,RunSkillCommand.ThreadRecall,"THREAD RECALL","Plant a safe anchor, then cast again to return. A Rift also plants the anchor.",6,8)
    };
    public static RunSkillDefinition Get(RunUpgradeId device)=>Array.Find(All,x=>x.Device==device);
}
