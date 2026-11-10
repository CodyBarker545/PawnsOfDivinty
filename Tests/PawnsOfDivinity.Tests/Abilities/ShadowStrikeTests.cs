using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class ShadowStrikeTests
{
    [Theory]
    [InlineData(false, 3, 16, false)]
    [InlineData(true, 5, 18, true)]
    public void Opening_UsesExplicitVisibilityAndOrdinaryTriangleDamage(bool invisible, int bonus, int damage, bool disarm)
    {
        var s = new Setup(invisible);
        var result = s.Resolve();

        Assert.True(result.Resolved);
        Assert.Equal(13, result.BaseAttackDamage); // 17 STR + 5 Power - 11 DEF + 2 triangle.
        Assert.Equal(bonus, result.ShadowStrikeBonus);
        Assert.Equal(damage, result.FinalCalculatedDamage);
        Assert.Equal(damage, result.ActualHPDamage);
        Assert.Equal(disarm, result.DisarmApplied);
        Assert.Equal(disarm, s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.Equal(WeaponTriangleOutcome.Advantage, result.Exchange!.OpeningStrike.TriangleOutcome);
        Assert.True(s.Assassin.SmokeReveal.RevealedByAttack);
        Assert.Equal(2, s.Rolls.Calls); // Opening and counter; equal Speed prevents a follow-up.
    }

    [Fact]
    public void PreApproachSnapshot_PreservesBonusDespiteAdjacencyRevealAtDestination()
    {
        var s = new Setup(true);
        Assert.True(s.Snapshot.IsInvisible);
        Assert.Equal(new GridPosition(3, 3), s.Snapshot.Position);
        Assert.False(s.CurrentVisibility().IsInvisible);
        Assert.True(s.CurrentVisibility().EnemyAdjacent);

        var result = s.Resolve();

        Assert.Equal(5, result.ShadowStrikeBonus);
        Assert.True(result.DisarmApplied);
        Assert.Equal(new GridPosition(4, 3), s.Assassin.TacticalPosition);
        Assert.True(s.Assassin.SmokeReveal.RevealedByAttack);
        Assert.False(s.CurrentVisibility().IsInvisible);
    }

    [Fact]
    public void AlreadyAdjacentAtStart_UsesVisibleBonusWithoutDisarm()
    {
        var s = new Setup(false);
        Assert.True(s.Snapshot.EnemyAdjacent);
        Assert.False(s.Snapshot.IsInvisible);

        var result = s.Resolve();

        Assert.Equal(3, result.ShadowStrikeBonus);
        Assert.False(result.DisarmApplied);
    }

    [Theory]
    [InlineData(WeaponCategory.Axe, 13, WeaponTriangleOutcome.Advantage)]
    [InlineData(WeaponCategory.Lance, 9, WeaponTriangleOutcome.Disadvantage)]
    [InlineData(WeaponCategory.Sword, 11, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, 11, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Unarmed, 11, WeaponTriangleOutcome.Neutral)]
    public void VisibleBonus_PreservesEachOrdinaryTriangleOutcome(WeaponCategory category, int ordinaryDamage,
        WeaponTriangleOutcome outcome)
    {
        var s = new Setup(false);
        s.Request = s.Request with { DefenderWeapon = category };
        var result = s.Resolve();
        Assert.Equal(ordinaryDamage, result.BaseAttackDamage);
        Assert.Equal(ordinaryDamage + 3, result.FinalCalculatedDamage);
        Assert.Equal(outcome, result.Exchange!.OpeningStrike.TriangleOutcome);
    }

    [Fact]
    public void Bonus_IsAddedAfterOrdinaryMinimumDamage()
    {
        var s = new Setup(false);
        var armored = CharacterFactory.Create(new CharacterDefinition("armored_fixture", "Armored fixture", "Test only",
            new CharacterStats(100, 0, 100, 0, 0, 0, 24, 4)), "armored-instance", 2);
        armored.SetTacticalPosition(new(5, 3));
        s.Request = s.Request with { Defender = armored, DefenderWeapon = WeaponCategory.Lance };
        var result = s.Resolve();
        Assert.Equal(1, result.BaseAttackDamage);
        Assert.Equal(4, result.FinalCalculatedDamage);
    }

    [Fact]
    public void InvisibleSnapshotThatOmittedAnAlreadyAdjacentTarget_IsRejected()
    {
        var s = new Setup(false);
        var incomplete = SmokeVisibility.Evaluate(s.Assassin, s.Assassin.TacticalPosition!.Value,
            s.Smoke, Array.Empty<(Character, GridPosition)>());
        Assert.True(incomplete.IsInvisible);

        var result = s.Service.Resolve(s.Request, incomplete, 1, 1);

        Assert.Equal(ShadowStrikeRejectionReason.InvalidVisibilitySnapshot, result.Eligibility.RejectionReason);
        Assert.False(result.Resolved);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.False(s.Assassin.SmokeReveal.RevealedByAttack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Miss_DealsNoDamageOrDisarm_ButStillReveals(bool invisible)
    {
        var s = new Setup(invisible, 99, 0);
        s.Target.SetShieldHP(6);
        s.Request = s.Request with { OpeningBaseAccuracy = 0 };

        var result = s.Resolve();

        Assert.False(result.Exchange!.OpeningStrike.Hit);
        Assert.Equal(0, result.FinalCalculatedDamage);
        Assert.Equal(0, result.ActualHPDamage);
        Assert.Equal(0, result.ShieldAbsorbed);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.False(result.DisarmApplied);
        Assert.False(s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.True(s.Assassin.SmokeReveal.RevealedByAttack);
        Assert.Equal(2, s.Rolls.Calls);
        Assert.Equal(WeaponTriangleOutcome.Disadvantage, result.Exchange.CounterStrike!.TriangleOutcome);
    }

    [Theory]
    [InlineData(6, 6, 12)]
    [InlineData(30, 18, 0)]
    public void Shields_AbsorbBeforeHealth_WithoutBlockingOnHitDisarm(int shield, int absorbed, int hpLost)
    {
        var s = new Setup(true);
        s.Target.SetShieldHP(shield);
        var result = s.Resolve();
        Assert.Equal(18, result.FinalCalculatedDamage);
        Assert.Equal(absorbed, result.ShieldAbsorbed);
        Assert.Equal(hpLost, result.ActualHPDamage);
        Assert.Equal(100 - hpLost, s.Target.CurrentHealth);
        Assert.True(result.DisarmApplied);
    }

    [Fact]
    public void LethalOpening_ReportsActualHealthLossAndSkipsDisarmCounterAndFollowUp()
    {
        var s = new Setup(true);
        s.Target.TakeDamage(99);
        var result = s.Resolve();
        Assert.Equal(18, result.FinalCalculatedDamage);
        Assert.Equal(1, result.ActualHPDamage);
        Assert.True(s.Target.IsDefeated);
        Assert.False(result.DisarmApplied);
        Assert.False(result.Exchange!.CounterattackOccurred);
        Assert.False(result.Exchange.FollowUpOccurred);
        Assert.Equal(1, s.Rolls.Calls);
    }

    [Fact]
    public void Disarm_RefreshesToOneRound_AndPreservesOtherStatuses()
    {
        var s = new Setup(true);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Disarm, 2);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.Statuses.ApplyPoison();
        var result = s.Resolve();
        Assert.True(result.DisarmApplied);
        Assert.Equal(1, s.Target.Statuses.TimedStatuses[StatusType.Disarm]);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Target.Statuses.IsPoisoned);
        s.Target.Statuses.AdvanceTimedStatuses();
        Assert.False(s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.True(s.Target.Statuses.HasStatus(StatusType.Burn));
    }

    [Fact]
    public void Disarm_ImmediatelyChangesCounterToExplicitUnarmedConfiguration()
    {
        var s = new Setup(true);
        s.Request = s.Request with { CounterattackPower = 50, DefenderUnarmedAttack = AttackConfiguration.CreateUnarmed(0, 100) };
        var result = s.Resolve();
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.Exchange!.CounterStrike!.TriangleOutcome);
        Assert.Equal(12, result.Exchange.CounterStrike.Damage); // 20 STR - 8 DEF; equipped Power is gone.
        Assert.Equal(80, result.Exchange.CounterStrike.HitChance); // Smoke Veil survives attack reveal.
        Assert.Equal(2, s.Rolls.Calls);
    }

    [Theory]
    [InlineData(false, 16, 9)]
    [InlineData(true, 18, 8)]
    public void DocumentedRosterMatchup_FollowUpUsesOrdinaryDamageWithoutOpeningBonus(bool invisible, int opening, int followUp)
    {
        var s = new Setup(invisible, 0, 0, 0);
        var nigel = CharacterFactory.Create("nigel", 2)!;
        nigel.SetTacticalPosition(new(5, 3));
        s.Request = s.Request with { Defender = nigel };

        var result = s.Resolve();

        Assert.Equal(opening, result.FinalCalculatedDamage);
        Assert.Equal(FollowUpParticipant.Attacker, result.Exchange!.FollowUpBy);
        Assert.Equal(followUp, result.Exchange.FollowUpStrike!.Damage);
        Assert.Equal(opening + followUp, result.Exchange.DefenderHealthLost);
        Assert.Equal(opening, result.ActualHPDamage);
        Assert.Equal(52 - opening - followUp, nigel.CurrentHealth);
        Assert.Equal(3, s.Rolls.Calls);
    }

    [Fact]
    public void MissedInvisibleOpening_FollowedBySuccessfulFollowUp_DoesNotApplyOpeningBonusOrDisarm()
    {
        var s = new Setup(true, 99, 0, 0);
        var nigel = CharacterFactory.Create("nigel", 2)!;
        nigel.SetTacticalPosition(new(5, 3));
        s.Request = s.Request with { Defender = nigel, OpeningBaseAccuracy = 80 };

        var result = s.Resolve();

        Assert.False(result.Exchange!.OpeningStrike.Hit);
        Assert.True(result.Exchange.FollowUpStrike!.Hit);
        Assert.Equal(9, result.Exchange.FollowUpStrike.Damage);
        Assert.Equal(0, result.ActualHPDamage);
        Assert.False(result.DisarmApplied);
        Assert.False(nigel.Statuses.HasStatus(StatusType.Disarm));
        Assert.Equal(3, s.Rolls.Calls);
    }

    [Fact]
    public void FasterDefender_CanCounterAndFollowUpWithUnarmedAttack()
    {
        var s = new Setup(true, 0, 0, 0);
        var fast = CharacterFactory.Create(new CharacterDefinition("fast_fixture", "Fast fixture", "Test only",
            new CharacterStats(100, 10, 11, 0, 0, 0, 42, 4)), "fast-instance", 2);
        fast.SetTacticalPosition(new(5, 3));
        s.Request = s.Request with { Defender = fast };
        var result = s.Resolve();
        Assert.Equal(FollowUpParticipant.Defender, result.Exchange!.FollowUpBy);
        Assert.Equal(2, result.Exchange.CounterStrike!.Damage);
        Assert.Equal(1, result.Exchange.FollowUpStrike!.Damage);
        Assert.Equal(3, s.Rolls.Calls);
    }

    [Theory]
    [InlineData("attacker", ShadowStrikeRejectionReason.InvalidAttacker)]
    [InlineData("deadAttacker", ShadowStrikeRejectionReason.AttackerDefeated)]
    [InlineData("deadTarget", ShadowStrikeRejectionReason.TargetDefeated)]
    [InlineData("ally", ShadowStrikeRejectionReason.TargetNotEnemy)]
    [InlineData("sourceMissing", ShadowStrikeRejectionReason.MissingPosition)]
    [InlineData("targetMissing", ShadowStrikeRejectionReason.MissingPosition)]
    [InlineData("distance", ShadowStrikeRejectionReason.OutsideRange)]
    [InlineData("positions", ShadowStrikeRejectionReason.OutsideRange)]
    [InlineData("magical", ShadowStrikeRejectionReason.RequiresPhysicalAttack)]
    [InlineData("noVisibility", ShadowStrikeRejectionReason.MissingVisibilitySnapshot)]
    [InlineData("otherVisibility", ShadowStrikeRejectionReason.InvalidVisibilitySnapshot)]
    public void IneligibleRequests_DoNotRollRevealOrApplyEffects(string invalid, ShadowStrikeRejectionReason reason)
    {
        var s = new Setup(true);
        SmokeVisibilityResult? snapshot = s.Snapshot;
        switch (invalid)
        {
            case "attacker": s.Request = s.Request with { Attacker = s.Target }; break;
            case "deadAttacker": s.Assassin.TakeDamage(int.MaxValue); break;
            case "deadTarget": s.Target.TakeDamage(int.MaxValue); break;
            case "ally": s.Request = s.Request with { Defender = s.Assassin }; break;
            case "sourceMissing": s.Request = s.Request with { Attacker = CharacterFactory.Create("hooded_assassin", 1)! }; break;
            case "targetMissing": s.Request = s.Request with { Defender = CharacterFactory.Create("nigel", 2)! }; break;
            case "distance": s.Request = s.Request with { Distance = 2 }; break;
            case "positions": s.Target.SetTacticalPosition(new(8, 3)); break;
            case "magical": s.Request = s.Request with { OpeningAttackType = AttackType.Magical }; break;
            case "noVisibility": snapshot = null; break;
            case "otherVisibility": snapshot = SmokeVisibility.Evaluate(s.Target, new(5, 3), s.Smoke, Array.Empty<(Character, GridPosition)>()); break;
        }
        int hp = s.Target.CurrentHealth;
        var result = s.Service.Resolve(s.Request, snapshot, 1, 1);
        Assert.Equal(reason, result.Eligibility.RejectionReason);
        Assert.False(result.Resolved);
        Assert.Equal(0, result.ShadowStrikeBonus);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(hp, s.Target.CurrentHealth);
        Assert.False(s.Assassin.SmokeReveal.RevealedByAttack);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 3)]
    public void WeaponRangeMustPermitTheAdjacentStrike(int min, int max)
    {
        var s = new Setup(false);
        Assert.Equal(ShadowStrikeRejectionReason.OutsideRange, s.Service.Resolve(s.Request, s.Snapshot, min, max).Eligibility.RejectionReason);
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Fact]
    public void ExpiredSmoke_InvalidatesHiddenSnapshot_WhileFreshVisibleSnapshotStillAllowsAttack()
    {
        var s = new Setup(true);
        s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(ShadowStrikeRejectionReason.InvalidVisibilitySnapshot, s.Resolve().Eligibility.RejectionReason);
        Assert.Equal(0, s.Rolls.Calls);
        var result = s.Service.Resolve(s.Request, s.CurrentVisibility(), 1, 1);
        Assert.Equal(3, result.ShadowStrikeBonus);
        Assert.False(result.DisarmApplied);
        Assert.Equal(90, result.Exchange!.CounterStrike!.HitChance); // Axe versus Sword, without Smoke Veil.
    }

    [Fact]
    public void AttackRevealSinceSnapshot_InvalidatesInvisibleOpening()
    {
        var s = new Setup(true);
        s.Assassin.SmokeReveal.RecordAttack();
        Assert.Equal(ShadowStrikeRejectionReason.InvalidVisibilitySnapshot, s.Resolve().Eligibility.RejectionReason);
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Theory]
    [InlineData("power")]
    [InlineData("accuracy")]
    [InlineData("category")]
    [InlineData("counter")]
    [InlineData("fallback")]
    [InlineData("missingFallback")]
    public void InvalidCombatConfiguration_IsRejectedBeforeRandomnessOrMutation(string invalid)
    {
        var s = new Setup(true);
        s.Request = invalid switch
        {
            "power" => s.Request with { OpeningPower = -1 },
            "accuracy" => s.Request with { OpeningBaseAccuracy = 101 },
            "category" => s.Request with { AttackerWeapon = (WeaponCategory)999 },
            "counter" => s.Request with { CounterattackMaximumRange = -1 },
            "fallback" => s.Request with { DefenderUnarmedAttack = new(AttackType.Physical, WeaponCategory.Sword, 0, 100, 1, 1) },
            _ => s.Request with { DefenderUnarmedAttack = null }
        };
        Assert.ThrowsAny<ArgumentException>(() => s.Resolve());
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(100, s.Target.CurrentHealth);
        Assert.False(s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.False(s.Assassin.SmokeReveal.RevealedByAttack);
    }

    [Fact]
    public void OpeningBonusOverflow_IsRejectedBeforeRollingOrRevealing()
    {
        var s = new Setup(true);
        s.Request = s.Request with { OpeningPower = int.MaxValue - 8, DefenderWeapon = WeaponCategory.Unarmed };
        Assert.Throws<OverflowException>(() => s.Resolve());
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(100, s.Target.CurrentHealth);
        Assert.False(s.Assassin.SmokeReveal.RevealedByAttack);
    }

    [Fact]
    public void NullInputsAndMalformedWeaponRanges_ThrowWithoutMutation()
    {
        var s = new Setup(true);
        Assert.Throws<ArgumentNullException>(() => new ShadowStrikeResolver(null!, s.Smoke));
        Assert.Throws<ArgumentNullException>(() => new ShadowStrikeResolver(new(new(new(s.Rolls))), null!));
        Assert.Throws<ArgumentNullException>(() => s.Service.Evaluate(null!, s.Snapshot, 1, 1));
        Assert.Throws<ArgumentNullException>(() => s.Service.Evaluate(s.Request with { Attacker = null! }, s.Snapshot, 1, 1));
        Assert.Throws<ArgumentNullException>(() => s.Service.Evaluate(s.Request with { Defender = null! }, s.Snapshot, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Service.Evaluate(s.Request, s.Snapshot, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Service.Evaluate(s.Request, s.Snapshot, 2, 1));
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Fact]
    public void EvaluationIsPure_AndResolutionPreservesUnrelatedRuntimeStateAndSnapshots()
    {
        var s = new Setup(true);
        var bystander = CharacterFactory.Create("gentle_guardian", 1)!;
        var baseStats = s.Assassin.BaseStats;
        var targetStats = s.Target.BaseStats;
        s.Assassin.EndActivation();
        s.Target.EndActivation();
        s.Assassin.SetShieldHP(30);
        s.Assassin.Cooldowns.RecordUse("other_ability", 2, 3);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var smokeBefore = s.Smoke.ActiveAreas.ToArray();

        var eligibility = s.Service.Evaluate(s.Request, s.Snapshot, 1, 1);
        Assert.True(eligibility.IsAllowed);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.False(s.Assassin.SmokeReveal.RevealedByAttack);
        var result = s.Resolve();

        Assert.True(s.Assassin.HasActedThisRound);
        Assert.True(s.Target.HasActedThisRound);
        Assert.Equal(0, s.Assassin.HealsReceivedThisRound);
        Assert.Equal(0, s.Target.HealsReceivedThisRound);
        Assert.Single(s.Assassin.Cooldowns.Cooldowns);
        Assert.Equal(5, s.Assassin.Cooldowns.Cooldowns["other_ability"].ReadyRound);
        Assert.Empty(s.Target.Cooldowns.Cooldowns);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Same(baseStats, s.Assassin.BaseStats);
        Assert.Same(targetStats, s.Target.BaseStats);
        Assert.Equal(smokeBefore, s.Smoke.ActiveAreas);
        Assert.Equal(bystander.BaseStats.MaxHealth, bystander.CurrentHealth);
        Assert.False(bystander.HasActedThisRound);
        s.Target.TakeDamage(int.MaxValue);
        Assert.Equal(18, result.ActualHPDamage);
        Assert.True(result.Eligibility.OpeningVisibility!.IsInvisible);
        Assert.Equal(new GridPosition(4, 3), result.Eligibility.SourcePosition);
        foreach (var type in new[] { typeof(ShadowStrikeResult), typeof(ShadowStrikeEligibilityResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    private sealed class Setup
    {
        public Character Assassin { get; } = CharacterFactory.Create("hooded_assassin", 1)!;
        // Equal Speed and ample HP isolate opening/counter behavior; these are test fixtures, not balance changes.
        public Character Target { get; } = CharacterFactory.Create(new CharacterDefinition("target_fixture", "Target fixture", "Test only",
            new CharacterStats(100, 20, 11, 3, 8, 4, 24, 4)), "target-instance", 2);
        public SmokePlacementService Smoke { get; } = new();
        public Rolls Rolls { get; }
        public ShadowStrikeResolver Service { get; }
        public SmokeVisibilityResult Snapshot { get; }
        public CombatExchangeRequest Request { get; set; }

        public Setup(bool invisible, params int[] rolls)
        {
            Rolls = new(rolls.Length == 0 ? new[] { 0, 0, 0 } : rolls);
            Service = new(new(new(new(Rolls))), Smoke);
            Smoke.Place("cloud", Assassin.Id, new(3, 3), 2, _ => true);
            Target.SetTacticalPosition(new(5, 3));
            Assassin.SetTacticalPosition(invisible ? new(3, 3) : new(4, 3));
            Snapshot = CurrentVisibility();
            // Approach is supplied by the caller. Shadow Strike itself never changes position.
            Assassin.SetTacticalPosition(new(4, 3));
            Request = new(Assassin, Target, AttackType.Physical, 5, WeaponCategory.Sword, WeaponCategory.Axe, 100,
                AttackType.Physical, 0, 100, 1, 1, 1, true, false,
                DefenderUnarmedAttack: AttackConfiguration.CreateUnarmed(0, 100));
            // Unarmed Power and accuracy above are explicit prototype inputs, not finalized equipment rules.
        }

        public SmokeVisibilityResult CurrentVisibility() => SmokeVisibility.Evaluate(Assassin, Assassin.TacticalPosition!.Value,
            Smoke, new[] { (Target, Target.TacticalPosition!.Value) });
        public ShadowStrikeResult Resolve() => Service.Resolve(Request, Snapshot, 1, 1);
    }

    private sealed class Rolls(params int[] values) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() => values[Calls++];
    }
}
