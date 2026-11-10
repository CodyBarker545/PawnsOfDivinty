namespace PawnsOfDivinity.Core.Turns;

/// <summary>An immutable eligibility summary after preparation, not another character state object.</summary>
public sealed record PreparedCharacter
{
    public string CharacterId { get; }
    public bool CanAct { get; }

    internal PreparedCharacter(string characterId, bool canAct)
    {
        CharacterId = characterId;
        CanAct = canAct;
    }
}
