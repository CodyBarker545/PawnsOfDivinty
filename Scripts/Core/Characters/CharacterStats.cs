using System;

namespace PawnsOfDivinity.Core.Characters;

/// <summary>Validated, immutable base stats shared by character definitions and instances.</summary>
public sealed record CharacterStats
{
    public int MaxHealth { get; }
    public int Strength { get; }
    public int Defense { get; }
    public int Intelligence { get; }
    public int Resistance { get; }
    public int Wisdom { get; }
    public int Speed { get; }
    public int Movement { get; }

    public CharacterStats(
        int MaxHealth,
        int Strength,
        int Defense,
        int Intelligence,
        int Resistance,
        int Wisdom,
        int Speed,
        int Movement)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxHealth);
        ArgumentOutOfRangeException.ThrowIfNegative(Strength);
        ArgumentOutOfRangeException.ThrowIfNegative(Defense);
        ArgumentOutOfRangeException.ThrowIfNegative(Intelligence);
        ArgumentOutOfRangeException.ThrowIfNegative(Resistance);
        ArgumentOutOfRangeException.ThrowIfNegative(Wisdom);
        ArgumentOutOfRangeException.ThrowIfNegative(Speed);
        ArgumentOutOfRangeException.ThrowIfNegative(Movement);

        this.MaxHealth = MaxHealth;
        this.Strength = Strength;
        this.Defense = Defense;
        this.Intelligence = Intelligence;
        this.Resistance = Resistance;
        this.Wisdom = Wisdom;
        this.Speed = Speed;
        this.Movement = Movement;
    }
}

