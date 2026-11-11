using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Scenarios;

public class CombatScenarioTests
{
    [Fact]
    public void PhysicalDuelUsesRealTargetingShieldsCounterAndFollowUp()
    {
        var scenario = Duel().WithShield("tank", 4).Accuracy(0, 0, 0).Build();
        var result = scenario.Runner.Execute(new("duel", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(4, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(5, result.Production.Combat.CounterStrike!.Damage);
        Assert.Equal(3, result.Production.Combat.FollowUpStrike!.Damage);
        Assert.Equal(61, result.After.Characters["tank"].CurrentHealth);
        Assert.Equal(47, result.After.Characters["nigel"].CurrentHealth);
        Assert.Equal(4, result.Before.Characters["tank"].ShieldHP);
        Assert.Equal(0, result.After.Characters["tank"].ShieldHP);
        Assert.Equal(3, result.RandomDraws.Count);
        Assert.Equal(8, scenario.Metrics.HPDamage);
        Assert.Equal(4, scenario.Metrics.ShieldDamage);
        Assert.Equal(3, scenario.Metrics.Hits);
        Assert.Equal(1, scenario.Metrics.CompletedAttacks);
    }

    [Fact]
    public void WinterHunterUsesActualCurrentFrostbite()
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("archer", "beothuk_spirit_guardian", 1, new(5, 5))
            .AddCharacter("enemy", "time_weaver", 2, new(8, 5))
            .WithAttack("archer", "fixture_bow", new(AttackType.Physical, WeaponCategory.Bow, 4, 100, 2, 3))
            .WithAttack("enemy", "fixture_scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2))
            .WithStatus("enemy", StatusType.Frostbite, 2).ActiveCharacter("archer").Accuracy(0).Build();
        var result = scenario.Runner.Execute(new("shot", ScenarioActionKind.Attack, "archer", "enemy"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(15, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(3, result.Production.Combat.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.Equal(23, result.After.Characters["enemy"].CurrentHealth);
    }

    [Fact]
    public void MissingSanctuaryOverlapRuleIsAnUnresolvedResultBeforeAnyRoll()
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("nigel", "nigel", 1, new(6, 5))
            .AddCharacter("guardian", "gentle_guardian", 2, new(7, 5))
            .AddCharacter("a", "gravebound_colossus", 2, new(5, 5))
            .AddCharacter("b", "gravebound_colossus", 2, new(6, 6))
            .WithAttack("nigel", "fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
            .WithAttack("guardian", "light", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2))
            .ActiveCharacter("nigel").Accuracy(0, 0).Build();
        var result = scenario.Runner.Execute(new("overlap", ScenarioActionKind.Attack, "nigel", "guardian"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Contains("OverlapUnresolved", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void SanctuaryBlessingAndShieldsComposeThroughRealHealingAndCombatExecutors()
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true).AtRound(2)
            .AddCharacter("colossus", "gravebound_colossus", 1, new(5, 5))
            .AddCharacter("guardian", "gentle_guardian", 1, new(7, 5))
            .AddCharacter("healer", "gentle_guardian", 1, new(7, 6))
            .AddCharacter("enemy", "nigel", 2, new(6, 5))
            .WithHealth("guardian", 43).WithShield("guardian", 6)
            .WithAttack("enemy", "axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
            .WithAttack("guardian", "light", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2))
            .ActiveCharacter("healer").Accuracy(0, 0).Build();
        var heal = scenario.Runner.Execute(new("heal", ScenarioActionKind.Ability, "healer", "guardian", "healing_light"));
        Assert.Equal(ScenarioOutcome.Completed, heal.Outcome);
        Assert.Equal(1, heal.Production!.HPHealed);
        Assert.Equal(13, heal.After.Characters["guardian"].EffectiveDefense);
        Assert.Equal(17, heal.After.Characters["guardian"].EffectiveResistance);
        Assert.Empty(heal.RandomDraws);
        Assert.Equal(ScenarioOutcome.Completed, scenario.Runner.Execute(new("start", ScenarioActionKind.StartActivation, "enemy")).Outcome);
        var attack = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "enemy", "guardian"));
        Assert.Equal(ScenarioOutcome.Completed, attack.Outcome);
        Assert.Equal(8, attack.Production!.Combat!.OpeningStrike.Damage); // DEF 11 + Blessing 2 + Sanctuary 3.
        Assert.Equal(6, attack.Production.Combat.OpeningShieldAbsorbed);
        Assert.Equal(2, attack.Production.Combat.OpeningHealthLost);
        Assert.Equal(42, attack.After.Characters["guardian"].CurrentHealth);
        Assert.Equal(11, attack.After.Characters["guardian"].BaseStats.Defense);
        Assert.Equal(9, scenario.Metrics.HPDamage);
        Assert.Equal(6, scenario.Metrics.ShieldDamage);
        Assert.Equal(2, scenario.Lifecycle.CurrentRound);
    }

    [Theory]
    [InlineData(false, false, 12)]
    [InlineData(true, false, 15)]
    [InlineData(true, true, 12)]
    public void WinterHunterDisappearsAfterProductionCleanse(bool frostbite, bool cleanse, int damage)
    {
        var builder = Archer();
        if (frostbite) builder.WithStatus("enemy", StatusType.Frostbite, 2);
        var scenario = builder.Accuracy(0).Build();
        if (cleanse) CleanseProcessor.BasicCleanse(scenario.Characters["enemy"], StatusType.Frostbite);
        var result = scenario.Runner.Execute(new("shot", ScenarioActionKind.Attack, "archer", "enemy"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(damage, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(frostbite && !cleanse ? 3 : 0, result.Production.Combat.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.False(result.After.Characters["archer"].CanAct);
        Assert.True(result.After.MainActionUsed);
    }

    [Theory]
    [InlineData(52, 0, 13)]
    [InlineData(26, 0, 18)]
    [InlineData(26, 26, 13)]
    public void FuryUsesCurrentMissingHealthIncludingHealing(int initialHP, int healing, int expectedDamage)
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("nigel", "nigel", 1, new(5, 5))
            .AddCharacter("enemy", "gentle_guardian", 2, new(8, 5))
            // Explicit ranged fixture input isolates Fury without claiming this is Nigel's starting equipment.
            .WithAttack("nigel", "ranged_axe_fixture", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 3))
            .WithAttack("enemy", "scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2))
            .WithHealth("nigel", initialHP).ActiveCharacter("nigel").Accuracy(0).Build();
        scenario.Characters["nigel"].Heal(healing);
        var result = scenario.Runner.Execute(new("hit", ScenarioActionKind.Attack, "nigel", "enemy"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(expectedDamage, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Single(result.RandomDraws);
        Assert.Single(result.Events, e => e.Type == ScenarioEventType.ActivationEnded);
        Assert.Empty(result.After.Characters["nigel"].Cooldowns);
    }

    [Fact]
    public void RoarRejectsWrongTargetThenAllowsRequiredAttackAndFinalizesOnce()
    {
        var scenario = TauntEncounter();
        var roar = scenario.Runner.Execute(new("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar"));
        Assert.Equal(ScenarioOutcome.Completed, roar.Outcome);
        Assert.Equal(new[] { "enemy" }, roar.Production!.TauntedIds);
        Assert.Equal(4, roar.Production.ReadyRound);
        Assert.Empty(roar.RandomDraws);
        scenario.Runner.Execute(new("select", ScenarioActionKind.StartActivation, "enemy"));
        var wrong = scenario.Runner.Execute(new("wrong", ScenarioActionKind.Attack, "enemy", "other",
            requiredAttack: new("nigel", true, true, true)));
        Assert.Equal(ScenarioOutcome.Rejected, wrong.Outcome);
        Assert.Equal("MustAttackSource", wrong.Reason);
        Assert.Equal(wrong.Before.ToCanonicalJson(), wrong.After.ToCanonicalJson());
        Assert.Empty(wrong.RandomDraws);
        var required = scenario.Runner.Execute(new("required", ScenarioActionKind.Attack, "enemy", "nigel",
            requiredAttack: new("nigel", true, true, true)));
        Assert.Equal(ScenarioOutcome.Completed, required.Outcome);
        Assert.True(required.After.MainActionUsed);
        Assert.Null(required.After.ActiveCharacterId);
        Assert.Single(required.Events, e => e.Type == ScenarioEventType.ActivationEnded);
        Assert.Empty(required.After.Characters["enemy"].Cooldowns);
    }

    [Fact]
    public void ExplicitTauntFallbackEndsActivationWithoutSpendingMainAction()
    {
        var scenario = TauntEncounter();
        scenario.Runner.Execute(new("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar"));
        scenario.Runner.Execute(new("select", ScenarioActionKind.StartActivation, "enemy"));
        var result = scenario.Runner.Execute(new("end", ScenarioActionKind.EndActivation, "enemy",
            requiredAttack: new("nigel", true, true, false)));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.False(result.After.MainActionUsed);
        Assert.True(result.After.Characters["enemy"].HasActed);
        Assert.Empty(result.RandomDraws);
        Assert.Equal(result.Before.Characters["enemy"].Position, result.After.Characters["enemy"].Position);
    }

    [Fact]
    public void MissingTauntLegalityRemainsExplicit()
    {
        var scenario = TauntEncounter();
        scenario.Runner.Execute(new("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar"));
        scenario.Runner.Execute(new("select", ScenarioActionKind.StartActivation, "enemy"));
        var result = scenario.Runner.Execute(new("unknown", ScenarioActionKind.Attack, "enemy", "nigel"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void RejectedAbilityDoesNotChangeAnyGameplayState()
    {
        var scenario = Duel().WithShield("tank", 6).WithStatus("tank", StatusType.Burn, 2).Build();
        var result = scenario.Runner.Execute(new("bad", ScenarioActionKind.Ability, "nigel", "tank", "healing_light"));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome);
        Assert.Equal("AbilityNotOwned", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Empty(result.RandomDraws);
        Assert.Equal(1, scenario.Metrics.RejectedActions);
    }

    [Fact]
    public void OrdinaryMagicalPlayerActionUsesIntelligenceAndLiveResistance()
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("mage", "time_weaver", 1, new(5, 5))
            .AddCharacter("enemy", "nigel", 2, new(7, 5))
            .WithScroll("mage", new("resolved_scroll", "Fixture scroll", power: 4, minimumRange: 1, maximumRange: 2), 100)
            .WithAttack("enemy", "axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
            .ActiveCharacter("mage").Accuracy(0, 0).Build();
        var result = scenario.Runner.Execute(new("spell", ScenarioActionKind.Attack, "mage", "enemy"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(13, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(6, result.Production.Combat.FollowUpStrike!.Damage);
        Assert.False(result.Production.Combat.CounterattackOccurred);
        Assert.Equal(33, result.After.Characters["enemy"].CurrentHealth);
        Assert.Equal("resolved_scroll", result.After.Characters["mage"].EquippedScroll!.Id);
    }

    [Fact]
    public void PreviewIsRepeatableAndDoesNotConsumeRandomnessHistoryOrActions()
    {
        var scenario = Duel().Accuracy(0, 0, 0).Build();
        var action = new ScenarioAction("attack", ScenarioActionKind.Attack, "nigel", "tank");
        var first = scenario.Runner.Preview(action);
        var second = scenario.Runner.Preview(action);
        Assert.Equal(ScenarioOutcome.Ready, first.Outcome);
        Assert.Equal(first.Before.ToCanonicalJson(), second.After.ToCanonicalJson());
        Assert.Empty(scenario.Runner.Draws);
        Assert.Empty(scenario.Runner.History);
        Assert.Empty(scenario.Runner.Results);
        Assert.Equal(ScenarioOutcome.Completed, scenario.Runner.Execute(action).Outcome);
    }

    [Fact]
    public void DeterministicReplayHasIdenticalSnapshotsEventsAndDraws()
    {
        var builder = Duel().Accuracy(0, 99, 0);
        var a = builder.Build(); var b = builder.Build();
        var action = new ScenarioAction("duel", ScenarioActionKind.Attack, "nigel", "tank");
        var first = a.Runner.Execute(action); var second = b.Runner.Execute(action);
        Assert.Equal(first.After.ToCanonicalJson(), second.After.ToCanonicalJson());
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.RandomDraws, second.RandomDraws);
        Assert.NotSame(a.Characters["nigel"], b.Characters["nigel"]);
        Assert.Equal(1, a.Metrics.Misses);
        Assert.Equal(2, a.Metrics.Hits);
    }

    [Fact]
    public void ChangingScriptedRollChangesOutcomeWithoutChangingRules()
    {
        var hit = Duel().Accuracy(0, 0, 0).Build();
        var miss = Duel().Accuracy(0, 99, 0).Build();
        var a = hit.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        var b = miss.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.True(a.Production!.Combat!.CounterStrike!.Hit);
        Assert.False(b.Production!.Combat!.CounterStrike!.Hit);
        Assert.NotEqual(a.After.Characters["nigel"].CurrentHealth, b.After.Characters["nigel"].CurrentHealth);
    }

    [Fact]
    public void ExhaustedRandomnessReportsPartialDamageAndBlocksReplay()
    {
        var scenario = Duel().Accuracy(0).Build();
        var action = new ScenarioAction("partial", ScenarioActionKind.Attack, "nigel", "tank");
        var result = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.PartiallyApplied, result.Outcome);
        Assert.Contains("exhausted", result.Reason);
        Assert.Equal(60, result.After.Characters["tank"].CurrentHealth);
        Assert.Null(result.Production); // The throwing production call returned no completed exchange.
        Assert.Contains(result.Events, e => e.Type == ScenarioEventType.HealthChangeObserved && e.Amount == 4);
        Assert.DoesNotContain(result.Events, e => e.Type == ScenarioEventType.Hit);
        Assert.Equal(ScenarioDrawOutcome.Exhausted, result.RandomDraws.Last().Outcome);
        var replay = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Rejected, replay.Outcome);
        Assert.Equal("ActionAlreadyAttempted", replay.Reason);
        Assert.Empty(replay.RandomDraws);
        Assert.Equal(result.After.ToCanonicalJson(), replay.After.ToCanonicalJson());
        Assert.Equal(4, scenario.Metrics.HPDamage);
        Assert.Equal(1, scenario.Metrics.PartialActions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void InvalidRandomDrawIsAnExplicitFailureWithoutInventingAHit(int roll)
    {
        var scenario = Duel().Accuracy(roll).Build();
        var result = scenario.Runner.Execute(new("invalid_rng", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.UnexpectedFailure, result.Outcome);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Equal(ScenarioDrawOutcome.Invalid, Assert.Single(result.RandomDraws).Outcome);
        Assert.Equal(0, scenario.Metrics.Hits);
    }

    [Fact]
    public void FrostbiteArrowUsesSeparateAccuracyAndProcStreamsThroughProduction()
    {
        var scenario = Archer().Accuracy(0).StatusRolls(39).Build();
        var action = Arrow("arrow", new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, false));
        Assert.Equal(ScenarioOutcome.Ready, scenario.Runner.Preview(action).Outcome);
        Assert.Empty(scenario.Runner.Draws);
        var result = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(new[] { "accuracy", "status" }, result.RandomDraws.Select(d => d.Stream));
        Assert.Equal(12, result.Production!.Combat!.OpeningStrike.Damage); // No retroactive Winter Hunter.
        Assert.Equal(2, result.After.Characters["enemy"].TimedStatuses[StatusType.Frostbite]);
        Assert.Single(result.Production.Statuses);
        Assert.True(result.Production.AbilityFinalized);
        Assert.Equal(1, result.Production.ReadyRound);
    }

    [Fact]
    public void FrostbiteArrowUnresolvedDurationDoesNotRollOrApplyEffects()
    {
        var scenario = Archer().Accuracy(0).StatusRolls(0).Build();
        var result = scenario.Runner.Execute(Arrow("unknown_duration", new(null, false, false)));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal("DurationUnresolved", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void ExhaustedProcStreamDoesNotPretendEarlierArrowDamageWasRolledBack()
    {
        var scenario = Archer().Accuracy(0).Build();
        var result = scenario.Runner.Execute(Arrow("partial_arrow", new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, false)));
        Assert.Equal(ScenarioOutcome.PartiallyApplied, result.Outcome);
        Assert.Equal(26, result.After.Characters["enemy"].CurrentHealth);
        Assert.Empty(result.After.Characters["enemy"].TimedStatuses);
        Assert.False(result.After.MainActionUsed);
        Assert.Empty(result.After.Characters["archer"].Cooldowns);
    }

    [Fact]
    public void SelectionStreamImplementsProductionIndexContractAndLogsBounds()
    {
        var scenario = Duel().Selection(2, 3).Build();
        Assert.Equal(2, scenario.Runner.Selection.NextIndex(3));
        Assert.Throws<InvalidOperationException>(() => scenario.Runner.Selection.NextIndex(3));
        Assert.Throws<InvalidOperationException>(() => scenario.Runner.Selection.NextIndex(3));
        Assert.Equal(new[] { ScenarioDrawOutcome.Valid, ScenarioDrawOutcome.Invalid, ScenarioDrawOutcome.Exhausted }, scenario.Runner.Draws.Select(d => d.Outcome));
        Assert.All(scenario.Runner.Draws, d => Assert.Equal(3, d.ExclusiveMaximum));
    }

    [Fact]
    public void SnapshotsAreDetachedAndReadOnly()
    {
        var scenario = Duel().WithStatus("tank", StatusType.Burn, 2).WithShield("tank", 6).Build();
        var snapshot = scenario.Capture(); var serialized = snapshot.ToCanonicalJson();
        scenario.Characters["tank"].Statuses.AdvanceTimedStatuses();
        scenario.Characters["tank"].TakeDamage(7);
        scenario.Characters["nigel"].Cooldowns.RecordUse("provoking_roar", 1, 3);
        Assert.Equal(serialized, snapshot.ToCanonicalJson());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ScenarioCharacterSnapshot>)snapshot.Characters).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<StatusType, int>)snapshot.Characters["tank"].TimedStatuses).Clear());
        Assert.Empty(snapshot.Characters["nigel"].Cooldowns);
    }

    [Fact]
    public void EventsHaveStableChronologicalOrderAndImmutableExposure()
    {
        var scenario = Duel().WithShield("tank", 4).Accuracy(0, 0, 0).Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(new[] { ScenarioEventType.ActionRequested, ScenarioEventType.AttackStarted, ScenarioEventType.Hit,
            ScenarioEventType.ShieldDamage, ScenarioEventType.Hit, ScenarioEventType.HPDamage, ScenarioEventType.Hit,
            ScenarioEventType.HPDamage, ScenarioEventType.ActivationEnded }, result.Events.Select(e => e.Type));
        Assert.Equal(Enumerable.Range(1, result.Events.Count).Select(i => (long)i), result.Events.Select(e => e.Sequence));
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioEvent>)result.Events).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioRandomDraw>)result.RandomDraws).Clear());
    }

    [Fact]
    public void StalePreviewIsRevalidatedAgainstCurrentPositions()
    {
        var scenario = Duel().Accuracy(0, 0, 0).Build();
        var action = new ScenarioAction("attack", ScenarioActionKind.Attack, "nigel", "tank");
        Assert.Equal(ScenarioOutcome.Ready, scenario.Runner.Preview(action).Outcome);
        scenario.Characters["tank"].SetTacticalPosition(new(9, 5));
        var result = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome);
        Assert.Equal("OutsideMaximumRange", result.Reason);
        Assert.Empty(result.RandomDraws);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.True(scenario.Map.IsOccupied(new(9, 5)));
        Assert.False(scenario.Map.IsOccupied(new(6, 5)));
    }

    [Theory]
    [InlineData(null, ScenarioOutcome.Unresolved)]
    [InlineData(false, ScenarioOutcome.Rejected)]
    public void CallerTargetLegalityIsNotGuessed(bool? legality, ScenarioOutcome expected)
    {
        var scenario = Duel().ConfirmTargeting(legality).Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(expected, result.Outcome);
        Assert.Empty(result.RandomDraws);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void ActionBudgetCannotBeBypassedWithAnotherRequestId()
    {
        var scenario = Duel().Accuracy(0, 0, 0).Build();
        Assert.Equal(ScenarioOutcome.Completed, scenario.Runner.Execute(new("first", ScenarioActionKind.Attack, "nigel", "tank")).Outcome);
        var second = scenario.Runner.Execute(new("second", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Rejected, second.Outcome);
        Assert.Empty(second.RandomDraws);
        Assert.Equal(second.Before.ToCanonicalJson(), second.After.ToCanonicalJson());
    }

    [Fact]
    public void CooldownsAndSilenceRemainProductionEligibilityRules()
    {
        var cooldown = TauntBuilder().AtRound(3).WithCooldown("nigel", "provoking_roar", 2, 3).Build();
        var result = cooldown.Runner.Execute(new("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar"));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome);
        Assert.Equal("OnCooldown", result.Reason);
        Assert.Equal(5, result.After.Characters["nigel"].Cooldowns["provoking_roar"].ReadyRound);
        var silenced = HealingEncounter().WithStatus("healer", StatusType.Silence, 1).Build();
        var heal = silenced.Runner.Execute(new("heal", ScenarioActionKind.Ability, "healer", "ally", "healing_light"));
        Assert.Equal(ScenarioOutcome.Unresolved, heal.Outcome);
        Assert.Equal("MetadataUnresolved", heal.Reason); // Healing Light has no confirmed spell tag under Silence.
        Assert.Equal(heal.Before.ToCanonicalJson(), heal.After.ToCanonicalJson());
    }

    [Fact]
    public void UnsupportedAbilityAdapterIsReportedWithoutPretendingToExecute()
    {
        var scenario = Duel().Build();
        var result = scenario.Runner.Execute(new("knockback", ScenarioActionKind.Ability, "nigel", "tank", "brutal_knockback"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Contains("HarnessUnsupported", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void AllSixRegisteredCharactersUseFreshProductionRuntimeState()
    {
        var ids = new[] { "hooded_assassin", "gentle_guardian", "time_weaver", "nigel", "gravebound_colossus", "beothuk_spirit_guardian" };
        var builder = new CombatScenarioBuilder();
        for (int i = 0; i < ids.Length; i++) builder.AddCharacter(ids[i], ids[i], i % 2 + 1, new(i, 0));
        var first = builder.Build(); var second = builder.Build();
        foreach (var id in ids)
        {
            Assert.NotSame(first.Characters[id], second.Characters[id]);
            Assert.Equal(id, first.Characters[id].Definition!.Id);
            Assert.Equal(first.Characters[id].BaseStats.MaxHealth, first.Characters[id].CurrentHealth);
        }
        first.Characters["nigel"].TakeDamage(5);
        Assert.Equal(52, second.Characters["nigel"].CurrentHealth);
        Assert.NotSame(first.Activations, second.Activations);
    }

    [Fact]
    public void InvalidFixtureStateIsRejectedWithoutTouchingAnEarlierScenario()
    {
        var builder = Duel(); var existing = builder.Build(); var original = existing.Capture().ToCanonicalJson();
        Assert.Throws<ArgumentException>(() => builder.AddCharacter("nigel", "time_weaver", 2, new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithHealth("tank", 65).Build());
        Assert.Equal(original, existing.Capture().ToCanonicalJson());
        Assert.Throws<ArgumentException>(() => Duel().BlockTile(new(5, 5)).Build());
        Assert.Throws<ArgumentException>(() => Duel().WithHealth("tank", 0).WithShield("tank", 1).Build());
        Assert.Throws<ArgumentException>(() => Duel().WithStatus("tank", StatusType.Paralysis, 2));
        Assert.Throws<ArgumentException>(() => Duel().WithStatus("tank", StatusType.Burn));
    }

    [Fact]
    public void SetupHealthAdjustmentPrecedesShieldsAndStatusesDoNotTickAutomatically()
    {
        var scenario = Duel().WithHealth("tank", 30).WithShield("tank", 6)
            .WithStatus("tank", StatusType.Burn, 2).WithStatus("tank", StatusType.Poison).Build();
        Assert.Equal(30, scenario.Characters["tank"].CurrentHealth);
        Assert.Equal(6, scenario.Characters["tank"].ShieldHP);
        var preview = scenario.Runner.Preview(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(2, preview.After.Characters["tank"].TimedStatuses[StatusType.Burn]);
        Assert.Equal(0, preview.After.Characters["tank"].PoisonTicks);
        Assert.Equal(1, preview.After.Round);
    }

    [Fact]
    public void MissingEquipmentInputsDoNotAcquireInventedPower()
    {
        var scenario = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("nigel", "nigel", 1, new(0, 0)).AddCharacter("tank", "gravebound_colossus", 2, new(1, 0))
            .ActiveCharacter("nigel").Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal("ExplicitAttackAndCounterConfigurationRequired", result.Reason);
        Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void DisarmRequiresExplicitUnarmedInputsRatherThanKeepingWeaponBenefits()
    {
        var scenario = Duel().WithStatus("nigel", StatusType.Disarm, 1).Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal("ExplicitUnarmedConfigurationRequired", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void BurnAndCallerSuppliedTemporaryModifierUseExistingEffectiveDefense()
    {
        var scenario = Duel().WithStatus("tank", StatusType.Burn, 2)
            .WithModifier("tank", new("fixture_defense", "nigel", "tank", TemporaryStat.Defense, 2, 1, 2))
            .Accuracy(0, 0, 0).Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(22, result.Before.Characters["tank"].BaseStats.Defense);
        Assert.Equal(21, result.Before.Characters["tank"].EffectiveDefense);
        Assert.Equal(5, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Single(result.After.Characters["tank"].Modifiers);
        Assert.Equal(2, result.After.Characters["tank"].TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void CounterShieldLossIsReportedAsAnObservationWithoutInventingPerStrikeData()
    {
        var scenario = Duel().WithShield("nigel", 3).Accuracy(0, 0, 0).Build();
        var result = scenario.Runner.Execute(new("attack", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.Production!.Combat!.AttackerHealthLost);
        Assert.Contains(result.Events, e => e.TargetId == "nigel" && e.Type == ScenarioEventType.ShieldChangeObserved && e.Amount == 3);
        Assert.Equal(3, scenario.Metrics.ShieldDamage);
        Assert.Equal(9, scenario.Metrics.HPDamage);
    }

    [Fact]
    public void DefeatEventAndMetricsUseActualHPNotOverkillDamage()
    {
        var scenario = Duel().WithHealth("tank", 1).Accuracy(0).Build();
        var result = scenario.Runner.Execute(new("lethal", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(4, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(1, result.Production.Combat.OpeningHealthLost);
        Assert.Equal(1, scenario.Metrics.HPDamage);
        Assert.Single(result.Events, e => e.Type == ScenarioEventType.CharacterDefeated && e.TargetId == "tank");
        Assert.False(result.Production.Combat.CounterattackOccurred);
        Assert.Single(result.RandomDraws);
    }

    [Fact]
    public void MapRequiresExplicitMovementAndTerrainDecisions()
    {
        var scenario = Duel().Build();
        var actor = scenario.Characters["nigel"];
        var from = actor.TacticalPosition!.Value; var destination = new GridPosition(4, 5);
        Assert.Null(scenario.Map.CanDisplace(actor, from, destination));
        scenario.Map.ConfirmDisplacement(actor.Id, from, destination, true);
        Assert.True(scenario.Map.CanDisplace(actor, from, destination));
        Assert.Null(scenario.Map.CanDisplace(actor, from, new(4, 4)));
        Assert.Null(scenario.Map.CanPlaceIce(actor, destination));
        Assert.Null(scenario.Map.CanPlaceWall(actor, destination));
        Assert.Equal(from, actor.TacticalPosition);
        Assert.False(scenario.Map.IsValidPosition(new(-1, 0)));
        Assert.False(scenario.Map.IsValidPosition(new(14, 0)));
        Assert.True(scenario.Map.IsOccupied(new(6, 5)));
    }

    [Fact]
    public void RoundPhaseAndTeamSelectionCannotBeBypassed()
    {
        var scenario = new CombatScenarioBuilder().AtRound(4).AtPhase(RoundPhase.Preparation)
            .AddCharacter("nigel", "nigel", 1, new(0, 0)).AddCharacter("tank", "gravebound_colossus", 2, new(1, 0)).Build();
        var result = scenario.Runner.Execute(new("select", ScenarioActionKind.StartActivation, "nigel"));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome);
        Assert.Equal(4, result.After.Round);
        Assert.Equal(RoundPhase.Preparation, result.After.Phase);
        var duel = Duel().Build();
        var wrong = duel.Runner.Execute(new("wrong_actor", ScenarioActionKind.Attack, "tank", "nigel"));
        Assert.Equal(ScenarioOutcome.Rejected, wrong.Outcome);
        Assert.Empty(wrong.RandomDraws);
    }

    [Fact]
    public void ExplicitUnarmedConfigurationUsesNeutralProductionCombat()
    {
        var scenario = Duel().WithStatus("nigel", StatusType.Disarm, 1)
            .WithAttack("nigel", "explicit_unarmed", new(AttackType.Physical, WeaponCategory.Unarmed, 0, 100, 1, 1))
            .Accuracy(0, 0, 0).Build();
        var result = scenario.Runner.Execute(new("unarmed", ScenarioActionKind.Attack, "nigel", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.Production.Combat.OpeningStrike.TriangleOutcome);
        Assert.Equal(7, result.Production.Combat.CounterStrike!.Damage); // 15 STR + 3 Power - 11 DEF, with a neutral triangle.
    }

    private static CombatScenarioBuilder Archer() => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("archer", "beothuk_spirit_guardian", 1, new(5, 5))
        .AddCharacter("enemy", "time_weaver", 2, new(8, 5))
        .WithAttack("archer", "fixture_bow", new(AttackType.Physical, WeaponCategory.Bow, 4, 100, 2, 3))
        .WithAttack("enemy", "fixture_scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2))
        .ActiveCharacter("archer");
    private static ScenarioAction Arrow(string id, FrostbiteArrowContext context)
        => new(id, ScenarioActionKind.Ability, "archer", "enemy", "frostbite_arrow", arrowContext: context);
    private static CombatScenarioBuilder HealingEncounter() => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("healer", "gentle_guardian", 1, new(5, 5))
        .AddCharacter("ally", "nigel", 1, new(6, 5))
        .AddCharacter("enemy", "time_weaver", 2, new(9, 5))
        .WithHealth("ally", 30).ActiveCharacter("healer");
    private static CombatScenarioBuilder TauntBuilder() => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("nigel", "nigel", 1, new(5, 5))
        .AddCharacter("enemy", "hooded_assassin", 2, new(6, 5))
        .AddCharacter("other", "time_weaver", 1, new(6, 6))
        .WithAttack("nigel", "axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
        .WithAttack("enemy", "sword", new(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1))
        .WithAttack("other", "scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2))
        .ActiveCharacter("nigel");
    private static CombatScenario TauntEncounter() => TauntBuilder().Accuracy(0, 0, 0).Build();

    private static CombatScenarioBuilder Duel() => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("nigel", "nigel", 1, new(5, 5))
        .AddCharacter("tank", "gravebound_colossus", 2, new(6, 5))
        .WithAttack("nigel", "fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
        .WithAttack("tank", "fixture_lance", new(AttackType.Physical, WeaponCategory.Lance, 3, 100, 1, 1))
        .ActiveCharacter("nigel");
}
