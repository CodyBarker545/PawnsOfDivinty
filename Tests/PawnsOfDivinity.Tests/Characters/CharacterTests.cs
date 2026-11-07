using System;
using PawnsOfDivinity.Core.Characters;
using Xunit;

namespace PawnsOfDivinity.Tests.Characters;

/// <summary>
/// Unit tests for the Character class.
///
/// Tests character creation, damage, healing,
/// defeat states, and turn activation.
///
/// Each test creates its own character to ensure
/// tests remain independent.
/// </summary>
public class CharacterTests
{
    // =========================================================
    // TEST DATA
    // =========================================================

    private const int MaxHealth = 46;

    /// <summary>
    /// Creates a standard Komodo Warrior for testing.
    ///
    /// Using a helper avoids repeating character creation
    /// throughout the test suite.
    /// </summary>
    private static Character CreateTestWarrior()
    {
        var stats = new CharacterStats(
            MaxHealth: MaxHealth,
            Strength: 15,
            Defense: 12,
            Resistance: 8,
            Speed: 10,
            Intelligence: 11,
            Wisdom: 10,
            Movement: 0 // Test fixture value.
        );

        return new Character(
            id: "warrior-1",
            name: "Komodo Warrior",
            teamId: 1,
            baseStats: stats
        );
    }

    // =========================================================
    // CHARACTER CREATION TESTS
    // =========================================================

    [Fact]
    public void Constructor_ValidStats_StartsAtMaximumHealth()
    {
        // Arrange & Act
        var warrior = CreateTestWarrior();

        // Assert
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
    }

    [Fact]
    public void Constructor_ValidStats_CharacterIsNotDefeated()
    {
        // Arrange & Act
        var warrior = CreateTestWarrior();

        // Assert
        Assert.False(warrior.IsDefeated);
    }

    [Fact]
    public void Constructor_ValidStats_PreservesCharacterStats()
    {
        // Arrange & Act
        var warrior = CreateTestWarrior();

        // Assert
        Assert.Equal(15, warrior.BaseStats.Strength);
        Assert.Equal(12, warrior.BaseStats.Defense);
        Assert.Equal(8, warrior.BaseStats.Resistance);
        Assert.Equal(0, warrior.BaseStats.Movement);
        Assert.Equal(10, warrior.BaseStats.Speed);
    }

    // =========================================================
    // DAMAGE TESTS
    // =========================================================

    [Fact]
    public void TakeDamage_ValidAmount_ReducesHealth()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(15);

        // Assert
        Assert.Equal(31, warrior.CurrentHealth);
        Assert.False(warrior.IsDefeated);
    }

    [Fact]
    public void TakeDamage_ExceedsCurrentHealth_HealthStopsAtZero()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(100);

        // Assert
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.True(warrior.IsDefeated);
    }

    [Fact]
    public void TakeDamage_ExactlyCurrentHealth_CharacterIsDefeated()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(MaxHealth);

        // Assert
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.True(warrior.IsDefeated);
    }

    [Fact]
    public void TakeDamage_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        Action action = () => warrior.TakeDamage(-10);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(action);

        Assert.Equal(MaxHealth, warrior.CurrentHealth);
    }

    [Fact]
    public void TakeDamage_ZeroAmount_DoesNotChangeHealth()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(0);

        // Assert
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
    }

    [Theory]
    [InlineData(0, 46)]
    [InlineData(5, 41)]
    [InlineData(10, 36)]
    [InlineData(20, 26)]
    [InlineData(30, 16)]
    [InlineData(46, 0)]
    [InlineData(100, 0)]
    public void TakeDamage_VariousAmounts_ProducesExpectedHealth(
        int damage,
        int expectedHealth)
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(damage);

        // Assert
        Assert.Equal(expectedHealth, warrior.CurrentHealth);
    }

    // =========================================================
    // HEALING TESTS
    // =========================================================

    [Fact]
    public void Heal_ValidAmount_RestoresHealth()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        warrior.TakeDamage(20);

        // Act
        int restored = warrior.Heal(10);

        // Assert
        Assert.Equal(36, warrior.CurrentHealth);
        Assert.Equal(10, restored);
    }

    [Fact]
    public void Heal_ExceedsMissingHealth_CapsAtMaximumHealth()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        warrior.TakeDamage(6);

        // Act
        int restored = warrior.Heal(20);

        // Assert
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
        Assert.Equal(6, restored);
    }

    [Fact]
    public void Heal_AtMaximumHealth_RestoresNothing()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        int restored = warrior.Heal(20);

        // Assert
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
        Assert.Equal(0, restored);
    }

    [Fact]
    public void Heal_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        Action action = () => warrior.Heal(-5);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Heal_DefeatedCharacter_DoesNotRevive()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        warrior.TakeDamage(100);

        // Act
        int restored = warrior.Heal(20);

        // Assert
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.Equal(0, restored);
        Assert.True(warrior.IsDefeated);
    }

    [Theory]
    [InlineData(10, 5, 41)]
    [InlineData(20, 10, 36)]
    [InlineData(30, 20, 36)]
    [InlineData(40, 50, 46)]
    public void Heal_VariousAmounts_ProducesExpectedHealth(
        int damage,
        int healing,
        int expectedHealth)
    {
        // Arrange
        var warrior = CreateTestWarrior();

        warrior.TakeDamage(damage);

        // Act
        warrior.Heal(healing);

        // Assert
        Assert.Equal(expectedHealth, warrior.CurrentHealth);
    }

    // =========================================================
    // DEFEAT TESTS
    // =========================================================

    [Fact]
    public void IsDefeated_WhenHealthIsAboveZero_ReturnsFalse()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        warrior.TakeDamage(45);

        // Act
        bool defeated = warrior.IsDefeated;

        // Assert
        Assert.False(defeated);
        Assert.Equal(1, warrior.CurrentHealth);
    }

    [Fact]
    public void IsDefeated_WhenHealthReachesZero_ReturnsTrue()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.TakeDamage(46);

        // Assert
        Assert.True(warrior.IsDefeated);
    }

    // =========================================================
    // TURN ACTIVATION TESTS
    // =========================================================

    [Fact]
    public void Constructor_NewCharacter_HasNotActed()
    {
        // Arrange & Act
        var warrior = CreateTestWarrior();

        // Assert
        Assert.False(warrior.HasActedThisRound);
    }

    [Fact]
    public void EndActivation_ReadyCharacter_MarksAsActed()
    {
        // Arrange
        var warrior = CreateTestWarrior();

        // Act
        warrior.EndActivation();

        // Assert
        Assert.True(warrior.HasActedThisRound);
    }

    [Fact]
    public void Constructor_PreservesInstanceIdentityTeamAndStatsReference()
    {
        var stats = CreateTestWarrior().BaseStats;

        var warrior = new Character("warrior-2", "Komodo Warrior", 2, stats);

        Assert.Equal("warrior-2", warrior.Id);
        Assert.Equal("Komodo Warrior", warrior.Name);
        Assert.Equal(2, warrior.TeamId);
        Assert.Same(stats, warrior.BaseStats);
        Assert.True(warrior.CanAct);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(36, 36)]
    [InlineData(int.MaxValue, 36)]
    public void TakeDamage_InjuredCharacter_ReturnsOnlyActualHealthLost(int damage, int expectedDamage)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(10);

        int taken = warrior.TakeDamage(damage);

        Assert.Equal(expectedDamage, taken);
        Assert.Equal(36 - expectedDamage, warrior.CurrentHealth);
        Assert.Equal(warrior.CurrentHealth == 0, warrior.IsDefeated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void TakeDamage_RepeatedAfterDefeat_ReturnsZeroAndKeepsHealthAtZero(int damage)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(int.MaxValue);

        Assert.Equal(0, warrior.TakeDamage(damage));
        Assert.Equal(0, warrior.TakeDamage(damage));
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.True(warrior.IsDefeated);
        Assert.False(warrior.CanAct);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, true)]
    public void TakeDamage_NegativeInput_IdentifiesParameterAndPreservesState(int damage, bool defeated)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(defeated ? MaxHealth : 10);
        warrior.EndActivation();
        int healthBefore = warrior.CurrentHealth;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => warrior.TakeDamage(damage));

        Assert.Equal("damage", error.ParamName);
        Assert.Equal(healthBefore, warrior.CurrentHealth);
        Assert.Equal(defeated, warrior.IsDefeated);
        Assert.True(warrior.HasActedThisRound);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, true)]
    public void Heal_NegativeInput_IdentifiesParameterAndPreservesState(int amount, bool defeated)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(defeated ? MaxHealth : 10);
        warrior.EndActivation();
        int healthBefore = warrior.CurrentHealth;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => warrior.Heal(amount));

        Assert.Equal("amount", error.ParamName);
        Assert.Equal(healthBefore, warrior.CurrentHealth);
        Assert.Equal(defeated, warrior.IsDefeated);
        Assert.True(warrior.HasActedThisRound);
    }

    [Fact]
    public void Heal_ZeroOnInjuredCharacter_ReturnsZeroWithoutChangingHealth()
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(10);

        Assert.Equal(0, warrior.Heal(0));
        Assert.Equal(36, warrior.CurrentHealth);
    }

    [Fact]
    public void Heal_LargestAmount_CapsAtMaximumAndDoesNotRevive()
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(MaxHealth - 1);

        Assert.Equal(MaxHealth - 1, warrior.Heal(int.MaxValue));
        Assert.Equal(MaxHealth, warrior.CurrentHealth);

        warrior.TakeDamage(int.MaxValue);

        Assert.Equal(0, warrior.Heal(int.MaxValue));
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.True(warrior.IsDefeated);
    }

    [Fact]
    public void HealthOperations_LargestMaximumHealth_DoNotOverflow()
    {
        var stats = new CharacterStats(int.MaxValue, 0, 0, 0, 0, 0, 0, 0);
        var warrior = new Character("large-health", "Test Character", 1, stats);

        Assert.Equal(int.MaxValue, warrior.CurrentHealth);
        Assert.Equal(int.MaxValue - 1, warrior.TakeDamage(int.MaxValue - 1));
        Assert.Equal(1, warrior.CurrentHealth);
        Assert.Equal(int.MaxValue - 1, warrior.Heal(int.MaxValue));
        Assert.Equal(int.MaxValue, warrior.CurrentHealth);
        Assert.Equal(int.MaxValue, warrior.TakeDamage(int.MaxValue));
        Assert.Equal(0, warrior.CurrentHealth);
    }

    [Fact]
    public void ResetActivation_LivingCharacter_IsReadyForNewRoundWithoutHealing()
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(10);
        warrior.EndActivation();
        warrior.EndActivation();
        Assert.False(warrior.CanAct);

        warrior.ResetActivation();
        warrior.ResetActivation();

        Assert.False(warrior.HasActedThisRound);
        Assert.True(warrior.CanAct);
        Assert.Equal(36, warrior.CurrentHealth);
    }

    [Fact]
    public void ResetActivation_DefeatedCharacter_RemainsUnableToAct()
    {
        var warrior = CreateTestWarrior();
        warrior.EndActivation();
        warrior.TakeDamage(MaxHealth);

        warrior.ResetActivation();

        Assert.False(warrior.HasActedThisRound);
        Assert.True(warrior.IsDefeated);
        Assert.False(warrior.CanAct);
        Assert.Equal(0, warrior.CurrentHealth);
    }

    [Fact]
    public void Instances_SharedStats_HaveIndependentHealthAndActivation()
    {
        var first = CreateTestWarrior();
        var second = new Character("warrior-2", first.Name, 2, first.BaseStats);

        first.TakeDamage(10);
        first.EndActivation();

        Assert.Equal(36, first.CurrentHealth);
        Assert.Equal(MaxHealth, second.CurrentHealth);
        Assert.True(first.HasActedThisRound);
        Assert.True(second.CanAct);
        Assert.False(second.HasActedThisRound);

        second.EndActivation();
        first.ResetActivation();
        first.Heal(5);

        Assert.Equal(41, first.CurrentHealth);
        Assert.Equal(MaxHealth, second.CurrentHealth);
        Assert.True(first.CanAct);
        Assert.True(second.HasActedThisRound);
        Assert.Same(first.BaseStats, second.BaseStats);
    }

    [Fact]
    public void Constructor_NewCharacter_StartsWithoutShield()
    {
        Assert.Equal(0, CreateTestWarrior().ShieldHP);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void SetShieldHP_ValidAmount_DoesNotChangeHealthOrBaseStats(int shieldHP)
    {
        var warrior = CreateTestWarrior();
        var stats = warrior.BaseStats with { };

        warrior.SetShieldHP(shieldHP);

        Assert.Equal(shieldHP, warrior.ShieldHP);
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
        Assert.Equal(stats, warrior.BaseStats);
        Assert.False(warrior.HasActedThisRound);
        Assert.Equal(0, warrior.HealsReceivedThisRound);
    }

    [Fact]
    public void SetShieldHP_ReplacesRemainingValueRatherThanAddingToIt()
    {
        var warrior = CreateTestWarrior();
        warrior.SetShieldHP(6);
        warrior.TakeDamage(2);
        Assert.Equal(4, warrior.ShieldHP);

        warrior.SetShieldHP(6);
        Assert.Equal(6, warrior.ShieldHP);
        warrior.SetShieldHP(3);
        Assert.Equal(3, warrior.ShieldHP);
        warrior.SetShieldHP(0);
        Assert.Equal(0, warrior.ShieldHP);
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, true)]
    public void SetShieldHP_NegativeAmount_IsRejectedWithoutChangingState(int amount, bool defeated)
    {
        var warrior = CreateTestWarrior();
        warrior.SetShieldHP(6);
        if (defeated) warrior.TakeDamage(int.MaxValue);
        int shieldBefore = warrior.ShieldHP;
        int healthBefore = warrior.CurrentHealth;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => warrior.SetShieldHP(amount));

        Assert.Equal("shieldHP", error.ParamName);
        Assert.Equal(shieldBefore, warrior.ShieldHP);
        Assert.Equal(healthBefore, warrior.CurrentHealth);
    }

    [Theory]
    [InlineData(0, 0, 6, 46)]
    [InlineData(2, 0, 4, 46)]
    [InlineData(6, 0, 0, 46)]
    [InlineData(8, 2, 0, 44)]
    [InlineData(52, 46, 0, 0)]
    [InlineData(int.MaxValue, 46, 0, 0)]
    public void TakeDamage_UsesShieldBeforeHealth_AndReturnsOnlyHpLost(
        int damage, int healthLost, int shieldRemaining, int healthRemaining)
    {
        var warrior = CreateTestWarrior();
        warrior.SetShieldHP(6);

        Assert.Equal(healthLost, warrior.TakeDamage(damage));
        Assert.Equal(shieldRemaining, warrior.ShieldHP);
        Assert.Equal(healthRemaining, warrior.CurrentHealth);
        Assert.Equal(healthRemaining == 0, warrior.IsDefeated);
    }

    [Fact]
    public void TakeDamage_LargestHealthAndShield_DoNotRequireAddingTheirValues()
    {
        var warrior = new Character("large", "Test", 1, new CharacterStats(int.MaxValue, 0, 0, 0, 0, 0, 0, 0));
        warrior.SetShieldHP(int.MaxValue);

        Assert.Equal(0, warrior.TakeDamage(int.MaxValue));
        Assert.Equal(0, warrior.ShieldHP);
        Assert.Equal(int.MaxValue, warrior.CurrentHealth);
        Assert.Equal(int.MaxValue, warrior.TakeDamage(int.MaxValue));
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.Equal(0, warrior.TakeDamage(int.MaxValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TakeDamage_NegativeInput_LeavesShieldAndHealthUnchanged(int damage)
    {
        var warrior = CreateTestWarrior();
        warrior.SetShieldHP(6);

        Assert.Throws<ArgumentOutOfRangeException>(() => warrior.TakeDamage(damage));
        Assert.Equal(6, warrior.ShieldHP);
        Assert.Equal(MaxHealth, warrior.CurrentHealth);
    }

    [Fact]
    public void TakeDamage_LargestDamageAgainstNearlyLargestShield_OnlyLosesOneHp()
    {
        var warrior = CreateTestWarrior();
        warrior.SetShieldHP(int.MaxValue - 1);

        Assert.Equal(1, warrior.TakeDamage(int.MaxValue));
        Assert.Equal(0, warrior.ShieldHP);
        Assert.Equal(MaxHealth - 1, warrior.CurrentHealth);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void SetShieldHP_DefeatedCharacter_CannotGainProtectionOrRevive(int amount)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(MaxHealth);

        Assert.Throws<InvalidOperationException>(() => warrior.SetShieldHP(amount));
        Assert.Equal(0, warrior.ShieldHP);
        Assert.Equal(0, warrior.CurrentHealth);
        Assert.True(warrior.IsDefeated);
    }

    [Fact]
    public void SetShieldHP_ZeroOnDefeatedCharacter_IsAllowed()
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(MaxHealth);

        warrior.SetShieldHP(0);

        Assert.Equal(0, warrior.ShieldHP);
        Assert.True(warrior.IsDefeated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Healing_OnlyRestoresHealth_NotConsumedShield(bool trackedHealing)
    {
        var warrior = CreateTestWarrior();
        warrior.TakeDamage(10);
        warrior.SetShieldHP(6);
        warrior.TakeDamage(2);

        if (trackedHealing)
            PawnsOfDivinity.Core.Combat.HealingApplicator.Apply(5, warrior);
        else
            warrior.Heal(5);

        Assert.Equal(41, warrior.CurrentHealth);
        Assert.Equal(4, warrior.ShieldHP);
    }

    [Fact]
    public void Shields_AreIndependentEvenWhenCharactersShareStats()
    {
        var first = CreateTestWarrior();
        var second = new Character("warrior-2", first.Name, 2, first.BaseStats);
        first.SetShieldHP(6);

        first.TakeDamage(2);
        second.TakeDamage(5);

        Assert.Equal(4, first.ShieldHP);
        Assert.Equal(0, second.ShieldHP);
        Assert.Equal(MaxHealth, first.CurrentHealth);
        Assert.Equal(41, second.CurrentHealth);
        Assert.Same(first.BaseStats, second.BaseStats);
    }

    [Fact]
    public void DamageAndHealing_DoNotChangeBaseStatsOrIdentity()
    {
        var warrior = CreateTestWarrior();
        var originalStats = warrior.BaseStats;
        var expectedStats = new CharacterStats(
            MaxHealth: 46, Strength: 15, Defense: 12, Intelligence: 11,
            Resistance: 8, Wisdom: 10, Speed: 10, Movement: 0);

        warrior.TakeDamage(20);
        warrior.Heal(10);

        Assert.Equal(36, warrior.CurrentHealth);
        Assert.Same(originalStats, warrior.BaseStats);
        Assert.Equal(expectedStats, warrior.BaseStats);
        Assert.Equal("warrior-1", warrior.Id);
        Assert.Equal("Komodo Warrior", warrior.Name);
        Assert.Equal(1, warrior.TeamId);
        Assert.True(warrior.CanAct);
    }
}
