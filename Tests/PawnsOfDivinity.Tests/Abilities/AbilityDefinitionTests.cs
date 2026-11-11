using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class AbilityDefinitionTests
{
    [Fact]
    public void Definition_PreservesExplicitMetadata_WithoutRuntimeState()
    {
        var range = new AbilityRange(AbilityRangeKind.ManhattanDistance, 1, 4);
        var ability = new AbilityDefinition("example", "Example", "Metadata only.",
            AbilityClassification.Active, true, AbilityActionType.MainAction, range, 3);

        Assert.Equal("example", ability.Id);
        Assert.Equal("Example", ability.DisplayName);
        Assert.Equal("Metadata only.", ability.Description);
        Assert.Equal(AbilityClassification.Active, ability.Classification);
        Assert.True(ability.IsSpellTagged);
        Assert.Equal(AbilityActionType.MainAction, ability.RequiredAction);
        Assert.Same(range, ability.Range);
        Assert.Equal(3, ability.ReuseInterval);
        Assert.All(typeof(AbilityDefinition).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(AbilityRange).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void UnresolvedMetadata_RemainsNull_DistinctFromFalseNoneOrZero()
    {
        var unknown = Ability("unknown");
        Assert.Null(unknown.Classification);
        Assert.Null(unknown.IsSpellTagged);
        Assert.Null(unknown.RequiredAction);
        Assert.Null(unknown.Range);
        Assert.Null(unknown.ReuseInterval);

        var explicitValues = new AbilityDefinition("known", "Known", "", AbilityClassification.Passive,
            false, AbilityActionType.None, reuseInterval: 0);
        Assert.False(explicitValues.IsSpellTagged);
        Assert.Equal(AbilityActionType.None, explicitValues.RequiredAction);
        Assert.Equal(0, explicitValues.ReuseInterval);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidIdsAndNames_AreRejected(string? invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AbilityDefinition(invalid!, "Name", "Description"));
        Assert.ThrowsAny<ArgumentException>(() => new AbilityDefinition("id", invalid!, "Description"));
    }

    [Fact]
    public void NullDescription_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => new AbilityDefinition("id", "Name", null!));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void ValidReuseMetadata_IsPreserved(int? reuse)
        => Assert.Equal(reuse, new AbilityDefinition("id", "Name", "Description", reuseInterval: reuse).ReuseInterval);

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeReuseMetadata_IsRejected(int reuse)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition("id", "Name", "Description", reuseInterval: reuse));

    [Theory]
    [InlineData(AbilityRangeKind.ManhattanDistance, 1, 4)]
    [InlineData(AbilityRangeKind.ManhattanDistance, null, 3)]
    [InlineData(AbilityRangeKind.ManhattanDistance, 0, 0)]
    [InlineData(AbilityRangeKind.ManhattanRadius, null, 2)]
    [InlineData(AbilityRangeKind.ManhattanDistance, 1, null)]
    [InlineData(AbilityRangeKind.EquippedWeapon, null, null)]
    public void RangeMetadata_PreservesKindAndOptionalBounds(AbilityRangeKind kind, int? minimum, int? maximum)
    {
        var range = new AbilityRange(kind, minimum, maximum);

        Assert.Equal(kind, range.Kind);
        Assert.Equal(minimum, range.Minimum);
        Assert.Equal(maximum, range.Maximum);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, -1)]
    [InlineData(4, 3)]
    public void InvalidRangeBounds_AreRejected(int minimum, int maximum)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityRange(AbilityRangeKind.ManhattanDistance, minimum, maximum));

    [Fact]
    public void EquippedRange_CannotPretendToHaveFixedNumericBounds()
        => Assert.Throws<ArgumentException>(() => new AbilityRange(AbilityRangeKind.EquippedWeapon, 1, 2));

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void InvalidMetadataEnums_AreRejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition("id", "Name", "", classification: (AbilityClassification)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition("id", "Name", "", requiredAction: (AbilityActionType)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityRange((AbilityRangeKind)value));
    }

    [Fact]
    public void CharacterDefinition_SnapshotsMultipleAbilities_AndAllowsSharingImmutableMetadata()
    {
        var active = Ability("active");
        var passive = Ability("passive");
        var input = new List<AbilityDefinition> { active, passive };
        var first = Definition("first", input);
        var second = Definition("second", new[] { active });
        input.Clear();

        Assert.Equal(new[] { "active", "passive" }, first.Abilities.Select(ability => ability.Id));
        Assert.Same(active, first.Abilities[0]);
        Assert.Same(active, second.Abilities[0]);
        var list = Assert.IsAssignableFrom<IList<AbilityDefinition>>(first.Abilities);
        Assert.Throws<NotSupportedException>(() => list.Add(Ability("extra")));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Throws<NotSupportedException>(() => list[0] = passive);
    }

    [Fact]
    public void DuplicateAbilityIds_AreRejectedEvenForDifferentDefinitionsWithSameId()
    {
        var ability = Ability("same_id");
        Assert.Throws<ArgumentException>(() => Definition("test", new[] { ability, ability }));
        Assert.Throws<ArgumentException>(() => Definition("test", new[] { ability, Ability("same_id") }));
    }

    [Fact]
    public void NullAbilityCollectionAndMembers_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Definition("test", null!));
        Assert.Throws<ArgumentException>(() => Definition("test", new[] { Ability("valid"), null! }));
    }

    [Fact]
    public void ExistingConstructorWithoutAbilities_AndRegisteredDefinitions_RemainFactoryCompatible()
    {
        var legacy = new CharacterDefinition("legacy", "Legacy", "", Stats());
        Assert.Empty(legacy.Abilities);
        var roster = CharacterRegistry.GetAllCharacters().ToArray();
        Assert.Equal(6, roster.Length);
        foreach (var definition in roster)
        {
            Assert.Equal(3, definition.Abilities.Count);
            var character = CharacterFactory.Create(definition.Id, 1)!;
            Assert.Same(definition.BaseStats, character.BaseStats);
            Assert.Equal(definition.BaseStats.MaxHealth, character.CurrentHealth);
        }
    }

    [Fact]
    public void SharedAbilityMetadata_DoesNotShareCooldowns_OrConsumeAnythingThroughTheFactory()
    {
        var ability = new AbilityDefinition("example", "Example", "Metadata only.", reuseInterval: 3);
        var definition = Definition("test", new[] { ability });
        var first = CharacterFactory.Create(definition, "first", 1);
        var second = CharacterFactory.Create(definition, "second", 2);
        Assert.Empty(first.Cooldowns.Cooldowns);
        Assert.Empty(second.Cooldowns.Cooldowns);
        first.Cooldowns.RecordUse(ability.Id, 2, ability.ReuseInterval!.Value);

        Assert.False(first.Cooldowns.IsReady(ability.Id, 3));
        Assert.True(second.Cooldowns.IsReady(ability.Id, 3));
        Assert.Equal(3, ability.ReuseInterval);
        Assert.Same(ability, definition.Abilities[0]);
        Assert.Equal(100, first.CurrentHealth);
        Assert.Equal(0, first.ShieldHP);
        Assert.False(first.HasActedThisRound);
        Assert.Empty(first.Statuses.TimedStatuses);
        Assert.False(first.Statuses.IsPoisoned);
        Assert.Same(definition.BaseStats, first.BaseStats);
    }

    private static AbilityDefinition Ability(string id) => new(id, id, "Metadata only.");
    private static CharacterStats Stats() => new(100, 20, 10, 12, 8, 4, 10, 5);
    private static CharacterDefinition Definition(string id, IEnumerable<AbilityDefinition> abilities)
        => new(id, id, "Test template.", Stats(), abilities);
}
