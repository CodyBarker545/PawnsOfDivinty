using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Gameplay.Levels;

public sealed record MobaMovementPlan(string ActorId,int Round,GridPosition Origin,
    GridPosition Destination,IReadOnlyList<GridPosition> Path,long Cost);

/// <summary>Godot integration adapter; production Character, lifecycle and movement services own game state.</summary>
public sealed partial class MobaHotseatSession
{
    public const int MaxRosterSlotsPerPlayer = 5;
    public MobaTeamReadiness Readiness(int teamId)
    {
        if(teamId is not (1 or 2))throw new ArgumentOutOfRangeException(nameof(teamId));
        var team=Characters.Values.Where(c=>c.TeamId==teamId).ToArray();
        return new(teamId,team.Count(c=>c.CanAct),team.Length,team.Count(c=>!c.IsDefeated));
    }
    public IReadOnlyDictionary<string, Character> Characters { get; }
    public ActivationManager Activations { get; }
    public RoundLifecycleManager Lifecycle { get; } = new();
    public MobaMovementMap MovementMap { get; }
    private readonly MobaBetaLayout _layout;
    private readonly MovementActionExecutor _movement;
    private readonly RoundCleanupCoordinator _cleanup;
    private readonly DelayedAttackScheduler _delayed = new();
    private long _moveSequence;
    public MovementActionResult? PendingVisualMovement { get; private set; }
    public bool IsPresentationBusy=>PendingVisualMovement is not null||_resolvingAttack;
    public bool RequiresManualRecovery { get; private set; }
    public int StartingPlayer { get; }
    public int Round => Activations.CurrentRound;
    public int? CurrentPlayer => Activations.ActiveCharacter?.TeamId ?? Activations.NextTeamId;
    public string? SelectedFixtureId { get; private set; }
    public Character? SelectedCharacter => SelectedFixtureId is { } id ? Characters[id] : null;

    public MobaHotseatSession(MobaBetaLayout layout, int startingPlayer = 1,
        IRandomRollProvider? random = null,bool developmentOpenTargeting = true,DevelopmentScoringProfile? scoringProfile=null,
        ActivationTimerConfiguration? timerConfiguration=null,IActivationTimeSource? timeSource=null,AssassinDevelopmentProfile? assassinProfile=null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout=layout;
        if(startingPlayer is not (1 or 2))throw new ArgumentOutOfRangeException(nameof(startingPlayer));
        var roster=new Dictionary<string,Character>(StringComparer.Ordinal);
        foreach(var fixture in layout.Fixtures)
        {
            if(fixture.TeamId is not (1 or 2))throw new ArgumentException($"Fixture {fixture.CharacterId}: Player must be 1 or 2.");
            var definition=CharacterRegistry.GetCharacter(fixture.DefinitionId)
                ?? throw new ArgumentException($"Fixture {fixture.CharacterId}: unknown exact registry DefinitionId '{fixture.DefinitionId}'.");
            var character=CharacterFactory.Create(definition,fixture.CharacterId,fixture.TeamId);
            character.SetTacticalPosition(fixture.Cell);
            if(!roster.TryAdd(fixture.CharacterId,character))throw new ArgumentException($"Duplicate fixture ID: {fixture.CharacterId}.");
        }
        foreach(int player in new[]{1,2})
        {
            int count=roster.Values.Count(c=>c.TeamId==player);
            if(count is <1 or >MaxRosterSlotsPerPlayer)
                throw new ArgumentException($"Player {player} needs 1–{MaxRosterSlotsPerPlayer} scene-authored roster slots; found {count}.");
        }
        Characters=new ReadOnlyDictionary<string,Character>(roster);
        InitializeObjectives(scoringProfile??DevelopmentScoringProfile.Default);
        StartingPlayer=startingPlayer;
        Activations=new(roster.Values.Where(c=>c.TeamId==1),roster.Values.Where(c=>c.TeamId==2),startingPlayer,Lifecycle);
        var walls=new WailingRampartService();var ice=new SpiritFrostService();var sentinel=new ImpalingSentinelTracker(Activations,Lifecycle);
        MovementMap=new(layout,Characters,walls,ice);
        _cleanup=new(Lifecycle,Activations,walls,ice,Smoke,sentinel);
        // Movement has no prepared reaction inputs; ordinary attacks share the existing combat resolver.
        var combat=new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(random??new SystemRandomRollProvider())));
        InitializeCombat(combat,developmentOpenTargeting);
        InitializeAssassin(combat,assassinProfile??new());
        _movement=new(Activations,Lifecycle,MovementMap,walls,ice,sentinel,combat,new NoCombatInputs());
        PrepareCurrentRound();
        InitializeTimer(timerConfiguration??new(),timeSource);
    }

    public bool CanSelect(string fixtureId) => Characters.TryGetValue(fixtureId,out var c)
        && !Victory.IsFinished&&!RequiresManualRecovery&&!IsPresentationBusy&&Lifecycle.CurrentPhase==RoundPhase.AlternatingActivations
        && c.CanAct && (Activations.ActiveCharacter is { } active ? ReferenceEquals(c,active) : c.TeamId==Activations.NextTeamId);

    /// <summary>Inspection/selection costs nothing. Commands use the production action economy.</summary>
    public BattlefieldCommandResult Select(string? fixtureId)
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation timed out; choose for the next activation.");
        if(Victory.IsFinished||IsPresentationBusy||RequiresManualRecovery)return new(BattlefieldOutcome.Rejected,"Match finished, movement presenting, or recovery required.");
        if(fixtureId is null){CancelAssassinTargeting();SelectedFixtureId=null;return new(BattlefieldOutcome.Completed,"Selection cleared.");}
        if(!CanSelect(fixtureId))return new(BattlefieldOutcome.Rejected,"Choose a ready, living unit belonging to the current player.");
        if(SelectedFixtureId!=fixtureId)CancelAssassinTargeting();SelectedFixtureId=fixtureId;
        return new(BattlefieldOutcome.Completed,$"Selected {Characters[fixtureId].Name}.");
    }

    public BattlefieldCommandResult EndSelectedActivation()
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation already ended by timeout.");
        if(SelectedFixtureId is not { } id || !CanSelect(id))
            return new(BattlefieldOutcome.Rejected,"Select an eligible friendly unit before ending its activation.");
        var actor=Characters[id];
        if(Activations.ActiveCharacter is null)Activations.StartActivation(actor);
        bool moved=Activations.MovementUsed;
        Activations.CompleteActivation();
        SelectedFixtureId=null;
        string message=$"{actor.Name} ended its activation{(moved ? " after moving" : " without moving")}.";
        var next=AdvanceCompletedRound();
        return next.Outcome==BattlefieldOutcome.Completed?new(BattlefieldOutcome.Completed,message+next.Reason):next;
    }
    private BattlefieldCommandResult AdvanceCompletedRound()
    {
        _activationSerial++;
        CancelAssassinTargeting();_approaches.Clear();
        if(Victory.IsFinished)return new(BattlefieldOutcome.Completed," Match finished.");
        if(Activations.IsRoundComplete && Characters.Values.Any(c=>!c.IsDefeated))
        {
            Lifecycle.AdvancePhase(); // Cleanup
            var cleanup=_cleanup.Process(Round);
            if(cleanup.Status!=RoundCleanupStatus.Completed)
            {RequiresManualRecovery=true;return new(BattlefieldOutcome.Failed,cleanup.Reason);}
            Lifecycle.AdvancePhase(); // Authoritative end-of-round scoring boundary.
            SettleRoundObjectives();
            if(Victory.IsFinished)return new(BattlefieldOutcome.Completed,Victory.WinnerTeamId is { } winner?$" Player {winner} wins at {Victory.BlueVP}:{Victory.RedVP} VP.":" Draw under development tie policy.");
            Lifecycle.StartNextRound();
            PrepareCurrentRound();
            return new(BattlefieldOutcome.Completed,$" Round {Round} begins; activation readiness reset.");
        }
        return new(BattlefieldOutcome.Completed,"");
    }

    private void PrepareCurrentRound()
    {
        int livingBefore=Characters.Values.Count(c=>!c.IsDefeated);
        while(Lifecycle.CurrentPhase!=RoundPhase.AlternatingActivations)
        {
            switch(Lifecycle.CurrentPhase)
            {
                case RoundPhase.Shop:case RoundPhase.OtherRoundStartTriggers:break; // Unimplemented extension points.
                case RoundPhase.PoisonBatch:new PoisonBatchProcessor(Lifecycle).Process(Characters.Values);break;
                case RoundPhase.DelayedAttacks:new DelayedAttackProcessor(_delayed,Lifecycle).Process(Characters.Values);break;
                case RoundPhase.TimedStatusUpdate:new TimedStatusBatchProcessor(Lifecycle).Process(Characters.Values);break;
                case RoundPhase.Preparation:new RoundPreparationCoordinator(Activations,Lifecycle).Process(Characters.Values,StartingPlayer);break;
                default:throw new InvalidOperationException("Unexpected beta round-start phase.");
            }
            Lifecycle.AdvancePhase();
        }
        if(Round>1)ReconcileControl("round-start:"+Round,Characters.Values.Count(c=>!c.IsDefeated)<livingBefore); // No invented Poison/delayed kill credit.
    }

    public int RemainingMovement=>SelectedCharacter is { } c&&CanSelect(c.Id)
        &&(Activations.ActiveCharacter is null||Activations.CanMove)?c.EffectiveMovement:0;

    /// <summary>Candidate routes share the executor's map costs; they do not authorize a command.</summary>
    public IReadOnlyDictionary<GridPosition,TacticalRoute> ReachableDestinations()
    {
        if(SelectedCharacter is not {TacticalPosition:{ } origin} actor||!CanSelect(actor.Id)
            ||(Activations.IsActivationActive&&!Activations.CanMove))return new Dictionary<GridPosition,TacticalRoute>();
        return GridPathfinder.Reachable(origin,_layout.Board.Width,_layout.Board.Height,actor.EffectiveMovement,
            p=>MovementMap.IsValidPosition(p)==true&&(p==origin||!MovementMap.IsOccupied(p)),
            (from,to)=>MovementMap.GetStepCost(actor,from,to)).Where(e=>e.Value.Cost>0).ToDictionary(e=>e.Key,e=>e.Value);
    }
    public MobaMovementPlan? PreviewMovement(GridPosition destination)
    {
        if(SelectedCharacter is not {TacticalPosition:{ } origin} actor||!ReachableDestinations().TryGetValue(destination,out var route))return null;
        return new(actor.Id,Round,origin,destination,route.Cells,route.Cost);
    }
    public MovementActionResult MoveTo(GridPosition destination)
    {
        var plan=PreviewMovement(destination);
        return CommitMovement(plan??new(SelectedFixtureId??"none",Round,SelectedCharacter?.TacticalPosition??default,
            destination,new[]{SelectedCharacter?.TacticalPosition??default,destination},0));
    }
    public MovementActionResult CommitMovement(MobaMovementPlan plan)
    {
        using var timing=BeginTimedCommand();
        var request=new MovementActionRequest($"beta-move-{++_moveSequence}",plan.ActorId,Math.Max(1,plan.Round),plan.Path);
        Characters.TryGetValue(plan.ActorId,out var actor);
        MovementActionResult Reject(string reason,MovementOutcome outcome=MovementOutcome.Rejected)=>new(request,outcome,reason,actor?.TacticalPosition,actor,
            Array.Empty<GridPosition>(),Array.Empty<GridPosition>(),null,0,false,MovementMap,Array.Empty<string>(),Array.Empty<MovementReactionObservation>());
        if(!timing.Accepted)return Reject("Activation timed out; stale movement command rejected.");
        if(IsPresentationBusy||RequiresManualRecovery)return Reject("MovementPresentingOrRecoveryRequired");
        if(SelectedFixtureId!=plan.ActorId||!CanSelect(plan.ActorId))return Reject("NotSelectedEligibleActor");
        if(plan.Round!=Round||plan.Origin!=actor!.TacticalPosition)return Reject("StaleMovementPlan");
        var current=PreviewMovement(plan.Destination);
        if(current is null)
        {
            if(MovementMap.IsValidPosition(plan.Destination)==true&&!MovementMap.HasKnownFlatCost(plan.Destination))
                return Reject("Terrain movement cost is unresolved for this authored tag.",MovementOutcome.UnresolvedRule);
            return Reject("No legal route within remaining MOV; occupied, blocked and unresolved-cost cells are excluded.");
        }
        if(current.Cost!=plan.Cost||!current.Path.SequenceEqual(plan.Path))return Reject("RouteOrCostChanged; preview again.");
        if(Activations.ActiveCharacter is null)Activations.StartActivation(actor);
        var preview=_movement.Preview(request);
        if(preview.Outcome!=MovementOutcome.Ready)return preview;
        var preApproach=Visibility(actor.Id);
        var result=_movement.Execute(request);
        if(result.Outcome==MovementOutcome.Completed&&actor.Definition?.Id=="hooded_assassin")_approaches[actor.Id]=(_activationSerial,Round,preApproach);
        if(result.TraversedPath.Count>1)PendingVisualMovement=result;
        if(result.Outcome==MovementOutcome.Completed)ReconcileControl("move:"+result.RequestId);
        if(result.Outcome is MovementOutcome.PartiallyApplied or MovementOutcome.Failed)RequiresManualRecovery=true;
        return result;
    }
    /// <summary>Only release the presentation lock after the view reaches the authoritative final cell.</summary>
    public BattlefieldCommandResult AcknowledgeVisualMovement(string requestId,GridPosition displayedCell)
    {
        using var timing=BeginTimedCommand();
        if(PendingVisualMovement is not { } move||move.RequestId!=requestId||move.FinalPosition!=displayedCell
            ||Characters[move.ActorId].TacticalPosition!=displayedCell)
            return new(BattlefieldOutcome.Rejected,"Movement presentation token or final coordinate mismatch; reconcile to runtime state.");
        PendingVisualMovement=null;return new(BattlefieldOutcome.Completed,RequiresManualRecovery
            ?"Movement view reconciled; the partial/failed command requires manual recovery or restart."
            :"Movement presentation reconciled; Main Action remains available.");
    }
    private sealed class NoCombatInputs:IMovementReactionInputs
    {
        public AttackConfiguration? CurrentLance(string ownerId)=>null;
        public CombatExchangeRequest? CreateRequest(string ownerId,string moverId,int distance)=>null;
    }

    /// <summary>Fresh immutable presentation snapshot; HP, movement and readiness are read from the real Character.</summary>
    public BattlefieldUnitState Capture(string fixtureId)
    {
        var c=Characters[fixtureId];
        var statuses=c.Statuses.TimedStatuses.Select(s=>$"{s.Key} ({s.Value})").ToList();
        if(c.Statuses.IsPoisoned)statuses.Add("Poison");
        return new(c.Id,c.Definition!.Id,c.Name,c.TeamId,c.TacticalPosition,c.CurrentHealth,c.BaseStats.MaxHealth,
            c.ShieldHP,c.EffectiveMovement,c.IsDefeated,c.HasActedThisRound,CanSelect(fixtureId),statuses.AsReadOnly());
    }
}
