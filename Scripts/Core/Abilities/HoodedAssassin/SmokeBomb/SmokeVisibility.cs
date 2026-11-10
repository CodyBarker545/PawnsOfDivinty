using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Evaluates the Assassin's documented smoke rules without rendering or moving characters.</summary>
public static class SmokeVisibility
{
    /// <remarks>Supply the current positioned roster so all living adjacent enemies can be considered.</remarks>
    public static SmokeVisibilityResult Evaluate(Character character, GridPosition position, SmokePlacementService smoke,
        IEnumerable<(Character Character, GridPosition Position)> participants)
        => EvaluateWithProposedSmoke(character, position, smoke, participants, null);

    // A cast can evaluate its future coverage before committing smoke or position.
    internal static SmokeVisibilityResult EvaluateWithProposedSmoke(Character character, GridPosition position,
        SmokePlacementService smoke, IEnumerable<(Character Character, GridPosition Position)> participants,
        SmokeArea? proposedSmoke)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(smoke);
        ArgumentNullException.ThrowIfNull(participants);
        var roster = participants.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in roster)
        {
            if (entry.Character is null || !ids.Add(entry.Character.Id))
                throw new ArgumentException("The positioned roster must contain non-null characters with unique instance IDs.", nameof(participants));
        }
        bool covered = smoke.IsCovered(position) || proposedSmoke?.Tiles.Contains(position) == true;
        bool enemyAdjacent = roster.Any(entry => !entry.Character.IsDefeated && entry.Character.TeamId != character.TeamId
            && position.ManhattanDistanceTo(entry.Position) == 1);
        bool attackReveal = character.SmokeReveal.RevealedByAttack;
        bool invisible = IsEligibleAssassin(character) && covered && !enemyAdjacent && !attackReveal;
        return new SmokeVisibilityResult(character.Id, character.TeamId, position, covered, enemyAdjacent, attackReveal, invisible);
    }

    internal static bool IsEligibleAssassin(Character character)
        => !character.IsDefeated && character.Definition?.Id == "hooded_assassin";

    /// <summary>A visibility-only gate: hidden enemies are not direct targets, while area effects can still affect them.</summary>
    /// <remarks>
    /// True does not establish range, geometry, team permissions or line of sight. The caller
    /// must identify an actual area effect, not label an arbitrary direct attack as tile targeting.
    /// Fog-of-war and smoke line-of-sight rules remain outside AbilityTargeting for now.
    /// </remarks>
    public static bool CanTargetThroughVisibility(Character observer, SmokeVisibilityResult target, bool isAreaEffect)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        return isAreaEffect || observer.TeamId == target.TeamId || !target.IsInvisible;
    }
}
