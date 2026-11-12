using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Gameplay.Levels;

public sealed record MobaAttackPreview(BattlefieldOutcome Outcome,string Reason,string? ActorId,string TargetId,
    long? Distance=null,int? MinimumRange=null,int? MaximumRange=null,int? HitChance=null,int? EstimatedOpeningDamage=null)
{
    public bool CanExecute=>Outcome==BattlefieldOutcome.Ready;
}

public sealed partial class MobaHotseatSession
{
    private AttackActionExecutor _attacks=null!;
    private Dictionary<string,AttackConfiguration> _loadouts=null!;
    private readonly HashSet<string> _attackAttempts=new(StringComparer.Ordinal);
    private bool _resolvingAttack,_developmentOpenTargeting;
    public MobaCombatJournal CombatHistory {get;}=new();
    public bool CanResetMatch=>!_resolvingAttack&&_commandDepth==0&&!_processingTimeout;
    /// <summary>Fresh domain/session objects from the original scene snapshot, not a mutation/rollback.</summary>
    public MobaHotseatSession CreateResetSession(IRandomRollProvider? random=null,IActivationTimeSource? timeSource=null)
    {
        if(!CanResetMatch)throw new InvalidOperationException("Wait for combat resolution before resetting.");
        return new(_layout,StartingPlayer,random,_developmentOpenTargeting,Victory.Profile,ActivationTimer.Configuration,timeSource??_timeSource,AssassinProfile);
    }
    public bool CanAttack=>SelectedCharacter is { } c&&CanSelect(c.Id)
        &&(Activations.ActiveCharacter is null||Activations.CanUseMainAction);

    private void InitializeCombat(CombatExchangeResolver combat,bool developmentOpenTargeting)
    {
        _attacks=new(combat);_developmentOpenTargeting=developmentOpenTargeting;
        // Reuse the exact existing development encounter configuration, including its unresolved balance labels.
        _loadouts=Characters.Values.ToDictionary(c=>c.Id,BattlefieldSession.FixtureAttack,StringComparer.Ordinal);
    }
    public BattlefieldCommandResult BeginAttackTargeting()
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation timed out; targeting cancelled.");
        if(!CanAttack)return new(BattlefieldOutcome.Rejected,"Selected unit has no available basic attack.");
        if(Activations.ActiveCharacter is null)Activations.StartActivation(SelectedCharacter!);
        return new(BattlefieldOutcome.Completed,"Choose an enemy, review the opening estimate, then Confirm Attack. DEV loadout / open targeting; LOS unresolved.");
    }
    private (AttackIntent Intent,CombatExchangeRequest Request,AttackConfiguration Attack) AttackContext(Character actor,Character target,string actionId="ordinary_attack")
    {
        var attack=DisarmEligibility.GetEffectiveAttack(actor,_loadouts[actor.Id],AssassinProfile.Unarmed);
        var counter=DisarmEligibility.GetEffectiveAttack(target,_loadouts[target.Id],AssassinProfile.Unarmed);
        var intent=new AttackIntent(actor,target,actionId,Activations,true,
            new(AbilityTargetType.Character,false,AbilityTargetTeam.Enemy,true,true,false,false),
            mapTargetLegal:()=>_developmentOpenTargeting
                ?MovementMap.IsValidPosition(actor.TacticalPosition!.Value)==true&&MovementMap.IsValidPosition(target.TacticalPosition!.Value)==true&&SmokeVisibility.CanTargetThroughVisibility(actor,Visibility(target.Id),false)
                :null);
        var request=new CombatExchangeRequest(actor,target,attack.Type,attack.Power,attack.Category,counter.Category,attack.BaseAccuracy,
            counter.Type,counter.Power,counter.BaseAccuracy,counter.MinimumRange,counter.MaximumRange,
            checked((int)actor.TacticalPosition!.Value.ManhattanDistanceTo(target.TacticalPosition!.Value)),true,false,
            IncomingAttackPermitsFollowUps:true,AttackerUnarmedAttack:AssassinProfile.Unarmed,DefenderUnarmedAttack:AssassinProfile.Unarmed,
            OpeningEvasionModifier:SmokeVeil.GetEvasionModifier(target,target.TacticalPosition!.Value,Smoke),CounterattackEvasionModifier:SmokeVeil.GetEvasionModifier(actor,actor.TacticalPosition!.Value,Smoke),Sanctuary:SanctuaryDefenseContext.FromActivations(Activations));
        return(intent,request,attack);
    }
    public MobaAttackPreview PreviewAttack(string targetId)
    {
        MobaAttackPreview Reject(BattlefieldOutcome outcome,string why)=>new(outcome,why,SelectedFixtureId,targetId);
        if(!CanAttack||Activations.ActiveCharacter!=SelectedCharacter)return Reject(BattlefieldOutcome.Rejected,"Enter Attack mode with an eligible selected unit first.");
        if(!Characters.TryGetValue(targetId,out var target))return Reject(BattlefieldOutcome.Rejected,"Unknown target.");
        var actor=SelectedCharacter!;
        if(!SmokeVisibility.CanTargetThroughVisibility(actor,Visibility(target.Id),false))return Reject(BattlefieldOutcome.Rejected,"Target is invisible in smoke; direct attacks are not legal.");
        if(actor.TacticalPosition is null||target.TacticalPosition is null)return Reject(BattlefieldOutcome.Unresolved,"Missing tactical positions.");
        try
        {
            var context=AttackContext(actor,target);
            var preview=_attacks.Preview(context.Intent,context.Request,context.Attack);
            if(!preview.CanExecute)
            {
                var reason=preview.Targeting?.RejectionReason;
                bool unresolved=reason is AbilityTargetingRejectionReason.MissingRequiredTargetInformation or AbilityTargetingRejectionReason.UnresolvedLineOfSight
                    or AbilityTargetingRejectionReason.UnresolvedElevation or AbilityTargetingRejectionReason.UnresolvedRange or AbilityTargetingRejectionReason.UnresolvedTargetingRule
                    ||preview.Validation.Outcome==AttackIntentOutcome.TauntContextUnresolved;
                return Reject(unresolved?BattlefieldOutcome.Unresolved:BattlefieldOutcome.Rejected,$"{preview.Validation.Outcome}: {reason}");
            }
            var r=context.Request;var a=context.Attack;
            var opening=CombatExchangeResolver.PrepareStrike(a.Type,actor,target,a.Power,a.Category,r.DefenderWeapon,a.BaseAccuracy,r.OpeningEvasionModifier,r.Sanctuary);
            return new(BattlefieldOutcome.Ready,"Opening estimate only; counters/follow-ups can change total damage. DEV loadouts, LOS not enforced.",actor.Id,target.Id,
                r.Distance,a.MinimumRange,a.MaximumRange,opening.HitChance,opening.CandidateDamage);
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException or OverflowException)
        {return Reject(BattlefieldOutcome.Unresolved,error.Message);}
    }
    public BattlefieldAttackResult ExecuteBasicAttack(string requestId,string targetId)
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation timed out; unconfirmed attack rejected.");
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if(Victory.IsFinished)return new(BattlefieldOutcome.Rejected,"Match finished.");
        if(_attackAttempts.Contains(requestId))return new(BattlefieldOutcome.AlreadyProcessed,"Attack request was already attempted.");
        var preview=PreviewAttack(targetId);
        if(!preview.CanExecute)return new(preview.Outcome,preview.Reason);
        var actor=SelectedCharacter!;var target=Characters[targetId];
        var context=AttackContext(actor,target);
        int attackRound=Round;
        var actorBefore=MobaUnitPresentation.Capture(actor);var targetBefore=MobaUnitPresentation.Capture(target);
        _attackAttempts.Add(requestId);_resolvingAttack=true;
        try
        {
            var result=_attacks.Execute(context.Intent,context.Request,context.Attack);
            if(result.Combat is { } resolved)
            {
                ApplySmokeAttackReveal(actor,target,resolved);
                CombatHistory.Append(MobaCombatFeedback.Interpret(requestId,attackRound,resolved,actorBefore,targetBefore,
                    MobaUnitPresentation.Capture(actor),MobaUnitPresentation.Capture(target)));
                RecordCombatObjectives(requestId,actor.Id,target.Id,resolved);
            }
            if(!result.Completed)
            {
                if(result.Combat is not null)
                {RequiresManualRecovery=true;return new(BattlefieldOutcome.PartiallyApplied,"Combat resolved but action completion failed; actual HP retained. Manual recovery required.",result.Combat);}
                return new(BattlefieldOutcome.Rejected,result.Validation.Outcome.ToString());
            }
            SelectedFixtureId=null;
            var combat=result.Combat!;
            var next=AdvanceCompletedRound();
            string feedback=$"{actor.Name} → {target.Name}: {(combat.OpeningStrike.Hit?"HIT":"MISS")}; HP -{combat.DefenderHealthLost}, shield -{combat.OpeningShieldAbsorbed}; "+
                $"target {target.CurrentHealth}/{target.BaseStats.MaxHealth} HP. Counter: {combat.CounterattackOccurred}; follow-up: {combat.FollowUpOccurred}. Activation ended.";
            return new(next.Outcome==BattlefieldOutcome.Completed?BattlefieldOutcome.Completed:next.Outcome,feedback+next.Reason,combat);
        }
        catch(Exception error)
        {RequiresManualRecovery=true;return new(BattlefieldOutcome.PartiallyApplied,$"Attack failed: {error.Message}. Actual HP retained; restart/manual recovery required. No replay.");}
        finally{_resolvingAttack=false;}
    }
}
