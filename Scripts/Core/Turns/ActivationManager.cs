using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Turns;

/// <summary>Coordinates individual activations for two teams without running other round phases.</summary>
public sealed class ActivationManager
{
    private int _preferredTeamId;
    private readonly RoundLifecycleManager? _roundLifecycle;
    private int _standaloneRound = 1;

    public IReadOnlyList<Character> FirstTeam { get; }
    public IReadOnlyList<Character> SecondTeam { get; }
    public int FirstTeamId => FirstTeam[0].TeamId;
    public int SecondTeamId => SecondTeam[0].TeamId;
    /// <summary>Reads the lifecycle's round when connected; standalone callers retain their own legacy counter.</summary>
    public int CurrentRound => _roundLifecycle?.CurrentRound ?? _standaloneRound;
    public Character? ActiveCharacter { get; private set; }

    // Keep the last activation's usage visible until a new selection succeeds.
    // Availability also requires a selected character that can still act.
    public bool MovementUsed { get; private set; }
    public bool UtilityActionUsed { get; private set; }
    public bool MainActionUsed { get; private set; }
    public bool IsActivationActive => ActiveCharacter is not null;
    public bool CanMove => ActiveCharacter?.CanAct == true && !MovementUsed;
    public bool CanUseUtilityAction => ActiveCharacter?.CanAct == true && !UtilityActionUsed;
    public bool CanUseMainAction => ActiveCharacter?.CanAct == true && !MainActionUsed;
    internal bool IsAbilityPhase => _roundLifecycle is null
        || _roundLifecycle.CurrentPhase == RoundPhase.AlternatingActivations;
    // A counter may defeat the actor after a Main Action has already begun.
    internal bool CanCompleteResolvedMainAction => ActiveCharacter is not null
        && !ActiveCharacter.HasActedThisRound && !MainActionUsed;

    /// <summary>The team allowed to select next; null during an activation or when the round is complete.</summary>
    public int? NextTeamId => ActiveCharacter is null ? GetAvailableTeamId() : null;
    public bool IsRoundComplete => ActiveCharacter is null && GetAvailableTeamId() is null;

    /// <summary>Snapshots two nonempty rosters with distinct team IDs, preserving existing runtime state.</summary>
    /// <remarks>
    /// There is no six-unit limit here. Match setup owns the standard 6v6 configuration.
    /// An optional lifecycle supplies the shared round number. RoundPreparationCoordinator
    /// explicitly prepares connected managers; this connection performs no automatic resets.
    /// </remarks>
    public ActivationManager(IEnumerable<Character> firstTeam, IEnumerable<Character> secondTeam, int startingTeamId,
        RoundLifecycleManager? roundLifecycle = null)
    {
        FirstTeam = CopyTeam(firstTeam, nameof(firstTeam));
        SecondTeam = CopyTeam(secondTeam, nameof(secondTeam));
        if (FirstTeamId == SecondTeamId)
            throw new ArgumentException("The two rosters must have different team IDs.", nameof(secondTeam));

        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in FirstTeam.Concat(SecondTeam))
        {
            if (!instances.Add(character) || !ids.Add(character.Id))
                throw new ArgumentException("Each character instance and instance ID must be unique in the match roster.");
        }
        ValidateStartingTeam(startingTeamId);
        _preferredTeamId = startingTeamId;
        _roundLifecycle = roundLifecycle;
    }

    /// <summary>Selects one ready, living character from the team whose turn it is.</summary>
    public void StartActivation(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (ActiveCharacter is not null)
            throw new InvalidOperationException("Complete the current activation before starting another.");
        if (!FirstTeam.Concat(SecondTeam).Any(unit => ReferenceEquals(unit, character)))
            throw new ArgumentException("The character instance must belong to this match roster.", nameof(character));
        if (!character.CanAct)
            throw new InvalidOperationException("The character is defeated or has already activated this round.");
        if (character.TeamId != NextTeamId)
            throw new InvalidOperationException("Only the team with the next activation may select a character.");

        MovementUsed = false;
        UtilityActionUsed = false;
        MainActionUsed = false;
        ActiveCharacter = character;
    }

    /// <summary>Records the completed use of the one unsplit movement opportunity, not a path step.</summary>
    public void RecordMovement()
    {
        ValidateActionRecording();
        if (MovementUsed) throw new InvalidOperationException("Movement has already been used in this activation.");
        MovementUsed = true;
    }

    /// <summary>Records one completed Utility Action without ending the activation.</summary>
    public void RecordUtilityAction()
    {
        ValidateActionRecording();
        if (UtilityActionUsed) throw new InvalidOperationException("The Utility Action has already been used in this activation.");
        UtilityActionUsed = true;
    }

    /// <summary>Records Main Action completion and ends the activation through the existing completion flow.</summary>
    /// <remarks>
    /// Call after the entire action resolves, including counters and follow-ups. This method
    /// does not execute the action. If the character died during resolution, use CompleteActivation
    /// to close the activation; a defeated character cannot record further actions.
    /// </remarks>
    public void CompleteMainAction()
    {
        ValidateActionRecording();
        if (MainActionUsed) throw new InvalidOperationException("The Main Action has already been used in this activation.");
        MainActionUsed = true;
        CompleteActivation();
    }

    // Only successful-use finalization calls this after validating the action before its effect.
    internal void CompleteResolvedMainAction()
    {
        if (!CanCompleteResolvedMainAction)
            throw new InvalidOperationException("The resolved Main Action no longer owns an available activation.");
        MainActionUsed = true;
        CompleteActivation();
    }

    private void ValidateActionRecording()
    {
        if (ActiveCharacter is null)
            throw new InvalidOperationException("Select an active character before recording an action.");
        if (!ActiveCharacter.CanAct)
            throw new InvalidOperationException("A defeated character or one whose activation has ended cannot act.");
    }

    /// <summary>Ends the selected activation, including when the player chooses to stop early.</summary>
    /// <remarks>CompleteMainAction uses this same flow. It also permits closing a defeated character's activation.</remarks>
    public void CompleteActivation()
    {
        var character = ActiveCharacter
            ?? throw new InvalidOperationException("There is no active character to complete.");
        character.EndActivation();
        _preferredTeamId = character.TeamId == FirstTeamId ? SecondTeamId : FirstTeamId;
        ActiveCharacter = null;
    }

    /// <summary>Advances a completed round and resets activation flags, using an explicit first team.</summary>
    /// <remarks>
    /// This is only activation preparation. A future round coordinator must arrange shop,
    /// Poison, delayed damage, status updates and scoring in their documented phases.
    /// Health, shields, statuses and healing history are not processed here.
    /// This legacy transition is available only in standalone mode. Connected managers
    /// read the lifecycle's round and use RoundPreparationCoordinator instead.
    /// </remarks>
    public void StartNextRound(int startingTeamId)
    {
        if (_roundLifecycle is not null)
            throw new InvalidOperationException("The connected lifecycle owns round advancement. Activation preparation must be coordinated separately.");
        ValidateStartingTeam(startingTeamId);
        if (!IsRoundComplete)
            throw new InvalidOperationException("Complete all eligible activations before starting the next round.");
        int nextRound = checked(CurrentRound + 1);
        ResetActivationReadiness(startingTeamId);
        _standaloneRound = nextRound;
    }

    // The coordinator checks these conditions before reserving preparation or changing any state.
    internal void ValidateRoundPreparation(RoundLifecycleManager lifecycle, int startingTeamId)
    {
        if (!ReferenceEquals(_roundLifecycle, lifecycle))
            throw new InvalidOperationException("Preparation requires an activation manager connected to this same lifecycle.");
        ValidateStartingTeam(startingTeamId);
        if (ActiveCharacter is not null)
            throw new InvalidOperationException("Complete the active activation before round preparation.");
    }

    // Called only after the coordinator validates the full batch and reserves preparation.
    internal void PrepareActivationReadiness(int startingTeamId) => ResetActivationReadiness(startingTeamId);

    private void ResetActivationReadiness(int startingTeamId)
    {
        foreach (var character in FirstTeam.Concat(SecondTeam)) character.ResetActivation();
        _preferredTeamId = startingTeamId;
    }

    private int? GetAvailableTeamId()
    {
        var preferred = _preferredTeamId == FirstTeamId ? FirstTeam : SecondTeam;
        var other = _preferredTeamId == FirstTeamId ? SecondTeam : FirstTeam;
        if (preferred.Any(character => character.CanAct)) return preferred[0].TeamId;
        // Stage 4.1 policy: skip an exhausted team so the other can finish. No voluntary pass rule.
        if (other.Any(character => character.CanAct)) return other[0].TeamId;
        return null;
    }

    private void ValidateStartingTeam(int startingTeamId)
    {
        if (startingTeamId != FirstTeamId && startingTeamId != SecondTeamId)
            throw new ArgumentOutOfRangeException(nameof(startingTeamId), "The starting team must be one of the two roster teams.");
    }

    private static IReadOnlyList<Character> CopyTeam(IEnumerable<Character> team, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(team, parameterName);
        var characters = team.ToArray();
        if (characters.Length == 0) throw new ArgumentException("A team roster must contain at least one character.", parameterName);
        if (characters.Any(character => character is null))
            throw new ArgumentException("A team roster cannot contain null characters.", parameterName);
        if (characters.Any(character => character.TeamId != characters[0].TeamId))
            throw new ArgumentException("All characters in a team roster must share its team ID.", parameterName);
        return Array.AsReadOnly(characters);
    }
}
