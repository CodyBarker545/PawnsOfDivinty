namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A read-only visibility snapshot; reevaluate after positions, attacks or smoke coverage change.</summary>
public sealed record SmokeVisibilityResult
{
    public string CharacterId { get; }
    public int TeamId { get; }
    public GridPosition Position { get; }
    public bool IsInSmoke { get; }
    public bool EnemyAdjacent { get; }
    public bool RevealedByAttack { get; }
    public bool IsInvisible { get; }

    internal SmokeVisibilityResult(string characterId, int teamId, GridPosition position, bool isInSmoke,
        bool enemyAdjacent, bool revealedByAttack, bool isInvisible)
    {
        CharacterId = characterId;
        TeamId = teamId;
        Position = position;
        IsInSmoke = isInSmoke;
        EnemyAdjacent = enemyAdjacent;
        RevealedByAttack = revealedByAttack;
        IsInvisible = isInvisible;
    }
}
