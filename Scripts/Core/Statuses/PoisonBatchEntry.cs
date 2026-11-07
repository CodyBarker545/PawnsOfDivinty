namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Identifies the character for one immutable tick result without storing mutable character state.</summary>
public sealed record PoisonBatchEntry
{
    public string CharacterId { get; }
    public PoisonTickResult Tick { get; }

    internal PoisonBatchEntry(string characterId, PoisonTickResult tick)
    {
        CharacterId = characterId;
        Tick = tick;
    }
}
