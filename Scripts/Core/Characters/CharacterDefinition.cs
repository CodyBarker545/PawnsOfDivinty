using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Characters;

/// <summary>An immutable character template, separate from an instance's battle state.</summary>
public sealed record CharacterDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public CharacterStats BaseStats { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }

    public CharacterDefinition(
        string Id,
        string Name,
        string Description,
        CharacterStats BaseStats)
        : this(Id, Name, Description, BaseStats, Array.Empty<AbilityDefinition>())
    {
    }

    /// <summary>Associates immutable ability metadata while preserving the existing template architecture.</summary>
    public CharacterDefinition(string Id, string Name, string Description, CharacterStats BaseStats,
        IEnumerable<AbilityDefinition> Abilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentNullException.ThrowIfNull(Description);
        ArgumentNullException.ThrowIfNull(BaseStats);
        ArgumentNullException.ThrowIfNull(Abilities);
        var abilitySnapshot = Abilities.ToArray();
        var abilityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ability in abilitySnapshot)
        {
            if (ability is null) throw new ArgumentException("Ability collections cannot contain null definitions.", nameof(Abilities));
            if (!abilityIds.Add(ability.Id)) throw new ArgumentException("Ability IDs must be unique within a character definition.", nameof(Abilities));
        }

        this.Id = Id;
        this.Name = Name;
        this.Description = Description;
        this.BaseStats = BaseStats;
        this.Abilities = Array.AsReadOnly(abilitySnapshot);
    }
}
