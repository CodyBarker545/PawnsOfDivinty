using System;

namespace PawnsOfDivinity.Core.Characters;

public static class CharacterFactory
{
    /// <summary>
    /// Creates a character with a generated instance ID from an exact registered definition ID.
    /// Returns null when the definition ID is unknown, null, or blank, matching the registry.
    /// </summary>
    public static Character? Create(string? characterId, int teamId)
    {
        var definition = CharacterRegistry.GetCharacter(characterId);
        if (definition is null) return null;

        return Create(definition, Guid.NewGuid().ToString("N"), teamId);
    }

    /// <summary>Creates an instance using a caller-supplied ID; the caller owns its uniqueness.</summary>
    public static Character Create(
        CharacterDefinition definition,
        string instanceId,
        int teamId
    )
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new Character(
            id: instanceId,
            name: definition.Name,
            teamId: teamId,
            baseStats: definition.BaseStats
        );
    }
}
