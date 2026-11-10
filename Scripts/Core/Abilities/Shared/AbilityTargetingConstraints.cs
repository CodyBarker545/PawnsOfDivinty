using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Explicit caller-supplied permissions; null means an applicable rule is not established.</summary>
public sealed record AbilityTargetingConstraints
{
    public AbilityTargetType? TargetType { get; }
    public bool? AllowSelf { get; }
    public AbilityTargetTeam? Team { get; }
    public bool? RequireLivingTarget { get; }
    public bool? RequiresMapValidation { get; }
    public bool? RequiresLineOfSight { get; }
    public bool? RequiresElevationRules { get; }

    public AbilityTargetingConstraints(AbilityTargetType? targetType = null, bool? allowSelf = null,
        AbilityTargetTeam? team = null, bool? requireLivingTarget = null,
        bool? requiresMapValidation = null, bool? requiresLineOfSight = null, bool? requiresElevationRules = null)
    {
        if (targetType.HasValue && !Enum.IsDefined(targetType.Value)) throw new ArgumentOutOfRangeException(nameof(targetType));
        if (team.HasValue && !Enum.IsDefined(team.Value)) throw new ArgumentOutOfRangeException(nameof(team));
        TargetType = targetType;
        AllowSelf = allowSelf;
        Team = team;
        RequireLivingTarget = requireLivingTarget;
        RequiresMapValidation = requiresMapValidation;
        RequiresLineOfSight = requiresLineOfSight;
        RequiresElevationRules = requiresElevationRules;
    }
}
