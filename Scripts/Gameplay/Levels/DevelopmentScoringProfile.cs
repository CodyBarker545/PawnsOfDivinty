using System;

namespace PawnsOfDivinity.Gameplay.Levels;
public enum DevelopmentTiePolicy { HigherScoreThenDraw, Draw }

/// <summary>Explicit beta playtest inputs, not permanent balance. All awards settle at round end.</summary>
public sealed record DevelopmentScoringProfile(int DefeatPoints=1,int CastleCapturePoints=0,
    int ForwardCastleHoldPoints=1,int CentralHoldPoints=2,DevelopmentTiePolicy TiePolicy=DevelopmentTiePolicy.HigherScoreThenDraw)
{
    public const int VictoryThreshold=15;
    public static DevelopmentScoringProfile Default=>new();
    public void Validate()
    {
        foreach(int value in new[]{DefeatPoints,CastleCapturePoints,ForwardCastleHoldPoints,CentralHoldPoints})
            if(value is <0 or >1000)throw new ArgumentOutOfRangeException(nameof(value),"Development VP values must be 0–1000.");
        if(!Enum.IsDefined(TiePolicy))throw new ArgumentOutOfRangeException(nameof(TiePolicy));
    }
    public string Label=>$"DEV VP · defeat {DefeatPoints} · capture {CastleCapturePoints} · occupied fort/round {ForwardCastleHoldPoints} · center/round {CentralHoldPoints}";
}
