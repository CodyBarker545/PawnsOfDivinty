using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Applies one Poison tick through the existing shield-first damage path.</summary>
public static class PoisonProcessor
{
    /// <summary>Processes exactly one tick for a living, poisoned character.</summary>
    /// <remarks>
    /// Call during the Poison phase, starting in the round after application. This method
    /// does not know the current round or prevent a caller from invoking it twice.
    /// PoisonBatchProcessor snapshots all living poisoned targets and their scheduled
    /// damage before applying the batch. Global death handling, drops and death-triggered
    /// effects must wait until the entire batch has received damage. This method runs none
    /// of those actions; TakeDamage only clears local Poison and depleted shield ownership.
    /// </remarks>
    public static PoisonTickResult Process(Character character)
        => ApplyPrepared(Prepare(character));

    // Preparation is pure so the batch can validate every scheduled tick before applying any.
    internal static (Character Character, int TickNumber, int Damage) Prepare(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.IsDefeated)
            throw new InvalidOperationException("Cannot process Poison on a defeated character.");
        if (!character.Statuses.IsPoisoned)
            throw new InvalidOperationException("Cannot process a tick when Poison is inactive.");

        // Check all arithmetic before touching shields, health or progression.
        int tick = checked(character.Statuses.PoisonTicksOccurred + 1);
        int damage = PoisonDamageCalculator.Calculate(tick);
        return (character, tick, damage);
    }

    // Consume the prepared values, not a recalculation after another participant's damage.
    // Only this processor and the batch coordinator use this internal path.
    internal static PoisonTickResult ApplyPrepared((Character Character, int TickNumber, int Damage) prepared)
    {
        var character = prepared.Character;
        int previousShield = character.ShieldHP;
        int healthLost = character.TakeDamage(prepared.Damage);
        if (!character.IsDefeated) character.Statuses.RecordPoisonTick();
        // A lethal tick is recorded in the result; TakeDamage already cleared its sequence.

        return new PoisonTickResult(prepared.TickNumber, prepared.Damage, previousShield - character.ShieldHP,
            healthLost, character.CurrentHealth, character.IsDefeated);
    }
}
