using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Grants, refreshes or transfers one Guardian instance's Sanctuary Shield.</summary>
/// <remarks>
/// Characters still have one shield HP value. Applying onto a live shield from another source
/// is unsupported and rejected rather than erasing it or inventing stacking rules.
/// Range, allied targeting, caster eligibility and action costs belong to the later ability layer.
/// A Guardian dying does not remove its shield from another living character.
/// </remarks>
public sealed class SanctuaryShieldManager
{
    private const int ShieldAmount = 6;

    /// <summary>Predicts application without changing shields or cleaning up cached ownership.</summary>
    public SanctuaryShieldPreview Preview(Character guardian, Character recipient)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(recipient);
        var previous = guardian.SanctuaryShieldRecipient;
        if (previous is not null && !previous.HasShieldFrom(guardian)) previous = null;
        var reason = SanctuaryShieldRejectionReason.None;
        try
        {
            // Use the same non-mutating assignment check as Apply. Its established
            // InvalidOperationException cases are defeat and conflicting shield sources.
            recipient.ValidateShieldAssignment(ShieldAmount, guardian);
        }
        catch (InvalidOperationException)
        {
            reason = recipient.IsDefeated ? SanctuaryShieldRejectionReason.RecipientDefeated
                : SanctuaryShieldRejectionReason.OtherShieldSourceUnsupported;
        }
        var operation = reason != SanctuaryShieldRejectionReason.None ? SanctuaryShieldOperation.None
            : ReferenceEquals(previous, recipient) ? SanctuaryShieldOperation.Refresh
            : previous is not null ? SanctuaryShieldOperation.Transfer : SanctuaryShieldOperation.Grant;
        return new(guardian.Id, recipient.Id, previous?.Id, previous?.ShieldHP ?? 0, recipient.ShieldHP,
            reason == SanctuaryShieldRejectionReason.None ? ShieldAmount : recipient.ShieldHP, operation, reason);
    }

    /// <summary>Gives the recipient 6 shield HP, removing only this Guardian's previous shield.</summary>
    public void Apply(Character guardian, Character recipient)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(recipient);

        // Check the destination first. An invalid transfer must leave the old shield untouched.
        recipient.ValidateShieldAssignment(ShieldAmount, guardian);
        var previous = GetRecipient(guardian);
        if (previous is not null && !ReferenceEquals(previous, recipient))
            previous.SetShieldHP(0, guardian);

        recipient.SetShieldHP(ShieldAmount, guardian);
        guardian.SanctuaryShieldRecipient = recipient;
    }

    /// <summary>Returns the active recipient, or null if no shield remains.</summary>
    public Character? GetRecipient(Character guardian)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        var recipient = guardian.SanctuaryShieldRecipient;
        if (recipient is not null && !recipient.HasShieldFrom(guardian))
        {
            guardian.SanctuaryShieldRecipient = null;
            return null;
        }

        return recipient;
    }
}
