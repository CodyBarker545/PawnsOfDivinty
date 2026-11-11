using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>The caller chooses the actor and every action, including an early End when wanted.</summary>
public sealed record ScenarioActivationPlan
{
    public string ActorId { get; }
    public IReadOnlyList<ScenarioAction> Actions { get; }
    public ScenarioActivationPlan(string actorId, params ScenarioAction[] actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(actions);
        if (actions.Any(a => a is null || a.ActorId != actorId || a.Kind == ScenarioActionKind.StartActivation))
            throw new ArgumentException("Actions must belong to this actor; activation selection is supplied by the plan.");
        ActorId = actorId; Actions = Array.AsReadOnly(actions.ToArray());
    }
}

/// <summary>A round script. Starting-team selection is an explicit fixture choice, not a gameplay rule.</summary>
public sealed record ScenarioRoundPlan
{
    public int Round { get; }
    public int StartingTeamId { get; }
    public IReadOnlyList<ScenarioActivationPlan> Activations { get; }
    public ScenarioRoundPlan(int round, int startingTeamId, params ScenarioActivationPlan[] activations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        ArgumentNullException.ThrowIfNull(activations);
        if (activations.Any(a => a is null)) throw new ArgumentException("Activation plans cannot contain null.");
        Round = round; StartingTeamId = startingTeamId; Activations = Array.AsReadOnly(activations.ToArray());
    }
}
