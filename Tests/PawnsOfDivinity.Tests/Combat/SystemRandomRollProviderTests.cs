using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class SystemRandomRollProviderTests
{
    [Fact]
    public void NextRoll_UsesExclusiveUpperBound100_AndReusesSuppliedGenerator()
    {
        var random = new RecordingRandom();
        var provider = new SystemRandomRollProvider(random);

        Assert.Equal(0, provider.NextRoll());
        Assert.Equal(99, provider.NextRoll());
        Assert.Equal(new[] { 100, 100 }, random.UpperBounds);
    }

    [Fact]
    public void Constructor_NullGenerator_IsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new SystemRandomRollProvider(null!));

        Assert.Equal("random", error.ParamName);
    }

    private sealed class RecordingRandom : Random
    {
        public List<int> UpperBounds { get; } = new();

        public override int Next(int maxValue)
        {
            UpperBounds.Add(maxValue);
            return UpperBounds.Count == 1 ? 0 : 99;
        }
    }
}
