using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Checks an explicit target proposal without executing an effect or checking action eligibility.</summary>
public static class AbilityTargeting
{
    /// <remarks>
    /// Positions and confirmed constraints come from the caller; this does not query TacticalMap.
    /// Map legality can be supplied by a map adapter. Line-of-sight and elevation targeting rules
    /// remain unresolved in spec v0.2, so requiring them cannot yield an unconditional legal result.
    /// A ManhattanRadius is explicitly Manhattan; an unspecified radius does not choose a metric.
    /// A tile at the source position is a tile selection, not implicitly a character self-target.
    /// </remarks>
    public static AbilityTargetingResult Validate(Character source, GridPosition? sourcePosition,
        AbilityDefinition ability, AbilityTarget target, AbilityTargetingConstraints constraints,
        AbilityRange? equipmentRange = null, bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(constraints);
        if (equipmentRange is not null && equipmentRange.Kind != AbilityRangeKind.ManhattanDistance)
            throw new ArgumentException("Resolved equipment range must use Manhattan distance.", nameof(equipmentRange));

        GridPosition? targetPosition = target.Type == AbilityTargetType.Self ? sourcePosition : target.Position;
        long? distance = null;
        if (!sourcePosition.HasValue || !targetPosition.HasValue)
            return Result(AbilityTargetingRejectionReason.MissingRequiredTargetInformation);
        if (!constraints.TargetType.HasValue) return Result(AbilityTargetingRejectionReason.UnresolvedTargetingRule);
        if (target.Type != constraints.TargetType &&
            !(target.Type == AbilityTargetType.Self && constraints.TargetType == AbilityTargetType.Character))
            return Result(AbilityTargetingRejectionReason.InvalidTargetType);
        var recipient = target.Type == AbilityTargetType.Self ? source : target.Character;
        if (target.Type == AbilityTargetType.Character && recipient is null)
            return Result(AbilityTargetingRejectionReason.MissingRequiredTargetInformation);

        var range = ability.Range;
        if (range is null) return Result(AbilityTargetingRejectionReason.UnresolvedRange);
        if (range.Kind == AbilityRangeKind.EquippedWeapon)
        {
            if (equipmentRange is null || !equipmentRange.Minimum.HasValue || !equipmentRange.Maximum.HasValue)
                return Result(AbilityTargetingRejectionReason.MissingRequiredTargetInformation);
            range = equipmentRange;
        }
        if (range.Kind == AbilityRangeKind.UnspecifiedRadius)
            return Result(AbilityTargetingRejectionReason.UnresolvedRadiusGeometry);
        if (!range.Minimum.HasValue || !range.Maximum.HasValue)
            return Result(AbilityTargetingRejectionReason.UnresolvedRange);
        distance = sourcePosition.Value.ManhattanDistanceTo(targetPosition.Value);

        if (recipient is not null)
        {
            bool isSelf = StringComparer.Ordinal.Equals(recipient.Id, source.Id);
            if (isSelf)
            {
                if (target.Position.HasValue && target.Position != sourcePosition)
                    return Result(AbilityTargetingRejectionReason.InvalidSelfTarget);
                if (!constraints.AllowSelf.HasValue) return Result(AbilityTargetingRejectionReason.UnresolvedTargetingRule);
                if (!constraints.AllowSelf.Value) return Result(AbilityTargetingRejectionReason.InvalidSelfTarget);
            }
            if (!constraints.Team.HasValue || !constraints.RequireLivingTarget.HasValue)
                return Result(AbilityTargetingRejectionReason.UnresolvedTargetingRule);
            bool allied = recipient.TeamId == source.TeamId;
            if ((constraints.Team == AbilityTargetTeam.Ally && !allied) || (constraints.Team == AbilityTargetTeam.Enemy && allied))
                return Result(AbilityTargetingRejectionReason.WrongTeam);
            if (constraints.RequireLivingTarget.Value && recipient.IsDefeated)
                return Result(AbilityTargetingRejectionReason.TargetDefeated);
        }
        if (distance.Value < range.Minimum.Value) return Result(AbilityTargetingRejectionReason.InsideMinimumRange);
        if (distance.Value > range.Maximum.Value) return Result(AbilityTargetingRejectionReason.OutsideMaximumRange);

        if (!constraints.RequiresMapValidation.HasValue) return Result(AbilityTargetingRejectionReason.UnresolvedTargetingRule);
        if (constraints.RequiresMapValidation.Value)
        {
            if (!mapTargetLegal.HasValue) return Result(AbilityTargetingRejectionReason.MissingRequiredTargetInformation);
            if (!mapTargetLegal.Value) return Result(AbilityTargetingRejectionReason.MapTargetIllegal);
        }
        if (!constraints.RequiresLineOfSight.HasValue || !constraints.RequiresElevationRules.HasValue)
            return Result(AbilityTargetingRejectionReason.UnresolvedTargetingRule);
        if (constraints.RequiresLineOfSight.Value) return Result(AbilityTargetingRejectionReason.UnresolvedLineOfSight);
        if (constraints.RequiresElevationRules.Value) return Result(AbilityTargetingRejectionReason.UnresolvedElevation);
        return Result(AbilityTargetingRejectionReason.None);

        AbilityTargetingResult Result(AbilityTargetingRejectionReason reason) => new(reason, sourcePosition, targetPosition, distance);
    }
}
