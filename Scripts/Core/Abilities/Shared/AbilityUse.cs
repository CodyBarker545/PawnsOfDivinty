using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A single attempted use, created during selection without consuming an action or cooldown.</summary>
/// <remarks>Keep the same instance through targeting and finalization; it is the once-only bookkeeping handle.</remarks>
public sealed class AbilityUse
{
    public string CharacterId => Character.Id;
    public string AbilityId { get; }
    public int RoundNumber { get; }
    internal Character Character { get; }
    internal ActivationManager Activations { get; }
    internal bool IsFinalized { get; private set; }
    internal bool BrutalKnockbackStarted { get; private set; }
    internal bool GentleBlessingProcessed { get; private set; }
    internal string? GentleBlessingRecipientId { get; private set; }

    public AbilityUse(Character character, string abilityId, ActivationManager activations)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(activations);
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);
        Character = character;
        AbilityId = abilityId;
        Activations = activations;
        RoundNumber = activations.CurrentRound;
    }

    internal void MarkFinalized() => IsFinalized = true;
    internal void MarkBrutalKnockbackStarted() => BrutalKnockbackStarted = true;
    internal void MarkGentleBlessingProcessed() => GentleBlessingProcessed = true;
    internal void BindGentleBlessingRecipient(string recipientId) => GentleBlessingRecipientId = recipientId;
}
