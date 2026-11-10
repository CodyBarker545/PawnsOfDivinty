using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Immutable descriptive ability data, separate from effects, execution and runtime cooldowns.</summary>
public sealed record AbilityDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public AbilityClassification? Classification { get; }
    /// <summary>Null means unconfirmed, which must not be treated as an explicit non-spell tag.</summary>
    public bool? IsSpellTagged { get; }
    public AbilityActionType? RequiredAction { get; }
    public AbilityRange? Range { get; }
    /// <summary>Null means unknown; zero explicitly means no cooldown interval.</summary>
    public int? ReuseInterval { get; }

    public AbilityDefinition(string id, string displayName, string description,
        AbilityClassification? classification = null, bool? isSpellTagged = null,
        AbilityActionType? requiredAction = null, AbilityRange? range = null, int? reuseInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(description);
        if (classification.HasValue && !Enum.IsDefined(classification.Value))
            throw new ArgumentOutOfRangeException(nameof(classification));
        if (requiredAction.HasValue && !Enum.IsDefined(requiredAction.Value))
            throw new ArgumentOutOfRangeException(nameof(requiredAction));
        if (reuseInterval.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(reuseInterval.Value, nameof(reuseInterval));
        Id = id;
        DisplayName = displayName;
        Description = description;
        Classification = classification;
        IsSpellTagged = isSpellTagged;
        RequiredAction = requiredAction;
        Range = range;
        ReuseInterval = reuseInterval;
    }
}
