using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Turns;

/// <summary>Resets activation readiness and received-healing history at the documented Preparation boundary.</summary>
public sealed class RoundPreparationCoordinator
{
    private readonly ActivationManager _activations;
    private readonly RoundLifecycleManager _lifecycle;

    public RoundPreparationCoordinator(ActivationManager activations, RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(activations);
        ArgumentNullException.ThrowIfNull(lifecycle);
        _activations = activations;
        _lifecycle = lifecycle;
    }

    /// <summary>Prepares exactly the managed match roster and returns summaries in supplied roster order.</summary>
    /// <remarks>
    /// The full roster is required so activation resets and healing resets cannot affect different
    /// subsets. Resetting a defeated character's flags or counters does not make it alive or eligible.
    /// Cooldowns retain absolute deadlines; health, shields, statuses and stats are untouched.
    /// Other Triggers is currently only a phase acknowledgement. No redeployment or other effects
    /// run here, and neither the round number nor the current phase is advanced.
    /// This synchronous operation requires match state not to change concurrently.
    /// </remarks>
    public RoundPreparationResult Process(IEnumerable<Character> participants, int startingTeamId)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _lifecycle.ValidatePreparationExecution();
        int round = _lifecycle.CurrentRound;
        var roster = participants.ToArray();
        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in roster)
        {
            if (character is null) throw new ArgumentException("Participants cannot contain null characters.", nameof(participants));
            if (!instances.Add(character) || !ids.Add(character.Id))
                throw new ArgumentException("Participants must have unique instances and instance IDs.", nameof(participants));
        }
        if (!instances.SetEquals(_activations.FirstTeam.Concat(_activations.SecondTeam)))
            throw new ArgumentException("Preparation must include exactly the activation manager's character instances.", nameof(participants));
        _activations.ValidateRoundPreparation(_lifecycle, startingTeamId);
        if (_lifecycle.CurrentRound != round)
            throw new InvalidOperationException("The round changed while validating preparation.");
        _lifecycle.RecordPreparationExecution();

        _activations.PrepareActivationReadiness(startingTeamId);
        foreach (var character in roster) character.ResetHealingHistory();
        return new RoundPreparationResult(round, startingTeamId,
            roster.Select(character => new PreparedCharacter(character.Id, character.CanAct)));
    }
}
