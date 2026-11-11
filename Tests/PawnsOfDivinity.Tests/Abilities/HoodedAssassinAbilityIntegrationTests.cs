using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class HoodedAssassinAbilityIntegrationTests
{
    private static AbilityTargetingConstraints TargetRules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_ContainsOnlyTheThreeDocumentedAssassinAbilities()
    {
        var roster = CharacterRegistry.GetAllCharacters().ToArray();
        Assert.Equal(6, roster.Length);
        var assassin = CharacterRegistry.GetCharacter("hooded_assassin")!;
        Assert.Equal(new[] { "smoke_bomb", "shadow_strike", "smoke_veil" }, assassin.Abilities.Select(a => a.Id));
        Assert.Equal(new[] { "Smoke Bomb", "Shadow Strike", "Smoke Veil" }, assassin.Abilities.Select(a => a.DisplayName));
        Assert.All(roster.Where(c => c.Id != "hooded_assassin"), c =>
            Assert.DoesNotContain(c.Abilities, ability => ability.Id is "smoke_bomb" or "shadow_strike" or "smoke_veil"));
        Assert.DoesNotContain(roster.Single(c => c.Id == "gentle_guardian").Abilities,
            ability => ability.Id is "smoke_bomb" or "shadow_strike" or "smoke_veil");
        foreach (var definition in roster)
        {
            var character = CharacterFactory.Create(definition.Id, 1)!;
            Assert.Same(definition, character.Definition);
            Assert.Same(definition.BaseStats, character.BaseStats);
            Assert.Equal(definition.BaseStats.MaxHealth, character.CurrentHealth);
        }
        Assert.Throws<NotSupportedException>(() => ((IList<AbilityDefinition>)assassin.Abilities).Clear());
    }

    [Fact]
    public void Metadata_PreservesConfirmedValuesAndUnknownSpellTags()
    {
        var abilities = HoodedAssassin.Definition.Abilities;
        var smoke = abilities[0];
        var strike = abilities[1];
        var veil = abilities[2];
        Assert.Equal(AbilityClassification.Active, smoke.Classification);
        Assert.Equal(AbilityActionType.MainAction, smoke.RequiredAction);
        Assert.Equal(3, smoke.ReuseInterval);
        Assert.Equal(0, smoke.Range!.Minimum); // Cast center, not cloud geometry or teleport distance.
        Assert.Equal(0, smoke.Range.Maximum);
        Assert.Equal(AbilityClassification.Active, strike.Classification);
        Assert.Equal(AbilityActionType.MainAction, strike.RequiredAction);
        Assert.Equal(0, strike.ReuseInterval);
        Assert.Equal(AbilityRangeKind.ManhattanDistance, strike.Range!.Kind);
        Assert.Equal(1, strike.Range.Minimum);
        Assert.Equal(1, strike.Range.Maximum);
        Assert.Equal(AbilityClassification.Passive, veil.Classification);
        Assert.Null(veil.RequiredAction);
        Assert.Null(veil.ReuseInterval);
        Assert.Null(veil.Range);
        Assert.All(abilities, a => { Assert.Null(a.IsSpellTagged); Assert.False(string.IsNullOrWhiteSpace(a.Description)); });
    }

    [Fact]
    public void SelectionAndPreview_ConsumeNothing()
    {
        var s = new Setup(true);
        var preview = s.Preview();
        Assert.True(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Null(preview.Strike);
        Assert.Null(preview.Finalization);
        s.AssertUntouched();
        Assert.True(s.Activations.CanMove);
        Assert.True(s.Activations.CanUseUtilityAction);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData(false, 3, 16, 9)]
    [InlineData(true, 5, 18, 8)]
    public void SuccessfulUse_ResolvesWholeExchangeBeforeEndingMainAction(bool invisible, int bonus, int opening, int followUp)
    {
        var s = new Setup(invisible);
        Assert.False(s.CurrentVisibility().IsInvisible); // Both are now adjacent, regardless of the initial snapshot.
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(bonus, result.Strike!.ShadowStrikeBonus);
        Assert.Equal(opening, result.Strike.FinalCalculatedDamage);
        Assert.Equal(opening, result.Strike.ActualHPDamage);
        Assert.Equal(invisible, result.Strike.DisarmApplied);
        Assert.Equal(invisible, s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.Equal(followUp, result.Strike.Exchange!.FollowUpStrike!.Damage);
        Assert.Equal(FollowUpParticipant.Attacker, result.Strike.Exchange.FollowUpBy);
        Assert.True(result.Strike.Exchange.CounterattackOccurred);
        Assert.Equal(invisible ? 80 : 70, result.Strike.Exchange.CounterStrike!.HitChance);
        Assert.Equal(52 - opening - followUp, s.Target.CurrentHealth);
        Assert.Equal(3, s.Random.Calls);
        Assert.True(s.Actor.SmokeReveal.RevealedByAttack);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.False(s.Activations.CanMove);
        Assert.False(s.Activations.CanUseUtilityAction);
        Assert.Equal(1, result.Finalization!.ReadyRound);
        var cooldown = s.Actor.Cooldowns.Cooldowns["shadow_strike"];
        Assert.Equal(0, cooldown.ReuseInterval);
        Assert.Equal(1, cooldown.RoundUsed);
        Assert.Equal(1, cooldown.ReadyRound);
    }

    [Fact]
    public void ShieldsAndImmediateDisarm_UseExistingCombatCalculations()
    {
        var s = new Setup(true);
        s.Target.SetShieldHP(20);
        s.Combat = s.Combat with { CounterattackPower = 100 };
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(18, result.Strike!.ShieldAbsorbed);
        Assert.Equal(0, result.Strike.ActualHPDamage);
        Assert.Equal(46, s.Target.CurrentHealth); // Remaining shield 2 absorbs part of the ordinary 8-damage follow-up.
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Equal(12, result.Strike.Exchange!.CounterStrike!.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.Strike.Exchange.CounterStrike.TriangleOutcome);
    }

    [Fact]
    public void MissedOpening_IsStillCompletedAndRevealsWithoutDisarm()
    {
        var s = new Setup(true, 99, 0, 99);
        s.Combat = s.Combat with { OpeningBaseAccuracy = 0 };
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Strike!.FinalCalculatedDamage);
        Assert.Equal(52, s.Target.CurrentHealth);
        Assert.False(result.Strike.DisarmApplied);
        Assert.True(s.Actor.SmokeReveal.RevealedByAttack);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void LethalCounter_StillFinalizesTheAlreadyStartedMainAction()
    {
        var s = new Setup(false);
        s.Combat = s.Combat with { CounterattackPower = 100 };
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(s.Actor.IsDefeated);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
        Assert.False(result.Strike!.Exchange!.FollowUpOccurred);
        Assert.Equal(2, s.Random.Calls);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, AbilityUseFinalizer.FinalizeSuccessfulUse(s.Use).Eligibility.RejectionReason);
        Assert.False(AbilityEligibility.Evaluate(s.Actor, "shadow_strike", s.Activations).IsAllowed);
    }

    [Fact]
    public void RepeatedExecutionOrFinalization_DoesNotRepeatCombatOrBookkeeping()
    {
        var s = new Setup(true);
        Assert.True(s.Execute().Succeeded);
        int hp = s.Target.CurrentHealth;
        var repeated = s.Execute();
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, repeated.Eligibility.RejectionReason);
        Assert.False(repeated.Succeeded);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, AbilityUseFinalizer.FinalizeSuccessfulUse(s.Use).Eligibility.RejectionReason);
        Assert.Equal(3, s.Random.Calls);
        Assert.Equal(hp, s.Target.CurrentHealth);
    }

    [Fact]
    public void UnrelatedActiveCharacter_DoesNotOwnTheAssassinsAbility()
    {
        var s = new Setup(false, actor: CharacterFactory.Create("nigel", 1)!);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("notActive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("unknownAbility", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("smokeBomb", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("smokeVeil", AbilityRejectionReason.NotManuallyActivatable)]
    [InlineData("usedMainBudget", AbilityRejectionReason.ActionUnavailable)]
    public void AbilityRejections_HappenBeforeCombat(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup(true);
        switch (invalid)
        {
            case "notActive": s.Activations.CompleteActivation(); break;
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse("shadow_strike", 1, 3); break;
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "unknownAbility": s.Use = new(s.Actor, "unknown", s.Activations); break;
            case "smokeBomb": s.Use = new(s.Actor, "smoke_bomb", s.Activations); break;
            case "smokeVeil": s.Use = new(s.Actor, "smoke_veil", s.Activations); break;
            case "usedMainBudget": s.Actor.EndActivation(); break;
        }
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.False(result.Succeeded);
        s.AssertUntouched(expectExistingCooldown: invalid == "cooldown");
    }

    [Theory]
    [InlineData("ally", AbilityTargetingRejectionReason.WrongTeam)]
    [InlineData("dead", AbilityTargetingRejectionReason.TargetDefeated)]
    [InlineData("far", AbilityTargetingRejectionReason.OutsideMaximumRange)]
    [InlineData("noPosition", AbilityTargetingRejectionReason.MissingRequiredTargetInformation)]
    public void IllegalTargets_AreRejectedWithoutBookkeeping(string invalid, AbilityTargetingRejectionReason reason)
    {
        var s = new Setup(true);
        switch (invalid)
        {
            case "ally":
                var ally = CharacterFactory.Create("nigel", 1)!;
                ally.SetTacticalPosition(new(5, 3));
                s.Combat = s.Combat with { Defender = ally };
                break;
            case "dead": s.Target.TakeDamage(int.MaxValue); break;
            case "far": s.Target.SetTacticalPosition(new(9, 3)); break;
            case "noPosition": s.Combat = s.Combat with { Defender = CharacterFactory.Create("nigel", 2)! }; break;
        }
        var result = s.Execute();
        Assert.Equal(reason, result.Targeting!.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(0, s.Random.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.False(s.Actor.SmokeReveal.RevealedByAttack);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("revealChanged")]
    [InlineData("adjacentHidden")]
    public void InvalidOpeningSnapshot_IsRejectedBeforeCombat(string invalid)
    {
        var s = new Setup(true);
        if (invalid == "missing") s.Snapshot = null;
        if (invalid == "expired") s.Smoke.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        if (invalid == "revealChanged") s.Actor.SmokeReveal.RecordAttack();
        if (invalid == "adjacentHidden")
            s.Snapshot = SmokeVisibility.Evaluate(s.Actor, s.Actor.TacticalPosition!.Value, s.Smoke, Array.Empty<(Character, GridPosition)>());
        var result = s.Execute();
        Assert.False(result.CombatEligibility!.IsAllowed);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(52, s.Target.CurrentHealth);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData("power")]
    [InlineData("accuracy")]
    [InlineData("category")]
    [InlineData("counter")]
    [InlineData("fallback")]
    [InlineData("bonusOverflow")]
    public void MalformedCombatInputs_ThrowBeforeAnyEffectOrBookkeeping(string invalid)
    {
        var s = new Setup(true);
        s.Combat = invalid switch
        {
            "power" => s.Combat with { OpeningPower = -1 },
            "accuracy" => s.Combat with { OpeningBaseAccuracy = 101 },
            "category" => s.Combat with { AttackerWeapon = (WeaponCategory)999 },
            "counter" => s.Combat with { CounterattackMaximumRange = -1 },
            "bonusOverflow" => s.Combat with { OpeningPower = int.MaxValue - 8, DefenderWeapon = WeaponCategory.Unarmed },
            _ => s.Combat with { DefenderUnarmedAttack = null }
        };
        if (invalid == "bonusOverflow") Assert.Throws<OverflowException>(() => s.Execute());
        else Assert.ThrowsAny<ArgumentException>(() => s.Execute());
        s.AssertUntouched();
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData(null, AbilityTargetingRejectionReason.UnresolvedTargetingRule)]
    [InlineData(true, AbilityTargetingRejectionReason.UnresolvedLineOfSight)]
    public void UnresolvedTargetingRequirements_RemainExplicit(bool? lineOfSight, AbilityTargetingRejectionReason reason)
    {
        var s = new Setup(true);
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
            true, false, lineOfSight, false);
        var result = s.Executor.Execute(s.Use, s.Combat, s.Snapshot, 1, 1, rules);
        Assert.Equal(reason, result.Targeting!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void ConnectedLifecycle_RejectsWrongPhaseAndStaleRound()
    {
        var lifecycle = new RoundLifecycleManager();
        var s = new Setup(true, lifecycle: lifecycle);
        Assert.Equal(AbilityRejectionReason.WrongPhase, s.Execute().Eligibility.RejectionReason);
        while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
        Assert.True(s.Preview().CanExecute);
        while (lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void CooldownOverflow_IsCheckedBeforeCombat_ForExplicitTestMetadata()
    {
        var original = HoodedAssassin.Definition;
        // Test-only interval exercises the reusable preflight; canonical Shadow Strike remains interval zero.
        var ability = new AbilityDefinition("shadow_strike", "Shadow Strike", "Test only", AbilityClassification.Active,
            false, AbilityActionType.MainAction, new(AbilityRangeKind.ManhattanDistance, 1, 1), int.MaxValue);
        var actor = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, new[] { ability }), "overflow-assassin", 1);
        var s = new Setup(true, actor: actor);
        Assert.Throws<OverflowException>(() => s.Execute());
        s.AssertUntouched();
    }

    [Fact]
    public void IndependentAssassins_ShareDefinitionsButNotCooldownsOrRuntimeState()
    {
        var first = new Setup(true);
        var second = new Setup(true);
        Assert.Same(first.Actor.Definition, second.Actor.Definition);
        first.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var stats = first.Actor.BaseStats;
        var snapshot = first.Snapshot;
        Assert.True(first.Execute().Succeeded);
        Assert.Equal(2, first.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Same(stats, first.Actor.BaseStats);
        Assert.Same(snapshot, first.Snapshot);
        Assert.True(first.Snapshot!.IsInvisible);
        Assert.Equal(0, first.Actor.HealsReceivedThisRound);
        Assert.Single(first.Smoke.ActiveAreas);
        second.AssertUntouched();
        Assert.True(second.Execute().Succeeded);
        Assert.Single(second.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ResultSnapshots_AreReadOnlyAndRemainStableAfterLaterDamage()
    {
        var s = new Setup(true);
        var result = s.Execute();
        s.Target.TakeDamage(int.MaxValue);
        Assert.Equal(18, result.Strike!.ActualHPDamage);
        Assert.Equal(1, result.Finalization!.ReadyRound);
        Assert.All(typeof(ShadowStrikeUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void BadRandomProviderAfterOpening_DoesNotClaimRollbackOrFinalizeIncompleteCombat()
    {
        var s = new Setup(true, 0, 100);
        Assert.Throws<InvalidOperationException>(() => s.Execute());
        Assert.Equal(34, s.Target.CurrentHealth);
        Assert.True(s.Target.Statuses.HasStatus(StatusType.Disarm));
        Assert.True(s.Actor.SmokeReveal.RevealedByAttack);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    private sealed class Setup
    {
        public Character Actor { get; }
        public Character Target { get; } = CharacterFactory.Create("nigel", 2)!;
        public SmokePlacementService Smoke { get; } = new();
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public CombatExchangeRequest Combat { get; set; }
        public SmokeVisibilityResult? Snapshot { get; set; }
        public RandomSequence Random { get; }
        public ShadowStrikeAbilityExecutor Executor { get; }

        public Setup(bool invisible, int firstRoll = 0, int secondRoll = 0, int thirdRoll = 0,
            RoundLifecycleManager? lifecycle = null, Character? actor = null)
        {
            Actor = actor ?? CharacterFactory.Create("hooded_assassin", 1)!;
            Random = new(firstRoll, secondRoll, thirdRoll);
            Executor = new(new(new(new(new(Random))), Smoke));
            Activations = new(new[] { Actor }, new[] { Target }, 1, lifecycle);
            Activations.StartActivation(Actor);
            Use = new(Actor, "shadow_strike", Activations);
            Smoke.Place("cloud", Actor.Id, new(3, 3), 1, _ => true);
            Target.SetTacticalPosition(new(5, 3));
            Actor.SetTacticalPosition(invisible ? new(3, 3) : new(4, 3));
            Snapshot = CurrentVisibility();
            Actor.SetTacticalPosition(new(4, 3)); // Legal approach is supplied separately, not executed by this ability.
            Combat = new(Actor, Target, AttackType.Physical, 5, WeaponCategory.Sword, WeaponCategory.Axe, 100,
                AttackType.Physical, 0, 100, 1, 1, 1, true, false,
                DefenderUnarmedAttack: AttackConfiguration.CreateUnarmed(0, 100));
            // Zero unarmed Power and 100 accuracy are explicit test inputs, not finalized balance.
        }

        public SmokeVisibilityResult CurrentVisibility() => SmokeVisibility.Evaluate(Actor, Actor.TacticalPosition!.Value,
            Smoke, new[] { (Target, Target.TacticalPosition!.Value) });
        public ShadowStrikeUseResult Preview() => Executor.Preview(Use, Combat, Snapshot, 1, 1, TargetRules);
        public ShadowStrikeUseResult Execute() => Executor.Execute(Use, Combat, Snapshot, 1, 1, TargetRules);
        public void AssertUntouched(bool expectExistingCooldown = false)
        {
            Assert.Equal(0, Random.Calls);
            Assert.Equal(52, Target.CurrentHealth);
            Assert.Equal(0, Target.ShieldHP);
            Assert.False(Target.Statuses.HasStatus(StatusType.Disarm));
            Assert.False(Actor.SmokeReveal.RevealedByAttack);
            Assert.Equal(expectExistingCooldown ? 1 : 0, Actor.Cooldowns.Cooldowns.Count);
        }
    }

    private sealed class RandomSequence(params int[] rolls) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() => rolls[Calls++];
    }
}
