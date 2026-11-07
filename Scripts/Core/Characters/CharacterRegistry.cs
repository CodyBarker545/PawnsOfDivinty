using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Characters;

public static class CharacterRegistry
{
    // Derive keys from the definitions so lookup keys cannot disagree with their IDs.
    // ToDictionary rejects duplicate IDs with ArgumentException during initialization.
    private static readonly Dictionary<string, CharacterDefinition> Characters = new[]
    {
        Nigel.Definition,
        BeothukSpiritGuardian.Definition,
        HoodedAssassin.Definition,
        GentleGuardian.Definition,
        TimeWeaver.Definition,
        GraveboundColossus.Definition
    }.ToDictionary(character => character.Id, StringComparer.Ordinal);

    /// <summary>Finds an exact, case-sensitive ID; returns null for missing or blank IDs.</summary>
    public static CharacterDefinition? GetCharacter(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return Characters.TryGetValue(id, out var character)
            ? character
            : null;
    }

    /// <summary>Enumerates the registered definitions through a read-only values collection.</summary>
    public static IEnumerable<CharacterDefinition> GetAllCharacters()
    {
        return Characters.Values;
    }
}
