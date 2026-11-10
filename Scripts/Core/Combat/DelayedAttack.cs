using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>An immutable cast-time outcome addressed by character instance IDs.</summary>
public sealed record DelayedAttack
{
    public string Id { get; }
    public string SourceCharacterId { get; }
    public string TargetCharacterId { get; }
    public int ScheduledRound { get; }
    public int ResolutionRound { get; }
    public StrikeResult Strike { get; }
    /// <summary>Zero before insertion; the scheduler assigns a positive sequence to its stored copy.</summary>
    public long InsertionSequence { get; }

    public DelayedAttack(string id, string sourceCharacterId, string targetCharacterId,
        int scheduledRound, int resolutionRound, StrikeResult strike)
    {
        Id = id;
        SourceCharacterId = sourceCharacterId;
        TargetCharacterId = targetCharacterId;
        ScheduledRound = scheduledRound;
        ResolutionRound = resolutionRound;
        Strike = strike;
        Validate();
    }

    // Shared by construction, scheduling and batch prevalidation; no outcome is recalculated.
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("An attack ID is required.", "id");
        if (string.IsNullOrWhiteSpace(SourceCharacterId)) throw new ArgumentException("A source instance ID is required.", "sourceCharacterId");
        if (string.IsNullOrWhiteSpace(TargetCharacterId)) throw new ArgumentException("A target instance ID is required.", "targetCharacterId");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ScheduledRound, "scheduledRound");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ResolutionRound, "resolutionRound");
        if (ResolutionRound < ScheduledRound)
            throw new ArgumentOutOfRangeException("resolutionRound", "Resolution cannot precede scheduling.");
        ArgumentNullException.ThrowIfNull(Strike, "strike");
        ArgumentOutOfRangeException.ThrowIfNegative(Strike.Damage, "strike");
        if (!Strike.Hit && Strike.Damage != 0)
            throw new ArgumentException("A missed attack must store zero damage.", "strike");
        ArgumentOutOfRangeException.ThrowIfNegative(InsertionSequence);
    }

    // Reuse the validated outcome when assigning order, without changing the caller's snapshot.
    private DelayedAttack(DelayedAttack attack, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        Id = attack.Id;
        SourceCharacterId = attack.SourceCharacterId;
        TargetCharacterId = attack.TargetCharacterId;
        ScheduledRound = attack.ScheduledRound;
        ResolutionRound = attack.ResolutionRound;
        Strike = attack.Strike;
        InsertionSequence = sequence;
    }

    internal DelayedAttack WithInsertionSequence(long sequence) => new(this, sequence);

    /// <summary>Uses Temporal Ignition's confirmed next-round timing from spec v0.2, section 12.3.</summary>
    /// <remarks>The caller supplies the complete cast-time strike. This does not roll or add ability bonuses.</remarks>
    public static DelayedAttack CreateTemporalIgnition(string id, string sourceCharacterId,
        string targetCharacterId, int scheduledRound, StrikeResult strike)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scheduledRound);
        return new DelayedAttack(id, sourceCharacterId, targetCharacterId, scheduledRound,
            checked(scheduledRound + 1), strike);
    }
}
