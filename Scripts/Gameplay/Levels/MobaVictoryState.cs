using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed record MobaScoreAward(string EventId,int Round,int TeamId,int Points,string Reason,bool Settled);

/// <summary>Idempotent event ledger and simultaneous round-end settlement. No rendering or kill inference.</summary>
public sealed class MobaVictoryState
{
    private readonly List<MobaScoreAward> _awards=new();
    private readonly HashSet<string> _events=new(StringComparer.Ordinal);
    private readonly HashSet<int> _rounds=new();
    public DevelopmentScoringProfile Profile {get;}
    public IReadOnlyList<MobaScoreAward> Awards {get;}
    public int BlueVP {get;private set;}
    public int RedVP {get;private set;}
    public bool IsFinished {get;private set;}
    public int? WinnerTeamId {get;private set;}
    public bool IsDraw=>IsFinished&&WinnerTeamId is null;
    public MobaVictoryState(DevelopmentScoringProfile profile){profile.Validate();Profile=profile;Awards=_awards.AsReadOnly();}
    public int Pending(int team)=>_awards.Where(a=>!a.Settled&&a.TeamId==team).Sum(a=>a.Points);
    internal void Queue(string eventId,int round,int team,int points,string reason)
    {
        if(IsFinished||points==0||!_events.Add(eventId))return;
        if(team is not (1 or 2)||points<0)throw new ArgumentException("Invalid scoring event.");
        _awards.Add(new(eventId,round,team,points,reason,false));
    }
    internal void Settle(int round)
    {
        if(IsFinished||!_rounds.Add(round))return;
        for(int i=0;i<_awards.Count;i++)
        {
            var a=_awards[i];if(a.Settled||a.Round>round)continue;
            if(a.TeamId==1)BlueVP=checked(BlueVP+a.Points);else RedVP=checked(RedVP+a.Points);
            _awards[i]=a with {Settled=true};
        }
        bool blue=BlueVP>=DevelopmentScoringProfile.VictoryThreshold,red=RedVP>=DevelopmentScoringProfile.VictoryThreshold;
        if(!blue&&!red)return;
        IsFinished=true;
        WinnerTeamId=blue&&!red?1:red&&!blue?2:Profile.TiePolicy==DevelopmentTiePolicy.HigherScoreThenDraw&&BlueVP!=RedVP?(BlueVP>RedVP?1:2):null;
    }
}
