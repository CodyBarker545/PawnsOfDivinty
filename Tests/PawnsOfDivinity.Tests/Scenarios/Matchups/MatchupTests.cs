using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

public class MatchupTests
{
    // TEST FIXTURE VALUES. Unresolved starting Axe/Lance/Bow Power and all accuracy values are caller choices.
    internal static readonly IReadOnlyDictionary<string, ScenarioAttackInput> Fixtures = new Dictionary<string, ScenarioAttackInput>
    {
        ["hooded_assassin"] = new("fixture_daggers", new(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1)),
        ["gentle_guardian"] = new("fixture_light", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2)),
        ["time_weaver"] = new("fixture_fire", new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2)),
        ["nigel"] = new("fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1)),
        ["gravebound_colossus"] = new("fixture_lance", new(AttackType.Physical, WeaponCategory.Lance, 3, 100, 1, 1)),
        ["beothuk_spirit_guardian"] = new("fixture_bow", new(AttackType.Physical, WeaponCategory.Bow, 4, 100, 2, 3))
    };
    private static readonly string[] Assumptions = { "TEST FIXTURE VALUES; flat visible map, no flying or smoke; supplied accuracy and equipment; no balance conclusion." };
    private static int[] Rolls => Enumerable.Repeat(0, 64).ToArray();
    private static readonly MatchupRunner Runner = new();

    [Theory]
    [InlineData("hooded_assassin", 38, 17, 8, 5, 9, 4, 24, 6)]
    [InlineData("gentle_guardian", 44, 7, 11, 12, 15, 18, 14, 4)]
    [InlineData("time_weaver", 38, 6, 8, 17, 13, 14, 17, 5)]
    [InlineData("nigel", 52, 20, 11, 3, 8, 4, 13, 4)]
    [InlineData("gravebound_colossus", 64, 15, 22, 3, 9, 5, 7, 3)]
    [InlineData("beothuk_spirit_guardian", 40, 16, 9, 6, 12, 13, 18, 5)]
    public void AllSixUseRegisteredConfirmedStats(string id, int hp, int str, int def, int intelligence, int res, int wis, int spd, int mov)
    {
        var definition = Duel(id, id == "nigel" ? "time_weaver" : "nigel", MatchupMode.IsolatedCombat);
        var scenario = MatchupScenarioFactory.Create(definition);
        var character = scenario.Characters["a"]; var stats = character.BaseStats;
        Assert.Equal(new[] { hp, str, def, intelligence, res, wis, spd, mov },
            new[] { stats.MaxHealth, stats.Strength, stats.Defense, stats.Intelligence, stats.Resistance, stats.Wisdom, stats.Speed, stats.Movement });
        Assert.Same(CharacterRegistry.GetCharacter(id)!.BaseStats, stats);
        Assert.Equal(1, character.TeamId); Assert.Equal(2, scenario.Characters["b"].TeamId);
    }

    public static IEnumerable<object[]> Pairs() => MatchupScenarioFactory.Pairings().Select(p => new object[] { p.Id });
    [Theory]
    [MemberData(nameof(Pairs))]
    public void AllFifteenPairingsExecuteOrReportRealRangeRejection(string id)
    {
        var definition = MatchupScenarioFactory.DefinePairings(Fixtures, 1, Rolls, Assumptions[0]).Single(p => p.Id == id);
        var result = Runner.Run(definition);
        bool bowOpens = definition.Participants[0].Equipment!.Configuration.Category == WeaponCategory.Bow;
        Assert.Equal(bowOpens ? MatchupStatus.Rejected : MatchupStatus.Completed, result.Status);
        if (bowOpens) { Assert.Equal("InsideMinimumRange", result.Observations.Single().Reason); Assert.Empty(result.RandomDraws); }
        Assert.Equal(definition.Participants[0].DefinitionId, result.Before!.Characters["first"].DefinitionId);
        Assert.Equal(1, result.Before.Characters["first"].TeamId); Assert.Equal(2, result.Before.Characters["second"].TeamId);
        Assert.Equal(MatchupMode.ScriptedGameplay, result.Definition.Mode);
    }

    [Fact]
    public void PairingIdsAreStableUniqueAndUnordered()
    {
        var first = MatchupScenarioFactory.Pairings(); var second = MatchupScenarioFactory.Pairings();
        Assert.Equal(15, first.Count); Assert.Equal(first, second); Assert.Equal(15, first.Select(p => p.Id).Distinct().Count());
        Assert.All(first, p => Assert.True(StringComparer.Ordinal.Compare(p.FirstDefinitionId, p.SecondDefinitionId) < 0));
    }

    [Fact]
    public void FreshFactoryInstancesDoNotShareRuntimeState()
    {
        var definition = Duel(); var first = MatchupScenarioFactory.Create(definition); var second = MatchupScenarioFactory.Create(definition);
        first.Characters["a"].TakeDamage(10); first.Characters["a"].Statuses.ApplyPoison();
        first.Characters["a"].Cooldowns.RecordUse("provoking_roar", 1, 3);
        Assert.Equal(52, second.Characters["a"].CurrentHealth); Assert.False(second.Characters["a"].Statuses.IsPoisoned);
        Assert.Empty(second.Characters["a"].Cooldowns.Cooldowns); Assert.NotSame(first.Activations, second.Activations);
    }

    [Fact]
    public void IsolatedComparisonUsesProductionPhysicalTriangleDamageWithoutSpendingActions()
    {
        var result = Runner.Run(Duel());
        Assert.Equal(MatchupStatus.Completed, result.Status);
        Assert.Equal(4, Assert.Single(result.Strikes).CalculatedDamage);
        Assert.Equal(60, result.After!.Characters["b"].CurrentHealth);
        Assert.False(result.After.Characters["a"].HasActed); Assert.False(result.After.MainActionUsed);
        Assert.Equal(0, result.Metrics!.CompletedMainActions); Assert.Equal(4, result.Metrics.PhysicalCalculatedDamage);
        Assert.Equal(0, result.Metrics.MagicalCalculatedDamage);
        Assert.Contains("IsolatedCombat", MatchupReport.Format(result));
    }

    [Fact]
    public void MagicalDamageUsesResistanceNotDefense()
    {
        var result = Runner.Run(Duel("time_weaver", "gravebound_colossus"));
        Assert.Equal(12, Assert.Single(result.Strikes).CalculatedDamage); // INT 17 + explicit Power 4 - RES 9.
        Assert.Equal(12, result.Metrics!.MagicalCalculatedDamage); Assert.Equal(0, result.Metrics.PhysicalCalculatedDamage);
        Assert.Null(result.Strikes[0].WinterHunterBonus);
    }

    [Fact]
    public void ShieldAndOverkillReportsDistinguishCalculatedDamageFromActualLoss()
    {
        var definition = Duel(participants: new[] { P("a", "nigel", 1, new(5, 5)), P("b", "gravebound_colossus", 2, new(6, 5), health: 1, shield: 2) });
        var result = Runner.Run(definition);
        Assert.Equal(4, result.Strikes.Single().CalculatedDamage); Assert.Equal(1, result.Metrics!.ObservedHPDamage);
        Assert.Equal(2, result.Metrics.ObservedShieldLoss); Assert.Equal(0, result.After!.Characters["b"].CurrentHealth);
        var target = result.Survivability.Single(s => s.InstanceId == "b");
        Assert.True(target.Defeated); Assert.Equal(1, target.DefeatRound); Assert.Equal(0, target.SupportedStrikesSurvived);
    }

    [Fact]
    public void CountersAndFollowUpsAreCountedOnceAndMissingShieldDetailsStayUnavailable()
    {
        var definition = Duel(counters: true, followUps: true,
            participants: new[] { P("a", "nigel", 1, new(5, 5), shield: 3), P("b", "gravebound_colossus", 2, new(6, 5), shield: 4) });
        var result = Runner.Run(definition);
        Assert.Equal(new[] { "Opening", "Counter", "FollowUp" }, result.Strikes.Select(s => s.Role));
        Assert.Equal(1, result.Metrics!.Counterattacks); Assert.Equal(1, result.Metrics.FollowUps); Assert.Equal(3, result.Metrics.Hits);
        Assert.Equal(5, result.Metrics.ObservedHPDamage); Assert.Equal(7, result.Metrics.ObservedShieldLoss);
        Assert.Equal(12, result.Metrics.PhysicalCalculatedDamage);
        Assert.Equal(4, result.Strikes[0].ShieldAbsorbed);
        Assert.Null(result.Strikes[1].ShieldAbsorbed); Assert.Null(result.Strikes[2].ShieldAbsorbed);
        Assert.All(result.Strikes, s => Assert.Null(s.BerserkersFuryBonus));
    }

    [Fact]
    public void ScriptedPlayerAttackConsumesMainActionThroughExistingExecutor()
    {
        var result = Runner.Run(Duel(mode: MatchupMode.ScriptedGameplay));
        Assert.Equal(MatchupStatus.Completed, result.Status); Assert.True(result.After!.MainActionUsed);
        Assert.True(result.After.Characters["a"].HasActed); Assert.Equal(1, result.Metrics!.CompletedMainActions);
        Assert.Equal(3, result.Strikes.Count);
        Assert.Equal(12, result.Metrics.ObservedHPDamage);
    }

    [Fact]
    public void BowCannotCounterAdjacentAndGameplayCannotInventAnApproach()
    {
        var isolated = Runner.Run(Duel("hooded_assassin", "beothuk_spirit_guardian", counters: true));
        Assert.Equal(0, isolated.Metrics!.Counterattacks);
        var scripted = Runner.Run(Duel("hooded_assassin", "beothuk_spirit_guardian", MatchupMode.ScriptedGameplay, distance: 3));
        Assert.Equal(MatchupStatus.Rejected, scripted.Status); Assert.Equal("OutsideMaximumRange", scripted.Observations.Single().Reason);
        Assert.Equal(new GridPosition(5, 5), scripted.After!.Characters["a"].Position);
    }

    [Fact]
    public void MissingEquipmentProducesUnknownInputBlockerNotAMiss()
    {
        var result = Runner.Run(Duel(participants: new[] { new MatchupParticipant("a", "nigel", 1, new(5, 5)), P("b", "gravebound_colossus", 2, new(6, 5)) }));
        Assert.Equal(MatchupStatus.UnresolvedRule, result.Status);
        Assert.Equal(MatchupBlockerCategory.UnknownEquipmentData, Assert.Single(result.Blockers).Category);
        Assert.Equal(0, result.Metrics!.Hits); Assert.Equal(0, result.Metrics.Misses); Assert.Empty(result.RandomDraws);
        Assert.Null(result.Metrics.PhysicalCalculatedDamage); Assert.Null(result.Metrics.MagicalCalculatedDamage);
    }

    [Theory]
    [InlineData(52, 0, 4)]
    [InlineData(26, 0, 9)]
    [InlineData(26, 26, 4)]
    public void FuryUsesCurrentHPAndHealingHistoryIsExplicitFixtureData(int hp, int healing, int damage)
    {
        var definition = Duel(participants: new[] { P("a", "nigel", 1, new(5, 5), health: hp, healing: healing), P("b", "gravebound_colossus", 2, new(6, 5)) });
        var result = Runner.Run(definition);
        Assert.Equal(damage, Assert.Single(result.Strikes).CalculatedDamage);
        Assert.Equal(0, result.Metrics!.RecordedHealing); // Pre-encounter setup is not an executed healing action.
        Assert.Equal(hp + healing, result.Before!.Characters["a"].CurrentHealth);
        Assert.Equal(20, result.After!.Characters["a"].BaseStats.Strength);
    }

    [Theory]
    [InlineData(false, 9, 4)]
    [InlineData(true, 12, 6)]
    public void WinterHunterAddsThreeBeforeExistingFollowUpScaling(bool frostbite, int opening, int follow)
    {
        var definition = Duel("beothuk_spirit_guardian", "nigel", distance: 3, followUps: true,
            participants: new[] { P("a", "beothuk_spirit_guardian", 1, new(5, 5)), P("b", "nigel", 2, new(8, 5), statuses: frostbite ? new[] { new MatchupStatusSetup(StatusType.Frostbite, 2) } : null) });
        var result = Runner.Run(definition);
        Assert.Equal(opening, result.Strikes[0].CalculatedDamage); Assert.Equal(follow, result.Strikes[1].CalculatedDamage);
        Assert.Equal(frostbite ? 3 : 0, result.Strikes[0].WinterHunterBonus);
        Assert.Equal(frostbite ? 3 : 0, result.Strikes[1].WinterHunterBonus); // Pre-scaling contribution, not an extra post-scaling +3.
    }

    [Fact]
    public void SanctuaryMatchedComparisonUsesCurrentConditionAndReportsObservedHPReduction()
    {
        var baselineDefinition = Sanctuary(false); var protectedDefinition = Sanctuary(true);
        var baseline = Runner.Run(baselineDefinition); var protection = Runner.Run(protectedDefinition);
        Assert.Equal(13, baseline.Strikes.Single().CalculatedDamage); Assert.Equal(10, protection.Strikes.Single().CalculatedDamage);
        var compared = MatchupComparison.Compare(baseline, protection,
            new[] { "$.Participants[2].Position.X", "$.Participants[2].Position.Y" }, "Move the same Colossus into the documented Sanctuary condition", "b");
        Assert.True(compared.IsComparable); Assert.Equal(3, compared.ObservedMatchedDefenseHPReduction);
        Assert.Equal(11, protection.After!.Characters["b"].BaseStats.Defense);
        Assert.Equal(15, protection.After.Characters["b"].BaseStats.Resistance);
    }

    [Fact]
    public void ShieldComparisonDoesNotConfuseCalculatedAndPreventedHPDamage()
    {
        var baseline = Runner.Run(Duel());
        var shield = Runner.Run(Duel(id: "shield", participants: new[] { P("a", "nigel", 1, new(5, 5)), P("b", "gravebound_colossus", 2, new(6, 5), shield: 6) }));
        var comparison = MatchupComparison.Compare(baseline, shield, new[] { "$.Participants[1].Shield" }, "Only the initial shield differs", "b");
        Assert.True(comparison.IsComparable); Assert.Equal(4, comparison.ObservedMatchedDefenseHPReduction);
        Assert.Equal(4, shield.Strikes.Single().CalculatedDamage); Assert.Equal(0, shield.Metrics!.ObservedHPDamage);
    }

    [Fact]
    public void SanctuaryDoesNotAutomaticallyPreventThreeHPAtTheMinimumDamageFloor()
    {
        MatchupDefinition Setup(bool protectedPosition) => Duel(id: protectedPosition ? "floor_protected" : "floor_baseline",
            participants: new[] {
                new MatchupParticipant("a", "nigel", 1, new(5, 5), new("explicit_zero_power", new(AttackType.Physical, WeaponCategory.Axe, 0, 100, 1, 1))),
                new MatchupParticipant("b", "gentle_guardian", 2, new(6, 5), Fixtures["gentle_guardian"],
                    modifiers: new[] { new TemporaryStatModifier("fixture_defense", "b", "b", TemporaryStat.Defense, 20, 1, 2) }),
                P("c", "gravebound_colossus", 2, protectedPosition ? new(5, 4) : new(1, 1)) });
        var baseline = Runner.Run(Setup(false)); var protection = Runner.Run(Setup(true));
        Assert.Equal(1, baseline.Strikes.Single().CalculatedDamage); Assert.Equal(1, protection.Strikes.Single().CalculatedDamage);
        var comparison = MatchupComparison.Compare(baseline, protection,
            new[] { "$.Participants[2].Position.X", "$.Participants[2].Position.Y" }, "Same explicit high-Defense fixture with and without qualifying Sanctuary", "b");
        Assert.True(comparison.IsComparable); Assert.Equal(0, comparison.ObservedMatchedDefenseHPReduction);
    }

    [Fact]
    public void EquipmentChangesMustBeDeclaredAndCannotBeAttributedToDefense()
    {
        var baseline = Runner.Run(Duel());
        var changed = Runner.Run(Duel(id: "different_power", participants: new[] {
            new MatchupParticipant("a", "nigel", 1, new(5, 5), new("fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 0, 100, 1, 1))),
            P("b", "gravebound_colossus", 2, new(6, 5)) }));
        Assert.False(MatchupComparison.Compare(baseline, changed, Array.Empty<string>(), "Undeclared gear change").IsComparable);
        var compared = MatchupComparison.Compare(baseline, changed, new[] { "$.Participants[0].Equipment.Configuration.Power" }, "Explicit Power 4 versus Power 0 fixture experiment", "b");
        Assert.True(compared.IsComparable); Assert.Null(compared.ObservedMatchedDefenseHPReduction);
        Assert.Equal("4", compared.Differences.Single().Baseline); Assert.Equal("0", compared.Differences.Single().Variant);
    }

    [Fact]
    public void AmbiguousSanctuaryRemainsUnresolvedWithoutDamageOrRolls()
    {
        var definition = Duel(participants: new[] { P("a", "nigel", 1, new(5, 5)), P("b", "gentle_guardian", 2, new(6, 5)),
            P("c", "gravebound_colossus", 2, new(5, 4)), P("d", "gravebound_colossus", 2, new(5, 6)) });
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.UnresolvedRule, result.Status); Assert.Contains("OverlapUnresolved", result.Blockers.Single().Condition);
        Assert.Equal(result.Before!.ToCanonicalJson(), result.After!.ToCanonicalJson()); Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void DefensiveTwoVersusTwoUsesHealingBlessingShieldsAndNormalCombat()
    {
        var result = Runner.Run(DefensiveFormation());
        Assert.Equal(MatchupStatus.Completed, result.Status); Assert.Equal(1, result.Metrics!.CompletedRounds);
        Assert.Equal(2, result.Before!.Characters.Values.Count(c => c.TeamId == 1));
        Assert.Equal(2, result.Before.Characters.Values.Count(c => c.TeamId == 2));
        Assert.Equal(1, result.Metrics.RecordedHealing); Assert.Equal(6, result.Metrics.ObservedShieldLoss);
        Assert.Equal(40, result.After!.Characters["guardian"].CurrentHealth);
        Assert.Equal(24, result.After.Characters["tank"].EffectiveDefense);
        Assert.Equal(22, result.After.Characters["tank"].BaseStats.Defense);
        Assert.Equal(3, result.Metrics.CompletedMainActions);
    }

    [Fact]
    public void FullRosterThreeVersusThreeExecutesExplicitActionsDeterministically()
    {
        var definition = FullRoster(); var a = Runner.Run(definition); var b = Runner.Run(definition);
        Assert.Equal(MatchupStatus.Completed, a.Status);
        Assert.Equal(6, a.After!.Characters.Count); Assert.Equal(3, a.After.Characters.Values.Count(c => c.TeamId == 1));
        Assert.Equal(6, a.After.Characters.Keys.Distinct().Count()); Assert.Equal(a.After.ToCanonicalJson(), b.After!.ToCanonicalJson());
        Assert.Equal(a.Events, b.Events); Assert.Equal(a.RandomDraws, b.RandomDraws);
        Assert.True(a.Metrics!.PhysicalCalculatedDamage > 0); Assert.True(a.Metrics.MagicalCalculatedDamage > 0);
        Assert.Empty(a.After.Characters["time"].Cooldowns); Assert.NotEmpty(a.After.Characters["guardian"].Cooldowns);
    }

    [Fact]
    public void UnsupportedAbilityInFullRosterIsNotACompletedWinOrMiss()
    {
        var roster = FullRoster().Participants;
        var definition = new MatchupDefinition("unsupported_roster", MatchupMode.ScriptedGameplay, roster, Assumptions, Rolls,
            actions: new[] { new ScenarioAction("transmute", ScenarioActionKind.Ability, "time", "spirit", "arcane_transmutation") },
            activeCharacterId: "time", targetingLegal: true);
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.HarnessUnsupported, result.Status);
        Assert.Equal(MatchupBlockerCategory.MissingHarnessAdapter, Assert.Single(result.Blockers).Category);
        Assert.Equal(0, result.Metrics!.CompletedMainActions); Assert.Equal(0, result.Metrics.Misses);
        Assert.False(result.After!.MainActionUsed); Assert.Equal(result.Before!.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void MultipleRoundsReuseExistingRunnerWithoutDoubleCountingPhaseSnapshots()
    {
        var participants = new[] { P("a", "nigel", 1, new(5, 5)), P("b", "gravebound_colossus", 2, new(6, 5)) };
        ScenarioRoundPlan Round(int r) => new(r, 1,
            new("a", new ScenarioAction($"a{r}", ScenarioActionKind.Attack, "a", "b")),
            new("b", new ScenarioAction($"b{r}", ScenarioActionKind.Attack, "b", "a")));
        var result = Runner.Run(new("two_rounds", MatchupMode.ScriptedGameplay, participants, Assumptions, Rolls, rounds: new[] { Round(1), Round(2) }, targetingLegal: true));
        Assert.True(result.Status == MatchupStatus.Completed, MatchupReport.Format(result)); Assert.Equal(2, result.Metrics!.CompletedRounds);
        Assert.Equal(4, result.Metrics.CompletedMainActions);
        long actualLoss = result.Before!.Characters.Values.Sum(c => c.CurrentHealth) - result.After!.Characters.Values.Sum(c => c.CurrentHealth);
        Assert.Equal(actualLoss, result.Metrics.ObservedHPDamage);
        Assert.All(result.After.Characters.Values, c => Assert.True(c.HasActed));
        Assert.Equal(Enumerable.Range(1, result.Events.Count).Select(i => (long)i), result.Events.Select(e => e.Sequence));
    }

    [Fact]
    public void RoundFixturesKeepCooldownsExpireStatusesAndRejectDefeatedActors()
    {
        var participants = new[] { P("a", "nigel", 1, new(5, 5), health: 1, statuses: new[] { new MatchupStatusSetup(StatusType.Poison) },
            cooldowns: new[] { new MatchupCooldown("provoking_roar", 1, 3) }),
            P("b", "gravebound_colossus", 2, new(6, 5), statuses: new[] { new MatchupStatusSetup(StatusType.Burn, 2) }) };
        ScenarioRoundPlan Round(int r) => new(r, 1, new("a", End($"a{r}", "a")), new("b", End($"b{r}", "b")));
        var result = Runner.Run(new("death_and_status", MatchupMode.ScriptedGameplay, participants, Assumptions, Rolls, rounds: new[] { Round(1), Round(2) }));
        Assert.Equal(MatchupStatus.Rejected, result.Status); Assert.Equal(2, result.Metrics!.CompletedRounds);
        Assert.Equal(0, result.After!.Characters["a"].CurrentHealth); Assert.Equal(4, result.After.Characters["a"].Cooldowns["provoking_roar"].ReadyRound);
        Assert.Empty(result.After.Characters["b"].TimedStatuses); Assert.Equal(22, result.After.Characters["b"].EffectiveDefense);
        Assert.Equal(1, result.Survivability.Single(s => s.InstanceId == "a").DefeatRound);
    }

    [Fact]
    public void RepeatedMainActionIsRejectedAndCannotApplyDamageAgain()
    {
        var definition = new MatchupDefinition("budget", MatchupMode.ScriptedGameplay, Duel().Participants, Assumptions, Rolls,
            actions: new[] { Attack("first"), Attack("second") }, activeCharacterId: "a", targetingLegal: true);
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.Rejected, result.Status); Assert.Equal(1, result.Metrics!.CompletedMainActions);
        Assert.Equal(2, result.Metrics.AttacksAttempted); Assert.Equal(3, result.RandomDraws.Count);
        Assert.Equal(12, result.Metrics.ObservedHPDamage);
    }

    [Theory]
    [InlineData(MatchupMode.IsolatedCombat)]
    [InlineData(MatchupMode.ScriptedGameplay)]
    public void ExhaustedRngReportsPartialDamageAndDoesNotRetryOrInventStrikeTotals(MatchupMode mode)
    {
        var definition = Duel(mode: mode, counters: true, followUps: true, rolls: new[] { 0 });
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.PartiallyApplied, result.Status); Assert.Equal(60, result.After!.Characters["b"].CurrentHealth);
        Assert.Equal(4, result.Metrics!.ObservedHPDamage); Assert.Null(result.Metrics.Hits); Assert.Null(result.Metrics.Misses);
        Assert.Null(result.Metrics.PhysicalCalculatedDamage); Assert.Null(result.Metrics.Counterattacks);
        Assert.Equal(2, result.RandomDraws.Count); Assert.Single(result.Observations);
        Assert.Contains("unavailable", MatchupReport.Format(result));
    }

    [Fact]
    public void InvalidRngIsUnexpectedFailureNotAnUnresolvedRuleOrMiss()
    {
        var result = Runner.Run(Duel(rolls: new[] { 100 }));
        Assert.Equal(MatchupStatus.UnexpectedFailure, result.Status); Assert.Null(result.Metrics!.Misses);
        Assert.Equal(0, result.Metrics.ObservedHPDamage); Assert.Equal(ScenarioDrawOutcome.Invalid, Assert.Single(result.RandomDraws).Outcome);
    }

    [Fact]
    public void CompleteReportsRetainDetachedStateAndFixtureInputs()
    {
        var definition = Duel(); var result = Runner.Run(definition); var json = JsonSerializer.Serialize(result);
        Runner.Run(definition);
        Assert.Equal(json, JsonSerializer.Serialize(result));
        Assert.Throws<NotSupportedException>(() => ((IList<MatchupStrike>)result.Strikes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)definition.Accuracy).Clear());
        var report = MatchupReport.Format(result);
        Assert.Contains("TEST FIXTURE VALUES", report); Assert.Contains("Power 4", report); Assert.Contains("position (5,5)", report);
        Assert.Contains("no winner", report); Assert.Equal(60, result.Survivability.Single(s => s.InstanceId == "b").FinalHP);
    }

    [Fact]
    public void ComparisonRejectsUndeclaredInputsIncompleteRunsAndMixedModes()
    {
        var baseline = Runner.Run(Duel());
        var changed = Runner.Run(Duel(participants: new[] { P("a", "nigel", 1, new(5, 5), health: 26), P("b", "gravebound_colossus", 2, new(6, 5)) }));
        Assert.False(MatchupComparison.Compare(baseline, changed, Array.Empty<string>(), "No changes declared").IsComparable);
        var approved = MatchupComparison.Compare(baseline, changed, new[] { "$.Participants[0].Health" }, "Only Nigel's initial HP differs");
        Assert.True(approved.IsComparable); Assert.Equal(5, approved.HPDamageDifference); Assert.Null(approved.ObservedMatchedDefenseHPReduction);
        Assert.Equal("null", approved.Differences.Single().Baseline); Assert.Equal("26", approved.Differences.Single().Variant);
        Assert.False(MatchupComparison.Compare(baseline, Runner.Run(Duel(mode: MatchupMode.ScriptedGameplay)), Array.Empty<string>(), "Different modes").IsComparable);
        Assert.False(MatchupComparison.Compare(baseline, Runner.Run(Duel(rolls: Array.Empty<int>())), Array.Empty<string>(), "Failed run").IsComparable);
    }

    [Fact]
    public void HitMissVariantsAreDeterministicAndRecordEveryChangedRandomInput()
    {
        var hit = Runner.Run(Duel("hooded_assassin", "nigel", counters: true, rolls: new[] { 0, 0 }));
        var miss = Runner.Run(Duel("hooded_assassin", "nigel", counters: true, rolls: new[] { 0, 99 }));
        Assert.Equal(2, hit.Metrics!.Hits); Assert.Equal(1, miss.Metrics!.Misses);
        var comparison = MatchupComparison.Compare(hit, miss, new[] { "$.Accuracy[1]" }, "Change only the counterattack roll");
        Assert.True(comparison.IsComparable); Assert.Single(comparison.Differences);
    }

    [Fact]
    public void DefinitionsCopyCallerCollectionsAndRejectConflictingIdentityOrMixedModes()
    {
        var participants = Duel().Participants.ToList(); var rolls = new[] { 0 }; var assumptions = new[] { "Explicit test input" };
        var definition = new MatchupDefinition("copied", MatchupMode.IsolatedCombat, participants, assumptions, rolls,
            isolatedAttacks: new[] { new IsolatedAttack("hit", "a", "b", false, false) });
        participants.Clear(); rolls[0] = 99; assumptions[0] = "changed";
        Assert.Equal(2, definition.Participants.Count); Assert.Equal(0, definition.Accuracy.Single()); Assert.Equal("Explicit test input", definition.Assumptions.Single());
        Assert.Throws<ArgumentException>(() => new MatchupDefinition("duplicate", MatchupMode.IsolatedCombat,
            new[] { P("same", "nigel", 1, new(1, 1)), P("same", "time_weaver", 2, new(2, 1)) }, Assumptions, Rolls));
        Assert.Throws<ArgumentException>(() => new MatchupDefinition("mixed", MatchupMode.IsolatedCombat, Duel().Participants, Assumptions, Rolls,
            actions: new[] { Attack("attack") }, isolatedAttacks: new[] { new IsolatedAttack("hit", "a", "b", false, false) }));
    }

    [Fact]
    public void ElementalPressureTwoVersusTwoReportsPhysicalMagicalAndExistingStatusState()
    {
        var definition = new MatchupDefinition("elemental_2v2", MatchupMode.ScriptedGameplay,
            new[] { P("spirit", "beothuk_spirit_guardian", 1, new(5, 5)), P("time", "time_weaver", 1, new(5, 6)),
                P("nigel", "nigel", 2, new(8, 5), statuses: new[] { new MatchupStatusSetup(StatusType.Frostbite, 2) }),
                P("guardian", "gentle_guardian", 2, new(7, 6)) }, Assumptions, Rolls,
            rounds: new[] { new ScenarioRoundPlan(1, 1,
                new("spirit", new ScenarioAction("bow", ScenarioActionKind.Attack, "spirit", "nigel")),
                new("nigel", End("nEnd", "nigel")),
                new("time", new ScenarioAction("magic", ScenarioActionKind.Attack, "time", "guardian")),
                new("guardian", End("gEnd", "guardian"))) }, targetingLegal: true);
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.Completed, result.Status);
        Assert.Equal(1, result.After!.Characters["nigel"].TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(3, result.Strikes.First().WinterHunterBonus);
        Assert.True(result.Metrics!.PhysicalCalculatedDamage > 0); Assert.True(result.Metrics.MagicalCalculatedDamage > 0);
        Assert.Equal(2, result.Metrics.CompletedMainActions);
    }

    [Fact]
    public void CooldownExperimentReportsRejectedReuseAndPreservesActualDeadlinesAcrossRounds()
    {
        var assumptions = Assumptions.Append("Enemy has no available attack configuration in this fixture; required-attack legality is explicitly false.");
        ScenarioRoundPlan Round(int r)
        {
            var roar = new ScenarioAction($"roar{r}", ScenarioActionKind.Ability, "a", abilityId: "provoking_roar");
            bool ready = r == 2 || r == 5;
            return new(r, 1, new("a", ready ? new[] { roar } : new[] { roar, End($"aEnd{r}", "a") }),
                new("b", new ScenarioAction($"bEnd{r}", ScenarioActionKind.EndActivation, "b", requiredAttack: ready ? new("a", true, true, false) : null)));
        }
        var definition = new MatchupDefinition("cooldown", MatchupMode.ScriptedGameplay,
            new[] { P("a", "nigel", 1, new(5, 5)), new MatchupParticipant("b", "time_weaver", 2, new(6, 5)) }, assumptions, Rolls,
            rounds: new[] { Round(2), Round(3), Round(4), Round(5) }, initialRound: 2, targetingLegal: true);
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.Rejected, result.Status); Assert.Equal(4, result.Metrics!.CompletedRounds);
        Assert.Equal(2, result.Metrics.CompletedAbilities); Assert.Equal(8, result.After!.Characters["a"].Cooldowns["provoking_roar"].ReadyRound);
        Assert.Equal(2, result.Observations.Count(o => o.Reason == "OnCooldown"));
        Assert.Equal(0, result.Metrics.ObservedHPDamage); Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void TauntCannotBeBypassedThroughMatchupActionsAndRejectedTargetConsumesNothing()
    {
        var definition = new MatchupDefinition("taunt", MatchupMode.ScriptedGameplay,
            new[] { P("a", "nigel", 1, new(5, 5)), P("b", "hooded_assassin", 2, new(6, 5)), P("other", "time_weaver", 1, new(6, 6)) }, Assumptions, Rolls,
            actions: new[] { new ScenarioAction("roar", ScenarioActionKind.Ability, "a", abilityId: "provoking_roar"),
                new ScenarioAction("select", ScenarioActionKind.StartActivation, "b"),
                new ScenarioAction("wrong", ScenarioActionKind.Attack, "b", "other", requiredAttack: new("a", true, true, true)),
                new ScenarioAction("required", ScenarioActionKind.Attack, "b", "a", requiredAttack: new("a", true, true, true)) },
            activeCharacterId: "a", targetingLegal: true);
        var result = Runner.Run(definition);
        Assert.Equal(MatchupStatus.Rejected, result.Status);
        var wrong = result.Observations.Single(o => o.Id == "wrong");
        Assert.Equal("MustAttackSource", wrong.Reason); Assert.Equal(wrong.Before.ToCanonicalJson(), wrong.After.ToCanonicalJson());
        Assert.Equal(MatchupStatus.Completed, result.Observations.Single(o => o.Id == "required").Status);
        Assert.Equal(2, result.Metrics!.CompletedMainActions); Assert.Equal(3, result.RandomDraws.Count);
    }

    [Fact]
    public void RepresentativeControlledSuiteProducesTypedDeterministicReports()
    {
        var definitions = new[] { Duel(mode: MatchupMode.ScriptedGameplay, id: "nigel_colossus"),
            Duel("hooded_assassin", "time_weaver", MatchupMode.ScriptedGameplay, id: "assassin_time_weaver"),
            Duel("beothuk_spirit_guardian", "nigel", MatchupMode.ScriptedGameplay, distance: 3, id: "spirit_nigel"),
            DefensiveFormation(), FullRoster() };
        var results = definitions.Select(Runner.Run).ToArray();
        Assert.All(results, r => Assert.Equal(MatchupStatus.Completed, r.Status));
        // Full roster: Assassin/Nigel exchanges lose 38 + 40 HP; Mage/Bow exchanges lose 21 + 21 HP.
        Assert.Equal(new long[] { 12, 33, 13, 25, 120 }, results.Select(r => r.Metrics!.ObservedHPDamage));
        Assert.Equal(6, results[3].Metrics!.ObservedShieldLoss);
        Assert.All(results, r => Assert.Equal(MatchupReport.Format(r), MatchupReport.Format(Runner.Run(r.Definition))));
        // Normal regression runs perform no file writes. This explicit opt-in exports the executed suite for review.
        var output = Environment.GetEnvironmentVariable("PAWNS_MATCHUP_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "RepresentativeSuite.json"), JsonSerializer.Serialize(results.Select(MatchupReport.ToData), new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "RepresentativeSuite.md"),
                "# Stage 6.3 controlled fixture results\n\nGenerated from actual production executions. These are not rankings or full-match outcomes.\n\n" +
                string.Join("\n\n", results.Select(MatchupReport.Format)));
        }
    }

    internal static MatchupDefinition Duel(string first = "nigel", string second = "gravebound_colossus", MatchupMode mode = MatchupMode.IsolatedCombat,
        int distance = 1, bool counters = false, bool followUps = false, IEnumerable<MatchupParticipant>? participants = null,
        IEnumerable<int>? rolls = null, string id = "duel")
        => new(id, mode, participants ?? new[] { P("a", first, 1, new(5, 5)), P("b", second, 2, new(5 + distance, 5)) }, Assumptions,
            rolls ?? Rolls, actions: mode == MatchupMode.ScriptedGameplay ? new[] { Attack("opening") } : null,
            isolatedAttacks: mode == MatchupMode.IsolatedCombat ? new[] { new IsolatedAttack("opening", "a", "b", counters, followUps) } : null,
            activeCharacterId: mode == MatchupMode.ScriptedGameplay ? "a" : null, targetingLegal: true);
    private static MatchupParticipant P(string id, string definition, int team, GridPosition position, int? health = null, int shield = 0,
        IEnumerable<MatchupStatusSetup>? statuses = null, IEnumerable<MatchupCooldown>? cooldowns = null, int healing = 0)
        => new(id, definition, team, position, Fixtures[definition], health, shield, statuses, cooldowns: cooldowns, fixtureHealing: healing);
    private static ScenarioAction Attack(string id) => new(id, ScenarioActionKind.Attack, "a", "b");
    private static ScenarioAction End(string id, string actor) => new(id, ScenarioActionKind.EndActivation, actor);
    private static MatchupDefinition Sanctuary(bool active) => Duel(id: active ? "protected" : "baseline",
        participants: new[] { P("a", "nigel", 1, new(5, 5)), P("b", "gentle_guardian", 2, new(6, 5)),
            P("c", "gravebound_colossus", 2, active ? new(5, 4) : new(1, 1)) });

    internal static MatchupDefinition DefensiveFormation()
        => new("defensive_2v2", MatchupMode.ScriptedGameplay,
            new[] { P("guardian", "gentle_guardian", 1, new(6, 5), shield: 6), P("tank", "gravebound_colossus", 1, new(4, 5), health: 63),
                P("nigel", "nigel", 2, new(5, 5)), P("assassin", "hooded_assassin", 2, new(4, 6)) }, Assumptions, Rolls,
            rounds: new[] { new ScenarioRoundPlan(1, 1,
                new("guardian", new ScenarioAction("heal", ScenarioActionKind.Ability, "guardian", "tank", "healing_light")),
                new("nigel", new ScenarioAction("axe", ScenarioActionKind.Attack, "nigel", "guardian")),
                new("tank", End("tankEnd", "tank")), new("assassin", new ScenarioAction("daggers", ScenarioActionKind.Attack, "assassin", "tank"))) }, targetingLegal: true);

    internal static MatchupDefinition FullRoster()
        => new("roster_3v3", MatchupMode.ScriptedGameplay,
            new[] { P("assassin", "hooded_assassin", 1, new(5, 5)), P("guardian", "gentle_guardian", 1, new(5, 6)), P("time", "time_weaver", 1, new(5, 7)),
                P("nigel", "nigel", 2, new(6, 5)), P("tank", "gravebound_colossus", 2, new(6, 6)), P("spirit", "beothuk_spirit_guardian", 2, new(7, 7)) }, Assumptions, Rolls,
            rounds: new[] { new ScenarioRoundPlan(1, 1,
                new("assassin", new ScenarioAction("assassinHit", ScenarioActionKind.Attack, "assassin", "nigel")),
                new("nigel", new ScenarioAction("nigelHit", ScenarioActionKind.Attack, "nigel", "assassin")),
                new("guardian", new ScenarioAction("healing", ScenarioActionKind.Ability, "guardian", "assassin", "healing_light")),
                new("tank", End("tankEnd", "tank")),
                new("time", new ScenarioAction("magic", ScenarioActionKind.Attack, "time", "spirit")),
                new("spirit", new ScenarioAction("bow", ScenarioActionKind.Attack, "spirit", "time"))) }, targetingLegal: true);
}
