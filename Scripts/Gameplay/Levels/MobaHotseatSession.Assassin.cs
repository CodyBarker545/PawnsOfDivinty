using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed record MobaAbilityInfo(string Id,string Name,bool Passive,int? ReadyRound,string Cost,string Eligibility);
public sealed record MobaAbilityPreview(BattlefieldOutcome Outcome,string Reason,string AbilityId,IReadOnlyList<GridPosition> Cells,
    GridPosition? Destination=null,string? TargetId=null,int? HitChance=null,int? OpeningDamage=null,bool InvisibleOpening=false)
{public bool CanExecute=>Outcome==BattlefieldOutcome.Ready;}
public sealed record MobaAbilityResult(BattlefieldOutcome Outcome,string Reason,string? ActorId,GridPosition? FinalPosition=null,CombatExchangeResult? Combat=null);

public sealed partial class MobaHotseatSession
{
    public SmokePlacementService Smoke {get;}=new();
    public AssassinDevelopmentProfile AssassinProfile {get;private set;}=null!;
    private SmokeBombAbilityExecutor _smokeBomb=null!;
    private ShadowStrikeAbilityExecutor _shadowStrike=null!;
    private ShadowStrikeResolver _shadowResolver=null!;
    private AbilityUse? _smokeUse;
    private string? _abilityActor,_abilityId,_smokeEffect;
    private long _abilitySequence,_abilityTurn;
    private readonly Dictionary<string,AbilityUse> _shadowUses=new(StringComparer.Ordinal);
    private readonly HashSet<string> _abilityAttempts=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(long Turn,int Round,SmokeVisibilityResult Visibility)> _approaches=new(StringComparer.Ordinal);
    public string? TargetedAbilityId=>_abilityTurn==_activationSerial&&_abilityActor==SelectedFixtureId?_abilityId:null;
    private void InitializeAssassin(CombatExchangeResolver combat,AssassinDevelopmentProfile profile)
    {profile.Validate();AssassinProfile=profile;_smokeBomb=new(Smoke);_shadowResolver=new(combat,Smoke);_shadowStrike=new(_shadowResolver);}
    private IEnumerable<(Character Character,GridPosition Position)> PositionedRoster()=>Characters.Values.Where(c=>c.TacticalPosition.HasValue).Select(c=>(c,c.TacticalPosition!.Value));
    public SmokeVisibilityResult Visibility(string fixtureId)
    {var c=Characters[fixtureId];return SmokeVisibility.Evaluate(c,c.TacticalPosition!.Value,Smoke,PositionedRoster());}
    public bool IsVisibleToPlayer(string fixtureId,int? player=null)
    {
        var target=Characters[fixtureId];var observer=Characters.Values.First(c=>c.TeamId==(player??CurrentPlayer??StartingPlayer));
        return SmokeVisibility.CanTargetThroughVisibility(observer,Visibility(fixtureId),false);
    }
    public IReadOnlyList<MobaAbilityInfo> AssassinAbilities()
    {
        if(SelectedCharacter?.Definition?.Id!=HoodedAssassin.Definition.Id)return Array.Empty<MobaAbilityInfo>();
        var c=SelectedCharacter;
        return c.Definition.Abilities.Select(a=>
        {
            bool passive=a.Classification==AbilityClassification.Passive;
            int? ready=c.Cooldowns.Cooldowns.TryGetValue(a.Id,out var cd)?cd.ReadyRound:null;
            var eligibilityResult=AbilityEligibility.Evaluate(c,a.Id,Activations);
            string eligibility=passive?$"Passive: {(Visibility(c.Id).IsInSmoke?"+20 evasion active":"outside smoke")}":!CanAttack?"No available activation/Main Action":ready>Round?$"Cooldown: ready R{ready}":
                Activations.ActiveCharacter is null?"Choose to inspect targets":eligibilityResult.IsAllowed?"Ready":eligibilityResult.RejectionReason.ToString();
            return new MobaAbilityInfo(a.Id,a.DisplayName,passive,ready,passive?"Passive":$"{a.RequiredAction} · reuse {a.ReuseInterval}",eligibility);
        }).ToArray();
    }
    public BattlefieldCommandResult BeginAssassinAbility(string abilityId)
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation expired; ability targeting cancelled.");
        if(!CanAttack||SelectedCharacter?.Definition?.Id!=HoodedAssassin.Definition.Id||abilityId is not (HoodedAssassin.SmokeBombId or HoodedAssassin.ShadowStrikeId))
            return new(BattlefieldOutcome.Rejected,"Choose an eligible Assassin active ability.");
        if(Activations.ActiveCharacter is null)Activations.StartActivation(SelectedCharacter);
        var eligibility=AbilityEligibility.Evaluate(SelectedCharacter,abilityId,Activations);
        if(!eligibility.IsAllowed)return new(BattlefieldOutcome.Rejected,eligibility.RejectionReason.ToString());
        _abilityActor=SelectedFixtureId;_abilityId=abilityId;_abilityTurn=_activationSerial;_shadowUses.Clear();
        _smokeEffect=$"assassin-smoke-{ActivationTimer.MatchId:N}-{++_abilitySequence}";
        _smokeUse=new(SelectedCharacter,abilityId,Activations);
        return new(BattlefieldOutcome.Completed,abilityId==HoodedAssassin.SmokeBombId?"Preview caster-centered smoke; choose an unoccupied in-cloud teleport tile, then Confirm Ability.":"Choose a range-1 enemy for Shadow Strike; pre-approach visibility is authoritative.");
    }
    public void CancelAssassinTargeting(){_abilityId=null;_abilityActor=null;_smokeUse=null;_shadowUses.Clear();}
    private bool SmokeCanCover(GridPosition cell)=>_layout.Board.Contains(cell)&&(AssassinProfile.SmokeOverObstacles||_layout.Walkable(cell));
    private sealed class SmokeMap(MobaMovementMap map) : ISmokeTeleportMap
    {
        public bool? IsValidPosition(GridPosition cell)=>map.IsValidPosition(cell);
        public bool? CanTeleportTo(GridPosition cell)=>map.IsValidPosition(cell)==true&&map.HasKnownFlatCost(cell);
        public bool? IsOccupied(GridPosition cell)=>map.IsOccupied(cell);
    }
    private SmokeBombUseResult SmokePreview(GridPosition? destination)=>_smokeBomb.Preview(_smokeUse!,_smokeEffect!,SmokeCanCover,destination,new SmokeMap(MovementMap),PositionedRoster(),new(false,false,false));
    private (AbilityUse Use,CombatExchangeRequest Request,AttackConfiguration Attack,SmokeVisibilityResult Visibility) ShadowContext(string targetId)
    {
        var actor=SelectedCharacter!;var target=Characters[targetId];var c=AttackContext(actor,target,HoodedAssassin.ShadowStrikeId);
        if(!_shadowUses.TryGetValue(targetId,out var use)){use=new(actor,HoodedAssassin.ShadowStrikeId,Activations,c.Intent);_shadowUses.Add(targetId,use);}
        var visibility=_approaches.TryGetValue(actor.Id,out var before)&&before.Turn==_activationSerial&&before.Round==Round?before.Visibility:Visibility(actor.Id);
        return(use,c.Request,c.Attack,visibility);
    }
    public MobaAbilityPreview PreviewAssassinAbility(GridPosition? destination=null,string? targetId=null)
    {
        MobaAbilityPreview Fail(string reason,BattlefieldOutcome outcome=BattlefieldOutcome.Rejected,IReadOnlyList<GridPosition>? cells=null)=>new(outcome,reason,TargetedAbilityId??"",cells??Array.Empty<GridPosition>(),destination,targetId);
        if(!CanAttack||TargetedAbilityId is null||Activations.ActiveCharacter!=SelectedCharacter)return Fail("Enter an eligible ability's targeting mode first.");
        try
        {
            if(TargetedAbilityId==HoodedAssassin.SmokeBombId)
            {
                var preview=SmokePreview(destination);return preview.CanExecute?new(BattlefieldOutcome.Ready,"Smoke Bomb + teleport, one Main Action. No separate cloud-jump action is enabled.",TargetedAbilityId,preview.CoveredTiles,destination):
                    Fail($"{preview.Eligibility.RejectionReason}; {preview.Placement?.RejectionReason}; {preview.TeleportValidation?.RejectionReason}",cells:preview.CoveredTiles);
            }
            if(targetId is null||!Characters.TryGetValue(targetId,out var target)||!SmokeVisibility.CanTargetThroughVisibility(SelectedCharacter!,Visibility(targetId),false))return Fail("Choose a visible living enemy target.");
            var c=ShadowContext(targetId);var p=_shadowStrike.Preview(c.Use,c.Request,c.Visibility,c.Attack.MinimumRange,c.Attack.MaximumRange,c.Use.AttackIntent!.TargetingRules,_developmentOpenTargeting?MovementMap.IsValidPosition(target.TacticalPosition!.Value):null);
            if(!p.CanExecute)return Fail($"{p.Eligibility.RejectionReason}; {p.Targeting?.RejectionReason}; {p.CombatEligibility?.RejectionReason}");
            bool invisible=p.CombatEligibility!.InvisibleOpening;
            if(invisible&&AssassinProfile.Unarmed is null)return Fail("Invisible Disarm requires explicit DEV unarmed Power/accuracy.",BattlefieldOutcome.Unresolved);
            var r=c.Request;var prepared=CombatExchangeResolver.PrepareStrike(c.Attack.Type,SelectedCharacter!,target,c.Attack.Power,c.Attack.Category,r.DefenderWeapon,c.Attack.BaseAccuracy,r.OpeningEvasionModifier,r.Sanctuary);
            int bonus=p.CombatEligibility.OpeningDamageBonus;
            return new(BattlefieldOutcome.Ready,$"{(invisible?$"Invisible opening: +{bonus} and Disarm on surviving hit":$"Visible opening: +{bonus}")}; opening only. DEV equipment; spell tags / smoke LOS unresolved.",TargetedAbilityId,Array.Empty<GridPosition>(),TargetId:targetId,HitChance:prepared.HitChance,OpeningDamage:checked(prepared.CandidateDamage+bonus),InvisibleOpening:invisible);
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException){return Fail(e.Message,BattlefieldOutcome.Unresolved);}
    }
    public MobaAbilityResult ExecuteAssassinAbility(string requestId,GridPosition? destination=null,string? targetId=null)
    {
        using var timing=BeginTimedCommand();
        if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation expired; unconfirmed ability cancelled.",null);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if(Victory.IsFinished)return new(BattlefieldOutcome.Rejected,"Match finished.",null);
        if(_abilityAttempts.Contains(requestId))return new(BattlefieldOutcome.AlreadyProcessed,"Ability request already attempted.",null);
        var preview=PreviewAssassinAbility(destination,targetId);if(!preview.CanExecute)return new(preview.Outcome,preview.Reason,SelectedFixtureId);
        var actor=SelectedCharacter!;_abilityAttempts.Add(requestId);_resolvingAttack=true;
        try
        {
            CombatExchangeResult? combat=null;string feedback;
            if(TargetedAbilityId==HoodedAssassin.SmokeBombId)
            {
                var result=_smokeBomb.Execute(_smokeUse!,_smokeEffect!,SmokeCanCover,destination,new SmokeMap(MovementMap),PositionedRoster(),new(false,false,false));
                if(!result.Succeeded){if(result.CreatedSmoke is not null)RequiresManualRecovery=true;return new(RequiresManualRecovery?BattlefieldOutcome.PartiallyApplied:BattlefieldOutcome.Rejected,"Smoke Bomb: "+result.Eligibility.RejectionReason,actor.Id,actor.TacticalPosition);}
                ReconcileControl("smoke:"+requestId,false);feedback=$"{actor.Name}: Smoke Bomb created {result.CoveredTiles.Count} cells through R{result.CreatedSmoke!.ExpirationRound}; teleported inside the cloud. Activation ended.";
            }
            else
            {
                var target=Characters[targetId!];var beforeA=MobaUnitPresentation.Capture(actor);var beforeD=MobaUnitPresentation.Capture(target);var c=ShadowContext(targetId!);
                var result=_shadowStrike.Execute(c.Use,c.Request,c.Visibility,c.Attack.MinimumRange,c.Attack.MaximumRange,c.Use.AttackIntent!.TargetingRules,_developmentOpenTargeting?MovementMap.IsValidPosition(target.TacticalPosition!.Value):null);
                combat=result.Strike?.Exchange;
                if(combat is not null){ApplySmokeAttackReveal(actor,target,combat);CombatHistory.Append(MobaCombatFeedback.Interpret(requestId,Round,combat,beforeA,beforeD,MobaUnitPresentation.Capture(actor),MobaUnitPresentation.Capture(target)));RecordCombatObjectives(requestId,actor.Id,target.Id,combat);}
                if(!result.Succeeded){RequiresManualRecovery=combat is not null;return new(RequiresManualRecovery?BattlefieldOutcome.PartiallyApplied:BattlefieldOutcome.Rejected,"Shadow Strike did not finalize; "+result.Eligibility.RejectionReason,actor.Id,actor.TacticalPosition,combat);}
                feedback=$"{actor.Name} Shadow Strike → {target.Name}: {(combat!.OpeningStrike.Hit?"HIT":"MISS")}; +{combat.OpeningDamageBonus} opening; HP -{combat.DefenderHealthLost}, shield -{combat.OpeningShieldAbsorbed}; Disarm {combat.OpeningDisarmApplied}; target {target.CurrentHealth}/{target.BaseStats.MaxHealth}. Activation ended.";
            }
            SelectedFixtureId=null;CancelAssassinTargeting();var next=AdvanceCompletedRound();return new(next.Outcome,feedback+next.Reason,actor.Id,actor.TacticalPosition,combat);
        }
        catch(Exception e){RequiresManualRecovery=true;return new(BattlefieldOutcome.PartiallyApplied,$"Ability failed: {e.Message}. Applied state retained; reset required.",actor.Id,actor.TacticalPosition);}
        finally{_resolvingAttack=false;}
    }
    private void ApplySmokeAttackReveal(Character actor,Character target,CombatExchangeResult result)
    {
        if(actor.Definition?.Id==HoodedAssassin.Definition.Id)actor.SmokeReveal.RecordAttack();
        if(target.Definition?.Id==HoodedAssassin.Definition.Id&&(result.CounterattackOccurred||result.FollowUpBy==FollowUpParticipant.Defender))target.SmokeReveal.RecordAttack();
    }
}
