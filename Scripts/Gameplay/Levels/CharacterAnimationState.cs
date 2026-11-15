using System.Collections.Generic;

namespace PawnsOfDivinity.Gameplay.Levels;

public enum CharacterAnimationState { Idle, Walk, Attack, HitReaction, Death }

/// <summary>Pure presentation policy. Death is terminal until reset; actions queue without overlap.</summary>
public sealed class CharacterAnimationStateMachine
{
    private readonly HashSet<CharacterAnimationState> _available;
    private readonly Queue<CharacterAnimationState> _pending = new();
    private bool _moving, _dead;
    public CharacterAnimationState? Current { get; private set; }
    public CharacterAnimationStateMachine(IEnumerable<CharacterAnimationState> available) => _available = new(available);
    public bool SetMoving(bool moving)
    {
        _moving = moving;
        if (_dead || Current is CharacterAnimationState.Attack or CharacterAnimationState.HitReaction) return false;
        return SelectLocomotion();
    }
    public bool Request(CharacterAnimationState state)
    {
        if (_dead) return false;
        if (state == CharacterAnimationState.Death)
        {
            _dead = true; _pending.Clear();
            return Select(_available.Contains(state) ? state : CharacterAnimationState.Idle);
        }
        if (state is not (CharacterAnimationState.Attack or CharacterAnimationState.HitReaction) || !_available.Contains(state)) return false;
        if (Current is CharacterAnimationState.Attack or CharacterAnimationState.HitReaction)
        { _pending.Enqueue(state); return false; }
        return Select(state);
    }
    public bool Complete(CharacterAnimationState finished)
    {
        if (_dead || Current != finished || finished is not (CharacterAnimationState.Attack or CharacterAnimationState.HitReaction)) return false;
        if (_pending.TryDequeue(out var next)) { Current = null; return Select(next); }
        return SelectLocomotion();
    }
    public bool Reset()
    { _pending.Clear(); _dead = false; _moving = false; Current = null; return SelectLocomotion(); }
    private bool SelectLocomotion() => Select(_moving && _available.Contains(CharacterAnimationState.Walk)
        ? CharacterAnimationState.Walk : CharacterAnimationState.Idle);
    private bool Select(CharacterAnimationState state)
    {
        if (!_available.Contains(state) || Current == state) return false;
        Current = state; return true;
    }
}
