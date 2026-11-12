using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed record CastleTransferPlan(string ActorId,int Round,string OriginCastleId,string DestinationCastleId,GridPosition OriginCell,GridPosition DeploymentCell);
public sealed record CastleTransferPreview(BattlefieldOutcome Outcome,string Reason,CastleTransferPlan? Plan=null)
{public bool CanExecute=>Outcome==BattlefieldOutcome.Ready;}

public sealed partial class MobaHotseatSession
{
    private readonly HashSet<string> _transferAttempts=new(StringComparer.Ordinal);
    public bool CanBeginTransfer=>CanAttack&&SelectedCharacter is {TacticalPosition:{ } cell} actor
        &&_layout.Castles.Any(c=>c.CaptureCells.Contains(cell)&&Control.Castle(c.Id) is {Contested:false} zone&&zone.OwnerTeamId==actor.TeamId);
    public BattlefieldCommandResult BeginTransferTargeting()
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation timed out; transfer targeting cancelled.");
        if(!CanAttack)return new(BattlefieldOutcome.Rejected,"Select an eligible unit with a Main Action available.");
        var actor=SelectedCharacter!;
        if(!_layout.Castles.Any(c=>c.CaptureCells.Contains(actor.TacticalPosition!.Value)&&Control.Castle(c.Id) is {Contested:false} z&&z.OwnerTeamId==actor.TeamId))
            return new(BattlefieldOutcome.Rejected,"Origin must be an uncontested friendly capture zone.");
        if(Activations.ActiveCharacter is null)Activations.StartActivation(actor);
        return new(BattlefieldOutcome.Completed,"Choose another friendly uncontested castle and an unoccupied deployment tile. Transfer costs Main Action and ends activation.");
    }
    public CastleTransferPreview PreviewTransfer(string destinationId,GridPosition deployment)
    {
        CastleTransferPreview Reject(string reason)=>new(BattlefieldOutcome.Rejected,reason);
        if(!CanAttack||Activations.ActiveCharacter!=SelectedCharacter)return Reject("Enter transfer mode with an eligible active unit first.");
        var actor=SelectedCharacter!;var origin=_layout.Castles.SingleOrDefault(c=>c.CaptureCells.Contains(actor.TacticalPosition!.Value));
        var destination=_layout.Castles.SingleOrDefault(c=>c.Id==destinationId);
        if(origin is null||destination is null)return Reject("Transfer requires two authored castles; origin must be a capture-zone cell.");
        if(origin.Id==destination.Id)return Reject("Origin and destination cannot be the same castle.");
        var from=Control.Castle(origin.Id);var to=Control.Castle(destination.Id);
        if(from.OwnerTeamId!=actor.TeamId||to.OwnerTeamId!=actor.TeamId)return Reject("Both castles must be friendly-controlled.");
        if(from.Contested||to.Contested)return Reject("Contested castles block outgoing and incoming transfers.");
        if(!destination.DeploymentCells.Contains(deployment)||MovementMap.IsValidPosition(deployment)!=true||MovementMap.IsOccupied(deployment))return Reject("Choose a walkable, unoccupied destination deployment tile.");
        var approval=Activations.EvaluateMainAction(actor,new(false));
        if(!approval.IsAllowed)return Reject($"Production Main Action restriction: {approval.Outcome}.");
        return new(BattlefieldOutcome.Ready,"Approved candidate; all origin/destination conditions are revalidated at execution.",
            new(actor.Id,Round,origin.Id,destination.Id,actor.TacticalPosition!.Value,deployment));
    }
    public BattlefieldCommandResult ExecuteTransfer(string requestId,CastleTransferPlan plan)
    {
        using var timing=BeginTimedCommand();if(!timing.Accepted)return new(BattlefieldOutcome.Rejected,"Activation timed out; unconfirmed transfer rejected.");
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if(Victory.IsFinished)return new(BattlefieldOutcome.Rejected,"Match finished.");
        if(_transferAttempts.Contains(requestId))return new(BattlefieldOutcome.AlreadyProcessed,"Transfer request already attempted.");
        var fresh=PreviewTransfer(plan.DestinationCastleId,plan.DeploymentCell);
        if(!fresh.CanExecute||fresh.Plan!=plan)return new(BattlefieldOutcome.Rejected,fresh.CanExecute?"Transfer context changed; preview again.":fresh.Reason);
        var actor=SelectedCharacter!;var approval=Activations.EvaluateMainAction(actor,new(false));
        if(!approval.IsAllowed)return new(BattlefieldOutcome.Rejected,approval.Outcome.ToString());
        _transferAttempts.Add(requestId);
        var begun=Activations.BeginApprovedMainAction(actor,approval);
        if(!begun.IsAllowed)return new(BattlefieldOutcome.Rejected,begun.Outcome.ToString());
        actor.SetTacticalPosition(plan.DeploymentCell);
        var completed=Activations.CompleteApprovedMainAction(actor,approval);
        if(!completed.IsAllowed){RequiresManualRecovery=true;return new(BattlefieldOutcome.PartiallyApplied,"Transfer position committed but activation completion failed; reset required.");}
        SelectedFixtureId=null;ReconcileControl("transfer:"+requestId,false);
        _territoryHistory.Add($"Round {Round}: {actor.Name} transferred {plan.OriginCastleId} → {plan.DestinationCastleId} ({plan.DeploymentCell.X},{plan.DeploymentCell.Y}).");
        var next=AdvanceCompletedRound();return new(next.Outcome,$"{actor.Name} transferred; Main Action consumed and activation ended."+next.Reason);
    }
}
