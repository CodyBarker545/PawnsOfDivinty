using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class FollowUpEligibilityTests
{
    [Theory]
    [InlineData(5, 4, FollowUpParticipant.Attacker)]
    [InlineData(4, 5, FollowUpParticipant.Defender)]
    [InlineData(24, 20, FollowUpParticipant.None)]
    [InlineData(20, 24, FollowUpParticipant.None)]
    [InlineData(25, 20, FollowUpParticipant.Attacker)]
    [InlineData(20, 25, FollowUpParticipant.Defender)]
    [InlineData(7, 4, FollowUpParticipant.Attacker)]
    [InlineData(4, 7, FollowUpParticipant.Defender)]
    [InlineData(8, 4, FollowUpParticipant.Attacker)]
    [InlineData(4, 8, FollowUpParticipant.Defender)]
    [InlineData(10, 10, FollowUpParticipant.None)]
    [InlineData(0, 0, FollowUpParticipant.None)]
    [InlineData(1, 0, FollowUpParticipant.Attacker)]
    [InlineData(0, 1, FollowUpParticipant.Defender)]
    [InlineData(int.MaxValue, int.MaxValue, FollowUpParticipant.None)]
    [InlineData(int.MaxValue, 1717986917, FollowUpParticipant.Attacker)]
    [InlineData(int.MaxValue, 1717986918, FollowUpParticipant.None)]
    [InlineData(1717986917, int.MaxValue, FollowUpParticipant.Defender)]
    [InlineData(int.MaxValue, 0, FollowUpParticipant.Attacker)]
    public void Evaluate_UsesExactIntegerThresholdAndReturnsOneParticipant(
        int attackerSpeed, int defenderSpeed, FollowUpParticipant expected)
    {
        Assert.Equal(expected, FollowUpEligibility.Evaluate(attackerSpeed, defenderSpeed));
    }

    [Theory]
    [InlineData(-1, 1, "attackerSpeed")]
    [InlineData(int.MinValue, 0, "attackerSpeed")]
    [InlineData(1, -1, "defenderSpeed")]
    [InlineData(0, int.MinValue, "defenderSpeed")]
    public void Evaluate_NegativeSpeed_Throws(int attackerSpeed, int defenderSpeed, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => FollowUpEligibility.Evaluate(attackerSpeed, defenderSpeed));

        Assert.Equal(parameter, error.ParamName);
    }
}
