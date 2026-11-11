using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;
using Xunit;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

/// <summary>Behavioral readiness diagnostics. These do not approve provisional policies or implement missing systems.</summary>
public class CombatReadinessTests
{
    [Theory]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("nigel")]
    [InlineData("gravebound_colossus")]
    [InlineData("beothuk_spirit_guardian")]
    public void RegisteredCharactersRemainAvailableThroughProductionFactory(string id)
    {
        var definition = CharacterRegistry.GetCharacter(id);
        Assert.NotNull(definition);
        var first = CharacterFactory.Create(definition, "readiness_first", 1);
        var second = CharacterFactory.Create(definition, "readiness_second", 2);
        Assert.Equal(id, first.Definition!.Id); Assert.Same(definition.BaseStats, first.BaseStats);
        Assert.NotSame(first, second); Assert.Equal(first.BaseStats.MaxHealth, first.CurrentHealth);
    }

    [Fact]
    public void AllFifteenExplicitMatchupDefinitionsRemainAvailable()
    {
        var definitions = MatchupScenarioFactory.DefinePairings(MatchupTests.Fixtures, 1, new[] { 0, 0, 0 },
            "TEST FIXTURE VALUES; coverage only, not a complete battle");
        Assert.Equal(15, definitions.Count); Assert.Equal(15, definitions.Select(d => d.Id).Distinct().Count());
        Assert.All(definitions, d => {
            Assert.NotEqual(d.Participants[0].DefinitionId, d.Participants[1].DefinitionId);
            Assert.NotEqual(d.Participants[0].TeamId, d.Participants[1].TeamId);
        });
    }

    [Fact]
    public void SupportedProductionAttackBoundaryConsumesTheExistingActionBudget()
    {
        var definition = MatchupTests.Duel(mode: MatchupMode.ScriptedGameplay);
        var scenario = MatchupScenarioFactory.Create(definition);
        var originalStats = scenario.Characters["a"].BaseStats;
        var result = scenario.Runner.Execute(definition.Actions.Single());
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        Assert.NotNull(result.Production!.Combat); Assert.True(result.After.MainActionUsed);
        Assert.Null(result.After.ActiveCharacterId); Assert.Same(originalStats, scenario.Characters["a"].BaseStats);
        Assert.False(scenario.Activations.CanUseMainAction);
    }

    [Fact]
    public void ForcedMovementContractRequiresActualTransitionLegality()
    {
        var scenario = MatchupScenarioFactory.Create(MatchupTests.Duel());
        IForcedMovementMap map = scenario.Map;
        var service = new ForcedMovementService();
        var before = scenario.Capture().ToCanonicalJson();
        var validation = service.Validate(scenario.Characters["a"], scenario.Characters["b"], new(1, 0), 2, map);
        Assert.False(validation.IsLegal);
        Assert.Equal(ForcedMovementRejectionReason.MissingMapInformation, validation.RejectionReason);
        Assert.Equal(before, scenario.Capture().ToCanonicalJson());
    }

    [Fact]
    public void UnconfirmedMovementBookkeepingDoesNotMoveOrSpendAnything()
    {
        var scenario = MatchupScenarioFactory.Create(MatchupTests.Duel(mode: MatchupMode.ScriptedGameplay));
        var result = scenario.Runner.Execute(new("movement", ScenarioActionKind.ConfirmedMovement, "a"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.False(result.After.MovementUsed); Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void MissingEquipmentIsNotReplacedWithRepresentativeFixturePower()
    {
        var definition = MatchupTests.Duel(participants: new[] {
            new MatchupParticipant("a", "nigel", 1, new(5, 5)),
            new MatchupParticipant("b", "gravebound_colossus", 2, new(6, 5)) });
        var result = new MatchupRunner().Run(definition);
        Assert.Equal(MatchupStatus.UnresolvedRule, result.Status);
        Assert.Equal(MatchupBlockerCategory.UnknownEquipmentData, Assert.Single(result.Blockers).Category);
        Assert.Null(result.Metrics!.PhysicalCalculatedDamage); Assert.Equal(0, result.Metrics.Misses);
        Assert.Equal(result.Before!.ToCanonicalJson(), result.After!.ToCanonicalJson());
    }

    [Fact]
    public void SanctuaryOverlapDoesNotSilentlyStackOrConsumeRandomness()
    {
        MatchupParticipant P(string id, string character, int team, GridPosition position)
            => new(id, character, team, position, MatchupTests.Fixtures[character]);
        var definition = MatchupTests.Duel(participants: new[] {
            P("a", "nigel", 1, new(5, 5)), P("b", "gentle_guardian", 2, new(6, 5)),
            P("c", "gravebound_colossus", 2, new(5, 4)), P("d", "gravebound_colossus", 2, new(5, 6)) });
        var result = new MatchupRunner().Run(definition);
        Assert.Equal(MatchupStatus.UnresolvedRule, result.Status);
        Assert.Contains(result.Blockers, b => b.Condition == nameof(SanctuaryDefenseReason.OverlapUnresolved));
        Assert.Empty(result.RandomDraws); Assert.Empty(result.Strikes);
        Assert.Equal(result.Before!.ToCanonicalJson(), result.After!.ToCanonicalJson());
    }

    [Fact]
    public void QualifyingDeadSanctuarySourceRemainsAnExplicitScopeQuestion()
    {
        var scenario = new CombatScenarioBuilder()
            .AddCharacter("source", "gravebound_colossus", 2, new(5, 4))
            .AddCharacter("recipient", "gentle_guardian", 2, new(6, 5))
            .AddCharacter("enemy", "nigel", 1, new(5, 5)).Build();
        scenario.Characters["source"].TakeDamage(int.MaxValue);
        var result = SanctuaryOfTheForgotten.Evaluate(scenario.Characters["source"], scenario.Characters["recipient"], scenario.Characters["enemy"]);
        Assert.False(result.IsResolved); Assert.Equal(SanctuaryReason.SourceDeathUnresolved, result.Reason);
        Assert.Null(result.DefenseBonus); Assert.Equal(11, scenario.Characters["recipient"].BaseStats.Defense);
    }

    [Fact]
    public void SanctuaryShieldManagementExistsButDoesNotInventCastRange()
    {
        var scenario = SupportScenario();
        var executor = new SanctuaryShieldAbilityExecutor(new SanctuaryShieldManager());
        var use = new AbilityUse(scenario.Characters["guardian"], "sanctuary_shield", scenario.Activations);
        var before = scenario.Capture().ToCanonicalJson();
        var preview = executor.Preview(use, scenario.Characters["ally"], AllyRules(), true);
        Assert.False(preview.CanExecute);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, preview.Targeting!.RejectionReason);
        Assert.Equal(before, scenario.Capture().ToCanonicalJson());
    }

    [Fact]
    public void UnknownSpellTagUnderSilenceDoesNotBecomeConfirmedNonSpell()
    {
        var scenario = SupportScenario();
        scenario.Characters["guardian"].Statuses.ApplyTimedStatus(PawnsOfDivinity.Core.Statuses.StatusType.Silence, 1);
        var result = scenario.Runner.Execute(new("heal", ScenarioActionKind.Ability, "guardian", "ally", "healing_light"));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal(nameof(AbilityRejectionReason.MetadataUnresolved), result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void MissingAbilityAdapterIsSeparateFromACompletedProductionAction()
    {
        var definition = new MatchupDefinition("readiness_transmutation", MatchupMode.ScriptedGameplay,
            new[] { new MatchupParticipant("a", "time_weaver", 1, new(5, 5), MatchupTests.Fixtures["time_weaver"]),
                new MatchupParticipant("b", "nigel", 2, new(6, 5), MatchupTests.Fixtures["nigel"]) },
            new[] { "TEST FIXTURE VALUES; no Transmutation adapter/catalog is supplied" }, new[] { 0 },
            actions: new[] { new ScenarioAction("transmute", ScenarioActionKind.Ability, "a", "b", "arcane_transmutation") },
            activeCharacterId: "a", targetingLegal: true);
        var result = new MatchupRunner().Run(definition);
        Assert.Equal(MatchupStatus.HarnessUnsupported, result.Status);
        Assert.Equal(0, result.Metrics!.CompletedMainActions); Assert.Equal(0, result.Metrics.Misses);
        Assert.Equal(MatchupBlockerCategory.MissingHarnessAdapter, result.Blockers.Single().Category);
        Assert.Empty(result.After!.Characters["a"].Cooldowns);
    }

    [Fact]
    public void PartialAttackCannotBeRetriedOnTheSameRuntimeScenario()
    {
        var scenario = MatchupScenarioFactory.Create(MatchupTests.Duel(mode: MatchupMode.ScriptedGameplay, rolls: new[] { 0 }));
        var action = new ScenarioAction("partial", ScenarioActionKind.Attack, "a", "b");
        var first = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.PartiallyApplied, first.Outcome);
        Assert.Equal(60, first.After.Characters["b"].CurrentHealth);
        var repeat = scenario.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Rejected, repeat.Outcome); Assert.Empty(repeat.RandomDraws);
        Assert.Equal(first.After.ToCanonicalJson(), repeat.After.ToCanonicalJson());
        Assert.Equal(2, scenario.Runner.Draws.Count); Assert.Null(first.Production);
    }

    [Fact]
    public void ShieldOnlyArrowEligibilityRemainsExplicitlyUnresolvedWithPrototypeDurationOptIn()
    {
        var definition = new MatchupDefinition("readiness_arrow", MatchupMode.ScriptedGameplay,
            new[] { new MatchupParticipant("a", "beothuk_spirit_guardian", 1, new(5, 5), MatchupTests.Fixtures["beothuk_spirit_guardian"]),
                new MatchupParticipant("b", "time_weaver", 2, new(8, 5), MatchupTests.Fixtures["time_weaver"], shield: 6) },
            new[] { "TEST FIXTURE VALUES; explicit provisional arrow duration, ground target, no terrain Frostbite" }, new[] { 0 },
            actions: new[] { new ScenarioAction("arrow", ScenarioActionKind.Ability, "a", "b", "frostbite_arrow",
                arrowContext: new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, false)) },
            activeCharacterId: "a", targetingLegal: true);
        var result = new MatchupRunner().Run(definition);
        Assert.Equal(MatchupStatus.UnresolvedRule, result.Status);
        Assert.Contains(result.Blockers, b => b.Condition == nameof(FrostbiteArrowReason.ShieldEligibilityUnresolved));
        Assert.Empty(result.RandomDraws); Assert.Equal(result.Before!.ToCanonicalJson(), result.After!.ToCanonicalJson());
    }

    [Fact]
    public void PersistedRepresentativeReportsMatchFreshTypedProductionEvidence()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "Tests", "PawnsOfDivinity.Tests", "Scenarios", "Matchups", "Reports", "RepresentativeSuite.json")));
        var stored = json.RootElement.EnumerateArray().ToDictionary(r => r.GetProperty("Definition").GetProperty("Id").GetString()!);
        var definitions = new[] { MatchupTests.Duel(mode: MatchupMode.ScriptedGameplay, id: "nigel_colossus"),
            MatchupTests.Duel("hooded_assassin", "time_weaver", MatchupMode.ScriptedGameplay, id: "assassin_time_weaver"),
            MatchupTests.Duel("beothuk_spirit_guardian", "nigel", MatchupMode.ScriptedGameplay, distance: 3, id: "spirit_nigel"),
            MatchupTests.DefensiveFormation(), MatchupTests.FullRoster() };
        foreach (var definition in definitions)
        {
            var actual = new MatchupRunner().Run(definition); var report = stored[definition.Id];
            Assert.Equal(MatchupStatus.Completed, actual.Status);
            var metrics = report.GetProperty("Metrics");
            Assert.Equal(metrics.GetProperty("ObservedHPDamage").GetInt64(), actual.Metrics!.ObservedHPDamage);
            Assert.Equal(metrics.GetProperty("ObservedShieldLoss").GetInt64(), actual.Metrics.ObservedShieldLoss);
            Assert.Equal(metrics.GetProperty("RecordedHealing").GetInt64(), actual.Metrics.RecordedHealing);
            Assert.Equal(metrics.GetProperty("CompletedMainActions").GetInt32(), actual.Metrics.CompletedMainActions);
            Assert.Equal(report.GetProperty("RandomDraws").GetArrayLength(), actual.RandomDraws.Count);
            Assert.All(actual.RandomDraws, d => Assert.Equal(0, d.Value));
            Assert.NotEmpty(actual.Definition.Assumptions);
            Assert.Contains("TEST FIXTURE VALUES", MatchupReport.Format(actual));
            Assert.Equal(actual.Before!.Characters.Values.Sum(c => (long)c.CurrentHealth) + actual.Metrics.RecordedHealing
                - actual.After!.Characters.Values.Sum(c => (long)c.CurrentHealth), actual.Metrics.ObservedHPDamage);
        }
    }

    private static CombatScenario SupportScenario() => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("guardian", "gentle_guardian", 1, new(5, 5)).AddCharacter("ally", "nigel", 1, new(6, 5))
        .AddCharacter("enemy", "time_weaver", 2, new(9, 9)).WithHealth("ally", 30).ActiveCharacter("guardian").Build();
    private static AbilityTargetingConstraints AllyRules() => new(AbilityTargetType.Character, false, AbilityTargetTeam.Ally, true, true, false, false);
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PawnsOfDivinity.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository's report fixtures are required for this diagnostic.");
    }
}
