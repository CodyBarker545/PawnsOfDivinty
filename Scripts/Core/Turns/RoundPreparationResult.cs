using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Turns;

/// <summary>Snapshots the prepared roster and explicit starting team without holding mutable characters.</summary>
public sealed record RoundPreparationResult
{
    public int RoundNumber { get; }
    public int StartingTeamId { get; }
    public IReadOnlyList<PreparedCharacter> Entries { get; }

    internal RoundPreparationResult(int roundNumber, int startingTeamId, IEnumerable<PreparedCharacter> entries)
    {
        RoundNumber = roundNumber;
        StartingTeamId = startingTeamId;
        Entries = Array.AsReadOnly(entries.ToArray());
    }
}
