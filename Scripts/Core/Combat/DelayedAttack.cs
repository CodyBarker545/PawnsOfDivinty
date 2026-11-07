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
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An attack ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(sourceCharacterId)) throw new ArgumentException("A source instance ID is required.", nameof(sourceCharacterId));
        if (string.IsNullOrWhiteSpace(targetCharacterId)) throw new ArgumentException("A target instance ID is required.", nameof(targetCharacterId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scheduledRound);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolutionRound);
        if (resolutionRound < scheduledRound)
            throw new ArgumentOutOfRangeException(nameof(resolutionRound), "Resolution cannot precede scheduling.");
        ArgumentNullException.ThrowIfNull(strike);
        ArgumentOutOfRangeException.ThrowIfNegative(strike.Damage, nameof(strike));
        if (!strike.Hit && strike.Damage != 0)
            throw new ArgumentException("A missed attack must store zero damage.", nameof(strike));

        Id = id;
        SourceCharacterId = sourceCharacterId;
        TargetCharacterId = targetCharacterId;
        ScheduledRound = scheduledRound;
        ResolutionRound = resolutionRound;
        Strike = strike;
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
