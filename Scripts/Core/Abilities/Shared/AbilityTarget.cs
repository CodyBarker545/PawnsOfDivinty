using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A proposed target. Missing selection data is reported by validation rather than guessed.</summary>
public sealed record AbilityTarget
{
    public AbilityTargetType Type { get; }
    public GridPosition? Position { get; }
    public Character? Character { get; }

    public AbilityTarget(AbilityTargetType type, GridPosition? position = null, Character? character = null)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (type != AbilityTargetType.Character && character is not null)
            throw new ArgumentException("Only character-form targets carry a character reference.", nameof(character));
        Type = type;
        Position = position;
        Character = character;
    }
}
