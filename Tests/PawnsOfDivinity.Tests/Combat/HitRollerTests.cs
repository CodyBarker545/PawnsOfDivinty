using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class HitRollerTests
{
    [Theory]
    [InlineData(75, 74, true)]
    [InlineData(75, 75, false)]
    [InlineData(75, 0, true)]
    [InlineData(75, 99, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 99, false)]
    [InlineData(100, 0, true)]
    [InlineData(100, 99, true)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(99, 98, true)]
    [InlineData(99, 99, false)]
    public void Roll_UsesStrictThreshold_AndConsumesExactlyOneRoll(int chance, int roll, bool expected)
    {
        var provider = new SequenceRollProvider(roll);
        var roller = new HitRoller(provider);

        Assert.Equal(expected, roller.Roll(chance));
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Roll_InvalidChance_ThrowsWithoutConsumingRandomness(int chance)
    {
        var provider = new SequenceRollProvider(0);
        var roller = new HitRoller(provider);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => roller.Roll(chance));

        Assert.Equal("hitChance", error.ParamName);
        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-1, 50)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, 50)]
    [InlineData(100, 100)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MinValue, 50)]
    [InlineData(int.MinValue, 100)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MaxValue, 50)]
    [InlineData(int.MaxValue, 100)]
    public void Roll_InvalidProviderValue_ThrowsWithoutRerolling(int roll, int chance)
    {
        var provider = new SequenceRollProvider(roll);
        var roller = new HitRoller(provider);

        Assert.Throws<InvalidOperationException>(() => roller.Roll(chance));
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Constructor_NullProvider_IsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new HitRoller(null!));

        Assert.Equal("randomProvider", error.ParamName);
    }

    [Fact]
    public void Roll_SeparateStrikes_ConsumeSeparateSequenceEntries()
    {
        var provider = new SequenceRollProvider(74, 75, 0, 99);
        var roller = new HitRoller(provider);
        bool[] expected = { true, false, true, false };

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], roller.Roll(75));
            Assert.Equal(i + 1, provider.CallCount);
        }
    }

    [Fact]
    public void Roll_ReplayingFakeSequence_ProducesIdenticalResults()
    {
        var firstProvider = new SequenceRollProvider(0, 49, 50, 99);
        var secondProvider = new SequenceRollProvider(0, 49, 50, 99);
        var first = new HitRoller(firstProvider);
        var second = new HitRoller(secondProvider);

        bool[] firstResults = Enumerable.Range(0, 4).Select(_ => first.Roll(50)).ToArray();
        bool[] secondResults = Enumerable.Range(0, 4).Select(_ => second.Roll(50)).ToArray();

        Assert.Equal(new[] { true, true, false, false }, firstResults);
        Assert.Equal(firstResults, secondResults);
        Assert.Equal(4, firstProvider.CallCount);
        Assert.Equal(4, secondProvider.CallCount);
    }

    private sealed class SequenceRollProvider : IRandomRollProvider
    {
        private readonly Queue<int> _rolls;
        public int CallCount { get; private set; }

        public SequenceRollProvider(params int[] rolls) => _rolls = new Queue<int>(rolls);

        public int NextRoll()
        {
            CallCount++;
            // The queue throws if the roller asks for more values than the test supplied.
            return _rolls.Dequeue();
        }
    }
}
