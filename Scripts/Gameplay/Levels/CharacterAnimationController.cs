using Godot;
using System;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Plays validated, per-rig skeletal clips. Owns no positions, stats, or combat rules.</summary>
public partial class CharacterAnimationController : Node
{
    private AnimationPlayer? _player;
    private CharacterAnimationStateMachine? _states;
    public CharacterAnimationState? State => _states?.Current;
    public bool Initialize(Node3D model)
    {
        var skeleton = FindSkeleton(model);
        var modelName = System.IO.Path.GetFileNameWithoutExtension(model.SceneFilePath);
        var path = $"res://Assets/Animations/Mixamo/{modelName}.tres";
        if (skeleton is null || !ResourceLoader.Exists(path))
        { GD.PushWarning($"Animation unavailable for {model.SceneFilePath}: skeleton/library missing."); return false; }
        var library = GD.Load<AnimationLibrary>(path);
        if (library is null) { GD.PushWarning($"Animation library could not load: {path}"); return false; }
        // Validate every binding before enabling any animation on this skeleton.
        foreach (var name in library.GetAnimationList())
        {
            var clip = library.GetAnimation(name);
            for (int i = 0; i < clip.GetTrackCount(); i++)
            {
                var track = clip.TrackGetPath(i);
                if (clip.TrackGetKeyCount(i)==0 || track.GetConcatenatedNames() != "." || track.GetSubNameCount() != 1 || skeleton.FindBone(track.GetSubName(0)) < 0)
                { GD.PushWarning($"{modelName}: incompatible animation track {track}"); return false; }
            }
        }
        // Imported animation systems were stopped by the fixture. Reuse a player when present;
        // these six static exports need one runtime player rooted directly at their own skeleton.
        _player = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (_player is null) { _player = new AnimationPlayer { Name = "CharacterAnimationPlayer" }; skeleton.AddChild(_player); }
        _player.RootNode = _player.GetPathTo(skeleton);
        foreach (var old in _player.GetAnimationLibraryList()) _player.RemoveAnimationLibrary(old);
        _player.AddAnimationLibrary("", library);
        _player.ProcessMode = ProcessModeEnum.Always; _player.Active = true;
        skeleton.ProcessMode = ProcessModeEnum.Always;
        _states = new(Enum.GetValues<CharacterAnimationState>().Where(s => _player.HasAnimation(s.ToString())));
        _player.AnimationFinished += Finished;
        Reset();
        return true;
    }
    public void SetMoving(bool moving) { if (_states?.SetMoving(moving) == true) Play(); }
    public void Request(CharacterAnimationState state) { if (_states?.Request(state) == true) Play(); }
    public void Reset()
    {
        _player?.Stop();
        if (_states?.Reset() == true) Play();
    }
    private void Finished(StringName name)
    {
        if (Enum.TryParse<CharacterAnimationState>(name.ToString(), out var state) && _states?.Complete(state) == true) Play();
    }
    private void Play()
    {
        if (_states?.Current is { } state) _player!.Play(state.ToString(), customBlend: .12);
    }
    private static Skeleton3D? FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton) return skeleton;
        foreach (var child in node.GetChildren()) if (FindSkeleton(child) is { } found) return found;
        return null;
    }
}
