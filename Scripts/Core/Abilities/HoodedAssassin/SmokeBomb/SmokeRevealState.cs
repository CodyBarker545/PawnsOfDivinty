namespace PawnsOfDivinity.Core.Abilities;

/// <summary>The minimal attack-reveal latch; it has no timer and does not track smoke coverage.</summary>
public sealed class SmokeRevealState
{
    public bool RevealedByAttack { get; private set; }

    /// <summary>The future attack executor reports the attack event that reveals the Assassin.</summary>
    public void RecordAttack() => RevealedByAttack = true;

    /// <summary>An explicit state reset, with no automatic round, re-entry or new-cloud reset policy.</summary>
    /// <remarks>Spec v0.2 confirms attack reveal but does not specify when it clears. Wire only a confirmed reset event later.</remarks>
    public void ResetAttackReveal() => RevealedByAttack = false;
}
