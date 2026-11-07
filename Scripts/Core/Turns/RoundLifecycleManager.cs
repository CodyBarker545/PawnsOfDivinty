using System;

namespace PawnsOfDivinity.Core.Turns;

/// <summary>Owns the round number and validates phase order without executing phase effects.</summary>
/// <remarks>
/// Phase changes are explicit acknowledgements from the caller. This foundation does not
/// determine whether shop, damage, activation or scoring work has finished. A future
/// coordinator must perform and validate that work before acknowledging each transition.
/// </remarks>
public sealed class RoundLifecycleManager
{
    private bool _poisonBatchProcessed;
    public int CurrentRound { get; private set; }
    public RoundPhase CurrentPhase { get; private set; } = RoundPhase.Shop;

    public RoundLifecycleManager(int initialRound = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRound);
        CurrentRound = initialRound;
    }

    /// <summary>Moves forward exactly one phase. Objective scoring requires a separate new-round call.</summary>
    public void AdvancePhase() => AdvanceTo(GetNextPhase());

    /// <summary>Advances only if the requested phase is the immediate successor.</summary>
    public void AdvanceTo(RoundPhase nextPhase)
    {
        if (!Enum.IsDefined(nextPhase)) throw new ArgumentOutOfRangeException(nameof(nextPhase));
        if (nextPhase != GetNextPhase())
            throw new InvalidOperationException("Phases must advance one step in the documented round order.");
        CurrentPhase = nextPhase;
    }

    /// <summary>Begins the next numbered round at Shop after objective scoring has been acknowledged.</summary>
    /// <remarks>This does not call ActivationManager.StartNextRound or reset any character state.</remarks>
    public void StartNextRound()
    {
        if (CurrentPhase != RoundPhase.ObjectiveScoring)
            throw new InvalidOperationException("Only objective scoring can be followed by a new round.");
        int nextRound = checked(CurrentRound + 1);
        CurrentRound = nextRound;
        CurrentPhase = RoundPhase.Shop;
        _poisonBatchProcessed = false;
    }

    // The guard belongs to this round lifecycle, so replacing a batch processor cannot run it twice.
    // Phase advancement remains explicit; this does not execute or automatically advance a phase.
    internal void ValidatePoisonBatchExecution()
    {
        if (CurrentPhase != RoundPhase.PoisonBatch)
            throw new InvalidOperationException("Poison batches can only run during the Poison batch phase.");
        if (_poisonBatchProcessed)
            throw new InvalidOperationException("The Poison batch has already been processed this round.");
    }

    internal void RecordPoisonBatchExecution()
    {
        ValidatePoisonBatchExecution();
        _poisonBatchProcessed = true;
    }

    private RoundPhase GetNextPhase() => CurrentPhase switch
    {
        RoundPhase.Shop => RoundPhase.PoisonBatch,
        RoundPhase.PoisonBatch => RoundPhase.DelayedAttacks,
        RoundPhase.DelayedAttacks => RoundPhase.TimedStatusUpdate,
        RoundPhase.TimedStatusUpdate => RoundPhase.OtherRoundStartTriggers,
        RoundPhase.OtherRoundStartTriggers => RoundPhase.Preparation,
        RoundPhase.Preparation => RoundPhase.AlternatingActivations,
        RoundPhase.AlternatingActivations => RoundPhase.Cleanup,
        RoundPhase.Cleanup => RoundPhase.ObjectiveScoring,
        RoundPhase.ObjectiveScoring => throw new InvalidOperationException("Use StartNextRound after objective scoring."),
        _ => throw new InvalidOperationException("Unknown current round phase.")
    };
}
