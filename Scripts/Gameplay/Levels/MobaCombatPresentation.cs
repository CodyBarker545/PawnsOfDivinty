using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Read-only presentation snapshot. Never applies damage, changes stats or determines eligibility.</summary>
public sealed record MobaUnitPresentation(string Id,string Name,int HP,int MaxHP,int Shield,bool Defeated)
{
    public bool ShowHealthBar=>!Defeated;
    public bool Pickable=>!Defeated;
    public double HealthFraction=>MaxHP>0?(double)HP/MaxHP:0;
    public string HealthText=>$"{HP}/{MaxHP} HP"+(Shield>0?$" · Shield {Shield}":"");
    public static MobaUnitPresentation Capture(Character c)=>new(c.Id,c.Name,c.CurrentHealth,c.BaseStats.MaxHealth,c.ShieldHP,c.IsDefeated);
}

public enum MobaFeedbackKind { Opening, Counter, FollowUp, Shield, Defeat }
public sealed record MobaCombatEvent(MobaFeedbackKind Kind,string SourceId,string TargetId,string Text,int HealthLost=0,int ShieldAbsorbed=0,bool? Hit=null)
{
    public string FloatingText=>Kind switch
    {
        MobaFeedbackKind.Defeat=>"DEFEATED",
        MobaFeedbackKind.Shield=>$"SHIELD -{ShieldAbsorbed}",
        _=>(Kind==MobaFeedbackKind.Counter?"COUNTER · ":Kind==MobaFeedbackKind.FollowUp?"FOLLOW-UP · ":"")+
            (Hit==true?$"HIT -{HealthLost} HP":"MISS")
    };
}
public sealed record MobaCombatRecord(string RequestId,int Round,IReadOnlyList<MobaCombatEvent> Events,string Summary);

/// <summary>Interprets resolved outcomes, not attack inputs. No RNG, damage formula or damage application.</summary>
public static class MobaCombatFeedback
{
    public static MobaCombatRecord Interpret(string requestId,int round,CombatExchangeResult result,
        MobaUnitPresentation attackerBefore,MobaUnitPresentation defenderBefore,
        MobaUnitPresentation attackerAfter,MobaUnitPresentation defenderAfter)
    {
        var events=new List<MobaCombatEvent>();
        var hp=new Dictionary<string,int>{{attackerBefore.Id,attackerBefore.HP},{defenderBefore.Id,defenderBefore.HP}};
        void Strike(MobaFeedbackKind kind,string label,MobaUnitPresentation source,MobaUnitPresentation target,StrikeResult strike,int healthLost,int shieldAbsorbed=0)
        {
            events.Add(new(kind,source.Id,target.Id,$"{label}: {source.Name} → {target.Name}: {(strike.Hit?"HIT":"MISS")} · -{healthLost} HP",healthLost,Hit:strike.Hit));
            if(shieldAbsorbed>0)events.Add(new(MobaFeedbackKind.Shield,source.Id,target.Id,$"{target.Name}: shield absorbed {shieldAbsorbed} (opening)",ShieldAbsorbed:shieldAbsorbed));
            hp[target.Id]-=healthLost;
            if(hp[target.Id]==0&&healthLost>0)
                events.Add(new(MobaFeedbackKind.Defeat,source.Id,target.Id,$"{target.Name} DEFEATED"));
        }
        Strike(MobaFeedbackKind.Opening,"Attack",attackerBefore,defenderBefore,result.OpeningStrike,result.OpeningHealthLost,result.OpeningShieldAbsorbed);
        if(result.CounterStrike is { } counter)
            Strike(MobaFeedbackKind.Counter,"Counter",defenderBefore,attackerBefore,counter,
                result.AttackerHealthLost-(result.FollowUpBy==FollowUpParticipant.Defender?result.FollowUpHealthLost:0));
        if(result.FollowUpStrike is { } follow)
        {
            bool byAttacker=result.FollowUpBy==FollowUpParticipant.Attacker;
            Strike(MobaFeedbackKind.FollowUp,"Follow-up",byAttacker?attackerBefore:defenderBefore,byAttacker?defenderBefore:attackerBefore,follow,result.FollowUpHealthLost);
        }
        // Production exposes no per-counter/follow-up shield field. Report measured deltas as an
        // exchange summary rather than attempting to reproduce shield/overkill application logic.
        void RemainingShield(MobaUnitPresentation source,MobaUnitPresentation before,MobaUnitPresentation after,int alreadyReported)
        {
            int absorbed=before.Shield-after.Shield-alreadyReported;
            if(absorbed>0)events.Add(new(MobaFeedbackKind.Shield,source.Id,before.Id,$"{before.Name}: shield absorbed {absorbed} (remaining exchange total)",ShieldAbsorbed:absorbed));
        }
        RemainingShield(defenderBefore,attackerBefore,attackerAfter,0);
        RemainingShield(attackerBefore,defenderBefore,defenderAfter,result.OpeningShieldAbsorbed);
        string summary=$"{attackerAfter.Name}: {attackerAfter.HealthText}; {defenderAfter.Name}: {defenderAfter.HealthText}.";
        return new(requestId,round,events.AsReadOnly(),summary);
    }
}

/// <summary>Append-only chronological presentation journal for this session. Fresh session = empty journal.</summary>
public sealed class MobaCombatJournal
{
    private readonly List<MobaCombatRecord> _records=new();
    public IReadOnlyList<MobaCombatRecord> Records {get;}
    public MobaCombatJournal()=>Records=_records.AsReadOnly();
    internal void Append(MobaCombatRecord record)=>_records.Add(record);
    public string DisplayText()=>string.Join("\n\n",_records.Select((r,i)=>
        $"#{i+1} · Round {r.Round}\n"+string.Join("\n",r.Events.Select(e=>e.Text))+"\n"+r.Summary));
}
