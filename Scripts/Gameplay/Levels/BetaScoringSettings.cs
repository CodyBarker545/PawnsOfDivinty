using Godot;
using PawnsOfDivinity.Gameplay.Levels;

/// <summary>Inspector-editable development values; copied into immutable match configuration at launch.</summary>
[Tool,GlobalClass]
public partial class BetaScoringSettings : Resource
{
    [Export(PropertyHint.Range,"0,1000,1")] public int DefeatPoints {get;set;}=1;
    [Export(PropertyHint.Range,"0,1000,1")] public int CastleCapturePoints {get;set;}
    [Export(PropertyHint.Range,"0,1000,1")] public int ForwardCastleHoldPoints {get;set;}=1;
    [Export(PropertyHint.Range,"0,1000,1")] public int CentralHoldPoints {get;set;}=2;
    [Export] public DevelopmentTiePolicy TiePolicy {get;set;}=DevelopmentTiePolicy.HigherScoreThenDraw;
    public DevelopmentScoringProfile Snapshot(){var p=new DevelopmentScoringProfile(DefeatPoints,CastleCapturePoints,ForwardCastleHoldPoints,CentralHoldPoints,TiePolicy);p.Validate();return p;}
}
