using System;

namespace PawnsOfDivinity.Core.Characters;

/// <summary>An immutable character template, separate from an instance's battle state.</summary>
public sealed record CharacterDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public CharacterStats BaseStats { get; }

    public CharacterDefinition(
        string Id,
        string Name,
        string Description,
        CharacterStats BaseStats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentNullException.ThrowIfNull(Description);
        ArgumentNullException.ThrowIfNull(BaseStats);

        this.Id = Id;
        this.Name = Name;
        this.Description = Description;
        this.BaseStats = BaseStats;
    }
}
