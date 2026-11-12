using Godot;
using PawnsOfDivinity.Gameplay.Levels;
[Tool,GlobalClass]
public partial class BetaAssassinSettings : Resource
{
    [Export] public bool EnableDevelopmentUnarmedFallback {get;set;}=true;
    [Export(PropertyHint.Range,"0,1000,1,or_greater")] public int DevelopmentUnarmedPower {get;set;}
    [Export(PropertyHint.Range,"0,100,1")] public int DevelopmentUnarmedAccuracy {get;set;}=90;
    [Export] public bool DevelopmentSmokeOverObstacles {get;set;}=true;
    public AssassinDevelopmentProfile Snapshot(){var p=new AssassinDevelopmentProfile(EnableDevelopmentUnarmedFallback,DevelopmentUnarmedPower,DevelopmentUnarmedAccuracy,DevelopmentSmokeOverObstacles);p.Validate();return p;}
}
