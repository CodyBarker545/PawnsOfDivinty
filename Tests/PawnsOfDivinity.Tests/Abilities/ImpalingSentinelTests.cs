using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class ImpalingSentinelTests
{
    [Fact]
    public void PreparationUsesOneMainActionAndReuseTwoWithoutAnImmediateAttack()
    {
        var s = new Setup();
        var result = s.Prepare();
        Assert.True(result.Succeeded);
        Assert.Equal(s.Owner.Id, result.Preparation!.OwnerCharacterId);
        Assert.Equal(2, result.Preparation.PreparationRound);
        Assert.Equal(2, result.Preparation.ExpirationRound);
        Assert.Same(s.Lance, result.Preparation.Lance);
        Assert.Equal(SentinelReactionLimit.ProvisionalOnePerPreparation, result.Preparation.ReactionLimit);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(4, result.Finalization!.ReadyRound);
        Assert.Equal(64, s.Owner.CurrentHealth);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Equal(new GridPosition(3, 5), s.Enemy.TacticalPosition);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Fact]
    public void PreparationPreviewDoesNotReserveOrConsumeAnything()
    {
        var s = new Setup();
        Assert.True(s.Executor.Preview(s.Use, "sentinel", s.Lance, Setup.PrototypeLimit).CanExecute);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void ReactionLimitRequiresExplicitPrototypeOptIn()
    {
        var s = new Setup();
        var result = s.Executor.Execute(s.Use, "sentinel", s.Lance, null);
        Assert.Equal(ImpalingSentinelReason.ReactionLimitUnresolved, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void ValidOrdinaryEntryUsesNormalCombatAndTheProvisionalSingleReactionLimit()
    {
        var s = new Setup();
        Assert.True(s.Prepare().Succeeded);
        var movement = s.Entry();
        var trigger = s.Evaluator.Evaluate(s.Owner.Id, movement, s.Lance, s.Map);
        Assert.True(trigger.IsEligible);
        var result = s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map);
        Assert.True(result.Resolved);
        Assert.True(result.Strike!.Hit);
        Assert.False(result.MovementMayContinue);
        Assert.True(result.Combat!.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(46, s.Enemy.CurrentHealth); // max(1, 15+4-11) - 2 triangle disadvantage.
        Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Fact]
    public void RegisteredMetadataPreservesMainActionReuseAndEquipmentDependentRange()
    {
        var definition = GraveboundColossus.Definition.Abilities.Single(a => a.Id == "impaling_sentinel");
        Assert.Equal("Impaling Sentinel", definition.DisplayName);
        Assert.Equal(AbilityClassification.Active, definition.Classification);
        Assert.Equal(AbilityActionType.MainAction, definition.RequiredAction);
        Assert.Equal(2, definition.ReuseInterval);
        Assert.Equal(AbilityRangeKind.EquippedWeapon, definition.Range!.Kind);
        Assert.Null(definition.Range.Minimum);
        Assert.Null(definition.IsSpellTagged);
        Assert.Contains("provisional", definition.Description);
        foreach (var id in new[] { "hooded_assassin", "gentle_guardian", "time_weaver", "nigel", "beothuk_spirit_guardian" })
            Assert.DoesNotContain(CharacterFactory.Create(id, 1)!.Definition!.Abilities, a => a.Id == definition.Id);
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    public void PreparationRespectsSharedEligibilityBeforeAnyStateChange(string condition, AbilityRejectionReason reason)
    {
        var s = new Setup();
        switch (condition)
        {
            case "dead": s.Owner.TakeDamage(int.MaxValue); break;
            case "inactive": s.Manager.CompleteMainAction(); break;
            case "cooldown": s.Owner.Cooldowns.RecordUse(GraveboundColossus.ImpalingSentinelId, 1, 2); break;
            case "silence": s.Owner.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
        }
        var count = s.Owner.Cooldowns.Cooldowns.Count;
        Assert.Equal(reason, s.Prepare().Eligibility.RejectionReason);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Equal(count, s.Owner.Cooldowns.Cooldowns.Count);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(52, s.Enemy.CurrentHealth);
    }

    [Fact]
    public void WrongOwnerAndUnrelatedAbilitiesCannotPrepareSentinel()
    {
        var outsider = new Setup(ownerDefinition: Nigel.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, outsider.Prepare().Eligibility.RejectionReason);
        var forged = new CharacterDefinition("other_character", "Other", "fixture", GraveboundColossus.Definition.BaseStats,
            GraveboundColossus.Definition.Abilities);
        var s = new Setup(ownerDefinition: forged);
        Assert.Equal(ImpalingSentinelReason.NotColossus, s.Prepare().RejectionReason);
        var proper = new Setup();
        var use = new AbilityUse(proper.Owner, GraveboundColossus.WailingRampartId, proper.Manager);
        Assert.Equal(AbilityRejectionReason.UnsupportedAbility,
            proper.Executor.Execute(use, "sentinel", proper.Lance, Setup.PrototypeLimit).Eligibility.RejectionReason);
        Assert.Empty(proper.Tracker.Preparations);
    }

    [Theory]
    [InlineData("missing", ImpalingSentinelReason.LanceRequired)]
    [InlineData("axe", ImpalingSentinelReason.LanceRequired)]
    [InlineData("magic", ImpalingSentinelReason.LanceRequired)]
    [InlineData("unarmed", ImpalingSentinelReason.LanceRequired)]
    [InlineData("disarm", ImpalingSentinelReason.WeaponUnavailable)]
    [InlineData("range2", ImpalingSentinelReason.RangeGeometryUnresolved)]
    public void PreparationDoesNotInventAnAvailableLanceOrUnresolvedRangeGeometry(string kind, ImpalingSentinelReason reason)
    {
        var s = new Setup();
        AttackConfiguration? attack = kind switch
        {
            "missing" => null,
            "axe" => new(AttackType.Physical, WeaponCategory.Axe, 4, 90, 1, 1),
            "magic" => new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 90, 1, 2),
            "unarmed" => AttackConfiguration.CreateUnarmed(0, 90),
            "range2" => new(AttackType.Physical, WeaponCategory.Lance, 4, 90, 2, 2),
            _ => s.Lance
        };
        if (kind == "disarm") s.Owner.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(reason, s.Executor.Execute(s.Use, "sentinel", attack, Setup.PrototypeLimit).RejectionReason);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void DuplicatePreparationOrRefreshIsRejectedWithoutCosts()
    {
        var s = new Setup();
        var existing = new ImpalingSentinelPreparation("existing", s.Owner.Id, 1, 2, s.Lance, Setup.PrototypeLimit);
        s.Tracker.Register(existing); // Storage fixture only; ordinary casts must use the executor.
        Assert.Equal(ImpalingSentinelReason.AlreadyPrepared, s.Prepare().RejectionReason);
        Assert.Equal(ImpalingSentinelReason.DuplicatePreparationId,
            s.Executor.Preview(s.Use, "existing", s.Lance, Setup.PrototypeLimit).RejectionReason);
        Assert.Throws<InvalidOperationException>(() => s.Tracker.Register(existing));
        Assert.Same(existing, Assert.Single(s.Tracker.Preparations));
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void PreparationExecuteRevalidatesDisarmAndCannotReplayItsSuccessfulUse()
    {
        var s = new Setup();
        Assert.True(s.Executor.Preview(s.Use, "sentinel", s.Lance, Setup.PrototypeLimit).CanExecute);
        s.Owner.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(ImpalingSentinelReason.WeaponUnavailable, s.Prepare().RejectionReason);
        Assert.Empty(s.Tracker.Preparations);
        s.Owner.Statuses.RemoveStatus(StatusType.Disarm);
        Assert.True(s.Prepare().Succeeded);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Prepare().Eligibility.RejectionReason);
        Assert.Single(s.Owner.Cooldowns.Cooldowns);
        Assert.Single(s.Tracker.Preparations);
    }

    [Fact]
    public void TrackerCannotUseAnUnrelatedLifecycleOrAbilityUse()
    {
        var a = new Setup();
        var b = new Setup();
        Assert.Throws<ArgumentException>(() => new ImpalingSentinelTracker(a.Manager, b.Lifecycle));
        Assert.Equal(ImpalingSentinelReason.ContextMismatch,
            a.Executor.Execute(b.Use, "sentinel", b.Lance, Setup.PrototypeLimit).RejectionReason);
        Assert.Empty(a.Tracker.Preparations);
        Assert.Empty(b.Tracker.Preparations);
    }

    [Theory]
    [InlineData("team", ImpalingSentinelReason.TeamMismatch)]
    [InlineData("round", ImpalingSentinelReason.WrongRound)]
    [InlineData("owner", ImpalingSentinelReason.ContextMismatch)]
    [InlineData("policy", ImpalingSentinelReason.ReactionLimitUnresolved)]
    public void MalformedStorageContextIsRejected(string invalid, ImpalingSentinelReason reason)
    {
        var s = new Setup();
        var preparation = new ImpalingSentinelPreparation("sentinel", invalid == "owner" ? "unknown" : s.Owner.Id,
            invalid == "team" ? 2 : 1, invalid == "round" ? 1 : 2, s.Lance, invalid == "policy" ? null : Setup.PrototypeLimit);
        Assert.Equal(reason, s.Tracker.ValidatePreparation(preparation));
        Assert.Throws<InvalidOperationException>(() => s.Tracker.Register(preparation));
        Assert.Empty(s.Tracker.Preparations);
    }

    [Fact]
    public void SnapshotAndEventConstructorsRejectInvalidIdentitiesRoundsAndEnums()
    {
        var s = new Setup();
        foreach (var id in new[] { null, "", " " })
        {
            Assert.ThrowsAny<ArgumentException>(() => new ImpalingSentinelPreparation(id!, "owner", 1, 2, s.Lance, Setup.PrototypeLimit));
            Assert.ThrowsAny<ArgumentException>(() => new ImpalingSentinelPreparation("id", id!, 1, 2, s.Lance, Setup.PrototypeLimit));
            Assert.ThrowsAny<ArgumentException>(() => new SentinelMovementEvent(id!, "enemy", 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, true));
            Assert.ThrowsAny<ArgumentException>(() => new SentinelMovementEvent("event", id!, 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, true));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImpalingSentinelPreparation("id", "owner", 1, 0, s.Lance, Setup.PrototypeLimit));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SentinelMovementEvent("event", "enemy", 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SentinelMovementEvent("event", "enemy", 2, new(3, 5), new(4, 5), (SentinelMovementKind)999, 2, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImpalingSentinelPreparation("id", "owner", 1, 2, s.Lance, (SentinelReactionLimit)999));
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelPreparation("id", "owner", 1, 2, null!, Setup.PrototypeLimit));
        Assert.Throws<OverflowException>(() => new Setup(int.MaxValue).Prepare());
    }

    [Theory]
    [InlineData(SentinelMovementKind.Teleport)]
    [InlineData(SentinelMovementKind.Forced)]
    public void SpecialMovementDoesNotSilentlyTriggerSentinel(SentinelMovementKind kind)
    {
        var s = new Setup();
        s.Prepare();
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(kind: kind), s.Lance, s.Request(), s.Map);
        Assert.Equal(ImpalingSentinelReason.SpecialMovementUnresolved, result.RejectionReason);
        Assert.False(result.Resolved);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Theory]
    [InlineData("ally", ImpalingSentinelReason.AlliedMovement)]
    [InlineData("stationary", ImpalingSentinelReason.Stationary)]
    [InlineData("unknown", ImpalingSentinelReason.MoverNotInRoster)]
    [InlineData("team", ImpalingSentinelReason.TeamMismatch)]
    [InlineData("round", ImpalingSentinelReason.WrongRound)]
    [InlineData("proposed", ImpalingSentinelReason.MovementNotCommitted)]
    [InlineData("stale", ImpalingSentinelReason.StalePosition)]
    [InlineData("path", ImpalingSentinelReason.MovementPathRequired)]
    [InlineData("outside", ImpalingSentinelReason.NotEnteringRange)]
    [InlineData("leaving", ImpalingSentinelReason.NotEnteringRange)]
    [InlineData("inside", ImpalingSentinelReason.NotEnteringRange)]
    public void OnlyCurrentEnemyRangeEntrySegmentsQualify(string kind, ImpalingSentinelReason reason)
    {
        var s = new Setup();
        if (kind == "inside") s.Lance = new(AttackType.Physical, WeaponCategory.Lance, 4, 90, 1, 2);
        s.Prepare();
        var from = kind switch { "path" => new GridPosition(1, 5), "outside" => new(2, 5), "leaving" => new(4, 5), _ => new(3, 5) };
        var to = kind switch { "stationary" => from, "outside" or "leaving" => new(3, 5), _ => new(4, 5) };
        s.Enemy.SetTacticalPosition(kind == "stale" ? new(13, 13) : kind == "proposed" ? from : to);
        var movement = new SentinelMovementEvent("event", kind switch { "ally" => s.Ally.Id, "unknown" => "unknown", _ => s.Enemy.Id },
            kind == "ally" ? 1 : kind == "team" ? 99 : 2, from, to, SentinelMovementKind.Ordinary, kind == "round" ? 1 : 2, kind != "proposed");
        Assert.Equal(reason, s.Evaluator.Evaluate(s.Owner.Id, movement, s.Lance, s.Map).RejectionReason);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ExtendedLanceUsesInclusiveMaximumRangeWithoutInferringAPath()
    {
        var s = new Setup();
        s.Lance = new(AttackType.Physical, WeaponCategory.Lance, 4, 90, 1, 2);
        s.Prepare();
        s.Enemy.SetTacticalPosition(new(3, 5));
        var movement = new SentinelMovementEvent("step", s.Enemy.Id, 2, new(2, 5), new(3, 5), SentinelMovementKind.Ordinary, 2, true);
        var result = s.Evaluator.Evaluate(s.Owner.Id, movement, s.Lance, s.Map);
        Assert.True(result.IsEligible);
        Assert.Equal(3, result.DistanceBefore);
        Assert.Equal(2, result.DistanceAfter);
        Assert.Equal(0, s.Random.Calls);
    }

    [Theory]
    [InlineData("missing", ImpalingSentinelReason.MissingMapInformation)]
    [InlineData("unknown", ImpalingSentinelReason.MissingMapInformation)]
    [InlineData("blocked", ImpalingSentinelReason.AttackBlocked)]
    [InlineData("invalid", ImpalingSentinelReason.InvalidPosition)]
    [InlineData("unknownPosition", ImpalingSentinelReason.MissingMapInformation)]
    public void MapAndVisibilityMustBeResolvedBeforeAnyReaction(string condition, ImpalingSentinelReason reason)
    {
        var s = new Setup();
        s.Prepare();
        var movement = s.Entry();
        if (condition == "unknown") s.Map.AttackLegal = null;
        if (condition == "blocked") s.Map.AttackLegal = false;
        if (condition == "invalid") s.Map.Valid = p => p != movement.Destination;
        if (condition == "unknownPosition") s.Map.Valid = _ => null;
        var result = s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), condition == "missing" ? null : s.Map);
        Assert.Equal(reason, result.RejectionReason);
        Assert.False(result.Resolved);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Empty(s.Tracker.MovementEvents);
    }

    [Fact]
    public void EventPublicationRejectsDuplicateIdsWithoutResolvingOrMovingAnything()
    {
        var s = new Setup();
        s.Prepare();
        var before = s.Enemy.TacticalPosition;
        var movement = new SentinelMovementEvent("entry", s.Enemy.Id, 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, true);
        s.Tracker.RegisterMovementEvent(movement);
        Assert.Equal(before, s.Enemy.TacticalPosition);
        Assert.Throws<ArgumentException>(() => s.Tracker.RegisterMovementEvent(movement));
        s.Enemy.SetTacticalPosition(movement.Destination);
        var conflicting = new SentinelMovementEvent("entry", s.Enemy.Id, 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, true);
        Assert.Equal(ImpalingSentinelReason.EventIdentityConflict, s.Evaluator.Evaluate(s.Owner.Id, conflicting, s.Lance, s.Map).RejectionReason);
        Assert.True(s.Evaluator.Evaluate(s.Owner.Id, movement, s.Lance, s.Map).IsEligible);
        Assert.Equal(0, s.Random.Calls);
        Assert.Throws<NotSupportedException>(() => ((IList<SentinelMovementEvent>)s.Tracker.MovementEvents).Clear());
    }

    [Fact]
    public void ReactionPreviewIsPureAndTheSameEventCannotResolveTwice()
    {
        var s = new Setup();
        var preparation = s.Prepare().Preparation!;
        var snapshot = s.Tracker.Preparations;
        var movement = s.Entry();
        for (int i = 0; i < 3; i++) Assert.True(s.Reactions.Preview(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).CanExecute);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Empty(s.Tracker.MovementEvents);
        Assert.Equal(0, preparation.ReactionCount);
        Assert.True(s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).Resolved);
        Assert.Equal(2, s.Random.Calls); // Opening and normal counter, independently rolled.
        Assert.Equal(ImpalingSentinelReason.DuplicateReactionEvent,
            s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        Assert.Equal(ImpalingSentinelReason.ReactionLimitReached,
            s.Reactions.Execute(s.Owner.Id, s.Entry("second_entry"), s.Lance, s.Request(), s.Map).RejectionReason);
        Assert.Equal(2, s.Random.Calls);
        Assert.Equal(0, snapshot[0].ReactionCount);
        Assert.Equal(SentinelPreparationStatus.Prepared, snapshot[0].Status);
        Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Fact]
    public void HitUsesExistingDamageTriangleAndShieldsWithoutSpendingTheMovingCharactersActions()
    {
        var s = new Setup();
        s.Prepare();
        s.Manager.StartActivation(s.Enemy);
        s.Manager.RecordUtilityAction();
        s.Enemy.SetShieldHP(4);
        var stats = s.Enemy.BaseStats;
        var movement = s.Entry();
        var result = s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map);
        Assert.True(result.Resolved);
        Assert.Equal(6, result.Strike!.Damage);
        Assert.Equal(WeaponTriangleOutcome.Disadvantage, result.Strike.TriangleOutcome);
        Assert.Equal(4, result.Combat!.OpeningShieldAbsorbed);
        Assert.Equal(2, result.Combat.OpeningHealthLost);
        Assert.Equal(0, s.Enemy.ShieldHP);
        Assert.Equal(50, s.Enemy.CurrentHealth);
        Assert.False(result.MovementMayContinue);
        Assert.Same(s.Enemy, s.Manager.ActiveCharacter);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.Empty(s.Enemy.Cooldowns.Cooldowns);
        Assert.Same(stats, s.Enemy.BaseStats);
        Assert.Equal(movement.Destination, s.Enemy.TacticalPosition);
        Assert.Equal(0, s.Enemy.HealsReceivedThisRound);
    }

    [Fact]
    public void MissConsumesTheExplicitPrototypeReactionButPermitsFurtherMovement()
    {
        var s = new Setup();
        s.Prepare();
        s.Random.Values.Enqueue(99);
        s.Random.Values.Enqueue(0);
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map);
        Assert.True(result.Resolved);
        Assert.False(result.Strike!.Hit);
        Assert.Equal(0, result.Strike.Damage);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.True(result.MovementMayContinue);
        Assert.False(result.PreparationRemainsActive);
        Assert.Equal(1, result.PreparationAfter!.ReactionCount);
        Assert.True(result.Combat!.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void OutOfRangeCounterDoesNotRollAndSentinelNeverAddsAFollowUp()
    {
        var s = new Setup();
        s.Prepare();
        var request = s.Request() with { CounterattackMinimumRange = 2, CounterattackMaximumRange = 3, DefenderWeapon = WeaponCategory.Bow };
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, request, s.Map);
        Assert.True(result.Resolved);
        Assert.False(result.Combat!.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void EffectiveDefenseAndExistingStatusInteractionsAreUsedByNormalCombat()
    {
        var s = new Setup();
        s.Prepare();
        s.Enemy.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map);
        Assert.Equal(9, result.Strike!.Damage); // Base DEF 11 becomes effective DEF 8; triangle remains -2.
        Assert.Equal(11, s.Enemy.BaseStats.Defense);
        Assert.Equal(2, s.Enemy.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void DefeatedTargetCannotTriggerAndALethalReactionDoesNotCounter()
    {
        var s = new Setup();
        s.Prepare();
        var movement = s.Entry();
        s.Enemy.TakeDamage(int.MaxValue);
        Assert.Equal(ImpalingSentinelReason.TargetDefeated, s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        Assert.Equal(0, s.Random.Calls);
        var lethal = new Setup();
        lethal.Prepare();
        lethal.Enemy.TakeDamage(50);
        var result = lethal.Reactions.Execute(lethal.Owner.Id, lethal.Entry(), lethal.Lance, lethal.Request(), lethal.Map);
        Assert.True(result.Combat!.DefenderDefeated);
        Assert.Equal(2, result.Combat.OpeningHealthLost);
        Assert.False(result.Combat.CounterattackOccurred);
        Assert.Equal(1, lethal.Random.Calls);
    }

    [Fact]
    public void OwnerDefeatInvalidatesPreparationWithoutClearingCooldownOrOtherOwners()
    {
        var s = new Setup();
        var before = s.Prepare().Preparation!;
        var other = new ImpalingSentinelPreparation("other", s.Ally.Id, 1, 2, s.Lance, Setup.PrototypeLimit);
        s.Tracker.Register(other);
        s.Owner.TakeDamage(int.MaxValue);
        Assert.Equal(SentinelPreparationStatus.OwnerDefeated, s.Tracker.GetPreparation(s.Owner.Id)!.Status);
        Assert.Equal(SentinelPreparationStatus.Prepared, s.Tracker.GetPreparation(s.Ally.Id)!.Status);
        Assert.Equal(ImpalingSentinelReason.OwnerDefeated, s.Evaluator.Evaluate(s.Owner.Id, s.Entry(), s.Lance, s.Map).RejectionReason);
        Assert.Equal(4, s.Owner.Cooldowns.Cooldowns[GraveboundColossus.ImpalingSentinelId].ReadyRound);
        Assert.Equal(SentinelPreparationStatus.OwnerDefeated, Assert.Single(s.Tracker.RetireInactivePreparations()).Status);
        Assert.Empty(s.Tracker.RetireInactivePreparations());
        Assert.Equal(SentinelPreparationStatus.Prepared, before.Status);
    }

    [Fact]
    public void PreparationExpiresAtThisRoundsCleanupAndCannotAffectTheNextRound()
    {
        var s = new Setup(round: 3);
        var before = s.Prepare().Preparation!;
        Assert.Equal(3, before.ExpirationRound);
        Assert.Empty(s.Tracker.RetireInactivePreparations());
        s.Lifecycle.AdvancePhase();
        Assert.Equal(RoundPhase.Cleanup, s.Lifecycle.CurrentPhase);
        Assert.Equal(SentinelPreparationStatus.Expired, s.Tracker.GetPreparation(s.Owner.Id)!.Status);
        Assert.Equal(ImpalingSentinelReason.PreparationExpired, s.Evaluator.Evaluate(s.Owner.Id, s.Entry(), s.Lance, s.Map).RejectionReason);
        Assert.Single(s.Tracker.RetireInactivePreparations());
        Assert.Empty(s.Tracker.RetireInactivePreparations());
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(4, s.Lifecycle.CurrentRound);
        Assert.Equal(SentinelPreparationStatus.Expired, s.Tracker.GetPreparation(s.Owner.Id)!.Status);
        Assert.Equal(SentinelPreparationStatus.Prepared, before.Status);
        Assert.Equal(5, s.Owner.Cooldowns.Cooldowns[GraveboundColossus.ImpalingSentinelId].ReadyRound);
    }

    [Fact]
    public void MissingCombatDataAndContextMismatchDoNotSpendTheReaction()
    {
        var s = new Setup();
        s.Prepare();
        var movement = s.Entry();
        Assert.Equal(ImpalingSentinelReason.CombatInformationRequired,
            s.Reactions.Execute(s.Owner.Id, movement, s.Lance, null, s.Map).RejectionReason);
        foreach (var request in new[] { s.Request() with { OpeningPower = 5 }, s.Request() with { AttackerWeapon = WeaponCategory.Sword },
                     s.Request() with { Defender = s.Ally }, s.Request() with { Distance = 2 }, s.Request() with { IncomingAttackPermitsCounters = false } })
            Assert.Equal(ImpalingSentinelReason.ContextMismatch, s.Reactions.Execute(s.Owner.Id, movement, s.Lance, request, s.Map).RejectionReason);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ArithmeticErrorsAreValidatedBeforeReactionConsumption()
    {
        var s = new Setup();
        s.Lance = new(AttackType.Physical, WeaponCategory.Lance, int.MaxValue, 90, 1, 1);
        s.Prepare();
        Assert.Throws<OverflowException>(() => s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map));
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Empty(s.Tracker.MovementEvents);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(52, s.Enemy.CurrentHealth);
    }

    [Fact]
    public void ReactionRechecksCurrentPositionsAndStatusesAfterPreview()
    {
        var s = new Setup();
        s.Prepare();
        var movement = s.Entry();
        Assert.True(s.Reactions.Preview(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).CanExecute);
        s.Enemy.SetTacticalPosition(new(13, 13));
        Assert.Equal(ImpalingSentinelReason.StalePosition, s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        s.Enemy.SetTacticalPosition(movement.Destination);
        s.Owner.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(ImpalingSentinelReason.WeaponUnavailable, s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        s.Owner.Statuses.RemoveStatus(StatusType.Disarm);
        s.Owner.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        Assert.Equal(ImpalingSentinelReason.SpellRestrictionUnresolved, s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Fact]
    public void CompetingColossiAreNotArbitrarilyOrdered()
    {
        var s = new Setup();
        s.Prepare();
        s.Ally.SetTacticalPosition(new(4, 4));
        s.Tracker.Register(new("other", s.Ally.Id, 1, 2, s.Lance, Setup.PrototypeLimit));
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map);
        Assert.Equal(ImpalingSentinelReason.MultipleReactionsOrderingUnresolved, result.RejectionReason);
        Assert.All(s.Tracker.Preparations, p => Assert.Equal(0, p.ReactionCount));
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void PublicSnapshotsExposeNoSettersOrMutableCollections()
    {
        var s = new Setup();
        s.Prepare();
        Assert.Throws<NotSupportedException>(() => ((IList<ImpalingSentinelPreparation>)s.Tracker.Preparations).Clear());
        foreach (var type in new[] { typeof(ImpalingSentinelPreparation), typeof(SentinelMovementEvent), typeof(ImpalingSentinelUseResult),
                     typeof(ImpalingSentinelTriggerResult), typeof(ImpalingSentinelReactionResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void MissingPreparationPositionIsRejectedWithoutAnActionOrCooldown()
    {
        var s = new Setup(placeOwner: false);
        Assert.Equal(ImpalingSentinelReason.MissingPosition, s.Prepare().RejectionReason);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void ChangedLanceConfigurationCannotBeSubstitutedAfterPreparation()
    {
        var s = new Setup();
        s.Prepare();
        var movement = s.Entry();
        var changed = new AttackConfiguration(AttackType.Physical, WeaponCategory.Lance, 99, 100, 1, 1);
        Assert.Equal(ImpalingSentinelReason.ContextMismatch, s.Evaluator.Evaluate(s.Owner.Id, movement, changed, s.Map).RejectionReason);
        Assert.Equal(ImpalingSentinelReason.LanceRequired, s.Evaluator.Evaluate(s.Owner.Id, movement, null, s.Map).RejectionReason);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitFixtureSpellMetadataUsesExistingSilenceRules(bool isSpell)
    {
        var canonical = GraveboundColossus.Definition;
        var ability = canonical.Abilities.Single(a => a.Id == GraveboundColossus.ImpalingSentinelId);
        var specified = new AbilityDefinition(ability.Id, ability.DisplayName, "Explicit test classification, not canonical metadata.",
            ability.Classification, isSpell, ability.RequiredAction, ability.Range, ability.ReuseInterval);
        var template = new CharacterDefinition(canonical.Id, canonical.Name, canonical.Description, canonical.BaseStats, new[] { specified });
        var s = new Setup(ownerDefinition: template);
        s.Prepare();
        s.Owner.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        var trigger = s.Evaluator.Evaluate(s.Owner.Id, s.Entry(), s.Lance, s.Map);
        Assert.Equal(isSpell ? ImpalingSentinelReason.BlockedBySilence : ImpalingSentinelReason.None, trigger.RejectionReason);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ExplicitCounterSuppressionStillUsesExistingCombatEligibility()
    {
        var s = new Setup();
        s.Prepare();
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance,
            s.Request() with { CounterattacksSuppressed = true }, s.Map);
        Assert.True(result.Resolved);
        Assert.False(result.Combat!.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void ACounterMayDefeatTheColossusWithoutReplayingOrCancellingTheReaction()
    {
        var s = new Setup();
        s.Prepare();
        s.Owner.TakeDamage(63);
        var movement = s.Entry();
        var result = s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map);
        Assert.True(result.Resolved);
        Assert.True(result.Combat!.CounterattackOccurred);
        Assert.True(result.Combat.AttackerDefeated);
        Assert.Equal(SentinelPreparationStatus.OwnerDefeated, result.PreparationAfter!.Status);
        Assert.Equal(1, result.PreparationAfter.ReactionCount);
        Assert.Equal(ImpalingSentinelReason.DuplicateReactionEvent,
            s.Reactions.Execute(s.Owner.Id, movement, s.Lance, s.Request(), s.Map).RejectionReason);
        Assert.Equal(2, s.Random.Calls);
        Assert.Single(s.Owner.Cooldowns.Cooldowns);
    }

    [Fact]
    public void DifferentColossiHaveIndependentReactionsWhenTheirEntryRangesDoNotCompete()
    {
        var s = new Setup();
        s.Prepare();
        s.Tracker.Register(new("other", s.Ally.Id, 1, 2, s.Lance, Setup.PrototypeLimit));
        var first = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map);
        Assert.True(first.Resolved);
        Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Equal(0, s.Tracker.GetPreparation(s.Ally.Id)!.ReactionCount);
        s.Enemy.SetTacticalPosition(new(4, 7));
        var otherEvent = new SentinelMovementEvent("other_entry", s.Enemy.Id, 2, new(3, 7), new(4, 7), SentinelMovementKind.Ordinary, 2, true);
        var otherRequest = s.Request() with { Attacker = s.Ally };
        Assert.True(s.Reactions.Execute(s.Ally.Id, otherEvent, s.Lance, otherRequest, s.Map).Resolved);
        Assert.Equal(1, s.Tracker.GetPreparation(s.Ally.Id)!.ReactionCount);
        Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        Assert.Equal(2, s.Tracker.MovementEvents.Count);
    }

    [Fact]
    public void ExhaustedPreparationCannotBeRefreshedOrRearmedInTheSameRound()
    {
        var s = new Setup();
        s.Prepare();
        s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance, s.Request(), s.Map);
        var replacement = new ImpalingSentinelPreparation("replacement", s.Owner.Id, 1, 2, s.Lance, Setup.PrototypeLimit);
        Assert.Equal(ImpalingSentinelReason.AlreadyPrepared, s.Tracker.ValidatePreparation(replacement));
        Assert.Throws<InvalidOperationException>(() => s.Tracker.Register(replacement));
        Assert.Equal(1, Assert.Single(s.Tracker.Preparations).ReactionCount);
    }

    [Fact]
    public void LaterRoundMayRegisterAFreshFinitePreparationWithoutReusingAnOldId()
    {
        var s = new Setup();
        s.Prepare();
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        while (s.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) s.Lifecycle.AdvancePhase();
        var next = new ImpalingSentinelPreparation("next", s.Owner.Id, 1, 3, s.Lance, Setup.PrototypeLimit);
        Assert.Same(next, s.Tracker.Register(next)); // Tests storage only; ability reuse is still blocked until R4.
        Assert.False(s.Owner.Cooldowns.IsReady(GraveboundColossus.ImpalingSentinelId, 3));
        Assert.Equal(ImpalingSentinelReason.DuplicatePreparationId,
            s.Tracker.ValidatePreparation(new("sentinel", s.Owner.Id, 1, 3, s.Lance, Setup.PrototypeLimit)));
        Assert.Equal(3, s.Tracker.GetPreparation(s.Owner.Id)!.ExpirationRound);
    }

    [Fact]
    public void NoPreparationAndInvalidMovementPublicationHaveNoSideEffects()
    {
        var s = new Setup();
        Assert.Equal(ImpalingSentinelReason.NotPrepared, s.Evaluator.Evaluate(s.Owner.Id, s.Entry(), s.Lance, s.Map).RejectionReason);
        Assert.Throws<ArgumentException>(() => s.Tracker.RegisterMovementEvent(new("event", "unknown", 2,
            new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, true)));
        Assert.Throws<ArgumentException>(() => s.Tracker.RegisterMovementEvent(new("event", s.Enemy.Id, 2,
            new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 1, true)));
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Tracker.MovementEvents);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
    }

    [Fact]
    public void NullInputsAreProgrammingErrorsAndDoNotMutateState()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelTracker(null!, s.Lifecycle));
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelTracker(s.Manager, null!));
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelAbilityExecutor(null!));
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelTriggerEvaluator(null!));
        Assert.Throws<ArgumentNullException>(() => new ImpalingSentinelReactionResolver(s.Tracker, null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, "sentinel", s.Lance, Setup.PrototypeLimit));
        Assert.Throws<ArgumentNullException>(() => s.Evaluator.Evaluate(s.Owner.Id, null!, s.Lance, s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Tracker.Register(null!));
        Assert.Throws<ArgumentNullException>(() => s.Tracker.RegisterMovementEvent(null!));
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
    }

    [Fact]
    public void PreparationAndReactionPreserveHealingHistoryShieldsStatusesAndUnrelatedCharacters()
    {
        var s = new Setup();
        s.Owner.SetShieldHP(6);
        s.Enemy.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Enemy);
        s.Prepare();
        Assert.Equal(6, s.Owner.ShieldHP);
        var result = s.Reactions.Execute(s.Owner.Id, s.Entry(), s.Lance,
            s.Request() with { CounterattacksSuppressed = true }, s.Map);
        Assert.True(result.Resolved);
        Assert.Equal(6, s.Owner.ShieldHP);
        Assert.True(s.Enemy.Statuses.IsPoisoned);
        Assert.Equal(1, s.Enemy.HealsReceivedThisRound);
        Assert.Equal(64, s.Ally.CurrentHealth);
        Assert.Equal(new GridPosition(5, 7), s.Ally.TacticalPosition);
        Assert.Empty(s.Ally.Cooldowns.Cooldowns);
        Assert.False(s.Ally.HasActedThisRound);
    }

    [Fact]
    public void ProposedStepCanBeCheckedBeforeExternalMovementCommitsButCannotAttackEarly()
    {
        var s = new Setup();
        s.Prepare();
        var proposal = new SentinelMovementEvent("entry", s.Enemy.Id, 2, new(3, 5), new(4, 5), SentinelMovementKind.Ordinary, 2, false);
        var preview = s.Evaluator.Evaluate(s.Owner.Id, proposal, s.Lance, s.Map);
        Assert.True(preview.MayTriggerAfterCommit);
        Assert.False(preview.IsEligible);
        Assert.Equal(2, preview.DistanceBefore);
        Assert.Equal(1, preview.DistanceAfter);
        Assert.Equal(new GridPosition(3, 5), s.Enemy.TacticalPosition);
        Assert.False(s.Reactions.Execute(s.Owner.Id, proposal, s.Lance, s.Request(), s.Map).Resolved);
        Assert.Throws<ArgumentException>(() => s.Tracker.RegisterMovementEvent(proposal));
        Assert.Empty(s.Tracker.MovementEvents);
        Assert.Equal(0, s.Random.Calls);
        var committed = s.Entry();
        Assert.True(s.Reactions.Execute(s.Owner.Id, committed, s.Lance, s.Request(), s.Map).Resolved);
    }

    [Fact]
    public void SentinelPreparationCannotBypassAnActiveProvokingRoar()
    {
        var s = new Setup();
        s.Manager.CompleteActivation();
        s.Owner.ResetActivation(); // Arrange Nigel's turn before the Colossus has acted.
        s.Manager.StartActivation(s.Enemy);
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character,
            requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);
        var roar = new ProvokingRoarAbilityExecutor();
        Assert.True(roar.Execute(new(s.Enemy, Nigel.ProvokingRoarId, s.Manager), constraints).Succeeded);
        s.Manager.StartActivation(s.Owner);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, s.Prepare().Eligibility.RejectionReason);
        Assert.Empty(s.Tracker.Preparations);
        Assert.Empty(s.Owner.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.Owner.Id));
    }

    private sealed class Map : ISentinelReactionMap
    {
        public Func<GridPosition, bool?> Valid { get; set; } = _ => true;
        public bool? AttackLegal { get; set; } = true;
        public bool? IsValidPosition(GridPosition position) => Valid(position);
        public bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement) => AttackLegal;
    }

    private sealed class Setup
    {
        // Explicit test Power/accuracy and provisional limit, not finalized Lance balance.
        public const SentinelReactionLimit PrototypeLimit = SentinelReactionLimit.ProvisionalOnePerPreparation;
        public Character Owner { get; }
        public Character Ally { get; } = CharacterFactory.Create(GraveboundColossus.Definition, "ally", 1);
        public Character Enemy { get; } = CharacterFactory.Create(Nigel.Definition, "enemy", 2);
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Manager { get; }
        public AbilityUse Use { get; }
        public AttackConfiguration Lance { get; set; } = new(AttackType.Physical, WeaponCategory.Lance, 4, 90, 1, 1);
        public ImpalingSentinelTracker Tracker { get; }
        public ImpalingSentinelAbilityExecutor Executor { get; }
        public ImpalingSentinelTriggerEvaluator Evaluator { get; }
        public ImpalingSentinelReactionResolver Reactions { get; }
        public Map Map { get; } = new();
        public Rolls Random { get; } = new();

        public Setup(int round = 2, CharacterDefinition? ownerDefinition = null, bool placeOwner = true)
        {
            Owner = CharacterFactory.Create(ownerDefinition ?? GraveboundColossus.Definition, "owner", 1);
            if (placeOwner) Owner.SetTacticalPosition(new(5, 5));
            Ally.SetTacticalPosition(new(5, 7));
            Enemy.SetTacticalPosition(new(3, 5));
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Owner, Ally }, new[] { Enemy }, 1, Lifecycle);
            Manager.StartActivation(Owner);
            Use = new(Owner, GraveboundColossus.ImpalingSentinelId, Manager);
            Tracker = new(Manager, Lifecycle);
            Executor = new(Tracker);
            Evaluator = new(Tracker);
            Reactions = new(Tracker, new(new(new HitRoller(Random))));
        }

        public ImpalingSentinelUseResult Prepare() => Executor.Execute(Use, "sentinel", Lance, PrototypeLimit);
        public CombatExchangeRequest Request() => new(Owner, Enemy, AttackType.Physical, Lance.Power, WeaponCategory.Lance,
            WeaponCategory.Axe, Lance.BaseAccuracy, AttackType.Physical, 4, 100, 1, 1, 1, true, false,
            IncomingAttackPermitsFollowUps: true); // Sentinel must override this ordinary opt-in.
        public SentinelMovementEvent Entry(string id = "entry", SentinelMovementKind kind = SentinelMovementKind.Ordinary)
        {
            var start = new GridPosition(3, 5);
            var destination = new GridPosition(4, 5);
            Enemy.SetTacticalPosition(destination); // Simulates the external movement coordinator's committed step.
            return new(id, Enemy.Id, Enemy.TeamId, start, destination, kind, Lifecycle.CurrentRound, true);
        }
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public Queue<int> Values { get; } = new();
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return Values.Count == 0 ? 0 : Values.Dequeue(); }
    }
}
