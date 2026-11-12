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
    private bool _delayedAttacksProcessed;
    private bool _timedStatusesProcessed;
    private bool _preparationProcessed;
    public int CurrentRound { get; private set; }
    public RoundPhase CurrentPhase { get; private set; } = RoundPhase.Shop;
    /// <summary>The single match-owned Cleanup boundary, when configured. Expiration does not advance phases.</summary>
    public RoundCleanupCoordinator? CleanupCoordinator { get; private set; }
    public MovementActionExecutor? MovementExecutor { get; private set; }

    internal void RegisterMovementExecutor(MovementActionExecutor executor)
    {
        if (MovementExecutor is not null) throw new InvalidOperationException("Reuse this match's movement executor.");
        MovementExecutor = executor;
    }

    internal void RegisterCleanupCoordinator(RoundCleanupCoordinator coordinator)
    {
        if (CleanupCoordinator is not null)
            throw new InvalidOperationException("This lifecycle already owns a Cleanup coordinator. Reuse that instance.");
        CleanupCoordinator = coordinator;
    }

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
        _delayedAttacksProcessed = false;
        _timedStatusesProcessed = false;
        _preparationProcessed = false;
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

    internal void ValidateDelayedAttackExecution()
    {
        if (CurrentPhase != RoundPhase.DelayedAttacks)
            throw new InvalidOperationException("Delayed attacks can only resolve in the Delayed Attacks phase.");
        if (!_poisonBatchProcessed)
            throw new InvalidOperationException("Process the Poison batch before resolving delayed attacks.");
        if (_delayedAttacksProcessed)
            throw new InvalidOperationException("Delayed attacks have already been processed this round.");
    }

    internal void RecordDelayedAttackExecution()
    {
        ValidateDelayedAttackExecution();
        _delayedAttacksProcessed = true;
    }

    internal void ValidateTimedStatusExecution()
    {
        if (CurrentPhase != RoundPhase.TimedStatusUpdate)
            throw new InvalidOperationException("Timed statuses can only update during the Timed-Status phase.");
        if (!_poisonBatchProcessed)
            throw new InvalidOperationException("Process the Poison batch before updating timed statuses.");
        if (!_delayedAttacksProcessed)
            throw new InvalidOperationException("Process delayed attacks before updating timed statuses.");
        if (_timedStatusesProcessed)
            throw new InvalidOperationException("Timed statuses have already been processed this round.");
    }

    internal void RecordTimedStatusExecution()
    {
        ValidateTimedStatusExecution();
        _timedStatusesProcessed = true;
    }

    internal void ValidatePreparationExecution()
    {
        if (CurrentPhase != RoundPhase.Preparation)
            throw new InvalidOperationException("Round preparation can only run during the Preparation phase.");
        if (!_poisonBatchProcessed || !_delayedAttacksProcessed || !_timedStatusesProcessed)
            throw new InvalidOperationException("Complete Poison, delayed attacks and timed statuses before preparation.");
        if (_preparationProcessed)
            throw new InvalidOperationException("Preparation has already been processed this round.");
        // Reaching Preparation acknowledges the preceding Other Triggers phase.
        // That extension point currently has no gameplay effects to execute.
    }

    internal void RecordPreparationExecution()
    {
        ValidatePreparationExecution();
        _preparationProcessed = true;
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
