using System;
using System.Collections.Generic;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Implements the real RNG interfaces with explicit scripted values, never a random fallback.</summary>
public sealed class ScriptedScenarioRandom : IRandomRollProvider, IRandomIndexProvider
{
    private readonly Queue<int> _values;
    private readonly string _name;
    private readonly Action<string, int?, int, ScenarioDrawOutcome> _record;
    public ScriptedScenarioRandom(string name, IEnumerable<int> values, Action<string, int?, int, ScenarioDrawOutcome> record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(record);
        _name = name; _values = new(values); _record = record;
    }
    public int NextRoll() => Take(100);
    public int NextIndex(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        return Take(exclusiveMaximum);
    }
    private int Take(int maximum)
    {
        if (!_values.TryDequeue(out int value))
        {
            _record(_name, null, maximum, ScenarioDrawOutcome.Exhausted);
            throw new InvalidOperationException($"Scripted RNG '{_name}' exhausted.");
        }
        bool valid = value >= 0 && value < maximum;
        _record(_name, value, maximum, valid ? ScenarioDrawOutcome.Valid : ScenarioDrawOutcome.Invalid);
        if (!valid) throw new InvalidOperationException($"Scripted RNG '{_name}' returned {value}; expected 0..{maximum - 1}.");
        return value;
    }
}
