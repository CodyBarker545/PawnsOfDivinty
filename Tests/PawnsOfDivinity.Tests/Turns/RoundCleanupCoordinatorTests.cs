using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class RoundCleanupCoordinatorTests
{
    [Fact]
    public void EmptyCleanupIsAValidProductionOperationAndDoesNotAdvancePhase()
    {
        var s = new Setup(); s.ToCleanup();
        var result = s.Cleanup.Process(1);
        Assert.Equal(RoundCleanupStatus.Completed, result.Status);
        Assert.Equal(1, result.RequestedRound); Assert.Equal(1, result.LifecycleRound);
        Assert.Equal(RoundPhase.Cleanup, result.Phase); Assert.Equal(RoundPhase.Cleanup, s.Life.CurrentPhase);
        Assert.Equal(s.Roster.Length + 5, result.Services.Count);
        Assert.Equal(s.Roster.Length + 5, result.ServicesInvokedThisRequest);
        Assert.All(result.Services, step => { Assert.Equal(CleanupServiceStatus.Completed, step.Status); Assert.Empty(step.Effects!); Assert.Equal(0, step.ExpiredOrRetiredCount); });
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.PoisonBatch)]
    [InlineData(RoundPhase.DelayedAttacks)]
    [InlineData(RoundPhase.TimedStatusUpdate)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void WrongPhaseDoesNotConsumeTheCleanupAttempt(RoundPhase phase)
    {
        var s = new Setup(); s.AdvanceTo(phase);
        var result = s.Cleanup.Process(1);
        Assert.Equal(RoundCleanupStatus.RejectedWrongPhase, result.Status); Assert.Empty(result.Services);
        Assert.Equal(phase, s.Life.CurrentPhase); Assert.Equal(44, s.Guardian.CurrentHealth);
        if (phase < RoundPhase.Cleanup) { s.ToCleanup(); Assert.Equal(RoundCleanupStatus.Completed, s.Cleanup.Process(1).Status); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void InvalidRequestedRoundDoesNotConsumeTheAttempt(int round)
    {
        var s = new Setup(); s.ToCleanup();
        Assert.Equal(RoundCleanupStatus.RejectedInvalidRound, s.Cleanup.Process(round).Status);
        Assert.Equal(RoundCleanupStatus.Completed, s.Cleanup.Process(1).Status);
    }

    [Fact]
    public void ActivationReadinessIsCheckedBeforeAnyExpiration()
    {
        var s = new Setup(); s.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(s.Guardian, 1, 1) });
        s.AdvanceTo(RoundPhase.Cleanup);
        Assert.Equal(RoundCleanupStatus.RejectedActivationsIncomplete, s.Cleanup.Process(1).Status);
        Assert.Single(s.Guardian.TemporaryModifiers.Active);
        s.ToCleanup(); Assert.Equal(RoundCleanupStatus.Completed, s.Cleanup.Process(1).Status);
        Assert.Empty(s.Guardian.TemporaryModifiers.Active);
    }

    [Fact]
    public void LifecycleOwnsOneCoordinatorAndReplacementCannotBypassItsGuard()
    {
        var s = new Setup();
        Assert.Same(s.Cleanup, s.Life.CleanupCoordinator);
        Assert.Throws<InvalidOperationException>(() => s.CreateCoordinator());
        s.ToCleanup(); Assert.Equal(RoundCleanupStatus.Completed, s.Cleanup.Process(1).Status);
        Assert.Throws<InvalidOperationException>(() => s.CreateCoordinator());
        Assert.Equal(RoundCleanupStatus.AlreadyProcessed, s.Life.CleanupCoordinator!.Process(1).Status);
    }

    [Theory]
    [InlineData("lifecycle")]
    [InlineData("activations")]
    [InlineData("walls")]
    [InlineData("ice")]
    [InlineData("smoke")]
    [InlineData("sentinel")]
    public void NullDependenciesAreRejectedWithoutRegistration(string missing)
    {
        var s = new Setup(register: false);
        Assert.Throws<ArgumentNullException>(() => new RoundCleanupCoordinator(missing == "lifecycle" ? null! : s.Life,
            missing == "activations" ? null! : s.Activations, missing == "walls" ? null! : s.Walls,
            missing == "ice" ? null! : s.Ice, missing == "smoke" ? null! : s.Smoke, missing == "sentinel" ? null! : s.Sentinel));
        Assert.Null(s.Life.CleanupCoordinator); Assert.NotNull(s.CreateCoordinator());
    }

    [Fact]
    public void MismatchedLifecycleAndSentinelAreRejectedBeforeRegistration()
    {
        var first = new Setup(register: false); var second = new Setup();
        Assert.Throws<ArgumentException>(() => new RoundCleanupCoordinator(first.Life, second.Activations, first.Walls, first.Ice, first.Smoke, first.Sentinel));
        Assert.Throws<ArgumentException>(() => new RoundCleanupCoordinator(first.Life, first.Activations, first.Walls, first.Ice, first.Smoke, second.Sentinel));
        Assert.Null(first.Life.CleanupCoordinator); Assert.NotNull(first.CreateCoordinator());
    }

    [Fact]
    public void TemporaryModifiersExpireOnlyWhenTheirOwnDeadlineIsDue()
    {
        var s = new Setup();
        s.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(s.Guardian, 1, 1) });
        s.Tank.TemporaryModifiers.AddRange(new[] { Bonus(s.Tank, 1, 2) });
        s.ToCleanup(); var first = s.Cleanup.Process(1);
        Assert.Contains(first.Services.SelectMany(step => step.Effects!), e => e.Id == "bonus_guardian");
        Assert.DoesNotContain(first.Services.SelectMany(step => step.Effects!), e => e.Id == "bonus_tank");
        Assert.Equal(11, s.Guardian.EffectiveDefense); Assert.Equal(24, s.Tank.EffectiveDefense);
        s.NextRound(); s.ToCleanup(); var second = s.Cleanup.Process(2);
        Assert.Contains(second.Services.SelectMany(step => step.Effects!), e => e.Id == "bonus_tank");
        Assert.Equal(22, s.Tank.EffectiveDefense); Assert.Equal(22, s.Tank.BaseStats.Defense);
    }

    [Fact]
    public void ActualCastsExpireTauntAndSentinelThisRoundButKeepTerrainAndBlessing()
    {
        var s = new Setup(); s.CastEffects(); s.ToCleanup(); var result = s.Cleanup.Process(1);
        Assert.Equal(RoundCleanupStatus.Completed, result.Status);
        Assert.Single(result.Services.Single(step => step.Service == RoundCleanupService.Taunts).Effects!);
        var retired = Assert.Single(result.Services.Single(step => step.Service == RoundCleanupService.Sentinel).Effects!);
        Assert.Equal("sentinel", retired.Id); Assert.Equal(SentinelPreparationStatus.Expired, retired.SentinelStatus);
        Assert.Single(s.Walls.ActiveAreas); Assert.Single(s.Ice.ActiveAreas); Assert.Single(s.Smoke.ActiveAreas);
        Assert.Equal(2, s.Tank.TemporaryModifiers.Active.Count); Assert.Equal(24, s.Tank.EffectiveDefense);
        Assert.Null(s.Activations.ActionRestrictions.GetActive(s.Enemy.Id));
    }

    [Fact]
    public void FollowingRoundExpiresActualWallIceSmokeAndBothBlessingModifiers()
    {
        var s = new Setup(); s.CastEffects(); s.ToCleanup(); s.Cleanup.Process(1);
        s.NextRound(); s.ToCleanup(); var result = s.Cleanup.Process(2);
        Assert.Equal("wall", Assert.Single(result.Services.Single(step => step.Service == RoundCleanupService.WailingRampart).Effects!).Id);
        Assert.Equal("ice", Assert.Single(result.Services.Single(step => step.Service == RoundCleanupService.SpiritFrost).Effects!).Id);
        Assert.Equal("smoke", Assert.Single(result.Services.Single(step => step.Service == RoundCleanupService.Smoke).Effects!).Id);
        Assert.Equal(2, result.Services.Single(step => step.Service == RoundCleanupService.TemporaryModifiers && step.CharacterId == s.Tank.Id).Effects!.Count);
        Assert.Empty(s.Walls.ActiveAreas); Assert.Empty(s.Ice.ActiveAreas); Assert.Empty(s.Smoke.ActiveAreas);
        Assert.Empty(s.Tank.TemporaryModifiers.Active); Assert.Equal(9, s.Tank.EffectiveResistance);
    }

    [Fact]
    public void DefeatedCreatorsDoNotRemovePersistentTerrainEarly()
    {
        var s = new Setup(); s.CastEffects();
        s.Tank.TakeDamage(int.MaxValue); s.Archer.TakeDamage(int.MaxValue); s.Assassin.TakeDamage(int.MaxValue);
        s.ToCleanup(); var first = s.Cleanup.Process(1);
        Assert.Equal(RoundCleanupStatus.Completed, first.Status);
        Assert.Single(s.Walls.ActiveAreas); Assert.Single(s.Ice.ActiveAreas); Assert.Single(s.Smoke.ActiveAreas);
        s.NextRound(); s.ToCleanup(); s.Cleanup.Process(2);
        Assert.Empty(s.Walls.ActiveAreas); Assert.Empty(s.Ice.ActiveAreas); Assert.Empty(s.Smoke.ActiveAreas);
        Assert.True(s.Tank.IsDefeated); Assert.True(s.Archer.IsDefeated); Assert.True(s.Assassin.IsDefeated);
    }

    [Fact]
    public void DuplicateRequestCannotExpireAnEffectAddedAfterSuccessfulCleanup()
    {
        var s = new Setup(2); s.ToCleanup(); var first = s.Cleanup.Process(2);
        // Probe the guard using a public service that can accept an out-of-band already-due fixture.
        s.Smoke.Place("late_smoke", s.Assassin.Id, s.Assassin.TacticalPosition!.Value, 1, p => s.Map.IsValidPosition(p) == true);
        var duplicate = s.Cleanup.Process(2);
        Assert.Equal(RoundCleanupStatus.AlreadyProcessed, duplicate.Status);
        Assert.Equal(RoundCleanupStatus.Completed, duplicate.PreviousAttemptStatus);
        Assert.Equal(0, duplicate.ServicesInvokedThisRequest);
        Assert.Single(s.Smoke.ActiveAreas);
        Assert.Equal(first.Services, duplicate.Services); // Original evidence, not newly executed operations.
    }

    [Fact]
    public void ANewRoundCanProcessNormallyAndOldRoundRequestsCannotExecuteThere()
    {
        var s = new Setup(); s.ToCleanup(); s.Cleanup.Process(1); s.NextRound(); s.ToCleanup();
        Assert.Equal(RoundCleanupStatus.RejectedInvalidRound, s.Cleanup.Process(1).Status);
        Assert.Equal(RoundCleanupStatus.Completed, s.Cleanup.Process(2).Status);
        Assert.Equal(2, s.Life.CurrentRound);
    }

    [Fact]
    public void DifferentMatchesHaveIndependentGuardsAndServices()
    {
        var a = new Setup(); var b = new Setup();
        a.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(a.Guardian, 1, 1) });
        b.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(b.Guardian, 1, 1) });
        a.ToCleanup(); b.ToCleanup(); a.Cleanup.Process(1);
        Assert.Single(b.Guardian.TemporaryModifiers.Active); Assert.Equal(RoundCleanupStatus.Completed, b.Cleanup.Process(1).Status);
        Assert.NotSame(a.Cleanup, b.Cleanup); Assert.NotSame(a.Life, b.Life);
    }

    [Fact]
    public void FailureAfterACompletedServicePreservesEffectsAndReportsUnstartedWork()
    {
        var s = FaultedSetup(); var result = s.Cleanup.Process(2);
        Assert.Equal(RoundCleanupStatus.PartiallyApplied, result.Status); Assert.True(result.RequiresManualResolution);
        Assert.Equal(2, result.ServicesInvokedThisRequest);
        Assert.Empty(s.Guardian.TemporaryModifiers.Active); // Earlier expiration remains applied.
        var failed = Assert.Single(result.Services, step => step.Status == CleanupServiceStatus.Failed);
        Assert.Equal(s.Tank.Id, failed.CharacterId); Assert.Equal(nameof(ArgumentOutOfRangeException), failed.FailureType);
        Assert.Null(failed.Effects); Assert.Null(failed.ExpiredOrRetiredCount); Assert.True(failed.MutationMayHaveOccurred);
        Assert.Contains(result.Services, step => step.Status == CleanupServiceStatus.NotStarted && step.Service == RoundCleanupService.Smoke);
        Assert.Single(s.Smoke.ActiveAreas); // Due, but never invoked after the failure.
        Assert.Equal(RoundPhase.Cleanup, s.Life.CurrentPhase);
    }

    [Fact]
    public void PartialFailureCannotRetryEarlierOrLaterOperations()
    {
        var s = FaultedSetup(); var first = s.Cleanup.Process(2);
        s.Guardian.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("late", s.Guardian.Id, s.Guardian.Id, TemporaryStat.Defense, 2, 3, 3) });
        var repeat = s.Cleanup.Process(2);
        Assert.Equal(RoundCleanupStatus.ManualResolutionRequired, repeat.Status);
        Assert.Equal(0, repeat.ServicesInvokedThisRequest);
        Assert.Equal(RoundCleanupStatus.PartiallyApplied, repeat.PreviousAttemptStatus); Assert.Equal(2, repeat.PreviousAttemptRound);
        Assert.Equal(first.Services, repeat.Services); Assert.Single(s.Guardian.TemporaryModifiers.Active); Assert.Single(s.Smoke.ActiveAreas);
        s.NextRound(); s.ToCleanup();
        var later = s.Cleanup.Process(3);
        Assert.Equal(RoundCleanupStatus.ManualResolutionRequired, later.Status); Assert.Equal(2, later.PreviousAttemptRound);
        Assert.Single(s.Guardian.TemporaryModifiers.Active); Assert.Single(s.Smoke.ActiveAreas);
    }

    [Fact]
    public void FirstServiceFailureDoesNotPretendItWasNonMutatingOrCompleted()
    {
        var s = new Setup(2);
        s.Guardian.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup); // Simulates external misuse of an existing public service API.
        s.ToCleanup(); var result = s.Cleanup.Process(2);
        Assert.Equal(RoundCleanupStatus.Failed, result.Status); Assert.True(result.RequiresManualResolution);
        Assert.Equal(CleanupServiceStatus.Failed, result.Services[0].Status); Assert.True(result.Services[0].MutationMayHaveOccurred);
        Assert.All(result.Services.Skip(1), step => Assert.Equal(CleanupServiceStatus.NotStarted, step.Status));
        Assert.Equal(RoundCleanupStatus.ManualResolutionRequired, s.Cleanup.Process(2).Status);
    }

    [Fact]
    public void ResultsAreReadOnlyAndDoNotChangeAfterLaterRounds()
    {
        var s = new Setup(); s.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(s.Guardian, 1, 1) }); s.ToCleanup();
        var result = s.Cleanup.Process(1); var json = JsonSerializer.Serialize(result);
        Assert.Throws<NotSupportedException>(() => ((IList<CleanupServiceResult>)result.Services).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CleanupEffect>)result.Services[0].Effects!).Clear());
        s.NextRound(); s.ToCleanup(); s.Cleanup.Process(2);
        Assert.Equal(json, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void CleanupDoesNotTickStatusesResetHealingOrChangeHealthShieldsCooldownsAndActivation()
    {
        var s = new Setup(); var g = s.Guardian;
        g.TakeDamage(20); HealingApplicator.Apply(1, g); g.SetShieldHP(6);
        g.Statuses.ApplyTimedStatus(StatusType.Burn, 2); g.Statuses.ApplyPoison();
        g.Cooldowns.RecordUse("healing_light", 1, 0); var stats = g.BaseStats;
        s.Enemy.TakeDamage(int.MaxValue); s.ToCleanup();
        int hp = g.CurrentHealth, history = g.HealsReceivedThisRound; bool acted = g.HasActedThisRound;
        s.Cleanup.Process(1);
        Assert.Equal(hp, g.CurrentHealth); Assert.Equal(6, g.ShieldHP); Assert.Equal(history, g.HealsReceivedThisRound);
        Assert.Equal(2, g.Statuses.TimedStatuses[StatusType.Burn]); Assert.Equal(0, g.Statuses.PoisonTicksOccurred);
        Assert.True(g.Statuses.IsPoisoned); Assert.Equal(acted, g.HasActedThisRound); Assert.Same(stats, g.BaseStats);
        Assert.Equal(1, g.Cooldowns.Cooldowns["healing_light"].ReadyRound); Assert.True(s.Enemy.IsDefeated);
    }

    private static Setup FaultedSetup()
    {
        var s = new Setup(2);
        s.Guardian.TemporaryModifiers.AddRange(new[] { Bonus(s.Guardian, 1, 2) });
        // No mock effects: a real service rejects backwards cleanup after a deliberately out-of-band future call.
        s.Tank.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        s.Smoke.Place("due_smoke", s.Assassin.Id, s.Assassin.TacticalPosition!.Value, 1, p => s.Map.IsValidPosition(p) == true);
        s.ToCleanup(); return s;
    }
    private static TemporaryStatModifier Bonus(Character c, int used, int expires)
        => new("bonus_" + c.Id, c.Id, c.Id, TemporaryStat.Defense, 2, used, expires);

    private sealed class Setup
    {
        public RoundLifecycleManager Life { get; }
        public ActivationManager Activations { get; }
        public WailingRampartService Walls { get; } = new();
        public SpiritFrostService Ice { get; } = new();
        public SmokePlacementService Smoke { get; } = new();
        public ImpalingSentinelTracker Sentinel { get; }
        public RoundCleanupCoordinator Cleanup => Life.CleanupCoordinator!;
        public Character Guardian { get; } = Character("gentle_guardian", "guardian", 1, new(3, 4));
        public Character Tank { get; } = Character("gravebound_colossus", "tank", 1, new(5, 5));
        public Character Sentry { get; } = Character("gravebound_colossus", "sentry", 1, new(11, 5));
        public Character Archer { get; } = Character("beothuk_spirit_guardian", "archer", 1, new(9, 9));
        public Character Nigel { get; } = Character("nigel", "nigel", 1, new(5, 3));
        public Character Assassin { get; } = Character("hooded_assassin", "assassin", 1, new(3, 9));
        public Character Enemy { get; } = Character("time_weaver", "enemy", 2, new(6, 3));
        public Character[] Roster { get; }
        public FlatMap Map { get; }
        public Setup(int round = 1, bool register = true)
        {
            Life = new(round); Roster = new[] { Guardian, Tank, Sentry, Archer, Nigel, Assassin, Enemy };
            Activations = new(Roster.Take(6), new[] { Enemy }, 1, Life); Sentinel = new(Activations, Life); Map = new(Roster);
            if (register) CreateCoordinator();
        }
        public RoundCleanupCoordinator CreateCoordinator() => new(Life, Activations, Walls, Ice, Smoke, Sentinel);
        public void AdvanceTo(RoundPhase phase) { while (Life.CurrentPhase < phase) Life.AdvancePhase(); }
        public void ToCleanup() { foreach (var c in Roster) c.EndActivation(); AdvanceTo(RoundPhase.Cleanup); }
        public void NextRound() { AdvanceTo(RoundPhase.ObjectiveScoring); Life.StartNextRound(); }
        public void CastEffects()
        {
            AdvanceTo(RoundPhase.AlternatingActivations);
            Activations.StartActivation(Nigel);
            var rules = new AbilityTargetingConstraints(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, true, false, false);
            Assert.True(new ProvokingRoarAbilityExecutor().Execute(new(Nigel, "provoking_roar", Activations), rules,
                Roster.ToDictionary(c => c.Id, _ => true), "roar").Succeeded);
            Activations.StartActivation(Enemy);
            // This fixture supplies no available enemy attack; it explicitly confirms the no-attack fallback.
            Assert.True(Activations.EndActivationForTaunt(Enemy, new(Nigel.Id, true, true, false)).ActivationEnded);
            Tank.TakeDamage(1); Activations.StartActivation(Guardian);
            Assert.True(new HealingLightAbilityExecutor().Execute(new(Guardian, "healing_light", Activations), Tank,
                new(AbilityTargetType.Character, false, AbilityTargetTeam.Ally, true, true, false, false), true).Succeeded);
            Activations.StartActivation(Tank);
            Assert.True(new WailingRampartAbilityExecutor(Walls).Execute(new(Tank, "wailing_rampart", Activations), "wall",
                new[] { new GridPosition(5, 6), new(6, 6), new(7, 6) }, Map).Succeeded);
            Activations.StartActivation(Sentry);
            Assert.True(new ImpalingSentinelAbilityExecutor(Sentinel).Execute(new(Sentry, "impaling_sentinel", Activations), "sentinel",
                new(AttackType.Physical, WeaponCategory.Lance, 3, 100, 1, 1), SentinelReactionLimit.ProvisionalOnePerPreparation).Succeeded);
            Activations.StartActivation(Archer);
            Assert.True(new SpiritFrostAbilityExecutor(Ice).Execute(new(Archer, "spirit_frost", Activations), "ice", new(-1, 0), Map).Succeeded);
            Smoke.Place("smoke", Assassin.Id, Assassin.TacticalPosition!.Value, Life.CurrentRound, p => Map.IsValidPosition(p) == true);
        }
        private static Character Character(string definition, string id, int team, GridPosition position)
        { var c = CharacterFactory.Create(CharacterRegistry.GetCharacter(definition)!, id, team); c.SetTacticalPosition(position); return c; }
    }
    private sealed class FlatMap : IWailingRampartMap, ISpiritFrostMap
    {
        private readonly Character[] _roster;
        public FlatMap(Character[] roster) => _roster = roster;
        public bool? IsValidPosition(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < 14 && p.Y < 14;
        public bool? IsOccupied(GridPosition p) => _roster.Any(c => !c.IsDefeated && c.TacticalPosition == p);
        public bool? CanPlaceWall(Character caster, GridPosition p) => true;
        public bool? CanPlaceIce(Character caster, GridPosition p) => true;
    }
}
