using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Gets rolls from a reusable .NET random-number generator.</summary>
public sealed class SystemRandomRollProvider : IRandomRollProvider
{
    private readonly Random _random;

    /// <summary>Uses Random.Shared so strikes do not each create their own generator.</summary>
    public SystemRandomRollProvider() : this(Random.Shared)
    {
    }

    /// <summary>Uses an existing generator. Pass a seeded Random for repeatable simulations.</summary>
    public SystemRandomRollProvider(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    /// <summary>Next(100) returns 0-99 because the upper bound is excluded.</summary>
    public int NextRoll() => _random.Next(100);
}
