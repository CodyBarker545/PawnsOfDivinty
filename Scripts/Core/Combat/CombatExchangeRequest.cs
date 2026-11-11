using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>The settings for an opening strike, a possible counterattack and one optional follow-up.</summary>
/// <remarks>
/// The caller supplies Power, base accuracy, counter range and the Manhattan distance
/// between the characters. The resolver checks every setting, even when counters are disabled.
/// Follow-ups are opt-in so existing callers keep their opening/counter-only behavior.
/// Set both incoming permission flags to false for AoE or other attacks that forbid both.
/// The opening attack is assumed to be in range when requested; its follow-up reuses that
/// same attack without moving. Defender follow-ups use the supplied counter range and restrictions.
/// Disarmed combatants need explicit unarmed fallback inputs unless their supplied attack is
/// already unarmed. Unarmed Power and accuracy are caller configuration, not finalized balance.
/// Evasion modifiers are resolved by the caller from confirmed effects such as Smoke Veil.
/// They are percentage points subtracted by AccuracyCalculator, and default to zero.
/// Sanctuary supplies the full match roster for live attack-dependent Defense. Player-action
/// executors populate it; context-free low-level calls retain their existing two-character behavior.
/// </remarks>
public sealed record CombatExchangeRequest(
    Character Attacker,
    Character Defender,
    AttackType OpeningAttackType,
    int OpeningPower,
    WeaponCategory AttackerWeapon,
    WeaponCategory DefenderWeapon,
    int OpeningBaseAccuracy,
    AttackType CounterattackType,
    int CounterattackPower,
    int CounterattackBaseAccuracy,
    int CounterattackMinimumRange,
    int CounterattackMaximumRange,
    int Distance,
    bool IncomingAttackPermitsCounters,
    bool CounterattacksSuppressed,
    bool IncomingAttackPermitsFollowUps = false,
    AttackConfiguration? AttackerUnarmedAttack = null,
    AttackConfiguration? DefenderUnarmedAttack = null,
    int OpeningEvasionModifier = 0,
    int CounterattackEvasionModifier = 0,
    SanctuaryDefenseContext? Sanctuary = null);
