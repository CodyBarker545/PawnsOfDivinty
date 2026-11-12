using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed partial class MobaHotseatSession
{
    public CastleControlService Control {get;private set;}=null!;
    public MobaVictoryState Victory {get;private set;}=null!;
    private readonly List<string> _territoryHistory=new();
    private readonly HashSet<string> _scoredDefeats=new(StringComparer.Ordinal);
    public IReadOnlyList<string> TerritoryHistory=>_territoryHistory.AsReadOnly();
    private void InitializeObjectives(DevelopmentScoringProfile profile)
    {Control=new(_layout,Characters);Victory=new(profile);}
    private void ReconcileControl(string eventId,bool allowCastleCapture=true)
    {
        foreach(var change in Control.Reconcile(eventId,allowCastleCapture))
        {
            _territoryHistory.Add($"Round {Round}: {change.Text}");
            if(change.CastleCapture&&change.Owner is { } team)
                Victory.Queue($"capture:{eventId}:{change.ZoneId}",Round,team,Victory.Profile.CastleCapturePoints,$"Capture {change.ZoneId}");
        }
    }
    private void RecordCombatObjectives(string requestId,string actorId,string targetId,CombatExchangeResult combat)
    {
        void Defeat(string id,int creditedTeam)
        {
            if(_scoredDefeats.Add(id))Victory.Queue("defeat:"+id,Round,creditedTeam,Victory.Profile.DefeatPoints,$"Defeat {id}");
        }
        if(combat.DefenderDefeated)Defeat(targetId,Characters[actorId].TeamId);
        if(combat.AttackerDefeated)Defeat(actorId,Characters[targetId].TeamId);
        ReconcileControl("attack:"+requestId,combat.AttackerDefeated||combat.DefenderDefeated);
    }
    private void SettleRoundObjectives()
    {
        ReconcileControl("round-end:"+Round,false);
        foreach(var castle in _layout.Castles.Where(c=>!c.IsHome))
        {
            var zone=Control.Castle(castle.Id);
            if(!zone.Contested&&zone.OwnerTeamId is { } team&&zone.OccupyingTeamId==team)
                Victory.Queue($"hold:{Round}:{castle.Id}",Round,team,Victory.Profile.ForwardCastleHoldPoints,$"Occupied forward castle {castle.Id}");
        }
        var objective=Control.Objective;
        if(!objective.Contested&&objective.OwnerTeamId is { } owner)
            Victory.Queue($"hold:{Round}:{objective.Id}",Round,owner,Victory.Profile.CentralHoldPoints,"Central objective occupation");
        Victory.Settle(Round);
        _territoryHistory.Add($"Round {Round} VP settled: Blue {Victory.BlueVP}, Red {Victory.RedVP}.");
        if(Victory.IsFinished)_territoryHistory.Add(Victory.WinnerTeamId is { } winner?$"Player {winner} WINS.":"DRAW (development tie policy).");
    }
}
