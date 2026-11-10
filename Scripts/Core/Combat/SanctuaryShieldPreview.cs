namespace PawnsOfDivinity.Core.Combat;

public enum SanctuaryShieldOperation { None, Grant, Refresh, Transfer }
public enum SanctuaryShieldRejectionReason { None, RecipientDefeated, OtherShieldSourceUnsupported }

/// <summary>A shield-manager snapshot, independent of targeting range and permission to spend an action.</summary>
public sealed record SanctuaryShieldPreview
{
    public string CasterId { get; }
    public string RecipientId { get; }
    public string? PreviousRecipientId { get; }
    public int PreviousRecipientShieldHP { get; }
    public int RecipientShieldHP { get; }
    public int ExpectedShieldHP { get; }
    public SanctuaryShieldOperation Operation { get; }
    public SanctuaryShieldRejectionReason RejectionReason { get; }
    public bool IsAllowed => RejectionReason == SanctuaryShieldRejectionReason.None;

    internal SanctuaryShieldPreview(string casterId, string recipientId, string? previousRecipientId,
        int previousShieldHP, int currentShieldHP, int expectedShieldHP,
        SanctuaryShieldOperation operation, SanctuaryShieldRejectionReason reason)
    {
        CasterId = casterId;
        RecipientId = recipientId;
        PreviousRecipientId = previousRecipientId;
        PreviousRecipientShieldHP = previousShieldHP;
        RecipientShieldHP = currentShieldHP;
        ExpectedShieldHP = expectedShieldHP;
        Operation = operation;
        RejectionReason = reason;
    }
}
