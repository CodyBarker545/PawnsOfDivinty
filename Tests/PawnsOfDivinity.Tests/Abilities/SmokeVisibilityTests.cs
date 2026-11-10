using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SmokeVisibilityTests
{
    [Fact]
    public void Assassin_EntersAndLeavesSmoke_AndExpirationRestoresVisibility()
    {
        var assassin = Assassin();
        var smoke = Smoke();
        Assert.False(View(assassin, new(9, 9), smoke).IsInvisible);
        Assert.True(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.False(View(assassin, new(9, 9), smoke).IsInvisible);
        Assert.True(View(assassin, new(3, 3), smoke).IsInvisible);

        smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.Equal(0, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
    }

    [Theory]
    [InlineData(4, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 4, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 4, false)]
    [InlineData(5, 3, false)]
    public void OnlyOrthogonallyAdjacentLivingEnemies_RevealTheAssassin(int x, int y, bool revealed)
    {
        var assassin = Assassin();
        var enemy = CharacterFactory.Create("nigel", 2)!;
        var smoke = Smoke();
        var result = SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, new[] { (enemy, new GridPosition(x, y)) });

        Assert.Equal(revealed, result.EnemyAdjacent);
        Assert.Equal(!revealed, result.IsInvisible);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
        enemy.TakeDamage(int.MaxValue);
        Assert.True(SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, new[] { (enemy, new GridPosition(x, y)) }).IsInvisible);
    }

    [Fact]
    public void FriendlyAdjacencyDoesNotReveal_AndEnemyDepartureIsReevaluated()
    {
        var assassin = Assassin();
        var ally = CharacterFactory.Create("nigel", 1)!;
        var enemy = CharacterFactory.Create("nigel", 2)!;
        var smoke = Smoke();
        Assert.True(SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, new[] { (ally, new GridPosition(4, 3)) }).IsInvisible);
        Assert.False(SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, new[] { (enemy, new GridPosition(4, 3)) }).IsInvisible);
        Assert.True(SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, new[] { (enemy, new GridPosition(6, 3)) }).IsInvisible);
    }

    [Fact]
    public void AttackReveal_IsPersistent_AndSmokeVeilStillApplies()
    {
        var assassin = Assassin();
        var smoke = Smoke();
        assassin.SmokeReveal.RecordAttack();

        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.True(View(assassin, new(3, 3), smoke).RevealedByAttack);
        Assert.False(View(assassin, new(9, 9), smoke).IsInvisible);
        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible); // No invented re-entry reset.
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
        assassin.SmokeReveal.ResetAttackReveal(); // Explicit state operation; automatic reset timing is not specified.
        Assert.True(View(assassin, new(3, 3), smoke).IsInvisible);
    }

    [Theory]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("nigel")]
    [InlineData("gravebound_colossus")]
    [InlineData("beothuk_spirit_guardian")]
    public void OtherCharacters_DoNotGainAssassinInvisibilityOrSmokeVeil(string id)
    {
        var character = CharacterFactory.Create(id, 1)!;
        var smoke = Smoke();

        Assert.True(View(character, new(3, 3), smoke).IsInSmoke);
        Assert.False(View(character, new(3, 3), smoke).IsInvisible);
        Assert.Equal(0, SmokeVeil.GetEvasionModifier(character, new(3, 3), smoke));
    }

    [Fact]
    public void AnyOwnerAndOverlappingAreas_ProvideOneUnstackedBonus()
    {
        var assassin = Assassin();
        var smoke = Smoke(); // A different owner supplies this cloud.
        smoke.Place("second", "another_owner", new(3, 3), 2, _ => true);

        Assert.True(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
        Assert.Equal(0, SmokeVeil.GetEvasionModifier(assassin, new(9, 9), smoke));
    }

    [Fact]
    public void OverlappingExpiration_RemainsInvisibleUntilTheLastCoveringCloudExpires()
    {
        var assassin = Assassin();
        var smoke = Smoke();
        smoke.Place("later", "other_owner", new(3, 3), 3, _ => true);

        smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        Assert.True(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
        smoke.ExpireAtRoundEnd(4, RoundPhase.Cleanup);
        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible);
    }

    [Fact]
    public void AttackRevealState_IsIndependentForEachAssassinInstance()
    {
        var first = Assassin();
        var second = Assassin();
        var smoke = Smoke();
        first.SmokeReveal.RecordAttack();

        Assert.False(View(first, new(3, 3), smoke).IsInvisible);
        Assert.True(View(second, new(3, 3), smoke).IsInvisible);
        Assert.NotSame(first.SmokeReveal, second.SmokeReveal);
    }

    [Theory]
    [InlineData(90, WeaponCategory.Sword, WeaponCategory.Axe, 80)]
    [InlineData(100, WeaponCategory.Sword, WeaponCategory.Axe, 90)]
    [InlineData(90, WeaponCategory.Sword, WeaponCategory.Lance, 60)]
    [InlineData(5, WeaponCategory.Bow, WeaponCategory.Bow, 0)]
    [InlineData(100, WeaponCategory.Bow, WeaponCategory.Bow, 80)]
    public void SmokeVeil_UsesPercentagePointsAndExistingAccuracyClamping(int baseline, WeaponCategory attacker,
        WeaponCategory defender, int expected)
    {
        var assassin = Assassin();
        var smoke = Smoke();

        Assert.Equal(expected, SmokeVeil.CalculateHitChance(baseline, attacker, defender, assassin, new(3, 3), smoke));
        Assert.Equal(AccuracyCalculator.CalculateHitChance(baseline, attacker, defender),
            SmokeVeil.CalculateHitChance(baseline, attacker, defender, assassin, new(9, 9), smoke));
    }

    [Fact]
    public void HiddenEnemy_BlocksDirectTargeting_ButDoesNotGrantAreaDamageImmunity()
    {
        var assassin = Assassin();
        var enemy = CharacterFactory.Create("nigel", 2)!;
        var ally = CharacterFactory.Create("nigel", 1)!;
        var view = View(assassin, new(3, 3), Smoke());

        Assert.False(SmokeVisibility.CanTargetThroughVisibility(enemy, view, isAreaEffect: false));
        Assert.True(SmokeVisibility.CanTargetThroughVisibility(enemy, view, isAreaEffect: true));
        Assert.True(SmokeVisibility.CanTargetThroughVisibility(ally, view, isAreaEffect: false));
    }

    [Fact]
    public void DefeatedOrLegacyNamedAssassin_DoesNotQualifyForSmokeBenefits()
    {
        var assassin = Assassin();
        var smoke = Smoke();
        var legacy = new Character("legacy", "Hooded Assassin", 1, assassin.BaseStats);
        Assert.False(View(legacy, new(3, 3), smoke).IsInvisible);
        assassin.TakeDamage(int.MaxValue);
        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.Equal(0, SmokeVeil.GetEvasionModifier(assassin, new(3, 3), smoke));
    }

    [Fact]
    public void Evaluation_ReturnsReadOnlySnapshots_AndLeavesRuntimeStateUnchanged()
    {
        var assassin = Assassin();
        var enemy = CharacterFactory.Create("nigel", 2)!;
        var manager = new ActivationManager(new[] { assassin }, new[] { enemy }, 1);
        manager.StartActivation(assassin);
        assassin.TakeDamage(5);
        assassin.SetShieldHP(6);
        assassin.Statuses.ApplyPoison();
        assassin.Cooldowns.RecordUse("test", 1, 3);
        var stats = assassin.BaseStats;
        var snapshot = stats with { };
        var smoke = Smoke();

        var result = View(assassin, new(3, 3), smoke);
        SmokeVeil.CalculateHitChance(90, WeaponCategory.Bow, WeaponCategory.Bow, assassin, new(3, 3), smoke);
        smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        Assert.True(result.IsInvisible);
        Assert.False(View(assassin, new(3, 3), smoke).IsInvisible);
        Assert.All(typeof(SmokeVisibilityResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(33, assassin.CurrentHealth);
        Assert.Equal(6, assassin.ShieldHP);
        Assert.True(assassin.Statuses.IsPoisoned);
        Assert.Equal(0, assassin.Statuses.PoisonTicksOccurred);
        Assert.Equal(4, assassin.Cooldowns.Cooldowns["test"].ReadyRound);
        Assert.False(assassin.HasActedThisRound);
        Assert.Same(stats, assassin.BaseStats);
        Assert.Equal(snapshot, assassin.BaseStats);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseMainAction);
        Assert.True(manager.CanUseUtilityAction);
    }

    [Fact]
    public void InvalidInputs_AreRejected()
    {
        var assassin = Assassin();
        var smoke = Smoke();
        Assert.Throws<ArgumentNullException>(() => View(null!, new(3, 3), smoke));
        Assert.Throws<ArgumentNullException>(() => View(assassin, new(3, 3), null!));
        Assert.Throws<ArgumentNullException>(() => SmokeVisibility.Evaluate(assassin, new(3, 3), smoke, null!));
        Assert.Throws<ArgumentException>(() => SmokeVisibility.Evaluate(assassin, new(3, 3), smoke,
            new[] { ((Character)null!, new GridPosition(4, 3)) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => AccuracyCalculator.CalculateHitChance(90, WeaponCategory.Bow, WeaponCategory.Bow, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SmokeVeil.CalculateHitChance(101, WeaponCategory.Bow, WeaponCategory.Bow, assassin, new(3, 3), smoke));
        Assert.Equal(0, AccuracyCalculator.CalculateHitChance(90, WeaponCategory.Bow, WeaponCategory.Bow, int.MaxValue));
    }

    private static Character Assassin() => CharacterFactory.Create("hooded_assassin", 1)!;
    private static SmokePlacementService Smoke()
    {
        var smoke = new SmokePlacementService();
        smoke.Place("smoke", "other_owner", new(3, 3), 2, _ => true);
        return smoke;
    }
    private static SmokeVisibilityResult View(Character character, GridPosition position, SmokePlacementService smoke)
        => SmokeVisibility.Evaluate(character, position, smoke, Array.Empty<(Character, GridPosition)>());
}
