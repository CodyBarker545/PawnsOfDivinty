using System;

namespace PawnsOfDivinity.Core.Equipment;

/// <summary>Chooses an equally likely index from zero through exclusiveMaximum minus one.</summary>
public interface IRandomIndexProvider
{
    int NextIndex(int exclusiveMaximum);
}

/// <summary>Uses a caller-owned reusable .NET generator; no hidden generator is created per replacement.</summary>
public sealed class SystemRandomIndexProvider : IRandomIndexProvider
{
    private readonly Random _random;

    public SystemRandomIndexProvider(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    public int NextIndex(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        return _random.Next(exclusiveMaximum);
    }
}
