using PawnsOfDivinity.Core.Characters;
using Xunit;

namespace PawnsOfDivinity.Tests.Characters;

public class CharacterStatsTests
{
    [Fact]
    public void Constructor_ValidStats_PreservesAllValues()
    {
        var stats = new CharacterStats(
            MaxHealth: 40, Strength: 12, Defense: 9, Intelligence: 8,
            Resistance: 7, Wisdom: 6, Speed: 5, Movement: 4);

        Assert.Equal(40, stats.MaxHealth);
        Assert.Equal(12, stats.Strength);
        Assert.Equal(9, stats.Defense);
        Assert.Equal(8, stats.Intelligence);
        Assert.Equal(7, stats.Resistance);
        Assert.Equal(6, stats.Wisdom);
        Assert.Equal(5, stats.Speed);
        Assert.Equal(4, stats.Movement);
    }

    [Fact]
    public void Constructor_MinimumValues_AcceptsOneHealthAndZeroOtherStats()
    {
        var stats = new CharacterStats(1, 0, 0, 0, 0, 0, 0, 0);

        Assert.Equal(1, stats.MaxHealth);
        Assert.Equal(0, stats.Strength);
        Assert.Equal(0, stats.Defense);
        Assert.Equal(0, stats.Intelligence);
        Assert.Equal(0, stats.Resistance);
        Assert.Equal(0, stats.Wisdom);
        Assert.Equal(0, stats.Speed);
        Assert.Equal(0, stats.Movement);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_NonpositiveMaxHealth_Throws(int maxHealth)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CharacterStats(maxHealth, 0, 0, 0, 0, 0, 0, 0));

        Assert.Equal("MaxHealth", error.ParamName);
    }

    public static IEnumerable<object[]> NegativeAttributeCases()
    {
        string[] attributes = { "Strength", "Defense", "Intelligence", "Resistance", "Wisdom", "Speed", "Movement" };
        for (int index = 0; index < attributes.Length; index++)
            foreach (int value in new[] { -1, int.MinValue })
                yield return new object[] { index + 1, attributes[index], value };
    }

    [Theory]
    [MemberData(nameof(NegativeAttributeCases))]
    public void Constructor_NegativeAttribute_ThrowsForThatParameter(int index, string parameter, int value)
    {
        int[] values = { 1, 0, 0, 0, 0, 0, 0, 0 };
        values[index] = value;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => FromValues(values));

        Assert.Equal(parameter, error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void Constructor_NonnegativeMovement_PreservesValue(int movement)
    {
        var stats = new CharacterStats(1, 0, 0, 0, 0, 0, 0, movement);

        Assert.Equal(movement, stats.Movement);
    }

    [Fact]
    public void Equality_IdenticalValues_RecordsAreEqual()
    {
        var first = new CharacterStats(40, 12, 9, 8, 7, 6, 5, 4);
        var second = new CharacterStats(40, 12, 9, 8, 7, 6, 5, 4);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Equality_AnyDifferentAttribute_RecordsAreNotEqual(int index)
    {
        int[] values = { 40, 12, 9, 8, 7, 6, 5, 4 };
        var original = FromValues(values);
        values[index]++;

        Assert.NotEqual(original, FromValues(values));
    }

    [Fact]
    public void Properties_AreGetOnlyIntegers_WithoutMutationThroughInitializers()
    {
        var properties = typeof(CharacterStats).GetProperties();

        Assert.Equal(8, properties.Length);
        Assert.All(properties, property =>
        {
            Assert.Equal(typeof(int), property.PropertyType);
            Assert.True(property.CanRead);
            // Even an init setter would allow bypassing constructor validation.
            Assert.Null(property.SetMethod);
        });
    }

    private static CharacterStats FromValues(int[] values) => new(
        values[0], values[1], values[2], values[3],
        values[4], values[5], values[6], values[7]);
}
