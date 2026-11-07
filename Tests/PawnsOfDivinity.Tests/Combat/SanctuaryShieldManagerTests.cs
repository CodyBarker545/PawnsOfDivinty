using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class SanctuaryShieldManagerTests
{
    [Fact]
    public void Apply_FirstCast_GrantsSixAndTracksRecipient()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        Assert.Null(manager.GetRecipient(guardian));

        manager.Apply(guardian, recipient);

        Assert.Equal(6, recipient.ShieldHP);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    [Fact]
    public void Apply_SameRecipient_RefreshesRemainingShieldWithoutStacking()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        manager.Apply(guardian, recipient);
        Assert.Equal(0, recipient.TakeDamage(2));
        Assert.Equal(4, recipient.ShieldHP);

        manager.Apply(guardian, recipient);
        manager.Apply(guardian, recipient);

        Assert.Equal(6, recipient.ShieldHP);
        Assert.Equal(52, recipient.CurrentHealth);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    [Fact]
    public void Apply_DifferentRecipient_RemovesOldRemainderAndGrantsSix()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var previous = Create("nigel");
        var next = Create("hooded_assassin");
        manager.Apply(guardian, previous);
        previous.TakeDamage(2);

        manager.Apply(guardian, next);

        Assert.Equal(0, previous.ShieldHP);
        Assert.Equal(6, next.ShieldHP);
        Assert.Same(next, manager.GetRecipient(guardian));
    }

    [Fact]
    public void DepletedShield_HasNoActiveRecipient_AndCanBeRefreshedOrTransferred()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var first = Create("nigel");
        var second = Create("hooded_assassin");
        manager.Apply(guardian, first);
        first.TakeDamage(6);

        Assert.Equal(0, first.ShieldHP);
        Assert.Null(manager.GetRecipient(guardian));
        manager.Apply(guardian, first);
        Assert.Equal(6, first.ShieldHP);
        Assert.Same(first, manager.GetRecipient(guardian));
        first.TakeDamage(6);

        manager.Apply(guardian, second);

        Assert.Equal(0, first.ShieldHP);
        Assert.Equal(6, second.ShieldHP);
        Assert.Same(second, manager.GetRecipient(guardian));
    }

    [Fact]
    public void Ownership_UsesGuardianInstances_EvenWithSameDefinitionAndId()
    {
        var definition = CharacterRegistry.GetCharacter("gentle_guardian");
        Assert.NotNull(definition);
        // Deliberately duplicate the text ID to check that ownership uses object identity.
        var firstGuardian = CharacterFactory.Create(definition, "same-id", 1);
        var secondGuardian = CharacterFactory.Create(definition, "same-id", 1);
        var firstRecipient = Create("nigel");
        var secondRecipient = Create("hooded_assassin");
        var thirdRecipient = Create("time_weaver");
        var manager = new SanctuaryShieldManager();
        manager.Apply(firstGuardian, firstRecipient);
        manager.Apply(secondGuardian, secondRecipient);

        manager.Apply(firstGuardian, thirdRecipient);

        Assert.Equal(0, firstRecipient.ShieldHP);
        Assert.Equal(6, secondRecipient.ShieldHP);
        Assert.Equal(6, thirdRecipient.ShieldHP);
        Assert.Same(thirdRecipient, manager.GetRecipient(firstGuardian));
        Assert.Same(secondRecipient, manager.GetRecipient(secondGuardian));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_NullArgument_LeavesExistingShieldAlone(bool nullGuardian)
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        manager.Apply(guardian, recipient);

        var error = Assert.Throws<ArgumentNullException>(() => manager.Apply(
            nullGuardian ? null! : guardian, nullGuardian ? recipient : null!));

        Assert.Equal(nullGuardian ? "guardian" : "recipient", error.ParamName);
        Assert.Equal(6, recipient.ShieldHP);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    [Fact]
    public void GetRecipient_NullGuardian_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SanctuaryShieldManager().GetRecipient(null!));
    }

    [Fact]
    public void Apply_DefeatedRecipient_RejectsBeforeRemovingExistingShield()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var previous = Create("nigel");
        var next = Create("hooded_assassin");
        manager.Apply(guardian, previous);
        previous.TakeDamage(2);
        next.TakeDamage(next.CurrentHealth);

        Assert.Throws<InvalidOperationException>(() => manager.Apply(guardian, next));

        Assert.Equal(4, previous.ShieldHP);
        Assert.Equal(0, next.ShieldHP);
        Assert.Equal(0, next.CurrentHealth);
        Assert.Same(previous, manager.GetRecipient(guardian));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(int.MaxValue)]
    public void Transfer_ToUnrelatedShield_IsRejectedWithoutPartialChanges(int otherShield)
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var previous = Create("nigel");
        var next = Create("hooded_assassin");
        manager.Apply(guardian, previous);
        previous.TakeDamage(2);
        next.SetShieldHP(otherShield);

        Assert.Throws<InvalidOperationException>(() => manager.Apply(guardian, next));

        Assert.Equal(4, previous.ShieldHP);
        Assert.Equal(otherShield, next.ShieldHP);
        Assert.Same(previous, manager.GetRecipient(guardian));
    }

    [Fact]
    public void Transfer_ToAnotherGuardiansShield_IsRejectedWithoutChangingEitherOwner()
    {
        var manager = new SanctuaryShieldManager();
        var firstGuardian = Create("gentle_guardian");
        var secondGuardian = Create("gentle_guardian");
        var first = Create("nigel");
        var second = Create("hooded_assassin");
        manager.Apply(firstGuardian, first);
        manager.Apply(secondGuardian, second);

        Assert.Throws<InvalidOperationException>(() => manager.Apply(firstGuardian, second));

        Assert.Equal(6, first.ShieldHP);
        Assert.Equal(6, second.ShieldHP);
        Assert.Same(first, manager.GetRecipient(firstGuardian));
        Assert.Same(second, manager.GetRecipient(secondGuardian));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transfer_AfterDepletion_DoesNotEraseNewProtectionOnPreviousRecipient(bool anotherGuardian)
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var otherGuardian = Create("gentle_guardian");
        var previous = Create("nigel");
        var next = Create("hooded_assassin");
        manager.Apply(guardian, previous);
        previous.TakeDamage(6);
        if (anotherGuardian) manager.Apply(otherGuardian, previous);
        else previous.SetShieldHP(9);

        manager.Apply(guardian, next);

        Assert.Equal(anotherGuardian ? 6 : 9, previous.ShieldHP);
        Assert.Equal(6, next.ShieldHP);
        Assert.Same(next, manager.GetRecipient(guardian));
        if (anotherGuardian) Assert.Same(previous, manager.GetRecipient(otherGuardian));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public void DirectAggregateAssignment_CannotOverwriteOrClearOwnedShield(int amount)
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        manager.Apply(guardian, recipient);

        Assert.Throws<InvalidOperationException>(() => recipient.SetShieldHP(amount));

        Assert.Equal(6, recipient.ShieldHP);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    [Fact]
    public void GuardianDeath_DoesNotRemoveShieldOnSurvivingRecipient()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        manager.Apply(guardian, recipient);

        guardian.TakeDamage(guardian.CurrentHealth);

        Assert.True(guardian.IsDefeated);
        Assert.Equal(6, recipient.ShieldHP);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    [Fact]
    public void OwnedShield_UsesExistingAbsorption_AndOrdinaryHealingDoesNotRefreshIt()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var recipient = Create("nigel");
        recipient.TakeDamage(10);
        manager.Apply(guardian, recipient);

        Assert.Equal(0, recipient.TakeDamage(2));
        Assert.Equal(4, recipient.ShieldHP);
        Assert.Equal(5, recipient.Heal(5));
        Assert.Equal(4, recipient.ShieldHP);
        Assert.Equal(2, recipient.TakeDamage(6));
        Assert.Equal(45, recipient.CurrentHealth);
        Assert.Equal(0, recipient.ShieldHP);
        Assert.Null(manager.GetRecipient(guardian));
    }

    [Fact]
    public void Transfer_PreservesHealthStatsActivationAndUnrelatedCharacters()
    {
        var manager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var previous = Create("nigel");
        var next = Create("hooded_assassin");
        var unrelated = Create("time_weaver");
        foreach (var character in new[] { guardian, previous, next, unrelated })
        {
            character.TakeDamage(5);
            character.EndActivation();
        }
        unrelated.SetShieldHP(9);
        HealingApplicator.Apply(0, next);
        var guardianStats = guardian.BaseStats with { };
        var previousStats = previous.BaseStats with { };
        var nextStats = next.BaseStats with { };
        manager.Apply(guardian, previous);

        manager.Apply(guardian, next);

        Assert.Equal(39, guardian.CurrentHealth);
        Assert.Equal(47, previous.CurrentHealth);
        Assert.Equal(33, next.CurrentHealth);
        Assert.Equal(33, unrelated.CurrentHealth);
        Assert.Equal(9, unrelated.ShieldHP);
        Assert.Equal(guardianStats, guardian.BaseStats);
        Assert.Equal(previousStats, previous.BaseStats);
        Assert.Equal(nextStats, next.BaseStats);
        Assert.All(new[] { guardian, previous, next, unrelated }, character => Assert.True(character.HasActedThisRound));
        Assert.Equal(1, next.HealsReceivedThisRound);
        Assert.Equal(0, previous.HealsReceivedThisRound);
    }

    [Fact]
    public void DifferentManagerObjects_StillMaintainOneRecipientForTheSameGuardian()
    {
        var firstManager = new SanctuaryShieldManager();
        var secondManager = new SanctuaryShieldManager();
        var guardian = Create("gentle_guardian");
        var first = Create("nigel");
        var second = Create("hooded_assassin");
        firstManager.Apply(guardian, first);

        secondManager.Apply(guardian, second);

        Assert.Equal(0, first.ShieldHP);
        Assert.Equal(6, second.ShieldHP);
        Assert.Same(second, firstManager.GetRecipient(guardian));
        Assert.Same(second, secondManager.GetRecipient(guardian));
    }

    private static Character Create(string id)
    {
        var character = CharacterFactory.Create(id, 1);
        Assert.NotNull(character);
        return character;
    }
}
