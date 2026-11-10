using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Terrain;

/// <summary>An immutable report of effects removed by an explicit end-of-round expiration call.</summary>
public sealed record SmokeExpirationResult
{
    public int RoundNumber { get; }
    public IReadOnlyList<SmokeArea> ExpiredAreas { get; }

    internal SmokeExpirationResult(int roundNumber, IEnumerable<SmokeArea> expiredAreas)
    {
        RoundNumber = roundNumber;
        ExpiredAreas = Array.AsReadOnly(expiredAreas.ToArray());
    }
}
