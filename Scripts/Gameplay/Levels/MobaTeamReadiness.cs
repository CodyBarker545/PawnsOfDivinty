namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Counts readiness from production CanAct, independently of which team may select next.</summary>
public sealed record MobaTeamReadiness(int TeamId,int Ready,int Total,int Living)
{
    public string Text=>$"{(TeamId==1?"BLUE":"RED")}: {Ready}/{Total} READY";
}
