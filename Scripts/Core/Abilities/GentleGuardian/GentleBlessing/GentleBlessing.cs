using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public enum GentleBlessingOutcome
{
    Applied, NotSuccessful, InvalidUseContext, NotPassiveOwner,
    IneligibleAbility, RecipientDefeated, AlreadyProcessed, Refreshed, Rejected
}

public enum GentleBlessingRejectionReason { None, ExpirationRoundOverflow, StatOverflow, ModifierConflict, InvalidModifierState }

/// <summary>Reports the passive separately from the successful supportive action that caused it.</summary>
public sealed record GentleBlessingResult
{
    public string SourceCharacterId { get; }
    public string RecipientCharacterId { get; }
    public int ApplicationRound { get; }
    public int? ExpirationRound { get; }
    public GentleBlessingOutcome Outcome { get; }
    public GentleBlessingRejectionReason RejectionReason { get; }
    public IReadOnlyList<TemporaryStatModifier> Modifiers { get; }

    internal GentleBlessingResult(AbilityUse use, Character recipient, GentleBlessingOutcome outcome,
        int? expirationRound = null, GentleBlessingRejectionReason reason = GentleBlessingRejectionReason.None,
        TemporaryStatModifier[]? modifiers = null)
    {
        SourceCharacterId = use.CharacterId;
        RecipientCharacterId = recipient.Id;
        ApplicationRound = use.RoundNumber;
        Outcome = outcome;
        ExpirationRound = expirationRound;
        RejectionReason = reason;
        Modifiers = Array.AsReadOnly(modifiers ?? Array.Empty<TemporaryStatModifier>());
    }
}

/// <summary>Applies the finalized, non-stacking blessing after a completed supportive ability.</summary>
public static class GentleBlessing
{
    public const int DefenseBonus = 2;
    public const int ResistanceBonus = 2;
    /// <remarks>
    /// The caller supplies the recipient of that resolved effect, not a newly selected target.
    /// GuardianSupportiveUseCoordinator supplies it directly from the executor. Processing is
    /// remembered on the use token, so changing coordinators cannot trigger the same use twice.
    /// Expected modifier conflicts and overflow are reported separately; they never undo the
    /// parent's already completed effect or bookkeeping. Both stat changes validate together.
    /// </remarks>
    public static GentleBlessingResult Process(AbilityUse use, Character recipient, AbilityUseResult? finalization)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(recipient);
        if (!use.IsFinalized || finalization?.Succeeded != true) return Result(GentleBlessingOutcome.NotSuccessful);
        if (finalization.Eligibility.CharacterId != use.CharacterId || finalization.Eligibility.AbilityId != use.AbilityId
            || finalization.RoundNumber != use.RoundNumber) return Result(GentleBlessingOutcome.InvalidUseContext);
        if (use.Character.Definition?.Id != "gentle_guardian"
            || !use.Character.Definition.Abilities.Any(a => a.Id == GentleGuardian.GentleBlessingId
                && a.Classification == AbilityClassification.Passive)) return Result(GentleBlessingOutcome.NotPassiveOwner);
        if (use.AbilityId is not (GentleGuardian.HealingLightId or GentleGuardian.SanctuaryShieldId))
            return Result(GentleBlessingOutcome.IneligibleAbility);
        if (use.GentleBlessingRecipientId != recipient.Id) return Result(GentleBlessingOutcome.InvalidUseContext);
        if (use.GentleBlessingProcessed) return Result(GentleBlessingOutcome.AlreadyProcessed);
        if (recipient.IsDefeated) return Result(GentleBlessingOutcome.RecipientDefeated);
        use.MarkGentleBlessingProcessed();

        int expirationRound;
        try { expirationRound = checked(use.RoundNumber + 1); }
        catch (OverflowException)
        {
            return new(use, recipient, GentleBlessingOutcome.Rejected,
                reason: GentleBlessingRejectionReason.ExpirationRoundOverflow);
        }
        string prefix = $"gentle_blessing/{recipient.Id}/";
        var existing = recipient.TemporaryModifiers.Active.Where(modifier => modifier.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var modifiers = new[]
        {
            Create(TemporaryStat.Defense, DefenseBonus, "defense"),
            Create(TemporaryStat.Resistance, ResistanceBonus, "resistance")
        };
        try { recipient.TemporaryModifiers.RefreshOrAddRange(modifiers); }
        catch (OverflowException) { return Rejected(GentleBlessingRejectionReason.StatOverflow); }
        catch (NotSupportedException) { return Rejected(GentleBlessingRejectionReason.ModifierConflict); }
        catch (ArgumentException) { return Rejected(GentleBlessingRejectionReason.InvalidModifierState); }
        catch (InvalidOperationException) { return Rejected(GentleBlessingRejectionReason.InvalidModifierState); }
        return new(use, recipient, existing.Length == 0 ? GentleBlessingOutcome.Applied : GentleBlessingOutcome.Refreshed,
            expirationRound, modifiers: modifiers);

        TemporaryStatModifier Create(TemporaryStat stat, int value, string suffix)
        {
            string id = existing.FirstOrDefault(modifier => modifier.Stat == stat)?.Id
                ?? $"{prefix}{use.RoundNumber}/{suffix}";
            return new(id, use.CharacterId, recipient.Id, stat, value, use.RoundNumber, expirationRound);
        }
        GentleBlessingResult Rejected(GentleBlessingRejectionReason reason)
            => new(use, recipient, GentleBlessingOutcome.Rejected, expirationRound, reason);

        GentleBlessingResult Result(GentleBlessingOutcome outcome) => new(use, recipient, outcome);
    }
}

/// <summary>A shared completion point for the Guardian's supportive abilities, not an event bus.</summary>
internal static class GuardianSupportiveUseCoordinator
{
    internal static (AbilityUseResult Finalization, GentleBlessingResult Blessing) Finalize(AbilityUse use, Character recipient)
    {
        var finalization = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        if (finalization.Succeeded) use.BindGentleBlessingRecipient(recipient.Id);
        return (finalization, GentleBlessing.Process(use, recipient, finalization));
    }
}
