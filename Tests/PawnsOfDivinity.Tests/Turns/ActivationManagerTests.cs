using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class ActivationManagerTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void StandardSixVsSix_UsesExplicitStartingTeam_AndAlternatesUntilComplete(int startingTeam)
    {
        string[] ids = { "hooded_assassin", "gentle_guardian", "time_weaver", "nigel", "gravebound_colossus", "beothuk_spirit_guardian" };
        var first = ids.Select(id => CharacterFactory.Create(id, 10)!).ToArray();
        var second = ids.Select(id => CharacterFactory.Create(id, 20)!).ToArray();
        var manager = new ActivationManager(first, second, startingTeam);
        Assert.Equal(6, manager.FirstTeam.Count);
        Assert.Equal(6, manager.SecondTeam.Count);
        Assert.Equal(1, manager.CurrentRound);
        Assert.Equal(startingTeam, manager.NextTeamId);

        for (int i = 0; i < 12; i++)
        {
            int team = i % 2 == 0 ? startingTeam : startingTeam == 10 ? 20 : 10;
            Assert.Equal(team, manager.NextTeamId);
            var character = (team == 10 ? first : second).First(unit => unit.CanAct);
            manager.StartActivation(character);
            Assert.Same(character, manager.ActiveCharacter);
            Assert.Null(manager.NextTeamId);
            Assert.False(character.HasActedThisRound);
            Assert.False(manager.IsRoundComplete);
            manager.CompleteActivation();
            Assert.True(character.HasActedThisRound);
            Assert.Null(manager.ActiveCharacter);
        }

        Assert.True(manager.IsRoundComplete);
        Assert.Null(manager.NextTeamId);
        Assert.Equal(1, manager.CurrentRound); // No automatic round transition.
    }

    [Fact]
    public void SmallTeams_CanExplicitlyEndAnActivationWithoutTakingAnyActions()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);

        manager.StartActivation(first);
        manager.CompleteActivation();
        Assert.Equal(2, manager.NextTeamId);
        manager.StartActivation(second);
        manager.CompleteActivation();

        Assert.True(manager.IsRoundComplete);
        Assert.Equal(first.BaseStats.MaxHealth, first.CurrentHealth);
        Assert.Equal(second.BaseStats.MaxHealth, second.CurrentHealth);
    }

    [Fact]
    public void EnemyAndNonRosterInstances_CannotStartActivation()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        var impostor = new Character(first.Id, first.Name, first.TeamId, first.BaseStats);

        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(second));
        Assert.Throws<ArgumentException>(() => manager.StartActivation(Unit(1)));
        Assert.Throws<ArgumentException>(() => manager.StartActivation(impostor));

        Assert.Null(manager.ActiveCharacter);
        Assert.Equal(1, manager.NextTeamId);
        Assert.True(first.CanAct);
    }

    [Fact]
    public void ActiveActivation_CannotBeReplacedOrAdvancedToNewRound()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        manager.StartActivation(first);

        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(first));
        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(second));
        Assert.Throws<InvalidOperationException>(() => manager.StartNextRound(2));

        Assert.Same(first, manager.ActiveCharacter);
        Assert.Equal(1, manager.CurrentRound);
        Assert.False(first.HasActedThisRound);
    }

    [Fact]
    public void CompletedCharacter_CannotActivateTwice_EvenWhenItsTeamHasAnotherUnit()
    {
        var first = Unit(1);
        var remaining = Unit(1);
        var second = Unit(2);
        var manager = new ActivationManager(new[] { first, remaining }, new[] { second }, 1);
        manager.StartActivation(first);
        manager.CompleteActivation();
        manager.StartActivation(second);
        manager.CompleteActivation();

        Assert.Equal(1, manager.NextTeamId);
        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(first));
        Assert.Null(manager.ActiveCharacter);
        manager.StartActivation(remaining);
        manager.CompleteActivation();
        Assert.True(manager.IsRoundComplete);
    }

    [Fact]
    public void DefeatedAndAlreadyActivatedCharacters_AreUnavailable_WithoutConstructorReset()
    {
        var dead = Unit(1);
        dead.TakeDamage(int.MaxValue);
        var acted = Unit(1);
        acted.EndActivation();
        var ready = Unit(1);
        var manager = new ActivationManager(new[] { dead, acted, ready }, new[] { Unit(2) }, 1);

        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(dead));
        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(acted));
        Assert.True(acted.HasActedThisRound);
        manager.StartActivation(ready);
        Assert.Same(ready, manager.ActiveCharacter);
    }

    [Fact]
    public void ExhaustedTeam_IsSkipped_WhileOtherTeamFinishesRemainingUnits()
    {
        var first = Unit(1);
        var second = new[] { Unit(2), Unit(2), Unit(2) };
        var manager = new ActivationManager(new[] { first }, second, 1);
        manager.StartActivation(first);
        manager.CompleteActivation();

        foreach (var character in second)
        {
            Assert.Equal(2, manager.NextTeamId);
            manager.StartActivation(character);
            manager.CompleteActivation();
        }
        Assert.True(manager.IsRoundComplete);
    }

    [Fact]
    public void ExternalDefeat_IsReflectedInTeamSelectionAndRoundCompletion()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        first.TakeDamage(int.MaxValue);

        Assert.Equal(2, manager.NextTeamId);
        Assert.False(manager.IsRoundComplete);
        second.TakeDamage(int.MaxValue);
        Assert.True(manager.IsRoundComplete);
        Assert.Null(manager.NextTeamId);
        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(second));
    }

    [Fact]
    public void CharacterDefeatedDuringActivation_CanBeExplicitlyCompleted()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        manager.StartActivation(first);
        first.TakeDamage(int.MaxValue);
        Assert.False(manager.IsRoundComplete);

        manager.CompleteActivation();

        Assert.True(first.HasActedThisRound);
        Assert.Null(manager.ActiveCharacter);
        Assert.Equal(2, manager.NextTeamId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NewRound_RequiresExplicitStartAndStartingTeam_ResetsOnlyActivation(int startingTeam)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.TakeDamage(5);
        HealingApplicator.Apply(1, first);
        first.SetShieldHP(6);
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        first.Statuses.ApplyPoison();
        var stats = first.BaseStats;
        var manager = Manager(first, second);
        manager.StartActivation(first);
        manager.CompleteActivation();
        manager.StartActivation(second);
        manager.CompleteActivation();
        Assert.True(manager.IsRoundComplete);
        Assert.Equal(1, manager.CurrentRound);

        manager.StartNextRound(startingTeam);

        Assert.Equal(2, manager.CurrentRound);
        Assert.Equal(startingTeam, manager.NextTeamId);
        Assert.False(manager.IsRoundComplete);
        Assert.True(first.CanAct);
        Assert.True(second.CanAct);
        Assert.Equal(stats.MaxHealth - 4, first.CurrentHealth);
        Assert.Equal(6, first.ShieldHP);
        Assert.Equal(1, first.HealsReceivedThisRound);
        Assert.Equal(2, first.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(first.Statuses.IsPoisoned);
        Assert.Equal(0, first.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, first.BaseStats);
        Assert.Equal(second.BaseStats.MaxHealth, second.CurrentHealth);
        Assert.Empty(second.Statuses.TimedStatuses);
        manager.StartActivation(startingTeam == 1 ? first : second);
    }

    [Fact]
    public void NewRound_DoesNotReviveDefeatedCharacters()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.TakeDamage(int.MaxValue);
        second.EndActivation();
        var manager = Manager(first, second);
        Assert.True(manager.IsRoundComplete);

        manager.StartNextRound(1);

        Assert.False(first.CanAct);
        Assert.Equal(0, first.CurrentHealth);
        Assert.Equal(2, manager.NextTeamId);
        Assert.True(second.CanAct);
    }

    [Fact]
    public void InvalidLifecycleCalls_LeaveStateUntouched()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);

        Assert.Throws<ArgumentNullException>(() => manager.StartActivation(null!));
        Assert.Throws<InvalidOperationException>(() => manager.CompleteActivation());
        Assert.Throws<InvalidOperationException>(() => manager.StartNextRound(1));
        manager.StartActivation(first);
        manager.CompleteActivation();
        manager.StartActivation(second);
        manager.CompleteActivation();
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.StartNextRound(99));
        Assert.Equal(1, manager.CurrentRound);
        Assert.True(first.HasActedThisRound);
        Assert.True(second.HasActedThisRound);
    }

    [Theory]
    [InlineData("nullFirst")]
    [InlineData("nullSecond")]
    [InlineData("emptyFirst")]
    [InlineData("emptySecond")]
    [InlineData("nullMember")]
    [InlineData("mixedTeams")]
    [InlineData("sameTeamId")]
    [InlineData("duplicateInstance")]
    [InlineData("duplicateAcrossTeams")]
    [InlineData("duplicateId")]
    [InlineData("duplicateIdAcrossTeams")]
    [InlineData("startingTeam")]
    public void InvalidRosters_AreRejectedWithoutMutatingCharacters(string invalid)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        IEnumerable<Character> firstTeam = new[] { first };
        IEnumerable<Character> secondTeam = new[] { second };
        int startingTeam = 1;
        switch (invalid)
        {
            case "nullFirst": firstTeam = null!; break;
            case "nullSecond": secondTeam = null!; break;
            case "emptyFirst": firstTeam = Array.Empty<Character>(); break;
            case "emptySecond": secondTeam = Array.Empty<Character>(); break;
            case "nullMember": firstTeam = new[] { first, null! }; break;
            case "mixedTeams": firstTeam = new[] { first, Unit(3) }; break;
            case "sameTeamId": secondTeam = new[] { Unit(1) }; break;
            case "duplicateInstance": firstTeam = new[] { first, first }; break;
            case "duplicateAcrossTeams": secondTeam = new[] { first }; break;
            case "duplicateId": firstTeam = new[] { first, new Character(first.Id, "Other", 1, first.BaseStats) }; break;
            case "duplicateIdAcrossTeams": secondTeam = new[] { new Character(first.Id, "Other", 2, first.BaseStats) }; break;
            case "startingTeam": startingTeam = 99; break;
        }

        Assert.ThrowsAny<ArgumentException>(() => new ActivationManager(firstTeam, secondTeam, startingTeam));
        Assert.True(first.HasActedThisRound);
        Assert.True(second.CanAct);
    }

    [Fact]
    public void Rosters_AreReadOnlySnapshots_OfTheOriginalRuntimeInstances()
    {
        var first = Unit(1);
        var second = Unit(2);
        var inputFirst = new List<Character> { first };
        var inputSecond = new List<Character> { second };
        var manager = new ActivationManager(inputFirst, inputSecond, 1);
        inputFirst.Clear();
        inputSecond.Add(Unit(2));

        Assert.Single(manager.FirstTeam);
        Assert.Single(manager.SecondTeam);
        Assert.Same(first, manager.FirstTeam[0]);
        Assert.Same(second, manager.SecondTeam[0]);
        foreach (var roster in new[] { manager.FirstTeam, manager.SecondTeam })
        {
            var list = Assert.IsAssignableFrom<IList<Character>>(roster);
            Assert.Throws<NotSupportedException>(() => list.Clear());
            Assert.Throws<NotSupportedException>(() => list.Add(Unit(1)));
            Assert.Throws<NotSupportedException>(() => list[0] = Unit(1));
        }
    }

    [Fact]
    public void StartingActivation_ProvidesFreshIndependentActionBudgets()
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        Assert.False(manager.IsActivationActive);
        Assert.False(manager.CanMove);
        Assert.False(manager.CanUseUtilityAction);
        Assert.False(manager.CanUseMainAction);

        manager.StartActivation(first);

        Assert.True(manager.IsActivationActive);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
        Assert.False(first.HasActedThisRound);
    }

    [Theory]
    [InlineData("move,main")]
    [InlineData("utility,move,main")]
    [InlineData("move,utility,main")]
    [InlineData("main")]
    [InlineData("utility,end")]
    [InlineData("move,end")]
    public void LegalSequences_CompleteActivationAndAlternateTeams(string sequence)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        manager.StartActivation(first);

        foreach (string action in sequence.Split(',')) RecordAction(manager, action);

        Assert.Equal(sequence.Contains("move"), manager.MovementUsed);
        Assert.Equal(sequence.Contains("utility"), manager.UtilityActionUsed);
        Assert.Equal(sequence.Contains("main"), manager.MainActionUsed);
        Assert.False(manager.IsActivationActive);
        Assert.Null(manager.ActiveCharacter);
        Assert.True(first.HasActedThisRound);
        Assert.Equal(2, manager.NextTeamId);
        Assert.Equal(1, manager.CurrentRound);
        Assert.False(manager.CanMove);
        Assert.False(manager.CanUseUtilityAction);
        Assert.False(manager.CanUseMainAction);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("utility")]
    public void RepeatedAction_IsRejectedWithoutUsingOtherBudgets(string action)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        manager.StartActivation(first);
        RecordAction(manager, action);

        Assert.Throws<InvalidOperationException>(() => RecordAction(manager, action));

        Assert.Same(first, manager.ActiveCharacter);
        Assert.True(manager.IsActivationActive);
        Assert.Equal(action == "move", manager.MovementUsed);
        Assert.Equal(action == "utility", manager.UtilityActionUsed);
        Assert.Equal(action != "move", manager.CanMove);
        Assert.Equal(action != "utility", manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        Assert.False(manager.MainActionUsed);
        Assert.False(first.HasActedThisRound);
        Assert.Null(manager.NextTeamId);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("utility")]
    [InlineData("main")]
    public void ActionsWithoutSelection_AreRejectedWithoutStateChanges(string action)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));

        Assert.Throws<InvalidOperationException>(() => RecordAction(manager, action));

        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
        Assert.False(manager.IsActivationActive);
        Assert.True(first.CanAct);
        Assert.Equal(1, manager.NextTeamId);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("utility")]
    [InlineData("main")]
    public void ActionsAfterMainCompletion_AreRejectedWithoutConsumingUnusedBudgets(string action)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        manager.StartActivation(first);
        manager.CompleteMainAction();

        Assert.Throws<InvalidOperationException>(() => RecordAction(manager, action));

        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.True(manager.MainActionUsed);
        Assert.True(first.HasActedThisRound);
        Assert.Equal(2, manager.NextTeamId);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("utility")]
    [InlineData("main")]
    public void ActionsAfterEarlyEnd_AreRejected(string action)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        manager.StartActivation(first);
        manager.CompleteActivation();

        Assert.Throws<InvalidOperationException>(() => RecordAction(manager, action));

        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
        Assert.False(manager.IsActivationActive);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("utility")]
    [InlineData("main")]
    public void DefeatedActiveCharacter_CannotRecordActions_ButCanBeExplicitlyCompleted(string action)
    {
        var first = Unit(1);
        var manager = Manager(first, Unit(2));
        manager.StartActivation(first);
        first.TakeDamage(int.MaxValue);

        Assert.Throws<InvalidOperationException>(() => RecordAction(manager, action));

        Assert.False(manager.CanMove);
        Assert.False(manager.CanUseUtilityAction);
        Assert.False(manager.CanUseMainAction);
        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
        Assert.Same(first, manager.ActiveCharacter);
        manager.CompleteActivation();
        Assert.Equal(2, manager.NextTeamId);
    }

    [Fact]
    public void EveryNewActivation_ResetsBudgets_IncludingAfterManualRoundAdvance()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        manager.StartActivation(first);
        manager.RecordMovement();
        manager.RecordUtilityAction();
        manager.CompleteMainAction();
        Assert.True(manager.MovementUsed);
        Assert.True(manager.UtilityActionUsed);
        Assert.True(manager.MainActionUsed);

        manager.StartActivation(second);

        Assert.False(manager.MovementUsed);
        Assert.False(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        manager.CompleteMainAction();
        Assert.True(manager.IsRoundComplete);
        Assert.Equal(1, manager.CurrentRound);
        manager.StartNextRound(1);
        Assert.True(first.CanAct);
        Assert.True(second.CanAct);
        Assert.False(manager.CanUseMainAction); // No selection yet in round 2.
        manager.StartActivation(first);
        Assert.False(manager.MainActionUsed);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        Assert.Equal(2, manager.CurrentRound);
    }

    [Fact]
    public void FailedSelection_DoesNotResetCurrentBudgets()
    {
        var first = Unit(1);
        var second = Unit(2);
        var manager = Manager(first, second);
        manager.StartActivation(first);
        manager.RecordMovement();
        manager.RecordUtilityAction();

        Assert.Throws<InvalidOperationException>(() => manager.StartActivation(second));

        Assert.Same(first, manager.ActiveCharacter);
        Assert.True(manager.MovementUsed);
        Assert.True(manager.UtilityActionUsed);
        Assert.False(manager.MainActionUsed);
    }

    [Fact]
    public void RecordingActions_OnlyChangesBudgetsAndActivationCompletion()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.TakeDamage(7);
        HealingApplicator.Apply(1, first);
        first.SetShieldHP(6);
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        first.Statuses.ApplyPoison();
        var stats = first.BaseStats;
        var snapshot = stats with { };
        var manager = Manager(first, second);
        manager.StartActivation(first);

        manager.RecordUtilityAction();
        manager.RecordMovement();
        Assert.False(first.HasActedThisRound);
        manager.CompleteMainAction();

        Assert.Equal(stats.MaxHealth - 6, first.CurrentHealth);
        Assert.Equal(6, first.ShieldHP);
        Assert.Equal(1, first.HealsReceivedThisRound);
        Assert.Equal(2, first.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(first.Statuses.IsPoisoned);
        Assert.Equal(0, first.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, first.BaseStats);
        Assert.Equal(snapshot, first.BaseStats);
        Assert.Equal(second.BaseStats.MaxHealth, second.CurrentHealth);
        Assert.True(second.CanAct);
    }

    private static void RecordAction(ActivationManager manager, string action)
    {
        switch (action)
        {
            case "move": manager.RecordMovement(); break;
            case "utility": manager.RecordUtilityAction(); break;
            case "main": manager.CompleteMainAction(); break;
            case "end": manager.CompleteActivation(); break;
            default: throw new ArgumentException("Unknown test action.", nameof(action));
        }
    }

    private static Character Unit(int teamId) => CharacterFactory.Create("nigel", teamId)!;
    private static ActivationManager Manager(Character first, Character second)
        => new(new[] { first }, new[] { second }, first.TeamId);
}
