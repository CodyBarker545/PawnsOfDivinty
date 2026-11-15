using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class CharacterAnimationStateTests
{
    private static CharacterAnimationStateMachine Full() => new(Enum.GetValues<CharacterAnimationState>());
    [Fact]
    public void LocomotionChangesOnlyWhenNeeded()
    {
        var state=Full(); Assert.True(state.Reset()); Assert.Equal(CharacterAnimationState.Idle,state.Current);
        Assert.False(state.SetMoving(false)); Assert.True(state.SetMoving(true));
        Assert.Equal(CharacterAnimationState.Walk,state.Current); Assert.False(state.SetMoving(true));
        Assert.True(state.SetMoving(false)); Assert.Equal(CharacterAnimationState.Idle,state.Current);
    }
    [Fact]
    public void OneShotsQueueAndReturnToLatestLocomotion()
    {
        var state=Full(); state.Reset(); state.Request(CharacterAnimationState.Attack);
        Assert.False(state.Request(CharacterAnimationState.HitReaction));
        Assert.False(state.SetMoving(true)); Assert.False(state.Complete(CharacterAnimationState.HitReaction));
        Assert.True(state.Complete(CharacterAnimationState.Attack)); Assert.Equal(CharacterAnimationState.HitReaction,state.Current);
        Assert.True(state.Complete(CharacterAnimationState.HitReaction)); Assert.Equal(CharacterAnimationState.Walk,state.Current);
    }
    [Fact]
    public void RepeatedAttacksPlayAsSeparateActions()
    {
        var state=Full(); state.Reset(); state.Request(CharacterAnimationState.Attack); state.Request(CharacterAnimationState.Attack);
        Assert.True(state.Complete(CharacterAnimationState.Attack)); Assert.Equal(CharacterAnimationState.Attack,state.Current);
        state.Complete(CharacterAnimationState.Attack); Assert.Equal(CharacterAnimationState.Idle,state.Current);
    }
    [Fact]
    public void MissingOptionalClipsFallBackWithoutGettingStuck()
    {
        var state=new CharacterAnimationStateMachine(new[]{CharacterAnimationState.Idle}); state.Reset();
        Assert.False(state.SetMoving(true)); Assert.False(state.Request(CharacterAnimationState.Attack));
        Assert.Equal(CharacterAnimationState.Idle,state.Current);
        Assert.False(state.Request(CharacterAnimationState.Death)); Assert.False(state.SetMoving(false));
        Assert.True(state.Reset());
        var empty=new CharacterAnimationStateMachine(Array.Empty<CharacterAnimationState>());
        Assert.False(empty.Reset()); Assert.Null(empty.Current);
    }
    [Fact]
    public void DeathInterruptsActionsClearsQueueAndHoldsUntilReset()
    {
        var state=Full(); state.Reset(); state.Request(CharacterAnimationState.Attack); state.Request(CharacterAnimationState.HitReaction);
        Assert.True(state.Request(CharacterAnimationState.Death)); Assert.False(state.Request(CharacterAnimationState.Attack));
        Assert.False(state.Complete(CharacterAnimationState.Death)); Assert.False(state.Complete(CharacterAnimationState.Attack));
        Assert.False(state.SetMoving(true)); Assert.Equal(CharacterAnimationState.Death,state.Current);
        state.Reset(); Assert.Equal(CharacterAnimationState.Idle,state.Current);
    }
}
