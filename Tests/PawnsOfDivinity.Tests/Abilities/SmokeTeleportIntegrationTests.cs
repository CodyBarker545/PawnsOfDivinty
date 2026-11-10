using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SmokeTeleportIntegrationTests
{
    [Fact]
    public void ActiveAssassinWithLegalDestination_IsExplicitlyBlockedByMissingActionCost()
    {
        var s = new Setup();
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.True(preview.TeleportValidation!.IsLegal);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, preview.RejectionReason);
        Assert.Equal(s.Actor.Id, preview.CharacterId);
        Assert.Equal(2, preview.RoundNumber);
        Assert.Equal(new GridPosition(5, 5), preview.SourcePosition);
        Assert.Equal(new GridPosition(6, 5), preview.DestinationPosition);
        var execution = s.Coordinator.Execute(s.Actor, preview, s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, execution.RejectionReason);
        Assert.False(execution.Succeeded);
        Assert.False(execution.PositionChanged);
        Assert.Equal(before, Snapshot(s));
    }

    [Theory]
    [InlineData("inactive", SmokeTeleportActionRejectionReason.CharacterNotActive)]
    [InlineData("dead", SmokeTeleportActionRejectionReason.CasterDefeated)]
    [InlineData("notAssassin", SmokeTeleportActionRejectionReason.NotAssassin)]
    [InlineData("ended", SmokeTeleportActionRejectionReason.CharacterNotActive)]
    [InlineData("acted", SmokeTeleportActionRejectionReason.ActionUnavailable)]
    public void ActivationRejections_DoNotQueryTeleportMapOrChangeState(string invalid,
        SmokeTeleportActionRejectionReason expected)
    {
        var s = new Setup(actor: invalid == "notAssassin" ? CharacterFactory.Create("nigel", 1)! : null);
        switch (invalid)
        {
            case "inactive": s.Activations.CompleteActivation(); break;
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "acted": s.Actor.EndActivation(); break;
        }
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.Equal(expected, preview.RejectionReason);
        Assert.Null(preview.TeleportValidation);
        var execution = s.Coordinator.Execute(s.Actor, preview, s.Map);
        Assert.Equal(expected, execution.RejectionReason);
        Assert.Equal(0, s.Map.Queries);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void DifferentActiveAssassin_CannotUseAnotherCharactersActivation()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("hooded_assassin", 1)!;
        other.SetTacticalPosition(new(5, 5));
        var result = s.Coordinator.Preview(other, new(6, 5), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.CharacterNotActive, result.RejectionReason);
        Assert.Same(s.Actor, s.Activations.ActiveCharacter);
        Assert.Equal(new GridPosition(5, 5), other.TacticalPosition);
        Assert.Empty(other.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.Cleanup)]
    public void ConnectedLifecycle_RequiresActivationPhase(RoundPhase phase)
    {
        var s = new Setup(phase: phase);
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.WrongPhase, preview.RejectionReason);
        Assert.Equal(SmokeTeleportActionRejectionReason.WrongPhase, s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        Assert.Equal(0, s.Map.Queries);
        Assert.Equal(before, Snapshot(s));
    }

    [Theory]
    [InlineData("invalidSource", SmokeTeleportRejectionReason.InvalidSourcePosition)]
    [InlineData("invalidDestination", SmokeTeleportRejectionReason.InvalidDestinationPosition)]
    [InlineData("occupied", SmokeTeleportRejectionReason.DestinationOccupied)]
    [InlineData("illegal", SmokeTeleportRejectionReason.IllegalDestination)]
    [InlineData("outsideSource", SmokeTeleportRejectionReason.SourceOutsideSmoke)]
    [InlineData("outsideDestination", SmokeTeleportRejectionReason.DestinationOutsideSmoke)]
    [InlineData("expired", SmokeTeleportRejectionReason.SourceOutsideSmoke)]
    [InlineData("missingMapData", SmokeTeleportRejectionReason.MissingMapInformation)]
    [InlineData("missingPosition", SmokeTeleportRejectionReason.MissingSourcePosition)]
    [InlineData("missingDestination", SmokeTeleportRejectionReason.MissingDestinationPosition)]
    public void TeleportLegality_ReusesExistingReasonsBeforeTheCostGate(string invalid,
        SmokeTeleportRejectionReason reason)
    {
        var s = new Setup(setPosition: invalid != "missingPosition");
        GridPosition? destination = new(6, 5);
        switch (invalid)
        {
            case "invalidSource": s.Map.Valid = p => p != new GridPosition(5, 5); break;
            case "invalidDestination": s.Map.Valid = p => p != new GridPosition(6, 5); break;
            case "occupied": s.Map.Occupied = true; break;
            case "illegal": s.Map.Legal = false; break;
            case "outsideSource": s.Actor.SetTacticalPosition(new(1, 1)); break;
            case "outsideDestination": destination = new(10, 10); break;
            case "expired": s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup); break;
            case "missingMapData": s.Map.Legal = null; break;
            case "missingDestination": destination = null; break;
        }
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, destination, s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.TeleportIllegal, preview.RejectionReason);
        Assert.Equal(reason, preview.TeleportValidation!.RejectionReason);
        Assert.Equal(reason, s.Coordinator.Execute(s.Actor, preview, s.Map).TeleportValidation!.RejectionReason);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void MissingMap_IsReportedWithoutAnyPositionOrBudgetChange()
    {
        var s = new Setup();
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), null);
        Assert.Equal(SmokeTeleportRejectionReason.MissingMapInformation, preview.TeleportValidation!.RejectionReason);
        Assert.Equal(SmokeTeleportRejectionReason.MissingMapInformation,
            s.Coordinator.Execute(s.Actor, preview, null).TeleportValidation!.RejectionReason);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void Execution_RechecksOccupancyInsteadOfTrustingPreview()
    {
        var s = new Setup();
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.True(preview.TeleportValidation!.IsLegal);
        int previousQueries = s.Map.Queries;
        s.Map.Occupied = true;
        var before = Snapshot(s);
        var result = s.Coordinator.Execute(s.Actor, preview, s.Map);
        Assert.Equal(SmokeTeleportRejectionReason.DestinationOccupied, result.TeleportValidation!.RejectionReason);
        Assert.True(s.Map.Queries > previousQueries);
        Assert.Equal(before, Snapshot(s));
        Assert.True(preview.TeleportValidation.IsLegal);
    }

    [Fact]
    public void Execution_RechecksExpiredSmokeAndActivationAvailability()
    {
        var s = new Setup();
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(SmokeTeleportRejectionReason.SourceOutsideSmoke,
            s.Coordinator.Execute(s.Actor, preview, s.Map).TeleportValidation!.RejectionReason);
        s.Activations.CompleteActivation();
        Assert.Equal(SmokeTeleportActionRejectionReason.CharacterNotActive,
            s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        Assert.Equal(new GridPosition(5, 5), s.Actor.TacticalPosition);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void Execution_RejectsPreviewFromAnotherSourcePosition()
    {
        var s = new Setup();
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        s.Actor.SetTacticalPosition(new(5, 6));
        var before = Snapshot(s);
        var result = s.Coordinator.Execute(s.Actor, preview, s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.SourcePositionChanged, result.RejectionReason);
        Assert.Equal(before, Snapshot(s));
        Assert.Equal(new GridPosition(5, 5), preview.SourcePosition);
    }

    [Fact]
    public void Execution_RejectsChangedRoundAndWrongPhase()
    {
        var s = new Setup();
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        s.Lifecycle.AdvancePhase();
        Assert.Equal(SmokeTeleportActionRejectionReason.WrongPhase, s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        var before = Snapshot(s);
        Assert.Equal(SmokeTeleportActionRejectionReason.RoundChanged, s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        Assert.Equal(before, Snapshot(s));
    }

    [Theory]
    [InlineData("crossCloud", SmokeTeleportRejectionReason.UnresolvedCrossAreaRule)]
    [InlineData("owner", SmokeTeleportRejectionReason.UnresolvedOwnerRule)]
    [InlineData("distance", SmokeTeleportRejectionReason.UnresolvedDistanceLimit)]
    [InlineData("same", SmokeTeleportRejectionReason.UnresolvedSamePositionRule)]
    public void UnresolvedTeleportRestrictions_StayExplicit(string unresolved, SmokeTeleportRejectionReason reason)
    {
        var s = new Setup();
        GridPosition destination = new(6, 5);
        SmokeTeleportConstraints? constraints = null;
        if (unresolved == "crossCloud" || unresolved == "distance")
        {
            s.Smoke.Place("far", s.Actor.Id, new(20, 20), 2, _ => true);
            destination = new(20, 20);
            if (unresolved == "distance") constraints = new(allowCrossArea: true);
        }
        if (unresolved == "owner")
        {
            s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
            s.Smoke.Place("other", s.Enemy.Id, new(5, 5), 2, _ => true);
        }
        if (unresolved == "same") destination = new(5, 5);
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, destination, s.Map, constraints);
        Assert.Equal(reason, preview.TeleportValidation!.RejectionReason);
        Assert.Equal(reason, s.Coordinator.Execute(s.Actor, preview, s.Map).TeleportValidation!.RejectionReason);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void ProvisionalCrossCloudPermission_DoesNotGrantFreeRepeatedTeleports()
    {
        var s = new Setup();
        s.Smoke.Place("far", s.Enemy.Id, new(20, 20), 2, _ => true);
        // Explicit prototype permissions exercise legality only; none establishes an action cost.
        var constraints = new SmokeTeleportConstraints(allowCrossArea: true, allowOtherOwners: true, maximumDistance: 40);
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(20, 20), s.Map, constraints);
        Assert.True(preview.TeleportValidation!.IsLegal);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var result = s.Coordinator.Execute(s.Actor, preview, s.Map);
            Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, result.RejectionReason);
            Assert.False(result.Succeeded);
        }
        Assert.Equal(before, Snapshot(s));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BudgetSnapshots_UseTheExistingManagerWithoutRecordingAnotherMovementFlag(bool utility)
    {
        var s = new Setup();
        if (utility) s.Activations.RecordUtilityAction();
        else s.Activations.RecordMovement();
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        var result = s.Coordinator.Execute(s.Actor, preview, s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, result.RejectionReason);
        Assert.Equal(!utility, result.MovementUsed);
        Assert.Equal(utility, result.UtilityActionUsed);
        Assert.False(result.MainActionUsed);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void SmokeBombAlreadyEndedActivation_DoesNotEnableAnExtraTeleport()
    {
        var s = new Setup();
        var use = new AbilityUse(s.Actor, "smoke_bomb", s.Activations);
        Assert.True(new SmokeBombAbilityExecutor(s.Smoke).Execute(use, "cast", _ => true, new(6, 5), s.Map,
            new[] { (s.Enemy, s.Enemy.TacticalPosition!.Value) }).Succeeded);
        var before = Snapshot(s);
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.CharacterNotActive, preview.RejectionReason);
        Assert.Equal(SmokeTeleportActionRejectionReason.CharacterNotActive, s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        Assert.Equal(5, s.Actor.Cooldowns.Cooldowns["smoke_bomb"].ReadyRound);
        Assert.Equal(before, Snapshot(s));
    }

    [Fact]
    public void Rejection_PreservesRevealVisibilityEvasionAndAllOtherRuntimeState()
    {
        var s = new Setup();
        s.Actor.SmokeReveal.RecordAttack();
        s.Actor.TakeDamage(5);
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Actor.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Actor);
        s.Actor.Cooldowns.RecordUse("other", 2, 3);
        var stats = s.Actor.BaseStats;
        var areas = s.Smoke.ActiveAreas.ToArray();
        var before = Snapshot(s);
        var visibility = s.Visibility();
        var preview = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, s.Coordinator.Execute(s.Actor, preview, s.Map).RejectionReason);
        Assert.Equal(before, Snapshot(s));
        Assert.Equal(visibility, s.Visibility());
        Assert.False(s.Visibility().IsInvisible);
        Assert.True(s.Actor.SmokeReveal.RevealedByAttack);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(s.Actor, s.Actor.TacticalPosition!.Value, s.Smoke));
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.True(s.Actor.Statuses.IsPoisoned);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(areas, s.Smoke.ActiveAreas);
    }

    [Fact]
    public void IndependentCharactersAndPreviewContexts_CannotBeInterchanged()
    {
        var first = new Setup();
        var second = new Setup();
        var firstPreview = first.Coordinator.Preview(first.Actor, new(6, 5), first.Map);
        var secondPreview = second.Coordinator.Preview(second.Actor, new(6, 5), second.Map);
        var firstBefore = Snapshot(first);
        var secondBefore = Snapshot(second);
        Assert.Equal(SmokeTeleportActionRejectionReason.InvalidPreviewContext,
            second.Coordinator.Execute(second.Actor, firstPreview, second.Map).RejectionReason);
        Assert.Equal(SmokeTeleportActionRejectionReason.InvalidPreviewContext,
            first.Coordinator.Execute(second.Actor, firstPreview, second.Map).RejectionReason);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost,
            second.Coordinator.Execute(second.Actor, secondPreview, second.Map).RejectionReason);
        Assert.Equal(firstBefore, Snapshot(first));
        Assert.Equal(secondBefore, Snapshot(second));
    }

    [Fact]
    public void Results_AreImmutableAndRetainOriginalPositionAndBudgetSnapshots()
    {
        var s = new Setup();
        var result = s.Coordinator.Preview(s.Actor, new(6, 5), s.Map);
        s.Actor.SetTacticalPosition(new(5, 6));
        s.Activations.RecordMovement();
        Assert.Equal(new GridPosition(5, 5), result.SourcePosition);
        Assert.Equal(new GridPosition(5, 5), result.ResultingPosition);
        Assert.False(result.MovementUsed);
        Assert.All(typeof(SmokeTeleportActionResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void InvalidProgrammingInputs_ThrowWithoutChangingState()
    {
        var s = new Setup();
        var before = Snapshot(s);
        Assert.Throws<ArgumentNullException>(() => new SmokeTeleportActionCoordinator(null!, s.Activations));
        Assert.Throws<ArgumentNullException>(() => new SmokeTeleportActionCoordinator(s.Service, null!));
        Assert.Throws<ArgumentNullException>(() => s.Coordinator.Preview(null!, new(6, 5), s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Coordinator.Execute(s.Actor, null!, s.Map));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmokeTeleportConstraints(maximumDistance: -1));
        Assert.Equal(before, Snapshot(s));
    }

    private static string Snapshot(Setup s) => string.Join("|", new object?[]
    {
        s.Actor.Id, s.Actor.TacticalPosition, s.Actor.CurrentHealth, s.Actor.ShieldHP,
        s.Actor.HasActedThisRound, s.Actor.HealsReceivedThisRound, s.Actor.SmokeReveal.RevealedByAttack,
        s.Activations.ActiveCharacter?.Id, s.Activations.MovementUsed, s.Activations.UtilityActionUsed,
        s.Activations.MainActionUsed, s.Actor.Cooldowns.Cooldowns.Count,
        string.Join(",", s.Smoke.ActiveAreas.Select(a => $"{a.Id}:{a.OwnerCharacterId}:{a.ExpirationRound}"))
    });

    private sealed class Setup
    {
        public Character Actor { get; }
        public Character Enemy { get; } = CharacterFactory.Create("nigel", 2)!;
        public SmokePlacementService Smoke { get; } = new();
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Activations { get; }
        public SmokeTeleportService Service { get; }
        public SmokeTeleportActionCoordinator Coordinator { get; }
        public Map Map { get; } = new();

        public Setup(Character? actor = null, bool setPosition = true,
            RoundPhase phase = RoundPhase.AlternatingActivations)
        {
            Actor = actor ?? CharacterFactory.Create("hooded_assassin", 1)!;
            if (setPosition) Actor.SetTacticalPosition(new(5, 5));
            Enemy.SetTacticalPosition(new(10, 10));
            while (Lifecycle.CurrentPhase != phase) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Enemy }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Smoke.Place("own", Actor.Id, new(5, 5), 2, _ => true);
            Service = new(Smoke);
            Coordinator = new(Service, Activations);
        }

        public SmokeVisibilityResult Visibility() => SmokeVisibility.Evaluate(Actor, Actor.TacticalPosition!.Value,
            Smoke, new[] { (Enemy, Enemy.TacticalPosition!.Value) });
    }

    private sealed class Map : ISmokeTeleportMap
    {
        public Func<GridPosition, bool?> Valid { get; set; } = _ => true;
        public bool? Legal { get; set; } = true;
        public bool? Occupied { get; set; } = false;
        public int Queries { get; private set; }
        public bool? IsValidPosition(GridPosition position) { Queries++; return Valid(position); }
        public bool? CanTeleportTo(GridPosition position) { Queries++; return Legal; }
        public bool? IsOccupied(GridPosition position) { Queries++; return Occupied; }
    }
}
