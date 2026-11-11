using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Abilities;

public enum WinterHunterReason
{
    None, NotPassiveOwner, AttackerDefeated, TargetDefeated, TargetNotEnemy, NonPhysicalAttack, TargetNotFrostbitten
}

/// <summary>The physical modifier available before this strike, not a stored buff or a promise that the strike hits.</summary>
public sealed record WinterHunterEvaluation
{
    public string AttackerId { get; }
    public string TargetId { get; }
    public bool FrostbiteWasActive { get; }
    public WinterHunterReason Reason { get; }
    public bool Applies => Reason == WinterHunterReason.None;
    public int PhysicalDamageBonus => Applies ? 3 : 0;
    internal WinterHunterEvaluation(Character attacker, Character target, bool frostbite, WinterHunterReason reason)
    {
        AttackerId = attacker.Id;
        TargetId = target.Id;
        FrostbiteWasActive = frostbite;
        Reason = reason;
    }
}

/// <summary>Section 12.6: +3 physical damage against a target already Frostbitten before the strike.</summary>
public static class WinterHunter
{
    public static WinterHunterEvaluation Evaluate(Character attacker, Character target, AttackType attackType = AttackType.Physical)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(attackType)) throw new ArgumentOutOfRangeException(nameof(attackType));
        bool frostbite = target.Statuses.HasStatus(StatusType.Frostbite);
        if (attacker.Definition?.Id != "beothuk_spirit_guardian" || !attacker.Definition.Abilities.Any(a =>
            a.Id == BeothukSpiritGuardian.WinterHunterId && a.Classification == AbilityClassification.Passive))
            return Result(WinterHunterReason.NotPassiveOwner);
        if (attacker.IsDefeated) return Result(WinterHunterReason.AttackerDefeated);
        if (target.IsDefeated) return Result(WinterHunterReason.TargetDefeated);
        if (attacker.Id == target.Id || attacker.TeamId == target.TeamId) return Result(WinterHunterReason.TargetNotEnemy);
        if (attackType != AttackType.Physical) return Result(WinterHunterReason.NonPhysicalAttack);
        if (!frostbite) return Result(WinterHunterReason.TargetNotFrostbitten);
        // HasActedThisRound is intentionally not checked: a previously activated unit can counter.
        return Result(WinterHunterReason.None);

        WinterHunterEvaluation Result(WinterHunterReason reason) => new(attacker, target, frostbite, reason);
    }

    public static int GetPhysicalDamageBonus(Character attacker, Character target)
        => Evaluate(attacker, target).PhysicalDamageBonus;
}
